using System.ComponentModel.DataAnnotations.Schema;
using CoffeeBeanery.GraphQL.Configuration;
using HotChocolate;

namespace Domain.Model;

public class ContactPoint : Process
{
    [BusinessKey] public Guid ContactPointKey { get; set; }

    public ContactPointType? ContactPointType { get; set; }

    public string? ContactPointValue { get; set; }

    public Guid? CustomerKey { get; set; }

    [JoinKey] public int? CustomerId { get; set; }

    [GraphQLIgnore] public Customer? Customer { get; set; }

    [NotMapped, BusinessSchema, GraphQLIgnore]
    public Schema Schema { get; set; } = Schema.Banking;
}

public enum ContactPointType
{
    Mobile,
    Landline,
    Email
}