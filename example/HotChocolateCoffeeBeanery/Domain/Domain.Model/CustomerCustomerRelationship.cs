
namespace Domain.Model;

public class CustomerCustomerRelationship
{
    public Guid? CustomerCustomerRelationshipKey { get; set; }

    public CustomerCustomerRelationshipType? CustomerCustomerRelationshipType { get; set; }
    
}
public enum CustomerCustomerRelationshipType
{
    Family,
    Partner,
    Widow,
    Single,
    Divorced
}