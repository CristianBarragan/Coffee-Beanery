
namespace Domain.Model;

public class Wrapper
{
    public string CacheKey { get; set; }
    
    public List<CustomerCustomerEdge>? CustomerCustomerEdge { get; set; }

    public Model Model { get; set; }
}

public enum Model
{
    CustomerCustomerEdge,
    Customer,
    ContactPoint,
    CustomerBankingRelationship,
    Product,
    Contract,
    Account,
    Transaction
}