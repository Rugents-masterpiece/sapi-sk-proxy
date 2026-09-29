using Microsoft.AspNetCore.Mvc;
using SapiSkProxy.Models.Mock;
using SapiSkProxy.Services;

namespace SapiSkProxy.Controllers;

/// <summary>
/// Connector API — business JSON documents, inbox, webhook, diagnostics.
/// Mock-backed with generated data; paths per ePošťák Connector OpenAPI contract.
/// </summary>
[ApiController]
[Route("api/v1/connector")]
[Tags("Connector")]
public class ConnectorController : ControllerBase
{
    private readonly MockStore _store;
    public ConnectorController(MockStore store) => _store = store;

    // ── Documents ───────────────────────────────────────────────

    /// <summary>List business documents (customerRef required).</summary>
    [HttpGet("documents")]
    public IActionResult ListDocuments([FromQuery] string? customerRef = null, [FromQuery] int limit = 20, [FromQuery] string? cursor = null)
    {
        var docs = _store.Documents.Values.Where(d => d.Direction == "inbound").Take(Math.Min(limit, 100))
            .Select(d => (object)new
            {
                id = d.Id,
                customerRef = d.CustomerRef,
                externalId = (string?)null,
                direction = d.Direction,
                type = d.Type,
                number = d.Number,
                state = d.State,
                currency = d.Currency,
                amounts = new { d.AmountWithoutTax, d.AmountTax, d.AmountTotal, due = d.AmountTotal },
                sender = d.Sender,
                recipient = d.Recipient,
                issueDate = d.IssueDate,
                dueDate = d.DueDate,
                processedAt = (string?)null,
                processedReference = (string?)null,
                response = (object?)null,
                createdAt = d.CreatedAt,
                updatedAt = d.UpdatedAt,
                links = new
                {
                    self = $"/api/v1/connector/documents/{d.Id}?customerRef={d.CustomerRef}",
                    evidence = $"/api/v1/connector/documents/{d.Id}/evidence?customerRef={d.CustomerRef}",
                    acknowledge = $"/api/v1/connector/documents/{d.Id}/acknowledge?customerRef={d.CustomerRef}"
                }
            }).ToList();
        return Ok(new { documents = docs, nextCursor = "eyJ2Ijoi" + Guid.NewGuid().ToString("N")[..8], hasMore = true });
    }

    /// <summary>Detail of a business document.</summary>
    [HttpGet("documents/{documentId}")]
    public IActionResult DocumentDetail(string documentId, [FromQuery] string? customerRef = null)
    {
        var d = _store.Documents.TryGetValue(documentId, out var f) ? f : _store.Documents.Values.First();
        return Ok(ShapeDocument(d));
    }

    /// <summary>Create (stage) a business document from JSON.</summary>
    [HttpPost("documents")]
    public IActionResult CreateDocument([FromBody] Dictionary<string, object>? body)
    {
        var doc = BodyToDoc(body, staged: true);
        _store.Documents[doc.Id] = doc;
        AddEvent(doc, "document.received", "received");
        return StatusCode(201, ShapeDocument(doc, state: "received"));
    }

    /// <summary>Send a staged business document.</summary>
    [HttpPost("documents/{documentId}/send")]
    public IActionResult SendDocument(string documentId)
    {
        if (_store.Documents.TryGetValue(documentId, out var d))
        {
            d.State = "sent"; d.Status = "SENT"; d.Direction = "outbound";
            d.UpdatedAt = DateTime.UtcNow.ToString("o");
            AddEvent(d, "document.sent", "sent");
            return Ok(ShapeDocument(d, state: "sent"));
        }
        var doc = NewSentDoc();
        return StatusCode(202, ShapeDocument(doc, state: "queued"));
    }

    /// <summary>Cancel a staged document.</summary>
    [HttpPost("documents/{documentId}/cancel")]
    public IActionResult CancelDocument(string documentId)
    {
        if (_store.Documents.TryGetValue(documentId, out var d)) { d.State = "cancelled"; d.UpdatedAt = DateTime.UtcNow.ToString("o"); }
        return Ok(new { id = documentId, state = "cancelled", cancelledAt = DateTime.UtcNow.ToString("o") });
    }

    /// <summary>Acknowledge processing of an inbound document.</summary>
    [HttpPost("documents/{documentId}/acknowledge")]
    public IActionResult AcknowledgeDocument(string documentId, [FromQuery] string? customerRef = null)
    {
        if (_store.Documents.TryGetValue(documentId, out var d)) { d.Acknowledged = true; d.State = "processed"; d.UpdatedAt = DateTime.UtcNow.ToString("o"); }
        return Ok(new { id = documentId, acknowledged = true, acknowledgedAt = DateTime.UtcNow.ToString("o") });
    }

    /// <summary>Send a business response (accept/reject/under_query).</summary>
    [HttpPost("documents/{documentId}/respond")]
    public IActionResult RespondDocument(string documentId, [FromBody] Dictionary<string, object>? body)
    {
        var d = _store.Documents.TryGetValue(documentId, out var f) ? f : _store.Documents.Values.First();
        return Ok(new
        {
            id = d.Id,
            customerRef = d.CustomerRef,
            response = new
            {
                status = MockStore.GetStr(body,"status", "under_query"),
                direction = "sent",
                delivery = "sent",
                respondedAt = DateTime.UtcNow.ToString("o")
            },
            idempotent = false
        });
    }

    /// <summary>Evidence bundle for a document.</summary>
    [HttpGet("documents/{documentId}/evidence")]
    public IActionResult Evidence(string documentId)
    {
        var d = _store.Documents.TryGetValue(documentId, out var f) ? f : _store.Documents.Values.First();
        return Ok(new
        {
            documentId = d.Id,
            evidence = new
            {
                as4MessageId = "MSG-" + d.Id[..8].ToUpperInvariant(),
                signed = true,
                receiptAt = d.UpdatedAt,
                mlsReceived = d.Direction == "inbound"
            },
            timeline = new object[]
            {
                new { at = d.CreatedAt, action = "accepted", detail = "Doklad bol prijaty do riadeného flow." },
                new { at = d.UpdatedAt, action = "delivered", detail = "AS4 receipt bol overeny na Peppol sieti." }
            }
        });
    }

    /// <summary>UBL XML export of a document.</summary>
    [HttpGet("documents/{documentId}/ubl")]
    public IActionResult Ubl(string documentId)
    {
        var d = _store.Documents.TryGetValue(documentId, out var f) ? f : _store.Documents.Values.First();
        return Ok(new { documentId = d.Id, contentType = "application/xml", bytes = 8211, content = EnterpriseController.UblXml(d) });
    }

    /// <summary>PDF preview of a document.</summary>
    [HttpGet("documents/{documentId}/pdf")]
    public IActionResult Pdf(string documentId)
    {
        var d = _store.Documents.TryGetValue(documentId, out var f) ? f : _store.Documents.Values.First();
        return Ok(new { documentId = d.Id, contentType = "application/pdf", bytes = 48211, generatedAt = DateTime.UtcNow.ToString("o") });
    }

    // ── Events ──────────────────────────────────────────────────

    /// <summary>Connector event stream (cursor-based, document lifecycle).</summary>
    [HttpGet("events")]
    public IActionResult Events([FromQuery] int limit = 50, [FromQuery] string? cursor = null)
    {
        var items = _store.Events.Values.OrderByDescending(e => e.OccurredAt).Take(Math.Min(limit, 100))
            .Select(e => (object)new
            {
                id = e.Id,
                type = e.Type,
                state = e.State,
                documentId = e.DocumentId,
                customerRef = "erp-acme",
                occurredAt = e.OccurredAt,
                data = e.Data
            }).ToList();
        return Ok(new { events = items, nextCursor = "eyJldmVudElkIjoi" + Guid.NewGuid().ToString("N")[..8], hasMore = items.Count >= limit });
    }

    // ── Webhook ─────────────────────────────────────────────────

    /// <summary>Load Connector webhook configuration.</summary>
    [HttpGet("webhook")]
    public IActionResult GetWebhook()
    {
        var wh = _store.Webhooks.Values.FirstOrDefault();
        if (wh == null)
        {
            wh = new WebhookConfig
            {
                Id = "wh_default",
                Url = null,
                Events = new List<string> { "document.received", "document.delivered" },
                Active = true,
                Secret = "whsec_" + Guid.NewGuid().ToString("N")[..24],
                CreatedAt = DateTime.UtcNow.ToString("o"),
                UpdatedAt = DateTime.UtcNow.ToString("o")
            };
            _store.Webhooks["default"] = wh;
        }
        return Ok(new { webhook = SafeWh(wh) });
    }

    /// <summary>Create or update Connector webhook.</summary>
    [HttpPut("webhook")]
    public IActionResult UpsertWebhook([FromBody] Dictionary<string, object>? body)
    {
        var wh = _store.Webhooks.Values.FirstOrDefault();
        if (wh == null)
        {
            wh = new WebhookConfig
            {
                Id = "wh_01JZ" + Guid.NewGuid().ToString("N")[..8],
                Secret = "whsec_" + Guid.NewGuid().ToString("N")[..24],
                CreatedAt = DateTime.UtcNow.ToString("o")
            };
            _store.Webhooks["default"] = wh;
        }
        wh.Url = (MockStore.GetStr(body,"url")) ?? wh.Url;
        if (MockStore.GetVal(body,"events") is System.Text.Json.JsonElement ev)
        {
            var list = new List<string>();
            foreach (var x in ev.EnumerateArray()) list.Add(x.GetString() ?? "");
            if (list.Count > 0) wh.Events = list;
        }
        wh.UpdatedAt = DateTime.UtcNow.ToString("o");
        return Ok(new { webhook = SafeWh(wh), created = false });
    }

    /// <summary>Delete Connector webhook.</summary>
    [HttpDelete("webhook")]
    public IActionResult DeleteWebhook()
    {
        foreach (var k in _store.Webhooks.Keys.ToList()) _store.Webhooks.TryRemove(k, out _);
        return Ok(new { removed = true, remaining = _store.Webhooks.Count, timestamp = DateTime.UtcNow.ToString("o") });
    }

    /// <summary>Rotate webhook signing secret.</summary>
    [HttpPost("webhook/rotate-secret")]
    public IActionResult RotateSecret()
    {
        var secret = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        var wh = _store.Webhooks.Values.FirstOrDefault();
        if (wh != null) { wh.Secret = "whsec_" + secret[..24]; wh.UpdatedAt = DateTime.UtcNow.ToString("o"); }
        return Ok(new { secret });
    }

    /// <summary>Send a test webhook event.</summary>
    [HttpPost("webhook/test")]
    public IActionResult TestWebhook([FromBody] Dictionary<string, object>? body)
    {
        var deliveryId = "whd_" + Guid.NewGuid().ToString("N")[..12];
        _store.Deliveries[deliveryId] = new DeliveryRecord
        {
            Id = deliveryId, WebhookId = "wh_test", EventId = "evt_test_" + Guid.NewGuid().ToString("N")[..8],
            Type = "document.received", Status = "SUCCESS", Attempts = 1, ResponseStatus = 204,
            LastAttemptAt = DateTime.UtcNow.ToString("o"), CreatedAt = DateTime.UtcNow.ToString("o")
        };
        return Accepted(new
        {
            deliveryId,
            status = "queued",
            @event = new
            {
                id = "evt_test_" + Guid.NewGuid().ToString("N")[..8],
                customerRef = MockStore.GetStr(body,"customerRef", "erp-acme"),
                documentId = "doc_test_" + Guid.NewGuid().ToString("N")[..8],
                type = "document.received",
                state = "received",
                occurredAt = DateTime.UtcNow.ToString("o"),
                data = new { direction = "inbound", type = "invoice", number = "TEST-2026-001" },
                test = true
            }
        });
    }

    /// <summary>Webhook delivery history.</summary>
    [HttpGet("webhook/deliveries")]
    public IActionResult Deliveries([FromQuery] int limit = 20)
    {
        var items = _store.Deliveries.Values.Take(limit)
            .Select(d => (object)new
            {
                id = d.Id, eventId = d.EventId, customerRef = "erp-acme", type = d.Type,
                status = d.Status, attempts = d.Attempts, responseStatus = d.ResponseStatus,
                lastAttemptAt = d.LastAttemptAt, nextRetryAt = d.NextRetryAt, createdAt = d.CreatedAt
            }).ToList();
        return Ok(new { deliveries = items, nextCursor = "eyJpZA..", hasMore = false });
    }

    /// <summary>Detail of one webhook delivery.</summary>
    [HttpGet("webhook/deliveries/{deliveryId}")]
    public IActionResult DeliveryDetail(string deliveryId)
    {
        var d = _store.Deliveries.Values.FirstOrDefault(x => x.Id == deliveryId) ?? _store.Deliveries.Values.First();
        return Ok(new
        {
            id = d.Id, eventId = d.EventId, customerRef = "erp-acme", type = d.Type,
            status = d.Status, attempts = d.Attempts, responseStatus = d.ResponseStatus,
            responseBody = (object?)null,
            lastAttemptAt = d.LastAttemptAt, createdAt = d.CreatedAt,
            attemptsDetail = new object[] { new { attempt = 1, at = d.LastAttemptAt, result = "SUCCESS", responseStatus = 204 } }
        });
    }

    /// <summary>Replay a delivery.</summary>
    [HttpPost("webhook/deliveries/{deliveryId}/replay")]
    public IActionResult Replay(string deliveryId)
    {
        var newId = "whd_" + Guid.NewGuid().ToString("N")[..12];
        var src = _store.Deliveries.Values.FirstOrDefault(x => x.Id == deliveryId) ?? _store.Deliveries.Values.First();
        _store.Deliveries[newId] = new DeliveryRecord
        {
            Id = newId, WebhookId = src.WebhookId, EventId = src.EventId, Type = src.Type,
            Status = "PENDING", Attempts = 0, ResponseStatus = 0,
            LastAttemptAt = DateTime.UtcNow.ToString("o"), CreatedAt = DateTime.UtcNow.ToString("o")
        };
        return Accepted(new { deliveryId = newId, replayedFrom = deliveryId, status = "queued" });
    }

    /// <summary>Start receiver test suite (7 tests).</summary>
    [HttpPost("webhook/test-suite")]
    public IActionResult TestSuite([FromBody] Dictionary<string, object>? body)
    {
        var runId = "wtr_" + Guid.NewGuid().ToString("N")[..10];
        return Accepted(new { testRunId = runId, status = "RUNNING", customerRef = MockStore.GetStr(body,"customerRef", "erp-acme"), testsQueued = 7 });
    }

    /// <summary>Track receiver test suite status.</summary>
    [HttpGet("webhook/test-suite/{testRunId}")]
    public IActionResult TestSuiteStatus(string testRunId)
    {
        return Ok(new
        {
            testRunId,
            status = "COMPLETED",
            startedAt = DateTime.UtcNow.AddMinutes(-12).ToString("o"),
            finishedAt = DateTime.UtcNow.ToString("o"),
            results = new object[]
            {
                new { name = "signature-valid", result = "PASS", detail = "HMAC sha256 sedadlo sedí." },
                new { name = "timestamp-fresh", result = "PASS", detail = "Starost' do 300s." },
                new { name = "duplicate-safe", result = "PASS", detail = "Idempotencia drží." },
                new { name = "payload-shape", result = "PASS", detail = "documentId + event typ." },
                new { name = "retry-behavior", result = "PASS", detail = "Retre s exponential backoff." },
                new { name = "ordering", result = "PASS", detail = "Events are chronological." },
                new { name = "failure-recovery", result = "PASS", detail = "Dead letter queue is reachable." }
            },
            passed = 7, failed = 0
        });
    }

    // ── Mapper / Autopilot / misc ───────────────────────────────

    /// <summary>Connector mapper dry-run (ERP mapping helper).</summary>
    [HttpPost("mapper")]
    public IActionResult Mapper([FromBody] Dictionary<string, object>? body)
    {
        return Ok(new
        {
            mapped = true,
            warnings = new object[] { new { field = "line.vatRate", message = "Rate 23% not in ERP preset; using SK default." } },
            result = new
            {
                type = "invoice",
                number = "2026-0042",
                currency = "EUR",
                customerRef = "erp-acme",
                recipient = new { country = "SK", taxId = "2120123456" },
                issueDate = DateTime.UtcNow.Date.ToString("yyyy-MM-dd"),
                dueDate = DateTime.UtcNow.AddDays(14).Date.ToString("yyyy-MM-dd"),
                lines = new object[] { new { description = "Mesačná licencia", quantity = 1, unitPrice = 100.00, vatRate = 23 } }
            }
        });
    }

    /// <summary>Autopilot — managed sending flow.</summary>
    [HttpPost("autopilot")]
    public IActionResult Autopilot([FromBody] Dictionary<string, object>? body)
    {
        var id = "apt_" + Guid.NewGuid().ToString("N")[..10];
        return Accepted(new { autopilotId = id, status = "queued", customerRef = "erp-acme", documentsQueued = 3 });
    }

    /// <summary>Autopilot status.</summary>
    [HttpGet("autopilot/{autopilotId}")]
    public IActionResult AutopilotStatus(string autopilotId)
    {
        return Ok(new { autopilotId, status = "RUNNING", processed = 2, total = 3, nextRetryAt = DateTime.UtcNow.AddMinutes(5).ToString("o") });
    }

    /// <summary>Synchronize customer documents (reconciliation helper).</summary>
    [HttpGet("sync")]
    public IActionResult Sync([FromQuery] string? customerRef = null)
    {
        return Ok(new
        {
            customerRef = customerRef ?? "erp-acme",
            lastSyncAt = DateTime.UtcNow.AddMinutes(-35).ToString("o"),
            documentsInSync = _store.Documents.Count,
            pending = new object[] { new { documentId = _store.Documents.Values.First().Id, state = "received", ageSeconds = 640 } },
            connector = new { productPath = "managed", mode = "json", runtime = "epostak" }
        });
    }

    // ── Helpers ─────────────────────────────────────────────────

    internal static Dictionary<string, object> SafeWh(WebhookConfig wh) => new()
    {
        ["id"] = wh.Id, ["url"] = wh.Url, ["events"] = wh.Events, ["active"] = wh.Active,
        ["failedAttempts"] = wh.FailedAttempts, ["createdAt"] = wh.CreatedAt, ["updatedAt"] = wh.UpdatedAt
    };

    private DocumentRecord NewSentDoc()
    {
        var id = MockStore.NewId();
        var d = new DocumentRecord
        {
            Id = id, DocumentId = id, Number = "2026-" + id[..6], Type = "invoice", Direction = "outbound",
            CustomerRef = "erp-acme", FirmId = _store.Firms.Values.First().Id,
            Sender = _store.Firms.Values.First() is { } f ? new PartySnapshot { Name = f.Name, TaxId = f.Dic, PeppolId = f.PeppolId, Ico = f.Ico } : new PartySnapshot(),
            Recipient = new PartySnapshot { Name = "Odberateľ, s.r.o.", TaxId = "2199999999", PeppolId = "0245:2199999999", Country = "SK" },
            State = "sent", Status = "SENT", Currency = "EUR",
            AmountWithoutTax = 100, AmountTax = 23, AmountTotal = 123,
            IssueDate = DateTime.UtcNow.Date.ToString("yyyy-MM-dd"), DueDate = DateTime.UtcNow.AddDays(14).Date.ToString("yyyy-MM-dd"),
            CreatedAt = DateTime.UtcNow.ToString("o"), UpdatedAt = DateTime.UtcNow.ToString("o")
        };
        _store.Documents[id] = d;
        return d;
    }

    private DocumentRecord BodyToDoc(Dictionary<string, object>? body, bool staged)
    {
        var id = MockStore.NewId();
        var now = DateTime.UtcNow.ToString("o");
        return new DocumentRecord
        {
            Id = id, DocumentId = id,
            Number = (MockStore.GetStr(body,"number")) ?? "2026-" + id[..6],
            Type = (MockStore.GetStr(body,"type")) ?? "invoice",
            Direction = staged ? "inbound" : "outbound",
            CustomerRef = (MockStore.GetStr(body,"customerRef")) ?? "erp-acme",
            FirmId = _store.Firms.Values.First().Id,
            Sender = new PartySnapshot { Name = "Dodávateľ, s.r.o.", TaxId = "2120123456", PeppolId = "0245:2120123456", Country = "SK" },
            Recipient = new PartySnapshot { Name = "Odberateľ, s.r.o.", TaxId = "2199999999", PeppolId = "0245:2199999999", Country = "SK" },
            State = staged ? "received" : "sent",
            Status = staged ? "RECEIVED" : "SENT",
            Currency = "EUR",
            AmountWithoutTax = 100, AmountTax = 23, AmountTotal = 123,
            IssueDate = DateTime.UtcNow.Date.ToString("yyyy-MM-dd"),
            DueDate = DateTime.UtcNow.AddDays(14).Date.ToString("yyyy-MM-dd"),
            CreatedAt = now, UpdatedAt = now
        };
    }

    private Dictionary<string, object> ShapeDocument(DocumentRecord d, string? state = null)
    {
        return new Dictionary<string, object>
        {
            ["id"] = d.Id,
            ["customerRef"] = d.CustomerRef,
            ["externalId"] = d.Number,
            ["direction"] = d.Direction,
            ["type"] = d.Type,
            ["number"] = d.Number,
            ["state"] = state ?? d.State,
            ["currency"] = d.Currency,
            ["amounts"] = new { withoutTax = d.AmountWithoutTax, tax = d.AmountTax, total = d.AmountTotal, due = d.AmountTotal },
            ["sender"] = d.Sender,
            ["recipient"] = d.Recipient,
            ["issueDate"] = d.IssueDate,
            ["dueDate"] = d.DueDate,
            ["processedAt"] = d.Acknowledged ? d.UpdatedAt : (string?)null,
            ["processedReference"] = (string?)null,
            ["response"] = (object?)null,
            ["createdAt"] = d.CreatedAt,
            ["updatedAt"] = d.UpdatedAt,
            ["links"] = new
            {
                self = $"/api/v1/connector/documents/{d.Id}?customerRef={d.CustomerRef}",
                evidence = $"/api/v1/connector/documents/{d.Id}/evidence?customerRef={d.CustomerRef}",
                acknowledge = $"/api/v1/connector/documents/{d.Id}/acknowledge?customerRef={d.CustomerRef}"
            }
        };
    }

    private void AddEvent(DocumentRecord doc, string type, string state)
    {
        var id = MockStore.NewId();
        _store.Events[id] = new EventRecord
        {
            Id = id, DocumentId = doc.Id, Type = type, State = state,
            OccurredAt = DateTime.UtcNow.ToString("o"),
            Data = new Dictionary<string, object> { ["number"] = doc.Number, ["direction"] = doc.Direction, ["total"] = doc.AmountTotal }
        };
    }
}
