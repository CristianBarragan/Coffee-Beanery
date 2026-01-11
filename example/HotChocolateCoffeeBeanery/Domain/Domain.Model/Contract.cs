
using CoffeeBeanery.GraphQL.Configuration;

namespace Domain.Model;

public class Contract
{
    public Guid? ContractKey { get; set; }

    public ContractType? ContractType { get; set; }

    public decimal? Amount { get; set; }

    public Guid? AccountKey { get; set; }
    
    public Guid? CustomerBankingRelationshipKey { get; set; }

    [LinkBusinessKey("Transaction","TransactionId")]
    public List<Transaction>? Transaction { get; set; }
}

public enum ContractType
{
    CreditCard,
    Mortgage,
    PersonalLoan
}