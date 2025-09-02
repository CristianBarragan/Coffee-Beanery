using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Database.Entity;

public class Account : Process
{
    public Guid AccountKey { get; set; }

    public string? AccountNumber { get; set; }

    public string? AccountName { get; set; }

    [NotMapped] public List<Transaction>? Transaction { get; set; }

    [NotMapped] public Schema Schema { get; set; } = Schema.Account;
}

public class AccountEntityConfiguration : IEntityTypeConfiguration<Account>
{
    private readonly string _schema;

    public AccountEntityConfiguration(string schema)
    {
        _schema = schema;
    }

    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable(nameof(Account), _schema);

        builder.HasKey(c => c.Id);

        builder.HasIndex(c => c.AccountKey).IsUnique();

        builder.Property(c => c.ProcessedDateTime).HasDefaultValueSql("(now() at time zone 'utc')");
    }
}