﻿using CoffeeBeanery.GraphQL.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Database.Entity;

public class Account : Process
{
    public Account()
    {
        Schema = Entity.Schema.Account;
    }
    
    [UpsertKey("Account","Account")]
    public Guid AccountKey { get; set; }

    public string? AccountNumber { get; set; }

    public string? AccountName { get; set; }
    
    [LinkKey("Contract","ContractKey")]
    public Contract? Contract { get; set; }
    
    [LinkKey("Transaction","TransactionKey")]
    public List<Transaction>? Transaction { get; set; }
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
        
        builder.HasOne(c => c.Contract).WithOne(c => c.Account).HasForeignKey<Contract>(c => c.AccountId);
        
        builder.HasMany(c => c.Transaction).WithOne(c => c.Account).HasForeignKey(c => c.AccountId);
        
        builder.Property(c => c.ProcessedDateTime).HasDefaultValueSql("(now() at time zone 'utc')");
    }
}