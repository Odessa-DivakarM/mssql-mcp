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
/// For full usage, workflow, and example documentation, see Documentation/EntitySchemaTool.md.
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
        [Description("Optional framework schema file path. Uses configured default if not specified.")]
        string? frameworkFilePath = null,
        [Description("Optional product schema file path. Uses configured default if not specified.")]
        string? productFilePath = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Use configured paths if not provided
            var effectiveFrameworkPath = frameworkFilePath ?? _schemaOptions.GetFrameworkFilePath();
            var effectiveProductPath = productFilePath ?? _schemaOptions.ProductEntityTypesFilePath;
                       
            logger.LogInformation("Getting entity schema for: {EntityName} from framework: {FrameworkPath}, product: {ProductPath}", 
                entityName, effectiveFrameworkPath, effectiveProductPath ?? "none");

            if (string.IsNullOrWhiteSpace(entityName))
            {
                return "❌ Error: Entity name must be provided. Please specify an entity name like 'GlobalParameter', 'User', 'Product', etc.";
            }

            if (string.IsNullOrWhiteSpace(effectiveFrameworkPath))
            {
                return "❌ Error: Framework EntityTypes.xaml file path must be provided either as parameter or configured via SCHEMA_FRAMEWORK_ENTITY_TYPES_FILE_PATH or legacy SCHEMA_ENTITY_TYPES_FILE_PATH environment variable.";
            }

            // Validate framework file existence
            if (!_schemaService.ValidateEntityTypesFile(effectiveFrameworkPath))
            {
                return $"❌ Error: Framework EntityTypes.xaml file not found at path: {effectiveFrameworkPath}";
            }

            // Validate product file if provided
            if (!string.IsNullOrWhiteSpace(effectiveProductPath) && !_schemaService.ValidateEntityTypesFile(effectiveProductPath))
            {
                logger.LogWarning("Product EntityTypes.xaml file not found at path: {ProductPath}, proceeding with framework only", effectiveProductPath);
                effectiveProductPath = null;
            }

            // Try to find exact match first using multi-layer approach
            var schema = await _schemaService.GetEntitySchemaAsync(entityName, effectiveFrameworkPath, effectiveProductPath, cancellationToken);
            
            if (schema == null)
            {
                // Try to find closest match for typo correction
                var closestMatch = await _schemaService.FindClosestEntityNameAsync(entityName, effectiveFrameworkPath, effectiveProductPath, cancellationToken);
                
                if (closestMatch != null)
                {
                    logger.LogInformation("Entity '{EntityName}' not found, but found closest match: '{ClosestMatch}'", entityName, closestMatch);
                    schema = await _schemaService.GetEntitySchemaAsync(closestMatch, effectiveFrameworkPath, effectiveProductPath, cancellationToken);
                    
                    if (schema != null)
                    {
                        var result = await FormatEntitySchemaWithSuggestion(schema, entityName, closestMatch, cancellationToken);
                        return result;
                    }
                }

                // If no close match found, show available entities
                var availableEntities = await _schemaService.GetAvailableEntitiesAsync(effectiveFrameworkPath, effectiveProductPath, cancellationToken);
                var entitiesList = availableEntities.Count > 0 ? string.Join(", ", availableEntities.Take(10)) : "None found";
                
                var errorMessage = $"❌ Entity '{entityName}' not found in EntityTypes.xaml files.";
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
        [Description("Optional framework schema file path. Uses configured default if not specified.")]
        string? frameworkFilePath = null,
        [Description("Optional product schema file path. Uses configured default if not specified.")]
        string? productFilePath = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Use configured paths if not provided
            var effectiveFrameworkPath = frameworkFilePath ?? _schemaOptions.GetFrameworkFilePath();
            var effectiveProductPath = productFilePath ?? _schemaOptions.ProductEntityTypesFilePath;
            
            logger.LogInformation("Getting available entities from framework: {FrameworkPath}, product: {ProductPath}", 
                effectiveFrameworkPath, effectiveProductPath ?? "none");

            if (string.IsNullOrWhiteSpace(effectiveFrameworkPath))
            {
                return "❌ Error: Framework EntityTypes.xaml file path must be provided either as parameter or configured via SCHEMA_FRAMEWORK_ENTITY_TYPES_FILE_PATH or legacy SCHEMA_ENTITY_TYPES_FILE_PATH environment variable.";
            }

            if (!_schemaService.ValidateEntityTypesFile(effectiveFrameworkPath))
            {
                return $"❌ Error: Framework EntityTypes.xaml file not found at path: {effectiveFrameworkPath}";
            }

            // Validate product file if provided
            if (!string.IsNullOrWhiteSpace(effectiveProductPath) && !_schemaService.ValidateEntityTypesFile(effectiveProductPath))
            {
                logger.LogWarning("Product EntityTypes.xaml file not found at path: {ProductPath}, proceeding with framework only", effectiveProductPath);
                effectiveProductPath = null;
            }

            var entities = await _schemaService.GetEntitiesWithPersistenceAsync(effectiveFrameworkPath, effectiveProductPath, cancellationToken);

            if (entities.Count == 0)
            {
                return "ℹ️ No entities found in the EntityTypes.xaml files.";
            }

            var result = $"✅ Found {entities.Count} entities";
            if (_schemaOptions.IsMultiLayerEnabled)
            {
                result += " (across Framework and Product layers)";
            }
            result += ":\n\n";
            
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

            if (_schemaOptions.IsMultiLayerEnabled)
            {
                result += "\n🏗️ Multi-layer system active - entities may be enhanced with Product extensions";
            }

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
        var result = $"✅ Entity Schema: {schema.Name}";
        
        // Show layer information if multi-layer
        if (schema.IsMergedFromLayers)
        {
            result += " 🏗️ (Multi-layer)";
        }
        else if (schema.SourceLayer != EntitySourceLayer.Framework)
        {
            result += $" ({schema.SourceLayer} layer)";
        }
        
        result += "\n\n";

        // Basic information
        if (!string.IsNullOrWhiteSpace(schema.Description))
            result += $"Description: {schema.Description}\n";
        
        if (!string.IsNullOrWhiteSpace(schema.Label))
            result += $"Label: {schema.Label}\n";

        result += $"Persistent: {schema.Persistent}\n";
        result += $"Securable: {schema.Securable}\n";
        
        // Layer information
        if (schema.IsMergedFromLayers)
        {
            result += "🏗️ Merged from Framework + Product layers\n";
        }
        else
        {
            result += $"Source Layer: {schema.SourceLayer}\n";
        }
        
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
                if (attr.IsAlteredInProduct) result += " *modified-in-product*";
                if (attr.SourceLayer == EntitySourceLayer.Product) result += " *product-layer*";
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
                
            // Show product layer enhancements
            var productAttrs = schema.Attributes.Where(a => a.SourceLayer == EntitySourceLayer.Product).ToList();
            var modifiedAttrs = schema.Attributes.Where(a => a.IsAlteredInProduct).ToList();
            
            if (productAttrs.Any())
                result += $"\n🆕 New in Product Layer: {string.Join(", ", productAttrs.Select(a => a.Name))}\n";
            if (modifiedAttrs.Any())
                result += $"🔧 Modified in Product Layer: {string.Join(", ", modifiedAttrs.Select(a => a.Name))}\n";
            
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
                if (reference.SourceLayer == EntitySourceLayer.Product) result += " *product-layer*";
                if (!string.IsNullOrWhiteSpace(reference.Description))
                    result += $" ({reference.Description})";
                result += "\n";
            }
            
            // Show product layer enhancements
            var productRefs = schema.References.Where(r => r.SourceLayer == EntitySourceLayer.Product).ToList();
            if (productRefs.Any())
                result += $"\n🆕 New References in Product Layer: {string.Join(", ", productRefs.Select(r => r.Name))}\n";
            
            result += "\n";
        }

        // Indexes
        if (schema.Indexes.Count > 0)
        {
            result += "📊 Indexes:\n";
            foreach (var index in schema.Indexes)
            {
                result += $"  • {index.Name} ({(index.IsUnique ? "Unique" : "Non-unique")}) on {string.Join(", ", index.Fields)}";
                if (index.SourceLayer == EntitySourceLayer.Product) result += " *product-layer*";
                result += "\n";
            }
            result += "\n";
        }

        // Child entities - Let AI handle complex formatting
        try
        {
            var frameworkPath = _schemaOptions.GetFrameworkFilePath();
            var productPath = _schemaOptions.ProductEntityTypesFilePath;
            
            if (!string.IsNullOrWhiteSpace(frameworkPath) && _schemaService.ValidateEntityTypesFile(frameworkPath))
            {
                var childEntities = await _schemaService.GetChildEntitiesAsync(schema.Name, frameworkPath, productPath, cancellationToken);
                
                if (childEntities.Count > 0)
                {
                    result += "👶 Child Entities:\n";
                    foreach (var child in childEntities.OrderBy(c => c.Name))
                    {
                        result += $"  • {child.Name} ({child.ParentRelation})";
                        if (!child.Persistent) result += " *transient*";
                        if (child.IsMergedFromLayers) result += " *multi-layer*";
                        else if (child.SourceLayer == EntitySourceLayer.Product) result += " *product-layer*";
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