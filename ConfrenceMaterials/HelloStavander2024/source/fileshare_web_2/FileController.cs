public class FileController
{
    private readonly ILogger<FileController> _logger;

    public FileController(ILogger<FileController> logger)
    {
        _logger = logger;
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

        return files;

    }

}
