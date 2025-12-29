namespace API.MCP.Services;

using API.MCP.Models;

public interface IApiRequestFactory
{
    ApiRequest CreateEntityRequest(string entityName, object? body = null);
    ApiRequest CreatePingRequest();
}

public interface IResponseProcessor
{
    string ProcessResponse(ApiResponse response, string entityName, string? originalEntityName = null);
}
