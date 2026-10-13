public static class KafkaProducerConfigEnvBinder
{
    public static Confluent.Kafka.ProducerConfig GetProducerConfig()
    {
        var clientConfig = KafkaClientConfigEnvBinder.GetClientConfig();
        var producerConfig = new Confluent.Kafka.ProducerConfig(clientConfig);

        var batchNumMessages = Environment.GetEnvironmentVariable(KAFKA_BATCH_NUM_MESSAGES);
        if(!string.IsNullOrEmpty(batchNumMessages)) producerConfig.BatchNumMessages = int.Parse(batchNumMessages);

        var compressionType = Environment.GetEnvironmentVariable(KAFKA_COMPRESSION_TYPE);
        switch (compressionType?.ToLowerInvariant())
        {
            case "gzip":
                producerConfig.CompressionType = Confluent.Kafka.CompressionType.Gzip;
                break;
            case "lz4":
                producerConfig.CompressionType = Confluent.Kafka.CompressionType.Lz4;
                break;
            case "none":
                producerConfig.CompressionType = Confluent.Kafka.CompressionType.None;
                break;
            case "snappy":
                producerConfig.CompressionType = Confluent.Kafka.CompressionType.Snappy;
                break;
            case "zstd":
                producerConfig.CompressionType = Confluent.Kafka.CompressionType.Zstd;
                break;
            default:
                break;
        }

        var queueBufferingBackpressureThreshold = Environment.GetEnvironmentVariable(KAFKA_QUEUE_BUFFERING_BACKPRESSURE_THRESHOLD);
        if(!string.IsNullOrEmpty(queueBufferingBackpressureThreshold)) producerConfig.QueueBufferingBackpressureThreshold = int.Parse(queueBufferingBackpressureThreshold);

        var retryBackoffMs = Environment.GetEnvironmentVariable(KAFKA_RETRY_BACKOFF_MS);
        if(!string.IsNullOrEmpty(retryBackoffMs)) producerConfig.RetryBackoffMs = int.Parse(retryBackoffMs);

        var messageSendMaxRetries = Environment.GetEnvironmentVariable(KAFKA_MESSAGE_SEND_MAX_RETRIES);
        if(!string.IsNullOrEmpty(messageSendMaxRetries)) producerConfig.MessageSendMaxRetries = int.Parse(messageSendMaxRetries);

        var lingerMs = Environment.GetEnvironmentVariable(KAFKA_LINGER_MS);
        if(!string.IsNullOrEmpty(lingerMs)) producerConfig.LingerMs = double.Parse(lingerMs);

        var queueBufferingMaxKbytes = Environment.GetEnvironmentVariable(KAFKA_QUEUE_BUFFERING_MAX_KBYTES);
        if(!string.IsNullOrEmpty(queueBufferingMaxKbytes)) producerConfig.QueueBufferingMaxKbytes = int.Parse(queueBufferingMaxKbytes);

        var queueBufferingMaxMessages = Environment.GetEnvironmentVariable(KAFKA_QUEUE_BUFFERING_MAX_MESSAGES);
        if(!string.IsNullOrEmpty(queueBufferingMaxMessages)) producerConfig.QueueBufferingMaxMessages = int.Parse(queueBufferingMaxMessages);

        var enableGaplessGuarantee = Environment.GetEnvironmentVariable(KAFKA_ENABLE_GAPLESS_GUARANTEE);
        switch (enableGaplessGuarantee?.ToLowerInvariant())
        {
            case "true":
                producerConfig.EnableGaplessGuarantee = true;
                break;
            case "false":
                producerConfig.EnableGaplessGuarantee = false;
                break;
            default:
                break;
        }

        var enableIdempotence = Environment.GetEnvironmentVariable(KAFKA_ENABLE_IDEMPOTENCE);
        switch (enableIdempotence?.ToLowerInvariant())
        {
            case "true":
                producerConfig.EnableIdempotence = true;
                break;
            case "false":
                producerConfig.EnableIdempotence = false;
                break;
            default:
                break;
        }

        var transactionTimeoutMs = Environment.GetEnvironmentVariable(KAFKA_TRANSACTION_TIMEOUT_MS);
        if(!string.IsNullOrEmpty(transactionTimeoutMs)) producerConfig.TransactionTimeoutMs = int.Parse(transactionTimeoutMs);

        var transactionalId = Environment.GetEnvironmentVariable(KAFKA_TRANSACTIONAL_ID);
        if(!string.IsNullOrEmpty(transactionalId)) producerConfig.TransactionalId = transactionalId;

        var compressionLevel = Environment.GetEnvironmentVariable(KAFKA_COMPRESSION_LEVEL);
        if(!string.IsNullOrEmpty(compressionLevel)) producerConfig.CompressionLevel = int.Parse(compressionLevel);

        var partitioner = Environment.GetEnvironmentVariable(KAFKA_PARTITIONER);
        switch (partitioner?.ToLowerInvariant())
        {
            case "consistent":
                producerConfig.Partitioner = Confluent.Kafka.Partitioner.Consistent;
                break;
            case "consistentrandom":
                producerConfig.Partitioner = Confluent.Kafka.Partitioner.ConsistentRandom;
                break;
            case "murmur2":
                producerConfig.Partitioner = Confluent.Kafka.Partitioner.Murmur2;
                break;
            case "murmur2random":
                producerConfig.Partitioner = Confluent.Kafka.Partitioner.Murmur2Random;
                break;
            case "random":
                producerConfig.Partitioner = Confluent.Kafka.Partitioner.Random;
                break;
            default:
                break;
        }

        var messageTimeoutMs = Environment.GetEnvironmentVariable(KAFKA_MESSAGE_TIMEOUT_MS);
        if(!string.IsNullOrEmpty(messageTimeoutMs)) producerConfig.MessageTimeoutMs = int.Parse(messageTimeoutMs);

        var requestTimeoutMs = Environment.GetEnvironmentVariable(KAFKA_REQUEST_TIMEOUT_MS);
        if(!string.IsNullOrEmpty(requestTimeoutMs)) producerConfig.RequestTimeoutMs = int.Parse(requestTimeoutMs);

        var deliveryReportFields = Environment.GetEnvironmentVariable(KAFKA_DELIVERY_REPORT_FIELDS);
        if(!string.IsNullOrEmpty(deliveryReportFields)) producerConfig.DeliveryReportFields = deliveryReportFields;

        var enableDeliveryReports = Environment.GetEnvironmentVariable(KAFKA_ENABLE_DELIVERY_REPORTS);
        switch (enableDeliveryReports?.ToLowerInvariant())
        {
            case "true":
                producerConfig.EnableDeliveryReports = true;
                break;
            case "false":
                producerConfig.EnableDeliveryReports = false;
                break;
            default:
                break;
        }

        var enableBackgroundPoll = Environment.GetEnvironmentVariable(KAFKA_ENABLE_BACKGROUND_POLL);
        switch (enableBackgroundPoll?.ToLowerInvariant())
        {
            case "true":
                producerConfig.EnableBackgroundPoll = true;
                break;
            case "false":
                producerConfig.EnableBackgroundPoll = false;
                break;
            default:
                break;
        }

        var batchSize = Environment.GetEnvironmentVariable(KAFKA_BATCH_SIZE);
        if(!string.IsNullOrEmpty(batchSize)) producerConfig.BatchSize = int.Parse(batchSize);

        var stickyPartitioningLingerMs = Environment.GetEnvironmentVariable(KAFKA_STICKY_PARTITIONING_LINGER_MS);
        if(!string.IsNullOrEmpty(stickyPartitioningLingerMs)) producerConfig.StickyPartitioningLingerMs = int.Parse(stickyPartitioningLingerMs);

        return producerConfig;
    }

    // Env var names
    public const string KAFKA_BATCH_NUM_MESSAGES = nameof(KAFKA_BATCH_NUM_MESSAGES);
    public const string KAFKA_BATCH_SIZE = nameof(KAFKA_BATCH_SIZE);
    public const string KAFKA_COMPRESSION_LEVEL = nameof(KAFKA_COMPRESSION_LEVEL);
    public const string KAFKA_COMPRESSION_TYPE = nameof(KAFKA_COMPRESSION_TYPE);
    public const string KAFKA_DELIVERY_REPORT_FIELDS = nameof(KAFKA_DELIVERY_REPORT_FIELDS);
    public const string KAFKA_ENABLE_BACKGROUND_POLL = nameof(KAFKA_ENABLE_BACKGROUND_POLL);
    public const string KAFKA_ENABLE_DELIVERY_REPORTS = nameof(KAFKA_ENABLE_DELIVERY_REPORTS);
    public const string KAFKA_ENABLE_GAPLESS_GUARANTEE = nameof(KAFKA_ENABLE_GAPLESS_GUARANTEE);
    public const string KAFKA_ENABLE_IDEMPOTENCE = nameof(KAFKA_ENABLE_IDEMPOTENCE);
    public const string KAFKA_LINGER_MS = nameof(KAFKA_LINGER_MS);
    public const string KAFKA_MESSAGE_SEND_MAX_RETRIES = nameof(KAFKA_MESSAGE_SEND_MAX_RETRIES);
    public const string KAFKA_MESSAGE_TIMEOUT_MS = nameof(KAFKA_MESSAGE_TIMEOUT_MS);
    public const string KAFKA_PARTITIONER = nameof(KAFKA_PARTITIONER);
    public const string KAFKA_QUEUE_BUFFERING_BACKPRESSURE_THRESHOLD = nameof(KAFKA_QUEUE_BUFFERING_BACKPRESSURE_THRESHOLD);
    public const string KAFKA_QUEUE_BUFFERING_MAX_KBYTES = nameof(KAFKA_QUEUE_BUFFERING_MAX_KBYTES);
    public const string KAFKA_QUEUE_BUFFERING_MAX_MESSAGES = nameof(KAFKA_QUEUE_BUFFERING_MAX_MESSAGES);
    public const string KAFKA_REQUEST_TIMEOUT_MS = nameof(KAFKA_REQUEST_TIMEOUT_MS);
    public const string KAFKA_RETRY_BACKOFF_MS = nameof(KAFKA_RETRY_BACKOFF_MS);
    public const string KAFKA_STICKY_PARTITIONING_LINGER_MS = nameof(KAFKA_STICKY_PARTITIONING_LINGER_MS);
    public const string KAFKA_TRANSACTION_TIMEOUT_MS = nameof(KAFKA_TRANSACTION_TIMEOUT_MS);
    public const string KAFKA_TRANSACTIONAL_ID = nameof(KAFKA_TRANSACTIONAL_ID);
}

