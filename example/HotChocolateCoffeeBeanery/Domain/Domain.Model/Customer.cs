using CoffeeBeanery.GraphQL.Configuration;

namespace Domain.Model;

public class Customer
{
    public Guid? CustomerKey { get; set; }

    public string? FirstNaming { get; set; }

    public string? LastNaming { get; set; }

    public string? FullNaming { get; set; }

    public CustomerType? CustomerType { get; set; }

    [LinkBusinessKey("Product","ProductId")]
    public List<Product>? Product { get; set; }

    [LinkBusinessKey("ContactPoint","ContactPointId")]
    public List<ContactPoint>? ContactPoint { get; set; }

    [LinkBusinessKey("CustomerBankingRelationship","CustomerBankingRelationshipId")]
    public List<CustomerBankingRelationship>? CustomerBankingRelationship { get; set; }
}

public enum CustomerType
{
    Person,
    Organisation
}