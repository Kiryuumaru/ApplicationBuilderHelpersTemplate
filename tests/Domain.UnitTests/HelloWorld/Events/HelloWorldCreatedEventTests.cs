using Domain.HelloWorld.Events;
using Domain.Shared.Models;

namespace Domain.UnitTests.HelloWorld.Events;

public class HelloWorldCreatedEventTests
{
    [Fact]
    public void Constructor_WithValidParameters_CreatesEvent()
    {
        var entityId = Guid.NewGuid();
        var message = "Test message";

        var domainEvent = new HelloWorldCreatedEvent(entityId, message);

        Assert.Equal(entityId, domainEvent.EntityId);
        Assert.Equal(message, domainEvent.Message);
    }

    [Fact]
    public void Event_HasUniqueId()
    {
        var event1 = new HelloWorldCreatedEvent(Guid.NewGuid(), "Test 1");
        var event2 = new HelloWorldCreatedEvent(Guid.NewGuid(), "Test 2");

        Assert.NotEqual(event1.Id, event2.Id);
    }

    [Fact]
    public void Event_HasOccurredOnTimestamp()
    {
        var beforeCreation = DateTimeOffset.UtcNow;
        var domainEvent = new HelloWorldCreatedEvent(Guid.NewGuid(), "Test");
        var afterCreation = DateTimeOffset.UtcNow;

        Assert.True(domainEvent.OccurredOn >= beforeCreation);
        Assert.True(domainEvent.OccurredOn <= afterCreation);
    }

    [Fact]
    public void Event_InheritsFromDomainEvent()
    {
        var domainEvent = new HelloWorldCreatedEvent(Guid.NewGuid(), "Test");

        Assert.IsAssignableFrom<DomainEvent>(domainEvent);
    }
}
