using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using ProcessCsvBlobTrigger.Core.Interfaces;

namespace ProcessCsvBlobTrigger.Services;

/// <summary>
/// In-memory event queue implementation for message processing events
/// </summary>
public class InMemoryEventQueue : IEventQueue
{
    private readonly ConcurrentQueue<MessageEvent> _eventQueue = new();
    private readonly ILogger<InMemoryEventQueue>? _logger;

    public InMemoryEventQueue(ILogger<InMemoryEventQueue>? logger = null)
    {
        _logger = logger;
    }

    public int PendingEventCount => _eventQueue.Count;

    public Task EnqueueMessageEventAsync(Guid messageId, string interfaceName, CancellationToken cancellationToken = default)
    {
        var messageEvent = new MessageEvent
        {
            MessageId = messageId,
            InterfaceName = interfaceName,
            EnqueuedAt = DateTime.UtcNow
        };

        _eventQueue.Enqueue(messageEvent);
        _logger?.LogInformation("Enqueued message event: MessageId={MessageId}, Interface={InterfaceName}, QueueSize={QueueSize}",
            messageId, interfaceName, _eventQueue.Count);

        return Task.CompletedTask;
    }

    public Task<MessageEvent?> DequeueMessageEventAsync(CancellationToken cancellationToken = default)
    {
        if (_eventQueue.TryDequeue(out var messageEvent))
        {
            _logger?.LogInformation("Dequeued message event: MessageId={MessageId}, Interface={InterfaceName}, QueueSize={QueueSize}",
                messageEvent.MessageId, messageEvent.InterfaceName, _eventQueue.Count);
            return Task.FromResult<MessageEvent?>(messageEvent);
        }

        return Task.FromResult<MessageEvent?>(null);
    }
}




