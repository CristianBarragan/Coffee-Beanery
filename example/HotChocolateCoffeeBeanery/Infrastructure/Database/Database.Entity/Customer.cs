using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Database.Entity;

public class Customer : Process
{
    public Customer()
    {
        Schema = Entity.Schema.Banking;
    }
    
    public Guid CustomerKey { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? FullName { get; set; }

    public CustomerType? CustomerType { get; set; }

    public List<ContactPoint>? ContactPoint { get; set; }

    public List<CustomerBankingRelationship>? Product { get; set; }
}

public enum CustomerType
{
    Person,
    Organisation
}

public class CustomerEntityConfiguration : IEntityTypeConfiguration<Customer>
{
    private readonly string _schema;

    public CustomerEntityConfiguration(string schema)
    {
        _schema = schema;
    }

    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable(nameof(Customer), _schema);

        builder.HasKey(c => c.Id);

        builder.HasIndex(c => c.CustomerKey).IsUnique();

        builder.HasMany(c => c.ContactPoint).WithOne(c => c.Customer).HasForeignKey(c => c.CustomerId);

        builder.HasMany(c => c.Product).WithOne(c => c.Customer).HasForeignKey(c => c.CustomerId);

        builder.Property(c => c.ProcessedDateTime).HasDefaultValueSql("(now() at time zone 'utc')");
    }
}