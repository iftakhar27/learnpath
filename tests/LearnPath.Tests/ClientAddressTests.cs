using System.Net;
using LearnPath.Api.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace LearnPath.Tests;

public class ClientAddressTests
{
    private const string Key = "0123456789abcdef0123456789abcdef-shared";

    private static ClientAddress Resolver(string? header = null, string? key = null) =>
        new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ClientIp:Header"] = header, ["Bff:SharedKey"] = key }).Build());

    private static HttpContext Request(params (string Name, string Value)[] headers)
    {
        var ctx = new DefaultHttpContext();
        ctx.Connection.RemoteIpAddress = IPAddress.Parse("10.24.0.5");
        foreach (var (n, v) in headers) ctx.Request.Headers[n] = v;
        return ctx;
    }

    [Fact]
    public void Without_configuration_the_connection_address_is_used() =>
        Assert.Equal("10.24.0.5", Resolver().Resolve(Request(("True-Client-IP", "1.2.3.4"), (ClientAddress.BffClientIpHeader, "5.6.7.8"))));

    [Fact]
    public void A_configured_edge_header_is_used_and_garbage_falls_back() 
    {
        var r = Resolver(header: "True-Client-IP");
        Assert.Equal("1.2.3.4", r.Resolve(Request(("True-Client-IP", "1.2.3.4"))));
        Assert.Equal("10.24.0.5", r.Resolve(Request(("True-Client-IP", "not-an-ip"))));
    }

    [Fact]
    public void Web_server_visitor_address_needs_the_shared_key()
    {
        var r = Resolver(header: "True-Client-IP", key: Key);
        Assert.Equal("5.6.7.8", r.Resolve(Request(("True-Client-IP", "9.9.9.9"), (ClientAddress.BffKeyHeader, Key), (ClientAddress.BffClientIpHeader, "5.6.7.8"))));
        Assert.Equal("9.9.9.9", r.Resolve(Request(("True-Client-IP", "9.9.9.9"), (ClientAddress.BffKeyHeader, Key + "x"), (ClientAddress.BffClientIpHeader, "5.6.7.8"))));
        Assert.Equal("9.9.9.9", r.Resolve(Request(("True-Client-IP", "9.9.9.9"), (ClientAddress.BffClientIpHeader, "5.6.7.8"))));
    }

    [Fact]
    public void Short_shared_keys_are_refused() => Assert.Throws<InvalidOperationException>(() => Resolver(key: "too-short"));
}
