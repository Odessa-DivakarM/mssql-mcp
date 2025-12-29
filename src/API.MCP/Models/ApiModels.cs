namespace API.MCP.Models;

/// <summary>
/// Represents a generic API request structure that can be customized based on your organization's API format
/// </summary>
public class ApiRequest
{
    public string? Action { get; set; }
    public string? Resource { get; set; }
    public Dictionary<string, object>? Parameters { get; set; }
    public Dictionary<string, string>? Headers { get; set; }
    public object? Body { get; set; }
    public string? Method { get; set; } = "GET";
}

/// <summary>
/// Represents a generic API response structure
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Message { get; set; }
    public string? ErrorCode { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Base class for API response when data type is not known
/// </summary>
public class ApiResponse : ApiResponse<object>
{
}


/// <summary>
/// Represents the response from a ping endpoint
/// </summary>
public class PingResponse
{
    public bool Success { get; set; }
    public DateTime ProcessedTime { get; set; }
}