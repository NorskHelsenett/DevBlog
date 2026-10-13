public interface IKeyValueStateService
{
    public bool Store(byte[] key, byte[] value, string correlationId);
    public bool TryRetrieve(byte[] key, out (byte[] Value, string CorrelationId) result);
    public bool Remove(byte[] key, string correlationId);

    public List<KafkaTopicPartitionOffset> GetLastConsumedTopicPartitionOffsets();
    public bool UpdateLastConsumedTopicPartitionOffsets(KafkaTopicPartitionOffset topicPartitionOffset);

    public bool Ready();
    public List<KafkaTopicPartitionOffset> GetStartupTimeHightestTopicPartitionOffsets();
    public bool SetStartupTimeHightestTopicPartitionOffsets(List<KafkaTopicPartitionOffset> topicPartitionOffsets);
}
