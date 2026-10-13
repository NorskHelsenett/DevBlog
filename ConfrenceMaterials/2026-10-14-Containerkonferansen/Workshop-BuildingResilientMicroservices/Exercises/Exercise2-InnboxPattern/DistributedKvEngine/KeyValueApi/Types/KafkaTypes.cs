public record KafkaTopic
{
    public required string Value { get; init; }
}

public record KafkaPartition
{
    public required int Value { get; init; }
}

public record KafkaOffset
{
    public required long Value { get; init; }
}

public record KafkaTopicPartitionOffset
{
    public required KafkaTopic Topic { get; init; }
    public required KafkaPartition Partition { get; init; }
    public required KafkaOffset Offset { get; init; }
}
