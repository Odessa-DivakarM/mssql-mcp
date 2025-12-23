# Column Selection Guide for GetEntityData Tool

## Overview
The `GetEntityData` tool now supports selective column retrieval using the `Select` parameter. This allows users to specify exactly which columns they want instead of retrieving all entity data, improving performance and reducing data transfer.

## Select Parameter Syntax

### Basic Column Selection
```
Select: "FirstName,LastName"
Result: Only FirstName and LastName columns are returned
```

### Multiple Columns
```
Select: "Id,LoginName,FirstName,LastName,IsActive"
Result: Returns the specified 5 columns for all matching records
```

### Single Column
```
Select: "LoginName"
Result: Returns only the LoginName column
```

## API Request Body Structure

The tool now constructs request bodies with both `Where` and `Select` parameters:

### Only Select (no filtering)
```json
{
  "Select": "FirstName,LastName"
}
```

### Only Where (no column selection)
```json
{
  "Where": "IsActive=true"
}
```

### Both Select and Where
```json
{
  "Where": "IsActive=true && LoginName.StartsWith(\"Admin\")",
  "Select": "Id,LoginName,FirstName,LastName"
}
```

### Neither (all data)
```json
{}
```

## AI Client Usage Examples

### Example 1: Basic Column Selection
```
User: "Get only the names and emails of users"

AI Workflow:
1. GetEntitySchema("User") ? Check field names (FirstName, LastName, EmailAddress)
2. GetEntityData("Get names and emails", "User", null, "FirstName,LastName,EmailAddress")
```

### Example 2: Column Selection with Filtering
```
User: "Show me IDs and login names of active users"

AI Workflow:
1. GetEntitySchema("User") ? Check fields (Id, LoginName, IsActive)
2. GetEntityData("Show active users", "User", "IsActive=true", "Id,LoginName")
```

### Example 3: Complex Selection and Filtering
```
User: "Get basic info for admin users whose login starts with Admin"

AI Workflow:
1. GetEntitySchema("User") ? Validate field names
2. GetEntityData("Get admin info", "User", "LoginName.StartsWith(\"Admin\")", "Id,LoginName,FirstName,LastName,IsActive")
```

### Example 4: Performance Optimization
```
User: "Get all user IDs for processing"

AI Workflow:
1. GetEntitySchema("User") ? Check ID field name
2. GetEntityData("Get all user IDs", "User", null, "Id")
```

## Error Handling and Recovery

### Column Name Errors
If the Select parameter contains invalid column names, the AI will get enhanced error messages:

```
? Error retrieving data from User: Invalid column 'Email' not found

?? RECOMMENDED ACTIONS:
1. Call GetEntitySchema("User") to see correct column names and data types
2. Check if the entity name is correct by calling GetAvailableEntities  
3. Retry GetEntityData with corrected column names and proper filter/select syntax

This error suggests there might be issues with column names, data types, filter syntax, or column selection.
```

### Recovery Workflow
```
1. GetEntitySchema("User") ? See correct field is "EmailAddress" not "Email"
2. GetEntityData(..., "User", null, "Id,FirstName,LastName,EmailAddress") ? Success
```

## Performance Benefits

### Before (All Columns)
```
GetEntityData("Get users", "User")
Returns: All 25+ columns for all users (large payload)
```

### After (Selected Columns)
```
GetEntityData("Get user names", "User", null, "FirstName,LastName")
Returns: Only 2 columns for all users (much smaller payload)
```

### Combined with Filtering
```
GetEntityData("Get active user names", "User", "IsActive=true", "FirstName,LastName")
Returns: Only 2 columns for filtered users (optimal payload)
```

## Best Practices for AI Client

### 1. Always Validate Column Names
- Call `GetEntitySchema` first when user mentions specific fields
- Use exact column names from schema (case-sensitive)
- Don't assume column name variations (e.g., "Email" vs "EmailAddress")

### 2. Optimize for User Intent
- **"Get names"** ? Select name-related columns only
- **"Show basic info"** ? Select essential fields (Id, Name, Status)
- **"List all"** ? May need all columns or ask user which fields they need

### 3. Error Recovery
- On column selection errors, always check schema first
- Retry with corrected column names
- Provide helpful suggestions to user about correct field names

### 4. Performance Considerations
- Use column selection for large entities to reduce data transfer
- Combine with filtering to minimize both rows and columns
- Select only fields needed for the specific use case

## Real-World Scenarios

### Scenario 1: User Management Dashboard
```
User: "Show me a list of all users with their basic info"
AI: GetEntityData("Show users", "User", null, "Id,FirstName,LastName,LoginName,IsActive,LastLoginDate")
```

### Scenario 2: Report Generation
```
User: "Generate a report of blocked users"
AI: GetEntityData("Get blocked users", "User", "IsLoginBlocked=true", "LoginName,FirstName,LastName,BlockedDate,BlockedReason")
```

### Scenario 3: Data Export
```
User: "Export user contact information"
AI: GetEntityData("Export contacts", "User", null, "FirstName,LastName,EmailAddress,PhoneNumber")
```

### Scenario 4: System Integration
```
User: "Get user IDs for synchronization"
AI: GetEntityData("Get sync IDs", "User", "LastModified>\"2025-01-01\"", "Id,LoginName,LastModified")
```

## Column Selection Quick Reference

| Use Case | Select Parameter | Description |
|----------|------------------|-------------|
| **Names Only** | `FirstName,LastName` | User names for display |
| **Basic Info** | `Id,FirstName,LastName,IsActive` | Essential user data |
| **Contact Info** | `FirstName,LastName,EmailAddress,PhoneNumber` | Contact details |
| **Login Data** | `Id,LoginName,IsActive,LastLoginDate` | Authentication info |
| **Security Info** | `LoginName,IsActive,IsLoginBlocked,FailedLoginAttempts` | Security status |
| **ID Only** | `Id` | For processing/syncing |
| **All Data** | (omit parameter) | Full entity data |

This column selection capability enables precise data retrieval, improving both performance and user experience! ??