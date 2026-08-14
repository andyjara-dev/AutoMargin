namespace Remates.Infrastructure.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "automargin";
    public string Audience { get; set; } = "automargin-web";

    /// <summary>
    /// Clave de firma. En desarrollo viene de appsettings; en producción debe llegar por
    /// variable de entorno o gestor de secretos y nunca versionarse.
    /// </summary>
    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 60;
    public int RefreshTokenDays { get; set; } = 14;
}
