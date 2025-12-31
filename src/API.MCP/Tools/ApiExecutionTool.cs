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
/// For full usage, workflow, and example documentation, see Documentation/ApiExecutionTool.md.
/// </summary>

[McpServerToolType]

public class ApiExecutionTool(
    IApiService apiService,
    IEntitySchemaService entitySchemaService,
    IOptions<SchemaOptions> schemaOptions,
    ILogger<ApiExecutionTool> logger,
    IEntityNameService entityNameService)
{
    private readonly IApiService _apiService = apiService;
    private readonly IEntitySchemaService _entitySchemaService = entitySchemaService;
    private readonly SchemaOptions _schemaOptions = schemaOptions.Value;
    private readonly IEntityNameService _entityNameService = entityNameService;

    [McpServerTool, Description("Ping the API to check if it's alive and running. Performs a health check to verify the API system is operational.")]
    public async Task<string> PingApi(CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Performing API ping check");
            
            var response = await ((ApiService)_apiService).PingAsync(cancellationToken);

            if (response.Success && response.Data != null)
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("API is alive and running!\n");
                sb.AppendLine($"Success: {response.Data.Success}");
                sb.AppendLine($"Processed Time: {response.Data.ProcessedTime:yyyy-MM-dd HH:mm:ss.fff zzz}");
                sb.Append($"Response Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
                return sb.ToString();
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
        [Description("Optional: Filter conditions in the format 'Field=Value || Field>Value' or 'Field=Value && Field>Value'. \n\nNUMERIC FILTERS: Examples: 'Id=1', 'Age>21', 'Price>=100', 'Count<50'. Operators: =, !=, >, <, >=, <= \n\nSTRING FILTERS: \n• Equals: 'LoginName=\"Security.Admin\"' \n• Not Equals: 'LoginName!=\"Security.Admin\"' \n• StartsWith: 'LoginName.StartsWith(\"Admin\")' \n• EndsWith: 'LoginName.EndsWith(\".Admin\")' \n• Contains (value in list): '(\"User01,User02\").Contains(LoginName)' \n• Contains (field contains substring): Use StartsWith/EndsWith for partial matches \n\nENUM FILTERS: For enum fields (typically ending with 'Values'), use GetEntitySchema first to identify them, then use .Value property: \n• Equals: 'DefaultPermissionValues.Value=\"Admin\"' \n• Not Equals: 'SystemRoleValues.Value!=\"Guest\"' \n• StartsWith: 'PermissionValues.Value.StartsWith(\"Admin\")' \n• Contains: '(\"Admin,User\").Contains(DefaultPermissionValues.Value)' \n\nREFERENCE FILTERS: Filter by related entity attributes using dot notation: \n• 'Role.Name=\"Admin\"' - Filter by referenced entity attribute \n• 'Type.Portfolio.Name=\"Default Portfolio\"' - Filter by chained reference \n• 'RolesForUsers.Role.Name.StartsWith(\"Admin\")' - Filter by child entity's reference \n• 'AssetLocations.Location.Name=\"Warehouse A\"' - Filter by child's referenced entity \n• 'LegalEntitiesForUsers.LegalEntityRef.Code=\"LE001\"' - Filter by reference from child entity \n\nCOMBINING CONDITIONS: Use '&&' (AND) or '||' (OR). Examples: \n• 'Age>21 && LoginName.StartsWith(\"Admin\")' \n• 'Status=\"Active\" || DefaultPermissionValues.Value=\"Admin\"' \n• 'Status.Value=\"Scrap\" && Quantity=1 && Type.Portfolio.Name=\"Default Portfolio\"' - Combining enum, numeric, and reference filters \n\nERROR RECOVERY: If this tool returns filter-related errors, call GetEntitySchema to see correct column names and data types, then retry with corrected filters.")]
        string? filterConditions = null,
        [Description("Optional: Comma-separated list of column names to return instead of all columns. Examples: 'FirstName,LastName', 'Id,LoginName,IsActive', 'Name,Email,Phone'. \n\nHIERARCHICAL SELECTION (Parent-Child): For entities with child relationships, use dot notation with plural child entity names: \n• 'FirstName,LastName,UserEmailAddresses.{Email,IsPrimary}' - Gets user data with related email addresses \n• 'Id,Status,AssetLocations.{LocationId,AssignedDate}' - Gets asset data with related locations \n• 'Name,PortfolioParameters.{ParameterName,ParameterValue}' - Gets portfolio with related parameters \n\nREFERENCE SELECTION: Access related entities via foreign key references using dot notation: \n• 'LoginName,RolesForUsers.{Id,Role.Name}' - Gets user with role names via reference \n• 'Status,Id,AssetLocations.{LocationId,Location.Id},Type.Portfolio.Name' - Gets asset with location and portfolio references \n• 'LegalEntitiesForUsers.{ActivationDate,LegalEntityRef.Name}' - Gets legal entity assignments with referenced entity details \n\nREFERENCE CHAINING: Navigate through multiple reference levels: \n• 'Type.Portfolio.Owner.Name' - Chain references: Type → Portfolio → Owner \n• 'RolesForUsers.{Role.Category.Name}' - Access referenced entity's references \n\nIMPORTANT: \n• Use exact column names from entity schema \n• Child entity names must be PLURAL in selection (UserEmailAddress → UserEmailAddresses) \n• References use singular names as defined in schema (Role.Name, not Roles.Name) \n• Use curly braces for child attributes: ChildEntities.{attr1,attr2} \n• Use dot notation for references: ReferenceName.Attribute or ReferenceName.AnotherRef.Attribute \n• If this tool returns column-related errors, call GetEntitySchema to see correct column names and relationships, then retry with corrected column names.")]
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
            var singularEntityName = _entityNameService.ConvertPluralToSingular(entityName);
            
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
                var errorMessage = $"Error retrieving data from {singularEntityName}: {response.Message}\n\n";
                
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
            var sb = new System.Text.StringBuilder();
            if (!string.IsNullOrEmpty(originalEntityName) && originalEntityName != entityName)
            {
                sb.AppendLine($"Converted plural '{originalEntityName}' to singular '{entityName}'\n");
            }
            sb.Append($"Query completed for {entityName}, but no data was returned.");
            return sb.ToString();
        }

        try
        {
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            var formattedData = JsonSerializer.Serialize(response.Data, jsonOptions);
            var sb = new System.Text.StringBuilder();
            if (!string.IsNullOrEmpty(originalEntityName) && originalEntityName != entityName)
            {
                sb.AppendLine($"Converted plural '{originalEntityName}' to singular '{entityName}'\n");
            }
            sb.AppendLine($"Successfully retrieved data from {entityName}\n");
            // Add HTTP Status Code
            if (!string.IsNullOrWhiteSpace(response.ErrorCode))
            {
                sb.AppendLine($"HTTP Status: {response.ErrorCode}");
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
                        sb.AppendLine("Pagination Info:");
                        sb.AppendLine($"  Page Size: {pageInfo.GetProperty("PageSize").GetInt32()}");
                        sb.AppendLine($"  Current Page: {pageInfo.GetProperty("PageIndex").GetInt32()}");
                        sb.AppendLine($"  Total Items: {pageInfo.GetProperty("TotalItems").GetInt32()}\n");
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to parse page-info header");
                    sb.AppendLine($"Page Info: {response.Metadata["page-info"]}\n");
                }
            }
            sb.AppendLine("Entity Data:");
            sb.Append(formattedData);
            return sb.ToString();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to format entity data response as JSON");
            var sb = new System.Text.StringBuilder();
            if (!string.IsNullOrEmpty(originalEntityName) && originalEntityName != entityName)
            {
                sb.AppendLine($"Converted plural '{originalEntityName}' to singular '{entityName}'\n");
            }
            sb.Append($"Successfully retrieved data from {entityName}\n\nData: {response.Data}");
            return sb.ToString();
        }
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
            var frameworkPath = _schemaOptions.GetFrameworkFilePath();
            if (string.IsNullOrWhiteSpace(frameworkPath))
            {
                logger.LogWarning("Framework EntityTypes.xaml file path not configured, skipping persistence validation for {EntityName}", entityName);
                return (true, string.Empty); // Allow operation to proceed
            }

            // Skip validation if schema file doesn't exist
            if (!_entitySchemaService.ValidateEntityTypesFile(frameworkPath))
            {
                logger.LogWarning("Framework EntityTypes.xaml file not found at {FilePath}, skipping persistence validation for {EntityName}", 
                    frameworkPath, entityName);
                return (true, string.Empty); // Allow operation to proceed
            }

            // Get entity schema to check persistence using multi-layer approach
            var productPath = _schemaOptions.ProductEntityTypesFilePath;
            var schema = await _entitySchemaService.GetEntitySchemaAsync(entityName, frameworkPath, productPath, cancellationToken);
            
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

            // Validate entity persistence before proceeding with fetch all pages (cache result for this workflow)
            (bool IsValid, string ErrorMessage)? persistenceValidation = null;
            async Task<(bool IsValid, string ErrorMessage)> GetPersistenceValidationAsync()
            {
                if (persistenceValidation == null)
                {
                    persistenceValidation = await ValidateEntityPersistenceAsync(singularEntityName, cancellationToken);
                }
                return persistenceValidation.Value;
            }

            var validation = await GetPersistenceValidationAsync();
            if (!validation.IsValid)
            {
                return validation.ErrorMessage;
            }

            var allData = new List<object>();
            var pageIndex = 1;
            const int pageSize = 100; // Use default page size for fetching all
            int totalItems = 0;
            int totalPages = 0;
            var responseMetadata = new Dictionary<string, object>();

            while (true)
            {
                var validationLoop = await GetPersistenceValidationAsync();
                if (!validationLoop.IsValid)
                {
                    return validationLoop.ErrorMessage;
                }

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