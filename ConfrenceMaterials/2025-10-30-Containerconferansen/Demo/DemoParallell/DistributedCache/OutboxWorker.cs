using System.Diagnostics;
using System.Diagnostics.Metrics;
using DistributedCache.Kafka.Producers;

namespace DistributedCache;

public class OutboxWorker: BackgroundService
{
    private readonly ILogger<OutboxWorker> _logger;
    private readonly ActivitySource _activitySource;
    private readonly IStorageOutbox _storageOutbox;
    private readonly IDcProducer _dcProducer;
    private readonly IHostApplicationLifetime _hostApplicationLifetime;

    public OutboxWorker(ILogger<OutboxWorker> logger, ActivitySource activitySource, Meter meter, IStorageOutbox storageOutbox, IDcProducer dcProducer, IHostApplicationLifetime hostApplicationLifetime)
    {
        _logger = logger;
        _activitySource = activitySource;
        _storageOutbox = storageOutbox;
        _dcProducer = dcProducer;
        _hostApplicationLifetime = hostApplicationLifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogDebug("Kafka refined addresses consumer service is doing pre startup blocking work.");
        await DoWork(stoppingToken);
        _hostApplicationLifetime.StopApplication();
    }

    private async Task DoWork(CancellationToken stoppingToken)
    {
        await Task.Delay(1, stoppingToken);
        TimeSpan interval = TimeSpan.FromMilliseconds(100);
        var configuredInterval = Environment.GetEnvironmentVariable(DISTRIBUTED_CACHE_OUTBOX_INTERVAL_MS);
        if (!string.IsNullOrEmpty(configuredInterval))
        {
            if (long.TryParse(configuredInterval, out var parsedIntervalMs))
            {
                interval = TimeSpan.FromMilliseconds(parsedIntervalMs);
            }
        }
        using PeriodicTimer timer = new PeriodicTimer(interval);
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            var next = _storageOutbox.RetrieveNext();
            if (next.Error != null)
            {
                _logger.LogError("Failed to get next. Marking as failed and moving on");
                _storageOutbox.DeleteNext();
                continue;
            }
            else if (next.NextItem == null)
            {
                continue;
            }
            else
            {
                while (next.Error == null && next.NextItem != null && !stoppingToken.IsCancellationRequested)
                {
                    var res = await _dcProducer.Produce(next.NextItem);
                    _storageOutbox.DeleteNext();
                    next = _storageOutbox.RetrieveNext();
                }
            }
        }
    }
}
