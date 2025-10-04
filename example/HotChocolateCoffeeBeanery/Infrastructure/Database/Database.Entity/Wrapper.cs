
using CoffeeBeanery.GraphQL.Configuration;

namespace Database.Entity;

public class Wrapper
{
    
    [LinkKey("Customer","CustomerId")]
    public List<Customer> Customer { get; set; }
}