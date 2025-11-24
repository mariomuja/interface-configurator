using InterfaceConfigurator.Main.Core.Models;

namespace InterfaceConfigurator.Main.Core.Interfaces;

/// <summary>
/// Service for reading and writing messages to Azure Service Bus
/// Replaces MessageBox database with Service Bus queues/topics
/// </summary>
public interface IServiceBusService
{
    /// <summary>
    /// Sends a single record message to Service Bus (debatching)
    /// Each record is sent as a separate message
    /// </summary>
    /// <param name="interfaceName">Name of the interface (e.g., "FromCsvToSqlServerExample")</param>
    /// <param name="adapterName">Name of the adapter (e.g., "CSV", "SqlServer")</param>
    /// <param name="adapterType">Type of adapter: "Source" or "Destination"</param>
    /// <param name="adapterInstanceGuid">GUID identifying the adapter instance</param>
    /// <param name="headers">Column headers</param>
    /// <param name="record">Single data record (debatching)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>MessageId of the sent message</returns>
    Task<string> SendMessageAsync(
        string interfaceName,
        string adapterName,
        string adapterType,
        Guid adapterInstanceGuid,
        List<string> headers,
        Dictionary<string, string> record,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends multiple records to Service Bus (debatching - creates one message per record)
    /// </summary>
    /// <param name="interfaceName">Name of the interface</param>
    /// <param name="adapterName">Name of the adapter</param>
    /// <param name="adapterType">Type of adapter: "Source" or "Destination"</param>
    /// <param name="adapterInstanceGuid">GUID identifying the adapter instance</param>
    /// <param name="headers">Column headers</param>
    /// <param name="records">Data records (will be debatched into individual messages)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of MessageIds created</returns>
    Task<List<string>> SendMessagesAsync(
        string interfaceName,
        string adapterName,
        string adapterType,
        Guid adapterInstanceGuid,
        List<string> headers,
        List<Dictionary<string, string>> records,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Receives messages from Service Bus for a destination adapter instance
    /// Messages are filtered by interface name and source adapter instance
    /// </summary>
    /// <param name="interfaceName">Name of the interface</param>
    /// <param name="destinationAdapterInstanceGuid">GUID of the destination adapter instance</param>
    /// <param name="maxMessages">Maximum number of messages to receive (default: 10)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of received messages</returns>
    Task<List<ServiceBusMessage>> ReceiveMessagesAsync(
        string interfaceName,
        Guid destinationAdapterInstanceGuid,
        int maxMessages = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Completes (acknowledges) a message after successful processing
    /// </summary>
    /// <param name="messageId">Message ID</param>
    /// <param name="lockToken">Lock token from the received message</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task CompleteMessageAsync(
        string messageId,
        string lockToken,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Abandons a message (returns it to the queue for retry)
    /// </summary>
    /// <param name="messageId">Message ID</param>
    /// <param name="lockToken">Lock token from the received message</param>
    /// <param name="propertiesToModify">Optional properties to modify</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task AbandonMessageAsync(
        string messageId,
        string lockToken,
        Dictionary<string, object>? propertiesToModify = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a message to the dead letter queue
    /// </summary>
    /// <param name="messageId">Message ID</param>
    /// <param name="lockToken">Lock token from the received message</param>
    /// <param name="reason">Reason for dead lettering</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeadLetterMessageAsync(
        string messageId,
        string lockToken,
        string reason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets message count for an interface (for UI display)
    /// </summary>
    /// <param name="interfaceName">Name of the interface</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Message count</returns>
    Task<int> GetMessageCountAsync(
        string interfaceName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets recent messages for an interface (for UI display)
    /// </summary>
    /// <param name="interfaceName">Name of the interface</param>
    /// <param name="maxMessages">Maximum number of messages to retrieve</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of messages</returns>
    Task<List<ServiceBusMessage>> GetRecentMessagesAsync(
        string interfaceName,
        int maxMessages = 100,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a Service Bus message
/// </summary>
public class ServiceBusMessage
{
    public string MessageId { get; set; } = string.Empty;
    public string InterfaceName { get; set; } = string.Empty;
    public string AdapterName { get; set; } = string.Empty;
    public string AdapterType { get; set; } = string.Empty;
    public Guid AdapterInstanceGuid { get; set; }
    public List<string> Headers { get; set; } = new();
    public Dictionary<string, string> Record { get; set; } = new();
    public DateTime EnqueuedTime { get; set; }
    public string LockToken { get; set; } = string.Empty;
    public int DeliveryCount { get; set; }
    public Dictionary<string, object> Properties { get; set; } = new();
}


