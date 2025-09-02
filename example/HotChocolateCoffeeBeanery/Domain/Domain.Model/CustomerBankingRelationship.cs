using System.ComponentModel.DataAnnotations.Schema;
using CoffeeBeanery.GraphQL.Configuration;
using HotChocolate;

namespace Domain.Model;

public class CustomerBankingRelationship : Process
{
    [BusinessKey]
    public Guid CustomerBankingRelationshipKey { get; set; }

    public Guid? CustomerKey { get; set; }
    
    public Guid? ContractKey { get; set; }

    [JoinKey]
    public int? CustomerId { get; set; }
        
    [GraphQLIgnore]
    public Customer? Customer { get; set; }
    
    public List<Contract>? Contract { get; set; }

    [NotMapped, BusinessSchema, GraphQLIgnore]
    public Schema Schema { get; set; } =  Schema.Banking;
}