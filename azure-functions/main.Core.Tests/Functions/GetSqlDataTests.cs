using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Moq;
using InterfaceConfigurator.Main;
using Xunit;

namespace InterfaceConfigurator.Main.Core.Tests.Functions;

/// <summary>
/// Unit tests for GetSqlData Azure Function
/// </summary>
public class GetSqlDataTests
{
    private readonly Mock<ILogger<GetSqlDataFunction>> _mockLogger;
    private readonly Mock<FunctionContext> _mockContext;
    private readonly Mock<HttpRequestData> _mockRequest;

    public GetSqlDataTests()
    {
        _mockLogger = new Mock<ILogger<GetSqlDataFunction>>();
        _mockContext = new Mock<FunctionContext>();
        _mockRequest = new Mock<HttpRequestData>(_mockContext.Object);
    }

    [Fact]
    public async Task GetSqlData_OPTIONS_Request_ShouldReturnCorsHeaders()
    {
        // Arrange
        var function = new GetSqlDataFunction(_mockLogger.Object);
        _mockRequest.Setup(x => x.Method).Returns("OPTIONS");
        
        var mockResponse = new Mock<HttpResponseData>(_mockContext.Object);
        var headers = new HttpHeadersCollection();
        mockResponse.SetupProperty(x => x.StatusCode);
        mockResponse.SetupProperty(x => x.Headers, headers);
        
        _mockRequest.Setup(x => x.CreateResponse(It.IsAny<HttpStatusCode>()))
            .Returns(mockResponse.Object);

        // Act
        var result = await function.Run(_mockRequest.Object, _mockContext.Object);

        // Assert
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.True(result.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.Equal("*", result.Headers.GetValues("Access-Control-Allow-Origin").First());
    }

    [Fact]
    public async Task GetSqlData_MissingSqlConfig_ShouldReturnError()
    {
        // Arrange
        var function = new GetSqlDataFunction(_mockLogger.Object);
        _mockRequest.Setup(x => x.Method).Returns("GET");
        _mockRequest.Setup(x => x.Url).Returns(new Uri("https://test.com/api/sql-data"));
        
        // Clear environment variables
        Environment.SetEnvironmentVariable("AZURE_SQL_SERVER", null);
        Environment.SetEnvironmentVariable("AZURE_SQL_USER", null);
        Environment.SetEnvironmentVariable("AZURE_SQL_PASSWORD", null);

        var mockResponse = new Mock<HttpResponseData>(_mockContext.Object);
        var headers = new HttpHeadersCollection();
        mockResponse.SetupProperty(x => x.StatusCode);
        mockResponse.SetupProperty(x => x.Headers, headers);
        mockResponse.Setup(x => x.WriteStringAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        
        _mockRequest.Setup(x => x.CreateResponse(It.IsAny<HttpStatusCode>()))
            .Returns(mockResponse.Object);

        // Act
        var result = await function.Run(_mockRequest.Object, _mockContext.Object);

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, result.StatusCode);
        Assert.True(result.Headers.Contains("Access-Control-Allow-Origin"));
    }
}





