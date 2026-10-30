# sapi-sk-proxy

SAPI-SK proxy server (.NET 10) — OAuth2 token proxy, Connector API (business JSON), Enterprise API (advanced core), and Client management for Slovak e-invoicing (ePošťák).

## Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│  sapi-sk-proxy (ASP.NET Core on :13333)                            │
│  ├── /sapi/*       → Upstream SAPI-SK (ePošťák) OAuth2 proxy       │
│  ├── /connector/* → Business-friendly JSON over SAPI               │
│  ├── /enterprise/* → Advanced core operations                      │
│  ├── /klienti/*   → Client/firm/consent management                 │
│  └── /swagger     → Swagger UI (4 categories)                      │
└─────────────────────────────────────────────────────────────────────┘
```

## Features

- **SAPI Proxy** (`/sapi/v1/*`): Direct pass-through to upstream SAPI-SK with OAuth2 client_credentials flow
- **Connector API** (`/connector/*`): Simplified business JSON, auto-handles token refresh
- **Enterprise API** (`/enterprise/*`): Low-level core operations for advanced use cases
- **Klienti API** (`/klienti/*`): CRUD for clients, firms, consents, mailboxes
- **Mailbox System**: Testbed routing for two demo participants (incoming/outgoing)
- **Swagger UI**: 4 documented categories with demo credentials
- **Health Check**: `/health` endpoint for monitoring

## API Categories

| Category | Path | Description |
|----------|------|-------------|
| SAPI | `/sapi/v1/*` | Direct upstream proxy (auth, documents, participants) |
| Connector | `/connector/v1/*` | Business JSON (invoice send, status, query) |
| Enterprise | `/enterprise/v1/*` | Core operations (batch, async, raw) |
| Klienti | `/klienti/v1/*` | Client/firm/consent management |

## Prerequisites

- .NET 10 SDK
- Upstream SAPI-SK endpoint (e.g., `https://sapi.patjo.uk`)
- OAuth2 client credentials (client_id, client_secret)

## Installation

```bash
git clone https://github.com/Rugents-masterpiece/sapi-sk-proxy.git
cd sapi-sk-proxy

# Configure
cp appsettings.Development.json appsettings.json
# Edit appsettings.json with your SAPI credentials

# Run
dotnet run --project SapiSkProxy.csproj

# Or with Docker
docker build -t sapi-sk-proxy .
docker run -d -p 13333:13333 sapi-sk-proxy
```

## Configuration (`appsettings.json`)

```json
{
  "SapiSk": {
    "BaseUrl": "https://sapi.patjo.uk",
    "ClientId": "your_client_id",
    "ClientSecret": "your_client_secret",
    "MailboxDataRoot": "/var/lib/sapi-sk-proxy/mailboxes"
  },
  "Logging": {
    "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" }
  }
}
```

## Demo Credentials (Swagger UI only)

| Field | Value |
|-------|-------|
| Client ID | `demo_swagger_client` |
| Client Secret | `demo_secret_swagger_12345` |

> Only works in Swagger UI — not valid for production upstream.

## Endpoints

### Auth
- `POST /sapi/v1/auth/token` — Get access_token (15 min) + refresh_token
- `POST /sapi/v1/auth/refresh` — Refresh access_token

### Documents
- `POST /sapi/v1/documents` — Submit invoice (UBL XML)
- `GET /sapi/v1/documents/{id}/status` — Document status
- `GET /sapi/v1/documents` — List with filters

### Participants
- `GET /sapi/v1/participants` — Search Peppol directory
- `GET /sapi/v1/participants/{id}` — Participant details

### Connector (simplified)
- `POST /connector/v1/invoices` — Send invoice from business JSON
- `GET /connector/v1/invoices/{id}` — Get status

### Klienti
- `GET /klienti/v1/clients` — List clients
- `POST /klienti/v1/clients` — Create client
- `GET /klienti/v1/firms` — List firms
- `POST /klienti/v1/consents` — Manage consents

## Deployment

```bash
# Build
dotnet publish -c Release -o ./publish

# Systemd service (example)
[Unit]
Description=SAPI-SK Proxy
After=network.target

[Service]
Type=notify
ExecStart=/usr/bin/dotnet /opt/sapi-sk-proxy/SapiSkProxy.dll
WorkingDirectory=/opt/sapi-sk-proxy
Environment=ASPNETCORE_URLS=http://0.0.0.0:13333
Restart=on-failure

[Install]
WantedBy=multi-user.target
```

## Security

- **No credentials in repo** — all secrets via `appsettings.json` (gitignored) or environment variables
- Upstream OAuth2 client credentials never logged
- Token cache in memory (no persistent storage)
- CORS restricted to known origins
- Rate limiting on auth endpoints

## Development

```bash
# Watch mode
dotnet watch run

# Tests
dotnet test

# Swagger UI
# Open http://localhost:13333/swagger
```

## License

MIT
