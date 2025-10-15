using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using DataTypes;

namespace DistributedCache.Storage.Outbox;

public class StorageOutboxConcurrentQueue: IStorageOutbox
{
    private readonly ILogger<StorageOutboxSqlite> _logger;
    private readonly ActivitySource _activitySource;
    private readonly Counter<long> _deleteNextRequestedCounter;
    private readonly Counter<long> _deleteNextFailedCounter;
    private readonly Counter<long> _enqueueRequestedCounter;
    private readonly Counter<long> _enqueueFailedCounter;
    private readonly Counter<long> _retrieveRequestedCounter;
    private readonly Counter<long> _markNextFailedCounter;

    private readonly ConcurrentQueue<DcItem> _outboxItemQueue;
    private readonly ConcurrentQueue<DcItem> _outboxFailedItemsQueue;

    public StorageOutboxConcurrentQueue(ILogger<StorageOutboxSqlite> logger, ActivitySource activitySource, Meter meter)
    {
        _logger = logger;
        _activitySource = activitySource;
        _deleteNextRequestedCounter = meter.CreateCounter<long>("storage.outbox.concurrentQueue.deleteNext.requested", description: "Number of requests for removing an entry from the outbox storage");
        _deleteNextFailedCounter = meter.CreateCounter<long>("storage.outbox.concurrentQueue.deleteNext.failed", description: "Number of requests for removing an entry from the outbox storage that have failed");
        _enqueueRequestedCounter = meter.CreateCounter<long>("storage.outbox.concurrentQueue.enqueue.requested", description: "Number of requests for storing an entry in the outbox storage");
        _enqueueFailedCounter = meter.CreateCounter<long>("storage.outbox.concurrentQueue.enqueue.failed", description: "Number of requests for storing an entry in the outbox storage that have failed");
        _retrieveRequestedCounter = meter.CreateCounter<long>("storage.outbox.concurrentQueue.retrieve.requested", description: "Number of requests for retrieving an entry from the outbox storage");
        _markNextFailedCounter = meter.CreateCounter<long>("storage.outbox.concurrentQueue.markNextFailed", description: "Number of times items in outbox have been marked as failed");

        _outboxItemQueue = new ConcurrentQueue<DcItem>();
        _outboxFailedItemsQueue = new ConcurrentQueue<DcItem>();

        _logger.LogDebug($"{nameof(StorageOutboxSqlite)} initialized");
    }

    public Error? Enqueue(DcItem item)
    {
        var span = _activitySource.StartActivity("storage.outbox.concurrentQueue.enqueueNext");
        _enqueueRequestedCounter.Add(1);
        span?.AddEvent(new ActivityEvent("Enqueued next", DateTimeOffset.UtcNow));
        _outboxItemQueue.Enqueue(item);
        return null;
    }

    public (Error? Error, DcItem? NextItem) RetrieveNext()
    {
        var span = _activitySource.StartActivity("storage.outbox.concurrentQueue.retrieveNext");
        _retrieveRequestedCounter.Add(1);
        var outboxEmpty = _outboxItemQueue.IsEmpty;
        span?.AddEvent(new ActivityEvent("Checked if outbox empty", DateTimeOffset.UtcNow));
        if (outboxEmpty)
        {
            return (Error: null, NextItem: null);
        }
        var peekGotItem = _outboxItemQueue.TryPeek(out var item);
        span?.AddEvent(new ActivityEvent("Peeked next item", DateTimeOffset.UtcNow));
        if (peekGotItem)
        {
            return (Error: null, NextItem: item);
        }
        _logger.LogError("Peek next from queue failed");
        return (Error: new Error { Message = "Failed to retrieve next item from in mem queue" }, NextItem: null);
    }

    public Error? DeleteNext()
    {
        var span = _activitySource.StartActivity("storage.outbox.concurrentQueue.deleteNext");
        _deleteNextRequestedCounter.Add(1);
        var dequeueSuccess = _outboxItemQueue.TryDequeue(out _);
        span?.AddEvent(new ActivityEvent("Dequeued next done", DateTimeOffset.UtcNow));
        if (dequeueSuccess)
        {
            return null;
        }
        _deleteNextFailedCounter.Add(1);
        return new Error { Message = "Failed to delete next item from in mem queue" };
    }

    public Error? MarkNextFailed()
    {
        _markNextFailedCounter.Add(1);
        var dequeueSuccess = _outboxItemQueue.TryDequeue(out var dequeued);
        if (dequeueSuccess && dequeued != null )
        {
            _outboxFailedItemsQueue.Enqueue(dequeued);
        }

        return null;
    }
}
