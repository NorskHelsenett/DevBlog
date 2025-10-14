global using static ConfigKeys;
using System.Net;
using System.Text;
using DistributedCache;
using DistributedCache.Kafka.Producers;

var builder = WebApplication.CreateBuilder(args);

builder.SetupOpenTelemetry();

var inboxKind = Environment.GetEnvironmentVariable(DISTRIBUTED_CACHE_STORAGE_INBOX_KIND);
switch (inboxKind?.ToLowerInvariant())
{
    case "memory":
    case "sqlite":
    default:
        builder.Services.AddSingleton<IStorageInbox, StorageInboxSqlite>();
        break;
}

var outboxKind = Environment.GetEnvironmentVariable(DISTRIBUTED_CACHE_STORAGE_OUTBOX_KIND);
switch (outboxKind?.ToLowerInvariant())
{
    case "memory":
    case "sqlite":
    default:
        builder.Services.AddSingleton<IStorageOutbox, StorageOutboxSqlite>();
        break;
}

builder.Services.AddHostedService<DcConsumerService>();

var kafkaProduceAsync = Environment.GetEnvironmentVariable(DISTRIBUTED_CACHE_DISTRIBUTION_KAFKA_PRODUCE_ASYNC);
switch (kafkaProduceAsync?.ToLowerInvariant())
{
    case "async":
        builder.Services.AddSingleton<IDcProducer, DcProducerAsync>();
        break;
    case "sycn":
    default:
        builder.Services.AddSingleton<IDcProducer, DcProducerSync>();
        break;
}

builder.Services.AddHostedService<OutboxWorker>();

var app = builder.Build();

#region endpoints

app.MapPost("/retrieve", (HttpContext http, DcItem postContent, IStorageInbox outputStateService, CancellationToken ct) =>
{
    var correlationId = System.Guid.NewGuid().ToString("D");
    if(http.Request.Headers.TryGetValue("X-Correlation-Id", out Microsoft.Extensions.Primitives.StringValues value))
    {
        if(!string.IsNullOrWhiteSpace(value.ToString()))
        {
            correlationId = value.ToString();
        }
    }
    var returnValue = string.Empty;

    var key = postContent.Key;

    var retrieveResult = outputStateService.Retrieve(key, ct);

    if(retrieveResult.Error == null && retrieveResult.RetrievedItem != null)
    {
        returnValue = System.Text.Json.JsonSerializer.Serialize(retrieveResult.RetrievedItem);
        var possiblyCorrelationId = retrieveResult.RetrievedItem.Headers?.FirstOrDefault(h => h.Key == "correlationId");
        if (!string.IsNullOrEmpty(possiblyCorrelationId?.Value))
        {
            correlationId = possiblyCorrelationId?.Value;
        }
    }

    http.Response.Headers.Append("X-Correlation-Id", correlationId);
    return Results.Text(returnValue);
});
app.MapPost("/remove", async (HttpContext http, DcItem postContent, IStorageOutbox outbox) =>
{
    var correlationId = System.Guid.NewGuid().ToString("D");
    if (http.Request.Headers.TryGetValue("X-Correlation-Id", out Microsoft.Extensions.Primitives.StringValues value))
    {
        if (!string.IsNullOrWhiteSpace(value.ToString()))
        {
            correlationId = value.ToString();
        }
    }
    http.Response.Headers.Append("X-Correlation-Id", correlationId);

    var outboxError = outbox.Enqueue(new DcItem() { Key = postContent.Key, Value = null, Headers = null });

    if (outboxError == null)
    {
        return Results.Ok($"Removed");
    }
    return Results.Text(
        content: $"Removal failed",
        contentType: "text/html",
        contentEncoding: Encoding.UTF8,
        statusCode: (int?)HttpStatusCode.InternalServerError);
});
app.MapPost("/store", async (HttpContext http, DcItem postContent, IStorageOutbox outbox) =>
{
    var correlationId = System.Guid.NewGuid().ToString("D");
    if(http.Request.Headers.TryGetValue("X-Correlation-Id", out Microsoft.Extensions.Primitives.StringValues value))
    {
        if(!string.IsNullOrWhiteSpace(value.ToString()))
        {
            correlationId = value.ToString();
        }
    }

    http.Response.Headers.Append("X-Correlation-Id", correlationId);
    var storeError = outbox.Enqueue(postContent);

    if(storeError == null)
    {
        return Results.Ok($"Stored");
    }
    return Results.Text(
        content: $"Storage failed",
        contentType: "text/html",
        contentEncoding: Encoding.UTF8,
        statusCode: (int?) HttpStatusCode.InternalServerError);
});

app.MapGet("/healthz", () => Results.Ok("Started successfully"));
app.MapGet("/healthz/live", () => Results.Ok("Alive and well"));
app.MapGet("/healthz/ready", (IStorageInbox outputStateService) =>
{
    if(outputStateService.Ready())
    {
        return Results.Ok("ready");
    }
    else
    {
        var offsetTarget = outputStateService.GetStartupTimeHightestTopicPartitionOffsets();
        var offsetCurrent = outputStateService.GetLastConsumedTopicPartitionOffsets();
        var sb = new StringBuilder();
        sb.Append('{').Append('\n');
        foreach(var target in offsetTarget)
        {
            var current = offsetCurrent.FirstOrDefault(c => c.Topic == target.Topic && c.Partition == target.Partition);
            sb.Append('\t').Append('{').Append('\n');
            sb.Append("\t\t").Append($"\"Topic\": \"{target.Topic}\"").Append(",\n");
            sb.Append("\t\t").Append($"\"Partition\": \"{target.Partition.Value}\"").Append(",\n");;
            sb.Append("\t\t").Append($"\"Current offset\": \"{current?.Offset.Value}\"").Append(",\n");
            sb.Append("\t\t").Append($"\"Target offset at startup\": \"{target.Offset.Value}\"").Append('\n');
            sb.Append('\t').Append('}').Append('\n');
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
});

var versionInfoCommit = File.Exists("/app/git-commit.txt")
    ? File.ReadAllText("/app/git-commit.txt")
    : "Git commit put here if built in pipeline";
var versionInfoBuild = File.Exists("/app/build-id.txt")
    ? File.ReadAllText("/app/build-id.txt")
    : "Pipeline ID put here if built in pipeline";
app.MapGet("/version", (HttpContext httpContext, CancellationToken ct) =>
{
    var versionInfoPayload = $"<h1>Version Information</h1><h2>Commit ID</h2><p>{versionInfoCommit}</p><h2>Build ID (pipeline ID)</h2><p>{versionInfoBuild}</p>";

    return Results.Text(content: versionInfoPayload,
        contentType: "text/html",
        statusCode: 200);
});
#endregion

app.Run();
