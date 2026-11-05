using System.Collections.Generic;
using System.Text;
using TAOM.Core.Logging;
using TaleWorlds.CampaignSystem;

namespace TAOM.Core.Persistence;

/// <summary>
/// Splits a serialized save payload across multiple SyncData strings so that no single string
/// ever exceeds the engine's per-archive-entry limit, and syncs them as one count key plus that
/// many chunk keys (<see cref="SyncChunks"/>). Used by the War of the Ring momentum behavior.
///
/// WHY: the engine's <c>ArchiveSerializer.SerializeEntry</c> writes each save-archive
/// entry's length as <c>(short)Data.Length</c> — a signed int16 truncation. Any single
/// SyncData string whose UTF-8 payload exceeds 32,763 bytes (the engine writes a short length
/// of 4 plus the bytes, v1.4.8 <c>SaveContext.SaveStringTo</c>, SaveContext.cs:308-321) is written with a wrong length
/// and CORRUPTS THE SAVE at write time (unloadable next launch). A developed campaign's
/// momentum log crosses that as one JSON string around day ~50. Chunking guarantees every
/// synced string stays under the cap regardless of how the log grows — no data dropped.
///
/// <see cref="MaxChunkChars"/> is a CHARACTER cap sized so the worst-case UTF-8 expansion
/// (3 bytes per UTF-16 code unit for the BMP; surrogate pairs are 2 chars → 4 bytes, i.e.
/// 2 bytes/char, so 3/char is the true ceiling) can never exceed the byte limit:
/// 10,000 chars × 3 bytes = 30,000 bytes &lt; 32,763. Pure + allocation-simple; testable.
/// </summary>
public static class SyncDataChunker
{
    // 10,000 UTF-16 code units → ≤ 30,000 UTF-8 bytes worst case, a safe margin under 32,763.
    public const int MaxChunkChars = 10000;

    // 4,096 chunks of 10,000 chars is 40 MB of payload: far past any real save, so a saved count beyond
    // it is a corrupt or foreign value, not a payload to loop over.
    public const int MaxChunks = 4096;

    /// <summary>
    /// One count-and-chunks SyncData pass. Saving: writes the count under <paramref name="countKey"/>
    /// and each of <paramref name="toSave"/> under <paramref name="chunkKeyPrefix"/> plus its index, and
    /// returns them. Loading: returns the saved chunks (a null chunk reads as empty), or none when the
    /// record is absent or its count is negative or past <see cref="MaxChunks"/>, which is logged as a
    /// warning under <paramref name="logTag"/>. Neither: returns none.
    /// </summary>
    public static IReadOnlyList<string> SyncChunks(
        IDataStore dataStore, string countKey, string chunkKeyPrefix, IReadOnlyList<string>? toSave,
        IModLogger logger, string logTag)
    {
        if (dataStore.IsSaving)
        {
            var chunks = toSave ?? new List<string>();
            var count = chunks.Count;
            dataStore.SyncData(countKey, ref count);
            for (var i = 0; i < count; i++)
            {
                var chunk = chunks[i];
                dataStore.SyncData(chunkKeyPrefix + i, ref chunk);
            }

            return chunks;
        }

        var saved = new List<string>();
        if (!dataStore.IsLoading)
            return saved;

        var savedCount = 0;
        dataStore.SyncData(countKey, ref savedCount);
        if (savedCount < 0 || savedCount > MaxChunks)
        {
            logger.LogWarning($"{logTag} Save: the chunk count {savedCount} is out of range, so the payload is ignored.");
            return saved;
        }

        for (var i = 0; i < savedCount; i++)
        {
            var chunk = string.Empty;
            dataStore.SyncData(chunkKeyPrefix + i, ref chunk);
            saved.Add(chunk ?? string.Empty);
        }

        return saved;
    }

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
