using System.ComponentModel.DataAnnotations;

namespace API.MCP.Configuration;

/// <summary>
/// Configuration options for entity schema functionality
/// </summary>
public class SchemaOptions
{
    public const string SectionName = "Schema";

    /// <summary>
    /// Path to the EntityTypes.xaml file that contains entity definitions.
    /// Can be an absolute or relative path.
    /// </summary>
    public string EntityTypesFilePath { get; set; } = string.Empty;

    /// <summary>
    /// Whether to cache entity schemas in memory to improve performance
    /// </summary>
    public bool EnableCaching { get; set; } = true;

    /// <summary>
    /// Cache expiration time in minutes for entity schemas
    /// </summary>
    public int CacheExpirationMinutes { get; set; } = 60;
}

/// <summary>
/// Validator for schema configuration options
/// </summary>
public class SchemaOptionsValidator : IValidateOptions<SchemaOptions>
{
    public ValidateOptionsResult Validate(string? name, SchemaOptions options)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(options.EntityTypesFilePath))
        {
            errors.Add("SCHEMA_ENTITY_TYPES_FILE_PATH environment variable must be provided and cannot be empty");
        }
        else if (!File.Exists(options.EntityTypesFilePath))
        {
            // Only warn if the file doesn't exist, don't fail validation
            // This allows the tool to provide better error messages at runtime
        }

        if (options.CacheExpirationMinutes <= 0)
        {
            errors.Add("CacheExpirationMinutes must be greater than 0");
        }

        return errors.Count > 0 ? ValidateOptionsResult.Fail(errors) : ValidateOptionsResult.Success;
    }
}