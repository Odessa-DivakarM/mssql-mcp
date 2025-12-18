using WireMock.Server;
using WireMock.Settings;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using API.MCP.Configuration;
using API.MCP.Services;
using System.Text.Json;
using Xunit;

namespace API.MCP.IntegrationTests.Infrastructure;

/// <summary>
/// Test fixture that sets up a mock API server for integration testing
/// </summary>
public class ApiTestFixture : IAsyncLifetime
{
    private WireMockServer? _mockServer;
    private ServiceProvider? _serviceProvider;

    public string BaseUrl => _mockServer?.Url ?? throw new InvalidOperationException("Mock server not initialized");
    public IApiService ApiService => _serviceProvider?.GetRequiredService<IApiService>() ?? 
        throw new InvalidOperationException("Service provider not initialized");

    public async Task InitializeAsync()
    {
        // Start WireMock server
        _mockServer = WireMockServer.Start(new WireMockServerSettings
        {
            Port = 0, // Random available port
            StartAdminInterface = true
        });

        // Set up default mock responses
        SetupDefaultMockResponses();

        // Configure services
        var services = new ServiceCollection();
        
        services.AddLogging(builder => builder.AddConsole());
        
        services.Configure<ApiOptions>(options =>
        {
            options.BaseUrl = BaseUrl;
            options.ApiKey = "test-api-key";
            options.Version = "v1";
            options.TimeoutSeconds = 30;
        });

        services.AddHttpClient<IApiService, ApiService>();

        _serviceProvider = services.BuildServiceProvider();
        
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _serviceProvider?.Dispose();
        _mockServer?.Stop();
        _mockServer?.Dispose();
        await Task.CompletedTask;
    }

    private void SetupDefaultMockResponses()
    {
        if (_mockServer == null) return;

        // Health check endpoint
        _mockServer
            .Given(Request.Create().WithPath("/v1/health/check").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("content-type", "application/json")
                .WithBody(JsonSerializer.Serialize(new { status = "healthy", timestamp = DateTime.UtcNow })));

        // Ping endpoint
        _mockServer
            .Given(Request.Create().WithPath("/api/ping").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("content-type", "application/json")
                .WithBody(JsonSerializer.Serialize(new 
                { 
                    Success = true, 
                    ProcessedTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffffK")
                })));

        // Schema endpoint
        _mockServer
            .Given(Request.Create().WithPath("/v1/describe/schema").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("content-type", "application/json")
                .WithBody(JsonSerializer.Serialize(new
                {
                    version = "1.0",
                    endpoints = new[]
                    {
                        new { path = "/v1/get/users", method = "GET", description = "Get all users" },
                        new { path = "/v1/create/users", method = "POST", description = "Create a new user" },
                        new { path = "/v1/update/users", method = "PUT", description = "Update a user" },
                        new { path = "/v1/delete/users", method = "DELETE", description = "Delete a user" }
                    }
                })));

        // Users endpoints
        _mockServer
            .Given(Request.Create().WithPath("/v1/get/users").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("content-type", "application/json")
                .WithBody(JsonSerializer.Serialize(new
                {
                    success = true,
                    data = new[]
                    {
                        new { id = 1, name = "John Doe", email = "john@example.com", status = "active" },
                        new { id = 2, name = "Jane Smith", email = "jane@example.com", status = "active" },
                        new { id = 3, name = "Bob Johnson", email = "bob@example.com", status = "inactive" }
                    },
                    count = 3
                })));

        _mockServer
            .Given(Request.Create().WithPath("/v1/create/users").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(201)
                .WithHeader("content-type", "application/json")
                .WithBody(JsonSerializer.Serialize(new
                {
                    success = true,
                    data = new { id = 4, name = "New User", email = "new@example.com", status = "active" },
                    message = "User created successfully"
                })));

        // Orders endpoints
        _mockServer
            .Given(Request.Create().WithPath("/v1/get/orders").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("content-type", "application/json")
                .WithBody(JsonSerializer.Serialize(new
                {
                    success = true,
                    data = new[]
                    {
                        new { id = 1, userId = 1, total = 99.99, status = "completed", date = DateTime.UtcNow.AddDays(-1) },
                        new { id = 2, userId = 2, total = 149.99, status = "pending", date = DateTime.UtcNow.AddHours(-2) }
                    },
                    count = 2
                })));

        // Error response for testing
        _mockServer
            .Given(Request.Create().WithPath("/v1/get/error").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(404)
                .WithHeader("content-type", "application/json")
                .WithBody(JsonSerializer.Serialize(new
                {
                    success = false,
                    message = "Resource not found",
                    errorCode = "NOT_FOUND"
                })));
    }

    public void SetupCustomResponse(string path, string method, int statusCode, object responseBody)
    {
        if (_mockServer == null) return;

        var request = Request.Create().WithPath(path);
        request = method.ToUpperInvariant() switch
        {
            "GET" => request.UsingGet(),
            "POST" => request.UsingPost(),
            "PUT" => request.UsingPut(),
            "DELETE" => request.UsingDelete(),
            _ => request.UsingGet()
        };

        _mockServer
            .Given(request)
            .RespondWith(Response.Create()
                .WithStatusCode(statusCode)
                .WithHeader("content-type", "application/json")
                .WithBody(JsonSerializer.Serialize(responseBody)));
    }

    public void ResetMockServer()
    {
        _mockServer?.Reset();
        SetupDefaultMockResponses();
    }
}