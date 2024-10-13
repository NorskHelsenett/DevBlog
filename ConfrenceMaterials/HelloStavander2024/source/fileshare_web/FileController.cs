public class FileController
{
    private readonly ILogger<FileController> _logger;
    private IChungingProducer _chungingProducer;

    public FileController(ILogger<FileController> logger, IChungingProducer producer)
    {
        _logger = logger;
        _chungingProducer = producer;
    }

    public List<SecretFile> GetFiles()
    {
        _logger.LogDebug("Feching Files");

        var files = new List<SecretFile>();
        files.Add(new SecretFile
        {
            Id = Guid.Empty,
            Name = "Random navn 1",
            Size = "1039320 kb",
            Rights = FileRights.Owner
        });
        files.Add(new SecretFile
        {
            Id = Guid.Empty,
            Name = "Random navn 2",
            Size = "97023 mb",
            Rights = FileRights.Shared
        });
        files.Add(new SecretFile
        {
            Id = Guid.Empty,
            Name = "Random navn 2",
            Size = "97023 mb",
            Rights = FileRights.Shared | FileRights.Owner
        });
        return files;

    }
    public async Task<bool> SaveFile(Stream theFileStream){
        return await _chungingProducer.ProduceAsync(theFileStream);
    }
}
