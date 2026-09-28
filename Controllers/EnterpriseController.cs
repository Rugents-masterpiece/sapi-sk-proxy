using Microsoft.AspNetCore.Mvc;
using SapiSkProxy.Models.Mock;
using SapiSkProxy.Services;

namespace SapiSkProxy.Controllers;

/// <summary>
/// Enterprise API Core endpoints — served from in-memory mock store (generated data),
/// matching the public ePošťák Enterprise API contract (epostak.sk/api/openapi.enterprise.json).
/// </summary>
[ApiController]
[Route("api/v1")]
[Tags("Enterprise")]
public class EnterpriseController : ControllerBase
{
    private readonly MockStore _store;
    public EnterpriseController(MockStore store) => _store = store;

    // ── Documents ───────────────────────────────────────────────

    /// <summary>Send a document (dry-run validated, queued for delivery).</summary>
    [HttpPost("documents/send")]
    public IActionResult SendDocument([FromBody] Dictionary<string, object>? body)
    {
        var docId = MockStore.NewId();
        var now = DateTime.UtcNow;
        var doc = NewDoc(docId, "outbound", "invoice",
            (body?["receiverPeppolId"]?.ToString()) ?? "0245:2199999999",
            (body?["receiverName"]?.ToString()) ?? "Odberateľ, s.r.o.", "sent", "SENT");
        _store.Documents[docId] = doc;
        AddEvent(doc, "document.sent", "sent");
        return Accepted(new
        {
            id = doc.Id,
            documentId = doc.Id,
            number = doc.Number,
            status = "SENT",
            state = "queued",
            direction = "outbound",
            receiverPeppolId = doc.Recipient.PeppolId,
            acceptedAt = now.ToString("o")
        });
    }

    /// <summary>Send up to 50 documents in a batch.</summary>
    [HttpPost("documents/send/batch")]
    public IActionResult SendBatch([FromBody] Dictionary<string, object>? body)
    {
        var items = new List<object>();
        for (int i = 0; i < 3; i++)
        {
            var docId = MockStore.NewId();
            var doc = NewDoc(docId, "outbound", "invoice", "0245:2199999999", "Odberateľ, s.r.o.", "sent", "SENT");
            _store.Documents[docId] = doc;
            items.Add(new { index = i, id = doc.Id, number = doc.Number, status = "QUEUED", acceptedAt = doc.CreatedAt });
        }
        return Accepted(new { accepted = items.Count, items });
    }

    /// <summary>Dry-run validation of a send without creating a document.</summary>
    [HttpPost("documents/preflight")]
    public IActionResult Preflight([FromBody] Dictionary<string, object>? body)
    {
        return Ok(new
        {
            ok = true,
            receiver = new { peppolId = body?["receiverPeppolId"] ?? "0245:2199999999", networkReady = true, documentTypesAccepted = new[] { "invoice", "credit-note" } },
            validation = new { valid = true, errors = Array.Empty<object>(), warnings = new[] { new { code = "WARN-101", field = "dueDate", message = "Due date is shorter than typical 14-day terms." } } },
            checkedAt = DateTime.UtcNow.ToString("o")
        });
    }

    /// <summary>Document delivery status.</summary>
    [HttpGet("documents/{id}/status")]
    public IActionResult DocumentStatus(string id)
    {
        if (!_store.Documents.TryGetValue(id, out var doc)) doc = FirstDoc();
        return Ok(new
        {
            providerDocumentId = doc.Id,
            number = doc.Number,
            direction = doc.Direction,
            status = doc.Status,
            state = doc.State,
            deliveryStatus = doc.State,
            tddStatus = "registered",
            mlsStatus = doc.Direction == "inbound" ? "received" : "not_applicable",
            acknowledged = doc.Acknowledged,
            updatedAt = doc.UpdatedAt
        });
    }

    /// <summary>Status of up to 100 documents.</summary>
    [HttpPost("documents/status/batch")]
    public IActionResult StatusBatch([FromBody] Dictionary<string, object>? body)
    {
        var docs = _store.Documents.Values.Take(10).Select(d => new
        {
            id = d.Id, status = d.Status, state = d.State, updatedAt = d.UpdatedAt
        }).ToList();
        return Ok(new { documents = docs, requested = 10, found = docs.Count });
    }

    /// <summary>AS4 delivery confirmation and MLS for a document.</summary>
    [HttpGet("documents/{id}/evidence")]
    public IActionResult Evidence(string id)
    {
        var doc = GetDoc(id);
        return Ok(new
        {
            documentId = doc.Id,
            as4 = new
            {
                messageId = "MSG-" + doc.Id[..8].ToUpperInvariant(),
                refToMessage = "REF-" + doc.Id[..8].ToUpperInvariant(),
                receiptSigned = true,
                receiptAt = doc.UpdatedAt,
                mpc = "urn:oasis:names:specification:ubl:scheme:xsd:2.1"
            },
            mls = new { received = doc.Direction == "inbound", receivedAt = doc.UpdatedAt },
            deliveredAt = doc.UpdatedAt
        });
    }

    /// <summary>Full support packet (ZIP manifest) for a document.</summary>
    [HttpGet("documents/{id}/support-packet")]
    public IActionResult SupportPacket(string id)
    {
        var doc = GetDoc(id);
        return Ok(new
        {
            documentId = doc.Id,
            fileName = $"support-{doc.Number}.zip",
            sizeBytes = 31457,
            generatedAt = DateTime.UtcNow.ToString("o"),
            files = new object[]
            {
                new { path = "document.xml", contentType = "application/xml", bytes = 8211 },
                new { path = "as4-envelope.txt", contentType = "text/plain", bytes = 12043 },
                new { path = "delivery-report.json", contentType = "application/json", bytes = 2331 },
                new { path = "events.json", contentType = "application/json", bytes = 891 }
            }
        });
    }

    /// <summary>Signed AS4 multipart envelope from WORM archive.</summary>
    [HttpGet("documents/{id}/envelope")]
    public IActionResult Envelope(string id)
    {
        var doc = GetDoc(id);
        return Ok(new
        {
            documentId = doc.Id,
            contentType = "multipart/related",
            signed = true,
            signatureAlgorithm = "RSA-SHA256",
            parts = new object[]
            {
                new { name = "sm", contentType = "application/ebms3", bytes = 4096 },
                new { name = "payload-1", contentType = "application/xml", bytes = 8211 }
            }
        });
    }

    /// <summary>Chronological lifecycle events of a document.</summary>
    [HttpGet("documents/{id}/events")]
    public IActionResult DocumentEvents(string id)
    {
        var doc = GetDoc(id);
        var events = _store.Events.Values
            .Where(e => e.DocumentId == doc.Id)
            .OrderByDescending(e => e.OccurredAt)
            .Take(20)
            .Select(e => (object)new { id = e.Id, type = e.Type, state = e.State, occurredAt = e.OccurredAt, documentId = e.DocumentId })
            .ToList();
        if (events.Count == 0)
        {
            events.Add(new { id = MockStore.NewId(), type = "document.received", state = doc.State, occurredAt = doc.CreatedAt, documentId = doc.Id });
            events.Add(new { id = MockStore.NewId(), type = "document.delivered", state = "delivered", occurredAt = doc.UpdatedAt, documentId = doc.Id });
        }
        return Ok(new { documentId = doc.Id, events, count = events.Count });
    }

    /// <summary>Invoice response history of a document.</summary>
    [HttpGet("documents/{id}/responses")]
    public IActionResult Responses(string id)
    {
        var doc = GetDoc(id);
        return Ok(new
        {
            documentId = doc.Id,
            responses = new object[]
            {
                new { code = "ACCEPTED", codeDescription = "Accepted", sentAt = doc.UpdatedAt, senderPeppolId = doc.Sender.PeppolId },
                new { code = "REJECTED", codeDescription = "Rejected: wrong buyer reference (rejected for test demo)", sentAt = doc.CreatedAt, senderPeppolId = doc.Sender.PeppolId }
            },
            count = 2
        });
    }

    /// <summary>PDF rendering of a document.</summary>
    [HttpGet("documents/{id}/pdf")]
    public IActionResult Pdf(string id)
    {
        var doc = GetDoc(id);
        return Ok(new { documentId = doc.Id, contentType = "application/pdf", bytes = 48211, generatedAt = doc.UpdatedAt });
    }

    /// <summary>UBL XML payload of a document.</summary>
    [HttpGet("documents/{id}/ubl")]
    public IActionResult Ubl(string id)
    {
        var doc = GetDoc(id);
        var xml = UblXml(doc);
        return Ok(new { documentId = doc.Id, contentType = "application/xml", bytes = xml.Length, content = xml });
    }

    /// <summary>Send an invoice response (accept/reject) to an inbound document.</summary>
    [HttpPost("documents/{id}/respond")]
    public IActionResult Respond(string id, [FromBody] Dictionary<string, object>? body)
    {
        var doc = GetDoc(id);
        return Ok(new
        {
            documentId = doc.Id,
            response = new
            {
                status = body?["status"] ?? "under_query",
                direction = "sent",
                delivery = "sent",
                respondedAt = DateTime.UtcNow.ToString("o")
            },
            idempotent = false
        });
    }

    /// <summary>Mark document state manually (processed / archived).</summary>
    [HttpPost("documents/{id}/mark")]
    public IActionResult Mark(string id, [FromBody] Dictionary<string, object>? body)
    {
        if (_store.Documents.TryGetValue(id, out var doc))
        {
            doc.State = "processed";
            doc.Acknowledged = true;
            doc.UpdatedAt = DateTime.UtcNow.ToString("o");
        }
        return Ok(new
        {
            documentId = id,
            state = "processed",
            markedAt = DateTime.UtcNow.ToString("o"),
            note = body?["note"] ?? "marked via API"
        });
    }

    // ── Inbox (documents/inbox) ─────────────────────────────────

    /// <summary>Paginated list of received invoice documents.</summary>
    [HttpGet("documents/inbox")]
    public IActionResult Inbox([FromQuery] int limit = 20, [FromQuery] string? cursor = null)
    {
        var docs = _store.Documents.Values.Where(d => d.Direction == "inbound").Take(Math.Min(limit, 100)).ToList();
        return Ok(new
        {
            documents = docs.Select(d => (object)ToInboxItem(d)).ToList(),
            total = docs.Count,
            nextCursor = "eyJ2Ijoi" + Guid.NewGuid().ToString("N")[..8],
            hasMore = _store.Documents.Values.Count(d => d.Direction == "inbound") > docs.Count
        });
    }

    /// <summary>Single received document with UBL payload.</summary>
    [HttpGet("documents/inbox/{id}")]
    public IActionResult InboxDetail(string id)
    {
        var doc = _store.Documents.Values.FirstOrDefault(d => d.Id == id) ?? _store.Documents.Values.First(d => d.Direction == "inbound");
        return Ok(ToInboxItem(doc, includeUbl: true));
    }

    /// <summary>Acknowledge processing of a received document (idempotent).</summary>
    [HttpPost("documents/inbox/{id}/acknowledge")]
    public IActionResult InboxAck(string id)
    {
        if (_store.Documents.TryGetValue(id, out var doc)) { doc.Acknowledged = true; doc.State = "processed"; doc.UpdatedAt = DateTime.UtcNow.ToString("o"); }
        return Ok(new { documentId = id, acknowledged = true, acknowledgedAt = DateTime.UtcNow.ToString("o") });
    }

    /// <summary>Cross-firm inbox for integrator keys.</summary>
    [HttpGet("documents/inbox/all")]
    public IActionResult InboxAll([FromQuery] int limit = 20)
    {
        var docs = _store.Documents.Values.Where(d => d.Direction == "inbound").Take(limit);
        return Ok(new
        {
            documents = docs.Select(d => { var i = ToInboxItem(d); return (object)new Dictionary<string, object>(i) { ["firmId"] = d.FirmId }; }).ToList(),
            total = docs.Count(),
            nextCursor = "eyJhbGciOiJIUzI1NiIsInR5cCI6Impvc2v0",
            hasMore = true
        });
    }

    // ── Pull API: inbound / outbound ────────────────────────────

    /// <summary>Cursor-based list of received Peppol documents.</summary>
    [HttpGet("inbound/documents")]
    public IActionResult InboundList([FromQuery] int limit = 20)
    {
        var docs = _store.Documents.Values.Where(d => d.Direction == "inbound").Take(limit)
            .Select(d => (object)new
            {
                id = d.Id,
                number = d.Number,
                type = d.Type,
                state = d.State,
                currency = d.Currency,
                amounts = new { withoutTax = d.AmountWithoutTax, tax = d.AmountTax, total = d.AmountTotal },
                sender = d.Sender, recipient = d.Recipient,
                issueDate = d.IssueDate, dueDate = d.DueDate,
                createdAt = d.CreatedAt, updatedAt = d.UpdatedAt
            }).ToList();
        return Ok(new { documents = docs, nextCursor = "eyJvbGRlc3RJZCI6ImQw", hasMore = true, count = docs.Count });
    }

    /// <summary>Pull: detail of received document.</summary>
    [HttpGet("inbound/documents/{id}")]
    public IActionResult InboundDetail(string id)
    {
        var d = _store.Documents.Values.FirstOrDefault(x => x.Id == id && x.Direction == "inbound") ?? FirstInbound();
        return Ok(new
        {
            id = d.Id, number = d.Number, type = d.Type, state = d.State,
            amounts = new { withoutTax = d.AmountWithoutTax, tax = d.AmountTax, total = d.AmountTotal },
            sender = d.Sender, recipient = d.Recipient,
            issueDate = d.IssueDate, dueDate = d.DueDate,
            ubl = UblXml(d), createdAt = d.CreatedAt, updatedAt = d.UpdatedAt
        });
    }

    /// <summary>Raw UBL XML of a received document.</summary>
    [HttpGet("inbound/documents/{id}/ubl")]
    public IActionResult InboundUbl(string id)
    {
        var d = _store.Documents.Values.FirstOrDefault(x => x.Id == id && x.Direction == "inbound") ?? FirstInbound();
        var xml = UblXml(d);
        return Ok(new { documentId = d.Id, contentType = "application/xml", bytes = xml.Length, content = xml });
    }

    /// <summary>Explicit acknowledgement that an inbound document was processed locally.</summary>
    [HttpPost("inbound/documents/{id}/ack")]
    public IActionResult InboundAck(string id)
    {
        if (_store.Documents.TryGetValue(id, out var d)) { d.Acknowledged = true; d.UpdatedAt = DateTime.UtcNow.ToString("o"); }
        return Ok(new { documentId = id, ackRecorded = true, ackAt = DateTime.UtcNow.ToString("o") });
    }

    /// <summary>Import UBL document received outside Peppol.</summary>
    [HttpPost("inbound/import")]
    public IActionResult InboundImport([FromBody] Dictionary<string, object>? body)
    {
        var id = MockStore.NewId();
        var doc = NewDoc(id, "inbound", "invoice", "0245:2120123456", "Dodávateľ, s.r.o.", "received", "RECEIVED");
        _store.Documents[id] = doc;
        AddEvent(doc, "document.received", "received");
        return Accepted(new { id, imported = true, source = body?["source"] ?? "email", messageRef = body?["messageId"] ?? "local-1", receivedAt = doc.CreatedAt });
    }

    /// <summary>Cached Peppol participant lookup by ico.</summary>
    [HttpGet("outbound/documents")]
    public IActionResult OutboundList([FromQuery] int limit = 20)
    {
        var docs = _store.Documents.Values.Where(d => d.Direction == "outbound").Take(limit)
            .Select(d => (object)new
            {
                id = d.Id, number = d.Number, type = d.Type, state = d.State, status = d.Status,
                amountTotal = d.AmountTotal, currency = d.Currency,
                receiver = new { d.Recipient.Name, peppolId = d.Recipient.PeppolId },
                createdAt = d.CreatedAt, updatedAt = d.UpdatedAt,
                attempts = 1 + ((int)char.GetNumericValue(d.Id[0])) % 3
            }).ToList();
        return Ok(new { documents = docs, nextCursor = "eyJzb3VyY2UiOiJvdXRib3VuZCIsIm9mZnNldCI6MjB9", hasMore = true, count = docs.Count });
    }

    /// <summary>Pull: detail of sent document.</summary>
    [HttpGet("outbound/documents/{id}")]
    public IActionResult OutboundDetail(string id)
    {
        var d = _store.Documents.Values.FirstOrDefault(x => x.Id == id && x.Direction == "outbound") ?? FirstOutbound();
        return Ok(new
        {
            id = d.Id, number = d.Number, type = d.Type, state = d.State, status = d.Status,
            amountTotal = d.AmountTotal, currency = d.Currency,
            receiver = new { d.Recipient.Name, peppolId = d.Recipient.PeppolId, taxId = d.Recipient.TaxId },
            attempts = new object[]
            {
                new { attempt = 1, sentAt = d.CreatedAt, result = "queued" },
                new { attempt = 2, sentAt = d.UpdatedAt, result = "delivered", as4MessageId = "MSG-" + d.Id[..8].ToUpperInvariant() }
            },
            createdAt = d.CreatedAt, updatedAt = d.UpdatedAt
        });
    }

    /// <summary>Raw AS4 MDN delivery receipt.</summary>
    [HttpGet("outbound/documents/{id}/mdn")]
    public IActionResult OutboundMdn(string id)
    {
        var d = FirstOutbound();
        var mdn = $"<?xml version=\"1.0\"?><soap:Envelope xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\"><soap:Header><eh:Mbnrexpress xmlns:eh=\"http://docs.oasis-open.org/ebxml-msg/ebms/v3.0/ns/core/200704/\">sent={d.UpdatedAt}</eh:Mbnrexpress></soap:Header><soap:Body><receipt>OK</receipt></soap:Body></soap:Envelope>";
        return Ok(new { documentId = id, contentType = "text/xml", bytes = mdn.Length, content = mdn });
    }

    /// <summary>Raw UBL XML of a sent document.</summary>
    [HttpGet("outbound/documents/{id}/ubl")]
    public IActionResult OutboundUbl(string id)
    {
        var d = _store.Documents.Values.FirstOrDefault(x => x.Id == id && x.Direction == "outbound") ?? FirstOutbound();
        var xml = UblXml(d);
        return Ok(new { documentId = d.Id, contentType = "application/xml", bytes = xml.Length, content = xml });
    }

    /// <summary>Internal document event stream (lifecycle).</summary>
    [HttpGet("outbound/events")]
    public IActionResult OutboundEvents([FromQuery] int limit = 50)
    {
        var evs = _store.Events.Values.Where(e => true).OrderByDescending(e => e.OccurredAt).Take(limit)
            .Select(e => (object)new { id = e.Id, type = e.Type, state = e.State, documentId = e.DocumentId, occurredAt = e.OccurredAt }).ToList();
        return Ok(new { events = evs, nextCursor = "eyJldmVudF9pZCI6IjU1MCIsImFmdGVyIjp0cnVlfQ", hasMore = true, count = evs.Count });
    }

    // ── ePošťák Box ─────────────────────────────────────────────

    /// <summary>List Box items (durable staging layer).</summary>
    [HttpGet("box/items")]
    public IActionResult BoxList()
    {
        var items = _store.BoxItems.Values.Select(b => (object)new
        {
            id = b.Id, state = b.State, scheduledFor = b.ScheduledFor, documentId = b.DocumentId,
            number = b.Number, attempts = b.Attempts, createdAt = b.CreatedAt
        }).ToList();
        return Ok(new { items, total = items.Count });
    }

    /// <summary>Box item detail.</summary>
    [HttpGet("box/items/{itemId}")]
    public IActionResult BoxDetail(string itemId)
    {
        var b = _store.BoxItems.Values.FirstOrDefault(x => x.Id == itemId) ?? _store.BoxItems.Values.First();
        return Ok(new
        {
            id = b.Id, state = b.State, scheduledFor = b.ScheduledFor, documentId = b.DocumentId,
            number = b.Number, attempts = b.Attempts,
            storage = new { bucket = "box-payloads", key = b.Id, bytes = 12091, sha256 = b.Id.Replace("-", "") },
            dispatch = new { lastAttemptAt = b.CreatedAt, nextRetryAt = b.State == "failed" ? DateTime.UtcNow.AddMinutes(15).ToString("o") : null },
            audit = new object[] { new { at = b.CreatedAt, action = "STAGED" }, new { at = b.CreatedAt, action = "DISPATCH", result = b.State } },
            createdAt = b.CreatedAt
        });
    }

    /// <summary>Stage a document in Box.</summary>
    [HttpPost("box/items")]
    public IActionResult BoxCreate([FromBody] Dictionary<string, object>? body)
    {
        var id = MockStore.NewId();
        var item = new BoxItem
        {
            Id = id, State = "queued", DocumentId = (body?["documentId"]?.ToString()) ?? MockStore.NewId(),
            Number = (body?["number"]?.ToString()) ?? "BOX-" + DateTime.UtcNow.ToString("yyyyMMdd") + "-001",
            CustomerRef = (body?["customerRef"]?.ToString()) ?? "erp-acme",
            CreatedAt = DateTime.UtcNow.ToString("o")
        };
        _store.BoxItems[id] = item;
        return Accepted(new { id, state = "queued", scheduledFor = item.ScheduledFor, createdAt = item.CreatedAt });
    }

    /// <summary>Schedule Box item send.</summary>
    [HttpPost("box/items/{itemId}/schedule")]
    public IActionResult BoxSchedule(string itemId, [FromBody] Dictionary<string, object>? body)
    {
        var item = _store.BoxItems.Values.FirstOrDefault(b => b.Id == itemId) ?? _store.BoxItems.Values.First();
        item.State = "scheduled";
        item.ScheduledFor = (body?["scheduledFor"]?.ToString()) ?? DateTime.UtcNow.AddHours(6).ToString("o");
        return Ok(new { id = item.Id, state = "scheduled", scheduledFor = item.ScheduledFor });
    }

    /// <summary>Trigger Box item dispatch now.</summary>
    [HttpPost("box/items/{itemId}/send-now")]
    public IActionResult BoxSendNow(string itemId)
    {
        var item = _store.BoxItems.Values.FirstOrDefault(b => b.Id == itemId) ?? _store.BoxItems.Values.First();
        item.State = "sent";
        return Ok(new { id = item.Id, state = "sent", dispatchedAt = DateTime.UtcNow.ToString("o") });
    }

    /// <summary>Retry Box item.</summary>
    [HttpPost("box/items/{itemId}/retry")]
    public IActionResult BoxRetry(string itemId)
    {
        var item = _store.BoxItems.Values.FirstOrDefault(b => b.Id == itemId) ?? _store.BoxItems.Values.First();
        item.State = "queued";
        item.Attempts += 1;
        return Ok(new { id = item.Id, state = "queued", attempts = item.Attempts, queuedAt = DateTime.UtcNow.ToString("o") });
    }

    /// <summary>Cancel Box item.</summary>
    [HttpPost("box/items/{itemId}/cancel")]
    public IActionResult BoxCancel(string itemId)
    {
        var item = _store.BoxItems.Values.FirstOrDefault(b => b.Id == itemId) ?? _store.BoxItems.Values.First();
        item.State = "cancelled";
        return Ok(new { id = item.Id, state = "cancelled", cancelledAt = DateTime.UtcNow.ToString("o") });
    }

    // ── Payload Assistant ───────────────────────────────────────

    /// <summary>OCR extraction from PDF/image.</summary>
    [HttpPost("payloads/extract")]
    public IActionResult Extract([FromBody] Dictionary<string, object>? body) => Ok(ExtractionResult(body));

    /// <summary>Batch OCR extraction (max 50).</summary>
    [HttpPost("payloads/extract/batch")]
    public IActionResult ExtractBatch([FromBody] Dictionary<string, object>? body)
    {
        var results = new[] { ExtractionResult(body), ExtractionResult(body) };
        return Ok(new { results, processed = 2, failed = 0 });
    }

    /// <summary>Apply human corrections to OCR result without re-OCR.</summary>
    [HttpPost("payloads/review")]
    public IActionResult Review([FromBody] Dictionary<string, object>? body)
    {
        return Ok(ExtractionResult(body));
    }

    /// <summary>Parse UBL XML into normalized JSON.</summary>
    [HttpPost("payloads/parse")]
    public IActionResult Parse([FromBody] Dictionary<string, object>? body)
    {
        return Ok(new
        {
            type = "invoice",
            number = "2026-0114",
            issueDate = DateTime.UtcNow.AddDays(-6).Date.ToString("yyyy-MM-dd"),
            dueDate = DateTime.UtcNow.AddDays(14).Date.ToString("yyyy-MM-dd"),
            currency = "EUR",
            buyer = new { name = "Odberateľ, s.r.o.", peppolId = "0245:2199999999", taxId = "2199999999" },
            seller = new { name = "Dodávateľ, s.r.o.", peppolId = "0245:2120123456", taxId = "2120123456" },
            totals = new { withoutTax = 100.00, tax = 23.00, total = 123.00, taxRates = new[] { new { rate = 23, taxable = 100.00, tax = 23.00 } } },
            lines = new object[] { new { description = "Mesačná licencia", quantity = 1, unitPrice = 100.00, vatRate = 23 } },
            warnings = new object[] { }
        });
    }

    /// <summary>Convert between JSON business document and UBL XML.</summary>
    [HttpPost("payloads/convert")]
    public IActionResult Convert([FromBody] Dictionary<string, object>? body)
    {
        var doc = FirstDoc();
        return Ok(new
        {
            format = "ubl",
            content = UblXml(doc),
            warnings = new object[] { },
            convertedAt = DateTime.UtcNow.ToString("o")
        });
    }

    /// <summary>Validate document against Peppol BIS 3.0 rules.</summary>
    [HttpPost("payloads/validate")]
    public IActionResult Validate([FromBody] Dictionary<string, object>? body)
    {
        return Ok(new
        {
            valid = true,
            schema = "Peppol BIS Billing 3.0",
            errors = Array.Empty<object>(),
            warnings = new[] { new { rule = "BR-CO-10", message = "Tax amount matches sum of line taxes." }, new { rule = "BR-DE-2", message = "Payment terms are shorter than recommended minimum." } },
            checkedAt = DateTime.UtcNow.ToString("o")
        });
    }

    /// <summary>Public BIS 3.0 validation (no registration required).</summary>
    [HttpPost("validate")]
    public IActionResult ValidatePublic([FromBody] Dictionary<string, object>? body)
    {
        return Ok(new
        {
            valid = true,
            errors = Array.Empty<object>(),
            warnings = new[] { new { rule = "BR-02", message = "Specification identifier is valid." }, new { rule = "BR-25", message = "Invoice number is unique." } },
            checkedAt = DateTime.UtcNow.ToString("o")
        });
    }

    // ── Peppol ──────────────────────────────────────────────────

    /// <summary>SMP capability lookup for a participant.</summary>
    [HttpGet("peppol/participants/{scheme}/{identifier}")]
    public IActionResult ParticipantLookup(string scheme, string identifier)
    {
        var firm = _firmsByPeppol($"0245:{identifier}") ?? _store.Firms.Values.First();
        return Ok(new
        {
            scheme, identifier,
            registered = true,
            documentTypes = new object[]
            {
                new { id = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2::Invoice##urn:cen.eu:en16931:2017#compliant#urn:fdc:peppol.eu:2017:poacc:billing:3.0::2.1", processIds = new[] { "urn:fdc:peppol.eu:2017:poacc:billing:01:1.0" } },
                new { id = "urn:oasis:names:specification:ubl:schema:xsd:CreditNote-2::CreditNote##urn:cen.eu:en16931:2017#compliant#urn:fdc:peppol.eu:2017:poacc:billing:3.0::2.1", processIds = new[] { "urn:fdc:peppol.eu:2017:poacc:billing:01:1.0" } }
            },
            endpoints = new[] { new { scheme = "0245", identifier, transport = "as4", address = "https://access.point/" + firm.Id } },
            name = firm.Name,
            lastCheckedAt = DateTime.UtcNow.ToString("o")
        });
    }

    /// <summary>Resolve a company to its Peppol participant (single step).</summary>
    [HttpGet("peppol/participants/resolve")]
    public IActionResult ResolveParticipant([FromQuery] string? ico = null, [FromQuery] string? dic = null, [FromQuery] string? name = null)
    {
        var firm = _store.Firms.Values.FirstOrDefault(f => f.Ico == ico || f.Dic == dic) ?? _store.Firms.Values.First();
        return Ok(new
        {
            found = true,
            peppolId = firm.PeppolId,
            scheme = "0245",
            identifier = firm.Dic,
            name = firm.Name,
            networkReady = true,
            capabilities = new { invoice = true, creditNote = true }
        });
    }

    /// <summary>Full-text search in cached Peppol directory (3.6M+ records).</summary>
    [HttpGet("peppol/directory/search")]
    public IActionResult DirectorySearch([FromQuery] string q = "s.r.o.")
    {
        var matches = _store.Firms.Values.Select(f => new
        {
            peppolId = f.PeppolId,
            participantName = f.Name,
            address = new { street = f.Address.Street, city = f.Address.City, country = "SK" },
            primaryIdentifier = new { scheme = "0245", value = f.Dic },
            registrationPriority = "primary"
        }).Take(20).ToList();
        return Ok(new
        {
            query = q,
            total = 3614207,
            returned = matches.Count,
            matches
        });
    }

    /// <summary>Sondest SMP of a participant for accepted document types.</summary>
    [HttpPost("peppol/capabilities")]
    public IActionResult Capabilities([FromBody] Dictionary<string, object>? body)
    {
        return Ok(new
        {
            participant = new { scheme = "0245", identifier = "2199999999", peppolId = "0245:2199999999" },
            networkReady = true,
            documentTypes = new object[]
            {
                new { id = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2::Invoice##urn:cen.eu:en16931:2017#compliant#urn:fdc:peppol.eu:2017:poacc:billing:3.0::2.1", processIds = new[] { "urn:fdc:peppol.eu:2017:poacc:billing:01:1.0" }, status = "ready" },
                new { id = "urn:oasis:names:specification:ubl:schema:xsd:CreditNote-2::CreditNote##urn:cen.eu:en16931:2017#compliant#urn:fdc:peppol.eu:2017:poacc:billing:3.0::2.1", processIds = new[] { "urn:fdc:peppol.eu:2017:poacc:billing:01:1.0" }, status = "ready" }
            },
            routing = new { status = "resolved", agentEndpoint = "https://access.point/agent-01" },
            checkedAt = DateTime.UtcNow.ToString("o")
        });
    }

    /// <summary>Batch verification of up to 100 participants.</summary>
    [HttpPost("peppol/participants/batch")]
    public IActionResult ParticipantsBatch([FromBody] Dictionary<string, object>? body)
    {
        var results = _store.Firms.Values.Take(3).Select(f => (object)new
        {
            peppolId = f.PeppolId,
            scheme = "0245",
            identifier = f.Dic,
            registered = true,
            documentTypesAccepted = new[] { "invoice", "credit-note" }
        }).ToList();
        return Ok(new { results, checkedCount = results.Count });
    }

    /// <summary>Company registry lookup (FSR) + Peppol status by ico.</summary>
    [HttpGet("company/lookup/{ico}")]
    public IActionResult CompanyLookup(string ico)
    {
        var firm = _store.Firms.Values.FirstOrDefault(f => f.Ico == ico) ?? _store.Firms.Values.First();
        return Ok(new
        {
            ico = firm.Ico,
            dic = firm.Dic,
            vatId = firm.VatId,
            name = firm.Name,
            registered = true,
            vatPayer = true,
            peppol = new { registered = true, peppolId = firm.PeppolId, documentTypes = new[] { "invoice", "credit-note" } },
            address = firm.Address,
            source = "Register FSR SR",
            checkedAt = DateTime.UtcNow.ToString("o")
        });
    }

    // ── Helpers ─────────────────────────────────────────────────

    private DocumentRecord FirstDoc() => _store.Documents.Values.First();
    private DocumentRecord FirstInbound() => _store.Documents.Values.First(d => d.Direction == "inbound");
    private DocumentRecord FirstOutbound() => _store.Documents.Values.First(d => d.Direction == "outbound");
    private Firm? _firmsByPeppol(string peppolId) => _store.Firms.Values.FirstOrDefault(f => f.PeppolId == peppolId);
    internal DocumentRecord GetDoc(string id) => _store.Documents.TryGetValue(id, out var d) ? d : FirstDoc();

    internal static DocumentRecord GetDocStatic(MockStore store, string id) => store.Documents.TryGetValue(id, out var d) ? d : store.Documents.Values.First();

    private DocumentRecord NewDoc(string id, string direction, string type, string otherPeppol, string otherName, string state, string status)
    {
        var firm = _store.Firms.Values.First();
        var total = 123.00;
        var now = DateTime.UtcNow.ToString("o");
        return new DocumentRecord
        {
            Id = id, DocumentId = id, Number = "2026" + id[..6].ToUpperInvariant(), Type = type, Direction = direction,
            CustomerRef = "erp-acme", FirmId = firm.Id,
            Sender = direction == "outbound"
                ? new PartySnapshot { Name = firm.Name, TaxId = firm.Dic, PeppolId = firm.PeppolId, Ico = firm.Ico }
                : new PartySnapshot { Name = otherName, TaxId = otherPeppol.Split(':')[1], PeppolId = otherPeppol, Ico = "12345678" },
            Recipient = direction == "outbound"
                ? new PartySnapshot { Name = otherName, TaxId = otherPeppol.Split(':')[1], PeppolId = otherPeppol, Ico = "87654321" }
                : new PartySnapshot { Name = firm.Name, TaxId = firm.Dic, PeppolId = firm.PeppolId, Ico = firm.Ico },
            State = state, Status = status, Currency = "EUR",
            AmountWithoutTax = 100.00, AmountTax = 23.00, AmountTotal = total,
            IssueDate = DateTime.UtcNow.Date.ToString("yyyy-MM-dd"),
            DueDate = DateTime.UtcNow.AddDays(14).Date.ToString("yyyy-MM-dd"),
            CreatedAt = now, UpdatedAt = now
        };
    }

    private void AddEvent(DocumentRecord doc, string type, string state)
    {
        var id = MockStore.NewId();
        _store.Events[id] = new EventRecord
        {
            Id = id, DocumentId = doc.Id, Type = type, State = state, Acknowledged = false,
            OccurredAt = DateTime.UtcNow.ToString("o"),
            Data = new Dictionary<string, object> { ["number"] = doc.Number, ["direction"] = doc.Direction, ["total"] = doc.AmountTotal }
        };
    }

    internal static string UblXml(DocumentRecord d)
    {
        var invoice = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2";
        var ubl = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
        var cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
        return $"<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<Invoice xmlns=\"{invoice}\" xmlns:cac=\"{ubl}\" xmlns:cbc=\"{cbc}\">"
            + $"<cbc:ID>{d.Number}</cbc:ID><cbc:IssueDate>{d.IssueDate}</cbc:IssueDate><cbc:DueDate>{d.DueDate}</cbc:DueDate><cbc:InvoiceTypeCode>380</cbc:InvoiceTypeCode><cbc:DocumentCurrencyCode>{d.Currency}</cbc:DocumentCurrencyCode>"
            + $"<cac:AccountingSupplierParty><cac:Party><cac:PartyIdentification><cbc:ID schemeID=\"0245\">{d.Sender.PeppolId}</cbc:ID></cac:PartyIdentification><cac:PartyName><cbc:Name>{d.Sender.Name}</cbc:Name></cac:PartyName></cac:Party></cac:AccountingSupplierParty>"
            + $"<cac:AccountingCustomerParty><cac:Party><cac:PartyIdentification><cbc:ID schemeID=\"0245\">{d.Recipient.PeppolId}</cbc:ID></cac:PartyIdentification><cac:PartyName><cbc:Name>{d.Recipient.Name}</cbc:Name></cac:PartyName></cac:Party></cac:AccountingCustomerParty>"
            + $"<cac:LegalMonetaryTotal><cbc:TaxExclusiveAmount>{d.AmountWithoutTax:F2}</cbc:TaxExclusiveAmount><cbc:PayableAmount>{d.AmountTotal:F2}</cbc:PayableAmount></cac:LegalMonetaryTotal>"
            + $"<cac:InvoiceLine><cbc:ID>1</cbc:ID><cbc:InvoicedQuantity unitCode=\"C62\">1</cbc:InvoicedQuantity><cbc:LineExtensionAmount>{d.AmountWithoutTax:F2}</cbc:LineExtensionAmount><cac:Item><cbc:Description>Mesačná licencia</cbc:Description><cbc:Name>licencia</cbc:Name></cac:Item><cac:Price><cbc:PriceAmount>{d.AmountWithoutTax:F2}</cbc:PriceAmount></cac:Price></cac:InvoiceLine></Invoice>";
    }

    internal static Dictionary<string, object> ToInboxItem(DocumentRecord d, bool includeUbl = false)
    {
        var i = new Dictionary<string, object>
        {
            ["id"] = d.Id, ["documentId"] = d.Id, ["number"] = d.Number, ["type"] = d.Type, ["state"] = d.State,
            ["currency"] = d.Currency,
            ["amounts"] = new { withoutTax = d.AmountWithoutTax, tax = d.AmountTax, total = d.AmountTotal },
            ["sender"] = d.Sender, ["recipient"] = d.Recipient,
            ["issueDate"] = d.IssueDate, ["dueDate"] = d.DueDate,
            ["createdAt"] = d.CreatedAt, ["updatedAt"] = d.UpdatedAt
        };
        if (includeUbl) i["ubl"] = UblXml(d);
        return i;
    }

    internal static Dictionary<string, object> ExtractionResult(Dictionary<string, object>? body)
    {
        return new Dictionary<string, object>
        {
            ["type"] = "invoice",
            ["number"] = "2026-0114",
            ["issueDate"] = DateTime.UtcNow.AddDays(-6).Date.ToString("yyyy-MM-dd"),
            ["dueDate"] = DateTime.UtcNow.AddDays(14).Date.ToString("yyyy-MM-dd"),
            ["currency"] = "EUR",
            ["totals"] = new { withoutTax = 100.00, tax = 23.00, total = 123.00 },
            ["seller"] = new { name = "Dodávateľ, s.r.o.", peppolId = "0245:2120123456", taxId = "2120123456" },
            ["buyer"] = new { name = "Odberateľ, s.r.o.", peppolId = "0245:2199999999", taxId = "2199999999" },
            ["lines"] = new object[] { new { description = "Mesačná licencia", quantity = 1, unitPrice = 100.00, vatRate = 23 } },
            ["ocr"] = new { engine = "vision-v2", confidence = 0.97, pages = 1, bytes = 131072, sourceFile = body?["filename"] ?? "scan.pdf" },
            ["warnings"] = new object[] { }
        };
    }
}
