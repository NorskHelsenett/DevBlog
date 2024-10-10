using System.Runtime.CompilerServices;
using Confluent.Kafka;
using Confluent.Kafka.SyncOverAsync;
using Confluent.SchemaRegistry.Serdes;
using KafkaBlobChunking;

public class ChunkConsumer
{
    private readonly ILogger<ChunkConsumer> _logger;
    private string _topicChunks;

    public ChunkConsumer(ILogger<ChunkConsumer> logger)
    {
        _logger = logger;
        var topicNameChunksTopic = Environment.GetEnvironmentVariable(BIG_PAYLOADS_CHUNKS_TOPIC);
        if (string.IsNullOrWhiteSpace(topicNameChunksTopic))
        {
            _logger.LogError($"Cannot consume if topic is not specified. Environment variable {nameof(BIG_PAYLOADS_CHUNKS_TOPIC)} was not set/is empty.");
            throw new InvalidOperationException($"Environment variable {nameof(BIG_PAYLOADS_CHUNKS_TOPIC)} has to have value.");
        }
        _topicChunks = topicNameChunksTopic;
    }
    public async IAsyncEnumerable<byte> GetBlobByMetadataAsync(BlobChunksMetadata metadata, string correlationId, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        _logger.LogDebug($"Got request to retrieve chunks for payload {metadata.BlobId}");
        var streamChecksum = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
        ulong totalNumberOfBytesConsumed = 0;

        // For simplicity of example, don't handle weird extreme of chunks across multiple topics
        if (metadata.ChunksTopicPartitionOffsetsEarliest.Any(tpo => tpo.Topic != _topicChunks))
        {
            _logger.LogError("No chunk topic partition offsets");
            throw new Exception($"Received metadata about chunks that are not on our configured topic for chunks (configured is \"{_topicChunks}\")");
        }
        _logger.LogDebug($"Setting up consumer to consume from earliest offsets specified in metadata");
        var chunkConsumer = GetChunkConsumer(metadata);
        chunkConsumer.Subscribe(_topicChunks);

        ulong nextExpectedChunkNumber = 1;
        while (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug($"Waiting for messages to consume on chunks topic");
            var result = chunkConsumer.Consume(cancellationToken);

            if (result?.Message == null)
            {
                // This should not happen.
                _logger.LogDebug("We've reached the end of the chunks topic.");
                await Task.Delay(TimeSpan.FromSeconds(8), cancellationToken);
            }
            else
            {
                _logger.LogDebug($"Consumed chunk {result.Message.Key} at topic {result.Topic} partition {result.Partition.Value} offset {result.Offset.Value}");
                var nextChunk = result.Message.Value;
                if (nextChunk != null)
                {
                    if (nextChunk.CompleteBlobId == metadata.BlobId)
                    {
                        if (nextExpectedChunkNumber != nextChunk.ChunkNumber)
                        {
                            if (nextChunk.ChunkNumber + 1 == nextExpectedChunkNumber) // Because uint, do check with + instead of -
                            {
                                _logger.LogWarning($"When consuming chunks for blob with id \"{metadata.BlobId}\" received duplicate chunk, number {nextChunk.ChunkNumber} at topic {result.Topic} partition {result.Partition.Value} offset {result.Offset.Value}");
                            }
                            else
                            {
                                chunkConsumer.Close();
                                throw new Exception($"When consuming chunks for blob with id \"{metadata.BlobId}\" expected next chunk to be chunk number {nextExpectedChunkNumber} but instead received chunk number {nextChunk.ChunkNumber}");
                            }
                        }
                        else
                        {
                            nextExpectedChunkNumber++;
                            streamChecksum.AppendData(nextChunk.ChunkPayload.ToByteArray(), 0, (int)nextChunk.ChunkNumberOfByes);
                            foreach (var payloadByte in nextChunk.ChunkPayload)
                            {
                                yield return payloadByte;
                            }
                            totalNumberOfBytesConsumed += (ulong)nextChunk.ChunkPayload.Length;
                            if (nextExpectedChunkNumber > metadata.TotalNumberOfChunks)
                            {
                                _logger.LogDebug($"All chunks of blob {metadata.BlobId} consumed, stopping consuming and proceeding to check checksum");
                                break;
                            }
                        }
                    }
                    else
                    {
                        _logger.LogDebug($"Consumed chunk {nextChunk.ChunkId} is not referenced in the metadata with blob id {metadata.BlobId}.");
                    }
                }
            }
        }

        chunkConsumer.Close();
        var finalChecksum = Convert.ToHexString(streamChecksum.GetHashAndReset());
        var checksumsMatch = finalChecksum == metadata.FinalChecksum;
        var numberOfBytesMatch = totalNumberOfBytesConsumed == metadata.CompleteBlobTotalNumberOfBytes;
        if(!checksumsMatch || !numberOfBytesMatch)
        {
            throw new Exception($"When consuming chunks for blob with id \"{metadata.BlobId}\" uncovered mismatch in consumed vs expected. Consumed checksum was \"{finalChecksum}\", expected \"{metadata.FinalChecksum}\". Consumed number of bytes was \"{totalNumberOfBytesConsumed}\", expected \"{metadata.CompleteBlobTotalNumberOfBytes}\"");
        }
        _logger.LogTrace($"CorrelationId {correlationId} all chunks of blob with id \"{metadata.BlobId}\" successfully consumed (final checksum matches, and number of bytes is correct)");
    }

    private IConsumer<string, BlobChunk?> GetChunkConsumer(BlobChunksMetadata metadata)
    {
        var kafkaConfig = KafkaConfigBinder.GetConsumerConfig();
        kafkaConfig.GroupId = $"{kafkaConfig.GroupId}-{metadata.BlobId}-{Guid.NewGuid()}";
        var earliestOffsetsWithChunks = metadata.ChunksTopicPartitionOffsetsEarliest
            .GroupBy(tpo => (tpo.Topic, tpo.Partition))
            .ToDictionary(tpGroupedOffsets => tpGroupedOffsets.Key.Partition, offsets => offsets.Min(o => o.Offset))
            .Select(partitionToOffsetMap => new TopicPartitionOffset(_topicChunks, new Partition(partitionToOffsetMap.Key), new Offset(partitionToOffsetMap.Value)))
            .ToArray();
        _logger.LogDebug($"Earliest offsets: {System.Text.Json.JsonSerializer.Serialize(earliestOffsetsWithChunks)}");
        // var chunkConsumer = new ConsumerBuilder<string, BlobChunk>(kafkaConfig)
        return new ConsumerBuilder<string, BlobChunk?>(kafkaConfig)
            .SetValueDeserializer(new ProtobufDeserializer<BlobChunk?>().AsSyncOverAsync())
            .SetErrorHandler((_, e) => _logger.LogError($"Error: {e.Reason}"))
            .SetPartitionsAssignedHandler((_, _) => earliestOffsetsWithChunks)
            .Build();
    }

}
