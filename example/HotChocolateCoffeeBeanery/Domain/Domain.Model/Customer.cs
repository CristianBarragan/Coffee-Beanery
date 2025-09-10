using CoffeeBeanery.GraphQL.Configuration;

namespace Domain.Model;

public class Customer
{
    [UpsertKey("Customer","Banking")]
    public Guid? CustomerKey { get; set; }

    public string? FirstNaming { get; set; }

    public string? LastNaming { get; set; }

    public string? FullNaming { get; set; }

    public CustomerType? CustomerType { get; set; }

    [LinkKey("Product", "Id")]
    public List<Product>? Product { get; set; }

    [LinkKey("ContactPoint", "Id")]
    public List<ContactPoint>? ContactPoint { get; set; }
}

public enum CustomerType
{
    Person,
    Organisation
}