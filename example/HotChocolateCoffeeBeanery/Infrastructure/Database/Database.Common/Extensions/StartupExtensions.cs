using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Database.Common.Configuration;

namespace Database.Common.Extensions
{
    public static class StartupExtensions
    {
        public static IServiceCollection AddPostgressDbContext<T>(
            this IServiceCollection services,
            NpgsqlConnectionStringBuilder connectionStringBuilder,
            string schema,
            ServiceLifetime serviceLifetime
        ) where T : DbContext
        {
            var databaseOptions = new DatabaseOptions
            {
                Host = connectionStringBuilder.Host!,
                Database = connectionStringBuilder.Database!,
                Port = connectionStringBuilder.Port,
                CommandTimeout = connectionStringBuilder.CommandTimeout,
                ConnectionString = connectionStringBuilder.ConnectionString,
                Password = connectionStringBuilder.Password!,
                Username = connectionStringBuilder.Username!,
                SslMode = connectionStringBuilder.SslMode
            };

            return services.AddDbContextFactory<T>(builder => builder.UseNpgsql(
                    databaseOptions.ConnectionString, p =>
                    {
                        p.MigrationsHistoryTable(DatabaseOptions.MigrationTable, schema);
                        p.EnableRetryOnFailure(5, TimeSpan.FromSeconds(5), null);
                    }
                )
            );
        }
    }
}