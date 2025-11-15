using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;

namespace ProcessCsvBlobTrigger;

public class TestMinimalFunction
{
    private readonly ILogger<TestMinimalFunction> _logger;

    public TestMinimalFunction(ILogger<TestMinimalFunction> logger)
    {
        _logger = logger;
    }

    [Function("TestMinimalFunction")]
    public HttpResponseData Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
        FunctionContext context)
    {
        _logger.LogInformation("TestMinimalFunction executed!");

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "text/plain; charset=utf-8");
        response.WriteString("TestMinimalFunction is working! ✅");

        return response;
    }
}

