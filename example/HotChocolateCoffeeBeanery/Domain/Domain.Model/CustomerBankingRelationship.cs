
using CoffeeBeanery.GraphQL.Configuration;

namespace Domain.Model;

public class CustomerBankingRelationship
{
    [UpsertKey("CustomerBankingRelationship","Banking")]
    public Guid CustomerBankingRelationshipKey { get; set; }

    [JoinKey("Customer")]
    public Guid? CustomerKey { get; set; }

    public int? CustomerId { get; set; }
    
    [LinkKey("Contract")]
    public List<Contract>? Contract { get; set; }
}