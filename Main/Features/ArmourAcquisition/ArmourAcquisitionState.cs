using System;
using System.Collections.Generic;
using System.Globalization;

namespace TAOM.Features.ArmourAcquisition;

/// <summary>
/// The feature's campaign state, one container-singleton shared by its behaviors and services and saved
/// by <c>ArmourAcquisitionCampaignBehavior.SyncData</c> as one flat Dictionary&lt;string, string&gt;
/// (the SiegeDefense shape; no SaveableTypeDefiner). Keys are "v" and "kind|id". It outlives a campaign
/// in the process, so the behavior resets it whenever a session starts without loading it
/// (csharp-architecture.md, "Singleton Services Holding Per-Campaign State").
/// </summary>
public sealed class ArmourAcquisitionState
{
    public const int Version = 1;

    /// <summary>
    /// The Lord's Harness per hero: absent while the quest has not been completed (whether it is running
    /// is the quest manager's to say), then ready to claim, then claimed.
    /// </summary>
    public const int HarnessReady = 1;

    public const int HarnessClaimed = 2;

    private const char Separator = '|';

    public Dictionary<string, int> HarnessStage { get; } = new(StringComparer.Ordinal);

    /// <summary>The day each hero last declined the quest offer.</summary>
    public Dictionary<string, int> HarnessDeclinedDay { get; } = new(StringComparer.Ordinal);

    /// <summary>The day each hero last saw the Lord's Harness event.</summary>
    public Dictionary<string, int> LordEventLastDay { get; } = new(StringComparer.Ordinal);

    /// <summary>Settlement id to the day a visiting master armourer leaves (the visit is over on that day).</summary>
    public Dictionary<string, int> VisitUntilDay { get; } = new(StringComparer.Ordinal);

    public void Reset()
    {
        HarnessStage.Clear();
        HarnessDeclinedDay.Clear();
        LordEventLastDay.Clear();
        VisitUntilDay.Clear();
    }

    public Dictionary<string, string> Encode()
    {
        var data = new Dictionary<string, string> { ["v"] = Version.ToString(CultureInfo.InvariantCulture) };
        Write(data, "stage", HarnessStage);
        Write(data, "declined", HarnessDeclinedDay);
        Write(data, "event", LordEventLastDay);
        Write(data, "visit", VisitUntilDay);
        return data;
    }

    /// <summary>Replaces the state with <paramref name="data"/>; returns how many entries were refused as damaged.</summary>
    public int Decode(IDictionary<string, string>? data)
    {
        Reset();
        if (data == null)
            return 0;

        var skipped = 0;
        foreach (var pair in data)
        {
            var cut = pair.Key.IndexOf(Separator);
            if (cut <= 0 || cut == pair.Key.Length - 1)
                continue;
            var kind = pair.Key.Substring(0, cut);
            var id = pair.Key.Substring(cut + 1);
            var target = Target(kind);
            if (target == null)
                continue;
            if (!int.TryParse(pair.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                || (kind == "stage" && value != HarnessReady && value != HarnessClaimed))
            {
                skipped++;
                continue;
            }
            target[id] = value;
        }
        return skipped;
    }

    private Dictionary<string, int>? Target(string kind) => kind switch
    {
        "stage" => HarnessStage,
        "declined" => HarnessDeclinedDay,
        "event" => LordEventLastDay,
        "visit" => VisitUntilDay,
        _ => null,
    };

    private static void Write(Dictionary<string, string> data, string kind, Dictionary<string, int> values)
    {
        foreach (var pair in values)
            data[kind + Separator + pair.Key] = pair.Value.ToString(CultureInfo.InvariantCulture);
    }
}
