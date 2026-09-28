namespace SapiSkProxy.Models.Documents;

public class ReceivedDocumentDetailResponse
{
    public DocumentMetadata Metadata { get; set; } = new();

    public string Payload { get; set; } = string.Empty;

    [JsonPropertyName("payloadFormat")]
    public string PayloadFormat { get; set; } = string.Empty;
}
