using Platera.Infrastructure;
using Platera.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
    await DbSeeder.SeedAsync(app.Services);

app.UseStaticFiles();
app.MapControllers();
app.Run();

public partial class Program { }
