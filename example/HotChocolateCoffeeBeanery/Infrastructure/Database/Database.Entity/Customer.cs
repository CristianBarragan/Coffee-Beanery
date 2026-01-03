using CoffeeBeanery.GraphQL.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Database.Entity;

public class Customer : Process
{
    public Customer()
    {
        Schema = Entity.Schema.Banking;
    }
    
    [UpsertKey("Customer","Banking")]
    public Guid CustomerKey { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? FullName { get; set; }

    public CustomerType? CustomerType { get; set; }
    
    [LinkKey("ContactPoint","ContactPointKey")]
    public List<ContactPoint>? ContactPoint { get; set; }
    
    [LinkKey("CustomerBankingRelationship","CustomerBankingRelationshipKey")]
    public List<CustomerBankingRelationship>? CustomerBankingRelationship { get; set; }
    
    [LinkKey("CustomerCustomerRelationship","OuterCustomerCustomerRelationshipKey")]
    public List<CustomerCustomerRelationship>? OuterCustomerCustomerRelationship { get; set; }
    
    [LinkKey("CustomerCustomerRelationship","InnerCustomerCustomerRelationshipKey")]
    public List<CustomerCustomerRelationship>? InnerCustomerCustomerRelationship { get; set; }
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

        builder.HasMany(c => c.CustomerBankingRelationship).WithOne(c => c.Customer).HasForeignKey(c => c.CustomerId);

        builder.HasMany(c => c.OuterCustomerCustomerRelationship).WithOne(c => c.OuterCustomer).HasForeignKey(c => c.OuterCustomerId);
        
        builder.HasMany(c => c.InnerCustomerCustomerRelationship).WithOne(c => c.InnerCustomer).HasForeignKey(c => c.InnerCustomerId);

        builder.Property(c => c.ProcessedDateTime).HasDefaultValueSql("(now() at time zone 'utc')");
    }
}