using Microsoft.Data.Sqlite;

public class KeyValueStateInSQLiteService : IKeyValueStateService
{
    private readonly ILogger<KeyValueStateInSQLiteService> _logger;
    private readonly SqliteConnection _sqliteDb;

    private List<KafkaTopicPartitionOffset> _highestOffsetsAtStartupTime;
    private bool _ready;

    public KeyValueStateInSQLiteService(ILogger<KeyValueStateInSQLiteService> logger)
    {
        _logger = logger;
        _sqliteDb = new SqliteConnection(GetSqliteConnectionString());
        _logger.LogTrace($"Connection to db using connection string \"{GetSqliteConnectionString()}\" set up");
        _sqliteDb.Open();
        InitializeDb();
        _highestOffsetsAtStartupTime = [];
        _ready = false;

        _logger.LogDebug($"{nameof(KeyValueStateInSQLiteService)} initialized");
    }

    public bool Remove(byte[] key, string correlationId)
    {
        var command = _sqliteDb.CreateCommand();
        command.CommandText =
        @"
            DELETE FROM keyValueStore
            WHERE kvKey = $k;
        ";
        command.Parameters.AddWithValue("$k", key);
        var rowsAffected = command.ExecuteNonQuery();
        return rowsAffected == 1;
    }

    public bool Store(byte[] key, byte[] value, string correlationId)
    {
        var command = _sqliteDb.CreateCommand();
        command.CommandText =
        @"
            INSERT INTO keyValueStore(kvKey, kvValue, correlationId)
            VALUES ($k, $v, $c)
            ON CONFLICT (kvKey) DO UPDATE SET kvValue=excluded.kvValue;
        ";
        command.Parameters.AddWithValue("$k", key);
        command.Parameters.AddWithValue("$v", value);
        command.Parameters.AddWithValue("$c", correlationId);
        var rowsAffected = command.ExecuteNonQuery();

        return rowsAffected == 1;
    }

    public bool TryRetrieve(byte[] key, out (byte[] Value, string CorrelationId) result)
    {
        var command = _sqliteDb.CreateCommand();
        command.CommandText =
        @"
            SELECT kvValue, correlationId
            FROM keyValueStore
            WHERE kvKey = $k
        ";
        command.Parameters.AddWithValue("$k", key);
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                var valueRaw = reader.GetStream(0);
                var correlationId = reader.GetString(1);

                byte[] valueConverted = [];
                if(valueRaw is MemoryStream stream)
                {
                    valueConverted = stream.ToArray();
                }
                else
                {
                    using MemoryStream ms = new();
                    valueRaw.CopyTo(ms);
                    valueConverted = ms.ToArray();
                }

                result = (Value: valueConverted, CorrelationId: correlationId);
                return true;
            }
        }
        result = (Value: [], CorrelationId: string.Empty);
        return false;
    }

    public List<KafkaTopicPartitionOffset> GetLastConsumedTopicPartitionOffsets()
    {
        List<KafkaTopicPartitionOffset> topicPartitionOffsets = [];

        var command = _sqliteDb.CreateCommand();
        command.CommandText =
        @"
            SELECT topic, partition, offset
            FROM topicPartitionOffsets
        ";
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                var topic = reader.GetString(0);
                var partition = reader.GetInt32(1);
                var offset = reader.GetInt64(2);

                topicPartitionOffsets.Add(new KafkaTopicPartitionOffset
                    {
                        Topic = new KafkaTopic { Value = topic },
                        Partition = new KafkaPartition { Value = partition },
                        Offset = new KafkaOffset{ Value = offset }
                    });
            }
        }
        return topicPartitionOffsets;
    }

    public bool UpdateLastConsumedTopicPartitionOffsets(KafkaTopicPartitionOffset topicPartitionOffset)
    {
        var command = _sqliteDb.CreateCommand();
        command.CommandText =
        @"
            INSERT INTO topicPartitionOffsets(topic, partition, offset)
            VALUES ($t, $p, $o)
            ON CONFLICT (topic, partition) DO UPDATE SET offset=excluded.offset;
        ";
        command.Parameters.AddWithValue("$t", topicPartitionOffset.Topic.Value);
        command.Parameters.AddWithValue("$p", topicPartitionOffset.Partition.Value);
        command.Parameters.AddWithValue("$o", topicPartitionOffset.Offset.Value);
        var rowsAffected = command.ExecuteNonQuery();

        return rowsAffected == 1;
    }

    // public List<KafkaTopicPartitionOffset> GetStartupTimeTopicPartitionOffsets()
    // {
    //     List<KafkaTopicPartitionOffset> topicPartitionOffsets = [];

    //     var command = _sqliteDb.CreateCommand();
    //     command.CommandText =
    //     @"
    //         SELECT topic, partition, offset
    //         FROM topicPartitionOffsetsHighAtStartup
    //     ";
    //     using (var reader = command.ExecuteReader())
    //     {
    //         while (reader.Read())
    //         {
    //             var topic = reader.GetString(0);
    //             var partition = reader.GetInt32(1);
    //             var offset = reader.GetInt64(2);

    //             topicPartitionOffsets.Add(new KafkaTopicPartitionOffset
    //                 {
    //                     Topic = new KafkaTopic { Value = topic },
    //                     Partition = new KafkaPartition { Value = partition },
    //                     Offset = new KafkaOffset{ Value = offset }
    //                 });
    //         }
    //     }
    //     return topicPartitionOffsets;
    // }

    public bool SetStartupTimeHightestTopicPartitionOffsets(List<KafkaTopicPartitionOffset> topicPartitionOffsets)
    {
        _highestOffsetsAtStartupTime = topicPartitionOffsets;
        return true;
        // var rowsAffected = 0;
        // foreach(var tpo in topicPartitionOffsets)
        // {
        //     var command = _sqliteDb.CreateCommand();
        //     command.CommandText =
        //     @"
        //         INSERT INTO topicPartitionOffsetsHighAtStartup(topic, partition, offset)
        //         VALUES ($t, $p, $o)
        //         ON CONFLICT (topic, partition) DO UPDATE SET offset=excluded.offset;
        //     ";
        //     command.Parameters.AddWithValue("$t", tpo.Topic.Value);
        //     command.Parameters.AddWithValue("$p", tpo.Partition.Value);
        //     command.Parameters.AddWithValue("$o", tpo.Offset.Value);
        //     rowsAffected += command.ExecuteNonQuery();

        // }
        // return rowsAffected == topicPartitionOffsets.Count;
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

    private string GetSqliteConnectionString()
    {
        var configuredLocation = Environment.GetEnvironmentVariable(KV_API_STATE_STORAGE_DISK_LOCATION);
        if(string.IsNullOrWhiteSpace(configuredLocation))
        {
            if(string.IsNullOrEmpty(configuredLocation))
            {
                _logger.LogDebug($"Failed to read content of environment variable \"{KV_API_STATE_STORAGE_DISK_LOCATION}\", got null/empty string");
            }
            else
            {
                _logger.LogWarning($"Failed to read proper content of environment variable \"{KV_API_STATE_STORAGE_DISK_LOCATION}\", contained only whitespaces");
            }
            _logger.LogDebug($"Because {KV_API_STATE_STORAGE_DISK_LOCATION} was not set, setting up in memory sqlite");
            var connectionStringBuilder = new SqliteConnectionStringBuilder
            {
                DataSource = "KeyValueStateInSQLiteMemDb",
                Mode = SqliteOpenMode.Memory,
                Cache = SqliteCacheMode.Shared
            };
            var connectionString = connectionStringBuilder.ToString();
            return connectionString;
        }

        configuredLocation = configuredLocation.Trim();

        if(!Path.Exists(configuredLocation))
        {
            if(Path.EndsInDirectorySeparator(configuredLocation))
            {
                // For future me: Directory.CreateDirectory() handles the case where it already exists.
                // So same as with mkdir -p, call it just in case instead of bloating path with if()'s.
                Directory.CreateDirectory(configuredLocation); // Create if not exists
                configuredLocation = $"{configuredLocation}/kvApi.sqlite";
            }
            else
            {
                new FileInfo(configuredLocation).Directory?.Create();
            }
        }
        else
        {
            if(Directory.Exists(configuredLocation))
            {
                configuredLocation = $"{configuredLocation}/kvApi.sqlite";
            }
        }

        var location = new FileInfo(configuredLocation);

        return new SqliteConnectionStringBuilder()
        {
            DataSource = location.FullName,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();
    }

    private void InitializeDb()
    {
        var command = _sqliteDb.CreateCommand();
        command.CommandText =
        @"
            CREATE TABLE IF NOT EXISTS keyValueStore (
                kvKey BLOB NOT NULL PRIMARY KEY,
                kvValue BLOB NOT NULL,
                correlationId TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS topicPartitionOffsets (
                topic TEXT NOT NULL,
                partition INTEGER NOT NULL,
                offset INTEGER NOT NULL,
                PRIMARY KEY(topic, partition)
            );
        ";
        command.ExecuteNonQuery();
    }
}
