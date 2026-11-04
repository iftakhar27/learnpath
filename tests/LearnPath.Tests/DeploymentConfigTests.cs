using LearnPath.Api.Infrastructure;
using Npgsql;

namespace LearnPath.Tests;

public class DeploymentConfigTests
{
    [Fact]
    public void Platform_database_urls_become_npgsql_connection_strings()
    {
        var cs = new NpgsqlConnectionStringBuilder(DatabaseUrl.ToNpgsql("postgresql://learnpath:p%40ss%3Aword@dpg-abc123-a/learnpath_db"));
        Assert.Equal("dpg-abc123-a", cs.Host);
        Assert.Equal(5432, cs.Port);
        Assert.Equal("learnpath_db", cs.Database);
        Assert.Equal("learnpath", cs.Username);
        Assert.Equal("p@ss:word", cs.Password);
    }

    [Fact]
    public void Explicit_port_and_sslmode_are_kept()
    {
        var cs = new NpgsqlConnectionStringBuilder(DatabaseUrl.ToNpgsql("postgres://u:p@db.example.com:6543/app?sslmode=require"));
        Assert.Equal(6543, cs.Port);
        Assert.Equal(SslMode.Require, cs.SslMode);
    }

    [Theory]
    [InlineData("Host=localhost;Database=x;Username=u;Password=p")]
    [InlineData("")]
    [InlineData(null)]
    public void Npgsql_strings_and_empty_values_pass_through(string? value) => Assert.Equal(value, DatabaseUrl.ToNpgsql(value));

    [Fact]
    public async Task Disabled_otp_refuses_with_a_clear_message()
    {
        var e = await Assert.ThrowsAsync<AppException>(() => new DisabledOtpSender().SendAsync("+919876543210", "123456", default));
        Assert.Equal((503, "otp_unavailable"), (e.Status, e.Code));
    }
}
