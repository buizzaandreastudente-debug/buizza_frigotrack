using System.Security.Cryptography;
using System.Text.Json.Nodes;
using FrigoTrack.Common;
using FrigoTrack.Server;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace FrigoTrack.Tests;

public class TestFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var file = Path.Combine(Path.GetTempPath(), $"frigotrack-test-{Guid.NewGuid():N}.json");
        builder.ConfigureServices(s =>
        {
            s.RemoveAll<ReadingArchive>();
            s.AddSingleton(new ReadingArchive(file));
        });
    }
}

public class ProtocolTests : IClassFixture<TestFactory>
{
    private readonly TestFactory _factory;
    public ProtocolTests(TestFactory factory) => _factory = factory;

    private static JsonObject Measure(double temp = 4.6) => new()
    {
        ["sensor_id"] = "CELLA-BG-007",
        ["timestamp"] = "2026-11-02T08:15:30Z",
        ["cargo_type"] = "pharma",
        ["temperature_c"] = temp,
        ["humidity"] = 55,
        ["battery"] = 87
    };

    private async Task<SensorSession> NewSessionAsync()
    {
        var session = new SensorSession(_factory.CreateClient());
        await session.DoHandshakeAsync();
        return session;
    }

    [Fact]
    public async Task Handshake_riuscito_produce_sessione_e_chiave()
    {
        var s = await NewSessionAsync();
        Assert.Matches("^[0-9a-f]{8}$", s.SessionId);
        Assert.Equal(32, s.Key!.Length);
    }

    [Fact]
    public async Task Misura_valida_riceve_200_e_ack_cifrato()
    {
        var s = await NewSessionAsync();
        var r = await s.SendAsync("/api/telemetry", Measure(), "telemetry-request");

        Assert.Equal(200, r.Status);
        Assert.True(r.Encrypted);
        Assert.Equal("ok", (string?)r.Body!["status"]);
        Assert.Null(r.Body["alarm"]);
    }

    [Fact]
    public async Task Misura_non_valida_riceve_400_con_il_campo_errato()
    {
        var s = await NewSessionAsync();
        var r = await s.SendAsync("/api/telemetry", Measure(99), "telemetry-request", validateFirst: false);

        Assert.Equal(400, r.Status);
        var details = r.Body!["details"]!.AsArray();
        Assert.Contains(details, d => (string?)d!["field"] == "/temperature_c");
    }

    [Fact]
    public async Task Busta_manomessa_riceve_400()
    {
        var s = await NewSessionAsync();
        var env = s.Encrypt(Measure());
        var bytes = Convert.FromBase64String(env.Ciphertext);
        bytes[^1] ^= 0x01;                                  
        var tampered = env with { Ciphertext = Convert.ToBase64String(bytes) };

        var r = await s.PostEnvelopeAsync("/api/telemetry", tampered);
        Assert.Equal(400, r.Status);
    }

    [Fact]
    public async Task Sessione_inesistente_riceve_401()
    {
        var s = new SensorSession(_factory.CreateClient());
        var env = Cipher.Encrypt("deadbeef", RandomNumberGenerator.GetBytes(32), Measure().ToJsonString());

        var r = await s.PostEnvelopeAsync("/api/telemetry", env);
        Assert.Equal(401, r.Status);
    }

    [Fact]
    public async Task Farmaci_a_12_gradi_generano_allarme()
    {
        var s = await NewSessionAsync();
        var r = await s.SendAsync("/api/telemetry", Measure(12.0), "telemetry-request");

        Assert.Equal(200, r.Status);
        Assert.Equal("temperature_high", (string?)r.Body!["alarm"]!["type"]);
    }
}
