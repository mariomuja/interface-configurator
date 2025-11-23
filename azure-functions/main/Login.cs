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
/// HTTP endpoint for user authentication
/// </summary>
public class LoginFunction
{
    private readonly ILogger<LoginFunction> _logger;
    private readonly AuthService _authService;

    public LoginFunction(ILogger<LoginFunction> logger, AuthService authService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
    }

    [Function("Login")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "Login")] HttpRequestData req,
        FunctionContext context)
    {
        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            if (string.IsNullOrWhiteSpace(requestBody))
            {
                return await ErrorResponseHelper.CreateValidationErrorResponse(
                    req, "body", "Request body is required");
            }

            var loginRequest = JsonSerializer.Deserialize<LoginRequest>(requestBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (loginRequest == null || string.IsNullOrWhiteSpace(loginRequest.Username))
            {
                return await ErrorResponseHelper.CreateValidationErrorResponse(
                    req, "body", "Username is required");
            }

            // Demo-User "test" can login without password
            // Admin user must provide password
            UserInfo? user;
            if (loginRequest.Username.Equals("test", StringComparison.OrdinalIgnoreCase) && 
                string.IsNullOrWhiteSpace(loginRequest.Password))
            {
                // Demo-User login without password
                user = await _authService.GetUserAsync(loginRequest.Username);
                if (user == null)
                {
                    var errorResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                    CorsHelper.AddCorsHeaders(errorResponse);
                    await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new LoginResponse
                    {
                        Success = false,
                        ErrorMessage = "Demo user not found"
                    }));
                    return errorResponse;
                }
            }
            else
            {
                // Regular login with password (required for admin and other users)
                if (string.IsNullOrWhiteSpace(loginRequest.Password))
                {
                    return await ErrorResponseHelper.CreateValidationErrorResponse(
                        req, "body", "Password is required for this user");
                }
                user = await _authService.AuthenticateAsync(loginRequest.Username, loginRequest.Password);
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(response);

            if (user == null)
            {
                var errorResponse = new LoginResponse
                {
                    Success = false,
                    ErrorMessage = "Invalid username or password"
                };
                await response.WriteStringAsync(JsonSerializer.Serialize(errorResponse));
                return response;
            }

            // Generate simple token (in production, use JWT)
            var token = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{user.Username}:{user.Role}:{DateTime.UtcNow.Ticks}"));

            var loginResponse = new LoginResponse
            {
                Success = true,
                Token = token,
                User = user
            };

            await response.WriteStringAsync(JsonSerializer.Serialize(loginResponse, new JsonSerializerOptions
            {
                WriteIndented = true
            }));

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login");
            return await ErrorResponseHelper.CreateErrorResponse(
                req, HttpStatusCode.InternalServerError, "Failed to process login", ex, _logger);
        }
    }
}

