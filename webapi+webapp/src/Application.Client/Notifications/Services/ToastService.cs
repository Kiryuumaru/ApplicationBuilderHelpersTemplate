using Application.Client.Notifications.Interfaces;
using Application.Client.Notifications.Models;

namespace Application.Client.Notifications.Services;

public class ToastService : IToastService
{
    public event Action<ToastMessage>? OnToastAdded;
    public event Action<Guid>? OnToastRemoved;

    public void Success(string message, string? title = null, int durationMs = 5000)
    {
        AddToast(ToastType.Success, message, title, durationMs);
    }

    public void Error(string message, string? title = null, int durationMs = 5000)
    {
        AddToast(ToastType.Error, message, title, durationMs);
    }

    public void Warning(string message, string? title = null, int durationMs = 5000)
    {
        AddToast(ToastType.Warning, message, title, durationMs);
    }

    public void Info(string message, string? title = null, int durationMs = 5000)
    {
        AddToast(ToastType.Info, message, title, durationMs);
    }

    private void AddToast(ToastType type, string message, string? title, int durationMs)
    {
        var toast = new ToastMessage(
            Id: Guid.NewGuid(),
            Type: type,
            Message: message,
            Title: title,
            DurationMs: durationMs,
            CreatedAt: DateTimeOffset.UtcNow
        );

        OnToastAdded?.Invoke(toast);
    }

    internal void RemoveToast(Guid id)
    {
        OnToastRemoved?.Invoke(id);
    }
}
