using Confluent.SchemaRegistry;

public static class KafkaSchemaRegistration
{
    public static async Task RegisterSchemasAsync()
    {
        var blobChunkSchemaAsString = File.ReadAllText("./Protos/BlobChunk.proto");
        var blobChunksMetadataSchemaAsString = File.ReadAllText("./Protos/BlobChunksMetadata.proto");
        var userAccessMappingSchemaAsString = File.ReadAllText("./Protos/UserAccessMapping.proto");
        var topicNameChunksTopic = Environment.GetEnvironmentVariable(BIG_PAYLOADS_CHUNKS_TOPIC);
        var topicNameMetadataTopic = Environment.GetEnvironmentVariable(BIG_PAYLOADS_METADATA_TOPIC);
        var topicNameUserAccessMappingTopic = Environment.GetEnvironmentVariable(BIG_PAYLOADS_USER_ACCESS_MAPPING_TOPIC);
        var chunkTopicSchemaSubject = $"{topicNameChunksTopic}-value";
        var metadataTopicSchemaSubject = $"{topicNameMetadataTopic}-value";
        var userAccessTopicSchemaSubject = $"{topicNameUserAccessMappingTopic}-value";

        var schemaRegistryConfig = KafkaConfigBinder.GetSchemaRegistryConfig();
        CachedSchemaRegistryClient schemaRegistryClient = new CachedSchemaRegistryClient(schemaRegistryConfig);

        var chunkSchema = new Schema(schemaString: blobChunkSchemaAsString, schemaType: SchemaType.Protobuf);
        _ = await schemaRegistryClient.RegisterSchemaAsync(subject: chunkTopicSchemaSubject, schema: chunkSchema, normalize: true);
        _ = await schemaRegistryClient.UpdateCompatibilityAsync(Compatibility.Backward, subject: chunkTopicSchemaSubject);

        var metadataSchema = new Schema(schemaString: blobChunksMetadataSchemaAsString, schemaType: SchemaType.Protobuf);
        _ = await schemaRegistryClient.RegisterSchemaAsync(subject: metadataTopicSchemaSubject, schema: metadataSchema, normalize: true);
        _ = await schemaRegistryClient.UpdateCompatibilityAsync(Compatibility.Backward, subject: metadataTopicSchemaSubject);

        var userAccessMappingSchema = new Schema(schemaString: userAccessMappingSchemaAsString, schemaType: SchemaType.Protobuf);
        _ = await schemaRegistryClient.RegisterSchemaAsync(subject: userAccessTopicSchemaSubject, schema: userAccessMappingSchema, normalize: true);
        _ = await schemaRegistryClient.UpdateCompatibilityAsync(Compatibility.Backward, subject: userAccessTopicSchemaSubject);
    }
}
