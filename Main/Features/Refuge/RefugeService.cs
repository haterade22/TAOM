using System;
using System.Collections.Generic;
using TAOM.Core.Logging;
using TAOM.Core.Validation;
using TAOM.Features.Enlistment;
using TAOM.Features.FieldCamp;
using TAOM.Features.FieldCamp.Domain;
using TAOM.Features.Refuge.Components;
using TAOM.Features.Refuge.Domain;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace TAOM.Features.Refuge;

/// <summary>
/// One hostile party eligible to raid a refuge. Built at the campaign boundary; the decision
/// logic reads the plain fields and <see cref="EngineParty"/> rides along as an opaque handle so
/// the boundary can start the battle without a second lookup (the AmbushCandidate precedent).
/// </summary>
public sealed class RaidThreat
{
    public string PartyId;
    public string Name;

    /// <summary>The engine <c>MobileParty</c>; null in tests, opaque to the decision logic.</summary>
    public object EngineParty;
}

/// <summary>
/// The refuge book and its lifecycle (port of the Refuge module's RefugeManager + the
/// campaign-behavior guts). Owns the persisted dictionary; the campaign behavior hands it through
/// LoadFrom/SaveInto at SyncData time and pumps the tick methods.
///
/// <para>Deliberate changes from the source: the hold-nearby rule announces itself once per build
/// instead of silently pinning the party; militia bookkeeping is persisted in RefugeData (the
/// source's transient dictionary baked militia into the garrison across a mid-battle save) and
/// stand-down removes min(recorded, present - pre-rally baseline) so a player-garrisoned stack of
/// the militia's own troop type survives its casualties; OnGameLoaded
/// re-pins the party AI (the source only pinned at spawn, so refuges wandered after reload) and
/// logs both reconcile directions; the warden is never killed (see IWardenService).</para>
///
/// <para>Campaign statics (parties, gold, distances, militia rosters, raids, messages) sit behind
/// protected virtual members, the CampService/SupplyOrderService pattern, so every decision path
/// is unit-testable; the virtual bodies are the honest untested boundary sliver.</para>
/// </summary>
public class RefugeService : IRefugeService, IRefugeBook
{
    private const string RefugeIdPrefix = "taom_refuge_";

    /// <summary>Floor on a build's target hours so BuildProgress never divides by ~zero.</summary>
    private const float MinBuildTargetHours = 0.1f;

    /// <summary>Frame work runs at most this often, in game hours (~36 game-seconds): builds,
    /// the hold-nearby rule and visual retries stay responsive without per-frame party scans.</summary>
    private const float FrameWorkIntervalHours = 0.01f;

    /// <summary>Per-refuge, per-hour chance of a raid roll succeeding (source value).</summary>
    private const float RaidChancePerHour = 0.05f;

    private const int StrongholdMilitiaBonus = 10;
    private const int MilitiaAgeDaysCap = 15;
    private const int GarrisonPerMilitia = 10;

    private readonly IRefugeSettingsProvider _settings;
    private readonly IWardenService _wardens;
    private readonly ICampService _camps;
    private readonly IEnlistmentStateQuery _enlistment;
    private readonly IRefugeVisualService _visuals;
    private readonly IModLogger _logger;

    private Dictionary<string, RefugeData> _refuges = new Dictionary<string, RefugeData>();
    private int _counter;

    /// <summary>Refuges whose visuals currently stand. Transient: entities never survive a save
    /// load, so OnGameLoaded clears it and the frame tick rebuilds.</summary>
    private readonly HashSet<string> _visualShown = new HashSet<string>();

    /// <summary>Refuges whose hold-nearby note was already shown this build. Transient by design:
    /// one note per build phase per session is the fix over the source's silent pin.</summary>
    private readonly HashSet<string> _holdNoteShown = new HashSet<string>();

    /// <summary>Next game-time (hours) the frame work may run. MinValue = run on the first frame.</summary>
    private double _nextFrameWorkHours = double.MinValue;

    public RefugeService(
        IRefugeSettingsProvider settings,
        IWardenService wardens,
        ICampService camps,
        IEnlistmentStateQuery enlistment,
        IRefugeVisualService visuals,
        IModLogger logger)
    {
        _settings = settings;
        _wardens = wardens;
        _camps = camps;
        _enlistment = enlistment;
        _visuals = visuals;
        _logger = logger;
    }

    public IReadOnlyCollection<RefugeData> AllRefuges => _refuges.Values;

    public RefugeData GetByPartyId(string partyId)
    {
        if (partyId == null)
            return null;
        return _refuges.TryGetValue(partyId, out var data) ? data : null;
    }

    public int RefugeLimit(int clanTier)
    {
        int scaled = 1 + clanTier / 2;
        if (scaled < 1)
            scaled = 1;
        int cap = _settings.MaxRefugesCap;
        if (cap < 1)
            cap = 1;
        return Math.Min(scaled, cap);
    }

    public RefugeBlockReason CanFound()
    {
        if (!_settings.Enabled)
            return RefugeBlockReason.FeatureDisabled;
        // No main party means no campaign to act in; the feature cannot operate at all.
        if (MainPartyId() == null)
            return RefugeBlockReason.FeatureDisabled;
        // An enlisted soldier's movement belongs to his commander; a refuge would fight the
        // enlistment attachment loop for control of the party (same reason camps block).
        if (_enlistment.IsEnlisted)
            return RefugeBlockReason.Enlisted;
        // Any refuge within manage range, building included: two overlapping layouts on one spot
        // is the mess the source's same check prevented.
        if (NearestRefugeId(_settings.ManageRange, readyOnly: false) != null)
            return RefugeBlockReason.RefugeAlreadyHere;

        var camp = _camps.PlayerCamp;
        if (camp == null)
            return RefugeBlockReason.NoReadyCampHere;
        if (camp.TypeEnum != CampType.Field && camp.TypeEnum != CampType.Fortified)
            return RefugeBlockReason.WrongCampType;
        if (!IsCampReady(camp))
            return RefugeBlockReason.NoReadyCampHere;

        if (_refuges.Count >= RefugeLimit(PlayerClanTier()))
            return RefugeBlockReason.AtRefugeLimit;

        float minTownDistance = _settings.MinTownDistance;
        if (FiniteFloatValidator.IsFinite(minTownDistance) && minTownDistance > 0f
            && DistanceToNearestFortification() < minTownDistance)
        {
            return RefugeBlockReason.TooCloseToTown;
        }

        if (!_wardens.AnyAvailable())
            return RefugeBlockReason.NoWardenAvailable;
        if (PlayerGold < _settings.FoundCost)
            return RefugeBlockReason.NotEnoughGold;
        return RefugeBlockReason.None;
    }

    public RefugeData Found(string wardenHeroId, out RefugeBlockReason reason)
    {
        reason = CanFound();
        if (reason != RefugeBlockReason.None)
            return null;
        if (string.IsNullOrEmpty(wardenHeroId))
        {
            reason = RefugeBlockReason.NoWardenAvailable;
            return null;
        }

        // Read the camp type BEFORE breaking the camp: a fortified camp founds a fortified
        // refuge, and after BreakPlayerCamp that fact is gone.
        var camp = _camps.PlayerCamp;
        bool fortified = camp != null && camp.TypeEnum == CampType.Fortified;

        string partyId = SpawnRefugeParty(RefugeIdPrefix + _counter++, wardenHeroId);
        if (partyId == null)
        {
            // Engine refusal with no player-facing cause: abort before any charge or camp break
            // so the player keeps camp and gold. Reason stays None; the menu simply does nothing.
            _logger.LogWarning("[Refuge] party spawn failed; founding aborted before any charge.");
            return null;
        }
        AttachWarden(partyId, wardenHeroId);
        ChargePlayer(_settings.FoundCost);
        _camps.BreakPlayerCamp();

        var data = new RefugeData
        {
            PartyId = partyId,
            TierEnum = RefugeTier.Refuge,
            Fortified = fortified,
            WardenHeroId = wardenHeroId,
            FoundedTime = NowTime(),
            Established = false,
            Building = true,
            BuildingUpgrade = false,
            BuildStartTime = NowTime(),
            BuildTargetHours = SaneBuildHours(),
        };
        _refuges[partyId] = data;
        return data;
    }

    public RefugeData NearestManageable()
    {
        string partyId = NearestRefugeId(_settings.ManageRange, readyOnly: true);
        return partyId != null ? _refuges[partyId] : null;
    }

    public RefugeData NearestDismantlable()
    {
        string partyId = NearestRefugeId(_settings.ManageRange, readyOnly: true, includeOrphans: true);
        return partyId != null ? _refuges[partyId] : null;
    }

    public RefugeBlockReason CanUpgrade(RefugeData refuge)
    {
        if (!_settings.Enabled)
            return RefugeBlockReason.FeatureDisabled;
        if (refuge?.PartyId == null || !_refuges.ContainsKey(refuge.PartyId))
            return RefugeBlockReason.NoRefugeInReach;
        if (!refuge.IsReady)
            return RefugeBlockReason.StillBuilding;
        if (refuge.TierEnum == RefugeTier.Stronghold)
            return RefugeBlockReason.AlreadyTopTier;

        float minDistance = _settings.StrongholdMinTownDistance;
        if (FiniteFloatValidator.IsFinite(minDistance) && minDistance > 0f
            && DistanceToNearestFortificationFrom(refuge.PartyId) < minDistance)
        {
            return RefugeBlockReason.TooCloseToTown;
        }

        if (PlayerGold < _settings.StrongholdUpgradeCost)
            return RefugeBlockReason.NotEnoughGold;
        return RefugeBlockReason.None;
    }

    public bool Upgrade(RefugeData refuge)
    {
        if (CanUpgrade(refuge) != RefugeBlockReason.None)
            return false;
        ChargePlayer(_settings.StrongholdUpgradeCost);
        refuge.Building = true;
        refuge.BuildingUpgrade = true;
        refuge.BuildStartTime = NowTime();
        refuge.BuildTargetHours = SaneBuildHours();
        // The rebuild is its own build phase: it gets its own one-time hold note.
        _holdNoteShown.Remove(refuge.PartyId);
        return true;
    }

    public void Dismantle(RefugeData refuge)
    {
        if (refuge?.PartyId == null || !_refuges.ContainsKey(refuge.PartyId))
            return;
        // A refuge inside a map event is a live battle participant; destroying it now would rip
        // rosters out from under the event. The menu gate (NearestDismantlable) already excludes
        // this; the belt here covers a stale menu handle.
        if (IsPartyInMapEvent(refuge.PartyId))
            return;

        // Warden first, roster merge second: releasing the warden (companion or promoted, same
        // path since the promoted-warden strand fix) moves him with a real AddHeroToPartyAction.
        // The merge then moves any REMAINING heroes (deposited companions, hero prisoners) with
        // engine actions before bulk-copying regulars, so no hero row ever rides a raw copy.
        _wardens.ReleaseWarden(refuge.WardenHeroId, refuge.WardenPromoted);
        MergeRefugeIntoMainParty(refuge.PartyId);
        _refuges.Remove(refuge.PartyId);
        DestroyRefugeParty(refuge.PartyId);
        _visuals.Remove(refuge.PartyId);
        _visualShown.Remove(refuge.PartyId);
        _holdNoteShown.Remove(refuge.PartyId);
        ShowMessage(
            new TextObject("{=taom_rf_dismantled}The refuge is dismantled; its garrison and stores return to your party."),
            error: false);
    }

    public void FrameTick()
    {
        if (_refuges.Count == 0)
            return;

        // Wind runs on REAL time, before the game-time throttle: cloths drop their forced wind
        // even while the campaign is paused, and a refuge standing alone (no player camp) has
        // no other driver.
        _visuals.TickWind();

        // Game-time throttle: while paused nothing here can change anyway, and at speed the
        // ~36-game-second cadence still re-holds the party long before it drifts anywhere.
        double now = NowInHours();
        if (now < _nextFrameWorkHours)
            return;
        _nextFrameWorkHours = now + FrameWorkIntervalHours;

        bool holdNearby = false;
        string holdNotePartyId = null;
        float manageRange = _settings.ManageRange;

        foreach (var pair in _refuges)
        {
            var data = pair.Value;
            if (data.Building)
            {
                if (BuildProgressOf(data) >= 1f)
                {
                    FinishBuild(pair.Key, data);
                    continue;
                }
                // Hold-nearby rule: while a build runs within manage range the company stays.
                // Ready refuges do not pin the party; only an active build does.
                float distance = DistanceFromMainPartyTo(pair.Key);
                if (FiniteFloatValidator.IsFinite(distance)
                    && FiniteFloatValidator.IsFinite(manageRange)
                    && distance <= manageRange)
                {
                    holdNearby = true;
                    if (holdNotePartyId == null)
                        holdNotePartyId = pair.Key;
                }
            }
            else if (data.IsReady && !_visualShown.Contains(pair.Key))
            {
                // The map scene may not have existed when the visual was first requested (save
                // load); retry until the visual service reports it standing.
                TryShowVisual(pair.Key, data);
            }
        }

        if (holdNearby)
        {
            HoldMainParty();
            // FIX over the source, which pinned the party silently every frame: say why, once
            // per build, so the player is not left clicking a map that ignores him.
            if (holdNotePartyId != null && _holdNoteShown.Add(holdNotePartyId))
            {
                ShowMessage(
                    new TextObject("{=taom_rf_hold_note}Your company holds position while the refuge is raised. It can march once the work is done."),
                    error: false);
            }
        }
    }

    public void HourlyTick()
    {
        // Raids are experimental and OFF by default (source parity); nothing else runs hourly.
        if (!_settings.EnableRaids)
            return;
        float raidRange = _settings.RaidRange;
        if (!FiniteFloatValidator.IsFinite(raidRange) || !(raidRange > 0f))
            return;

        foreach (var pair in _refuges)
        {
            var data = pair.Value;
            if (!data.IsReady)
                continue;
            if (IsPartyInMapEvent(pair.Key))
                continue;
            // Positive gate so a NaN roll skips: proceed only on roll <= chance (the source's
            // "RandomFloat > 0.05 -> skip", boundary value 0.05 itself raids).
            if (!(NextRandomFloat() <= RaidChancePerHour))
                continue;

            var threat = FindNearestHostile(pair.Key, raidRange);
            if (threat == null)
                continue;
            // Rally BEFORE the battle starts: MapEventParty's constructor captures
            // NumberOfHealthyMembers and auto-resolve allocates from that frozen count, so a
            // rally from the MapEventStarted callback arrives too late to add simulated bodies
            // (1.4.8 MapEvent.cs:849 dispatches OnMapEventStarted after the sides are built).
            // MapEventStarted still rallies for battles this path did not start.
            RallyMilitia(pair.Key, data);
            StartRaid(threat, pair.Key);
            var message = new TextObject("{=taom_rf_attacked}Your refuge is under attack by {ENEMY}!");
            message.SetTextVariable("ENEMY",
                string.IsNullOrEmpty(threat.Name)
                    ? new TextObject("{=taom_rf_enemy}the enemy").ToString()
                    : threat.Name);
            ShowMessage(message, error: true);
        }
    }

    public void OnMapEventStarted(string partyId)
    {
        if (partyId == null || !_refuges.TryGetValue(partyId, out var data))
            return;
        RallyMilitia(partyId, data);
    }

    /// <summary>Adds the militia stack once per battle. The raid path calls this BEFORE
    /// StartBattleAction so auto-resolve sees the militia; MapEventStarted covers battles other
    /// parties start (there the militia reaches player-fought missions, whose spawns read the
    /// live roster, but not a frozen auto-resolve count - an engine ordering limit).</summary>
    private void RallyMilitia(string partyId, RefugeData data)
    {
        if (!data.IsReady)
            return;
        // Already boosted: MilitiaAdded is PERSISTED, so a save made mid-battle cannot re-add on
        // load (the source's transient dictionary forgot, then removal deleted player troops).
        if (data.MilitiaAdded != 0)
            return;

        string troopId = ResolveMilitiaTroopId(partyId);
        if (string.IsNullOrEmpty(troopId))
            return;
        int count = MilitiaCountFor(data, partyId);
        if (count <= 0)
            return;

        // The baseline BEFORE the add is what stand-down protects: everything up to this count
        // belongs to the garrison, everything above it (that survives) is militia.
        data.MilitiaPreRallyCount = GetTroopCountInRefuge(partyId, troopId);
        AddTroopsToRefuge(partyId, troopId, count);
        data.MilitiaAdded = count;
        data.MilitiaTroopId = troopId;
    }

    public void OnMapEventEnded(string partyId)
    {
        if (partyId == null || !_refuges.TryGetValue(partyId, out var data))
            return;
        if (data.MilitiaAdded > 0 && !string.IsNullOrEmpty(data.MilitiaTroopId))
        {
            // The roster aggregates identical characters into ONE stack, so the militia and any
            // player-garrisoned troops of the same type are indistinguishable by row. Casualties
            // are attributed to militia first: only the surplus above the pre-rally baseline is
            // militia survivors, and only that (capped at the recorded add) is removed. A plain
            // min(recorded, present) deleted the pre-existing garrison stack whenever losses
            // exceeded the militia count.
            int present = GetTroopCountInRefuge(partyId, data.MilitiaTroopId);
            int militiaSurvivors = Math.Max(0, present - data.MilitiaPreRallyCount);
            int remove = Math.Min(data.MilitiaAdded, militiaSurvivors);
            if (remove > 0)
                RemoveTroopsFromRefuge(partyId, data.MilitiaTroopId, remove);
        }
        data.MilitiaAdded = 0;
        data.MilitiaTroopId = null;
        data.MilitiaPreRallyCount = 0;
    }

    public void OnPartyDestroyed(string partyId)
    {
        if (partyId == null || !_refuges.TryGetValue(partyId, out var data))
            return;
        // The engine already destroyed the party (a lost defense wipes the garrison and
        // MapEventSide.HandleMapEventEnd applies DestroyPartyAction directly; disband paths land
        // here too). Drop the row and visuals now instead of waiting for the next load's
        // reconcile: a phantom row keeps counting against the refuge cap all session.
        _refuges.Remove(partyId);
        _visuals.Remove(partyId);
        _visualShown.Remove(partyId);
        _holdNoteShown.Remove(partyId);
        if (!string.IsNullOrEmpty(data.WardenHeroId))
        {
            // Never orphan the warden silently: he is (or became, via promotion) a clan
            // companion, so the clan roster still carries him; his battle fate (killed, captured,
            // escaped) is the engine's own hero accounting from the lost fight.
            _logger.LogInfo(
                $"[Refuge] refuge '{partyId}' destroyed; warden '{data.WardenHeroId}' remains a clan companion (promoted: {data.WardenPromoted}).");
        }
        ShowMessage(
            new TextObject("{=taom_rf_fallen}Your refuge has fallen. What remains of its garrison is lost; your warden's fate rides with the survivors."),
            error: true);
    }

    public void OnPeaceMade()
    {
        foreach (var partyId in _refuges.Keys)
            ReleasePeacePrisoners(partyId);
    }

    public void LoadFrom(Dictionary<string, RefugeData> refuges, int counter)
    {
        _refuges = refuges ?? new Dictionary<string, RefugeData>();
        // A partially recovered or externally modified save can carry null rows; every tick path
        // dereferences the value, so scrub here rather than crash on the first FrameTick.
        List<string> nullKeys = null;
        foreach (var pair in _refuges)
        {
            if (pair.Value == null)
                (nullKeys ?? (nullKeys = new List<string>())).Add(pair.Key);
        }
        if (nullKeys != null)
        {
            foreach (var key in nullKeys)
                _refuges.Remove(key);
            _logger.LogWarning($"[Refuge] dropped {nullKeys.Count} null book row(s) from the loaded save.");
        }
        _counter = counter < 0 ? 0 : counter;
        _visualShown.Clear();
        _holdNoteShown.Clear();
        _nextFrameWorkHours = double.MinValue;
    }

    public void SaveInto(out Dictionary<string, RefugeData> refuges, out int counter)
    {
        refuges = _refuges;
        counter = _counter;
    }

    public void ResetForNewSession()
    {
        // A fresh campaign (or a save written before this feature existed) runs no SyncData load,
        // so a process-lifetime singleton would otherwise carry the previous campaign's book into
        // this one and then SAVE it. Clear the book AND every transient cache.
        _refuges.Clear();
        _counter = 0;
        _visualShown.Clear();
        _holdNoteShown.Clear();
        _nextFrameWorkHours = double.MinValue;
        _visuals.ClearAll();
    }

    public void OnGameLoaded()
    {
        // Entities never survive a load; clear the transient sets so the frame tick rebuilds.
        // The VISUAL SERVICE's own records must go too: its per-refuge Shown=true short-circuit
        // answers "already standing" with entity handles from the dead previous map scene, so
        // after quit-to-menu + load every refuge layout would stay invisible all session.
        _visuals.ClearAll();
        _visualShown.Clear();
        _holdNoteShown.Clear();
        _nextFrameWorkHours = double.MinValue;

        var live = new HashSet<string>();
        foreach (var partyId in AllRefugePartyIds())
        {
            if (string.IsNullOrEmpty(partyId))
                continue;
            live.Add(partyId);
            if (!_refuges.ContainsKey(partyId))
            {
                // Orphan party without a book row (older save, external tampering): adopt it
                // un-established rather than leak an unmanaged party, and say so in the log.
                _refuges[partyId] = new RefugeData
                {
                    PartyId = partyId,
                    TierEnum = RefugeTier.Refuge,
                    Established = false,
                };
                _logger.LogWarning($"[Refuge] party '{partyId}' had no book row on load; adopted un-established.");
            }
            // Re-pin the AI on EVERY refuge party. The source pinned only at spawn;
            // SetDoNotMakeNewDecisions is not persisted, so loaded refuges wandered off.
            PinRefugePartyAi(partyId);
        }

        List<string> orphanRows = null;
        foreach (var partyId in _refuges.Keys)
        {
            if (!live.Contains(partyId))
                (orphanRows ?? (orphanRows = new List<string>())).Add(partyId);
        }
        if (orphanRows != null)
        {
            foreach (var partyId in orphanRows)
            {
                _refuges.Remove(partyId);
                _logger.LogWarning($"[Refuge] book row '{partyId}' has no party on load; dropped.");
            }
        }
    }

    // --- internals ---

    private void FinishBuild(string partyId, RefugeData data)
    {
        bool wasUpgrade = data.BuildingUpgrade;
        data.Building = false;
        data.BuildingUpgrade = false;
        data.BuildTargetHours = 0f;
        if (wasUpgrade)
        {
            data.TierEnum = RefugeTier.Stronghold;
            // Drop the refuge-scale layout so the next Show rebuilds at stronghold scale.
            _visuals.Remove(partyId);
            _visualShown.Remove(partyId);
            ShowMessage(
                new TextObject("{=taom_rf_stronghold_done}The stronghold is complete; its walls and garrison stand stronger."),
                error: false);
        }
        else
        {
            data.Established = true;
            ShowMessage(
                new TextObject("{=taom_rf_raised}The refuge is raised. It stands as your forward base; manage it to deposit troops."),
                error: false);
        }
        TryShowVisual(partyId, data);
    }

    private void TryShowVisual(string partyId, RefugeData data)
    {
        if (partyId == null || _visualShown.Contains(partyId))
            return;
        if (_visuals.Show(partyId, data.TierEnum, data.Fortified, PartyPosition(partyId)))
            _visualShown.Add(partyId);
    }

    /// <summary>Nearest refuge within maxDistance of the main party, or null. With readyOnly the
    /// candidate must be manageable, which also excludes a refuge fighting a map event: its
    /// rosters belong to the live battle, not to the manage/dismantle screens. includeOrphans
    /// widens readyOnly to orphan-adopted rows so they stay dismantlable.</summary>
    private string NearestRefugeId(float maxDistance, bool readyOnly, bool includeOrphans = false)
    {
        if (!FiniteFloatValidator.IsFinite(maxDistance) || !(maxDistance > 0f))
            return null;
        string best = null;
        float bestDistance = maxDistance;
        foreach (var pair in _refuges)
        {
            if (readyOnly)
            {
                bool eligible = pair.Value.IsReady || (includeOrphans && pair.Value.IsOrphanAdopted);
                if (!eligible || IsPartyInMapEvent(pair.Key))
                    continue;
            }
            float distance = DistanceFromMainPartyTo(pair.Key);
            if (FiniteFloatValidator.IsFinite(distance) && distance <= bestDistance)
            {
                bestDistance = distance;
                best = pair.Key;
            }
        }
        return best;
    }

    private int MilitiaCountFor(RefugeData data, string partyId)
    {
        int tierBonus = data.TierEnum == RefugeTier.Stronghold ? StrongholdMilitiaBonus : 0;
        double days = DaysSince(data.FoundedTime);
        if (!FiniteFloatValidator.IsFinite(days) || days < 0.0)
            days = 0.0;
        int ageBonus = (int)Math.Min(days, MilitiaAgeDaysCap);
        int garrisonBonus = RefugeGarrisonCount(partyId) / GarrisonPerMilitia;
        int total = _settings.MilitiaBase + tierBonus + ageBonus + garrisonBonus;
        int max = _settings.MilitiaMax;
        if (max < 0)
            max = 0;
        if (total < 0)
            total = 0;
        return Math.Min(total, max);
    }

    private float SaneBuildHours()
    {
        float hours = _settings.BuildHours;
        // Belt over the provider's validation: a degenerate target would make BuildProgress
        // divide by ~zero or never finish; the floor makes it finish almost immediately instead.
        if (!FiniteFloatValidator.IsFinite(hours) || hours < MinBuildTargetHours)
            return MinBuildTargetHours;
        return hours;
    }

    // --- campaign-static seams (the untested boundary sliver; overridden in tests) ---

    protected virtual string MainPartyId() => MobileParty.MainParty?.StringId;

    protected virtual int PlayerGold => Hero.MainHero?.Gold ?? 0;

    protected virtual void ChargePlayer(int amount) =>
        GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, amount, disableNotification: true);

    protected virtual int PlayerClanTier() => Clan.PlayerClan?.Tier ?? 0;

    // Readiness routes through the service so tests can pin it; CampState.IsReady dereferences
    // Campaign.Current for the elapsed clock.
    protected virtual bool IsCampReady(CampState camp) => camp.IsReady;

    // Same reason: RefugeData.BuildProgress() reads CampaignTime.Now.
    protected virtual float BuildProgressOf(RefugeData data) => data.BuildProgress();

    protected virtual CampaignTime NowTime() => CampaignTime.Now;

    protected virtual double NowInHours() => CampaignTime.Now.ToHours;

    protected virtual double DaysSince(CampaignTime time) => (CampaignTime.Now - time).ToDays;

    protected virtual float NextRandomFloat() => MBRandom.RandomFloat;

    protected virtual float DistanceFromMainPartyTo(string partyId)
    {
        var main = MobileParty.MainParty;
        var party = FindParty(partyId);
        if (main == null || party == null)
            return float.MaxValue;
        return main.GetPosition2D.Distance(party.GetPosition2D);
    }

    protected virtual Vec2 PartyPosition(string partyId) =>
        FindParty(partyId)?.GetPosition2D ?? default;

    protected virtual float DistanceToNearestFortification()
    {
        var main = MobileParty.MainParty;
        return main == null ? float.MaxValue : DistanceToNearestFortificationFromPosition(main.GetPosition2D);
    }

    protected virtual float DistanceToNearestFortificationFrom(string partyId)
    {
        var party = FindParty(partyId);
        return party == null ? float.MaxValue : DistanceToNearestFortificationFromPosition(party.GetPosition2D);
    }

    private static float DistanceToNearestFortificationFromPosition(Vec2 position)
    {
        float nearest = float.MaxValue;
        foreach (var settlement in Settlement.All)
        {
            if (settlement == null || (!settlement.IsTown && !settlement.IsCastle))
                continue;
            float distance = position.Distance(settlement.GetPosition2D);
            if (distance < nearest)
                nearest = distance;
        }
        return nearest;
    }

    /// <summary>
    /// Creates the stationary refuge party at the player's position and pins its AI. Returns the
    /// actual StringId (CreateParty may uniquify the requested one), or null on engine refusal.
    /// </summary>
    protected virtual string SpawnRefugeParty(string stringId, string wardenHeroId)
    {
        var main = MobileParty.MainParty;
        if (main == null)
            return null;
        var warden = FindHero(wardenHeroId);
        var component = new RefugePartyComponent(stringId, Hero.MainHero?.HomeSettlement, warden);
        var party = MobileParty.CreateParty(stringId, component);
        if (party == null)
            return null;
        party.InitializeMobilePartyAtPosition(new CampaignVec2(main.GetPosition2D, isOnLand: true));
        party.ActualClan = Clan.PlayerClan;
        PinPartyAi(party);
        return party.StringId;
    }

    protected virtual void AttachWarden(string partyId, string wardenHeroId)
    {
        var party = FindParty(partyId);
        var warden = FindHero(wardenHeroId);
        if (party == null || warden == null)
            return;
        AddHeroToPartyAction.Apply(warden, party, showNotification: false);
        (party.PartyComponent as RefugePartyComponent)?.SetWarden(warden);
        try
        {
            party.ChangePartyLeader(warden);
        }
        catch (Exception ex)
        {
            // A leaderless refuge still garrisons and defends; the leader slot is menu polish.
            // The engine's leader change asserts on edge states (source hit this too), so it
            // must not abort the founding.
            _logger.LogWarning($"[Refuge] leader change failed for '{partyId}': {ex.Message}");
        }
    }

    /// <summary>Moves troops, prisoners and stash into the main party, ignoring party-size
    /// limits (the player chose to dismantle; dropping soldiers on the ground is worse than an
    /// oversize party), then clears the refuge rosters.
    ///
    /// <para>HEROES NEVER RIDE THE BULK COPY. TroopRoster.Add fires OnHeroAdded, but the source
    /// roster's Clear() then fires OnHeroRemoved, and Hero.OnRemovedFromParty nulls
    /// PartyBelongedTo UNCONDITIONALLY (1.4.8 Hero.cs:2165), leaving the hero's persisted party
    /// binding null while his row sits in the main party - a save-corrupting desync. The same
    /// mechanism corrupts PartyBelongedToAsPrisoner for hero prisoners. So every hero member
    /// moves via AddHeroToPartyAction and every hero prisoner via TransferPrisonerAction FIRST;
    /// only the remaining regulars are bulk-copied.</para></summary>
    protected virtual void MergeRefugeIntoMainParty(string partyId)
    {
        foreach (var heroId in HeroesInRefugeRoster(partyId))
            MoveRefugeHeroToMainParty(heroId);
        foreach (var heroId in HeroPrisonersInRefuge(partyId))
            TransferHeroPrisonerToMainParty(partyId, heroId);
        BulkMergeRegularsIntoMainParty(partyId);
    }

    /// <summary>Hero StringIds among the refuge's members (the warden, deposited companions).</summary>
    protected virtual IReadOnlyList<string> HeroesInRefugeRoster(string partyId)
    {
        var result = new List<string>();
        var roster = FindParty(partyId)?.MemberRoster;
        if (roster == null)
            return result;
        for (int i = 0; i < roster.Count; i++)
        {
            var hero = roster.GetCharacterAtIndex(i)?.HeroObject;
            if (hero != null)
                result.Add(hero.StringId);
        }
        return result;
    }

    /// <summary>Moves one hero member into the main party with the real engine action, which
    /// removes him from the refuge roster and rebinds PartyBelongedTo correctly.</summary>
    protected virtual void MoveRefugeHeroToMainParty(string heroId)
    {
        var hero = FindHero(heroId);
        var main = MobileParty.MainParty;
        if (hero == null || main == null)
            return;
        AddHeroToPartyAction.Apply(hero, main, showNotification: false);
    }

    /// <summary>Hero StringIds among the refuge's prisoners.</summary>
    protected virtual IReadOnlyList<string> HeroPrisonersInRefuge(string partyId)
    {
        var result = new List<string>();
        var roster = FindParty(partyId)?.PrisonRoster;
        if (roster == null)
            return result;
        for (int i = 0; i < roster.Count; i++)
        {
            var hero = roster.GetCharacterAtIndex(i)?.HeroObject;
            if (hero != null)
                result.Add(hero.StringId);
        }
        return result;
    }

    /// <summary>Transfers one hero prisoner to the main party with the engine action, which keeps
    /// PartyBelongedToAsPrisoner in sync.</summary>
    protected virtual void TransferHeroPrisonerToMainParty(string partyId, string heroId)
    {
        var refuge = FindParty(partyId);
        var hero = FindHero(heroId);
        var main = MobileParty.MainParty;
        if (refuge == null || hero == null || main == null)
            return;
        TransferPrisonerAction.Apply(hero.CharacterObject, refuge.Party, main.Party);
    }

    /// <summary>Bulk-copies the remaining NON-HERO members, prisoners and items, then clears the
    /// refuge rosters. Hero rows are skipped defensively even though the action-based moves above
    /// should have emptied them (a hero the actions could not move must not be corrupted by a raw
    /// copy+clear).</summary>
    protected virtual void BulkMergeRegularsIntoMainParty(string partyId)
    {
        var refuge = FindParty(partyId);
        if (refuge == null)
            return;
        var main = MobileParty.MainParty;
        if (main != null)
        {
            CopyRegularRows(refuge.MemberRoster, main.MemberRoster);
            CopyRegularRows(refuge.PrisonRoster, main.PrisonRoster);
            for (int i = refuge.ItemRoster.Count - 1; i >= 0; i--)
                main.ItemRoster.Add(refuge.ItemRoster.GetElementCopyAtIndex(i));
        }
        RemoveRegularRows(refuge.MemberRoster);
        RemoveRegularRows(refuge.PrisonRoster);
        refuge.ItemRoster.Clear();
    }

    private static void CopyRegularRows(
        TaleWorlds.CampaignSystem.Roster.TroopRoster from,
        TaleWorlds.CampaignSystem.Roster.TroopRoster to)
    {
        for (int i = 0; i < from.Count; i++)
        {
            var element = from.GetElementCopyAtIndex(i);
            if (element.Character == null || element.Character.IsHero || element.Number <= 0)
                continue;
            to.AddToCounts(element.Character, element.Number, insertAtFront: false,
                element.WoundedNumber, element.Xp);
        }
    }

    private static void RemoveRegularRows(TaleWorlds.CampaignSystem.Roster.TroopRoster roster)
    {
        for (int i = roster.Count - 1; i >= 0; i--)
        {
            var element = roster.GetElementCopyAtIndex(i);
            if (element.Character == null || element.Character.IsHero)
                continue;
            roster.AddToCountsAtIndex(i, -element.Number, -element.WoundedNumber, -element.Xp);
        }
    }

    /// <summary>Releases the refuge's hero prisoners who are no longer at war with the refuge's
    /// faction, mirroring vanilla PrisonerReleaseCampaignBehavior.ReleasePartyPrisoners (which
    /// only enumerates caravans, war parties, villages and garrisons - never a custom
    /// component). Called after a peace involving the player's faction.</summary>
    protected virtual void ReleasePeacePrisoners(string partyId)
    {
        var refuge = FindParty(partyId);
        var refugeFaction = refuge?.MapFaction;
        if (refuge == null || refugeFaction == null)
            return;
        var roster = refuge.PrisonRoster;
        for (int i = roster.Count - 1; i >= 0; i--)
        {
            var hero = roster.GetCharacterAtIndex(i)?.HeroObject;
            if (hero == null || hero == Hero.MainHero)
                continue;
            if (hero.MapFaction != null && hero.MapFaction.IsAtWarWith(refugeFaction))
                continue;
            if (hero.PartyBelongedToAsPrisoner == refuge.Party)
                EndCaptivityAction.ApplyByPeace(hero);
            else
                roster.RemoveTroop(hero.CharacterObject);
        }
    }

    protected virtual void DestroyRefugeParty(string partyId)
    {
        var party = FindParty(partyId);
        if (party == null)
            return;
        try
        {
            DestroyPartyAction.Apply(null, party);
        }
        catch (Exception ex)
        {
            // Source parity: destruction can assert on a party mid-event; the book row is gone
            // either way and reconcile drops a straggler on the next load.
            _logger.LogWarning($"[Refuge] destroy failed for '{partyId}': {ex.Message}");
        }
    }

    protected virtual void HoldMainParty() => MobileParty.MainParty?.SetMoveModeHold();

    protected virtual IReadOnlyList<string> AllRefugePartyIds()
    {
        var result = new List<string>();
        foreach (var party in MobileParty.All)
        {
            if (party?.PartyComponent is RefugePartyComponent)
                result.Add(party.StringId);
        }
        return result;
    }

    protected virtual void PinRefugePartyAi(string partyId)
    {
        var party = FindParty(partyId);
        if (party != null)
            PinPartyAi(party);
    }

    private void PinPartyAi(MobileParty party)
    {
        party.SetMoveModeHold();
        try
        {
            party.Ai?.SetDoNotMakeNewDecisions(true);
        }
        catch (Exception ex)
        {
            // A refuge whose AI still decides is an annoyance, not a corruption; never let the
            // pin kill founding or loading.
            _logger.LogWarning($"[Refuge] AI pin failed for '{party.StringId}': {ex.Message}");
        }
    }

    /// <summary>Player-culture melee militia first, then the refuge clan's culture: the rally
    /// should look like the player's people whenever the culture defines militia at all.</summary>
    protected virtual string ResolveMilitiaTroopId(string partyId)
    {
        var playerTroop = Hero.MainHero?.Culture?.MeleeMilitiaTroop;
        if (playerTroop != null)
            return playerTroop.StringId;
        return FindParty(partyId)?.ActualClan?.Culture?.MeleeMilitiaTroop?.StringId;
    }

    protected virtual int RefugeGarrisonCount(string partyId) =>
        FindParty(partyId)?.MemberRoster?.TotalManCount ?? 0;

    protected virtual int GetTroopCountInRefuge(string partyId, string troopId)
    {
        var roster = FindParty(partyId)?.MemberRoster;
        var troop = FindTroop(troopId);
        if (roster == null || troop == null)
            return 0;
        return roster.GetTroopCount(troop);
    }

    protected virtual void AddTroopsToRefuge(string partyId, string troopId, int count)
    {
        var roster = FindParty(partyId)?.MemberRoster;
        var troop = FindTroop(troopId);
        if (roster == null || troop == null)
            return;
        roster.AddToCounts(troop, count);
    }

    protected virtual void RemoveTroopsFromRefuge(string partyId, string troopId, int count)
    {
        var roster = FindParty(partyId)?.MemberRoster;
        var troop = FindTroop(troopId);
        if (roster == null || troop == null)
            return;
        roster.AddToCounts(troop, -count);
    }

    protected virtual bool IsPartyInMapEvent(string partyId) =>
        FindParty(partyId)?.MapEvent != null;

    /// <summary>Nearest active hostile party with at least one soldier inside range, or null.
    /// Straight-line distance, like the source; a raid trigger does not need pathfinding.</summary>
    protected virtual RaidThreat FindNearestHostile(string refugePartyId, float range)
    {
        var refuge = FindParty(refugePartyId);
        var refugeFaction = refuge?.MapFaction;
        if (refuge == null || refugeFaction == null)
            return null;

        var position = refuge.GetPosition2D;
        MobileParty best = null;
        float bestDistance = range;
        foreach (var party in MobileParty.All)
        {
            if (party == null || party.IsMainParty || !party.IsActive || party.MapEvent != null)
                continue;
            if (party.PartyComponent is RefugePartyComponent)
                continue;
            var faction = party.MapFaction;
            if (faction == null || !faction.IsAtWarWith(refugeFaction))
                continue;
            if ((party.MemberRoster?.TotalManCount ?? 0) < 1)
                continue;
            float distance = position.Distance(party.GetPosition2D);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = party;
            }
        }
        if (best == null)
            return null;
        return new RaidThreat
        {
            PartyId = best.StringId,
            Name = best.Name?.ToString(),
            EngineParty = best,
        };
    }

    protected virtual void StartRaid(RaidThreat threat, string refugePartyId)
    {
        var refuge = FindParty(refugePartyId);
        if (refuge == null || !(threat?.EngineParty is MobileParty enemy))
            return;
        StartBattleAction.Apply(enemy.Party, refuge.Party);
    }

    protected virtual void ShowMessage(TextObject text, bool error) =>
        InformationManager.DisplayMessage(
            new InformationMessage(text.ToString(), error ? Colors.Red : Colors.Green));

    private static MobileParty FindParty(string partyId) =>
        string.IsNullOrEmpty(partyId) ? null : Campaign.Current?.CampaignObjectManager?.Find<MobileParty>(partyId);

    private static Hero FindHero(string heroId) =>
        string.IsNullOrEmpty(heroId) ? null : Campaign.Current?.CampaignObjectManager?.Find<Hero>(heroId);

    private static CharacterObject FindTroop(string troopId) =>
        string.IsNullOrEmpty(troopId) ? null : MBObjectManager.Instance?.GetObject<CharacterObject>(troopId);
}
