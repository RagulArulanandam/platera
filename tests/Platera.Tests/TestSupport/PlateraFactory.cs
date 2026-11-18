using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Platera.Application.Auth;
using Platera.Infrastructure.Persistence;

namespace Platera.Tests.TestSupport;

public class PlateraFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = "platera-" + Guid.NewGuid();
    public const string AdminEmail = "admin@platera.local";
    public const string AdminPassword = "Admin@12345!";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:MigrateOnStartup"] = "false",
            ["RateLimiting:AuthPermitLimit"] = "1000",
            ["RateLimiting:FeedbackPermitLimit"] = "1000",
            ["Seed:AdminEmail"] = AdminEmail,
            ["Storage:UploadsPath"] = Path.Combine(Path.GetTempPath(), "platera-tests-uploads"),
            ["Seed:AdminPassword"] = AdminPassword,
        }));
        builder.ConfigureServices(services =>
        {
            foreach (var d in services.Where(d =>
                         d.ServiceType == typeof(DbContextOptions<PlateraDbContext>) ||
                         d.ServiceType == typeof(DbContextOptions) ||
                         (d.ServiceType.IsGenericType && d.ServiceType.GenericTypeArguments.Contains(typeof(PlateraDbContext))
                          && d.ServiceType != typeof(PlateraDbContext))).ToList())
                services.Remove(d);
            services.AddDbContext<PlateraDbContext>(o => o.UseInMemoryDatabase(_dbName));
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        DbSeeder.SeedAsync(host.Services).GetAwaiter().GetResult();
        return host;
    }

    public async Task<AuthResponse> RegisterAsync(HttpClient client, string? email = null, string password = "Passw0rd!x")
    {
        email ??= $"user{Guid.NewGuid():N}@test.local";
        var res = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, password, "Test User"));
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    public async Task<HttpClient> AuthedClientAsync(string? email = null)
    {
        var c = CreateClient();
        var auth = await RegisterAsync(c, email);
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return c;
    }

    public async Task<HttpClient> AdminClientAsync()
    {
        var c = CreateClient();
        var res = await c.PostAsJsonAsync("/api/auth/login", new LoginRequest(AdminEmail, AdminPassword));
        res.EnsureSuccessStatusCode();
        var auth = (await res.Content.ReadFromJsonAsync<AuthResponse>())!;
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return c;
    }
}
