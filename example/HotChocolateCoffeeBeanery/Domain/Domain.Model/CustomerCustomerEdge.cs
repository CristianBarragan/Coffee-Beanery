
using CoffeeBeanery.GraphQL.Configuration;

namespace Domain.Model;

public class CustomerCustomerEdge : GraphProcess
{
    [GraphKey("CustomerCustomerRelationshipEdge")]
    public Guid? CustomerCustomerRelationshipKey { get; set; }
    
    [LinkBusinessKey("CustomerCustomerRelationship","CustomerCustomerRelationshipKey")]
    public CustomerCustomerRelationship? CustomerCustomerRelationship { get; set; }

    [GraphKey("CustomerCustomerRelationshipEdge")]
    public Guid? OuterCustomerKey { get; set; }
    
    [LinkBusinessKey("Customer","OuterCustomerKey")]
    public Customer? OuterCustomer { get; set; }

    [GraphKey("CustomerCustomerRelationshipEdge")]
    public Guid? InnerCustomerKey { get; set; }
    
    [LinkBusinessKey("Customer","InnerCustomerKey")]
    public Customer? InnerCustomer { get; set; }
    
    [GraphKey("CustomerCustomerRelationshipEdge")]
    public CustomerCustomerRelationshipType CustomerCustomerRelationshipType { get; set; }
}
public enum CustomerCustomerRelationshipType
{
    Family,
    Partner,
    Widow,
    Single,
    Divorced
}