
using CoffeeBeanery.GraphQL.Configuration;

namespace Domain.Model;

public class CustomerCustomerEdge : GraphProcess
{
    public Guid? CustomerCustomerRelationshipKey { get; set; }
    
    [LinkBusinessKey("CustomerCustomerRelationship","CustomerCustomerRelationshipKey")]
    public CustomerCustomerRelationship? CustomerCustomerRelationship { get; set; }

    public Guid? OuterCustomerKey { get; set; }
    
    [LinkBusinessKey("Customer","OuterCustomerKey")]
    public Customer? OuterCustomer { get; set; }

    public Guid? InnerCustomerKey { get; set; }
    
    [LinkBusinessKey("Customer","InnerCustomerKey")]
    public Customer? InnerCustomer { get; set; }
}