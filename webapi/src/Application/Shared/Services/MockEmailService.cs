using Microsoft.Extensions.Logging;

namespace Application.Shared.Services;

/// <summary>
/// Mock email service that logs emails instead of sending them.
/// Use this for development/testing. Replace with a real implementation in production.
/// </summary>
public class MockEmailService : Interfaces.IEmailService
{
    private readonly ILogger<MockEmailService> _logger;

    public MockEmailService(ILogger<MockEmailService> logger)
    {
        _logger = logger;
    }

    public Task SendPasswordResetLinkAsync(string email, string resetLink, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[MOCK EMAIL] Password Reset Link sent to {Email}\n" +
            "Reset Link: {ResetLink}",
            email, resetLink);

        return Task.CompletedTask;
    }

    public Task SendPasswordResetCodeAsync(string email, string resetCode, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[MOCK EMAIL] Password Reset Code sent to {Email}\n" +
            "Reset Code: {ResetCode}",
            email, resetCode);

        return Task.CompletedTask;
    }

    public Task SendEmailConfirmationLinkAsync(string email, string confirmationLink, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[MOCK EMAIL] Email Confirmation Link sent to {Email}\n" +
            "Confirmation Link: {ConfirmationLink}",
            email, confirmationLink);

        return Task.CompletedTask;
    }
}
