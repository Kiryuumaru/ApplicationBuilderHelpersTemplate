using Application.HelloWorld.Services;
using Domain.HelloWorld.Entities;
using Domain.HelloWorld.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;

namespace Application.UnitTests.HelloWorld.Services;

public class HelloWorldServiceTests
{
    private readonly IHelloWorldRepository _repository;
    private readonly IHelloWorldUnitOfWork _unitOfWork;
    private readonly HelloWorldService _service;

    public HelloWorldServiceTests()
    {
        _repository = Substitute.For<IHelloWorldRepository>();
        _unitOfWork = Substitute.For<IHelloWorldUnitOfWork>();
        _service = new HelloWorldService(_repository, _unitOfWork, NullLogger<HelloWorldService>.Instance);
    }

    [Fact]
    public async Task CreateGreetingAsync_WithValidMessage_ReturnsResult()
    {
        var message = "Hello, World!";

        var result = await _service.CreateGreetingAsync(message);

        Assert.NotEqual(Guid.Empty, result.EntityId);
        Assert.Equal(message, result.Message);
        Assert.True(result.CreatedAt <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task CreateGreetingAsync_AddsEntityToRepository()
    {
        var message = "Test message";
        HelloWorldEntity? capturedEntity = null;
        _repository.When(r => r.Add(Arg.Any<HelloWorldEntity>()))
            .Do(callInfo => capturedEntity = callInfo.Arg<HelloWorldEntity>());

        await _service.CreateGreetingAsync(message);

        Assert.NotNull(capturedEntity);
        Assert.Equal(message, capturedEntity!.Message);
    }

    [Fact]
    public async Task CreateGreetingAsync_CommitsUnitOfWork()
    {
        var message = "Test message";

        await _service.CreateGreetingAsync(message);

        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateGreetingAsync_WithCancellationToken_PassesToUnitOfWork()
    {
        var message = "Test message";
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        await _service.CreateGreetingAsync(message, token);

        await _unitOfWork.Received(1).CommitAsync(token);
    }

    [Fact]
    public async Task CreateGreetingAsync_WithNullMessage_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.CreateGreetingAsync(null!));
    }

    [Fact]
    public async Task CreateGreetingAsync_WhenCommitFails_PropagatesException()
    {
        var message = "Test message";
        _unitOfWork.When(u => u.CommitAsync(Arg.Any<CancellationToken>()))
            .Do(_ => throw new InvalidOperationException("Commit failed"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateGreetingAsync(message));
    }
}
