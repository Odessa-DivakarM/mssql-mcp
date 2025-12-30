using API.MCP.Configuration;
using API.MCP.Models;
using static API.MCP.Configuration.AuthenticationType;

namespace API.MCP.Services;

public interface IApiService
{
    Task<ApiResponse<T>> ExecuteRequestAsync<T>(ApiRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse> ExecuteRequestAsync(ApiRequest request, CancellationToken cancellationToken = default);
    Task<string> GetApiSchemaAsync(CancellationToken cancellationToken = default);
    Task<bool> ValidateConnectionAsync(CancellationToken cancellationToken = default);
    Task<ApiResponse<PingResponse>> PingAsync(CancellationToken cancellationToken = default);
}

public class ApiService : IApiService
{
    private readonly HttpClient _httpClient;
    private readonly ApiOptions _options;
    private readonly ILogger<ApiService> _logger;

    public ApiService(HttpClient httpClient, IOptions<ApiOptions> options, ILogger<ApiService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        ConfigureHttpClient();
    }

    private void ConfigureHttpClient()
    {
        _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
        
        // Add common headers
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "API-MCP/1.0");
        
        if (!string.IsNullOrWhiteSpace(_options.Version))
        {
            _httpClient.DefaultRequestHeaders.Add("X-API-Version", _options.Version);
        }
        if (!string.IsNullOrWhiteSpace(_options.BusinessUnit))
        {
            _httpClient.DefaultRequestHeaders.Add("BusinessUnit", _options.BusinessUnit);
        }

        // Configure authentication based on auth type
        ConfigureAuthentication();
    }

    private void ConfigureAuthentication()
    {
        _logger.LogInformation("Configuring authentication. Auth type: {AuthType}", _options.AuthType);

        switch (_options.AuthType)
        {
            case AuthenticationType.ApiKey:
                if (!string.IsNullOrWhiteSpace(_options.ApiKey))
                {
                    _httpClient.DefaultRequestHeaders.Add("X-API-Key", _options.ApiKey);
                    _logger.LogInformation("API Key authentication configured");
                }
                else
                {
                    _logger.LogWarning("API Key authentication requested but no API key provided");
                }
                break;

            case AuthenticationType.BasicAuth:
                if (!string.IsNullOrWhiteSpace(_options.Username) && !string.IsNullOrWhiteSpace(_options.Password))
                {
                    var credentials = Convert.ToBase64String(
                        System.Text.Encoding.UTF8.GetBytes($"{_options.Username}:{_options.Password}"));
                    _httpClient.DefaultRequestHeaders.Authorization = 
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
                    
                    _logger.LogInformation("Basic Auth configured for user: {Username}", _options.Username);
                }
                else
                {
                    _logger.LogWarning("Basic Auth requested but username or password is missing");
                }
                break;

            case AuthenticationType.NtlmAuth:
                // NTLM authentication is handled by the HttpClientHandler, not headers
                // This would typically be configured when creating the HttpClient
                _logger.LogInformation("NTLM authentication configured - ensure HttpClientHandler is properly set up");
                break;

            case AuthenticationType.None:
                // No authentication configured
                _logger.LogInformation("No authentication configured for API requests");
                break;
        }
    }

    public async Task<ApiResponse<T>> ExecuteRequestAsync<T>(ApiRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Executing API request: {Action} {Resource}", request.Action, request.Resource);

            var httpRequest = BuildHttpRequest(request, cancellationToken);
            var httpResponse = await _httpClient.SendAsync(httpRequest, cancellationToken);

            var responseContent = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
            
            // Extract response headers and metadata
            var metadata = new Dictionary<string, object>();
            
            // Add HTTP status code
            metadata["http-status-code"] = ((int)httpResponse.StatusCode).ToString();
            metadata["http-status-description"] = httpResponse.StatusCode.ToString();
            
            // Extract specific headers of interest
            foreach (var header in httpResponse.Headers)
            {
                if (header.Key.Equals("page-info", StringComparison.OrdinalIgnoreCase))
                {
                    metadata["page-info"] = string.Join(", ", header.Value);
                }
                else
                {
                    metadata[header.Key.ToLowerInvariant()] = string.Join(", ", header.Value);
                }
            }
            
            // Also check content headers
            foreach (var header in httpResponse.Content.Headers)
            {
                metadata[header.Key.ToLowerInvariant()] = string.Join(", ", header.Value);
            }
            
            if (httpResponse.IsSuccessStatusCode)
            {
                var data = JsonSerializer.Deserialize<T>(responseContent);
                return new ApiResponse<T>
                {
                    Success = true,
                    Data = data,
                    Message = "Request completed successfully",
                    ErrorCode = ((int)httpResponse.StatusCode).ToString(),
                    Metadata = metadata
                };
            }
            else
            {
                _logger.LogError("API request failed with status {StatusCode}: {Content}", 
                    httpResponse.StatusCode, responseContent);
                
                return new ApiResponse<T>
                {
                    Success = false,
                    Message = $"API request failed with status {httpResponse.StatusCode}: {responseContent}",
                    ErrorCode = ((int)httpResponse.StatusCode).ToString(),
                    Metadata = metadata
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while executing API request");
            return new ApiResponse<T>
            {
                Success = false,
                Message = $"Error executing API request: {ex.Message}",
                ErrorCode = "EXECUTION_ERROR"
            };
        }
    }

    public async Task<ApiResponse> ExecuteRequestAsync(ApiRequest request, CancellationToken cancellationToken = default)
    {
        var response = await ExecuteRequestAsync<object>(request, cancellationToken);
        return new ApiResponse
        {
            Success = response.Success,
            Data = response.Data,
            Message = response.Message,
            ErrorCode = response.ErrorCode,
            Metadata = response.Metadata
        };
    }

    public async Task<string> GetApiSchemaAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // This is a placeholder - customize based on your API's schema endpoint
            var request = new ApiRequest
            {
                Action = "describe",
                Resource = "schema",
                Method = "GET"
            };

            var response = await ExecuteRequestAsync<object>(request, cancellationToken);
            
            if (response.Success && response.Data != null)
            {
                return JsonSerializer.Serialize(response.Data, new JsonSerializerOptions { WriteIndented = true });
            }
            
            return "Schema information not available";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving API schema");
            return $"Error retrieving schema: {ex.Message}";
        }
    }

    public async Task<bool> ValidateConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Use the ping endpoint to validate connection
            var request = new ApiRequest
            {
                Action = "ping",
                Resource = "",
                Method = "GET"
            };

            var response = await ExecuteRequestAsync(request, cancellationToken);
            return response.Success;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Performs a ping check against the API to verify it's alive and running
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Ping response with success status and processed time</returns>
    public async Task<ApiResponse<PingResponse>> PingAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Performing API ping check");

            var request = new ApiRequest
            {
                Action = "ping",
                Resource = "",
                Method = "GET"
            };

            var response = await ExecuteRequestAsync<PingResponse>(request, cancellationToken);
            
            if (response.Success)
            {
                _logger.LogInformation("API ping successful. Processed time: {ProcessedTime}", 
                    response.Data?.ProcessedTime);
            }
            else
            {
                _logger.LogWarning("API ping failed: {Message}", response.Message);
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred during API ping");
            return new ApiResponse<PingResponse>
            {
                Success = false,
                Message = $"Error during API ping: {ex.Message}",
                ErrorCode = "PING_ERROR"
            };
        }
    }

    private HttpRequestMessage BuildHttpRequest(ApiRequest apiRequest, CancellationToken cancellationToken)
    {
        // Build the endpoint URL based on your organization's API URL structure
        var endpoint = BuildEndpoint(apiRequest);
        var httpMethod = GetHttpMethod(apiRequest.Method ?? "GET");
        
        var httpRequest = new HttpRequestMessage(httpMethod, endpoint);

        // Add custom headers if provided
        if (apiRequest.Headers != null)
        {
            foreach (var header in apiRequest.Headers)
            {
                httpRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        // Add query parameters if GET request
        if (httpMethod == HttpMethod.Get && apiRequest.Parameters != null)
        {
            var queryParams = string.Join("&", 
                apiRequest.Parameters.Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value?.ToString() ?? "")}"));
            
            if (!string.IsNullOrEmpty(queryParams))
            {
                httpRequest.RequestUri = new Uri($"{httpRequest.RequestUri}?{queryParams}");
            }
        }

        // Add body for POST/PUT requests
        if (apiRequest.Body != null && (httpMethod == HttpMethod.Post || httpMethod == HttpMethod.Put || httpMethod == HttpMethod.Patch))
        {
            var jsonContent = JsonSerializer.Serialize(apiRequest.Body);
            httpRequest.Content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
        }

        return httpRequest;
    }

    private string BuildEndpoint(ApiRequest request)
    {
        // Handle special endpoints first
        if (IsPingRequest(request))
        {
            return "api/ping";
        }

        // Handle entity endpoints: /api/Entity/{EntityName}
        if (IsEntityRequest(request))
        {
            return $"api/Entity/{request.Resource}";
        }

        // Customize this method based on your organization's API URL structure
        // Example: /api/v1/{action}/{resource}
        var parts = new List<string> { "api" };
        
        if (!string.IsNullOrWhiteSpace(_options.Version))
        {
            parts.Add(_options.Version);
        }
        
        if (!string.IsNullOrWhiteSpace(request.Action))
        {
            parts.Add(request.Action);
        }
        
        if (!string.IsNullOrWhiteSpace(request.Resource))
        {
            parts.Add(request.Resource);
        }

        return string.Join("/", parts);
    }

    private bool IsPingRequest(ApiRequest request)
    {
        return (string.Equals(request.Action, "ping", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(request.Resource, "ping", StringComparison.OrdinalIgnoreCase)) ||
               (string.Equals(request.Action, "health", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(request.Resource, "ping", StringComparison.OrdinalIgnoreCase));
    }

    private bool IsEntityRequest(ApiRequest request)
    {
        return string.Equals(request.Action, "Entity", StringComparison.OrdinalIgnoreCase);
    }

    private static HttpMethod GetHttpMethod(string method)
    {
        return method.ToUpperInvariant() switch
        {
            "GET" => HttpMethod.Get,
            "POST" => HttpMethod.Post,
            "PUT" => HttpMethod.Put,
            "DELETE" => HttpMethod.Delete,
            "PATCH" => HttpMethod.Patch,
            "HEAD" => HttpMethod.Head,
            "OPTIONS" => HttpMethod.Options,
            _ => HttpMethod.Get
        };
    }
}