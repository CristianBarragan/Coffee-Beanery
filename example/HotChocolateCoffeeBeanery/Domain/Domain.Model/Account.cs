using CoffeeBeanery.GraphQL.Configuration;

namespace Domain.Model;

public class Account
{
    [UpsertKey("Account", "Account")]
    public Guid AccountKey { get; set; }

    public string? AccountNumber { get; set; }

    public string? AccountName { get; set; }
    
    [LinkKey("Transaction")]
    public List<Transaction>? Transaction { get; set; }
}