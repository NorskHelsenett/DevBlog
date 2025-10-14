namespace DistributedCache.Kafka.Producers;

public interface IDcProducer
{
    public Task<DataTypes.Error?> Produce(DcItem item);
}
