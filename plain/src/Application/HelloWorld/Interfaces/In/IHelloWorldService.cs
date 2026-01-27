using Application.HelloWorld.Models;

namespace Application.HelloWorld.Interfaces.In;

/// <summary>
/// Application service for HelloWorld operations.
/// Called by Presentation layer.
/// </summary>
public interface IHelloWorldService
{
    /// <summary>
    /// Creates a new HelloWorld greeting.
    /// </summary>
    /// <param name="message">The greeting message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the operation.</returns>
    Task<HelloWorldResult> CreateGreetingAsync(string message, CancellationToken cancellationToken = default);
}
