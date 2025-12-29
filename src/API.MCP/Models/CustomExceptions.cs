namespace API.MCP.Models;

public class EntityNotFoundException : Exception
{
    public EntityNotFoundException(string entityName)
        : base($"Entity '{entityName}' not found.") { }
}

public class TransientEntityException : Exception
{
    public TransientEntityException(string entityName)
        : base($"Entity '{entityName}' is transient and cannot be queried.") { }
}

public class SchemaValidationException : Exception
{
    public SchemaValidationException(string message) : base(message) { }
}
