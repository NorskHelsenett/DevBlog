using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using AddressWebApi.Dtos;

namespace AddressWebApi;

public static class QueryEndpoints
{
    private static string RequestCorrelationId(this HttpContext http)
    {
        var correlationId = System.Guid.NewGuid().ToString("D");
        if(http.Request.Headers.TryGetValue("X-Correlation-Id", out Microsoft.Extensions.Primitives.StringValues value))
        {
            if(!string.IsNullOrWhiteSpace(value.ToString()))
            {
                correlationId = value.ToString();
            }
        }
        return correlationId;
    }
    public static void MapQueryEndpoints(this WebApplication app)
    {

        var options = new JsonSerializerOptions
        {
            // Encoder = JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.BasicLatin,),
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // Remember to set the app/json content type in the response to make it safe against people piping it weird places
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
            }
        };

        app.MapPost("/query", (HttpContext http, Query postContent, IAddressStorage addressStorage, CancellationToken cancellationToken) =>
        {
            // var bodyStream = await http.Request.ReadFromJsonAsync<Query>();
            app.Logger.LogInformation("Received query: {query}", JsonSerializer.Serialize(postContent, options));
            var correlationId = http.RequestCorrelationId();
            http.Response.Headers.Append("X-Correlation-Id", correlationId);

            try
            {
                var resultStatus = addressStorage.TryQuery(postContent, correlationId, cancellationToken, out var foundAddresses);
                http.Response.Headers.Append("X-Query-Results-Count", resultStatus.AdditionalInfo?["resultCount"]);
                switch (resultStatus.Type)
                {
                    case ResultStatusTypes.Success:
                        http.Response.Headers.Append("X-Query-Result-Status", "Success");
                        break;
                    case ResultStatusTypes.Warning:
                        http.Response.Headers.Append("X-Query-Result-Status", "Warning");
                        http.Response.Headers.Append("X-Warning-Message", resultStatus.AdditionalInfo?["message"]);
                        http.Response.Headers.Append("X-Warning-Reason", resultStatus.AdditionalInfo?["reason"]);
                        break;
                    case ResultStatusTypes.Error:
                        http.Response.Headers.Append("X-Query-Result-Status", "Error");
                        http.Response.Headers.Append("X-Error-Message", resultStatus.AdditionalInfo?["message"]);
                        http.Response.Headers.Append("X-Error-Reason", resultStatus.AdditionalInfo?["reason"]);
                        break;
                    default:
                        return Results.Text(
                            content: $"Query failed",
                            contentType: "text/html",
                            contentEncoding: Encoding.UTF8,
                            statusCode: (int?) HttpStatusCode.InternalServerError);
                }

                return Results.Text(
                    content: System.Text.Json.JsonSerializer.Serialize(foundAddresses, options),
                    contentEncoding: Encoding.UTF8,
                    contentType: "application/json"
                );
            }
            catch (Exception e)
            {
                using var scope = app.Logger.BeginScope("CorrelationId: {correlationId}", correlationId);
                app.Logger.LogError(e, "Got exception while processing query");

                http.Response.Headers.Append("X-Query-Result-Status", "Error");
                http.Response.Headers.Append("X-Error-Reason", "Exception");
                return Results.Text(
                    content: $"Query failed",
                    contentType: "text/html",
                    contentEncoding: Encoding.UTF8,
                    statusCode: (int?) HttpStatusCode.InternalServerError);
            }
        })
        .WithOpenApi()
        .Produces<IEnumerable<Dtos.CadastreRoadAddress>>(StatusCodes.Status200OK)
        ;
    }
}
