global using static EnvVarNames;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.AddSingleton<ChunkingProducer>();
builder.Services.AddScoped<ChunkConsumer>();
builder.Services.AddHostedService<BlobMetadataConsumer>();
builder.Services.AddSingleton<OutputStateService>();

var app = builder.Build();

app.Logger.LogInformation("Registering schemas");
await KafkaSchemaRegistration.RegisterSchemasAsync();
app.Logger.LogInformation("Creating topics");
await KafkaTopicCreation.CreateTopicsAsync();
app.Logger.LogInformation("Waiting for a couple of seconds so that the Kafka cluster has time to sync topics and things");
await Task.Delay(TimeSpan.FromSeconds(5));

string GetBlobId(string nameOfOwner, string suppliedBlobName)
{
    // Don't rely on propagating externally supplied IDs (they could be user supplied :O)
    // Get 2 different checksums of name to reduce odds of ID collision to near enough zero.
    // Use checksum of users name to avoid having to deal with weird characters and stuff.
    var ownerNameChecksum = Convert.ToHexString(System.IO.Hashing.Crc32.Hash(System.Text.Encoding.UTF8.GetBytes(nameOfOwner))).ToLowerInvariant();
    var blobNameBytes = System.Text.Encoding.UTF8.GetBytes(suppliedBlobName);
    var suppliedBlobNameFirstChecksum = Convert.ToHexString(System.IO.Hashing.Crc32.Hash(blobNameBytes)).ToLowerInvariant();
    var suppliedBlobNameSecondChecksum = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(blobNameBytes)).ToLowerInvariant();
    return $"{ownerNameChecksum}.{suppliedBlobNameFirstChecksum}.{suppliedBlobNameSecondChecksum}";
}

app.MapPost("/store", async (HttpRequest req, Stream body, ChunkingProducer chunkingProducer) =>
{
    var correlationId = System.Guid.NewGuid().ToString("D");
    if(req.Headers.TryGetValue("X-Correlation-Id", out Microsoft.Extensions.Primitives.StringValues headerCorrelationId))
    {
        if(!string.IsNullOrWhiteSpace(headerCorrelationId.ToString()))
        {
            correlationId = headerCorrelationId.ToString();
        }
    }
    var suppliedBlobName = "";
    if(req.Headers.TryGetValue("X-Blob-Name", out Microsoft.Extensions.Primitives.StringValues headerSuppliedBlobName))
    {
        if(!string.IsNullOrWhiteSpace(headerSuppliedBlobName.ToString()))
        {
            suppliedBlobName = headerSuppliedBlobName.ToString();
        }
    }
    var cancellationToken = req.HttpContext.RequestAborted;

    var ownerId = "ToDo";
    var internalBlobId = GetBlobId(nameOfOwner: ownerId, suppliedBlobName: suppliedBlobName);

    app.Logger.LogInformation($"CorrelationId {correlationId} Received request from \"{ownerId}\" to store blob they named \"{suppliedBlobName}\" with internal blob ID \"{internalBlobId}\"");

    var produceSuccessful = await chunkingProducer.ProduceAsync(body, blobId: internalBlobId, ownerId: ownerId, callersBlobName: suppliedBlobName, correlationId: correlationId, cancellationToken);
    if (produceSuccessful)
    {
        return Results.Ok();
    }
    return Results.StatusCode(StatusCodes.Status500InternalServerError);
});

app.MapGet("/retrieve", (HttpContext context, ChunkConsumer consumer, OutputStateService stateService) =>
{
    var correlationId = System.Guid.NewGuid().ToString("D");
    if(context.Request.Headers.TryGetValue("X-Correlation-Id", out Microsoft.Extensions.Primitives.StringValues headerCorrelationId))
    {
        if(!string.IsNullOrWhiteSpace(headerCorrelationId.ToString()))
        {
            correlationId = headerCorrelationId.ToString();
        }
    }
    var suppliedBlobName = "";
    if(context.Request.Headers.TryGetValue("X-Blob-Name", out Microsoft.Extensions.Primitives.StringValues headerSuppliedBlobName))
    {
        if(!string.IsNullOrWhiteSpace(headerSuppliedBlobName.ToString()))
        {
            suppliedBlobName = headerSuppliedBlobName.ToString();
        }
    }
    var cancellationToken = context.Request.HttpContext.RequestAborted;

    var ownerId = "ToDo";
    var internalBlobId = GetBlobId(nameOfOwner: ownerId, suppliedBlobName: suppliedBlobName);

    app.Logger.LogInformation($"CorrelationId {correlationId} Received request from \"{ownerId}\" for blob they named \"{suppliedBlobName}\" with internal blob ID \"{internalBlobId}\"");

    context.Response.Headers.Append("X-Correlation-Id", correlationId);

    if(!stateService.TryRetrieve(internalBlobId, out var blobChunksMetadata) || blobChunksMetadata == null)
    {
        app.Logger.LogInformation($"CorrelationId {correlationId} Received request from \"{ownerId}\" for blob they named \"{suppliedBlobName}\" with internal blob ID \"{internalBlobId}\" resulted in not found");
        return Task.FromResult(Results.NotFound());
    }

    context.Response.Headers.Append("X-Blob-Correlation-Id", blobChunksMetadata.CorrelationId);
    context.Response.Headers.Append("X-Blob-User-Supplied-Name", blobChunksMetadata.BlobName);
    context.Response.Headers.Append("X-Blob-Owner-Id", blobChunksMetadata.BlobOwnerId);
    context.Response.Headers.Append("X-Blob-Checksum", blobChunksMetadata.FinalChecksum);
    context.Response.Headers.Append("X-Blob-Checksum-Algorithm", "sha-256");
    // var contentStream = new MemoryStream();
    // await foreach(var b in consumer.GetBlobByMetadataAsync(blobChunksMetadata, correlationId, cancellationToken))
    // {
    //     contentStream.WriteByte(b);
    //     // context.Response.BodyWriter.Wr(b);
    // }
    // return contentStream;

    byte[] buffer = new byte[1];
    return Task.FromResult(Results.Stream(streamWriterCallback: async (outStream) =>
        {
            await foreach (var b in consumer.GetBlobByMetadataAsync(blobChunksMetadata, correlationId, cancellationToken))
            {
                buffer[0] = b;
                await outStream.WriteAsync(buffer);
            }
        }
    ));

    // return Results.Ok(consumer.GetBlobByMetadataAsync(blobChunksMetadata, correlationId, cancellationToken));
});

app.MapPost("/remove", async (HttpContext context, ChunkingProducer chunkingProducer, OutputStateService stateService) =>
{
    var correlationId = System.Guid.NewGuid().ToString("D");
    if(context.Request.Headers.TryGetValue("X-Correlation-Id", out Microsoft.Extensions.Primitives.StringValues headerCorrelationId))
    {
        if(!string.IsNullOrWhiteSpace(headerCorrelationId.ToString()))
        {
            correlationId = headerCorrelationId.ToString();
        }
    }
    var suppliedBlobName = "";
    if(context.Request.Headers.TryGetValue("X-Blob-Name", out Microsoft.Extensions.Primitives.StringValues headerSuppliedBlobName))
    {
        if(!string.IsNullOrWhiteSpace(headerSuppliedBlobName.ToString()))
        {
            suppliedBlobName = headerSuppliedBlobName.ToString();
        }
    }
    var cancellationToken = context.Request.HttpContext.RequestAborted;

    var ownerId = "ToDo";
    var internalBlobId = GetBlobId(nameOfOwner: ownerId, suppliedBlobName: suppliedBlobName);

    app.Logger.LogInformation($"CorrelationId {correlationId} Received request from \"{ownerId}\" to delete blob they named \"{suppliedBlobName}\" with internal blob ID \"{internalBlobId}\"");

    context.Response.Headers.Append("X-Correlation-Id", correlationId);

    if(!stateService.TryRetrieve(internalBlobId, out var blobChunksMetadata) || blobChunksMetadata == null)
    {
        app.Logger.LogInformation($"CorrelationId {correlationId} Received request from \"{ownerId}\" to delete blob they named \"{suppliedBlobName}\" with internal blob ID \"{internalBlobId}\" resulted in not found");
        return Results.NotFound();
    }

    context.Response.Headers.Append("X-Correlation-Id", correlationId);
    context.Response.Headers.Append("X-Deleted-Blob-Correlation-Id", blobChunksMetadata.CorrelationId);
    context.Response.Headers.Append("X-Deleted-Blob-User-Supplied-Name", blobChunksMetadata.BlobName);
    context.Response.Headers.Append("X-Deleted-Blob-Owner-Id", blobChunksMetadata.BlobOwnerId);

    var deleteSuccess = await chunkingProducer.ProduceTombstones(blobChunksMetadata, correlationId, cancellationToken);
    if (deleteSuccess)
    {
        return Results.Ok();
    }
    return Results.StatusCode(StatusCodes.Status500InternalServerError);
});


app.MapGet("/healthz", () => Results.Ok("Started successfully"));
app.MapGet("/healthz/live", () => Results.Ok("Alive and well"));
app.MapGet("/healthz/ready", (OutputStateService outputStateService) =>
{
    if(outputStateService.Ready())
    {
        return Results.Ok("ready");
    }

    var offsetTarget = outputStateService.GetStartupTimeHightestTopicPartitionOffsets();
    var offsetCurrent = outputStateService.GetLastConsumedTopicPartitionOffsets();
    var sb = new System.Text.StringBuilder();
    sb.Append('{').Append('\n');
    foreach(var target in offsetTarget)
    {
        var current = offsetCurrent.FirstOrDefault(c => c.Topic == target.Topic && c.Partition == target.Partition);
        sb.Append('\t').Append('{');
        sb.Append($"\"Topic\": \"{target.Topic}\"").Append(",\t");
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
        contentEncoding: System.Text.Encoding.UTF8,
        statusCode: (int?) StatusCodes.Status503ServiceUnavailable);
});

app.Run();
