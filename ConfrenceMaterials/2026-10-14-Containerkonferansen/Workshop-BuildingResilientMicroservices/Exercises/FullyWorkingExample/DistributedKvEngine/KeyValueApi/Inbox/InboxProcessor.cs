using Microsoft.Extensions.Hosting;

public sealed class InboxProcessor : BackgroundService
{
    private readonly ILogger<InboxProcessor> _logger;
    private readonly IInboxStateService _inbox;
    private readonly IKeyValueStateService _kvStore;
    private readonly int _batchSize;
    private readonly TimeSpan _pollDelay;

    public InboxProcessor(ILogger<InboxProcessor> logger, IInboxStateService inbox, IKeyValueStateService kvStore)
    {
        _logger = logger;
        _inbox = inbox;
        _kvStore = kvStore;
        _batchSize = 100;
        _pollDelay = TimeSpan.FromMilliseconds(5000);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("InboxProcessor started");

        while (!stoppingToken.IsCancellationRequested)
        {
            var pending = _inbox.GetPending(_batchSize);

            if (pending.Count == 0)
            {
                // Nothing to do – back off a bit.
                await Task.Delay(_pollDelay, stoppingToken);
                continue;
            }

            foreach (var msg in pending)
            {
                try
                {
                    if (msg.Value == null)
                    {
                        _kvStore.Remove(msg.Key, msg.CorrelationId);
                    }
                    else
                    {
                        _kvStore.Store(msg.Key, msg.Value, msg.CorrelationId);
                    }

                    _inbox.MarkProcessed(msg.MessageId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to process inbox message {MsgId} (topic {Topic} [{Partition} @{Offset})",
                        msg.MessageId, msg.Topic, msg.Partition, msg.Offset);

                    _inbox.MoveToDeadLetter(msg.MessageId, ex.Message);
                }
            }
        }

        _logger.LogInformation("InboxProcessor shutting down");
    }
}
