using System.ComponentModel.DataAnnotations.Schema;
using CoffeeBeanery.GraphQL.Configuration;
using Database.Entity;
using HotChocolate;

namespace Domain.Model;

public class Contract : Process
{
    [BusinessKey] public Guid ContractKey { get; set; }

    public ContractType? ContractType { get; set; }

    public decimal? Amount { get; set; }

    public Guid? AccountKey { get; set; }

    public Guid? CustomerBankingRelationshipKey { get; set; }

    [JoinKey] public int? CustomerBankingRelationshipId { get; set; }

    [NotMapped, BusinessSchema, GraphQLIgnore]
    public Schema Schema { get; set; } = Schema.Lending;
}