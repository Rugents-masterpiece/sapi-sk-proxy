using Microsoft.AspNetCore.Mvc;
using SapiSkProxy.Models;
using SapiSkProxy.Models.Documents;
using SapiSkProxy.Services;

namespace SapiSkProxy.Controllers;

[ApiController]
[Route("sapi/document")]
[Tags("Documents")]
public class DocumentController : ControllerBase
{
    private readonly HttpClient _upstreamClient;
    private readonly TokenService _tokenService;
    private readonly ILogger<DocumentController> _logger;

    public DocumentController(IHttpClientFactory httpClientFactory, TokenService tokenService, ILogger<DocumentController> logger)
    {
        _upstreamClient = httpClientFactory.CreateClient("SapiSkUpstream");
        _tokenService = tokenService;
        _logger = logger;
    }

    /// <summary>Submit a single electronic business document for processing and delivery via Peppol network.</summary>
    [HttpPost("send")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(SendDocumentResponse), 202)]
    [ProducesResponseType(typeof(ErrorResponseEnvelope), 400)]
    [ProducesResponseType(typeof(ErrorResponseEnvelope), 401)]
    [ProducesResponseType(typeof(ErrorResponseEnvelope), 409)]
    public async Task<IActionResult> SendDocument([FromBody] SendDocumentRequest request)
    {
        try
        {
            var accessToken = await _tokenService.GetAccessTokenAsync();
            var participantId = Request.Headers.TryGetValue("X-Peppol-Participant-Id", out var pid) ? pid.ToString() : "0245:1234567890";

            var requestMsg = new HttpRequestMessage(HttpMethod.Post, "document/send");
            requestMsg.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            requestMsg.Headers.TryAddWithoutValidation("X-Peppol-Participant-Id", participantId);

            if (Request.Headers.TryGetValue("Idempotency-Key", out var ik))
                requestMsg.Headers.TryAddWithoutValidation("Idempotency-Key", new[] { ik.ToString() });

            requestMsg.Content = new StringContent(JsonSerializer.Serialize(request), System.Text.Encoding.UTF8, "application/json");

            var response = await _upstreamClient.SendAsync(requestMsg);
            var responseText = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                var error = JsonSerializer.Deserialize<ErrorResponseEnvelope>(responseText);
                return StatusCode((int)response.StatusCode, error ?? new ErrorResponseEnvelope { Error = new SapiError { Message = responseText } });
            }

            return StatusCode((int)response.StatusCode, JsonSerializer.Deserialize<SendDocumentResponse>(responseText));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error proxying send document request");
            return ProxyError(ex);
        }
    }

    /// <summary>Retrieve a cursor-paginated list of documents received via Peppol network.</summary>
    [HttpGet("receive")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ReceivedDocumentListResponse), 200)]
    [ProducesResponseType(typeof(ErrorResponseEnvelope), 401)]
    public async Task<IActionResult> ListReceivedDocuments(
        [FromQuery] string? pageToken = null,
        [FromQuery] int limit = 20,
        [FromQuery] string? status = null)
    {
        try
        {
            var accessToken = await _tokenService.GetAccessTokenAsync();
            limit = Math.Clamp(limit, 1, 100);

            var queryParams = new List<string>();
            if (!string.IsNullOrEmpty(pageToken)) queryParams.Add($"pageToken={Uri.EscapeDataString(pageToken!)}");
            queryParams.Add($"limit={limit}");
            if (!string.IsNullOrEmpty(status)) queryParams.Add($"status={Uri.EscapeDataString(status!)}");
            var queryString = string.Join("&", queryParams);
            var url = $"document/receive{(!string.IsNullOrEmpty(queryString) ? "?" + queryString : "")}";

            var requestMsg = new HttpRequestMessage(HttpMethod.Get, url);
            requestMsg.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            requestMsg.Headers.TryAddWithoutValidation("X-Peppol-Participant-Id", "0245:1234567890");

            var response = await _upstreamClient.SendAsync(requestMsg);
            var responseText = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                var error = JsonSerializer.Deserialize<ErrorResponseEnvelope>(responseText);
                return StatusCode((int)response.StatusCode, error ?? new ErrorResponseEnvelope { Error = new SapiError { Message = responseText } });
            }

            return Ok(JsonSerializer.Deserialize<ReceivedDocumentListResponse>(responseText));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error proxying list received documents request");
            return ProxyError(ex);
        }
    }

    /// <summary>Retrieve the full details and content of a specific received document.</summary>
    [HttpGet("receive/{documentId}")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ReceivedDocumentDetailResponse), 200)]
    [ProducesResponseType(typeof(ErrorResponseEnvelope), 404)]
    public async Task<IActionResult> GetReceivedDocument([FromRoute] string documentId)
    {
        try
        {
            var accessToken = await _tokenService.GetAccessTokenAsync();

            var requestMsg = new HttpRequestMessage(HttpMethod.Get, $"document/receive/{Uri.EscapeDataString(documentId)}");
            requestMsg.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            requestMsg.Headers.TryAddWithoutValidation("X-Peppol-Participant-Id", "0245:1234567890");

            var response = await _upstreamClient.SendAsync(requestMsg);
            var responseText = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                var error = JsonSerializer.Deserialize<ErrorResponseEnvelope>(responseText);
                return StatusCode((int)response.StatusCode, error ?? new ErrorResponseEnvelope { Error = new SapiError { Message = responseText } });
            }

            return Ok(JsonSerializer.Deserialize<ReceivedDocumentDetailResponse>(responseText));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error proxying get received document request");
            return ProxyError(ex);
        }
    }

    /// <summary>Acknowledge document receipt. This operation is idempotent.</summary>
    [HttpPost("receive/{documentId}/acknowledge")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(AcknowledgeResponse), 200)]
    [ProducesResponseType(typeof(ErrorResponseEnvelope), 404)]
    public async Task<IActionResult> AcknowledgeDocument([FromRoute] string documentId)
    {
        try
        {
            var accessToken = await _tokenService.GetAccessTokenAsync();

            var requestMsg = new HttpRequestMessage(HttpMethod.Post, $"document/receive/{Uri.EscapeDataString(documentId)}/acknowledge");
            requestMsg.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            requestMsg.Headers.TryAddWithoutValidation("X-Peppol-Participant-Id", "0245:1234567890");

            var response = await _upstreamClient.SendAsync(requestMsg);
            var responseText = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                var error = JsonSerializer.Deserialize<ErrorResponseEnvelope>(responseText);
                return StatusCode((int)response.StatusCode, error ?? new ErrorResponseEnvelope { Error = new SapiError { Message = responseText } });
            }

            return Ok(JsonSerializer.Deserialize<AcknowledgeResponse>(responseText));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error proxying acknowledge document request");
            return ProxyError(ex);
        }
    }

    private IActionResult ProxyError(Exception ex)
    {
        return StatusCode(500, new ErrorResponseEnvelope
        {
            Error = new SapiError
            {
                Category = "PROCESSING",
                Code = "SAPI-PROC-001",
                Message = "Internal error proxying to SAPI-SK",
                Retryable = true,
                CorrelationId = Guid.NewGuid().ToString()
            }
        });
    }
}
