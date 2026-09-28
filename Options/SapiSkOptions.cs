namespace SapiSkProxy.Options;

public class SapiSkOptions
{
    public const string SectionName = "SapiSk";

    [ConfigurationKeyName("BaseUrl")]
    public string BaseUrl { get; set; } = "https://www.sapi-sk.sk/sapi";

    [ConfigurationKeyName("ClientId")]
    public string ClientId { get; set; } = string.Empty;

    [ConfigurationKeyName("ClientSecret")]
    public string ClientSecret { get; set; } = string.Empty;

    [ConfigurationKeyName("DefaultParticipantId")]
    public string DefaultParticipantId { get; set; } = "0245:1234567890";

    /// <summary>
    /// How many seconds before expiry to proactively refresh the token. Default 60s.
    /// </summary>
    [ConfigurationKeyName("RefreshAheadSeconds")]
    public int RefreshAheadSeconds { get; set; } = 60;
}
