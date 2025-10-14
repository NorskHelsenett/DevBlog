namespace AddressRefiner;

public static class KafkaConfigBinder
{
    public static Confluent.Kafka.AdminClientConfig GetAdminClientConfig()
    {
        var adminClientConfig = new Confluent.Kafka.AdminClientConfig();

        var bootstrapServers = Environment.GetEnvironmentVariable(KAFKA_BOOTSTRAP_SERVERS);
        if(!string.IsNullOrEmpty(bootstrapServers)) adminClientConfig.BootstrapServers = bootstrapServers;

        adminClientConfig.SecurityProtocol = Confluent.Kafka.SecurityProtocol.Ssl;

        var sslCaPemLocation = Environment.GetEnvironmentVariable(KAFKA_SSL_CA_PEM_LOCATION);
        if(!string.IsNullOrEmpty(sslCaPemLocation)) adminClientConfig.SslCaPem = File.ReadAllText(sslCaPemLocation);

        var sslCertificateLocation = Environment.GetEnvironmentVariable(KAFKA_SSL_CERTIFICATE_LOCATION);
        if(!string.IsNullOrEmpty(sslCertificateLocation)) adminClientConfig.SslCertificateLocation = sslCertificateLocation;

        var sslKeyPasswordLocation = Environment.GetEnvironmentVariable(KAFKA_SSL_KEY_PASSWORD_LOCATION);
        if(!string.IsNullOrEmpty(sslKeyPasswordLocation)) adminClientConfig.SslKeyPassword = File.ReadAllLines(sslKeyPasswordLocation).FirstOrDefault();

        var sslKeyLocation = Environment.GetEnvironmentVariable(KAFKA_SSL_KEY_LOCATION);
        if(!string.IsNullOrEmpty(sslKeyLocation)) adminClientConfig.SslKeyLocation = sslKeyLocation;

        var clientId = Environment.GetEnvironmentVariable(KAFKA_CLIENT_ID);
        if(!string.IsNullOrEmpty(clientId)) adminClientConfig.ClientId = clientId;

        return adminClientConfig;
    }

    public static Confluent.Kafka.ProducerConfig GetProducerConfig()
    {
        var producerConfig = new Confluent.Kafka.ProducerConfig();

        var bootstrapServers = Environment.GetEnvironmentVariable(KAFKA_BOOTSTRAP_SERVERS);
        if(!string.IsNullOrEmpty(bootstrapServers)) producerConfig.BootstrapServers = bootstrapServers;

        producerConfig.SecurityProtocol = Confluent.Kafka.SecurityProtocol.Ssl;

        var sslCaPemLocation = Environment.GetEnvironmentVariable(KAFKA_SSL_CA_PEM_LOCATION);
        if(!string.IsNullOrEmpty(sslCaPemLocation)) producerConfig.SslCaPem = File.ReadAllText(sslCaPemLocation);

        var sslCertificateLocation = Environment.GetEnvironmentVariable(KAFKA_SSL_CERTIFICATE_LOCATION);
        if(!string.IsNullOrEmpty(sslCertificateLocation)) producerConfig.SslCertificateLocation = sslCertificateLocation;

        var sslKeyPasswordLocation = Environment.GetEnvironmentVariable(KAFKA_SSL_KEY_PASSWORD_LOCATION);
        if(!string.IsNullOrEmpty(sslKeyPasswordLocation)) producerConfig.SslKeyPassword = File.ReadAllLines(sslKeyPasswordLocation).FirstOrDefault();

        var sslKeyLocation = Environment.GetEnvironmentVariable(KAFKA_SSL_KEY_LOCATION);
        if(!string.IsNullOrEmpty(sslKeyLocation)) producerConfig.SslKeyLocation = sslKeyLocation;

        var acks = Environment.GetEnvironmentVariable(KAFKA_ACKS);
        switch (acks?.ToLowerInvariant())
        {
            case "all":
                producerConfig.Acks = Confluent.Kafka.Acks.All;
                break;
            case "leader":
                producerConfig.Acks = Confluent.Kafka.Acks.Leader;
                break;
            case "none":
                producerConfig.Acks = Confluent.Kafka.Acks.None;
                break;
            default:
                break;
        }

        var clientId = Environment.GetEnvironmentVariable(KAFKA_CLIENT_ID);
        if(!string.IsNullOrEmpty(clientId)) producerConfig.ClientId = clientId;

        return producerConfig;
    }

    public static Confluent.Kafka.ConsumerConfig GetConsumerConfig()
    {
        var consumerConfig = new Confluent.Kafka.ConsumerConfig();

        var bootstrapServers = Environment.GetEnvironmentVariable(KAFKA_BOOTSTRAP_SERVERS);
        if(!string.IsNullOrEmpty(bootstrapServers)) consumerConfig.BootstrapServers = bootstrapServers;

        consumerConfig.SecurityProtocol = Confluent.Kafka.SecurityProtocol.Ssl;

        var sslCaPemLocation = Environment.GetEnvironmentVariable(KAFKA_SSL_CA_PEM_LOCATION);
        if(!string.IsNullOrEmpty(sslCaPemLocation)) consumerConfig.SslCaPem = File.ReadAllText(sslCaPemLocation);

        var sslCertificateLocation = Environment.GetEnvironmentVariable(KAFKA_SSL_CERTIFICATE_LOCATION);
        if(!string.IsNullOrEmpty(sslCertificateLocation)) consumerConfig.SslCertificateLocation = sslCertificateLocation;

        var sslKeyPasswordLocation = Environment.GetEnvironmentVariable(KAFKA_SSL_KEY_PASSWORD_LOCATION);
        if(!string.IsNullOrEmpty(sslKeyPasswordLocation)) consumerConfig.SslKeyPassword = File.ReadAllLines(sslKeyPasswordLocation).FirstOrDefault();

        var sslKeyLocation = Environment.GetEnvironmentVariable(KAFKA_SSL_KEY_LOCATION);
        if(!string.IsNullOrEmpty(sslKeyLocation)) consumerConfig.SslKeyLocation = sslKeyLocation;

        var clientId = Environment.GetEnvironmentVariable(KAFKA_CLIENT_ID);
        if(!string.IsNullOrEmpty(clientId)) consumerConfig.ClientId = clientId;

        var groupId = Environment.GetEnvironmentVariable(KAFKA_GROUP_ID);
        if(!string.IsNullOrEmpty(groupId)) consumerConfig.GroupId = groupId;

        var autoOffsetReset = Environment.GetEnvironmentVariable(KAFKA_AUTO_OFFSET_RESET);
        switch (autoOffsetReset?.ToLowerInvariant())
        {
            case "earliest":
                consumerConfig.AutoOffsetReset = Confluent.Kafka.AutoOffsetReset.Earliest;
                break;
            case "error":
                consumerConfig.AutoOffsetReset = Confluent.Kafka.AutoOffsetReset.Error;
                break;
            case "latest":
                consumerConfig.AutoOffsetReset = Confluent.Kafka.AutoOffsetReset.Latest;
                break;
            default:
                break;
        }

        var enableAutoOffsetStore = Environment.GetEnvironmentVariable(KAFKA_ENABLE_AUTO_OFFSET_STORE);
        switch (enableAutoOffsetStore?.ToLowerInvariant())
        {
            case "true":
                consumerConfig.EnableAutoOffsetStore = true;
                break;
            case "false":
                consumerConfig.EnableAutoOffsetStore = false;
                break;
            default:
                break;
        }

        var enablePartitionEof = Environment.GetEnvironmentVariable(KAFKA_ENABLE_PARTITION_EOF);
        switch (enablePartitionEof?.ToLowerInvariant())
        {
            case "true":
                consumerConfig.EnablePartitionEof = true;
                break;
            case "false":
                consumerConfig.EnablePartitionEof = false;
                break;
            default:
                break;
        }

        return consumerConfig;
    }
    public static Confluent.SchemaRegistry.SchemaRegistryConfig GetSchemaRegistryConfig()
    {
        var schemaRegistryConfig = new Confluent.SchemaRegistry.SchemaRegistryConfig();

        var url = Environment.GetEnvironmentVariable(KAFKA_SCHEMA_REGISTRY_URL);
        if(!string.IsNullOrEmpty(url)) schemaRegistryConfig.Url = url;

        return schemaRegistryConfig;
    }
}
