using Confluent.Kafka;
using DataTypes;
public interface IStorageInbox
{
    public DataTypes.Error? Store(DcItem item);
    public (DataTypes.Error? Error, DcItem? RetrievedItem) Retrieve(string key, CancellationToken cancellationToken);
    public DataTypes.Error? Remove(string key);

    public List<TopicPartitionOffset> GetLastConsumedTopicPartitionOffsets();
    public DataTypes.Error? UpdateLastConsumedTopicPartitionOffsets(TopicPartitionOffset topicPartitionOffset);

    public bool Ready();
    public List<TopicPartitionOffset> GetStartupTimeHightestTopicPartitionOffsets();
    public DataTypes.Error? SetStartupTimeHightestTopicPartitionOffsets(List<TopicPartitionOffset> topicPartitionOffsets);
}
