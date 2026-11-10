using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using FrigoTrack.Common;

namespace FrigoTrack.Server;

public record HandlerResult(int Status, JsonNode Body, bool CloseSession = false);


public static class ProtectedApi
{
    public static async Task<IResult> HandleAsync(
        HttpContext ctx, Sessions sessions, string requestSchema, Func<JsonNode, HandlerResult> handler)
    {
        var raw = await HttpJson.ReadBodyAsync(ctx);
        if (raw is null)
            return HttpJson.Error(400, "bad_request", "Il corpo non è un JSON valido");

        var envErrors = Schemas.Check("envelope", raw);
        if (envErrors.Count > 0)
            return HttpJson.Error(400, "bad_request", "Busta non valida", envErrors);

        var env = raw.Deserialize<Envelope>(JsonCfg.Options)!;

        if (!sessions.TryGet(env.SessionId, out var session))
            return HttpJson.Error(401, "unauthorized", "Sessione inesistente o chiusa");

        string plain;
        try { plain = Cipher.Decrypt(env, session.Key); }
        catch (CryptographicException)
        {
            return HttpJson.Error(400, "bad_request", "Decifratura o tag non validi");
        }

        JsonNode? msg;
        try { msg = JsonNode.Parse(plain); }
        catch (JsonException)
        {
            return Encrypted(session, 400, HttpJson.ErrorBody("bad_request", "Il contenuto cifrato non è JSON"));
        }

        var errors = Schemas.Check(requestSchema, msg);
        if (errors.Count > 0)
            return Encrypted(session, 400, HttpJson.ErrorBody("bad_request", "Messaggio non valido", errors));

        var result = handler(msg!);
        var response = Encrypted(session, result.Status, result.Body);
        if (result.CloseSession) sessions.Remove(session.Id);   
        return response;
    }

    private static IResult Encrypted(Session s, int status, JsonNode body)
    {
        var env = Cipher.Encrypt(s.Id, s.Key, body.ToJsonString());
        return HttpJson.Send(status, JsonSerializer.SerializeToNode(env, JsonCfg.Options)!);
    }
}
