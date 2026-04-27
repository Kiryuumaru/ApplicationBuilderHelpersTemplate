using Application.Shared.Models;
using Domain.HelloWorld.Events;
using Microsoft.Extensions.Logging;

namespace Application.HelloWorld.EventHandlers;

internal sealed class RecordAnalyticsHandler(ILogger<RecordAnalyticsHandler> logger) : DomainEventHandler<HelloWorldCreatedEvent>
{
    public override ValueTask HandleAsync(HelloWorldCreatedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        // Simulate recording analytics/metrics
        logger.LogInformation("[RecordAnalyticsHandler] Analytics recorded: EntityId={EntityId}, MessageLength={Length}",
            domainEvent.EntityId,
            domainEvent.Message.Length);

        return ValueTask.CompletedTask;
    }
}
