using Microsoft.AspNetCore.Mvc;
using Platera.Api.Extensions;
using Platera.Api.Middleware;
using Platera.Application;
using Platera.Infrastructure;
using Platera.Infrastructure.Persistence;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, services, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] [{UserId}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/platera-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14,
        outputTemplate: "{Timestamp:o} [{Level:u3}] [{CorrelationId}] [{UserId}] {Message:lj}{NewLine}{Exception}"));

builder.Services.AddControllers(o => o.Filters.Add<ValidationFilter>())
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.Configure<ApiBehaviorOptions>(o => o.SuppressModelStateInvalidFilter = true);
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddPlateraAuth(builder.Configuration);
builder.Services.AddPlateraRateLimiting(builder.Configuration);
builder.Services.AddPlateraCors(builder.Configuration);
builder.Services.AddPlateraSwagger();

var app = builder.Build();

if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
    await DbSeeder.SeedAsync(app.Services);

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<RequestTimingMiddleware>();
app.UseSerilogRequestLogging(o => o.GetLevel = (_, _, _) => Serilog.Events.LogEventLevel.Debug);

app.UseSwagger();
app.UseSwaggerUI();

app.UseStaticFiles();
app.UseCors(ServiceExtensions.CorsPolicy);
app.UseAuthentication();
app.UseMiddleware<UserLogEnricherMiddleware>();
app.UseRateLimiter();
app.UseAuthorization();
app.MapControllers();
app.Run();

public partial class Program { }
