using Application.HelloWorld.Interfaces.In;
using Application.HelloWorld.Models;
using Application.Shared.Interfaces;
using Domain.HelloWorld.Entities;
using Microsoft.Extensions.Logging;

namespace Application.HelloWorld.Services;

internal sealed class HelloWorldService(
    IDomainEventDispatcher eventDispatcher,
    ILogger<HelloWorldService> logger) : IHelloWorldService
{
    public async Task<HelloWorldResult> CreateGreetingAsync(string message, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Creating greeting with message: {Message}", message);

        // Domain operation - entity raises events internally
        var entity = HelloWorldEntity.Create(message);

        // In a real app, this would be:
        // 1. Save to repository (via Interfaces/Out interface)
        // 2. Commit transaction (UnitOfWork)
        // 3. Events dispatched by EF Core interceptor after SaveChanges

        // For this demo without persistence, we dispatch events directly
        // This simulates what DomainEventInterceptor would do post-commit
        await eventDispatcher.DispatchAsync(entity.DomainEvents, cancellationToken);
        entity.ClearDomainEvents();

        logger.LogInformation("Greeting created with Id: {EntityId}", entity.Id);

        return new HelloWorldResult(entity.Id, entity.Message, DateTimeOffset.UtcNow);
    }
}
