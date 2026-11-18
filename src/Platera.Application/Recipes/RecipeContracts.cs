using FluentValidation;
using Platera.Application.Common;
using Platera.Domain.Entities;

namespace Platera.Application.Recipes;

public record IngredientDto(string Name, string Quantity);
public record ImageDto(string Url, string AltText);

public record SaveRecipeRequest(
    string Title, string Description, int CuisineId, int CategoryId,
    int PrepMinutes, int CookMinutes, int Servings, Difficulty Difficulty,
    List<IngredientDto> Ingredients, List<string> Steps, List<ImageDto> Images, List<string> Tags);

public record RecipeSummaryDto(
    Guid Id, string Title, string Description, string Cuisine, string Category, int TotalMinutes,
    Difficulty Difficulty, string? CoverImageUrl, string AuthorName, int LikeCount,
    double AverageRating, int RatingCount, bool IsFeatured, DateTime CreatedAt);

public record RecipeDetailDto(
    Guid Id, string Title, string Description, int CuisineId, string Cuisine, int CategoryId, string Category,
    int PrepMinutes, int CookMinutes, int Servings, Difficulty Difficulty,
    Guid AuthorId, string AuthorName, bool IsFeatured, DateTime CreatedAt, DateTime UpdatedAt,
    List<IngredientDto> Ingredients, List<string> Steps, List<ImageDto> Images, List<string> Tags,
    int LikeCount, double AverageRating, int RatingCount, int CommentCount,
    bool LikedByMe, int? MyRating, bool CanEdit);

public enum RecipeSort { Newest, TopRated, MostLiked, Trending }

public class RecipeQuery : PageQuery
{
    public string? Q { get; set; }
    public int? CuisineId { get; set; }
    public int? CategoryId { get; set; }
    public int? MaxCookMinutes { get; set; }
    public double? MinRating { get; set; }
    public string? Tag { get; set; }
    public RecipeSort Sort { get; set; } = RecipeSort.Newest;
}

public interface IRecipeService
{
    Task<PagedResult<RecipeSummaryDto>> ListAsync(RecipeQuery query, CancellationToken ct = default);
    Task<PagedResult<RecipeSummaryDto>> MineAsync(PageQuery query, CancellationToken ct = default);
    Task<PagedResult<RecipeSummaryDto>> LikedAsync(PageQuery query, CancellationToken ct = default);
    Task<IReadOnlyList<RecipeSummaryDto>> FeaturedAsync(int take, CancellationToken ct = default);
    Task<IReadOnlyList<RecipeSummaryDto>> TrendingAsync(int take, CancellationToken ct = default);
    Task<RecipeDetailDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<RecipeDetailDto> CreateAsync(SaveRecipeRequest request, CancellationToken ct = default);
    Task<RecipeDetailDto> UpdateAsync(Guid id, SaveRecipeRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public class SaveRecipeRequestValidator : AbstractValidator<SaveRecipeRequest>
{
    public SaveRecipeRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(160);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.CuisineId).GreaterThan(0);
        RuleFor(x => x.CategoryId).GreaterThan(0);
        RuleFor(x => x.PrepMinutes).InclusiveBetween(0, 24 * 60);
        RuleFor(x => x.CookMinutes).InclusiveBetween(0, 24 * 60);
        RuleFor(x => x.Servings).InclusiveBetween(1, 100);
        RuleFor(x => x.Difficulty).IsInEnum();
        RuleFor(x => x.Ingredients).NotNull().Must(l => l is { Count: > 0 and <= 80 }).WithMessage("Add between 1 and 80 ingredients.");
        RuleForEach(x => x.Ingredients).ChildRules(i =>
        {
            i.RuleFor(a => a.Name).NotEmpty().MaximumLength(160);
            i.RuleFor(a => a.Quantity).MaximumLength(60);
        });
        RuleFor(x => x.Steps).NotNull().Must(l => l is { Count: > 0 and <= 60 }).WithMessage("Add between 1 and 60 steps.");
        RuleForEach(x => x.Steps).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.Images).NotNull().Must(l => l is { Count: >= 1 and <= 5 }).WithMessage("Add between 1 and 5 images.");
        RuleForEach(x => x.Images).ChildRules(i =>
        {
            i.RuleFor(a => a.Url).NotEmpty().MaximumLength(500)
                .Must(u => u.StartsWith("/uploads/") || u.StartsWith("/seed/")).WithMessage("Image must be uploaded through the API.");
            i.RuleFor(a => a.AltText).MaximumLength(200);
        });
        RuleFor(x => x.Tags).NotNull().Must(l => l.Count <= 10).WithMessage("At most 10 tags.");
        RuleForEach(x => x.Tags).NotEmpty().MaximumLength(40);
    }
}

public class RecipeQueryValidator : AbstractValidator<RecipeQuery>
{
    public RecipeQueryValidator()
    {
        RuleFor(x => x.Q).MaximumLength(100);
        RuleFor(x => x.MinRating).InclusiveBetween(0, 5).When(x => x.MinRating.HasValue);
        RuleFor(x => x.MaxCookMinutes).GreaterThanOrEqualTo(0).When(x => x.MaxCookMinutes.HasValue);
    }
}
