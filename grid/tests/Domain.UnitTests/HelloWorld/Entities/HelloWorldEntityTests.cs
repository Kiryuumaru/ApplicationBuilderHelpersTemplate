using Domain.HelloWorld.Entities;
using Domain.HelloWorld.Events;

namespace Domain.UnitTests.HelloWorld.Entities;

public class HelloWorldEntityTests
{
    [Fact]
    public void Create_WithValidParameters_CreatesEntity()
    {
        var message = "Test message";

        var entity = HelloWorldEntity.Create(message);

        Assert.NotEqual(Guid.Empty, entity.Id);
        Assert.Equal(message, entity.Message);
    }

    [Fact]
    public void Create_WithNullMessage_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => HelloWorldEntity.Create(null!));
    }

    [Fact]
    public void Create_RaisesHelloWorldCreatedEvent()
    {
        var message = "Test message";

        var entity = HelloWorldEntity.Create(message);

        Assert.Single(entity.DomainEvents);
        var domainEvent = Assert.IsType<HelloWorldCreatedEvent>(entity.DomainEvents.First());
        Assert.Equal(entity.Id, domainEvent.EntityId);
        Assert.Equal(message, domainEvent.Message);
    }

    [Fact]
    public void ClearDomainEvents_RemovesAllEvents()
    {
        var entity = HelloWorldEntity.Create("Test");

        entity.ClearDomainEvents();

        Assert.Empty(entity.DomainEvents);
    }
}
