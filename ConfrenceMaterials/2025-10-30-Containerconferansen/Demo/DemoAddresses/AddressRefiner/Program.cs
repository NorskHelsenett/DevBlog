global using static AddressRefiner.ConfigKeys;
using System.Net;
using System.Text;
using AddressRefiner;

// This is a web app, because it will be long running and therefore should have health check endpoints
var builder = WebApplication.CreateBuilder(args);

builder.SetupOpenTelemetry();

var configuredAddressStorageType = Environment.GetEnvironmentVariable(ADDRESS_REFINER_STATE_STORAGE_TYPE);
switch (configuredAddressStorageType)
{
    case "dict":
        builder.Services.AddSingleton<IAddressStorage, AddressStorageDict>();
        break;
    case "sqlite":
    default:
        builder.Services.AddSingleton<IAddressStorage, AddressStorageSqlite>();
        break;
}

var configuredKafkaProduceAsync = Environment.GetEnvironmentVariable(ADDRESS_REFINER_KAFKA_PRODUCE_ASYNC)?.ToLowerInvariant();
switch (configuredKafkaProduceAsync)
{
    case "true":
        builder.Services.AddSingleton<IRefinedAddressStreamProducer, RefinedAddressStreamProducerAsync>();
        break;
    case "false":
    default:
        builder.Services.AddSingleton<IRefinedAddressStreamProducer, RefinedAddressStreamProducer>();
        break;
}
builder.Services.AddHostedService<RefinedAddressStreamConsumer>();
builder.Services.AddHostedService<RawAddressStreamConsumer>();

var app = builder.Build();

app.MapGet("/healthz", () => Results.Ok("Started successfully")).AllowAnonymous();
app.MapGet("/healthz/live", () => Results.Ok("Alive and well")).AllowAnonymous();
app.MapGet("/healthz/ready", (IAddressStorage addressStorage) =>
{
    if(addressStorage.Ready())
    {
        return Results.Ok("ready");
    }
    else
    {
        var offsetTarget = addressStorage.GetStartupTimeHightestTopicPartitionOffsets();
        var offsetCurrent = addressStorage.GetLastConsumedTopicPartitionOffsets();
        var sb = new StringBuilder();
        sb.Append('{').Append('\n');
        foreach(var target in offsetTarget)
        {
            var current = offsetCurrent.FirstOrDefault(c => c.Topic == target.Topic && c.Partition == target.Partition);
            sb.Append('\t').Append('{');
            sb.Append("\t\t").Append($"\"Topic\": \"{target.Topic}\"").Append(",\n");
            sb.Append("\t\t").Append($"\"Partition\": \"{target.Partition.Value}\"").Append(",\n");
            sb.Append("\t\t").Append($"\"Current offset\": \"{current?.Offset.Value}\"").Append(",\n");
            sb.Append("\t\t").Append($"\"Target offset at startup\": \"{target.Offset.Value}\"").Append(",\n");;
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
}).AllowAnonymous();

var versionInfoCommit = File.Exists("/app/git-commit.txt")
    ? File.ReadAllText("/app/git-commit.txt")
    : "Git commit put here if built in pipeline";
var versionInfoBuild = File.Exists("/app/build-id.txt")
    ? File.ReadAllText("/app/build-id.txt")
    : "Pipeline ID put here if built in pipeline";
var versionInfoPayload = $"<h1>Version Information</h1><h2>Commit ID</h2><p>{versionInfoCommit}</p><h2>Build ID (pipeline ID)</h2><p>{versionInfoBuild}</p>";
app.MapGet("/version", (HttpContext httpContext, CancellationToken ct) =>
        Results.Text(content: versionInfoPayload,
            contentType: "text/html",
            statusCode: 200))
    .AllowAnonymous();

app.Run();
