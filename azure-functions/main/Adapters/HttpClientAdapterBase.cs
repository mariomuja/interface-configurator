using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Core.Services;
using InterfaceConfigurator.Main.Services;

namespace InterfaceConfigurator.Main.Adapters;

/// <summary>
/// Base class for adapters that use HTTP clients for API communication
/// Provides common token management and HTTP request functionality
/// Reduces code duplication across SAP, Dynamics365, and CRM adapters
/// </summary>
public abstract class HttpClientAdapterBase : AdapterBase
{
    protected readonly HttpClient _httpClient;
    protected readonly bool _disposeHttpClient;
    protected string? _accessToken;
    protected DateTime _tokenExpiry = DateTime.MinValue;

    protected HttpClientAdapterBase(
        IServiceBusService? serviceBusService = null,
        string? interfaceName = null,
        Guid? adapterInstanceGuid = null,
        int batchSize = 1000,
        string adapterRole = "Source",
        ILogger? logger = null,
        HttpClient? httpClient = null,
        JQTransformationService? jqService = null,
        string? jqScriptFile = null)
        : base(serviceBusService, interfaceName, adapterInstanceGuid, batchSize, adapterRole, logger, jqService, jqScriptFile)
    {
        if (httpClient != null)
        {
            _httpClient = httpClient;
            _disposeHttpClient = false;
        }
        else
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(60)
            };
            _disposeHttpClient = true;
        }
    }

    /// <summary>
    /// Gets OAuth 2.0 access token using Client Credentials Flow
    /// Must be implemented by derived classes with their specific authentication logic
    /// </summary>
    protected abstract Task<string> GetAccessTokenInternalAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets access token with caching support
    /// Uses GetAccessTokenInternalAsync for actual token retrieval
    /// </summary>
    protected async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        // Return cached token if still valid (refresh 5 minutes before expiry)
        if (!string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _tokenExpiry.AddMinutes(-5))
        {
            _logger?.LogDebug("Using cached access token (expires at {Expiry})", _tokenExpiry);
            return _accessToken;
        }

        _logger?.LogDebug("Access token expired or missing, requesting new token");
        _accessToken = await GetAccessTokenInternalAsync(cancellationToken);
        return _accessToken;
    }

    /// <summary>
    /// Sets the access token and expiry time (for caching)
    /// </summary>
    protected void SetAccessToken(string token, int expiresInSeconds)
    {
        _accessToken = token;
        _tokenExpiry = DateTime.UtcNow.AddSeconds(expiresInSeconds);
        _logger?.LogDebug("Access token cached (expires at {Expiry})", _tokenExpiry);
    }

    /// <summary>
    /// Sets the access token with a custom expiry time
    /// </summary>
    protected void SetAccessToken(string token, DateTime expiry)
    {
        _accessToken = token;
        _tokenExpiry = expiry;
        _logger?.LogDebug("Access token cached (expires at {Expiry})", _tokenExpiry);
    }

    /// <summary>
    /// Creates an HTTP request message with common headers
    /// </summary>
    protected HttpRequestMessage CreateRequest(HttpMethod method, string url, string? accessToken = null, string? contentType = "application/json")
    {
        var request = new HttpRequestMessage(method, url);
        
        if (!string.IsNullOrEmpty(accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }
        
        if (contentType != null)
        {
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(contentType));
        }
        
        return request;
    }

    /// <summary>
    /// Creates an HTTP request message with Basic Authentication
    /// </summary>
    protected HttpRequestMessage CreateBasicAuthRequest(HttpMethod method, string url, string username, string password)
    {
        var request = new HttpRequestMessage(method, url);
        var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authValue);
        return request;
    }

    /// <summary>
    /// Sends HTTP request and handles common errors
    /// </summary>
    protected async Task<HttpResponseMessage> SendRequestAsync(
        HttpRequestMessage request, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.SendAsync(request, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger?.LogError("HTTP request failed: {StatusCode} {ReasonPhrase}. Response: {ErrorContent}", 
                    response.StatusCode, response.ReasonPhrase, errorContent);
            }
            
            return response;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error sending HTTP request to {RequestUri}", request.RequestUri);
            throw;
        }
    }

    /// <summary>
    /// Disposes the HttpClient if it was created by this adapter
    /// </summary>
    protected virtual void DisposeHttpClient()
    {
        if (_disposeHttpClient)
        {
            _httpClient?.Dispose();
        }
    }
}

