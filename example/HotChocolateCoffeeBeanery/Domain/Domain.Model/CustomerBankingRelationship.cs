
using CoffeeBeanery.GraphQL.Configuration;

namespace Domain.Model;

public class CustomerBankingRelationship
{
    
    public Guid? CustomerBankingRelationshipKey { get; set; }
    
    public Guid? CustomerKey { get; set; }
    
    [LinkBusinessKey("Contract","ContractId")]

    public Contract? Contract { get; set; }
}