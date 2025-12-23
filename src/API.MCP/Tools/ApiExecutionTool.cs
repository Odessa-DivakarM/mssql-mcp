using System.ComponentModel;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using API.MCP.Services;
using API.MCP.Models;
using System.Text.Json;

namespace API.MCP.Tools;

/// <summary>
/// API execution tool for retrieving entity data with filtering support.
/// 
/// RECOMMENDED AI WORKFLOW:
/// 1. If user mentions specific column names or has potential typos:
///    - First call GetEntitySchema(entityName) to understand column structure
///    - Use the schema information to validate column names and data types
///    - Form correct filter conditions based on schema
/// 2. If unsure about entity names:
///    - First call GetAvailableEntities() to see what entities exist
/// 3. Then call GetEntityData() with validated column names and proper filter syntax
/// 
/// EXAMPLES:
/// User: "Get users where username is John and age > 25"
/// 1. GetEntitySchema("User") - to check if columns are "username"/"Username" and "age"/"Age"
/// 2. GetEntityData("Get users...", "User", "Username=\"John\" && Age>25")
/// 
/// User: "Show me products with high priority"
/// 1. GetEntitySchema("Product") - to understand what "priority" field looks like
/// 2. GetEntityData("Show me products...", "Product", "Priority=\"High\"") or "Priority=1" based on schema
/// </summary>

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

    [McpServerTool, Description("Retrieve data from a specific entity with optional filtering. IMPORTANT: If the user's query mentions specific column names in filters or if there might be typos in column names, first call GetEntitySchema tool to understand the entity structure and validate column names. The AI should extract the entity name from the query and provide it as a parameter.")]
    public async Task<string> GetEntityData(
        [Description("Natural language query for retrieving entity data. Examples: 'Get all Users', 'Show me Products', 'Give me Orders for customer 123'.")]
        string query,
        [Description("Entity name extracted from the query. Can be plural or singular - the tool will automatically convert plural forms to singular. Examples: 'Users' will become 'User', 'Products' will become 'Product', 'EntityResources' will become 'EntityResource'. If unsure about the exact entity name, use GetAvailableEntities tool first to see what entities are available.")]
        string entityName,
        [Description("Optional: Filter conditions in the format 'Field=Value || Field>Value' or 'Field=Value && Field>Value'. Examples: 'Id=1', 'Name=\"John\"', 'Status=\"Active\" && Age>21', 'Term>5 || Status=1'. For STRING values, wrap in double quotes with escaping: 'Name=\"value\"'. For NUMERIC values, use without quotes: 'Id=123'. Supports operators: =, !=, >, <, >=, <= for numeric values and = for string values. Multiple conditions can be joined with '&&' (AND logic) or '||' (OR logic). IMPORTANT: Ensure column names are correct by checking GetEntitySchema first if unsure.")]
        string? filterConditions = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Convert plural to singular automatically
            var singularEntityName = ConvertPluralToSingular(entityName);
            
            logger.LogInformation("Received entity data query: {Query} for entity: {OriginalName} -> {SingularName}", 
                query, entityName, singularEntityName);

            if (string.IsNullOrWhiteSpace(singularEntityName))
            {
                logger.LogWarning("Empty entity name received");
                return "? Error: Entity name must be provided. Please specify an entity name like 'User', 'Product', 'Order', etc.";
            }

            logger.LogInformation("Using entity name: {EntityName}", singularEntityName);

            // Log filter conditions if provided
            if (!string.IsNullOrWhiteSpace(filterConditions))
            {
                logger.LogInformation("Using filter conditions: {FilterConditions}", filterConditions);
            }
            
            // Create API request for entity data retrieval
            var apiRequest = new ApiRequest
            {
                Action = "Entity",
                Resource = singularEntityName, // Use singular form for API call
                Method = "POST",
                Body = string.IsNullOrWhiteSpace(filterConditions) ? new { } : new { Where = filterConditions }
            };

            var response = await _apiService.ExecuteRequestAsync(apiRequest, cancellationToken);

            if (response.Success)
            {
                var result = FormatEntityDataResponse(response, singularEntityName, entityName);
                logger.LogInformation("Entity data retrieval completed successfully for {EntityName}", singularEntityName);
                return result;
            }
            else
            {
                logger.LogError("Entity data retrieval failed for {EntityName}: {Message}", singularEntityName, response.Message);
                return $"? Error retrieving data from {singularEntityName}: {response.Message}";
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing entity data query");
            return $"? Error: {ex.Message}";
        }
    }

    private string FormatEntityDataResponse(ApiResponse response, string entityName, string originalEntityName = "")
    {
        if (response.Data == null)
        {
            var message = $"? Query completed for {entityName}, but no data was returned.";
            if (!string.IsNullOrEmpty(originalEntityName) && originalEntityName != entityName)
            {
                message = $"?? Converted plural '{originalEntityName}' to singular '{entityName}'\n\n" + message;
            }
            return message;
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
            
            var result = $"? Successfully retrieved data from {entityName}\n";
            
            // Add conversion notice if entity name was converted
            if (!string.IsNullOrEmpty(originalEntityName) && originalEntityName != entityName)
            {
                result = $"?? Converted plural '{originalEntityName}' to singular '{entityName}'\n\n" + result;
            }
            
            result += "\n";
            
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
            var fallback = $"? Successfully retrieved data from {entityName}\n\nData: {response.Data}";
            if (!string.IsNullOrEmpty(originalEntityName) && originalEntityName != entityName)
            {
                fallback = $"?? Converted plural '{originalEntityName}' to singular '{entityName}'\n\n" + fallback;
            }
            return fallback;
        }
    }

    /// <summary>
    /// Converts plural entity names to singular form using common English pluralization rules
    /// </summary>
    /// <param name="entityName">The potentially plural entity name</param>
    /// <returns>The singular form of the entity name</returns>
    private string ConvertPluralToSingular(string entityName)
    {
        if (string.IsNullOrWhiteSpace(entityName))
            return entityName;

        // Don't process if it's already likely singular (less than 3 characters or doesn't end with 's')
        if (entityName.Length < 3 || !entityName.EndsWith("s", StringComparison.OrdinalIgnoreCase))
            return entityName;

        var lowerName = entityName.ToLowerInvariant();

        // Special irregular cases first
        var irregularPlurals = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "children", "child" },
            { "people", "person" },
            { "men", "man" },
            { "women", "woman" },
            { "feet", "foot" },
            { "teeth", "tooth" },
            { "geese", "goose" },
            { "mice", "mouse" }
        };

        if (irregularPlurals.TryGetValue(lowerName, out var irregularSingular))
        {
            // Preserve the original casing pattern
            return PreserveCasing(entityName, irregularSingular);
        }

        // Handle common plural endings
        if (lowerName.EndsWith("ies"))
        {
            // entities -> entity, categories -> category
            return entityName.Substring(0, entityName.Length - 3) + "y";
        }
        else if (lowerName.EndsWith("ves"))
        {
            // lives -> life, knives -> knife
            return entityName.Substring(0, entityName.Length - 3) + "fe";
        }
        else if (lowerName.EndsWith("ses") || lowerName.EndsWith("ches") || lowerName.EndsWith("shes") || lowerName.EndsWith("xes"))
        {
            // classes -> class, churches -> church, dishes -> dish, boxes -> box
            return entityName.Substring(0, entityName.Length - 2);
        }
        else if (lowerName.EndsWith("s") && !lowerName.EndsWith("ss") && !lowerName.EndsWith("us"))
        {
            // Most common case: remove trailing 's'
            // users -> user, products -> product, parameters -> parameter
            // But not: class -> clas, status -> statu
            return entityName.Substring(0, entityName.Length - 1);
        }

        // If no rules match, return as-is
        return entityName;
    }

    /// <summary>
    /// Preserves the casing pattern of the original word when applying singular conversion
    /// </summary>
    /// <param name="original">The original word with its casing</param>
    /// <param name="converted">The converted word in lowercase</param>
    /// <returns>The converted word with preserved casing pattern</returns>
    private string PreserveCasing(string original, string converted)
    {
        if (string.IsNullOrEmpty(original) || string.IsNullOrEmpty(converted))
            return converted;

        var result = new char[converted.Length];
        
        for (int i = 0; i < converted.Length && i < original.Length; i++)
        {
            result[i] = char.IsUpper(original[i]) ? char.ToUpper(converted[i]) : converted[i];
        }
        
        // Handle remaining characters if converted is longer
        for (int i = original.Length; i < converted.Length; i++)
        {
            result[i] = converted[i];
        }

        return new string(result);
    }
}