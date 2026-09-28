namespace SapiSkProxy.Models.Mock;

// ── Common shapes for mock-backed API categories ───────────────────

public class Address
{
    public string Street { get; set; } = "";
    public string City { get; set; } = "";
    public string Zip { get; set; } = "";
    public string Country { get; set; } = "SK";
}

public class PartySnapshot
{
    public string Name { get; set; } = "";
    public string Country { get; set; } = "SK";
    public string? Ico { get; set; }
    public string? TaxId { get; set; }
    public string? VatId { get; set; }
    public string? PeppolId { get; set; }
}

public class Firm
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Ico { get; set; } = "";
    public string Dic { get; set; } = "";
    public string? VatId { get; set; }
    public string PeppolId { get; set; } = "";
    public string Status { get; set; } = "active";
    public string Plan { get; set; } = "business";
    public string ConsentState { get; set; } = "active";
    public Address Address { get; set; } = new();
    public int DocumentsSent { get; set; }
    public int DocumentsReceived { get; set; }
    public string CreatedAt { get; set; } = "";
    public string UpdatedAt { get; set; } = "";
}

public class Customer
{
    public string CustomerRef { get; set; } = "";
    public string FirmId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Dic { get; set; } = "";
    public string? Ico { get; set; }
    public string Relationship { get; set; } = "managed";
    public string Status { get; set; } = "active";
    public string IntegrationPath { get; set; } = "connector";
    public string CreatedAt { get; set; } = "";
    public string UpdatedAt { get; set; } = "";
}

public class ConsentOffer
{
    public string Id { get; set; } = "";
    public string TargetIdentifier { get; set; } = "";
    public string TargetIdentifierType { get; set; } = "dic";
    public string CompanyName { get; set; } = "";
    public string RelationshipMode { get; set; } = "technical_delegation";
    public string IntegrationPath { get; set; } = "connector";
    public List<string> RequestedInterfaces { get; set; } = new();
    public List<string> Scopes { get; set; } = new();
    public string Status { get; set; } = "issued";
    public string ConsentUrl { get; set; } = "";
    public string ExpiresAt { get; set; } = "";
    public string CreatedAt { get; set; } = "";
}

public class OnboardingRequest
{
    public string Id { get; set; } = "";
    public string TargetIdentifier { get; set; } = "";
    public string TargetIdentifierType { get; set; } = "dic";
    public string ServiceMode { get; set; } = "send_only";
    public string RelationshipMode { get; set; } = "technical_delegation";
    public string IntegrationPath { get; set; } = "connector";
    public List<string> Scopes { get; set; } = new();
    public string Status { get; set; } = "pending";
    public string? FirmId { get; set; }
    public string CreatedAt { get; set; } = "";
}

public class Participant
{
    public string Id { get; set; } = "";
    public string PeppolId { get; set; } = "";
    public string Scheme { get; set; } = "0245";
    public string Identifier { get; set; } = "";
    public string Name { get; set; } = "";
    public string Status { get; set; } = "active";
    public string CreatedAt { get; set; } = "";
    public string UpdatedAt { get; set; } = "";
}

public class IntegratorKey
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string KeyPrefix { get; set; } = "sk_int_demo";
    public List<string> Scopes { get; set; } = new();
    public bool Active { get; set; }
    public string CreatedAt { get; set; } = "";
}

public class DocumentRecord
{
    public string Id { get; set; } = "";
    public string DocumentId { get; set; } = "";
    public string Number { get; set; } = "";
    public string Type { get; set; } = "invoice";
    public string Direction { get; set; } = "inbound";
    public string CustomerRef { get; set; } = "";
    public string FirmId { get; set; } = "";
    public PartySnapshot Sender { get; set; } = new();
    public PartySnapshot Recipient { get; set; } = new();
    public string State { get; set; } = "received";
    public string Status { get; set; } = "RECEIVED";
    public string Currency { get; set; } = "EUR";
    public double AmountWithoutTax { get; set; }
    public double AmountTax { get; set; }
    public double AmountTotal { get; set; }
    public string IssueDate { get; set; } = "";
    public string DueDate { get; set; } = "";
    public bool Acknowledged { get; set; }
    public string CreatedAt { get; set; } = "";
    public string UpdatedAt { get; set; } = "";
}

public class BoxItem
{
    public string Id { get; set; } = "";
    public string State { get; set; } = "queued";
    public string? ScheduledFor { get; set; }
    public string DocumentId { get; set; } = "";
    public string Number { get; set; } = "";
    public string CustomerRef { get; set; } = "";
    public int Attempts { get; set; }
    public string CreatedAt { get; set; } = "";
}

public class EventRecord
{
    public string Id { get; set; } = "";
    public string DocumentId { get; set; } = "";
    public string Type { get; set; } = "";
    public string State { get; set; } = "";
    public bool Acknowledged { get; set; }
    public string OccurredAt { get; set; } = "";
    public Dictionary<string, object> Data { get; set; } = new();
}

public class WebhookConfig
{
    public string Id { get; set; } = "";
    public string? Url { get; set; }
    public List<string> Events { get; set; } = new();
    public bool Active { get; set; }
    public int FailedAttempts { get; set; }
    public string Secret { get; set; } = "";
    public string CreatedAt { get; set; } = "";
    public string UpdatedAt { get; set; } = "";
}

public class DeliveryRecord
{
    public string Id { get; set; } = "";
    public string WebhookId { get; set; } = "";
    public string EventId { get; set; } = "";
    public string Type { get; set; } = "";
    public string Status { get; set; } = "SUCCESS";
    public int Attempts { get; set; }
    public int ResponseStatus { get; set; }
    public string? NextRetryAt { get; set; }
    public string LastAttemptAt { get; set; } = "";
    public string CreatedAt { get; set; } = "";
}
