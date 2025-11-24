using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Moq;
using InterfaceConfigurator.Main.Middleware;
using Xunit;

namespace InterfaceConfigurator.Main.Core.Tests.Middleware;

/// <summary>
/// Unit tests for CorsMiddleware
/// </summary>
public class CorsMiddlewareTests
{
    [Fact]
    public async Task Invoke_OPTIONS_Request_ShouldReturnCorsHeadersAndNotCallNext()
    {
        // Arrange
        var middleware = new CorsMiddleware();
        var mockContext = new Mock<FunctionContext>();
        var mockRequest = new Mock<HttpRequestData>(mockContext.Object);
        mockRequest.Setup(x => x.Method).Returns("OPTIONS");

        var mockResponse = new Mock<HttpResponseData>(mockContext.Object);
        var headers = new HttpHeadersCollection();
        mockResponse.SetupProperty(x => x.StatusCode);
        mockResponse.SetupProperty(x => x.Headers, headers);
        mockRequest.Setup(x => x.CreateResponse(It.IsAny<HttpStatusCode>()))
            .Returns(mockResponse.Object);

        var invocationResult = new Mock<InvocationResult>();
        mockContext.Setup(x => x.GetInvocationResult()).Returns(invocationResult.Object);
        mockContext.Setup(x => x.GetHttpRequestDataAsync())
            .ReturnsAsync(mockRequest.Object);

        var nextCalled = false;
        FunctionExecutionDelegate next = (context) =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        // Act
        await middleware.Invoke(mockContext.Object, next);

        // Assert
        Assert.False(nextCalled, "Next middleware should not be called for OPTIONS requests");
        Assert.Equal(HttpStatusCode.NoContent, mockResponse.Object.StatusCode);
        Assert.True(mockResponse.Object.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Invoke_NonOPTIONS_Request_ShouldCallNextAndAddCorsHeaders()
    {
        // Arrange
        var middleware = new CorsMiddleware();
        var mockContext = new Mock<FunctionContext>();
        var mockRequest = new Mock<HttpRequestData>(mockContext.Object);
        mockRequest.Setup(x => x.Method).Returns("GET");

        var mockResponse = new Mock<HttpResponseData>(mockContext.Object);
        var headers = new HttpHeadersCollection();
        mockResponse.SetupProperty(x => x.StatusCode);
        mockResponse.SetupProperty(x => x.Headers, headers);
        mockRequest.Setup(x => x.CreateResponse(It.IsAny<HttpStatusCode>()))
            .Returns(mockResponse.Object);

        var invocationResult = new Mock<InvocationResult>();
        invocationResult.SetupProperty(x => x.Value, mockResponse.Object);
        mockContext.Setup(x => x.GetInvocationResult()).Returns(invocationResult.Object);
        mockContext.Setup(x => x.GetHttpRequestDataAsync())
            .ReturnsAsync(mockRequest.Object);

        var nextCalled = false;
        FunctionExecutionDelegate next = (context) =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        // Act
        await middleware.Invoke(mockContext.Object, next);

        // Assert
        Assert.True(nextCalled, "Next middleware should be called for non-OPTIONS requests");
        Assert.True(mockResponse.Object.Headers.Contains("Access-Control-Allow-Origin"));
    }
}





