using Microsoft.Azure.Functions.Worker.Http;

namespace InterfaceConfigurator.Main.Helpers;

/// <summary>
/// Helper class for adding CORS headers to HTTP responses
/// </summary>
public static class CorsHelper
{
    /// <summary>
    /// Adds CORS headers to the HTTP response
    /// </summary>
    public static void AddCorsHeaders(HttpResponseData response)
    {
        // Allow all origins for now (can be restricted to specific domains in production)
        response.Headers.Add("Access-Control-Allow-Origin", "*");
        response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS, PATCH");
        response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization, X-Requested-With");
        response.Headers.Add("Access-Control-Max-Age", "3600");
    }
}

