namespace SapiSkProxy.Models;

public class SapiError
{
    public string Category { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public List<ErrorDetail>? Details { get; set; }

    public bool Retryable { get; set; }

    [JsonPropertyName("correlation_id")]
    public string CorrelationId { get; set; } = string.Empty;
}

public class ErrorDetail
{
    public string? Field { get; set; }
    public string? Issue { get; set; }
    public string? Value { get; set; }
}

public class ErrorResponseEnvelope
{
    public SapiError Error { get; set; } = new();
}
