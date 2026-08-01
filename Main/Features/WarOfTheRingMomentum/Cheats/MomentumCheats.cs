using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using TaleWorlds.Library;
using TAOM.Features.DevConsole;

namespace TAOM.Features.WarOfTheRingMomentum.Cheats;

/// <summary>
/// `taom.print_momentum [keys]` — how close the momentum save payload is to the engine cliff.
///
/// Why it exists: `ArchiveSerializer.SerializeEntry` writes each archive entry's length as
/// `(short)Data.Length`, so any single SyncData string over the int16 ceiling is written with a wrong
/// length and corrupts the save AT WRITE TIME — unloadable next launch.
/// <see cref="MomentumSyncChunker"/> keeps every chunk under the cap, but verifying that today costs
/// hours: a campaign has to reach roughly day 50 with active wars before the log crosses the
/// threshold, and the failure only shows on the next load. This reads the same number on demand.
///
/// Read-only by construction: <see cref="IMomentumStateStore.Serialize"/> builds a fresh dictionary
/// and mutates nothing, and the payload is assembled through the same helper `SyncData` uses so the
/// report can never describe a shape the save does not actually write.
/// </summary>
public static class MomentumCheats
{
    private const int TopKeys = 10;

    private const string Usage =
        "Format is \"taom.print_momentum [keys]\".\n"
        + "Prints the momentum save payload's size, chunk count, and headroom against the engine's\n"
        + "per-archive-entry byte limit. Add \"keys\" to also list the largest individual state keys.";

    [CommandLineFunctionality.CommandLineArgumentFunction("print_momentum", "taom")]
    public static string PrintMomentum(List<string> strings) =>
        TaomConsole.RunInCampaign(strings, Usage, args =>
        {
            var store = IoC.Resolve<IMomentumStateStore>();
            var state = store.Serialize();

            // The SAME call SyncData makes, not a copy of it — a duplicated expression could drift if
            // the save serialization ever gained settings, and this command would then cheerfully
            // report headroom for a payload shape the save no longer writes.
            var payload = WarOfTheRingMomentumBehavior.BuildSavePayload(store);
            var chunks = MomentumSyncChunker.Split(payload);

            var report = FormatPayloadReport(state?.Count ?? 0, payload, chunks);

            var wantsKeys = args.Count > 0 && args[0].Trim().ToLowerInvariant() == "keys";
            return wantsKeys ? report + "\n" + FormatKeyReport(state, TopKeys) : report;
        });

    /// <summary>
    /// Pure. Reports chars AND bytes separately — the chunker's cap is a character cap while the
    /// engine's is a byte cap, and conflating them would understate how close a non-ASCII save sits to
    /// the cliff.
    /// </summary>
    internal static string FormatPayloadReport(int keyCount, string payload, IReadOnlyList<string> chunks)
    {
        payload = payload ?? string.Empty;
        chunks = chunks ?? new List<string>();

        var totalBytes = MomentumSyncChunker.Utf8ByteLength(payload);
        var largestBytes = 0;
        var offendingChunk = -1;

        for (var i = 0; i < chunks.Count; i++)
        {
            var bytes = MomentumSyncChunker.Utf8ByteLength(chunks[i] ?? string.Empty);
            if (bytes > largestBytes) largestBytes = bytes;
            if (offendingChunk < 0 && bytes > MomentumSyncChunker.EngineEntryByteLimit) offendingChunk = i + 1;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"[Momentum] payload: {keyCount} keys, {payload.Length:N0} chars, {totalBytes:N0} bytes");
        sb.AppendLine(
            $"[Momentum] chunks: {chunks.Count} (cap {MomentumSyncChunker.MaxChunkChars:N0} chars) | "
            + $"largest {largestBytes:N0} bytes | engine limit {MomentumSyncChunker.EngineEntryByteLimit:N0} | "
            + $"headroom {MomentumSyncChunker.EngineEntryByteLimit - largestBytes:N0} bytes");

        if (offendingChunk > 0)
            sb.Append(
                $"[Momentum] status: WOULD CORRUPT — chunk {offendingChunk} is {largestBytes:N0} bytes "
                + $"(> {MomentumSyncChunker.EngineEntryByteLimit:N0}). Saving now would write a truncated "
                + "int16 length and produce an unloadable save.");
        else
            sb.Append("[Momentum] status: OK (every chunk is under the engine entry limit)");

        return sb.ToString();
    }

    /// <summary>
    /// Pure. Largest keys first — when the payload is near the cliff the question is always "which key
    /// is growing", and the answer points straight at the offending event log.
    /// </summary>
    internal static string FormatKeyReport(Dictionary<string, string> state, int top)
    {
        if (state == null || state.Count == 0)
            return "[Momentum] no keys in the momentum state.";

        var ranked = state
            .Select(kv => new { kv.Key, Bytes = MomentumSyncChunker.Utf8ByteLength(kv.Value ?? string.Empty) })
            .OrderByDescending(x => x.Bytes)
            .ThenBy(x => x.Key)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine($"[Momentum] largest keys ({System.Math.Min(top, ranked.Count)} of {ranked.Count}):");
        foreach (var row in ranked.Take(top))
            sb.AppendLine($"[Momentum]   {row.Key} = {row.Bytes:N0} bytes");

        return sb.ToString().TrimEnd();
    }
}
