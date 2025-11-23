using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Services;
using InterfaceConfigurator.Main.Helpers;

namespace InterfaceConfigurator.Main;

/// <summary>
/// HTTP endpoint to toggle feature enabled state (admin only)
/// </summary>
public class ToggleFeatureFunction
{
    private readonly ILogger<ToggleFeatureFunction> _logger;
    private readonly FeatureService _featureService;
    private readonly AuthService _authService;

    public ToggleFeatureFunction(
        ILogger<ToggleFeatureFunction> logger,
        FeatureService featureService,
        AuthService authService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _featureService = featureService ?? throw new ArgumentNullException(nameof(featureService));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
    }

    [Function("ToggleFeature")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "ToggleFeature")] HttpRequestData req,
        FunctionContext context)
    {
        try
        {
            // Get user from token
            string? username = null;
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
                            username = parts[0];
                            userRole = parts[1];
                        }
                    }
                    catch
                    {
                        // Invalid token
                    }
                }
            }

            if (userRole != "admin")
            {
                var forbiddenResponse = req.CreateResponse(HttpStatusCode.Forbidden);
                CorsHelper.AddCorsHeaders(forbiddenResponse);
                await forbiddenResponse.WriteStringAsync(JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Only administrators can toggle features"
                }));
                return forbiddenResponse;
            }

            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            if (string.IsNullOrWhiteSpace(requestBody))
            {
                return await ErrorResponseHelper.CreateValidationErrorResponse(
                    req, "body", "Request body is required");
            }

            var request = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(requestBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (request == null || !request.ContainsKey("featureId"))
            {
                return await ErrorResponseHelper.CreateValidationErrorResponse(
                    req, "body", "featureId is required");
            }

            var featureId = request["featureId"].GetInt32();
            if (string.IsNullOrEmpty(username))
            {
                return await ErrorResponseHelper.CreateValidationErrorResponse(
                    req, "authorization", "Username not found in token");
            }

            var success = await _featureService.ToggleFeatureAsync(featureId, username);

            var response = req.CreateResponse(success ? HttpStatusCode.OK : HttpStatusCode.NotFound);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(response);

            await response.WriteStringAsync(JsonSerializer.Serialize(new
            {
                success = success
            }));

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling feature");
            return await ErrorResponseHelper.CreateErrorResponse(
                req, HttpStatusCode.InternalServerError, "Failed to toggle feature", ex, _logger);
        }
    }
}

