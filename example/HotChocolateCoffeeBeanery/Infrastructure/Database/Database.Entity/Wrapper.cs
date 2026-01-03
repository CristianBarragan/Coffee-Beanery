
using CoffeeBeanery.GraphQL.Configuration;

namespace Database.Entity;

public class Wrapper
{
    
    [LinkKey("CustomerCustomerRelationship","CustomerCustomerRelationshipId")]
    public List<CustomerCustomerRelationship> CustomerCustomerRelationship { get; set; }
}