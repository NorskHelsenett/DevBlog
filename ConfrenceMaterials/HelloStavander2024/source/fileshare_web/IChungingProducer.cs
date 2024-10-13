public interface IChungingProducer
{
    Task<bool> ProduceAsync(Stream theFileStream);
}
public class MockProducer : IChungingProducer
{
    public Task<bool> ProduceAsync(Stream theFileStream)
    {
        throw new NotImplementedException();
    }
}