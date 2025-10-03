using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Options;
using Auth.Infrastructure.Services.Notifications;
using Microsoft.Extensions.Logging;

public class SmtpEmailNotificationService : INotificationService
{
    private readonly EmailOptions _opt;
    private readonly ILogger<SmtpEmailNotificationService> _logger;

    public SmtpEmailNotificationService(IOptions<EmailOptions> opt, ILogger<SmtpEmailNotificationService> logger)
    {
        _opt = opt.Value;
        _logger = logger;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string htmlBody,
        (string FileName, byte[] Content, string ContentType)? attachment = null)
    {
        if (string.IsNullOrWhiteSpace(_opt.Host)) throw new InvalidOperationException("Email.Host no configurado.");
        if (_opt.Port <= 0) throw new InvalidOperationException("Email.Port inválido.");
        if (string.IsNullOrWhiteSpace(_opt.User)) throw new InvalidOperationException("Email.User no configurado.");
        if (string.IsNullOrWhiteSpace(_opt.Password)) throw new InvalidOperationException("Email.Password no configurado.");
        if (string.IsNullOrWhiteSpace(_opt.From)) throw new InvalidOperationException("Email.From no configurado.");
        if (string.IsNullOrWhiteSpace(toEmail)) throw new ArgumentException("El email de destino está vacío.", nameof(toEmail));

        var msg = new MimeMessage();
        msg.From.Add(MailboxAddress.Parse(_opt.From));
        msg.To.Add(MailboxAddress.Parse(toEmail));
        msg.Subject = subject ?? string.Empty;

        var builder = new BodyBuilder { HtmlBody = htmlBody ?? string.Empty };
        if (attachment.HasValue)
        {
            builder.Attachments.Add(attachment.Value.FileName, attachment.Value.Content,
                                    ContentType.Parse(attachment.Value.ContentType));
        }
        msg.Body = builder.ToMessageBody();

        using var smtp = new SmtpClient();

        var security = _opt.UseStartTls
            ? SecureSocketOptions.StartTls
            : (_opt.Port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.Auto);

        try
        {
            await smtp.ConnectAsync(_opt.Host, _opt.Port, security);
            await smtp.AuthenticateAsync(_opt.User, _opt.Password);
            await smtp.SendAsync(msg);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enviando correo a {To}. Host={Host}:{Port}", toEmail, _opt.Host, _opt.Port);
            throw; // opcional: relanzar o manejar según tu flujo
        }
        finally
        {
            if (smtp.IsConnected) await smtp.DisconnectAsync(true);
        }
    }
}
