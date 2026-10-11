using Confluent.Kafka;
using Confluent.Kafka.SyncOverAsync;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;

public class KafkaProducerService
{
    private readonly ILogger<KafkaProducerService> _logger;
    private readonly IProducer<byte[], Person?> _producer;
    private readonly string _topic;

    public KafkaProducerService(ILogger<KafkaProducerService> logger)
    {
        _logger = logger;

        AppDomain.CurrentDomain.ProcessExit += new EventHandler(OnProcessExit);

        var producerConfig = KafkaConfigBinder.GetProducerConfig();
        var schemaRegistryConfig = KafkaConfigBinder.GetSchemaRegistryConfig();
        var schemaRegistryClient = new CachedSchemaRegistryClient(schemaRegistryConfig);
        var protobufSerializerConfig = new ProtobufSerializerConfig { BufferBytes = 100 };
        _producer = new ProducerBuilder<byte[], Person?>(producerConfig)
            .SetValueSerializer(new ProtobufSerializer<Person?>(schemaRegistryClient, protobufSerializerConfig).AsSyncOverAsync())
            .Build();

        var topicName = Environment.GetEnvironmentVariable(STATE_DISTRIBUTOR_KAFKA_STATE_TOPIC);
        if(string.IsNullOrWhiteSpace(topicName))
        {
            _logger.LogError($"Cannot consume if topic is not specified. Environment variable {nameof(STATE_DISTRIBUTOR_KAFKA_STATE_TOPIC)} was not set/is empty.");
            throw new InvalidOperationException($"Environment variable {nameof(STATE_DISTRIBUTOR_KAFKA_STATE_TOPIC)} has to have value.");
        }
        _topic = topicName;

        _logger.LogDebug($"{nameof(KafkaProducerService)} initialized");
    }

    public async Task<bool> Produce(byte[] key, Person? value, Dictionary<string, byte[]> headers, string correlationId)
    {
        _logger.LogTrace($"Producing message with correlation ID {correlationId}");

        var message = new Message<byte[], Person?>
        {
            Key = key,
            Value = value
        };

        if(headers.Count > 0)
        {
            message.Headers = new Headers();
            foreach(var header in headers)
            {
                message.Headers.Add(header.Key, header.Value);
            }
        }
        try
        {
            _producer.Produce(_topic, message, report =>
            {
                _logger.LogTrace($"Produce result status is {report.Status}");
            });
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, $"Got exception when producing message with correlation ID {correlationId} to topic {_topic}");
            return false;
        }
        return true;
    }

    private void OnProcessExit(object? sender, EventArgs e)
    {
        // Because finalizers are not necessarily called on program exit in newer dotnet:
        // https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/finalizers
        // Could maybe be handled by making this a BackgroundService and using the provided shutdown handling there,
        // but then again this is not really for doing long running background work.
        _logger.LogDebug("Kafka producer process exit event triggered.");
        try
        {
            _producer.Flush();
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "Kafka producer got exception while flushing during process termination");
        }
    }

    ~KafkaProducerService()
    {
        _logger.LogDebug("Kafka producer finalizer called.");
        try
        {
            _producer.Flush();
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "Kafka producer got exception while flushing during finalization");
        }
    }
}
