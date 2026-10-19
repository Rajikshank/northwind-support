namespace SupportDesk.Presentation.Security;

public sealed class JwtSettings
{
    public string SigningKey { get; set; } = "";
    public string Issuer { get; set; } = "northwind-support";
    public string Audience { get; set; } = "northwind-support-web";
    public int LifetimeMinutes { get; set; } = 30;
}
