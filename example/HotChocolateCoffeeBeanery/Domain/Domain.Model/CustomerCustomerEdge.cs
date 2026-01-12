
using CoffeeBeanery.GraphQL.Configuration;

namespace Domain.Model;

public class CustomerCustomerEdge : GraphProcess
{
    [GraphKey("CustomerCustomerRelationshipEdge")]
    [LinkBusinessKey("CustomerCustomerRelationship","CustomerCustomerRelationshipId")]
    public Guid? CustomerCustomerRelationshipKey { get; set; }
    
    public CustomerCustomerRelationship? CustomerCustomerRelationship { get; set; }

    [GraphKey("CustomerCustomerRelationshipEdge")]
    [LinkBusinessKey("Customer","OuterCustomerId")]
    public Guid? OuterCustomerKey { get; set; }

    public Customer? OuterCustomer { get; set; }

    [GraphKey("CustomerCustomerRelationshipEdge")]
    [LinkBusinessKey("Customer","InnerCustomerId")]
    public Guid? InnerCustomerKey { get; set; }
    
    public Customer? InnerCustomer { get; set; }
    
    [GraphKey("CustomerCustomerRelationshipEdge")]
    [LinkBusinessKey("Customer","CustomerCustomerRelationshipType")]
    public CustomerCustomerRelationshipType? CustomerCustomerRelationshipType { get; set; }
}