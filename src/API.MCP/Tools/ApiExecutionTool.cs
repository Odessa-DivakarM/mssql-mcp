using System.ComponentModel;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using API.MCP.Services;
using API.MCP.Models;
using System.Text.Json;
using System.Text.RegularExpressions;

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

    [McpServerTool, Description("Retrieve data from a specific entity with optional filtering. The AI should extract the entity name and generate appropriate filter conditions in the specified syntax.")]
    public async Task<string> GetEntityData(
        [Description("Natural language query for retrieving entity data. Examples: 'Get all Users', 'Show me Products', 'Give me Orders for customer 123'. The entity name can be singular or plural.")]
        string query,
        [Description("Optional: Specific entity name if you want to override natural language parsing")]
        string? entityName = null,
        [Description("Optional: Filter conditions in the format 'Field=Value || Field>Value' or 'Field=Value && Field>Value'. Examples: 'Id=1', 'Term>5 || Status=1', 'UserId=123 && Active=1', 'Id>10 && Status=1 || Priority=5'. Supports operators: =, !=, >, <, >=, <= for integer/long values. Multiple conditions can be joined with '&&' (AND logic) or '||' (OR logic). You can combine both for complex conditions.")]
        string? filterConditions = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Received entity data query: {Query}", query);

            if (string.IsNullOrWhiteSpace(query) && string.IsNullOrWhiteSpace(entityName))
            {
                logger.LogWarning("Empty query and entity name received");
                return "? Error: Query or entity name must be provided";
            }

            // Extract entity name from query or use provided entity name
            var extractedEntityName = entityName ?? ExtractEntityNameFromQuery(query);
            
            if (string.IsNullOrWhiteSpace(extractedEntityName))
            {
                return "? Error: Could not identify entity name from the query. Please specify an entity name like 'GlobalParameter', 'Users', etc.";
            }

            logger.LogInformation("Extracted entity name: {EntityName}", extractedEntityName);

            // Log filter conditions if provided
            if (!string.IsNullOrWhiteSpace(filterConditions))
            {
                logger.LogInformation("Using filter conditions: {FilterConditions}", filterConditions);
            }
            
            // Create API request for entity data retrieval
            var apiRequest = new ApiRequest
            {
                Action = "Entity",
                Resource = NormalizeEntityName(extractedEntityName),
                Method = "POST",
                Body = string.IsNullOrWhiteSpace(filterConditions) ? new { } : new { Where = filterConditions }
            };

            var response = await _apiService.ExecuteRequestAsync(apiRequest, cancellationToken);

            if (response.Success)
            {
                var result = FormatEntityDataResponse(response, extractedEntityName);
                logger.LogInformation("Entity data retrieval completed successfully for {EntityName}", extractedEntityName);
                return result;
            }
            else
            {
                logger.LogError("Entity data retrieval failed for {EntityName}: {Message}", extractedEntityName, response.Message);
                return $"? Error retrieving data from {extractedEntityName}: {response.Message}";
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing entity data query");
            return $"? Error: {ex.Message}";
        }
    }

    private string ExtractEntityNameFromQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return string.Empty;

        var queryLower = query.ToLowerInvariant();

        // Common patterns for entity data retrieval - prioritize more specific patterns
        var patterns = new[]
        {
            @"(?:get|show|give|retrieve)\s+(?:all\s+)?(?:data\s+)?(?:from|in|of)\s+([A-Za-z]\w+)",
            @"(?:all|the)\s+([A-Za-z]\w+)(?:\s+(?:data|entities|records))?",
            @"([A-Za-z]\w+)\s+(?:data|entities|records)",
            @"(?:fetch|load)\s+([A-Za-z]\w+)",
            @"list\s+(?:all\s+)?([A-Za-z]\w+)"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(query, pattern, RegexOptions.IgnoreCase);
            if (match.Success && match.Groups.Count > 1)
            {
                var entityName = match.Groups[1].Value;
                // Skip common stop words and ensure minimum length
                if (!IsStopWord(entityName.ToLowerInvariant()) && entityName.Length > 3)
                {
                    return entityName;
                }
            }
        }

        // Fallback: look for capitalized words that might be entity names
        var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var word in words)
        {
            if (char.IsUpper(word[0]) && word.Length > 3 && !IsStopWord(word.ToLowerInvariant()))
            {
                return word;
            }
        }

        return string.Empty;
    }

    private bool IsStopWord(string word)
    {
        var stopWords = new HashSet<string>
        {
            "get", "show", "give", "me", "all", "data", "from", "the", "in", "of", "and", "or", "with", "for", "to", "a", "an", "is", "are", "was", "were", "some", "random", "text", "without", "entity"
        };
        return stopWords.Contains(word.ToLowerInvariant());
    }

    private string NormalizeEntityName(string entityName)
    {
        // Remove plural forms and normalize to singular form for consistency
        // This can be enhanced with more sophisticated pluralization rules
        if (string.IsNullOrWhiteSpace(entityName))
            return entityName;

        var normalized = entityName.Trim();

        // Handle common plural patterns - preserve original case when possible
        if (normalized.EndsWith("ies", StringComparison.OrdinalIgnoreCase) && normalized.Length > 4)
        {
            normalized = normalized.Substring(0, normalized.Length - 3) + "y";
        }
        else if (normalized.EndsWith("es", StringComparison.OrdinalIgnoreCase) && normalized.Length > 3)
        {
            // Only remove 'es' if it's not part of the root word
            if (!normalized.EndsWith("ees", StringComparison.OrdinalIgnoreCase) && 
                !normalized.EndsWith("ses", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring(0, normalized.Length - 2);
            }
        }
        else if (normalized.EndsWith("s", StringComparison.OrdinalIgnoreCase) && normalized.Length > 1)
        {
            // Only remove 's' if it looks like a plural
            var beforeS = normalized.Substring(normalized.Length - 2, 1);
            if (beforeS != "s") // Avoid removing 's' from words ending in 'ss'
            {
                normalized = normalized.Substring(0, normalized.Length - 1);
            }
        }

        // Capitalize first letter
        return char.ToUpperInvariant(normalized[0]) + normalized.Substring(1);
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