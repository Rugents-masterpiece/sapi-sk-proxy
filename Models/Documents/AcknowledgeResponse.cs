namespace SapiSkProxy.Models.Documents;

public class AcknowledgeResponse
{
    [JsonPropertyName("documentId")]
    public string DocumentId { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("acknowledgedDateTime")]
    public DateTime AcknowledgedDateTime { get; set; }
}
