using System;
using System.Collections.Generic;
using TAOM.Core.Domain;
using TAOM.Features.SiegeForces.Domain;

namespace TAOM.Features.SiegeForces;

/// <summary>
/// The siege troop picker's pure rules (docs/features/siege-forces.md): who is offered, what starts ticked, how the
/// picked roster maps back to the parties it came from, which entries a plan drops and how the spawn totals shrink.
/// No engine type reaches here: the adapter hands in a snapshot of plain values.
/// </summary>
public static class SiegeForcesRules
{
    /// <summary>The least the selection screen accepts: the player himself, who is always ticked and locked.</summary>
    public const int MinSelectable = 1;

    /// <summary>
    /// The parties whose troops the player chooses, in fill order: the main party, then the parties of his army when
    /// he leads it, then the garrison when he defends a fief his own clan owns. A lord's party outside the army, and
    /// every party of an army the player merely follows, stay out of scope and fight as vanilla sends them.
    /// </summary>
    public static IReadOnlyList<SiegeParty> PartiesInScope(SiegeForcesSnapshot snapshot)
    {
        if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

        var main = new List<SiegeParty>();
        var army = new List<SiegeParty>();
        var garrison = new List<SiegeParty>();
        foreach (var party in snapshot.Parties)
        {
            if (party.IsMainParty)
                main.Add(party);
            else if (snapshot.PlayerLeadsArmy && party.IsInPlayerArmy)
                army.Add(party);
            else if (snapshot.DefendingOwnFief && party.IsGarrison)
                garrison.Add(party);
        }

        var inScope = new List<SiegeParty>(main.Count + army.Count + garrison.Count);
        inScope.AddRange(main);
        inScope.AddRange(army);
        inScope.AddRange(garrison);
        return inScope;
    }

    /// <summary>
    /// What the troop selection screen opens with, or null when there is nothing to choose (the player is the only
    /// one who can fight). One row per character, aggregated across the parties in scope, in fill order. The player
    /// starts ticked; an oversized creature starts unticked when <paramref name="startOversizedUnticked"/>; everyone
    /// else healthy starts ticked; the wounded are listed but never ticked. The oversized race ids are resolved once
    /// (<see cref="OversizedCreatureRaces.ResolveRaceIds"/>, validate-before-lookup), so a race the table does not hold
    /// is never oversized and no troop's race is looked up by name.
    /// </summary>
    public static PickerRequest? BuildRequest(SiegeForcesSnapshot snapshot, bool startOversizedUnticked, IRaceManager raceManager)
    {
        if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

        var order = new List<RowTally>();
        var byId = new Dictionary<string, RowTally>(StringComparer.Ordinal);
        foreach (var party in PartiesInScope(snapshot))
        {
            foreach (var troop in party.Troops)
            {
                if (troop == null || troop.CharacterId == null) continue;
                if (!byId.TryGetValue(troop.CharacterId, out var tally))
                {
                    tally = new RowTally(troop);
                    byId[troop.CharacterId] = tally;
                    order.Add(tally);
                }

                tally.Number += troop.Number;
                tally.Wounded += troop.Wounded;
                tally.Healthy += troop.Healthy;
            }
        }

        var oversizedRaces = startOversizedUnticked ? OversizedCreatureRaces.ResolveRaceIds(raceManager) : Array.Empty<int>();
        var rows = new List<PickerRow>(order.Count);
        var healthyTotal = 0;
        var anyoneElseCanFight = false;
        foreach (var tally in order)
        {
            int initial;
            if (tally.IsPlayerCharacter)
                initial = Math.Min(MinSelectable, tally.Healthy);
            else if (Array.IndexOf(oversizedRaces, tally.RaceId) >= 0)
                initial = 0;
            else
                initial = tally.Healthy;

            if (!tally.IsPlayerCharacter && tally.Healthy > 0)
                anyoneElseCanFight = true;
            healthyTotal += tally.Healthy;
            rows.Add(new PickerRow(tally.Source, tally.CharacterId, tally.Number, tally.Wounded, initial));
        }

        return anyoneElseCanFight ? new PickerRequest(rows, healthyTotal, MinSelectable) : null;
    }

    /// <summary>
    /// Maps the picked roster back to the parties it came from. Walking the parties in fill order, each keeps
    /// <c>min(healthy, still-picked)</c> of every character, so the main party's own troops fight first. The player is
    /// always kept and takes nothing from the selection. An id the selection holds and no party does is ignored, a
    /// negative pick counts as zero, and a party out of scope is not in the plan at all.
    /// </summary>
    public static SiegeForcesPlan BuildPlan(SiegeForcesSnapshot snapshot, IReadOnlyDictionary<string, int> selected)
    {
        if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
        if (selected == null) throw new ArgumentNullException(nameof(selected));

        var remaining = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var pair in selected)
            remaining[pair.Key] = Math.Max(0, pair.Value);

        var keepByParty = new Dictionary<string, IReadOnlyDictionary<string, int>>(StringComparer.Ordinal);
        foreach (var party in PartiesInScope(snapshot))
        {
            var keep = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var troop in party.Troops)
            {
                if (troop == null || troop.CharacterId == null) continue;

                int kept;
                if (troop.IsPlayerCharacter)
                {
                    kept = troop.Healthy;
                }
                else
                {
                    remaining.TryGetValue(troop.CharacterId, out var picked);
                    kept = Math.Min(troop.Healthy, picked);
                    remaining[troop.CharacterId] = picked - kept;
                }

                keep[troop.CharacterId] = keep.TryGetValue(troop.CharacterId, out var already) ? already + kept : kept;
            }

            keepByParty[party.Id] = keep;
        }

        return new SiegeForcesPlan(keepByParty);
    }

    /// <summary>
    /// Whether one entry of a party's ready list stays. <paramref name="keptSoFar"/> is how many of the same character
    /// earlier entries of this party already kept, so a party keeps K of each character (the service scans a window from its end, so the kept K are the highest priority). The player is never
    /// dropped. A party the plan does not hold, a character its row does not name and an entry with no id all stay:
    /// anything the plan did not see is left to vanilla, never dropped on a guess.
    /// </summary>
    public static bool Keeps(SiegeForcesPlan plan, string partyId, string? characterId, bool isPlayerCharacter, int keptSoFar)
    {
        if (isPlayerCharacter) return true;
        if (plan == null || !plan.TryGetKeep(partyId, out var keep)) return true;
        if (characterId == null || !keep.TryGetValue(characterId, out var limit)) return true;
        return keptSoFar < limit;
    }

    /// <summary>
    /// The player side's spawn totals once <paramref name="dropped"/> troops are left out: the total shrinks by that
    /// many and the initial spawn is clamped to it, because deployment waits for as many reserved troops as the
    /// initial spawn asks for. <paramref name="readyCount"/>, the size of the side's final ready list, caps the total
    /// too: the involved-men count and the ready list can disagree (a hero healed inside one continued map event is
    /// counted but not listed), and an initial spawn above what the supplier can deliver never completes (v1.5.3
    /// DefaultBattleMissionAgentSpawnLogic.CheckDeployment :539-547). A count of zero is a real answer, an empty
    /// list, and fits the side to nobody: CheckDeployment skips a side whose initial spawn is 0 (:535-538). A null
    /// count was never recorded and a negative one is no list size at all, so both leave the arithmetic alone. The
    /// count is the size at the last model call that reached the service: a zero recorded before a later window
    /// factory threw stands, and that side then spawns nobody.
    /// </summary>
    public static (int Total, int Initial) FitTotals(int total, int initial, int dropped, int? readyCount)
    {
        var fitted = Math.Max(0, total) - Math.Max(0, dropped);
        if (readyCount is int ready && ready >= 0)
            fitted = Math.Min(fitted, ready);
        fitted = Math.Max(0, fitted);
        return (fitted, Math.Min(Math.Max(0, initial), fitted));
    }

    private sealed class RowTally
    {
        public RowTally(SiegeTroop first)
        {
            Source = first.Source;
            CharacterId = first.CharacterId;
            IsPlayerCharacter = first.IsPlayerCharacter;
            RaceId = first.RaceId;
        }

        public object Source { get; }

        public string CharacterId { get; }

        public bool IsPlayerCharacter { get; }

        public int RaceId { get; }

        public int Number { get; set; }

        public int Wounded { get; set; }

        public int Healthy { get; set; }
    }
}
