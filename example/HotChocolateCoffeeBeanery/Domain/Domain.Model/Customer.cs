using CoffeeBeanery.GraphQL.Configuration;

namespace Domain.Model;

public class Customer
{
    public Guid? CustomerKey { get; set; }

    public string? FirstNaming { get; set; }

    public string? LastNaming { get; set; }

    public string? FullNaming { get; set; }

    public CustomerType? CustomerType { get; set; }

    [LinkBusinessKeyAttribute("Product","ProductKey")]
    public List<Product>? Product { get; set; }

    [LinkBusinessKeyAttribute("ContactPoint","ContactPointKey")]
    public List<ContactPoint>? ContactPoint { get; set; }
}

public enum CustomerType
{
    Person,
    Organisation
}