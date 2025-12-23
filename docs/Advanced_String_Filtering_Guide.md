# Advanced String Filtering Guide for MCP AI Client

## Overview
The `GetEntityData` tool now supports advanced string filtering operations beyond basic equality. This guide shows the AI client how to construct proper filter conditions for various string operations.

## String Filter Syntax

### 1. Basic String Operations

#### Equals (Exact Match)
```
Filter: LoginName="Security.Admin"
Usage: Find records where LoginName exactly matches "Security.Admin"
```

#### Not Equals
```
Filter: LoginName!="Security.Admin"
Usage: Find records where LoginName is not "Security.Admin"
```

### 2. Pattern Matching Operations

#### StartsWith (Prefix Matching)
```
Filter: LoginName.StartsWith("Admin")
Usage: Find records where LoginName begins with "Admin"
Examples: Matches "Admin", "Administrator", "AdminUser", etc.
```

#### EndsWith (Suffix Matching)
```
Filter: LoginName.EndsWith(".Admin")
Usage: Find records where LoginName ends with ".Admin"
Examples: Matches "Security.Admin", "System.Admin", etc.
```

### 3. List-Based Operations

#### Contains (Value in List)
```
Filter: ("User01,User02").Contains(LoginName)
Usage: Find records where LoginName is one of the specified values
Note: This checks if the LoginName field value exists in the comma-separated list
```

### 4. Combining String Filters

#### Multiple String Conditions (AND)
```
Filter: LoginName.StartsWith("Admin") && Status="Active"
Usage: Find active records with LoginName starting with "Admin"
```

#### Multiple String Conditions (OR)
```
Filter: LoginName.EndsWith(".Admin") || LoginName.StartsWith("Root")
Usage: Find records where LoginName ends with ".Admin" OR starts with "Root"
```

#### Complex Combinations
```
Filter: (Status="Active" || Status="Pending") && LoginName.StartsWith("User")
Usage: Find records with Status Active or Pending AND LoginName starting with "User"
```

## Practical AI Usage Examples

### Example 1: User Query with StartsWith
```
User: "Find all users whose email starts with admin"

AI Workflow:
1. GetEntitySchema("User") ? Check email field name (e.g., "EmailAddress")
2. GetEntityData("Find users...", "User", "EmailAddress.StartsWith(\"admin\")")
```

### Example 2: User Query with EndsWith
```
User: "Get all accounts ending with .test"

AI Workflow:
1. GetEntitySchema("Account") ? Check account field name (e.g., "AccountName")
2. GetEntityData("Get accounts...", "Account", "AccountName.EndsWith(\".test\")")
```

### Example 3: User Query with Specific Values
```
User: "Show me data for users John, Mary, and Bob"

AI Workflow:
1. GetEntitySchema("User") ? Check name field (e.g., "UserName")
2. GetEntityData("Show users...", "User", "(\"John,Mary,Bob\").Contains(UserName)")
```

### Example 4: User Query with Exclusion
```
User: "Get all active users except system accounts"

AI Workflow:
1. GetEntitySchema("User") ? Check fields: "Status", "UserType"
2. GetEntityData("Get users...", "User", "Status=\"Active\" && UserType!=\"System\"")
```

### Example 5: Complex Mixed Conditions
```
User: "Find active or pending orders from customers whose name starts with Tech"

AI Workflow:
1. GetEntitySchema("Order") ? Check status field
2. GetEntitySchema("Customer") ? Check customer name field and relationship
3. GetEntityData("Find orders...", "Order", "(Status=\"Active\" || Status=\"Pending\") && CustomerName.StartsWith(\"Tech\")")
```

## Important Notes for AI Client

### 1. String Quoting Rules
- **Always** use double quotes for string literals: `"value"`
- **Escape** double quotes in values: `"Say \"Hello\""`
- **Case sensitivity** depends on the database - check schema for guidance

### 2. Field Name Validation
- **Always** call `GetEntitySchema` first when user mentions specific field operations
- **Validate** field names exist before constructing filters
- **Use** exact case-sensitive field names from schema

### 3. Error Handling
- If filter fails, call `GetEntitySchema` to check field names and types
- Common errors: wrong field name, incorrect syntax, unsupported operations
- Always retry with corrected syntax based on schema information

### 4. Performance Considerations
- `StartsWith` operations are generally efficient with proper indexing
- `EndsWith` operations may be slower on large datasets
- `Contains` with large lists should be used judiciously

## Quick Reference

| Operation | Syntax | Example |
|-----------|--------|---------|
| Equals | `Field="value"` | `Name="John"` |
| Not Equals | `Field!="value"` | `Status!="Inactive"` |
| Starts With | `Field.StartsWith("prefix")` | `Email.StartsWith("admin")` |
| Ends With | `Field.EndsWith("suffix")` | `Domain.EndsWith(".com")` |
| Value in List | `("val1,val2").Contains(Field)` | `("A,B,C").Contains(Grade)` |
| AND | `condition1 && condition2` | `Status="Active" && Age>21` |
| OR | `condition1 \|\| condition2` | `Type="Admin" \|\| Type="Super"` |

This enhanced filtering capability enables sophisticated data queries while maintaining simplicity for the AI client to construct proper filter expressions! ??