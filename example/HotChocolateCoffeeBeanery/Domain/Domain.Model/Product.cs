using CoffeeBeanery.GraphQL.Configuration;
using Database.Entity;
using HotChocolate;

namespace Domain.Model;

public class Product
{
    [UpsertKey("Product","Customer")]
    public Guid? ProductKey { get; set; }
    
    [JoinKey("CustomerBankingRelationship", "Id")]
    public Guid? CustomerBankingRelationshipKey { get; set; }
    
    [LinkKey("Contract", "Id")]
    public Guid? ContractKey { get; set; }
    
    [JoinKey("Customer", "Id")]
    public Guid? CustomerKey { get; set; }
    
    [LinkKey("Account", "Id")]
    public Guid? AccountKey { get; set; }
    
    [LinkKey("Transaction", "Id")]
    public Guid? TransactionKey { get; set; }

    public string? AccountName { get; set; }
    
    public string? AccountNumber { get; set; }

    public decimal Amount { get; set; }

    public decimal Balance { get; set; }

    public ProductType ProductType { get; set; }

    [LinkKey("CustomerBankingRelationship", "Id")]
    public List<CustomerBankingRelationship>? CustomerBankingRelationship { get; set; }
    
    [LinkKey("Contract", "Id")]
    public List<Contract>? Contract { get; set; }

    [LinkKey("Account", "Id")]
    public List<Account>? Account { get; set; }
}

public enum ProductType
{
    CreditCard,
    Mortgage,
    PersonalLoan
}