using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Platera.Application.Auth;
using Platera.Application.Images;
using Platera.Application.Lookups;
using Platera.Application.Recipes;

namespace Platera.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IRecipeService, RecipeService>();
        services.AddScoped<ILookupService, LookupService>();
        services.AddScoped<IImageUploadService, ImageUploadService>();
        services.AddScoped<Platera.Application.Interactions.IInteractionService, Platera.Application.Interactions.InteractionService>();
        services.AddScoped<Platera.Application.Admin.IAdminService, Platera.Application.Admin.AdminService>();
        return services;
    }
}
