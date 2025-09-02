using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Database.Common.Extensions;
using Database.Entity;

namespace Database.Banking
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true).AddEnvironmentVariables()
                .AddCommandLine(args).Build();

            var connectionStringBuilder =
                new NpgsqlConnectionStringBuilder(configuration.GetConnectionString("BankingConnectionString")!);

            await Host.CreateDefaultBuilder(args).ConfigureServices(services =>
            {
                services.AddPostgressDbContext<BankingDbContext>(
                    connectionStringBuilder,
                    Schema.Banking.ToString(),
                    ServiceLifetime.Transient);
            }).Build().RunAsync();
        }
    }
}