using System.ComponentModel;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using API.MCP.Services;

namespace API.MCP.Tools;

[McpServerToolType]
public class ApiExecutionTool(IApiService apiService, ILogger<ApiExecutionTool> logger)
{
    private readonly IApiService _apiService = apiService;

    [McpServerTool, Description("Ping the API to check if it's alive and running. Performs a health check to verify the API system is operational.")]
    public async Task<string> PingApi(CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Performing API ping check");
            
            var response = await ((ApiService)_apiService).PingAsync(cancellationToken);

            if (response.Success && response.Data != null)
            {
                return $"? API is alive and running!\n\n" +
                       $"Success: {response.Data.Success}\n" +
                       $"Processed Time: {response.Data.ProcessedTime:yyyy-MM-dd HH:mm:ss.fff zzz}\n" +
                       $"Response Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}";
            }
            else
            {
                return $"? API ping failed: {response.Message ?? "Unknown error"}";
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during API ping");
            return $"? Error pinging API: {ex.Message}";
        }
    }
}