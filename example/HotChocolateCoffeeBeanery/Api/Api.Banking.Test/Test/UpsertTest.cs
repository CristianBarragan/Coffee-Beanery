
using Api.Banking.Extension;
using Api.Banking.Mutation;
using Api.Banking.Query;
using FluentAssertions;
using HotChocolate.Execution;
using HotChocolate.Types.Pagination;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Banking.Test.Test;

[TestClass]
public class UpsertTest
{
    private IRequestExecutor _requestExecutor = null!;
    private TestServices _services = null!;
    
    public UpsertTest(IServiceProvider serviceProvider)
    {
      _services = new TestServices(serviceProvider);
    }
  
    [Fact]
    public async Task SchemaChangeTest()
    {
        var schema = await _services.Executor.GetSchemaAsync(default);
        schema.ToString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task FetchAuthor()
    {
        await using var result = await _services.ExecuteRequestAsync(
            b => b.SetQuery(
                """
                mutation m {
                  [
                    customer(
                      first: 1
                      last: 10
                      customer: {
                        customerBankingRelationship: {
                          contractKey: "c2704d47-10ed-4110-875f-0eea5f99d623"
                          customerBankingRelationshipKey: "c2704d47-10ed-4111-875f-0eea5f99d622"
                          customerKey: "c2704d47-10ed-4110-875f-0eea5f99d622"
                        }
                        customerKey: "c2704d47-10ed-4110-875f-0eea5f99d622"
                        contactPoint: { contactPointKey: "c6d38e28-4f68-4060-b87e-b7732abcb9a0" }
                        firstNaming: "c2704d47-10ed-4110-875f-0eea5f99d622FirstName111"
                        fullNaming: "c2704d47-10ed-4110-875f-0eea5f99d622FullName222"
                        lastNaming: "c2704d47-10ed-4110-875f-0eea5f99d622LastName333"
                        customerType: ORGANISATION
                      }
                    order: { customerType: DESC },
                    customer(
                      first: 1
                      last: 10
                      customer: {
                        customerBankingRelationship: {
                          contractKey: "c2704d47-10ed-4110-875f-0eea5f99d624"
                          customerBankingRelationshipKey: "c2704d47-10ed-4111-875f-0eea5f99d624"
                          customerKey: "c2704d47-10ed-4110-875f-0eea5f99d624"
                        }
                        customerKey: "c2704d47-10ed-4110-875f-0eea5f99d624"
                        contactPoint: { contactPointKey: "c6d38e28-4f68-4060-b87e-b7732abcb9a4" }
                        firstNaming: "c2704d47-10ed-4110-875f-0eea5f99d622FirstName114"
                        fullNaming: "c2704d47-10ed-4110-875f-0eea5f99d622FullName224"
                        lastNaming: "c2704d47-10ed-4110-875f-0eea5f99d622LastName334"
                        customerType: ORGANISATION
                      }
                    order: { customerType: DESC }
                  ]
                  ) {
                    edges {
                      node {
                        fullNaming
                        lastNaming
                        customerBankingRelationship {
                          contractKey
                          customerBankingRelationshipKey
                          customerKey
                        }
                        contactPoint {
                          contactPointKey
                          contactPointValue
                          contactPointType
                        }
                        customerType
                        customerKey
                      }
                    }
                    totalCount
                  }
                }
                """));

        result.ToJson().Should().BeEquivalentTo
        (
            """
            {
              "data": {
                "customer": {
                  "edges": [
                    {
                      "node": {
                        "fullNaming": "c2704d47-10ed-4110-875f-0eea5f99d622FullName222",
                        "lastNaming": "c2704d47-10ed-4110-875f-0eea5f99d622LastName333",
                        "customerBankingRelationship": [
                          {
                            "contractKey": "c2704d47-10ed-4111-875f-0eea5f99d622",
                            "customerBankingRelationshipKey": "c2704d47-10ed-4110-875f-0eea5f99d623",
                            "customerKey": "c2704d47-10ed-4110-875f-0eea5f99d622"
                          }
                        ],
                        "contactPoint": [
                          {
                            "contactPointKey": "c6d38e28-4f68-4060-b87e-b7732abcb9a0",
                            "contactPointValue": null,
                            "contactPointType": null
                          }
                        ],
                        "customerType": "ORGANISATION",
                        "customerKey": "c2704d47-10ed-4110-875f-0eea5f99d622"
                      }
                    }
                  ],
                  "totalCount": 1
                }
              }
            }
            """);
    }
}