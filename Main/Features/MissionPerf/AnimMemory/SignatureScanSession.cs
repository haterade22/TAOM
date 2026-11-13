using System;
using System.Collections.Generic;

namespace TAOM.Features.MissionPerf.AnimMemory;

/// <summary>
/// One run of signature searches over a module's code, answering from the address cache where it can. A remembered
/// address counts as a hit only when the pattern still matches the bytes at it; a miss runs the full
/// <see cref="ClipBudgetSignature.Find"/> and remembers a single match. A hit stands for exactly one match, which is what
/// the cache stored: the file's key (size, write time, header fields) says the DLL has not changed, so the pattern
/// that was unique then is unique now. <see cref="Complete"/> writes the cache back if anything changed.
/// </summary>
public sealed class SignatureScanSession
{
    /// <summary>No cache: every search is a full scan and nothing is remembered (shared, so it keeps no state).</summary>
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

    /// <summary>Start offsets of <paramref name="pattern"/> in <paramref name="text"/>, at most <paramref name="maxHits"/>, remembered under <paramref name="id"/>.</summary>
    internal List<int> Find(string id, byte[] text, int[] pattern, int maxHits)
    {
        if (_save != null && _entries.TryGetValue(id, out var rva))
        {
            var offset = rva - _textRva;
            if (offset >= 0 && offset <= text.Length - pattern.Length && MatchesAt(text, offset, pattern))
            {
                _cachedIds.Add(id);
                return new List<int> { offset };
            }
            _entries.Remove(id);
            _dirty = true;
        }

        var hits = FindPlain(text, pattern, maxHits);
        if (hits.Count == 1 && _save != null)
        {
            _entries[id] = _textRva + hits[0];
            _dirty = true;
        }
        return hits;
    }

    /// <summary>A full scan that is not remembered (a pattern whose normal result is no match).</summary>
    internal List<int> FindPlain(byte[] text, int[] pattern, int maxHits)
    {
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
