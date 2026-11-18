using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Platera.Application.Abstractions;
using Platera.Application.Common;
using Platera.Application.Images;
using Platera.Domain.Entities;

namespace Platera.Application.Interactions;

public record LikeStateDto(bool Liked, int LikeCount);
public record RateRequest(int Stars);
public record RatingStateDto(int MyRating, double AverageRating, int RatingCount);
public record AddCommentRequest(string Text, string? PhotoUrl);
public record CommentDto(Guid Id, Guid UserId, string UserName, string Text, string? PhotoUrl, DateTime CreatedAt, bool IsHidden, bool CanDelete);

public interface IInteractionService
{
    Task<LikeStateDto> ToggleLikeAsync(Guid recipeId, CancellationToken ct = default);
    Task<RatingStateDto> RateAsync(Guid recipeId, RateRequest request, CancellationToken ct = default);
    Task<PagedResult<CommentDto>> CommentsAsync(Guid recipeId, PageQuery query, CancellationToken ct = default);
    Task<CommentDto> AddCommentAsync(Guid recipeId, AddCommentRequest request, CancellationToken ct = default);
    Task DeleteCommentAsync(Guid commentId, CancellationToken ct = default);
}

public class RateRequestValidator : AbstractValidator<RateRequest>
{
    public RateRequestValidator() { RuleFor(x => x.Stars).InclusiveBetween(1, 5); }
}

public class AddCommentRequestValidator : AbstractValidator<AddCommentRequest>
{
    public AddCommentRequestValidator()
    {
        RuleFor(x => x.Text).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.PhotoUrl).Must(u => u is null || u.StartsWith("/uploads/")).WithMessage("Photo must be uploaded through the API.")
            .MaximumLength(500);
    }
}

public class InteractionService : IInteractionService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _me;
    private readonly IImageStorage _storage;
    public InteractionService(IApplicationDbContext db, ICurrentUser me, IImageStorage storage) { _db = db; _me = me; _storage = storage; }

    public async Task<LikeStateDto> ToggleLikeAsync(Guid recipeId, CancellationToken ct = default)
    {
        var uid = RequireUser();
        await EnsureRecipeAsync(recipeId, ct);
        var like = await _db.Likes.FirstOrDefaultAsync(l => l.RecipeId == recipeId && l.UserId == uid, ct);
        bool liked;
        if (like is null) { _db.Likes.Add(new Like { RecipeId = recipeId, UserId = uid }); liked = true; }
        else { _db.Likes.Remove(like); liked = false; }
        await _db.SaveChangesAsync(ct);
        return new LikeStateDto(liked, await _db.Likes.CountAsync(l => l.RecipeId == recipeId, ct));
    }

    public async Task<RatingStateDto> RateAsync(Guid recipeId, RateRequest request, CancellationToken ct = default)
    {
        var uid = RequireUser();
        await EnsureRecipeAsync(recipeId, ct);
        var rating = await _db.Ratings.FirstOrDefaultAsync(r => r.RecipeId == recipeId && r.UserId == uid, ct);
        if (rating is null) _db.Ratings.Add(new Rating { RecipeId = recipeId, UserId = uid, Stars = request.Stars });
        else { rating.Stars = request.Stars; rating.UpdatedAt = DateTime.UtcNow; }
        await _db.SaveChangesAsync(ct);

        var stars = await _db.Ratings.Where(r => r.RecipeId == recipeId).Select(r => r.Stars).ToListAsync(ct);
        return new RatingStateDto(request.Stars, Math.Round(stars.Average(), 1), stars.Count);
    }

    public async Task<PagedResult<CommentDto>> CommentsAsync(Guid recipeId, PageQuery q, CancellationToken ct = default)
    {
        await EnsureRecipeAsync(recipeId, ct);
        var query = _db.Comments.AsNoTracking().Where(c => c.RecipeId == recipeId && (!c.IsHidden || _me.IsAdmin));
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(c => c.CreatedAt)
            .Skip((q.SafePage - 1) * q.SafePageSize).Take(q.SafePageSize)
            .Select(c => new { c.Id, c.UserId, c.User!.DisplayName, c.Text, c.PhotoUrl, c.CreatedAt, c.IsHidden }).ToListAsync(ct);
        var me = _me.UserId;
        var items = rows.Select(c => new CommentDto(c.Id, c.UserId, c.DisplayName, c.Text, c.PhotoUrl, c.CreatedAt, c.IsHidden,
            _me.IsAdmin || (me is not null && me == c.UserId))).ToList();
        return new PagedResult<CommentDto>(items, q.SafePage, q.SafePageSize, total);
    }

    public async Task<CommentDto> AddCommentAsync(Guid recipeId, AddCommentRequest request, CancellationToken ct = default)
    {
        var uid = RequireUser();
        await EnsureRecipeAsync(recipeId, ct);
        var user = await _db.Users.AsNoTracking().FirstAsync(u => u.Id == uid, ct);
        var c = new Comment { RecipeId = recipeId, UserId = uid, Text = request.Text.Trim(), PhotoUrl = request.PhotoUrl };
        _db.Comments.Add(c);
        await _db.SaveChangesAsync(ct);
        return new CommentDto(c.Id, uid, user.DisplayName, c.Text, c.PhotoUrl, c.CreatedAt, false, true);
    }

    public async Task DeleteCommentAsync(Guid commentId, CancellationToken ct = default)
    {
        var uid = RequireUser();
        var c = await _db.Comments.FirstOrDefaultAsync(x => x.Id == commentId, ct) ?? throw new NotFoundException("Comment not found.");
        if (!_me.IsAdmin && c.UserId != uid) throw new ForbiddenException("You can only delete your own comments.");
        _db.Comments.Remove(c);
        await _db.SaveChangesAsync(ct);
        if (c.PhotoUrl is not null) await _storage.DeleteAsync(c.PhotoUrl, ct);
    }

    private Guid RequireUser() => _me.UserId ?? throw new UnauthorizedAppException("Sign in required.");
    private async Task EnsureRecipeAsync(Guid id, CancellationToken ct)
    {
        if (!await _db.Recipes.AnyAsync(r => r.Id == id, ct)) throw new NotFoundException("Recipe not found.");
    }
}
