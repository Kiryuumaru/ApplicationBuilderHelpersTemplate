using Application.Client.Notifications.Models;

namespace Application.Client.Notifications.Services;

public record ConfirmDialogRequest(
    string Message,
    string? Title,
    string ConfirmText,
    string CancelText,
    DialogVariant Variant,
    TaskCompletionSource<bool> TaskCompletionSource
);

public record AlertDialogRequest(
    string Message,
    string? Title,
    string OkText,
    TaskCompletionSource TaskCompletionSource
);

public record PromptDialogRequest(
    string Message,
    string? Title,
    string? DefaultValue,
    string? Placeholder,
    string ConfirmText,
    string CancelText,
    TaskCompletionSource<string?> TaskCompletionSource
);
