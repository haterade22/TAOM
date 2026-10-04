using System;

namespace TAOM.Features.LocalizationOverride;

/// <summary>
/// A (string, start, length) slice that keys the English override table, so the per-call probe in
/// <see cref="Hooks.MBTextManager_GetLocalizedText_Patch"/> reads a <c>{=ID}</c> in place instead of
/// cutting it out into a new string. Equality is ordinal and case-sensitive over the slice's chars,
/// exactly as the <c>Substring</c> string key it replaces; the length is checked first, so an id that is
/// a prefix of another never matches. As an <see cref="IEquatable{T}"/> struct it is compared by the
/// dictionary's default comparer without boxing.
/// </summary>
internal readonly struct IdSlice : IEquatable<IdSlice>
{
    private readonly string _source;
    private readonly int _start;
    private readonly int _length;

    public IdSlice(string source, int start, int length)
    {
        _source = source;
        _start = start;
        _length = length;
    }

    public bool Equals(IdSlice other) =>
        _length == other._length && string.CompareOrdinal(_source, _start, other._source, other._start, _length) == 0;

    public override bool Equals(object? obj) => obj is IdSlice other && Equals(other);

    // FNV-1a over the slice's chars, so equal slices of different strings hash alike.
    public override int GetHashCode()
    {
        unchecked
        {
            int hash = (int)2166136261;
            for (int i = _start, end = _start + _length; i < end; i++)
                hash = (hash ^ _source[i]) * 16777619;
            return hash;
        }
    }
}
