namespace SapiSkProxy.Models.Documents;

public class DocumentMetadata
{
    [JsonPropertyName("documentId")]
    public string DocumentId { get; set; } = string.Empty;

    [JsonPropertyName("documentTypeId")]
    public string DocumentTypeId { get; set; } = string.Empty;

    public string ProcessId { get; set; } = string.Empty;

    [JsonPropertyName("senderParticipantId")]
    public string SenderParticipantId { get; set; } = string.Empty;

    [JsonPropertyName("receiverParticipantId")]
    public string ReceiverParticipantId { get; set; } = string.Empty;

    [JsonPropertyName("creationDateTime")]
    public DateTime CreationDateTime { get; set; }
}
