using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Platera.Api.Extensions;
using Platera.Application.Admin;
using Platera.Application.Common;
using Platera.Application.Interactions;
using Platera.Application.Lookups;

namespace Platera.Api.Controllers;

[ApiController]
[Route("api")]
public class InteractionsController : ControllerBase
{
    private readonly IInteractionService _svc;
    public InteractionsController(IInteractionService svc) { _svc = svc; }

    [Authorize, HttpPost("recipes/{id:guid}/like")]
    [EnableRateLimiting(ServiceExtensions.FeedbackPolicy)]
    public async Task<ActionResult<LikeStateDto>> ToggleLike(Guid id, CancellationToken ct) => Ok(await _svc.ToggleLikeAsync(id, ct));

    [Authorize, HttpPut("recipes/{id:guid}/rating")]
    [EnableRateLimiting(ServiceExtensions.FeedbackPolicy)]
    public async Task<ActionResult<RatingStateDto>> Rate(Guid id, RateRequest request, CancellationToken ct) => Ok(await _svc.RateAsync(id, request, ct));

    [HttpGet("recipes/{id:guid}/comments")]
    public async Task<ActionResult<PagedResult<CommentDto>>> Comments(Guid id, [FromQuery] PageQuery query, CancellationToken ct) =>
        Ok(await _svc.CommentsAsync(id, query, ct));

    [Authorize, HttpPost("recipes/{id:guid}/comments")]
    [EnableRateLimiting(ServiceExtensions.FeedbackPolicy)]
    public async Task<ActionResult<CommentDto>> AddComment(Guid id, AddCommentRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await _svc.AddCommentAsync(id, request, ct));

    [Authorize, HttpDelete("comments/{commentId:guid}")]
    public async Task<IActionResult> DeleteComment(Guid commentId, CancellationToken ct)
    {
        await _svc.DeleteCommentAsync(commentId, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/admin")]
[Authorize(Policy = "Admin")]
public class AdminController : ControllerBase
{
    private readonly IAdminService _admin;
    private readonly ILookupService _lookups;
    private readonly Platera.Application.Interactions.IInteractionService _interactions;
    public AdminController(IAdminService admin, ILookupService lookups, IInteractionService interactions)
    { _admin = admin; _lookups = lookups; _interactions = interactions; }

    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardDto>> Dashboard(CancellationToken ct) => Ok(await _admin.DashboardAsync(ct));

    [HttpGet("users")]
    public async Task<ActionResult<PagedResult<AdminUserDto>>> Users([FromQuery] string? search, [FromQuery] PageQuery query, CancellationToken ct) =>
        Ok(await _admin.UsersAsync(search, query, ct));

    [HttpPut("users/{id:guid}/active")]
    public async Task<IActionResult> SetActive(Guid id, SetActiveRequest request, CancellationToken ct) { await _admin.SetActiveAsync(id, request, ct); return NoContent(); }

    [HttpPut("users/{id:guid}/role")]
    public async Task<IActionResult> SetRole(Guid id, SetRoleRequest request, CancellationToken ct) { await _admin.SetRoleAsync(id, request, ct); return NoContent(); }

    [HttpPut("recipes/{id:guid}/featured")]
    public async Task<IActionResult> SetFeatured(Guid id, SetFeaturedRequest request, CancellationToken ct) { await _admin.SetFeaturedAsync(id, request, ct); return NoContent(); }

    [HttpGet("comments")]
    public async Task<ActionResult<PagedResult<AdminCommentDto>>> Comments([FromQuery] bool hiddenOnly, [FromQuery] PageQuery query, CancellationToken ct) =>
        Ok(await _admin.CommentsAsync(hiddenOnly, query, ct));

    [HttpPut("comments/{id:guid}/hidden")]
    public async Task<IActionResult> SetHidden(Guid id, SetHiddenRequest request, CancellationToken ct) { await _admin.SetCommentHiddenAsync(id, request, ct); return NoContent(); }

    [HttpDelete("comments/{id:guid}")]
    public async Task<IActionResult> DeleteComment(Guid id, CancellationToken ct) { await _interactions.DeleteCommentAsync(id, ct); return NoContent(); }

    // Categories & cuisines management
    [HttpPost("categories")]
    public async Task<ActionResult<LookupDto>> CreateCategory(SaveLookupRequest r, CancellationToken ct) => Ok(await _lookups.SaveCategoryAsync(null, r, ct));
    [HttpPut("categories/{id:int}")]
    public async Task<ActionResult<LookupDto>> UpdateCategory(int id, SaveLookupRequest r, CancellationToken ct) => Ok(await _lookups.SaveCategoryAsync(id, r, ct));
    [HttpDelete("categories/{id:int}")]
    public async Task<IActionResult> DeleteCategory(int id, CancellationToken ct) { await _lookups.DeleteCategoryAsync(id, ct); return NoContent(); }

    [HttpPost("cuisines")]
    public async Task<ActionResult<LookupDto>> CreateCuisine(SaveLookupRequest r, CancellationToken ct) => Ok(await _lookups.SaveCuisineAsync(null, r, ct));
    [HttpPut("cuisines/{id:int}")]
    public async Task<ActionResult<LookupDto>> UpdateCuisine(int id, SaveLookupRequest r, CancellationToken ct) => Ok(await _lookups.SaveCuisineAsync(id, r, ct));
    [HttpDelete("cuisines/{id:int}")]
    public async Task<IActionResult> DeleteCuisine(int id, CancellationToken ct) { await _lookups.DeleteCuisineAsync(id, ct); return NoContent(); }
}
