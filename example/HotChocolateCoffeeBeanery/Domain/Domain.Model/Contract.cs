
using CoffeeBeanery.GraphQL.Configuration;
using HotChocolate;

namespace Domain.Model;

public class Contract
{
    [UpsertKey("Contract","Lending")]
    public Guid ContractKey { get; set; }

    public ContractType? ContractType { get; set; }

    public decimal? Amount { get; set; }

    [JoinKey("Product", "Id")]
    public Guid? ProductKey { get; set; }
    
    [LinkKey("Account", "Id")]
    public Guid? AccountKey { get; set; }
    
    [JoinKey("CustomerBankingRelationship", "Id")]
    public Guid? CustomerBankingRelationshipKey { get; set; }
    
    [LinkKey("Transaction", "Id")]
    public List<Transaction>? Transaction { get; set; }
}

public enum ContractType
{
    CreditCard,
    Mortgage,
    PersonalLoan
}