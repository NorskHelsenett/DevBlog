using Confluent.Kafka;
using Confluent.Kafka.SyncOverAsync;
using Confluent.SchemaRegistry.Serdes;
using KafkaBlobChunking;

public class UserAccessMappingConsumer: BackgroundService
{
    private readonly ILogger<UserAccessMappingConsumer> _logger;
    private readonly IHostApplicationLifetime _hostApplicationLifetime;
    private readonly UserAccessMappingStateService _userAccessMappingStateService;
    private readonly string _topic;

    public UserAccessMappingConsumer(ILogger<UserAccessMappingConsumer> logger, IHostApplicationLifetime hostApplicationLifetime, UserAccessMappingStateService userAccessMappingStateService)
    {
        _logger = logger;
        _hostApplicationLifetime = hostApplicationLifetime;
        _userAccessMappingStateService = userAccessMappingStateService;
        _topic = Environment.GetEnvironmentVariable(BIG_PAYLOADS_USER_ACCESS_MAPPING_TOPIC) ?? throw new Exception($"Environment variable {BIG_PAYLOADS_USER_ACCESS_MAPPING_TOPIC} has to be set");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogDebug($"{nameof(UserAccessMappingConsumer)} doing pre startup blocking work.");
        await DoWork(stoppingToken);
        _hostApplicationLifetime.StopApplication();
    }

    private async Task DoWork(CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(1));
        var consumer = GetUserAccessMappingConsumer();
        _logger.LogDebug($"Subscribing to topic {_topic}");
        consumer.Subscribe(_topic);
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var result = consumer.Consume(cancellationToken);
                if (result?.Message == null)
                {
                    _logger.LogDebug($"{nameof(UserAccessMappingConsumer)} We've reached the end of the topic {_topic}.");
                    await Task.Delay(TimeSpan.FromSeconds(8), cancellationToken);
                }
                else
                {
                    _logger.LogDebug($"{nameof(UserAccessMappingConsumer)} New event received");
                    var nextUserAccessMapping = result.Message.Value;
                    if (nextUserAccessMapping != null)
                    {
                        _userAccessMappingStateService.SetUserAccessMapping(result.Message.Key, nextUserAccessMapping);
                        _logger.LogDebug($"{nameof(UserAccessMappingConsumer)} Consumed user access mapping with id \"{result.Message.Key}\"");
                    }
                    else
                    {
                        _logger.LogInformation($"{nameof(UserAccessMappingConsumer)} Received tombstone for user access mapping with id \"{result.Message.Key}\"");
                        _userAccessMappingStateService.RemoveUserAccessMapping(result.Message.Key);
                    }
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
        finally
        {
            consumer.Close();
        }
    }

    private IConsumer<string, UserAccessMapping?> GetUserAccessMappingConsumer()
    {
        return new ConsumerBuilder<string, UserAccessMapping?>(KafkaConfigBinder.GetConsumerConfig())
            .SetPartitionsAssignedHandler((c, partitions) =>
            {
                // Always start at the beginning, only use cg for tracking liveliness and lag from the outside
                return partitions.Select(tp => new TopicPartitionOffset(tp, Offset.Beginning));
            })
            .SetValueDeserializer(new ProtobufDeserializer<UserAccessMapping?>().AsSyncOverAsync())
            .SetErrorHandler((_, e) => _logger.LogError($"Error: {e.Reason}"))
            .Build();
    }
}
