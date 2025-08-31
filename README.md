### Coffee bean

<img src="https://github.com/CristianBarragan/CoffeeBean/blob/main/CoffeeBean/CoffeeBeanLogo.jpg" alt="100% Colombian Coffee" height="80" width="52">

Coffee bean library is a dynamic parser from graphQL queries into SQL queries; this library example is using:

- Dapper
- Hot Chocolate
- Automapper
- Entity Framework
- PostgreSQL

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
- Manages mutations for any generic objects
- Paging
- Filtering
- Sorting * Currently sorting has a limitation on advance sorting due to missing out of the box Hot Chocolate implementation


## Future Features

- Granular access by table/columns based on token-claims
- Add support to other database providers
- Query cache
- Data and column validations

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

- Contains basic setup for graphQL API
- Supports any framework since it is not tightly couple to a vendor

  
[Buy me a Coffee ☕]([https://www.buymeacoffee.com/cristianbarragan]