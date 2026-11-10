using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FrigoTrack.Common;


public class SensorSession
{
    private readonly HttpClient _http;

    public SensorSession(HttpClient http) => _http = http;

    public string? SessionId { get; private set; }
    public byte[]? Key { get; private set; }

    public async Task DoHandshakeAsync()
    {
        var a = KeyExchange.NewSecret();               
        var bigA = KeyExchange.PublicValue(a);         

        var req = new JsonObject
        {
            ["p"] = KeyExchange.ToHex(KeyExchange.P),
            ["g"] = KeyExchange.ToHex(KeyExchange.G),
            ["A"] = KeyExchange.ToHex(bigA)
        };
        Schemas.CheckOrThrow("handshake-request", req);

        var (status, body) = await PostJsonAsync("/api/handshake", req);
        if (status != 200 || body is null)
            throw new InvalidOperationException($"Handshake fallito (HTTP {status}): {body?.ToJsonString()}");
        Schemas.CheckOrThrow("handshake-response", body);

        var bigB = KeyExchange.ParseHex(body["B"]!.GetValue<string>());
        if (!KeyExchange.PublicLooksValid(bigB))
            throw new CryptographicException("Chiave pubblica del server non valida");

        var k = KeyExchange.SharedSecret(bigB, a);   
        Key = KeyExchange.AesKeyFrom(k);
        SessionId = body["session_id"]!.GetValue<string>();
    }

    public Envelope Encrypt(JsonNode message)
        => Cipher.Encrypt(SessionId!, Key!, message.ToJsonString());

    public async Task<ServerReply> SendAsync(string path, JsonNode message, string requestSchema, bool validateFirst = true)
    {
        if (validateFirst) Schemas.CheckOrThrow(requestSchema, message);   
        return await PostEnvelopeAsync(path, Encrypt(message));
    }

    public async Task<ServerReply> PostEnvelopeAsync(string path, Envelope env)
    {
        var node = JsonSerializer.SerializeToNode(env, JsonCfg.Options)!;
        var (status, body) = await PostJsonAsync(path, node);

        if (body is JsonObject o && o.ContainsKey("ciphertext") && o.ContainsKey("tag") && Key is not null)
        {
            var respEnv = body.Deserialize<Envelope>(JsonCfg.Options)!;
            var plain = Cipher.Decrypt(respEnv, Key);
            return new ServerReply(status, JsonNode.Parse(plain), true);
        }
        return new ServerReply(status, body, false);
    }

    private async Task<(int Status, JsonNode? Body)> PostJsonAsync(string path, JsonNode body)
    {
        using var content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        using var resp = await _http.PostAsync(path, content);
        var text = await resp.Content.ReadAsStringAsync();

        JsonNode? parsed = null;
        if (!string.IsNullOrWhiteSpace(text))
        {
            try { parsed = JsonNode.Parse(text); }
            catch (JsonException) { /* non era JSON: lascio null */ }
        }
        return ((int)resp.StatusCode, parsed);
    }
}
