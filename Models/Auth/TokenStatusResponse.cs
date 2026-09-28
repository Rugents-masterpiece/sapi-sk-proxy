namespace SapiSkProxy.Models.Auth;

public class TokenStatusResponse
{
    public bool Valid { get; set; }

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = string.Empty;

    [JsonPropertyName("client_id")]
    public string ClientId { get; set; } = string.Empty;

    [JsonPropertyName("issued_at")]
    public DateTime IssuedAt { get; set; }

    [JsonPropertyName("expires_at")]
    public DateTime ExpiresAt { get; set; }

    [JsonPropertyName("expires_in_seconds")]
    public int ExpiresInSeconds { get; set; }

    [JsonPropertyName("should_refresh")]
    public bool ShouldRefresh { get; set; }

    [JsonPropertyName("refresh_recommended_at")]
    public DateTime? RefreshRecommendedAt { get; set; }
}
