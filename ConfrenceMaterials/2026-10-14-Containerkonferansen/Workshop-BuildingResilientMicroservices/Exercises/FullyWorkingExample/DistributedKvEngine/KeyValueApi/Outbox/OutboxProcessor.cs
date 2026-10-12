namespace KeyValueApi;

public class OutboxProcessor : BackgroundService
{
    private readonly ILogger<OutboxProcessor> _logger;
    private readonly IOutboxStateService _outboxStateService;
    private readonly KafkaProducerService _kafkaProducerService;

    private readonly TimeSpan _emptyQueueDelay; // how long to wait when there is nothing to do
    private readonly TimeSpan _errorDelay; // base delay after a produce error (exponential back‑off)
    private readonly int _maxRetryAttempts; // after this many consecutive fails we pause longer

    private int _consecutiveErrors = 0;

    public OutboxProcessor(
        ILogger<OutboxProcessor> logger,
        IOutboxStateService outboxStateService,
        KafkaProducerService kafkaProducerService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _outboxStateService = outboxStateService ?? throw new ArgumentNullException(nameof(outboxStateService));
        _kafkaProducerService = kafkaProducerService ?? throw new ArgumentNullException(nameof(kafkaProducerService));

        var emptyQueueDelay = TimeSpan.FromMilliseconds(200);
        var emptyQueueDelayMsString = Environment.GetEnvironmentVariable(KV_API_OUTBOX_PROCESSOR_EMPTY_QUEUE_DELAY_MILLISECONDS);
        if(!string.IsNullOrWhiteSpace(emptyQueueDelayMsString))
        {
            if (int.TryParse(emptyQueueDelayMsString, out var emptyQueueDelayParsedInt))
            {
                emptyQueueDelay = TimeSpan.FromMilliseconds(emptyQueueDelayParsedInt);
            }
        }
        _emptyQueueDelay = emptyQueueDelay;

        var errorDelay = TimeSpan.FromMilliseconds(2000);
        var errorDelayMsString = Environment.GetEnvironmentVariable(KV_API_OUTBOX_PROCESSOR_ERROR_DELAY_MILLISECONDS);
        if(!string.IsNullOrWhiteSpace(errorDelayMsString))
        {
            if (int.TryParse(errorDelayMsString, out var errorDelayParsedInt))
            {
                errorDelay = TimeSpan.FromMilliseconds(errorDelayParsedInt);
            }
        }
        _errorDelay = errorDelay;

        var maxRetryAttempts = 5;
        var maxRetryAttemptsEnvString = Environment.GetEnvironmentVariable(KV_API_OUTBOX_PROCESSOR_ERROR_DELAY_MILLISECONDS);
        if(!string.IsNullOrWhiteSpace(maxRetryAttemptsEnvString))
        {
            if (int.TryParse(maxRetryAttemptsEnvString, out var maxRetriesOnErrorParsedInt))
            {
                maxRetryAttempts = maxRetriesOnErrorParsedInt;
            }
        }
        _maxRetryAttempts = maxRetryAttempts;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxProcessor started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_outboxStateService.TryRetrieveNext(out var entry))
                {

                    var headers = new Dictionary<string, byte[]>
                    {
                        ["correlation-id"] = System.Text.Encoding.UTF8.GetBytes(entry.CorrelationId.Value)
                    };

                    var correlation = entry.CorrelationId;

                    bool produced = _kafkaProducerService.Produce(
                        key: entry.Key,
                        value: entry.Value,
                        headers: headers,
                        correlationId: correlation);

                    if (produced)
                    {
                        bool removed = _outboxStateService.RemoveNext();
                        if (!removed)
                        {
                            _logger.LogWarning("Message with correlation ID {CorrId} was produced but could not be removed from the outbox.", correlation.Value);
                        }
                        else
                        {
                            _logger.LogDebug("Successfully shipped and removed outbox entry (CorrelationId={CorrId}).", correlation.Value);
                        }
                        _consecutiveErrors = 0;

                        continue;
                    }

                    _logger.LogError("Failed to produce outbox entry (CorrelationId={CorrId}). Will retry after back‑off.", correlation.Value);
                }
                else
                {
                    await Task.Delay(_emptyQueueDelay, stoppingToken);
                    continue;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in OutboxProcessor loop.");
            }

            _consecutiveErrors++;

            // Exponential back‑off: 2^n * baseDelay (capped)
            var backoff = TimeSpan.FromTicks(
                Math.Min(
                    _errorDelay.Ticks * (1L << Math.Min(_consecutiveErrors, 30)), // protect overflow
                    TimeSpan.FromMinutes(5).Ticks)); // hard ceiling

            // If we have hit the max‑retry‑attempts we “flatten” the back‑off to avoid hammering the broker forever.
            if (_consecutiveErrors > _maxRetryAttempts)
            {
                backoff = TimeSpan.FromSeconds(30);
            }

            _logger.LogWarning(
                "OutboxProcessor will pause for {Backoff}s (consecutive errors = {ErrCount}).",
                backoff.TotalSeconds,
                _consecutiveErrors);

            try
            {
                await Task.Delay(backoff, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("OutboxProcessor stopped.");
    }

    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Flushing OutboxProcessor queue...");

        while (!cancellationToken.IsCancellationRequested)
        {
            if (!_outboxStateService.TryRetrieveNext(out _))
            {
                break;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }

        _logger.LogInformation("Flush complete.");
    }
}
