namespace AddressWebApi;

public static class ConfigKeys
{
    public const string ADDRESS_WEB_API_KAFKA_TOPIC_REFINED_ADDRESSES = nameof(ADDRESS_WEB_API_KAFKA_TOPIC_REFINED_ADDRESSES);

    public const string ADDRESS_WEB_API_QUERY_NUMBER_OF_RESULTS_LIMIT = nameof(ADDRESS_WEB_API_QUERY_NUMBER_OF_RESULTS_LIMIT);
    public const string ADDRESS_WEB_API_QUERY_TIMEOUT_MS = nameof(ADDRESS_WEB_API_QUERY_TIMEOUT_MS);

    // Kafka client (producer/consumer/admin) configs
    public const string KAFKA_BOOTSTRAP_SERVERS = nameof(KAFKA_BOOTSTRAP_SERVERS);

    public const string KAFKA_SECURITY_PROTOCOL = nameof(KAFKA_SECURITY_PROTOCOL);
    public const string KAFKA_SSL_CA_PEM_LOCATION = nameof(KAFKA_SSL_CA_PEM_LOCATION);
    public const string KAFKA_SSL_CERTIFICATE_LOCATION = nameof(KAFKA_SSL_CERTIFICATE_LOCATION);
    public const string KAFKA_SSL_KEY_LOCATION = nameof(KAFKA_SSL_KEY_LOCATION);
    public const string KAFKA_SSL_KEY_PASSWORD_LOCATION = nameof(KAFKA_SSL_KEY_PASSWORD_LOCATION);

    public const string KAFKA_ACKS = nameof(KAFKA_ACKS);

    public const string KAFKA_CLIENT_ID = nameof(KAFKA_CLIENT_ID);
    public const string KAFKA_GROUP_ID = nameof(KAFKA_GROUP_ID);
    public const string KAFKA_AUTO_OFFSET_RESET = nameof(KAFKA_AUTO_OFFSET_RESET);
    public const string KAFKA_ENABLE_AUTO_OFFSET_STORE = nameof(KAFKA_ENABLE_AUTO_OFFSET_STORE);
    public const string KAFKA_ENABLE_PARTITION_EOF = nameof(KAFKA_ENABLE_PARTITION_EOF);

    /// <summary>
    /// The url(s) you can reach a schema registry at. You can specify more than one schema registry url by separating them with commas.
    /// </summary>
    public const string KAFKA_SCHEMA_REGISTRY_URL = nameof(KAFKA_SCHEMA_REGISTRY_URL);

}
