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
    [JoinKey("Customer","Id")]
    public List<ContactPoint>? ContactPoint { get; set; }
    
    [LinkKey("CustomerBankingRelationship","CustomerBankingRelationshipKey")]
    [JoinKey("Customer","Id")]
    public List<CustomerBankingRelationship>? CustomerBankingRelationship { get; set; }
    
    public Guid? OuterCustomerCustomerRelationshipCustomerKey { get; set; }
    
    public int? OuterCustomerCustomerRelationshipCustomerId { get; set; }
    
    // [LinkKey("CustomerCustomerRelationshipCustomer","OuterCustomerCustomerRelationshipCustomerKey")]
    // [JoinKey("Customer","Id")]
    public List<CustomerCustomerRelationshipCustomer>? OuterCustomerCustomerRelationshipCustomer { get; set; }
    
    public Guid? InnerCustomerCustomerRelationshipCustomerKey { get; set; }
    
    public int? InnerCustomerCustomerRelationshipCustomerId { get; set; }
    
    // [LinkKey("CustomerCustomerRelationshipCustomer","InnerCustomerCustomerRelationshipCustomerKey")]
    // [JoinKey("Customer","Id")]
    public List<CustomerCustomerRelationshipCustomer>? InnerCustomerCustomerRelationshipCustomer { get; set; }
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

        builder.HasMany(c => c.OuterCustomerCustomerRelationshipCustomer).WithOne(c => c.OuterCustomer).HasForeignKey(c => c.OuterCustomerId);
        
        builder.HasMany(c => c.InnerCustomerCustomerRelationshipCustomer).WithOne(c => c.InnerCustomer).HasForeignKey(c => c.InnerCustomerId);

        builder.Property(c => c.ProcessedDateTime).HasDefaultValueSql("(now() at time zone 'utc')");
    }
}