
namespace Domain.Model;

public class Wrapper
{
    public string CacheKey { get; set; }
    
    public List<Customer>? Customer { get; set; }

    public Model Model { get; set; }
}

public enum Model
{
    Customer,
    ContactPoint,
    CustomerBankingRelationship,
    Product,
    Contract,
    Account,
    Transaction
}