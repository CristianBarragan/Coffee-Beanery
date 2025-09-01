### Coffee beaner

<img src="https://github.com/CristianBarragan/CoffeeBean/blob/main/CoffeeBean/CoffeeBeanLogo.jpg" alt="100% Colombian Coffee" height="80" width="52">

Coffee beaner library is a dynamic parser from GraphQL queries into raw SQL queries; this library will not be shipped in a nuget package since optimization and changes are case by case; this example is using:

- Dapper
- Hot Chocolate
- Automapper
- Entity Framework
- PostgreSQL
- FasterKV

Coffee Beaner provides the following features and can be fully customized

## Current Features

- Configuration based and faster development
- No N+1 problem since the entire query/mutation is batched and materialized by the database engine
- Framework agnostic
- Allows business service logic within the GraphQL API project
- Allows custom mapping between Entity models and Data models
- Supports subgraph mutations and queries
- Data annotation based configuration (Data models and Entity models)
- Leverage to a mapping framework the mapping between Data models and Entity models
- Leverage generics to generate the column names based on the data entities
- Supports any GraphQL framework or vanilla .NET API, since it is not tightly couple to a vendor
- Nodes (Left joins between entities)
- Edges (Joins between entities)
- Paging
- Filtering
- Sorting


## Customizable Features

- Granular access by table/columns based on token-claims
- Data and column validations
- Query cache can be customized in multiple layers
- Query result handling can be fully customized
	
### Queries

Even though the query cannot be directly converted and translated during the tree processing, the library supports multiple caching and techniques; since queries need to be processed as a single statement for the entire tree

 Support of multiple cache levels

	1. key: main node string
	   Value: Calculated query
	2. Where and Pagination are decoupled of the query, allowing a better cache plans
	3. Query Result data: based on the requirements, the returning data result can be upserted and save into the cache

### Mutations

Mutations are not cached as they are directly converted and translated during the tree processing, since the upserts do not require to be combined within a single statement. Also, mutations are tightly couple with data so, there is not to much gain to cache the queries with specific values
	
	1. Returning query cache techniques can be used in mutation result

## Future Features

- Add support to other database providers

## Setup

### Database.Entity 
Contains plain ef core entity models where a property schema and id are required for joining purposes.

### Domain.Model 
Contains the custom model and attributes which will be exposed through the API. 

- It does not need to map each data model property and naming
- The EF List/Entity one direction link is required for generating each entity tree
- Any property can be hidden to the GraphQL API
- BusinessKey data annotation is required to generate the tree graphs, column mappings, and mappings between Entity models and Data models
- BusinessSchema data annotation is required to register the schema which will be used by the table/columnn; bringing flexibility if there is a need to scale or split the GraphQL API into different database logic structures
- JoinKey data annotation is required for foreign keys and join keys
- Uses a wrapper entity model to allow multiple root entity upserts

### Domain.Shared

- Contains main root entity query handler where customer logic and mapping can be applied after SQL statement execution
- Requires a basic mapping structure framework agnostic to translate column mappings between Entity models and Data models during the SQL query generation
- Further custom mapping can be added for root entity query handler
- Service Collection extension for adding the root entity query handler

### Api

- Contains basic setup for GraphQL API
- Supports any framework since it is not tightly couple to a vendor

  
[Buy me a Coffee ☕]([https://www.buymeacoffee.com/cristianbarragan]
