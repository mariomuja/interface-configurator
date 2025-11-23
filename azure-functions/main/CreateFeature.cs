using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Services;
using InterfaceConfigurator.Main.Models;
using InterfaceConfigurator.Main.Helpers;

namespace InterfaceConfigurator.Main;

/// <summary>
/// HTTP endpoint to create a new feature (admin only)
/// </summary>
public class CreateFeatureFunction
{
    private readonly ILogger<CreateFeatureFunction> _logger;
    private readonly FeatureService _featureService;
    private readonly AuthService _authService;

    public CreateFeatureFunction(
        ILogger<CreateFeatureFunction> logger,
        FeatureService featureService,
        AuthService authService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _featureService = featureService ?? throw new ArgumentNullException(nameof(featureService));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
    }

    [Function("CreateFeature")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "CreateFeature")] HttpRequestData req,
        FunctionContext context)
    {
        try
        {
            // Check admin role
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
                    error = "Only administrators can create features"
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

            if (request == null || !request.ContainsKey("title") || !request.ContainsKey("description") || 
                !request.ContainsKey("detailedDescription"))
            {
                return await ErrorResponseHelper.CreateValidationErrorResponse(
                    req, "body", "title, description, and detailedDescription are required");
            }

            var feature = await _featureService.CreateFeatureAsync(
                title: request["title"].GetString() ?? "",
                description: request["description"].GetString() ?? "",
                detailedDescription: request["detailedDescription"].GetString() ?? "",
                technicalDetails: request.ContainsKey("technicalDetails") ? request["technicalDetails"].GetString() : null,
                testInstructions: request.ContainsKey("testInstructions") ? request["testInstructions"].GetString() : null,
                knownIssues: request.ContainsKey("knownIssues") ? request["knownIssues"].GetString() : null,
                dependencies: request.ContainsKey("dependencies") ? request["dependencies"].GetString() : null,
                breakingChanges: request.ContainsKey("breakingChanges") ? request["breakingChanges"].GetString() : null,
                screenshots: request.ContainsKey("screenshots") ? request["screenshots"].GetString() : null,
                category: request.ContainsKey("category") ? request["category"].GetString() ?? "General" : "General",
                priority: request.ContainsKey("priority") ? request["priority"].GetString() ?? "Medium" : "Medium",
                implementationDetails: request.ContainsKey("implementationDetails") ? request["implementationDetails"].GetString() : null
            );

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(response);

            await response.WriteStringAsync(JsonSerializer.Serialize(new
            {
                success = true,
                feature = new
                {
                    id = feature.Id,
                    featureNumber = feature.FeatureNumber,
                    title = feature.Title
                }
            }));

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating feature");
            return await ErrorResponseHelper.CreateErrorResponse(
                req, HttpStatusCode.InternalServerError, "Failed to create feature", ex, _logger);
        }
    }
}

