namespace Prism.Core.Content;

public sealed class StableIdRegistry
{
    private readonly Dictionary<string, ushort> _idsByKey;
    private readonly string[] _keysById;

    public StableIdRegistry(IEnumerable<string> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);

        var sortedKeys = keys.ToArray();
        if (sortedKeys.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Registry keys must not be null, empty, or whitespace.", nameof(keys));
        }

        Array.Sort(sortedKeys, StringComparer.Ordinal);
        for (var index = 1; index < sortedKeys.Length; index++)
        {
            if (StringComparer.Ordinal.Equals(sortedKeys[index - 1], sortedKeys[index]))
            {
                throw new ArgumentException($"Duplicate registry key '{sortedKeys[index]}'.", nameof(keys));
            }
        }

        if (sortedKeys.Length > ushort.MaxValue + 1)
        {
            throw new ArgumentException($"A registry cannot contain more than {ushort.MaxValue + 1} keys.", nameof(keys));
        }

        _keysById = sortedKeys;
        _idsByKey = new Dictionary<string, ushort>(sortedKeys.Length, StringComparer.Ordinal);
        for (var index = 0; index < sortedKeys.Length; index++)
        {
            _idsByKey.Add(sortedKeys[index], (ushort)index);
        }
    }

    public int Count => _keysById.Length;

    public bool TryGetId(string key, out ushort id) =>
        _idsByKey.TryGetValue(key, out id);

    public ushort GetId(string key) =>
        _idsByKey.TryGetValue(key, out var id)
            ? id
            : throw new KeyNotFoundException($"Registry key '{key}' was not found.");

    public bool TryGetKey(ushort id, out string? key)
    {
        if (id < _keysById.Length)
        {
            key = _keysById[id];
            return true;
        }

        key = null;
        return false;
    }

    public string GetKey(ushort id)
    {
        if (id >= _keysById.Length)
        {
            throw new KeyNotFoundException($"Registry id '{id}' was not found.");
        }

        return _keysById[id];
    }
}
