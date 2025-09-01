
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Database.Entity;

    public class Contract : Process
    {
        public Guid ContractKey { get; set; }

        public ContractType? ContractType { get; set; }

        public decimal? Amount { get; set; }

        public Guid? AccountKey { get; set; }

        public Guid? CustomerBankingRelationshipKey { get; set; }
        
        [NotMapped]
        public CustomerBankingRelationship? CustomerBankingRelationship { get; set; }

        public int? CustomerBankingRelationshipId { get; set; }

        [NotMapped]
        public List<Transaction>? Transaction { get; set; }

        [NotMapped]
        public Schema Schema { get; set; } =  Schema.Lending;
    }

    public enum ContractType
    {
        CreditCard,
        Mortgage,
        PersonalLoan
    }

    public class ContractEntityConfiguration : IEntityTypeConfiguration<Contract>
    {
        private readonly string _schema;

        public ContractEntityConfiguration(string schema) 
        {
            _schema = schema;
        }

        public void Configure(EntityTypeBuilder<Contract> builder)
        {
            builder.ToTable(nameof(Contract), _schema);

            builder.HasKey(c => c.Id);

            builder.HasIndex(c => c.ContractKey).IsUnique();

            // builder.HasMany(c => c.Transaction).WithOne(c => c.Contract).
            //     HasForeignKey(t => t.ContractId);

            builder.Property(c => c.ProcessedDateTime).HasDefaultValueSql("(now() at time zone 'utc')");
        }
    }
