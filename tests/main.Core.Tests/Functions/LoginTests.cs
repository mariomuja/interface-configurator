using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Moq;
using InterfaceConfigurator.Main;
using InterfaceConfigurator.Main.Services;
using InterfaceConfigurator.Main.Models;
using Xunit;

namespace InterfaceConfigurator.Main.Core.Tests.Functions;

/// <summary>
/// Unit tests for Login Azure Function
/// </summary>
public class LoginTests
{
    private readonly Mock<ILogger<LoginFunction>> _mockLogger;
    private readonly Mock<AuthService> _mockAuthService;
    private readonly Mock<FunctionContext> _mockContext;
    private readonly Mock<HttpRequestData> _mockRequest;

    public LoginTests()
    {
        _mockLogger = new Mock<ILogger<LoginFunction>>();
        _mockAuthService = new Mock<AuthService>(Mock.Of<ILogger<AuthService>>());
        _mockContext = new Mock<FunctionContext>();
        _mockRequest = new Mock<HttpRequestData>(_mockContext.Object);
    }

    [Fact]
    public async Task Login_OPTIONS_Request_ShouldReturnCorsHeaders()
    {
        // Arrange
        var function = new LoginFunction(_mockLogger.Object, _mockAuthService.Object);
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
    public async Task Login_EmptyBody_ShouldReturnValidationError()
    {
        // Arrange
        var function = new LoginFunction(_mockLogger.Object, _mockAuthService.Object);
        _mockRequest.Setup(x => x.Method).Returns("POST");
        _mockRequest.Setup(x => x.Body).Returns(new MemoryStream());

        var mockResponse = new Mock<HttpResponseData>(_mockContext.Object);
        var headers = new HttpHeadersCollection();
        mockResponse.Setup(x => x.StatusCode).Returns(HttpStatusCode.OK);
        mockResponse.Setup(x => x.Headers).Returns(headers);
        mockResponse.Setup(x => x.WriteStringAsync(It.IsAny<string>(), default(CancellationToken)))
            .Returns(Task.CompletedTask);
        
        _mockRequest.Setup(x => x.CreateResponse(It.IsAny<HttpStatusCode>()))
            .Returns(mockResponse.Object);

        // Act
        var result = await function.Run(_mockRequest.Object, _mockContext.Object);

        // Assert
        Assert.True(result.StatusCode == HttpStatusCode.BadRequest || result.StatusCode == HttpStatusCode.UnprocessableEntity);
        Assert.True(result.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Login_DemoUser_WithoutPassword_ShouldSucceed()
    {
        // Arrange
        var function = new LoginFunction(_mockLogger.Object, _mockAuthService.Object);
        _mockRequest.Setup(x => x.Method).Returns("POST");
        
        var loginRequest = new { username = "test", password = "" };
        var requestBody = JsonSerializer.Serialize(loginRequest);
        _mockRequest.Setup(x => x.Body).Returns(new MemoryStream(Encoding.UTF8.GetBytes(requestBody)));

        var demoUser = new UserInfo { Username = "test", Role = "user", Id = 1 };
        _mockAuthService.Setup(x => x.GetUserAsync("test"))
            .ReturnsAsync(demoUser);

        var mockResponse = new Mock<HttpResponseData>(_mockContext.Object);
        var headers = new HttpHeadersCollection();
        mockResponse.Setup(x => x.StatusCode).Returns(HttpStatusCode.OK);
        mockResponse.Setup(x => x.Headers).Returns(headers);
        mockResponse.Setup(x => x.WriteStringAsync(It.IsAny<string>(), default(CancellationToken)))
            .Returns(Task.CompletedTask);
        
        _mockRequest.Setup(x => x.CreateResponse(It.IsAny<HttpStatusCode>()))
            .Returns(mockResponse.Object);

        // Act
        var result = await function.Run(_mockRequest.Object, _mockContext.Object);

        // Assert
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.True(result.Headers.Contains("Access-Control-Allow-Origin"));
    }
}







