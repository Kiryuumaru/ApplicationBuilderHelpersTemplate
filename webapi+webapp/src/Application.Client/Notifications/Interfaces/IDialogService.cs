using Application.Client.Notifications.Models;

namespace Application.Client.Notifications.Interfaces;

public interface IDialogService
{
    Task<bool> ConfirmAsync(
        string message,
        string? title = null,
        string confirmText = "Confirm",
        string cancelText = "Cancel",
        DialogVariant variant = DialogVariant.Default);

    Task AlertAsync(
        string message,
        string? title = null,
        string okText = "OK");

    Task<string?> PromptAsync(
        string message,
        string? title = null,
        string? defaultValue = null,
        string? placeholder = null,
        string confirmText = "OK",
        string cancelText = "Cancel");
}
