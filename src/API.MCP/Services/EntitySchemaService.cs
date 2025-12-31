using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using API.MCP.Models;

namespace API.MCP.Services;

/// <summary>
/// Implementation of entity schema service for parsing EntityTypes.xaml files with multi-layer support
/// </summary>
public class EntitySchemaService : IEntitySchemaService
{
    private readonly ILogger<EntitySchemaService> _logger;
    private readonly IMemoryCache _cache;
    private readonly SchemaOptions _schemaOptions;

    public EntitySchemaService(ILogger<EntitySchemaService> logger, IMemoryCache cache, IOptions<API.MCP.Configuration.SchemaOptions> schemaOptions)
    {
        _logger = logger;
        _cache = cache;
        _schemaOptions = schemaOptions.Value;
    }

    public async Task<EntitySchema?> GetEntitySchemaAsync(string entityName, string frameworkFilePath, string? productFilePath = null, CancellationToken cancellationToken = default)
    {
        string cacheKey = $"schema_{frameworkFilePath}_{productFilePath ?? "none"}_{entityName}";
        if (_schemaOptions.EnableCaching && _cache.TryGetValue(cacheKey, out EntitySchema? cachedSchema))
        {
            return cachedSchema;
        }
        
        try
        {
            // Validate framework file
            if (!ValidateEntityTypesFile(frameworkFilePath))
            {
                _logger.LogWarning("Framework EntityTypes.xaml file not found at path: {FilePath}", frameworkFilePath);
                return null;
            }

            // Get framework entity schema
            var frameworkSchema = await GetEntitySchemaFromFileAsync(entityName, frameworkFilePath, EntitySourceLayer.Framework, cancellationToken);
            
            // If no product file specified, return framework schema
            if (string.IsNullOrWhiteSpace(productFilePath))
            {
                if (_schemaOptions.EnableCaching && frameworkSchema != null)
                {
                    _cache.Set(cacheKey, frameworkSchema, TimeSpan.FromMinutes(_schemaOptions.CacheExpirationMinutes));
                }
                return frameworkSchema;
            }

            // Validate product file if provided
            if (!ValidateEntityTypesFile(productFilePath))
            {
                _logger.LogWarning("Product EntityTypes.xaml file not found at path: {FilePath}", productFilePath);
                // Return framework schema even if product file is missing
                if (_schemaOptions.EnableCaching && frameworkSchema != null)
                {
                    _cache.Set(cacheKey, frameworkSchema, TimeSpan.FromMinutes(_schemaOptions.CacheExpirationMinutes));
                }
                return frameworkSchema;
            }

            // Try to get entity from product layer
            var productSchema = await GetEntitySchemaFromFileAsync(entityName, productFilePath, EntitySourceLayer.Product, cancellationToken);
            
            // Try to get entity extensions from product layer
            var entityExtensions = await GetEntityExtensionsAsync(entityName, productFilePath, cancellationToken);

            // Merge schemas and extensions
            var mergedSchema = MergeEntitySchemas(frameworkSchema, productSchema, entityExtensions);
            
            if (_schemaOptions.EnableCaching && mergedSchema != null)
            {
                _cache.Set(cacheKey, mergedSchema, TimeSpan.FromMinutes(_schemaOptions.CacheExpirationMinutes));
            }
            
            return mergedSchema;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing entity schema for '{EntityName}' from framework: '{FrameworkPath}', product: '{ProductPath}'", 
                entityName, frameworkFilePath, productFilePath);
            return null;
        }
    }

    // Legacy method for backward compatibility
    [Obsolete("Use overload with frameworkFilePath and productFilePath for multi-layer support")]
    public async Task<EntitySchema?> GetEntitySchemaAsync(string entityName, string entityTypesFilePath, CancellationToken cancellationToken = default)
    {
        return await GetEntitySchemaAsync(entityName, entityTypesFilePath, null, cancellationToken);
    }

    public async Task<List<string>> GetAvailableEntitiesAsync(string frameworkFilePath, string? productFilePath = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var allEntities = new HashSet<string>();
            
            // Get entities from framework
            if (ValidateEntityTypesFile(frameworkFilePath))
            {
                var frameworkEntities = await GetEntityNamesFromFileAsync(frameworkFilePath, cancellationToken);
                foreach (var entity in frameworkEntities)
                {
                    allEntities.Add(entity);
                }
            }
            else
            {
                _logger.LogWarning("Framework EntityTypes.xaml file not found at path: {FilePath}", frameworkFilePath);
            }

            // Get entities from product layer if provided
            if (!string.IsNullOrWhiteSpace(productFilePath) && ValidateEntityTypesFile(productFilePath))
            {
                var productEntities = await GetEntityNamesFromFileAsync(productFilePath, cancellationToken);
                foreach (var entity in productEntities)
                {
                    allEntities.Add(entity);
                }
                
                // Also get entities that are being extended (but not redefined) in product layer
                var extendedEntities = await GetExtendedEntityNamesAsync(productFilePath, cancellationToken);
                foreach (var entity in extendedEntities)
                {
                    allEntities.Add(entity);
                }
            }
            else if (!string.IsNullOrWhiteSpace(productFilePath))
            {
                _logger.LogWarning("Product EntityTypes.xaml file not found at path: {FilePath}", productFilePath);
            }

            var result = allEntities.ToList();
            _logger.LogInformation("Found {EntityCount} total entities across all layers", result.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available entities from framework: '{FrameworkPath}', product: '{ProductPath}'", 
                frameworkFilePath, productFilePath);
            return new List<string>();
        }
    }

    // Legacy method for backward compatibility
    [Obsolete("Use overload with frameworkFilePath and productFilePath for multi-layer support")]
    public async Task<List<string>> GetAvailableEntitiesAsync(string entityTypesFilePath, CancellationToken cancellationToken = default)
    {
        return await GetAvailableEntitiesAsync(entityTypesFilePath, null, cancellationToken);
    }

    public async Task<string?> FindClosestEntityNameAsync(string inputEntityName, string frameworkFilePath, string? productFilePath = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var availableEntities = await GetAvailableEntitiesAsync(frameworkFilePath, productFilePath, cancellationToken);
            
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

    // Legacy method for backward compatibility
    [Obsolete("Use overload with frameworkFilePath and productFilePath for multi-layer support")]
    public async Task<string?> FindClosestEntityNameAsync(string inputEntityName, string entityTypesFilePath, CancellationToken cancellationToken = default)
    {
        return await FindClosestEntityNameAsync(inputEntityName, entityTypesFilePath, null, cancellationToken);
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

    public async Task<Dictionary<string, bool>> GetEntitiesWithPersistenceAsync(string frameworkFilePath, string? productFilePath = null, CancellationToken cancellationToken = default)
    {
        string cacheKey = $"entities_persistence_{frameworkFilePath}_{productFilePath ?? "none"}";
        if (_schemaOptions.EnableCaching && _cache.TryGetValue(cacheKey, out Dictionary<string, bool>? cachedPersistence))
        {
            return cachedPersistence ?? new Dictionary<string, bool>();
        }
        
        try
        {
            var entitiesWithPersistence = new Dictionary<string, bool>();
            
            // Get entities from framework
            if (ValidateEntityTypesFile(frameworkFilePath))
            {
                var frameworkEntities = await GetEntitiesWithPersistenceFromFileAsync(frameworkFilePath, cancellationToken);
                foreach (var entity in frameworkEntities)
                {
                    entitiesWithPersistence[entity.Key] = entity.Value;
                }
            }
            else
            {
                _logger.LogWarning("Framework EntityTypes.xaml file not found at path: {FilePath}", frameworkFilePath);
            }

            // Get/Override with entities from product layer if provided
            if (!string.IsNullOrWhiteSpace(productFilePath) && ValidateEntityTypesFile(productFilePath))
            {
                var productEntities = await GetEntitiesWithPersistenceFromFileAsync(productFilePath, cancellationToken);
                foreach (var entity in productEntities)
                {
                    entitiesWithPersistence[entity.Key] = entity.Value; // Product layer overrides framework
                }
            }
            else if (!string.IsNullOrWhiteSpace(productFilePath))
            {
                _logger.LogWarning("Product EntityTypes.xaml file not found at path: {FilePath}", productFilePath);
            }

            if (_schemaOptions.EnableCaching)
            {
                _cache.Set(cacheKey, entitiesWithPersistence, TimeSpan.FromMinutes(_schemaOptions.CacheExpirationMinutes));
            }

            _logger.LogInformation("Found {EntityCount} entities with persistence info across all layers", entitiesWithPersistence.Count);
            return entitiesWithPersistence;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting entities with persistence info from framework: '{FrameworkPath}', product: '{ProductPath}'", 
                frameworkFilePath, productFilePath);
            return new Dictionary<string, bool>();
        }
    }

    // Legacy method for backward compatibility
    [Obsolete("Use overload with frameworkFilePath and productFilePath for multi-layer support")]
    public async Task<Dictionary<string, bool>> GetEntitiesWithPersistenceAsync(string entityTypesFilePath, CancellationToken cancellationToken = default)
    {
        return await GetEntitiesWithPersistenceAsync(entityTypesFilePath, null, cancellationToken);
    }

    public async Task<List<EntitySchema>> GetChildEntitiesAsync(string parentEntityName, string frameworkFilePath, string? productFilePath = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var childEntities = new Dictionary<string, EntitySchema>();
            
            // Get child entities from framework
            if (ValidateEntityTypesFile(frameworkFilePath))
            {
                var frameworkChildren = await GetChildEntitiesFromFileAsync(parentEntityName, frameworkFilePath, EntitySourceLayer.Framework, cancellationToken);
                foreach (var child in frameworkChildren)
                {
                    childEntities[child.Name] = child;
                }
            }
            else
            {
                _logger.LogWarning("Framework EntityTypes.xaml file not found at path: {FilePath}", frameworkFilePath);
            }

            // Get/Override with child entities from product layer if provided
            if (!string.IsNullOrWhiteSpace(productFilePath) && ValidateEntityTypesFile(productFilePath))
            {
                var productChildren = await GetChildEntitiesFromFileAsync(parentEntityName, productFilePath, EntitySourceLayer.Product, cancellationToken);
                foreach (var child in productChildren)
                {
                    // If child exists in framework, merge; otherwise add as new
                    if (childEntities.ContainsKey(child.Name))
                    {
                        var frameworkChild = childEntities[child.Name];
                        var extensions = await GetEntityExtensionsAsync(child.Name, productFilePath, cancellationToken);
                        var mergedChild = MergeEntitySchemas(frameworkChild, child, extensions);
                        if (mergedChild != null)
                        {
                            childEntities[child.Name] = mergedChild;
                        }
                    }
                    else
                    {
                        childEntities[child.Name] = child;
                    }
                }
            }
            else if (!string.IsNullOrWhiteSpace(productFilePath))
            {
                _logger.LogWarning("Product EntityTypes.xaml file not found at path: {FilePath}", productFilePath);
            }

            var result = childEntities.Values.ToList();
            _logger.LogInformation("Found {ChildCount} child entities for parent '{ParentEntityName}' across all layers", result.Count, parentEntityName);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting child entities for parent '{ParentEntityName}' from framework: '{FrameworkPath}', product: '{ProductPath}'", 
                parentEntityName, frameworkFilePath, productFilePath);
            return new List<EntitySchema>();
        }
    }

    // Legacy method for backward compatibility
    [Obsolete("Use overload with frameworkFilePath and productFilePath for multi-layer support")]
    public async Task<List<EntitySchema>> GetChildEntitiesAsync(string parentEntityName, string entityTypesFilePath, CancellationToken cancellationToken = default)
    {
        return await GetChildEntitiesAsync(parentEntityName, entityTypesFilePath, null, cancellationToken);
    }

    /// <summary>
    /// Gets entity schema from a single file with specified source layer
    /// </summary>
    private async Task<EntitySchema?> GetEntitySchemaFromFileAsync(string entityName, string filePath, EntitySourceLayer sourceLayer, CancellationToken cancellationToken)
    {
        var xmlContent = await File.ReadAllTextAsync(filePath, cancellationToken);
        var document = XDocument.Parse(xmlContent);

        var defaultNamespace = document.Root?.GetDefaultNamespace() ?? XNamespace.None;
        
        var entityElement = document.Descendants(defaultNamespace + "Entity")
            .FirstOrDefault(e => e.Attribute("Name")?.Value.Equals(entityName, StringComparison.OrdinalIgnoreCase) == true);

        if (entityElement == null)
        {
            _logger.LogDebug("Entity '{EntityName}' not found in {Layer} layer file: {FilePath}", entityName, sourceLayer, filePath);
            return null;
        }

        var schema = ParseEntitySchema(entityElement, sourceLayer);
        _logger.LogDebug("Found entity '{EntityName}' in {Layer} layer", entityName, sourceLayer);
        return schema;
    }

    /// <summary>
    /// Gets entity extensions from product layer
    /// </summary>
    private async Task<List<EntityExtension>> GetEntityExtensionsAsync(string entityName, string productFilePath, CancellationToken cancellationToken)
    {
        var extensions = new List<EntityExtension>();
        
        try
        {
            var xmlContent = await File.ReadAllTextAsync(productFilePath, cancellationToken);
            var document = XDocument.Parse(xmlContent);
            var defaultNamespace = document.Root?.GetDefaultNamespace() ?? XNamespace.None;
            
            var extensionElements = document.Descendants(defaultNamespace + "EntityExtension")
                .Where(e => e.Attribute("Entity")?.Value?.Equals(entityName, StringComparison.OrdinalIgnoreCase) == true);

            foreach (var extensionElement in extensionElements)
            {
                var extension = ParseEntityExtension(extensionElement, defaultNamespace);
                extensions.Add(extension);
            }
            
            if (extensions.Any())
            {
                _logger.LogDebug("Found {ExtensionCount} extensions for entity '{EntityName}' in product layer", extensions.Count, entityName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error parsing entity extensions for '{EntityName}' from product file '{FilePath}'", entityName, productFilePath);
        }

        return extensions;
    }

    /// <summary>
    /// Merges framework schema, product schema, and extensions into final schema
    /// </summary>
    private EntitySchema? MergeEntitySchemas(EntitySchema? frameworkSchema, EntitySchema? productSchema, List<EntityExtension> extensions)
    {
        // If no framework and no product, return null
        if (frameworkSchema == null && productSchema == null)
        {
            return null;
        }

        // If only product exists, start with product
        var mergedSchema = productSchema != null 
            ? CloneEntitySchema(productSchema)
            : CloneEntitySchema(frameworkSchema!);

        // If both exist, product overrides framework for main properties
        if (frameworkSchema != null && productSchema != null)
        {
            // Keep framework as base, but product overrides main properties
            mergedSchema = CloneEntitySchema(frameworkSchema);
            
            // Override with product properties
            if (!string.IsNullOrWhiteSpace(productSchema.Description))
                mergedSchema.Description = productSchema.Description;
            if (!string.IsNullOrWhiteSpace(productSchema.Label))
                mergedSchema.Label = productSchema.Label;
            
            mergedSchema.Persistent = productSchema.Persistent;
            mergedSchema.Securable = productSchema.Securable;
            
            if (!string.IsNullOrWhiteSpace(productSchema.ParentEntity))
                mergedSchema.ParentEntity = productSchema.ParentEntity;
            if (!string.IsNullOrWhiteSpace(productSchema.ParentRelation))
                mergedSchema.ParentRelation = productSchema.ParentRelation;

            // Merge attributes - product overrides framework
            foreach (var productAttr in productSchema.Attributes)
            {
                var existingAttr = mergedSchema.Attributes.FirstOrDefault(a => 
                    a.Name.Equals(productAttr.Name, StringComparison.OrdinalIgnoreCase));
                
                if (existingAttr != null)
                {
                    // Replace framework attribute with product version
                    mergedSchema.Attributes.Remove(existingAttr);
                    productAttr.IsAlteredInProduct = true;
                }
                mergedSchema.Attributes.Add(productAttr);
            }

            // Merge references - product adds to framework
            foreach (var productRef in productSchema.References)
            {
                var existingRef = mergedSchema.References.FirstOrDefault(r => 
                    r.Name.Equals(productRef.Name, StringComparison.OrdinalIgnoreCase));
                
                if (existingRef != null)
                {
                    mergedSchema.References.Remove(existingRef);
                }
                mergedSchema.References.Add(productRef);
            }

            // Merge indexes - product adds to framework
            foreach (var productIndex in productSchema.Indexes)
            {
                var existingIndex = mergedSchema.Indexes.FirstOrDefault(i => 
                    i.Name.Equals(productIndex.Name, StringComparison.OrdinalIgnoreCase));
                
                if (existingIndex != null)
                {
                    mergedSchema.Indexes.Remove(existingIndex);
                }
                mergedSchema.Indexes.Add(productIndex);
            }

            mergedSchema.IsMergedFromLayers = true;
            mergedSchema.SourceLayer = EntitySourceLayer.Both;
        }

        // Apply extensions
        foreach (var extension in extensions)
        {
            ApplyEntityExtension(mergedSchema, extension);
        }

        return mergedSchema;
    }

    /// <summary>
    /// Applies an entity extension to an existing schema
    /// </summary>
    private void ApplyEntityExtension(EntitySchema schema, EntityExtension extension)
    {
        // Add new attributes
        foreach (var newAttr in extension.AdditionalAttributes)
        {
            // Check if attribute already exists
            var existingAttr = schema.Attributes.FirstOrDefault(a => 
                a.Name.Equals(newAttr.Name, StringComparison.OrdinalIgnoreCase));
            
            if (existingAttr == null)
            {
                newAttr.SourceLayer = EntitySourceLayer.Product;
                schema.Attributes.Add(newAttr);
            }
            else
            {
                _logger.LogWarning("Extension attribute '{AttributeName}' already exists in entity '{EntityName}', skipping", 
                    newAttr.Name, schema.Name);
            }
        }

        // Add new references
        foreach (var newRef in extension.AdditionalReferences)
        {
            var existingRef = schema.References.FirstOrDefault(r => 
                r.Name.Equals(newRef.Name, StringComparison.OrdinalIgnoreCase));
            
            if (existingRef == null)
            {
                newRef.SourceLayer = EntitySourceLayer.Product;
                schema.References.Add(newRef);
            }
            else
            {
                _logger.LogWarning("Extension reference '{ReferenceName}' already exists in entity '{EntityName}', skipping", 
                    newRef.Name, schema.Name);
            }
        }

        // Apply attribute alterations
        foreach (var alteration in extension.AttributeAlterations)
        {
            var existingAttr = schema.Attributes.FirstOrDefault(a => 
                a.Name.Equals(alteration.AttributeName, StringComparison.OrdinalIgnoreCase));
            
            if (existingAttr != null)
            {
                // Apply alterations
                if (!string.IsNullOrWhiteSpace(alteration.NewType))
                    existingAttr.Type = alteration.NewType;
                if (!string.IsNullOrWhiteSpace(alteration.NewLabel))
                    existingAttr.Label = alteration.NewLabel;
                if (!string.IsNullOrWhiteSpace(alteration.NewDescription))
                    existingAttr.Description = alteration.NewDescription;
                if (alteration.NewNullable.HasValue)
                    existingAttr.Nullable = alteration.NewNullable.Value;
                if (alteration.NewPersistent.HasValue)
                    existingAttr.Persistent = alteration.NewPersistent.Value;
                if (alteration.NewNaturalIdentity.HasValue)
                    existingAttr.NaturalIdentity = alteration.NewNaturalIdentity.Value;
                if (alteration.NewQueryUnchangedValue.HasValue)
                    existingAttr.QueryUnchangedValue = alteration.NewQueryUnchangedValue.Value;
                
                existingAttr.IsAlteredInProduct = true;
                
                _logger.LogDebug("Applied alteration to attribute '{AttributeName}' in entity '{EntityName}'", 
                    alteration.AttributeName, schema.Name);
            }
            else
            {
                _logger.LogWarning("Attribute '{AttributeName}' not found for alteration in entity '{EntityName}'", 
                    alteration.AttributeName, schema.Name);
            }
        }

        schema.IsMergedFromLayers = true;
        if (schema.SourceLayer == EntitySourceLayer.Framework)
        {
            schema.SourceLayer = EntitySourceLayer.Both;
        }
    }

    /// <summary>
    /// Creates a deep clone of an entity schema
    /// </summary>
    private EntitySchema CloneEntitySchema(EntitySchema original)
    {
        return new EntitySchema
        {
            Name = original.Name,
            Description = original.Description,
            Label = original.Label,
            Persistent = original.Persistent,
            Securable = original.Securable,
            ParentEntity = original.ParentEntity,
            ParentRelation = original.ParentRelation,
            Attributes = original.Attributes.Select(CloneEntityAttribute).ToList(),
            References = original.References.Select(CloneEntityReference).ToList(),
            Indexes = original.Indexes.Select(CloneEntityIndex).ToList(),
            IsMergedFromLayers = original.IsMergedFromLayers,
            SourceLayer = original.SourceLayer
        };
    }

    /// <summary>
    /// Creates a deep clone of an entity attribute
    /// </summary>
    private EntityAttribute CloneEntityAttribute(EntityAttribute original)
    {
        return new EntityAttribute
        {
            Name = original.Name,
            Type = original.Type,
            Label = original.Label,
            Description = original.Description,
            Nullable = original.Nullable,
            Persistent = original.Persistent,
            NaturalIdentity = original.NaturalIdentity,
            QueryUnchangedValue = original.QueryUnchangedValue,
            SourceLayer = original.SourceLayer,
            IsAlteredInProduct = original.IsAlteredInProduct
        };
    }

    /// <summary>
    /// Creates a deep clone of an entity reference
    /// </summary>
    private EntityReference CloneEntityReference(EntityReference original)
    {
        return new EntityReference
        {
            Name = original.Name,
            RefersTo = original.RefersTo,
            Nullable = original.Nullable,
            Description = original.Description,
            SourceLayer = original.SourceLayer
        };
    }

    /// <summary>
    /// Creates a deep clone of an entity index
    /// </summary>
    private EntityIndex CloneEntityIndex(EntityIndex original)
    {
        return new EntityIndex
        {
            Name = original.Name,
            IsUnique = original.IsUnique,
            Fields = new List<string>(original.Fields),
            CoveredFields = new List<string>(original.CoveredFields),
            SourceLayer = original.SourceLayer
        };
    }

    /// <summary>
    /// Gets entity names from a single file
    /// </summary>
    private async Task<List<string>> GetEntityNamesFromFileAsync(string filePath, CancellationToken cancellationToken)
    {
        var xmlContent = await File.ReadAllTextAsync(filePath, cancellationToken);
        var document = XDocument.Parse(xmlContent);
        var defaultNamespace = document.Root?.GetDefaultNamespace() ?? XNamespace.None;

        return document.Descendants(defaultNamespace + "Entity")
            .Select(e => e.Attribute("Name")?.Value)
            .Where(name => !string.IsNullOrEmpty(name))
            .Cast<string>()
            .ToList();
    }

    /// <summary>
    /// Gets entity names that are being extended in the product layer
    /// </summary>
    private async Task<List<string>> GetExtendedEntityNamesAsync(string filePath, CancellationToken cancellationToken)
    {
        var xmlContent = await File.ReadAllTextAsync(filePath, cancellationToken);
        var document = XDocument.Parse(xmlContent);
        var defaultNamespace = document.Root?.GetDefaultNamespace() ?? XNamespace.None;

        return document.Descendants(defaultNamespace + "EntityExtension")
            .Select(e => e.Attribute("Entity")?.Value)
            .Where(name => !string.IsNullOrEmpty(name))
            .Cast<string>()
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// Gets entities with persistence from a single file
    /// </summary>
    private async Task<Dictionary<string, bool>> GetEntitiesWithPersistenceFromFileAsync(string filePath, CancellationToken cancellationToken)
    {
        var xmlContent = await File.ReadAllTextAsync(filePath, cancellationToken);
        var document = XDocument.Parse(xmlContent);
        var defaultNamespace = document.Root?.GetDefaultNamespace() ?? XNamespace.None;

        return document.Descendants(defaultNamespace + "Entity")
            .Where(e => e.Attribute("Name")?.Value != null)
            .ToDictionary(
                e => e.Attribute("Name")!.Value,
                e => bool.Parse(e.Attribute("Persistent")?.Value ?? "true")
            );
    }

    /// <summary>
    /// Gets child entities from a single file
    /// </summary>
    private async Task<List<EntitySchema>> GetChildEntitiesFromFileAsync(string parentEntityName, string filePath, EntitySourceLayer sourceLayer, CancellationToken cancellationToken)
    {
        var xmlContent = await File.ReadAllTextAsync(filePath, cancellationToken);
        var document = XDocument.Parse(xmlContent);
        var defaultNamespace = document.Root?.GetDefaultNamespace() ?? XNamespace.None;

        var childEntities = new List<EntitySchema>();
        
        var childElements = document.Descendants(defaultNamespace + "Entity")
            .Where(e => e.Attribute("ParentEntity")?.Value?.Equals(parentEntityName, StringComparison.OrdinalIgnoreCase) == true);

        foreach (var childElement in childElements)
        {
            var childSchema = ParseEntitySchema(childElement, sourceLayer);
            childEntities.Add(childSchema);
        }

        return childEntities;
    }

    /// <summary>
    /// Parses an EntityExtension element
    /// </summary>
    private EntityExtension ParseEntityExtension(XElement extensionElement, XNamespace entityNamespace)
    {
        var extension = new EntityExtension
        {
            EntityName = extensionElement.Attribute("Entity")?.Value ?? string.Empty,
            SourceLayer = EntitySourceLayer.Product
        };

        // Parse additional attributes
        var attributesElement = extensionElement.Element(entityNamespace + "EntityExtension.Attributes");
        if (attributesElement != null)
        {
            extension.AdditionalAttributes = attributesElement.Elements(entityNamespace + "Attribute")
                .Select(e => ParseEntityAttribute(e, EntitySourceLayer.Product))
                .ToList();
        }

        // Parse additional references
        var referencesElement = extensionElement.Element(entityNamespace + "EntityExtension.References");
        if (referencesElement != null)
        {
            extension.AdditionalReferences = referencesElement.Elements(entityNamespace + "Reference")
                .Select(e => ParseEntityReference(e, EntitySourceLayer.Product))
                .ToList();
        }

        // Parse attribute alterations
        var alterationsElement = extensionElement.Element(entityNamespace + "EntityExtension.AttributeAlterations");
        if (alterationsElement != null)
        {
            extension.AttributeAlterations = alterationsElement.Elements(entityNamespace + "AttributeAlteration")
                .Select(ParseAttributeAlteration)
                .ToList();
        }

        return extension;
    }

    /// <summary>
    /// Parses an AttributeAlteration element
    /// </summary>
    private AttributeAlteration ParseAttributeAlteration(XElement alterationElement)
    {
        return new AttributeAlteration
        {
            AttributeName = alterationElement.Attribute("Attribute")?.Value ?? string.Empty,
            NewType = alterationElement.Attribute("Type")?.Value,
            NewLabel = alterationElement.Attribute("Label")?.Value,
            NewDescription = alterationElement.Attribute("Description")?.Value,
            NewNullable = GetOptionalBoolAttribute(alterationElement, "Nullable"),
            NewPersistent = GetOptionalBoolAttribute(alterationElement, "Persistent"),
            NewNaturalIdentity = GetOptionalBoolAttribute(alterationElement, "NaturalIdentity"),
            NewQueryUnchangedValue = GetOptionalBoolAttribute(alterationElement, "QueryUnchangedValue")
        };
    }

    /// <summary>
    /// Helper method to parse optional boolean attributes
    /// </summary>
    private bool? GetOptionalBoolAttribute(XElement element, string attributeName)
    {
        var value = element.Attribute(attributeName)?.Value;
        return string.IsNullOrWhiteSpace(value) ? null : bool.Parse(value);
    }

    private EntitySchema ParseEntitySchema(XElement entityElement, EntitySourceLayer sourceLayer = EntitySourceLayer.Framework)
    {
        // Get the namespace from the entity element
        var entityNamespace = entityElement.GetDefaultNamespace();
        
        var schema = new EntitySchema
        {
            Name = entityElement.Attribute("Name")?.Value ?? string.Empty,
            Description = entityElement.Attribute("Description")?.Value,
            Label = entityElement.Attribute("Label")?.Value,
            Persistent = bool.Parse(entityElement.Attribute("Persistent")?.Value ?? "true"),
            Securable = bool.Parse(entityElement.Attribute("Securable")?.Value ?? "true"),
            ParentEntity = entityElement.Attribute("ParentEntity")?.Value,
            ParentRelation = entityElement.Attribute("ParentRelation")?.Value,
            SourceLayer = sourceLayer
        };

        // Parse attributes
        var attributesElement = entityElement.Element(entityNamespace + "Entity.Attributes");
        if (attributesElement != null)
        {
            schema.Attributes = attributesElement.Elements(entityNamespace + "Attribute")
                .Select(e => ParseEntityAttribute(e, sourceLayer))
                .ToList();
        }

        // Parse references
        var referencesElement = entityElement.Element(entityNamespace + "Entity.References");
        if (referencesElement != null)
        {
            schema.References = referencesElement.Elements(entityNamespace + "Reference")
                .Select(e => ParseEntityReference(e, sourceLayer))
                .ToList();
        }

        // Parse indexes
        var indexesElement = entityElement.Element(entityNamespace + "Entity.Indexes");
        if (indexesElement != null)
        {
            schema.Indexes = indexesElement.Elements(entityNamespace + "UniqueIndex")
                .Select(e => ParseEntityIndex(e, sourceLayer))
                .ToList();
        }

        _logger.LogDebug("Parsed entity schema for '{EntityName}' from {SourceLayer} layer with {AttributeCount} attributes, {ReferenceCount} references, and {IndexCount} indexes",
            schema.Name, sourceLayer, schema.Attributes.Count, schema.References.Count, schema.Indexes.Count);

        return schema;
    }

    private EntityAttribute ParseEntityAttribute(XElement attributeElement, EntitySourceLayer sourceLayer = EntitySourceLayer.Framework)
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
            QueryUnchangedValue = bool.Parse(attributeElement.Attribute("QueryUnchangedValue")?.Value ?? "false"),
            SourceLayer = sourceLayer
        };
    }

    private EntityReference ParseEntityReference(XElement referenceElement, EntitySourceLayer sourceLayer = EntitySourceLayer.Framework)
    {
        return new EntityReference
        {
            Name = referenceElement.Attribute("Name")?.Value ?? string.Empty,
            RefersTo = referenceElement.Attribute("RefersTo")?.Value ?? string.Empty,
            Nullable = bool.Parse(referenceElement.Attribute("Nullable")?.Value ?? "true"),
            Description = referenceElement.Attribute("Description")?.Value,
            SourceLayer = sourceLayer
        };
    }

    private EntityIndex ParseEntityIndex(XElement indexElement, EntitySourceLayer sourceLayer = EntitySourceLayer.Framework)
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
                .ToList(),
            SourceLayer = sourceLayer
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