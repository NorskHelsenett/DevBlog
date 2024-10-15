global using static EnvVarNames;
using System.Collections.Immutable;
using System.Security.Claims;
using System.Text;
using KafkaBlobChunking;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.AddSingleton<ChunkingProducer>();
builder.Services.AddScoped<ChunkConsumer>();
builder.Services.AddHostedService<BlobMetadataConsumer>();
builder.Services.AddHostedService<UserAccessMappingConsumer>();
builder.Services.AddSingleton<OutputStateService>();
builder.Services.AddSingleton<UserAccessMappingStateService>();
builder.Services.AddScoped<UserAccessMappingProducer>();

HttpClient httpClient;
if (Environment.GetEnvironmentVariable(HTTPCLIENT_VALIDATE_EXTERNAL_CERTIFICATES) == "false")
{
    var handler = new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    };
    httpClient = new HttpClient(handler);
}
else
{
    httpClient = new HttpClient();
}

builder.Services.AddSingleton(httpClient);
builder.Services.AddAuthentication().AddJwtBearer("OurBearerScheme", options =>
{
     var backendIdpUrl =
         Environment.GetEnvironmentVariable(
             OIDC_IDP_ADDRESS_FOR_SERVER); // "http://keycloak:8088/realms/lokalmaskin"
     var clientIdpUrl =
         Environment.GetEnvironmentVariable(
             OIDC_IDP_ADDRESS_FOR_USERS); // "http://localhost:8088/realms/lokalmaskin"
     options.Configuration = new()
     {
         Issuer = backendIdpUrl,
         AuthorizationEndpoint = $"{clientIdpUrl}/protocol/openid-connect/auth",
         TokenEndpoint = $"{backendIdpUrl}/protocol/openid-connect/token",
         JwksUri = $"{backendIdpUrl}/protocol/openid-connect/certs",
         JsonWebKeySet = FetchJwks($"{backendIdpUrl}/protocol/openid-connect/certs"),
         EndSessionEndpoint = $"{clientIdpUrl}/protocol/openid-connect/logout",
     };
     Console.WriteLine("Jwks: " + options.Configuration.JsonWebKeySet);
     foreach (var key in options.Configuration.JsonWebKeySet.GetSigningKeys())
     {
         options.Configuration.SigningKeys.Add(key);
         Console.WriteLine("Added SigningKey: " + key.KeyId);
     }

     options.TokenValidationParameters.ValidIssuers = [clientIdpUrl, backendIdpUrl];
     options.TokenValidationParameters.NameClaimType = "name"; // This is what populates @context.User.Identity?.Name
     options.TokenValidationParameters.RoleClaimType = "role";
     options.RequireHttpsMetadata =
         Environment.GetEnvironmentVariable(OIDC_REQUIRE_HTTPS_METADATA) != "false"; // disable only in dev env
     options.MapInboundClaims = true;
     options.Audience = Environment.GetEnvironmentVariable(OIDC_AUDIENCE);
});

JsonWebKeySet FetchJwks(string url)
{
    var result = httpClient.GetAsync(url).Result;
    if (!result.IsSuccessStatusCode || result.Content is null)
    {
        throw new Exception(
            $"Getting token issuers (Keycloaks) JWKS from {url} failed. Status code {result.StatusCode}");
    }

    var jwks = result.Content.ReadAsStringAsync().Result;
    return new JsonWebKeySet(jwks);
}

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthorization();

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

    var userEmailClaim = req.HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
    app.Logger.LogDebug($"CorrelationId {correlationId} Current user email: \"{userEmailClaim}\"");
    if (string.IsNullOrEmpty(userEmailClaim))
    {
        app.Logger.LogWarning($"CorrelationId {correlationId} failed to auth user because email claim in token appears to be empty");
        return Results.StatusCode(StatusCodes.Status401Unauthorized);
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

    var internalBlobId = GetBlobId(nameOfOwner: userEmailClaim, suppliedBlobName: suppliedBlobName);

    app.Logger.LogInformation($"CorrelationId {correlationId} Received request from \"{userEmailClaim}\" to store blob they named \"{suppliedBlobName}\" with internal blob ID \"{internalBlobId}\"");

    var produceSuccessful = await chunkingProducer.ProduceAsync(body, blobId: internalBlobId, ownerId: userEmailClaim, callersBlobName: suppliedBlobName, correlationId: correlationId, cancellationToken);
    if (produceSuccessful)
    {
        return Results.Ok();
    }
    return Results.StatusCode(StatusCodes.Status500InternalServerError);
})
.RequireAuthorization();

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

    var userEmailClaim = context.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
    app.Logger.LogDebug($"CorrelationId {correlationId} Current user email: \"{userEmailClaim}\"");
    if (string.IsNullOrEmpty(userEmailClaim))
    {
        app.Logger.LogWarning($"CorrelationId {correlationId} failed to auth user because email claim in token appears to be empty");
        return Task.FromResult(Results.StatusCode(StatusCodes.Status401Unauthorized));
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

    var internalBlobId = GetBlobId(nameOfOwner: userEmailClaim, suppliedBlobName: suppliedBlobName);

    app.Logger.LogInformation($"CorrelationId {correlationId} Received request from \"{userEmailClaim}\" for blob they named \"{suppliedBlobName}\" with internal blob ID \"{internalBlobId}\"");

    context.Response.Headers.Append("X-Correlation-Id", correlationId);

    if(!stateService.TryRetrieve(internalBlobId, out var blobChunksMetadata) || blobChunksMetadata == null)
    {
        app.Logger.LogInformation($"CorrelationId {correlationId} Received request from \"{userEmailClaim}\" for blob they named \"{suppliedBlobName}\" with internal blob ID \"{internalBlobId}\" resulted in not found");
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
})
.RequireAuthorization();

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

    var userEmailClaim = context.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
    app.Logger.LogDebug($"CorrelationId {correlationId} Current user email: \"{userEmailClaim}\"");
    if (string.IsNullOrEmpty(userEmailClaim))
    {
        app.Logger.LogWarning($"CorrelationId {correlationId} failed to auth user because email claim in token appears to be empty");
        return Results.Unauthorized();
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

    var internalBlobId = GetBlobId(nameOfOwner: userEmailClaim, suppliedBlobName: suppliedBlobName);

    app.Logger.LogInformation($"CorrelationId {correlationId} Received request from \"{userEmailClaim}\" to delete blob they named \"{suppliedBlobName}\" with internal blob ID \"{internalBlobId}\"");

    context.Response.Headers.Append("X-Correlation-Id", correlationId);

    if(!stateService.TryRetrieve(internalBlobId, out var blobChunksMetadata) || blobChunksMetadata == null)
    {
        app.Logger.LogInformation($"CorrelationId {correlationId} Received request from \"{userEmailClaim}\" to delete blob they named \"{suppliedBlobName}\" with internal blob ID \"{internalBlobId}\" resulted in not found");
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
})
.RequireAuthorization();

app.MapGet("/list", (HttpContext context, OutputStateService stateService, UserAccessMappingStateService userAccessMappingStateService) =>
{
    var correlationId = System.Guid.NewGuid().ToString("D");
    if(context.Request.Headers.TryGetValue("X-Correlation-Id", out Microsoft.Extensions.Primitives.StringValues headerCorrelationId))
    {
        if(!string.IsNullOrWhiteSpace(headerCorrelationId.ToString()))
        {
            correlationId = headerCorrelationId.ToString();
        }
    }

    var cancellationToken = context.Request.HttpContext.RequestAborted;

    // Useful for figuring out what your code could do, but not something you should continuously log runtime
    // var currentUserToken = context.Request.Headers.Authorization;
    // app.Logger.LogDebug($"Current user token \"{currentUserToken}\"");
    // foreach (var x in context.User.Claims)
    // {
    //     app.Logger.LogDebug($"Claim {x.Type}: {x.Value}");
    // }

    // Use user email claim, because it plays nicer when KC is used as proxy for companys auth service.
    // Beware security tradeoff if you trust multiple external identity providers (employee has been terminated, but do they still have a private facebook/apple/google account, which they gladly say the user has logged in to with their old account registerd with the email @company.tld?)
    var userEmailClaim = context.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
    app.Logger.LogDebug($"Current user email: \"{userEmailClaim}\"");
    if (string.IsNullOrEmpty(userEmailClaim))
    {
        app.Logger.LogWarning($"CorrelationId {correlationId} failed to auth user because email claim in token appears to be empty");
        return Results.Unauthorized();
    }
    var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
    app.Logger.LogDebug($"Current user sid: \"{userIdClaim}\"");

    var filesUserCanSee = userAccessMappingStateService.GetAllUserAccessMappings()
        .Where(um => um.Owner == userEmailClaim
            || um.CanChangeAccess.Contains(userEmailClaim)
            || um.CanChange.Contains(userEmailClaim)
            || um.CanRetrieve.Contains(userEmailClaim)
            || um.CanDelete.Contains(userEmailClaim))
        .Select(um => (BlobName: um.BlobName, Owner: um.Owner))
        .ToImmutableHashSet();

    var resultingCollection = stateService.ListStoredBlobs(userEmailClaim, correlationId)
        .Where(bm => bm.BlobOwnerId == userEmailClaim
            || filesUserCanSee.Any(x => x.Owner == bm.BlobOwnerId && x.BlobName == bm.BlobName))
        .Aggregate(new StringBuilder(),
            (current, next) => current.Append(current.Length == 0 ? "" : "\n").Append($"{next.BlobOwnerId}\t{next.BlobName}"))
        .ToString();

    return Results.Ok(resultingCollection);
})
.RequireAuthorization();

app.MapPost("/updateUserAccessMapping", async (ApiParamUserAccessMapping apiParamUserAccessMapping, HttpContext context, UserAccessMappingStateService userAccessMappingStateService, UserAccessMappingProducer userAccessMappingProducer) =>
{
    var correlationId = System.Guid.NewGuid().ToString("D");
    if(context.Request.Headers.TryGetValue("X-Correlation-Id", out Microsoft.Extensions.Primitives.StringValues headerCorrelationId))
    {
        if(!string.IsNullOrWhiteSpace(headerCorrelationId.ToString()))
        {
            correlationId = headerCorrelationId.ToString();
        }
    }

    var cancellationToken = context.Request.HttpContext.RequestAborted;
    var userEmailClaim = context.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
    app.Logger.LogDebug($"CorrelationId {correlationId} Current user email: \"{userEmailClaim}\"");
    if (string.IsNullOrEmpty(userEmailClaim))
    {
        app.Logger.LogWarning($"CorrelationId {correlationId} failed to auth user because email claim in token appears to be empty");
        return Results.Unauthorized();
    }

    var updatedUserAccessMapping = new UserAccessMapping
    {
        BlobName = apiParamUserAccessMapping.BlobName,
        Owner = apiParamUserAccessMapping.Owner,
        CanChangeAccess = { apiParamUserAccessMapping.CanChangeAccess },
        CanRetrieve = { apiParamUserAccessMapping.CanRetrieve },
        CanChange = { apiParamUserAccessMapping.CanChange },
        CanDelete = { apiParamUserAccessMapping.CanDelete },
        UpdatedAt = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow),
        UpdatedBy = userEmailClaim,
        CorrelationId = correlationId,
    };

    var internalBlobId = GetBlobId(nameOfOwner: apiParamUserAccessMapping.Owner, suppliedBlobName: apiParamUserAccessMapping.BlobName);
    var accessMappingAlreadyExists = userAccessMappingStateService.TryGetUserAccessMapping(internalBlobId, out var preExistingAccessMapping);
    if (!accessMappingAlreadyExists && apiParamUserAccessMapping.Owner != userEmailClaim)
    {
        app.Logger.LogWarning($"CorrelationId {correlationId} user {userEmailClaim} tried to set access configs for object that doesn't belong to them nor exists yet. This may indicate that they're trying to highjack a future resource belonging to someone else. The permissions they tried to set were {updatedUserAccessMapping}. Noping out");
        return Results.StatusCode(statusCode: StatusCodes.Status403Forbidden);
    }
    if (accessMappingAlreadyExists && preExistingAccessMapping != null && (preExistingAccessMapping.Owner != userEmailClaim || preExistingAccessMapping.CanChangeAccess.Contains(userEmailClaim)))
    {
        app.Logger.LogWarning($"CorrelationId {correlationId} user {userEmailClaim} tried to set access configs for object that they neither own nor have access to change access rights for. This may indicate that they're trying to highjack a current belonging to someone else. The permissions they tried to set were {updatedUserAccessMapping}. Noping out");
        return Results.StatusCode(statusCode: StatusCodes.Status403Forbidden);
    }

    if (accessMappingAlreadyExists && preExistingAccessMapping != null && preExistingAccessMapping.Owner != apiParamUserAccessMapping.Owner)
    {
        app.Logger.LogInformation($"CorrelationId {correlationId} user {userEmailClaim} tried to change owner, which is not currently supported. The permissions they tried to set were {updatedUserAccessMapping}.");
        return Results.Text(
            content: $"Changing owner is not supported",
            contentType: "text/html",
            contentEncoding: Encoding.UTF8,
            statusCode: StatusCodes.Status400BadRequest);
    }

    if (accessMappingAlreadyExists && preExistingAccessMapping != null && preExistingAccessMapping.BlobName != apiParamUserAccessMapping.BlobName)
    {
        app.Logger.LogInformation($"CorrelationId {correlationId} user {userEmailClaim} tried to change target resource, which is not currently supported. The permissions they tried to set were {updatedUserAccessMapping}.");
        return Results.Text(
            content: $"Changing target resource is not supported",
            contentType: "text/html",
            contentEncoding: Encoding.UTF8,
            statusCode: StatusCodes.Status400BadRequest);
    }

    app.Logger.LogInformation($"CorrelationId {correlationId} This is the updated user access mapping object {updatedUserAccessMapping}");
    var produceResult = await userAccessMappingProducer.ProduceUserAccessMappingAsync(updatedUserAccessMapping, internalBlobId, cancellationToken);
    if (produceResult)
    {
        return Results.Ok($"Successfully updated user access mapping");
    }

    return Results.StatusCode(StatusCodes.Status500InternalServerError);
})
.RequireAuthorization();

app.MapGet("/userAccessMappings", (HttpContext context, UserAccessMappingStateService userAccessMappingStateService) =>
{
    var correlationId = System.Guid.NewGuid().ToString("D");
    if(context.Request.Headers.TryGetValue("X-Correlation-Id", out Microsoft.Extensions.Primitives.StringValues headerCorrelationId))
    {
        if(!string.IsNullOrWhiteSpace(headerCorrelationId.ToString()))
        {
            correlationId = headerCorrelationId.ToString();
        }
    }
    app.Logger.LogInformation($"CorrelationId {correlationId} Received request to retrieve list of user access mappings");
    var userEmailClaim = context.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
    app.Logger.LogDebug($"CorrelationId {correlationId} Current user email: \"{userEmailClaim}\"");
    if (string.IsNullOrEmpty(userEmailClaim))
    {
        app.Logger.LogWarning($"CorrelationId {correlationId} failed to auth user because email claim in token appears to be empty");
        return Results.Unauthorized();
    }
    var allMappings = userAccessMappingStateService.GetAllUserAccessMappings()
        .Where(uam => uam.Owner == userEmailClaim
            || uam.CanChangeAccess.Contains(userEmailClaim)
            || uam.CanChange.Contains(userEmailClaim)
            || uam.CanRetrieve.Contains(userEmailClaim)
            || uam.CanDelete.Contains(userEmailClaim))
        .Select(m => new ApiParamUserAccessMapping
        {
            BlobName = m.BlobName,
            Owner = m.Owner,
            CanChangeAccess = m.CanChangeAccess.ToList(),
            CanChange = m.CanChangeAccess.ToList(),
            CanRetrieve = m.CanRetrieve.ToList(),
            CanDelete = m.CanDelete.ToList()
        });
    return Results.Json(allMappings);
}).RequireAuthorization();

app.MapPost("/deleteUserAccessMapping", async (ApiParamUserAccessMapping apiParamUserAccessMapping, HttpContext context, UserAccessMappingStateService userAccessMappingStateService, UserAccessMappingProducer userAccessMappingProducer) =>
{
    var correlationId = System.Guid.NewGuid().ToString("D");
    if(context.Request.Headers.TryGetValue("X-Correlation-Id", out Microsoft.Extensions.Primitives.StringValues headerCorrelationId))
    {
        if(!string.IsNullOrWhiteSpace(headerCorrelationId.ToString()))
        {
            correlationId = headerCorrelationId.ToString();
        }
    }

    var cancellationToken = context.Request.HttpContext.RequestAborted;
    var userEmailClaim = context.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
    app.Logger.LogDebug($"CorrelationId {correlationId} Current user email: \"{userEmailClaim}\"");
    if (string.IsNullOrEmpty(userEmailClaim))
    {
        app.Logger.LogWarning($"CorrelationId {correlationId} failed to auth user because email claim in token appears to be empty");
        return Results.Unauthorized();
    }

    var updatedUserAccessMapping = new UserAccessMapping
    {
        BlobName = apiParamUserAccessMapping.BlobName,
        Owner = apiParamUserAccessMapping.Owner,
        CanChangeAccess = { apiParamUserAccessMapping.CanChangeAccess },
        CanRetrieve = { apiParamUserAccessMapping.CanRetrieve },
        CanChange = { apiParamUserAccessMapping.CanChange },
        CanDelete = { apiParamUserAccessMapping.CanDelete },
        UpdatedAt = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow),
        UpdatedBy = userEmailClaim,
        CorrelationId = correlationId,
    };

    var internalBlobId = GetBlobId(nameOfOwner: apiParamUserAccessMapping.Owner, suppliedBlobName: apiParamUserAccessMapping.BlobName);
    var accessMappingAlreadyExists = userAccessMappingStateService.TryGetUserAccessMapping(internalBlobId, out var preExistingAccessMapping);
    if (!accessMappingAlreadyExists)
    {
        app.Logger.LogInformation($"CorrelationId {correlationId} user {userEmailClaim} tried to delete access configs that don't exists. The permissions they tried to delete were {updatedUserAccessMapping}.");
        return Results.StatusCode(StatusCodes.Status200OK);
    }
    if (accessMappingAlreadyExists && preExistingAccessMapping != null && (preExistingAccessMapping.Owner != userEmailClaim || preExistingAccessMapping.CanDelete.Contains(userEmailClaim)))
    {
        app.Logger.LogWarning($"CorrelationId {correlationId} user {userEmailClaim} tried to delete access configs for object that they neither own nor have access to delete. This may indicate that they're trying to do mischief. The permissions they tried to delete were {updatedUserAccessMapping}. Noping out");
        return Results.StatusCode(statusCode: StatusCodes.Status403Forbidden);
    }

    if (accessMappingAlreadyExists && preExistingAccessMapping != null && preExistingAccessMapping.Owner != apiParamUserAccessMapping.Owner)
    {
        app.Logger.LogInformation($"CorrelationId {correlationId} user {userEmailClaim} tried to delete resource, but supplied an altered owner for the resource to delete, which is weird. The permissions they tried to delete were {updatedUserAccessMapping}.");
        return Results.Text(
            content: $"Changing owner while deleting is not supported",
            contentType: "text/html",
            contentEncoding: Encoding.UTF8,
            statusCode: StatusCodes.Status400BadRequest);
    }

    if (accessMappingAlreadyExists && preExistingAccessMapping != null && preExistingAccessMapping.BlobName != apiParamUserAccessMapping.BlobName)
    {
        app.Logger.LogInformation($"CorrelationId {correlationId} user {userEmailClaim} tried to change target resource while performing a delete, which is not currently supported. The permissions they tried to set were {updatedUserAccessMapping}.");
        return Results.Text(
            content: $"Changing target resource while deletig is not supported",
            contentType: "text/html",
            contentEncoding: Encoding.UTF8,
            statusCode: StatusCodes.Status400BadRequest);
    }

    app.Logger.LogInformation($"CorrelationId {correlationId} This is the user access mapping object {updatedUserAccessMapping} that will be deleted");
    var produceResult = await userAccessMappingProducer.ProduceUserAccessMappingAsync(null, internalBlobId, cancellationToken);
    if (produceResult)
    {
        return Results.Ok($"Successfully deleted user access mapping");
    }

    return Results.StatusCode(StatusCodes.Status500InternalServerError);
})
.RequireAuthorization();

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
