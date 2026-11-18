using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Platera.Application.Abstractions;
using Platera.Application.Common;
using Platera.Application.Images;
using Platera.Domain.Entities;

namespace Platera.Application.Recipes;

public class RecipeService : IRecipeService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _me;
    private readonly IImageStorage _storage;

    public RecipeService(IApplicationDbContext db, ICurrentUser me, IImageStorage storage)
    { _db = db; _me = me; _storage = storage; }

    private static readonly Expression<Func<Recipe, RecipeSummaryDto>> ToSummary = r => new RecipeSummaryDto(
        r.Id, r.Title, r.Description, r.Cuisine!.Name, r.Category!.Name, r.PrepMinutes + r.CookMinutes,
        r.Difficulty, r.Images.OrderBy(i => i.Order).Select(i => i.Url).FirstOrDefault(),
        r.Author!.DisplayName, r.Likes.Count(), r.Ratings.Average(x => (double?)x.Stars) ?? 0, r.Ratings.Count(),
        r.IsFeatured, r.CreatedAt);

    public async Task<PagedResult<RecipeSummaryDto>> ListAsync(RecipeQuery q, CancellationToken ct = default)
    {
        IQueryable<Recipe> query = _db.Recipes.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q.Q))
        {
            foreach (var term in q.Q.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(6))
            {
                var t = term;
                query = query.Where(r => r.Title.Contains(t) || r.Ingredients.Any(i => i.Name.Contains(t)) || r.Tags.Any(x => x.Name.Contains(t)));
            }
        }
        if (q.CuisineId is { } cu) query = query.Where(r => r.CuisineId == cu);
        if (q.CategoryId is { } ca) query = query.Where(r => r.CategoryId == ca);
        if (q.MaxCookMinutes is { } mc) query = query.Where(r => r.CookMinutes <= mc);
        if (q.MinRating is { } mr) query = query.Where(r => r.Ratings.Any() && r.Ratings.Average(x => (double)x.Stars) >= mr);
        if (!string.IsNullOrWhiteSpace(q.Tag))
        {
            var tag = q.Tag.Trim().ToLowerInvariant();
            query = query.Where(r => r.Tags.Any(x => x.Name == tag));
        }

        query = q.Sort switch
        {
            RecipeSort.TopRated => query.OrderByDescending(r => r.Ratings.Average(x => (double?)x.Stars) ?? 0)
                                        .ThenByDescending(r => r.Ratings.Count()).ThenByDescending(r => r.CreatedAt),
            RecipeSort.MostLiked => query.OrderByDescending(r => r.Likes.Count()).ThenByDescending(r => r.CreatedAt),
            RecipeSort.Trending => query.OrderByDescending(r => r.Likes.Count(l => l.CreatedAt > DateTime.UtcNow.AddDays(-7))).ThenByDescending(r => r.CreatedAt),
            _ => query.OrderByDescending(r => r.CreatedAt),
        };
        return await PageAsync(query, q, ct);
    }

    public async Task<PagedResult<RecipeSummaryDto>> MineAsync(PageQuery q, CancellationToken ct = default)
    {
        var uid = RequireUser();
        return await PageAsync(_db.Recipes.AsNoTracking().Where(r => r.AuthorId == uid).OrderByDescending(r => r.CreatedAt), q, ct);
    }

    public async Task<PagedResult<RecipeSummaryDto>> LikedAsync(PageQuery q, CancellationToken ct = default)
    {
        var uid = RequireUser();
        var query = _db.Likes.AsNoTracking().Where(l => l.UserId == uid).OrderByDescending(l => l.CreatedAt)
            .Join(_db.Recipes, l => l.RecipeId, r => r.Id, (l, r) => r);
        return await PageAsync(query, q, ct);
    }

    public async Task<IReadOnlyList<RecipeSummaryDto>> FeaturedAsync(int take, CancellationToken ct = default) =>
        await _db.Recipes.AsNoTracking().Where(r => r.IsFeatured).OrderByDescending(r => r.CreatedAt)
            .Take(Math.Clamp(take, 1, 24)).Select(ToSummary).ToListAsync(ct);

    public async Task<IReadOnlyList<RecipeSummaryDto>> TrendingAsync(int take, CancellationToken ct = default)
    {
        var since = DateTime.UtcNow.AddDays(-7);
        return await _db.Recipes.AsNoTracking()
            .OrderByDescending(r => r.Likes.Count(l => l.CreatedAt > since) + r.Comments.Count(c => c.CreatedAt > since && !c.IsHidden))
            .ThenByDescending(r => r.Likes.Count()).ThenByDescending(r => r.CreatedAt)
            .Take(Math.Clamp(take, 1, 24)).Select(ToSummary).ToListAsync(ct);
    }

    public async Task<RecipeDetailDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var r = await _db.Recipes.AsNoTracking().AsSplitQuery()
            .Include(x => x.Cuisine).Include(x => x.Category).Include(x => x.Author)
            .Include(x => x.Ingredients).Include(x => x.Steps).Include(x => x.Images).Include(x => x.Tags)
            .FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Recipe not found.");

        var likeCount = await _db.Likes.CountAsync(l => l.RecipeId == id, ct);
        var ratings = await _db.Ratings.Where(x => x.RecipeId == id).Select(x => x.Stars).ToListAsync(ct);
        var commentCount = await _db.Comments.CountAsync(c => c.RecipeId == id && !c.IsHidden, ct);
        var uid = _me.UserId;
        var liked = uid is not null && await _db.Likes.AnyAsync(l => l.RecipeId == id && l.UserId == uid, ct);
        int? mine = uid is null ? null : (await _db.Ratings.Where(x => x.RecipeId == id && x.UserId == uid).Select(x => (int?)x.Stars).FirstOrDefaultAsync(ct));

        return new RecipeDetailDto(
            r.Id, r.Title, r.Description, r.CuisineId, r.Cuisine!.Name, r.CategoryId, r.Category!.Name,
            r.PrepMinutes, r.CookMinutes, r.Servings, r.Difficulty, r.AuthorId, r.Author!.DisplayName,
            r.IsFeatured, r.CreatedAt, r.UpdatedAt,
            r.Ingredients.OrderBy(i => i.Order).Select(i => new IngredientDto(i.Name, i.Quantity)).ToList(),
            r.Steps.OrderBy(s => s.Order).Select(s => s.Instruction).ToList(),
            r.Images.OrderBy(i => i.Order).Select(i => new ImageDto(i.Url, i.AltText)).ToList(),
            r.Tags.Select(t => t.Name).OrderBy(n => n).ToList(),
            likeCount, ratings.Count == 0 ? 0 : Math.Round(ratings.Average(), 1), ratings.Count, commentCount,
            liked, mine, CanModify(r.AuthorId));
    }

    public async Task<RecipeDetailDto> CreateAsync(SaveRecipeRequest req, CancellationToken ct = default)
    {
        var uid = RequireUser();
        await EnsureLookupsAsync(req, ct);
        var recipe = new Recipe { AuthorId = uid };
        await ApplyAsync(recipe, req, ct);
        _db.Recipes.Add(recipe);
        await _db.SaveChangesAsync(ct);
        return await GetAsync(recipe.Id, ct);
    }

    public async Task<RecipeDetailDto> UpdateAsync(Guid id, SaveRecipeRequest req, CancellationToken ct = default)
    {
        RequireUser();
        var recipe = await _db.Recipes
            .Include(r => r.Ingredients).Include(r => r.Steps).Include(r => r.Images).Include(r => r.Tags)
            .FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw new NotFoundException("Recipe not found.");
        if (!CanModify(recipe.AuthorId)) throw new ForbiddenException("You can only edit your own recipes.");
        await EnsureLookupsAsync(req, ct);

        var oldUploads = recipe.Images.Select(i => i.Url).ToList();
        _db.Ingredients.RemoveRange(recipe.Ingredients);
        _db.RecipeSteps.RemoveRange(recipe.Steps);
        _db.RecipeImages.RemoveRange(recipe.Images);
        recipe.Ingredients = new(); recipe.Steps = new(); recipe.Images = new();
        await ApplyAsync(recipe, req, ct);
        recipe.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var kept = req.Images.Select(i => i.Url).ToHashSet();
        foreach (var url in oldUploads.Where(u => !kept.Contains(u))) await _storage.DeleteAsync(url, ct);
        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        RequireUser();
        var recipe = await _db.Recipes.Include(r => r.Images).Include(r => r.Comments)
            .FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw new NotFoundException("Recipe not found.");
        if (!CanModify(recipe.AuthorId)) throw new ForbiddenException("You can only delete your own recipes.");
        var urls = recipe.Images.Select(i => i.Url).Concat(recipe.Comments.Where(c => c.PhotoUrl != null).Select(c => c.PhotoUrl!)).ToList();
        _db.Recipes.Remove(recipe);
        await _db.SaveChangesAsync(ct);
        foreach (var u in urls) await _storage.DeleteAsync(u, ct);
    }

    // ---- helpers ----
    private Guid RequireUser() => _me.UserId ?? throw new UnauthorizedAppException("Sign in required.");
    private bool CanModify(Guid authorId) => _me.IsAdmin || (_me.UserId is { } u && u == authorId);

    private async Task EnsureLookupsAsync(SaveRecipeRequest req, CancellationToken ct)
    {
        if (!await _db.Cuisines.AnyAsync(c => c.Id == req.CuisineId, ct)) throw new BadRequestException("Unknown cuisine.");
        if (!await _db.Categories.AnyAsync(c => c.Id == req.CategoryId, ct)) throw new BadRequestException("Unknown category.");
    }

    private async Task ApplyAsync(Recipe r, SaveRecipeRequest req, CancellationToken ct)
    {
        r.Title = req.Title.Trim();
        r.Description = req.Description.Trim();
        r.CuisineId = req.CuisineId;
        r.CategoryId = req.CategoryId;
        r.PrepMinutes = req.PrepMinutes;
        r.CookMinutes = req.CookMinutes;
        r.Servings = req.Servings;
        r.Difficulty = req.Difficulty;
        r.Ingredients = req.Ingredients.Select((i, n) => new Ingredient { Name = i.Name.Trim(), Quantity = (i.Quantity ?? "").Trim(), Order = n }).ToList();
        r.Steps = req.Steps.Select((s, n) => new RecipeStep { Instruction = s.Trim(), Order = n + 1 }).ToList();
        r.Images = req.Images.Select((i, n) => new RecipeImage { Url = i.Url, AltText = string.IsNullOrWhiteSpace(i.AltText) ? req.Title : i.AltText.Trim(), Order = n }).ToList();

        var names = req.Tags.Select(t => t.Trim().ToLowerInvariant()).Where(t => t.Length > 0).Distinct().ToList();
        var existing = await _db.Tags.Where(t => names.Contains(t.Name)).ToListAsync(ct);
        r.Tags = names.Select(n => existing.FirstOrDefault(t => t.Name == n) ?? new Tag { Name = n }).ToList();
    }

    private static async Task<PagedResult<RecipeSummaryDto>> PageAsync(IQueryable<Recipe> query, PageQuery q, CancellationToken ct)
    {
        var total = await query.CountAsync(ct);
        var items = await query.Skip((q.SafePage - 1) * q.SafePageSize).Take(q.SafePageSize).Select(ToSummary).ToListAsync(ct);
        return new PagedResult<RecipeSummaryDto>(items, q.SafePage, q.SafePageSize, total);
    }
}
