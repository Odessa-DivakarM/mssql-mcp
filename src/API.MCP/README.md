# API-MCP

A .NET-powered Model Context Protocol (MCP) server for custom API integration.

## Abstract

This MCP server provides a ping/health check tool for your organization's custom API system. It allows AI agents to verify if your API is alive and running, providing essential monitoring capabilities.

## Features

- **API Health Monitoring**: Check if your API is alive and running
- **Authentication Support**: Multiple authentication methods (Basic Auth, NTLM, API Key)
- **Flexible Configuration**: Easy setup for different API authentication methods
- **Error Handling**: Comprehensive error handling with clear, actionable error messages
- **Response Formatting**: Ping responses formatted in readable format for AI consumption
- **Docker Support**: Easy deployment with built-in .NET Docker tooling

## Available Tools

| Tool | Description |
|------|-------------|
| `ping_api` | Check if the API is alive and running (health check) |

## Configuration

### Required Environment Variables

The MCP server requires the following environment variables:

- **`API_BASE_URL`**: Base URL of your API (e.g., `https://api.yourcompany.com`)

### Authentication Configuration

Choose one of the following authentication methods:

**For API Key Authentication:**
- **`API_API_KEY`**: API key for authentication

**For Basic Authentication:**
- **`API_AUTH_TYPE`**: Set to `BasicAuth`
- **`API_USERNAME`**: Username for Basic Auth
- **`API_PASSWORD`**: Password for Basic Auth

**For NTLM Authentication:**
- **`API_AUTH_TYPE`**: Set to `NtlmAuth`
- **`API_USERNAME`**: Username for NTLM Auth
- **`API_PASSWORD`**: Password for NTLM Auth
- **`API_DOMAIN`**: Domain for NTLM Auth (optional)

### Optional Environment Variables

- **`API_VERSION`**: API version to use (default: `v1`)
- **`API_TIMEOUT_SECONDS`**: Request timeout in seconds (default: `30`)

#### Example Configuration

**Basic Auth Example:**
```bash
API_BASE_URL="https://api.yourcompany.com"
API_AUTH_TYPE="BasicAuth"
API_USERNAME="your-username"
API_PASSWORD="your-password"
API_TIMEOUT_SECONDS="60"
```

**API Key Example:**
```bash
API_BASE_URL="https://api.yourcompany.com"
API_API_KEY="your-secret-api-key"
API_TIMEOUT_SECONDS="60"
```

## Usage Examples

Once configured, AI agents can use natural language to interact with your API:

**Natural Language Queries:**
- *"Get all active users"* ? Translates to `GET /v1/get/users?status=active`
- *"Create a new user with email john@example.com"* ? Translates to `POST /v1/create/users`
- *"Find orders from last week"* ? Translates to `GET /v1/get/orders?date_from=2024-01-01`
- *"Update user ID 123 status to inactive"* ? Translates to `PUT /v1/update/users`

**Direct API Calls:**
- Execute specific API endpoints with precise control over parameters
- Get API schema and documentation
- Test API connectivity with ping endpoint

**Ping Examples:**
- *"Ping API"* ? Calls dedicated `ping_api` tool
- *"Test connection"* ? Uses `ping_api` tool to verify API status
- *"Is API running?"* ? Checks API health via ping endpoint

## Customization

### Ping Endpoint

The ping functionality calls your API's `/api/ping` endpoint. If your API uses a different ping endpoint, you can modify the `IsPingRequest` and `BuildEndpoint` methods in `ApiService.cs`.

### Authentication

The system supports multiple authentication methods:
- **API Key**: Uses `X-API-Key` header
- **Basic Auth**: Uses standard HTTP Basic authentication
- **NTLM Auth**: Uses Windows NTLM authentication (requires proper HttpClientHandler setup)
- **None**: No authentication

## Running the MCP Server

### Option 1: Docker (Recommended)

Build the Docker image:
```bash
cd src/API.MCP
dotnet publish --os linux --arch x64 /t:PublishContainer
```

Run with Docker:
```bash
docker run -it --rm \
  -e API_BASE_URL="https://api.yourcompany.com" \
  -e API_API_KEY="your-secret-key" \
  api-mcp:latest
```

### Option 2: Direct Execution

```bash
cd src/API.MCP
dotnet run
```

## MCP Client Configuration

### Cursor IDE

Add to your Cursor settings (`Cursor Settings > Features > Model Context Protocol`):

```json
{
  "mcpServers": {
    "api": {
      "command": "docker",
      "args": [
          "run",
          "-i",
          "--rm",
          "-e",
          "API_BASE_URL",
          "-e", 
          "API_API_KEY",
          "api-mcp:latest"
      ],
      "env": {
          "API_BASE_URL": "https://api.yourcompany.com",
          "API_API_KEY": "your-secret-api-key"
      }
    }
  }
}
```

### Claude Desktop

Add to your Claude Desktop configuration file:

**Windows:** `%APPDATA%\Claude\claude_desktop_config.json`
**macOS:** `~/Library/Application Support/Claude/claude_desktop_config.json`

```json
{
  "mcpServers": {
    "api": {
      "command": "docker",
      "args": [
          "run",
          "-i", 
          "--rm",
          "-e",
          "API_BASE_URL",
          "-e",
          "API_API_KEY", 
          "api-mcp:latest"
      ],
      "env": {
          "API_BASE_URL": "https://api.yourcompany.com",
          "API_API_KEY": "your-secret-api-key"
      }
    }
  }
}
```

## Development

### Project Structure

```
src/API.MCP/
??? Configuration/     # API configuration and validation
??? Models/           # Data models for requests/responses  
??? Services/         # Core API service implementation
??? Tools/           # MCP tool implementations
??? Actors/          # Akka.NET actors for validation
??? Program.cs       # Main entry point

tests/API.MCP.IntegrationTests/
??? Infrastructure/   # Test fixtures and utilities
??? Tools/           # Tool integration tests
??? TestData/        # Test data files
```

### Running Tests

```bash
dotnet test tests/API.MCP.IntegrationTests/
```

### Building

```bash
dotnet build src/API.MCP/
```

## Security Considerations

### ?? Important Security Warnings

- **API Key Security**: Store API keys securely and never commit them to source control
- **Network Security**: Use HTTPS for all API communications
- **Access Control**: Ensure your API has proper authentication and authorization
- **Rate Limiting**: Consider implementing rate limiting for API calls
- **Audit Logging**: Enable logging for all API requests for security auditing

## Troubleshooting

### Connection Issues

1. **Verify API URL**: Test with curl or Postman first
2. **Check API Key**: Ensure the key has necessary permissions
3. **Network Access**: Verify Docker containers can reach your API
4. **Certificates**: For HTTPS APIs, ensure SSL certificates are valid

### Common Errors

1. **"API connection validation failed"**: Check `API_BASE_URL` and `API_API_KEY`
2. **"Request timeout"**: Increase `API_TIMEOUT_SECONDS` or check API performance
3. **"Invalid JSON"**: Verify API responses are valid JSON format

## License

This software is licensed under Apache 2.0. Use responsibly with your organization's API systems.

## Contributing

1. Fork the repository
2. Create a feature branch
3. Add tests for new functionality
4. Submit a pull request

## Architecture

- **[Akka.NET](https://getakka.net/)**: Actor system coordination and API validation
- **[MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk)**: Official Model Context Protocol implementation
- **HttpClient**: HTTP communication with your API
- **WireMock.NET**: Mock API server for integration testing