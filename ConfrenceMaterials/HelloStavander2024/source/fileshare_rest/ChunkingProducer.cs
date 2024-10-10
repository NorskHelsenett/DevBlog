using System.Security.Cryptography;
using Confluent.Kafka;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using Google.Protobuf;
using KafkaBlobChunking;

public class ChunkingProducer
{
    private readonly ILogger<ChunkingProducer> _logger;
    private readonly string _topicChunks;
    private readonly string _topicMetadata;
    private readonly int _chunkSizeBytes;

    public ChunkingProducer(ILogger<ChunkingProducer> logger)
    {
        _logger = logger;
        var topicNameChunksTopic = Environment.GetEnvironmentVariable(BIG_PAYLOADS_CHUNKS_TOPIC);
        if(string.IsNullOrWhiteSpace(topicNameChunksTopic))
        {
            _logger.LogError($"Cannot consume if topic is not specified. Environment variable {nameof(BIG_PAYLOADS_CHUNKS_TOPIC)} was not set/is empty.");
            throw new InvalidOperationException($"Environment variable {nameof(BIG_PAYLOADS_CHUNKS_TOPIC)} has to have value.");
        }
        _topicChunks = topicNameChunksTopic;

        var topicNameMetadataTopic = Environment.GetEnvironmentVariable(BIG_PAYLOADS_METADATA_TOPIC);
        if(string.IsNullOrWhiteSpace(topicNameMetadataTopic))
        {
            _logger.LogError($"Cannot consume if topic is not specified. Environment variable {nameof(BIG_PAYLOADS_METADATA_TOPIC)} was not set/is empty.");
            throw new InvalidOperationException($"Environment variable {nameof(BIG_PAYLOADS_METADATA_TOPIC)} has to have value.");
        }
        _topicMetadata = topicNameMetadataTopic;

        // var chunkSizeBytesDefault = $"{1024 * 1024 - 1024}";
        var chunkSizeBytesDefault = $"{128}";
        var chunkSizeBytesConfigured = Environment.GetEnvironmentVariable(BIG_PAYLOADS_CHUNK_PAYLOAD_SIZE_BYTES);
        if(string.IsNullOrWhiteSpace(chunkSizeBytesConfigured))
        {
            _logger.LogError($"Chunk. Environment variable {nameof(BIG_PAYLOADS_CHUNK_PAYLOAD_SIZE_BYTES)} was not set/is empty, using default value {chunkSizeBytesDefault} for number of bytes in payload per chunk");
            chunkSizeBytesConfigured = chunkSizeBytesDefault;
        }
        _chunkSizeBytes = int.Parse(chunkSizeBytesConfigured);
    }

    public async Task<bool> ProduceAsync(Stream stream, string blobId, string ownerId, string callersBlobName, string correlationId, CancellationToken cancellationToken)
    {
        var streamChecksum = System.Security.Cryptography.IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[_chunkSizeBytes];
        Dictionary<int, long> lowOffsetsPerPartition = new();
        Dictionary<int, long> highOffsetsPerPartition = new();
        Dictionary<int, ulong> numberOfChunksPerPartition = new();
        ulong numberOfChunks = 0;
        ulong numberOfBytes = 0;

        var chunkProducer = GetChunkProducer();

        // ToDo: Do first produce here, so that we only check and set low offsets once
        numberOfChunks++;
        var firstTimeNumberOfBytesRead = await stream.ReadAtLeastAsync(buffer, _chunkSizeBytes, throwOnEndOfStream: false, cancellationToken);
        if(firstTimeNumberOfBytesRead < _chunkSizeBytes)
        {
            // We're at the end, resize buffer
            buffer = buffer[0..firstTimeNumberOfBytesRead];
        }
        streamChecksum.AppendData(buffer, 0, firstTimeNumberOfBytesRead);
        numberOfBytes += (uint) firstTimeNumberOfBytesRead;

        var firstTimeNextChunkKey = $"{blobId}_{numberOfChunks}";
        var firstTimeNextChunkPayload = new BlobChunk
        {
            ChunkId = firstTimeNextChunkKey,
            CompleteBlobId = blobId,
            ChunkNumber = numberOfChunks,
            ChunkNumberOfByes = (ulong) firstTimeNumberOfBytesRead,
            ChunkPayload = ByteString.CopyFrom(buffer),
            CompleteBlobSchemaSubject = "You should use this!",
            CompleteBlobSchemaVersion = "And this too",
        };
        var firstTimeNextChunkMessage = new Message<string, BlobChunk?>
        {
            Key = firstTimeNextChunkKey,
            Value = firstTimeNextChunkPayload
        };
        var firstTimeProduceResult = await chunkProducer.ProduceAsync(_topicChunks, firstTimeNextChunkMessage, cancellationToken);
        if (firstTimeProduceResult.Status == PersistenceStatus.Persisted)
        {
            lowOffsetsPerPartition[firstTimeProduceResult.Partition] = firstTimeProduceResult.Offset;
            highOffsetsPerPartition[firstTimeProduceResult.Partition] = firstTimeProduceResult.Offset;
            if (!numberOfChunksPerPartition.ContainsKey(firstTimeProduceResult.Partition))
                numberOfChunksPerPartition[firstTimeProduceResult.Partition] = 0;
            numberOfChunksPerPartition[firstTimeProduceResult.Partition] ++;
        }
        else
        {
            Console.WriteLine($"Failed when producing chunk {numberOfChunks} of payload with id {blobId}");
            return false;
        }

        if (firstTimeNumberOfBytesRead < _chunkSizeBytes)
        {
            _logger.LogDebug($"Read {firstTimeNumberOfBytesRead}, indicating we've reached the end of the stream.");
        }
        else
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                numberOfChunks++;
                var numberOfBytesRead = await stream.ReadAtLeastAsync(buffer, _chunkSizeBytes, throwOnEndOfStream: false, cancellationToken);
                if(numberOfBytesRead < _chunkSizeBytes)
                {
                    // We're at the end, resize buffer
                    buffer = buffer[0..numberOfBytesRead];
                }
                streamChecksum.AppendData(buffer, 0, numberOfBytesRead);
                numberOfBytes += (uint) numberOfBytesRead;

                var nextChunkKey = $"{blobId}_{numberOfChunks}";
                var nextChunkPayload = new BlobChunk
                {
                    ChunkId = nextChunkKey,
                    CompleteBlobId = blobId,
                    ChunkNumber = numberOfChunks,
                    ChunkNumberOfByes = (ulong) numberOfBytesRead,
                    ChunkPayload = ByteString.CopyFrom(buffer),
                    CompleteBlobSchemaSubject = "You should use this!",
                    CompleteBlobSchemaVersion = "And this too",
                };
                var nextChunkMessage = new Message<string, BlobChunk?>
                {
                    Key = nextChunkKey,
                    Value = nextChunkPayload
                };
                var produceResult = await chunkProducer.ProduceAsync(_topicChunks, nextChunkMessage, cancellationToken);
                if (produceResult.Status == PersistenceStatus.Persisted)
                {
                    highOffsetsPerPartition[produceResult.Partition] = produceResult.Offset;
                    if (!numberOfChunksPerPartition.ContainsKey(produceResult.Partition))
                        numberOfChunksPerPartition[produceResult.Partition] = 0;
                    numberOfChunksPerPartition[produceResult.Partition] ++;
                }
                else
                {
                    Console.WriteLine($"Failed when producing chunk {numberOfChunks} of payload with id {blobId}");
                    return false;
                }

                if (numberOfBytesRead < _chunkSizeBytes)
                {
                    _logger.LogDebug($"Read {numberOfBytesRead}, indicating we've reached the end of the stream.");
                    break;
                }
            }
        }

        var finalChecksum = Convert.ToHexString(streamChecksum.GetHashAndReset());

        _logger.LogDebug("Payload chunked and given to producer, flushing to guarantee progress");
        chunkProducer.Flush(cancellationToken); // Don't proceed unless everything's successfully shipped

        var metadataProducer = GetChunkMetadataProducer();
        var metadataPayload = new BlobChunksMetadata
        {
            BlobId = blobId,
            BlobOwnerId = ownerId,
            BlobName = callersBlobName,
            ChunksTopicPartitionOffsetsEarliest = { lowOffsetsPerPartition.Select(lows => new KafkaTopicPartitionOffset { Topic = _topicChunks, Partition = lows.Key, Offset = lows.Value }) },
            ChunksTopicPartitionOffsetsLatest = { highOffsetsPerPartition.Select(high => new KafkaTopicPartitionOffset { Topic = _topicChunks, Partition = high.Key, Offset = high.Value }) },
            ChunksPerTopicPartitionCount = { numberOfChunksPerPartition.Select(x => new KafkaNumberOfChunksPerTopicPartition {Topic = _topicChunks, Partition = x.Key, NumberOfChunks = x.Value})},
            TotalNumberOfChunks = numberOfChunks,
            CompleteBlobTotalNumberOfBytes = numberOfBytes,
            FinalChecksum = finalChecksum,
            BlobSchemaSubject = "It is really best practice",
            BlobSchemaVersion = "The people who love semver haven't had the joys of rc versions",
            CorrelationId = correlationId,
        };
        var metadataMessage = new Message<string, BlobChunksMetadata?>
        {
            Key = blobId,
            Value = metadataPayload
        };
        var metadataProduceResult = await metadataProducer.ProduceAsync(_topicMetadata, metadataMessage, cancellationToken);
        if (metadataProduceResult.Status != PersistenceStatus.Persisted)
        {
            Console.WriteLine($"Failed when producing metadata for shipped payload with id {blobId}");
            return false;
        }
        return true;
    }

    public async Task<bool> ProduceTombstones(BlobChunksMetadata blobMetadata, string correlationId, CancellationToken cancellationToken)
    {
        var allChunksTombstonedSuccessfully = true; // Do best effort of deleting remaining should 1 fail
        var tombstoneChunksProducer = GetChunkProducer();
        for (ulong i = 1; i < blobMetadata.TotalNumberOfChunks + 1; i++)
        {
            var nextChunkTombstone = new Message<string, BlobChunk?>
            {
                Key = $"{blobMetadata.BlobId}_{i}",
                Value = null
            };
            var nextTombstoneProduceResult = await tombstoneChunksProducer.ProduceAsync(_topicChunks, nextChunkTombstone, cancellationToken);
            if (nextTombstoneProduceResult.Status != PersistenceStatus.Persisted)
            {
                _logger.LogError($"CorrelationId \"{correlationId}\" failed to produce tombstone for chunk \"{i}\" of blob with id \"{blobMetadata.BlobId}\"");
                allChunksTombstonedSuccessfully = false;
            }
        }
        tombstoneChunksProducer.Flush(cancellationToken);

        if (!allChunksTombstonedSuccessfully)
        {
            return false;
        }
        // Only delete metada if all chunks successfully tombstoned
        var tombstoneMetadataProducer = GetChunkMetadataProducer();
        var nextMetadaTombstone = new Message<string, BlobChunksMetadata?>
        {
            Key = blobMetadata.BlobId,
            Value = null
        };
        var tombstoneMetadataResult = await tombstoneMetadataProducer.ProduceAsync(_topicMetadata, nextMetadaTombstone, cancellationToken);
        if (tombstoneMetadataResult.Status != PersistenceStatus.Persisted)
        {
            _logger.LogError($"CorrelationId \"{correlationId}\" failed to produce tombstone for blob metadata for blob with id \"{blobMetadata.BlobId}\"");
            return false;
        }

        tombstoneMetadataProducer.Flush(cancellationToken);
        return true;
    }

    private IProducer<string, BlobChunk?> GetChunkProducer()
    {
        var schemaRegistry = new CachedSchemaRegistryClient(KafkaConfigBinder.GetSchemaRegistryConfig());
        return  new ProducerBuilder<string, BlobChunk?>(KafkaConfigBinder.GetProducerConfig())
            .SetValueSerializer(new ProtobufSerializer<BlobChunk?>(schemaRegistry, GetProtobufSerializerConfig()))
            .Build();
    }

    private IProducer<string, BlobChunksMetadata?> GetChunkMetadataProducer()
    {
        var schemaRegistry = new CachedSchemaRegistryClient(KafkaConfigBinder.GetSchemaRegistryConfig());
        return  new ProducerBuilder<string, BlobChunksMetadata?>(KafkaConfigBinder.GetProducerConfig())
            .SetValueSerializer(new ProtobufSerializer<BlobChunksMetadata?>(schemaRegistry, GetProtobufSerializerConfig()))
            .Build();
    }

    private ProtobufSerializerConfig GetProtobufSerializerConfig()
    {
        return new ProtobufSerializerConfig
        {
            AutoRegisterSchemas = false,
            NormalizeSchemas = true,
            UseLatestVersion = true
        };
    }
}
