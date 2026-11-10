using System.Globalization;
using System.Text.Json.Nodes;
using FrigoTrack.Common;


var baseUrl = args.FirstOrDefault(a => a.StartsWith("http")) ?? "http://localhost:5000";
var demoInvalid = args.Contains("--invalid");
const int Measures = 6;     
const int IntervalMs = 2000;  

var sensors = new[]
{
    new SensorDef("CELLA-BG-007", "pharma", 5.0),
    new SensorDef("CAMION-MI-012", "food", 3.0),
    new SensorDef("CELLA-BS-003", "frozen", -20.0)
};

Console.WriteLine($"Avvio {sensors.Length} sensori verso {baseUrl}\n");
await Task.WhenAll(sensors.Select((s, i) => RunSensorAsync(s, demoInvalid && i == 0)));
Console.WriteLine("\nFatto.");

async Task RunSensorAsync(SensorDef s, bool sendInvalid)
{
    void Log(string m) => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {s.Id,-14} {m}");

    using var http = new HttpClient { BaseAddress = new Uri(baseUrl) };
    var session = new SensorSession(http);   
    var rnd = new Random();

    try
    {
       
        await session.DoHandshakeAsync();
        Log($"handshake ok, sessione {session.SessionId}");

        int battery = 100;
        for (int i = 0; i < Measures; i++)
        {
            
            var temp = Math.Round(s.BaseTemp + (rnd.NextDouble() - 0.5) * 3 + (rnd.Next(5) == 0 ? 6 : 0), 1);
            battery -= rnd.Next(0, 3);

            var msg = new JsonObject
            {
                ["sensor_id"] = s.Id,
                ["timestamp"] = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
                ["cargo_type"] = s.Cargo,
                ["temperature_c"] = temp,
                ["humidity"] = rnd.Next(40, 71),
                ["battery"] = battery
            };

           
            var r = await session.SendAsync("/api/telemetry", msg, "telemetry-request");

            if (r.Status == 200)
            {
                Schemas.CheckOrThrow("telemetry-response", r.Body);
                var alarm = r.Body!["alarm"];
                Log(alarm is null
                    ? $"{temp,5} °C  ->  200 ok"
                    : $"{temp,5} °C  ->  200 ok  *** ALLARME {alarm["type"]} (limiti {alarm["min_c"]}..{alarm["max_c"]}) ***");
            }
            else
            {
                Log($"HTTP {r.Status}: {r.Body?.ToJsonString()}");
            }

            await Task.Delay(IntervalMs);
        }

        if (sendInvalid)
        {
            var bad = new JsonObject
            {
                ["sensor_id"] = s.Id,
                ["timestamp"] = "ieri sera",       
                ["cargo_type"] = "carne",        
                ["temperature_c"] = 99             
            };
            var r = await session.SendAsync("/api/telemetry", bad, "telemetry-request", validateFirst: false);
            Log($"misura NON valida -> HTTP {r.Status}: {r.Body?.ToJsonString()}");
        }

        var q = await session.SendAsync("/api/readings", new JsonObject { ["sensor_id"] = s.Id }, "readings-request");
        if (q.Status == 200)
        {
            Schemas.CheckOrThrow("readings-response", q.Body);
            Log($"il server ha salvato {q.Body!["count"]} misure di questo sensore");
        }

        var c = await session.SendAsync("/api/close", new JsonObject { ["action"] = "close" }, "close-request");
        Log($"sessione chiusa (HTTP {c.Status})");
    }
    catch (InvalidMessageException e)
    {
        Log($"messaggio bloccato dal client: {e.Message}");
    }
    catch (Exception e)
    {
        Log($"errore: {e.Message}");
    }
}

record SensorDef(string Id, string Cargo, double BaseTemp);
