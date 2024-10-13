public interface IChungingProducer
{
    Task<bool> ProduceAsync(Stream theFileStream);
}