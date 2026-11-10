using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FrigoTrack.Common;


public class SecureClient
{
    private readonly HttpClient _http;

    public SecureClient(HttpClient http) => _http = http;

    public string? SessionId { get; private set; }
    public byte[]? Key { get; private set; }

    public async Task HandshakeAsync()
    {
        var a = DiffieHellman.GeneratePrivate();                                  
        var bigA = DiffieHellman.ComputePublic(DiffieHellman.G, a, DiffieHellman.P); 

        var req = new JsonObject
        {
            ["p"] = DiffieHellman.ToHex(DiffieHellman.P),
            ["g"] = DiffieHellman.ToHex(DiffieHellman.G),
            ["A"] = DiffieHellman.ToHex(bigA)
        };
        SchemaValidator.ValidateOrThrow("handshake-request", req);

        var (status, body) = await PostJsonAsync("/api/handshake", req);
        if (status != 200 || body is null)
            throw new InvalidOperationException($"Handshake fallito (HTTP {status}): {body?.ToJsonString()}");
        SchemaValidator.ValidateOrThrow("handshake-response", body);

        var bigB = DiffieHellman.FromHex(body["B"]!.GetValue<string>());
        if (!DiffieHellman.IsValidPublic(bigB, DiffieHellman.P))
            throw new CryptographicException("Chiave pubblica del server non valida");

        var k = DiffieHellman.ComputeShared(bigB, a, DiffieHellman.P);            
        Key = DiffieHellman.DeriveKey(k);
        SessionId = body["session_id"]!.GetValue<string>();
    }

    public Envelope Encrypt(JsonNode message)
        => SecureChannel.Encrypt(SessionId!, Key!, message.ToJsonString());

    public async Task<ApiResponse> SendAsync(string path, JsonNode message, string requestSchema, bool validateFirst = true)
    {
        if (validateFirst) SchemaValidator.ValidateOrThrow(requestSchema, message);  
        return await PostEnvelopeAsync(path, Encrypt(message));
    }

    public async Task<ApiResponse> PostEnvelopeAsync(string path, Envelope env)
    {
        var node = JsonSerializer.SerializeToNode(env, JsonOpts.Options)!;
        var (status, body) = await PostJsonAsync(path, node);

        if (body is JsonObject o && o.ContainsKey("ciphertext") && o.ContainsKey("tag") && Key is not null)
        {
            var respEnv = body.Deserialize<Envelope>(JsonOpts.Options)!;
            var plain = SecureChannel.Decrypt(respEnv, Key);
            return new ApiResponse(status, JsonNode.Parse(plain), true);
        }
        return new ApiResponse(status, body, false);
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
            catch (JsonException) { /* corpo non JSON: lasciamo null */ }
        }
        return ((int)resp.StatusCode, parsed);
    }
}
