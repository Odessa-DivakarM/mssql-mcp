using System.ComponentModel;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using API.MCP.Services;
using API.MCP.Models;
using System.Text.Json;

namespace API.MCP.Tools;

/// <summary>
/// API execution tool for retrieving entity data with filtering and column selection support.
/// 
/// CRITICAL AI WORKFLOW FOR ERROR RECOVERY:
/// 1. If GetEntityData returns errors about columns, fields, or entity not found:
///    - IMMEDIATELY call GetEntitySchema(entityName) to understand the correct structure
///    - Use schema info to fix column names and data types
///    - Retry GetEntityData with corrected parameters
/// 
/// 2. If user mentions specific column names or has potential typos:
///    - Call GetEntitySchema FIRST to validate column structure  
///    - Form correct filter conditions based on schema
///    - Then call GetEntityData with validated filters
/// 
/// 3. If unsure about entity names:
///    - Call GetAvailableEntities() to see what entities exist
///    - Call GetEntitySchema for the correct entity
///    - Then call GetEntityData
/// 
/// ERROR PATTERNS TO WATCH FOR:
/// - "column not found" ? Call GetEntitySchema to see correct columns
/// - "invalid field" ? Call GetEntitySchema to validate field names  
/// - "entity not found" ? Call GetAvailableEntities, then GetEntitySchema
/// - Filter syntax errors ? Call GetEntitySchema to check data types
/// - Select column errors ? Call GetEntitySchema to validate column names
/// 
/// ADVANCED STRING FILTERING EXAMPLES:
/// User: "Get users whose login starts with Admin"
/// Filter: "LoginName.StartsWith(\"Admin\")"
/// 
/// User: "Find users whose login ends with .Admin"  
/// Filter: "LoginName.EndsWith(\".Admin\")"
/// 
/// User: "Get users with specific logins User01 or User02"
/// Filter: "(\"User01,User02\").Contains(LoginName)"
/// 
/// User: "Get active users whose name is not Security.Admin"
/// Filter: "Status=\"Active\" && LoginName!=\"Security.Admin\""
/// 
/// COLUMN SELECTION EXAMPLES:
/// User: "Get only the names and emails of users"
/// Select: "FirstName,LastName,EmailAddress"
/// 
/// User: "Show me user IDs and login names for active users"
/// Select: "Id,LoginName" + Filter: "IsActive=true"
/// 
/// User: "Get basic user info - ID, name, and status"
/// Select: "Id,FirstName,LastName,Status"
/// 
/// EXAMPLES:
/// User: "Get users where username is John and age > 25"
/// ERROR SCENARIO: GetEntityData fails with "column 'username' not found"
/// RECOVERY: 1) GetEntitySchema("User") ? see actual column is "Username" 
///           2) GetEntityData("Get users...", "User", "Username=\"John\" && Age>25")
/// 
/// User: "Show me products with high priority"  
/// PROACTIVE: 1) GetEntitySchema("Product") ? understand priority field structure
///            2) GetEntityData("Show products...", "Product", "Priority=\"High\"") or "Priority=1"
/// 
/// User: "Find users whose email starts with admin"
/// PROACTIVE: 1) GetEntitySchema("User") ? see email field is "EmailAddress"
///            2) GetEntityData("Find users...", "User", "EmailAddress.StartsWith(\"admin\")")
/// 
/// User: "Get only names of active users"
/// PROACTIVE: 1) GetEntitySchema("User") ? see name fields are "FirstName", "LastName", status field is "IsActive"
///            2) GetEntityData("Get names...", "User", "IsActive=true", "FirstName,LastName")
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

    [McpServerTool, Description("Retrieve data from a specific entity with optional filtering and column selection. CRITICAL WORKFLOW: 1) If this tool returns errors about unknown columns, invalid filters, or entity not found, IMMEDIATELY call GetEntitySchema tool to understand the correct entity structure. 2) If user mentions specific column names in filters or selection, call GetEntitySchema FIRST to validate column names and data types. 3) If unsure about entity names, call GetAvailableEntities first. The AI should extract the entity name from the query and provide it as a parameter.")]
    public async Task<string> GetEntityData(
        [Description("Natural language query for retrieving entity data. Examples: 'Get all Users', 'Show me Products', 'Give me Orders for customer 123', 'Get only names and emails of users'.")]
        string query,
        [Description("Entity name extracted from the query. Can be plural or singular - the tool will automatically convert plural forms to singular. Examples: 'Users' will become 'User', 'Products' will become 'Product'. IMPORTANT: If this tool fails with entity not found error, use GetAvailableEntities to see available entities, then GetEntitySchema to understand the correct entity structure.")]
        string entityName,
        [Description("Optional: Filter conditions in the format 'Field=Value || Field>Value' or 'Field=Value && Field>Value'. \n\nNUMERIC FILTERS: Examples: 'Id=1', 'Age>21', 'Price>=100', 'Count<50'. Operators: =, !=, >, <, >=, <= \n\nSTRING FILTERS: \n• Equals: 'LoginName=\"Security.Admin\"' \n• Not Equals: 'LoginName!=\"Security.Admin\"' \n• StartsWith: 'LoginName.StartsWith(\"Admin\")' \n• EndsWith: 'LoginName.EndsWith(\".Admin\")' \n• Contains (value in list): '(\"User01,User02\").Contains(LoginName)' \n• Contains (field contains substring): Use StartsWith/EndsWith for partial matches \n\nCOMBINING CONDITIONS: Use '&&' (AND) or '||' (OR). Examples: \n• 'Age>21 && LoginName.StartsWith(\"Admin\")' \n• 'Status=\"Active\" || Priority>=3' \n\nERROR RECOVERY: If this tool returns filter-related errors, call GetEntitySchema to see correct column names and data types, then retry with corrected filters.")]
        string? filterConditions = null,
        [Description("Optional: Comma-separated list of column names to return instead of all columns. Examples: 'FirstName,LastName', 'Id,LoginName,IsActive', 'Name,Email,Phone'. Use exact column names from entity schema. IMPORTANT: If this tool returns column-related errors, call GetEntitySchema to see correct column names and spelling, then retry with corrected column names.")]
        string? selectColumns = null,
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
                return "? Error: Entity name must be provided. Please specify an entity name like 'User', 'Product', 'Order', etc.\n\n" +
                       "?? SUGGESTION: Use GetAvailableEntities tool to see what entities are available in the system.";
            }

            logger.LogInformation("Using entity name: {EntityName}", singularEntityName);

            // Log filter conditions if provided
            if (!string.IsNullOrWhiteSpace(filterConditions))
            {
                logger.LogInformation("Using filter conditions: {FilterConditions}", filterConditions);
            }
            
            // Log select columns if provided
            if (!string.IsNullOrWhiteSpace(selectColumns))
            {
                logger.LogInformation("Using select columns: {SelectColumns}", selectColumns);
            }
            
            // Build request body with Where and/or Select parameters
            object requestBody;
            if (string.IsNullOrWhiteSpace(filterConditions) && string.IsNullOrWhiteSpace(selectColumns))
            {
                requestBody = new { };
            }
            else
            {
                var bodyProperties = new Dictionary<string, object>();
                
                if (!string.IsNullOrWhiteSpace(filterConditions))
                {
                    bodyProperties["Where"] = filterConditions;
                }
                
                if (!string.IsNullOrWhiteSpace(selectColumns))
                {
                    bodyProperties["Select"] = selectColumns;
                }
                
                requestBody = bodyProperties;
            }
            
            // Create API request for entity data retrieval
            var apiRequest = new ApiRequest
            {
                Action = "Entity",
                Resource = singularEntityName, // Use singular form for API call
                Method = "POST",
                Body = requestBody
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
                
                // Enhanced error message with guidance to use schema tools
                var errorMessage = $"? Error retrieving data from {singularEntityName}: {response.Message}\n\n";
                
                // Check if it's likely a schema-related issue
                var message = response.Message?.ToLowerInvariant() ?? "";
                if (message.Contains("column") || message.Contains("field") || message.Contains("attribute") || 
                    message.Contains("unknown") || message.Contains("invalid") || message.Contains("not found") ||
                    message.Contains("filter") || message.Contains("where") || message.Contains("syntax") ||
                    message.Contains("select") || message.Contains("property"))
                {
                    errorMessage += "?? RECOMMENDED ACTIONS:\n" +
                                  $"1. Call GetEntitySchema(\"{singularEntityName}\") to see correct column names and data types\n" +
                                  "2. Check if the entity name is correct by calling GetAvailableEntities\n" +
                                  "3. Retry GetEntityData with corrected column names and proper filter/select syntax\n\n" +
                                  "This error suggests there might be issues with column names, data types, filter syntax, or column selection.";
                }
                else if (message.Contains("entity") && message.Contains("not found"))
                {
                    errorMessage += "?? RECOMMENDED ACTIONS:\n" +
                                  "1. Call GetAvailableEntities to see what entities are available\n" +
                                  "2. Check for typos in entity name\n" +
                                  "3. Try GetEntitySchema with the correct entity name";
                }
                else
                {
                    errorMessage += "?? TIP: If this error relates to columns or filtering, try GetEntitySchema to understand the entity structure.";
                }
                
                return errorMessage;
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