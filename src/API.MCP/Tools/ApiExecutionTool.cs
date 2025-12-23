using System.ComponentModel;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using API.MCP.Services;
using API.MCP.Models;
using System.Text.Json;

namespace API.MCP.Tools;

[McpServerToolType]
public class ApiExecutionTool(IApiService apiService, ILogger<ApiExecutionTool> logger)
{
    private readonly IApiService _apiService = apiService;

    [McpServerTool, Description("Ping the API to check if it's alive and running. Performs a health check to verify the API system is operational.")]
    public async Task<string> PingApi(CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Performing API ping check");
            
            var response = await ((ApiService)_apiService).PingAsync(cancellationToken);

            if (response.Success && response.Data != null)
            {
                return $"? API is alive and running!\n\n" +
                       $"Success: {response.Data.Success}\n" +
                       $"Processed Time: {response.Data.ProcessedTime:yyyy-MM-dd HH:mm:ss.fff zzz}\n" +
                       $"Response Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}";
            }
            else
            {
                return $"? API ping failed: {response.Message ?? "Unknown error"}";
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during API ping");
            return $"? Error pinging API: {ex.Message}";
        }
    }

    [McpServerTool, Description("Retrieve data from a specific entity with optional filtering. The AI should extract the entity name from the query and provide it as a parameter.")]
    public async Task<string> GetEntityData(
        [Description("Natural language query for retrieving entity data. Examples: 'Get all Users', 'Show me Products', 'Give me Orders for customer 123'.")]
        string query,
        [Description("Entity name extracted from the query in SINGULAR form. Examples: 'User' (not 'Users'), 'Product' (not 'Products'), 'Order' (not 'Orders'), 'GlobalParameter'. The AI must convert plural forms to singular before providing this parameter.")]
        string entityName,
        [Description("Optional: Filter conditions in the format 'Field=Value || Field>Value' or 'Field=Value && Field>Value'. Examples: 'Id=1', 'Term>5 || Status=1', 'UserId=123 && Active=1', 'Id>10 && Status=1 || Priority=5'. Supports operators: =, !=, >, <, >=, <= for integer/long values. Multiple conditions can be joined with '&&' (AND logic) or '||' (OR logic). You can combine both for complex conditions.")]
        string? filterConditions = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Received entity data query: {Query} for entity: {EntityName}", query, entityName);

            if (string.IsNullOrWhiteSpace(entityName))
            {
                logger.LogWarning("Empty entity name received");
                return "? Error: Entity name must be provided. Please specify an entity name like 'User', 'Product', 'Order', etc.";
            }

            logger.LogInformation("Using entity name: {EntityName}", entityName);

            // Log filter conditions if provided
            if (!string.IsNullOrWhiteSpace(filterConditions))
            {
                logger.LogInformation("Using filter conditions: {FilterConditions}", filterConditions);
            }
            
            // Create API request for entity data retrieval
            var apiRequest = new ApiRequest
            {
                Action = "Entity",
                Resource = entityName, // AI provides singular form directly
                Method = "POST",
                Body = string.IsNullOrWhiteSpace(filterConditions) ? new { } : new { Where = filterConditions }
            };

            var response = await _apiService.ExecuteRequestAsync(apiRequest, cancellationToken);

            if (response.Success)
            {
                var result = FormatEntityDataResponse(response, entityName);
                logger.LogInformation("Entity data retrieval completed successfully for {EntityName}", entityName);
                return result;
            }
            else
            {
                logger.LogError("Entity data retrieval failed for {EntityName}: {Message}", entityName, response.Message);
                return $"? Error retrieving data from {entityName}: {response.Message}";
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing entity data query");
            return $"? Error: {ex.Message}";
        }
    }

    private string FormatEntityDataResponse(ApiResponse response, string entityName)
    {
        if (response.Data == null)
        {
            return $"? Query completed for {entityName}, but no data was returned.";
        }

        try
        {
            // Format the response data as readable JSON
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var formattedData = JsonSerializer.Serialize(response.Data, jsonOptions);
            
            var result = $"? Successfully retrieved data from {entityName}\n\n";
            
            // Add HTTP Status Code
            if (!string.IsNullOrWhiteSpace(response.ErrorCode))
            {
                result += $"HTTP Status: {response.ErrorCode}\n";
            }
            
            // Add pagination information from page-info header if available
            if (response.Metadata != null && response.Metadata.ContainsKey("page-info"))
            {
                try
                {
                    var pageInfoValue = response.Metadata["page-info"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(pageInfoValue))
                    {
                        var pageInfo = JsonSerializer.Deserialize<JsonElement>(pageInfoValue);
                        result += "Pagination Info:\n";
                        result += $"  Page Size: {pageInfo.GetProperty("PageSize").GetInt32()}\n";
                        result += $"  Current Page: {pageInfo.GetProperty("PageIndex").GetInt32()}\n";
                        result += $"  Total Items: {pageInfo.GetProperty("TotalItems").GetInt32()}\n\n";
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to parse page-info header");
                    result += $"Page Info: {response.Metadata["page-info"]}\n\n";
                }
            }
            
            // Add response headers if available (excluding page-info since we already handled it)
            if (response.Metadata != null && response.Metadata.Count > 0)
            {
                var otherHeaders = response.Metadata.Where(kvp => kvp.Key != "page-info").ToList();
                if (otherHeaders.Count > 0)
                {
                    result += "Response Headers:\n";
                    foreach (var header in otherHeaders)
                    {
                        result += $"  {header.Key}: {header.Value}\n";
                    }
                    result += "\n";
                }
            }

            result += "Entity Data:\n";
            result += formattedData;

            return result;
        }
        catch (Exception ex)
        {
            // Fallback to simple string representation
            logger.LogWarning(ex, "Failed to format entity data response as JSON");
            return $"? Successfully retrieved data from {entityName}\n\nData: {response.Data}";
        }
    }
}