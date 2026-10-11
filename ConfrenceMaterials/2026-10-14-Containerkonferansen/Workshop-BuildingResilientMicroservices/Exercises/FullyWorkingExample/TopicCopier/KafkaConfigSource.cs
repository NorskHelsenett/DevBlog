public record KafkaConfigSource
{
    public KafkaConfigSource()
    {
        var config = new ConfigurationBuilder().AddEnvironmentVariables().Build();
        config.Bind(nameof(KafkaConfigSource), this);
    }
    public required string BootstrapServers { get; init; }
    public required string SourceTopic { get; init; }
    public required string GroupId { get; init; }
    public required string SecurityProtocol { get; init; }
    public string? SslKeyPemLocation { get; init; }
    public string? SslCaPemLocation { get; init; }
    public string? SslCertificatePemLocation { get; init; }
    public string? SslKeyPasswordLocation { get; init; }

    public int MaxMessageBytes { get; init; } = 1048588;
    public required bool OriginalTopicHasSchema { get; init; }
}
