using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Platera.Application.Common;
using Platera.Application.Images;
using Platera.Application.Interactions;
using Platera.Application.Recipes;
using Platera.Domain.Entities;
using Platera.Infrastructure.Persistence;

namespace Platera.Tests;

internal class FakeUser : ICurrentUser
{
    public Guid? UserId { get; set; }
    public bool IsAuthenticated => UserId is not null;
    public bool IsAdmin { get; set; }
}

internal class NullStorage : IImageStorage
{
    public List<string> Deleted { get; } = new();
    public Task<string> SaveAsync(Stream content, string extension, CancellationToken ct = default) => Task.FromResult("/uploads/x" + extension);
    public Task DeleteAsync(string url, CancellationToken ct = default) { Deleted.Add(url); return Task.CompletedTask; }
}

public class ApplicationUnitTests
{
    private static PlateraDbContext NewDb() =>
        new(new DbContextOptionsBuilder<PlateraDbContext>().UseInMemoryDatabase("unit-" + Guid.NewGuid()).Options);

    private static async Task<(PlateraDbContext db, AppUser author, Cuisine cu, Category ca)> SeedAsync()
    {
        var db = NewDb();
        var author = new AppUser { Id = Guid.NewGuid(), UserName = "a@x.com", Email = "a@x.com", DisplayName = "Author" };
        var cu = new Cuisine { Name = "Thai" };
        var ca = new Category { Name = "Main" };
        db.AddRange(author, cu, ca);
        await db.SaveChangesAsync();
        return (db, author, cu, ca);
    }

    private static SaveRecipeRequest Req(int cuisine, int category, string title = "Pad Thai") => new(
        title, "Sweet, sour, nutty noodles.", cuisine, category, 10, 10, 2, Difficulty.Medium,
        new() { new("rice noodles", "200 g") }, new() { "Soak noodles.", "Stir-fry." },
        new() { new("/uploads/a.png", "pad thai") }, new() { "Noodles", "noodles", " Quick " });

    [Fact]
    public async Task Create_normalises_tags_and_sets_author()
    {
        var (db, author, cu, ca) = await SeedAsync();
        var svc = new RecipeService(db, new FakeUser { UserId = author.Id }, new NullStorage());
        var created = await svc.CreateAsync(Req(cu.Id, ca.Id));
        Assert.Equal(author.Id, created.AuthorId);
        Assert.Equal(new[] { "noodles", "quick" }, created.Tags);
        Assert.Equal(new[] { "Soak noodles.", "Stir-fry." }, created.Steps);
    }

    [Fact]
    public async Task Create_without_login_is_unauthorized()
    {
        var (db, _, cu, ca) = await SeedAsync();
        var svc = new RecipeService(db, new FakeUser(), new NullStorage());
        await Assert.ThrowsAsync<UnauthorizedAppException>(() => svc.CreateAsync(Req(cu.Id, ca.Id)));
    }

    [Fact]
    public async Task Create_with_unknown_cuisine_is_bad_request()
    {
        var (db, author, _, ca) = await SeedAsync();
        var svc = new RecipeService(db, new FakeUser { UserId = author.Id }, new NullStorage());
        await Assert.ThrowsAsync<BadRequestException>(() => svc.CreateAsync(Req(9999, ca.Id)));
    }

    [Fact]
    public async Task Only_owner_or_admin_can_delete_and_images_are_cleaned_up()
    {
        var (db, author, cu, ca) = await SeedAsync();
        var storage = new NullStorage();
        var id = (await new RecipeService(db, new FakeUser { UserId = author.Id }, storage).CreateAsync(Req(cu.Id, ca.Id))).Id;

        var stranger = new RecipeService(db, new FakeUser { UserId = Guid.NewGuid() }, storage);
        await Assert.ThrowsAsync<ForbiddenException>(() => stranger.DeleteAsync(id));

        var admin = new RecipeService(db, new FakeUser { UserId = Guid.NewGuid(), IsAdmin = true }, storage);
        await admin.DeleteAsync(id);
        Assert.Empty(db.Recipes);
        Assert.Contains("/uploads/a.png", storage.Deleted);
    }

    [Fact]
    public async Task Update_removes_dropped_images_from_storage()
    {
        var (db, author, cu, ca) = await SeedAsync();
        var storage = new NullStorage();
        var svc = new RecipeService(db, new FakeUser { UserId = author.Id }, storage);
        var created = await svc.CreateAsync(Req(cu.Id, ca.Id));
        var changed = Req(cu.Id, ca.Id, "Pad Thai v2") with { Images = new() { new("/uploads/b.png", "new") } };
        var updated = await svc.UpdateAsync(created.Id, changed);
        Assert.Equal("/uploads/b.png", updated.Images.Single().Url);
        Assert.Equal(new[] { "/uploads/a.png" }, storage.Deleted);
    }

    [Fact]
    public async Task Ratings_average_and_one_vote_per_user()
    {
        var (db, author, cu, ca) = await SeedAsync();
        var recipe = await new RecipeService(db, new FakeUser { UserId = author.Id }, new NullStorage()).CreateAsync(Req(cu.Id, ca.Id));
        var u1 = Guid.NewGuid(); var u2 = Guid.NewGuid();

        var s1 = await new InteractionService(db, new FakeUser { UserId = u1 }, new NullStorage()).RateAsync(recipe.Id, new RateRequest(5));
        var s2 = await new InteractionService(db, new FakeUser { UserId = u2 }, new NullStorage()).RateAsync(recipe.Id, new RateRequest(2));
        Assert.Equal(3.5, s2.AverageRating);
        Assert.Equal(2, s2.RatingCount);

        var again = await new InteractionService(db, new FakeUser { UserId = u2 }, new NullStorage()).RateAsync(recipe.Id, new RateRequest(4));
        Assert.Equal(2, again.RatingCount);
        Assert.Equal(4.5, again.AverageRating);
        Assert.Equal(5, s1.MyRating);
    }

    [Fact]
    public async Task Ranking_by_top_rated_and_most_liked()
    {
        var (db, author, cu, ca) = await SeedAsync();
        var svc = new RecipeService(db, new FakeUser { UserId = author.Id }, new NullStorage());
        var a = await svc.CreateAsync(Req(cu.Id, ca.Id, "A"));
        var b = await svc.CreateAsync(Req(cu.Id, ca.Id, "B"));
        var voter = new InteractionService(db, new FakeUser { UserId = Guid.NewGuid() }, new NullStorage());
        await voter.RateAsync(b.Id, new RateRequest(5));
        await voter.RateAsync(a.Id, new RateRequest(2));
        await voter.ToggleLikeAsync(a.Id);

        var top = await svc.ListAsync(new RecipeQuery { Sort = RecipeSort.TopRated });
        Assert.Equal("B", top.Items[0].Title);
        var liked = await svc.ListAsync(new RecipeQuery { Sort = RecipeSort.MostLiked });
        Assert.Equal("A", liked.Items[0].Title);
        var min4 = await svc.ListAsync(new RecipeQuery { MinRating = 4 });
        Assert.Equal(new[] { "B" }, min4.Items.Select(i => i.Title));
    }

    [Fact]
    public async Task Pagination_clamps_page_size()
    {
        var (db, author, cu, ca) = await SeedAsync();
        var svc = new RecipeService(db, new FakeUser { UserId = author.Id }, new NullStorage());
        for (var i = 0; i < 3; i++) await svc.CreateAsync(Req(cu.Id, ca.Id, "R" + i));
        var page = await svc.ListAsync(new RecipeQuery { Page = 0, PageSize = 1000 });
        Assert.Equal(1, page.Page);
        Assert.Equal(50, page.PageSize);
        Assert.Equal(3, page.TotalCount);
    }

    [Fact]
    public void Recipe_validator_enforces_limits()
    {
        var v = new SaveRecipeRequestValidator();
        var ok = Req(1, 1);
        Assert.True(v.Validate(ok).IsValid);
        Assert.False(v.Validate(ok with { Images = Enumerable.Range(0, 6).Select(i => new ImageDto($"/uploads/{i}.png", "")).ToList() }).IsValid);
        Assert.False(v.Validate(ok with { Tags = Enumerable.Range(0, 11).Select(i => "t" + i).ToList() }).IsValid);
        Assert.False(v.Validate(ok with { Steps = new() }).IsValid);
        Assert.False(v.Validate(ok with { Title = new string('x', 161) }).IsValid);
    }

    [Fact]
    public async Task Hidden_comments_are_visible_only_to_admins()
    {
        var (db, author, cu, ca) = await SeedAsync();
        var recipe = await new RecipeService(db, new FakeUser { UserId = author.Id }, new NullStorage()).CreateAsync(Req(cu.Id, ca.Id));
        db.Comments.Add(new Comment { RecipeId = recipe.Id, UserId = author.Id, Text = "ok" });
        db.Comments.Add(new Comment { RecipeId = recipe.Id, UserId = author.Id, Text = "spam", IsHidden = true });
        await db.SaveChangesAsync();

        var pub = await new InteractionService(db, new FakeUser(), new NullStorage()).CommentsAsync(recipe.Id, new PageQuery());
        Assert.Equal(new[] { "ok" }, pub.Items.Select(c => c.Text));
        var adm = await new InteractionService(db, new FakeUser { UserId = Guid.NewGuid(), IsAdmin = true }, new NullStorage()).CommentsAsync(recipe.Id, new PageQuery());
        Assert.Equal(2, adm.TotalCount);
    }
}
