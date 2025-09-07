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

    [LinkKey("Product")]
    public List<Product>? Product { get; set; }

    [LinkKey("ContactPoint")]
    public List<ContactPoint>? ContactPoint { get; set; }
}

public enum CustomerType
{
    Person,
    Organisation
}