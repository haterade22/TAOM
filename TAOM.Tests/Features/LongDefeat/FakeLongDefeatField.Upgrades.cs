using System;
using System.Collections.Generic;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat;

/// <summary>The upgrades' half of the field fake (#1161): a read answers the standing counts a test set, and a replace moves soldiers between those counts unless a test set its answer.</summary>
internal sealed partial class FakeLongDefeatField
{
    /// <summary>The standing defenders by troop id that a read reports; a replace moves soldiers from the old troop to the new one.</summary>
    public Dictionary<string, int> StandingByTroop { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>What the next replace answers instead of moving soldiers, or null to move them.</summary>
    public LdUpgradeReport? ReplaceAnswer { get; set; }

    /// <summary>Every replace asked for, in order.</summary>
    public List<(string From, string To, int Count, float HealthFactor)> Replaces { get; } = new();

    public IReadOnlyDictionary<string, int> StandingDefendersByTroop()
    {
        Calls.Add("StandingDefendersByTroop");
        return new Dictionary<string, int>(StandingByTroop, StringComparer.OrdinalIgnoreCase);
    }

    public LdUpgradeReport ReplaceDefenders(string fromTroop, string toTroop, int count, float healthFactor)
    {
        Calls.Add("ReplaceDefenders");
        Replaces.Add((fromTroop, toTroop, count, healthFactor));
        if (ReplaceAnswer is { } answer)
        {
            ReplaceAnswer = null;
            return answer;
        }

        var found = StandingByTroop.TryGetValue(fromTroop, out var standing) ? standing : 0;
        var done = Math.Min(found, count);
        StandingByTroop[fromTroop] = found - done;
        StandingByTroop[toTroop] = (StandingByTroop.TryGetValue(toTroop, out var had) ? had : 0) + done;
        return new LdUpgradeReport(found, done, done, null);
    }
}
