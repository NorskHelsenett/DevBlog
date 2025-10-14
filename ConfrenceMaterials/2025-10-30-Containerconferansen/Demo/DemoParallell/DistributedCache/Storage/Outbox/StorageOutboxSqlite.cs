using Microsoft.Data.Sqlite;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Confluent.Kafka;
using DataTypes;

public class StorageOutboxSqlite : IStorageOutbox
{
    private readonly ILogger<StorageOutboxSqlite> _logger;
    private readonly ActivitySource _activitySource;
    private readonly Counter<long> _deleteNextRequestedCounter;
    private readonly Counter<long> _deleteNextFailedCounter;
    private readonly Counter<long> _enqueueRequestedCounter;
    private readonly Counter<long> _enqueueFailedCounter;
    private readonly Counter<long> _retrieveRequestedCounter;
    private readonly Counter<long> _markNextFailedCounter;

    private readonly SqliteConnection _sqliteDb;

    public StorageOutboxSqlite(ILogger<StorageOutboxSqlite> logger, ActivitySource activitySource, Meter meter)
    {
        _logger = logger;
        _activitySource = activitySource;
        _deleteNextRequestedCounter = meter.CreateCounter<long>("storage.outbox.sqlite.deleteNext.requested", description: "Number of requests for removing an entry from the outbox storage");
        _deleteNextFailedCounter = meter.CreateCounter<long>("storage.outbox.sqlite.deleteNext.failed", description: "Number of requests for removing an entry from the outbox storage that have failed");
        _enqueueRequestedCounter = meter.CreateCounter<long>("storage.outbox.sqlite.enqueue.requested", description: "Number of requests for storing an entry in the outbox storage");
        _enqueueFailedCounter = meter.CreateCounter<long>("storage.outbox.sqlite.enqueue.failed", description: "Number of requests for storing an entry in the outbox storage that have failed");
        _retrieveRequestedCounter = meter.CreateCounter<long>("storage.outbox.sqlite.retrieve.requested", description: "Number of requests for retrieving an entry from the outbox storage");
        _markNextFailedCounter = meter.CreateCounter<long>("storage.outbox.sqlite.markNextFailed", description: "Number of times items in outbox have been marked as failed");

        _sqliteDb = new SqliteConnection(GetSqliteConnectionString());
        _logger.LogTrace($"Connection to db using connection string \"{GetSqliteConnectionString()}\" set up");
        _sqliteDb.Open();
        InitializeDb();

        _logger.LogDebug($"{nameof(StorageOutboxSqlite)} initialized");
    }

    public DataTypes.Error? DeleteNext()
    {
        using var activity = _activitySource.StartActivity("storage.outbox.sqlite.remove");
        _deleteNextRequestedCounter.Add(1);
        var command = _sqliteDb.CreateCommand();
        command.CommandText =
        @"
            DELETE FROM outboxKeyValueStore
            WHERE rowid IN (SELECT rowid FROM outboxKeyValueStore ORDER BY rowid LIMIT 1);
        ";
        activity?.AddEvent(new ActivityEvent("Command created", DateTimeOffset.UtcNow));
        var rowsAffected = command.ExecuteNonQuery();
        activity?.AddEvent(new ActivityEvent("Command executed", DateTimeOffset.UtcNow));
        if (rowsAffected != 1)
        {
            _deleteNextFailedCounter.Add(1);
            return new DataTypes.Error { Message = $"Deleting next caused {rowsAffected} rows to be affected, but expected it to be only 1" };
        }
        return null;
    }

    public DataTypes.Error? Enqueue(DcItem item)
    {
        using var activity = _activitySource.StartActivity("storage.outbox.sqlite.enqueue");
        string? serializedHeaders = null;
        if (item.Headers != null)
        {
            serializedHeaders = System.Text.Json.JsonSerializer.Serialize(item.Headers);
        }
        activity?.AddEvent(new ActivityEvent("Headers packaged for storage", DateTimeOffset.UtcNow));
        var command = _sqliteDb.CreateCommand();
        command.CommandText =
        @"
            INSERT INTO outboxKeyValueStore(kvKey, kvValue, kvHeaders, timestamp)
            VALUES ($k, $v, $h, $t);
        ";
        command.Parameters.AddWithValue("$k", item.Key);
        command.Parameters.AddWithValue("$v", item.Value);
        command.Parameters.AddWithValue("$h", serializedHeaders);
        command.Parameters.AddWithValue("$t", DateTimeOffset.UtcNow);
        activity?.AddEvent(new ActivityEvent("Command created", DateTimeOffset.UtcNow));
        var rowsAffected = command.ExecuteNonQuery();
        activity?.AddEvent(new ActivityEvent("Command executed", DateTimeOffset.UtcNow));

        if (rowsAffected != 1)
        {
            return new DataTypes.Error { Message = $"Storing next item in outbox with key {item.Key} caused {rowsAffected} rows to be affected, but expected it to be only 1" };
        }

        return null;
    }

    public (DataTypes.Error? Error, DcItem? NextItem) RetrieveNext()
    {
        using var activity = _activitySource.StartActivity("storage.outbox.sqlite.retrieve");
        _retrieveRequestedCounter.Add(1);
        var command = _sqliteDb.CreateCommand();
        command.CommandText =
        @"
            SELECT kvKey, kvValue, kvHeaders
            FROM outboxKeyValueStore
            WHERE rowid IN (SELECT rowid FROM outboxKeyValueStore ORDER BY rowid LIMIT 1);
        ";
        activity?.AddEvent(new ActivityEvent("Command created", DateTimeOffset.UtcNow));
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                activity?.AddEvent(new ActivityEvent("Row retrieved", DateTimeOffset.UtcNow));
                var key = reader.GetString(0);
                var valueRaw = reader.GetStream(1);
                var headersSerialized = reader.GetString(2);

                byte[] valueConverted = [];
                if (valueRaw is MemoryStream stream)
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
                var headers = System.Text.Json.JsonSerializer.Deserialize<List<KeyValuePair<string, string>>>(headersSerialized);
                activity?.AddEvent(new ActivityEvent("Done unpackaging headers", DateTimeOffset.UtcNow));

                return (Error: null, NextItem: new DcItem { Key = key, Value = valueConverted, Headers = headers });
            }
        }
        return (Error: null, NextItem: null);
    }

    public DataTypes.Error? MarkNextFailed()
    {
        using var activity = _activitySource.StartActivity("storage.outbox.sqlite.markNextFailed");
        var next = RetrieveNext();
        if (next.Error != null)
        {
            return new DataTypes.Error { Message = $"Failed to retrieve next when working on marking it as failed, inner error: {next.Error.Message}" };
        }
        if (next.NextItem == null)
        {
            return new DataTypes.Error { Message = "Failed to mark next as failed: It doesn't seem to exist" };
        }

        var command = _sqliteDb.CreateCommand();
        command.CommandText =
        @"
            INSERT INTO outboxKeyValueStoreFailed(kvKey, kvValue, kvHeaders, timestamp)
            VALUES ($k, $v, $h, $t);
        ";
        command.Parameters.AddWithValue("$k", next.NextItem.Key);
        command.Parameters.AddWithValue("$v", next.NextItem.Value);
        command.Parameters.AddWithValue("$h", next.NextItem.Headers);
        command.Parameters.AddWithValue("$t", DateTimeOffset.UtcNow);
        activity?.AddEvent(new ActivityEvent("Command created", DateTimeOffset.UtcNow));
        var rowsAffected = command.ExecuteNonQuery();
        activity?.AddEvent(new ActivityEvent("Command executed", DateTimeOffset.UtcNow));

        if (rowsAffected != 1)
        {
            // _storeFailedCounter.Add(1);
            // This can only be warning if greater than 0, if item successfully stored in failed we still need to remove from regular queue
            // return new Error { Message = $"Marking next item in outbox with key {key} as failed caused {rowsAffected} rows to be affected, but expected it to be only 1" };
        }
        var deleteNextError = DeleteNext();
        if (deleteNextError != null)
        {
            return new DataTypes.Error { Message = $"Failed to delete next from main queue when working on marking it as failed, inner error: {deleteNextError.Message}" };
        }
        return null;
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
            configuredLocationPath = "/ContainerData/DistributedCache/outbox.sqlite";
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
            DataSource = "KeyValueStateInSQLiteOutboxMemDb",
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
            CREATE TABLE IF NOT EXISTS outboxKeyValueStore (
                kvKey TEXT NOT NULL,
                kvValue BLOB,
                kvHeaders TEXT,
                timestamp TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS outboxKeyValueStoreFailed (
                kvKey TEXT NOT NULL,
                kvValue BLOB,
                kvHeaders TEXT,
                timestamp TEXT NOT NULL
            );
        ";
        command.ExecuteNonQuery();
    }
}
