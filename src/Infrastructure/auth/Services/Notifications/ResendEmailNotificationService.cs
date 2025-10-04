using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Auth.Infrastructure.Services.Notifications;

public class ResendEmailNotificationService : INotificationService
{
    private readonly HttpClient _http;
    private readonly ResendOptions _opt;
    private readonly ILogger<ResendEmailNotificationService> _logger;

    private static readonly JsonSerializerOptions _jsonOpts = new(JsonSerializerDefaults.Web);

    public ResendEmailNotificationService(
        HttpClient http,
        IOptions<ResendOptions> opt,
        ILogger<ResendEmailNotificationService> logger)
    {
        _http = http;
        _opt = opt.Value;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_opt.ApiKey))
            throw new InvalidOperationException("Resend.ApiKey no configurado.");
        if (string.IsNullOrWhiteSpace(_opt.From))
            throw new InvalidOperationException("Resend.From no configurado.");

        _http.BaseAddress = new Uri(_opt.BaseUrl.TrimEnd('/'));
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _opt.ApiKey);
    }

    public async Task SendEmailAsync(
        string toEmail,
        string subject,
        string htmlBody,
        (string FileName, byte[] Content, string ContentType)? attachment = null)
    {
        if (string.IsNullOrWhiteSpace(toEmail))
            throw new ArgumentException("El email de destino está vacío.", nameof(toEmail));

        var payload = new Dictionary<string, object?>
        {
            ["from"] = _opt.From,
            ["to"] = new[] { toEmail },
            ["subject"] = subject ?? string.Empty,
            ["html"] = htmlBody ?? string.Empty
        };

        if (attachment.HasValue)
        {
            payload["attachments"] = new[]
            {
                new Dictionary<string, string>
                {
                    ["filename"] = attachment.Value.FileName,
                    ["content"]  = Convert.ToBase64String(attachment.Value.Content) // Resend espera Base64
                }
            };
        }

        var json = JsonSerializer.Serialize(payload, _jsonOpts);
        using var req = new HttpRequestMessage(HttpMethod.Post, "/emails")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        using var res = await _http.SendAsync(req);
        if (!res.IsSuccessStatusCode)
        {
            var body = await res.Content.ReadAsStringAsync();
            _logger.LogError("Resend error {Status}: {Body}", (int)res.StatusCode, body);
            throw new InvalidOperationException($"Resend devolvió {(int)res.StatusCode}: {body}");
        }
    }
}
