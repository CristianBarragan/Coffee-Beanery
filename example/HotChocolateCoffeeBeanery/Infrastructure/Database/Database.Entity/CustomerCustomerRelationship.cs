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
    
    [JoinKey("Customer","CustomerKey")]
    public Guid? OuterCustomerKey { get; set; }
    
    public int? OuterCustomerId { get; set; }
    
    [LinkKey("Customer","OuterCustomerKey")]
    public Customer? OuterCustomer { get; set; }
    
    [JoinKey("Customer","CustomerKey")]
    public Guid? InnerCustomerKey { get; set; }
    
    public int? InnerCustomerId { get; set; }
    
    [LinkKey("Customer","InnerCustomerKey")]
    public Customer? InnerCustomer { get; set; }
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