using Confluent.Kafka;
using Confluent.Kafka.SyncOverAsync;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using Google.Protobuf;
using Namespace.Workshop.V1;

public class KafkaConsumerService : BackgroundService
{
    private readonly ILogger<KafkaConsumerService> _logger;
    private readonly IKeyValueStateService _keyValueStateService;
    private readonly IInboxStateService _inbox;
    private readonly KafkaAdminClient _kafkaAdminClient;
    private readonly CachedSchemaRegistryClient _schemaRegistryClient;
    private readonly KafkaTopic _topic;
    private readonly Func<byte[], byte[]> _decrypt;
    private readonly Func<string, string> _decryptHeaderKey;

    public KafkaConsumerService(ILogger<KafkaConsumerService> logger, IKeyValueStateService keyValueStateService, IInboxStateService inbox, KafkaAdminClient kafkaAdminClient)
    {
        _logger = logger;
        _keyValueStateService = keyValueStateService;
        _inbox = inbox;
        _kafkaAdminClient = kafkaAdminClient;
        if (Environment.GetEnvironmentVariable(KV_API_ENCRYPT_DATA_ON_KAFKA) == "true")
        {
            var cryptoService = new CryptoService();
            _decrypt = cryptoService.Decrypt;
            _decryptHeaderKey = delegate(string input) { return System.Text.Encoding.UTF8.GetString(cryptoService.Decrypt(Convert.FromBase64String(input))); };
        }
        else
        {
            _decrypt = delegate(byte[] input) { return input; };
            _decryptHeaderKey = delegate(string input) { return input; };
        }

        var topicName = Environment.GetEnvironmentVariable(KV_API_KAFKA_TOPIC_READ);
        if(string.IsNullOrWhiteSpace(topicName))
        {
            _logger.LogError($"Cannot consume if topic is not specified. Environment variable {nameof(KV_API_KAFKA_TOPIC_READ)} was not set/is empty.");
            throw new InvalidOperationException($"Environment variable {nameof(KV_API_KAFKA_TOPIC_READ)} has to have value.");
        }
        _topic = new KafkaTopic { Value = topicName };

        var schemaRegistryClientConfig = KafkaSchemaRegistryConfigEnvBinder.GetSchemaRegistryConfig();
        var schemaRegistryClient = new Confluent.SchemaRegistry.CachedSchemaRegistryClient(schemaRegistryClientConfig);
        _schemaRegistryClient = schemaRegistryClient;

        _logger.LogDebug($"{nameof(KafkaConsumerService)} initialized");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _ = await _kafkaAdminClient.TryCreateTopics();
        _logger.LogDebug("Kafka consumer service is doing pre startup blocking work.");

        await DoWork(stoppingToken);
    }

    private async Task DoWork(CancellationToken stoppingToken)
    {
        _logger.LogDebug("Kafka consumer service background task started.");

        var consumer = GetConsumer();

        await SaveStartupTimeLastTopicPartitionOffsets(consumer);

        consumer.Subscribe(_topic.Value);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var result = consumer.Consume(stoppingToken);

                // await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken); // Slow down read for playing with readiness checks
                // _logger.LogInformation("Waited");

                if (result?.Message == null)
                {
                    _logger.LogDebug("We've reached the end of the topic.");
                    await Task.Delay(TimeSpan.FromSeconds(8), stoppingToken);
                }
                else
                {
                    var correlationIdFromHeader = string.Empty;
                    if(result.Message.Headers.TryGetLastBytes("Correlation-Id", out byte[] correlationIdHeaderValue)) correlationIdFromHeader = System.Text.Encoding.UTF8.GetString(correlationIdHeaderValue);
                    _logger.LogTrace($"The next event on topic {result.TopicPartitionOffset.Topic} partition {result.TopicPartitionOffset.Partition.Value} offset {result.TopicPartitionOffset.Offset.Value} received to the topic at the time {result.Message.Timestamp.UtcDateTime:o} has correlation ID \"{correlationIdFromHeader}\"");

                    var key = _decrypt(result.Message.Key);
                    var value = result.Message.Value == null
                        ? null
                        : _decrypt(result.Message.Value.ToByteArray());

                    _inbox.Enqueue(
                        key,
                        value,
                        correlationIdFromHeader,
                        result.Topic,
                        result.Partition.Value,
                        result.Offset.Value,
                        result.Message.Timestamp.UtcDateTime);
                    _keyValueStateService.UpdateLastConsumedTopicPartitionOffsets(new KafkaTopicPartitionOffset
                        {
                            Topic = new KafkaTopic { Value = result.Topic },
                            Partition = new KafkaPartition { Value = result.Partition.Value },
                            Offset = new KafkaOffset{ Value = result.Offset.Value }
                        });
                }
            }
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "Kafka consumer received exception while consuming, exiting");
        }
        finally
        {
            // Close consumer
            _logger.LogDebug("Disconnecting consumer from Kafka cluster, leaving consumer group and all that");
            consumer.Close();
        }
    }

    private IConsumer<byte[], ByteArrayMessage?> GetConsumer()
    {
        var consumerConfig = KafkaConsumerConfigEnvBinder.GetConsumerConfig();
        var schemaRegistryConfig = KafkaSchemaRegistryConfigEnvBinder.GetSchemaRegistryConfig();
        var schemaRegistry = new CachedSchemaRegistryClient(schemaRegistryConfig);
        var protobufSerializerConfig = new ProtobufSerializerConfig()
        {
            BufferBytes = 100
        };
        var consumer = new ConsumerBuilder<byte[], ByteArrayMessage?>(consumerConfig)
            .SetValueDeserializer(new ProtobufDeserializer<ByteArrayMessage?>(schemaRegistry, protobufSerializerConfig).AsSyncOverAsync())
            .SetPartitionsAssignedHandler((c, partitions) =>
            {
                var savedTpos = _keyValueStateService.GetLastConsumedTopicPartitionOffsets();
                if(savedTpos.Count > 0)
                {
                    // If partitions changed, all hope is lost, just start from the beginning on all of them
                    if(savedTpos.Count == partitions.Count)
                    {
                        // var savedPartitions = savedTpos.Select(x => x.Partition).ToList();
                        if(partitions.All(p => savedTpos.Any(s => s.Partition.Value == p.Partition.Value)))
                        {
                            return savedTpos.Select(tpo => new TopicPartitionOffset(tpo.Topic.Value, new Partition(tpo.Partition.Value), new Offset(tpo.Offset.Value)));
                        }
                        _logger.LogWarning($"There were saved processed topic partition offsets in storage, but the partition IDs didn't match the ones received from the consumer group. Something is disturbing.");
                    }
                    _logger.LogWarning($"There were saved processed topic partition offsets in storage, but number of saved partitions didn't match number of partitions received from consumer group. Something is fishy.");
                    _logger.LogInformation($"Topics received form consumer: {System.Text.Json.JsonSerializer.Serialize(partitions)}");
                    _logger.LogInformation($"Topics form state storage: {System.Text.Json.JsonSerializer.Serialize(savedTpos)}");
                }
                _logger.LogDebug($"Starting consuming all topics from beginning");
                // When starting up, always read the topic from the beginning.
                var offsets = partitions.Select(tp => new TopicPartitionOffset(tp, Offset.Beginning));
                return offsets;
            })
            .Build();
        return consumer;
    }

    private async Task SaveStartupTimeLastTopicPartitionOffsets(IConsumer<byte[], ByteArrayMessage?> consumer)
    {
        var partitions = await _kafkaAdminClient.GetTopicPartitions(_topic);
        List<KafkaTopicPartitionOffset> highOffsetsAtStartupTime = [];
        List<KafkaTopicPartitionOffset> lowOffsetsAtStartupTime = [];
        foreach(var partition in partitions)
        {
            var currentOffsets = consumer.QueryWatermarkOffsets(partition, timeout: TimeSpan.FromSeconds(5));
            if(currentOffsets?.High.Value != null)
            {
                highOffsetsAtStartupTime.Add(new KafkaTopicPartitionOffset
                    {
                        Topic = _topic,
                        Partition = new KafkaPartition { Value = partition.Partition.Value },
                        Offset = currentOffsets.High.Value == 0 ? new KafkaOffset { Value = 0 } : new KafkaOffset { Value = currentOffsets.High.Value - 1 }, // Subtract 1, because received value is "the next that would be written"
                    });
                lowOffsetsAtStartupTime.Add(new KafkaTopicPartitionOffset
                    {
                        Topic = _topic,
                        Partition = new KafkaPartition { Value = partition.Partition.Value },
                        Offset = new KafkaOffset { Value = currentOffsets.Low.Value }, // Defaults to 0 if none are written
                    });
            }
        }
        if(!_keyValueStateService.SetStartupTimeHightestTopicPartitionOffsets(highOffsetsAtStartupTime))
        {
            _logger.LogError($"Failed to save what topic high watermark offsets are at startup time");
        }
        if(_keyValueStateService.GetLastConsumedTopicPartitionOffsets().Count == 0)
        {
            foreach(var partitionOffset in lowOffsetsAtStartupTime)
            {
                if(!_keyValueStateService.UpdateLastConsumedTopicPartitionOffsets(partitionOffset))
                {
                    _logger.LogError($"Failed to set up low watermark offset for partition {partitionOffset.Offset.Value} at startup time");
                }
            }
        }
    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        _logger.LogDebug("Kafka consumer received request for graceful shutdown.");

        await base.StopAsync(stoppingToken);
    }
}
