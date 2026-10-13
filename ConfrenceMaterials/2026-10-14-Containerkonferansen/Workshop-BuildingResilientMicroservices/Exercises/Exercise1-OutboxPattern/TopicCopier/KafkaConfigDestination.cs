public record KafkaConfigDestination
{
    public KafkaConfigDestination()
    {
        var config = new ConfigurationBuilder().AddEnvironmentVariables().Build();
        config.Bind(nameof(KafkaConfigDestination), this);
    }
    public required string BootstrapServers { get; init; }
    public required string DestinationTopic { get; init; }
    public required string SecurityProtocol { get; init; }
    public string? SchemaRegistryAddress { get; init; }
    public string? SslKeyPemLocation { get; init; }
    public string? SslCaPemLocation { get; init; }
    public string? SslCertificatePemLocation { get; init; }
    public string? SslKeyPasswordLocation { get; init; }

    public int MaxMessageBytes { get; init; } = 1048588;
}
