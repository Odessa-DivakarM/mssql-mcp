namespace API.MCP.Models;

/// <summary>
/// Represents the schema of an entity with its attributes and metadata
/// </summary>
public class EntitySchema
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Label { get; set; }
    public bool Persistent { get; set; } = true;
    public bool Securable { get; set; } = true;
    public string? ParentEntity { get; set; }
    public string? ParentRelation { get; set; }
    public List<EntityAttribute> Attributes { get; set; } = new();
    public List<EntityReference> References { get; set; } = new();
    public List<EntityIndex> Indexes { get; set; } = new();
    
    /// <summary>
    /// Indicates if this entity schema was merged from multiple layers (Framework + Product)
    /// </summary>
    public bool IsMergedFromLayers { get; set; } = false;
    
    /// <summary>
    /// The layer(s) this entity was sourced from (Framework, Product, or Both)
    /// </summary>
    public EntitySourceLayer SourceLayer { get; set; } = EntitySourceLayer.Framework;
    
    /// <summary>
    /// Determines if this entity is a child entity (has a parent)
    /// </summary>
    public bool IsChildEntity => !string.IsNullOrEmpty(ParentEntity);
    
    /// <summary>
    /// Gets the plural form of the entity name for hierarchical selection
    /// Note: This is a simple fallback. AI should handle complex pluralization.
    /// </summary>
    public string PluralName => Name.EndsWith("s", StringComparison.OrdinalIgnoreCase) ? Name : Name + "s";
}

/// <summary>
/// Represents the source layer(s) of an entity
/// </summary>
public enum EntitySourceLayer
{
    Framework = 1,
    Product = 2,
    Both = 3
}

/// <summary>
/// Represents an extension to an existing entity in the product layer
/// </summary>
public class EntityExtension
{
    public string EntityName { get; set; } = string.Empty;
    public List<EntityAttribute> AdditionalAttributes { get; set; } = new();
    public List<EntityReference> AdditionalReferences { get; set; } = new();
    public List<AttributeAlteration> AttributeAlterations { get; set; } = new();
    
    /// <summary>
    /// The layer this extension is from (typically Product)
    /// </summary>
    public EntitySourceLayer SourceLayer { get; set; } = EntitySourceLayer.Product;
}

/// <summary>
/// Represents an alteration to an existing attribute in an entity
/// </summary>
public class AttributeAlteration
{
    public string AttributeName { get; set; } = string.Empty;
    public string? NewType { get; set; }
    public string? NewLabel { get; set; }
    public string? NewDescription { get; set; }
    public bool? NewNullable { get; set; }
    public bool? NewPersistent { get; set; }
    public bool? NewNaturalIdentity { get; set; }
    public bool? NewQueryUnchangedValue { get; set; }
}

/// <summary>
/// Represents an attribute/property of an entity
/// </summary>
public class EntityAttribute
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Label { get; set; }
    public string? Description { get; set; }
    public bool Nullable { get; set; } = true;
    public bool Persistent { get; set; } = true;
    public bool NaturalIdentity { get; set; } = false;
    public bool QueryUnchangedValue { get; set; } = false;
    
    /// <summary>
    /// The layer this attribute was sourced from (Framework, Product, or modified in Product)
    /// </summary>
    public EntitySourceLayer SourceLayer { get; set; } = EntitySourceLayer.Framework;
    
    /// <summary>
    /// Indicates if this attribute was modified from the framework definition in the product layer
    /// </summary>
    public bool IsAlteredInProduct { get; set; } = false;
    
    /// <summary>
    /// Determines if the attribute is a string type based on the Type property
    /// </summary>
    public bool IsStringType => Type.Contains("Text") || Type.Contains("Name") || Type.Contains("Description") || 
                               Type.Equals("IPAddressType", StringComparison.OrdinalIgnoreCase);
    
    /// <summary>
    /// Determines if the attribute is a numeric type based on the Type property
    /// </summary>
    public bool IsNumericType => Type.Equals("Int", StringComparison.OrdinalIgnoreCase) || 
                                Type.Equals("Long", StringComparison.OrdinalIgnoreCase) || 
                                Type.Equals("Decimal", StringComparison.OrdinalIgnoreCase);
    
    /// <summary>
    /// Determines if the attribute is a boolean type based on the Type property
    /// </summary>
    public bool IsBooleanType => Type.Equals("Boolean", StringComparison.OrdinalIgnoreCase);
    
    /// <summary>
    /// Determines if the attribute is a date/time type based on the Type property
    /// </summary>
    public bool IsDateTimeType => Type.Equals("Date", StringComparison.OrdinalIgnoreCase) || 
                                 Type.Equals("Timestamp", StringComparison.OrdinalIgnoreCase) ||
                                 Type.Equals("DateTime", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Represents a reference to another entity
/// </summary>
public class EntityReference
{
    public string Name { get; set; } = string.Empty;
    public string RefersTo { get; set; } = string.Empty;
    public bool Nullable { get; set; } = true;
    public string? Description { get; set; }
    
    /// <summary>
    /// The layer this reference was sourced from (Framework or Product)
    /// </summary>
    public EntitySourceLayer SourceLayer { get; set; } = EntitySourceLayer.Framework;
}

/// <summary>
/// Represents an index on an entity
/// </summary>
public class EntityIndex
{
    public string Name { get; set; } = string.Empty;
    public bool IsUnique { get; set; } = false;
    public List<string> Fields { get; set; } = new();
    public List<string> CoveredFields { get; set; } = new();
    
    /// <summary>
    /// The layer this index was sourced from (Framework or Product)
    /// </summary>
    public EntitySourceLayer SourceLayer { get; set; } = EntitySourceLayer.Framework;
}