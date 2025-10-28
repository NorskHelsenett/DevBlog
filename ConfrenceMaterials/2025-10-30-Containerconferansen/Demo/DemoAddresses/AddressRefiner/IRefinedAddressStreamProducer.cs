using Confluent.Kafka;
using No.Nhn.Address.Cadastre.Road;

namespace AddressRefiner;

public interface IRefinedAddressStreamProducer
{
    public Task<bool> Produce(string key, CadastreRoadAddress? value, Headers headers, string correlationId);
}
