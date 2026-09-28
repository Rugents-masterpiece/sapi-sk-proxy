using SapiSkProxy.Options;
using SapiSkProxy.Services;
using SapiSkProxy.Swagger;

var builder = WebApplication.CreateBuilder(args);

// ── Configuration ──────────────────────────────────────────────
builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
builder.Configuration.AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true);

var sapiOptions = builder.Configuration.GetSection(SapiSkOptions.SectionName).Get<SapiSkOptions>()
    ?? new SapiSkOptions();

builder.Services.Configure<SapiSkOptions>(builder.Configuration.GetSection(SapiSkOptions.SectionName));

// ── HTTP Client for upstream SAPI-SK ───────────────────────────
builder.Services.AddHttpClient("SapiSkUpstream", client =>
{
    client.BaseAddress = new Uri(sapiOptions.BaseUrl.TrimEnd('/') + "/");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.DefaultRequestHeaders.Add("User-Agent", "SapiSkProxy/.NET10");
});

builder.Services.AddSingleton<TokenService>();

// ── Controllers ────────────────────────────────────────────────
builder.Services.AddControllers();

// ── Swagger ────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
    {
        Title = "SAPI-SK Proxy",
        Version = "v1",
        Description = "REST API proxy pre SAPI-SK (Standardised Access Point Interface – Slovakia).\n\n" +
                      "Tento proxy smeruje požiadavky na SAPI-SK upstream API a poskytuje Swagger UI pre interaktívne testovanie.\n\n" +
                      "**Prihlásenie používateľa:**\n" +
                      "- Client ID: `demo_swagger_client`\n" +
                      "- Client Secret: `demo_secret_swagger_12345`\n" +
                      "- Grant Type: `client_credentials`\n" +
                      "- Token endpoint: `POST /sapi/auth/token`\n\n" +
                      "**Ako použiť:**\n" +
                      "1. Zavolajte `POST /sapi/auth/token` s Client ID, Client Secret a grant type.\n" +
                      "2. Skopírujte získaný access token.\n" +
                      "3. Kliknite na `Authorize` a vložte access token.\n" +
                      "4. Používajte dokumentové endpointy s headerom `X-Peppol-Participant-Id`",
        Contact = new Microsoft.OpenApi.OpenApiContact
        {
            Name = "Slovak OpenPeppol Community",
            Url = new Uri("https://www.sapi-sk.sk")
        }
    });

    // Security scheme – Bearer JWT
    c.AddSecurityDefinition("bearerAuth", new Microsoft.OpenApi.OpenApiSecurityScheme
    {
        Type = Microsoft.OpenApi.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "JWT access token z /auth/token alebo /auth/renew"
    });

    // Security requirement using the 2.x API
    var secRef = new Microsoft.OpenApi.OpenApiSecuritySchemeReference("bearerAuth", null, null);
    var secReq = new Microsoft.OpenApi.OpenApiSecurityRequirement();
    secReq.Add(secRef, new List<string>());
    c.AddSecurityRequirement(_ => secReq);

    // Include XML comments
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        c.IncludeXmlComments(xmlPath);

    c.UseInlineDefinitionsForEnums();

    // Custom document filter for headers
    c.DocumentFilter<AddSecurityHeadersDocumentFilter>();
});

var app = builder.Build();

// ── Middleware ─────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "SAPI-SK Proxy v1");
        c.RoutePrefix = "docs";
        c.DocumentTitle = "SAPI-SK Proxy – Swagger UI";
    });

    // Default route → redirect to Swagger UI
    app.MapGet("/", () => Results.Redirect("/docs"));
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// ── Health check ───────────────────────────────────────────────
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow.ToString("o") }));

app.Run();
