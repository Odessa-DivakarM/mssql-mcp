# ApiExecutionTool Documentation

This document contains the full usage, workflow, and example documentation for the `ApiExecutionTool` class, extracted from the codebase. It covers filtering, column selection, sorting, pagination, error recovery, and hierarchical selection patterns. Please refer to this file for all usage and workflow details previously found in the class XML documentation.

---

[Full documentation content extracted from the class:]

## Overview
API execution tool for retrieving entity data with filtering, column selection, sorting, and pagination support.

### TRANSIENT ENTITY VALIDATION
The tool automatically validates that entities are persistent (Persistent="True") before attempting data retrieval. Entities marked with Persistent="False" in EntityTypes.xaml are transient and will be rejected with an appropriate error message.

### CRITICAL AI WORKFLOW FOR ERROR RECOVERY
1. If GetEntityData returns errors about columns, fields, or entity not found:
   - IMMEDIATELY call GetEntitySchema(entityName) to understand the correct structure
   - Use schema info to fix column names and data types
   - Retry GetEntityData with corrected parameters
2. If user mentions specific column names or has potential typos:
   - Call GetEntitySchema FIRST to validate column structure
   - Form correct filter conditions based on schema
   - Then call GetEntityData with validated filters
3. If unsure about entity names:
   - Call GetAvailableEntities() to see what entities exist
   - Call GetEntitySchema for the correct entity
   - Then call GetEntityData
4. If GetEntityData returns "Transient entity" error:
   - The entity is marked as Persistent="False" in EntityTypes.xaml
   - Use GetAvailableEntities to find persistent entities
   - Use GetEntitySchema to verify entity persistence status

### ERROR PATTERNS TO WATCH FOR
- "column not found" ? Call GetEntitySchema to see correct columns
- "invalid field" ? Call GetEntitySchema to validate field names
- "entity not found" ? Call GetAvailableEntities, then GetEntitySchema
- "Transient entity" ? Entity is Persistent="False", use GetAvailableEntities to find valid entities
- Filter syntax errors ? Call GetEntitySchema to check data types
- Select column errors ? Call GetEntitySchema to validate column names
- OrderBy column errors ? Call GetEntitySchema to validate column names for sorting

### SORTING SUPPORT
Control the sort order of results using the OrderBy parameter. Sorting is especially important for paginated results.

#### SORTING EXAMPLES
- User: "Get users ordered by last name" ? orderBy="LastName asc"
- User: "Show me the newest users first" ? orderBy="CreatedDate desc"
- User: "Get users sorted by ID descending, then by name ascending" ? orderBy="Id desc, FirstName asc"
- User: "Show me products ordered by price high to low" ? orderBy="Price desc"

#### SORTING WORKFLOW
1. Use GetEntitySchema FIRST to validate column names for OrderBy
2. Format as "ColumnName asc" or "ColumnName desc"
3. For multiple columns: "Column1 asc, Column2 desc, Column3 asc"
4. Essential for consistent pagination results across pages

### PAGINATION SUPPORT
The API returns data in pages (default: 100 records per page). Use pagination parameters to control data retrieval:

#### PAGINATION EXAMPLES
- User: "Get first 50 users" ? pageSize=50, pageIndex=1
- User: "Get users on page 2" ? pageIndex=2 (uses default pageSize=100)
- User: "Get ALL users regardless of pagination" ? fetchAllPages=true (automatically retrieves all pages)
- User: "Show me all products with status Active" ? filterConditions="Status=\"Active\"", fetchAllPages=true

#### PAGINATION WORKFLOW
1. If user wants ALL data ? set fetchAllPages=true
2. If user wants specific page/size ? set pageSize and pageIndex
3. If pagination info shows more data available ? guide user to use pagination or fetchAllPages
4. Default behavior: returns first 100 records with pagination info in response

### ADVANCED STRING FILTERING EXAMPLES
- User: "Get users whose login starts with Admin" ? Filter: "LoginName.StartsWith(\"Admin\")"
- User: "Find users whose login ends with .Admin" ? Filter: "LoginName.EndsWith(\".Admin\")"
- User: "Get users with specific logins User01 or User02" ? Filter: "(\"User01,User02\").Contains(LoginName)"
- User: "Get active users whose name is not Security.Admin" ? Filter: "Status=\"Active\" && LoginName!=\"Security.Admin\""

### ENUM FILTERING EXAMPLES (for fields ending with 'Values' suffix)
IMPORTANT: Use GetEntitySchema first to identify enum fields, then format filters correctly:
- User: "Get users with Admin permission" ? Filter: "DefaultPermissionValues.Value=\"Admin\""
- User: "Find users whose permission is not Guest" ? Filter: "DefaultPermissionValues.Value!=\"Guest\""
- User: "Get users whose permission starts with Admin" ? Filter: "DefaultPermissionValues.Value.StartsWith(\"Admin\")"
- User: "Find users with specific permissions Admin or User" ? Filter: "(\"Admin,User\").Contains(DefaultPermissionValues.Value)"

### REFERENCE FILTERING EXAMPLES
Filter by attributes of related entities using reference relationships.
- User: "Get users with Admin role" ? Filter: "RolesForUsers.Role.Name=\"Admin\""
- User: "Find assets in Default Portfolio" ? Filter: "Type.Portfolio.Name=\"Default Portfolio\""
- User: "Get assets assigned to Warehouse A location" ? Filter: "AssetLocations.Location.Name=\"Warehouse A\""
- User: "Find users assigned to legal entity LE001" ? Filter: "LegalEntitiesForUsers.LegalEntityRef.Code=\"LE001\""
- User: "Get scrap assets in Default Portfolio with quantity 1" ? Filter: "Status.Value=\"Scrap\" && Quantity=1 && Type.Portfolio.Name=\"Default Portfolio\""

### COLUMN SELECTION EXAMPLES
- User: "Get only the names and emails of users" ? Select: "FirstName,LastName,EmailAddress"
- User: "Show me user IDs and login names for active users" ? Select: "Id,LoginName" + Filter: "IsActive=true"
- User: "Get basic user info - ID, name, and status" ? Select: "Id,FirstName,LastName,Status"

### HIERARCHICAL SELECTION EXAMPLES (Parent-Child Entities)
- User: "Get users with their email addresses" ? Select: "FirstName,LastName,UserEmailAddresses.{Email,IsPrimary}"
- User: "Get assets with their locations" ? Select: "Id,Status,AssetLocations.{LocationId,AssignedDate}"
- User: "Get portfolios with their parameters" ? Select: "Id,Name,PortfolioParameters.{ParameterName,ParameterValue}"

### REFERENCE ENTITY SELECTION EXAMPLES
- User: "Get users with their role names" ? Select: "LoginName,RolesForUsers.{Id,Role.Name}"
- User: "Get users with role information" ? Select: "FirstName,LastName,Id,LoginName,IsWindowsAuthenticated,IsLoginBlocked,RolesForUsers.{Id,Role.Name}"
- User: "Get assets with location and portfolio details" ? Select: "Status,Id,Quantity,AssetLocations.{LocationId,Location.Id},Type.Portfolio.Id,Type.Portfolio.Name"

### REFERENCE CHAINING EXAMPLES
- User: "Get legal entities for users with entity details" ? Select: "LoginName,LegalEntitiesForUsers.{ActivationDate,LegalEntityRef.Name,LegalEntityRef.Code}"
- User: "Get assets with detailed type and portfolio information" ? Select: "Id,Name,Type.Portfolio.Name,Type.Portfolio.Owner.Name"

### HIERARCHICAL SELECTION RULES
1. Query the PARENT entity (e.g., User, Asset, Portfolio)
2. Use PLURAL form of child entity name in selection (e.g., UserEmailAddresses, AssetLocations)
3. Use dot notation with curly braces for child entities: "ChildEntities.{attr1,attr2,attr3}"
4. Use dot notation without curly braces for references: "ReferenceName.Attribute"
5. Chain references with additional dots: "ReferenceName.AnotherReference.Attribute"
6. Combine child entities and references: "ChildEntities.{attr1,ReferenceName.attr2}"
7. All attributes and references must exist in the entity schema

### RELATIONSHIP TYPES
• OneToMany: Parent can have multiple children (e.g., User ? UserEmailAddresses)
• OneToOneOptional: Parent may have 0 or 1 child (e.g., User ? UserProfile)
• OneToOneMandatory: Parent must have exactly 1 child (e.g., User ? UserSecurity)
• Reference: Entity refers to another entity via foreign key (e.g., LegalEntitiesForUser.LegalEntityRef ? LegalEntity)

### COMBINED EXAMPLES (Filtering + Sorting + Selection + Pagination)
- User: "Get active users ordered by creation date, show only names and emails, first 20 records"
  Parameters: filterConditions="IsActive=true", orderBy="CreatedDate desc", selectColumns="FirstName,LastName,EmailAddress", pageSize=20, pageIndex=1
- User: "Show me all users with Admin role, sorted by last name, get all pages"
  Parameters: filterConditions="DefaultPermissionValues.Value=\"Admin\"", orderBy="LastName asc, FirstName asc", fetchAllPages=true

### HIERARCHICAL COMBINED EXAMPLES
- User: "Get active users with their email addresses, ordered by name, first 10 records"
  Parameters: filterConditions="IsActive=true", selectColumns="FirstName,LastName,UserEmailAddresses.{Email,IsPrimary}", orderBy="LastName asc, FirstName asc", pageSize=10, pageIndex=1
- User: "Get scrap assets with their locations and quantities"
  Parameters: filterConditions="Status.Value=\"Scrap\" && Quantity=1", selectColumns="Status,Id,Quantity,AssetLocations.{LocationId}", fetchAllPages=true
- User: "Get users with admin roles and their legal entity assignments"
  Parameters: filterConditions="RolesForUsers.Role.Name.StartsWith(\"Admin\")", selectColumns="LoginName,FirstName,LastName,RolesForUsers.{Id,Role.Name},LegalEntitiesForUsers.{ActivationDate,LegalEntityRef.Name}", orderBy="LastName asc", fetchAllPages=true
- User: "Get assets with their portfolio and location information"
  Parameters: filterConditions="Type.Portfolio.Name!=\"\"", selectColumns="Id,Status,Type.Portfolio.Name,Type.Portfolio.Owner.Name,AssetLocations.{LocationId,Location.Name}", orderBy="Type.Portfolio.Name asc", pageSize=50
- User: "Get scrap assets in specific portfolio with location details"
  Parameters: filterConditions="Status.Value=\"Scrap\" && Quantity=1 && Type.Portfolio.Name=\"Default Portfolio\"", selectColumns="Status,Id,Quantity,AssetLocations.{LocationId,Location.Id},Type.Portfolio.Id,Type.Portfolio.Name", orderBy="Id asc", fetchAllPages=true
- User: "Find users with specific roles assigned to certain legal entities"
  Parameters: filterConditions="RolesForUsers.Role.Name=\"Manager\" && LegalEntitiesForUsers.LegalEntityRef.Code.StartsWith(\"LE\")", selectColumns="LoginName,FirstName,LastName,RolesForUsers.{Role.Name},LegalEntitiesForUsers.{LegalEntityRef.Name,LegalEntityRef.Code}", orderBy="LastName asc, FirstName asc"

### EXAMPLES
- User: "Get users where username is John and age > 25"
  ERROR SCENARIO: GetEntityData fails with "column 'username' not found"
  RECOVERY: 1) GetEntitySchema("User") ? see actual column is "Username" 2) GetEntityData("Get users...", "User", "Username=\"John\" && Age>25")
- User: "Show me products with high priority"
  PROACTIVE: 1) GetEntitySchema("Product") ? understand priority field structure 2) GetEntityData("Show products...", "Product", "Priority=\"High\"") or "Priority=1"
- User: "Find users whose email starts with admin"
  PROACTIVE: 1) GetEntitySchema("User") ? see email field is "EmailAddress" 2) GetEntityData("Find users...", "User", "EmailAddress.StartsWith(\"admin\")")
- User: "Get only names of active users"
  PROACTIVE: 1) GetEntitySchema("User") ? see name fields are "FirstName", "LastName", status field is "IsActive" 2) GetEntityData("Get names...", "User", "IsActive=true", "FirstName,LastName")
- User: "Get users with their role information"
  PROACTIVE: 1) GetEntitySchema("User") ? see child "RolesForUsers" with reference "Role" 2) GetEntityData("Get users with roles", "User", selectColumns: "LoginName,RolesForUsers.{Id,Role.Name}")
- User: "Get assets with portfolio owner details"
  PROACTIVE: 1) GetEntitySchema("Asset") ? see reference chain "Type.Portfolio.Owner" 2) GetEntityData("Get assets with portfolio owners", "Asset", selectColumns: "Id,Name,Type.Portfolio.Owner.Name")
- User: "Get legal entity assignments for users"
  PROACTIVE: 1) GetEntitySchema("User") ? see child "LegalEntitiesForUsers" with reference "LegalEntityRef" 2) GetEntityData("Get user legal entities", "User", selectColumns: "LoginName,LegalEntitiesForUsers.{ActivationDate,LegalEntityRef.Name,LegalEntityRef.Code}")
- User: "Find assets in a specific portfolio"
  PROACTIVE: 1) GetEntitySchema("Asset") ? see reference chain "Type.Portfolio" 2) GetEntityData("Find portfolio assets", "Asset", filterConditions: "Type.Portfolio.Name=\"Default Portfolio\"", selectColumns: "Id,Status,Type.Portfolio.Name")
- User: "Get users with Admin role"
  PROACTIVE: 1) GetEntitySchema("User") ? see child "RolesForUsers" with reference "Role" 2) GetEntityData("Get admin users", "User", filterConditions: "RolesForUsers.Role.Name=\"Admin\"", selectColumns: "LoginName,RolesForUsers.{Role.Name}")
- User: "Show me scrap assets with location details"
  PROACTIVE: 1) GetEntitySchema("Asset") ? see enum "Status" and child "AssetLocations" with reference "Location" 2) GetEntityData("Get scrap assets with locations", "Asset", filterConditions: "Status.Value=\"Scrap\"", selectColumns: "Id,Status,AssetLocations.{LocationId,Location.Name}")
- User: "Get all users (there might be thousands)"
  SOLUTION: GetEntityData("Get all users", "User", fetchAllPages: true) ? retrieves all pages automatically
- User: "Get users ordered by newest first"
  PROACTIVE: 1) GetEntitySchema("User") ? see date field is "CreatedDate" 2) GetEntityData("Get newest users", "User", orderBy: "CreatedDate desc")

---

(See the code for essential XML summaries only.)
