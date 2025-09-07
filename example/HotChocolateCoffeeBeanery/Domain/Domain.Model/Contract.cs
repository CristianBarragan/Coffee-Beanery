
using CoffeeBeanery.GraphQL.Configuration;
using HotChocolate;

namespace Domain.Model;

public class Contract
{
    [UpsertKey("Contract","Lending")]
    public Guid ContractKey { get; set; }

    public ContractType? ContractType { get; set; }

    public decimal? Amount { get; set; }
    
    [JoinKey("Account")]
    public Guid? AccountKey { get; set; }
    
    [JoinKey("CustomerBankingRelationship")]
    public Guid? CustomerBankingRelationshipKey { get; set; }
    
    [LinkKey("Transaction")]
    public List<Transaction>? Transaction { get; set; }
}

public enum ContractType
{
    CreditCard,
    Mortgage,
    PersonalLoan
}