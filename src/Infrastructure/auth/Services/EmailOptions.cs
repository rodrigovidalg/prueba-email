// Infrastructure/Services/Notifications/EmailOptions.cs
namespace Auth.Infrastructure.Services.Notifications;

public class EmailOptions
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;               // Gmail StartTLS
    public string User { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty; // <-- renombrado
    public string From { get; set; } = string.Empty;
    public bool UseStartTls { get; set; } = true;        // <-- agregado
}
