using Confluent.Kafka;
using Confluent.Kafka.Admin;

public class KafkaAdminClient
{
    private readonly ILogger<KafkaAdminClient> _logger;

    public KafkaAdminClient(ILogger<KafkaAdminClient> logger)
    {
        _logger = logger;
        logger.LogDebug($"{nameof(KafkaAdminClient)} initialized");
    }

    public async Task<bool> TryCreateTopics()
    {
        var topicWrite = Environment.GetEnvironmentVariable(KV_API_KAFKA_TOPIC_WRITE);
        var tWCreateResult = false;
        if (!string.IsNullOrEmpty(topicWrite))
        {
            tWCreateResult = await TryCreateTopic(topicWrite);
        }

        var topicRead = Environment.GetEnvironmentVariable(KV_API_KAFKA_TOPIC_READ);
        var tRCreateResult = false;
        if (!string.IsNullOrEmpty(topicRead))
        {
            tRCreateResult = await TryCreateTopic(topicRead);
        }

        return tWCreateResult && tRCreateResult;
    }

    public async Task<bool> TryCreateTopic(string topic)
    {
        var adminClientConfig = KafkaAdminClientEnvBinder.GetAdminClientConfig();

        using var adminClient = new AdminClientBuilder(adminClientConfig).Build();
        try
        {
            _logger.LogDebug($"Asking admin client to create topic {topic} in case it doesn't exist");
            await adminClient.CreateTopicsAsync(new TopicSpecification[] {
                    new TopicSpecification
                    {
                         Name = topic,
                         ReplicationFactor = -1,
                         NumPartitions = -1,
                         Configs = new Dictionary<string, string>
                         {
                            { "cleanup.policy", "compact" },
                            { "retention.bytes", "-1" },
                            { "retention.ms", "-1" },
                            { "min-compaction-lag-ms", $"{TimeSpan.FromMinutes(15).TotalMilliseconds}" },
                            { "max.compaction.lag.ms", $"{TimeSpan.FromHours(1).TotalMilliseconds}" },
                            { "segment.ms", $"{TimeSpan.FromHours(2).TotalMilliseconds}" },
                            { "delete.retention.ms", $"{TimeSpan.FromDays(1).TotalMilliseconds}" },
                            { "min.cleanable.dirty.ratio", "0.90" },
                        }
                    }
                });
            _logger.LogDebug($"Admin client done trying to create topic {topic}");
            return true;
        }
        catch (Exception e)
        {
            if (e is Confluent.Kafka.Admin.CreateTopicsException && e.Message.Contains($"Topic '{topic}' already exists."))
            {
                // Doing it this way with exception to check is kind of bad, but for a 1-off "check this during startup" it's not really worth it to start the "query cluster for info about everything including all topics" dance.
                // Still, don't do this at home kids.
                _logger.LogDebug($"Admin client did not create topic {topic} because it already exists");
                return false;
            }
            _logger.LogError(e, $"An error occurred creating topic");
        }

        return false;
    }

    public async Task<List<TopicPartition>> GetTopicPartitions(KafkaTopic topic)
    {
        var adminClientConfig = KafkaAdminClientEnvBinder.GetAdminClientConfig();
        using var adminClient = new AdminClientBuilder(adminClientConfig).Build();
        try
        {
            var description = await adminClient.DescribeTopicsAsync(TopicCollection.OfTopicNames([topic.Value]));
            List<TopicPartition> topicPartitions = description.TopicDescriptions
                .FirstOrDefault(tDescription => tDescription.Name == topic.Value)
                ?.Partitions
                .Select(tpInfo => new TopicPartition(topic.Value, tpInfo.Partition))
                .ToList() ?? [];
            return topicPartitions;
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"An error occurred when retrieving list of partitions on topic");
        }
        return [];
    }

}
