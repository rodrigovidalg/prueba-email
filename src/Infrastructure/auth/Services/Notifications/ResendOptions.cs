namespace Auth.Infrastructure.Services.Notifications;

public class ResendOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string From   { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.resend.com";
}
