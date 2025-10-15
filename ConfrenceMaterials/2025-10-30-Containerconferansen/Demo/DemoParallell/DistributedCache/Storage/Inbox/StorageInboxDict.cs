using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Confluent.Kafka;
using Error = DataTypes.Error;

namespace DistributedCache.Storage.Inbox;

public class StorageInboxDict: IStorageInbox
{
    private readonly ILogger<StorageInboxSqlite> _logger;
    private readonly ActivitySource _activitySource;
    private readonly Counter<long> _removeRequestedCounter;
    private readonly Counter<long> _removeFailedCounter;
    private readonly Counter<long> _storeRequestedCounter;
    private readonly Counter<long> _storeFailedCounter;
    private readonly Counter<long> _retrieveRequestedCounter;
    private readonly Counter<long> _retrieveNotFoundCounter;

    private readonly ConcurrentDictionary<string, DcItem> _inboxDict;

    private readonly ConcurrentDictionary<string, ConcurrentDictionary<int, TopicPartitionOffset>> _highestOffsetsAtStartupTime;
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<int, TopicPartitionOffset>> _lastConsumedTopicPartitionOffsets;

    private bool _ready;

    public StorageInboxDict(ILogger<StorageInboxSqlite> logger, ActivitySource activitySource, Meter meter)
    {
        _logger = logger;
        _activitySource = activitySource;
        _removeRequestedCounter = meter.CreateCounter<long>("storage.inbox.dict.remove.requested", description: "Number of requests for removing an entry from the inbox storage");
        _removeFailedCounter = meter.CreateCounter<long>("storage.inbox.dict.remove.failed", description: "Number of requests for removing an entry from the inbox storage that have failed");
        _storeRequestedCounter = meter.CreateCounter<long>("storage.inbox.dict.store.requested", description: "Number of requests for storing an entry in the inbox storage");
        _storeFailedCounter = meter.CreateCounter<long>("storage.inbox.dict.store.failed", description: "Number of requests for storing an entry in the inbox storage that have failed");
        _retrieveRequestedCounter = meter.CreateCounter<long>("storage.inbox.dict.retrieve.requested", description: "Number of requests for retrieving an entry from the inbox storage");
        _retrieveNotFoundCounter = meter.CreateCounter<long>("storage.inbox.dict.retrieve.notFound", description: "Number of requests for retrieving an entry from the inbox storage that is not present");

        _inboxDict = new ConcurrentDictionary<string, DcItem>();

        _highestOffsetsAtStartupTime = new ConcurrentDictionary<string, ConcurrentDictionary<int, TopicPartitionOffset>>();
        _lastConsumedTopicPartitionOffsets = new ConcurrentDictionary<string, ConcurrentDictionary<int, TopicPartitionOffset>>();
        _ready = false;

        _logger.LogDebug($"{nameof(StorageInboxSqlite)} initialized");
    }
    public Error? Store(DcItem item)
    {
        _storeRequestedCounter.Add(1);
        _inboxDict[item.Key] = item;
        return null;
    }

    public (Error? Error, DcItem? RetrievedItem) Retrieve(string key, CancellationToken cancellationToken)
    {
        _retrieveRequestedCounter.Add(1);
        var retrieveSuccess = _inboxDict.TryGetValue(key, out var retrieved);
        if (retrieveSuccess)
        {
            return (Error: null, RetrievedItem: retrieved);
        }
        _retrieveNotFoundCounter.Add(1);

        return (Error: null, RetrievedItem: null);
    }

    public Error? Remove(string key)
    {
        _removeRequestedCounter.Add(1);
        var removeSuccess = _inboxDict.TryRemove(key, out var removedItem);
        if (!removeSuccess)
        {
            _logger.LogWarning("Got request to remove item that wasn't present to begin with, by key {key}", key);
            _removeFailedCounter.Add(1);
        }
        return null;
    }

    public List<TopicPartitionOffset> GetLastConsumedTopicPartitionOffsets()
    {
        return _lastConsumedTopicPartitionOffsets.Values.SelectMany(tp => tp.Values).ToList();
    }

    public Error? UpdateLastConsumedTopicPartitionOffsets(TopicPartitionOffset topicPartitionOffset)
    {
        var topicRegistered = _lastConsumedTopicPartitionOffsets.ContainsKey(topicPartitionOffset.Topic);
        if (!topicRegistered)
        {
            _lastConsumedTopicPartitionOffsets[topicPartitionOffset.Topic] = new ConcurrentDictionary<int, TopicPartitionOffset>();
        }

        _lastConsumedTopicPartitionOffsets[topicPartitionOffset.Topic][topicPartitionOffset.Partition.Value] = topicPartitionOffset;
        return null;
    }

    public bool Ready()
    {
        _logger.LogTrace($"{nameof(StorageInboxDict)} received request to check readiness");
        if(_ready) return true;

        if (_highestOffsetsAtStartupTime.IsEmpty) return false;

        if (_highestOffsetsAtStartupTime.Values.All(tp => tp.Values.All(tpo => tpo.Offset == 0))) return true;

        if (_lastConsumedTopicPartitionOffsets.IsEmpty) return false;

        foreach (var currentTopic in _lastConsumedTopicPartitionOffsets.Keys)
        {
            if (!_highestOffsetsAtStartupTime.TryGetValue(currentTopic, out ConcurrentDictionary<int, TopicPartitionOffset>? startuptimePartitionsTpos))
            {
                throw new Exception($"Startup time tpos overview didn't contain entries for topic {currentTopic} while current did, surely this is impossible");
            }
            foreach (var currentPartition in _lastConsumedTopicPartitionOffsets[currentTopic].Keys)
            {
                if (!startuptimePartitionsTpos.TryGetValue(currentPartition, out TopicPartitionOffset? startuptimeTpo))
                {
                    throw new Exception($"Startup time tpos overview for topic {currentTopic} didn't contain entries for partition {currentPartition} while current did, surely this is impossible");
                }
                var currentOffset = _lastConsumedTopicPartitionOffsets[currentTopic][currentPartition].Offset.Value;
                var startupOffset = startuptimeTpo.Offset.Value;
                if (currentOffset < startupOffset)
                {
                    return false;
                }
            }
        }
        _ready = true;
        return _ready;
    }

    public List<TopicPartitionOffset> GetStartupTimeHightestTopicPartitionOffsets()
    {
        return _highestOffsetsAtStartupTime.Values.SelectMany(tp => tp.Values).ToList();
    }

    public Error? SetStartupTimeHightestTopicPartitionOffsets(List<TopicPartitionOffset> topicPartitionOffsets)
    {
        foreach (var topicPartitionOffset in topicPartitionOffsets)
        {
            var topicRegistered = _highestOffsetsAtStartupTime.ContainsKey(topicPartitionOffset.Topic);
            if (!topicRegistered)
            {
                _highestOffsetsAtStartupTime[topicPartitionOffset.Topic] = new ConcurrentDictionary<int, TopicPartitionOffset>();
            }

            _highestOffsetsAtStartupTime[topicPartitionOffset.Topic][topicPartitionOffset.Partition.Value] = topicPartitionOffset;

        }

        return null;
    }
}
