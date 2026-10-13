public static class KafkaClientConfigEnvBinder
{
    public static Confluent.Kafka.ClientConfig GetClientConfig()
    {
        var clientConfig = new Confluent.Kafka.ClientConfig();

        var sslProviders = Environment.GetEnvironmentVariable(KAFKA_SSL_PROVIDERS);
        if(!string.IsNullOrEmpty(sslProviders)) clientConfig.SslProviders = sslProviders;

        var sslKeystorePassword = Environment.GetEnvironmentVariable(KAFKA_SSL_KEYSTORE_PASSWORD);
        if(!string.IsNullOrEmpty(sslKeystorePassword)) clientConfig.SslKeystorePassword = sslKeystorePassword;

        var sslKeystoreLocation = Environment.GetEnvironmentVariable(KAFKA_SSL_KEYSTORE_LOCATION);
        if(!string.IsNullOrEmpty(sslKeystoreLocation)) clientConfig.SslKeystoreLocation = sslKeystoreLocation;

        var sslCrlLocation = Environment.GetEnvironmentVariable(KAFKA_SSL_CRL_LOCATION);
        if(!string.IsNullOrEmpty(sslCrlLocation)) clientConfig.SslCrlLocation = sslCrlLocation;

        var sslCaCertificateStores = Environment.GetEnvironmentVariable(KAFKA_SSL_CA_CERTIFICATE_STORES);
        if(!string.IsNullOrEmpty(sslCaCertificateStores)) clientConfig.SslCaCertificateStores = sslCaCertificateStores;

        var sslCaPem = Environment.GetEnvironmentVariable(KAFKA_SSL_CA_PEM);
        if(!string.IsNullOrEmpty(sslCaPem)) clientConfig.SslCaPem = sslCaPem;

        var sslCaPemLocation = Environment.GetEnvironmentVariable(KAFKA_SSL_CA_PEM_LOCATION);
        if(!string.IsNullOrEmpty(sslCaPemLocation)) clientConfig.SslCaPem = File.ReadAllText(sslCaPemLocation);

        var sslCaLocation = Environment.GetEnvironmentVariable(KAFKA_SSL_CA_LOCATION);
        if(!string.IsNullOrEmpty(sslCaLocation)) clientConfig.SslCaLocation = sslCaLocation;

        var sslCertificateLocation = Environment.GetEnvironmentVariable(KAFKA_SSL_CERTIFICATE_LOCATION);
        if(!string.IsNullOrEmpty(sslCertificateLocation)) clientConfig.SslCertificateLocation = sslCertificateLocation;

        var sslEngineLocation = Environment.GetEnvironmentVariable(KAFKA_SSL_ENGINE_LOCATION);
        if(!string.IsNullOrEmpty(sslEngineLocation)) clientConfig.SslEngineLocation = sslEngineLocation;

        var sslKeyPem = Environment.GetEnvironmentVariable(KAFKA_SSL_KEY_PEM);
        if(!string.IsNullOrEmpty(sslKeyPem)) clientConfig.SslKeyPem = sslKeyPem;

        var sslKeyPassword = Environment.GetEnvironmentVariable(KAFKA_SSL_KEY_PASSWORD);
        if(!string.IsNullOrEmpty(sslKeyPassword)) clientConfig.SslKeyPassword = sslKeyPassword;

        var sslKeyPasswordLocation = Environment.GetEnvironmentVariable(KAFKA_SSL_KEY_PASSWORD_LOCATION);
        if(!string.IsNullOrEmpty(sslKeyPasswordLocation)) clientConfig.SslKeyPassword = File.ReadAllLines(sslKeyPasswordLocation).FirstOrDefault();

        var sslKeyLocation = Environment.GetEnvironmentVariable(KAFKA_SSL_KEY_LOCATION);
        if(!string.IsNullOrEmpty(sslKeyLocation)) clientConfig.SslKeyLocation = sslKeyLocation;

        var sslSigalgsList = Environment.GetEnvironmentVariable(KAFKA_SSL_SIGALGS_LIST);
        if(!string.IsNullOrEmpty(sslSigalgsList)) clientConfig.SslSigalgsList = sslSigalgsList;

        var sslCurvesList = Environment.GetEnvironmentVariable(KAFKA_SSL_CURVES_LIST);
        if(!string.IsNullOrEmpty(sslCurvesList)) clientConfig.SslCurvesList = sslCurvesList;

        var sslCipherSuites = Environment.GetEnvironmentVariable(KAFKA_SSL_CIPHER_SUITES);
        if(!string.IsNullOrEmpty(sslCipherSuites)) clientConfig.SslCipherSuites = sslCipherSuites;

        var sslCertificatePem = Environment.GetEnvironmentVariable(KAFKA_SSL_CERTIFICATE_PEM);
        if(!string.IsNullOrEmpty(sslCertificatePem)) clientConfig.SslCertificatePem = sslCertificatePem;

        var sslEngineId = Environment.GetEnvironmentVariable(KAFKA_SSL_ENGINE_ID);
        if(!string.IsNullOrEmpty(sslEngineId)) clientConfig.SslEngineId = sslEngineId;

        var sslEndpointIdentificationAlgorithm = Environment.GetEnvironmentVariable(KAFKA_SSL_ENDPOINT_IDENTIFICATION_ALGORITHM);
        switch (sslEndpointIdentificationAlgorithm?.ToLowerInvariant())
        {
            case "none":
                clientConfig.SslEndpointIdentificationAlgorithm = Confluent.Kafka.SslEndpointIdentificationAlgorithm.None;
                break;
            case "https":
                clientConfig.SslEndpointIdentificationAlgorithm = Confluent.Kafka.SslEndpointIdentificationAlgorithm.Https;
                break;
            default:
                break;
        }

        var securityProtocol = Environment.GetEnvironmentVariable(KAFKA_SECURITY_PROTOCOL);
        switch (securityProtocol?.ToLowerInvariant())
        {
            case "plaintext":
                clientConfig.SecurityProtocol = Confluent.Kafka.SecurityProtocol.Plaintext;
                break;
            case "saslplaintext":
                clientConfig.SecurityProtocol = Confluent.Kafka.SecurityProtocol.SaslPlaintext;
                break;
            case "saslssl":
                clientConfig.SecurityProtocol = Confluent.Kafka.SecurityProtocol.SaslSsl;
                break;
            case "ssl":
                clientConfig.SecurityProtocol = Confluent.Kafka.SecurityProtocol.Ssl;
                break;
            default:
                break;
        }

        var pluginLibraryPaths = Environment.GetEnvironmentVariable(KAFKA_PLUGIN_LIBRARY_PATHS);
        if(!string.IsNullOrEmpty(pluginLibraryPaths)) clientConfig.PluginLibraryPaths = pluginLibraryPaths;

        var saslOauthbearerTokenEndpointUrl = Environment.GetEnvironmentVariable(KAFKA_SASL_OAUTHBEARER_TOKEN_ENDPOINT_URL);
        if(!string.IsNullOrEmpty(saslOauthbearerTokenEndpointUrl)) clientConfig.SaslOauthbearerTokenEndpointUrl = saslOauthbearerTokenEndpointUrl;

        var saslOauthbearerExtensions = Environment.GetEnvironmentVariable(KAFKA_SASL_OAUTHBEARER_EXTENSIONS);
        if(!string.IsNullOrEmpty(saslOauthbearerExtensions)) clientConfig.SaslOauthbearerExtensions = saslOauthbearerExtensions;

        var saslOauthbearerScope = Environment.GetEnvironmentVariable(KAFKA_SASL_OAUTHBEARER_SCOPE);
        if(!string.IsNullOrEmpty(saslOauthbearerScope)) clientConfig.SaslOauthbearerScope = saslOauthbearerScope;

        var saslOauthbearerClientSecret = Environment.GetEnvironmentVariable(KAFKA_SASL_OAUTHBEARER_CLIENT_SECRET);
        if(!string.IsNullOrEmpty(saslOauthbearerClientSecret)) clientConfig.SaslOauthbearerClientSecret = saslOauthbearerClientSecret;

        var saslOauthbearerClientId = Environment.GetEnvironmentVariable(KAFKA_SASL_OAUTHBEARER_CLIENT_ID);
        if(!string.IsNullOrEmpty(saslOauthbearerClientId)) clientConfig.SaslOauthbearerClientId = saslOauthbearerClientId;

        var saslOauthbearerMethod = Environment.GetEnvironmentVariable(KAFKA_SASL_OAUTHBEARER_METHOD);
        switch (saslOauthbearerMethod?.ToLowerInvariant())
        {
            case "default":
                clientConfig.SaslOauthbearerMethod = Confluent.Kafka.SaslOauthbearerMethod.Default;
                break;
            case "oidc":
                clientConfig.SaslOauthbearerMethod = Confluent.Kafka.SaslOauthbearerMethod.Oidc;
                break;
            default:
                break;
        }

        var enableSslCertificateVerification = Environment.GetEnvironmentVariable(KAFKA_ENABLE_SSL_CERTIFICATE_VERIFICATION);
        switch (enableSslCertificateVerification?.ToLowerInvariant())
        {
            case "true":
                clientConfig.EnableSslCertificateVerification = true;
                break;
            case "false":
                clientConfig.EnableSslCertificateVerification = false;
                break;
            default:
                break;
        }

        var enableSaslOauthbearerUnsecureJwt = Environment.GetEnvironmentVariable(KAFKA_ENABLE_SASL_OAUTHBEARER_UNSECURE_JWT);
        switch (enableSaslOauthbearerUnsecureJwt?.ToLowerInvariant())
        {
            case "true":
                clientConfig.EnableSaslOauthbearerUnsecureJwt = true;
                break;
            case "false":
                clientConfig.EnableSaslOauthbearerUnsecureJwt = false;
                break;
            default:
                break;
        }

        var saslPassword = Environment.GetEnvironmentVariable(KAFKA_SASL_PASSWORD);
        if(!string.IsNullOrEmpty(saslPassword)) clientConfig.SaslPassword = saslPassword;

        var saslUsername = Environment.GetEnvironmentVariable(KAFKA_SASL_USERNAME);
        if(!string.IsNullOrEmpty(saslUsername)) clientConfig.SaslUsername = saslUsername;

        var saslKerberosMinTimeBeforeRelogin = Environment.GetEnvironmentVariable(KAFKA_SASL_KERBEROS_MIN_TIME_BEFORE_RELOGIN);
        if(!string.IsNullOrEmpty(saslKerberosMinTimeBeforeRelogin)) clientConfig.SaslKerberosMinTimeBeforeRelogin = int.Parse(saslKerberosMinTimeBeforeRelogin);

        var saslKerberosKeytab = Environment.GetEnvironmentVariable(KAFKA_SASL_KERBEROS_KEYTAB);
        if(!string.IsNullOrEmpty(saslKerberosKeytab)) clientConfig.SaslKerberosKeytab = saslKerberosKeytab;

        var saslKerberosKinitCmd = Environment.GetEnvironmentVariable(KAFKA_SASL_KERBEROS_KINIT_CMD);
        if(!string.IsNullOrEmpty(saslKerberosKinitCmd)) clientConfig.SaslKerberosKinitCmd = saslKerberosKinitCmd;

        var saslKerberosPrincipal = Environment.GetEnvironmentVariable(KAFKA_SASL_KERBEROS_PRINCIPAL);
        if(!string.IsNullOrEmpty(saslKerberosPrincipal)) clientConfig.SaslKerberosPrincipal = saslKerberosPrincipal;

        var saslKerberosServiceName = Environment.GetEnvironmentVariable(KAFKA_SASL_KERBEROS_SERVICE_NAME);
        if(!string.IsNullOrEmpty(saslKerberosServiceName)) clientConfig.SaslKerberosServiceName = saslKerberosServiceName;

        var saslOauthbearerConfig = Environment.GetEnvironmentVariable(KAFKA_SASL_OAUTHBEARER_CONFIG);
        if(!string.IsNullOrEmpty(saslOauthbearerConfig)) clientConfig.SaslOauthbearerConfig = saslOauthbearerConfig;

        var allowAutoCreateTopics = Environment.GetEnvironmentVariable(KAFKA_ALLOW_AUTO_CREATE_TOPICS_VALUE);
        switch (allowAutoCreateTopics?.ToLowerInvariant())
        {
            case "true":
                clientConfig.AllowAutoCreateTopics = true;
                break;
            case "false":
                clientConfig.AllowAutoCreateTopics = false;
                break;
            default:
                break;
        }

        var brokerVersionFallback = Environment.GetEnvironmentVariable(KAFKA_BROKER_VERSION_FALLBACK);
        if(!string.IsNullOrEmpty(brokerVersionFallback)) clientConfig.BrokerVersionFallback = brokerVersionFallback;

        var apiVersionFallbackMs = Environment.GetEnvironmentVariable(KAFKA_API_VERSION_FALLBACK_MS);
        if(!string.IsNullOrEmpty(apiVersionFallbackMs)) clientConfig.ApiVersionFallbackMs = int.Parse(apiVersionFallbackMs);

        var debug = Environment.GetEnvironmentVariable(KAFKA_DEBUG);
        if(!string.IsNullOrEmpty(debug)) clientConfig.Debug = debug;

        var topicBlacklist = Environment.GetEnvironmentVariable(KAFKA_TOPIC_BLACKLIST);
        if(!string.IsNullOrEmpty(topicBlacklist)) clientConfig.TopicBlacklist = topicBlacklist;

        var topicMetadataPropagationMaxMs = Environment.GetEnvironmentVariable(KAFKA_TOPIC_METADATA_PROPAGATION_MAX_MS);
        if(!string.IsNullOrEmpty(topicMetadataPropagationMaxMs)) clientConfig.TopicMetadataPropagationMaxMs = int.Parse(topicMetadataPropagationMaxMs);

        var topicMetadataRefreshSparse = Environment.GetEnvironmentVariable(KAFKA_TOPIC_METADATA_REFRESH_SPARSE);
        switch (topicMetadataRefreshSparse?.ToLowerInvariant())
        {
            case "true":
                clientConfig.TopicMetadataRefreshSparse = true;
                break;
            case "false":
                clientConfig.TopicMetadataRefreshSparse = false;
                break;
            default:
                break;
        }

        var topicMetadataRefreshFastIntervalMs = Environment.GetEnvironmentVariable(KAFKA_TOPIC_METADATA_REFRESH_FAST_INTERVAL_MS);
        if(!string.IsNullOrEmpty(topicMetadataRefreshFastIntervalMs)) clientConfig.TopicMetadataRefreshFastIntervalMs = int.Parse(topicMetadataRefreshFastIntervalMs);

        var metadataMaxAgeMs = Environment.GetEnvironmentVariable(KAFKA_METADATA_MAX_AGE_MS);
        if(!string.IsNullOrEmpty(metadataMaxAgeMs)) clientConfig.MetadataMaxAgeMs = int.Parse(metadataMaxAgeMs);

        var topicMetadataRefreshIntervalMs = Environment.GetEnvironmentVariable(KAFKA_TOPIC_METADATA_REFRESH_INTERVAL_MS);
        if(!string.IsNullOrEmpty(topicMetadataRefreshIntervalMs)) clientConfig.TopicMetadataRefreshIntervalMs = int.Parse(topicMetadataRefreshIntervalMs);

        var maxInFlight = Environment.GetEnvironmentVariable(KAFKA_MAX_IN_FLIGHT);
        if(!string.IsNullOrEmpty(maxInFlight)) clientConfig.MaxInFlight = int.Parse(maxInFlight);

        var receiveMessageMaxBytes = Environment.GetEnvironmentVariable(KAFKA_RECEIVE_MESSAGE_MAX_BYTES);
        if(!string.IsNullOrEmpty(receiveMessageMaxBytes)) clientConfig.ReceiveMessageMaxBytes = int.Parse(receiveMessageMaxBytes);

        var messageCopyMaxBytes = Environment.GetEnvironmentVariable(KAFKA_MESSAGE_COPY_MAX_BYTES);
        if(!string.IsNullOrEmpty(messageCopyMaxBytes)) clientConfig.MessageCopyMaxBytes = int.Parse(messageCopyMaxBytes);

        var messageMaxBytes = Environment.GetEnvironmentVariable(KAFKA_MESSAGE_MAX_BYTES);
        if(!string.IsNullOrEmpty(messageMaxBytes)) clientConfig.MessageMaxBytes = int.Parse(messageMaxBytes);

        var bootstrapServers = Environment.GetEnvironmentVariable(KAFKA_BOOTSTRAP_SERVERS);
        if(!string.IsNullOrEmpty(bootstrapServers)) clientConfig.BootstrapServers = bootstrapServers;

        var clientId = Environment.GetEnvironmentVariable(KAFKA_CLIENT_ID);
        if(!string.IsNullOrEmpty(clientId)) clientConfig.ClientId = clientId;

        var acks = Environment.GetEnvironmentVariable(KAFKA_ACKS);
        switch (acks?.ToLowerInvariant())
        {
            case "all":
                clientConfig.Acks = Confluent.Kafka.Acks.All;
                break;
            case "leader":
                clientConfig.Acks = Confluent.Kafka.Acks.Leader;
                break;
            case "none":
                clientConfig.Acks = Confluent.Kafka.Acks.None;
                break;
            default:
                break;
        }

        var saslMechanism = Environment.GetEnvironmentVariable(KAFKA_SASL_MECHANISM);
        switch (saslMechanism?.ToLowerInvariant())
        {
            case "gssapi":
                clientConfig.SaslMechanism = Confluent.Kafka.SaslMechanism.Gssapi;
                break;
            case "oauthbearer":
                clientConfig.SaslMechanism = Confluent.Kafka.SaslMechanism.OAuthBearer;
                break;
            case "plain":
                clientConfig.SaslMechanism = Confluent.Kafka.SaslMechanism.Plain;
                break;
            case "scramsha256":
                clientConfig.SaslMechanism = Confluent.Kafka.SaslMechanism.ScramSha256;
                break;
            case "scramsha512":
                clientConfig.SaslMechanism = Confluent.Kafka.SaslMechanism.ScramSha512;
                break;
            default:
                break;
        }

        var socketTimeoutMs = Environment.GetEnvironmentVariable(KAFKA_SOCKET_TIMEOUT_MS);
        if(!string.IsNullOrEmpty(socketTimeoutMs)) clientConfig.SocketTimeoutMs = int.Parse(socketTimeoutMs);

        var socketSendBufferBytes = Environment.GetEnvironmentVariable(KAFKA_SOCKET_SEND_BUFFER_BYTES);
        if(!string.IsNullOrEmpty(socketSendBufferBytes)) clientConfig.SocketSendBufferBytes = int.Parse(socketSendBufferBytes);

        var socketReceiveBufferBytes = Environment.GetEnvironmentVariable(KAFKA_SOCKET_RECEIVE_BUFFER_BYTES);
        if(!string.IsNullOrEmpty(socketReceiveBufferBytes)) clientConfig.SocketReceiveBufferBytes = int.Parse(socketReceiveBufferBytes);

        var socketKeepaliveEnable = Environment.GetEnvironmentVariable(KAFKA_SOCKET_KEEPALIVE_ENABLE);
        switch (socketKeepaliveEnable?.ToLowerInvariant())
        {
            case "true":
                clientConfig.SocketKeepaliveEnable = true;
                break;
            case "false":
                clientConfig.SocketKeepaliveEnable = false;
                break;
            default:
                break;
        }

        var apiVersionRequestTimeoutMs = Environment.GetEnvironmentVariable(KAFKA_API_VERSION_REQUEST_TIMEOUT_MS);
        if(!string.IsNullOrEmpty(apiVersionRequestTimeoutMs)) clientConfig.ApiVersionRequestTimeoutMs = int.Parse(apiVersionRequestTimeoutMs);

        var apiVersionRequest = Environment.GetEnvironmentVariable(KAFKA_API_VERSION_REQUEST);
        switch (apiVersionRequest?.ToLowerInvariant())
        {
            case "true":
                clientConfig.ApiVersionRequest = true;
                break;
            case "false":
                clientConfig.ApiVersionRequest = false;
                break;
            default:
                break;
        }

        var internalTerminationSignal = Environment.GetEnvironmentVariable(KAFKA_INTERNAL_TERMINATION_SIGNAL);
        if(!string.IsNullOrEmpty(internalTerminationSignal)) clientConfig.InternalTerminationSignal = int.Parse(internalTerminationSignal);

        var logConnectionClose = Environment.GetEnvironmentVariable(KAFKA_LOG_CONNECTION_CLOSE);
        switch (logConnectionClose?.ToLowerInvariant())
        {
            case "true":
                clientConfig.LogConnectionClose = true;
                break;
            case "false":
                clientConfig.LogConnectionClose = false;
                break;
            default:
                break;
        }

        var enableRandomSeed = Environment.GetEnvironmentVariable(KAFKA_ENABLE_RANDOM_SEED);
        switch (enableRandomSeed?.ToLowerInvariant())
        {
            case "true":
                clientConfig.EnableRandomSeed = true;
                break;
            case "false":
                clientConfig.EnableRandomSeed = false;
                break;
            default:
                break;
        }

        var logThreadName = Environment.GetEnvironmentVariable(KAFKA_LOG_THREAD_NAME);
        switch (logThreadName?.ToLowerInvariant())
        {
            case "true":
                clientConfig.LogThreadName = true;
                break;
            case "false":
                clientConfig.LogThreadName = false;
                break;
            default:
                break;
        }

        var logQueue = Environment.GetEnvironmentVariable(KAFKA_LOG_QUEUE);
        switch (logQueue?.ToLowerInvariant())
        {
            case "true":
                clientConfig.LogQueue = true;
                break;
            case "false":
                clientConfig.LogQueue = false;
                break;
            default:
                break;
        }

        var clientRack = Environment.GetEnvironmentVariable(KAFKA_CLIENT_RACK);
        if(!string.IsNullOrEmpty(clientRack)) clientConfig.ClientRack = clientRack;

        var statisticsIntervalMs = Environment.GetEnvironmentVariable(KAFKA_STATISTICS_INTERVAL_MS);
        if(!string.IsNullOrEmpty(statisticsIntervalMs)) clientConfig.StatisticsIntervalMs = int.Parse(statisticsIntervalMs);

        var reconnectBackoffMs = Environment.GetEnvironmentVariable(KAFKA_RECONNECT_BACKOFF_MS);
        if(!string.IsNullOrEmpty(reconnectBackoffMs)) clientConfig.ReconnectBackoffMs = int.Parse(reconnectBackoffMs);

        var connectionsMaxIdleMs = Environment.GetEnvironmentVariable(KAFKA_CONNECTIONS_MAX_IDLE_MS);
        if(!string.IsNullOrEmpty(connectionsMaxIdleMs)) clientConfig.ConnectionsMaxIdleMs = int.Parse(connectionsMaxIdleMs);

        var socketConnectionSetupTimeoutMs = Environment.GetEnvironmentVariable(KAFKA_SOCKET_CONNECTION_SETUP_TIMEOUT_MS);
        if(!string.IsNullOrEmpty(socketConnectionSetupTimeoutMs)) clientConfig.SocketConnectionSetupTimeoutMs = int.Parse(socketConnectionSetupTimeoutMs);

        var brokerAddressFamily = Environment.GetEnvironmentVariable(KAFKA_BROKER_ADDRESS_FAMILY);
        switch (brokerAddressFamily?.ToLowerInvariant())
        {
            case "any":
                clientConfig.BrokerAddressFamily = Confluent.Kafka.BrokerAddressFamily.Any;
                break;
            case "v4":
                clientConfig.BrokerAddressFamily = Confluent.Kafka.BrokerAddressFamily.V4;
                break;
            case "v6":
                clientConfig.BrokerAddressFamily = Confluent.Kafka.BrokerAddressFamily.V6;
                break;
            default:
                break;
        }

        var brokerAddressTtl = Environment.GetEnvironmentVariable(KAFKA_BROKER_ADDRESS_TTL);
        if(!string.IsNullOrEmpty(brokerAddressTtl)) clientConfig.BrokerAddressTtl = int.Parse(brokerAddressTtl);

        var socketMaxFails = Environment.GetEnvironmentVariable(KAFKA_SOCKET_MAX_FAILS);
        if(!string.IsNullOrEmpty(socketMaxFails)) clientConfig.SocketMaxFails = int.Parse(socketMaxFails);

        var socketNagleDisable = Environment.GetEnvironmentVariable(KAFKA_SOCKET_NAGLE_DISABLE);
        switch (socketNagleDisable?.ToLowerInvariant())
        {
            case "true":
                clientConfig.SocketNagleDisable = true;
                break;
            case "false":
                clientConfig.SocketNagleDisable = false;
                break;
            default:
                break;
        }

        var reconnectBackoffMaxMs = Environment.GetEnvironmentVariable(KAFKA_RECONNECT_BACKOFF_MAX_MS);
        if(!string.IsNullOrEmpty(reconnectBackoffMaxMs)) clientConfig.ReconnectBackoffMaxMs = int.Parse(reconnectBackoffMaxMs);

        var clientDnsLookup = Environment.GetEnvironmentVariable(KAFKA_CLIENT_DNS_LOOKUP);
        switch (clientDnsLookup?.ToLowerInvariant())
        {
            case "resolvecanonicalbootstrapserversonly":
                clientConfig.ClientDnsLookup = Confluent.Kafka.ClientDnsLookup.ResolveCanonicalBootstrapServersOnly;
                break;
            case "usealldnsips":
                clientConfig.ClientDnsLookup = Confluent.Kafka.ClientDnsLookup.UseAllDnsIps;
                break;
            default:
                break;
        }

        return clientConfig;
    }

    public const string KAFKA_ACKS = nameof(KAFKA_ACKS);
    public const string KAFKA_ALLOW_AUTO_CREATE_TOPICS_VALUE = nameof(KAFKA_ALLOW_AUTO_CREATE_TOPICS_VALUE);
    public const string KAFKA_API_VERSION_FALLBACK_MS = nameof(KAFKA_API_VERSION_FALLBACK_MS);
    public const string KAFKA_API_VERSION_REQUEST = nameof(KAFKA_API_VERSION_REQUEST);
    public const string KAFKA_API_VERSION_REQUEST_TIMEOUT_MS = nameof(KAFKA_API_VERSION_REQUEST_TIMEOUT_MS);
    public const string KAFKA_BOOTSTRAP_SERVERS = nameof(KAFKA_BOOTSTRAP_SERVERS);
    public const string KAFKA_BROKER_ADDRESS_FAMILY = nameof(KAFKA_BROKER_ADDRESS_FAMILY);
    public const string KAFKA_BROKER_ADDRESS_TTL = nameof(KAFKA_BROKER_ADDRESS_TTL);
    public const string KAFKA_BROKER_VERSION_FALLBACK = nameof(KAFKA_BROKER_VERSION_FALLBACK);
    public const string KAFKA_CLIENT_DNS_LOOKUP = nameof(KAFKA_CLIENT_DNS_LOOKUP);
    public const string KAFKA_CLIENT_ID = nameof(KAFKA_CLIENT_ID);
    public const string KAFKA_CLIENT_RACK = nameof(KAFKA_CLIENT_RACK);
    public const string KAFKA_CONNECTIONS_MAX_IDLE_MS = nameof(KAFKA_CONNECTIONS_MAX_IDLE_MS);
    public const string KAFKA_DEBUG = nameof(KAFKA_DEBUG);
    public const string KAFKA_ENABLE_RANDOM_SEED = nameof(KAFKA_ENABLE_RANDOM_SEED);
    public const string KAFKA_ENABLE_SASL_OAUTHBEARER_UNSECURE_JWT = nameof(KAFKA_ENABLE_SASL_OAUTHBEARER_UNSECURE_JWT);
    public const string KAFKA_ENABLE_SSL_CERTIFICATE_VERIFICATION = nameof(KAFKA_ENABLE_SSL_CERTIFICATE_VERIFICATION);
    public const string KAFKA_INTERNAL_TERMINATION_SIGNAL = nameof(KAFKA_INTERNAL_TERMINATION_SIGNAL);
    public const string KAFKA_LOG_CONNECTION_CLOSE = nameof(KAFKA_LOG_CONNECTION_CLOSE);
    public const string KAFKA_LOG_QUEUE = nameof(KAFKA_LOG_QUEUE);
    public const string KAFKA_LOG_THREAD_NAME = nameof(KAFKA_LOG_THREAD_NAME);
    public const string KAFKA_MAX_IN_FLIGHT = nameof(KAFKA_MAX_IN_FLIGHT);
    public const string KAFKA_MESSAGE_COPY_MAX_BYTES = nameof(KAFKA_MESSAGE_COPY_MAX_BYTES);
    public const string KAFKA_MESSAGE_MAX_BYTES = nameof(KAFKA_MESSAGE_MAX_BYTES);
    public const string KAFKA_METADATA_MAX_AGE_MS = nameof(KAFKA_METADATA_MAX_AGE_MS);
    public const string KAFKA_PLUGIN_LIBRARY_PATHS = nameof(KAFKA_PLUGIN_LIBRARY_PATHS);
    public const string KAFKA_RECEIVE_MESSAGE_MAX_BYTES = nameof(KAFKA_RECEIVE_MESSAGE_MAX_BYTES);
    public const string KAFKA_RECONNECT_BACKOFF_MAX_MS = nameof(KAFKA_RECONNECT_BACKOFF_MAX_MS);
    public const string KAFKA_RECONNECT_BACKOFF_MS = nameof(KAFKA_RECONNECT_BACKOFF_MS);
    public const string KAFKA_SASL_KERBEROS_KEYTAB = nameof(KAFKA_SASL_KERBEROS_KEYTAB);
    public const string KAFKA_SASL_KERBEROS_KINIT_CMD = nameof(KAFKA_SASL_KERBEROS_KINIT_CMD);
    public const string KAFKA_SASL_KERBEROS_MIN_TIME_BEFORE_RELOGIN = nameof(KAFKA_SASL_KERBEROS_MIN_TIME_BEFORE_RELOGIN);
    public const string KAFKA_SASL_KERBEROS_PRINCIPAL = nameof(KAFKA_SASL_KERBEROS_PRINCIPAL);
    public const string KAFKA_SASL_KERBEROS_SERVICE_NAME = nameof(KAFKA_SASL_KERBEROS_SERVICE_NAME);
    public const string KAFKA_SASL_MECHANISM = nameof(KAFKA_SASL_MECHANISM);
    public const string KAFKA_SASL_OAUTHBEARER_CLIENT_ID = nameof(KAFKA_SASL_OAUTHBEARER_CLIENT_ID);
    public const string KAFKA_SASL_OAUTHBEARER_CLIENT_SECRET = nameof(KAFKA_SASL_OAUTHBEARER_CLIENT_SECRET);
    public const string KAFKA_SASL_OAUTHBEARER_CONFIG = nameof(KAFKA_SASL_OAUTHBEARER_CONFIG);
    public const string KAFKA_SASL_OAUTHBEARER_EXTENSIONS = nameof(KAFKA_SASL_OAUTHBEARER_EXTENSIONS);
    public const string KAFKA_SASL_OAUTHBEARER_METHOD = nameof(KAFKA_SASL_OAUTHBEARER_METHOD);
    public const string KAFKA_SASL_OAUTHBEARER_SCOPE = nameof(KAFKA_SASL_OAUTHBEARER_SCOPE);
    public const string KAFKA_SASL_OAUTHBEARER_TOKEN_ENDPOINT_URL = nameof(KAFKA_SASL_OAUTHBEARER_TOKEN_ENDPOINT_URL);
    public const string KAFKA_SASL_PASSWORD = nameof(KAFKA_SASL_PASSWORD);
    public const string KAFKA_SASL_USERNAME = nameof(KAFKA_SASL_USERNAME);
    public const string KAFKA_SECURITY_PROTOCOL = nameof(KAFKA_SECURITY_PROTOCOL);
    public const string KAFKA_SOCKET_CONNECTION_SETUP_TIMEOUT_MS = nameof(KAFKA_SOCKET_CONNECTION_SETUP_TIMEOUT_MS);
    public const string KAFKA_SOCKET_KEEPALIVE_ENABLE = nameof(KAFKA_SOCKET_KEEPALIVE_ENABLE);
    public const string KAFKA_SOCKET_MAX_FAILS = nameof(KAFKA_SOCKET_MAX_FAILS);
    public const string KAFKA_SOCKET_NAGLE_DISABLE = nameof(KAFKA_SOCKET_NAGLE_DISABLE);
    public const string KAFKA_SOCKET_RECEIVE_BUFFER_BYTES = nameof(KAFKA_SOCKET_RECEIVE_BUFFER_BYTES);
    public const string KAFKA_SOCKET_SEND_BUFFER_BYTES = nameof(KAFKA_SOCKET_SEND_BUFFER_BYTES);
    public const string KAFKA_SOCKET_TIMEOUT_MS = nameof(KAFKA_SOCKET_TIMEOUT_MS);
    public const string KAFKA_SSL_CA_CERTIFICATE_STORES = nameof(KAFKA_SSL_CA_CERTIFICATE_STORES);
    public const string KAFKA_SSL_CA_LOCATION = nameof(KAFKA_SSL_CA_LOCATION);
    public const string KAFKA_SSL_CA_PEM = nameof(KAFKA_SSL_CA_PEM);
    public const string KAFKA_SSL_CA_PEM_LOCATION = nameof(KAFKA_SSL_CA_PEM_LOCATION);
    public const string KAFKA_SSL_CERTIFICATE_LOCATION = nameof(KAFKA_SSL_CERTIFICATE_LOCATION);
    public const string KAFKA_SSL_CERTIFICATE_PEM = nameof(KAFKA_SSL_CERTIFICATE_PEM);
    public const string KAFKA_SSL_CIPHER_SUITES = nameof(KAFKA_SSL_CIPHER_SUITES);
    public const string KAFKA_SSL_CRL_LOCATION = nameof(KAFKA_SSL_CRL_LOCATION);
    public const string KAFKA_SSL_CURVES_LIST = nameof(KAFKA_SSL_CURVES_LIST);
    public const string KAFKA_SSL_ENDPOINT_IDENTIFICATION_ALGORITHM = nameof(KAFKA_SSL_ENDPOINT_IDENTIFICATION_ALGORITHM);
    public const string KAFKA_SSL_ENGINE_ID = nameof(KAFKA_SSL_ENGINE_ID);
    public const string KAFKA_SSL_ENGINE_LOCATION = nameof(KAFKA_SSL_ENGINE_LOCATION);
    public const string KAFKA_SSL_KEY_LOCATION = nameof(KAFKA_SSL_KEY_LOCATION);
    public const string KAFKA_SSL_KEY_PASSWORD = nameof(KAFKA_SSL_KEY_PASSWORD);
    public const string KAFKA_SSL_KEY_PASSWORD_LOCATION = nameof(KAFKA_SSL_KEY_PASSWORD_LOCATION);
    public const string KAFKA_SSL_KEY_PEM = nameof(KAFKA_SSL_KEY_PEM);
    public const string KAFKA_SSL_KEYSTORE_LOCATION = nameof(KAFKA_SSL_KEYSTORE_LOCATION);
    public const string KAFKA_SSL_KEYSTORE_PASSWORD = nameof(KAFKA_SSL_KEYSTORE_PASSWORD);
    public const string KAFKA_SSL_PROVIDERS = nameof(KAFKA_SSL_PROVIDERS);
    public const string KAFKA_SSL_SIGALGS_LIST = nameof(KAFKA_SSL_SIGALGS_LIST);
    public const string KAFKA_STATISTICS_INTERVAL_MS = nameof(KAFKA_STATISTICS_INTERVAL_MS);
    public const string KAFKA_TOPIC_BLACKLIST = nameof(KAFKA_TOPIC_BLACKLIST);
    public const string KAFKA_TOPIC_METADATA_PROPAGATION_MAX_MS = nameof(KAFKA_TOPIC_METADATA_PROPAGATION_MAX_MS);
    public const string KAFKA_TOPIC_METADATA_REFRESH_FAST_INTERVAL_MS = nameof(KAFKA_TOPIC_METADATA_REFRESH_FAST_INTERVAL_MS);
    public const string KAFKA_TOPIC_METADATA_REFRESH_INTERVAL_MS = nameof(KAFKA_TOPIC_METADATA_REFRESH_INTERVAL_MS);
    public const string KAFKA_TOPIC_METADATA_REFRESH_SPARSE = nameof(KAFKA_TOPIC_METADATA_REFRESH_SPARSE);
}
