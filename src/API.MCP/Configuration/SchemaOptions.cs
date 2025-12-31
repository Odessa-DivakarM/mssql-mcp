using System.ComponentModel.DataAnnotations;

namespace API.MCP.Configuration;

/// <summary>
/// Configuration options for entity schema functionality with multi-layer support
/// </summary>
public class SchemaOptions
{
    public const string SectionName = "Schema";

    /// <summary>
    /// Path to the Framework layer EntityTypes.xaml file that contains base entity definitions.
    /// Can be an absolute or relative path.
    /// </summary>
    public string FrameworkEntityTypesFilePath { get; set; } = string.Empty;

    /// <summary>
    /// Path to the Product layer EntityTypes.xaml file that contains entity extensions.
    /// Can be an absolute or relative path. Optional - if not provided, only framework entities will be used.
    /// </summary>
    public string? ProductEntityTypesFilePath { get; set; }

    /// <summary>
    /// Legacy single file path - maintained for backward compatibility.
    /// If FrameworkEntityTypesFilePath is not provided, this will be used as the framework path.
    /// </summary>
    [Obsolete("Use FrameworkEntityTypesFilePath and ProductEntityTypesFilePath instead")]
    public string EntityTypesFilePath { get; set; } = string.Empty;

    /// <summary>
    /// Whether to cache entity schemas in memory to improve performance
    /// </summary>
    public bool EnableCaching { get; set; } = true;

    /// <summary>
    /// Cache expiration time in minutes for entity schemas
    /// </summary>
    public int CacheExpirationMinutes { get; set; } = 60;

    /// <summary>
    /// Gets the effective framework file path, using legacy path if new one is not provided
    /// </summary>
    public string GetFrameworkFilePath()
    {
#pragma warning disable CS0618 // Type or member is obsolete
        return !string.IsNullOrWhiteSpace(FrameworkEntityTypesFilePath) 
            ? FrameworkEntityTypesFilePath 
            : EntityTypesFilePath;
#pragma warning restore CS0618 // Type or member is obsolete
    }

    /// <summary>
    /// Determines if multi-layer processing is enabled (both framework and product paths are configured)
    /// </summary>
    public bool IsMultiLayerEnabled => !string.IsNullOrWhiteSpace(GetFrameworkFilePath()) && 
                                      !string.IsNullOrWhiteSpace(ProductEntityTypesFilePath);
}

/// <summary>
/// Validator for schema configuration options with multi-layer support
/// </summary>
public class SchemaOptionsValidator : IValidateOptions<SchemaOptions>
{
    public ValidateOptionsResult Validate(string? name, SchemaOptions options)
    {
        var errors = new List<string>();

        var frameworkPath = options.GetFrameworkFilePath();
        if (string.IsNullOrWhiteSpace(frameworkPath))
        {
            errors.Add("Either SCHEMA_FRAMEWORK_ENTITY_TYPES_FILE_PATH or legacy SCHEMA_ENTITY_TYPES_FILE_PATH environment variable must be provided");
        }
        else if (!File.Exists(frameworkPath))
        {
            // Only warn if the file doesn't exist, don't fail validation
            // This allows the tool to provide better error messages at runtime
        }

        // Validate product path if provided
        if (!string.IsNullOrWhiteSpace(options.ProductEntityTypesFilePath) && 
            !File.Exists(options.ProductEntityTypesFilePath))
        {
            // Only warn if the file doesn't exist, don't fail validation
        }

        if (options.CacheExpirationMinutes <= 0)
        {
            errors.Add("CacheExpirationMinutes must be greater than 0");
        }

        return errors.Count > 0 ? ValidateOptionsResult.Fail(errors) : ValidateOptionsResult.Success;
    }
}