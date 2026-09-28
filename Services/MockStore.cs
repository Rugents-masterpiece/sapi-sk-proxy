using System.Collections.Concurrent;
using SapiSkProxy.Models.Mock;

namespace SapiSkProxy.Services;

/// <summary>
/// In-memory store for mock-backed API categories (Klienti, Connector, Enterprise).
/// Seeded with generated realistic data so every endpoint returns non-empty payloads.
/// </summary>
public class MockStore
{
    private readonly ConcurrentDictionary<string, Firm> _firms = new();
    private readonly ConcurrentDictionary<string, Customer> _customers = new();
    private readonly ConcurrentDictionary<string, ConsentOffer> _consents = new();
    private readonly ConcurrentDictionary<string, DocumentRecord> _documents = new();
    private readonly ConcurrentDictionary<string, BoxItem> _boxItems = new();
    private readonly ConcurrentDictionary<string, EventRecord> _events = new();
    private readonly ConcurrentDictionary<string, WebhookConfig> _webhooks = new();
    private readonly ConcurrentDictionary<string, DeliveryRecord> _deliveries = new();
    private readonly ConcurrentDictionary<string, Participant> _participants = new();
    private readonly ConcurrentDictionary<string, OnboardingRequest> _onboarding = new();
    private readonly ConcurrentDictionary<string, IntegratorKey> _keys = new();

    public ConcurrentDictionary<string, Firm> Firms => _firms;
    public ConcurrentDictionary<string, Customer> Customers => _customers;
    public ConcurrentDictionary<string, ConsentOffer> Consents => _consents;
    public ConcurrentDictionary<string, DocumentRecord> Documents => _documents;
    public ConcurrentDictionary<string, BoxItem> BoxItems => _boxItems;
    public ConcurrentDictionary<string, EventRecord> Events => _events;
    public ConcurrentDictionary<string, WebhookConfig> Webhooks => _webhooks;
    public ConcurrentDictionary<string, DeliveryRecord> Deliveries => _deliveries;
    public ConcurrentDictionary<string, Participant> Participants => _participants;
    public ConcurrentDictionary<string, OnboardingRequest> OnboardingRequests => _onboarding;
    public ConcurrentDictionary<string, IntegratorKey> IntegratorKeys => _keys;

    private static readonly string[] Companies =
    {
        "Alfa Trading, s.r.o.", "Beta Logistics, a.s.", "Gamma Energy, s.r.o.", "Delta Services, s.r.o.",
        "Epsilon Retail, a.s.", "Zeta Consulting, s.r.o.", "Omega Manufacturing, a.s.", "Sigma Foods, s.r.o."
    };

    private static readonly Random Rng = new();

    public MockStore()
    {
        Seed();
    }

    private void Seed()
    {
        var now = DateTime.UtcNow;
        var icos = new[] { "45698201", "31315431", "36802220", "44570192", "35912013", "46941312", "34462310", "51361011" };
        var dics = new[] { "1234567890", "2122701339", "2020123456", "2120123456", "1234501339", "2009812345", "1234512345", "2111098765" };

        for (int i = 0; i < 6; i++)
        {
            var firmId = Guid.NewGuid().ToString();
            var peppol = $"0245:{dics[i % dics.Length]}";
            _firms[firmId] = new Firm
            {
                Id = firmId,
                Name = Companies[i % Companies.Length],
                Ico = icos[i % icos.Length],
                Dic = dics[i % dics.Length],
                VatId = "SK" + dics[i % dics.Length],
                PeppolId = peppol,
                Status = i % 3 == 0 ? "pending" : "active",
                Plan = "business",
                ConsentState = i % 3 == 0 ? "pending" : "active",
                Address = new Address { Street = "Hlavná " + (10 + i), City = "Bratislava", Zip = "811" + (i + 1).ToString("D2"), Country = "SK" },
                DocumentsSent = 40 + i * 7,
                DocumentsReceived = 12 + i * 5,
                CreatedAt = now.AddDays(-30 + i).ToString("o"),
                UpdatedAt = now.AddDays(-i).ToString("o")
            };
        }
        foreach (var f in _firms.Values)
        {
            _participants[f.Id] = new Participant
            {
                Id = f.Id,
                PeppolId = f.PeppolId,
                Scheme = "0245",
                Identifier = f.Dic,
                Name = f.Name,
                Status = f.Status,
                CreatedAt = f.CreatedAt,
                UpdatedAt = f.UpdatedAt
            };
        }

        foreach (var (firmId, f) in _firms.Take(5))
        {
            _customers[firmId] = new Customer
            {
                CustomerRef = "erp-" + f.Ico,
                FirmId = firmId,
                Name = f.Name,
                Dic = f.Dic,
                Ico = f.Ico,
                Relationship = "managed",
                Status = f.Status,
                IntegrationPath = "connector",
                CreatedAt = f.CreatedAt,
                UpdatedAt = f.UpdatedAt
            };
        }

        for (int i = 0; i < 5; i++)
        {
            var id = Guid.NewGuid().ToString();
            var targetFirm = _firms.Values.ElementAt(i % _firms.Count);
            _consents[id] = new ConsentOffer
            {
                Id = id,
                TargetIdentifier = targetFirm.Dic,
                TargetIdentifierType = "dic",
                CompanyName = targetFirm.Name,
                RelationshipMode = "technical_delegation",
                IntegrationPath = i % 2 == 0 ? "connector" : "sapi",
                RequestedInterfaces = new List<string> { "connector", "sapi", "enterprise" }.GetRange(0, 1 + i % 3),
                Scopes = new List<string> { "documents:send", "documents:read", "webhooks:read", "webhooks:write" }.GetRange(0, 1 + i % 4),
                Status = i % 2 == 0 ? "issued" : "active",
                ConsentUrl = "https://sapi.patjo.uk/consent/" + id,
                ExpiresAt = now.AddDays(14 - i).ToString("o"),
                CreatedAt = now.AddDays(-10 + i).ToString("o")
            };
        }

        for (int i = 0; i < 10; i++)
        {
            var docId = Guid.NewGuid().ToString();
            var firm = _firms.Values.ElementAt(i % _firms.Count);
            var inbound = i % 3 != 0;
            var type = i % 4 == 3 ? "credit-note" : "invoice";
            var total = 100.0 + i * 12.5;
            _documents[docId] = new DocumentRecord
            {
                Id = docId,
                DocumentId = docId,
                Number = $"2026{i + 1:D6}",
                Type = type,
                Direction = inbound ? "inbound" : "outbound",
                CustomerRef = "erp-" + firm.Ico,
                FirmId = firm.Id,
                Sender = new PartySnapshot
                {
                    Name = inbound ? "Dodávateľ, s.r.o." : firm.Name,
                    Country = "SK",
                    TaxId = inbound ? "2120123456" : firm.Dic,
                    PeppolId = inbound ? "0245:2120123456" : firm.PeppolId,
                    Ico = inbound ? "12345678" : firm.Ico
                },
                Recipient = new PartySnapshot
                {
                    Name = inbound ? firm.Name : "Odberateľ, s.r.o.",
                    Country = "SK",
                    TaxId = inbound ? firm.Dic : "2199999999",
                    PeppolId = inbound ? firm.PeppolId : "0245:2199999999",
                    Ico = inbound ? firm.Ico : "87654321"
                },
                State = i % 5 == 0 ? "received" : i % 5 == 1 ? "delivered" : i % 5 == 2 ? "sent" : "processed",
                Status = i % 5 == 0 ? "RECEIVED" : i % 5 == 1 ? "DELIVERED" : i % 5 == 2 ? "SENT" : "PROCESSED",
                Currency = "EUR",
                AmountWithoutTax = Math.Round(total / 1.23, 2),
                AmountTax = Math.Round(total - total / 1.23, 2),
                AmountTotal = Math.Round(total, 2),
                IssueDate = now.AddDays(-10 + i).Date.ToString("yyyy-MM-dd"),
                DueDate = now.AddDays(18 + i).Date.ToString("yyyy-MM-dd"),
                Acknowledged = i % 4 == 0,
                CreatedAt = now.AddHours(-200 + i * 9).ToString("o"),
                UpdatedAt = now.AddHours(-i).ToString("o")
            };
        }

        foreach (var doc in _documents.Values.Take(6))
        {
            var id = Guid.NewGuid().ToString();
            _events[id] = new EventRecord
            {
                Id = id,
                DocumentId = doc.Id,
                Type = doc.Direction == "inbound" ? "document.received" : "document.sent",
                State = doc.State,
                Acknowledged = doc.Direction == "outbound",
                OccurredAt = doc.CreatedAt,
                Data = new Dictionary<string, object> { ["number"] = doc.Number, ["direction"] = doc.Direction, ["total"] = doc.AmountTotal }
            };
        }

        for (int i = 0; i < 4; i++)
        {
            var id = Guid.NewGuid().ToString();
            _boxItems[id] = new BoxItem
            {
                Id = id,
                State = i % 4 == 0 ? "queued" : i % 4 == 1 ? "sent" : i % 4 == 2 ? "scheduled" : "failed",
                ScheduledFor = i % 4 == 2 ? now.AddHours(6).ToString("o") : null,
                DocumentId = _documents.Values.ElementAt(i).Id,
                Number = _documents.Values.ElementAt(i).Number,
                CustomerRef = "erp-acme",
                CreatedAt = now.AddDays(-4 + i).ToString("o")
            };
        }

        var whId = Guid.NewGuid().ToString();
        _webhooks[whId] = new WebhookConfig
        {
            Id = whId,
            Url = "https://erp.example.com/epostak/events",
            Events = new List<string> { "document.received", "document.delivered", "document.sent", "document.mls_received" },
            Active = true,
            FailedAttempts = 0,
            Secret = "whsec_" + Guid.NewGuid().ToString("N")[..24],
            CreatedAt = now.AddDays(-15).ToString("o"),
            UpdatedAt = now.AddDays(-2).ToString("o")
        };

        for (int i = 0; i < 5; i++)
        {
            var id = Guid.NewGuid().ToString();
            var ev = _events.Values.ElementAt(i % _events.Count);
            _deliveries[id] = new DeliveryRecord
            {
                Id = id,
                WebhookId = whId,
                EventId = ev.Id,
                Type = ev.Type,
                Status = i % 3 == 2 ? "FAILED" : "SUCCESS",
                Attempts = 1 + i % 3,
                ResponseStatus = 200 + i,
                LastAttemptAt = now.AddHours(-48 + i * 8).ToString("o"),
                CreatedAt = now.AddHours(-50 + i * 8).ToString("o")
            };
        }

        var onbId = Guid.NewGuid().ToString();
        _onboarding[onbId] = new OnboardingRequest
        {
            Id = onbId,
            TargetIdentifier = "2122701339",
            TargetIdentifierType = "dic",
            ServiceMode = "send_only",
            RelationshipMode = "technical_delegation",
            IntegrationPath = "connector",
            Scopes = new List<string> { "documents:send", "documents:read" },
            Status = "pending",
            CreatedAt = now.AddDays(-3).ToString("o")
        };

        _keys["main-key"] = new IntegratorKey
        {
            Id = "main-key",
            Name = "Main integrator key",
            KeyPrefix = "sk_int_demo",
            Scopes = new List<string> { "keys:manage", "documents:send", "documents:read", "webhooks:read", "webhooks:write", "firms:manage" },
            Active = true,
            CreatedAt = now.AddDays(-60).ToString("o")
        };
        _keys["scoped-key"] = new IntegratorKey
        {
            Id = "scoped-key",
            Name = "Scoped ERP key",
            KeyPrefix = "sk_int_scop",
            Scopes = new List<string> { "documents:send", "documents:read" },
            Active = true,
            CreatedAt = now.AddDays(-20).ToString("o")
        };
    }

    public int NextIndex(int current, int max) => (current + 1) % max;

    public static string NewId() => Guid.NewGuid().ToString();
}
