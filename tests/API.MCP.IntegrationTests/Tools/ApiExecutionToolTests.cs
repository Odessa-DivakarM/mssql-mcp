using Microsoft.Extensions.Logging.Abstractions;
using API.MCP.IntegrationTests.Infrastructure;
using API.MCP.Tools;
using Xunit;

namespace API.MCP.IntegrationTests.Tools;

/// <summary>
/// Integration tests for ApiExecutionTool that validate ping and entity data functionality
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

    #region Entity Data Tests

    [Fact]
    public async Task GetEntityData_WithValidEntityName_ReturnsData()
    {
        // Act
        var result = await _tool.GetEntityData("Get all data from GlobalParameter");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Successfully retrieved data from", result);
    }

    [Fact]
    public async Task GetEntityData_WithPluralEntityName_NormalizesToSingular()
    {
        // Act
        var result = await _tool.GetEntityData("Show me all GlobalParameters");

        // Assert
        Assert.NotNull(result);
        // Should work with normalized entity name
        Assert.DoesNotContain("? Error", result);
    }

    [Fact]
    public async Task GetEntityData_WithExplicitEntityName_UsesProvidedName()
    {
        // Act
        var result = await _tool.GetEntityData("Get the data", "GlobalParameter");

        // Assert
        Assert.NotNull(result);
        Assert.DoesNotContain("? Error", result);
    }

    [Fact]
    public async Task GetEntityData_WithEmptyQuery_ReturnsError()
    {
        // Act
        var result = await _tool.GetEntityData("");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("? Error: Query or entity name must be provided", result);
    }

    [Fact]
    public async Task GetEntityData_WithUnparseableQuery_ReturnsError()
    {
        // Act
        var result = await _tool.GetEntityData("some random text without entity");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("? Error: Could not identify entity name", result);
    }

    [Fact]
    public async Task GetEntityData_IncludesPaginationInfo()
    {
        // Act
        var result = await _tool.GetEntityData("Get all data from GlobalParameter");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Pagination Info:", result);
        Assert.Contains("Page Size: 100", result);
        Assert.Contains("Current Page: 1", result);
        Assert.Contains("Total Items: 103", result);
    }

    [Fact]
    public async Task GetEntityData_IncludesHttpStatusCode()
    {
        // Act
        var result = await _tool.GetEntityData("Get all data from User");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Successfully retrieved data from", result);
        // Should contain pagination info from User endpoint
        Assert.Contains("Page Size: 50", result);
        Assert.Contains("Total Items: 25", result);
    }

    #endregion
}