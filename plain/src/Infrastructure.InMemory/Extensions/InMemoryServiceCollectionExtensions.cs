using Infrastructure.InMemory.Adapters;
using Infrastructure.InMemory.Interfaces;
using Infrastructure.InMemory.Repositories;
using Application.HelloWorld.Interfaces.Outbound;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.InMemory.Extensions;

internal static class InMemoryServiceCollectionExtensions
{
    internal static IServiceCollection AddInMemoryServices(this IServiceCollection services)
    {
        // HelloWorld feature UnitOfWork
        services.AddScoped<IHelloWorldUnitOfWork, InMemoryHelloWorldUnitOfWork>();

        // Repositories (registered as both the Application interface and the internal trackable interface)
        services.AddScoped<InMemoryHelloWorldRepository>();
        services.AddScoped<IHelloWorldRepository>(sp => sp.GetRequiredService<InMemoryHelloWorldRepository>());
        services.AddScoped<ITrackableRepository>(sp => sp.GetRequiredService<InMemoryHelloWorldRepository>());

        return services;
    }
}
