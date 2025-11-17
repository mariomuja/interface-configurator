namespace ProcessCsvBlobTrigger.Core.Interfaces;

/// <summary>
/// Event queue for triggering message processing when new messages are added to MessageBox
/// </summary>
public interface IEventQueue
{
    /// <summary>
    /// Enqueues an event when a new message is added to MessageBox
    /// </summary>
    /// <param name="messageId">MessageId of the new message</param>
    /// <param name="interfaceName">Interface name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task EnqueueMessageEventAsync(Guid messageId, string interfaceName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Dequeues the next message event for processing
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Message event or null if queue is empty</returns>
    Task<MessageEvent?> DequeueMessageEventAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of pending events in the queue
    /// </summary>
    int PendingEventCount { get; }
}

/// <summary>
/// Represents a message event in the queue
/// </summary>
public class MessageEvent
{
    public Guid MessageId { get; set; }
    public string InterfaceName { get; set; } = string.Empty;
    public DateTime EnqueuedAt { get; set; } = DateTime.UtcNow;
}

