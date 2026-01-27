namespace Application.HelloWorld.Models;

public sealed record HelloWorldResult(Guid EntityId, string Message, DateTimeOffset CreatedAt);
