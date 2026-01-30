using Domain.HelloWorld.Events;
using Domain.Shared.Models;

namespace Domain.HelloWorld.Entities;

public class HelloWorldEntity : AggregateRoot
{
    public string Message { get; private set; }

    protected HelloWorldEntity(Guid id, string message) : base(id)
    {
        Message = message;
    }

    public static HelloWorldEntity Create(string message)
    {
        var entity = new HelloWorldEntity(
            Guid.NewGuid(),
            message ?? throw new ArgumentNullException(nameof(message)));
        entity.AddDomainEvent(new HelloWorldCreatedEvent(entity.Id, entity.Message));
        return entity;
    }
}
