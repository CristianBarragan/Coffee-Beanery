using CoffeeBeanery.GraphQL.Configuration;

namespace Domain.Model;

public class Wrapper
{
    public string CacheKey { get; set; }
    
    [LinkKey("Customer", "Id")]
    public List<Customer>? Customer { get; set; }
}