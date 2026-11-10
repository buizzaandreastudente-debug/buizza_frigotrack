using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace FrigoTrack.Common;


public static class JsonCfg
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static readonly JsonSerializerOptions Pretty = new(Options) { WriteIndented = true };
}

public record Envelope(string SessionId, string Ciphertext, string Tag);

public record Reading(
    string SensorId,
    string Timestamp,
    string CargoType,
    double TemperatureC,
    double? Humidity,
    int? Battery);

public record FieldError(string Field, string Message);

public record ServerReply(int Status, JsonNode? Body, bool Encrypted);

public class InvalidMessageException : Exception
{
    public IReadOnlyList<FieldError> Errors { get; }

    public InvalidMessageException(string schema, IReadOnlyList<FieldError> errors)
        : base($"Messaggio non valido per lo schema '{schema}': " +
               string.Join("; ", errors.Select(e => $"{e.Field} -> {e.Message}")))
        => Errors = errors;
}
