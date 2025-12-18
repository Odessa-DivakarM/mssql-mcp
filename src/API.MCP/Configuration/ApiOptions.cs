using System.ComponentModel.DataAnnotations;

namespace API.MCP.Configuration;

public enum AuthenticationType
{
    None,
    ApiKey,
    BasicAuth,
    NtlmAuth
}

public class ApiOptions
{
    public const string SectionName = "Api";

    [Required(ErrorMessage = "API_BASE_URL environment variable is required")]
    public string BaseUrl { get; set; } = string.Empty;

    public string? ApiKey { get; set; }

    public int TimeoutSeconds { get; set; } = 30;

    public string? Version { get; set; } = "v1";

    /// <summary>
    /// Authentication type to use for API requests
    /// </summary>
    public AuthenticationType AuthType { get; set; } = AuthenticationType.ApiKey;

    /// <summary>
    /// Username for Basic Auth or NTLM Auth
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Password for Basic Auth or NTLM Auth
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Domain for NTLM Auth (optional)
    /// </summary>
    public string? Domain { get; set; }
}

public class ApiOptionsValidator : IValidateOptions<ApiOptions>
{
    public ValidateOptionsResult Validate(string? name, ApiOptions options)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            errors.Add("API_BASE_URL environment variable must be provided and cannot be empty");
        }
        else if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _))
        {
            errors.Add("API_BASE_URL must be a valid URL");
        }

        // Validate authentication configuration based on auth type
        switch (options.AuthType)
        {
            case AuthenticationType.ApiKey:
                if (string.IsNullOrWhiteSpace(options.ApiKey))
                {
                    errors.Add("API_API_KEY environment variable must be provided when using ApiKey authentication");
                }
                break;

            case AuthenticationType.BasicAuth:
            case AuthenticationType.NtlmAuth:
                if (string.IsNullOrWhiteSpace(options.Username))
                {
                    errors.Add($"Username must be provided when using {options.AuthType} authentication");
                }
                if (string.IsNullOrWhiteSpace(options.Password))
                {
                    errors.Add($"Password must be provided when using {options.AuthType} authentication");
                }
                break;

            case AuthenticationType.None:
                // No additional validation needed
                break;
        }

        if (options.TimeoutSeconds <= 0)
        {
            errors.Add("TimeoutSeconds must be greater than 0");
        }

        return errors.Count > 0 ? ValidateOptionsResult.Fail(errors) : ValidateOptionsResult.Success;
    }
}