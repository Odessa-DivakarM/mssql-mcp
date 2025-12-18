using Microsoft.Extensions.Logging.Abstractions;
using API.MCP.IntegrationTests.Infrastructure;
using API.MCP.Tools;
using Xunit;

namespace API.MCP.IntegrationTests.Tools;

/// <summary>
/// Integration tests for ApiExecutionTool that validate ping functionality
/// with a mock API server.
/// </summary>
public class ApiExecutionToolTests : IClassFixture<ApiTestFixture>, IAsyncLifetime
{
    private readonly ApiTestFixture _fixture;
    private readonly ApiExecutionTool _tool;

    public ApiExecutionToolTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
        _tool = new ApiExecutionTool(fixture.ApiService, NullLogger<ApiExecutionTool>.Instance);
    }

    public async Task InitializeAsync()
    {
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _fixture.ResetMockServer();
        await Task.CompletedTask;
    }

    #region Ping Tests

    [Fact]
    public async Task PingApi_ReturnsSuccessResponse()
    {
        // Act
        var result = await _tool.PingApi();

        // Assert
        Assert.NotNull(result);
        Assert.Contains("? API is alive and running!", result);
        Assert.Contains("Success: True", result);
        Assert.Contains("Processed Time:", result);
    }

    [Fact]
    public async Task PingApi_WithCancellation_HandlesGracefully()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        
        // Should handle cancellation gracefully
        var result = await _tool.PingApi(cts.Token);
        Assert.NotNull(result);
    }

    #endregion
}