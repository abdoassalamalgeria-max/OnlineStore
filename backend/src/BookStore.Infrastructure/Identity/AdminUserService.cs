using BookStore.Application.Common;
using BookStore.Application.Features.Admin;
using BookStore.Application.Features.Auth;
using BookStore.Application.Features.Ordering;
using BookStore.Domain.Ordering;
using BookStore.Infrastructure.Persistence;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Infrastructure.Identity;

/// <summary>
/// Back-office user management. Suspension uses Identity's lockout with an end date far in the future, so the
/// existing sign-in and refresh checks (<c>IsLockedOutAsync</c>) enforce it without extra code paths.
/// </summary>
internal sealed class AdminUserService(
    AppDbContext db,
    UserManager<ApplicationUser> users,
    IOrderQueryService orders,
    TimeProvider clock,
    IValidator<AdminUserQuery> queryValidator,
    IValidator<AdminUpdateUserRequest> updateValidator) : IAdminUserService
{
    /// <summary>Lockout end used for "suspended"; anything past <see cref="SuspendedThreshold"/> counts as suspended.</summary>
    internal static readonly DateTimeOffset SuspendedUntil = new(9000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset SuspendedThreshold = new(3000, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly OrderStatus[] Paid = [OrderStatus.Paid, OrderStatus.Shipped, OrderStatus.Delivered];

    public async Task<PagedResult<AdminUserListItemDto>> ListAsync(AdminUserQuery query, CancellationToken cancellationToken)
    {
        await queryValidator.ValidateAndThrowAsync(query, cancellationToken);

        var filtered = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = LikePattern.Contains(query.Search.Trim());
            filtered = filtered.Where(u =>
                EF.Functions.ILike(u.FullName, pattern, "\\")
                || EF.Functions.ILike(u.Email!, pattern, "\\")
                || (u.PhoneNumber != null && EF.Functions.ILike(u.PhoneNumber, pattern, "\\")));
        }

        if (query.Suspended is { } suspended)
        {
            filtered = suspended
                ? filtered.Where(u => u.LockoutEnd >= SuspendedThreshold)
                : filtered.Where(u => u.LockoutEnd == null || u.LockoutEnd < SuspendedThreshold);
        }

        var total = await filtered.CountAsync(cancellationToken);
        var page = await filtered
            .OrderByDescending(u => u.CreatedAt)
            .ThenBy(u => u.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(u => new
            {
                u.Id,
                u.FullName,
                u.Email,
                u.PhoneNumber,
                u.CreatedAt,
                u.LockoutEnd,
                OrderCount = db.Orders.Count(o => o.UserId == u.Id),
            })
            .ToListAsync(cancellationToken);

        var roles = await RolesAsync(page.Select(u => u.Id).ToList(), cancellationToken);
        var items = page.Select(u => new AdminUserListItemDto(
            u.Id,
            u.FullName,
            u.Email ?? string.Empty,
            u.PhoneNumber,
            u.CreatedAt,
            roles.GetValueOrDefault(u.Id) ?? [],
            u.LockoutEnd >= SuspendedThreshold,
            u.OrderCount)).ToList();

        return new PagedResult<AdminUserListItemDto>(items, query.Page, query.PageSize, total);
    }

    public async Task<AdminUserDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
            ?? throw new NotFoundException("User", id);

        var stats = await db.Orders
            .Where(o => o.UserId == id)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Count = g.Count(),
                Spent = g.Where(o => Paid.Contains(o.Status)).Sum(o => (int?)o.Total) ?? 0,
            })
            .FirstOrDefaultAsync(cancellationToken);

        var recent = await orders.ListForUserAsync(id, page: 1, pageSize: 10, cancellationToken);
        var roles = await RolesAsync([id], cancellationToken);
        var now = clock.GetUtcNow();
        var suspended = user.LockoutEnd >= SuspendedThreshold;

        return new AdminUserDto(
            user.Id,
            user.FullName,
            user.Email ?? string.Empty,
            user.PhoneNumber,
            user.CreatedAt,
            roles.GetValueOrDefault(id) ?? [],
            suspended,
            !suspended && user.LockoutEnd > now ? user.LockoutEnd : null,
            stats?.Count ?? 0,
            stats?.Spent ?? 0,
            recent.Items);
    }

    public async Task<AdminUserDto> UpdateAsync(Guid id, AdminUpdateUserRequest request, CancellationToken cancellationToken)
    {
        await updateValidator.ValidateAndThrowAsync(request, cancellationToken);
        var user = await FindAsync(id);
        var email = request.Email.Trim();

        if (!string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            var owner = await users.FindByEmailAsync(email);
            if (owner is not null && owner.Id != id)
            {
                throw new ValidationException([new ValidationFailure(nameof(AdminUpdateUserRequest.Email), "This email is already used by another account.")]);
            }
        }

        // The email doubles as the sign-in name; both are normalized by Identity on update.
        user.FullName = request.FullName.Trim();
        user.Email = email;
        user.UserName = email;
        user.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();

        var result = await users.UpdateAsync(user);
        if (!result.Succeeded)
        {
            throw new ValidationException(result.Errors.Select(e => new ValidationFailure(
                e.Code.Contains("Email", StringComparison.Ordinal) || e.Code.Contains("UserName", StringComparison.Ordinal)
                    ? nameof(AdminUpdateUserRequest.Email)
                    : nameof(AdminUpdateUserRequest.FullName),
                e.Description)));
        }

        return await GetAsync(id, cancellationToken);
    }

    public async Task<AdminUserDto> SetSuspendedAsync(Guid id, bool suspended, CancellationToken cancellationToken)
    {
        var user = await FindAsync(id);

        if (suspended)
        {
            if (await users.IsInRoleAsync(user, Roles.Admin))
            {
                throw new ConflictException("Admin accounts can't be suspended.");
            }

            await users.SetLockoutEnabledAsync(user, true);
            await users.SetLockoutEndDateAsync(user, SuspendedUntil);

            // End every session now; sign-in and refresh are refused while locked out.
            var now = clock.GetUtcNow();
            await db.RefreshTokens
                .Where(t => t.UserId == id && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), cancellationToken);
        }
        else
        {
            await users.SetLockoutEndDateAsync(user, null);
            await users.ResetAccessFailedCountAsync(user);
        }

        return await GetAsync(id, cancellationToken);
    }

    private async Task<Dictionary<Guid, IReadOnlyList<string>>> RolesAsync(List<Guid> userIds, CancellationToken cancellationToken)
    {
        var pairs = await db.UserRoles
            .Where(ur => userIds.Contains(ur.UserId))
            .Join(db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, r.Name })
            .ToListAsync(cancellationToken);

        return pairs
            .GroupBy(p => p.UserId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(p => p.Name ?? string.Empty).Order().ToList());
    }

    private async Task<ApplicationUser> FindAsync(Guid id) =>
        await users.FindByIdAsync(id.ToString()) ?? throw new NotFoundException("User", id);
}
