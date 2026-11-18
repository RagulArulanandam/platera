using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Platera.Application.Auth;

namespace Platera.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();
        services.AddScoped<IAuthService, AuthService>();
        return services;
    }
}
