using AddressWebApi.Dtos;
using Confluent.Kafka;
using CadastreRoadAddress = No.Nhn.Address.Cadastre.Road.CadastreRoadAddress;

namespace AddressWebApi;

public interface IAddressStorage
{
    public bool Store(CadastreRoadAddress cadastreRoadAddress);
    public bool TryRetrieve(string addressId, out CadastreRoadAddress result);
    public ResultStatus TryQuery(Query query, string correlationId, CancellationToken cancellationToken, out IEnumerable<AddressWebApi.Dtos.CadastreRoadAddress> result);
    public bool Remove(string key, string correlationId);

    public List<TopicPartitionOffset> GetLastConsumedTopicPartitionOffsets();
    public bool UpdateLastConsumedTopicPartitionOffsets(TopicPartitionOffset topicPartitionOffset);

    public bool Ready();
    public List<TopicPartitionOffset> GetStartupTimeHightestTopicPartitionOffsets();
    public bool SetStartupTimeHightestTopicPartitionOffsets(List<TopicPartitionOffset> topicPartitionOffsets);
}
