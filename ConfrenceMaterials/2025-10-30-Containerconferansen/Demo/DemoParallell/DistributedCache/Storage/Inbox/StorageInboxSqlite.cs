using Microsoft.Data.Sqlite;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Confluent.Kafka;
using DataTypes;

public class StorageInboxSqlite : IStorageInbox
{
    private readonly ILogger<StorageInboxSqlite> _logger;
    private readonly ActivitySource _activitySource;
    private readonly Counter<long> _removeRequestedCounter;
    private readonly Counter<long> _removeFailedCounter;
    private readonly Counter<long> _storeRequestedCounter;
    private readonly Counter<long> _storeFailedCounter;
    private readonly Counter<long> _retrieveRequestedCounter;
    private readonly Counter<long> _retrieveNotFoundCounter;

    private readonly SqliteConnection _sqliteDb;

    private List<TopicPartitionOffset> _highestOffsetsAtStartupTime;
    private bool _ready;

    public StorageInboxSqlite(ILogger<StorageInboxSqlite> logger, ActivitySource activitySource, Meter meter)
    {
        _logger = logger;
        _activitySource = activitySource;
        _removeRequestedCounter = meter.CreateCounter<long>("storage.inbox.sqlite.remove.requested", description: "Number of requests for removing an entry from the inbox storage");
        _removeFailedCounter = meter.CreateCounter<long>("storage.inbox.sqlite.remove.failed", description: "Number of requests for removing an entry from the inbox storage that have failed");
        _storeRequestedCounter = meter.CreateCounter<long>("storage.inbox.sqlite.store.requested", description: "Number of requests for storing an entry in the inbox storage");
        _storeFailedCounter = meter.CreateCounter<long>("storage.inbox.sqlite.store.failed", description: "Number of requests for storing an entry in the inbox storage that have failed");
        _retrieveRequestedCounter = meter.CreateCounter<long>("storage.inbox.sqlite.retrieve.requested", description: "Number of requests for retrieving an entry from the inbox storage");
        _retrieveNotFoundCounter = meter.CreateCounter<long>("storage.inbox.sqlite.retrieve.notFound", description: "Number of requests for retrieving an entry from the inbox storage that is not present");

        _sqliteDb = new SqliteConnection(GetSqliteConnectionString());
        _logger.LogTrace($"Connection to db using connection string \"{GetSqliteConnectionString()}\" set up");
        _sqliteDb.Open();
        InitializeDb();
        _highestOffsetsAtStartupTime = [];
        _ready = false;

        _logger.LogDebug($"{nameof(StorageInboxSqlite)} initialized");
    }

    public DataTypes.Error? Remove(string key)
    {
        using var activity = _activitySource.StartActivity("storage.inbox.sqlite.remove");
        _removeRequestedCounter.Add(1);
        var command = _sqliteDb.CreateCommand();
        command.CommandText =
        @"
            DELETE FROM keyValueStore
            WHERE kvKey = $k;
        ";
        command.Parameters.AddWithValue("$k", key);
        activity?.AddEvent(new ActivityEvent("Command created", DateTimeOffset.UtcNow));
        var rowsAffected = command.ExecuteNonQuery();
        activity?.AddEvent(new ActivityEvent("Command executed", DateTimeOffset.UtcNow));
        if (rowsAffected != 1)
        {
            _removeFailedCounter.Add(1);
            return new DataTypes.Error { Message = $"Deleting key {key} caused {rowsAffected} rows to be affected, but expected it to be only 1" };
        }
        return null;
    }

    public DataTypes.Error? Store(DcItem item)
    {
        using var activity = _activitySource.StartActivity("storage.inbox.sqlite.store");
        _storeRequestedCounter.Add(1);
        var serializedHeaders = item.Headers == null ? null : System.Text.Json.JsonSerializer.Serialize(item.Headers);
        activity?.AddEvent(new ActivityEvent("Headers packaged for storage", DateTimeOffset.UtcNow));
        var command = _sqliteDb.CreateCommand();
        command.CommandText =
        @"
            INSERT INTO keyValueStore(kvKey, kvValue, kvHeaders)
            VALUES ($k, $v, $h)
            ON CONFLICT (kvKey) DO UPDATE SET kvValue=excluded.kvValue, kvHeaders=excluded.kvHeaders;
        ";
        command.Parameters.AddWithValue("$k", item.Key);
        command.Parameters.AddWithValue("$v", item.Value ?? (object) DBNull.Value);
        command.Parameters.AddWithValue("$h", serializedHeaders ?? (object) DBNull.Value);
        activity?.AddEvent(new ActivityEvent("Command created", DateTimeOffset.UtcNow));
        var rowsAffected = command.ExecuteNonQuery();
        activity?.AddEvent(new ActivityEvent("Command executed", DateTimeOffset.UtcNow));

        if (rowsAffected != 1)
        {
            _storeFailedCounter.Add(1);
            return new DataTypes.Error { Message = $"Storing for key {item.Key} caused {rowsAffected} rows to be affected, but expected it to be only 1" };
        }

        return null;
    }

    public (DataTypes.Error? Error, DcItem? RetrievedItem) Retrieve(string key, CancellationToken cancellationToken)
    {
        using var activity = _activitySource.StartActivity("storage.inbox.sqlite.retrieve");
        _retrieveRequestedCounter.Add(1);
        var command = _sqliteDb.CreateCommand();
        command.CommandText =
        @"
            SELECT kvValue, kvHeaders
            FROM keyValueStore
            WHERE kvKey = $k
        ";
        command.Parameters.AddWithValue("$k", key);
        activity?.AddEvent(new ActivityEvent("Command created", DateTimeOffset.UtcNow));
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                activity?.AddEvent(new ActivityEvent("Row retrieved", DateTimeOffset.UtcNow));
                var valueRaw = reader.IsDBNull(0) ? null : reader.GetStream(0);
                var headersSerialized = reader.IsDBNull(1) ? null : reader.GetString(1);

                byte[]? valueConverted;
                if (valueRaw == null)
                {
                    valueConverted = null;
                }
                else if (valueRaw is MemoryStream stream)
                {
                    valueConverted = stream.ToArray();
                }
                else
                {
                    using MemoryStream ms = new();
                    valueRaw.CopyTo(ms);
                    valueConverted = ms.ToArray();
                }
                activity?.AddEvent(new ActivityEvent("Start unpackaging headers", DateTimeOffset.UtcNow));
                var headers = headersSerialized == null ? null : System.Text.Json.JsonSerializer.Deserialize<List<KeyValuePair<string, string>>>(headersSerialized);
                activity?.AddEvent(new ActivityEvent("Done unpackaging headers", DateTimeOffset.UtcNow));

                return (Error: null, RetrievedItem: new DcItem { Key = key, Value = valueConverted, Headers = headers});
            }
        }
        return (Error: null, RetrievedItem: null);
    }

    public List<TopicPartitionOffset> GetLastConsumedTopicPartitionOffsets()
    {
        List<TopicPartitionOffset> topicPartitionOffsets = [];

        var command = _sqliteDb.CreateCommand();
        command.CommandText =
        @"
            SELECT Topic, Partition, Offset
            FROM TopicPartitionOffsets
        ";
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                var topic = reader.GetString(0);
                var partition = reader.GetInt32(1);
                var offset = reader.GetInt64(2);

                topicPartitionOffsets.Add(new TopicPartitionOffset(topic, partition, offset));
            }
        }
        return topicPartitionOffsets;
    }

    public DataTypes.Error? UpdateLastConsumedTopicPartitionOffsets(TopicPartitionOffset topicPartitionOffset)
    {
        var command = _sqliteDb.CreateCommand();
        command.CommandText =
        @"
            INSERT INTO TopicPartitionOffsets(Topic, Partition, Offset)
            VALUES ($t, $p, $o)
            ON CONFLICT (Topic, Partition) DO UPDATE SET Offset=excluded.Offset;
        ";
        command.Parameters.AddWithValue("$t", topicPartitionOffset.Topic);
        command.Parameters.AddWithValue("$p", topicPartitionOffset.Partition.Value);
        command.Parameters.AddWithValue("$o", topicPartitionOffset.Offset.Value);
        var rowsAffected = command.ExecuteNonQuery();

        if (rowsAffected != 1)
        {
            return new DataTypes.Error { Message = $"Updating last consumed topic partition offsets updated {rowsAffected}, expected there to be only 1" };
        }
        return null;
    }

    public DataTypes.Error? SetStartupTimeHightestTopicPartitionOffsets(List<TopicPartitionOffset> topicPartitionOffsets)
    {
        _highestOffsetsAtStartupTime = topicPartitionOffsets;
        return null;
    }

    public List<TopicPartitionOffset> GetStartupTimeHightestTopicPartitionOffsets()
    {
        return _highestOffsetsAtStartupTime;
    }

    public bool Ready()
    {
        _logger.LogTrace($"{nameof(StorageInboxSqlite)} received request to check readiness");
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

    private string GetSqliteConnectionString()
    {
        var locationType = Environment.GetEnvironmentVariable(DISTRIBUTED_CACHE_STORAGE_OUTBOX_SQLITE_MODE);
        switch (locationType)
        {
            case "file":
                return GetSqliteConnectionStringFileBacked();
                // break;
            case "memory":
            default:
                return GetSqliteConnectionStringInMem();
                // break;
        }
    }

    private string GetSqliteConnectionStringFileBacked()
    {
        var configuredLocationPath = Environment.GetEnvironmentVariable(DISTRIBUTED_CACHE_STORAGE_OUTBOX_SQLITE_FILE_LOCATION);
        if (string.IsNullOrEmpty(configuredLocationPath))
        {
            configuredLocationPath = "/ContainerData/DistributedCache/inbox.sqlite";
        }
        return new SqliteConnectionStringBuilder()
        {
            DataSource = new FileInfo(configuredLocationPath).FullName,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();
    }

    private string GetSqliteConnectionStringInMem()
    {
        var connectionStringBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = "KeyValueStateInSQLiteMemDb",
            Mode = SqliteOpenMode.Memory,
            Cache = SqliteCacheMode.Shared
        };
        var connectionString = connectionStringBuilder.ToString();
        return connectionString;
    }

    private void InitializeDb()
    {
        var command = _sqliteDb.CreateCommand();
        command.CommandText =
        @"
            CREATE TABLE IF NOT EXISTS keyValueStore (
                kvKey TEXT NOT NULL PRIMARY KEY,
                kvValue BLOB,
                kvHeaders TEXT
            );

            CREATE TABLE IF NOT EXISTS TopicPartitionOffsets (
                Topic TEXT NOT NULL,
                Partition INTEGER NOT NULL,
                Offset INTEGER NOT NULL,
                PRIMARY KEY(Topic, Partition)
            );
        ";
        command.ExecuteNonQuery();
    }
}
