public class KeyValueStateInDictService : IKeyValueStateService
{
    private readonly ILogger<KeyValueStateInDictService> _logger;
    private readonly Dictionary<string, List<KeyValue>> _keyValueState;
    private List<KafkaTopicPartitionOffset> _highestOffsetsAtStartupTime;
    private readonly List<KafkaTopicPartitionOffset> _lastConsumedOffsets;
    private bool _ready;

    public KeyValueStateInDictService(ILogger<KeyValueStateInDictService> logger)
    {
        _logger = logger;
        _keyValueState = new Dictionary<string, List<KeyValue>>();
        // _consumerConfig = GetConsumerConfig();
        // crc32 = new();

        // resources todo
        // https://docs.axual.io/axual/2022.1/getting_started/consumer/dotnet/dotnet-kafka-client-consumer.html
        // https://docs.axual.io/axual/2022.1/getting_started/producer/dotnet/dotnet-kafka-client-producer.html
        // https://docs.confluent.io/platform/current/clients/confluent-kafka-dotnet/_site/api/Confluent.Kafka.IConsumer-2.html

        _highestOffsetsAtStartupTime = [];
        _lastConsumedOffsets = [];

        _logger.LogDebug($"{nameof(KeyValueStateInDictService)} initialized");
    }

    public bool Store(byte[] key, byte[] value, string correlationId)
    {
        _logger.LogDebug($"Storing key {key}");
        var cid = new CorrelationId { Value = correlationId };
        var keyHash = key.GetHashString();
        if(_keyValueState.TryGetValue(keyHash, out List<KeyValue>? pairsSharingHash))
        {
            for (int i = 0; i < pairsSharingHash.Count; i++)
            {
                if(pairsSharingHash[i].Key.SequenceEqual(key))
                {
                    pairsSharingHash[i] =  new KeyValue { Key = key, Value = value, CorrelationId = cid };
                    _keyValueState[keyHash] = pairsSharingHash;
                    return true;
                }
            }
            pairsSharingHash.Add(new KeyValue { Key = key, Value = value, CorrelationId = cid });
            _keyValueState[keyHash] = pairsSharingHash;
            return true;
        }
        else
        {
            _keyValueState.Add(keyHash, [new() { Key = key, Value = value, CorrelationId = cid }]);
            return true;
        }
    }

    public bool TryRetrieve(byte[] keyRaw, out (byte[] Value, string CorrelationId) result)
    {
        var keyHash = keyRaw.GetHashString();
        if(_keyValueState.TryGetValue(keyHash, out List<KeyValue>? pairsSharingHash))
        {
            for (int i = 0; i < pairsSharingHash.Count; i++)
            {
                if(pairsSharingHash[i].Key.SequenceEqual(keyRaw))
                {
                    result = (Value: pairsSharingHash[i].Value, CorrelationId: pairsSharingHash[i].CorrelationId.Value);
                    return true;
                }
            }
        }
        result = (Value: [], CorrelationId: string.Empty);
        return false;
    }

    public bool Remove(byte[] keyRaw, string correlationId)
    {
        var keyHash = keyRaw.GetHashString();
        if(_keyValueState.TryGetValue(keyHash, out List<KeyValue>? pairsSharingHash))
        {
            for (int i = 0; i < pairsSharingHash.Count; i++)
            {
                if(pairsSharingHash[i].Key.SequenceEqual(keyRaw))
                {
                    if(pairsSharingHash.Count == 1)
                    {
                        _keyValueState.Remove(keyHash);
                    }
                    else
                    {
                        _keyValueState[keyHash].RemoveAt(i);
                    }
                    return true;
                }
            }
            // This is an actual hash collision, but the key is not present, so there is nothing to remove, noop
            return true;
        }
        else
        {
            return true;
        }
    }

    public List<KafkaTopicPartitionOffset> GetLastConsumedTopicPartitionOffsets()
    {
        return _lastConsumedOffsets;
    }

    public bool UpdateLastConsumedTopicPartitionOffsets(KafkaTopicPartitionOffset topicPartitionOffset)
    {
        for (int i = 0; i < _lastConsumedOffsets.Count; i++)
        {
            var tpo = _lastConsumedOffsets[i];
            if(tpo.Topic.Value == topicPartitionOffset.Topic.Value && tpo.Partition.Value == topicPartitionOffset.Partition.Value)
            {
                _lastConsumedOffsets.RemoveAt(i);
                break;
            }
        }
        _lastConsumedOffsets.Add(topicPartitionOffset);
        return true;
    }

    public bool SetStartupTimeHightestTopicPartitionOffsets(List<KafkaTopicPartitionOffset> topicPartitionOffsets)
    {
        _highestOffsetsAtStartupTime = topicPartitionOffsets;
        return true;
    }

    public List<KafkaTopicPartitionOffset> GetStartupTimeHightestTopicPartitionOffsets()
    {
        return _highestOffsetsAtStartupTime;
    }

    public bool Ready()
    {
        _logger.LogTrace($"{nameof(KeyValueStateInSQLiteService)} received request to check readiness");
        if(_ready) return true;

        if(_highestOffsetsAtStartupTime.Count == 0) return false;

        if(_highestOffsetsAtStartupTime.All(tpo => tpo.Offset.Value == 0)) return true;

        var latestConsumedOffsets = GetLastConsumedTopicPartitionOffsets();

        if(latestConsumedOffsets.Count == 0) return false; // This case should not happen any more as earliest is set to low watermark before first consume, but leave it in as a safeguard

        foreach(var latestOffset in latestConsumedOffsets)
        {
            var partitionHighWatermarkAtStartupTime = _highestOffsetsAtStartupTime.FirstOrDefault(tpo => tpo.Topic == latestOffset.Topic && tpo.Partition == latestOffset.Partition);
            if(latestOffset.Offset.Value < (partitionHighWatermarkAtStartupTime?.Offset.Value ?? long.MaxValue))
            {
                return false;
            }
        }

        _ready = true;
        return _ready;
    }
}
