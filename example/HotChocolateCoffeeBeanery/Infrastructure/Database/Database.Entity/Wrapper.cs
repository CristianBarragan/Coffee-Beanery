
using CoffeeBeanery.GraphQL.Configuration;

namespace Database.Entity;

public class Wrapper
{
    
    [LinkKey("CustomerCustomerRelationshipCustomer","CustomerCustomerRelationshipCustomerId")]
    public List<CustomerCustomerRelationshipCustomer> CustomerCustomerRelationshipCustomer { get; set; }
}