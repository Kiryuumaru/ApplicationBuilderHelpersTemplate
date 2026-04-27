using Domain.Shared.Models;

namespace Domain.HelloWorld.Events;

public sealed record HelloWorldCreatedEvent(Guid EntityId, string Message) : DomainEvent;
