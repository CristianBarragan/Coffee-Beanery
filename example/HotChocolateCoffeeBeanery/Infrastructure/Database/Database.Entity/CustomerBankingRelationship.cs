using CoffeeBeanery.GraphQL.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Database.Entity;

public class CustomerBankingRelationship : Process
{
    public CustomerBankingRelationship()
    {
        Schema = Entity.Schema.Banking;
    }

    [UpsertKey("CustomerBankingRelationship", "Banking")]
    public Guid CustomerBankingRelationshipKey { get; set; }

    [JoinKey("Customer","CustomerKey")]
    public Guid? CustomerKey { get; set; }
    
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    
    [LinkKey("Contract","ContractKey")]
    public List<Contract>? Contract { get; set; }
}

public class CustomerBankingRelationshipEntityConfiguration : IEntityTypeConfiguration<CustomerBankingRelationship>
{
    private readonly string _schema;

    public CustomerBankingRelationshipEntityConfiguration(string schema)
    {
        _schema = schema;
    }

    public void Configure(EntityTypeBuilder<CustomerBankingRelationship> builder)
    {
        builder.ToTable(nameof(CustomerBankingRelationship), _schema);

        builder.HasKey(c => c.Id);

        builder.HasIndex(c => c.CustomerBankingRelationshipKey).IsUnique();

        builder.HasMany(c => c.Contract).WithOne(c => c.CustomerBankingRelationship).HasForeignKey(c => c.CustomerBankingRelationshipId);
        
        builder.Property(c => c.ProcessedDateTime).HasDefaultValueSql("(now() at time zone 'utc')");
    }
}