using Microsoft.Data.Sqlite;

namespace KeyValueApi;

public class OutboxStateInSqLiteService : IOutboxStateService
{
    private readonly ILogger<OutboxStateInSqLiteService> _logger;
    private readonly SqliteConnection _sqliteDb;

    public OutboxStateInSqLiteService(ILogger<OutboxStateInSqLiteService> logger)
    {
        _logger = logger;
        _sqliteDb = new SqliteConnection(GetSqliteConnectionString());
        _logger.LogTrace($"Connection to db using connection string \"{GetSqliteConnectionString()}\" set up");
        _sqliteDb.Open();
        InitializeDb();

        _logger.LogDebug($"{nameof(OutboxStateInSqLiteService)} initialized");
    }

    public bool RemoveNext()
    {
        var command = _sqliteDb.CreateCommand();
        command.CommandText =
        @"
            DELETE FROM keyValueOutboxStore
            WHERE id = (SELECT id FROM keyValueOutboxStore ORDER BY id ASC LIMIT 1);
        ";
        var rowsAffected = command.ExecuteNonQuery();
        return rowsAffected == 1;
    }

    public bool Store(byte[] key, byte[]? value, Dictionary<string,byte[]>? headers, CorrelationId correlationId)
    {
        var command = _sqliteDb.CreateCommand();
        command.CommandText =
        @"
            INSERT INTO keyValueOutboxStore(kvKey, kvValue, correlationId)
            VALUES ($k, $v, $c);
        ";
        command.Parameters.AddWithValue("$k", key);
        // command.Parameters.AddWithValue("$v", value);
        // var vParam = command.Parameters.Add("$v", SqliteType.Blob);
        // vParam.Value = (object?)value ?? DBNull.Value;
        command.Parameters.AddWithValue("$v", (object?)value ?? DBNull.Value);
        command.Parameters.AddWithValue("$c", correlationId.Value);
        var rowsAffected = command.ExecuteNonQuery();

        return rowsAffected == 1;
    }

    public bool TryRetrieveNext(
        out (byte[] Key,
             byte[]? Value,
             Dictionary<string, byte[]>? Headers,
             CorrelationId CorrelationId) result)
    {
        using var cmd = _sqliteDb.CreateCommand();
        cmd.CommandText = @"
            SELECT kvKey, kvValue, correlationId
            FROM keyValueOutboxStore
            ORDER BY id ASC
            LIMIT 1;
        ";

        using var reader = cmd.ExecuteReader();

        if (!reader.Read())
        {
            // No rows at all – return a “not‑found” tuple.
            result = (Key: Array.Empty<byte>(),
                      Value: null,
                      Headers: null,
                      CorrelationId: new CorrelationId { Value = string.Empty });
            return false;
        }

        static byte[] ReadBlob(SqliteDataReader r, int ordinal)
        {
            // The column is guaranteed NOT NULL when we call this helper.
            using var stream = r.GetStream(ordinal);
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }

        byte[] key = ReadBlob(reader, 0);

        byte[]? value = null;
        if (!reader.IsDBNull(1))
        {
            value = ReadBlob(reader, 1);
        }

        string correlationIdText = reader.GetString(2);
        var correlationId = new CorrelationId { Value = correlationIdText };

        result = (Key: key,
                  Value: value,
                  Headers: null,
                  CorrelationId: correlationId);
        return true;
    }

    private string GetSqliteConnectionString()
    {
        var configuredLocation = Environment.GetEnvironmentVariable(KV_API_OUTBOX_STORAGE_DISK_LOCATION);
        if(string.IsNullOrWhiteSpace(configuredLocation))
        {
            if(string.IsNullOrEmpty(configuredLocation))
            {
                _logger.LogDebug($"Failed to read content of environment variable \"{KV_API_OUTBOX_STORAGE_DISK_LOCATION}\", got null/empty string");
            }
            else
            {
                _logger.LogWarning($"Failed to read proper content of environment variable \"{KV_API_OUTBOX_STORAGE_DISK_LOCATION}\", contained only whitespaces");
            }
            _logger.LogDebug($"Because {KV_API_OUTBOX_STORAGE_DISK_LOCATION} was not set, setting up in memory sqlite");
            var connectionStringBuilder = new SqliteConnectionStringBuilder
            {
                DataSource = "KeyValueOutboxInSQLiteMemDb",
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
                configuredLocation = $"{configuredLocation}/kvApiOutbox.sqlite";
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
                configuredLocation = $"{configuredLocation}/kvApiOutbox.sqlite";
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
            CREATE TABLE IF NOT EXISTS keyValueOutboxStore (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                kvKey BLOB NOT NULL,
                kvValue BLOB NULL,
                correlationId TEXT NOT NULL
            );
        ";
        command.ExecuteNonQuery();
    }
}
