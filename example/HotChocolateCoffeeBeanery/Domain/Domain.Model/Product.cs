using CoffeeBeanery.GraphQL.Configuration;

namespace Domain.Model;

public class Product
{

    public Guid? CustomerBankingRelationshipKey { get; set; }
    

    public Guid? ContractKey { get; set; }
    

    public Guid? CustomerKey { get; set; }
    

    public Guid? AccountKey { get; set; }

    public string? AccountName { get; set; }
    
    public string? AccountNumber { get; set; }

    public decimal? Amount { get; set; }

    public decimal? Balance { get; set; }

    public ProductType? ProductType { get; set; }

    [LinkBusinessKey("CustomerBankingRelationship","CustomerBankingRelationshipId")]
    public CustomerBankingRelationship? CustomerBankingRelationship { get; set; }
    
    [LinkBusinessKey("Contract","ContractId")]
    public Contract? Contract { get; set; }

    [LinkBusinessKey("Account","AccountId")]
    public Account? Account { get; set; }
}

public enum ProductType
{
    CreditCard,
    Mortgage,
    PersonalLoan
}