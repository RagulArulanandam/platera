using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Platera.Application.Abstractions;
using Platera.Domain.Entities;
using Platera.Infrastructure.Persistence;

namespace Platera.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<PlateraDbContext>(o =>
            o.UseSqlServer(config.GetConnectionString("Default"), sql => sql.EnableRetryOnFailure()));
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<PlateraDbContext>());

        services.AddIdentityCore<AppUser>(o =>
            {
                o.Password.RequiredLength = 8;
                o.User.RequireUniqueEmail = true;
            })
            .AddRoles<AppRole>()
            .AddEntityFrameworkStores<PlateraDbContext>();

        return services;
    }
}
