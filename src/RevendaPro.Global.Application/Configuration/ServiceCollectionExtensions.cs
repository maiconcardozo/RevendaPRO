using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using RevendaPro.Global.Application.Authentication.Services;
using RevendaPro.Global.Application.Behaviors;

namespace RevendaPro.Global.Application.Configuration
{
    /// <summary>Registers the Application layer.</summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers MediatR, the FluentValidation validators and the validation behavior.
        /// </summary>
        /// <param name="services">The service collection to add services to.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);

            var assembly = typeof(ServiceCollectionExtensions).Assembly;

            services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
            services.AddValidatorsFromAssembly(assembly);
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

            services.AddScoped<ISessionBuilder, SessionBuilder>();

            return services;
        }
    }
}
