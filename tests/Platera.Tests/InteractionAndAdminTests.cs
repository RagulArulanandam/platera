using System.Net;
using System.Net.Http.Json;
using Platera.Application.Admin;
using Platera.Application.Auth;
using Platera.Application.Common;
using Platera.Application.Interactions;
using Platera.Application.Lookups;
using Platera.Application.Recipes;
using Platera.Tests.TestSupport;

namespace Platera.Tests;

public class InteractionAndAdminTests : IClassFixture<PlateraFactory>
{
    private readonly PlateraFactory _f;
    public InteractionAndAdminTests(PlateraFactory f) { _f = f; }

    private async Task<Guid> AnyRecipeIdAsync()
    {
        var page = await _f.CreateClient().GetFromJsonAsync<PagedResult<RecipeSummaryDto>>("/api/recipes?pageSize=1");
        return page!.Items[0].Id;
    }

    [Fact]
    public async Task Like_toggles_on_and_off()
    {
        var id = await AnyRecipeIdAsync();
        var c = await _f.AuthedClientAsync();
        var before = (await c.GetFromJsonAsync<RecipeDetailDto>($"/api/recipes/{id}"))!.LikeCount;

        var on = (await (await c.PostAsync($"/api/recipes/{id}/like", null)).Content.ReadFromJsonAsync<LikeStateDto>())!;
        Assert.True(on.Liked);
        Assert.Equal(before + 1, on.LikeCount);
        Assert.True((await c.GetFromJsonAsync<RecipeDetailDto>($"/api/recipes/{id}"))!.LikedByMe);

        var liked = await c.GetFromJsonAsync<PagedResult<RecipeSummaryDto>>("/api/recipes/liked");
        Assert.Contains(liked!.Items, r => r.Id == id);

        var off = (await (await c.PostAsync($"/api/recipes/{id}/like", null)).Content.ReadFromJsonAsync<LikeStateDto>())!;
        Assert.False(off.Liked);
        Assert.Equal(before, off.LikeCount);
    }

    [Fact]
    public async Task Like_requires_auth()
    {
        var id = await AnyRecipeIdAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await _f.CreateClient().PostAsync($"/api/recipes/{id}/like", null)).StatusCode);
    }

    [Fact]
    public async Task Rating_is_one_per_user_and_updates_average()
    {
        var id = await AnyRecipeIdAsync();
        var a = await _f.AuthedClientAsync();
        var first = (await (await a.PutAsJsonAsync($"/api/recipes/{id}/rating", new RateRequest(2))).Content.ReadFromJsonAsync<RatingStateDto>())!;
        var again = (await (await a.PutAsJsonAsync($"/api/recipes/{id}/rating", new RateRequest(5))).Content.ReadFromJsonAsync<RatingStateDto>())!;
        Assert.Equal(first.RatingCount, again.RatingCount); // updated, not duplicated
        Assert.Equal(5, again.MyRating);
        Assert.Equal(5, (await a.GetFromJsonAsync<RecipeDetailDto>($"/api/recipes/{id}"))!.MyRating);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public async Task Rating_outside_1_to_5_is_rejected(int stars)
    {
        var id = await AnyRecipeIdAsync();
        var c = await _f.AuthedClientAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PutAsJsonAsync($"/api/recipes/{id}/rating", new RateRequest(stars))).StatusCode);
    }

    [Fact]
    public async Task Comment_lifecycle_with_moderation()
    {
        var id = await AnyRecipeIdAsync();
        var user = await _f.AuthedClientAsync();
        var other = await _f.AuthedClientAsync();
        var admin = await _f.AdminClientAsync();

        var created = await user.PostAsJsonAsync($"/api/recipes/{id}/comments", new AddCommentRequest("I tried this - lovely!", null));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var comment = (await created.Content.ReadFromJsonAsync<CommentDto>())!;

        Assert.Equal(HttpStatusCode.Forbidden, (await other.DeleteAsync($"/api/comments/{comment.Id}")).StatusCode);

        // admin hides it -> public list no longer shows it
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"/api/admin/comments/{comment.Id}/hidden", new SetHiddenRequest(true))).StatusCode);
        var publicList = await _f.CreateClient().GetFromJsonAsync<PagedResult<CommentDto>>($"/api/recipes/{id}/comments?pageSize=50");
        Assert.DoesNotContain(publicList!.Items, c => c.Id == comment.Id);
        var adminList = await admin.GetFromJsonAsync<PagedResult<CommentDto>>($"/api/recipes/{id}/comments?pageSize=50");
        Assert.Contains(adminList!.Items, c => c.Id == comment.Id && c.IsHidden);

        Assert.Equal(HttpStatusCode.NoContent, (await user.DeleteAsync($"/api/comments/{comment.Id}")).StatusCode);
    }

    [Fact]
    public async Task Empty_comment_is_rejected()
    {
        var id = await AnyRecipeIdAsync();
        var c = await _f.AuthedClientAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync($"/api/recipes/{id}/comments", new AddCommentRequest("", null))).StatusCode);
    }

    [Fact]
    public async Task Admin_endpoints_are_forbidden_for_normal_users()
    {
        var user = await _f.AuthedClientAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync("/api/admin/dashboard")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _f.CreateClient().GetAsync("/api/admin/dashboard")).StatusCode);
    }

    [Fact]
    public async Task Admin_dashboard_returns_counts_and_activity()
    {
        var admin = await _f.AdminClientAsync();
        var d = await admin.GetFromJsonAsync<DashboardDto>("/api/admin/dashboard");
        Assert.True(d!.Users >= 2);
        Assert.True(d.Recipes >= 12);
        Assert.NotEmpty(d.LatestActivity);
    }

    [Fact]
    public async Task Admin_can_deactivate_user_who_then_cannot_login_and_can_change_role()
    {
        var admin = await _f.AdminClientAsync();
        var anon = _f.CreateClient();
        var reg = await _f.RegisterAsync(anon, "victim@test.local");

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"/api/admin/users/{reg.User.Id}/active", new SetActiveRequest(false))).StatusCode);
        var login = await anon.PostAsJsonAsync("/api/auth/login", new LoginRequest("victim@test.local", "Passw0rd!x"));
        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(reg.RefreshToken))).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"/api/admin/users/{reg.User.Id}/active", new SetActiveRequest(true))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"/api/admin/users/{reg.User.Id}/role", new SetRoleRequest("Admin"))).StatusCode);
        var users = await admin.GetFromJsonAsync<PagedResult<AdminUserDto>>("/api/admin/users?search=victim");
        Assert.Contains("Admin", users!.Items.Single().Roles);
    }

    [Fact]
    public async Task Admin_cannot_demote_self_and_invalid_role_rejected()
    {
        var admin = await _f.AdminClientAsync();
        var me = (await admin.GetFromJsonAsync<UserDto>("/api/auth/me"))!;
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/admin/users/{me.Id}/role", new SetRoleRequest("User"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/admin/users/{Guid.NewGuid()}/role", new SetRoleRequest("Root"))).StatusCode);
    }

    [Fact]
    public async Task Admin_can_feature_recipe_and_manage_lookups()
    {
        var admin = await _f.AdminClientAsync();
        var id = await AnyRecipeIdAsync();
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"/api/admin/recipes/{id}/featured", new SetFeaturedRequest(true))).StatusCode);

        var created = await admin.PostAsJsonAsync("/api/admin/cuisines", new SaveLookupRequest("Ethiopian"));
        created.EnsureSuccessStatusCode();
        var cuisine = (await created.Content.ReadFromJsonAsync<LookupDto>())!;
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/admin/cuisines", new SaveLookupRequest("Ethiopian"))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/admin/cuisines/{cuisine.Id}")).StatusCode);

        var user = await _f.AuthedClientAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await user.PostAsJsonAsync("/api/admin/categories", new SaveLookupRequest("Nope"))).StatusCode);
    }
}
