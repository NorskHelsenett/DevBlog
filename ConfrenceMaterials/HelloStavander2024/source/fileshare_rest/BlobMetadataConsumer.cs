using Confluent.Kafka;
using Confluent.Kafka.SyncOverAsync;
using Confluent.SchemaRegistry.Serdes;
using KafkaBlobChunking;
using Timestamp = Google.Protobuf.WellKnownTypes.Timestamp;

public class BlobMetadataConsumer: BackgroundService
{
    private readonly ILogger<BlobMetadataConsumer> _logger;
    private readonly IHostApplicationLifetime _hostApplicationLifetime;
    private readonly OutputStateService _outputStateService;
    private readonly UserAccessMappingStateService _userAccessMappingStateService;
    private readonly UserAccessMappingProducer _userAccessMappingProducer;
    private readonly string _topicMetadata;

    public BlobMetadataConsumer(ILogger<BlobMetadataConsumer> logger, IHostApplicationLifetime hostApplicationLifetime, OutputStateService outputStateService, UserAccessMappingStateService userAccessMappingStateService, UserAccessMappingProducer userAccessMappingProducer)
    {
        _logger = logger;
        _hostApplicationLifetime = hostApplicationLifetime;
        _outputStateService = outputStateService;
        _userAccessMappingStateService = userAccessMappingStateService;
        _userAccessMappingProducer = userAccessMappingProducer;

        var topicNameMetadataTopic = Environment.GetEnvironmentVariable(BIG_PAYLOADS_METADATA_TOPIC);
        if(string.IsNullOrWhiteSpace(topicNameMetadataTopic))
        {
            _logger.LogError($"Cannot consume if topic is not specified. Environment variable {nameof(BIG_PAYLOADS_METADATA_TOPIC)} was not set/is empty.");
            throw new InvalidOperationException($"Environment variable {nameof(BIG_PAYLOADS_METADATA_TOPIC)} has to have value.");
        }
        _topicMetadata = topicNameMetadataTopic;

        _logger.LogDebug($"{nameof(BlobMetadataConsumer)} initialized");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogDebug("Kafka consumer service is doing pre startup blocking work.");

        await DoWork(stoppingToken);
        _hostApplicationLifetime.StopApplication();
    }

    private async Task DoWork(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(1));
        _logger.LogDebug("Setting up Kafka blob metadata consumer.");
        var metadataConsumer = GetChunkMetadataConsumer();
        _logger.LogDebug("Storing high watermarks at startup time");
        await SaveStartupTimeLastTopicPartitionOffsets(metadataConsumer);
        _logger.LogDebug($"Subscribing to topic {_topicMetadata}");
        metadataConsumer.Subscribe(_topicMetadata);
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var result = metadataConsumer.Consume(stoppingToken);

                if (result?.Message == null)
                {
                    _logger.LogDebug("We've reached the end of the metadata topic.");
                    await Task.Delay(TimeSpan.FromSeconds(8), stoppingToken);
                }
                else
                {
                    _logger.LogDebug("New blob metadata event received");
                    var nextBlobMetadata = result.Message.Value;
                    if (nextBlobMetadata != null)
                    {
                        _outputStateService.Store(blobChunksMetadata: nextBlobMetadata, correlationId: nextBlobMetadata.CorrelationId);
                        var userAccessMappingRegistered = _userAccessMappingStateService.TryGetUserAccessMapping(blobId: result.Message.Key, out _);
                        if (!userAccessMappingRegistered)
                        {
                            await _userAccessMappingProducer.ProduceUserAccessMappingAsync(new UserAccessMapping
                            {
                                BlobName = nextBlobMetadata.BlobName,
                                Owner = nextBlobMetadata.BlobOwnerId,
                                UpdatedAt =
                                    Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow),
                                UpdatedBy = nextBlobMetadata.BlobOwnerId,
                                CorrelationId = nextBlobMetadata.CorrelationId,
                            }, result.Message.Key, stoppingToken);
                        }
                        _logger.LogDebug($"Consumed metadata for blob with id \"{result.Message.Key}\"");
                    }
                    else
                    {
                        _logger.LogInformation($"Received tombstone for blob with id \"{result.Message.Key}\"");
                        _outputStateService.Remove(result.Message.Key, "");
                    }
                    _outputStateService.UpdateLastConsumedTopicPartitionOffsets(result.TopicPartitionOffset);
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Kafka payload metadata consumer received exception while consuming, exiting");
        }
        finally
        {
            _logger.LogDebug("MetadataConsumer shutting down and disconnecting from the cluster");
            metadataConsumer.Close();
        }
    }

    private IConsumer<string, BlobChunksMetadata?> GetChunkMetadataConsumer()
    {
        return  new ConsumerBuilder<string, BlobChunksMetadata?>(KafkaConfigBinder.GetConsumerConfig())
            .SetPartitionsAssignedHandler((c, partitions) =>
            {
                // Always start at the beginning, only use cg for tracking liveliness and lag from the outside
                return partitions.Select(tp => new TopicPartitionOffset(tp, Offset.Beginning));
            })
            .SetValueDeserializer(new ProtobufDeserializer<BlobChunksMetadata?>().AsSyncOverAsync())
            .SetErrorHandler((_, e) => _logger.LogError($"Error: {e.Reason}"))
            .Build();
    }

    private async Task SaveStartupTimeLastTopicPartitionOffsets(IConsumer<string, BlobChunksMetadata?> consumer)
    {
        var partitions = await GetTopicPartitions(_topicMetadata);
        List<TopicPartitionOffset> highOffsetsAtStartupTime = [];
        List<TopicPartitionOffset> lowOffsetsAtStartupTime = [];
        foreach(var partition in partitions)
        {
            var currentOffsets = consumer.QueryWatermarkOffsets(partition, timeout: TimeSpan.FromSeconds(5));
            if(currentOffsets?.High.Value != null)
            {
                // Subtract 1, because received value is "the next that would be written"
                long offsetHigh = currentOffsets.High.Value == 0 ? 0 : currentOffsets.High.Value - 1;
                highOffsetsAtStartupTime.Add(new TopicPartitionOffset(_topicMetadata, partition.Partition.Value, offsetHigh));

                // Offset value defaults to 0 if none are written
                lowOffsetsAtStartupTime.Add(new TopicPartitionOffset(_topicMetadata, partition.Partition.Value, currentOffsets.Low.Value));
            }
        }
        if(!_outputStateService.SetStartupTimeHightestTopicPartitionOffsets(highOffsetsAtStartupTime))
        {
            _logger.LogError($"Failed to save what topic high watermark offsets are at startup time");
        }
        if(_outputStateService.GetLastConsumedTopicPartitionOffsets().Count == 0)
        {
            foreach(var partitionOffset in lowOffsetsAtStartupTime)
            {
                if(!_outputStateService.UpdateLastConsumedTopicPartitionOffsets(partitionOffset))
                {
                    _logger.LogError($"Failed to set up low watermark offset for partition {partitionOffset.Offset.Value} at startup time");
                }
            }
        }
    }

    private async Task<List<TopicPartition>> GetTopicPartitions(string topic)
    {
        var adminClientConfig = KafkaConfigBinder.GetAdminClientConfig();
        using var adminClient = new AdminClientBuilder(adminClientConfig).Build();
        try
        {
            var description = await adminClient.DescribeTopicsAsync(TopicCollection.OfTopicNames([topic]));
            List<TopicPartition> topicPartitions = description.TopicDescriptions
                .FirstOrDefault(tDescription => tDescription.Name == topic)
                ?.Partitions
                .Select(tpInfo => new TopicPartition(topic, tpInfo.Partition))
                .ToList() ?? [];
            return topicPartitions;
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"An error occurred when retrieving list of partitions on topic");
        }
        return [];
    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        _logger.LogDebug("Kafka consumer received request for graceful shutdown.");

        await base.StopAsync(stoppingToken);
    }
}
