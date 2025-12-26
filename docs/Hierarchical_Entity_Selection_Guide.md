# Hierarchical Entity Selection Guide

## Overview

The API.MCP system supports hierarchical entity relationships where child entities can be included in parent entity queries using dot notation. This allows retrieving related data in a single API call instead of making multiple requests.

## Entity Relationships

### Relationship Types

1. **OneToMany** - One parent can have multiple child records
   - Example: User ? UserEmailAddresses (one user can have multiple email addresses)
   - Example: Asset ? AssetLocations (one asset can be at multiple locations over time)

2. **OneToOneOptional** - One parent may have zero or one child record
   - Example: User ? UserProfile (user may or may not have an extended profile)

3. **OneToOneMandatory** - One parent must have exactly one child record
   - Example: User ? UserSecurity (every user must have security settings)

### Entity Definition Format

Child entities are defined in EntityTypes.xaml with parent relationship attributes:

```xml
<!-- Parent Entity -->
<Entity Name="User" Label="User" Persistent="True">
  <!-- User attributes -->
</Entity>

<!-- Child Entity -->
<Entity Name="UserEmailAddress" Label="User Email Address" 
        ParentEntity="User" ParentRelation="OneToMany" Persistent="True">
  <Entity.Attributes>
    <Attribute Name="Email" Type="Text" Nullable="false" />
    <Attribute Name="IsPrimary" Type="Boolean" Nullable="false" />
  </Entity.Attributes>
</Entity>
```

## Hierarchical Selection Syntax

### Basic Syntax

```
ParentAttribute1,ParentAttribute2,ChildEntities.{ChildAttribute1,ChildAttribute2}
```

### Key Rules

1. **Query the Parent Entity**: Always make the API call to the parent entity endpoint
2. **Pluralize Child Entity Names**: Child entity names must be plural in selection
3. **Use Dot Notation**: Separate child entity and attributes with dot
4. **Use Curly Braces**: Enclose child attributes in curly braces `{}`

## Examples

### User with Email Addresses

```http
POST /api/Entity/User
{
  "Select": "FirstName,LastName,Id,LoginName,UserEmailAddresses.{Email,IsPrimary}"
}
```

**Response Structure:**
```json
{
  "data": [
    {
      "firstName": "John",
      "lastName": "Doe", 
      "id": 1,
      "loginName": "johndoe",
      "userEmailAddresses": [
        {
          "email": "john.doe@company.com",
          "isPrimary": true
        },
        {
          "email": "j.doe@personal.com", 
          "isPrimary": false
        }
      ]
    }
  ]
}
```

### Asset with Locations

```http
POST /api/Entity/Asset
{
  "Where": "Status.Value=\"Scrap\" && Quantity=1",
  "Select": "Status,Id,Quantity,AssetLocations.{LocationId,AssignedDate}"
}
```

### Portfolio with Parameters

```http
POST /api/Entity/Portfolio
{
  "Select": "Id,Name,Description,PortfolioParameters.{ParameterName,ParameterValue,Category}"
}
```

## AI Tool Usage Examples

### GetEntityData with Hierarchical Selection

```typescript
// User request: "Get users with their email addresses"
await GetEntityData(
  "Get users with their email addresses",
  "User", 
  null, // no filter
  "FirstName,LastName,UserEmailAddresses.{Email,IsPrimary}" // hierarchical selection
);

// User request: "Get active users with primary emails only"
await GetEntityData(
  "Get active users with primary emails",
  "User",
  "IsActive=true", // filter
  "FirstName,LastName,UserEmailAddresses.{Email,IsPrimary}" // hierarchical selection
);

// User request: "Get scrap assets with their current locations"
await GetEntityData(
  "Get scrap assets with locations", 
  "Asset",
  "Status.Value=\"Scrap\"", // enum filter
  "Id,Status,AssetLocations.{LocationId,AssignedDate}" // hierarchical selection
);
```

## Schema Discovery for Hierarchical Queries

### Finding Child Entities

Use `GetEntitySchema` to discover parent-child relationships:

```typescript
// Check if User has child entities
const userSchema = await GetEntitySchema("User");
// Response will show if this entity has children or is a child itself

// Check child entity structure  
const emailSchema = await GetEntitySchema("UserEmailAddress");
// Response will show:
// - Parent Entity: User
// - Parent Relation: OneToMany
// - Available attributes for hierarchical selection
```

### Schema Response for Child Entity

```
? Entity Schema: UserEmailAddress

Parent Entity: User
Parent Relation: OneToMany
?? This is a child entity. Query via parent entity using: User
?? Use in hierarchical selection as: UserEmailAddresses.{attribute1,attribute2}

Persistent: True
Securable: True

?? Attributes:
? Email                   ? Text           ? No       ? User email address             ?
? IsPrimary               ? Boolean        ? No       ? Primary email indicator        ?
? UserId                  ? Int            ? No       ? Reference to parent user       ?
```

## Common Patterns

### 1. User Information with Related Data

```javascript
// Basic user info with emails
"FirstName,LastName,UserEmailAddresses.{Email,IsPrimary}"

// User with security info
"LoginName,IsActive,UserSecurity.{LastPasswordChange,LoginAttempts}"

// User with profile details
"FirstName,LastName,UserProfile.{Department,JobTitle,PhoneNumber}"
```

### 2. Asset Management

```javascript
// Asset with current location
"Id,Name,Status,AssetLocations.{LocationId,AssignedDate}"

// Asset with maintenance history
"Id,Name,AssetMaintenances.{MaintenanceDate,MaintenanceType,Cost}"

// Asset with depreciation info
"Id,PurchasePrice,AssetDepreciations.{DepreciationDate,BookValue}"
```

### 3. Portfolio Management

```javascript
// Portfolio with parameters
"Id,Name,PortfolioParameters.{ParameterName,ParameterValue}"

// Portfolio with holdings
"Name,TotalValue,PortfolioHoldings.{SecurityId,Quantity,MarketValue}"
```

## Filtering with Hierarchical Data

### Parent Entity Filters

Filter on parent entity attributes as usual:

```http
POST /api/Entity/User
{
  "Where": "IsActive=true && Department=\"Engineering\"",
  "Select": "FirstName,LastName,UserEmailAddresses.{Email,IsPrimary}"
}
```

### Child Entity Filters

Currently, filtering is applied at the parent level. Child data is included based on the parent filter results. For child-specific filtering, you may need separate queries or API-level filtering capabilities.

## Error Handling

### Common Issues

1. **Incorrect Child Entity Name**
   ```
   Error: Column 'UserEmailAddress' not found
   Solution: Use plural form 'UserEmailAddresses'
   ```

2. **Invalid Child Attributes**
   ```
   Error: Column 'EmailAddr' not found in UserEmailAddress
   Solution: Use GetEntitySchema("UserEmailAddress") to see correct attributes
   ```

3. **Missing Curly Braces**
   ```
   Incorrect: "UserEmailAddresses.Email,IsPrimary"
   Correct: "UserEmailAddresses.{Email,IsPrimary}"
   ```

### AI Error Recovery Workflow

1. **Schema Validation First**
   ```typescript
   // If hierarchical selection fails, check parent schema
   const parentSchema = await GetEntitySchema("User");
   
   // Check child schema for correct attributes
   const childSchema = await GetEntitySchema("UserEmailAddress");
   ```

2. **Verify Relationship Structure**
   - Confirm parent-child relationship exists
   - Verify child entity has ParentEntity attribute
   - Check ParentRelation type (OneToMany, OneToOneOptional, OneToOneMandatory)

3. **Correct Selection Syntax**
   - Use singular parent entity name for API call
   - Use plural child entity name in selection
   - Ensure proper dot notation and curly braces

## Performance Considerations

### Benefits
- **Reduced API Calls**: Get related data in single request
- **Consistent Data**: All data retrieved in single transaction
- **Efficient Network Usage**: Less round-trip time

### Considerations  
- **Payload Size**: Including child data increases response size
- **Complex Queries**: May impact database performance
- **Pagination**: Child data included in parent pagination

### Best Practices

1. **Selective Child Attributes**: Only include needed child attributes
   ```javascript
   // Good: Only essential child attributes
   "UserEmailAddresses.{Email,IsPrimary}"
   
   // Avoid: All child attributes if not needed
   "UserEmailAddresses.{Email,IsPrimary,CreatedDate,ModifiedDate,IsVerified}"
   ```

2. **Consider Pagination**: Large datasets with hierarchical data may need careful pagination
   ```javascript
   // Use pagination for large parent datasets
   await GetEntityData("Get users with emails", "User", null, 
     "FirstName,LastName,UserEmailAddresses.{Email}", null, 50, 1);
   ```

3. **Filter Appropriately**: Use parent filters to reduce dataset size
   ```javascript
   // Filter parents first to reduce data volume
   await GetEntityData("Get active users with emails", "User", 
     "IsActive=true", "FirstName,LastName,UserEmailAddresses.{Email}");
   ```

This hierarchical selection capability significantly enhances the API's ability to retrieve related data efficiently while maintaining clear, structured responses.