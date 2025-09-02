using System.ComponentModel.DataAnnotations.Schema;
using CoffeeBeanery.GraphQL.Configuration;
using HotChocolate;

namespace Domain.Model;

public class Account : Process
{
    [BusinessKey] public Guid AccountKey { get; set; }

    public string? AccountNumber { get; set; }

    public string? AccountName { get; set; }

    [NotMapped] public List<Transaction>? Transaction { get; set; }

    [NotMapped, BusinessSchema, GraphQLIgnore]
    public Schema Schema { get; set; } = Schema.Account;
}