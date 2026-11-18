using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Platera.Application.Common;
using Platera.Domain.Entities;
using Platera.Infrastructure.Auth;

namespace Platera.Api.Extensions;

public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _http;
    public CurrentUser(IHttpContextAccessor http) { _http = http; }
    private ClaimsPrincipal? P => _http.HttpContext?.User;
    public bool IsAuthenticated => P?.Identity?.IsAuthenticated == true;
    public Guid? UserId => Guid.TryParse(P?.FindFirstValue(ClaimTypes.NameIdentifier) ?? P?.FindFirstValue("sub"), out var id) ? id : null;
    public bool IsAdmin => P?.IsInRole(Roles.Admin) == true;
}

public static class ServiceExtensions
{
    public const string AuthPolicy = "auth";
    public const string FeedbackPolicy = "feedback";
    public const string CorsPolicy = "web";

    public static IServiceCollection AddPlateraAuth(this IServiceCollection services, IConfiguration config)
    {
        var jwt = config.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();
        if (string.IsNullOrWhiteSpace(jwt.Key) || jwt.Key.Length < 32)
            throw new InvalidOperationException("Jwt:Key must be configured (>= 32 chars) via user-secrets or environment variables.");

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.MapInboundClaims = false;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true, ValidIssuer = jwt.Issuer,
                    ValidateAudience = true, ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "name",
                    RoleClaimType = ClaimTypes.Role,
                };
            });
        services.AddAuthorizationBuilder()
            .AddPolicy("Admin", p => p.RequireRole(Roles.Admin));
        return services;
    }

    /// <summary>Own wrapper around the built-in rate limiter: per-IP fixed windows for auth and feedback endpoints.</summary>
    public static IServiceCollection AddPlateraRateLimiting(this IServiceCollection services, IConfiguration config)
    {
        var authLimit = config.GetValue("RateLimiting:AuthPermitLimit", 10);
        var feedbackLimit = config.GetValue("RateLimiting:FeedbackPermitLimit", 20);
        var windowSeconds = config.GetValue("RateLimiting:WindowSeconds", 60);

        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.AddPolicy(AuthPolicy, ctx => Window(ctx, authLimit, windowSeconds));
            o.AddPolicy(FeedbackPolicy, ctx => Window(ctx, feedbackLimit, windowSeconds));
            o.OnRejected = async (ctx, ct) =>
            {
                ctx.HttpContext.Response.ContentType = "application/problem+json";
                await ctx.HttpContext.Response.WriteAsJsonAsync(new Microsoft.AspNetCore.Mvc.ProblemDetails
                {
                    Status = 429, Title = "Too many requests", Detail = "Slow down and try again shortly.",
                }, ct);
            };
        });
        return services;
    }

    private static RateLimitPartition<string> Window(HttpContext ctx, int limit, int seconds) =>
        RateLimitPartition.GetFixedWindowLimiter(
            (ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown") + ":" + ctx.Request.Path,
            _ => new FixedWindowRateLimiterOptions { PermitLimit = limit, Window = TimeSpan.FromSeconds(seconds), QueueLimit = 0 });

    public static IServiceCollection AddPlateraSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = "Platera API", Version = "v1", Description = "Community recipe book" });
            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT",
                Description = "Paste the access token (without the 'Bearer ' prefix).",
            });
            c.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", doc)] = new List<string>(),
            });
        });
        return services;
    }

    public static IServiceCollection AddPlateraCors(this IServiceCollection services, IConfiguration config)
    {
        var origins = config.GetSection("Cors:Origins").Get<string[]>() ?? new[] { "http://localhost:4200" };
        services.AddCors(o => o.AddPolicy(CorsPolicy, p => p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()
            .WithExposedHeaders("X-Correlation-ID")));
        return services;
    }
}
