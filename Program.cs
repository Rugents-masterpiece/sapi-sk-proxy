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
builder.Services.AddSingleton<MockStore>();

// ── Controllers ────────────────────────────────────────────────
builder.Services.AddControllers();

// ── Swagger – 4 categories ─────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    var baseDescription =
        "Proxy server API — štyri kategórie: SAPI (priamy upstream), Connector (business JSON), " +
        "Enterprise (advanced core) a Klienti (riadenie klientov / firiem / súhlasov).";

    c.SwaggerDoc("sapi", new Microsoft.OpenApi.OpenApiInfo
    {
        Title = "SAPI — Access Point Interface",
        Version = "v1",
        Description = "Priamy proxy na upstream SAPI-SK (ePošťák). Autentizácia OAuth 2.0 client_credentials.\n\n" +
                      "**Demo:** `POST /sapi/v1/auth/token` → `access_token` (15 min) + `refresh_token`.\n\n" +
                      "Prihlásenie: Client ID `demo_swagger_client`, Client Secret `demo_secret_swagger_12345`.",
        Contact = new Microsoft.OpenApi.OpenApiContact { Name = "SAPI-SK Proxy", Url = new Uri("https://sapi.patjo.uk") }
    });

    c.SwaggerDoc("connector", new Microsoft.OpenApi.OpenApiInfo
    {
        Title = "Connector API (business JSON)",
        Version = "v1",
        Description = baseDescription,
        Contact = new Microsoft.OpenApi.OpenApiContact { Name = "SAPI-SK Proxy", Url = new Uri("https://sapi.patjo.uk") }
    });

    c.SwaggerDoc("enterprise", new Microsoft.OpenApi.OpenApiInfo
    {
        Title = "Enterprise API Core",
        Version = "v1",
        Description = baseDescription,
        Contact = new Microsoft.OpenApi.OpenApiContact { Name = "SAPI-SK Proxy", Url = new Uri("https://sapi.patjo.uk") }
    });

    c.SwaggerDoc("klienti", new Microsoft.OpenApi.OpenApiInfo
    {
        Title = "Klienti — správa klientov a firiem",
        Version = "v1",
        Description = baseDescription,
        Contact = new Microsoft.OpenApi.OpenApiContact { Name = "SAPI-SK Proxy", Url = new Uri("https://sapi.patjo.uk") }
    });

    // Group filter — SAPI = Auth/Docs controllers, mock categories by controller class name
    c.DocInclusionPredicate((docName, apiDesc) =>
    {
        var ctrl = (apiDesc.ActionDescriptor as Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor)?.ControllerName ?? "";
        return docName switch
        {
            "sapi" => ctrl == "Auth" || ctrl == "Document",
            "connector" => ctrl == "Connector",
            "enterprise" => ctrl == "Enterprise",
            "klienti" => ctrl == "Klienti",
            _ => true
        };
    });

    // Security scheme – Bearer JWT
    c.AddSecurityDefinition("bearerAuth", new Microsoft.OpenApi.OpenApiSecurityScheme
    {
        Type = Microsoft.OpenApi.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "JWT access token z /sapi/v1/auth/token"
    });

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

    c.DocumentFilter<AddSecurityHeadersDocumentFilter>();
    c.OperationFilter<SlovakDescriptionsOperationFilter>();
});

var app = builder.Build();

// ── Middleware ─────────────────────────────────────────────────
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/sapi/swagger.json", "SAPI — Access Point Interface v1");
    c.SwaggerEndpoint("/swagger/connector/swagger.json", "Connector API v1");
    c.SwaggerEndpoint("/swagger/enterprise/swagger.json", "Enterprise API Core v1");
    c.SwaggerEndpoint("/swagger/klienti/swagger.json", "Klienti — správa klientov v1");
    c.RoutePrefix = "docs";
    c.DocumentTitle = "SAPI-SK Proxy — Swagger UI (4 kategórie)";
});

app.MapGet("/", () => Results.Redirect("/docs/index.html"));


// Serve raw OpenAPI JSON files (for external docs tooling)
app.UseStaticFiles();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// ── Health check ───────────────────────────────────────────────
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow.ToString("o"), categories = new[] { "sapi", "connector", "enterprise", "klienti" } }));

app.Run();
