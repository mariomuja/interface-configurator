using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Moq;
using InterfaceConfigurator.Main.Helpers;
using Xunit;

namespace InterfaceConfigurator.Main.Core.Tests.Helpers;

/// <summary>
/// Unit tests for CorsHelper
/// </summary>
public class CorsHelperTests
{
    [Fact]
    public void AddCorsHeaders_ShouldAddAllRequiredHeaders()
    {
        // Arrange
        var mockContext = new Mock<FunctionContext>();
        var mockResponse = new Mock<HttpResponseData>(mockContext.Object);
        var headers = new HttpHeadersCollection();
        mockResponse.SetupProperty(x => x.Headers, headers);

        // Act
        CorsHelper.AddCorsHeaders(mockResponse.Object);

        // Assert
        Assert.True(mockResponse.Object.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.True(mockResponse.Object.Headers.Contains("Access-Control-Allow-Methods"));
        Assert.True(mockResponse.Object.Headers.Contains("Access-Control-Allow-Headers"));
        Assert.True(mockResponse.Object.Headers.Contains("Access-Control-Max-Age"));
        
        Assert.Equal("*", mockResponse.Object.Headers.GetValues("Access-Control-Allow-Origin").First());
        Assert.Equal("GET, POST, PUT, DELETE, OPTIONS, PATCH", 
            mockResponse.Object.Headers.GetValues("Access-Control-Allow-Methods").First());
        Assert.Equal("Content-Type, Authorization, X-Requested-With", 
            mockResponse.Object.Headers.GetValues("Access-Control-Allow-Headers").First());
        Assert.Equal("3600", mockResponse.Object.Headers.GetValues("Access-Control-Max-Age").First());
    }

    [Fact]
    public void AddCorsHeaders_WithExistingHeaders_ShouldReplaceThem()
    {
        // Arrange
        var mockContext = new Mock<FunctionContext>();
        var mockResponse = new Mock<HttpResponseData>(mockContext.Object);
        var headers = new HttpHeadersCollection();
        headers.Add("Access-Control-Allow-Origin", "old-value");
        headers.Add("Access-Control-Allow-Methods", "old-methods");
        mockResponse.SetupProperty(x => x.Headers, headers);

        // Act
        CorsHelper.AddCorsHeaders(mockResponse.Object);

        // Assert
        Assert.Equal("*", mockResponse.Object.Headers.GetValues("Access-Control-Allow-Origin").First());
        Assert.Equal("GET, POST, PUT, DELETE, OPTIONS, PATCH", 
            mockResponse.Object.Headers.GetValues("Access-Control-Allow-Methods").First());
    }
}





