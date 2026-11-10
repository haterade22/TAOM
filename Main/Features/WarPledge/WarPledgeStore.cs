using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TAOM.Features.Execution;

namespace TAOM.Features.WarPledge;

/// <summary>
/// The pledges of one campaign (#1113, docs/features/war-pledge.md): a Neutral realm that joined the War
/// of the Ring, and the side it joined. <c>AlignmentService.GetKingdomSide</c> and
/// <c>DiplomacyService.GetRelationshipTier</c> read it on every call, so a pledge changes the realm's
/// side and its wars at the source.
/// <para>
/// <b>Threads.</b> The party AI's late tick reads this store on a worker, by two routes:
/// <c>TaomMobilePartyAIModel</c> through <c>FireAndTributeService.IsOwnSide</c> (a side), and vanilla's
/// <c>MobilePartyAi</c> through <c>FactionManager.IsAtWarAgainstFaction</c>, <c>IsAtConstantWar</c> and
/// <c>DiplomacyService.GetRelationshipTier</c> (a tier). The daily tick cannot overlap that task in v1.5.7:
/// <c>Campaign.RealTick</c> waits for it (<c>WaitAsyncTasks</c>, Campaign.cs:907) before <c>Campaign.Tick</c>
/// fires the daily events. Whether a console command can run while the task is in flight is not known, so
/// the map is never changed after it is published: every write builds a new map and swaps the volatile
/// reference, and a reader takes the reference once. <see cref="TryGetSide"/> is the whole read path
/// (UnlookedForAidWiringTests and WarPledgeStoreTests pin both halves). <see cref="NextCourtingDay"/> is
/// read and written on the main thread only.
/// </para>
/// <para>
/// A process singleton with per-campaign state: <see cref="ResetForNewSession"/> runs from
/// <see cref="WarPledgeBehavior"/>'s constructor, which the module builds for every campaign, and a
/// load then calls <see cref="LoadFrom"/> over the reset.
/// </para>
/// </summary>
public sealed class WarPledgeStore
{
    /// <summary><see cref="NextCourtingDay"/> before the first Full War day is seen.</summary>
    public const int NoCourtingDay = -1;

    private const char RowSeparator = ';';
    private const char FieldSeparator = '=';

    private static readonly Dictionary<string, FactionSide> NoPledges = NewMap();

    // Published maps are read-only by contract: replace the reference, never add to or clear the map it names.
    private volatile Dictionary<string, FactionSide> _pledges = NoPledges;

    public int Count => _pledges.Count;

    /// <summary>
    /// The first whole campaign day the AI may court a realm again; <see cref="NoCourtingDay"/> until the
    /// daily step first sees Full War. Saved, so a reload of the same day changes nothing.
    /// </summary>
    public int NextCourtingDay { get; set; } = NoCourtingDay;

    public bool TryGetSide(string? kingdomId, out FactionSide side)
    {
        // Neutral on a miss, set by hand: a failed TryGetValue would leave the enum's zero value, which is Free.
        side = FactionSide.Neutral;
        var pledges = _pledges;
        if (pledges.Count == 0 || string.IsNullOrEmpty(kingdomId) || !pledges.TryGetValue(kingdomId!, out var found))
            return false;
        side = found;
        return true;
    }

    public bool IsPledged(string? kingdomId) => TryGetSide(kingdomId, out _);

    /// <summary>Records the pledge. False for a blank id, for Neutral, and for a realm that already pledged.</summary>
    public bool Pledge(string? kingdomId, FactionSide side)
    {
        var current = _pledges;
        if (string.IsNullOrWhiteSpace(kingdomId) || side == FactionSide.Neutral || current.ContainsKey(kingdomId!))
            return false;

        var next = NewMap();
        foreach (var pledge in current)
            next[pledge.Key] = pledge.Value;
        next[kingdomId!] = side;
        _pledges = next;
        return true;
    }

    /// <summary>Every pledge, sorted by kingdom id.</summary>
    public IReadOnlyList<KeyValuePair<string, FactionSide>> Snapshot() =>
        _pledges.OrderBy(p => p.Key, StringComparer.Ordinal).ToList();

    public void ResetForNewSession()
    {
        _pledges = NoPledges;
        NextCourtingDay = NoCourtingDay;
    }

    /// <summary>The pledges as one save string, <c>id=Side;id=Side</c>; empty without a pledge.</summary>
    public string CaptureRows()
    {
        var sb = new StringBuilder();
        foreach (var pledge in Snapshot())
        {
            if (sb.Length > 0)
                sb.Append(RowSeparator);
            sb.Append(pledge.Key).Append(FieldSeparator).Append(pledge.Value);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Replaces the store with a save's content, in one swap. A row that is not <c>id=Free</c> or
    /// <c>id=Evil</c> is skipped alone, and the first row of an id wins. A courting day below
    /// <see cref="NoCourtingDay"/> reads as none.
    /// </summary>
    public void LoadFrom(string? rows, int nextCourtingDay)
    {
        var loaded = NewMap();
        if (!string.IsNullOrEmpty(rows))
        {
            foreach (var row in rows!.Split(RowSeparator))
            {
                var fields = row.Split(FieldSeparator);
                if (fields.Length != 2)
                    continue;
                var id = fields[0].Trim();
                if (id.Length > 0 && !loaded.ContainsKey(id) && TryReadSide(fields[1], out var side))
                    loaded[id] = side;
            }
        }

        _pledges = loaded.Count == 0 ? NoPledges : loaded;
        NextCourtingDay = nextCourtingDay < NoCourtingDay ? NoCourtingDay : nextCourtingDay;
    }

    // AlignmentService compares ids without case, so the override does too.
    private static Dictionary<string, FactionSide> NewMap() =>
        new Dictionary<string, FactionSide>(StringComparer.OrdinalIgnoreCase);

    // By name only: Enum.TryParse also accepts a number, and a saved number is not a side this build wrote.
    private static bool TryReadSide(string text, out FactionSide side)
    {
        side = FactionSide.Neutral;
        var name = text.Trim();
        if (string.Equals(name, nameof(FactionSide.Free), StringComparison.Ordinal))
            side = FactionSide.Free;
        else if (string.Equals(name, nameof(FactionSide.Evil), StringComparison.Ordinal))
            side = FactionSide.Evil;
        return side != FactionSide.Neutral;
    }
}
