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

    public bool ShouldBlockPeace(string kingdomAId, string kingdomBId)
    {
        if (CurrentPhase != WarPhase.FullWar) return false;
        if (!_config.Phase2.BlockPeaceBetweenHostileTiers) return false;

        return _diplomacyService.GetRelationshipTier(kingdomAId, kingdomBId) == AllianceTier.Hostile;
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
    // (idempotent). Only pairs the model calls at war (ShouldBlockPeace) are declared: a peace made
    // legitimately, or with blockPeaceBetweenHostileTiers off, is never undone by a reload.
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
    // kingdom, and TAOM's own DeclareWarAction veto (IsWarAllowed) can refuse a pair.
    private bool TryDeclareWar(string a, string b, string suffix)
    {
        _allianceAdapter.DeclareWar(a, b);
        if (_allianceAdapter.HasDeclaredWar(a, b))
        {
            _logger.LogInfo($"War of the Ring: {a} declares war on {b}{suffix}");
            return true;
        }
        _logger.LogWarning($"War of the Ring: war {a} -> {b} had no effect (unknown kingdom, or vetoed by IsWarAllowed)");
        return false;
    }
}
