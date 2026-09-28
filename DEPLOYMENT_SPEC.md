# ZADANIE: Nasadenie SAPI-SK Proxy na verejný hosting (Cloudflare)

## 1. CIEĽ

Prevziať existujúci .NET 10 Web API projekt a nasadiť ho na verejný hosting za Cloudflare proxy s HTTPS. Projekty má byť plne funkčné REST API proxy pre SAPI-SK (Standardised Access Point Interface – Slovakia) s Swagger UI, autentizáciou JWT, podporou Peppol dokumentov a voliteľnou PostgreSQL databázou pre logging/audit.

---

## 2. REPOZITÁR A ŠTRUKTÚRA

**Cesta:** `/home/jamailo/workspace/sapi-sk-proxy/`

**Štruktúra:**
```
sapi-sk-proxy/
├── Controllers/
│   ├── AuthController.cs          # POST /sapi/auth/token, GET /sapi/auth/token/status, POST /sapi/auth/renew, POST /sapi/auth/revoke
│   └── DocumentController.cs      # POST /sapi/document/send, GET /sapi/document/receive, GET /sapi/document/receive/{id}, POST /sapi/document/receive/{id}/acknowledge
├── Models/
│   ├── SapiError.cs               # SAPIError, ErrorDetail, ErrorResponseEnvelope
│   ├── Auth/
│   │   ├── TokenRequest.cs
│   │   ├── TokenResponse.cs
│   │   └── TokenStatusResponse.cs
│   └── Documents/
│       ├── DocumentMetadata.cs
│       ├── SendDocumentRequest.cs
│       ├── SendDocumentResponse.cs
│       ├── ReceivedDocumentListResponse.cs
│       ├── ReceivedDocumentDetailResponse.cs
│       └── AcknowledgeResponse.cs
├── Services/
│   └── TokenService.cs            # JWT token lifecycle (acquire, refresh, revoke, status)
├── Options/
│   └── SapiSkOptions.cs           # Konfigurácia: BaseUrl, ClientId, ClientSecret, DefaultParticipantId
├── Swagger/
│   └── CustomDocumentFilter.cs    # Swagger filter — pridáva X-Peppol-Participant-Id header do dokumentácie
├── GlobalUsings.cs                 # Globálne usingy
├── Program.cs                      # Entry point — DI, Swagger, Kestrel, routing
├── appsettings.json                # Konfigurácia (BaseUrl, credentials, participant ID)
├── appsettings.Development.json    # (voliteľné)
├── sapi-sk-proxy.csproj            # Projekt file (.NET 10, Swashbuckle, Microsoft.OpenApi)
├── Properties/
│   └── launchSettings.json         # launch profiles (http :13333, https :13334)
├── certs/
│   └── https.pfx                   # Self-signed dev cert (len pre lokálny dev)
└── start.sh                        # Spúšťací skript (len pre lokálny dev)
```

**Technológie:**
- .NET 10 (ASP.NET Core Web API)
- Swashbuckle.AspNetCore 10.2.3 (Swagger UI)
- Microsoft.OpenApi 2.7.5
- System.Text.Json (JSON serializácia)
- PostgreSQL (voliteľne — pre logging/audit)

---

## 3. ČO PROJEKT ROBÍ

Tento projekt je **REST API proxy** pre [SAPI-SK](https://www.sapi-sk.sk/) — Standardised Access Point Interface pre Slovensko v rámci Peppol siete.

### Fungovanie:
1. Klient pošle požiadavku na náš proxy (napr. `POST /sapi/auth/token`)
2. Proxy ju forwarduje na upstream SAPI-SK API (`https://www.sapi-sk.sk/sapi/...`)
3. Odozva sa vráti klientovi
4. JWT tokeny sa automaticky refreshujú
5. `X-Peppol-Participant-Id` header sa pridáva automaticky

### Endpoints:

| Metóda | Route | Popis |
|--------|-------|-------|
| `POST` | `/sapi/auth/token` | Získanie JWT tokenu (OAuth 2.0 client_credentials) |
| `GET` | `/sapi/auth/token/status` | Stav tokenu |
| `POST` | `/sapi/auth/renew` | Refresh tokenu |
| `POST` | `/sapi/auth/revoke` | Revokácia tokenu |
| `POST` | `/sapi/document/send` | Odoslanie Peppol dokumentu |
| `GET` | `/sapi/document/receive` | Zoznam prijatých dokumentov (paginácia) |
| `GET` | `/sapi/document/receive/{id}` | Detail dokumentu |
| `POST` | `/sapi/document/receive/{id}/acknowledge` | Potvrdenie prijatia dokumentu |
| `GET` | `/health` | Health check |

### Swagger UI:
- Dostupné na `/docs`
- Podpora Bearer JWT autentifikácie cez "Authorize" tlačidlo
- Custom headers (X-Peppol-Participant-Id, Idempotency-Key) sú dokumentované

---

## 4. KONFIGURÁCIA

### 4.1 appsettings.json (hlavná konfigurácia)

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "SapiSkProxy": "Debug"
    }
  },
  "AllowedHosts": "*",
  "SapiSk": {
    "BaseUrl": "https://www.sapi-sk.sk/sapi",
    "ClientId": "VASE_CLIENT_ID",
    "ClientSecret": "VASE_CLIENT_SECRET",
    "DefaultParticipantId": "0245:1234567890",
    "RefreshAheadSeconds": 60
  }
}
```

**Kľúčové polia:**
- `BaseUrl` — URL upstream SAPI-SK API (predvolené: `https://www.sapi-sk.sk/sapi`)
- `ClientId` / `ClientSecret` — OAuth 2.0 credentials od SAPI-SK poskytovateľa
- `DefaultParticipantId` — Peppol Participant ID predvolené pre všetky requesty (formát: `0245:1234567890`)
- `RefreshAheadSeconds` — o koľko sekúnd pred expiráciou refreshnúť token

### 4.2 Environment Variables (pre produkciu)

```bash
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://0.0.0.0:8080;https://0.0.0.0:8443
ASPNETCORE_HTTPS_PORT=8443
SAPISK_BASEURL=https://www.sapi-sk.sk/sapi
SAPISK_CLIENTID=your_client_id
SAPISK_CLIENTSECRET=your_client_secret
SAPISK_DEFAULTPARTICIPANTID=0245:1234567890
DB_CONNECTION_STRING=Host=db-host;Port=5432;Database=sapisk;User Id=...;Password=...
```

### 4.3 PostgreSQL (voliteľné — logging/audit)

Ak máš nasadiť databázu:

```sql
-- Vytvorenie tabuliek
CREATE TABLE IF NOT EXISTS document_audit (
    id BIGSERIAL PRIMARY KEY,
    document_id VARCHAR(255) NOT NULL,
    document_type VARCHAR(500),
    sender_participant_id VARCHAR(100),
    receiver_participant_id VARCHAR(100),
    action VARCHAR(50) NOT NULL,  -- 'SEND', 'RECEIVE', 'ACKNOWLEDGE'
    status VARCHAR(50),
    payload_hash VARCHAR(64),  -- SHA-256 hash payloadu
    created_at TIMESTAMPTZ DEFAULT NOW(),
    metadata JSONB
);

CREATE INDEX idx_audit_document_id ON document_audit(document_id);
CREATE INDEX idx_audit_created_at ON document_audit(created_at);
CREATE INDEX idx_audit_action ON document_audit(action);

CREATE TABLE IF NOT EXISTS token_log (
    id BIGSERIAL PRIMARY KEY,
    client_id VARCHAR(255),
    token_type VARCHAR(20),  -- 'access', 'refresh'
    action VARCHAR(20) NOT NULL,  -- 'ISSUED', 'REFRESHED', 'REVOKED'
    ip_address VARCHAR(45),
    created_at TIMESTAMPTZ DEFAULT NOW()
);
```

---

## 5. INŠTAČNÉ KROKY

### 5.1 Lokálne spustenie (dev)

```bash
cd /home/jamailo/workspace/sapi-sk-proxy
dotnet restore
dotnet build
dotnet run --urls="https://0.0.0.0:13334;http://0.0.0.0:13333"
```

Swagger UI: `https://localhost:13334/docs`

### 5.2 Build a Publish

```bash
cd /home/jamailo/workspace/sapi-sk-proxy
dotnet publish -c Release -o ./publish
```

Výstup: `./publish/sapi-sk-proxy.dll`

### 5.3 Docker (voliteľné — odporúčané)

Vytvor `Dockerfile`:

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8443

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY sapi-sk-proxy.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM build AS publish
RUN dotnet publish sapi-sk-proxy.csproj -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080;https://+:8443
ENV ASPNETCORE_HTTPS_PORT=8443
ENTRYPOINT ["dotnet", "sapi-sk-proxy.dll"]
```

```bash
docker build -t sapi-sk-proxy .
docker run -d \
  -p 8080:8080 -p 8443:8443 \
  -e SAPISK_CLIENTID=your_id \
  -e SAPISK_CLIENTSECRET=your_secret \
  -e DB_CONNECTION_STRING="Host=db;Port=5432;Database=sapisk;..." \
  --name sapi-sk-proxy \
  sapi-sk-proxy
```

### 5.4 Nginx + Cloudflare Tunnel (odporúčané pre produkciu)

```nginx
# /etc/nginx/sites-available/sapi-sk-proxy
server {
    listen 80;
    server_name api.tvoja-domena.sk;

    location / {
        proxy_pass http://127.0.0.1:8080;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_read_timeout 300s;
        proxy_send_timeout 300s;
    }
}
```

Alebo **Cloudflare Tunnel** (bez verejnej IP):

```bash
# Inštalácia cloudflared
curl -L https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-linux-amd64 -o /usr/local/bin/cloudflared
chmod +x /usr/local/bin/cloudflared

# Konfigurácia
cloudflared tunnel --config /etc/cloudflared/config.yml create sapi-sk-proxy

# config.yml
tunnel: <TUNNEL_UUID>
credentials-file: /etc/cloudflared/<TUNNEL_UUID>.json

ingress:
  - hostname: api.tvoja-domena.sk
    service: http://localhost:8080
  - service: http_status:404
```

---

## 6. TRÉNINGOVÉ DÁTA

### 6.1 Demo credentials od SAPI-SK

Pre testovanie použi demo credentials od SAPI-SK:
- **ClientId:** `demo_swagger_client`
- **ClientSecret:** `demo_secret_swagger_12345`
- **ParticipantId:** `0245:1234567890`

### 6.2 Príklad requestu — získanie tokenu

```bash
curl -X POST https://api.tvoja-domena.sk/sapi/auth/token \
  -H "Content-Type: application/json" \
  -d '{
    "client_id": "demo_swagger_client",
    "client_secret": "demo_secret_swagger_12345",
    "grant_type": "client_credentials"
  }'
```

Odpoveď:
```json
{
  "access_token": "eyJhbGciOiJIUzI1NiIs...",
  "token_type": "Bearer",
  "expires_in": 900,
  "scope": null
}
```

### 6.3 Príklad requestu — odoslanie dokumentu

```bash
TOKEN="eyJhbGciOiJIUzI1NiIs..."

curl -X POST https://api.tvoja-domena.sk/sapi/document/send \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: 550e8400-e29b-41d4-a716-446655440000" \
  -H "X-Peppol-Participant-Id: 0245:1234567890" \
  -d '{
    "metadata": {
      "documentId": "INV-2026-0001",
      "documentTypeId": "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2::Invoice##urn:cen.eu:en16931:2017#compliant#urn:fdc:peppol.eu:2017:poacc:billing:3.0::2.1",
      "processId": "urn:fdc:peppol.eu:2017:poacc:billing:01:1.0",
      "senderParticipantId": "0245:1234567890",
      "receiverParticipantId": "0245:9876543210",
      "creationDateTime": "2026-08-21T10:30:00Z"
    },
    "payload": "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Invoice xmlns=\"urn:oasis:names:specification:ubl:schema:xsd:Invoice-2\"><cbc:ID>INV-2026-0001</cbc:ID></Invoice>",
    "payloadFormat": "XML"
  }'
```

### 6.4 Príklad requestu — zoznam dokumentov

```bash
curl -X GET "https://api.tvoja-domena.sk/sapi/document/receive?limit=10&pageToken=" \
  -H "Authorization: Bearer $TOKEN" \
  -H "X-Peppol-Participant-Id: 0245:1234567890"
```

### 6.5 Príklad requestu — potvrdenie prijatia

```bash
curl -X POST "https://api.tvoja-domena.sk/sapi/document/receive/INV-2026-0001/acknowledge" \
  -H "Authorization: Bearer $TOKEN" \
  -H "X-Peppol-Participant-Id: 0245:1234567890"
```

---

## 7. DEPLOYMENT CHECKLIST

- [ ] Nainštalovať .NET 10 Runtime na server
- [ ] Skonfigurovať `appsettings.Production.json` alebo environment variables
- [ ] Nahrať PFX certifikát alebo použiť Let's Encrypt (ACME)
- [ ] Nasadiť Nginx ako reverse proxy na porte 80/443
- [ ] Nastaviť Cloudflare proxy (DNS CNAME → tunnel)
- [ ] Nastaviť firewall (len porty 80, 443, ssh)
- [ ] Nastaviť log rotation (journalctl / Nginx logs)
- [ ] Nastaviť monitoring (health endpoint `/health`)
- [ ] Nastaviť automatický restart (systemd service)
- [ ] (Voliteľne) Nasadiť PostgreSQL a pripojiť connectionString
- [ ] (Voliteľne) Vytvoriť seed data pre dokumenty
- [ ] (Voliteľne) Pridať rate limiting
- [ ] Otestovať všetky endpointy cez Swagger UI
- [ ] Otestovať HTTPS certifikát (SSL Labs)

### 7.1 Systemd Service (ak nie je Docker)

```ini
# /etc/systemd/system/sapi-sk-proxy.service
[Unit]
Description=SAPI-SK Proxy API
After=network.target

[Service]
Type=notify
WorkingDirectory=/opt/sapi-sk-proxy
ExecStart=/usr/share/dotnet/dotnet /opt/sapi-sk-proxy/sapi-sk-proxy.dll
Restart=always
RestartSec=10
User=www-data
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://127.0.0.1:8080
Environment=SAPISK_CLIENTID=your_client_id
Environment=SAPISK_CLIENTSECRET=your_client_secret

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl enable sapi-sk-proxy
sudo systemctl start sapi-sk-proxy
sudo systemctl status sapi-sk-proxy
```

---

## 8. DÔLEŽITÉ POZNÁMKY

1. **Self-signed certifikát** v `certs/https.pfx` je len pre lokálny dev. V produkcii použi Let's Encrypt alebo Cloudflare Origin Cert.

2. **Microsoft.OpenApi** verzia 2.7.5 je transitive dependency od Swashbuckle 10.x. Má špecifické API — žiadny `Models` namespace, typy sú v `Microsoft.OpenApi` priamo.

3. **TokenService** je singleton a spravuje JWT lifecycle. Automaticky refreshuje tokeny 60s pred expiráciou.

4. **Upstream URL** je `https://www.sapi-sk.sk/sapi` — všetky endpointy majú prefix `/sapi`. Proxy ho pridáva automaticky.

5. **Idempotency-Key** header je povinný pre `POST /sapi/document/send`. Je to UUID, ktoré zabezpečí, že rovnaký request sa nepočíta dvakrát.

6. **X-Peppol-Participant-Id** header je povinný pre všetky dokument endpointy. Ak nie je zadaný, použije sa `DefaultParticipantId` z konfigurácie.

7. **Chybové response** používajú konzistentný `SapiError` objekt s fieldami: `category`, `code`, `message`, `retryable`, `correlation_id`.

8. **Rate limiting** nie je implementovaný. Ak je potrebné, pridaj `Microsoft.AspNetCore.RateLimiting` middleware.

9. **Databáza** nie je súčasťou základného projektu. Je voliteľná pre logging/audit. Ak ju nepotrebuješ, projekt funguje bez nej.

---

## 9. KONTAKT / OTÁZKY

Ak niečo nie je jasné, over si:
- OpenAPI špecifikáciu upstreamu: `https://www.sapi-sk.sk/openapi.json`
- Dokumentáciu: `https://www.sapi-sk.sk/docs`
- Dema credentials: `demo_swagger_client` / `demo_secret_swagger_12345`
