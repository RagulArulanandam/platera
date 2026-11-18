using System.Diagnostics;
using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Platera.Application.Common;
using Serilog.Context;

namespace Platera.Api.Middleware;

/// <summary>Reads or creates X-Correlation-ID, echoes it back and pushes it to the Serilog LogContext.</summary>
public class CorrelationIdMiddleware
{
    public const string Header = "X-Correlation-ID";
    private readonly RequestDelegate _next;
    public CorrelationIdMiddleware(RequestDelegate next) { _next = next; }

    public async Task InvokeAsync(HttpContext ctx)
    {
        var id = ctx.Request.Headers[Header].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(id) || id.Length > 64) id = Guid.NewGuid().ToString("N");
        ctx.TraceIdentifier = id;
        ctx.Response.OnStarting(() => { ctx.Response.Headers[Header] = id; return Task.CompletedTask; });
        using (LogContext.PushProperty("CorrelationId", id))
            await _next(ctx);
    }
}

/// <summary>Pushes the authenticated user id to the LogContext. Must run after authentication.</summary>
public class UserLogEnricherMiddleware
{
    private readonly RequestDelegate _next;
    public UserLogEnricherMiddleware(RequestDelegate next) { _next = next; }

    public async Task InvokeAsync(HttpContext ctx)
    {
        var uid = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? ctx.User.FindFirstValue("sub") ?? "anonymous";
        using (LogContext.PushProperty("UserId", uid))
            await _next(ctx);
    }
}

/// <summary>Logs method, path, status and elapsed time for every request, and adds X-Response-Time-ms.</summary>
public class RequestTimingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestTimingMiddleware> _log;
    public RequestTimingMiddleware(RequestDelegate next, ILogger<RequestTimingMiddleware> log) { _next = next; _log = log; }

    public async Task InvokeAsync(HttpContext ctx)
    {
        var sw = Stopwatch.StartNew();
        ctx.Response.OnStarting(() => { ctx.Response.Headers["X-Response-Time-ms"] = sw.ElapsedMilliseconds.ToString(); return Task.CompletedTask; });
        try { await _next(ctx); }
        finally
        {
            sw.Stop();
            var level = sw.ElapsedMilliseconds > 1000 ? LogLevel.Warning : LogLevel.Information;
            _log.Log(level, "HTTP {Method} {Path} responded {Status} in {Elapsed} ms",
                ctx.Request.Method, ctx.Request.Path.Value, ctx.Response.StatusCode, sw.ElapsedMilliseconds);
        }
    }
}

/// <summary>Maps exceptions to RFC 7807 ProblemDetails responses.</summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _log;
    private readonly IHostEnvironment _env;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> log, IHostEnvironment env)
    { _next = next; _log = log; _env = env; }

    public async Task InvokeAsync(HttpContext ctx)
    {
        try { await _next(ctx); }
        catch (OperationCanceledException) when (ctx.RequestAborted.IsCancellationRequested)
        {
            ctx.Response.StatusCode = 499;
        }
        catch (Exception ex)
        {
            if (ctx.Response.HasStarted) throw;
            var problem = Map(ex);
            if (problem.Status >= 500) _log.LogError(ex, "Unhandled exception");
            else _log.LogWarning("Handled {Type}: {Message}", ex.GetType().Name, ex.Message);

            problem.Instance = ctx.Request.Path;
            problem.Extensions["correlationId"] = ctx.TraceIdentifier;
            ctx.Response.Clear();
            ctx.Response.StatusCode = problem.Status ?? 500;
            ctx.Response.ContentType = "application/problem+json";
            await ctx.Response.WriteAsJsonAsync(problem, problem.GetType(), options: null, contentType: "application/problem+json");
        }
    }

    private ProblemDetails Map(Exception ex) => ex switch
    {
        ValidationException v => new ValidationProblemDetails(
            v.Errors.GroupBy(e => e.PropertyName).ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()))
        { Status = 400, Title = "Validation failed" },
        NotFoundException => Simple(404, "Not found", ex),
        ForbiddenException => Simple(403, "Forbidden", ex),
        ConflictException => Simple(409, "Conflict", ex),
        UnauthorizedAppException => Simple(401, "Unauthorized", ex),
        BadRequestException => Simple(400, "Bad request", ex),
        _ => new ProblemDetails
        {
            Status = 500,
            Title = "An unexpected error occurred",
            Detail = _env.IsDevelopment() ? ex.Message : "Please try again later or contact support with the correlation id.",
        },
    };

    private static ProblemDetails Simple(int status, string title, Exception ex) =>
        new() { Status = status, Title = title, Detail = ex.Message };
}
