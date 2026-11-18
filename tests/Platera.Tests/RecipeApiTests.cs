using System.Net;
using System.Net.Http.Json;
using Platera.Application.Common;
using Platera.Application.Images;
using Platera.Application.Lookups;
using Platera.Application.Recipes;
using Platera.Domain.Entities;
using Platera.Tests.TestSupport;

namespace Platera.Tests;

public class RecipeApiTests : IClassFixture<PlateraFactory>
{
    private readonly PlateraFactory _f;
    public RecipeApiTests(PlateraFactory f) { _f = f; }

    private static SaveRecipeRequest NewRecipe(string title = "Test Dal", int cuisine = 1, int category = 1) => new(
        title, "A comforting lentil dish.", cuisine, category, 10, 25, 4, Difficulty.Easy,
        new() { new("red lentils", "1 cup"), new("turmeric", "1 tsp") },
        new() { "Rinse lentils.", "Simmer until soft." },
        new() { new("/seed/recipe-1.svg", "Bowl of dal") },
        new() { "Vegan", "lentils" });

    private static async Task<(int cuisine, int category)> LookupsAsync(HttpClient c)
    {
        var cu = await c.GetFromJsonAsync<List<LookupDto>>("/api/lookups/cuisines");
        var ca = await c.GetFromJsonAsync<List<LookupDto>>("/api/lookups/categories");
        return (cu![0].Id, ca![0].Id);
    }

    [Fact]
    public async Task Seeded_feed_is_paginated_and_public()
    {
        var c = _f.CreateClient();
        var page = await c.GetFromJsonAsync<PagedResult<RecipeSummaryDto>>("/api/recipes?pageSize=5");
        Assert.Equal(5, page!.Items.Count);
        Assert.True(page.TotalCount >= 12);
        Assert.True(page.TotalPages >= 3);
    }

    [Fact]
    public async Task Create_requires_auth()
    {
        var c = _f.CreateClient();
        var (cu, ca) = await LookupsAsync(c);
        var res = await c.PostAsJsonAsync("/api/recipes", NewRecipe(cuisine: cu, category: ca));
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Owner_can_create_update_and_delete_but_others_cannot()
    {
        var owner = await _f.AuthedClientAsync();
        var other = await _f.AuthedClientAsync();
        var (cu, ca) = await LookupsAsync(owner);

        var created = await owner.PostAsJsonAsync("/api/recipes", NewRecipe("Owner Dal", cu, ca));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var detail = (await created.Content.ReadFromJsonAsync<RecipeDetailDto>())!;
        Assert.True(detail.CanEdit);
        Assert.Equal(new[] { "lentils", "vegan" }, detail.Tags);

        var forbidden = await other.PutAsJsonAsync($"/api/recipes/{detail.Id}", NewRecipe("Hijack", cu, ca));
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.DeleteAsync($"/api/recipes/{detail.Id}")).StatusCode);

        var upd = await owner.PutAsJsonAsync($"/api/recipes/{detail.Id}", NewRecipe("Owner Dal v2", cu, ca));
        upd.EnsureSuccessStatusCode();
        Assert.Equal("Owner Dal v2", (await upd.Content.ReadFromJsonAsync<RecipeDetailDto>())!.Title);

        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"/api/recipes/{detail.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/api/recipes/{detail.Id}")).StatusCode);
    }

    [Fact]
    public async Task Admin_can_edit_any_recipe()
    {
        var owner = await _f.AuthedClientAsync();
        var admin = await _f.AdminClientAsync();
        var (cu, ca) = await LookupsAsync(owner);
        var detail = (await (await owner.PostAsJsonAsync("/api/recipes", NewRecipe("To Moderate", cu, ca))).Content.ReadFromJsonAsync<RecipeDetailDto>())!;
        var res = await admin.PutAsJsonAsync($"/api/recipes/{detail.Id}", NewRecipe("Moderated", cu, ca));
        res.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Invalid_recipe_returns_400_with_field_errors()
    {
        var c = await _f.AuthedClientAsync();
        var bad = NewRecipe() with { Title = "", Images = new(), Servings = 0 };
        var res = await c.PostAsJsonAsync("/api/recipes", bad);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await res.Content.ReadAsStringAsync();
        Assert.Contains("Title", body);
        Assert.Contains("Images", body);
    }

    [Fact]
    public async Task Rejects_external_image_urls()
    {
        var c = await _f.AuthedClientAsync();
        var (cu, ca) = await LookupsAsync(c);
        var bad = NewRecipe(cuisine: cu, category: ca) with { Images = new() { new("https://evil.example/x.png", "x") } };
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/recipes", bad)).StatusCode);
    }

    [Fact]
    public async Task Search_filters_by_keyword_and_cuisine()
    {
        var c = _f.CreateClient();
        var hits = await c.GetFromJsonAsync<PagedResult<RecipeSummaryDto>>("/api/recipes?q=pizza");
        Assert.Contains(hits!.Items, r => r.Title.Contains("Pizza"));

        var byIngredient = await c.GetFromJsonAsync<PagedResult<RecipeSummaryDto>>("/api/recipes?q=chickpeas");
        Assert.Contains(byIngredient!.Items, r => r.Title.Contains("Hummus"));

        var byTag = await c.GetFromJsonAsync<PagedResult<RecipeSummaryDto>>("/api/recipes?q=street-food");
        Assert.True(byTag!.TotalCount >= 2);

        var quick = await c.GetFromJsonAsync<PagedResult<RecipeSummaryDto>>("/api/recipes?maxCookMinutes=5");
        Assert.All(quick!.Items, r => Assert.True(r.TotalMinutes <= 5 + 120));
        Assert.NotEmpty(quick.Items);
    }

    [Fact]
    public async Task Featured_and_trending_return_items()
    {
        var c = _f.CreateClient();
        var featured = await c.GetFromJsonAsync<List<RecipeSummaryDto>>("/api/recipes/featured?take=4");
        Assert.NotEmpty(featured!);
        Assert.All(featured, r => Assert.True(r.IsFeatured));
        Assert.NotEmpty((await c.GetFromJsonAsync<List<RecipeSummaryDto>>("/api/recipes/trending"))!);
    }

    [Fact]
    public async Task Upload_accepts_png_and_rejects_other_types()
    {
        var c = await _f.AuthedClientAsync();
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0 };
        using var ok = new MultipartFormDataContent { { new ByteArrayContent(png), "file", "pic.png" } };
        var good = await c.PostAsync("/api/uploads/images", ok);
        good.EnsureSuccessStatusCode();
        var url = (await good.Content.ReadFromJsonAsync<ImageUploadResult>())!.Url;
        Assert.StartsWith("/uploads/", url);
        Assert.EndsWith(".png", url);

        using var bad = new MultipartFormDataContent { { new ByteArrayContent("<script>alert(1)</script>"u8.ToArray()), "file", "evil.png" } };
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsync("/api/uploads/images", bad)).StatusCode);

        var anon = _f.CreateClient();
        using var anonBody = new MultipartFormDataContent { { new ByteArrayContent(png), "file", "pic.png" } };
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsync("/api/uploads/images", anonBody)).StatusCode);
    }
}

public class ImageUploadServiceTests
{
    private class FakeStorage : IImageStorage
    {
        public Task<string> SaveAsync(Stream content, string extension, CancellationToken ct = default) => Task.FromResult("/uploads/x" + extension);
        public Task DeleteAsync(string url, CancellationToken ct = default) => Task.CompletedTask;
    }

    [Theory]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, ".jpg")]
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 }, ".gif")]
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0, 0x57, 0x45, 0x42, 0x50 }, ".webp")]
    public void Detect_recognises_known_formats(byte[] header, string expected) =>
        Assert.Equal(expected, ImageUploadService.Detect(header));

    [Fact]
    public async Task Upload_rejects_files_over_5MB()
    {
        var svc = new ImageUploadService(new FakeStorage());
        await Assert.ThrowsAsync<BadRequestException>(() =>
            svc.UploadAsync(new MemoryStream(new byte[10]), ImageUploadService.MaxBytes + 1));
    }

    [Fact]
    public async Task Upload_rejects_empty_and_unknown_content()
    {
        var svc = new ImageUploadService(new FakeStorage());
        await Assert.ThrowsAsync<BadRequestException>(() => svc.UploadAsync(new MemoryStream(), 0));
        await Assert.ThrowsAsync<BadRequestException>(() => svc.UploadAsync(new MemoryStream(new byte[] { 1, 2, 3, 4 }), 4));
    }
}
