using CoffeeBeanery.GraphQL.Configuration;

namespace Domain.Model;

public class ContactPoint
{
    [UpsertKey("ContactPoint","Banking")]
    public Guid? ContactPointKey { get; set; }

    public ContactPointType? ContactPointType { get; set; }

    public string? ContactPointValue { get; set; }

    [JoinKey("Customer", "Id")]
    public Guid? CustomerKey { get; set; }
}

public enum ContactPointType
{
    Mobile,
    Landline,
    Email
}