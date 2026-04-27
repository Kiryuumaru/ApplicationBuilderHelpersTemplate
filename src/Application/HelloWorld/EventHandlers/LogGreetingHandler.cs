using Application.Shared.Models;
using Domain.HelloWorld.Events;
using Microsoft.Extensions.Logging;

namespace Application.HelloWorld.EventHandlers;

internal sealed class LogGreetingHandler(ILogger<LogGreetingHandler> logger) : DomainEventHandler<HelloWorldCreatedEvent>
{
    public override ValueTask HandleAsync(HelloWorldCreatedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("[LogGreetingHandler] Greeting logged: EntityId={EntityId}, Message=\"{Message}\"",
            domainEvent.EntityId,
            domainEvent.Message);

        return ValueTask.CompletedTask;
    }
}
