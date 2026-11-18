using System.Net;
using System.Net.Http.Json;
using Platera.Application.Auth;
using Platera.Tests.TestSupport;

namespace Platera.Tests;

public class AuthAndMiddlewareTests : IClassFixture<PlateraFactory>
{
    private readonly PlateraFactory _f;
    public AuthAndMiddlewareTests(PlateraFactory f) { _f = f; }

    [Fact]
    public async Task Register_then_login_returns_tokens_and_user_role()
    {
        var c = _f.CreateClient();
        var reg = await _f.RegisterAsync(c, "reg1@test.local");
        Assert.Contains("User", reg.User.Roles);

        var res = await c.PostAsJsonAsync("/api/auth/login", new LoginRequest("reg1@test.local", "Passw0rd!x"));
        res.EnsureSuccessStatusCode();
        var auth = (await res.Content.ReadFromJsonAsync<AuthResponse>())!;
        Assert.False(string.IsNullOrEmpty(auth.AccessToken));
    }

    [Fact]
    public async Task Login_with_bad_password_returns_401_problem_details()
    {
        var c = _f.CreateClient();
        await _f.RegisterAsync(c, "reg2@test.local");
        var res = await c.PostAsJsonAsync("/api/auth/login", new LoginRequest("reg2@test.local", "wrong"));
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        Assert.Equal("application/problem+json", res.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task Register_with_invalid_payload_returns_400_validation_problem()
    {
        var c = _f.CreateClient();
        var res = await c.PostAsJsonAsync("/api/auth/register", new RegisterRequest("not-an-email", "short", ""));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await res.Content.ReadAsStringAsync();
        Assert.Contains("errors", body);
    }

    [Fact]
    public async Task Duplicate_registration_returns_409()
    {
        var c = _f.CreateClient();
        await _f.RegisterAsync(c, "dup@test.local");
        var res = await c.PostAsJsonAsync("/api/auth/register", new RegisterRequest("dup@test.local", "Passw0rd!x", "Dup"));
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task Refresh_rotates_token_and_old_token_is_rejected()
    {
        var c = _f.CreateClient();
        var reg = await _f.RegisterAsync(c, "refresh@test.local");
        var ok = await c.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(reg.RefreshToken));
        ok.EnsureSuccessStatusCode();
        var reuse = await c.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(reg.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
    }

    [Fact]
    public async Task Me_requires_authentication_and_returns_profile()
    {
        var anon = _f.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/auth/me")).StatusCode);

        var authed = await _f.AuthedClientAsync("me@test.local");
        var me = await authed.GetFromJsonAsync<UserDto>("/api/auth/me");
        Assert.Equal("me@test.local", me!.Email);
    }

    [Fact]
    public async Task Seeded_admin_can_login_and_has_admin_role()
    {
        var admin = await _f.AdminClientAsync();
        var me = await admin.GetFromJsonAsync<UserDto>("/api/auth/me");
        Assert.Contains("Admin", me!.Roles);
    }

    [Fact]
    public async Task Correlation_id_is_echoed_or_generated()
    {
        var c = _f.CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        req.Headers.Add("X-Correlation-ID", "abc-123");
        var res = await c.SendAsync(req);
        Assert.Equal("abc-123", res.Headers.GetValues("X-Correlation-ID").Single());

        var res2 = await c.GetAsync("/api/auth/me");
        Assert.False(string.IsNullOrWhiteSpace(res2.Headers.GetValues("X-Correlation-ID").Single()));
        Assert.True(res2.Headers.Contains("X-Response-Time-ms"));
    }
}
