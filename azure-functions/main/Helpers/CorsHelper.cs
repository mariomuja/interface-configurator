using Microsoft.Azure.Functions.Worker.Http;

namespace InterfaceConfigurator.Main.Helpers;

/// <summary>
/// Helper class for adding CORS headers to HTTP responses
/// </summary>
public static class CorsHelper
{
    /// <summary>
    /// Adds CORS headers to the HTTP response
    /// Ensures headers are set even if they already exist
    /// </summary>
    public static void AddCorsHeaders(HttpResponseData response)
    {
        // Remove existing CORS headers if present to avoid duplicates
        if (response.Headers.Contains("Access-Control-Allow-Origin"))
        {
            response.Headers.Remove("Access-Control-Allow-Origin");
        }
        if (response.Headers.Contains("Access-Control-Allow-Methods"))
        {
            response.Headers.Remove("Access-Control-Allow-Methods");
        }
        if (response.Headers.Contains("Access-Control-Allow-Headers"))
        {
            response.Headers.Remove("Access-Control-Allow-Headers");
        }
        if (response.Headers.Contains("Access-Control-Max-Age"))
        {
            response.Headers.Remove("Access-Control-Max-Age");
        }
        
        // Add CORS headers - set them FIRST before Content-Type
        // Allow all origins for now (can be restricted to specific domains in production)
        response.Headers.Add("Access-Control-Allow-Origin", "*");
        response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS, PATCH");
        response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization, X-Requested-With");
        response.Headers.Add("Access-Control-Max-Age", "3600");
    }
}

