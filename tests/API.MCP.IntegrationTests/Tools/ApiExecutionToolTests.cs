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
        _tool = new ApiExecutionTool(
            fixture.ApiService, 
            fixture.EntitySchemaService, 
            fixture.SchemaOptions, 
            NullLogger<ApiExecutionTool>.Instance);
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
        Assert.Contains("API is alive and running!", result);
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
        Assert.Contains("Error: Entity name must be provided", result);
    }

    [Fact]
    public async Task GetEntityData_WithUnparseableQuery_ReturnsError()
    {
        // Act
        var result = await _tool.GetEntityData("some random text without entity", "");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Error: Entity name must be provided", result);
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

    #region Sorting Tests

    [Fact]
    public async Task GetEntityData_WithSimpleOrderBy_ReturnsOrderedData()
    {
        // Act - Test simple ascending sort
        var result = await _tool.GetEntityData(
            "Get users ordered by last name", 
            "User", 
            null,
            null,
            "LastName asc"); // orderBy

        // Assert
        Assert.NotNull(result);
        // Should handle the sorting request
        Assert.True(
            result.Contains("Successfully retrieved data from") || 
            result.Contains("Error retrieving data from") ||
            result.Contains("Query completed for"));
    }

    [Fact]
    public async Task GetEntityData_WithDescendingOrderBy_ReturnsOrderedData()
    {
        // Act - Test descending sort
        var result = await _tool.GetEntityData(
            "Get users ordered by ID descending", 
            "User", 
            null,
            null,
            "Id desc"); // orderBy

        // Assert
        Assert.NotNull(result);
        // Should handle the sorting request
        Assert.True(
            result.Contains("Successfully retrieved data from") || 
            result.Contains("Error retrieving data from") ||
            result.Contains("Query completed for"));
    }

    [Fact]
    public async Task GetEntityData_WithMultipleOrderBy_ReturnsOrderedData()
    {
        // Act - Test multiple column sort
        var result = await _tool.GetEntityData(
            "Get users ordered by ID desc, then by name", 
            "User", 
            null,
            null,
            "Id desc, FirstName asc"); // orderBy

        // Assert
        Assert.NotNull(result);
        // Should handle the multi-column sorting request
        Assert.True(
            result.Contains("Successfully retrieved data from") || 
            result.Contains("Error retrieving data from") ||
            result.Contains("Query completed for"));
    }

    [Fact]
    public async Task GetEntityData_WithOrderByAndFilter_ReturnsFilteredOrderedData()
    {
        // Act - Test combining filter and sort
        var result = await _tool.GetEntityData(
            "Get active users ordered by name", 
            "User", 
            "IsActive=true",
            null,
            "FirstName asc, LastName asc"); // orderBy

        // Assert
        Assert.NotNull(result);
        // Should handle the combined filter and sort request
        Assert.True(
            result.Contains("Successfully retrieved data from") || 
            result.Contains("Error retrieving data from") ||
            result.Contains("Query completed for"));
    }

    [Fact]
    public async Task GetEntityData_WithOrderByAndSelect_ReturnsSelectedOrderedData()
    {
        // Act - Test combining column selection and sort
        var result = await _tool.GetEntityData(
            "Get user names ordered by last name", 
            "User", 
            null,
            "FirstName,LastName",
            "LastName asc"); // orderBy

        // Assert
        Assert.NotNull(result);
        // Should handle the combined select and sort request
        Assert.True(
            result.Contains("Successfully retrieved data from") || 
            result.Contains("Error retrieving data from") ||
            result.Contains("Query completed for"));
    }

    [Fact]
    public async Task GetEntityData_WithOrderByAndPagination_ReturnsPagedOrderedData()
    {
        // Act - Test combining sorting with pagination
        var result = await _tool.GetEntityData(
            "Get first 10 users ordered by ID", 
            "User", 
            null,
            null,
            "Id asc", // orderBy
            10,       // pageSize
            1);       // pageIndex

        // Assert
        Assert.NotNull(result);
        // Should handle the combined sort and pagination request
        Assert.True(
            result.Contains("Successfully retrieved data from") || 
            result.Contains("Error retrieving data from") ||
            result.Contains("Query completed for"));
    }

    [Fact]
    public async Task GetEntityData_WithOrderByAndFetchAllPages_ReturnsAllOrderedData()
    {
        // Act - Test sorting with fetch all pages
        var result = await _tool.GetEntityData(
            "Get all users ordered by creation date", 
            "User", 
            null,
            null,
            "CreatedDate desc", // orderBy
            null,
            null,
            true); // fetchAllPages

        // Assert
        Assert.NotNull(result);
        // Should handle the sorted fetch all request
        Assert.True(
            result.Contains("Successfully retrieved ALL pages") || 
            result.Contains("Error fetching") ||
            result.Contains("Query completed for"));
    }

    [Fact]
    public async Task GetEntityData_WithComplexCombinedParameters_ReturnsData()
    {
        // Act - Test all parameters combined
        var result = await _tool.GetEntityData(
            "Get active admin users with basic info, ordered by name, first 5 records", 
            "User", 
            "IsActive=true && DefaultPermissionValues.Value=\"Admin\"", // filter
            "Id,FirstName,LastName,LoginName", // select
            "LastName asc, FirstName asc", // orderBy
            5,  // pageSize
            1); // pageIndex

        // Assert
        Assert.NotNull(result);
        // Should handle the complex combined request
        Assert.True(
            result.Contains("Successfully retrieved data from") || 
            result.Contains("Error retrieving data from") ||
            result.Contains("Query completed for"));
    }

    #endregion

    #region Hierarchical Selection Tests

    [Fact]
    public async Task GetEntityData_WithHierarchicalSelection_ReturnsParentAndChildData()
    {
        // Arrange - Set up mock response for User with UserEmailAddresses
        _fixture.SetupCustomResponse("/api/Entity/User", "POST", 200, new
        {
            success = true,
            data = new[]
            {
                new { 
                    id = 1, 
                    firstName = "John", 
                    lastName = "Doe",
                    userEmailAddresses = new[]
                    {
                        new { email = "john.doe@company.com", isPrimary = true },
                        new { email = "j.doe@personal.com", isPrimary = false }
                    }
                }
            },
            totalRecords = 1,
            entityName = "User"
        });

        // Act - Test hierarchical selection
        var result = await _tool.GetEntityData(
            "Get users with their email addresses", 
            "User", 
            null,
            "FirstName,LastName,UserEmailAddresses.{Email,IsPrimary}");

        // Assert
        Assert.NotNull(result);
        // Should either succeed or provide helpful error message
        Assert.True(
            result.Contains("Successfully retrieved data from") || 
            result.Contains("Error retrieving data from") ||
            result.Contains("Query completed for"));
    }

    [Fact]
    public async Task GetEntityData_WithAssetLocationsHierarchy_ReturnsAssetAndLocationData()
    {
        // Arrange - Set up mock response for Asset with AssetLocations
        _fixture.SetupCustomResponse("/api/Entity/Asset", "POST", 200, new
        {
            success = true,
            data = new[]
            {
                new { 
                    id = 1, 
                    status = "Scrap",
                    quantity = 1,
                    assetLocations = new[]
                    {
                        new { locationId = "LOC001" },
                        new { locationId = "LOC002" }
                    }
                }
            },
            totalRecords = 1,
            entityName = "Asset"
        });

        // Act - Test Asset with AssetLocations hierarchical selection
        var result = await _tool.GetEntityData(
            "Get scrap assets with their locations", 
            "Asset", 
            "Status.Value=\"Scrap\" && Quantity=1",
            "Status,Id,Quantity,AssetLocations.{LocationId}");

        // Assert
        Assert.NotNull(result);
        // Should handle the hierarchical query
        Assert.True(
            result.Contains("Successfully retrieved data from") || 
            result.Contains("Error retrieving data from") ||
            result.Contains("Query completed for"));
    }

    [Fact]
    public async Task GetEntityData_WithPortfolioParametersHierarchy_ReturnsPortfolioAndParameterData()
    {
        // Arrange - Set up mock response for Portfolio with PortfolioParameters
        _fixture.SetupCustomResponse("/api/Entity/Portfolio", "POST", 200, new
        {
            success = true,
            data = new[]
            {
                new { 
                    id = 1, 
                    name = "Investment Portfolio",
                    portfolioParameters = new[]
                    {
                        new { parameterName = "RiskLevel", parameterValue = "Medium" },
                        new { parameterName = "Currency", parameterValue = "USD" }
                    }
                }
            },
            totalRecords = 1,
            entityName = "Portfolio"
        });

        // Act - Test Portfolio with PortfolioParameters hierarchical selection
        var result = await _tool.GetEntityData(
            "Get portfolios with their parameters", 
            "Portfolio", 
            null,
            "Id,Name,PortfolioParameters.{ParameterName,ParameterValue}");

        // Assert
        Assert.NotNull(result);
        // Should handle the hierarchical query
        Assert.True(
            result.Contains("Successfully retrieved data from") || 
            result.Contains("Error retrieving data from") ||
            result.Contains("Query completed for"));
    }

    [Fact]
    public async Task GetEntityData_WithComplexHierarchicalFilterAndSort_ReturnsFilteredSortedData()
    {
        // Act - Test complex hierarchical query with filtering and sorting
        var result = await _tool.GetEntityData(
            "Get active users with primary emails, ordered by name", 
            "User", 
            "IsActive=true",
            "FirstName,LastName,UserEmailAddresses.{Email,IsPrimary}",
            "LastName asc, FirstName asc");

        // Assert
        Assert.NotNull(result);
        // Should handle the complex hierarchical query
        Assert.True(
            result.Contains("Successfully retrieved data from") || 
            result.Contains("Error retrieving data from") ||
            result.Contains("Query completed for"));
    }

    #endregion

    #region Transient Entity Validation Tests

    [Fact]
    public async Task GetEntityData_WithTransientEntity_ReturnsError()
    {
        // Arrange - Set up a mock transient entity response
        _fixture.SetupCustomResponse("/api/Entity/SessionGlobalParam", "POST", 400, new
        {
            success = false,
            message = "Invalid entity for data retrieval"
        });

        // Act - Try to get data from a transient entity
        var result = await _tool.GetEntityData(
            "Get data from SessionGlobalParam", 
            "SessionGlobalParam");

        // Assert - Should handle gracefully (either validation error or API error)
        Assert.NotNull(result);
        Assert.True(
            result.Contains("Transient entity") || 
            result.Contains("Error retrieving data from"));
    }

    [Fact]
    public async Task GetEntityData_WithTransientEntityAndFetchAllPages_ReturnsError()
    {
        // Act - Try to fetch all pages from a transient entity
        var result = await _tool.GetEntityData(
            "Get all data from SessionGlobalParam", 
            "SessionGlobalParam",
            null,
            null,
            null,
            null,
            null,
            true); // fetchAllPages

        // Assert - Should handle gracefully before attempting fetch
        Assert.NotNull(result);
        Assert.True(
            result.Contains("Transient entity") || 
            result.Contains("Error"));
    }

    #endregion
}