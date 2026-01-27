using Domain.HelloWorld.Entities;
using Domain.HelloWorld.Events;

namespace Domain.UnitTests.HelloWorld.Entities;

public class HelloWorldEntityTests
{
    [Fact]
    public void Constructor_WithValidParameters_CreatesEntity()
    {
        var id = Guid.NewGuid();
        var message = "Test message";

        var entity = new HelloWorldEntity(id, message);

        Assert.Equal(id, entity.Id);
        Assert.Equal(message, entity.Message);
    }

    [Fact]
    public void Constructor_WithEmptyId_ThrowsArgumentException()
    {
        var id = Guid.Empty;
        var message = "Test message";

        Assert.Throws<ArgumentException>(() => new HelloWorldEntity(id, message));
    }

    [Fact]
    public void Constructor_WithNullMessage_ThrowsArgumentNullException()
    {
        var id = Guid.NewGuid();

        Assert.Throws<ArgumentNullException>(() => new HelloWorldEntity(id, null!));
    }

    [Fact]
    public void Constructor_RaisesHelloWorldCreatedEvent()
    {
        var id = Guid.NewGuid();
        var message = "Test message";

        var entity = new HelloWorldEntity(id, message);

        Assert.Single(entity.DomainEvents);
        var domainEvent = Assert.IsType<HelloWorldCreatedEvent>(entity.DomainEvents.First());
        Assert.Equal(id, domainEvent.EntityId);
        Assert.Equal(message, domainEvent.Message);
    }

    [Fact]
    public void ClearDomainEvents_RemovesAllEvents()
    {
        var entity = new HelloWorldEntity(Guid.NewGuid(), "Test");

        entity.ClearDomainEvents();

        Assert.Empty(entity.DomainEvents);
    }
}
