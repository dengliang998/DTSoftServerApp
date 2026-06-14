using System.Reflection;
using DTSoft.Core.DbContexts;
using DTSoft.Core.DbProviders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DTSoft.Core.DbProviders
{
    public static class DatabaseConfigurationService
    {
        public static void ConfigureDatabase(IServiceCollection services, IConfiguration configuration)
        {
            var dbType = configuration["DBType"] ?? "SqlServer";
            var connectionString = configuration.GetConnectionString("DBConnection")!;

            var databaseName = ExtractDatabaseName(connectionString);
            var provider = DbProviderFactory.Create(dbType, databaseName);
            provider.ConfigureDbContext(services, connectionString);
        }

        public static IEnumerable<string> GetSupportedDatabaseTypes() => DbProviderFactory.GetSupportedProviders();

        private static string? ExtractDatabaseName(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString)) return null;

            var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var kvp = part.Split('=', 2);
                if (kvp.Length != 2) continue;

                var key = kvp[0].Trim();
                var value = kvp[1].Trim();
                if (string.IsNullOrEmpty(value)) continue;

                if (key.Equals("database", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("initial catalog", StringComparison.OrdinalIgnoreCase))
                {
                    return value;
                }
            }

            return null;
        }
    }
}
