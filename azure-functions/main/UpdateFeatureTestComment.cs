using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Services;
using InterfaceConfigurator.Main.Helpers;

namespace InterfaceConfigurator.Main;

/// <summary>
/// HTTP endpoint to update test comment for a feature (all authenticated users can add comments)
/// </summary>
public class UpdateFeatureTestCommentFunction
{
    private readonly ILogger<UpdateFeatureTestCommentFunction> _logger;
    private readonly FeatureService _featureService;

    public UpdateFeatureTestCommentFunction(
        ILogger<UpdateFeatureTestCommentFunction> logger,
        FeatureService featureService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _featureService = featureService ?? throw new ArgumentNullException(nameof(featureService));
    }

    [Function("UpdateFeatureTestComment")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "UpdateFeatureTestComment")] HttpRequestData req,
        FunctionContext context)
    {
        try
        {
            // Get user from token
            string? username = null;
            if (req.Headers.TryGetValues("Authorization", out var authHeaders))
            {
                var token = authHeaders.FirstOrDefault()?.Replace("Bearer ", "");
                if (!string.IsNullOrEmpty(token))
                {
                    try
                    {
                        var tokenData = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(token));
                        var parts = tokenData.Split(':');
                        if (parts.Length >= 1)
                        {
                            username = parts[0];
                        }
                    }
                    catch
                    {
                        // Invalid token
                    }
                }
            }

            if (string.IsNullOrEmpty(username))
            {
                return await ErrorResponseHelper.CreateValidationErrorResponse(
                    req, "authorization", "User must be authenticated to add test comments");
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
            var testComment = request.ContainsKey("testComment") 
                ? request["testComment"].GetString() 
                : null;

            var success = await _featureService.UpdateTestCommentAsync(featureId, testComment ?? string.Empty, username);

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
            _logger.LogError(ex, "Error updating feature test comment");
            return await ErrorResponseHelper.CreateErrorResponse(
                req, HttpStatusCode.InternalServerError, "Failed to update feature test comment", ex, _logger);
        }
    }
}


