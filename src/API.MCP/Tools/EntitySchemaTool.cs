using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using API.MCP.Services;
using API.MCP.Models;
using API.MCP.Configuration;

namespace API.MCP.Tools;

/// <summary>
/// Entity schema tool for discovering and understanding entity structures from XML definitions.
/// 
/// AI USAGE GUIDANCE:
/// - Use GetEntitySchema() when users ask about specific entities or mention column names in filters
/// - Use GetAvailableEntities() when users ask what entities exist or when entity name is unclear
/// - Always check schema before forming complex filter conditions in GetEntityData()
/// - Use the data type information to properly format filter values (strings with quotes, numbers without)
/// 
/// INTEGRATION WITH GetEntityData:
/// 1. User asks for filtered data → GetEntitySchema() first to validate columns
/// 2. Use schema info to correct column names and data types
/// 3. Form proper filter syntax for GetEntityData()
/// </summary>

[McpServerToolType]
public class EntitySchemaTool(IEntitySchemaService schemaService, IOptions<SchemaOptions> schemaOptions, ILogger<EntitySchemaTool> logger)
{
    private readonly IEntitySchemaService _schemaService = schemaService;
    private readonly SchemaOptions _schemaOptions = schemaOptions.Value;

    [McpServerTool, Description("Get the schema (structure) of a specific entity to understand its attributes, types, and constraints. This helps in forming correct queries and understanding data types for filtering. CRITICAL: Use this tool when GetEntityData returns column-related errors or before creating complex filter conditions.")]
    public async Task<string> GetEntitySchema(
        [Description("Entity name that the AI extracted from user query. Should be singular - the AI should convert plural forms to singular. Examples: 'GlobalParameters' will become 'GlobalParameter', 'Users' will become 'User', 'EntityResources' will become 'EntityResource'. USAGE: Call this when GetEntityData fails or when you need to validate column names before filtering.")]
        string entityName,
        [Description("Optional: Path to the EntityTypes.xaml file that contains entity definitions. If not provided, uses the configured default path from SCHEMA_ENTITY_TYPES_FILE_PATH environment variable.")]
        string? entityTypesFilePath = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Use configured path if not provided
            var filePath = entityTypesFilePath ?? _schemaOptions.EntityTypesFilePath;
                       
            logger.LogInformation("Getting entity schema for: {OriginalName} -> {SingularName} from file: {FilePath}", 
                entityName, entityName, filePath);

            if (string.IsNullOrWhiteSpace(entityName))
            {
                return "❌ Error: Entity name must be provided. Please specify an entity name like 'GlobalParameter', 'User', 'Product', etc.";
            }

            if (string.IsNullOrWhiteSpace(filePath))
            {
                return "❌ Error: EntityTypes.xaml file path must be provided either as parameter or configured via SCHEMA_ENTITY_TYPES_FILE_PATH environment variable.";
            }

            // Validate file existence
            if (!_schemaService.ValidateEntityTypesFile(filePath))
            {
                return $"❌ Error: EntityTypes.xaml file not found at path: {filePath}";
            }

            // Try to find exact match first using singular form
            var schema = await _schemaService.GetEntitySchemaAsync(entityName, filePath, cancellationToken);
            
            if (schema == null)
            {
                // Try to find closest match for typo correction
                var closestMatch = await _schemaService.FindClosestEntityNameAsync(entityName, filePath, cancellationToken);
                
                if (closestMatch != null)
                {
                    logger.LogInformation("Entity '{EntityName}' not found, but found closest match: '{ClosestMatch}'", entityName, closestMatch);
                    schema = await _schemaService.GetEntitySchemaAsync(closestMatch, filePath, cancellationToken);
                    
                    if (schema != null)
                    {
                        var result = FormatEntitySchemaWithSuggestion(schema, entityName, closestMatch);
                        return result;
                    }
                }

                // If no close match found, show available entities
                var availableEntities = await _schemaService.GetAvailableEntitiesAsync(filePath, cancellationToken);
                var entitiesList = availableEntities.Count > 0 ? string.Join(", ", availableEntities.Take(10)) : "None found";
                
                var errorMessage = $"❌ Entity '{entityName}' not found in EntityTypes.xaml.";
                errorMessage += $"\n\nAvailable entities: {entitiesList}";
                if (availableEntities.Count > 10)
                {
                    errorMessage += $" (and {availableEntities.Count - 10} more)";
                }
                
                return errorMessage;
            }

            var finalResult = FormatEntitySchema(schema);
            return finalResult;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting entity schema for '{EntityName}'", entityName);
            return $"❌ Error retrieving schema for '{entityName}': {ex.Message}";
        }
    }

    [McpServerTool, Description("Get a list of all available entities from the EntityTypes.xaml file. Useful for discovering what entities are available in the system.")]
    public async Task<string> GetAvailableEntities(
        [Description("Optional: Path to the EntityTypes.xaml file that contains entity definitions. If not provided, uses the configured default path from SCHEMA_ENTITY_TYPES_FILE_PATH environment variable.")]
        string? entityTypesFilePath = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Use configured path if not provided
            var filePath = entityTypesFilePath ?? _schemaOptions.EntityTypesFilePath;
            
            logger.LogInformation("Getting available entities from file: {FilePath}", filePath);

            if (string.IsNullOrWhiteSpace(filePath))
            {
                return "❌ Error: EntityTypes.xaml file path must be provided either as parameter or configured via SCHEMA_ENTITY_TYPES_FILE_PATH environment variable.";
            }

            if (!_schemaService.ValidateEntityTypesFile(filePath))
            {
                return $"❌ Error: EntityTypes.xaml file not found at path: {filePath}";
            }

            var entities = await _schemaService.GetAvailableEntitiesAsync(filePath, cancellationToken);

            if (entities.Count == 0)
            {
                return "ℹ️ No entities found in the EntityTypes.xaml file.";
            }

            var result = $"✅ Found {entities.Count} available entities:\n\n";
            
            // Group entities alphabetically
            var groupedEntities = entities.OrderBy(e => e).ToList();
            
            for (int i = 0; i < groupedEntities.Count; i++)
            {
                result += $"{i + 1:D2}. {groupedEntities[i]}\n";
            }

            result += $"\nTo get detailed schema for any entity, use GetEntitySchema with the entity name.";

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting available entities");
            return $"❌ Error retrieving available entities: {ex.Message}";
        }
    }

    private string FormatEntitySchema(EntitySchema schema)
    {
        var result = $"✅ Entity Schema: {schema.Name}\n\n";

        // Basic information
        if (!string.IsNullOrWhiteSpace(schema.Description))
        {
            result += $"Description: {schema.Description}\n";
        }
        
        if (!string.IsNullOrWhiteSpace(schema.Label))
        {
            result += $"Label: {schema.Label}\n";
        }

        result += $"Persistent: {schema.Persistent}\n";
        result += $"Securable: {schema.Securable}\n\n";

        // Attributes
        if (schema.Attributes.Count > 0)
        {
            result += "📋 Attributes:\n";
            result += "┌─────────────────────────────────────────────────────────────────────────────────────┐\n";
            result += "│ Name                    │ Type           │ Nullable │ Description                    │\n";
            result += "├─────────────────────────────────────────────────────────────────────────────────────┤\n";

            foreach (var attr in schema.Attributes)
            {
                var name = attr.Name.PadRight(23);
                var type = attr.Type.PadRight(14);
                var nullable = (attr.Nullable ? "Yes" : "No").PadRight(8);
                var description = (attr.Description?.Length > 30 ? 
                    attr.Description.Substring(0, 27) + "..." : (attr.Description ?? "")).PadRight(30);
                
                result += $"│ {name} │ {type} │ {nullable} │ {description} │\n";
                
                // Add additional info for special attributes
                if (attr.NaturalIdentity || attr.QueryUnchangedValue || !attr.Persistent)
                {
                    var flags = new List<string>();
                    if (attr.NaturalIdentity) flags.Add("Natural ID");
                    if (attr.QueryUnchangedValue) flags.Add("Query Unchanged");
                    if (!attr.Persistent) flags.Add("Non-Persistent");
                    
                    var flagText = $"({string.Join(", ", flags)})".PadRight(75);
                    result += $"│   └─ {flagText} │\n";
                }
            }
            result += "└─────────────────────────────────────────────────────────────────────────────────────┘\n\n";

            // Data type summary for AI assistance
            result += "🔍 Data Type Summary for Filtering:\n";
            var stringAttrs = schema.Attributes.Where(a => a.IsStringType).Select(a => a.Name).ToList();
            var numericAttrs = schema.Attributes.Where(a => a.IsNumericType).Select(a => a.Name).ToList();
            var booleanAttrs = schema.Attributes.Where(a => a.IsBooleanType).Select(a => a.Name).ToList();
            var dateTimeAttrs = schema.Attributes.Where(a => a.IsDateTimeType).Select(a => a.Name).ToList();
            var enumAttrs = schema.Attributes.Where(a => a.Name.EndsWith("Values", StringComparison.OrdinalIgnoreCase)).Select(a => a.Name).ToList();

            if (stringAttrs.Any())
                result += $"• String fields: {string.Join(", ", stringAttrs)}\n";
            if (numericAttrs.Any())
                result += $"• Numeric fields: {string.Join(", ", numericAttrs)}\n";
            if (booleanAttrs.Any())
                result += $"• Boolean fields: {string.Join(", ", booleanAttrs)}\n";
            if (dateTimeAttrs.Any())
                result += $"• Date/Time fields: {string.Join(", ", dateTimeAttrs)}\n";
            if (enumAttrs.Any())
            {
                result += $"• ENUM fields (use .Value property): {string.Join(", ", enumAttrs)}\n";
                result += "  ⚠️ IMPORTANT: For enum fields, always use '.Value' in filters: EnumField.Value=\"SomeValue\"\n";
            }

            result += "\n";
        }

        // References
        if (schema.References.Count > 0)
        {
            result += "🔗 References:\n";
            foreach (var reference in schema.References)
            {
                result += $"• {reference.Name} → {reference.RefersTo}";
                if (!reference.Nullable) result += " (Required)";
                if (!string.IsNullOrWhiteSpace(reference.Description))
                    result += $" - {reference.Description}";
                result += "\n";
            }
            result += "\n";
        }

        // Indexes
        if (schema.Indexes.Count > 0)
        {
            result += "📊 Indexes:\n";
            foreach (var index in schema.Indexes)
            {
                result += $"• {index.Name} ({(index.IsUnique ? "Unique" : "Non-unique")})";
                result += $" - Fields: {string.Join(", ", index.Fields)}";
                if (index.CoveredFields.Any())
                    result += $" | Covered: {string.Join(", ", index.CoveredFields)}";
                result += "\n";
            }
        }

        return result;
    }

    private string FormatEntitySchemaWithSuggestion(EntitySchema schema, string originalInput, string suggestedName)
    {
        var result = $"💡 Entity '{originalInput}' not found, but found similar entity: '{suggestedName}'\n\n";
        result += FormatEntitySchema(schema);
        result += $"\n🔧 NEXT STEP: Use GetEntityData with entity name '{suggestedName}' for your data queries.";
        return result;
    }
}