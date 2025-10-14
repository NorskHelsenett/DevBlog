using Confluent.Kafka;
using No.Nhn.Address.Cadastre.Road;

namespace AddressRefiner;

public interface IAddressStorage
{
    public bool Store(CadastreRoadAddress cadastreRoadAddress);
    public bool TryRetrieve(string addressId, out CadastreRoadAddress result);
    public bool Remove(string key, string correlationId);

    public List<TopicPartitionOffset> GetLastConsumedTopicPartitionOffsets();
    public bool UpdateLastConsumedTopicPartitionOffsets(TopicPartitionOffset topicPartitionOffset);

    public bool Ready();
    public List<TopicPartitionOffset> GetStartupTimeHightestTopicPartitionOffsets();
    public bool SetStartupTimeHightestTopicPartitionOffsets(List<TopicPartitionOffset> topicPartitionOffsets);
}
