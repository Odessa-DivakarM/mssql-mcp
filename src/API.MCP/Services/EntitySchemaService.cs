using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using API.MCP.Models;

namespace API.MCP.Services;

/// <summary>
/// Implementation of entity schema service for parsing EntityTypes.xaml files
/// </summary>
public class EntitySchemaService : IEntitySchemaService
{
    private readonly ILogger<EntitySchemaService> _logger;

    public EntitySchemaService(ILogger<EntitySchemaService> logger)
    {
        _logger = logger;
    }

    public async Task<EntitySchema?> GetEntitySchemaAsync(string entityName, string entityTypesFilePath, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!ValidateEntityTypesFile(entityTypesFilePath))
            {
                _logger.LogWarning("EntityTypes.xaml file not found at path: {FilePath}", entityTypesFilePath);
                return null;
            }

            var xmlContent = await File.ReadAllTextAsync(entityTypesFilePath, cancellationToken);
            var document = XDocument.Parse(xmlContent);

            // Handle namespaces - get the default namespace from the root element if it exists
            var defaultNamespace = document.Root?.GetDefaultNamespace() ?? XNamespace.None;
            
            var entityElement = document.Descendants(defaultNamespace + "Entity")
                .FirstOrDefault(e => e.Attribute("Name")?.Value.Equals(entityName, StringComparison.OrdinalIgnoreCase) == true);

            if (entityElement == null)
            {
                _logger.LogInformation("Entity '{EntityName}' not found in EntityTypes.xaml", entityName);
                return null;
            }

            return ParseEntitySchema(entityElement);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing entity schema for '{EntityName}' from file '{FilePath}'", entityName, entityTypesFilePath);
            return null;
        }
    }

    public async Task<List<string>> GetAvailableEntitiesAsync(string entityTypesFilePath, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!ValidateEntityTypesFile(entityTypesFilePath))
            {
                _logger.LogWarning("EntityTypes.xaml file not found at path: {FilePath}", entityTypesFilePath);
                return new List<string>();
            }

            var xmlContent = await File.ReadAllTextAsync(entityTypesFilePath, cancellationToken);
            var document = XDocument.Parse(xmlContent);

            // Handle namespaces - get the default namespace from the root element if it exists
            var defaultNamespace = document.Root?.GetDefaultNamespace() ?? XNamespace.None;

            var entities = document.Descendants(defaultNamespace + "Entity")
                .Select(e => e.Attribute("Name")?.Value)
                .Where(name => !string.IsNullOrEmpty(name))
                .Cast<string>()
                .ToList();

            _logger.LogInformation("Found {EntityCount} entities in EntityTypes.xaml", entities.Count);
            return entities;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available entities from file '{FilePath}'", entityTypesFilePath);
            return new List<string>();
        }
    }

    public async Task<string?> FindClosestEntityNameAsync(string inputEntityName, string entityTypesFilePath, CancellationToken cancellationToken = default)
    {
        try
        {
            var availableEntities = await GetAvailableEntitiesAsync(entityTypesFilePath, cancellationToken);
            
            if (availableEntities.Count == 0)
            {
                return null;
            }

            // First try exact match (case insensitive)
            var exactMatch = availableEntities.FirstOrDefault(e => 
                e.Equals(inputEntityName, StringComparison.OrdinalIgnoreCase));
            
            if (exactMatch != null)
            {
                return exactMatch;
            }

            // Use Levenshtein distance for typo correction only
            var bestMatch = availableEntities
                .Select(entity => new { Entity = entity, Distance = CalculateLevenshteinDistance(inputEntityName.ToLowerInvariant(), entity.ToLowerInvariant()) })
                .Where(x => x.Distance <= 2) // Only allow minor typos (max 2 character differences)
                .OrderBy(x => x.Distance)
                .FirstOrDefault();

            if (bestMatch != null)
            {
                _logger.LogInformation("Found close match '{ClosestMatch}' for input '{InputEntityName}' with distance {Distance}", 
                    bestMatch.Entity, inputEntityName, bestMatch.Distance);
                return bestMatch.Entity;
            }

            _logger.LogInformation("No exact or close match found for entity '{InputEntityName}'", inputEntityName);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding closest entity name for '{InputEntityName}'", inputEntityName);
            return null;
        }
    }

    public bool ValidateEntityTypesFile(string entityTypesFilePath)
    {
        try
        {
            return File.Exists(entityTypesFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating EntityTypes.xaml file at path: {FilePath}", entityTypesFilePath);
            return false;
        }
    }

    public async Task<Dictionary<string, bool>> GetEntitiesWithPersistenceAsync(string entityTypesFilePath, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!ValidateEntityTypesFile(entityTypesFilePath))
            {
                _logger.LogWarning("EntityTypes.xaml file not found at path: {FilePath}", entityTypesFilePath);
                return new Dictionary<string, bool>();
            }

            var xmlContent = await File.ReadAllTextAsync(entityTypesFilePath, cancellationToken);
            var document = XDocument.Parse(xmlContent);

            // Handle namespaces - get the default namespace from the root element if it exists
            var defaultNamespace = document.Root?.GetDefaultNamespace() ?? XNamespace.None;

            var entitiesWithPersistence = document.Descendants(defaultNamespace + "Entity")
                .Where(e => e.Attribute("Name")?.Value != null)
                .ToDictionary(
                    e => e.Attribute("Name")!.Value,
                    e => bool.Parse(e.Attribute("Persistent")?.Value ?? "true")
                );

            _logger.LogInformation("Found {EntityCount} entities in EntityTypes.xaml with persistence info", entitiesWithPersistence.Count);
            return entitiesWithPersistence;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting entities with persistence info from file '{FilePath}'", entityTypesFilePath);
            return new Dictionary<string, bool>();
        }
    }

    private EntitySchema ParseEntitySchema(XElement entityElement)
    {
        // Get the namespace from the entity element
        var entityNamespace = entityElement.GetDefaultNamespace();
        
        var schema = new EntitySchema
        {
            Name = entityElement.Attribute("Name")?.Value ?? string.Empty,
            Description = entityElement.Attribute("Description")?.Value,
            Label = entityElement.Attribute("Label")?.Value,
            Persistent = bool.Parse(entityElement.Attribute("Persistent")?.Value ?? "true"),
            Securable = bool.Parse(entityElement.Attribute("Securable")?.Value ?? "true")
        };

        // Parse attributes
        var attributesElement = entityElement.Element(entityNamespace + "Entity.Attributes");
        if (attributesElement != null)
        {
            schema.Attributes = attributesElement.Elements(entityNamespace + "Attribute")
                .Select(ParseEntityAttribute)
                .ToList();
        }

        // Parse references
        var referencesElement = entityElement.Element(entityNamespace + "Entity.References");
        if (referencesElement != null)
        {
            schema.References = referencesElement.Elements(entityNamespace + "Reference")
                .Select(ParseEntityReference)
                .ToList();
        }

        // Parse indexes
        var indexesElement = entityElement.Element(entityNamespace + "Entity.Indexes");
        if (indexesElement != null)
        {
            schema.Indexes = indexesElement.Elements(entityNamespace + "UniqueIndex")
                .Select(ParseEntityIndex)
                .ToList();
        }

        _logger.LogDebug("Parsed entity schema for '{EntityName}' with {AttributeCount} attributes, {ReferenceCount} references, and {IndexCount} indexes",
            schema.Name, schema.Attributes.Count, schema.References.Count, schema.Indexes.Count);

        return schema;
    }

    private EntityAttribute ParseEntityAttribute(XElement attributeElement)
    {
        return new EntityAttribute
        {
            Name = attributeElement.Attribute("Name")?.Value ?? string.Empty,
            Type = attributeElement.Attribute("Type")?.Value ?? string.Empty,
            Label = attributeElement.Attribute("Label")?.Value,
            Description = attributeElement.Attribute("Description")?.Value,
            Nullable = bool.Parse(attributeElement.Attribute("Nullable")?.Value ?? "true"),
            Persistent = bool.Parse(attributeElement.Attribute("Persistent")?.Value ?? "true"),
            NaturalIdentity = bool.Parse(attributeElement.Attribute("NaturalIdentity")?.Value ?? "false"),
            QueryUnchangedValue = bool.Parse(attributeElement.Attribute("QueryUnchangedValue")?.Value ?? "false")
        };
    }

    private EntityReference ParseEntityReference(XElement referenceElement)
    {
        return new EntityReference
        {
            Name = referenceElement.Attribute("Name")?.Value ?? string.Empty,
            RefersTo = referenceElement.Attribute("RefersTo")?.Value ?? string.Empty,
            Nullable = bool.Parse(referenceElement.Attribute("Nullable")?.Value ?? "true"),
            Description = referenceElement.Attribute("Description")?.Value
        };
    }

    private EntityIndex ParseEntityIndex(XElement indexElement)
    {
        var fieldsStr = indexElement.Attribute("Fields")?.Value ?? string.Empty;
        var coveredFieldsStr = indexElement.Attribute("CoveredFields")?.Value ?? string.Empty;

        return new EntityIndex
        {
            Name = indexElement.Attribute("Name")?.Value ?? string.Empty,
            IsUnique = true, // All parsed indexes are unique as we're only parsing "UniqueIndex" elements
            Fields = fieldsStr.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(f => f.Trim())
                .ToList(),
            CoveredFields = coveredFieldsStr.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(f => f.Trim())
                .ToList()
        };
    }

    private static int CalculateLevenshteinDistance(string source, string target)
    {
        if (string.IsNullOrEmpty(source))
            return string.IsNullOrEmpty(target) ? 0 : target.Length;

        if (string.IsNullOrEmpty(target))
            return source.Length;

        var matrix = new int[source.Length + 1, target.Length + 1];

        // Initialize first row and column
        for (int i = 0; i <= source.Length; i++)
            matrix[i, 0] = i;

        for (int j = 0; j <= target.Length; j++)
            matrix[0, j] = j;

        // Calculate distances
        for (int i = 1; i <= source.Length; i++)
        {
            for (int j = 1; j <= target.Length; j++)
            {
                int cost = source[i - 1] == target[j - 1] ? 0 : 1;

                matrix[i, j] = Math.Min(
                    Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                    matrix[i - 1, j - 1] + cost);
            }
        }

        return matrix[source.Length, target.Length];
    }
}