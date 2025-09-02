using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Database.Entity;

public class CustomerBankingRelationship : Process
{
    public Guid CustomerBankingRelationshipKey { get; set; }

    public Guid? CustomerKey { get; set; }

    public Guid? ContractKey { get; set; }

    public int? CustomerId { get; set; }

    public Customer? Customer { get; set; }

    public List<Contract>? Contract { get; set; }

    [NotMapped] public Schema Schema { get; set; } = Schema.Banking;
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

        builder.Property(c => c.ProcessedDateTime).HasDefaultValueSql("(now() at time zone 'utc')");
    }
}