using Presentation.WebApp.Client.Notifications.Interfaces;
using Presentation.WebApp.Client.Notifications.Models;

namespace Presentation.WebApp.Client.Notifications.Services;

internal class DialogService : IDialogService
{
    public event Action<ConfirmDialogRequest>? OnConfirmRequested;
    public event Action<AlertDialogRequest>? OnAlertRequested;
    public event Action<PromptDialogRequest>? OnPromptRequested;

    public Task<bool> ConfirmAsync(
        string message,
        string? title = null,
        string confirmText = "Confirm",
        string cancelText = "Cancel",
        DialogVariant variant = DialogVariant.Default)
    {
        var tcs = new TaskCompletionSource<bool>();
        var request = new ConfirmDialogRequest(message, title, confirmText, cancelText, variant, tcs);
        OnConfirmRequested?.Invoke(request);
        return tcs.Task;
    }

    public Task AlertAsync(
        string message,
        string? title = null,
        string okText = "OK")
    {
        var tcs = new TaskCompletionSource();
        var request = new AlertDialogRequest(message, title, okText, tcs);
        OnAlertRequested?.Invoke(request);
        return tcs.Task;
    }

    public Task<string?> PromptAsync(
        string message,
        string? title = null,
        string? defaultValue = null,
        string? placeholder = null,
        string confirmText = "OK",
        string cancelText = "Cancel")
    {
        var tcs = new TaskCompletionSource<string?>();
        var request = new PromptDialogRequest(message, title, defaultValue, placeholder, confirmText, cancelText, tcs);
        OnPromptRequested?.Invoke(request);
        return tcs.Task;
    }
}
