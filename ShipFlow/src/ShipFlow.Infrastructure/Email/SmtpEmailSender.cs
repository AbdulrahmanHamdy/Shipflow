using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShipFlow.Application.Common.Interfaces;

namespace ShipFlow.Infrastructure.Email;

public sealed class EmailSettings
{
    public const string SectionName = "EmailSettings";

    public string SmtpHost { get; set; } = string.Empty;

    public int SmtpPort { get; set; } = 587;

    public string SenderName { get; set; } = "ShipFlow";

    public string SenderEmail { get; set; } = string.Empty;

    public string SenderPassword { get; set; } = string.Empty;

    public bool EnableSsl { get; set; } = true;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(SmtpHost) &&
        !string.IsNullOrWhiteSpace(SenderEmail);
}

public sealed partial class SmtpEmailSender : IEmailSender
{
    private readonly IOptions<EmailSettings> _settings;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<EmailSettings> settings, ILogger<SmtpEmailSender> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var settings = _settings.Value;
        if (!settings.IsConfigured)
        {
            LogSkipped(message.Subject, message.To);
            return;
        }

        try
        {
            using var client = new SmtpClient(settings.SmtpHost, settings.SmtpPort)
            {
                EnableSsl = settings.EnableSsl,
                Credentials = new NetworkCredential(settings.SenderEmail, settings.SenderPassword)
            };

            using var mail = new MailMessage
            {
                From = new MailAddress(settings.SenderEmail, settings.SenderName),
                Subject = message.Subject,
                Body = message.HtmlBody,
                IsBodyHtml = true
            };

            mail.To.Add(new MailAddress(message.To, message.ToName));

            if (!string.IsNullOrWhiteSpace(message.PlainBody))
            {
                mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(message.PlainBody, null, "text/plain"));
            }

            await client.SendMailAsync(mail, cancellationToken);
            LogSent(message.Subject);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Do not log credentials or sensitive data
            LogFailed(ex, message.Subject);
            throw;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Email delivery skipped: SMTP is not configured. Would send '{Subject}' to '{To}'.")]
    private partial void LogSkipped(string subject, string to);

    [LoggerMessage(Level = LogLevel.Information, Message = "Email successfully sent to recipient for subject '{Subject}'.")]
    private partial void LogSent(string subject);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to deliver email to recipient for subject '{Subject}'.")]
    private partial void LogFailed(Exception ex, string subject);
}
