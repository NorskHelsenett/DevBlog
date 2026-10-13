public static class KafkaSchemaRegistryConfigEnvBinder
{
    public static Confluent.SchemaRegistry.SchemaRegistryConfig GetSchemaRegistryConfig()
    {
        var schemaRegistryConfig = new Confluent.SchemaRegistry.SchemaRegistryConfig();

        var basicAuthCredentialsSource = Environment.GetEnvironmentVariable(KAFKA_SCHEMA_REGISTRY_BASIC_AUTH_CREDENTIALS_SOURCE);
        switch (basicAuthCredentialsSource?.ToLowerInvariant())
        {
            case "userinfo":
                schemaRegistryConfig.BasicAuthCredentialsSource = Confluent.SchemaRegistry.AuthCredentialsSource.UserInfo;
                break;
            case "saslinherit":
                schemaRegistryConfig.BasicAuthCredentialsSource = Confluent.SchemaRegistry.AuthCredentialsSource.SaslInherit;
                break;
            default:
                break;
        }

        var basicAuthUserInfo = Environment.GetEnvironmentVariable(KAFKA_SCHEMA_REGISTRY_BASIC_AUTH_USER_INFO);
        if(!string.IsNullOrEmpty(basicAuthUserInfo)) schemaRegistryConfig.BasicAuthUserInfo = basicAuthUserInfo;

       var enableSslCertificateVerification = Environment.GetEnvironmentVariable(KAFKA_SCHEMA_REGISTRY_ENABLE_SSL_CERTIFICATE_VERIFICATION);
        switch (enableSslCertificateVerification?.ToLowerInvariant())
        {
            case "true":
                schemaRegistryConfig.EnableSslCertificateVerification = true;
                break;
            case "false":
                schemaRegistryConfig.EnableSslCertificateVerification = false;
                break;
            default:
                break;
        }

        var maxCachedSchemas = Environment.GetEnvironmentVariable(KAFKA_SCHEMA_REGISTRY_MAX_CACHED_SCHEMAS);
        if(!string.IsNullOrEmpty(maxCachedSchemas)) schemaRegistryConfig.MaxCachedSchemas = int.Parse(maxCachedSchemas);

        var requestTimeoutMs = Environment.GetEnvironmentVariable(KAFKA_SCHEMA_REGISTRY_REQUEST_TIMEOUT_MS);
        if(!string.IsNullOrEmpty(requestTimeoutMs)) schemaRegistryConfig.RequestTimeoutMs = int.Parse(requestTimeoutMs);

        var sslCaLocation = Environment.GetEnvironmentVariable(KAFKA_SCHEMA_REGISTRY_SSL_CA_LOCATION);
        if(!string.IsNullOrEmpty(sslCaLocation)) schemaRegistryConfig.SslCaLocation = sslCaLocation;

        var sslKeystoreLocation = Environment.GetEnvironmentVariable(KAFKA_SCHEMA_REGISTRY_SSL_KEYSTORE_LOCATION);
        if(!string.IsNullOrEmpty(sslKeystoreLocation)) schemaRegistryConfig.SslKeystoreLocation = sslKeystoreLocation;

        var sslKeystorePassword = Environment.GetEnvironmentVariable(KAFKA_SCHEMA_REGISTRY_SSL_KEYSTORE_PASSWORD);
        if(!string.IsNullOrEmpty(sslKeystorePassword)) schemaRegistryConfig.SslKeystorePassword = sslKeystorePassword;

        var url = Environment.GetEnvironmentVariable(KAFKA_SCHEMA_REGISTRY_URL);
        if(!string.IsNullOrEmpty(url)) schemaRegistryConfig.Url = url;

        return schemaRegistryConfig;
    }

    public const string KAFKA_SCHEMA_REGISTRY_BASIC_AUTH_CREDENTIALS_SOURCE = nameof(KAFKA_SCHEMA_REGISTRY_BASIC_AUTH_CREDENTIALS_SOURCE);
    public const string KAFKA_SCHEMA_REGISTRY_BASIC_AUTH_USER_INFO = nameof(KAFKA_SCHEMA_REGISTRY_BASIC_AUTH_USER_INFO);
    public const string KAFKA_SCHEMA_REGISTRY_ENABLE_SSL_CERTIFICATE_VERIFICATION = nameof(KAFKA_SCHEMA_REGISTRY_ENABLE_SSL_CERTIFICATE_VERIFICATION);
    public const string KAFKA_SCHEMA_REGISTRY_MAX_CACHED_SCHEMAS = nameof(KAFKA_SCHEMA_REGISTRY_MAX_CACHED_SCHEMAS);
    public const string KAFKA_SCHEMA_REGISTRY_REQUEST_TIMEOUT_MS = nameof(KAFKA_SCHEMA_REGISTRY_REQUEST_TIMEOUT_MS);
    public const string KAFKA_SCHEMA_REGISTRY_SSL_CA_LOCATION = nameof(KAFKA_SCHEMA_REGISTRY_SSL_CA_LOCATION);
    public const string KAFKA_SCHEMA_REGISTRY_SSL_KEYSTORE_LOCATION = nameof(KAFKA_SCHEMA_REGISTRY_SSL_KEYSTORE_LOCATION);
    public const string KAFKA_SCHEMA_REGISTRY_SSL_KEYSTORE_PASSWORD = nameof(KAFKA_SCHEMA_REGISTRY_SSL_KEYSTORE_PASSWORD);
    public const string KAFKA_SCHEMA_REGISTRY_URL = nameof(KAFKA_SCHEMA_REGISTRY_URL);
}
