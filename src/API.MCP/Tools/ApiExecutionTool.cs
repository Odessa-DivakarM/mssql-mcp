using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using API.MCP.Services;
using API.MCP.Models;
using API.MCP.Configuration;
using System.Text.Json;

namespace API.MCP.Tools;

/// <summary>
/// API execution tool for retrieving entity data with filtering, column selection, sorting, and pagination support.
/// 
/// TRANSIENT ENTITY VALIDATION:
/// The tool automatically validates that entities are persistent (Persistent="True") before attempting data retrieval.
/// Entities marked with Persistent="False" in EntityTypes.xaml are transient and will be rejected with an appropriate error message.
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
/// 4. If GetEntityData returns "Transient entity" error:
///    - The entity is marked as Persistent="False" in EntityTypes.xaml
///    - Use GetAvailableEntities to find persistent entities
///    - Use GetEntitySchema to verify entity persistence status
/// 
 /// ERROR PATTERNS TO WATCH FOR:
/// - "column not found" → Call GetEntitySchema to see correct columns
/// - "invalid field" → Call GetEntitySchema to validate field names  
/// - "entity not found" → Call GetAvailableEntities, then GetEntitySchema
/// - "Transient entity" → Entity is Persistent="False", use GetAvailableEntities to find valid entities
/// - Filter syntax errors → Call GetEntitySchema to check data types
/// - Select column errors → Call GetEntitySchema to validate column names
/// - OrderBy column errors → Call GetEntitySchema to validate column names for sorting
/// 
/// SORTING SUPPORT:
/// Control the sort order of results using the OrderBy parameter. Sorting is especially important for paginated results.
/// 
/// SORTING EXAMPLES:
/// User: "Get users ordered by last name"
/// Parameters: orderBy="LastName asc"
/// 
/// User: "Show me the newest users first"
/// Parameters: orderBy="CreatedDate desc"
/// 
/// User: "Get users sorted by ID descending, then by name ascending"
/// Parameters: orderBy="Id desc, FirstName asc"
/// 
/// User: "Show me products ordered by price high to low"
/// Parameters: orderBy="Price desc"
/// 
/// SORTING WORKFLOW:
/// 1. Use GetEntitySchema FIRST to validate column names for OrderBy
/// 2. Format as "ColumnName asc" or "ColumnName desc"
/// 3. For multiple columns: "Column1 asc, Column2 desc, Column3 asc"
/// 4. Essential for consistent pagination results across pages
/// 
/// PAGINATION SUPPORT:
/// The API returns data in pages (default: 100 records per page). Use pagination parameters to control data retrieval:
/// 
/// PAGINATION EXAMPLES:
/// User: "Get first 50 users"
/// Parameters: pageSize=50, pageIndex=1
/// 
/// User: "Get users on page 2"
/// Parameters: pageIndex=2 (uses default pageSize=100)
/// 
/// User: "Get ALL users regardless of pagination"
/// Parameters: fetchAllPages=true (automatically retrieves all pages)
/// 
/// User: "Show me all products with status Active"
/// Parameters: filterConditions="Status=\"Active\"", fetchAllPages=true
/// 
/// PAGINATION WORKFLOW:
/// 1. If user wants ALL data ? set fetchAllPages=true
/// 2. If user wants specific page/size ? set pageSize and pageIndex
/// 3. If pagination info shows more data available ? guide user to use pagination or fetchAllPages
/// 4. Default behavior: returns first 100 records with pagination info in response
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
/// ENUM FILTERING EXAMPLES (for fields ending with 'Values' suffix):
/// IMPORTANT: Use GetEntitySchema first to identify enum fields, then format filters correctly:
/// 
/// User: "Get users with Admin permission"
/// WORKFLOW: 1) GetEntitySchema("User") ? See "DefaultPermissionValues" is enum type
///           2) GetEntityData("Get users...", "User", "DefaultPermissionValues.Value=\"Admin\"")
/// 
/// User: "Find users whose permission is not Guest"
/// Filter: "DefaultPermissionValues.Value!=\"Guest\"" (Use .Value for enum fields)
/// 
/// User: "Get users whose permission starts with Admin"
/// Filter: "DefaultPermissionValues.Value.StartsWith(\"Admin\")" (Use .Value for enum fields)
/// 
/// User: "Find users with specific permissions Admin or User"
/// Filter: "(\"Admin,User\").Contains(DefaultPermissionValues.Value)" (Use .Value for enum fields)
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
/// HIERARCHICAL SELECTION EXAMPLES (Parent-Child Entities):
/// User: "Get users with their email addresses"
/// Select: "FirstName,LastName,UserEmailAddresses.{Email,IsPrimary}"
/// (Note: Child entity "UserEmailAddress" becomes plural "UserEmailAddresses" in selection)
/// 
/// User: "Get assets with their locations"
/// Select: "Id,Status,AssetLocations.{LocationId,AssignedDate}"
/// (Child entity "AssetLocation" becomes plural "AssetLocations")
/// 
/// User: "Get portfolios with their parameters"
/// Select: "Id,Name,PortfolioParameters.{ParameterName,ParameterValue}"
/// (Child entity "PortfolioParameter" becomes plural "PortfolioParameters")
/// 
/// HIERARCHICAL SELECTION RULES:
/// 1. Query the PARENT entity (e.g., User, Asset, Portfolio)
/// 2. Use PLURAL form of child entity name in selection (e.g., UserEmailAddresses, AssetLocations)
/// 3. Use dot notation with curly braces: "ChildEntities.{attr1,attr2,attr3}"
/// 4. Child attributes must exist in the child entity schema
/// 
/// RELATIONSHIP TYPES:
/// • OneToMany: Parent can have multiple children (e.g., User → UserEmailAddresses)
/// • OneToOneOptional: Parent may have 0 or 1 child (e.g., User → UserProfile)
/// • OneToOneMandatory: Parent must have exactly 1 child (e.g., User → UserSecurity)
/// 
/// COMBINED EXAMPLES (Filtering + Sorting + Selection + Pagination):
/// User: "Get active users ordered by creation date, show only names and emails, first 20 records"
/// Parameters: 
/// - filterConditions="IsActive=true"
/// - orderBy="CreatedDate desc"
/// - selectColumns="FirstName,LastName,EmailAddress"
/// - pageSize=20, pageIndex=1
/// 
/// User: "Show me all users with Admin role, sorted by last name, get all pages"
/// Parameters:
/// - filterConditions="DefaultPermissionValues.Value=\"Admin\""
/// - orderBy="LastName asc, FirstName asc"
/// - fetchAllPages=true
/// 
/// HIERARCHICAL COMBINED EXAMPLES:
/// User: "Get active users with their email addresses, ordered by name, first 10 records"
/// Parameters:
/// - filterConditions="IsActive=true"
/// - selectColumns="FirstName,LastName,UserEmailAddresses.{Email,IsPrimary}"
/// - orderBy="LastName asc, FirstName asc"
/// - pageSize=10, pageIndex=1
/// 
/// User: "Get scrap assets with their locations and quantities"
/// Parameters:
/// - filterConditions="Status.Value=\"Scrap\" && Quantity=1"
/// - selectColumns="Status,Id,Quantity,AssetLocations.{LocationId}"
/// - fetchAllPages=true
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
/// 
/// User: "Get all users (there might be thousands)"
/// SOLUTION: GetEntityData("Get all users", "User", fetchAllPages: true) ? retrieves all pages automatically
/// 
/// User: "Get users ordered by newest first"
/// PROACTIVE: 1) GetEntitySchema("User") ? see date field is "CreatedDate"
///            2) GetEntityData("Get newest users", "User", orderBy: "CreatedDate desc")
/// </summary>

[McpServerToolType]
public class ApiExecutionTool(IApiService apiService, IEntitySchemaService entitySchemaService, IOptions<SchemaOptions> schemaOptions, ILogger<ApiExecutionTool> logger)
{
    private readonly IApiService _apiService = apiService;
    private readonly IEntitySchemaService _entitySchemaService = entitySchemaService;
    private readonly SchemaOptions _schemaOptions = schemaOptions.Value;

    [McpServerTool, Description("Ping the API to check if it's alive and running. Performs a health check to verify the API system is operational.")]
    public async Task<string> PingApi(CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Performing API ping check");
            
            var response = await ((ApiService)_apiService).PingAsync(cancellationToken);

            if (response.Success && response.Data != null)
            {
                return $"API is alive and running!\n\n" +
                       $"Success: {response.Data.Success}\n" +
                       $"Processed Time: {response.Data.ProcessedTime:yyyy-MM-dd HH:mm:ss.fff zzz}\n" +
                       $"Response Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}";
            }
            else
            {
                return $"API ping failed: {response.Message ?? "Unknown error"}";
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during API ping");
            return $"Error pinging API: {ex.Message}";
        }
    }

    [McpServerTool, Description("Retrieve data from a specific entity with optional filtering and column selection. CRITICAL WORKFLOW: 1) If this tool returns errors about unknown columns, invalid filters, or entity not found, IMMEDIATELY call GetEntitySchema tool to understand the correct entity structure. 2) If user mentions specific column names in filters or selection, call GetEntitySchema FIRST to validate column names and data types. 3) If unsure about entity names, call GetAvailableEntities first. The AI should extract the entity name from the query and provide it as a parameter.")]
    public async Task<string> GetEntityData(
        [Description("Natural language query for retrieving entity data. Examples: 'Get all Users', 'Show me Products', 'Give me Orders for customer 123', 'Get only names and emails of users'.")]
        string query,
        [Description("Entity name extracted from the query. Can be plural or singular - the tool will automatically convert plural forms to singular. Examples: 'Users' will become 'User', 'Products' will become 'Product'. IMPORTANT: If this tool fails with entity not found error, use GetAvailableEntities to see available entities, then GetEntitySchema to understand the correct entity structure.")]
        string entityName,
        [Description("Optional: Filter conditions in the format 'Field=Value || Field>Value' or 'Field=Value && Field>Value'. \n\nNUMERIC FILTERS: Examples: 'Id=1', 'Age>21', 'Price>=100', 'Count<50'. Operators: =, !=, >, <, >=, <= \n\nSTRING FILTERS: \n• Equals: 'LoginName=\"Security.Admin\"' \n• Not Equals: 'LoginName!=\"Security.Admin\"' \n• StartsWith: 'LoginName.StartsWith(\"Admin\")' \n• EndsWith: 'LoginName.EndsWith(\".Admin\")' \n• Contains (value in list): '(\"User01,User02\").Contains(LoginName)' \n• Contains (field contains substring): Use StartsWith/EndsWith for partial matches \n\nENUM FILTERS: For enum fields (typically ending with 'Values'), use GetEntitySchema first to identify them, then use .Value property: \n• Equals: 'DefaultPermissionValues.Value=\"Admin\"' \n• Not Equals: 'SystemRoleValues.Value!=\"Guest\"' \n• StartsWith: 'PermissionValues.Value.StartsWith(\"Admin\")' \n• Contains: '(\"Admin,User\").Contains(DefaultPermissionValues.Value)' \n\nCOMBINING CONDITIONS: Use '&&' (AND) or '||' (OR). Examples: \n• 'Age>21 && LoginName.StartsWith(\"Admin\")' \n• 'Status=\"Active\" || DefaultPermissionValues.Value=\"Admin\"' \n\nERROR RECOVERY: If this tool returns filter-related errors, call GetEntitySchema to see correct column names and data types, then retry with corrected filters.")]
        string? filterConditions = null,
        [Description("Optional: Comma-separated list of column names to return instead of all columns. Examples: 'FirstName,LastName', 'Id,LoginName,IsActive', 'Name,Email,Phone'. \n\nHIERARCHICAL SELECTION (Parent-Child): For entities with child relationships, use dot notation with plural child entity names: \n• 'FirstName,LastName,UserEmailAddresses.{Email,IsPrimary}' - Gets user data with related email addresses \n• 'Id,Status,AssetLocations.{LocationId,AssignedDate}' - Gets asset data with related locations \n• 'Name,PortfolioParameters.{ParameterName,ParameterValue}' - Gets portfolio with related parameters \n\nIMPORTANT: \n• Use exact column names from entity schema \n• Child entity names must be PLURAL in selection (UserEmailAddress → UserEmailAddresses) \n• Use curly braces for child attributes: ChildEntities.{attr1,attr2} \n• If this tool returns column-related errors, call GetEntitySchema to see correct column names and relationships, then retry with corrected column names.")]
        string? selectColumns = null,
        [Description("Optional: Sort order for the results in the format 'ColumnName SortOrder, ColumnName SortOrder'. \n\nSORT ORDER VALUES: \n• 'asc' for ascending order \n• 'desc' for descending order \n\nEXAMPLES: \n• 'Id desc' - Sort by Id in descending order \n• 'LastName asc' - Sort by LastName in ascending order \n• 'Id desc, LastName asc' - Sort by Id descending, then LastName ascending \n• 'CreatedDate desc, Name asc' - Sort by CreatedDate descending, then Name ascending \n\nIMPORTANT: Use exact column names from entity schema. If this tool returns column-related errors, call GetEntitySchema to see correct column names, then retry with corrected column names.")]
        string? orderBy = null,
        [Description("Optional: Page size for pagination (default: 100, max: 1000). Specify how many records to return per page.")]
        int? pageSize = null,
        [Description("Optional: Page index for pagination (1-based, default: 1). Specify which page to retrieve.")]
        int? pageIndex = null,
        [Description("Optional: Whether to automatically fetch all available records across multiple pages (default: false). When true, ignores pageSize and pageIndex parameters and retrieves all records that match the filter criteria. Use with caution for large datasets.")]
        bool fetchAllPages = false,
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
                return "Error: Entity name must be provided. Please specify an entity name like 'User', 'Product', 'Order', etc.\n\n" +
                       "SUGGESTION: Use GetAvailableEntities tool to see what entities are available in the system.";
            }

            logger.LogInformation("Using entity name: {EntityName}", singularEntityName);

            // Check if entity is persistent (not transient) before proceeding
            var persistenceValidation = await ValidateEntityPersistenceAsync(singularEntityName, cancellationToken);
            if (!persistenceValidation.IsValid)
            {
                return persistenceValidation.ErrorMessage;
            }

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

            // Log order by if provided
            if (!string.IsNullOrWhiteSpace(orderBy))
            {
                logger.LogInformation("Using order by: {OrderBy}", orderBy);
            }

            // Log pagination parameters if provided
            if (pageSize.HasValue || pageIndex.HasValue || fetchAllPages)
            {
                logger.LogInformation("Pagination - PageSize: {PageSize}, PageIndex: {PageIndex}, FetchAllPages: {FetchAllPages}", 
                    pageSize, pageIndex, fetchAllPages);
            }

            // Handle fetchAllPages scenario
            if (fetchAllPages)
            {
                return await FetchAllPagesAsync(singularEntityName, entityName, filterConditions, selectColumns, orderBy, cancellationToken);
            }
            
            // Build request body with Where, Select, OrderBy, and/or PaginationInfo parameters
            object requestBody;
            var bodyProperties = new Dictionary<string, object>();
            
            if (!string.IsNullOrWhiteSpace(filterConditions))
            {
                bodyProperties["Where"] = filterConditions;
            }
            
            if (!string.IsNullOrWhiteSpace(selectColumns))
            {
                bodyProperties["Select"] = selectColumns;
            }
            
            if (!string.IsNullOrWhiteSpace(orderBy))
            {
                bodyProperties["OrderBy"] = orderBy;
            }
            
            // Add pagination info if specified
            if (pageSize.HasValue || pageIndex.HasValue)
            {
                var paginationInfo = new Dictionary<string, object>();
                
                // Use provided pageSize or default to 100, max 1000
                var effectivePageSize = pageSize ?? 100;
                if (effectivePageSize > 1000) effectivePageSize = 1000;
                if (effectivePageSize < 1) effectivePageSize = 1;
                
                // Use provided pageIndex or default to 1 (1-based)
                var effectivePageIndex = pageIndex ?? 1;
                if (effectivePageIndex < 1) effectivePageIndex = 1;
                
                paginationInfo["PageSize"] = effectivePageSize;
                paginationInfo["PageIndex"] = effectivePageIndex;
                
                bodyProperties["PaginationInfo"] = paginationInfo;
                
                logger.LogInformation("Added pagination to request - PageSize: {PageSize}, PageIndex: {PageIndex}", 
                    effectivePageSize, effectivePageIndex);
            }
            
            requestBody = bodyProperties.Count > 0 ? bodyProperties : new { };
            
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
                                  "3. For hierarchical selection, ensure child entity names are plural (e.g., UserEmailAddresses not UserEmailAddress)\n" +
                                  "4. Use proper syntax for child entities: ChildEntities.{attr1,attr2}\n" +
                                  "5. Retry GetEntityData with corrected column names and proper filter/select syntax\n\n" +
                                  "This error suggests there might be issues with column names, data types, filter syntax, hierarchical selection, or column selection.";
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
            return $"Error: {ex.Message}";
        }
    }

    private string FormatEntityDataResponse(ApiResponse response, string entityName, string originalEntityName = "")
    {
        if (response.Data == null)
        {
            var message = $"Query completed for {entityName}, but no data was returned.";
            if (!string.IsNullOrEmpty(originalEntityName) && originalEntityName != entityName)
            {
                message = $"Converted plural '{originalEntityName}' to singular '{entityName}'\n\n" + message;
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
            
            var result = $"Successfully retrieved data from {entityName}\n";
            
            // Add conversion notice if entity name was converted
            if (!string.IsNullOrEmpty(originalEntityName) && originalEntityName != entityName)
            {
                result = $"Converted plural '{originalEntityName}' to singular '{entityName}'\n\n" + result;
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

            result += "Entity Data:\n";
            result += formattedData;

            return result;
        }
        catch (Exception ex)
        {
            // Fallback to simple string representation
            logger.LogWarning(ex, "Failed to format entity data response as JSON");
            var fallback = $"Successfully retrieved data from {entityName}\n\nData: {response.Data}";
            if (!string.IsNullOrEmpty(originalEntityName) && originalEntityName != entityName)
            {
                fallback = $"Converted plural '{originalEntityName}' to singular '{entityName}'\n\n" + fallback;
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

    /// <summary>
    /// Validates that an entity is persistent (not transient) before attempting data retrieval
    /// </summary>
    /// <param name="entityName">The entity name to validate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result indicating if entity is valid for data retrieval</returns>
    private async Task<(bool IsValid, string ErrorMessage)> ValidateEntityPersistenceAsync(string entityName, CancellationToken cancellationToken)
    {
        try
        {
            // Skip validation if schema file path is not configured
            if (string.IsNullOrWhiteSpace(_schemaOptions.EntityTypesFilePath))
            {
                logger.LogWarning("EntityTypes.xaml file path not configured, skipping persistence validation for {EntityName}", entityName);
                return (true, string.Empty); // Allow operation to proceed
            }

            // Skip validation if schema file doesn't exist
            if (!_entitySchemaService.ValidateEntityTypesFile(_schemaOptions.EntityTypesFilePath))
            {
                logger.LogWarning("EntityTypes.xaml file not found at {FilePath}, skipping persistence validation for {EntityName}", 
                    _schemaOptions.EntityTypesFilePath, entityName);
                return (true, string.Empty); // Allow operation to proceed
            }

            // Get entity schema to check persistence
            var schema = await _entitySchemaService.GetEntitySchemaAsync(entityName, _schemaOptions.EntityTypesFilePath, cancellationToken);
            
            if (schema == null)
            {
                // Entity not found in schema - let the API handle this error
                logger.LogInformation("Entity {EntityName} not found in schema, allowing API to handle entity validation", entityName);
                return (true, string.Empty);
            }

            // Check if entity is transient (Persistent="False")
            if (!schema.Persistent)
            {
                logger.LogWarning("Entity {EntityName} is transient (Persistent=false), data retrieval not allowed", entityName);
                var errorMessage = $"❌ Error: '{entityName}' is a Transient entity (Persistent=false) and is invalid for this request.\n\n" +
                                 "ℹ️ Transient entities are temporary and do not store persistent data that can be retrieved.\n" +
                                 "Please use GetAvailableEntities or GetEntitySchema to find entities that support data retrieval.";
                return (false, errorMessage);
            }

            logger.LogDebug("Entity {EntityName} validation passed - entity is persistent", entityName);
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error validating entity persistence for {EntityName}", entityName);
            // In case of validation errors, allow the operation to proceed and let the API handle any issues
            return (true, string.Empty);
        }
    }

    /// <summary>
    /// Fetches all pages of data for an entity query by making multiple API requests
    /// </summary>
    /// <param name="singularEntityName">The singular entity name for API calls</param>
    /// <param name="originalEntityName">The original entity name provided by user</param>
    /// <param name="filterConditions">Optional filter conditions</param>
    /// <param name="selectColumns">Optional column selection</param>
    /// <param name="orderBy">Optional sort order specification</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Formatted response containing all pages of data</returns>
    private async Task<string> FetchAllPagesAsync(
        string singularEntityName, 
        string originalEntityName, 
        string? filterConditions, 
        string? selectColumns, 
        string? orderBy,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Starting to fetch all pages for entity: {EntityName}", singularEntityName);

            // Validate entity persistence before proceeding with fetch all pages
            var persistenceValidation = await ValidateEntityPersistenceAsync(singularEntityName, cancellationToken);
            if (!persistenceValidation.IsValid)
            {
                return persistenceValidation.ErrorMessage;
            }
            
            var allData = new List<object>();
            var pageIndex = 1;
            const int pageSize = 100; // Use default page size for fetching all
            int totalItems = 0;
            int totalPages = 0;
            var responseMetadata = new Dictionary<string, object>();

            while (true)
            {
                // Build request body for current page
                var bodyProperties = new Dictionary<string, object>();
                
                if (!string.IsNullOrWhiteSpace(filterConditions))
                {
                    bodyProperties["Where"] = filterConditions;
                }
                
                if (!string.IsNullOrWhiteSpace(selectColumns))
                {
                    bodyProperties["Select"] = selectColumns;
                }
                
                if (!string.IsNullOrWhiteSpace(orderBy))
                {
                    bodyProperties["OrderBy"] = orderBy;
                }
                
                // Add pagination info for current page
                bodyProperties["PaginationInfo"] = new Dictionary<string, object>
                {
                    ["PageSize"] = pageSize,
                    ["PageIndex"] = pageIndex
                };

                var apiRequest = new ApiRequest
                {
                    Action = "Entity",
                    Resource = singularEntityName,
                    Method = "POST",
                    Body = bodyProperties
                };

                var response = await _apiService.ExecuteRequestAsync(apiRequest, cancellationToken);

                if (!response.Success)
                {
                    logger.LogError("Failed to fetch page {PageIndex} for {EntityName}: {Message}", 
                        pageIndex, singularEntityName, response.Message);
                    return $"? Error fetching page {pageIndex} from {singularEntityName}: {response.Message}";
                }

                // Parse pagination info from first response
                if (pageIndex == 1)
                {
                    responseMetadata = response.Metadata ?? new Dictionary<string, object>();
                    
                    if (responseMetadata.ContainsKey("page-info"))
                    {
                        try
                        {
                            var pageInfoValue = responseMetadata["page-info"]?.ToString();
                            if (!string.IsNullOrWhiteSpace(pageInfoValue))
                            {
                                var pageInfo = JsonSerializer.Deserialize<JsonElement>(pageInfoValue);
                                totalItems = pageInfo.GetProperty("TotalItems").GetInt32();
                                totalPages = (int)Math.Ceiling((double)totalItems / pageSize);
                                
                                logger.LogInformation("Total items: {TotalItems}, Total pages: {TotalPages}", 
                                    totalItems, totalPages);
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "Failed to parse page-info header");
                        }
                    }
                }

                // Add current page data to collection
                if (response.Data != null)
                {
                    // Handle both single object and array responses
                    if (response.Data is JsonElement element)
                    {
                        if (element.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var item in element.EnumerateArray())
                            {
                                allData.Add(item);
                            }
                        }
                        else
                        {
                            allData.Add(element);
                        }
                    }
                    else if (response.Data is IEnumerable<object> enumerable)
                    {
                        allData.AddRange(enumerable);
                    }
                    else
                    {
                        allData.Add(response.Data);
                    }
                }

                logger.LogInformation("Fetched page {PageIndex}, collected {ItemCount} items so far", 
                    pageIndex, allData.Count);

                // Check if we have more pages
                if (totalPages > 0 && pageIndex >= totalPages)
                {
                    logger.LogInformation("Reached final page {PageIndex} of {TotalPages}", pageIndex, totalPages);
                    break;
                }
                
                // Safety check - if no pagination info or current page returned no data, stop
                if (totalItems == 0 || (response.Data != null && !HasData(response.Data)))
                {
                    logger.LogInformation("No more data available, stopping at page {PageIndex}", pageIndex);
                    break;
                }

                pageIndex++;
                
                // Safety limit to prevent infinite loops
                if (pageIndex > 1000) 
                {
                    logger.LogWarning("Reached safety limit of 1000 pages, stopping fetch");
                    break;
                }
            }

            // Format combined response
            var result = $"Successfully retrieved ALL pages from {singularEntityName}\n";
            
            // Add conversion notice if entity name was converted
            if (!string.IsNullOrEmpty(originalEntityName) && originalEntityName != singularEntityName)
            {
                result = $"Converted plural '{originalEntityName}' to singular '{singularEntityName}'\n\n" + result;
            }
            
            result += $"\nFetch Summary:\n";
            result += $"  Total Pages Fetched: {pageIndex}\n";
            result += $"  Total Records Retrieved: {allData.Count}\n";
            
            if (totalItems > 0)
            {
                result += $"  Total Records Available: {totalItems}\n";
            }
            
            result += $"  Page Size Used: {pageSize}\n\n";

            // Add formatted data
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            result += "All Entity Data:\n";
            result += JsonSerializer.Serialize(allData, jsonOptions);

            logger.LogInformation("Successfully fetched all {PageCount} pages with {ItemCount} total items for {EntityName}", 
                pageIndex, allData.Count, singularEntityName);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching all pages for entity: {EntityName}", singularEntityName);
            return $"? Error fetching all pages from {singularEntityName}: {ex.Message}";
        }
    }

    /// <summary>
    /// Checks if the response data contains any items
    /// </summary>
    /// <param name="data">Response data to check</param>
    /// <returns>True if data contains items, false otherwise</returns>
    private static bool HasData(object data)
    {
        if (data == null) return false;
        
        if (data is JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Array)
            {
                return element.GetArrayLength() > 0;
            }
            return true;
        }
        
        if (data is IEnumerable<object> enumerable)
        {
            return enumerable.Any();
        }
        
        return true;
    }
}