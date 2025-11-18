using ProcessCsvBlobTrigger.Core.Models;

namespace ProcessCsvBlobTrigger.Core.Interfaces;

/// <summary>
/// Service for reading and writing messages to the MessageBox staging area
/// </summary>
public interface IMessageBoxService
{
    /// <summary>
    /// Writes a single record message to the MessageBox (debatching)
    /// Each record is written as a separate message, triggering an event for each
    /// </summary>
    /// <param name="interfaceName">Name of the interface (e.g., "FromCsvToSqlServerExample")</param>
    /// <param name="adapterName">Name of the adapter (e.g., "CSV", "SqlServer")</param>
    /// <param name="adapterType">Type of adapter: "Source" or "Destination"</param>
    /// <param name="adapterInstanceGuid">GUID identifying the adapter instance</param>
    /// <param name="headers">Column headers</param>
    /// <param name="record">Single data record (debatching)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>MessageId of the created message</returns>
    Task<Guid> WriteSingleRecordMessageAsync(
        string interfaceName,
        string adapterName,
        string adapterType,
        Guid adapterInstanceGuid,
        List<string> headers,
        Dictionary<string, string> record,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes multiple records to MessageBox (debatching - creates one message per record)
    /// </summary>
    /// <param name="interfaceName">Name of the interface</param>
    /// <param name="adapterName">Name of the adapter</param>
    /// <param name="adapterType">Type of adapter: "Source" or "Destination"</param>
    /// <param name="adapterInstanceGuid">GUID identifying the adapter instance</param>
    /// <param name="headers">Column headers</param>
    /// <param name="records">Data records (will be debatched into individual messages)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of MessageIds created</returns>
    Task<List<Guid>> WriteMessagesAsync(
        string interfaceName,
        string adapterName,
        string adapterType,
        Guid adapterInstanceGuid,
        List<string> headers,
        List<Dictionary<string, string>> records,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures an adapter instance exists in the AdapterInstances table
    /// Creates or updates the adapter instance record
    /// </summary>
    /// <param name="adapterInstanceGuid">GUID identifying the adapter instance</param>
    /// <param name="interfaceName">Name of the interface</param>
    /// <param name="instanceName">User-editable instance name</param>
    /// <param name="adapterName">Name of the adapter</param>
    /// <param name="adapterType">Type of adapter: "Source" or "Destination"</param>
    /// <param name="isEnabled">Whether the adapter instance is enabled</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task EnsureAdapterInstanceAsync(
        Guid adapterInstanceGuid,
        string interfaceName,
        string instanceName,
        string adapterName,
        string adapterType,
        bool isEnabled,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads messages from the MessageBox by interface name and status
    /// </summary>
    /// <param name="interfaceName">Name of the interface</param>
    /// <param name="status">Status filter (e.g., "Pending", "Processed", null for all)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of messages</returns>
    Task<List<MessageBoxMessage>> ReadMessagesAsync(
        string interfaceName,
        string? status = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a single message by MessageId
    /// </summary>
    /// <param name="messageId">MessageId</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Message or null if not found</returns>
    Task<MessageBoxMessage?> ReadMessageAsync(
        Guid messageId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a message as processed
    /// </summary>
    /// <param name="messageId">MessageId</param>
    /// <param name="processingDetails">Optional processing details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task MarkMessageAsProcessedAsync(
        Guid messageId,
        string? processingDetails = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a message as error
    /// </summary>
    /// <param name="messageId">MessageId</param>
    /// <param name="errorMessage">Error message</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task MarkMessageAsErrorAsync(
        Guid messageId,
        string errorMessage,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts headers and single record from a MessageBox message
    /// </summary>
    /// <param name="message">MessageBox message</param>
    /// <returns>Tuple containing headers and single record</returns>
    (List<string> headers, Dictionary<string, string> record) ExtractDataFromMessage(MessageBoxMessage message);

    /// <summary>
    /// Removes a message from MessageBox (only after all subscriptions are processed)
    /// </summary>
    /// <param name="messageId">MessageId</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task RemoveMessageAsync(Guid messageId, CancellationToken cancellationToken = default);
}


