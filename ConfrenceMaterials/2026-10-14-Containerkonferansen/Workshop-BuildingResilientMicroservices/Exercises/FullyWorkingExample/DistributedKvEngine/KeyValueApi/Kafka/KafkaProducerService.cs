using Confluent.Kafka;

public class KafkaProducerService
{
    private readonly ILogger<KafkaConsumerService> _logger;
    private readonly IProducer<byte[], byte[]?> _producer;
    private readonly KafkaTopic _topic;
    private readonly Func<byte[], byte[]> _encrypt;
    private readonly Func<string, string> _encryptHeaderKey;

    public KafkaProducerService(ILogger<KafkaConsumerService> logger)
    {
        _logger = logger;
        if (Environment.GetEnvironmentVariable(KV_API_ENCRYPT_DATA_ON_KAFKA) == "true")
        {
            var cryptoService = new CryptoService();
            _encrypt = cryptoService.Encrypt;
            _encryptHeaderKey = delegate(string input) { return Convert.ToBase64String(cryptoService.Encrypt(System.Text.Encoding.UTF8.GetBytes(input))); };
        }
        else
        {
            _encrypt = delegate(byte[] input) { return input; };
            _encryptHeaderKey = delegate(string input) { return input; };
        }
        AppDomain.CurrentDomain.ProcessExit += new EventHandler(OnProcessExit);
        var config = KafkaProducerConfigEnvBinder.GetProducerConfig();
        _producer = new ProducerBuilder<byte[], byte[]?>(config).Build();
        var topicName = Environment.GetEnvironmentVariable(KV_API_KAFKA_TOPIC_WRITE);
        if(string.IsNullOrWhiteSpace(topicName))
        {
            _logger.LogError($"Cannot consume if topic is not specified. Environment variable {nameof(KV_API_KAFKA_TOPIC_WRITE)} was not set/is empty.");
            throw new InvalidOperationException($"Environment variable {nameof(KV_API_KAFKA_TOPIC_WRITE)} has to have value.");
        }
        _topic = new KafkaTopic { Value = topicName };
        _logger.LogDebug($"{nameof(KafkaProducerService)} initialized");
    }

    public bool Produce(byte[] key, byte[]? value, Dictionary<string, byte[]> headers, CorrelationId correlationId)
    {
        _logger.LogTrace($"Producing message with correlation ID {correlationId.Value}");

        var message = new Message<byte[], byte[]?>
        {
            Key = _encrypt(key),
            Value = (value == null) ? null : _encrypt(value)
        };

        if(headers.Count > 0)
        {
            message.Headers = new Headers();
            foreach(var header in headers)
            {
                message.Headers.Add(_encryptHeaderKey(header.Key), _encrypt(header.Value));
            }
        }
        try
        {
            _producer.Produce(_topic.Value, message);
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, $"Got exception when producing message with correlation ID {correlationId.Value} to topic {_topic}");
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
