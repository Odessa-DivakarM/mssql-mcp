using API.MCP.Models;

namespace API.MCP.Services;

/// <summary>
/// Service interface for parsing entity schemas from EntityTypes.xaml files with multi-layer support
/// </summary>
public interface IEntitySchemaService
{
    /// <summary>
    /// Gets the schema for a specific entity by name with multi-layer support.
    /// If both framework and product paths are provided, merges extensions from product layer.
    /// </summary>
    /// <param name="entityName">Name of the entity to get schema for</param>
    /// <param name="frameworkFilePath">Path to the Framework layer EntityTypes.xaml file</param>
    /// <param name="productFilePath">Optional path to the Product layer EntityTypes.xaml file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Entity schema if found, null otherwise</returns>
    Task<EntitySchema?> GetEntitySchemaAsync(string entityName, string frameworkFilePath, string? productFilePath = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Legacy method for backward compatibility - uses single file path
    /// </summary>
    /// <param name="entityName">Name of the entity to get schema for</param>
    /// <param name="entityTypesFilePath">Path to the EntityTypes.xaml file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Entity schema if found, null otherwise</returns>
    [Obsolete("Use overload with frameworkFilePath and productFilePath for multi-layer support")]
    Task<EntitySchema?> GetEntitySchemaAsync(string entityName, string entityTypesFilePath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all available entity names from both framework and product layer files
    /// </summary>
    /// <param name="frameworkFilePath">Path to the Framework layer EntityTypes.xaml file</param>
    /// <param name="productFilePath">Optional path to the Product layer EntityTypes.xaml file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of entity names from both layers</returns>
    Task<List<string>> GetAvailableEntitiesAsync(string frameworkFilePath, string? productFilePath = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Legacy method for backward compatibility - uses single file path
    /// </summary>
    /// <param name="entityTypesFilePath">Path to the EntityTypes.xaml file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of entity names</returns>
    [Obsolete("Use overload with frameworkFilePath and productFilePath for multi-layer support")]
    Task<List<string>> GetAvailableEntitiesAsync(string entityTypesFilePath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Finds the closest matching entity name from available entities in both layers (for typo correction)
    /// </summary>
    /// <param name="inputEntityName">Input entity name that might have typos</param>
    /// <param name="frameworkFilePath">Path to the Framework layer EntityTypes.xaml file</param>
    /// <param name="productFilePath">Optional path to the Product layer EntityTypes.xaml file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Closest matching entity name if found, null otherwise</returns>
    Task<string?> FindClosestEntityNameAsync(string inputEntityName, string frameworkFilePath, string? productFilePath = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Legacy method for backward compatibility - uses single file path
    /// </summary>
    /// <param name="inputEntityName">Input entity name that might have typos</param>
    /// <param name="entityTypesFilePath">Path to the EntityTypes.xaml file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Closest matching entity name if found, null otherwise</returns>
    [Obsolete("Use overload with frameworkFilePath and productFilePath for multi-layer support")]
    Task<string?> FindClosestEntityNameAsync(string inputEntityName, string entityTypesFilePath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Validates if the EntityTypes.xaml file exists and is accessible
    /// </summary>
    /// <param name="entityTypesFilePath">Path to the EntityTypes.xaml file</param>
    /// <returns>True if file exists and is accessible, false otherwise</returns>
    bool ValidateEntityTypesFile(string entityTypesFilePath);
    
    /// <summary>
    /// Gets all available entities with their persistence status from both framework and product layer files
    /// </summary>
    /// <param name="frameworkFilePath">Path to the Framework layer EntityTypes.xaml file</param>
    /// <param name="productFilePath">Optional path to the Product layer EntityTypes.xaml file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary of entity names with their persistence status from both layers</returns>
    Task<Dictionary<string, bool>> GetEntitiesWithPersistenceAsync(string frameworkFilePath, string? productFilePath = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Legacy method for backward compatibility - uses single file path
    /// </summary>
    /// <param name="entityTypesFilePath">Path to the EntityTypes.xaml file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary of entity names with their persistence status</returns>
    [Obsolete("Use overload with frameworkFilePath and productFilePath for multi-layer support")]
    Task<Dictionary<string, bool>> GetEntitiesWithPersistenceAsync(string entityTypesFilePath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all child entities for a specified parent entity from both layers
    /// </summary>
    /// <param name="parentEntityName">Name of the parent entity</param>
    /// <param name="frameworkFilePath">Path to the Framework layer EntityTypes.xaml file</param>
    /// <param name="productFilePath">Optional path to the Product layer EntityTypes.xaml file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of child entity schemas related to the parent from both layers</returns>
    Task<List<EntitySchema>> GetChildEntitiesAsync(string parentEntityName, string frameworkFilePath, string? productFilePath = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Legacy method for backward compatibility - uses single file path
    /// </summary>
    /// <param name="parentEntityName">Name of the parent entity</param>
    /// <param name="entityTypesFilePath">Path to the EntityTypes.xaml file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of child entity schemas related to the parent</returns>
    [Obsolete("Use overload with frameworkFilePath and productFilePath for multi-layer support")]
    Task<List<EntitySchema>> GetChildEntitiesAsync(string parentEntityName, string entityTypesFilePath, CancellationToken cancellationToken = default);
}