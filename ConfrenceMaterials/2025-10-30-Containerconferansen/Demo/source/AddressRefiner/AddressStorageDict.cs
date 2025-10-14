using No.Nhn.Address.Cadastre.Road;

namespace AddressRefiner;

using Confluent.Kafka;
public class AddressStorageDict : IAddressStorage
{
    private readonly ILogger<AddressStorageDict> _logger;
    private Dictionary<string, CadastreRoadAddress> _addresses;

    private List<TopicPartitionOffset> _highestOffsetsAtStartupTime;
    private List<TopicPartitionOffset> _lastConsumedTopicPartitionOffsets;
    private bool _ready;

    public AddressStorageDict(ILogger<AddressStorageDict> logger)
    {
        _logger = logger;
        _addresses = new Dictionary<string, CadastreRoadAddress>();
        _lastConsumedTopicPartitionOffsets = [];
        _highestOffsetsAtStartupTime = [];
        _ready = false;

        _logger.LogDebug($"{nameof(AddressStorageDict)} initialized");
    }

    public bool Remove(string key, string correlationId)
    {
        _addresses.Remove(key);
        return true;
    }

    public bool Store(CadastreRoadAddress cadastreRoadAddress)
    {
        return _addresses.TryAdd(cadastreRoadAddress.AddressId, cadastreRoadAddress);
    }

    public bool TryRetrieve(string addressId, out CadastreRoadAddress result)
    {
        var retrievalStatus = _addresses.TryGetValue(addressId, out var retrieved);
        retrieved ??= new CadastreRoadAddress();
        result = retrieved;
        return retrievalStatus;
    }

    public List<TopicPartitionOffset> GetLastConsumedTopicPartitionOffsets()
    {
        return _lastConsumedTopicPartitionOffsets;
    }

    public bool UpdateLastConsumedTopicPartitionOffsets(TopicPartitionOffset topicPartitionOffset)
    {
        for (int i = 0; i < _lastConsumedTopicPartitionOffsets.Count; i++)
        {
            var tpo = _lastConsumedTopicPartitionOffsets[i];
            if(tpo.Topic == topicPartitionOffset.Topic && tpo.Partition.Value == topicPartitionOffset.Partition.Value)
            {
                _lastConsumedTopicPartitionOffsets.RemoveAt(i);
                break;
            }
        }
        _lastConsumedTopicPartitionOffsets.Add(topicPartitionOffset);
        return true;
    }

    public bool SetStartupTimeHightestTopicPartitionOffsets(List<TopicPartitionOffset> topicPartitionOffsets)
    {
        _highestOffsetsAtStartupTime = topicPartitionOffsets;
        return true;
    }

    public List<TopicPartitionOffset> GetStartupTimeHightestTopicPartitionOffsets()
    {
        return _highestOffsetsAtStartupTime;
    }

    public bool Ready()
    {
        _logger.LogTrace($"{nameof(AddressStorageDict)} received request to check readiness");
        if(_ready) return true;

        if(_highestOffsetsAtStartupTime.Count == 0) return false;

        if(_highestOffsetsAtStartupTime.All(tpo => tpo.Offset.Value == 0)) return true;

        var latestConsumedOffsets = GetLastConsumedTopicPartitionOffsets();

        if(latestConsumedOffsets.Count == 0) return false; // This case should not happen when earliest is set to low watermark before first consume, but leave it in as a safeguard

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
