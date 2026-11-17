using BookStore.Api.Extensions;
using BookStore.Application.Features.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookStore.Api.Controllers;

[ApiController]
[Route("api/v1/admin")]
[Authorize(Policy = Policies.AdminOnly)]
[Produces("application/json")]
public sealed class AdminDashboardController(IAdminOverviewService overview) : ControllerBase
{
    /// <summary>Catalog totals and stock warnings for the dashboard (Admin role only).</summary>
    [HttpGet("overview")]
    [ProducesResponseType<AdminOverviewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<AdminOverviewDto> Overview(CancellationToken cancellationToken) =>
        await overview.GetAsync(cancellationToken);
}
