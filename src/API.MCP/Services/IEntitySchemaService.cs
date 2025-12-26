using API.MCP.Models;

namespace API.MCP.Services;

/// <summary>
/// Service interface for parsing entity schemas from EntityTypes.xaml files
/// </summary>
public interface IEntitySchemaService
{
    /// <summary>
    /// Gets the schema for a specific entity by name
    /// </summary>
    /// <param name="entityName">Name of the entity to get schema for</param>
    /// <param name="entityTypesFilePath">Path to the EntityTypes.xaml file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Entity schema if found, null otherwise</returns>
    Task<EntitySchema?> GetEntitySchemaAsync(string entityName, string entityTypesFilePath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all available entity names from the EntityTypes.xaml file
    /// </summary>
    /// <param name="entityTypesFilePath">Path to the EntityTypes.xaml file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of entity names</returns>
    Task<List<string>> GetAvailableEntitiesAsync(string entityTypesFilePath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Finds the closest matching entity name from available entities (for typo correction)
    /// </summary>
    /// <param name="inputEntityName">Input entity name that might have typos</param>
    /// <param name="entityTypesFilePath">Path to the EntityTypes.xaml file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Closest matching entity name if found, null otherwise</returns>
    Task<string?> FindClosestEntityNameAsync(string inputEntityName, string entityTypesFilePath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Validates if the EntityTypes.xaml file exists and is accessible
    /// </summary>
    /// <param name="entityTypesFilePath">Path to the EntityTypes.xaml file</param>
    /// <returns>True if file exists and is accessible, false otherwise</returns>
    bool ValidateEntityTypesFile(string entityTypesFilePath);
    
    /// <summary>
    /// Gets all available entities with their persistence status from the EntityTypes.xaml file
    /// </summary>
    /// <param name="entityTypesFilePath">Path to the EntityTypes.xaml file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary of entity names with their persistence status</returns>
    Task<Dictionary<string, bool>> GetEntitiesWithPersistenceAsync(string entityTypesFilePath, CancellationToken cancellationToken = default);
}