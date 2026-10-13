public static class KafkaAdminClientEnvBinder
{
    public static Confluent.Kafka.AdminClientConfig GetAdminClientConfig()
    {
        var clientConfig = KafkaClientConfigEnvBinder.GetClientConfig();
        var adminClientConfig = new Confluent.Kafka.AdminClientConfig(clientConfig);
        return adminClientConfig;
    }
}
