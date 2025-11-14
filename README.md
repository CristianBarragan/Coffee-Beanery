### Coffee Beanery

Coffee beanery library is a dynamic parser from GraphQL queries into raw SQL queries; this library will not be shipped as a nuget package since optimization and changes are case by case; this example is using:

- Dapper
- Hot Chocolate
- Automapper
- Entity Framework
- PostgreSQL
- FasterKV

Coffee Beanery provides the following features and can be fully customized

## Current Features

- Configuration based and faster development
- No N+1 problem since the entire query/mutation is batched and materialized by the database engine
- Complex domain models
- Business service logic within the GraphQL API project
- Custom mapping between data entities and domain models
- Subgraph mutations and queries
- Data annotation based configuration (Data entities and domain models)
- Leverage to a mapping framework the mapping between data entities and domain models
- Leverage generics to generate the column names based on the data entities
- Can be customized and integrate with multiple GraphQL framework/libraries and databases
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

- Owner of the relationship (Parent)

`[LinkKey("Account","AccountKey")]`

`[JoinKey("Contract","Id")]`

`public Account? Account { get; set; }`

- Related of the relationship (child)

`[LinkKey("Contract","ContractKey")]`

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

- Customer mutation with query test without any cache level

10 iterations
5 datasets each with random data

Assertions

- CustomerKey 
- CustomerType

Result

<img src="example/HotChocolateCoffeeBeanery/Test/Customer-mutation-test-result.png" alt="Customer_Test" height="60%" width="100%">

Request template

```json
mutation a {
  wrapper(
    wrapper: {
      customer:
        {
        customerKey: "{{CustomerKey1}}"
        customerType: PERSON
        firstNaming: "{{FirstNaming1}}"
        fullNaming: "{{FullNaming1}}"
        lastNaming: "{{LastNaming1}}"
        product: {
          accountKey: "{{AccountKey1}}"
          accountName: "123AN"
          accountNumber: "321AN"
          amount: 100
          balance: 1200
          contractKey: "{{ContractKey1}}"
          productType: CREDIT_CARD
          customerKey: "{{CustomerKey1}}"
          customerBankingRelationshipKey: "{{CustomerBankingRelationshipKey1}}"
          contract: {
            transaction: {
              amount: 12
              accountKey: "{{AccountKey1}}"
              contractKey: "{{ContractKey1}}"
              transactionKey: "{{TransactionKey1}}"
              balance: 1500
            }
            contractKey: "{{ContractKey1}}"
            customerBankingRelationshipKey: "{{CustomerBankingRelationshipKey1}}"
          }
          customerBankingRelationship: {
            contract: {
              accountKey: "{{AccountKey1}}"
              amount: 12
              transaction: {
                amount: 12
                accountKey: "{{AccountKey1}}"
                contractKey: "{{ContractKey1}}"
                transactionKey: "{{TransactionKey1}}"
              }
              customerBankingRelationshipKey: "{{CustomerBankingRelationshipKey1}}"
              contractKey: "{{ContractKey1}}"
            }
            customerBankingRelationshipKey: "{{CustomerBankingRelationshipKey1}}"
            customerKey: "{{CustomerKey1}}"
          }
        }
      }
      cacheKey: "2c0c7698-465f-4fbb-a8c1-9614f7ec6c05"
    }
    where: { customerKey: { eq: "{{CustomerKey1}}" } }
  ) {
    edges {
      node {
        customerKey
        customerType
        firstNaming
        fullNaming
        lastNaming
      }
    }
  }
}
```

Response sample

```json
{
    "data": {
        "wrapper": {
            "edges": [
                {
                    "node": {
                        "customerKey": "2fc26db4-a55d-463c-be7b-b5ad0ec86352",
                        "customerType": "PERSON",
                        "firstNaming": "Maverick",
                        "fullNaming": "Philip Predovic II",
                        "lastNaming": "Beer"
                    }
                }
            ]
        }
    }
}
```

### [Buy me a Coffee ☕]
*I would love a 100% colombian coffee!*

<a href="https://www.buymeacoffee.com/cristianbarragan" target="_blank">
<img src="https://cdn.buymeacoffee.com/buttons/default-orange.png" alt="Buy Me A Coffee" height="41" width="174">
</a>
