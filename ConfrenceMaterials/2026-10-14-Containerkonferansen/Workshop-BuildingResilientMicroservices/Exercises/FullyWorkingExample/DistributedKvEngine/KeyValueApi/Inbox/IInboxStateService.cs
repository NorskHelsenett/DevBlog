public interface IInboxStateService
{
    /// <summary>
    /// Persist a raw Kafka record for later processing.
    /// </summary>
    bool Enqueue(
        byte[] key,
        byte[]? value,               // null → delete
        string correlationId,
        string topic,
        int partition,
        long offset,
        DateTime utcTimestamp);

    /// <summary>
    /// Returns the next batch of unprocessed messages ordered by (topic,partition,offset).
    /// </summary>
    IReadOnlyList<InboxMessage> GetPending(int maxBatchSize = 100);

    /// <summary>
    /// Mark a message as successfully processed.
    /// </summary>
    bool MarkProcessed(Guid messageId);

    /// <summary>
    /// Move a message to a dead‑letter table (or just delete it) after too many retries.
    /// </summary>
    bool MoveToDeadLetter(Guid messageId, string reason);
}
