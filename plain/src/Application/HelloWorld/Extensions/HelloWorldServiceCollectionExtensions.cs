using Application.HelloWorld.EventHandlers;
using Application.HelloWorld.Interfaces.In;
using Application.HelloWorld.Services;
using Application.Shared.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Application.HelloWorld.Extensions;

internal static class HelloWorldServiceCollectionExtensions
{
    internal static IServiceCollection AddHelloWorldServices(this IServiceCollection services)
    {
        // Application service (Interfaces/In)
        services.AddScoped<IHelloWorldService, HelloWorldService>();

        // Event handlers - all registered independently, executed in parallel
        services.AddDomainEventHandler<LogGreetingHandler>();
        services.AddDomainEventHandler<SendNotificationHandler>();
        services.AddDomainEventHandler<RecordAnalyticsHandler>();

        return services;
    }
}
