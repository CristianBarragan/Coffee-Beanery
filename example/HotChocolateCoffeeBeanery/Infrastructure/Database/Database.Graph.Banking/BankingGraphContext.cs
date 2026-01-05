using Microsoft.EntityFrameworkCore;

namespace Database.Graph.Banking;

public class BankingGraphContext : DbContext
{
    public BankingGraphContext(DbContextOptions<BankingGraphContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        
    }
}