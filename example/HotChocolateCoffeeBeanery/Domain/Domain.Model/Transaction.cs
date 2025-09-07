using CoffeeBeanery.GraphQL.Configuration;

namespace Domain.Model;

public class Transaction
{
    [UpsertKey("Transaction","Lending")]
    public Guid TransactionKey { get; set; }

    public decimal? Amount { get; set; }

    public decimal? Balance { get; set; }
    
    [JoinKey("Account")]
    public Guid? AccountKey { get; set; }
    
    [JoinKey("Contract")]
    public Guid? ContractKey { get; set; }
}