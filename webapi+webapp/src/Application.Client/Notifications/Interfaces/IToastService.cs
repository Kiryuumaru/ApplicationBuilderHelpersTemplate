using Application.Client.Notifications.Models;

namespace Application.Client.Notifications.Interfaces;

public interface IToastService
{
    void Success(string message, string? title = null, int durationMs = 5000);
    void Error(string message, string? title = null, int durationMs = 5000);
    void Warning(string message, string? title = null, int durationMs = 5000);
    void Info(string message, string? title = null, int durationMs = 5000);

    event Action<ToastMessage>? OnToastAdded;
    event Action<Guid>? OnToastRemoved;
}
