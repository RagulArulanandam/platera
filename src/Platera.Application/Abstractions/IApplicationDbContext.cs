using Microsoft.EntityFrameworkCore;
using Platera.Domain.Entities;

namespace Platera.Application.Abstractions;

public interface IApplicationDbContext
{
    DbSet<AppUser> Users { get; }
    DbSet<Category> Categories { get; }
    DbSet<Cuisine> Cuisines { get; }
    DbSet<Tag> Tags { get; }
    DbSet<Recipe> Recipes { get; }
    DbSet<Ingredient> Ingredients { get; }
    DbSet<RecipeStep> RecipeSteps { get; }
    DbSet<RecipeImage> RecipeImages { get; }
    DbSet<Like> Likes { get; }
    DbSet<Rating> Ratings { get; }
    DbSet<Comment> Comments { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
