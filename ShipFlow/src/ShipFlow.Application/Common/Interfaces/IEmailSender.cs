namespace ShipFlow.Application.Common.Interfaces;

/// <summary>An outbound email message with HTML and optional plain-text body.</summary>
public sealed record EmailMessage(string To, string ToName, string Subject, string HtmlBody, string? PlainBody = null);

/// <summary>
/// Delivers email messages. The implementation is in Infrastructure (SMTP). Application code never
/// reads SMTP credentials; failures are logged and re-thrown so the caller can decide to swallow or surface them.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
