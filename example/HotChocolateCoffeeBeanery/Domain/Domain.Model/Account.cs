using CoffeeBeanery.GraphQL.Configuration;

namespace Domain.Model;

public class Account
{
    public Guid? AccountKey { get; set; }

    public string? AccountNumber { get; set; }

    public string? AccountName { get; set; }
    
    [LinkBusinessKey("Transaction","TransactionId")]
    public Transaction? Transaction { get; set; }
    
    [LinkBusinessKey("Contract","ContractId")]
    public Contract? Contract { get; set; }
}