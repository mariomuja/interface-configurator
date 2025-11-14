using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;

namespace ProcessCsvBlobTrigger;

public class SimpleTestFunction
{
    private readonly ILogger<SimpleTestFunction> _logger;

    public SimpleTestFunction(ILogger<SimpleTestFunction> logger)
    {
        _logger = logger;
    }

    [Function("SimpleTestFunction")]
    public HttpResponseData Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
        FunctionContext context)
    {
        _logger.LogInformation("SimpleTestFunction executed successfully!");

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "text/plain; charset=utf-8");
        response.WriteString("SimpleTestFunction is working! ✅");

        return response;
    }
}

