namespace Presentation.WebApp.Client.Notifications.Models;

/// <summary>
/// Request data for a confirmation dialog.
/// </summary>
/// <param name="Message">The message to display.</param>
/// <param name="Title">Optional dialog title.</param>
/// <param name="ConfirmText">Text for the confirm button.</param>
/// <param name="CancelText">Text for the cancel button.</param>
/// <param name="Variant">Visual styling variant.</param>
/// <param name="TaskCompletionSource">Completion source for the async result.</param>
public record ConfirmDialogRequest(
    string Message,
    string? Title,
    string ConfirmText,
    string CancelText,
    DialogVariant Variant,
    TaskCompletionSource<bool> TaskCompletionSource
);

/// <summary>
/// Request data for an alert dialog.
/// </summary>
/// <param name="Message">The message to display.</param>
/// <param name="Title">Optional dialog title.</param>
/// <param name="OkText">Text for the OK button.</param>
/// <param name="TaskCompletionSource">Completion source for the async result.</param>
public record AlertDialogRequest(
    string Message,
    string? Title,
    string OkText,
    TaskCompletionSource TaskCompletionSource
);

/// <summary>
/// Request data for a prompt dialog.
/// </summary>
/// <param name="Message">The message to display.</param>
/// <param name="Title">Optional dialog title.</param>
/// <param name="DefaultValue">Default value for the input.</param>
/// <param name="Placeholder">Placeholder text for the input.</param>
/// <param name="ConfirmText">Text for the confirm button.</param>
/// <param name="CancelText">Text for the cancel button.</param>
/// <param name="TaskCompletionSource">Completion source for the async result.</param>
public record PromptDialogRequest(
    string Message,
    string? Title,
    string? DefaultValue,
    string? Placeholder,
    string ConfirmText,
    string CancelText,
    TaskCompletionSource<string?> TaskCompletionSource
);
