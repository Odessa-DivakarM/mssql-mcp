# AI Error Recovery Workflow for MCP Tools

## Overview
The MCP tools now provide enhanced error guidance to ensure the AI can automatically recover from common issues by using the schema tools appropriately.

## Error Recovery Scenarios

### 1. Column Name Errors

**Scenario**: User asks "Get users where usrname = 'John'"

**What Happens**:
```
AI GetEntityData("Get users where usrname = 'John'", "User", "usrname=\"John\"")
API Error: column 'usrname' not found

?? RECOMMENDED ACTIONS:
1. Call GetEntitySchema("User") to see correct column names and data types
2. Check if the entity name is correct by calling GetAvailableEntities  
3. Retry GetEntityData with corrected column names and proper filter syntax

This error suggests there might be issues with column names, data types, or filter syntax.
```

**AI Recovery**:
```
AI ? GetEntitySchema("User")
API ? ? Shows schema with correct column "Username"
AI ? GetEntityData("Get users where username = 'John'", "User", "Username=\"John\"")  
API ? ? Returns user data successfully
```

### 2. Entity Not Found Errors

**Scenario**: User asks "Show me all Products" but entity is called "Product"

**What Happens**:
```
AI ? GetEntityData("Show me all Products", "Products")
API ? ? Entity not found error

?? RECOMMENDED ACTIONS:
1. Call GetAvailableEntities to see what entities are available
2. Check for typos in entity name
3. Try GetEntitySchema with the correct entity name
```

**AI Recovery**:
```
AI ? GetAvailableEntities()
API ? ? Shows available entities including "Product"
AI ? GetEntitySchema("Product") 
API ? ? Shows Product schema
AI ? GetEntityData("Show me all Products", "Product")
API ? ? Returns product data successfully
```

### 3. Filter Syntax Errors

**Scenario**: User asks for complex filtering with wrong syntax

**What Happens**:
```
AI ? GetEntityData("Get active users over 25", "User", "active=true && age>25")
API ? ? Filter syntax error - unknown field 'active', 'age'

?? RECOMMENDED ACTIONS:
1. Call GetEntitySchema("User") to see correct column names and data types
2. Check if the entity name is correct by calling GetAvailableEntities
3. Retry GetEntityData with corrected column names and proper filter syntax
```

**AI Recovery**:
```
AI ? GetEntitySchema("User")
API ? ? Shows schema with fields "IsActive" (Boolean), "Age" (Integer)
AI ? GetEntityData("Get active users over 25", "User", "IsActive=true && Age>25")
API ? ? Returns filtered user data successfully  
```

## Key Enhancements

### 1. Enhanced Tool Descriptions
- **GetEntityData**: Explicit instructions to use schema tools when errors occur
- **GetEntitySchema**: Clear guidance about when to use for error recovery
- **GetAvailableEntities**: Better integration with the error recovery workflow

### 2. Smart Error Messages
- Detect error patterns (column, field, entity, filter issues)
- Provide specific recovery instructions
- Suggest exact tool calls with parameters

### 3. Proactive Schema Checking
- AI knows to check schema BEFORE creating complex filters
- Validates column names and data types upfront
- Prevents errors rather than just recovering from them

## Benefits

? **Automatic Recovery**: AI can fix most schema-related issues without user intervention  
? **Reduced Errors**: Proactive schema checking prevents many issues  
? **Better UX**: Users get correct results even with typos or wrong assumptions  
? **Self-Learning**: AI learns correct entity structures and reuses knowledge  
? **Robust Filtering**: Complex filters work reliably with proper validation  

## Example Complete Workflow

```
User: "Find all customers from New York with high priority orders"

1. AI ? GetEntitySchema("Customer") 
   ? Learns: "City" field (string), no "priority" field

2. AI ? GetEntitySchema("Order")
   ? Learns: "Priority" field (integer: 1=Low, 2=Med, 3=High), "CustomerId" field

3. AI ? GetEntityData("Find customers...", "Customer", "City=\"New York\"")
   ? Gets customers from NY

4. AI ? GetEntityData("Get high priority orders", "Order", "Priority=3")  
   ? Gets high priority orders

5. AI ? Combines results and presents to user
```

This workflow ensures reliable, error-free data retrieval with intelligent error recovery! ??