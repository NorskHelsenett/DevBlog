using System.Diagnostics;
using System.Diagnostics.Metrics;
using Confluent.Kafka;
using Confluent.Kafka.SyncOverAsync;
using Confluent.SchemaRegistry.Serdes;
using No.Nhn.Address.Cadastre.ImportFormat;

namespace AddressRefiner;

public class RawAddressStreamConsumer : BackgroundService
{
    private readonly ILogger<RawAddressStreamConsumer> _logger;
    private readonly ActivitySource _activitySource;
    private readonly IAddressStorage _addressStorage;
    private readonly IRefinedAddressStreamProducer _refinedAddressStreamProducer;
    private readonly string _topic;
    private readonly string _bootstrapServersMetadata;
    private readonly IHostApplicationLifetime _hostApplicationLifetime;
    private readonly Counter<long> _successCounter;

    public RawAddressStreamConsumer(ILogger<RawAddressStreamConsumer> logger, ActivitySource activitySource, Meter meter, IAddressStorage addressStorage, IRefinedAddressStreamProducer refinedAddressStreamProducer, IHostApplicationLifetime hostApplicationLifetime)
    {
        _logger = logger;
        _activitySource = activitySource;
        _hostApplicationLifetime = hostApplicationLifetime;
        _addressStorage = addressStorage;
        _refinedAddressStreamProducer = refinedAddressStreamProducer;

        _successCounter = meter.CreateCounter<long>("consume.addresses.raw.successes.count", description: "Number of raw input addresses successfully consumed");

        var topicName = Environment.GetEnvironmentVariable(ADDRESS_REFINER_KAFKA_TOPIC_RAW_ADDRESSES);
        if(string.IsNullOrWhiteSpace(topicName))
        {
            _logger.LogError($"Cannot consume if topic is not specified. Environment variable {nameof(ADDRESS_REFINER_KAFKA_TOPIC_RAW_ADDRESSES)} was not set/is empty.");
            throw new InvalidOperationException($"Environment variable {nameof(ADDRESS_REFINER_KAFKA_TOPIC_RAW_ADDRESSES)} has to have value.");
        }
        _topic = topicName;
        _bootstrapServersMetadata = Environment.GetEnvironmentVariable(KAFKA_BOOTSTRAP_SERVERS) ?? "";

        _logger.LogDebug($"{nameof(RawAddressStreamConsumer)} initialized");
    }

    private async Task WaitForAddressStorageReady(CancellationToken stoppingToken)
    {
        _logger.LogInformation($"{nameof(RefinedAddressStreamConsumer)} Waiting for address storage to be populated.");
        while (!stoppingToken.IsCancellationRequested)
        {
            if(_addressStorage.Ready()) break;
            _logger.LogDebug($"{nameof(WaitForAddressStorageReady)} address storage not ready, waiting a bit before checking again");
            await Task.Delay(TimeSpan.FromSeconds(7), stoppingToken);
        }
        _logger.LogInformation($"{nameof(RefinedAddressStreamConsumer)} Address storage ready!");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogDebug("Kafka refined addresses consumer service is doing pre startup blocking work.");
        await WaitForAddressStorageReady(stoppingToken);
        await DoWork(stoppingToken);
        _hostApplicationLifetime.StopApplication();
    }

    private async Task DoWork(CancellationToken stoppingToken)
    {
        _logger.LogDebug("Kafka raw addresses consumer service background task started.");

        var consumer = GetConsumer();
        var heartbeatTime = DateTime.MinValue;
        var heartbeatOffset = 0L;

        consumer.Subscribe(_topic);
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using var activity = _activitySource.StartActivity("RawAddressStreamConsumer.DoWork.BestestActivity");

                activity?.AddEvent(new ActivityEvent("Stating consume", DateTimeOffset.UtcNow));
                var result = consumer.Consume(stoppingToken);
                activity?.AddEvent(new ActivityEvent("Consume done", DateTimeOffset.UtcNow));
                _successCounter.Add(1);
                if (heartbeatTime + TimeSpan.FromSeconds(15) < DateTime.UtcNow)
                {
                    _logger.LogTrace("{HeartbeatRawAddressStreamConsumer}", new
                    {
                        component = nameof(RawAddressStreamConsumer),
                        method = nameof(DoWork),
                        topic = _topic,
                        currentOffset = result.Offset,
                        currentAddressId = result.Message.Key,
                        currentTimeStamp = $"{DateTime.UtcNow:u}",
                        changeInTimestampSinceLast = DateTime.UtcNow - heartbeatTime,
                        changeInOffsetSinceLast = result.Offset - heartbeatOffset,
                        message = "Raw address consumer heartbeat"
                    });
                    heartbeatOffset = result.Offset;
                    heartbeatTime = DateTime.UtcNow;
                }

                if (result?.Message == null)
                {
                    _logger.LogDebug("We've reached the end of the raw addresses topic.");
                    await Task.Delay(TimeSpan.FromSeconds(8), stoppingToken);
                }
                else
                {
                    var correlationIdFromHeader = Guid.NewGuid().ToString("D");
                    if(result.Message.Headers.TryGetLastBytes("Correlation-Id", out byte[] correlationIdHeaderValue)) correlationIdFromHeader = System.Text.Encoding.UTF8.GetString(correlationIdHeaderValue);
                    // _logger.LogTrace($"The next event on topic {result.TopicPartitionOffset.Topic} partition {result.TopicPartitionOffset.Partition.Value} offset {result.TopicPartitionOffset.Offset.Value} received to the topic at the time {result.Message.Timestamp.UtcDateTime:o} has correlation ID \"{correlationIdFromHeader}\"");
                    if(result.Message.Value == null)
                    {
                        var key = result.Message.Key;
                        _logger.LogWarning($"Raw import addresses topic contained tombstone for key {key}, which is highly unexpected. Ignoring event");
                        continue;
                    }

                    activity?.AddEvent(new ActivityEvent("Start converting raw address", DateTimeOffset.UtcNow));
                    var addressUpdate = result.Message.Value.ToCadastreRoadAddress();
                    var valueIsNew = true;
                    activity?.AddEvent(new ActivityEvent("Fetch pre registered address", DateTimeOffset.UtcNow));
                    var addressPreviouslyRegistered = _addressStorage.TryRetrieve(addressUpdate.AddressId, out var addressOldData);
                    if (addressPreviouslyRegistered)
                    {
                        activity?.AddEvent(new ActivityEvent("Comparing candidate to pre registered address", DateTimeOffset.UtcNow));
                        var dataIsUpdated = addressOldData.ValueEquals(addressUpdate);
                        // var dataIsUpdated = addressOldData.AddressId == addressUpdate.AddressId; // No need, isn't performance bottleneck yet
                        if (!dataIsUpdated)
                        {
                            valueIsNew = false;
                        }
                    }
                    if(valueIsNew)
                    {
                        var headers = new Confluent.Kafka.Headers
                        {
                            { "Correlation-Id", System.Text.Encoding.UTF8.GetBytes(correlationIdFromHeader) },
                            { "provenance.origin.clusterBootstrapAddress", System.Text.Encoding.UTF8.GetBytes(_bootstrapServersMetadata) },
                            { "provenance.origin.topic", System.Text.Encoding.UTF8.GetBytes(result.Topic) },
                            { "provenance.origin.partition", System.Text.Encoding.UTF8.GetBytes(result.Partition.Value.ToString()) },
                            { "provenance.origin.offset", System.Text.Encoding.UTF8.GetBytes(result.Offset.Value.ToString()) }
                        };
                        activity?.AddEvent(new ActivityEvent("Publishing update", DateTimeOffset.UtcNow));
                        _ = await _refinedAddressStreamProducer.Produce(key: addressUpdate.AddressId, value: addressUpdate, headers: headers, correlationId: correlationIdFromHeader);
                    }
                    activity?.AddEvent(new ActivityEvent("Reached end", DateTimeOffset.UtcNow));
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

    private IConsumer<string, CadastreRoadAddressImport> GetConsumer()
    {
        var consumerConfig = KafkaConfigBinder.GetConsumerConfig();
        var consumerBuilder = new ConsumerBuilder<string, CadastreRoadAddressImport>(consumerConfig);
        if (Environment.GetEnvironmentVariable(ADDRESS_REFINER_KAFKA_READ_RAW_TOPIC_FROM_BEGINNING)?.ToLowerInvariant() == "true")
        {
            consumerBuilder
                .SetPartitionsAssignedHandler((c, partitions) =>
                {
                    return partitions.Select(tp => new TopicPartitionOffset(tp, Offset.Beginning));
                });
        }
        var consumer = consumerBuilder
            .SetValueDeserializer(new ProtobufDeserializer<CadastreRoadAddressImport>().AsSyncOverAsync())
            .SetErrorHandler((_, e) => _logger.LogError($"Error: {e.Reason}"))
            .Build();
        return consumer;
    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        _logger.LogDebug("Kafka consumer received request for graceful shutdown.");

        await base.StopAsync(stoppingToken);
    }
}
