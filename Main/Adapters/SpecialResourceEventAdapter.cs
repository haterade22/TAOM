using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TAOM.Core.Logging;
using TAOM.Features;
using TAOM.Features.CoopInterop;
using TAOM.Features.SpecialResources;
using TAOM.Features.TroopWeight;

namespace TAOM.Adapters;

/// <summary>
/// Builds the special-resource earning snapshots from engine objects (ADR-007). Sig notes: the
/// RaidCompleted and hideout <c>BattleSideEnum</c> argument IS the winner side; <c>MapEventComponent.MapEvent</c>
/// is a plain property, but a snapshot still walks many engine getters, so each read is exception-guarded
/// as a whole (the WarEventSnapshotAdapter shape) and logs its first failure only. <c>MapEvent.PlayerSide</c>
/// dereferences <c>MainParty</c> unguarded, so it is read only behind the null-safe <c>IsPlayerMapEvent</c>.
/// The player registry is asked only for the candidates of a live session on the co-op authority, never in
/// single-player.
/// </summary>
public sealed class SpecialResourceEventAdapter : ISpecialResourceEventAdapter
{
    private readonly IPlayerControlQuery _playerControl;
    private readonly ICoopSessionProvider _coop;
    private readonly ITroopWeightService _troopWeight;
    private readonly ISpecialResourceConfigProvider _config;
    private readonly IModLogger _logger;

    private bool _victoryFailureLogged;
    private bool _raidFailureLogged;
    private bool _hideoutFailureLogged;
    private bool _dailyFailureLogged;
    private bool _recruitFailureLogged;
    private bool _heroLookupFailureLogged;

    public SpecialResourceEventAdapter(
        IPlayerControlQuery playerControl,
        ICoopSessionProvider coop,
        ITroopWeightService troopWeight,
        ISpecialResourceConfigProvider config,
        IModLogger logger)
    {
        _playerControl = playerControl;
        _coop = coop;
        _troopWeight = troopWeight;
        _config = config;
        _logger = logger;
    }

    // Candidates are read only on the authority of a live session.
    private bool ReadsCandidates => _coop.IsSessionActive && _coop.IsAuthority;

    public EarnerRef DescribeLocalHero() => Describe(Hero.MainHero);

    public VictorySnapshot SnapshotVictory(MapEvent mapEvent)
    {
        try
        {
            if (mapEvent == null) return null;
            var readsCandidates = ReadsCandidates;
            var isPlayerEvent = mapEvent.IsPlayerMapEvent;
            if (!readsCandidates && !isPlayerEvent) return null;

            var isVictory = mapEvent.BattleState == BattleState.AttackerVictory
                            || mapEvent.BattleState == BattleState.DefenderVictory;
            var snapshot = new VictorySnapshot
            {
                State = mapEvent.BattleState.ToString(),
                IsPlayerMapEvent = isPlayerEvent,
                PlayerSide = isPlayerEvent ? mapEvent.PlayerSide : BattleSideEnum.None,
                // None unless the battle resolved to a victory: the service pays nothing without a winner.
                WinningSide = isVictory ? mapEvent.WinningSide : BattleSideEnum.None,
                EventType = mapEvent.EventType,
                IsSiege = mapEvent.IsSiegeAssault || mapEvent.IsSiegeOutside,
                LocalHero = DescribeLocalHero(),
            };
            if (!isVictory) return snapshot;

            if (readsCandidates)
            {
                snapshot.WinnerCandidates = DescribeLeaders(mapEvent.Winner);
                if (!HasRemotePlayer(snapshot.WinnerCandidates)) return null;
            }

            var enemySide = mapEvent.BattleState == BattleState.AttackerVictory
                ? mapEvent.DefenderSide : mapEvent.AttackerSide;
            // Enemy strength for reward scaling, preserved EXACTLY across the 2026-07-11 TroopWeight rework.
            // Pre-rework this read `p.Party.NumberOfAllMembers`, whose Harmony weighting was gated on
            // EnableTroopWeight — so toggling the feature off reverted this to raw "for free". The getter is
            // now raw always, so replicate the old toggle behaviour: weighted when on, raw when off
            // (deep-review 2026-07-11 GAP#2 — otherwise "off = vanilla" breaks for this consumer).
            bool weightOn = TaomSettings.Instance?.EnableTroopWeight ?? true;
            var enemyCount = 0;
            var beatAFieldParty = false;
            foreach (var p in enemySide.Parties)
            {
                enemyCount += weightOn
                    ? (int)System.Math.Ceiling(_troopWeight.CalculateWeightedMemberCount(p.Party))
                    : (p.Party?.NumberOfAllMembers ?? 0);
                if (p.Party?.MobileParty is { IsMilitia: false, IsVillager: false } && p.HealthyManCountAtStart > 0)
                    beatAFieldParty = true;
            }

            snapshot.EnemyCount = enemyCount;
            snapshot.BeatAFieldParty = beatAFieldParty;
            return snapshot;
        }
        catch (Exception ex)
        {
            LogReadFailure(ref _victoryFailureLogged, "the ended map event", ex);
            return null;
        }
    }

    public RaidOrHideoutSnapshot SnapshotRaid(BattleSideEnum side, RaidEventComponent component)
    {
        try
        {
            var snapshot = SnapshotEvent(side, component?.MapEvent);
            if (snapshot == null) return null;
            snapshot.VillageLooted = component.MapEventSettlement?.Village?.VillageState == Village.VillageStates.Looted;
            return snapshot;
        }
        catch (Exception ex)
        {
            LogReadFailure(ref _raidFailureLogged, "the completed raid", ex);
            return null;
        }
    }

    public RaidOrHideoutSnapshot SnapshotHideout(BattleSideEnum winnerSide, HideoutEventComponent component)
    {
        try
        {
            var snapshot = SnapshotEvent(winnerSide, component?.MapEvent);
            if (snapshot == null) return null;
            // A "send troops" clear raises the hideout event twice for one event (#790): first from its
            // success menu, before the event is finalized, then from HideoutEventComponent.OnBeforeMapEventFinalize.
            // MapEvent.FinalizeEventAux sets WaitingRemoval just before that second raise (v1.5.5), so
            // IsFinalized is true only on the second one. Re-check this line order at an engine bump.
            snapshot.IsFinalized = component.MapEvent.IsFinalized;
            return snapshot;
        }
        catch (Exception ex)
        {
            LogReadFailure(ref _hideoutFailureLogged, "the completed hideout battle", ex);
            return null;
        }
    }

    public EarnerRef DescribeTournamentWinner(CharacterObject winner)
    {
        if (!_coop.IsSessionActive)
            return winner == Hero.MainHero?.CharacterObject ? DescribeLocalHero() : null;
        return Describe(winner?.HeroObject);
    }

    public int CountPrisoners(FlattenedTroopRoster roster)
    {
        var count = 0;
        if (roster != null)
            foreach (var _ in roster)
                count++;
        return count;
    }

    public DailyTickInput SnapshotDaily(Hero hero, bool perPlayer)
    {
        // A client holds no balance, so it never reads a hero for the daily tick.
        if (perPlayer && !_coop.IsAuthority) return null;

        try
        {
            if (perPlayer)
            {
                if (hero == null || !hero.IsAlive || _playerControl.OfHero(hero) != PlayerControl.Player) return null;
            }
            else if (hero != Hero.MainHero)
            {
                return null;
            }

            return new DailyTickInput(
                Describe(hero),
                PartyUpkeepReader.CountOwnedTowns(hero),
                PartyUpkeepReader.Collect(hero.PartyBelongedTo, _config));
        }
        catch (Exception ex)
        {
            LogReadFailure(ref _dailyFailureLogged, "the daily tick inputs", ex);
            return null;
        }
    }

    public RecruitContext SnapshotRecruit()
    {
        try
        {
            var scopedPartyId = RecruitAttributionScope.Current;
            var scopedLeader = scopedPartyId != null
                ? Campaign.Current?.CampaignObjectManager.Find<MobileParty>(scopedPartyId)?.LeaderHero
                : null;
            return new RecruitContext(scopedPartyId, Describe(scopedLeader), DescribeLocalHero());
        }
        catch (Exception ex)
        {
            LogReadFailure(ref _recruitFailureLogged, "the recruit scope", ex);
            return null;
        }
    }

    public EarnerRef DescribeHero(string heroId)
    {
        try
        {
            return heroId == null ? null : Describe(Hero.Find(heroId));
        }
        catch (Exception ex)
        {
            LogReadFailure(ref _heroLookupFailureLogged, "a hero by id", ex);
            return null;
        }
    }

    public int ApplyDesertion(Hero hero, IReadOnlyList<TroopDesertionEntry> desertions)
    {
        var party = hero?.PartyBelongedTo;
        if (party?.MemberRoster == null || desertions == null || desertions.Count == 0)
            return 0;

        var totalDeserted = 0;
        foreach (var entry in desertions)
        {
            var removed = TroopRosterRemoval.RemoveUpTo(party.MemberRoster, entry.TroopId, entry.DesertCount);
            if (removed <= 0) continue;

            totalDeserted += removed;
            _logger.LogInfo($"[SpecRes] Deserted: {entry.TroopId} x{removed}");
        }

        return totalDeserted;
    }

    // Shared head of the raid and hideout reads. Null when the event is missing, or when without a
    // session it is not the local player's event (the old IsPlayerMapEvent gate).
    private RaidOrHideoutSnapshot SnapshotEvent(BattleSideEnum side, MapEvent mapEvent)
    {
        if (side != BattleSideEnum.Attacker || mapEvent == null) return null;
        var readsCandidates = ReadsCandidates;
        var isPlayerEvent = mapEvent.IsPlayerMapEvent;
        if (!readsCandidates && !isPlayerEvent) return null;

        return new RaidOrHideoutSnapshot
        {
            Side = side,
            IsPlayerMapEvent = isPlayerEvent,
            PlayerSide = isPlayerEvent ? mapEvent.PlayerSide : BattleSideEnum.None,
            LocalHero = DescribeLocalHero(),
            AttackerLeaders = readsCandidates ? DescribeLeaders(mapEvent.AttackerSide) : Array.Empty<EarnerRef>(),
        };
    }

    // The leader of each party on a side, once per hero.
    private IReadOnlyList<EarnerRef> DescribeLeaders(MapEventSide side)
    {
        var result = new List<EarnerRef>();
        if (side == null) return result;

        var seen = new HashSet<string>();
        foreach (var p in side.Parties)
        {
            var leader = p.Party?.LeaderHero;
            if (leader == null || !seen.Add(leader.StringId)) continue;
            result.Add(Describe(leader));
        }
        return result;
    }

    // A pre-filter only: the service applies the full earner policy.
    private static bool HasRemotePlayer(IReadOnlyList<EarnerRef> candidates)
    {
        foreach (var candidate in candidates)
            if (candidate.Control == PlayerControl.Player)
                return true;
        return false;
    }

    // One warning per snapshot kind, with the stack: a failing read repeats on every event.
    private void LogReadFailure(ref bool logged, string what, Exception ex)
    {
        if (logged) return;
        logged = true;
        _logger.LogWarning($"[SpecRes] Could not read {what}: {ex}. Further failures of this kind are not logged.");
    }

    private EarnerRef Describe(Hero hero)
    {
        if (hero == null) return null;

        // Hero.MainHero reads Game.Current.PlayerTroop unguarded, and Coop's source says it is null on a dedicated
        // server, where DescribeHero (Patch144) runs. So read the local hero null-safe.
        var isLocal = hero == (Game.Current?.PlayerTroop as CharacterObject)?.HeroObject;
        return new EarnerRef(
            hero.StringId,
            hero.Clan?.Kingdom?.StringId,
            hero.Culture?.StringId,
            isLocal,
            _playerControl.OfHero(hero));
    }
}
