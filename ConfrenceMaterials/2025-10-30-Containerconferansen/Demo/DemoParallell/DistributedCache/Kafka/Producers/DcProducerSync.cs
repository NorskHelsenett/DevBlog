using System.Diagnostics;
using System.Diagnostics.Metrics;
using Confluent.Kafka;
using DistributedCache.Kafka.Producers;
using Error = DataTypes.Error;

public class DcProducerSync: IDcProducer
{
    private readonly ILogger<DcProducerSync> _logger;
    private readonly ActivitySource _activitySource;
    private readonly IProducer<string, byte[]?> _producer;
    private readonly string _topic;

    public DcProducerSync(ILogger<DcProducerSync> logger, ActivitySource activitySource, Meter meter)
    {
        _logger = logger;
        _activitySource = activitySource;

        AppDomain.CurrentDomain.ProcessExit += new EventHandler(OnProcessExit);

        var producerConfig = KafkaConfigBinder.GetProducerConfig();
        _producer = new ProducerBuilder<string, byte[]?>(producerConfig).Build();

        var topicName = Environment.GetEnvironmentVariable(DISTRIBUTED_CACHE_KAFKA_TOPIC);
        if(string.IsNullOrWhiteSpace(topicName))
        {
            _logger.LogError($"Cannot consume if topic is not specified. Environment variable {nameof(DISTRIBUTED_CACHE_KAFKA_TOPIC)} was not set/is empty.");
            throw new InvalidOperationException($"Environment variable {nameof(DISTRIBUTED_CACHE_KAFKA_TOPIC)} has to have value.");
        }
        _topic = topicName;

        _logger.LogDebug($"{nameof(DcProducerSync)} initialized");
    }

    public async Task<DataTypes.Error?> Produce(DcItem item)
    {
        // _logger.LogTrace($"Producing message with correlation ID {correlationId}"); // We produce quite a bit the first time this runs, logging individual success feels like it would be too much (read: enable this if there actually ever arises a need for it)
        using var activity = _activitySource.StartActivity("Producer.Produce", ActivityKind.Producer);
        activity?.AddTag("variant", "sync");

        var message = new Message<string, byte[]?>
        {
            Key = item.Key,
            Value = item.Value
        };

        if(item.Headers?.Count > 0)
        {
            _logger.LogDebug("outbox item had following headers we're kafkaifying: {outbokxItemHeaders}", System.Text.Json.JsonSerializer.Serialize(item.Headers));
            Headers kHeaders = [];
            foreach (var dch in item.Headers.Where(h => !string.IsNullOrEmpty(h.Key)))
            {
                kHeaders.Add(new Header(dch.Key, System.Text.Encoding.UTF8.GetBytes(dch.Value)));
            }
            message.Headers = kHeaders;
        }

        activity?.AddEvent(new ActivityEvent("Done creating message type", DateTimeOffset.UtcNow));
        try
        {
            var notSent = true;
            while (notSent)
            {
                try
                {
                    activity?.AddEvent(new ActivityEvent("Trying to produce", DateTimeOffset.UtcNow));
                    // var produceResult = await _producer.ProduceAsync(_topic, message);
                    _producer.Produce(_topic, message, report =>
                    {
                        if (report.Status == PersistenceStatus.NotPersisted)
                        {
                            _logger.LogError("{LogEvent}", new {component = nameof(DcProducerSync), method = nameof(Produce), itemKey = item.Key, timeStamp = $"{DateTime.UtcNow:u}", persistenceStatus = "NotPersisted", message = "Producing event resulted in unexpected persistence status" });
                        }
                        else if (report.Status == PersistenceStatus.PossiblyPersisted)
                        {
                            _logger.LogWarning("{LogEvent}", new {component = nameof(DcProducerSync), method = nameof(Produce), itemKey = item.Key, timeStamp = $"{DateTime.UtcNow:u}", persistenceStatus = "PossiblyPersisted", message = "Producing event resulted in unexpected persistence status" });
                        }

                    });
                    activity?.AddEvent(new ActivityEvent("Produce result received", DateTimeOffset.UtcNow));
                    notSent = false;
                    activity?.AddEvent(new ActivityEvent("Produce result evaluated, done now", DateTimeOffset.UtcNow));
                }
                catch (ProduceException<string, byte[]?> ex)
                {
                    if (!ex.Message.Contains("Queue full"))
                    {
                        throw;
                    }
                    _logger.LogWarning("{LogEvent}", new {component = nameof(DcProducerSync), method = nameof(Produce), itemKey = item.Key, timeStamp = $"{DateTime.UtcNow:u}", message = "We are producing too fast, producer queue is full, sleeping and retrying" });
                    await Task.Delay(TimeSpan.FromSeconds(3));
                }
            }
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "{LogEvent}", new {component = nameof(DcProducerSync), method = nameof(Produce), itemKey = item.Key, timeStamp = $"{DateTime.UtcNow:u}", topic = _topic, message = "Got exception when producing message" });
            return new Error { Message = $"Got exception {ex.Message} when producing to Kafka"};
        }
        return null;
    }

    private void OnProcessExit(object? sender, EventArgs e)
    {
        // Because finalizers are not necessarily called on program exit in newer dotnet:
        // https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/finalizers
        // Could maybe be handled by making this a BackgroundService and using the provided shutdown handling there,
        // but then again this is not really for doing long running background work.
        _logger.LogDebug("Kafka producer process exit event triggered.");
        try
        {
            _producer.Flush();
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "Kafka producer got exception while flushing during process termination");
        }
    }

    ~DcProducerSync()
    {
        _logger.LogDebug("Kafka producer finalizer called.");
        try
        {
            _producer.Flush();
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "Kafka producer got exception while flushing during finalization");
        }
    }
}
