using Connecto.Infrastructure.Services.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Connecto.Tests.Unit;

public class DatabaseConfigurationTests
{
    [Fact]
    public void BuildsEncryptedSqlConnectionAndEscapesSecretCharacters()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Server=stale;Database=old;Integrated Security=true",
            ["Database:Server"] = "db.example.com,1433",
            ["Database:Name"] = "Connecto",
            ["Database:Username"] = "admin",
            ["Database:Password"] = "password;with=delimiters"
        }).Build();
        var result = new SqlConnectionStringBuilder(DatabaseConfiguration.GetConnectionString(config));
        Assert.Equal("password;with=delimiters", result.Password);
        Assert.Equal("Connecto", result.InitialCatalog);
        Assert.Equal("db.example.com,1433", result.DataSource);
        Assert.False(result.TrustServerCertificate);
        Assert.Equal(SqlConnectionEncryptOption.Mandatory, result.Encrypt);
    }

    [Fact]
    public void PreservesLocalConnectionStringConfiguration()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Server=local;Database=Connecto;Integrated Security=true"
        }).Build();
        Assert.Equal(config.GetConnectionString("DefaultConnection"), DatabaseConfiguration.GetConnectionString(config));
    }

    [Fact]
    public void MissingConfigurationFailsWithoutExposingPassword()
    {
        var config = new ConfigurationBuilder().Build();
        var error = Assert.Throws<InvalidOperationException>(() => DatabaseConfiguration.GetConnectionString(config));
        Assert.Equal("Missing database configuration: Database:Server.", error.Message);
    }
}

