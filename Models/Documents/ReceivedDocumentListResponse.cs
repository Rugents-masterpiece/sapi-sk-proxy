namespace SapiSkProxy.Models.Documents;

public class ReceivedDocumentListResponse
{
    public List<DocumentMetadata> Documents { get; set; } = new();

    [JsonPropertyName("nextPageToken")]
    public string? NextPageToken { get; set; }
}
