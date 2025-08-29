using System.ComponentModel.DataAnnotations.Schema;
using Database.Entity;
using Domain.Util.GraphQL.Configuration;
using HotChocolate;

namespace Domain.Model;

public class Contract : Process
{
    [BusinessKey]
    public Guid ContractKey { get; set; }

    public ContractType? ContractType { get; set; }

    public decimal? Amount { get; set; }

    public Guid? AccountKey { get; set; }

    public Guid? CustomerBankingRelationshipKey { get; set; }

    // [NotMapped]
    // [GraphQLIgnore]
    // public CustomerBankingRelationship? CustomerBankingRelationship { get; set; }

    // [NotMapped, 
    [JoinKey]
    public int? CustomerBankingRelationshipId { get; set; }
        
    // [NotMapped]
    // [GraphQLIgnore]
    // public List<Transaction>? Transaction { get; set; }

    [NotMapped, BusinessSchema, GraphQLIgnore]
    public Schema Schema { get; set; } =  Schema.Lending;
}
