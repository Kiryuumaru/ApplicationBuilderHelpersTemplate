using Application.HelloWorld.Interfaces.Inbound;
using Application.HelloWorld.Interfaces.Outbound;
using Application.HelloWorld.Models;
using Domain.HelloWorld.Entities;
using Microsoft.Extensions.Logging;

namespace Application.HelloWorld.Services;

internal sealed class HelloWorldService(
    IHelloWorldRepository repository,
    IHelloWorldUnitOfWork unitOfWork,
    ILogger<HelloWorldService> logger) : IHelloWorldService
{
    public async Task<HelloWorldResult> CreateGreetingAsync(string message, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Creating greeting with message: {Message}", message);

        // 1. Domain operation - entity raises events internally
        var entity = HelloWorldEntity.Create(message);

        // 2. Save to repository (via Interfaces/Outbound interface)
        repository.Add(entity);

        // 3. Commit transaction - events dispatched post-commit by UnitOfWork
        await unitOfWork.CommitAsync(cancellationToken);

        logger.LogInformation("Greeting created with Id: {EntityId}", entity.Id);

        return new HelloWorldResult(entity.Id, entity.Message, DateTimeOffset.UtcNow);
    }
}
