namespace SapiSkProxy.Models.Documents;

public class SendDocumentRequest
{
    public DocumentMetadata Metadata { get; set; } = new();

    public string Payload { get; set; } = string.Empty;

    [JsonPropertyName("payloadFormat")]
    public string PayloadFormat { get; set; } = "XML";

    [JsonPropertyName("payloadEncoding")]
    public string? PayloadEncoding { get; set; }

    public string? Checksum { get; set; }
}
