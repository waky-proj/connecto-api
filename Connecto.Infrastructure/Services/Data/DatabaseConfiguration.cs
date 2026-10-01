using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Connecto.Infrastructure.Services.Data;

public static class DatabaseConfiguration
{
    public static string GetConnectionString(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(configuration["Database:Server"]) && !string.IsNullOrWhiteSpace(connectionString)) return connectionString;

        string Required(string key) => !string.IsNullOrWhiteSpace(configuration[key])
            ? configuration[key]!
            : throw new InvalidOperationException($"Missing database configuration: {key}.");

        return new SqlConnectionStringBuilder
        {
            DataSource = Required("Database:Server"),
            InitialCatalog = Required("Database:Name"),
            UserID = Required("Database:Username"),
            Password = Required("Database:Password"),
            Encrypt = true,
            TrustServerCertificate = false
        }.ConnectionString;
    }
}

