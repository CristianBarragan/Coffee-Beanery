
using CoffeeBeanery.GraphQL.Configuration;

namespace Domain.Model;

public class Wrapper
{
    public string CacheKey { get; set; }
    
    public Guid? CustomerCustomerEdgeKey { get; set; }
    
    [LinkBusinessKey("CustomerCustomerEdge","CustomerCustomerEdgeId")]
    public List<CustomerCustomerEdge>? CustomerCustomerEdge { get; set; }

    public Model Model { get; set; }
}

public enum Model
{
    CustomerCustomerEdge,
    CustomerCustomerRelationship,
    Customer,
    ContactPoint,
    CustomerBankingRelationship,
    Product,
    Contract,
    Account,
    Transaction
}