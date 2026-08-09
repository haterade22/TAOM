using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TAOM.Core.Logging;
using TAOM.Features.AutoResolveDiagnostics.Domain;

namespace TAOM.Adapters;

/// <summary>
/// Walks a MapEvent's sides, parties and allocated battle rosters into a flat record.
///
/// TIMING — verified against v1.4.7:
///   MapEvent.cs:301   BattleState setter -> OnBattleWon()
///   MapEvent.cs:1042    -> CalculateAndCommitMapEventResults()
///   MapEvent.cs:2018      -> CaptureDefeatedPartyMembers(): REMOVES captured troops from the
///                            defeated parties' Party.MemberRoster
///   MapEvent.cs:1250  MapEventSide.Route() empties MemberRoster again on a run-away
///   MapEvent.cs:2067  State = WaitingRemoval        (so IsFinalized is ALREADY true below)
///   MapEvent.cs:2068  CampaignEventDispatcher.OnMapEventEnded(this)   <-- we run here
///   MapEvent.cs:2147  MapEventSide.HandleMapEventEnd() teardown
///
/// Three consequences, all load-bearing:
///
/// 1. Party.MemberRoster is USELESS to us here. The loser's has already been stripped of captured
///    and routed troops, so measuring composition from it samples winners only — a silent
///    survivorship bias in exactly the measurement this feature exists to make.
/// 2. MapEventParty.Troops is NO refuge either, despite only flipping per-descriptor state in
///    OnTroopKilled/Wounded/Routed. MapEventSide.MakeReadyParty (:774-781) calls
///    MapEventParty.Update(), which does _roster.Clear() and REBUILDS from the current — by then
///    stripped — MemberRoster. Measured over 4,380 live battles: losing sides came back a median
///    55% short, winning sides 1%. So the only reliable record of the army as fielded is a
///    snapshot taken at battle START.
///
///    (The per-troop casualty rosters DiedInBattle / WoundedInBattle / RoutedInBattle are a
///    different matter — they are TroopRoster fields that accumulate and are never cleared, so
///    they remain trustworthy at end of battle and are what losses are read from.)
/// 3. Do NOT gate on mapEvent.IsFinalized: it is already true at :2067, one line before the
///    dispatch. Sibling code (EncounterAdapter.cs:137) gates on it for a different purpose.
///
/// The same reasoning extends past rosters to every simulation INPUT. Morale, the leader, tactics
/// and the power modifier are all altered by losing - measured over 5,546 battles, a losing side
/// recorded sideMorale == 0 in 5,543 cases and a resolvable leader in 17 (0%, against 74% for
/// winners). They are captured at START with the rosters; only casualties and the outcome are
/// read here.
/// </summary>
public class MapEventBattleLogAdapter : IMapEventBattleLogAdapter
{
    private readonly IModLogger _logger;

    public MapEventBattleLogAdapter(IModLogger logger)
    {
        _logger = logger;
    }

    public BattleStartSnapshot SnapshotStart(MapEvent mapEvent)
    {
        var snapshot = new BattleStartSnapshot();
        if (mapEvent == null)
            return snapshot;

        try
        {
            SnapshotRosters(mapEvent.AttackerSide, snapshot.Rosters);
            SnapshotRosters(mapEvent.DefenderSide, snapshot.Rosters);

            // The advantage multipliers as the simulation will apply them, read before either
            // leader can be removed.
            float attackerAdvantage = 1f, defenderAdvantage = 1f;
            try
            {
                // Resolve the model to a local first: a null-conditional call would leave the out
                // params unassigned, which is a compile error and would also mean silently
                // recording a neutral 1.0 as though it had been measured.
                var simModel = Campaign.Current?.Models?.CombatSimulationModel;
                if (simModel != null)
                {
                    simModel.GetBattleAdvantage(mapEvent, out var def, out var atk);
                    attackerAdvantage = atk.ResultNumber;
                    defenderAdvantage = def.ResultNumber;
                }
            }
            catch
            {
                // A model override could throw on a degenerate event; the rest of the snapshot is
                // still worth having.
            }

            snapshot.Attacker = SnapshotSide(mapEvent.AttackerSide, attackerAdvantage);
            snapshot.Defender = SnapshotSide(mapEvent.DefenderSide, defenderAdvantage);

            CaptureContextModifiers(mapEvent, mapEvent.AttackerSide,
                BattleSideEnum.Attacker, snapshot.Attacker.ContextModifier);
            CaptureContextModifiers(mapEvent, mapEvent.DefenderSide,
                BattleSideEnum.Defender, snapshot.Defender.ContextModifier);
        }
        catch (System.Exception ex)
        {
            _logger?.LogWarning($"[AutoResolve] start snapshot failed: {ex.Message}");
        }
        return snapshot;
    }

    public void SnapshotParty(PartyBase party, BattleStartSnapshot snapshot)
    {
        if (party == null || snapshot == null)
            return;

        try
        {
            string key = party.Id;
            if (!snapshot.Rosters.ContainsKey(key))
            {
                var roster = new Dictionary<string, int>();
                AccumulateRoster(roster, party.MemberRoster);
                snapshot.Rosters[key] = roster;
            }

            // A reinforcement can bring a troop class the battle had not contained. Without this
            // the record shows cavalry in `fielded` but carries no Cavalry contextModifier, and
            // the offline model silently scores those men with no terrain term at all.
            var side = party.Side == BattleSideEnum.Attacker ? snapshot.Attacker : snapshot.Defender;
            var mapEvent = party.MapEvent;
            if (side != null && mapEvent != null)
                CaptureContextModifiersForRoster(mapEvent, party.MemberRoster, party.Side,
                                                 side.ContextModifier);
        }
        catch (System.Exception ex)
        {
            _logger?.LogWarning($"[AutoResolve] late-joiner snapshot failed: {ex.Message}");
        }
    }

    /// <summary>The per-side simulation inputs, read while both sides are still intact.</summary>
    private static BattleStartSide SnapshotSide(MapEventSide side, float advantage)
    {
        var result = new BattleStartSide { Advantage = advantage };
        if (side == null)
            return result;

        // MapEventSide.MapFaction is a computed getter that derefs LeaderParty - resolve through
        // LeaderParty instead (WarEventSnapshotAdapter.cs:12-19).
        var leaderParty = side.LeaderParty;
        var leaderHero = leaderParty?.LeaderHero;

        result.LeaderCulture = ResolveCulture(leaderParty);
        result.Kingdom = leaderParty?.MapFaction?.StringId;
        result.Leader = leaderHero?.StringId;
        result.Tactics = leaderHero?.GetSkillValue(DefaultSkills.Tactics) ?? 0;
        result.PowerModifier = leaderHero?.PowerModifier ?? 0f;
        result.SideMorale = side.GetSideMorale();
        return result;
    }

    /// <summary>
    /// Reads GetContextModifier once per troop CLASS present on the side. The modifier is a pure
    /// function of (class, side, battle context) and the context is fixed for the battle, so four
    /// calls per side is the complete picture — no per-troop work.
    ///
    /// A representative CharacterObject per class comes from the side's own roster, which avoids
    /// depending on DefaultMilitaryPowerModel's PRIVATE PowerFlags classifier: whatever the engine
    /// decides that troop is, is what we record.
    /// </summary>
    private void CaptureContextModifiers(
        MapEvent mapEvent, MapEventSide side, BattleSideEnum which, Dictionary<string, float> into)
    {
        // Estimated is a LANDMINE: DefaultMilitaryPowerModel.GetContextModifier's switch sets no
        // terrain flag for it, so the key is absent from _battleModifiers and the raw indexer
        // throws KeyNotFoundException. Vanilla never hits it because GetTroopPower guards the call;
        // we have to guard it ourselves.
        var context = mapEvent.SimulationContext;
        if (context == MapEvent.PowerCalculationContext.Estimated)
            return;

        var parties = side?.Parties;
        if (parties == null)
            return;

        foreach (var mapEventParty in parties)
        {
            CaptureContextModifiersForRoster(mapEvent, mapEventParty?.Party?.MemberRoster, which, into);
            if (into.Count >= 4)
                return;                           // all four classes seen; nothing left to learn
        }
    }

    /// <summary>
    /// The per-roster half, shared by the battle-start walk and the late-joiner path so a
    /// reinforcement contributes any class the battle had not already seen.
    /// </summary>
    private void CaptureContextModifiersForRoster(
        MapEvent mapEvent, TroopRoster? roster, BattleSideEnum which, Dictionary<string, float> into)
    {
        if (mapEvent == null || roster == null || into.Count >= 4)
            return;

        // Estimated is a LANDMINE — see CaptureContextModifiers. Re-checked here because this is
        // also reached directly from SnapshotParty, not only through that caller.
        var context = mapEvent.SimulationContext;
        if (context == MapEvent.PowerCalculationContext.Estimated)
            return;

        var model = Campaign.Current?.Models?.MilitaryPowerModel;
        if (model == null)
            return;

        try
        {
            int count = roster.Count;
            for (int i = 0; i < count && into.Count < 4; i++)
            {
                var troop = roster.GetElementCopyAtIndex(i).Character;
                if (troop == null)
                    continue;

                string cls = ClassOf(troop);
                if (into.ContainsKey(cls))
                    continue;
                into[cls] = model.GetContextModifier(troop, which, context);
            }
        }
        catch (System.Exception ex)
        {
            _logger?.LogWarning($"[AutoResolve] context modifier read failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Mirrors DefaultMilitaryPowerModel.GetTroopPowerContext, which is private: mounted+ranged is
    /// a horse archer, mounted alone is cavalry, ranged alone is an archer, everything else is
    /// infantry. Used only to KEY the recorded values — the values themselves come from the engine.
    /// </summary>
    private static string ClassOf(CharacterObject troop)
    {
        if (troop.HasMount())
            return troop.IsRanged ? "HorseArcher" : "Cavalry";
        return troop.IsRanged ? "Archer" : "Infantry";
    }

    private static void SnapshotRosters(MapEventSide side, Dictionary<string, Dictionary<string, int>> into)
    {
        var parties = side?.Parties;
        if (parties == null)
            return;

        foreach (var mapEventParty in parties)
        {
            var party = mapEventParty?.Party;
            if (party == null)
                continue;
            string key = party.Id;
            if (into.ContainsKey(key))
                continue;

            var roster = new Dictionary<string, int>();
            AccumulateRoster(roster, party.MemberRoster);
            into[key] = roster;
        }
    }

    /// <summary>Sums a TroopRoster into a troop-id -> count map.</summary>
    private static void AccumulateRoster(Dictionary<string, int> into, TroopRoster roster)
    {
        if (roster == null)
            return;

        int count = roster.Count;                       // hoisted — do not re-read in the loop
        for (int i = 0; i < count; i++)
        {
            TroopRosterElement element = roster.GetElementCopyAtIndex(i);
            var character = element.Character;
            if (character == null)
                continue;

            // HEALTHY men only. Element.Number counts the wounded too, but a wounded troop cannot
            // be allocated into the simulation — vanilla's supplier skips !IsWounded, and
            // PartyBase.NumberOfHealthyMembers is TotalManCount - TotalWounded. Recording Number
            // credits a side with men that will never throw a strike, which is the same class of
            // error as the roster-stripping bug: a plausible number measuring the wrong thing.
            // Measured on a live v6 corpus before this fix: 12.3% of sides reported more fielded
            // men than menStart, worst case menStart=1 against fielded=141 (a party that was 140
            // wounded), and the corpus-wide overstatement averaged +4.5%.
            int healthy = element.Number - element.WoundedNumber;
            if (healthy <= 0)
                continue;

            string troopId = character.StringId;
            if (string.IsNullOrEmpty(troopId))
                continue;
            into.TryGetValue(troopId, out int existing);
            into[troopId] = existing + healthy;
        }
    }

    public BattleLogRecord? Capture(MapEvent mapEvent, string id, BattleStartSnapshot? start)
    {
        if (mapEvent == null)
            return null;

        try
        {
            var record = new BattleLogRecord
            {
                Id = id,
                Session = Campaign.Current?.UniqueGameId,
                Day = (int)CampaignTime.Now.ToDays,
                Hour = (float)(CampaignTime.Now.ToHours % 24.0),
                Type = mapEvent.EventType.ToString(),
                Settlement = mapEvent.MapEventSettlement?.StringId,
                Terrain = mapEvent.SimulationContext.ToString(),
                PlayerInvolved = mapEvent.IsPlayerMapEvent,
                PlayerSimulated = mapEvent.IsPlayerSimulation,
                Rounds = mapEvent.UpdateCount,
                Winner = ResolveWinner(mapEvent),
                EndedBy = ResolveEndedBy(mapEvent),
            };

            record.Siege = CaptureSiege(mapEvent);
            record.Sides["attacker"] = CaptureSide(mapEvent.AttackerSide,
                StrengthOf(mapEvent, BattleSideEnum.Attacker), start?.Attacker, start?.Rosters);
            record.Sides["defender"] = CaptureSide(mapEvent.DefenderSide,
                StrengthOf(mapEvent, BattleSideEnum.Defender), start?.Defender, start?.Rosters);
            return record;
        }
        catch (System.Exception ex)
        {
            // Never propagate. A lost record is a gap in the data; a throw here is a campaign-tick
            // crash on every battle.
            _logger?.LogWarning($"[AutoResolve] capture failed: {ex.Message}");
            return null;
        }
    }

    private static string ResolveWinner(MapEvent mapEvent)
    {
        var winner = mapEvent.Winner;
        if (winner == null)
            return "none";
        return winner == mapEvent.AttackerSide ? "attacker" : "defender";
    }

    private static string ResolveEndedBy(MapEvent mapEvent)
    {
        if (mapEvent.EndedByRetreat)
            return "retreat";
        return mapEvent.BattleState switch
        {
            BattleState.AttackerVictory => "attackerVictory",
            BattleState.DefenderVictory => "defenderVictory",
            _ => "none",
        };
    }

    /// <summary>
    /// The siege terms, or null for a field battle. GetSettlementAdvantage is the multiplier that
    /// decides these events — without it a siege outcome is unexplainable, since it dwarfs every
    /// troop-quality term the rest of this record captures.
    /// </summary>
    private static BattleLogSiege? CaptureSiege(MapEvent mapEvent)
    {
        var settlement = mapEvent.MapEventSettlement;
        if (settlement == null || !settlement.IsFortification)
            return null;

        try
        {
            var model = Campaign.Current?.Models?.CombatSimulationModel;
            return new BattleLogSiege
            {
                SettlementAdvantage = model?.GetSettlementAdvantage(settlement) ?? 0f,
                EnginesBuilt = model?.GetNumberOfEquipmentsBuilt(settlement) ?? 0,
                EngineProgress = model?.GetMaximumSiegeEquipmentProgress(settlement) ?? 0f,
                WallLevel = settlement.Town?.GetWallLevel() ?? 0,
                WallHitPoints = settlement.SettlementTotalWallHitPoints,
                SettlementOwner = settlement.MapFaction?.StringId,
            };
        }
        catch
        {
            // A siege event mid-teardown can leave these unreadable; the rest of the record still
            // stands, and a null siege block is honest about the gap.
            return null;
        }
    }

    /// <summary>The engine's own strength figure for a side — ground truth for the power model.</summary>
    private static float StrengthOf(MapEvent mapEvent, BattleSideEnum side)
    {
        var strengths = mapEvent.StrengthOfSide;
        int i = (int)side;
        return strengths != null && i >= 0 && i < strengths.Length ? strengths[i] : 0f;
    }

    private static BattleLogSide CaptureSide(MapEventSide side, float strength,
        BattleStartSide? start, Dictionary<string, Dictionary<string, int>>? startRosters)
    {
        // Every leader-derived field comes from the START snapshot. Reading them here would record
        // the defeated side's post-defeat state — morale zeroed, leader removed — as though it had
        // been an input to the battle rather than a consequence of losing it.
        var result = new BattleLogSide
        {
            Strength = strength,
            LeaderCulture = start?.LeaderCulture,
            Kingdom = start?.Kingdom,
            Leader = start?.Leader,
            Tactics = start?.Tactics ?? 0,
            PowerModifier = start?.PowerModifier ?? 0f,
            SideMorale = start?.SideMorale ?? 0f,
            Advantage = start?.Advantage ?? 1f,
            ContextModifier = start?.ContextModifier ?? new Dictionary<string, float>(),
        };
        if (side == null)
            return result;

        var parties = side.Parties;
        if (parties == null)
            return result;

        int menStart = 0;
        foreach (var mapEventParty in parties)
        {
            if (mapEventParty == null)
                continue;

            menStart += mapEventParty.HealthyManCountAtStart;

            var entry = new BattleLogParty
            {
                Culture = ResolveCulture(mapEventParty.Party),
                Present = mapEventParty.HealthyManCountAtStart,
                Participating = mapEventParty.ParticipatingTroopCount,
                TroopLimit = mapEventParty.HasTroopLimit,
            };

            // Fielded comes from the START snapshot — see the class comment for why nothing
            // readable at battle end still holds it.
            string partyKey = mapEventParty.Party?.Id ?? string.Empty;
            if (startRosters != null && partyKey.Length > 0
                && startRosters.TryGetValue(partyKey, out var startRoster))
            {
                entry.Fielded = startRoster;
            }

            // Casualties DO survive to battle end: these are accumulating TroopRoster fields on
            // MapEventParty, never cleared by the readiness rebuild.
            AccumulateRoster(entry.Killed, mapEventParty.DiedInBattle);
            AccumulateRoster(entry.Wounded, mapEventParty.WoundedInBattle);
            AccumulateRoster(entry.Routed, mapEventParty.RoutedInBattle);
            result.Parties.Add(entry);
        }

        result.MenStart = menStart;
        return result;
    }

    /// <summary>
    /// PartyBase.Culture is `MapFaction.Culture` with NO null guard (PartyBase.cs:255), so it
    /// throws outright for a party whose faction is null — reachable for a settlement party whose
    /// SettlementComponent is null. Route through MapFaction so such a party yields null instead of
    /// losing the whole record to the catch.
    /// </summary>
    private static string? ResolveCulture(PartyBase? party) => party?.MapFaction?.Culture?.StringId;

}
