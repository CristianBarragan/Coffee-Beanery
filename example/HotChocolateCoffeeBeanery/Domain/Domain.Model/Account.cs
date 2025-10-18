using CoffeeBeanery.GraphQL.Configuration;

namespace Domain.Model;

public class Account
{
    public Guid? AccountKey { get; set; }

    public string? AccountNumber { get; set; }

    public string? AccountName { get; set; }
    
    [LinkBusinessKey("Transaction","TransactionKey")]
    public List<Transaction>? Transaction { get; set; }
}