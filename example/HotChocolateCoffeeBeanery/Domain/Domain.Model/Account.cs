using CoffeeBeanery.GraphQL.Configuration;

namespace Domain.Model;

public class Account
{
    [UpsertKey("Account", "Account")]
    public Guid? AccountKey { get; set; }

    public string? AccountNumber { get; set; }

    public string? AccountName { get; set; }

    [JoinKey("Product", "Id")]
    public Guid? ProductKey { get; set; }
    
    [JoinKey("Contract", "Id")]
    public Guid ContractKey { get; set; }
    
    [LinkKey("Transaction", "Id")]
    public List<Transaction>? Transaction { get; set; }
}