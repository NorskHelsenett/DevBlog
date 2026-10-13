namespace KeyValueApi;

public interface IOutboxStateService
{
    public bool Store(byte[] key, byte[]? value,  Dictionary<string,byte[]>? headers, CorrelationId correlationId);
    public bool TryRetrieveNext(out (byte[] Key, byte[]? Value, Dictionary<string,byte[]>? Headers, CorrelationId CorrelationId) result);
    public bool RemoveNext();
}
