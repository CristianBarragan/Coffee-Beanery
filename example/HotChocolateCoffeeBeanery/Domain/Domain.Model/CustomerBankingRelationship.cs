
using CoffeeBeanery.GraphQL.Configuration;

namespace Domain.Model;

public class CustomerBankingRelationship
{
    [UpsertKey("CustomerBankingRelationship","Banking")]
    public Guid CustomerBankingRelationshipKey { get; set; }

    [JoinKey("Product", "Id")]
    public Guid? ProductKey { get; set; }

    public int? CustomerId { get; set; }
    
    [LinkKey("Contract", "Id")]
    public List<Contract>? Contract { get; set; }
}