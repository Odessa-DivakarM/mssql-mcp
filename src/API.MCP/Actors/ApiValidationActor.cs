using API.MCP.Services;

namespace API.MCP.Actors;

public class ApiValidationActor : ReceiveActor
{
    private readonly IApiService _apiService;
    private readonly ILogger<ApiValidationActor> _logger;

    public ApiValidationActor(IApiService apiService, ILogger<ApiValidationActor> logger)
    {
        _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        Receive<ValidateApiConnection>(_ => HandleValidateApiConnection());
    }

    private async void HandleValidateApiConnection()
    {
        try
        {
            _logger.LogInformation("Starting API connection validation...");

            var isValid = await _apiService.ValidateConnectionAsync();

            if (isValid)
            {
                _logger.LogInformation("? API connection validation successful");
                Sender.Tell(new ApiConnectionValidated(true, "API connection is healthy"));
            }
            else
            {
                _logger.LogError("? API connection validation failed");
                Sender.Tell(new ApiConnectionValidated(false, "API connection failed"));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "? API connection validation failed with exception");
            Sender.Tell(new ApiConnectionValidated(false, $"API validation error: {ex.Message}"));
        }
        finally
        {
            // Actor terminates itself after validation attempt
            Context.Stop(Self);
        }
    }
}

// Messages
public record ValidateApiConnection();
public record ApiConnectionValidated(bool IsValid, string Message);