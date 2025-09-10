using CoffeeBeanery.GraphQL.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Database.Entity;

public class ContactPoint : Process
{
    public ContactPoint()
    {
        Schema = Entity.Schema.Banking;
    }
    
    [UpsertKey("ContactPoint","Customer")]
    public Guid ContactPointKey { get; set; }

    public ContactPointType? ContactPointType { get; set; }

    public string? ContactPointValue { get; set; }

    [JoinKey("Customer","Id")]
    public Guid? CustomerKey { get; set; }

    public int? CustomerId { get; set; }
    
    public Customer? Customer { get; set; }
}

public enum ContactPointType
{
    Mobile,
    Landline,
    Email
}

public class ContactPointEntityConfiguration : IEntityTypeConfiguration<ContactPoint>
{
    private readonly string _schema;

    public ContactPointEntityConfiguration(string schema)
    {
        _schema = schema;
    }

    public void Configure(EntityTypeBuilder<ContactPoint> builder)
    {
        builder.ToTable(nameof(ContactPoint), _schema);

        builder.HasKey(c => c.Id);

        builder.HasIndex(c => c.ContactPointKey).IsUnique();

        builder.Property(c => c.ProcessedDateTime).HasDefaultValueSql("(now() at time zone 'utc')");
    }
}