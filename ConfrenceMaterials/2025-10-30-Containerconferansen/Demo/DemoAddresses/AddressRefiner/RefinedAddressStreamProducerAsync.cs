using System.Diagnostics;
using System.Diagnostics.Metrics;
using Confluent.Kafka;
using Confluent.Kafka.SyncOverAsync;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using No.Nhn.Address.Cadastre.Road;

namespace AddressRefiner;

public class RefinedAddressStreamProducerAsync: IRefinedAddressStreamProducer
{
    private readonly ILogger<RefinedAddressStreamProducer> _logger;
    private readonly ActivitySource _activitySource;
    private readonly Counter<long> _successCounter;
    private readonly Counter<long> _failureCounter;
    private readonly IProducer<string, CadastreRoadAddress?> _producer;
    private readonly string _topic;

    public RefinedAddressStreamProducerAsync(ILogger<RefinedAddressStreamProducer> logger, ActivitySource activitySource, Meter meter)
    {
        _logger = logger;
        _activitySource = activitySource;

        _successCounter = meter.CreateCounter<long>("produce.successes.count", description: "Number of events successfully produced");
        _failureCounter = meter.CreateCounter<long>("produce.fail.exception.count", description: "Number of events not produced failed with exception");

        AppDomain.CurrentDomain.ProcessExit += new EventHandler(OnProcessExit);

        var producerConfig = KafkaConfigBinder.GetProducerConfig();
        var schemaRegistryConfig = KafkaConfigBinder.GetSchemaRegistryConfig();
        var schemaRegistryClient = new CachedSchemaRegistryClient(schemaRegistryConfig);
        var protobufSerializerConfig = new ProtobufSerializerConfig
        {
            AutoRegisterSchemas = false,
            UseLatestVersion = true
        };
        _producer = new ProducerBuilder<string, CadastreRoadAddress?>(producerConfig)
            .SetValueSerializer(new ProtobufSerializer<CadastreRoadAddress?>(schemaRegistryClient, protobufSerializerConfig).AsSyncOverAsync())
            .Build();

        var topicName = Environment.GetEnvironmentVariable(ADDRESS_REFINER_KAFKA_TOPIC_REFINED_ADDRESSES);
        if(string.IsNullOrWhiteSpace(topicName))
        {
            _logger.LogError($"Cannot consume if topic is not specified. Environment variable {nameof(ADDRESS_REFINER_KAFKA_TOPIC_REFINED_ADDRESSES)} was not set/is empty.");
            throw new InvalidOperationException($"Environment variable {nameof(ADDRESS_REFINER_KAFKA_TOPIC_REFINED_ADDRESSES)} has to have value.");
        }
        _topic = topicName;

        _logger.LogDebug($"{nameof(RefinedAddressStreamProducer)} initialized");
    }

    public async Task<bool> Produce(string key, CadastreRoadAddress? value, Headers headers, string correlationId)
    {
        // _logger.LogTrace($"Producing message with correlation ID {correlationId}"); // We produce quite a bit the first time this runs, logging individual success feels like it would be too much (read: enable this if there actually ever arises a need for it)
        using var activity = _activitySource.StartActivity("Producer.Produce");

        var message = new Message<string, CadastreRoadAddress?>
        {
            Key = key,
            Value = value
        };

        if(headers.Count > 0)
        {
            message.Headers = headers;
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
                    var produceResult = await _producer.ProduceAsync(_topic, message);
                    // _producer.Produce(_topic, message, report =>
                    // {
                    // });
                    if (produceResult.Status == PersistenceStatus.NotPersisted)
                    {
                        _logger.LogError("{LogEvent}", new {component = nameof(RefinedAddressStreamProducer), method = nameof(Produce), correlationId = $"{correlationId}", addressId = key, timeStamp = $"{DateTime.UtcNow:u}", persistenceStatus = "NotPersisted", message = "Producing event resulted in unexpected persistence status" });
                    }
                    else if (produceResult.Status == PersistenceStatus.PossiblyPersisted)
                    {
                        _logger.LogWarning("{LogEvent}", new {component = nameof(RefinedAddressStreamProducer), method = nameof(Produce), correlationId = $"{correlationId}", addressId = key, timeStamp = $"{DateTime.UtcNow:u}", persistenceStatus = "PossiblyPersisted", message = "Producing event resulted in unexpected persistence status" });
                    }
                    activity?.AddEvent(new ActivityEvent("Produce result received", DateTimeOffset.UtcNow));
                    notSent = false;
                    activity?.AddEvent(new ActivityEvent("Produce result evaluated, done now", DateTimeOffset.UtcNow));
                    _successCounter.Add(1);
                }
                catch (ProduceException<string, CadastreRoadAddress> ex)
                {
                    if (!ex.Message.Contains("Queue full"))
                    {
                        throw;
                    }
                    _logger.LogWarning("{LogEvent}", new {component = nameof(RefinedAddressStreamProducer), method = nameof(Produce), correlationId = $"{correlationId}", addressId = key, timeStamp = $"{DateTime.UtcNow:u}", message = "We are producing too fast, producer queue is full, sleeping and retrying" });
                    await Task.Delay(TimeSpan.FromSeconds(3));
                }
            }
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "{LogEvent}", new {component = nameof(RefinedAddressStreamProducer), method = nameof(Produce), correlationId = $"{correlationId}", addressId = key, timeStamp = $"{DateTime.UtcNow:u}", topic = _topic, message = "Got exception when producing message" });
            _failureCounter.Add(1);
            return false;
        }
        return true;
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

    ~RefinedAddressStreamProducerAsync()
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
