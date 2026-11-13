using System;
using System.Collections.Generic;

namespace TAOM.Features.MissionPerf.AnimMemory;

/// <summary>
/// One run of signature searches over a module's code, answering from the address cache where it can. An entry is stored
/// under the search's id plus a hash of its pattern, so an address found with another pattern is a miss. A remembered
/// address counts as a hit only when the pattern still matches the bytes at it; a miss runs the full
/// <see cref="ClipBudgetSignature.Find"/> and remembers a single match. A hit stands for exactly one match, and that is
/// trusted, not checked: the cache's key says the DLL is the one scanned before, the entry's hash says the pattern is the
/// one used then, and a pattern that matched once then matches once now. A second copy of the pattern elsewhere in the
/// code cannot be seen on a hit. <see cref="Complete"/> writes the cache back if anything changed.
/// </summary>
public sealed class SignatureScanSession
{
    /// <summary>No cache: every search is a full scan and nothing is remembered.</summary>
    internal static readonly SignatureScanSession Disabled = new(0, new Dictionary<string, int>(), null);

    private readonly int _textRva;
    private readonly Dictionary<string, int> _entries;
    private readonly Action<Dictionary<string, int>>? _save;
    private readonly HashSet<string> _cachedIds = new();
    private bool _scanned;
    private bool _dirty;

    internal SignatureScanSession(int textRva, Dictionary<string, int> entries, Action<Dictionary<string, int>>? save)
    {
        _textRva = textRva;
        _entries = entries;
        _save = save;
    }

    /// <summary><c>cached</c> when every answer came from the cache, otherwise <c>scanned</c>; goes in the log line next to scanMs.</summary>
    public string Path => !_scanned && _cachedIds.Count > 0 ? "cached" : "scanned";

    internal bool WasCached(string id) => _cachedIds.Contains(id);

    /// <summary>
    /// A stable hash (FNV-1a) of a parsed pattern: every byte and wildcard in order, so any edit to the pattern changes it.
    /// Eight hex digits, no spaces, so it can sit in the id of a cache line.
    /// </summary>
    internal static string PatternHash(int[] pattern)
    {
        unchecked
        {
            var hash = 2166136261u;
            hash = (hash ^ (uint)pattern.Length) * 16777619u;
            foreach (var value in pattern)
                hash = (hash ^ (uint)(value + 1)) * 16777619u;
            return hash.ToString("X8", System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    /// <summary>Start offsets of <paramref name="pattern"/> in <paramref name="text"/>, at most <paramref name="maxHits"/>, remembered under <paramref name="id"/> and the pattern's hash.</summary>
    internal List<int> Find(string id, byte[] text, int[] pattern, int maxHits)
    {
        var key = id + "#" + PatternHash(pattern);
        if (_save != null)
        {
            DropOtherPatterns(id, key);
            if (_entries.TryGetValue(key, out var rva))
            {
                var offset = rva - _textRva;
                if (offset >= 0 && offset <= text.Length - pattern.Length && MatchesAt(text, offset, pattern))
                {
                    _cachedIds.Add(id);
                    return new List<int> { offset };
                }
                _entries.Remove(key);
                _dirty = true;
            }
        }

        var hits = FindPlain(text, pattern, maxHits);
        if (hits.Count == 1 && _save != null)
        {
            _entries[key] = _textRva + hits[0];
            _dirty = true;
        }
        return hits;
    }

    /// <summary>A full scan that is not remembered (a pattern whose normal result is no match).</summary>
    internal List<int> FindPlain(byte[] text, int[] pattern, int maxHits)
    {
        if (_save != null)    // the shared Disabled session keeps no state
            _scanned = true;
        return ClipBudgetSignature.Find(text, pattern, maxHits);
    }

    /// <summary>Writes the cache back when this session changed it. Never throws.</summary>
    public void Complete()
    {
        if (!_dirty || _save == null)
            return;
        _dirty = false;
        _save(_entries);
    }

    /// <summary>An entry of the same id with another pattern hash was made for an older pattern: it is not used and is not written back.</summary>
    private void DropOtherPatterns(string id, string key)
    {
        var prefix = id + "#";
        List<string>? stale = null;
        foreach (var existing in _entries.Keys)
        {
            if (existing != key && existing.StartsWith(prefix, StringComparison.Ordinal))
                (stale ??= new List<string>()).Add(existing);
        }
        if (stale == null)
            return;
        foreach (var existing in stale)
            _entries.Remove(existing);
        _dirty = true;
    }

    private static bool MatchesAt(byte[] haystack, int offset, int[] pattern)
    {
        for (var j = 0; j < pattern.Length; j++)
        {
            if (pattern[j] != -1 && haystack[offset + j] != pattern[j])
                return false;
        }
        return true;
    }
}
