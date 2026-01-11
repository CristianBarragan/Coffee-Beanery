using CoffeeBeanery.GraphQL.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Database.Entity;

public class CustomerCustomerRelationship : Process
{
    public CustomerCustomerRelationship()
    {
        Schema = Entity.Schema.Banking;
    }
    
    [UpsertKey("CustomerCustomerRelationship", "Banking")]
    public Guid CustomerCustomerRelationshipKey { get; set; }
    
    [LinkKey("Customer", "OuterCustomerKey")]
    // [LinkIdKey("Customer","Id")]
    [JoinKey("CustomerCustomerRelationship","OuterCustomerId")]
    public Guid? OuterCustomerKey { get; set; }
    
    public int? OuterCustomerId { get; set; }
    public Customer? OuterCustomer { get; set; }
    
    [LinkKey("Customer", "InnerCustomerKey")]
    // [LinkIdKey("Customer","Id")]
    [JoinKey("CustomerCustomerRelationship","InnerCustomerId")]
    public Guid? InnerCustomerKey { get; set; }
    
    public int? InnerCustomerId { get; set; }
    
    public Customer? InnerCustomer { get; set; }

    public CustomerCustomerRelationshipType? CustomerCustomerRelationshipType { get; set; }
}

public enum CustomerCustomerRelationshipType
{
    Family,
    Partner,
    Widow,
    Single,
    Divorced
}

public class CustomerCustomerRelationshipEntityConfiguration : IEntityTypeConfiguration<CustomerCustomerRelationship>
{
    private readonly string _schema;

    public CustomerCustomerRelationshipEntityConfiguration(string schema)
    {
        _schema = schema;
    }

    public void Configure(EntityTypeBuilder<CustomerCustomerRelationship> builder)
    {
        builder.ToTable(nameof(CustomerCustomerRelationship), _schema);

        builder.HasKey(c => c.Id);
        
        builder.HasIndex(c => new { c.CustomerCustomerRelationshipKey }).IsUnique();

        builder.HasIndex(c => new { c.OuterCustomerKey, c.InnerCustomerKey }).IsUnique();
        
        builder.Property(c => c.ProcessedDateTime).HasDefaultValueSql("(now() at time zone 'utc')");
    }
}