using Application.Shared.Models;
using Domain.HelloWorld.Events;
using Microsoft.Extensions.Logging;

namespace Application.HelloWorld.EventHandlers;

internal sealed class SendNotificationHandler(ILogger<SendNotificationHandler> logger) : DomainEventHandler<HelloWorldCreatedEvent>
{
    public override ValueTask HandleAsync(HelloWorldCreatedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        // Simulate sending a notification (email, push, etc.)
        logger.LogInformation("[SendNotificationHandler] Notification sent for greeting: EntityId={EntityId}",
            domainEvent.EntityId);

        return ValueTask.CompletedTask;
    }
}
