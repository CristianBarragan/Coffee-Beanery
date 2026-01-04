## Coffee Beanery

#### “Why Dapper with 
#### “Graphql + C# + Dapper Example”
#### “Coffee Beanery + Entity Framework GraphQL”
#### “GraphQL Dapper C# 
#### “HotChocolate + Dapper Example”
#### “Automapper + Dapper Example”

Coffee beanery is a dynamic parser from GraphQL queries into raw SQL queries; the translation happens on the fly and all the features are available out of the box. 

It only requires mappings between models and entities and a few annotations to signal the framework about the relationship between models or entities.

Also, the feature to have the means for using business transactions and add custom business code within the api. Makes a unique opportunity to do any integration possible.

Running example

1. Clone repository
2. Run entity framework migrations
3. Compile and run api project
3. Use nitro IDE to create any type of graphql operation.
4. Validate data persistance and query result.

The following libraries are used to achieve all the features listed below:

- Dapper
- Hot Chocolate
- Automapper
- Entity Framework
- PostgreSQL
- FasterKV

## Current Features

- Configuration based for faster development.
- No N+1 problem since the entire query/mutation is batched and materialized by the database engine
- Complex domain models
- Hability to add any additional business logic or integration within the GraphQL API project.
- Custom and complex mapping between data entities and domain models
- Allows subgraph mutations and queries using the same endpoint and wrapper object.
- Data annotation based for creating relationships between Data entities and domain models.
- Leverage to a mapping framework the mapping between data entities and domain models
- Leverage generics to generate the column names based on the data entities.
- Can be customized and integrate with multiple GraphQL framework / libraries and databases.
- Node types are translated into Left joins between entities.
- Edge types are translated into joins between entities.
- Paging support
- Filtering support
- Sorting support

## Customizable Features

- Granular access by table/columns based on token-claims
- Data and column validations
- Query cache can be customized in multiple layers
- Query result handling can be fully customized

## Setup

### Database entity Setup

There are 3 types of annotations

#### Upsert
Field that will be used to upsert the record

First argument table, Second argument database schema

`[UpsertKey("Transaction","Lending")]`

`public Guid TransactionKey { get; set; }`

#### JoinKey
Foreign key to join the child with parent

Note: all joins are done via Ids

##### For one to many relationship:

- Transaction table (Child)

No setup required

- Account table Parent

First argument parent table, Second argument parent property to be joined

`[LinkKey("Transaction","TransactionKey")]`

`[JoinKey("Account","Id")]`

`public List<Transaction>? Transaction { get; set; }`

##### For one to one relationship

`[LinkKey("Contract","Id")]`

`[JoinOneKey("Account","Id")]`

`public Contract? Contract { get; set; }`

### Business Model setup

First argument child table, Second argument child property to be joined (mapped)

`[LinkBusinessKey("Transaction","TransactionKey")]`

`public List<Transaction>? Transaction { get; set; }`
	
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
- Allow complex joins between properties (currently supports Id columns)

## Setup

### Database.Entity 
Contains plain ef core entity models where a property schema and id are required for joining purposes.

### CoffeeBeanery
Contains all util helpers and core implementations

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

## Tests

<img src="https://github.com/CristianBarragan/Coffee-Beanery/blob/main/example/HotChocolateCoffeeBeanery/Test/Test_Results.png" alt="Test_Results" height="60%" width="100%">

## Multiple architecture options

## Stitching
Stitching frameworks like Apollo of Fusion can be used to integrate multiple graphQL api. 

keeping cache can be challenging specially when multiple process are happening in the background.

From the example viewpoint each database schema can be split into microservices. And then stitching each context together

## Database Replication

A readonly replica can be used to join every context together. And then make queries against the replica without N+1 problems.

## Hybrid

To take advantage of each approach. A mix can be used. Stitching for mutations and replica for querying.

### [Buy me a Coffee ☕]
*I would love a 100% colombian coffee!*

<a href="https://www.buymeacoffee.com/cristianbarragan" target="_blank">
<img src="https://cdn.buymeacoffee.com/buttons/default-orange.png" alt="Buy Me A Coffee" height="41" width="174">
</a>
