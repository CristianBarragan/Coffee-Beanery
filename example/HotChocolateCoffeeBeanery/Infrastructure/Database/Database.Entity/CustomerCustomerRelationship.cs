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
    
    public List<CustomerCustomerRelationshipCustomer>? CustomerCustomerRelationshipCustomer { get; set; }
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

        builder.HasIndex(c => c.CustomerCustomerRelationshipKey).IsUnique();

        builder.HasMany(c => c.CustomerCustomerRelationshipCustomer).WithOne(c => c.CustomerCustomerRelationship).HasForeignKey(c => c.CustomerCustomerRelationshipId);
        
        builder.Property(c => c.ProcessedDateTime).HasDefaultValueSql("(now() at time zone 'utc')");
    }
}