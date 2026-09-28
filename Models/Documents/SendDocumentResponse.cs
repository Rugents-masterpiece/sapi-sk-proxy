namespace SapiSkProxy.Models.Documents;

public class SendDocumentResponse
{
    [JsonPropertyName("providerDocumentId")]
    public string ProviderDocumentId { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("receivedAt")]
    public DateTime? ReceivedAt { get; set; }

    public DateTime Timestamp { get; set; }
}
