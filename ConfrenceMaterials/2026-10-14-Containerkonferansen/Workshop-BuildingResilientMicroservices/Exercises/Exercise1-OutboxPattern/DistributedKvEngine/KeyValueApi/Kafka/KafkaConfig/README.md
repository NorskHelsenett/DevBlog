# What

This is a collection of classes for binding Kafka config variables from the environment,
so that you can specify all the dotnet kafka client config settings in your environment,
and call one of the functions here to get the appropriate config object.

For instance set the environment variable `KAFKA_BOOTSTRAP_SERVERS`, and it will appear
in all the configs you create with this code.

Currently supported:

| Config Type | Method |
|-------------|--------|
| [ProducerConfig](https://docs.confluent.io/platform/current/clients/confluent-kafka-dotnet/_site/api/Confluent.Kafka.ProducerConfig.html)                                    | [`KafkaProducerConfigEnvBinder.GetProducerConfig()`](./KafkaProducerConfigEnvBinder.cs#L3)                   |
| [ConsumerConfig](https://docs.confluent.io/platform/current/clients/confluent-kafka-dotnet/_site/api/Confluent.Kafka.ConsumerConfig.html)                                    | [`KafkaConsumerConfigEnvBinder.GetConsumerConfig()`](./KafkaConsumerConfigEnvBinder.cs#L3)                   |
| [SchemaRegistryConfig](https://docs.confluent.io/platform/current/clients/confluent-kafka-dotnet/_site/api/Confluent.SchemaRegistry.SchemaRegistryConfig.PropertyNames.html) | [`KafkaSchemaRegistryConfigEnvBinder.GetSchemaRegistryConfig()`](./KafkaSchemaRegistryConfigEnvBinder.cs#L3) |
| [AdminClientConfig](https://docs.confluent.io/platform/current/clients/confluent-kafka-dotnet/_site/api/Confluent.Kafka.AdminClientConfig.html)                              | [`KafkaAdminClientEnvBinder.GetAdminClientConfig()`](./KafkaAdminClientEnvBinder.cs#L3)                      |
| [ClientConfig](https://docs.confluent.io/platform/current/clients/confluent-kafka-dotnet/_site/api/Confluent.Kafka.ClientConfig.html)                                        | [`KafkaClientConfigEnvBinder.GetClientConfig()`](./KafkaClientConfigEnvBinder.cs#L3)                         |

# Why

Because this way you can allow those that want to set up how the code is going to run
against your kafka cluster to do that, without having to come back to the code to set
up parsing of the specific config variable they suddenly figured out they wanted to use.
Someone wants to set `KAFKA_STICKY_PARTITIONING_LINGER_MS` for the producers where
the code is run? No problem, they can update the environment and restart the pod/container/service,
without you having to do anything further to enable them to do that.

# How

To use this to for instance get the config you need to create a producer based on whats
set up in the environment variables where your code runs, you can do like this:

```cs
var config = KafkaProducerConfigEnvBinder.GetProducerConfig();
var producer = new ProducerBuilder<byte[], byte[]?>(config).Build();
```

# Caveats

Both the `ProducerConfig`, `ConsumerConfig`, and `AdminClientConfig` all inherit form the `ClientConfig`
and share the variables put there. So using this, you might get some weird messages about a certain config
not making sense for a producer for instance.

Effectively, this also means that you cannot do things like connect to two separate clusters using this.

# Links to packages and documentation

The Kafka dotnet client documentation:
https://docs.confluent.io/platform/current/clients/confluent-kafka-dotnet/_site/api/Confluent.Kafka.html

The nuget packages:
- https://www.nuget.org/packages/Confluent.Kafka
    - ProducerConfig
    - ConsumerConfig
    - AdminClientConfig
    - ClientConfig
- https://www.nuget.org/packages/Confluent.SchemaRegistry
    - SchemaRegistryConfig

# Supported variables

| Environment variable name                                 | Passed to|          |             |                |
|-----------------------------------------------------------|----------|----------|-------------|----------------|
| KAFKA_ACKS                                                | Producer | Consumer | AdminClient |                |
| KAFKA_ALLOW_AUTO_CREATE_TOPICS_VALUE                      | Producer | Consumer | AdminClient |                |
| KAFKA_API_VERSION_FALLBACK_MS                             | Producer | Consumer | AdminClient |                |
| KAFKA_API_VERSION_REQUEST                                 | Producer | Consumer | AdminClient |                |
| KAFKA_API_VERSION_REQUEST_TIMEOUT_MS                      | Producer | Consumer | AdminClient |                |
| KAFKA_AUTO_COMMIT_INTERVAL_MS                             |          | Consumer |             |                |
| KAFKA_AUTO_OFFSET_RESET                                   |          | Consumer |             |                |
| KAFKA_BATCH_NUM_MESSAGES                                  | Producer |          |             |                |
| KAFKA_BATCH_SIZE                                          | Producer |          |             |                |
| KAFKA_BOOTSTRAP_SERVERS                                   | Producer | Consumer | AdminClient |                |
| KAFKA_BROKER_ADDRESS_FAMILY                               | Producer | Consumer | AdminClient |                |
| KAFKA_BROKER_ADDRESS_TTL                                  | Producer | Consumer | AdminClient |                |
| KAFKA_BROKER_VERSION_FALLBACK                             | Producer | Consumer | AdminClient |                |
| KAFKA_CHECK_CRCS                                          |          | Consumer |             |                |
| KAFKA_CLIENT_DNS_LOOKUP                                   | Producer | Consumer | AdminClient |                |
| KAFKA_CLIENT_ID                                           | Producer | Consumer | AdminClient |                |
| KAFKA_CLIENT_RACK                                         | Producer | Consumer | AdminClient |                |
| KAFKA_COMPRESSION_LEVEL                                   | Producer |          |             |                |
| KAFKA_COMPRESSION_TYPE                                    | Producer |          |             |                |
| KAFKA_CONNECTIONS_MAX_IDLE_MS                             | Producer | Consumer | AdminClient |                |
| KAFKA_CONSUME_RESULT_FIELDS                               |          | Consumer |             |                |
| KAFKA_COORDINATOR_QUERY_INTERVAL_MS                       |          | Consumer |             |                |
| KAFKA_DEBUG                                               | Producer | Consumer | AdminClient |                |
| KAFKA_DELIVERY_REPORT_FIELDS                              | Producer |          |             |                |
| KAFKA_ENABLE_AUTO_COMMIT                                  |          | Consumer |             |                |
| KAFKA_ENABLE_AUTO_OFFSET_STORE                            |          | Consumer |             |                |
| KAFKA_ENABLE_BACKGROUND_POLL                              | Producer |          |             |                |
| KAFKA_ENABLE_DELIVERY_REPORTS                             | Producer |          |             |                |
| KAFKA_ENABLE_GAPLESS_GUARANTEE                            | Producer |          |             |                |
| KAFKA_ENABLE_IDEMPOTENCE                                  | Producer |          |             |                |
| KAFKA_ENABLE_PARTITION_EOF                                |          | Consumer |             |                |
| KAFKA_ENABLE_RANDOM_SEED                                  | Producer | Consumer | AdminClient |                |
| KAFKA_ENABLE_SASL_OAUTHBEARER_UNSECURE_JWT                | Producer | Consumer | AdminClient |                |
| KAFKA_ENABLE_SSL_CERTIFICATE_VERIFICATION                 | Producer | Consumer | AdminClient |                |
| KAFKA_FETCH_ERROR_BACKOFF_MS                              |          | Consumer |             |                |
| KAFKA_FETCH_MAX_BYTES                                     |          | Consumer |             |                |
| KAFKA_FETCH_MIN_BYTES                                     |          | Consumer |             |                |
| KAFKA_FETCH_QUEUE_BACKOFF_MS                              |          | Consumer |             |                |
| KAFKA_FETCH_WAIT_MAX_MS                                   |          | Consumer |             |                |
| KAFKA_GROUP_ID                                            |          | Consumer |             |                |
| KAFKA_GROUP_INSTANCE_ID                                   |          | Consumer |             |                |
| KAFKA_GROUP_PROTOCOL_TYPE                                 |          | Consumer |             |                |
| KAFKA_HEARTBEAT_INTERVAL_MS                               |          | Consumer |             |                |
| KAFKA_INTERNAL_TERMINATION_SIGNAL                         | Producer | Consumer | AdminClient |                |
| KAFKA_ISOLATION_LEVEL                                     |          | Consumer |             |                |
| KAFKA_LINGER_MS                                           | Producer |          |             |                |
| KAFKA_LOG_CONNECTION_CLOSE                                | Producer | Consumer | AdminClient |                |
| KAFKA_LOG_QUEUE                                           | Producer | Consumer | AdminClient |                |
| KAFKA_LOG_THREAD_NAME                                     | Producer | Consumer | AdminClient |                |
| KAFKA_MAX_IN_FLIGHT                                       | Producer | Consumer | AdminClient |                |
| KAFKA_MAX_PARTITION_FETCH_BYTES                           |          | Consumer |             |                |
| KAFKA_MAX_POLL_INTERVAL_MS                                |          | Consumer |             |                |
| KAFKA_MESSAGE_COPY_MAX_BYTES                              | Producer | Consumer | AdminClient |                |
| KAFKA_MESSAGE_MAX_BYTES                                   | Producer | Consumer | AdminClient |                |
| KAFKA_MESSAGE_SEND_MAX_RETRIES                            | Producer |          |             |                |
| KAFKA_MESSAGE_TIMEOUT_MS                                  | Producer |          |             |                |
| KAFKA_METADATA_MAX_AGE_MS                                 | Producer | Consumer | AdminClient |                |
| KAFKA_PARTITION_ASSIGNMENT_STRATEGY                       |          | Consumer |             |                |
| KAFKA_PARTITIONER                                         | Producer |          |             |                |
| KAFKA_PLUGIN_LIBRARY_PATHS                                | Producer | Consumer | AdminClient |                |
| KAFKA_QUEUE_BUFFERING_BACKPRESSURE_THRESHOLD              | Producer |          |             |                |
| KAFKA_QUEUE_BUFFERING_MAX_KBYTES                          | Producer |          |             |                |
| KAFKA_QUEUE_BUFFERING_MAX_MESSAGES                        | Producer |          |             |                |
| KAFKA_QUEUED_MAX_MESSAGES_KBYTES                          |          | Consumer |             |                |
| KAFKA_QUEUED_MIN_MESSAGES                                 |          | Consumer |             |                |
| KAFKA_RECEIVE_MESSAGE_MAX_BYTES                           | Producer | Consumer | AdminClient |                |
| KAFKA_RECONNECT_BACKOFF_MAX_MS                            | Producer | Consumer | AdminClient |                |
| KAFKA_RECONNECT_BACKOFF_MS                                | Producer | Consumer | AdminClient |                |
| KAFKA_REQUEST_TIMEOUT_MS                                  | Producer |          |             |                |
| KAFKA_RETRY_BACKOFF_MS                                    | Producer |          |             |                |
| KAFKA_SASL_KERBEROS_KEYTAB                                | Producer | Consumer | AdminClient |                |
| KAFKA_SASL_KERBEROS_KINIT_CMD                             | Producer | Consumer | AdminClient |                |
| KAFKA_SASL_KERBEROS_MIN_TIME_BEFORE_RELOGIN               | Producer | Consumer | AdminClient |                |
| KAFKA_SASL_KERBEROS_PRINCIPAL                             | Producer | Consumer | AdminClient |                |
| KAFKA_SASL_KERBEROS_SERVICE_NAME                          | Producer | Consumer | AdminClient |                |
| KAFKA_SASL_MECHANISM                                      | Producer | Consumer | AdminClient |                |
| KAFKA_SASL_OAUTHBEARER_CLIENT_ID                          | Producer | Consumer | AdminClient |                |
| KAFKA_SASL_OAUTHBEARER_CLIENT_SECRET                      | Producer | Consumer | AdminClient |                |
| KAFKA_SASL_OAUTHBEARER_CONFIG                             | Producer | Consumer | AdminClient |                |
| KAFKA_SASL_OAUTHBEARER_EXTENSIONS                         | Producer | Consumer | AdminClient |                |
| KAFKA_SASL_OAUTHBEARER_METHOD                             | Producer | Consumer | AdminClient |                |
| KAFKA_SASL_OAUTHBEARER_SCOPE                              | Producer | Consumer | AdminClient |                |
| KAFKA_SASL_OAUTHBEARER_TOKEN_ENDPOINT_URL                 | Producer | Consumer | AdminClient |                |
| KAFKA_SASL_PASSWORD                                       | Producer | Consumer | AdminClient |                |
| KAFKA_SASL_USERNAME                                       | Producer | Consumer | AdminClient |                |
| KAFKA_SCHEMA_REGISTRY_BASIC_AUTH_CREDENTIALS_SOURCE       |          |          |             | SchemaRegistry |
| KAFKA_SCHEMA_REGISTRY_BASIC_AUTH_USER_INFO                |          |          |             | SchemaRegistry |
| KAFKA_SCHEMA_REGISTRY_ENABLE_SSL_CERTIFICATE_VERIFICATION |          |          |             | SchemaRegistry |
| KAFKA_SCHEMA_REGISTRY_MAX_CACHED_SCHEMAS                  |          |          |             | SchemaRegistry |
| KAFKA_SCHEMA_REGISTRY_REQUEST_TIMEOUT_MS                  |          |          |             | SchemaRegistry |
| KAFKA_SCHEMA_REGISTRY_SSL_CA_LOCATION                     |          |          |             | SchemaRegistry |
| KAFKA_SCHEMA_REGISTRY_SSL_KEYSTORE_LOCATION               |          |          |             | SchemaRegistry |
| KAFKA_SCHEMA_REGISTRY_SSL_KEYSTORE_PASSWORD               |          |          |             | SchemaRegistry |
| KAFKA_SCHEMA_REGISTRY_URL                                 |          |          |             | SchemaRegistry |
| KAFKA_SECURITY_PROTOCOL                                   | Producer | Consumer | AdminClient |                |
| KAFKA_SESSION_TIMEOUT_MS                                  |          | Consumer |             |                |
| KAFKA_SOCKET_CONNECTION_SETUP_TIMEOUT_MS                  | Producer | Consumer | AdminClient |                |
| KAFKA_SOCKET_KEEPALIVE_ENABLE                             | Producer | Consumer | AdminClient |                |
| KAFKA_SOCKET_MAX_FAILS                                    | Producer | Consumer | AdminClient |                |
| KAFKA_SOCKET_NAGLE_DISABLE                                | Producer | Consumer | AdminClient |                |
| KAFKA_SOCKET_RECEIVE_BUFFER_BYTES                         | Producer | Consumer | AdminClient |                |
| KAFKA_SOCKET_SEND_BUFFER_BYTES                            | Producer | Consumer | AdminClient |                |
| KAFKA_SOCKET_TIMEOUT_MS                                   | Producer | Consumer | AdminClient |                |
| KAFKA_SSL_CA_CERTIFICATE_STORES                           | Producer | Consumer | AdminClient |                |
| KAFKA_SSL_CA_LOCATION                                     | Producer | Consumer | AdminClient |                |
| KAFKA_SSL_CA_PEM                                          | Producer | Consumer | AdminClient |                |
| KAFKA_SSL_CA_PEM_LOCATION                                 | Producer | Consumer | AdminClient |                |
| KAFKA_SSL_CERTIFICATE_LOCATION                            | Producer | Consumer | AdminClient |                |
| KAFKA_SSL_CERTIFICATE_PEM                                 | Producer | Consumer | AdminClient |                |
| KAFKA_SSL_CIPHER_SUITES                                   | Producer | Consumer | AdminClient |                |
| KAFKA_SSL_CRL_LOCATION                                    | Producer | Consumer | AdminClient |                |
| KAFKA_SSL_CURVES_LIST                                     | Producer | Consumer | AdminClient |                |
| KAFKA_SSL_ENDPOINT_IDENTIFICATION_ALGORITHM               | Producer | Consumer | AdminClient |                |
| KAFKA_SSL_ENGINE_ID                                       | Producer | Consumer | AdminClient |                |
| KAFKA_SSL_ENGINE_LOCATION                                 | Producer | Consumer | AdminClient |                |
| KAFKA_SSL_KEY_LOCATION                                    | Producer | Consumer | AdminClient |                |
| KAFKA_SSL_KEY_PASSWORD                                    | Producer | Consumer | AdminClient |                |
| KAFKA_SSL_KEY_PASSWORD_LOCATION                           | Producer | Consumer | AdminClient |                |
| KAFKA_SSL_KEY_PEM                                         | Producer | Consumer | AdminClient |                |
| KAFKA_SSL_KEYSTORE_LOCATION                               | Producer | Consumer | AdminClient |                |
| KAFKA_SSL_KEYSTORE_PASSWORD                               | Producer | Consumer | AdminClient |                |
| KAFKA_SSL_PROVIDERS                                       | Producer | Consumer | AdminClient |                |
| KAFKA_SSL_SIGALGS_LIST                                    | Producer | Consumer | AdminClient |                |
| KAFKA_STATISTICS_INTERVAL_MS                              | Producer | Consumer | AdminClient |                |
| KAFKA_STICKY_PARTITIONING_LINGER_MS                       | Producer |          |             |                |
| KAFKA_TOPIC_BLACKLIST                                     | Producer | Consumer | AdminClient |                |
| KAFKA_TOPIC_METADATA_PROPAGATION_MAX_MS                   | Producer | Consumer | AdminClient |                |
| KAFKA_TOPIC_METADATA_REFRESH_FAST_INTERVAL_MS             | Producer | Consumer | AdminClient |                |
| KAFKA_TOPIC_METADATA_REFRESH_INTERVAL_MS                  | Producer | Consumer | AdminClient |                |
| KAFKA_TOPIC_METADATA_REFRESH_SPARSE                       | Producer | Consumer | AdminClient |                |
| KAFKA_TRANSACTION_TIMEOUT_MS                              | Producer |          |             |                |
| KAFKA_TRANSACTIONAL_ID                                    | Producer |          |             |                |
