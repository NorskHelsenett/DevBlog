public static class EnvVarNames
{
    // App specific config
    public const string BIG_PAYLOADS_CHUNKS_TOPIC = nameof(BIG_PAYLOADS_CHUNKS_TOPIC);
    public const string BIG_PAYLOADS_METADATA_TOPIC = nameof(BIG_PAYLOADS_METADATA_TOPIC);
    public const string BIG_PAYLOADS_CHUNK_PAYLOAD_SIZE_BYTES = nameof(BIG_PAYLOADS_CHUNK_PAYLOAD_SIZE_BYTES);
    public const string BIG_PAYLOADS_USER_ACCESS_MAPPING_TOPIC = nameof(BIG_PAYLOADS_USER_ACCESS_MAPPING_TOPIC);

    // Kafka client (producer/consumer/admin) configs
    public const string KAFKA_BOOTSTRAP_SERVERS = nameof(KAFKA_BOOTSTRAP_SERVERS);

    public const string KAFKA_SECURITY_PROTOCOL = nameof(KAFKA_SECURITY_PROTOCOL);
    public const string KAFKA_SSL_CA_PEM_LOCATION = nameof(KAFKA_SSL_CA_PEM_LOCATION);
    public const string KAFKA_SSL_CERTIFICATE_LOCATION = nameof(KAFKA_SSL_CERTIFICATE_LOCATION);
    public const string KAFKA_SSL_KEY_LOCATION = nameof(KAFKA_SSL_KEY_LOCATION);
    public const string KAFKA_SSL_KEY_PASSWORD_LOCATION = nameof(KAFKA_SSL_KEY_PASSWORD_LOCATION);

    public const string KAFKA_ACKS = nameof(KAFKA_ACKS);

    public const string KAFKA_GROUP_ID = nameof(KAFKA_GROUP_ID);
    public const string KAFKA_AUTO_OFFSET_RESET = nameof(KAFKA_AUTO_OFFSET_RESET);
    public const string KAFKA_ENABLE_AUTO_OFFSET_STORE = nameof(KAFKA_ENABLE_AUTO_OFFSET_STORE);
    public const string KAFKA_ENABLE_PARTITION_EOF = nameof(KAFKA_ENABLE_PARTITION_EOF);

    /// <summary>
    /// The url(s) you can reach a schema registry at. You can specify more than one schema registry url by separating them with commas.
    /// </summary>
    public const string KAFKA_SCHEMA_REGISTRY_URL = nameof(KAFKA_SCHEMA_REGISTRY_URL);

    // Auth confing
    public const string OIDC_IDP_ADDRESS_FOR_SERVER = nameof(OIDC_IDP_ADDRESS_FOR_SERVER);
    public const string OIDC_IDP_ADDRESS_FOR_USERS = nameof(OIDC_IDP_ADDRESS_FOR_USERS);
    public const string OIDC_REQUIRE_HTTPS_METADATA = nameof(OIDC_REQUIRE_HTTPS_METADATA);
    public const string OIDC_AUDIENCE = nameof(OIDC_AUDIENCE);
}
