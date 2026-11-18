using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Platera.Application.Abstractions;
using Platera.Domain.Entities;

namespace Platera.Infrastructure.Persistence;

public class PlateraDbContext : IdentityDbContext<AppUser, AppRole, Guid>, IApplicationDbContext
{
    public PlateraDbContext(DbContextOptions<PlateraDbContext> options) : base(options) { }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Cuisine> Cuisines => Set<Cuisine>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<RecipeStep> RecipeSteps => Set<RecipeStep>();
    public DbSet<RecipeImage> RecipeImages => Set<RecipeImage>();
    public DbSet<Like> Likes => Set<Like>();
    public DbSet<Rating> Ratings => Set<Rating>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<AppUser>().Property(u => u.DisplayName).HasMaxLength(80);

        b.Entity<Category>(e => { e.Property(x => x.Name).HasMaxLength(60); e.HasIndex(x => x.Name).IsUnique(); });
        b.Entity<Cuisine>(e => { e.Property(x => x.Name).HasMaxLength(60); e.HasIndex(x => x.Name).IsUnique(); });
        b.Entity<Tag>(e => { e.Property(x => x.Name).HasMaxLength(40); e.HasIndex(x => x.Name).IsUnique(); });

        b.Entity<Recipe>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(160).IsRequired();
            e.Property(x => x.Description).HasMaxLength(2000);
            e.HasIndex(x => x.CreatedAt);
            e.HasOne(x => x.Cuisine).WithMany().HasForeignKey(x => x.CuisineId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Author).WithMany().HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Tags).WithMany(t => t.Recipes);
        });

        b.Entity<Ingredient>(e => { e.Property(x => x.Name).HasMaxLength(160); e.Property(x => x.Quantity).HasMaxLength(60); });
        b.Entity<RecipeStep>().Property(x => x.Instruction).HasMaxLength(2000);
        b.Entity<RecipeImage>(e => { e.Property(x => x.Url).HasMaxLength(500); e.Property(x => x.AltText).HasMaxLength(200); });

        b.Entity<Like>(e =>
        {
            e.HasKey(x => new { x.RecipeId, x.UserId });
            e.HasOne<Recipe>().WithMany(r => r.Likes).HasForeignKey(x => x.RecipeId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
        });
        b.Entity<Rating>(e =>
        {
            e.HasKey(x => new { x.RecipeId, x.UserId });
            e.HasOne<Recipe>().WithMany(r => r.Ratings).HasForeignKey(x => x.RecipeId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
        });
        b.Entity<Comment>(e =>
        {
            e.Property(x => x.Text).HasMaxLength(2000);
            e.Property(x => x.PhotoUrl).HasMaxLength(500);
            e.HasOne(x => x.Recipe).WithMany(r => r.Comments).HasForeignKey(x => x.RecipeId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
        });
        b.Entity<RefreshToken>(e =>
        {
            e.Property(x => x.TokenHash).HasMaxLength(128);
            e.HasIndex(x => x.TokenHash);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
