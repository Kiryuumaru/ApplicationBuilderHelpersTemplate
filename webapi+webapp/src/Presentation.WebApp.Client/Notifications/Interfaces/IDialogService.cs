using Presentation.WebApp.Client.Notifications.Models;

namespace Presentation.WebApp.Client.Notifications.Interfaces;

/// <summary>
/// Service for displaying modal dialogs.
/// </summary>
public interface IDialogService
{
    /// <summary>Shows a confirmation dialog and returns the user's choice.</summary>
    Task<bool> ConfirmAsync(
        string message,
        string? title = null,
        string confirmText = "Confirm",
        string cancelText = "Cancel",
        DialogVariant variant = DialogVariant.Default);

    /// <summary>Shows an alert dialog.</summary>
    Task AlertAsync(
        string message,
        string? title = null,
        string okText = "OK");

    /// <summary>Shows a prompt dialog and returns the user's input.</summary>
    Task<string?> PromptAsync(
        string message,
        string? title = null,
        string? defaultValue = null,
        string? placeholder = null,
        string confirmText = "OK",
        string cancelText = "Cancel");
}
