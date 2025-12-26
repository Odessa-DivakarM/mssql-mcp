# Transient Entity Validation

## Overview

The `ApiExecutionTool` now includes automatic validation to prevent data retrieval from transient entities (entities marked with `Persistent="False"` in EntityTypes.xaml).

## How It Works

When `GetEntityData` is called, the tool performs the following validation:

1. **Entity Existence Check**: Validates that the EntityTypes.xaml file exists and is accessible
2. **Schema Retrieval**: Retrieves the entity schema from EntityTypes.xaml
3. **Persistence Validation**: Checks if the entity has `Persistent="True"` (default) or `Persistent="False"`
4. **Error Handling**: Returns appropriate error message if entity is transient

## Example Scenario

### EntityTypes.xaml Entry
```xml
<Entity Name="SessionGlobalParam" Persistent="False">
  <Entity.Attributes>
    <Attribute Name="Id" Type="Int" Nullable="false" />
    <Attribute Name="SessionId" Type="Text" Nullable="false" />
    <Attribute Name="ParameterName" Type="Text" Nullable="false" />
    <Attribute Name="ParameterValue" Type="Text" Nullable="true" />
  </Entity.Attributes>
</Entity>
```

### API Call and Response
```
User Request: "Get data from SessionGlobalParam"

Tool Response:
Error: 'SessionGlobalParam' is a Transient entity (Persistent=false) and is invalid for this request.

?? Transient entities are temporary and do not store persistent data that can be retrieved.
Please use GetAvailableEntities or GetEntitySchema to find entities that support data retrieval.
```

## Error Message Format

The error message follows this format:
```
Error: '{EntityName}' is a Transient entity (Persistent=false) and is invalid for this request.

?? Transient entities are temporary and do not store persistent data that can be retrieved.
Please use GetAvailableEntities or GetEntitySchema to find entities that support data retrieval.
```

## Schema Tools Integration

### GetEntitySchema Tool
- Shows a warning when displaying schema for transient entities
- Clearly indicates `Persistent: False` status
- Provides guidance about data retrieval limitations

### GetAvailableEntities Tool
- Separates persistent and transient entities in the output
- Shows persistent entities first (can be used with GetEntityData)
- Shows transient entities separately with warning

## Validation Features

### Graceful Error Handling
- If EntityTypes.xaml is not configured or missing, validation is skipped (allows API to handle errors)
- If entity is not found in schema, validation is skipped (allows API to provide entity-not-found errors)
- Only blocks requests for explicitly transient entities

### Performance Considerations
- Schema validation is performed only when needed
- Results can be cached (if caching is enabled in SchemaOptions)
- Minimal performance impact on valid requests

### Logging
- Debug logs for successful validation
- Warning logs for transient entity attempts
- Error logs for validation failures (with graceful fallback)

## Configuration

The validation uses the existing `SchemaOptions` configuration:

```json
{
  "Schema": {
    "EntityTypesFilePath": "path/to/EntityTypes.xaml",
    "EnableCaching": true,
    "CacheExpirationMinutes": 60
  }
}
```

Environment variables:
- `SCHEMA_ENTITY_TYPES_FILE_PATH`: Path to EntityTypes.xaml file
- `SCHEMA_ENABLE_CACHING`: Enable schema caching (true/false)
- `SCHEMA_CACHE_EXPIRATION_MINUTES`: Cache expiration time in minutes

## AI Workflow Integration

The AI should handle transient entity errors as follows:

1. **Error Detection**: Look for "Transient entity" in error messages
2. **Recovery Action**: Call `GetAvailableEntities` to find valid persistent entities
3. **Alternative Suggestion**: Suggest similar persistent entities to the user
4. **Schema Verification**: Use `GetEntitySchema` to verify persistence status

### Error Pattern Recognition
```
Pattern: "Transient entity"
Action: ? Call GetAvailableEntities to find valid entities
        ? Suggest persistent alternatives
        ? Explain why transient entities cannot be queried
```

## Benefits

1. **Prevents Invalid Requests**: Stops transient entity queries before they reach the API
2. **Clearer Error Messages**: Provides specific guidance about entity limitations
3. **Better User Experience**: Explains why the operation failed and suggests alternatives
4. **Consistent Behavior**: Applies validation uniformly across all data retrieval operations
5. **Performance**: Reduces unnecessary API calls for invalid entities

This feature ensures that users and AI systems understand which entities support data retrieval and provides helpful guidance when attempting to query transient entities.