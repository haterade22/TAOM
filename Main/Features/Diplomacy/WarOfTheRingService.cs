using System;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Diplomacy.Models;

namespace TAOM.Features.Diplomacy;

public class WarOfTheRingService : IWarOfTheRingService
{
    private readonly IDiplomacyService _diplomacyService;
    private readonly IAllianceAdapter _allianceAdapter;
    private readonly ITaomSettingsProvider _settingsProvider;
    private readonly IModLogger _logger;
    private readonly WarOfTheRingConfig _config;
    private readonly HashSet<string> _warnedSkippedWars = new HashSet<string>(StringComparer.Ordinal);

    public WarPhase CurrentPhase { get; private set; } = WarPhase.Peace;
    public bool IsWarOfTheRingActive => CurrentPhase == WarPhase.FullWar;
    public WarOutcome Outcome { get; private set; } = WarOutcome.None;
    public bool IsEnabled => GetEffectiveEnabled();

    // WotR Momentum #327 — terminal transition. First non-None outcome wins; the phase flip
    // to WarEnded is what lifts all three peace-block layers (they key off FullWar via
    // IsWarOfTheRingActive/ShouldBlockPeace). Peace-out of live wars is the momentum victory
    // service's job, ordered AFTER this call so MakePeaceAction is no longer blocked.
    public void EndWar(WarOutcome outcome)
    {
        if (outcome == WarOutcome.None) return;
        if (Outcome != WarOutcome.None) return;

        Outcome = outcome;
        CurrentPhase = WarPhase.WarEnded;
        _logger.LogInfo($"War of the Ring: war ended — {outcome}");
    }

    public void SetOutcomeFromSave(WarOutcome outcome)
    {
        Outcome = outcome;
        _logger.LogInfo($"War of the Ring: Outcome restored from save → {outcome}");
    }

    // Phase 9b #129 P1 — exposed for SyncData round-trip without giving public setter.
    // Behavior's SyncData calls SetPhaseFromSave during load to rehydrate.
    public void SetPhaseFromSave(WarPhase phase)
    {
        CurrentPhase = phase;
        _logger.LogInfo($"War of the Ring: Phase restored from save → {phase}");
    }

    // #764: the service outlives the campaign, and a brand-new game never loads SyncData.
    public void ResetForNewSession()
    {
        CurrentPhase = WarPhase.Peace;
        Outcome = WarOutcome.None;
        _logger.LogInfo("War of the Ring: phase reset to Peace at campaign start; a load restores the saved phase next");
    }

    public WarOfTheRingService(
        IWarOfTheRingConfigProvider configProvider,
        IDiplomacyService diplomacyService,
        IAllianceAdapter allianceAdapter,
        ITaomSettingsProvider settingsProvider,
        IModLogger logger)
    {
        _diplomacyService = diplomacyService;
        _allianceAdapter = allianceAdapter;
        _settingsProvider = settingsProvider;
        _logger = logger;

        _config = configProvider.LoadConfig();
    }

    // #819: the master toggle (MCM, else the JSON flag) gates the block like GetWarVeto and the
    // reconcile do, so switching the War of the Ring off during Full War lifts it at all three
    // peace layers (PeaceActionHook, IsAtConstantWar, the kingdom decision permission model).
    public bool ShouldBlockPeace(string kingdomAId, string kingdomBId)
    {
        if (CurrentPhase != WarPhase.FullWar) return false;
        if (!GetEffectiveEnabled()) return false;
        if (!_config.Phase2.BlockPeaceBetweenHostileTiers) return false;

        return _diplomacyService.GetRelationshipTier(kingdomAId, kingdomBId) == AllianceTier.Hostile;
    }

    // Score offsets for TaomDiplomacyModel.GetScoreOfDeclaringWar. The phase offset is vanilla's own
    // "never declare" value (DefaultDiplomacyModel.GetScoreOfDeclaringWar returns -10000000 for a
    // faction too weak to fight), so no settlement-value benefit outweighs it. Subtracting keeps the
    // order among phase-vetoed neighbours, which the Mercenary start uses to pick its enemy; the lore
    // offset is ten times larger so a Permanent or same-side pair always ranks below them, in every
    // phase: a same-side pair reads SameAlignment before Full War too (GetWarVeto checks it first).
    public const float PhaseVetoScorePenalty = 10000000f;
    public const float LoreVetoScorePenalty = 100000000f;

    // Order: Permanent, then (War of the Ring on and WarEnded) None, then the same-side rule, then the
    // phase vetoes. A same-side pair is refused in every phase before WarEnded, so the phase only
    // refuses pairs the lore rule allows. A player-caused war skips the phase vetoes but keeps the
    // Permanent and same-side ones, and WarEnded lifts the same-side rule for it as for the AI.
    public WarVeto GetWarVeto(string kingdomAId, string kingdomBId, bool causedByPlayer = false)
    {
        if (_diplomacyService.GetRelationshipTier(kingdomAId, kingdomBId) == AllianceTier.Permanent)
            return WarVeto.Permanent;

        var enabled = GetEffectiveEnabled();
        if (enabled && CurrentPhase == WarPhase.WarEnded) return WarVeto.None;

        if (!_diplomacyService.IsWarAllowed(kingdomAId, kingdomBId)) return WarVeto.SameAlignment;

        if (!enabled || causedByPlayer) return WarVeto.None;

        switch (CurrentPhase)
        {
            case WarPhase.Peace:
                return WarVeto.PhasePeace;
            case WarPhase.IsengardWar:
                return IsScriptedPhase1War(kingdomAId, kingdomBId) ? WarVeto.None : WarVeto.PhaseNotScripted;
            default:
                return WarVeto.None;
        }
    }

    public float GetDeclareWarScorePenalty(string kingdomAId, string kingdomBId)
    {
        switch (GetWarVeto(kingdomAId, kingdomBId))
        {
            case WarVeto.None:
                return 0f;
            case WarVeto.PhasePeace:
            case WarVeto.PhaseNotScripted:
                return PhaseVetoScorePenalty;
            default:
                return LoreVetoScorePenalty;
        }
    }

    private bool IsScriptedPhase1War(string kingdomAId, string kingdomBId)
    {
        foreach (var war in _config.Phase1.Wars)
        {
            if ((war.Attacker == kingdomAId && war.Defender == kingdomBId)
                || (war.Attacker == kingdomBId && war.Defender == kingdomAId))
                return true;
        }
        return false;
    }

    // Peace is blocked only in FullWar (PeaceActionHook), so MakePeace works in both phases handled here.
    // AreAtWar is the model-aware check, which differs from the stored stance only in FullWar.
    // A war involving the player's kingdom is judged as the player's own (causedByPlayer), so one that
    // only a phase veto forbids is kept: it comes from the player's own hostile act, the Mercenary
    // start or an old save, and the lore vetoes still end it.
    public int EndWarsForbiddenByPhase()
    {
        if (!GetEffectiveEnabled()) return 0;
        if (CurrentPhase != WarPhase.Peace && CurrentPhase != WarPhase.IsengardWar) return 0;

        int ended = 0;
        var playerKingdom = _allianceAdapter.GetPlayerKingdomId();
        var kingdoms = _allianceAdapter.GetAllKingdomIds();
        for (int i = 0; i < kingdoms.Count; i++)
        {
            for (int j = i + 1; j < kingdoms.Count; j++)
            {
                var a = kingdoms[i];
                var b = kingdoms[j];
                if (!_allianceAdapter.AreAtWar(a, b)) continue;

                var involvesPlayer = playerKingdom != null && (a == playerKingdom || b == playerKingdom);
                if (GetWarVeto(a, b, causedByPlayer: involvesPlayer) == WarVeto.None) continue;

                _allianceAdapter.MakePeace(a, b);
                if (!_allianceAdapter.AreAtWar(a, b)) ended++;
            }
        }

        if (ended > 0)
        {
            _logger.LogInfo($"War of the Ring: {CurrentPhase} ended {ended} wars the phase forbids");
        }
        return ended;
    }

    public void CheckPhaseTransition(float elapsedDays)
    {
        // WotR Momentum #327 — WarEnded is terminal; never re-enter the escalation ladder.
        if (CurrentPhase == WarPhase.WarEnded) return;
        if (!GetEffectiveEnabled()) return;

        var (phase1Day, phase2Day) = GetEffectivePhaseDays();

        if (CurrentPhase == WarPhase.Peace && elapsedDays >= phase1Day)
        {
            TransitionToPhase(WarPhase.IsengardWar);
        }

        if (CurrentPhase == WarPhase.IsengardWar && elapsedDays >= phase2Day)
        {
            TransitionToPhase(WarPhase.FullWar);
        }
    }

    private bool GetEffectiveEnabled()
    {
        if (_settingsProvider.IsAvailable) return _settingsProvider.WarOfTheRingEnabled;
        return _config.Enabled;
    }

    // #765: the War Chronicle's phase anchors read the same clamped pair the transitions use.
    public int? GetEffectivePhaseDay(WarPhase phase)
    {
        var (phase1Day, phase2Day) = GetEffectivePhaseDays();
        switch (phase)
        {
            case WarPhase.IsengardWar: return phase1Day;
            case WarPhase.FullWar: return phase2Day;
            default: return null;
        }
    }

    // Phase 2 must land strictly after Phase 1. CheckPhaseTransition's two guards are sequential
    // ifs and TransitionToPhase mutates CurrentPhase in place, so an equal or inverted pair runs
    // BOTH transitions inside one call — IsengardWar is entered and overwritten by FullWar before
    // any external reader (map meter, momentum service, save) can observe it, silently collapsing
    // the phased escalation. Clamping here rather than per-source covers all four inputs at once:
    // the MCM sliders (validated nowhere — they accept any pair inside [1,365]), the JSON pair,
    // the TestMode pair, and the compiled defaults. Dual-surface rule, csharp-architecture.md.
    private (int phase1Day, int phase2Day) GetEffectivePhaseDays()
    {
        var (phase1Day, phase2Day) = GetRawPhaseDays();

        phase1Day = Math.Max(1, phase1Day);
        phase2Day = Math.Max(phase1Day + 1, phase2Day);

        return (phase1Day, phase2Day);
    }

    private (int phase1Day, int phase2Day) GetRawPhaseDays()
    {
        if (_settingsProvider.IsAvailable && _settingsProvider.TestMode)
            return (_config.TestMode.Phase1Day, _config.TestMode.Phase2Day);

        if (_config.TestMode.Enabled)
            return (_config.TestMode.Phase1Day, _config.TestMode.Phase2Day);

        if (_settingsProvider.IsAvailable)
            return (_settingsProvider.Phase1TriggerDay, _settingsProvider.Phase2TriggerDay);

        return (_config.Phase1.TriggerDay, _config.Phase2.TriggerDay);
    }

    // #772: the phase turns first so that vanilla's OnWarDeclared listeners already treat every
    // Hostile pair as constantly at war: the ally filter in AllianceCampaignBehavior.OnWarDeclared
    // then calls no ally into a war this loop is about to declare. It also keeps TAOM's peace gates
    // live while KingdomDecisionProposalBehavior re-checks pending decisions. The declarations
    // guard on HasDeclaredWar (the real stance), so each one still runs under FullWar.
    private void TransitionToPhase(WarPhase newPhase)
    {
        _logger.LogInfo($"War of the Ring: Transitioning to {newPhase}");

        CurrentPhase = newPhase;

        switch (newPhase)
        {
            case WarPhase.IsengardWar:
                DeclareConfiguredWars(_config.Phase1.Wars, null);
                break;
            case WarPhase.FullWar:
                DeclareFullWarWars(null);
                break;
        }
    }

    private int DeclareFullWarWars(Func<string, string, bool> eligible)
    {
        int declared = DeclareConfiguredWars(_config.Phase2.Wars, eligible);
        if (_config.Phase2.AutoWarBetweenHostileTiers)
        {
            declared += DeclareHostileTierWars(eligible);
        }
        return declared;
    }

    // #772: in FullWar, declares every Phase 2 / Hostile-pair war whose stored stance is not War
    // (idempotent). Only pairs the model calls at war (ShouldBlockPeace) are declared: a peace the
    // model allows (a scripted pair that is not Hostile), or one made with
    // blockPeaceBetweenHostileTiers off, is never undone by a reload. The exception (#819): a peace
    // a Hostile pair made while the War of the Ring was switched off does not last. Once the switch
    // is on again the model holds the pair at war, and this declares that war on the host's next
    // daily tick (#1163), or at the next launch.
    public void ReconcileDeclaredWars()
    {
        if (CurrentPhase != WarPhase.FullWar) return;
        if (!GetEffectiveEnabled()) return;

        int declared = DeclareFullWarWars(ShouldBlockPeace);
        if (declared > 0)
        {
            _logger.LogInfo($"War of the Ring: declared {declared} missing Full War wars (#772)");
        }
    }

    // eligible == null means every pair (a phase transition); the reconcile passes ShouldBlockPeace.
    private int DeclareConfiguredWars(List<WarDeclaration> wars, Func<string, string, bool> eligible)
    {
        int declared = 0;
        var live = new HashSet<string>(_allianceAdapter.GetAllKingdomIds());
        foreach (var war in wars)
        {
            // An eliminated or unknown kingdom keeps no stance link, so HasDeclaredWar stays false
            // and the declaration would be retried and counted on every load.
            if (!live.Contains(war.Attacker) || !live.Contains(war.Defender))
            {
                WarnSkippedOnce(war);
                continue;
            }
            if (eligible != null && !eligible(war.Attacker, war.Defender)) continue;

            if (!_allianceAdapter.HasDeclaredWar(war.Attacker, war.Defender)
                && TryDeclareWar(war.Attacker, war.Defender, ""))
            {
                declared++;
            }
        }
        return declared;
    }

    private int DeclareHostileTierWars(Func<string, string, bool> eligible)
    {
        int declared = 0;
        var kingdoms = _allianceAdapter.GetAllKingdomIds();
        for (int i = 0; i < kingdoms.Count; i++)
        {
            for (int j = i + 1; j < kingdoms.Count; j++)
            {
                var a = kingdoms[i];
                var b = kingdoms[j];

                if (_diplomacyService.GetRelationshipTier(a, b) == AllianceTier.Hostile
                    && (eligible == null || eligible(a, b))
                    && !_allianceAdapter.HasDeclaredWar(a, b)
                    && TryDeclareWar(a, b, " (hostile tier)"))
                {
                    declared++;
                }
            }
        }
        return declared;
    }

    // #772 Codex R3: the provider cannot see the live kingdoms, so a misspelt id ("rohan") reaches
    // here. Named once per process, not on every load; an eliminated kingdom is named the same way.
    private void WarnSkippedOnce(WarDeclaration war)
    {
        if (_warnedSkippedWars.Add(war.Attacker + " -> " + war.Defender))
        {
            _logger.LogWarning($"War of the Ring: scripted war {war.Attacker} -> {war.Defender} skipped: a kingdom id is unknown or eliminated (check war_of_the_ring.json)");
        }
    }

    // Counts only a war the stance confirms after the call: DeclareWar does nothing for an unknown
    // kingdom, and TAOM's own DeclareWarAction veto (GetWarVeto) can refuse a pair.
    private bool TryDeclareWar(string a, string b, string suffix)
    {
        _allianceAdapter.DeclareWar(a, b);
        if (_allianceAdapter.HasDeclaredWar(a, b))
        {
            _logger.LogInfo($"War of the Ring: {a} declares war on {b}{suffix}");
            return true;
        }
        // Once per pair and process: the daily repair retries the declaration (#1163), and a pair that
        // is refused every day would otherwise write this line every day.
        if (_warnedSkippedWars.Add(a + " -> " + b + " (no effect)"))
            _logger.LogWarning($"War of the Ring: war {a} -> {b} had no effect (unknown kingdom, or vetoed by GetWarVeto)");
        return false;
    }
}
