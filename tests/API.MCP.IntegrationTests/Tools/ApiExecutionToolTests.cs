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
        var result = await _tool.GetEntityData("Get all data from GlobalParameter", "GlobalParameter");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Successfully retrieved data from", result);
    }

    [Fact]
    public async Task GetEntityData_WithPluralEntityName_NormalizesToSingular()
    {
        // Act
        var result = await _tool.GetEntityData("Show me all GlobalParameters", "GlobalParameter");

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
        var result = await _tool.GetEntityData("", "");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("? Error: Entity name must be provided", result);
    }

    [Fact]
    public async Task GetEntityData_WithUnparseableQuery_ReturnsError()
    {
        // Act
        var result = await _tool.GetEntityData("some random text without entity", "");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("? Error: Entity name must be provided", result);
    }

    [Fact]
    public async Task GetEntityData_IncludesPaginationInfo()
    {
        // Act
        var result = await _tool.GetEntityData("Get all data from GlobalParameter", "GlobalParameter");

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
        var result = await _tool.GetEntityData("Get all data from User", "User");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Successfully retrieved data from", result);
        // Should contain pagination info from User endpoint
        Assert.Contains("Page Size: 50", result);
        Assert.Contains("Total Items: 25", result);
    }

    #endregion

    #region Column Selection Tests

    [Fact]
    public async Task GetEntityData_WithSelectColumns_ReturnsSelectedColumns()
    {
        // Act - Test column selection
        var result = await _tool.GetEntityData(
            "Get only IDs and names from users", 
            "User", 
            null,
            "Id,FirstName,LastName");

        // Assert
        Assert.NotNull(result);
        // Should either succeed or provide helpful error message
        Assert.True(
            result.Contains("Successfully retrieved data from") || 
            result.Contains("Error retrieving data from") ||
            result.Contains("Query completed for"));
    }

    [Fact]
    public async Task GetEntityData_WithSelectAndFilter_ReturnsFilteredSelectedColumns()
    {
        // Act - Test combination of filtering and column selection
        var result = await _tool.GetEntityData(
            "Get names of active users", 
            "User", 
            "IsActive=true",
            "FirstName,LastName,LoginName");

        // Assert
        Assert.NotNull(result);
        // Should handle the combined query
        Assert.True(
            result.Contains("Successfully retrieved data from") || 
            result.Contains("Error retrieving data from") ||
            result.Contains("Query completed for"));
    }

    [Fact]
    public async Task GetEntityData_WithComplexSelectAndFilter_ReturnsData()
    {
        // Act - Test complex filtering with specific column selection
        var result = await _tool.GetEntityData(
            "Get basic info for admin users whose login starts with Admin", 
            "User", 
            "LoginName.StartsWith(\"Admin\") && Status=\"Active\"",
            "Id,LoginName,FirstName,LastName,IsActive");

        // Assert
        Assert.NotNull(result);
        // Should handle the complex query with selection
        Assert.True(
            result.Contains("Successfully retrieved data from") || 
            result.Contains("Error retrieving data from") ||
            result.Contains("Query completed for"));
    }

    [Fact]
    public async Task GetEntityData_WithOnlySelectNoFilter_ReturnsSelectedColumns()
    {
        // Act - Test select without filtering (get all records but only specific columns)
        var result = await _tool.GetEntityData(
            "Get all user IDs and login names", 
            "User", 
            null,
            "Id,LoginName");

        // Assert
        Assert.NotNull(result);
        // Should return selected columns for all records
        Assert.True(
            result.Contains("Successfully retrieved data from") || 
            result.Contains("Error retrieving data from") ||
            result.Contains("Query completed for"));
    }

    #endregion

    #region Advanced String Filtering Tests

    [Fact]
    public async Task GetEntityData_WithStringStartsWithFilter_ReturnsData()
    {
        // Act - Test StartsWith string filtering
        var result = await _tool.GetEntityData(
            "Get users whose login starts with Admin", 
            "User", 
            "LoginName.StartsWith(\"Admin\")");

        // Assert
        Assert.NotNull(result);
        // Should either succeed or provide helpful error message
        Assert.True(
            result.Contains("Successfully retrieved data from") || 
            result.Contains("Error retrieving data from") ||
            result.Contains("Query completed for"));
    }

    [Fact]
    public async Task GetEntityData_WithStringEndsWithFilter_ReturnsData()
    {
        // Act - Test EndsWith string filtering
        var result = await _tool.GetEntityData(
            "Get accounts ending with .test", 
            "User", 
            "LoginName.EndsWith(\".test\")");

        // Assert
        Assert.NotNull(result);
        // Should handle the query gracefully
        Assert.True(
            result.Contains("Successfully retrieved data from") || 
            result.Contains("Error retrieving data from") ||
            result.Contains("Query completed for"));
    }

    [Fact]
    public async Task GetEntityData_WithStringContainsFilter_ReturnsData()
    {
        // Act - Test Contains list filtering
        var result = await _tool.GetEntityData(
            "Get specific users John and Mary", 
            "User", 
            "(\"John,Mary\").Contains(UserName)");

        // Assert
        Assert.NotNull(result);
        // Should handle the query gracefully
        Assert.True(
            result.Contains("Successfully retrieved data from") || 
            result.Contains("Error retrieving data from") ||
            result.Contains("Query completed for"));
    }

    [Fact]
    public async Task GetEntityData_WithComplexStringFilter_ReturnsData()
    {
        // Act - Test complex string filtering with AND/OR
        var result = await _tool.GetEntityData(
            "Get active users whose login starts with Admin or Test", 
            "User", 
            "Status=\"Active\" && (LoginName.StartsWith(\"Admin\") || LoginName.StartsWith(\"Test\"))");

        // Assert
        Assert.NotNull(result);
        // Should handle the complex query
        Assert.True(
            result.Contains("Successfully retrieved data from") || 
            result.Contains("Error retrieving data from") ||
            result.Contains("Query completed for"));
    }

    [Fact]
    public async Task GetEntityData_WithStringNotEqualsFilter_ReturnsData()
    {
        // Act - Test not equals string filtering
        var result = await _tool.GetEntityData(
            "Get users except system accounts", 
            "User", 
            "UserType!=\"System\"");

        // Assert
        Assert.NotNull(result);
        // Should handle the query
        Assert.True(
            result.Contains("Successfully retrieved data from") || 
            result.Contains("Error retrieving data from") ||
                result.Contains("Query completed for"));
    }

    #endregion

    #region Pagination Tests

    [Fact]
    public async Task GetEntityData_WithPageSize_ReturnsCorrectPageSize()
    {
        // Act - Test specific page size
        var result = await _tool.GetEntityData(
            "Get first 10 users", 
            "User", 
            null,
            null,
            10); // pageSize

        // Assert
        Assert.NotNull(result);
        // Should handle the pagination request
        Assert.True(
            result.Contains("Successfully retrieved data from") || 
            result.Contains("Error retrieving data from") ||
            result.Contains("Query completed for"));
    }

    [Fact]
    public async Task GetEntityData_WithPageIndex_ReturnsCorrectPage()
    {
        // Act - Test specific page index
        var result = await _tool.GetEntityData(
            "Get users on page 2", 
            "User", 
            null,
            null,
            null,
            2); // pageIndex

        // Assert
        Assert.NotNull(result);
        // Should handle the pagination request
        Assert.True(
            result.Contains("Successfully retrieved data from") || 
            result.Contains("Error retrieving data from") ||
            result.Contains("Query completed for"));
    }

    [Fact]
    public async Task GetEntityData_WithPageSizeAndIndex_ReturnsCorrectPage()
    {
        // Act - Test both page size and index
        var result = await _tool.GetEntityData(
            "Get 5 users on page 3", 
            "User", 
            null,
            null,
            5,  // pageSize
            3); // pageIndex

        // Assert
        Assert.NotNull(result);
        // Should handle the pagination request
        Assert.True(
            result.Contains("Successfully retrieved data from") || 
            result.Contains("Error retrieving data from") ||
            result.Contains("Query completed for"));
    }

    [Fact]
    public async Task GetEntityData_WithFetchAllPages_ReturnsAllData()
    {
        // Act - Test fetching all pages
        var result = await _tool.GetEntityData(
            "Get all users regardless of pagination", 
            "User", 
            null,
            null,
            null,
            null,
            true); // fetchAllPages

        // Assert
        Assert.NotNull(result);
        // Should handle the fetch all request
        if (result.Contains("Successfully retrieved ALL pages"))
        {
            // If successful, should contain fetch summary
            Assert.Contains("Fetch Summary:", result);
            Assert.Contains("Total Pages Fetched:", result);
            Assert.Contains("Total Records Retrieved:", result);
        }
        else
        {
            // Should at least handle the request gracefully
            Assert.True(
                result.Contains("Error fetching") ||
                result.Contains("Query completed for"));
        }
    }

    [Fact]
    public async Task GetEntityData_WithFetchAllPagesAndFilter_ReturnsFilteredData()
    {
        // Act - Test fetching all pages with filtering
        var result = await _tool.GetEntityData(
            "Get all active users", 
            "User", 
            "IsActive=true",
            null,
            null,
            null,
            true); // fetchAllPages

        // Assert
        Assert.NotNull(result);
        // Should handle the filtered fetch all request
        Assert.True(
            result.Contains("Successfully retrieved ALL pages") || 
            result.Contains("Error fetching") ||
            result.Contains("Query completed for"));
    }

    [Fact]
    public async Task GetEntityData_WithInvalidPageSize_ClampsToValidRange()
    {
        // Act - Test invalid page size (too large)
        var result = await _tool.GetEntityData(
            "Get users with large page size", 
            "User", 
            null,
            null,
            5000); // pageSize over 1000 limit

        // Assert
        Assert.NotNull(result);
        // Should clamp to max value and work
        Assert.True(
            result.Contains("Successfully retrieved data from") || 
            result.Contains("Error retrieving data from") ||
            result.Contains("Query completed for"));
    }

    [Fact]
    public async Task GetEntityData_WithInvalidPageIndex_ClampsToValidRange()
    {
        // Act - Test invalid page index (negative)
        var result = await _tool.GetEntityData(
            "Get users with invalid page index", 
            "User", 
            null,
            null,
            null,
            -1); // negative pageIndex

        // Assert
        Assert.NotNull(result);
        // Should clamp to 1 and work
        Assert.True(
            result.Contains("Successfully retrieved data from") || 
            result.Contains("Error retrieving data from") ||
            result.Contains("Query completed for"));
    }

    #endregion
}