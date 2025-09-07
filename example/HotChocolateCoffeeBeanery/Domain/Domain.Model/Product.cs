using CoffeeBeanery.GraphQL.Configuration;
using Database.Entity;
using HotChocolate;

namespace Domain.Model;

public class Product
{
    [JoinKey("CustomerBankingRelationship")]
    public Guid? CustomerBankingRelationshipKey { get; set; }
    
    [JoinKey("Contract")]
    public Guid? ContractKey { get; set; }
    
    [JoinKey("Customer")]
    public Guid? CustomerKey { get; set; }
    
    [JoinKey("Account")]
    public Guid? AccountKey { get; set; }
    
    [JoinKey("Transaction")]
    public Guid? TransactionKey { get; set; }

    public string? AccountName { get; set; }
    
    public string? AccountNumber { get; set; }

    public decimal Amount { get; set; }

    public decimal Balance { get; set; }

    public ProductType ProductType { get; set; }

    [LinkKey("CustomerBankingRelationship")]
    public List<CustomerBankingRelationship>? CustomerBankingRelationship { get; set; }
    
    [LinkKey("Contract")]
    public List<Contract>? Contract { get; set; }

    [LinkKey("Account")]
    public List<Account>? Account { get; set; }
    
    // [LinkKey("Transaction"), GraphQLIgnore]
    // public List<Transaction>? Transaction { get; set; }
}

public enum ProductType
{
    CreditCard,
    Mortgage,
    PersonalLoan
}