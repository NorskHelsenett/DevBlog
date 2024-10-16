using Confluent.Kafka;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using KafkaBlobChunking;

public class UserAccessMappingProducer
{
    private readonly ILogger<UserAccessMappingProducer> _logger;
    private readonly string _topic;
    private readonly IProducer<string, UserAccessMapping?> _producer;

    public UserAccessMappingProducer(ILogger<UserAccessMappingProducer> logger)
    {
        _logger = logger;
        _topic = Environment.GetEnvironmentVariable(BIG_PAYLOADS_USER_ACCESS_MAPPING_TOPIC) ?? throw new Exception($"Environment variable {BIG_PAYLOADS_USER_ACCESS_MAPPING_TOPIC} has to be set");
        _producer = GetUserAccessMappingProducer();
    }

    public async Task<bool> ProduceUserAccessMappingAsync(UserAccessMapping? userAccessMapping, string internalBlobId, CancellationToken cancellationToken)
    {
        var message = new Message<string, UserAccessMapping?>
        {
            Key = internalBlobId,
            Value = userAccessMapping
        };
        var produceResult = await _producer.ProduceAsync(_topic, message, cancellationToken);
        if (produceResult.Status == PersistenceStatus.Persisted)
        {
            if (userAccessMapping == null)
            {
                _logger.LogInformation($"Produced tombstone for user access mapping for blob with internal id {internalBlobId}");
            }
            else
            {
                _logger.LogInformation($"CorrelationId {userAccessMapping.CorrelationId} produced updated user access mapping for blob with internal id {internalBlobId}");
            }
            return true;
        }
        return false;
    }

    private IProducer<string, UserAccessMapping?> GetUserAccessMappingProducer()
    {
        var schemaRegistry = new CachedSchemaRegistryClient(KafkaConfigBinder.GetSchemaRegistryConfig());
        return  new ProducerBuilder<string, UserAccessMapping?>(KafkaConfigBinder.GetProducerConfig())
            .SetValueSerializer(new ProtobufSerializer<UserAccessMapping?>(schemaRegistry, GetProtobufSerializerConfig()))
            .Build();
    }

    private ProtobufSerializerConfig GetProtobufSerializerConfig()
    {
        return new ProtobufSerializerConfig
        {
            AutoRegisterSchemas = false,
            NormalizeSchemas = true,
            UseLatestVersion = true
        };
    }
}
