using Microsoft.Data.Sqlite;

public class InboxStateInSQLiteService : IInboxStateService
{
    private readonly ILogger<InboxStateInSQLiteService> _logger;
    private readonly SqliteConnection _db;

    public InboxStateInSQLiteService(ILogger<InboxStateInSQLiteService> logger)
    {
        _logger = logger;
        _db = new SqliteConnection(BuildConnectionString());
        _db.Open();

        InitializeDb();
    }

    public bool Enqueue(
        byte[] key,
        byte[]? value,
        string correlationId,
        string topic,
        int partition,
        long offset,
        DateTime utcTimestamp)
    {
        var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO inbox(
                messageId,
                kvKey,
                kvValue,
                correlationId,
                topic,
                partition,
                offset,
                enqueuedUtc)
            VALUES (
                $id,
                $k,
                $v,
                $c,
                $t,
                $p,
                $o,
                $e);
        ";

        var id = Guid.NewGuid();
        cmd.Parameters.AddWithValue("$id", id.ToString());
        cmd.Parameters.AddWithValue("$k", key);
        cmd.Parameters.AddWithValue("$v", (object?)value ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$c", correlationId);
        cmd.Parameters.AddWithValue("$t", topic);
        cmd.Parameters.AddWithValue("$p", partition);
        cmd.Parameters.AddWithValue("$o", offset);
        cmd.Parameters.AddWithValue("$e", utcTimestamp.ToString("o")); // ISO‑8601

        var rows = cmd.ExecuteNonQuery();
        return rows == 1;
    }

    public IReadOnlyList<InboxMessage> GetPending(int maxBatchSize = 100)
    {
        var list = new List<InboxMessage>();

        var cmd = _db.CreateCommand();
        cmd.CommandText = $@"
            SELECT
                messageId,
                kvKey,
                kvValue,
                correlationId,
                topic,
                partition,
                offset,
                enqueuedUtc
            FROM inbox
            ORDER BY topic, partition, offset
            LIMIT {maxBatchSize};
        ";

        using var rdr = cmd.ExecuteReader();

        while (rdr.Read())
        {
            var id   = Guid.Parse(rdr.GetString(0));

            // kvKey & kvValue are BLOBs => use GetStream → MemoryStream → ToArray()
            var key   = ReadBlob(rdr, 1);
            var value = rdr.IsDBNull(2) ? null : ReadBlob(rdr, 2);

            var correlationId = rdr.GetString(3);
            var topic = rdr.GetString(4);
            var partition = rdr.GetInt32(5);
            var offset = rdr.GetInt64(6);
            var enqueuedUtc = DateTime.Parse(rdr.GetString(7), null, System.Globalization.DateTimeStyles.RoundtripKind);

            list.Add(new InboxMessage
            {
                MessageId      = id,
                Key            = key,
                Value          = value,
                CorrelationId  = correlationId,
                Topic          = topic,
                Partition      = partition,
                Offset         = offset,
                EnqueuedUtc    = enqueuedUtc
            });
        }

        return list;
    }

    public bool MarkProcessed(Guid messageId)
    {
        var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            DELETE FROM inbox WHERE messageId = $id;
        ";
        cmd.Parameters.AddWithValue("$id", messageId.ToString());

        var rows = cmd.ExecuteNonQuery();
        return rows == 1;
    }

    public bool MoveToDeadLetter(Guid messageId, string reason)
    {
        // Simple implementation: copy to dead_letter table then delete from inbox.
        using var tx = _db.BeginTransaction();

        var copy = _db.CreateCommand();
        copy.Transaction = tx;
        copy.CommandText = @"
            INSERT INTO dead_letter(
                messageId,
                kvKey,
                kvValue,
                correlationId,
                topic,
                partition,
                offset,
                enqueuedUtc,
                failureReason)
            SELECT
                messageId,
                kvKey,
                kvValue,
                correlationId,
                topic,
                partition,
                offset,
                enqueuedUtc,
                $reason
            FROM inbox
            WHERE messageId = $id;
        ";
        copy.Parameters.AddWithValue("$id", messageId.ToString());
        copy.Parameters.AddWithValue("$reason", reason);
        copy.ExecuteNonQuery();

        var del = _db.CreateCommand();
        del.Transaction = tx;
        del.CommandText = @"DELETE FROM inbox WHERE messageId = $id;";
        del.Parameters.AddWithValue("$id", messageId.ToString());
        del.ExecuteNonQuery();

        tx.Commit();
        return true;
    }

    private static byte[] ReadBlob(SqliteDataReader rdr, int ordinal)
    {
        using var stream = rdr.GetStream(ordinal);
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    private static string BuildConnectionString()
    {
        var basePath = Environment.GetEnvironmentVariable(KV_API_STATE_STORAGE_DISK_LOCATION);
        if (string.IsNullOrWhiteSpace(basePath))
        {
            var cs = new SqliteConnectionStringBuilder
            {
                DataSource = "InboxStateInMemory",
                Mode = SqliteOpenMode.Memory,
                Cache = SqliteCacheMode.Shared
            };
            return cs.ToString();
        }

        basePath = basePath.Trim();
        if (!Path.Exists(basePath))
        {
            Directory.CreateDirectory(basePath);
        }

        var filePath = Path.Combine(basePath, "kvApiInbox.sqlite");
        return new SqliteConnectionStringBuilder
        {
            DataSource = filePath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();
    }

    private void InitializeDb()
    {
        var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS inbox (
                messageId      TEXT PRIMARY KEY,
                kvKey          BLOB NOT NULL,
                kvValue        BLOB,
                correlationId  TEXT NOT NULL,
                topic          TEXT NOT NULL,
                partition      INTEGER NOT NULL,
                offset         INTEGER NOT NULL,
                enqueuedUtc    TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS dead_letter (
                messageId      TEXT PRIMARY KEY,
                kvKey          BLOB NOT NULL,
                kvValue        BLOB,
                correlationId  TEXT NOT NULL,
                topic          TEXT NOT NULL,
                partition      INTEGER NOT NULL,
                offset         INTEGER NOT NULL,
                enqueuedUtc    TEXT NOT NULL,
                failureReason  TEXT NOT NULL,
                failedAt       TEXT NOT NULL DEFAULT (datetime('now'))
            );
        ";
        cmd.ExecuteNonQuery();
    }
}
