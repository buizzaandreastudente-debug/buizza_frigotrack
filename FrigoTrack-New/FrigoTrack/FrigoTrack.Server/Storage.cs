using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using FrigoTrack.Common;

namespace FrigoTrack.Server;

public class Session
{
    public Session(string id, byte[] key) { Id = id; Key = key; }
    public string Id { get; }
    public byte[] Key { get; }
}

public class Sessions
{
    private readonly ConcurrentDictionary<string, Session> _map = new();

    public string Create(byte[] key)
    {
        while (true)
        {
            var id = Convert.ToHexString(RandomNumberGenerator.GetBytes(4)).ToLowerInvariant(); // 8 caratteri
            if (_map.TryAdd(id, new Session(id, key))) return id;
        }
    }

    public bool TryGet(string id, out Session session) => _map.TryGetValue(id, out session!);

    public void Remove(string id)
    {
        if (_map.TryRemove(id, out var s)) Array.Clear(s.Key);
    }
}

public class ReadingArchive
{
    private readonly object _lock = new();
    private readonly string _path;
    private readonly List<Reading> _items;

    public ReadingArchive(string path)
    {
        _path = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        _items = File.Exists(_path)
            ? JsonSerializer.Deserialize<List<Reading>>(File.ReadAllText(_path), JsonCfg.Options) ?? new()
            : new();
    }

    public void Add(Reading r)
    {
        lock (_lock)
        {
            _items.Add(r);
            File.WriteAllText(_path, JsonSerializer.Serialize(_items, JsonCfg.Pretty));
        }
    }

    public bool HasSensor(string sensorId)
    {
        lock (_lock) return _items.Any(r => r.SensorId == sensorId);
    }

    public List<Reading> Query(string sensorId, DateTimeOffset? from, DateTimeOffset? to)
    {
        lock (_lock)
        {
            return _items.Where(r => r.SensorId == sensorId && InRange(r, from, to)).ToList();
        }
    }

    private static bool InRange(Reading r, DateTimeOffset? from, DateTimeOffset? to)
    {
        var t = DateTimeOffset.Parse(r.Timestamp, CultureInfo.InvariantCulture);
        return (from is null || t >= from) && (to is null || t <= to);
    }
}
