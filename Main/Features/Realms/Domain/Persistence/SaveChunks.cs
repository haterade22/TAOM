using System.Collections.Generic;
using System.Text;

namespace TAOM.Features.Realms.Domain.Persistence;

/// <summary>
/// Splits the save payload into strings the engine can store safely and joins them back.
/// The engine writes a SyncData string's length as a signed 16-bit value, so one string over
/// about 32 KB of UTF-8 corrupts the save (documented in TAOM's SyncDataChunker). 10,000 UTF-16
/// characters are at most 30,000 UTF-8 bytes. A cut never falls inside a surrogate pair.
/// </summary>
public static class SaveChunks
{
    public const int MaxChunkChars = 10000;

    public static IReadOnlyList<string> Split(string payload, int maxChars = MaxChunkChars)
    {
        var chunks = new List<string>();
        var start = 0;
        while (start < payload.Length)
        {
            var length = System.Math.Min(maxChars, payload.Length - start);
            if (start + length < payload.Length && char.IsHighSurrogate(payload[start + length - 1]))
                length--;
            chunks.Add(payload.Substring(start, length));
            start += length;
        }

        return chunks;
    }

    public static string Join(IEnumerable<string> chunks)
    {
        var builder = new StringBuilder();
        foreach (var chunk in chunks)
            builder.Append(chunk);
        return builder.ToString();
    }
}
