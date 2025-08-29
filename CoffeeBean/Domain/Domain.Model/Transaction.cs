using System.ComponentModel.DataAnnotations.Schema;
using Domain.Util.GraphQL.Configuration;
using HotChocolate;

namespace Domain.Model;

public class Transaction : Process
{
    [BusinessKey]
    public Guid TransactionKey { get; set; }

    public decimal? Amount { get; set; }

    public decimal? Balance { get; set; }

    [JoinKey]
    public int? ContractId { get; set; }
        
    // [NotMapped]
    public Contract? Contract { get; set; }

    public int? AccountId { get; set; }
        
    // [NotMapped]
    public Account? Account { get; set; }

    [NotMapped, BusinessSchema, GraphQLIgnore]
    public Schema Schema { get; set; } =  Schema.Lending;
}