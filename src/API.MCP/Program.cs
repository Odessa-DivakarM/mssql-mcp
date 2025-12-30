using Akka.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using API.MCP.Configuration;
using API.MCP.Services;
using API.MCP.Actors;

var hostBuilder = new HostBuilder();

hostBuilder
    .ConfigureAppConfiguration((context, builder) =>
    {
        builder.AddEnvironmentVariables();

        // Map environment variables to configuration sections
        builder.AddInMemoryCollection([
            new KeyValuePair<string, string?>("Api:BaseUrl",
                Environment.GetEnvironmentVariable("API_BASE_URL")),
            new KeyValuePair<string, string?>("Api:ApiKey",
                Environment.GetEnvironmentVariable("API_API_KEY")),
            new KeyValuePair<string, string?>("Api:Version",
                Environment.GetEnvironmentVariable("API_VERSION") ?? "v1"),
            new KeyValuePair<string, string?>("Api:TimeoutSeconds",
                Environment.GetEnvironmentVariable("API_TIMEOUT_SECONDS") ?? "30"),
            new KeyValuePair<string, string?>("Api:AuthType",
                Environment.GetEnvironmentVariable("API_AUTH_TYPE") ?? "ApiKey"),
            new KeyValuePair<string, string?>("Api:BusinessUnit",
                Environment.GetEnvironmentVariable("API_BUSINESS_UNIT") ?? "BU"),
            new KeyValuePair<string, string?>("Api:Username",
                Environment.GetEnvironmentVariable("API_USERNAME")),
            new KeyValuePair<string, string?>("Api:Password",
                Environment.GetEnvironmentVariable("API_PASSWORD")),
            new KeyValuePair<string, string?>("Api:Domain",
                Environment.GetEnvironmentVariable("API_DOMAIN")),
            new KeyValuePair<string, string?>("Schema:EntityTypesFilePath",
                Environment.GetEnvironmentVariable("SCHEMA_ENTITY_TYPES_FILE_PATH")),
            new KeyValuePair<string, string?>("Schema:EnableCaching",
                Environment.GetEnvironmentVariable("SCHEMA_ENABLE_CACHING") ?? "true"),
            new KeyValuePair<string, string?>("Schema:CacheExpirationMinutes",
                Environment.GetEnvironmentVariable("SCHEMA_CACHE_EXPIRATION_MINUTES") ?? "60")
        ]);
    })
    .ConfigureServices((context, services) =>
    {
        // Configure logging to stderr for MCP protocol compatibility
        services.AddLogging(builder =>
        {
            builder.AddConsole(consoleLogOptions =>
            {
                consoleLogOptions.LogToStandardErrorThreshold = Microsoft.Extensions.Logging.LogLevel.Trace;
            });
        });

        // Configure API options with validation
        services.AddSingleton<IValidateOptions<ApiOptions>, ApiOptionsValidator>();
        services.AddOptionsWithValidateOnStart<ApiOptions>()
            .BindConfiguration("Api");

        // Configure Schema options with validation
        services.AddSingleton<IValidateOptions<SchemaOptions>, SchemaOptionsValidator>();
        services.AddOptionsWithValidateOnStart<SchemaOptions>()
            .BindConfiguration("Schema");


        // Register HTTP client and API service
        services.AddHttpClient<IApiService, ApiService>();

        // Register memory cache for schema caching
        services.AddMemoryCache();


        // Register entity name pluralization service
        services.AddSingleton<IEntityNameService, EntityNameService>();

        // Register entity schema service with caching
        services.AddSingleton<IEntitySchemaService, EntitySchemaService>();

        // Add MCP Server
        services.AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly();

        // Add Akka.NET
        services.AddAkka("APIMcpActorSystem", (builder, sp) =>
        {
            builder
                .ConfigureLoggers(configBuilder =>
                {
                    configBuilder.ClearLoggers();
                    configBuilder.AddLoggerFactory();
                })
                .WithActors((system, registry, resolver) =>
                {
                    // API validation actor - tests actual connection
                    var apiValidationActorProps = resolver.Props<ApiValidationActor>();
                    var apiValidationActor = system.ActorOf(apiValidationActorProps, "api-validation");

                    // We would normally register this actor in the registry, but since it dies immediately after validation,
                    // there's not much point in keeping it around.
                });
        });
    });

var host = hostBuilder.Build();

await host.RunAsync();