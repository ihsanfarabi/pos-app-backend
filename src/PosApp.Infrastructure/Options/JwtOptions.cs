namespace PosApp.Infrastructure.Options;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenTtlMinutes { get; set; } = 15;
}

public sealed class RefreshJwtOptions
{
    public const string SectionName = "RefreshJwt";

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public string SigningKey { get; set; } = string.Empty;

    public int TtlDays { get; set; } = 7;
}
