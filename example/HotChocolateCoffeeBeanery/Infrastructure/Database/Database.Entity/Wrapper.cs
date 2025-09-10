using CoffeeBeanery.GraphQL.Configuration;

namespace Database.Entity;

public class Wrapper
{
    [LinkKey("Customer", "Id")]
    public List<Customer> Customer { get; set; }
}