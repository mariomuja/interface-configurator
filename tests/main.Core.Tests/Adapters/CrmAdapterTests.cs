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
/// Unit tests for CrmAdapter
/// Tests both Source and Destination roles with FetchXML and Web API
/// </summary>
public class CrmAdapterTests
{
    private readonly Mock<IMessageBoxService> _mockMessageBoxService;
    private readonly Mock<IMessageSubscriptionService> _mockSubscriptionService;
    private readonly Mock<ILogger<CrmAdapter>> _mockLogger;

    public CrmAdapterTests()
    {
        _mockMessageBoxService = new Mock<IMessageBoxService>();
        _mockSubscriptionService = new Mock<IMessageSubscriptionService>();
        _mockLogger = new Mock<ILogger<CrmAdapter>>();
    }

    [Fact]
    public void CrmAdapter_Constructor_ShouldInitializeProperties()
    {
        // Arrange & Act
        var adapter = new CrmAdapter(
            _mockMessageBoxService.Object,
            _mockSubscriptionService.Object,
            "TestInterface",
            Guid.NewGuid(),
            batchSize: 50,
            adapterRole: "Source",
            logger: _mockLogger.Object,
            organizationUrl: "https://org.crm.dynamics.com",
            username: "testuser",
            password: "testpass",
            entityName: "contacts",
            fetchXml: "<fetch><entity name='contact'/></fetch>",
            pollingInterval: 120,
            adapterBatchSize: 200,
            useBatch: true);

        // Assert
        Assert.Equal("CRM", adapter.AdapterName);
        Assert.Equal("Microsoft CRM", adapter.AdapterAlias);
        Assert.True(adapter.SupportsRead);
        Assert.True(adapter.SupportsWrite);
    }

    [Fact]
    public async Task CrmAdapter_ReadAsync_WithFetchXml_ShouldReturnRecords()
    {
        // Arrange
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        
        var fetchXmlResponse = JsonSerializer.Serialize(new
        {
            value = new[]
            {
                new { contactid = Guid.NewGuid().ToString(), firstname = "John", lastname = "Doe", emailaddress1 = "john@example.com" },
                new { contactid = Guid.NewGuid().ToString(), firstname = "Jane", lastname = "Smith", emailaddress1 = "jane@example.com" }
            }
        });

        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(fetchXmlResponse, Encoding.UTF8, "application/json")
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);

        var adapter = new CrmAdapter(
            _mockMessageBoxService.Object,
            _mockSubscriptionService.Object,
            "TestInterface",
            Guid.NewGuid(),
            adapterRole: "Source",
            logger: _mockLogger.Object,
            organizationUrl: "https://org.crm.dynamics.com",
            username: "testuser",
            password: "testpass",
            entityName: "contacts",
            fetchXml: "<fetch><entity name='contact'/></fetch>",
            httpClient: httpClient);

        // Act
        var (headers, records) = await adapter.ReadAsync("source");

        // Assert
        Assert.NotEmpty(headers);
        Assert.NotEmpty(records);
        Assert.Contains("contactid", headers);
        Assert.Equal(2, records.Count);
    }

    [Fact]
    public async Task CrmAdapter_ReadAsync_WithOData_ShouldReturnRecords()
    {
        // Arrange
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        
        var odataResponse = JsonSerializer.Serialize(new
        {
            value = new[]
            {
                new { contactid = Guid.NewGuid().ToString(), firstname = "John", lastname = "Doe" }
            }
        });

        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(odataResponse, Encoding.UTF8, "application/json")
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);

        var adapter = new CrmAdapter(
            _mockMessageBoxService.Object,
            _mockSubscriptionService.Object,
            "TestInterface",
            Guid.NewGuid(),
            adapterRole: "Source",
            logger: _mockLogger.Object,
            organizationUrl: "https://org.crm.dynamics.com",
            username: "testuser",
            password: "testpass",
            entityName: "contacts",
            httpClient: httpClient);

        // Act
        var (headers, records) = await adapter.ReadAsync("source");

        // Assert
        Assert.NotEmpty(headers);
        Assert.NotEmpty(records);
        Assert.Single(records);
    }

    [Fact]
    public async Task CrmAdapter_WriteAsync_WithExecuteMultiple_ShouldSendRecords()
    {
        // Arrange
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        
        var executeMultipleResponse = JsonSerializer.Serialize(new
        {
            Responses = new[]
            {
                new { RequestIndex = 0, Response = new { Id = Guid.NewGuid().ToString() } },
                new { RequestIndex = 1, Response = new { Id = Guid.NewGuid().ToString() } }
            }
        });

        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(executeMultipleResponse, Encoding.UTF8, "application/json")
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);

        var adapter = new CrmAdapter(
            _mockMessageBoxService.Object,
            _mockSubscriptionService.Object,
            "TestInterface",
            Guid.NewGuid(),
            adapterRole: "Destination",
            logger: _mockLogger.Object,
            organizationUrl: "https://org.crm.dynamics.com",
            username: "testuser",
            password: "testpass",
            entityName: "contacts",
            useBatch: true,
            httpClient: httpClient);

        var headers = new List<string> { "firstname", "lastname", "emailaddress1" };
        var records = new List<Dictionary<string, string>>
        {
            new Dictionary<string, string> { { "firstname", "John" }, { "lastname", "Doe" }, { "emailaddress1", "john@example.com" } },
            new Dictionary<string, string> { { "firstname", "Jane" }, { "lastname", "Smith" }, { "emailaddress1", "jane@example.com" } }
        };

        // Act
        await adapter.WriteAsync("destination", headers, records);

        // Assert
        // Should complete without exception
    }

    [Fact]
    public async Task CrmAdapter_GetSchemaAsync_ShouldReturnSchema()
    {
        // Arrange
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        
        var metadataResponse = JsonSerializer.Serialize(new
        {
            value = new[]
            {
                new { LogicalName = "contactid", AttributeType = "Uniqueidentifier" },
                new { LogicalName = "firstname", AttributeType = "String", MaxLength = 50 }
            }
        });

        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(metadataResponse, Encoding.UTF8, "application/json")
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);

        var adapter = new CrmAdapter(
            _mockMessageBoxService.Object,
            _mockSubscriptionService.Object,
            "TestInterface",
            Guid.NewGuid(),
            logger: _mockLogger.Object,
            organizationUrl: "https://org.crm.dynamics.com",
            username: "testuser",
            password: "testpass",
            entityName: "contacts",
            httpClient: httpClient);

        // Act
        var schema = await adapter.GetSchemaAsync("source");

        // Assert
        Assert.NotEmpty(schema);
    }

    [Fact]
    public void CrmAdapter_ReadAsync_WithMissingCredentials_ShouldThrowException()
    {
        // Arrange
        var adapter = new CrmAdapter(
            _mockMessageBoxService.Object,
            _mockSubscriptionService.Object,
            "TestInterface",
            Guid.NewGuid(),
            adapterRole: "Source",
            logger: _mockLogger.Object,
            organizationUrl: "https://org.crm.dynamics.com",
            entityName: "contacts");

        // Act & Assert
        Assert.ThrowsAsync<InvalidOperationException>(() => adapter.ReadAsync("source"));
    }

    [Fact]
    public async Task CrmAdapter_EnsureDestinationStructureAsync_ShouldComplete()
    {
        // Arrange
        var adapter = new CrmAdapter(
            _mockMessageBoxService.Object,
            _mockSubscriptionService.Object,
            "TestInterface",
            Guid.NewGuid(),
            adapterRole: "Destination",
            logger: _mockLogger.Object);

        var columnTypes = new Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo>
        {
            { "contactid", new CsvColumnAnalyzer.ColumnTypeInfo { DataType = CsvColumnAnalyzer.SqlDataType.NVARCHAR, MaxLength = 36 } }
        };

        // Act & Assert (should not throw)
        await adapter.EnsureDestinationStructureAsync("destination", columnTypes);
    }
}

