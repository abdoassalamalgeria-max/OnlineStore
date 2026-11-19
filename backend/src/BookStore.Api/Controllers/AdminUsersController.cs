using BookStore.Api.Extensions;
using BookStore.Application.Common;
using BookStore.Application.Features.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookStore.Api.Controllers;

[ApiController]
[Route("api/v1/admin/users")]
[Authorize(Policy = Policies.AdminOnly)]
[Produces("application/json")]
public sealed class AdminUsersController(IAdminUserService users) : ControllerBase
{
    /// <summary>Registered users, newest first. Search matches name, email or phone; filter by suspension.</summary>
    [HttpGet]
    public async Task<PagedResult<AdminUserListItemDto>> List(
        CancellationToken cancellationToken,
        [FromQuery] string? search = null,
        [FromQuery] bool? suspended = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) =>
        await users.ListAsync(new AdminUserQuery(search, suspended, page, pageSize), cancellationToken);

    /// <summary>A user's details with order count, amount spent and latest orders.</summary>
    [HttpGet("{id:guid}")]
    public async Task<AdminUserDto> Get(Guid id, CancellationToken cancellationToken) =>
        await users.GetAsync(id, cancellationToken);

    /// <summary>Correct name, email (also the sign-in name) and phone. 400 if the email belongs to another account.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<AdminUserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<AdminUserDto> Update(Guid id, AdminUpdateUserRequest request, CancellationToken cancellationToken) =>
        await users.UpdateAsync(id, request, cancellationToken);

    /// <summary>Blocks sign-in and ends the user's sessions. 409 for admin accounts.</summary>
    [HttpPost("{id:guid}/suspend")]
    [ProducesResponseType<AdminUserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<AdminUserDto> Suspend(Guid id, CancellationToken cancellationToken) =>
        await users.SetSuspendedAsync(id, suspended: true, cancellationToken);

    /// <summary>Lets the user sign in again (also clears a failed-sign-in lockout).</summary>
    [HttpPost("{id:guid}/reactivate")]
    public async Task<AdminUserDto> Reactivate(Guid id, CancellationToken cancellationToken) =>
        await users.SetSuspendedAsync(id, suspended: false, cancellationToken);
}
