using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TAOM.Features.SaveLoadDiagnostics.Domain;

// How much one campaign behavior put in its save record (plan 114, SAVE-TIME-05): records, elements
// in its collections, and its longest string, since one string over 32,763 UTF-8 bytes corrupts a save.
public readonly struct BehaviorRecordStats
{
    // A record's string scan stops here so one huge collection cannot lengthen the save. Counts stay exact.
    private const int ScanCap = 200_000;

    public BehaviorRecordStats(int records, long elements, int maxStringBytes, string? maxStringKey)
    {
        Records = records;
        Elements = elements;
        MaxStringBytes = maxStringBytes;
        MaxStringKey = maxStringKey;
    }

    public int Records { get; }
    public long Elements { get; }
    public int MaxStringBytes { get; }
    public string? MaxStringKey { get; }

    public static BehaviorRecordStats Measure(IDictionary<string, object>? records)
    {
        if (records == null) return default;

        long elements = 0;
        int maxBytes = 0;
        string? maxKey = null;
        foreach (var record in records)
        {
            int bytes = 0;
            switch (record.Value)
            {
                case string text:
                    bytes = Encoding.UTF8.GetByteCount(text);
                    break;
                case IDictionary dictionary:
                    elements += dictionary.Count;
                    bytes = LongestString(FlattenEntries(dictionary));
                    break;
                case ICollection collection:
                    elements += collection.Count;
                    bytes = LongestString(collection);
                    break;
            }
            if (bytes > maxBytes)
            {
                maxBytes = bytes;
                maxKey = record.Key;
            }
        }
        return new BehaviorRecordStats(records.Count, elements, maxBytes, maxKey);
    }

    private static IEnumerable FlattenEntries(IDictionary dictionary)
    {
        foreach (DictionaryEntry entry in dictionary)
        {
            yield return entry.Key;
            yield return entry.Value;
        }
    }

    private static int LongestString(IEnumerable items)
    {
        int longest = 0;
        int scanned = 0;
        foreach (var item in items)
        {
            if (++scanned > ScanCap) break;
            if (item is string text)
                longest = Math.Max(longest, Encoding.UTF8.GetByteCount(text));
        }
        return longest;
    }
}

internal readonly struct BehaviorSaveSample
{
    public BehaviorSaveSample(string id, string type, BehaviorRecordStats stats, double syncMs)
    {
        Id = id;
        Type = type;
        Stats = stats;
        SyncMs = syncMs;
    }

    public string Id { get; }
    public string Type { get; }
    public BehaviorRecordStats Stats { get; }
    public double SyncMs { get; }
}

// One summary line per save, then a row only for a behavior worth naming.
internal static class BehaviorSaveSummary
{
    internal const int ElementsThreshold = 10_000;
    internal const int StringBytesThreshold = 30_000; // the entry limit is 32,763 bytes
    internal const int SyncMsThreshold = 250;

    public static IReadOnlyList<string> Format(IReadOnlyCollection<BehaviorSaveSample> samples)
    {
        var lines = new List<string>();
        if (samples.Count == 0) return lines;

        var longest = samples.OrderByDescending(s => s.Stats.MaxStringBytes).First();
        var slowest = samples.OrderByDescending(s => s.SyncMs).First();
        var maxString = longest.Stats.MaxStringBytes == 0
            ? string.Empty
            : $" maxStringBehavior='{longest.Id}:{longest.Stats.MaxStringKey}'";
        lines.Add(FormattableString.Invariant(
            $"behaviors={samples.Count} records={samples.Sum(s => s.Stats.Records)} elements={samples.Sum(s => s.Stats.Elements)} syncMs={samples.Sum(s => s.SyncMs):F1} maxStringBytes={longest.Stats.MaxStringBytes}{maxString} slowest='{slowest.Id}' slowestMs={slowest.SyncMs:F1}"));

        var flagged = samples
            .Where(s => s.Stats.Elements >= ElementsThreshold || s.Stats.MaxStringBytes >= StringBytesThreshold || s.SyncMs >= SyncMsThreshold)
            .OrderByDescending(s => s.Stats.Elements)
            .ThenBy(s => s.Id, StringComparer.Ordinal);
        foreach (var s in flagged)
            lines.Add(FormattableString.Invariant(
                $"behavior='{s.Id}' type='{s.Type}' records={s.Stats.Records} elements={s.Stats.Elements} maxStringBytes={s.Stats.MaxStringBytes} syncMs={s.SyncMs:F1}"));
        return lines;
    }
}
