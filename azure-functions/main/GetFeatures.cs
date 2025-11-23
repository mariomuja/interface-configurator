using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Services;
using InterfaceConfigurator.Main.Helpers;

namespace InterfaceConfigurator.Main;

/// <summary>
/// HTTP endpoint to get all features
/// </summary>
public class GetFeaturesFunction
{
    private readonly ILogger<GetFeaturesFunction> _logger;
    private readonly FeatureService _featureService;
    private readonly AuthService _authService;

    public GetFeaturesFunction(
        ILogger<GetFeaturesFunction> logger,
        FeatureService featureService,
        AuthService authService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _featureService = featureService ?? throw new ArgumentNullException(nameof(featureService));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
    }

    [Function("GetFeatures")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "GetFeatures")] HttpRequestData req,
        FunctionContext context)
    {
        try
        {
            // Get user role from token (simplified - in production use proper JWT)
            string? userRole = null;
            if (req.Headers.TryGetValues("Authorization", out var authHeaders))
            {
                var token = authHeaders.FirstOrDefault()?.Replace("Bearer ", "");
                if (!string.IsNullOrEmpty(token))
                {
                    try
                    {
                        var tokenData = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(token));
                        var parts = tokenData.Split(':');
                        if (parts.Length >= 2)
                        {
                            userRole = parts[1];
                        }
                    }
                    catch
                    {
                        // Invalid token, continue as anonymous
                    }
                }
            }

            var features = await _featureService.GetAllFeaturesAsync(userRole);

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(response);

            await response.WriteStringAsync(JsonSerializer.Serialize(features, new JsonSerializerOptions
            {
                WriteIndented = true
            }));

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting features");
            return await ErrorResponseHelper.CreateErrorResponse(
                req, HttpStatusCode.InternalServerError, "Failed to get features", ex, _logger);
        }
    }
}


