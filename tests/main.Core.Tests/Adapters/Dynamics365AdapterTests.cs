using Microsoft.Extensions.Logging;
using Moq;
using InterfaceConfigurator.Adapters;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Core.Services;
using Xunit;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace InterfaceConfigurator.Main.Core.Tests.Adapters;

/// <summary>
/// Unit tests for Dynamics365Adapter
/// Tests both Source and Destination roles with OAuth and OData API
/// </summary>
public class Dynamics365AdapterTests
{
    private readonly Mock<IMessageBoxService> _mockMessageBoxService;
    private readonly Mock<IMessageSubscriptionService> _mockSubscriptionService;
    private readonly Mock<ILogger<Dynamics365Adapter>> _mockLogger;

    public Dynamics365AdapterTests()
    {
        _mockMessageBoxService = new Mock<IMessageBoxService>();
        _mockSubscriptionService = new Mock<IMessageSubscriptionService>();
        _mockLogger = new Mock<ILogger<Dynamics365Adapter>>();
    }

    [Fact]
    public void Dynamics365Adapter_Constructor_ShouldInitializeProperties()
    {
        // Arrange & Act
        var adapter = new Dynamics365Adapter(
            _mockMessageBoxService.Object,
            _mockSubscriptionService.Object,
            "TestInterface",
            Guid.NewGuid(),
            batchSize: 50,
            adapterRole: "Source",
            logger: _mockLogger.Object,
            tenantId: "tenant-id",
            clientId: "client-id",
            clientSecret: "client-secret",
            instanceUrl: "https://org.crm.dynamics.com",
            entityName: "accounts",
            odataFilter: "name eq 'Test'",
            pollingInterval: 120,
            adapterBatchSize: 200,
            pageSize: 100,
            useBatch: true);

        // Assert
        Assert.Equal("Dynamics365", adapter.AdapterName);
        Assert.Equal("Microsoft Dynamics 365", adapter.AdapterAlias);
        Assert.True(adapter.SupportsRead);
        Assert.True(adapter.SupportsWrite);
    }

    [Fact]
    public async Task Dynamics365Adapter_ReadAsync_WithOData_ShouldReturnRecords()
    {
        // Arrange
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        
        // Mock OAuth token response
        var tokenResponse = JsonSerializer.Serialize(new
        {
            access_token = "test-token",
            expires_in = 3600,
            token_type = "Bearer"
        });

        // Mock OData response
        var odataResponse = JsonSerializer.Serialize(new
        {
            value = new[]
            {
                new { accountid = Guid.NewGuid().ToString(), name = "Account 1", emailaddress1 = "account1@example.com" },
                new { accountid = Guid.NewGuid().ToString(), name = "Account 2", emailaddress1 = "account2@example.com" }
            }
        });

        var callCount = 0;
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    // First call: OAuth token
                    return new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = new StringContent(tokenResponse, Encoding.UTF8, "application/json")
                    };
                }
                else
                {
                    // Second call: OData query
                    return new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = new StringContent(odataResponse, Encoding.UTF8, "application/json")
                    };
                }
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);

        var adapter = new Dynamics365Adapter(
            _mockMessageBoxService.Object,
            _mockSubscriptionService.Object,
            "TestInterface",
            Guid.NewGuid(),
            adapterRole: "Source",
            logger: _mockLogger.Object,
            tenantId: "tenant-id",
            clientId: "client-id",
            clientSecret: "client-secret",
            instanceUrl: "https://org.crm.dynamics.com",
            entityName: "accounts",
            httpClient: httpClient);

        // Act
        var (headers, records) = await adapter.ReadAsync("source");

        // Assert
        Assert.NotEmpty(headers);
        Assert.NotEmpty(records);
        Assert.Contains("accountid", headers);
        Assert.Equal(2, records.Count);
    }

    [Fact]
    public async Task Dynamics365Adapter_WriteAsync_WithBatch_ShouldSendRecords()
    {
        // Arrange
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        
        var tokenResponse = JsonSerializer.Serialize(new
        {
            access_token = "test-token",
            expires_in = 3600
        });

        var callCount = 0;
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    return new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = new StringContent(tokenResponse, Encoding.UTF8, "application/json")
                    };
                }
                else
                {
                    return new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.Created,
                        Content = new StringContent("{}", Encoding.UTF8, "application/json")
                    };
                }
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);

        var adapter = new Dynamics365Adapter(
            _mockMessageBoxService.Object,
            _mockSubscriptionService.Object,
            "TestInterface",
            Guid.NewGuid(),
            adapterRole: "Destination",
            logger: _mockLogger.Object,
            tenantId: "tenant-id",
            clientId: "client-id",
            clientSecret: "client-secret",
            instanceUrl: "https://org.crm.dynamics.com",
            entityName: "accounts",
            useBatch: true,
            httpClient: httpClient);

        var headers = new List<string> { "accountid", "name", "emailaddress1" };
        var records = new List<Dictionary<string, string>>
        {
            new Dictionary<string, string> { { "name", "Account 1" }, { "emailaddress1", "account1@example.com" } },
            new Dictionary<string, string> { { "name", "Account 2" }, { "emailaddress1", "account2@example.com" } }
        };

        // Act
        await adapter.WriteAsync("destination", headers, records);

        // Assert
        Assert.True(callCount >= 2); // At least token + batch request
    }

    [Fact]
    public async Task Dynamics365Adapter_GetSchemaAsync_ShouldReturnSchema()
    {
        // Arrange
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        
        var tokenResponse = JsonSerializer.Serialize(new
        {
            access_token = "test-token",
            expires_in = 3600
        });

        var metadataResponse = JsonSerializer.Serialize(new
        {
            value = new[]
            {
                new { LogicalName = "accountid", AttributeType = "Uniqueidentifier" },
                new { LogicalName = "name", AttributeType = "String", MaxLength = 160 }
            }
        });

        var callCount = 0;
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    return new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = new StringContent(tokenResponse, Encoding.UTF8, "application/json")
                    };
                }
                else
                {
                    return new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = new StringContent(metadataResponse, Encoding.UTF8, "application/json")
                    };
                }
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);

        var adapter = new Dynamics365Adapter(
            _mockMessageBoxService.Object,
            _mockSubscriptionService.Object,
            "TestInterface",
            Guid.NewGuid(),
            logger: _mockLogger.Object,
            tenantId: "tenant-id",
            clientId: "client-id",
            clientSecret: "client-secret",
            instanceUrl: "https://org.crm.dynamics.com",
            entityName: "accounts",
            httpClient: httpClient);

        // Act
        var schema = await adapter.GetSchemaAsync("source");

        // Assert
        Assert.NotEmpty(schema);
    }

    [Fact]
    public void Dynamics365Adapter_ReadAsync_WithMissingCredentials_ShouldThrowException()
    {
        // Arrange
        var adapter = new Dynamics365Adapter(
            _mockMessageBoxService.Object,
            _mockSubscriptionService.Object,
            "TestInterface",
            Guid.NewGuid(),
            adapterRole: "Source",
            logger: _mockLogger.Object,
            instanceUrl: "https://org.crm.dynamics.com",
            entityName: "accounts");

        // Act & Assert
        Assert.ThrowsAsync<InvalidOperationException>(() => adapter.ReadAsync("source"));
    }

    [Fact]
    public async Task Dynamics365Adapter_EnsureDestinationStructureAsync_ShouldComplete()
    {
        // Arrange
        var adapter = new Dynamics365Adapter(
            _mockMessageBoxService.Object,
            _mockSubscriptionService.Object,
            "TestInterface",
            Guid.NewGuid(),
            adapterRole: "Destination",
            logger: _mockLogger.Object);

        var columnTypes = new Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo>
        {
            { "accountid", new CsvColumnAnalyzer.ColumnTypeInfo { DataType = CsvColumnAnalyzer.SqlDataType.NVARCHAR, MaxLength = 36 } }
        };

        // Act & Assert (should not throw)
        await adapter.EnsureDestinationStructureAsync("destination", columnTypes);
    }
}

