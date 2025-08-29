using Microsoft.EntityFrameworkCore;
using Database.Entity;

namespace Database.Banking
{
    public class BankingDbContext : DbContext
    {
        public BankingDbContext(DbContextOptions<BankingDbContext> options) : base(options)
        {
        }
        
        public DbSet<Customer> Customer { get; set; }

        public DbSet<ContactPoint> ContactPoint { get; set; }

        public DbSet<CustomerBankingRelationship> CustomerBankingRelationship { get; set; }

        public DbSet<Contract> Contract { get; set; }

        public DbSet<Transaction> Transaction { get; set; }

        public DbSet<Account> Account { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfiguration(new CustomerEntityConfiguration(Schema.Banking.ToString()));

            modelBuilder.ApplyConfiguration(new ContactPointEntityConfiguration(Schema.Banking.ToString()));

            modelBuilder.ApplyConfiguration(new CustomerBankingRelationshipEntityConfiguration(Schema.Banking.ToString()));
            
            modelBuilder.ApplyConfiguration(new ContractEntityConfiguration(Schema.Lending.ToString()));
            
            modelBuilder.ApplyConfiguration(new TransactionEntityConfiguration(Schema.Lending.ToString()));
            
            modelBuilder.ApplyConfiguration(new AccountEntityConfiguration(Schema.Account.ToString()));
            
        }
    }
}
