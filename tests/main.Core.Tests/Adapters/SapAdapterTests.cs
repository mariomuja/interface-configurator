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
/// Unit tests for SapAdapter
/// Tests both Source and Destination roles with OData, REST API, and RFC connections
/// </summary>
public class SapAdapterTests
{
    private readonly Mock<IMessageBoxService> _mockMessageBoxService;
    private readonly Mock<IMessageSubscriptionService> _mockSubscriptionService;
    private readonly Mock<ILogger<SapAdapter>> _mockLogger;

    public SapAdapterTests()
    {
        _mockMessageBoxService = new Mock<IMessageBoxService>();
        _mockSubscriptionService = new Mock<IMessageSubscriptionService>();
        _mockLogger = new Mock<ILogger<SapAdapter>>();
    }

    [Fact]
    public void SapAdapter_Constructor_ShouldInitializeProperties()
    {
        // Arrange & Act
        var adapter = new SapAdapter(
            _mockMessageBoxService.Object,
            _mockSubscriptionService.Object,
            "TestInterface",
            Guid.NewGuid(),
            batchSize: 50,
            adapterRole: "Source",
            logger: _mockLogger.Object,
            sapApplicationServer: "sap.example.com",
            sapSystemNumber: "00",
            sapClient: "100",
            sapUsername: "testuser",
            sapPassword: "testpass",
            sapLanguage: "EN",
            sapIdocType: "MATMAS01",
            sapIdocMessageType: "MATMAS",
            sapIdocFilter: "STATUS eq '30'",
            sapPollingInterval: 120,
            sapBatchSize: 200,
            sapConnectionTimeout: 60,
            sapUseRfc: true,
            sapRfcDestination: "TEST_DEST",
            sapReceiverPort: "PORT01",
            sapReceiverPartner: "PARTNER01",
            sapRfcFunctionModule: "IDOC_INBOUND_ASYNCHRONOUS",
            sapRfcParameters: "{\"PARAM1\":\"VALUE1\"}",
            sapODataServiceUrl: "/sap/opu/odata/sap/API_MATERIAL_SRV",
            sapRestApiEndpoint: "/api/v1/materials",
            sapUseOData: false,
            sapUseRestApi: false);

        // Assert
        Assert.Equal("SAP", adapter.AdapterName);
        Assert.Equal("SAP IDOC", adapter.AdapterAlias);
        Assert.True(adapter.SupportsRead);
        Assert.True(adapter.SupportsWrite);
    }

    [Fact]
    public async Task SapAdapter_ReadAsync_WithOData_ShouldReturnRecords()
    {
        // Arrange
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        var responseContent = JsonSerializer.Serialize(new
        {
            value = new[]
            {
                new { IDOC_NUMBER = "IDOC0000001", IDOC_TYPE = "MATMAS01", MANDT = "100", DOCNUM = 1, STATUS = "30", DATA = "{}" },
                new { IDOC_NUMBER = "IDOC0000002", IDOC_TYPE = "MATMAS01", MANDT = "100", DOCNUM = 2, STATUS = "30", DATA = "{}" }
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
                Content = new StringContent(responseContent, Encoding.UTF8, "application/json")
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);

        var adapter = new SapAdapter(
            _mockMessageBoxService.Object,
            _mockSubscriptionService.Object,
            "TestInterface",
            Guid.NewGuid(),
            adapterRole: "Source",
            logger: _mockLogger.Object,
            sapApplicationServer: "sap.example.com",
            sapUsername: "testuser",
            sapPassword: "testpass",
            sapODataServiceUrl: "/sap/opu/odata/sap/API_MATERIAL_SRV",
            sapUseOData: true,
            httpClient: httpClient);

        // Act
        var (headers, records) = await adapter.ReadAsync("source");

        // Assert
        Assert.NotEmpty(headers);
        Assert.NotEmpty(records);
        Assert.Contains("IDOC_NUMBER", headers);
        Assert.Equal(2, records.Count);
    }

    [Fact]
    public async Task SapAdapter_ReadAsync_WithRestApi_ShouldReturnRecords()
    {
        // Arrange
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        var responseContent = JsonSerializer.Serialize(new[]
        {
            new { IDOC_NUMBER = "IDOC0000001", IDOC_TYPE = "ORDERS05", MANDT = "100" },
            new { IDOC_NUMBER = "IDOC0000002", IDOC_TYPE = "ORDERS05", MANDT = "100" }
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
                Content = new StringContent(responseContent, Encoding.UTF8, "application/json")
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);

        var adapter = new SapAdapter(
            _mockMessageBoxService.Object,
            _mockSubscriptionService.Object,
            "TestInterface",
            Guid.NewGuid(),
            adapterRole: "Source",
            logger: _mockLogger.Object,
            sapApplicationServer: "sap.example.com",
            sapUsername: "testuser",
            sapPassword: "testpass",
            sapRestApiEndpoint: "/api/v1/orders",
            sapUseRestApi: true,
            httpClient: httpClient);

        // Act
        var (headers, records) = await adapter.ReadAsync("source");

        // Assert
        Assert.NotEmpty(headers);
        Assert.NotEmpty(records);
        Assert.Equal(2, records.Count);
    }

    [Fact]
    public async Task SapAdapter_ReadAsync_WithRfc_ShouldReturnRecords()
    {
        // Arrange
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        var responseContent = JsonSerializer.Serialize(new
        {
            RETURN = new
            {
                IDOC_NUMBER = "IDOC0000001",
                IDOC_TYPE = "MATMAS01",
                MANDT = "100"
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
                Content = new StringContent(responseContent, Encoding.UTF8, "application/json")
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);

        var adapter = new SapAdapter(
            _mockMessageBoxService.Object,
            _mockSubscriptionService.Object,
            "TestInterface",
            Guid.NewGuid(),
            adapterRole: "Source",
            logger: _mockLogger.Object,
            sapApplicationServer: "sap.example.com",
            sapSystemNumber: "00",
            sapUsername: "testuser",
            sapPassword: "testpass",
            sapRfcFunctionModule: "IDOC_INBOUND_ASYNCHRONOUS",
            sapUseRfc: true,
            httpClient: httpClient);

        // Act
        var (headers, records) = await adapter.ReadAsync("source");

        // Assert
        Assert.NotEmpty(headers);
        Assert.NotEmpty(records);
    }

    [Fact]
    public async Task SapAdapter_WriteAsync_WithOData_ShouldSendRecords()
    {
        // Arrange
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        var requestMessages = new List<HttpRequestMessage>();

        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage request, CancellationToken token) =>
            {
                requestMessages.Add(request);
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.Created,
                    Content = new StringContent("{}", Encoding.UTF8, "application/json")
                };
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);

        var adapter = new SapAdapter(
            _mockMessageBoxService.Object,
            _mockSubscriptionService.Object,
            "TestInterface",
            Guid.NewGuid(),
            adapterRole: "Destination",
            logger: _mockLogger.Object,
            sapApplicationServer: "sap.example.com",
            sapUsername: "testuser",
            sapPassword: "testpass",
            sapODataServiceUrl: "/sap/opu/odata/sap/API_MATERIAL_SRV",
            sapUseOData: true,
            httpClient: httpClient);

        var headers = new List<string> { "IDOC_NUMBER", "IDOC_TYPE", "DATA" };
        var records = new List<Dictionary<string, string>>
        {
            new Dictionary<string, string> { { "IDOC_NUMBER", "IDOC0000001" }, { "IDOC_TYPE", "MATMAS01" }, { "DATA", "{}" } },
            new Dictionary<string, string> { { "IDOC_NUMBER", "IDOC0000002" }, { "IDOC_TYPE", "MATMAS01" }, { "DATA", "{}" } }
        };

        // Act
        await adapter.WriteAsync("destination", headers, records);

        // Assert
        Assert.Equal(2, requestMessages.Count);
        Assert.All(requestMessages, msg => Assert.Equal(HttpMethod.Post, msg.Method));
    }

    [Fact]
    public async Task SapAdapter_WriteAsync_WithRestApi_ShouldSendRecords()
    {
        // Arrange
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        var requestMessages = new List<HttpRequestMessage>();

        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage request, CancellationToken token) =>
            {
                requestMessages.Add(request);
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("{}", Encoding.UTF8, "application/json")
                };
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);

        var adapter = new SapAdapter(
            _mockMessageBoxService.Object,
            _mockSubscriptionService.Object,
            "TestInterface",
            Guid.NewGuid(),
            adapterRole: "Destination",
            logger: _mockLogger.Object,
            sapApplicationServer: "sap.example.com",
            sapUsername: "testuser",
            sapPassword: "testpass",
            sapRestApiEndpoint: "/api/v1/orders",
            sapUseRestApi: true,
            httpClient: httpClient);

        var headers = new List<string> { "IDOC_NUMBER", "IDOC_TYPE" };
        var records = new List<Dictionary<string, string>>
        {
            new Dictionary<string, string> { { "IDOC_NUMBER", "IDOC0000001" }, { "IDOC_TYPE", "ORDERS05" } }
        };

        // Act
        await adapter.WriteAsync("destination", headers, records);

        // Assert
        Assert.Single(requestMessages);
        Assert.Equal(HttpMethod.Post, requestMessages[0].Method);
    }

    [Fact]
    public async Task SapAdapter_WriteAsync_WithRfc_ShouldSendRecords()
    {
        // Arrange
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        var requestMessages = new List<HttpRequestMessage>();

        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage request, CancellationToken token) =>
            {
                requestMessages.Add(request);
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("{}", Encoding.UTF8, "application/json")
                };
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);

        var adapter = new SapAdapter(
            _mockMessageBoxService.Object,
            _mockSubscriptionService.Object,
            "TestInterface",
            Guid.NewGuid(),
            adapterRole: "Destination",
            logger: _mockLogger.Object,
            sapApplicationServer: "sap.example.com",
            sapSystemNumber: "00",
            sapUsername: "testuser",
            sapPassword: "testpass",
            sapRfcFunctionModule: "IDOC_OUTBOUND_ASYNCHRONOUS",
            sapReceiverPort: "PORT01",
            sapReceiverPartner: "PARTNER01",
            sapUseRfc: true,
            httpClient: httpClient);

        var headers = new List<string> { "IDOC_NUMBER", "IDOC_TYPE" };
        var records = new List<Dictionary<string, string>>
        {
            new Dictionary<string, string> { { "IDOC_NUMBER", "IDOC0000001" }, { "IDOC_TYPE", "MATMAS01" } }
        };

        // Act
        await adapter.WriteAsync("destination", headers, records);

        // Assert
        Assert.Single(requestMessages);
        Assert.Equal(HttpMethod.Post, requestMessages[0].Method);
    }

    [Fact]
    public async Task SapAdapter_GetSchemaAsync_ShouldReturnSchema()
    {
        // Arrange
        var adapter = new SapAdapter(
            _mockMessageBoxService.Object,
            _mockSubscriptionService.Object,
            "TestInterface",
            Guid.NewGuid(),
            logger: _mockLogger.Object,
            sapIdocType: "MATMAS01");

        // Act
        var schema = await adapter.GetSchemaAsync("source");

        // Assert
        Assert.NotEmpty(schema);
        Assert.Contains("IDOC_NUMBER", schema.Keys);
        Assert.Contains("IDOC_TYPE", schema.Keys);
        Assert.Contains("MANDT", schema.Keys);
    }

    [Fact]
    public async Task SapAdapter_EnsureDestinationStructureAsync_ShouldComplete()
    {
        // Arrange
        var adapter = new SapAdapter(
            _mockMessageBoxService.Object,
            _mockSubscriptionService.Object,
            "TestInterface",
            Guid.NewGuid(),
            adapterRole: "Destination",
            logger: _mockLogger.Object);

        var columnTypes = new Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo>
        {
            { "IDOC_NUMBER", new CsvColumnAnalyzer.ColumnTypeInfo { DataType = CsvColumnAnalyzer.SqlDataType.NVARCHAR, MaxLength = 16 } }
        };

        // Act & Assert (should not throw)
        await adapter.EnsureDestinationStructureAsync("destination", columnTypes);
    }

    [Fact]
    public void SapAdapter_ReadAsync_WithMissingServer_ShouldThrowException()
    {
        // Arrange
        var adapter = new SapAdapter(
            _mockMessageBoxService.Object,
            _mockSubscriptionService.Object,
            "TestInterface",
            Guid.NewGuid(),
            adapterRole: "Source",
            logger: _mockLogger.Object,
            sapUseOData: true,
            sapODataServiceUrl: "/sap/opu/odata/sap/API_MATERIAL_SRV");

        // Act & Assert
        Assert.ThrowsAsync<InvalidOperationException>(() => adapter.ReadAsync("source"));
    }

    [Fact]
    public async Task SapAdapter_ReadAsync_WithIdocType_ShouldReturnSimulatedRecords()
    {
        // Arrange
        var adapter = new SapAdapter(
            _mockMessageBoxService.Object,
            _mockSubscriptionService.Object,
            "TestInterface",
            Guid.NewGuid(),
            adapterRole: "Source",
            logger: _mockLogger.Object,
            sapIdocType: "MATMAS01",
            sapBatchSize: 5);

        // Act
        var (headers, records) = await adapter.ReadAsync("source");

        // Assert
        Assert.NotEmpty(headers);
        Assert.NotEmpty(records);
        Assert.All(records, r => Assert.Equal("MATMAS01", r["IDOC_TYPE"]));
    }
}

