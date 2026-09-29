using Microsoft.AspNetCore.Mvc;
using SapiSkProxy.Models.Mock;
using SapiSkProxy.Services;

namespace SapiSkProxy.Controllers;

/// <summary>
/// Klienti — sprava klientov a firiem. Onboarding, consent, firmy, white-label,
/// integrator keys, webhook infra. Mock-backed so every op returns non-empty 200.
/// </summary>
[ApiController]
[Tags("Klienti")]
public class KlientiController : ControllerBase
{
    private readonly MockStore _store;
    public KlientiController(MockStore store) => _store = store;

    // ── Consent offers ──────────────────────────────────────────

    /// <summary>Create consent offer (pozvánka).</summary>
    [HttpPost("api/v1/consent-offers")]
    public IActionResult CreateConsentOffer([FromBody] Dictionary<string, object>? body)
    {
        var id = MockStore.NewId();
        var offer = new ConsentOffer
        {
            Id = id,
            TargetIdentifier = (MockStore.GetStr(body,"targetIdentifier")) ?? "2122701339",
            TargetIdentifierType = (MockStore.GetStr(body,"targetIdentifierType")) ?? "dic",
            CompanyName = "Beta Logistics, a.s.",
            RelationshipMode = (MockStore.GetStr(body,"relationshipMode")) ?? "technical_delegation",
            IntegrationPath = (MockStore.GetStr(body,"integrationPath")) ?? "sapi",
            RequestedInterfaces = new List<string> { "connector", "sapi" },
            Scopes = (MockStore.GetVal(body,"scopes") is System.Text.Json.JsonElement sc)
                ? sc.EnumerateArray().Select(x => x.GetString() ?? "").Where(s => s.Length > 0).ToList()
                : new List<string> { "firms:manage", "documents:send" },
            Status = "issued",
            ConsentUrl = "https://sapi.patjo.uk/consent/" + id,
            ExpiresAt = DateTime.UtcNow.AddDays(14).ToString("o"),
            CreatedAt = DateTime.UtcNow.ToString("o")
        };
        _store.Consents[id] = offer;
        return StatusCode(201, offer);
    }

    /// <summary>Get consent offer status.</summary>
    [HttpGet("api/v1/consent-offers/{offerId}")]
    public IActionResult ConsentOfferStatus(string offerId)
    {
        var o = _store.Consents.TryGetValue(offerId, out var v) ? v : _store.Consents.Values.First();
        return Ok(o);
    }

    // ── Customers ───────────────────────────────────────────────

    /// <summary>List customers.</summary>
    [HttpGet("api/v1/customers")]
    public IActionResult ListCustomers([FromQuery] int limit = 50, [FromQuery] string? cursor = null)
    {
        var items = _store.Customers.Values.Take(limit).Select(c => (object)c).ToList();
        return Ok(new { customers = items, nextCursor = "eyJjdXN..." , hasMore = false, count = items.Count });
    }

    /// <summary>Create / upsert a customer.</summary>
    [HttpPost("api/v1/customers")]
    public IActionResult CreateCustomer([FromBody] Dictionary<string, object>? body)
    {
        var firm = _store.Firms.Values.First();
        var refKey = (MockStore.GetStr(body,"customerRef")) ?? ("erp-" + DateTime.UtcNow.ToString("yyyyMMddHHmm"));
        _store.Customers[refKey] = new Customer
        {
            CustomerRef = refKey,
            FirmId = (MockStore.GetStr(body,"firmId")) ?? firm.Id,
            Name = (MockStore.GetStr(body,"name")) ?? "Nový klient, s.r.o.",
            Dic = (MockStore.GetStr(body,"dic")) ?? "2122701339",
            Ico = (MockStore.GetStr(body,"ico")) ?? "45698201",
            Relationship = (MockStore.GetStr(body,"relationship")) ?? "represented",
            Status = "active",
            IntegrationPath = (MockStore.GetStr(body,"integrationPath")) ?? "connector",
            CreatedAt = DateTime.UtcNow.ToString("o"),
            UpdatedAt = DateTime.UtcNow.ToString("o")
        };
        return StatusCode(201, _store.Customers[refKey]);
    }

    // ── Onboarding ──────────────────────────────────────────────

    /// <summary>Create onboarding request.</summary>
    [HttpPost("api/v1/onboarding-requests")]
    public IActionResult CreateOnboarding([FromBody] Dictionary<string, object>? body)
    {
        var id = MockStore.NewId();
        var req = new OnboardingRequest
        {
            Id = id,
            TargetIdentifier = (MockStore.GetStr(body,"targetIdentifier")) ?? "2122701339",
            TargetIdentifierType = (MockStore.GetStr(body,"targetIdentifierType")) ?? "dic",
            ServiceMode = (MockStore.GetStr(body,"serviceMode")) ?? "send_only",
            RelationshipMode = (MockStore.GetStr(body,"relationshipMode")) ?? "technical_delegation",
            IntegrationPath = (MockStore.GetStr(body,"integrationPath")) ?? "connector",
            Scopes = new List<string> { "documents:send", "documents:read" },
            Status = "pending",
            CreatedAt = DateTime.UtcNow.ToString("o")
        };
        _store.OnboardingRequests[id] = req;
        return StatusCode(201, req);
    }

    /// <summary>Get onboarding request.</summary>
    [HttpGet("api/v1/onboarding-requests/{id}")]
    public IActionResult GetOnboarding(string id)
    {
        var r = _store.OnboardingRequests.TryGetValue(id, out var v) ? v : _store.OnboardingRequests.Values.First();
        return Ok(r);
    }

    // ── Firms ───────────────────────────────────────────────────

    /// <summary>List accessible firms.</summary>
    [HttpGet("api/v1/firms")]
    public IActionResult ListFirms()
    {
        var items = _store.Firms.Values.Select(f => (object)new
        {
            id = f.Id, name = f.Name, ico = f.Ico, dic = f.Dic, vatId = f.VatId,
            peppolId = f.PeppolId, status = f.Status, plan = f.Plan,
            consentState = f.ConsentState, createdAt = f.CreatedAt, updatedAt = f.UpdatedAt
        }).ToList();
        return Ok(new { firms = items, count = items.Count });
    }

    /// <summary>Firm detail.</summary>
    [HttpGet("api/v1/firms/{id}")]
    public IActionResult FirmDetail(string id)
    {
        var f = _store.Firms.TryGetValue(id, out var v) ? v : _store.Firms.Values.First();
        return Ok(f);
    }

    /// <summary>Documents of a firm.</summary>
    [HttpGet("api/v1/firms/{id}/documents")]
    public IActionResult FirmDocuments(string id, [FromQuery] int limit = 20)
    {
        var docs = _store.Documents.Values.Take(limit)
            .Select(d => (object)new
            {
                id = d.Id, number = d.Number, type = d.Type, state = d.State, direction = d.Direction,
                currency = d.Currency, amountTotal = d.AmountTotal, issueDate = d.IssueDate, dueDate = d.DueDate,
                createdAt = d.CreatedAt
            }).ToList();
        return Ok(new { firmId = id, documents = docs, count = docs.Count, hasMore = false });
    }

    /// <summary>Assign firm by dic/ico.</summary>
    [HttpPost("api/v1/firms/assign")]
    public IActionResult AssignFirm([FromBody] Dictionary<string, object>? body)
    {
        var dic = (MockStore.GetStr(body,"dic")) ?? "2122701339";
        var firm = _store.Firms.Values.FirstOrDefault(f => f.Dic == dic) ?? _store.Firms.Values.First();
        firm.ConsentState = "active"; firm.Status = "active";
        return Ok(new { firmId = firm.Id, dic, status = "active", activatedAt = DateTime.UtcNow.ToString("o") });
    }

    /// <summary>Batch assign firms (max 50).</summary>
    [HttpPost("api/v1/firms/assign/batch")]
    public IActionResult AssignBatch([FromBody] Dictionary<string, object>? body)
    {
        var dics = new List<string> { "2122701339", "2020123456", "2120123456" };
        var items = _store.Firms.Values.Take(3).Select(f => (object)new { dic = f.Dic, firmId = f.Id, activated = true, status = "active" }).ToList();
        return Ok(new { assigned = items.Count, items, failed = new object[0] });
    }

    /// <summary>Create consent link (one-shot).</summary>
    [HttpPost("api/v1/firms/consent-link")]
    public IActionResult CreateConsentLink([FromBody] Dictionary<string, object>? body)
    {
        return Ok(new
        {
            consentUrl = "https://sapi.patjo.uk/consent/" + Guid.NewGuid().ToString("N"),
            dic = MockStore.GetStr(body,"dic", "2122701339"),
            expiresAt = DateTime.UtcNow.AddDays(7).ToString("o"),
            state = "pending"
        });
    }

    /// <summary>Read consent status by dic/ico.</summary>
    [HttpGet("api/v1/firms/consent-status")]
    public IActionResult ConsentStatus([FromQuery] string? dic = null, [FromQuery] string? ico = null)
    {
        var firm = _store.Firms.Values.FirstOrDefault(f => f.Dic == dic || f.Ico == ico) ?? _store.Firms.Values.First();
        return Ok(new { dic = firm.Dic, ico = firm.Ico, name = firm.Name, consentState = firm.ConsentState, checkedAt = DateTime.UtcNow.ToString("o") });
    }

    /// <summary>Register Peppol identifier for a firm.</summary>
    [HttpPost("api/v1/firms/{id}/peppol-identifiers")]
    public IActionResult RegisterPeppolId(string id, [FromBody] Dictionary<string, object>? body)
    {
        var f = _store.Firms.TryGetValue(id, out var v) ? v : _store.Firms.Values.First();
        return StatusCode(202, new { firmId = id, peppolId = "0245:" + f.Dic, status = "registering", estimatedActivationAt = DateTime.UtcNow.AddHours(6).ToString("o") });
    }

    // ── White-label participants ────────────────────────────────

    /// <summary>List managed participants.</summary>
    [HttpGet("api/v1/white-label/participants")]
    public IActionResult ListParticipants()
    {
        var items = _store.Participants.Values.Select(p => (object)p).ToList();
        return Ok(new { participants = items, count = items.Count, nextCursor = (string?)null });
    }

    /// <summary>Participant detail.</summary>
    [HttpGet("api/v1/white-label/participants/{participantId}")]
    public IActionResult ParticipantDetail(string participantId)
    {
        var p = _store.Participants.TryGetValue(participantId, out var v) ? v : _store.Participants.Values.First();
        return Ok(p);
    }

    /// <summary>Register participant from FS SR webhook / verification token.</summary>
    [HttpPost("api/v1/white-label/participants/registrations")]
    public IActionResult RegisterParticipant([FromBody] Dictionary<string, object>? body)
    {
        var id = MockStore.NewId();
        var p = new Participant
        {
            Id = id, PeppolId = "0245:" + (MockStore.GetStr(body,"dic", "2122701339")),
            Scheme = "0245", Identifier = (MockStore.GetStr(body,"dic")) ?? "2122701339",
            Name = (MockStore.GetStr(body,"companyName")) ?? "Nová firma, s.r.o.",
            Status = "pending", CreatedAt = DateTime.UtcNow.ToString("o"), UpdatedAt = DateTime.UtcNow.ToString("o")
        };
        _store.Participants[id] = p;
        return StatusCode(201, new { id, status = "pending", registration = p, smpTaskQueued = true });
    }

    /// <summary>Migrate participant with SMP migration code.</summary>
    [HttpPost("api/v1/white-label/participants/migrations")]
    public IActionResult MigrateParticipant([FromBody] Dictionary<string, object>? body)
    {
        var id = MockStore.NewId();
        var p = new Participant
        {
            Id = id, PeppolId = "0245:" + (MockStore.GetStr(body,"dic", "2020123456")),
            Scheme = "0245", Identifier = (MockStore.GetStr(body,"dic")) ?? "2020123456",
            Name = (MockStore.GetStr(body,"companyName")) ?? "Migrovaná firma, s.r.o.",
            Status = "active", CreatedAt = DateTime.UtcNow.ToString("o"), UpdatedAt = DateTime.UtcNow.ToString("o")
        };
        _store.Participants[id] = p;
        return StatusCode(202, new { id, status = "in_progress", operationId = "op_" + Guid.NewGuid().ToString("N")[..10] });
    }

    /// <summary>Request migration code for participant leaving platform.</summary>
    [HttpPost("api/v1/white-label/participants/{participantId}/migration-code")]
    public IActionResult MigrationCode(string participantId)
    {
        var p = _store.Participants.TryGetValue(participantId, out var v) ? v : _store.Participants.Values.First();
        return Ok(new { participantId = p.Id, migrationCode = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(), expiresAt = DateTime.UtcNow.AddDays(3).ToString("o") });
    }

    /// <summary>Operation status (white-label / manual_review).</summary>
    [HttpGet("api/v1/white-label/operations/{operationId}")]
    public IActionResult OperationStatus(string operationId)
    {
        return Ok(new { id = operationId, status = "succeeded", kind = "registration", startedAt = DateTime.UtcNow.AddHours(-2).ToString("o"), finishedAt = DateTime.UtcNow.AddMinutes(-35).ToString("o") });
    }

    // ── Integrator keys ─────────────────────────────────────────

    /// <summary>List integrator keys.</summary>
    [HttpGet("api/v1/integrator/keys")]
    public IActionResult ListKeys()
    {
        var items = _store.IntegratorKeys.Values.Select(k => (object)new
        {
            id = k.Id, name = k.Name, keyPrefix = k.KeyPrefix, scopes = k.Scopes, active = k.Active, createdAt = k.CreatedAt
        }).ToList();
        return Ok(new { keys = items, count = items.Count });
    }

    /// <summary>Create scoped integrator key.</summary>
    [HttpPost("api/v1/integrator/keys")]
    public IActionResult CreateKey([FromBody] Dictionary<string, object>? body)
    {
        var id = MockStore.NewId();
        var k = new IntegratorKey
        {
            Id = id, Name = (MockStore.GetStr(body,"name")) ?? "ERP scoped key",
            KeyPrefix = "sk_int_" + Guid.NewGuid().ToString("N")[..6],
            Scopes = new List<string> { "documents:send", "documents:read" },
            Active = true, CreatedAt = DateTime.UtcNow.ToString("o")
        };
        _store.IntegratorKeys[id] = k;
        // plaintext secret shown once
        return StatusCode(201, new { id = k.Id, name = k.Name, keyPrefix = k.KeyPrefix, scopes = k.Scopes, secret = "sk_int_live_" + Guid.NewGuid().ToString("N"), createdAt = k.CreatedAt });
    }

    /// <summary>Revoke integrator key.</summary>
    [HttpDelete("api/v1/integrator/keys/{keyId}")]
    public IActionResult RevokeKey(string keyId)
    {
        if (_store.IntegratorKeys.TryGetValue(keyId, out var k)) { k.Active = false; }
        return Ok(new { id = keyId, revoked = true, revokedAt = DateTime.UtcNow.ToString("o") });
    }

    /// <summary>Integrator license / plan consumption.</summary>
    [HttpGet("api/v1/integrator/licenses/info")]
    public IActionResult LicenseInfo()
    {
        var firm = _store.Firms.Values.First();
        return Ok(new
        {
            plan = new { id = "integrator", name = "Integrator", maxFirms = 100, maxDocumentsPerMonth = 50000 },
            consumption = new { firmsActive = _store.Firms.Count, documentsSent = 1248, documentsReceived = 863, usedThisMonth = 2111, creditsRemaining = 47889 },
            billingPeriod = new { from = DateTime.UtcNow.AddDays(-30).Date.ToString("yyyy-MM-dd"), to = DateTime.UtcNow.Date.ToString("yyyy-MM-dd") },
            firmId = firm.Id
        });
    }

    // ── Webhook infra (shared) ──────────────────────────────────

    /// <summary>List webhooks.</summary>
    [HttpGet("api/v1/webhooks")]
    public IActionResult ListWebhooks()
    {
        var items = _store.Webhooks.Values.Select(ConnectorController.SafeWh).ToList();
        if (items.Count == 0)
        {
            items.Add(new Dictionary<string, object>
            {
                ["id"] = "wh_default", ["url"] = "https://erp.example.com/epostak/events",
                ["events"] = new List<string> { "document.received", "document.delivered" },
                ["active"] = true, ["failedAttempts"] = 0,
                ["createdAt"] = DateTime.UtcNow.ToString("o"), ["updatedAt"] = DateTime.UtcNow.ToString("o")
            });
        }
        return Ok(new { webhooks = items, count = items.Count });
    }

    /// <summary>Create webhook subscription.</summary>
    [HttpPost("api/v1/webhooks")]
    public IActionResult CreateWebhook([FromBody] Dictionary<string, object>? body)
    {
        var id = "wh_" + Guid.NewGuid().ToString("N")[..12];
        var wh = new WebhookConfig
        {
            Id = id, Url = (MockStore.GetStr(body,"url")),
            Events = (MockStore.GetVal(body,"events") is System.Text.Json.JsonElement e) ? e.EnumerateArray().Select(x => x.GetString() ?? "").ToList() : new List<string> { "document.received" },
            Active = true, FailedAttempts = 0,
            Secret = "whsec_" + Guid.NewGuid().ToString("N")[..24],
            CreatedAt = DateTime.UtcNow.ToString("o"), UpdatedAt = DateTime.UtcNow.ToString("o")
        };
        _store.Webhooks[id] = wh;
        return StatusCode(201, ConnectorController.SafeWh(wh));
    }

    /// <summary>Webhook detail + last deliveries.</summary>
    [HttpGet("api/v1/webhooks/{id}")]
    public IActionResult WebhookDetail(string id)
    {
        var wh = _store.Webhooks.TryGetValue(id, out var v) ? v : _store.Webhooks.Values.FirstOrDefault() ??
            new WebhookConfig { Id = id, Url = "https://erp.example.com/epostak/events", Active = true, Secret = "whsec_x", CreatedAt = DateTime.UtcNow.ToString("o"), UpdatedAt = DateTime.UtcNow.ToString("o") };
        var deliver = _store.Deliveries.Values.Take(5).Select(d => (object)d).ToList();
        var result = ConnectorController.SafeWh(wh);
        result["deliveries"] = deliver;
        return Ok(result);
    }

    /// <summary>Update webhook.</summary>
    [HttpPatch("api/v1/webhooks/{id}")]
    public IActionResult PatchWebhook(string id, [FromBody] Dictionary<string, object>? body)
    {
        var wh = _store.Webhooks.TryGetValue(id, out var v) ? v : _store.Webhooks.Values.FirstOrDefault();
        if (wh != null)
        {
            if (MockStore.GetVal(body,"url") is not null) wh.Url = MockStore.GetStr(body,"url");
            wh.Active = (body != null && body.TryGetValue("active", out var av) && av is System.Text.Json.JsonElement ba) ? ba.GetBoolean() : wh.Active;
            wh.UpdatedAt = DateTime.UtcNow.ToString("o");
            return Ok(ConnectorController.SafeWh(wh));
        }
        return Ok(new { id, updated = false });
    }

    /// <summary>Delete webhook.</summary>
    [HttpDelete("api/v1/webhooks/{id}")]
    public IActionResult DeleteWebhook(string id)
    {
        _store.Webhooks.TryRemove(id, out _);
        return Ok(new { id = id, removed = true, timestamp = DateTime.UtcNow.ToString("o") });
    }

    /// <summary>Send test event to webhook.</summary>
    [HttpPost("api/v1/webhooks/{id}/test")]
    public IActionResult TestWebhook(string id)
    {
        return Ok(new { success = true, mode = "queued", testRunId = "wht_" + Guid.NewGuid().ToString("N")[..10], webhookId = id });
    }

    /// <summary>Rotate webhook secret.</summary>
    [HttpPost("api/v1/webhooks/{id}/rotate-secret")]
    public IActionResult RotateWebhookSecret(string id)
    {
        return Ok(new { id, secret = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"), rotatedAt = DateTime.UtcNow.ToString("o") });
    }

    /// <summary>Delivery history for webhook.</summary>
    [HttpGet("api/v1/webhooks/{id}/deliveries")]
    public IActionResult WebhookDeliveries(string id, [FromQuery] int limit = 20)
    {
        var items = _store.Deliveries.Values.Take(limit).Select(d => (object)new
        {
            id = d.Id, eventId = d.EventId, type = d.Type, status = d.Status, attempts = d.Attempts,
            responseStatus = d.ResponseStatus, lastAttemptAt = d.LastAttemptAt, createdAt = d.CreatedAt
        }).ToList();
        return Ok(new { webhookId = id, deliveries = items, nextCursor = "eyJ...", hasMore = false });
    }

    /// <summary>Dead-letter queue (failed terminal deliveries).</summary>
    [HttpGet("api/v1/webhook-dead-letter")]
    public IActionResult DeadLetter([FromQuery] int limit = 20)
    {
        var failed = _store.Deliveries.Values.Where(d => d.Status == "FAILED").Take(limit)
            .Select(d => (object)new { id = d.Id, eventId = d.EventId, type = d.Type, status = "FAILED", attempts = d.Attempts, responseStatus = d.ResponseStatus, lastAttemptAt = d.LastAttemptAt }).ToList();
        return Ok(new { deliveries = failed, nextCursor = (string?)null, hasMore = false });
    }

    /// <summary>Replay a failed delivery.</summary>
    [HttpPost("api/v1/webhook-dead-letter/{deliveryId}/replay")]
    public IActionResult ReplayDead(string deliveryId)
    {
        var newId = "whd_" + Guid.NewGuid().ToString("N")[..12];
        return Accepted(new { deliveryId = newId, replayedFrom = deliveryId, status = "queued" });
    }

    /// <summary>Mark failure resolved.</summary>
    [HttpPost("api/v1/webhook-dead-letter/{deliveryId}/resolve")]
    public IActionResult ResolveDead(string deliveryId, [FromBody] Dictionary<string, object>? body)
    {
        return Ok(new { deliveryId, resolved = true, reason = MockStore.GetStr(body,"reason", "acknowledged"), resolvedAt = DateTime.UtcNow.ToString("o") });
    }

    /// <summary>Cross-firm webhook queue.</summary>
    [HttpGet("api/v1/webhook-queue/all")]
    public IActionResult CrossFirmQueue([FromQuery] int limit = 20)
    {
        var items = _store.Deliveries.Values.Take(limit)
            .Select(d => (object)new { id = d.Id, firmId = _store.Firms.Values.First().Id, eventId = d.EventId, type = d.Type, status = d.Status, lastAttemptAt = d.LastAttemptAt }).ToList();
        return Ok(new { items, nextCursor = "eyJ...", hasMore = items.Count >= limit });
    }

    /// <summary>Cross-firm batch ack.</summary>
    [HttpPost("api/v1/webhook-queue/all/batch-ack")]
    public IActionResult CrossFirmAck([FromBody] Dictionary<string, object>? body)
    {
        return Ok(new { acked = 5, remaining = _store.Deliveries.Values.Count(d => d.Status != "SUCCESS"), ackedAt = DateTime.UtcNow.ToString("o") });
    }

    // ── Events / Reporting / Account ────────────────────────────

    /// <summary>Unacknowledged pull events.</summary>
    [HttpGet("api/v1/events/pull")]
    public IActionResult EventsPull([FromQuery] int limit = 20)
    {
        var items = _store.Events.Values.Where(e => !e.Acknowledged).Take(limit)
            .Select(e => (object)new { id = e.Id, type = e.Type, state = e.State, documentId = e.DocumentId, occurredAt = e.OccurredAt, data = e.Data }).ToList();
        return Ok(new { events = items, nextCursor = "eyJ...", hasMore = items.Count >= limit });
    }

    /// <summary>Ack one event.</summary>
    [HttpPost("api/v1/events/{eventId}/ack")]
    public IActionResult AckEvent(string eventId)
    {
        if (_store.Events.TryGetValue(eventId, out var e)) e.Acknowledged = true;
        return Ok(new { acked = true, eventId, ackedAt = DateTime.UtcNow.ToString("o") });
    }

    /// <summary>Batch ack events.</summary>
    [HttpPost("api/v1/events/batch-ack")]
    public IActionResult BatchAck([FromBody] Dictionary<string, object>? body)
    {
        var ids = _store.Events.Values.Take(10).Select(e => e.Id).ToList();
        foreach (var e in _store.Events.Values) e.Acknowledged = true;
        return Ok(new { acked = ids.Count, ids, ackedAt = DateTime.UtcNow.ToString("o") });
    }

    /// <summary>Document statistics.</summary>
    [HttpGet("api/v1/reporting/statistics")]
    public IActionResult Statistics()
    {
        return Ok(new
        {
            period = new { from = DateTime.UtcNow.AddDays(-30).Date.ToString("yyyy-MM-dd"), to = DateTime.UtcNow.Date.ToString("yyyy-MM-dd") },
            sent = new { total = 1248, invoices = 1191, creditNotes = 57 },
            received = new { total = 863, invoices = 812, creditNotes = 51 }
        });
    }

    /// <summary>Audit feed.</summary>
    [HttpGet("api/v1/audit")]
    public IActionResult Audit()
    {
        return Ok(new
        {
            items = new object[]
            {
                new { id = MockStore.NewId(), at = DateTime.UtcNow.AddMinutes(-12).ToString("o"), actor = "api:demo_swagger_client", action = "DOCUMENT_SENT", ip = "185.14.2.10" },
                new { id = MockStore.NewId(), at = DateTime.UtcNow.AddHours(-3).ToString("o"), actor = "api:sk_int_demo", action = "TOKEN_ISSUED", ip = "185.14.2.11" },
                new { id = MockStore.NewId(), at = DateTime.UtcNow.AddDays(-1).ToString("o"), actor = "web:ops", action = "CONSENT_GRANTED", ip = "213.81.199.1" }
            },
            nextCursor = "eyJhd..."
        });
    }

    /// <summary>Account info.</summary>
    [HttpGet("api/v1/account")]
    public IActionResult Account()
    {
        var firm = _store.Firms.Values.First();
        return Ok(new
        {
            firm = new { id = firm.Id, name = firm.Name, peppolId = firm.PeppolId, dic = firm.Dic },
            plan = new { id = "business", name = "Business", maxDocumentsPerMonth = 5000, usedThisMonth = 1248 },
            meter = new { sent = 1248, received = 863, creditsRemaining = 3752 },
            createdAt = firm.CreatedAt
        });
    }
}
