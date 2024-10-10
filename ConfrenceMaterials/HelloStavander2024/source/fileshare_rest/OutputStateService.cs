using Confluent.Kafka;
using KafkaBlobChunking;

public class OutputStateService
{
    private readonly ILogger<OutputStateService> _logger;

    private readonly Dictionary<string, BlobChunksMetadata> _blobMetadatas;
    private bool _ready;
    private readonly Dictionary<(string TopicName, int Partition), long> _startupTimeLatestMetadataTpo;
    private readonly Dictionary<(string TopicName, int Partition), long> _latestConsumedMetadataTpo;

    public OutputStateService(ILogger<OutputStateService> logger)
    {
        _logger = logger;
        _blobMetadatas = new();
        _startupTimeLatestMetadataTpo = new();
        _latestConsumedMetadataTpo = new();
        _ready = false;

        _logger.LogDebug($"{nameof(OutputStateService)} initialized");
    }

    public bool Remove(string key, string correlationId)
    {
        if(_blobMetadatas.ContainsKey(key))
        {
            _blobMetadatas.Remove(key);
            _logger.LogDebug($"CorrelationId \"{correlationId}\" Removed blobmetadata with blobId \"{key}\" from output state service.");
            return true;
        }
        return false;
    }

    public bool Store(BlobChunksMetadata blobChunksMetadata, string correlationId)
    {
        _logger.LogDebug($"CorrelationId \"{correlationId}\" Storing new blob metadata with blobId {blobChunksMetadata.BlobId}");
        _blobMetadatas[blobChunksMetadata.BlobId] = blobChunksMetadata;
        return true;
    }

    public bool TryRetrieve(string key, out BlobChunksMetadata? result)
    {
        if(_blobMetadatas.ContainsKey(key))
        {
            result = _blobMetadatas[key];
            return true;
        }
        result = default;
        return false;
    }

    public List<TopicPartitionOffset> GetLastConsumedTopicPartitionOffsets()
    {
        return _latestConsumedMetadataTpo
            .Select(KeyValuePair => new TopicPartitionOffset(KeyValuePair.Key.TopicName, KeyValuePair.Key.Partition, KeyValuePair.Value))
            .ToList();
    }

    public List<TopicPartitionOffset> GetStartupTimeHightestTopicPartitionOffsets()
    {
        return _startupTimeLatestMetadataTpo
            .Select(KeyValuePair => new TopicPartitionOffset(KeyValuePair.Key.TopicName, KeyValuePair.Key.Partition, KeyValuePair.Value))
            .ToList();
    }

    public bool UpdateLastConsumedTopicPartitionOffsets(TopicPartitionOffset topicPartitionOffset)
    {
        _latestConsumedMetadataTpo[(TopicName: topicPartitionOffset.Topic, Partition: topicPartitionOffset.Partition)] = topicPartitionOffset.Offset;
        return true;
    }

    public bool SetStartupTimeHightestTopicPartitionOffsets(List<TopicPartitionOffset> topicPartitionOffsets)
    {
        foreach(var tpo in topicPartitionOffsets)
        {
            _startupTimeLatestMetadataTpo[(TopicName: tpo.Topic, Partition: tpo.Partition)] = tpo.Offset;
        }
        return true;
    }

    public bool Ready()
    {
        _logger.LogTrace($"{nameof(OutputStateService)} received request to check readiness");
        if(_ready) return true;

        if(_startupTimeLatestMetadataTpo.Count == 0) return false;

        if(_startupTimeLatestMetadataTpo.All(KeyValuePair => KeyValuePair.Value == 0))
        {
            _ready = true;
            return true;
        }

        var latestConsumedOffsets = GetLastConsumedTopicPartitionOffsets();

        if(latestConsumedOffsets.Count == 0) return false; // This case should not happen when earliest is set to low watermark before first consume, but leave it in as a safeguard

        foreach(var latestOffset in latestConsumedOffsets)
        {
            var topicPartitionRegistered = _startupTimeLatestMetadataTpo.TryGetValue((latestOffset.Topic, latestOffset.Partition), out long partitionHighWatermarkAtStartupTime);
            if(!topicPartitionRegistered)
                return false;
            if(latestOffset.Offset.Value < partitionHighWatermarkAtStartupTime)
                return false;
        }

        _ready = true;
        return _ready;
    }
}
