using System;
using System.Collections.Generic;
using System.Globalization;
using TAOM.Features.ArmourAcquisition.Domain;

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

    private const char Separator = '|';

    /// <summary>
    /// The lord's gear ladder per hero: which rungs are claimed, one bit per <c>LadderSlot</c> value (absent:
    /// none), so a config that reorders or drops a rung leaves every hero at their first unclaimed one. Whether
    /// a rung's quest is running is the quest manager's to say.
    /// </summary>
    public Dictionary<string, int> LadderClaimed { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// The rungs each hero has done (by deeds or by materials) and not yet claimed, one bit per <c>LadderSlot</c>
    /// value like <see cref="LadderClaimed"/>: a rung's readiness stays with its slot whatever the config does.
    /// </summary>
    public Dictionary<string, int> LadderReady { get; } = new(StringComparer.Ordinal);

    /// <summary>The day each hero last saw the Lord's Harness event.</summary>
    public Dictionary<string, int> LordEventLastDay { get; } = new(StringComparer.Ordinal);

    /// <summary>Settlement id to the day a visiting master armourer leaves (the visit is over on that day).</summary>
    public Dictionary<string, int> VisitUntilDay { get; } = new(StringComparer.Ordinal);

    public void Reset()
    {
        LadderClaimed.Clear();
        LadderReady.Clear();
        LordEventLastDay.Clear();
        VisitUntilDay.Clear();
    }

    public Dictionary<string, string> Encode()
    {
        var data = new Dictionary<string, string> { ["v"] = Version.ToString(CultureInfo.InvariantCulture) };
        Write(data, "rung", LadderClaimed);
        Write(data, "ready", LadderReady);
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
            if (!IsKnown(kind))
                continue;
            if (!int.TryParse(pair.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                || (kind is "rung" or "ready" && (value < 0 || value > LadderSlotRules.AllBits)))
            {
                skipped++;
                continue;
            }
            Target(kind)[id] = value;
        }
        return skipped;
    }

    private static bool IsKnown(string kind) => kind is "rung" or "ready" or "event" or "visit";

    private Dictionary<string, int> Target(string kind) => kind switch
    {
        "rung" => LadderClaimed,
        "ready" => LadderReady,
        "event" => LordEventLastDay,
        "visit" => VisitUntilDay,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "not a saved dictionary kind"),
    };

    private static void Write(Dictionary<string, string> data, string kind, Dictionary<string, int> values)
    {
        foreach (var pair in values)
            data[kind + Separator + pair.Key] = pair.Value.ToString(CultureInfo.InvariantCulture);
    }
}
