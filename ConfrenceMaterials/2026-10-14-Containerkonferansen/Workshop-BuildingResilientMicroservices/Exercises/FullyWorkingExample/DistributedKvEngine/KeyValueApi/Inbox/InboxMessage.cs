public sealed record InboxMessage
{
    public Guid   MessageId      { get; init; }   // PK
    public byte[] Key            { get; init; }
    public byte[]? Value         { get; init; }   // null = delete
    public string CorrelationId  { get; init; }
    public string Topic          { get; init; }
    public int    Partition      { get; init; }
    public long   Offset         { get; init; }
    public DateTime EnqueuedUtc   { get; init; } // when the consumer wrote it
}
