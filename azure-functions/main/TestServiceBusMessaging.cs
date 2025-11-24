using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Helpers;

namespace InterfaceConfigurator.Main;

/// <summary>
/// HTTP Function to test Service Bus messaging flow
/// Tests both source adapters (writing to Service Bus) and destination adapters (reading from Service Bus)
/// </summary>
public class TestServiceBusMessaging
{
    private readonly IServiceBusService _serviceBusService;
    private readonly IServiceBusSubscriptionService _subscriptionService;
    private readonly IInterfaceConfigurationService _configService;
    private readonly ILogger<TestServiceBusMessaging> _logger;

    public TestServiceBusMessaging(
        IServiceBusService serviceBusService,
        IServiceBusSubscriptionService subscriptionService,
        IInterfaceConfigurationService configService,
        ILogger<TestServiceBusMessaging> logger)
    {
        _serviceBusService = serviceBusService ?? throw new ArgumentNullException(nameof(serviceBusService));
        _subscriptionService = subscriptionService ?? throw new ArgumentNullException(nameof(subscriptionService));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("TestServiceBusMessaging")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", "options", Route = "TestServiceBusMessaging")] HttpRequestData req,
        FunctionContext executionContext)
    {
        _logger.LogInformation("TestServiceBusMessaging function triggered");

        // Handle CORS preflight requests
        if (req.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
        {
            var optionsResponse = req.CreateResponse(System.Net.HttpStatusCode.OK);
            CorsHelper.AddCorsHeaders(optionsResponse);
            return optionsResponse;
        }

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var request = JsonSerializer.Deserialize<TestServiceBusRequest>(requestBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (request == null || string.IsNullOrWhiteSpace(request.InterfaceName))
            {
                var badRequestResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                badRequestResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
                CorsHelper.AddCorsHeaders(badRequestResponse);
                await badRequestResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = "InterfaceName is required" }));
                return badRequestResponse;
            }

            var results = new List<TestResult>();

            // Test 1: Ensure topic exists
            _logger.LogInformation("Test 1: Ensuring Service Bus topic exists for interface '{InterfaceName}'", request.InterfaceName);
            try
            {
                await _subscriptionService.EnsureTopicExistsAsync(request.InterfaceName, executionContext.CancellationToken);
                results.Add(new TestResult
                {
                    TestName = "EnsureTopicExists",
                    Success = true,
                    Message = $"Topic 'interface-{request.InterfaceName.ToLowerInvariant()}' exists or was created successfully"
                });
            }
            catch (Exception ex)
            {
                results.Add(new TestResult
                {
                    TestName = "EnsureTopicExists",
                    Success = false,
                    Message = $"Failed to ensure topic exists: {ex.Message}"
                });
            }

            // Test 2: Get source adapter instances and test writing to Service Bus
            var config = await _configService.GetConfigurationAsync(request.InterfaceName, executionContext.CancellationToken);
            if (config != null && config.Sources.Count > 0)
            {
                _logger.LogInformation("Test 2: Testing source adapter message writing to Service Bus");
                foreach (var source in config.Sources.Values)
                {
                    try
                    {
                        var testHeaders = new List<string> { "Id", "Name", "Email" };
                        var testRecords = new List<Dictionary<string, string>>
                        {
                            new Dictionary<string, string>
                            {
                                { "Id", "1" },
                                { "Name", "Test User 1" },
                                { "Email", "test1@example.com" }
                            },
                            new Dictionary<string, string>
                            {
                                { "Id", "2" },
                                { "Name", "Test User 2" },
                                { "Email", "test2@example.com" }
                            }
                        };

                        var messageIds = await _serviceBusService.SendMessagesAsync(
                            request.InterfaceName,
                            source.AdapterName,
                            "Source",
                            source.AdapterInstanceGuid,
                            testHeaders,
                            testRecords,
                            executionContext.CancellationToken);

                        results.Add(new TestResult
                        {
                            TestName = $"SourceAdapterWrite_{source.InstanceName}_{source.AdapterName}",
                            Success = true,
                            Message = $"Successfully sent {messageIds.Count} messages to Service Bus topic",
                            Details = new Dictionary<string, object>
                            {
                                { "AdapterInstanceGuid", source.AdapterInstanceGuid },
                                { "MessageIds", messageIds },
                                { "RecordsSent", testRecords.Count }
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        results.Add(new TestResult
                        {
                            TestName = $"SourceAdapterWrite_{source.InstanceName}_{source.AdapterName}",
                            Success = false,
                            Message = $"Failed to write messages: {ex.Message}",
                            Details = new Dictionary<string, object>
                            {
                                { "AdapterInstanceGuid", source.AdapterInstanceGuid },
                                { "Error", ex.ToString() }
                            }
                        });
                    }
                }
            }
            else
            {
                results.Add(new TestResult
                {
                    TestName = "SourceAdapterWrite",
                    Success = false,
                    Message = "No source adapter instances found for this interface"
                });
            }

            // Test 3: Get destination adapter instances, ensure subscriptions exist, and test reading from Service Bus
            if (config != null && config.Destinations.Count > 0)
            {
                _logger.LogInformation("Test 3: Testing destination adapter message reading from Service Bus");
                foreach (var destination in config.Destinations.Values)
                {
                    try
                    {
                        // Ensure subscription exists
                        await _subscriptionService.CreateSubscriptionAsync(
                            request.InterfaceName,
                            destination.AdapterInstanceGuid,
                            executionContext.CancellationToken);

                        // Wait a bit for messages to be available
                        await Task.Delay(2000, executionContext.CancellationToken);

                        // Try to receive messages
                        var messages = await _serviceBusService.ReceiveMessagesAsync(
                            request.InterfaceName,
                            destination.AdapterInstanceGuid,
                            maxMessages: 10,
                            executionContext.CancellationToken);

                        if (messages != null && messages.Count > 0)
                        {
                            results.Add(new TestResult
                            {
                                TestName = $"DestinationAdapterRead_{destination.InstanceName}_{destination.AdapterName}",
                                Success = true,
                                Message = $"Successfully received {messages.Count} messages from Service Bus subscription",
                                Details = new Dictionary<string, object>
                                {
                                    { "AdapterInstanceGuid", destination.AdapterInstanceGuid },
                                    { "MessagesReceived", messages.Count },
                                    { "MessageIds", messages.Select(m => m.MessageId).ToList() },
                                    { "SampleMessage", new
                                        {
                                            MessageId = messages[0].MessageId,
                                            InterfaceName = messages[0].InterfaceName,
                                            AdapterName = messages[0].AdapterName,
                                            Headers = messages[0].Headers,
                                            RecordKeys = messages[0].Record?.Keys.ToList()
                                        }
                                    }
                                }
                            });

                            // Complete messages to clean up
                            foreach (var message in messages)
                            {
                                try
                                {
                                    await _serviceBusService.CompleteMessageAsync(
                                        message.MessageId,
                                        message.LockToken,
                                        executionContext.CancellationToken);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "Failed to complete message {MessageId}", message.MessageId);
                                }
                            }
                        }
                        else
                        {
                            results.Add(new TestResult
                            {
                                TestName = $"DestinationAdapterRead_{destination.InstanceName}_{destination.AdapterName}",
                                Success = true,
                                Message = "No messages available in subscription (this is OK if no source messages were sent)",
                                Details = new Dictionary<string, object>
                                {
                                    { "AdapterInstanceGuid", destination.AdapterInstanceGuid },
                                    { "SubscriptionName", $"destination-{destination.AdapterInstanceGuid.ToString().ToLowerInvariant()}" }
                                }
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        results.Add(new TestResult
                        {
                            TestName = $"DestinationAdapterRead_{destination.InstanceName}_{destination.AdapterName}",
                            Success = false,
                            Message = $"Failed to read messages: {ex.Message}",
                            Details = new Dictionary<string, object>
                            {
                                { "AdapterInstanceGuid", destination.AdapterInstanceGuid },
                                { "Error", ex.ToString() }
                            }
                        });
                    }
                }
            }
            else
            {
                results.Add(new TestResult
                {
                    TestName = "DestinationAdapterRead",
                    Success = false,
                    Message = "No destination adapter instances found for this interface"
                });
            }

            // Summary
            var successCount = results.Count(r => r.Success);
            var totalCount = results.Count;
            var summary = $"Tests completed: {successCount}/{totalCount} passed";

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(response);
            await response.WriteStringAsync(JsonSerializer.Serialize(new
            {
                success = successCount == totalCount,
                summary = summary,
                results = results
            }, new JsonSerializerOptions { WriteIndented = true }));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing Service Bus messaging");
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(errorResponse);
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = ex.Message, details = ex.ToString() }));
            return errorResponse;
        }
    }

    private class TestServiceBusRequest
    {
        public string InterfaceName { get; set; } = string.Empty;
    }

    private class TestResult
    {
        public string TestName { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public Dictionary<string, object>? Details { get; set; }
    }
}

