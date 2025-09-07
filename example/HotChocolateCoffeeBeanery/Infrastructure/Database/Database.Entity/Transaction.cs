using System.ComponentModel.DataAnnotations.Schema;
using CoffeeBeanery.GraphQL.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Database.Entity;

public class Transaction : Process
{
    public Transaction()
    {
        Schema = Entity.Schema.Lending;
    }

    public Guid TransactionKey { get; set; }

    public decimal? Amount { get; set; }

    public decimal? Balance { get; set; }

    public Guid? ContractKey { get; set; }

    [JoinKey("Contract")]
    public Contract? Contract { get; set; }

    public int? ContractId { get; set; }

    public Guid? AccountKey { get; set; }

    [JoinKey("Account")]
    public Account? Account { get; set; }

    public int? AccountId { get; set; }
}

public class TransactionEntityConfiguration : IEntityTypeConfiguration<Transaction>
{
    private readonly string _schema;

    public TransactionEntityConfiguration(string schema)
    {
        _schema = schema;
    }

    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable(nameof(Transaction), _schema);

        builder.HasKey(c => c.Id);

        builder.HasIndex(c => c.TransactionKey).IsUnique();

        builder.Property(c => c.ProcessedDateTime).HasDefaultValueSql("(now() at time zone 'utc')");
    }
}