namespace Application.Logger.Models;

internal class LogJourney
{
    public required string? ServiceName { get; init; }

    public required string? ServiceActionName { get; init; }

    public required string? ServiceCallerName { get; init; }
}
