namespace Application.Client.Notifications.Models;

public record ToastMessage(
    Guid Id,
    ToastType Type,
    string Message,
    string? Title,
    int DurationMs,
    DateTimeOffset CreatedAt
);
