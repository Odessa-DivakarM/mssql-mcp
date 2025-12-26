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
/// AI-FIRST DESIGN APPROACH:
/// This tool provides structured, AI-friendly data output. The AI should handle:
/// - Complex formatting and presentation logic
/// - Smart pluralization (beyond simple "add s" rule)
/// - Context-sensitive error messages and suggestions
/// - Intelligent schema interpretation and recommendations
/// 
/// CORE USAGE PATTERNS:
/// - GetEntitySchema() → Get structured entity information
/// - GetAvailableEntities() → List all entities with persistence status
/// - Use schema data to validate GetEntityData() parameters
/// 
/// The tool returns concise, structured information that AI can enhance with:
/// - Better formatting, explanations, and examples
/// - Context-aware suggestions and error recovery
/// - Smart relationship analysis and recommendations
/// </summary>

[McpServerToolType]
public class EntitySchemaTool(IEntitySchemaService schemaService, IOptions<SchemaOptions> schemaOptions, ILogger<EntitySchemaTool> logger)
{
    private readonly IEntitySchemaService _schemaService = schemaService;
    private readonly SchemaOptions _schemaOptions = schemaOptions.Value;

    [McpServerTool, Description("Get structured entity schema information. Returns entity structure, attributes, relationships, and usage guidance. AI should use this data to help users understand entities and construct proper queries.")]
    public async Task<string> GetEntitySchema(
        [Description("Entity name to analyze. AI should handle pluralization and name normalization. Examples: 'User', 'GlobalParameter', 'AssetLocation'.")]
        string entityName,
        [Description("Optional schema file path. Uses configured default if not specified.")]
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
                        var result = await FormatEntitySchemaWithSuggestion(schema, entityName, closestMatch, cancellationToken);
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

            var finalResult = await FormatEntitySchema(schema, cancellationToken);
            return finalResult;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting entity schema for '{EntityName}'", entityName);
            return $"❌ Error retrieving schema for '{entityName}': {ex.Message}";
        }
    }

    [McpServerTool, Description("List all available entities with persistence status. Returns categorized list of queryable vs non-queryable entities. AI should use this to guide users to appropriate entities.")]
    public async Task<string> GetAvailableEntities(
        [Description("Optional schema file path. Uses configured default if not specified.")]
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

            var entities = await _schemaService.GetEntitiesWithPersistenceAsync(filePath, cancellationToken);

            if (entities.Count == 0)
            {
                return "ℹ️ No entities found in the EntityTypes.xaml file.";
            }

            var result = $"✅ Found {entities.Count} entities:\n\n";
            
            // Simplified categorization - let AI handle complex formatting
            var persistentEntities = entities.Where(e => e.Value).OrderBy(e => e.Key).ToList();
            var transientEntities = entities.Where(e => !e.Value).OrderBy(e => e.Key).ToList();
            
            if (persistentEntities.Count > 0)
            {
                result += "📊 PERSISTENT (queryable):\n";
                result += string.Join(", ", persistentEntities.Select(e => e.Key)) + "\n\n";
            }
            
            if (transientEntities.Count > 0)
            {
                result += "⚠️ TRANSIENT (non-queryable):\n";
                result += string.Join(", ", transientEntities.Select(e => e.Key)) + "\n\n";
            }

            result += "💡 Use GetEntitySchema(entityName) for detailed information\n";
            result += "💡 Use GetEntityData() only with PERSISTENT entities";

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting available entities");
            return $"❌ Error retrieving available entities: {ex.Message}";
        }
    }

    private async Task<string> FormatEntitySchema(EntitySchema schema, CancellationToken cancellationToken = default)
    {
        var result = $"✅ Entity Schema: {schema.Name}\n\n";

        // Basic information
        if (!string.IsNullOrWhiteSpace(schema.Description))
            result += $"Description: {schema.Description}\n";
        
        if (!string.IsNullOrWhiteSpace(schema.Label))
            result += $"Label: {schema.Label}\n";

        result += $"Persistent: {schema.Persistent}\n";
        result += $"Securable: {schema.Securable}\n";
        
        // Parent-child relationship information
        if (schema.IsChildEntity)
        {
            result += $"Parent Entity: {schema.ParentEntity}\n";
            result += $"Parent Relation: {schema.ParentRelation}\n";
            result += $"💡 Child entity - query via parent: {schema.ParentEntity}\n";
            result += $"📝 Hierarchical selection: {schema.PluralName}.{{attr1,attr2}}\n";
        }
        
        // Transient entity warning
        if (!schema.Persistent)
        {
            result += "\n⚠️ TRANSIENT ENTITY - Cannot retrieve data\n";
            result += "💡 Use GetAvailableEntities for queryable entities\n";
        }
        
        result += "\n";

        // Simplified attributes display
        if (schema.Attributes.Count > 0)
        {
            result += "📋 Attributes:\n";
            foreach (var attr in schema.Attributes)
            {
                result += $"  • {attr.Name} ({attr.Type})";
                if (!attr.Nullable) result += " *required*";
                if (!attr.Persistent) result += " *non-persistent*";
                if (attr.NaturalIdentity) result += " *natural-id*";
                result += "\n";
                
                if (!string.IsNullOrWhiteSpace(attr.Description))
                    result += $"    {attr.Description}\n";
            }
            
            // Data type categorization for AI
            var categories = new[]
            {
                ("String", schema.Attributes.Where(a => a.IsStringType).Select(a => a.Name)),
                ("Numeric", schema.Attributes.Where(a => a.IsNumericType).Select(a => a.Name)),
                ("Boolean", schema.Attributes.Where(a => a.IsBooleanType).Select(a => a.Name)),
                ("DateTime", schema.Attributes.Where(a => a.IsDateTimeType).Select(a => a.Name)),
                ("Enum", schema.Attributes.Where(a => a.Name.EndsWith("Values", StringComparison.OrdinalIgnoreCase)).Select(a => a.Name))
            };
            
            result += "\n🔍 Field Types:\n";
            foreach (var (category, fields) in categories)
            {
                var fieldList = fields.ToList();
                if (fieldList.Any())
                    result += $"  {category}: {string.Join(", ", fieldList)}\n";
            }
            
            var enumFields = schema.Attributes.Where(a => a.Name.EndsWith("Values", StringComparison.OrdinalIgnoreCase)).ToList();
            if (enumFields.Any())
                result += "  ⚠️ Enum fields require .Value syntax in filters\n";
            
            result += "\n";
        }

        // References
        if (schema.References.Count > 0)
        {
            result += "🔗 References:\n";
            foreach (var reference in schema.References)
            {
                result += $"  • {reference.Name} → {reference.RefersTo}";
                if (!reference.Nullable) result += " *required*";
                if (!string.IsNullOrWhiteSpace(reference.Description))
                    result += $" ({reference.Description})";
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
                result += $"  • {index.Name} ({(index.IsUnique ? "Unique" : "Non-unique")}) on {string.Join(", ", index.Fields)}\n";
            }
            result += "\n";
        }

        // Child entities - Let AI handle complex formatting
        try
        {
            var filePath = _schemaOptions.EntityTypesFilePath;
            if (!string.IsNullOrWhiteSpace(filePath) && _schemaService.ValidateEntityTypesFile(filePath))
            {
                var childEntities = await _schemaService.GetChildEntitiesAsync(schema.Name, filePath, cancellationToken);
                
                if (childEntities.Count > 0)
                {
                    result += "👶 Child Entities:\n";
                    foreach (var child in childEntities.OrderBy(c => c.Name))
                    {
                        result += $"  • {child.Name} ({child.ParentRelation})";
                        if (!child.Persistent) result += " *transient*";
                        result += $"\n    Selection: {child.PluralName}.{{attributes}}\n";
                    }
                    
                    // Simple example - let AI generate more sophisticated ones
                    var persistentChild = childEntities.FirstOrDefault(c => c.Persistent);
                    if (persistentChild != null)
                    {
                        var sampleAttrs = persistentChild.Attributes.Take(2).Select(a => a.Name);
                        result += $"\n💡 Example: Select=\"Id,Name,{persistentChild.PluralName}.{{{string.Join(",", sampleAttrs)}}}\"\n";
                    }
                    result += "\n";
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not load child entities for {EntityName}", schema.Name);
        }

        return result;
    }

    private async Task<string> FormatEntitySchemaWithSuggestion(EntitySchema schema, string originalInput, string suggestedName, CancellationToken cancellationToken = default)
    {
        var result = $"💡 '{originalInput}' not found. Did you mean '{suggestedName}'?\n\n";
        result += await FormatEntitySchema(schema, cancellationToken);
        result += $"\n🔧 Use GetEntityData with '{suggestedName}' for queries";
        return result;
    }
}