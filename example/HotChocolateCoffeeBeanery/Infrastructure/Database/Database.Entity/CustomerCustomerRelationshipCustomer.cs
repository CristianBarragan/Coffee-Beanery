using CoffeeBeanery.GraphQL.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Database.Entity;

public class CustomerCustomerRelationshipCustomer : Process
{
    public CustomerCustomerRelationshipCustomer()
    {
        Schema = Entity.Schema.Banking;
    }
    
    [UpsertKey("CustomerCustomerRelationshipCustomer", "Banking")]
    public Guid CustomerCustomerRelationshipCustomerKey { get; set; }
    
    public Guid? OuterCustomerKey { get; set; }
    
    public int? OuterCustomerId { get; set; }
    
    [LinkKey("Customer","OuterCustomerKey")]
    [JoinOneKey("CustomerCustomerRelationshipCustomer","Id")]
    public Customer? OuterCustomer { get; set; }
    
    public Guid? InnerCustomerKey { get; set; }
    
    public int? InnerCustomerId { get; set; }
    
    [LinkKey("Customer","InnerCustomerKey")]
    [JoinOneKey("CustomerCustomerRelationshipCustomer","Id")]
    public Customer? InnerCustomer { get; set; }
    
    public Guid? CustomerCustomerRelationshipKey { get; set; }
    
    public int? CustomerCustomerRelationshipId { get; set; }
    
    [LinkKey("CustomerCustomerRelationship","CustomerCustomerRelationshipKey")]
    [JoinOneKey("CustomerCustomerRelationshipCustomer","Id")]
    public CustomerCustomerRelationship? CustomerCustomerRelationship { get; set; }
}

public class CustomerCustomerRelationshipCustomerEntityConfiguration : IEntityTypeConfiguration<CustomerCustomerRelationshipCustomer>
{
    private readonly string _schema;

    public CustomerCustomerRelationshipCustomerEntityConfiguration(string schema)
    {
        _schema = schema;
    }

    public void Configure(EntityTypeBuilder<CustomerCustomerRelationshipCustomer> builder)
    {
        builder.ToTable(nameof(CustomerCustomerRelationshipCustomer), _schema);

        builder.HasKey(c => c.Id);
        
        builder.HasIndex(c => new { c.CustomerCustomerRelationshipCustomerKey }).IsUnique();

        builder.HasIndex(c => new { c.OuterCustomerKey, c.InnerCustomerKey }).IsUnique();
        
        builder.Property(c => c.ProcessedDateTime).HasDefaultValueSql("(now() at time zone 'utc')");
    }
}