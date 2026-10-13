public static class KafkaConsumerConfigEnvBinder
{
    public static Confluent.Kafka.ConsumerConfig GetConsumerConfig()
    {
        var clientConfig = KafkaClientConfigEnvBinder.GetClientConfig();
        var consumerConfig = new Confluent.Kafka.ConsumerConfig(clientConfig);

        var IsolationLevel = Environment.GetEnvironmentVariable(KAFKA_ISOLATION_LEVEL);
        switch (IsolationLevel?.ToLowerInvariant())
        {
            case "readcommitted":
                consumerConfig.IsolationLevel = Confluent.Kafka.IsolationLevel.ReadCommitted;
                break;
            case "readuncommitted":
                consumerConfig.IsolationLevel = Confluent.Kafka.IsolationLevel.ReadUncommitted;
                break;
            default:
                break;
        }

        var fetchErrorBackoffMs = Environment.GetEnvironmentVariable(KAFKA_FETCH_ERROR_BACKOFF_MS);
        if(!string.IsNullOrEmpty(fetchErrorBackoffMs)) consumerConfig.FetchErrorBackoffMs = int.Parse(fetchErrorBackoffMs);

        var fetchMinBytes = Environment.GetEnvironmentVariable(KAFKA_FETCH_MIN_BYTES);
        if(!string.IsNullOrEmpty(fetchMinBytes)) consumerConfig.FetchMinBytes = int.Parse(fetchMinBytes);

        var fetchMaxBytes = Environment.GetEnvironmentVariable(KAFKA_FETCH_MAX_BYTES);
        if(!string.IsNullOrEmpty(fetchMaxBytes)) consumerConfig.FetchMaxBytes = int.Parse(fetchMaxBytes);

        var maxPartitionFetchBytes = Environment.GetEnvironmentVariable(KAFKA_MAX_PARTITION_FETCH_BYTES);
        if(!string.IsNullOrEmpty(maxPartitionFetchBytes)) consumerConfig.MaxPartitionFetchBytes = int.Parse(maxPartitionFetchBytes);

        var fetchQueueBackoffMs = Environment.GetEnvironmentVariable(KAFKA_FETCH_QUEUE_BACKOFF_MS);
        if(!string.IsNullOrEmpty(fetchQueueBackoffMs)) consumerConfig.FetchQueueBackoffMs = int.Parse(fetchQueueBackoffMs);

        var fetchWaitMaxMs = Environment.GetEnvironmentVariable(KAFKA_FETCH_WAIT_MAX_MS);
        if(!string.IsNullOrEmpty(fetchWaitMaxMs)) consumerConfig.FetchWaitMaxMs = int.Parse(fetchWaitMaxMs);

        var queuedMaxMessagesKbytes = Environment.GetEnvironmentVariable(KAFKA_QUEUED_MAX_MESSAGES_KBYTES);
        if(!string.IsNullOrEmpty(queuedMaxMessagesKbytes)) consumerConfig.QueuedMaxMessagesKbytes = int.Parse(queuedMaxMessagesKbytes);

        var queuedMinMessages = Environment.GetEnvironmentVariable(KAFKA_QUEUED_MIN_MESSAGES);
        if(!string.IsNullOrEmpty(queuedMinMessages)) consumerConfig.QueuedMinMessages = int.Parse(queuedMinMessages);

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

        var autoCommitIntervalMs = Environment.GetEnvironmentVariable(KAFKA_AUTO_COMMIT_INTERVAL_MS);
        if(!string.IsNullOrEmpty(autoCommitIntervalMs)) consumerConfig.AutoCommitIntervalMs = int.Parse(autoCommitIntervalMs);

        var maxPollIntervalMs = Environment.GetEnvironmentVariable(KAFKA_MAX_POLL_INTERVAL_MS);
        if(!string.IsNullOrEmpty(maxPollIntervalMs)) consumerConfig.MaxPollIntervalMs = int.Parse(maxPollIntervalMs);

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

        var coordinatorQueryIntervalMs = Environment.GetEnvironmentVariable(KAFKA_COORDINATOR_QUERY_INTERVAL_MS);
        if(!string.IsNullOrEmpty(coordinatorQueryIntervalMs)) consumerConfig.CoordinatorQueryIntervalMs = int.Parse(coordinatorQueryIntervalMs);

        var groupProtocolType = Environment.GetEnvironmentVariable(KAFKA_GROUP_PROTOCOL_TYPE);
        if(!string.IsNullOrEmpty(groupProtocolType)) consumerConfig.GroupProtocolType = groupProtocolType;

        var heartbeatIntervalMs = Environment.GetEnvironmentVariable(KAFKA_HEARTBEAT_INTERVAL_MS);
        if(!string.IsNullOrEmpty(heartbeatIntervalMs)) consumerConfig.HeartbeatIntervalMs = int.Parse(heartbeatIntervalMs);

        var sessionTimeoutMs = Environment.GetEnvironmentVariable(KAFKA_SESSION_TIMEOUT_MS);
        if(!string.IsNullOrEmpty(sessionTimeoutMs)) consumerConfig.SessionTimeoutMs = int.Parse(sessionTimeoutMs);

        var partitionAssignmentStrategy = Environment.GetEnvironmentVariable(KAFKA_PARTITION_ASSIGNMENT_STRATEGY);
        switch (partitionAssignmentStrategy?.ToLowerInvariant())
        {
            case "cooperativesticky":
                consumerConfig.PartitionAssignmentStrategy = Confluent.Kafka.PartitionAssignmentStrategy.CooperativeSticky;
                break;
            case "range":
                consumerConfig.PartitionAssignmentStrategy = Confluent.Kafka.PartitionAssignmentStrategy.Range;
                break;
            case "roundrobin":
                consumerConfig.PartitionAssignmentStrategy = Confluent.Kafka.PartitionAssignmentStrategy.RoundRobin;
                break;
            default:
                break;
        }

        var groupInstanceId = Environment.GetEnvironmentVariable(KAFKA_GROUP_INSTANCE_ID);
        if(!string.IsNullOrEmpty(groupInstanceId)) consumerConfig.GroupInstanceId = groupInstanceId;

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

        var consumeResultFields = Environment.GetEnvironmentVariable(KAFKA_CONSUME_RESULT_FIELDS);
        if(!string.IsNullOrEmpty(consumeResultFields)) consumerConfig.ConsumeResultFields = consumeResultFields;

        var enableAutoCommit = Environment.GetEnvironmentVariable(KAFKA_ENABLE_AUTO_COMMIT);
        switch (enableAutoCommit?.ToLowerInvariant())
        {
            case "true":
                consumerConfig.EnableAutoCommit = true;
                break;
            case "false":
                consumerConfig.EnableAutoCommit = false;
                break;
            default:
                break;
        }

        var checkCrcs = Environment.GetEnvironmentVariable(KAFKA_CHECK_CRCS);
        switch (checkCrcs?.ToLowerInvariant())
        {
            case "true":
                consumerConfig.CheckCrcs = true;
                break;
            case "false":
                consumerConfig.CheckCrcs = false;
                break;
            default:
                break;
        }

        return consumerConfig;
    }

    public const string KAFKA_AUTO_COMMIT_INTERVAL_MS = nameof(KAFKA_AUTO_COMMIT_INTERVAL_MS);
    public const string KAFKA_AUTO_OFFSET_RESET = nameof(KAFKA_AUTO_OFFSET_RESET);
    public const string KAFKA_CHECK_CRCS = nameof(KAFKA_CHECK_CRCS);
    public const string KAFKA_CONSUME_RESULT_FIELDS = nameof(KAFKA_CONSUME_RESULT_FIELDS);
    public const string KAFKA_COORDINATOR_QUERY_INTERVAL_MS = nameof(KAFKA_COORDINATOR_QUERY_INTERVAL_MS);
    public const string KAFKA_ENABLE_AUTO_COMMIT = nameof(KAFKA_ENABLE_AUTO_COMMIT);
    public const string KAFKA_ENABLE_AUTO_OFFSET_STORE = nameof(KAFKA_ENABLE_AUTO_OFFSET_STORE);
    public const string KAFKA_ENABLE_PARTITION_EOF = nameof(KAFKA_ENABLE_PARTITION_EOF);
    public const string KAFKA_FETCH_ERROR_BACKOFF_MS = nameof(KAFKA_FETCH_ERROR_BACKOFF_MS);
    public const string KAFKA_FETCH_MAX_BYTES = nameof(KAFKA_FETCH_MAX_BYTES);
    public const string KAFKA_FETCH_MIN_BYTES = nameof(KAFKA_FETCH_MIN_BYTES);
    public const string KAFKA_FETCH_QUEUE_BACKOFF_MS = nameof(KAFKA_FETCH_QUEUE_BACKOFF_MS);
    public const string KAFKA_FETCH_WAIT_MAX_MS = nameof(KAFKA_FETCH_WAIT_MAX_MS);
    public const string KAFKA_GROUP_ID = nameof(KAFKA_GROUP_ID);
    public const string KAFKA_GROUP_INSTANCE_ID = nameof(KAFKA_GROUP_INSTANCE_ID);
    public const string KAFKA_GROUP_PROTOCOL_TYPE = nameof(KAFKA_GROUP_PROTOCOL_TYPE);
    public const string KAFKA_HEARTBEAT_INTERVAL_MS = nameof(KAFKA_HEARTBEAT_INTERVAL_MS);
    public const string KAFKA_ISOLATION_LEVEL = nameof(KAFKA_ISOLATION_LEVEL);
    public const string KAFKA_MAX_PARTITION_FETCH_BYTES = nameof(KAFKA_MAX_PARTITION_FETCH_BYTES);
    public const string KAFKA_MAX_POLL_INTERVAL_MS = nameof(KAFKA_MAX_POLL_INTERVAL_MS);
    public const string KAFKA_PARTITION_ASSIGNMENT_STRATEGY = nameof(KAFKA_PARTITION_ASSIGNMENT_STRATEGY);
    public const string KAFKA_QUEUED_MAX_MESSAGES_KBYTES = nameof(KAFKA_QUEUED_MAX_MESSAGES_KBYTES);
    public const string KAFKA_QUEUED_MIN_MESSAGES = nameof(KAFKA_QUEUED_MIN_MESSAGES);
    public const string KAFKA_SESSION_TIMEOUT_MS = nameof(KAFKA_SESSION_TIMEOUT_MS);
}
