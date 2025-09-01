
using Npgsql;

namespace Database.Common.Configuration
{
    public class DatabaseOptions
    {
        public const string Key = "DatabaseOptionsConfiguration";

        public required string ConnectionString { get; set; }

        public required string Host { get; set; }

        public required string Database { get; set; }

        public required int CommandTimeout { get; set; }

        public required int Port { get; set; }

        public required string Username { get; set; }

        public required string Password { get; set; }

        public required SslMode SslMode { get; set; }

        public static string MigrationTable { get; } = "__Migrations";
    }
}
