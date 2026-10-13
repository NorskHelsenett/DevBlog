global using static EnvVarNames;

using System.Net;
using System.Text;

using ILoggerFactory factory = LoggerFactory.Create(
    builder =>
    {
        builder.SetMinimumLevel(LogLevel.Information);
        // builder.SetMinimumLevel(LogLevel.Debug);
        builder.AddConsole();
    });
ILogger logger = factory.CreateLogger("KeyValueApi");

logger.LogInformation("Starting up");

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

builder.Services.AddHttpClient();
builder.Services.AddSingleton<KafkaAdminClient>();

bool authEnabled = Environment.GetEnvironmentVariable(KV_API_DISABLE_API_AUTH)?.ToLowerInvariant() != "true";
if(authEnabled)
{
    builder.Services.AddOidcAuth();
}
else
{
    logger.LogInformation($"Environment variable {KV_API_DISABLE_API_AUTH} set to true, not setting up auth");
}

bool writeEnabled = Environment.GetEnvironmentVariable(KV_API_DISABLE_WRITE)?.ToLowerInvariant() != "true";
bool readEnabled = Environment.GetEnvironmentVariable(KV_API_DISABLE_READ)?.ToLowerInvariant() != "true";

if(readEnabled)
{
    var configuredStorageType = Environment.GetEnvironmentVariable(KV_API_STATE_STORAGE_TYPE)?.ToLowerInvariant();
    switch (configuredStorageType)
    {
        case "sqlite":
            logger.LogInformation($"Setting up local state storage to use SQLite");
            builder.Services.AddSingleton<IKeyValueStateService, KeyValueStateInSQLiteService>();
            break;
        case "disk":
            logger.LogInformation($"Setting up local state storage to use disk");
            builder.Services.AddSingleton<IKeyValueStateService, KeyValeStateOnFileSystemService>();
            break;
        case "dict":
            logger.LogInformation($"Setting up local state storage to use in memory dict");
            builder.Services.AddSingleton<IKeyValueStateService, KeyValueStateInDictService>();
            break;
        default:
            logger.LogInformation($"Environment variable {KV_API_STATE_STORAGE_TYPE} not set. Valid values are [dict, disk, sqlite]. Setting up default option.");
            logger.LogInformation($"Setting up local state storage to use SQLite");
            builder.Services.AddSingleton<IKeyValueStateService, KeyValueStateInSQLiteService>();
            break;
    }

    builder.Services.AddHostedService<KafkaConsumerService>();
}
else
{
    logger.LogInformation($"Environment variable {KV_API_DISABLE_READ} set to true, not setting up read services and endpoints");
}
if(writeEnabled)
{
    builder.Services.AddSingleton<KafkaProducerService>();
}
else
{
    logger.LogInformation($"Environment variable {KV_API_DISABLE_WRITE} set to true, not setting up write services and endpoints");
}

SQLitePCL.Batteries.Init();
var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseHttpsRedirection();

if(authEnabled)
{
    app.UseAuthorization();
}

app.MapControllers();
// app.MapGroup(prefix: "").RequireAuthorization();


if(writeEnabled)
{
    app.MapPost("/store", (HttpContext http, ApiParamStore postContent, KafkaProducerService kafkaProducerService) =>
    {
        var correlationIdValue = System.Guid.NewGuid().ToString("D");
        if(http.Request.Headers.TryGetValue("X-Correlation-Id", out Microsoft.Extensions.Primitives.StringValues value))
        {
            if(!string.IsNullOrWhiteSpace(value.ToString()))
            {
                correlationIdValue = value.ToString();
            }
        }
        if(postContent.Headers?.ContainsKey("Correlation-Id") ?? false) correlationIdValue = postContent.Headers["Correlation-Id"];

        var correlationId = new CorrelationId { Value = correlationIdValue };

        http.Response.Headers.Append("X-Correlation-Id", correlationId.Value);

        var eventKeyBytes = System.Text.Encoding.UTF8.GetBytes(postContent.Key);
        var eventValueBytes = System.Text.Encoding.UTF8.GetBytes(postContent.Value);

        Dictionary<string, byte[]> headers = [];
        foreach(var kvp in postContent.Headers ?? [])
        {
            headers.Add(kvp.Key, System.Text.Encoding.UTF8.GetBytes(kvp.Value));
        }
        if(!headers.ContainsKey("Correlation-Id")) headers["Correlation-Id"] = System.Text.Encoding.UTF8.GetBytes(correlationId.Value);

        var produceSuccess = kafkaProducerService.Produce(eventKeyBytes, eventValueBytes, headers, correlationId);
        if(produceSuccess)
        {
            return Results.Ok($"Stored");
        }
        return Results.Text(
            content: $"Storage failed",
            contentType: "text/html",
            contentEncoding: Encoding.UTF8,
            statusCode: (int?) HttpStatusCode.InternalServerError);
    });

    app.MapPost("/store/b64", (HttpContext http, ApiParamStore postContent, KafkaProducerService kafkaProducerService) =>
    {
        var correlationIdValue = System.Guid.NewGuid().ToString("D");
        if(http.Request.Headers.TryGetValue("X-Correlation-Id", out Microsoft.Extensions.Primitives.StringValues value))
        {
            if(!string.IsNullOrWhiteSpace(value.ToString()))
            {
                correlationIdValue = value.ToString();
            }
        }
        if(postContent.Headers?.ContainsKey("correlationId") ?? false) correlationIdValue = postContent.Headers["correlationId"];
        var correlationId = new CorrelationId { Value = correlationIdValue };
        http.Response.Headers.Append("X-Correlation-Id", correlationId.Value);

        var eventKeyBytes = Convert.FromBase64String(postContent.Key);
        var eventValueBytes = Convert.FromBase64String(postContent.Value);

        Dictionary<string, byte[]> headers = [];
        foreach(var kvp in postContent.Headers ?? [])
        {
            headers.Add(kvp.Key, Convert.FromBase64String(kvp.Value));
        }
        if(!headers.ContainsKey("Correlation-Id")) headers["Correlation-Id"] = System.Text.Encoding.UTF8.GetBytes(correlationId.Value);

        var produceSuccess = kafkaProducerService.Produce(eventKeyBytes, eventValueBytes, headers, correlationId);
        if(produceSuccess)
        {
            return Results.Ok($"Stored");
        }
        return Results.Text(
            content: $"Storage failed",
            contentType: "text/html",
            contentEncoding: Encoding.UTF8,
            statusCode: (int?) HttpStatusCode.InternalServerError);
    });

    app.MapPost("/remove", (HttpContext http, ApiParamRemove postContent, KafkaProducerService kafkaProducerService) =>
    {
        var correlationIdValue = System.Guid.NewGuid().ToString("D");
        if(http.Request.Headers.TryGetValue("X-Correlation-Id", out Microsoft.Extensions.Primitives.StringValues value))
        {
            if(!string.IsNullOrWhiteSpace(value.ToString()))
            {
                correlationIdValue = value.ToString();
            }
        }
        var correlationId = new CorrelationId { Value = correlationIdValue };
        http.Response.Headers.Append("X-Correlation-Id", correlationId.Value);

        var eventKeyBytes = System.Text.Encoding.UTF8.GetBytes(postContent.Key);

        Dictionary<string, byte[]> headers = [];
        headers["Correlation-Id"] = System.Text.Encoding.UTF8.GetBytes(correlationId.Value);

        var produceSuccess = kafkaProducerService.Produce(eventKeyBytes, null, headers, correlationId);
        if(produceSuccess)
        {
            return Results.Ok($"Removed");
        }
        return Results.Text(
            content: $"Removal failed",
            contentType: "text/html",
            contentEncoding: Encoding.UTF8,
            statusCode: (int?) HttpStatusCode.InternalServerError);
    });
}

if(readEnabled)
{
    app.MapPost("/retrieve", (HttpContext http, ApiParamRetrieve postContent, IKeyValueStateService keyValueStateService) =>
    {
        var correlationIdValue = System.Guid.NewGuid().ToString("D");
        if(http.Request.Headers.TryGetValue("X-Correlation-Id", out Microsoft.Extensions.Primitives.StringValues value))
        {
            if(!string.IsNullOrWhiteSpace(value.ToString()))
            {
                correlationIdValue = value.ToString();
            }
        }
        var correlationId = new CorrelationId { Value = correlationIdValue };
        var returnValue = string.Empty;

        var keyBytes = System.Text.Encoding.UTF8.GetBytes(postContent.Key);
        if(keyValueStateService.TryRetrieve(keyBytes, out var retrieveResult))
        {
            returnValue = System.Text.Encoding.UTF8.GetString(retrieveResult.Value);
            correlationId = new CorrelationId { Value = retrieveResult.CorrelationId };
        }

        http.Response.Headers.Append("X-Correlation-Id", correlationId.Value);
        return Results.Ok(returnValue);
    })
    // .RequireAuthorization()
    ;

    app.MapPost("/retrieve/b64", (HttpContext http, ApiParamRetrieve postContent, IKeyValueStateService keyValueStateService) =>
    {
        var correlationIdValue = System.Guid.NewGuid().ToString("D");
        if(http.Request.Headers.TryGetValue("X-Correlation-Id", out Microsoft.Extensions.Primitives.StringValues value))
        {
            if(!string.IsNullOrWhiteSpace(value.ToString()))
            {
                correlationIdValue = value.ToString();
            }
        }

        var correlationId = new CorrelationId { Value = correlationIdValue };
        var returnValue = string.Empty;

        var keyBytes = Convert.FromBase64String(postContent.Key);
        if(keyValueStateService.TryRetrieve(keyBytes, out var retrieveResult))
        {
            returnValue = Convert.ToBase64String(retrieveResult.Value);
            correlationId = new CorrelationId { Value = retrieveResult.CorrelationId };
        }

        http.Response.Headers.Append("X-Correlation-Id", correlationId.Value);

        return Results.Ok(returnValue);
    });
}

// If we've gotten this far, all config and everything has been parsed ok and set up.
// In the future more thorough validation could be done, like checking that the kafka consumer has happily connected, and the state storage is up and running.
// However, during first time start up when there are no events or data anywhere things become complicated.
// So, just don't bother with it until a pressing need arises.
app.MapGet("/healthz", () => Results.Ok("Started successfully")).AllowAnonymous();
app.MapGet("/healthz/live", () => Results.Ok("Alive and well")).AllowAnonymous();
// /healthz/live
if(!writeEnabled && !readEnabled)
{
    app.MapGet("/healthz/ready", () => Results.Ok("Both reading and writing are disabled? Why even bother")).AllowAnonymous();
}
else if(writeEnabled && !readEnabled)
{
    app.MapGet("/healthz/ready", () => Results.Ok("ready")).AllowAnonymous();
}
else
{
    app.MapGet("/healthz/ready", (IKeyValueStateService keyValueStateService) =>
    {
        if(keyValueStateService.Ready())
        {
            return Results.Ok("ready");
        }
        else
        {
            var offsetTarget = keyValueStateService.GetStartupTimeHightestTopicPartitionOffsets();
            var offsetCurrent = keyValueStateService.GetLastConsumedTopicPartitionOffsets();
            var sb = new StringBuilder();
            sb.Append('{').Append('\n');
            foreach(var target in offsetTarget)
            {
                var current = offsetCurrent.FirstOrDefault(c => c.Topic == target.Topic && c.Partition == target.Partition);
                sb.Append('\t').Append('{');
                sb.Append($"\"Topic\": \"{target.Topic.Value}\"").Append(",\t");
                sb.Append($"\"Partition\": \"{target.Partition.Value}\"").Append(",\t");
                sb.Append($"\"Current offset\": \"{current?.Offset.Value}\"").Append(",\t");
                sb.Append($"\"Target offset at startup\": \"{target.Offset.Value}\"");
                sb.Append('}').Append('\n');
            }
            sb.Append('}');
            var statusString = sb.ToString();
            // Because kubernetes by default treats responses with status codes 200-399 as passes and 400+ as failures, blindly follow that convention and rely on the juicy status code.
            // https://kubernetes.io/docs/tasks/configure-pod-container/configure-liveness-readiness-startup-probes/#define-a-liveness-http-request
            return Results.Text(
                content: $"Not ready. State hasn't caught up\n\nStatus:\n{statusString}",
                contentType: "text/html",
                contentEncoding: Encoding.UTF8,
                statusCode: (int?) HttpStatusCode.ServiceUnavailable);
        }
    })
    .AllowAnonymous();
}

app.Run();
