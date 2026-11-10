using System.Text.Json;
using System.Text.Json.Nodes;
using FrigoTrack.Common;
using FrigoTrack.Server;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5000");   

builder.Services.AddSingleton<Sessions>();
builder.Services.AddSingleton(_ => new ReadingArchive(
    builder.Configuration["DataFile"] ?? Path.Combine(AppContext.BaseDirectory, "data", "readings.json")));

var app = builder.Build();

app.MapPost("/api/handshake", async (HttpContext ctx, Sessions sessions) =>
{
    var body = await HttpJson.ReadBodyAsync(ctx);
    if (body is null)
        return HttpJson.Error(400, "bad_request", "Il corpo non è un JSON valido");

    var errors = Schemas.Check("handshake-request", body);
    if (errors.Count > 0)
        return HttpJson.Error(400, "bad_request", "Handshake non valido", errors);

    var p = KeyExchange.ParseHex(body["p"]!.GetValue<string>());
    var g = KeyExchange.ParseHex(body["g"]!.GetValue<string>());
    var bigA = KeyExchange.ParseHex(body["A"]!.GetValue<string>());

    if (p != KeyExchange.P || g != KeyExchange.G)
        return HttpJson.Error(400, "bad_request", "Parametri non accettati",
            new[] { new FieldError("/p", "Gruppo non supportato: usare RFC 3526 gruppo 14 con g = 2") });

    if (!KeyExchange.PublicLooksValid(bigA))
        return HttpJson.Error(400, "bad_request", "Chiave pubblica non valida",
            new[] { new FieldError("/A", "Deve essere compresa tra 2 e p-2") });

    var b = KeyExchange.NewSecret();                    
    var bigB = KeyExchange.PublicValue(b);              
    var k = KeyExchange.SharedSecret(bigA, b);          
    var sessionId = sessions.Create(KeyExchange.AesKeyFrom(k));   

    return HttpJson.Send(200, new JsonObject
    {
        ["session_id"] = sessionId,
        ["B"] = KeyExchange.ToHex(bigB)
    });
});

app.MapPost("/api/telemetry", (HttpContext ctx, Sessions sessions, ReadingArchive archive) =>
    ProtectedApi.HandleAsync(ctx, sessions, "telemetry-request", msg =>
    {
        var reading = msg.Deserialize<Reading>(JsonCfg.Options)!;
        archive.Add(reading);

        var ack = new JsonObject { ["status"] = "ok", ["sensor_id"] = reading.SensorId };
        var alarm = Alarms.Check(reading);
        if (alarm is not null) ack["alarm"] = alarm;
        return new HandlerResult(200, ack);
    }));

app.MapPost("/api/readings", (HttpContext ctx, Sessions sessions, ReadingArchive archive) =>
    ProtectedApi.HandleAsync(ctx, sessions, "readings-request", msg =>
    {
        var sensorId = msg["sensor_id"]!.GetValue<string>();
        var from = HttpJson.GetDate(msg, "from");
        var to = HttpJson.GetDate(msg, "to");

        if (!archive.HasSensor(sensorId))
            return new HandlerResult(404,
                HttpJson.ErrorBody("not_found", $"Nessuna misura per il sensore {sensorId}"));

        var list = archive.Query(sensorId, from, to);
        return new HandlerResult(200, new JsonObject
        {
            ["sensor_id"] = sensorId,
            ["count"] = list.Count,
            ["readings"] = JsonSerializer.SerializeToNode(list, JsonCfg.Options)
        });
    }));

app.MapPost("/api/close", (HttpContext ctx, Sessions sessions) =>
    ProtectedApi.HandleAsync(ctx, sessions, "close-request", _ =>
        new HandlerResult(200, new JsonObject { ["status"] = "closed" }, CloseSession: true)));

app.MapFallback(() => HttpJson.Error(404, "not_found", "Endpoint inesistente"));

app.Run();

public partial class Program { }
