using Domain.HelloWorld.Events;
using Domain.Shared.Interfaces;
using Domain.Shared.Models;

namespace Domain.HelloWorld.Entities;

public sealed class HelloWorldEntity : Entity, IAggregateRoot
{
    public string Message { get; private set; }

    public HelloWorldEntity(Guid id, string message) : base(id)
    {
        Message = message ?? throw new ArgumentNullException(nameof(message));
        AddDomainEvent(new HelloWorldCreatedEvent(Id, Message));
    }
}
