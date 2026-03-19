using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace jobAgentApi.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(
                Assembly.GetExecutingAssembly());

            cfg.AddOpenBehavior(typeof(
                Behaviors.CorrelationBehavior<,>));

            cfg.AddOpenBehavior(typeof(
                Behaviors.LoggingBehavior<,>));

            cfg.AddOpenBehavior(typeof(
                Behaviors.ValidationBehavior<,>));

            cfg.AddOpenBehavior(typeof(
                Behaviors.PerformanceBehavior<,>));

            cfg.AddOpenBehavior(typeof(
                Behaviors.ExceptionHandlingBehavior<,>));
        });

        services.AddValidatorsFromAssembly(
            Assembly.GetExecutingAssembly());

        return services;
    }
}
