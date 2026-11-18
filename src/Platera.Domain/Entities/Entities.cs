using Microsoft.AspNetCore.Identity;

namespace Platera.Domain.Entities;

public static class Roles
{
    public const string Admin = "Admin";
    public const string User = "User";
}

public enum Difficulty { Easy = 1, Medium = 2, Hard = 3 }

public class AppUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class AppRole : IdentityRole<Guid>
{
    public AppRole() { }
    public AppRole(string name) : base(name) { }
}

public class Category { public int Id { get; set; } public string Name { get; set; } = ""; }
public class Cuisine { public int Id { get; set; } public string Name { get; set; } = ""; }

public class Tag
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public List<Recipe> Recipes { get; set; } = new();
}

public class Recipe
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public int CuisineId { get; set; }
    public Cuisine? Cuisine { get; set; }
    public int CategoryId { get; set; }
    public Category? Category { get; set; }
    public int PrepMinutes { get; set; }
    public int CookMinutes { get; set; }
    public int Servings { get; set; } = 1;
    public Difficulty Difficulty { get; set; } = Difficulty.Easy;
    public Guid AuthorId { get; set; }
    public AppUser? Author { get; set; }
    public bool IsFeatured { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<Ingredient> Ingredients { get; set; } = new();
    public List<RecipeStep> Steps { get; set; } = new();
    public List<RecipeImage> Images { get; set; } = new();
    public List<Tag> Tags { get; set; } = new();
    public List<Like> Likes { get; set; } = new();
    public List<Rating> Ratings { get; set; } = new();
    public List<Comment> Comments { get; set; } = new();
}

public class Ingredient
{
    public int Id { get; set; }
    public Guid RecipeId { get; set; }
    public string Name { get; set; } = "";
    public string Quantity { get; set; } = "";
    public int Order { get; set; }
}

public class RecipeStep
{
    public int Id { get; set; }
    public Guid RecipeId { get; set; }
    public int Order { get; set; }
    public string Instruction { get; set; } = "";
}

public class RecipeImage
{
    public int Id { get; set; }
    public Guid RecipeId { get; set; }
    public string Url { get; set; } = "";
    public string AltText { get; set; } = "";
    public int Order { get; set; }
}

public class Like
{
    public Guid RecipeId { get; set; }
    public Guid UserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Rating
{
    public Guid RecipeId { get; set; }
    public Guid UserId { get; set; }
    public int Stars { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class Comment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RecipeId { get; set; }
    public Recipe? Recipe { get; set; }
    public Guid UserId { get; set; }
    public AppUser? User { get; set; }
    public string Text { get; set; } = "";
    public string? PhotoUrl { get; set; }
    public bool IsHidden { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
