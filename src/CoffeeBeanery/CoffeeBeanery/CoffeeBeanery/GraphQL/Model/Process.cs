namespace CoffeeBeanery.GraphQL.Model;

public class Process
{
    public int? Id { get; set; }

    public string? EntityName { get; set; }

    public bool? Processed { get; set; }

    public DateTime? ProcessedDateTime { get; set; }
}