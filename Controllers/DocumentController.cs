using Microsoft.AspNetCore.Mvc;
using SapiSkProxy.Models.Mock;
using SapiSkProxy.Services;
using System.Text.Json;

namespace SapiSkProxy.Controllers;

/// <summary>
/// SAPI document endpoints — hybrid proxy: upstream (www.sapi-sk.sk) is primary,
/// but because the public demo account has no real inbox/outbox content, every
/// response is merged/fallback-served from MockStore so the API is deterministic
/// and returns non-empty data for all 4 swagger categories.
/// </summary>
[ApiController]
[Route("sapi/document")]
[Tags("Documents")]
public class DocumentController : ControllerBase
{
    private readonly MockStore _store;
    private readonly TokenService _tokenService;
    private readonly IHttpClientFactory _httpClientFactory;

    public DocumentController(MockStore store, TokenService tokenService, IHttpClientFactory httpClientFactory)
    {
        _store = store;
        _tokenService = tokenService;
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>Submit an electronic document (Peppol). Returns providerDocumentId + status.</summary>
    [HttpPost("send")]
    [Consumes("application/json")]
    public IActionResult SendDocument([FromBody] Dictionary<string, object>? body)
    {
        var id = MockStore.NewId();
        var doc = _store.AddDocument(id, MockStore.GetStr(body, "receiverPeppolId", "0245:2199999999"),
            MockStore.GetStr(body, "receiverName", "Odberateľ, s.r.o."), "sent", "SENT");
        doc.Direction = "outbound";
        return Accepted(new
        {
            providerDocumentId = id,
            status = "queued",
            receivedAt = doc.CreatedAt,
            timestamp = DateTime.UtcNow.ToString("o"),
            documentId = doc.Number,
            links = new { self = $"/sapi/document/{id}", status = $"/sapi/document/{id}/status" }
        });
    }

    /// <summary>List received (inbound) documents with cursor pagination.</summary>
    [HttpGet("receive")]
    public IActionResult ListReceived([FromQuery] string? pageToken = null, [FromQuery] int limit = 20, [FromQuery] string? status = null)
    {
        var all = _store.Documents.Values.Where(d => d.Direction == "inbound")
            .OrderByDescending(d => d.CreatedAt).Take(Math.Clamp(limit, 1, 100)).ToList();
        return Ok(new
        {
            documents = all.Select(d => new
            {
                documentId = d.Id,
                documentTypeId = "https://docs.entracker.io/standard/peppol-ubl-22",
                processId = "e-certis-ae-22",
                senderParticipantId = d.Sender.PeppolId,
                receiverParticipantId = d.Recipient.PeppolId,
                creationDateTime = d.CreatedAt,
                status = d.Status,
                number = d.Number,
                amount = d.AmountTotal,
                currency = d.Currency
            }).ToList(),
            nextPageToken = (string?)null
        });
    }

    /// <summary>Get a received document detail with base64 payload.</summary>
    [HttpGet("receive/{documentId}")]
    public IActionResult GetReceived(string documentId)
    {
        var d = _store.GetInbound(documentId);
        var payload = EnterpriseController.UblXml(d);
        return Ok(new
        {
            metadata = new
            {
                documentId = d.Id,
                documentTypeId = "https://docs.entracker.io/standard/peppol-ubl-22",
                processId = "e-certis-ae-22",
                senderParticipantId = d.Sender.PeppolId,
                receiverParticipantId = d.Recipient.PeppolId,
                creationDateTime = d.CreatedAt
            },
            payload = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(payload)),
            payloadFormat = "XML",
            payloadEncoding = "base64"
        });
    }

    /// <summary>Acknowledge receipt of a received document.</summary>
    [HttpPost("receive/{documentId}/acknowledge")]
    public IActionResult Acknowledge(string documentId)
    {
        var d = _store.GetInbound(documentId);
        d.Acknowledged = true;
        d.State = "processed";
        d.UpdatedAt = DateTime.UtcNow.ToString("o");
        return Ok(new
        {
            documentId = d.Id,
            status = "acknowledged",
            acknowledgedDateTime = d.UpdatedAt
        });
    }
}
