using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace SapiSkProxy.Swagger;

/// <summary>
/// Injects Slovak descriptions, parameter docs and pre-filled request/response examples
/// from Swagger/Descriptions/sapi_descriptions.json into every operation of all 4 categories.
/// (Microsoft.OpenApi 2.x — example values are System.Text.Json.Nodes.JsonNode.)
/// </summary>
public class SlovakDescriptionsOperationFilter : IOperationFilter
{
    static SlovakDescriptionsOperationFilter()
    {
        Console.WriteLine($"[SlovakDescriptions] static init done, Endpoints={Endpoints.Count}");
    }
    private static readonly Dictionary<string, JsonElement> Endpoints = Load();
    private static JsonElement CommonHeaders;
    private static JsonElement Params;

    private static Dictionary<string, JsonElement> Load()
    {
        var map = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Swagger", "Descriptions", "sapi_descriptions.json");
            if (!File.Exists(path))
                path = Path.Combine(Directory.GetCurrentDirectory(), "Swagger", "Descriptions", "sapi_descriptions.json");
            if (!File.Exists(path))
                return map;
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            Console.WriteLine($"[SlovakDescriptions] loaded {path}");
            if (doc.RootElement.TryGetProperty("endpoints", out var eps))
                foreach (var prop in eps.EnumerateObject())
                    map[prop.Name] = prop.Value.Clone();
            if (doc.RootElement.TryGetProperty("commonHeaders", out var ch)) CommonHeaders = ch.Clone();
            if (doc.RootElement.TryGetProperty("params", out var pr)) Params = pr.Clone();
        }
        catch
        {
            // best-effort
        }
        return map;
    }

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        try
        {
        var apiDesc = context.ApiDescription;
        var method = (apiDesc.HttpMethod ?? "GET").ToUpperInvariant();
        var rp = apiDesc.RelativePath ?? "";
        if (rp.Length > 0 && !rp.StartsWith("/")) rp = "/" + rp;
        var key = $"{method} {rp}";

        if (!Endpoints.TryGetValue(key, out var e))
            return;

        // ── Slovak description ──
        if (e.TryGetProperty("d", out var d))
            operation.Description = d.GetString();

        // ── Parameters (path/query/header) ──
        JsonElement pmap = default;
        var hasPmap = e.TryGetProperty("p", out pmap) && pmap.ValueKind == JsonValueKind.Object;
        foreach (var param in operation.Parameters)
        {
            if (hasPmap && pmap.TryGetProperty(param.Name, out var pd))
            {
                param.Description = pd.GetString();
                continue;
            }
            if (!string.IsNullOrEmpty(param.Description)) continue;
            if (param.In == ParameterLocation.Header &&
                CommonHeaders.ValueKind == JsonValueKind.Object &&
                CommonHeaders.TryGetProperty(param.Name, out var hd))
            {
                param.Description = hd.GetString();
                continue;
            }
            if (Params.ValueKind == JsonValueKind.Object &&
                Params.TryGetProperty(param.Name, out var gp))
            {
                param.Description = gp.GetString();
            }
        }

        // ── Request body example (media type + schema properties) ──
        if (e.TryGetProperty("b", out var b) && b.TryGetProperty("example", out var ex) &&
            operation.RequestBody != null)
        {
            foreach (var media in operation.RequestBody.Content.Values)
            {
                media.Example = ToNode(ex);
                ApplyExampleToSchema(media.Schema, ex);
            }
        }
        }
        catch
        {
            // never break swagger generation
        }
    }

    private static void ApplyExampleToSchema(IOpenApiSchema? schema, JsonElement example)
    {
        if (schema == null) return;
        if (example.ValueKind != JsonValueKind.Object) return;
        foreach (var prop in (schema.Properties ?? new Dictionary<string, IOpenApiSchema>()).ToList())
        {
            if (prop.Value is OpenApiSchema concrete && example.TryGetProperty(prop.Key, out var v))
            {
                concrete.Example = ToNode(v);
            }
        }
    }

    private static JsonNode? ToNode(JsonElement el)
    {
        try
        {
            return JsonNode.Parse(el.GetRawText());
        }
        catch
        {
            return null;
        }
    }
}
