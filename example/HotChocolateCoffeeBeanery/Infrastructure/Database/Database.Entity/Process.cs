using System.ComponentModel.DataAnnotations.Schema;

namespace Database.Entity;

public class Process
{
    public int? Id { get; set; }

    [NotMapped]
    public Schema? Schema { get; set; }

    public DateTime ProcessedDateTime { get; set; }
}

public enum Schema
{
    Banking,
    Lending,
    Account
}