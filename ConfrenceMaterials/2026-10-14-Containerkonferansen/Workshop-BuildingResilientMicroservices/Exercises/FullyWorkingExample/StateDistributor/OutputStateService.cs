using Confluent.Kafka;
using Microsoft.Data.Sqlite;

public class OutputStateService
{
    private readonly ILogger<OutputStateService> _logger;
    private readonly SqliteConnection _sqliteDb;

    private List<TopicPartitionOffset> _highestOffsetsAtStartupTime;
    private bool _ready;

    public OutputStateService(ILogger<OutputStateService> logger)
    {
        _logger = logger;
        _sqliteDb = new SqliteConnection(GetSqliteConnectionString());
        _logger.LogTrace($"Connection to db using connection string \"{GetSqliteConnectionString()}\" set up");
        _sqliteDb.Open();
        InitializeDb();
        _highestOffsetsAtStartupTime = [];
        _ready = false;

        _logger.LogDebug($"{nameof(OutputStateService)} initialized");
    }

    public bool Remove(string key, string correlationId)
    {
        var storageFormattedKey = key.GetUtf8Bytes();
        var command = _sqliteDb.CreateCommand();
        command.CommandText =
        @"
            DELETE FROM stateStore
            WHERE stateKey = $k;
        ";
        command.Parameters.AddWithValue("$k", storageFormattedKey);
        var rowsAffected = command.ExecuteNonQuery();
        return rowsAffected == 1;
    }

    public bool Store(string key, Person value, string correlationId)
    {
        var storageFormattedKey = key.GetUtf8Bytes();
        var storageFormattedValue = value.ToPersonJson().SerializeExplicitlyForAot().GetUtf8Bytes();
        var command = _sqliteDb.CreateCommand();
        command.CommandText =
        @"
            INSERT INTO stateStore(stateKey, stateValue, correlationId)
            VALUES ($k, $v, $c)
            ON CONFLICT (stateKey) DO UPDATE SET stateValue=excluded.stateValue;
        ";
        command.Parameters.AddWithValue("$k", storageFormattedKey);
        command.Parameters.AddWithValue("$v", storageFormattedValue);
        command.Parameters.AddWithValue("$c", correlationId);
        var rowsAffected = command.ExecuteNonQuery();

        return rowsAffected == 1;
    }

    public bool TryRetrieve(string key, out (string Value, string CorrelationId) result)
    {
        var storageFormattedKey = key.GetUtf8Bytes();
        var command = _sqliteDb.CreateCommand();
        command.CommandText =
        @"
            SELECT stateValue, correlationId
            FROM stateStore
            WHERE stateKey = $k
        ";
        command.Parameters.AddWithValue("$k", storageFormattedKey);
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

                var valueAsString = valueConverted.GetUtf8String();

                result = (Value: valueAsString, CorrelationId: correlationId);
                return true;
            }
        }
        result = (Value: string.Empty, CorrelationId: string.Empty);
        return false;
    }

    public List<TopicPartitionOffset> GetLastConsumedTopicPartitionOffsets()
    {
        List<TopicPartitionOffset> topicPartitionOffsets = [];

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

                topicPartitionOffsets.Add(new TopicPartitionOffset(topic, partition, offset));
            }
        }
        return topicPartitionOffsets;
    }

    public bool UpdateLastConsumedTopicPartitionOffsets(TopicPartitionOffset topicPartitionOffset)
    {
        var command = _sqliteDb.CreateCommand();
        command.CommandText =
        @"
            INSERT INTO topicPartitionOffsets(topic, partition, offset)
            VALUES ($t, $p, $o)
            ON CONFLICT (topic, partition) DO UPDATE SET offset=excluded.offset;
        ";
        command.Parameters.AddWithValue("$t", topicPartitionOffset.Topic);
        command.Parameters.AddWithValue("$p", topicPartitionOffset.Partition.Value);
        command.Parameters.AddWithValue("$o", topicPartitionOffset.Offset.Value);
        var rowsAffected = command.ExecuteNonQuery();

        return rowsAffected == 1;
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
        _logger.LogTrace($"{nameof(OutputStateService)} received request to check readiness");
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
        // return GetSqliteConnectionStringInMem();
        return GetSqliteConnectionStringFileBacked();
    }

    private string GetSqliteConnectionStringFileBacked()
    {
        return new SqliteConnectionStringBuilder()
        {
            DataSource = new FileInfo("/ContainerData/StateDistributor.sqlite").FullName,
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
            CREATE TABLE IF NOT EXISTS stateStore (
                stateKey BLOB NOT NULL PRIMARY KEY,
                stateValue BLOB NOT NULL,
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
