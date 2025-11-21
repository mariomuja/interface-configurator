using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using InterfaceConfigurator.Main.Helpers;

namespace InterfaceConfigurator.Main.Middleware;

/// <summary>
/// Middleware to handle CORS for all HTTP functions
/// </summary>
public class CorsMiddleware : IFunctionsWorkerMiddleware
{
    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        // Check if this is an HTTP trigger function
        var httpRequestData = await context.GetHttpRequestDataAsync();
        
        if (httpRequestData != null)
        {
            // Handle OPTIONS preflight requests immediately
            if (httpRequestData.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
            {
                var response = httpRequestData.CreateResponse(System.Net.HttpStatusCode.NoContent);
                // Set CORS headers FIRST before any other headers
                CorsHelper.AddCorsHeaders(response);
                
                // Set the response in the context and return early
                context.GetInvocationResult().Value = response;
                return;
            }
        }
        
        // Continue to the next middleware/function
        await next(context);
        
        // Add CORS headers to all HTTP responses AFTER function execution
        if (httpRequestData != null)
        {
            var invocationResult = context.GetInvocationResult();
            if (invocationResult?.Value is HttpResponseData httpResponseData)
            {
                // Always add CORS headers - even if already present, ensure they're correct
                // This ensures middleware-level CORS is applied consistently
                CorsHelper.AddCorsHeaders(httpResponseData);
            }
        }
    }
}

