using System.Collections.Generic;
using System.Text;

namespace TAOM.Features.WarOfTheRingMomentum;

/// <summary>
/// Splits the momentum state's serialized JSON across multiple SyncData strings so that no
/// single string ever exceeds the engine's per-archive-entry limit.
///
/// WHY: the engine's <c>ArchiveSerializer.SerializeEntry</c> writes each save-archive
/// entry's length as <c>(short)Data.Length</c> — a signed int16 truncation. Any single
/// SyncData string whose UTF-8 payload exceeds 32,767 bytes is written with a wrong length
/// and CORRUPTS THE SAVE at write time (unloadable next launch). A developed campaign's
/// momentum log crosses that as one JSON string around day ~50. Chunking guarantees every
/// synced string stays under the cap regardless of how the log grows — no data dropped.
///
/// <see cref="MaxChunkChars"/> is a CHARACTER cap sized so the worst-case UTF-8 expansion
/// (3 bytes per UTF-16 code unit for the BMP; surrogate pairs are 2 chars → 4 bytes, i.e.
/// 2 bytes/char, so 3/char is the true ceiling) can never exceed the byte limit:
/// 10,000 chars × 3 bytes = 30,000 bytes &lt; 32,767. Pure + allocation-simple; testable.
/// </summary>
public static class MomentumSyncChunker
{
    // 10,000 UTF-16 code units → ≤ 30,000 UTF-8 bytes worst case, a safe margin under 32,767.
    public const int MaxChunkChars = 10000;

    // The largest UTF-8 payload the engine can represent in one archive entry: 32,767 total
    // minus the 4-byte ReadString length prefix stored inside the entry data.
    public const int EngineEntryByteLimit = 32763;

    public static List<string> Split(string full, int maxChunkChars = MaxChunkChars)
    {
        var chunks = new List<string>();
        if (string.IsNullOrEmpty(full))
            return chunks;

        for (int i = 0; i < full.Length; i += maxChunkChars)
        {
            int len = full.Length - i;
            if (len > maxChunkChars) len = maxChunkChars;
            chunks.Add(full.Substring(i, len));
        }
        return chunks;
    }

    public static string Join(IReadOnlyList<string> chunks)
    {
        if (chunks == null || chunks.Count == 0)
            return string.Empty;
        if (chunks.Count == 1)
            return chunks[0] ?? string.Empty;

        var sb = new StringBuilder();
        foreach (var chunk in chunks)
            sb.Append(chunk);
        return sb.ToString();
    }

    /// <summary>UTF-8 byte length of a chunk — the quantity the engine's int16 length field must hold.</summary>
    public static int Utf8ByteLength(string chunk) =>
        string.IsNullOrEmpty(chunk) ? 0 : Encoding.UTF8.GetByteCount(chunk);
}
