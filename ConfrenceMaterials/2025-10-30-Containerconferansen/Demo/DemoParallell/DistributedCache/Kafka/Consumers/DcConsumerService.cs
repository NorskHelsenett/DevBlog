using System.Diagnostics;
using System.Diagnostics.Metrics;
using Confluent.Kafka;

public class DcConsumerService : BackgroundService
{
    private readonly ILogger<DcConsumerService> _logger;
    private readonly ActivitySource _activitySource;
    private readonly IStorageInbox _storageInbox;
    private readonly string _topic;
    private readonly IHostApplicationLifetime _hostApplicationLifetime;

    public DcConsumerService(ILogger<DcConsumerService> logger, ActivitySource activitySource, Meter meter, IStorageInbox storageInbox, IHostApplicationLifetime hostApplicationLifetime)
    {
        _logger = logger;
        _activitySource = activitySource;

        _hostApplicationLifetime = hostApplicationLifetime;
        _storageInbox = storageInbox;

        var topicName = Environment.GetEnvironmentVariable(DISTRIBUTED_CACHE_KAFKA_TOPIC);
        if(string.IsNullOrWhiteSpace(topicName))
        {
            _logger.LogError($"Cannot consume if topic is not specified. Environment variable {nameof(DISTRIBUTED_CACHE_KAFKA_TOPIC)} was not set/is empty.");
            throw new InvalidOperationException($"Environment variable {nameof(DISTRIBUTED_CACHE_KAFKA_TOPIC)} has to have value.");
        }
        _topic = topicName;

        _logger.LogDebug($"{nameof(DcConsumerService)} initialized");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogDebug("Kafka refined addresses consumer service is doing pre startup blocking work.");
        await DoWork(stoppingToken);
        _hostApplicationLifetime.StopApplication();
    }

    private async Task DoWork(CancellationToken stoppingToken)
    {
        _logger.LogDebug("Kafka refined addresses consumer service background task started.");

        var consumer = GetConsumer();

        await SaveStartupTimeLastTopicPartitionOffsets(consumer);

        // consumer.Subscribe(_topic);
        await AssignTopicPartitions(consumer, _topic, stoppingToken);
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var result = consumer.Consume(stoppingToken);
                using var activity = _activitySource.StartActivity($"{nameof(DcConsumerService)}.consume.processNextConsumed");

                if (result?.Message == null)
                {
                    _logger.LogDebug("We've reached the end of the refined addresses topic.");
                    activity?.AddEvent(new ActivityEvent("Reached end of topic, sleeping", DateTimeOffset.UtcNow));
                    await Task.Delay(TimeSpan.FromSeconds(8), stoppingToken);
                }
                else
                {
                    // var correlationIdFromHeader = string.Empty;
                    // if(result.Message.Headers.TryGetLastBytes("Correlation-Id", out byte[] correlationIdHeaderValue)) correlationIdFromHeader = System.Text.Encoding.UTF8.GetString(correlationIdHeaderValue);
                    if(result.Message.Value == null)
                    {
                        activity?.AddEvent(new ActivityEvent("Processing tombstone", DateTimeOffset.UtcNow));
                        var key = result.Message.Key;
                        _storageInbox.Remove(key);
                        activity?.AddEvent(new ActivityEvent("DoneProcessing tombstone", DateTimeOffset.UtcNow));
                    }
                    else
                    {
                        activity?.AddEvent(new ActivityEvent("Processing new item", DateTimeOffset.UtcNow));
                        var key = result.Message.Key;
                        var value = result.Message.Value;
                        List<KeyValuePair<string, string>>? headers = null;
                        if (result.Message.Headers.Count != 0)
                        {
                            headers = [];
                            foreach (var kHeader in result.Message.Headers)
                            {
                                try
                                {
                                    var valueAsUtf8 = System.Text.Encoding.UTF8.GetString(kHeader.GetValueBytes());
                                    headers.Add(new KeyValuePair<string, string> (kHeader.Key, valueAsUtf8));
                                }
                                catch
                                {
                                    _logger.LogWarning("Failed to deserialize header {kafkaHeaderName} as utf-8", kHeader.Key);
                                }
                            }
                        }
                        activity?.AddEvent(new ActivityEvent("Done extracting item data", DateTimeOffset.UtcNow));
                        _storageInbox.Store(new DcItem { Key = key, Value = value, Headers = headers });
                        activity?.AddEvent(new ActivityEvent("New item stored", DateTimeOffset.UtcNow));
                    }
                    _storageInbox.UpdateLastConsumedTopicPartitionOffsets(new TopicPartitionOffset(result.Topic, result.Partition.Value, result.Offset.Value));
                    activity?.AddEvent(new ActivityEvent("Last consumed tpo updated", DateTimeOffset.UtcNow));
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

    private async Task AssignTopicPartitions(IConsumer<string, byte[]?> consumer, string topic, CancellationToken cancellationToken)
    {
        using var activity = _activitySource.StartActivity($"{nameof(DcConsumerService)}.AssigningTopicPartitions");
        var adminClient = new AdminClientBuilder(KafkaConfigBinder.GetAdminClientConfig()).Build();
        activity?.AddEvent(new ActivityEvent("admin client created", DateTimeOffset.UtcNow));
        var topicsMetadata = adminClient.GetMetadata(topic,TimeSpan.FromSeconds(3));
        activity?.AddEvent(new ActivityEvent("retrieved metadata", DateTimeOffset.UtcNow));
        if (topicsMetadata.Topics.Count != 1)
        {
            _logger.LogError($"Topic {topic} not found. Shutting down");
            _hostApplicationLifetime.StopApplication();
        }
        var topicMetadata = topicsMetadata.Topics.Single();
        var topicPartitions = topicMetadata.Partitions.Select(p=>new TopicPartition(topicMetadata.Topic, p.PartitionId)).ToArray();
        var topicPartitionOffsets = topicPartitions.Select(tp => new TopicPartitionOffset(tp,Offset.Beginning)).ToArray();
        activity?.AddEvent(new ActivityEvent("extracted info from metadata", DateTimeOffset.UtcNow));
        consumer.Assign(topicPartitionOffsets);
        activity?.AddEvent(new ActivityEvent("Consumer assigned", DateTimeOffset.UtcNow));
        _logger.LogInformation($"Give Kafka brokers some time to figure out who handles the assignment to the {topic} topic");
        await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
    }

    private IConsumer<string, byte[]?> GetConsumer()
    {
        using var activity = _activitySource.StartActivity($"{nameof(DcConsumerService)}.CreateConsumer");
        var consumerConfig = KafkaConfigBinder.GetConsumerConfig();
        var consumer = new ConsumerBuilder<string, byte[]?>(consumerConfig)
            .SetPartitionsAssignedHandler((c, partitions) =>
            {
                var savedTpos = _storageInbox.GetLastConsumedTopicPartitionOffsets();
                // Check if we can start consuming where we left of, if persisted state
                if(savedTpos.Count > 0)
                {
                    // If partitions changed, all hope is lost, just start from the beginning on all of them
                    if(savedTpos.Count == partitions.Count)
                    {
                        if(partitions.All(p => savedTpos.Any(s => s.Partition.Value == p.Partition.Value)))
                        {
                            return savedTpos.Select(tpo => new TopicPartitionOffset(tpo.Topic, new Partition(tpo.Partition.Value), new Offset(tpo.Offset.Value)));
                        }
                        _logger.LogWarning($"There were saved processed topic partition offsets in storage, but the partition IDs didn't match the ones received from the consumer group. Something is disturbing.");
                    }
                    _logger.LogWarning($"There were saved processed topic partition offsets in storage, but number of saved partitions didn't match number of partitions received from consumer group. Something is fishy.");
                    _logger.LogInformation($"Topics received form consumer: {string.Join(";\n",partitions.Select(p => "Topic: " + p.Topic + ", Partition: " + p.Partition.Value))}");
                    _logger.LogInformation($"Topics form state storage: {string.Join(";\n",savedTpos.Select(p => "Topic: " + p.Topic + ", Partition: " + p.Partition.Value + ", offset: " + p.Offset.Value))}");
                }
                _logger.LogDebug($"Starting consuming all topics from beginning");
                // When starting up, always read the topic from the beginning.
                var offsets = partitions.Select(tp => new TopicPartitionOffset(tp, Offset.Beginning));
                return offsets;
            })
            .SetErrorHandler((_, e) => _logger.LogError($"Error: {e.Reason}"))
            .Build();
        activity?.AddEvent(new ActivityEvent("done setting up consumer", DateTimeOffset.UtcNow));
        return consumer;
    }

    private async Task SaveStartupTimeLastTopicPartitionOffsets(IConsumer<string, byte[]?> consumer)
    {
        var partitions = await GetTopicPartitions(_topic);
        List<TopicPartitionOffset> highOffsetsAtStartupTime = [];
        List<TopicPartitionOffset> lowOffsetsAtStartupTime = [];
        foreach(var partition in partitions)
        {
            var currentOffsets = consumer.QueryWatermarkOffsets(partition, timeout: TimeSpan.FromSeconds(5));
            if(currentOffsets?.High.Value != null)
            {
                // Subtract 1, because received value is "the next that would be written"
                long offsetHigh = currentOffsets.High.Value == 0 ? 0 : currentOffsets.High.Value - 1;
                highOffsetsAtStartupTime.Add(new TopicPartitionOffset(_topic, partition.Partition.Value, offsetHigh));

                // Offset value defaults to 0 if none are written
                lowOffsetsAtStartupTime.Add(new TopicPartitionOffset(_topic, partition.Partition.Value, currentOffsets.Low.Value));
            }
        }
        if(_storageInbox.SetStartupTimeHightestTopicPartitionOffsets(highOffsetsAtStartupTime) != null)
        {
            _logger.LogError($"Failed to save what topic high watermark offsets are at startup time");
        }
        if(_storageInbox.GetLastConsumedTopicPartitionOffsets().Count == 0)
        {
            foreach(var partitionOffset in lowOffsetsAtStartupTime)
            {
                if(_storageInbox.SetStartupTimeHightestTopicPartitionOffsets(highOffsetsAtStartupTime) != null)
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
