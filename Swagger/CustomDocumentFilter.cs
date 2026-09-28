using Swashbuckle.AspNetCore.SwaggerGen;

namespace SapiSkProxy.Swagger;

public class AddSecurityHeadersDocumentFilter : IDocumentFilter
{
    public void Apply(Microsoft.OpenApi.OpenApiDocument doc, DocumentFilterContext context)
    {
        // Add X-Peppol-Participant-Id header to all document endpoints
        var documentPaths = doc.Paths.Where(p => p.Key.Contains("/document/")).ToList();

        foreach (var path in documentPaths)
        {
            foreach (var operation in path.Value.Operations)
            {
                var header = new Microsoft.OpenApi.OpenApiParameter
                {
                    Name = "X-Peppol-Participant-Id",
                    In = Microsoft.OpenApi.ParameterLocation.Header,
                    Required = true,
                    Description = "Peppol Participant ID (e.g. 0245:1234567890)"
                };
                operation.Value.Parameters.Add(header);
            }
        }

        // Add Idempotency-Key header to POST /document/send
        if (doc.Paths.TryGetValue("/sapi/document/send", out var sendPath))
        {
            if (sendPath.Operations.TryGetValue(System.Net.Http.HttpMethod.Post, out var postOp))
            {
                var header = new Microsoft.OpenApi.OpenApiParameter
                {
                    Name = "Idempotency-Key",
                    In = Microsoft.OpenApi.ParameterLocation.Header,
                    Required = true,
                    Description = "Unique key for idempotent document submission (UUID)"
                };
                postOp.Parameters.Add(header);
            }
        }

        // Add pagination parameters to list received documents
        if (doc.Paths.TryGetValue("/sapi/document/receive", out var listPath))
        {
            if (listPath.Operations.TryGetValue(System.Net.Http.HttpMethod.Get, out var getOp))
            {
                getOp.Parameters.Add(new Microsoft.OpenApi.OpenApiParameter
                {
                    Name = "pageToken",
                    In = Microsoft.OpenApi.ParameterLocation.Query,
                    Description = "Opaque cursor token for the next page"
                });
                getOp.Parameters.Add(new Microsoft.OpenApi.OpenApiParameter
                {
                    Name = "limit",
                    In = Microsoft.OpenApi.ParameterLocation.Query,
                    Description = "Maximum number of records (1-100)"
                });
                getOp.Parameters.Add(new Microsoft.OpenApi.OpenApiParameter
                {
                    Name = "status",
                    In = Microsoft.OpenApi.ParameterLocation.Query,
                    Description = "Filter by status (RECEIVED, ACKNOWLEDGED)"
                });
            }
        }
    }
}
