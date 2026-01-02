
using CoffeeBeanery.GraphQL.Configuration;

namespace Database.Entity;

public class Wrapper
{
    
    [LinkKey("CustomerCustomerRelationship","CustomerCustomerRelationshipCustomerId")]
    public List<CustomerCustomerRelationship> CustomerCustomerRelationshipCustomer { get; set; }
}