using System;
using System.Collections.Generic;
using BehaviorTreeWrapper;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.Animalia;
using TAOM.Features.AnimationVariety.Hooks;
using TAOM.Features.ArmourAcquisition.Hooks;
using TAOM.Features.BannerBearers.Hooks;
using TAOM.Features.BattleCorpses.Hooks;
using TAOM.Features.BattleLoadDiagnostics.Hooks;
using TAOM.Features.CareerSystem;
using TAOM.Features.CompanionTactics.BattleActionBar.Hooks;
using TAOM.Features.CrashReport.DevTriggers;
using TAOM.Features.CreatureBandits;
using TAOM.Features.CreatureBandits.Diagnostics;
using TAOM.Features.CreatureSiegeRole.Hooks;
using TAOM.Features.CultureDoctrine.Hooks;
using TAOM.Features.CustomBattles;
using TAOM.Features.DreadAura.Hooks;
using TAOM.Features.Elephant;
using TAOM.Features.Elk;
using TAOM.Features.Enlistment.Hooks;
using TAOM.Features.FellFoes.Hooks;
using TAOM.Features.FieldCommission.Hooks;
using TAOM.Features.HostReview.Hooks;
using TAOM.Features.LostFooting.Hooks;
using TAOM.Features.MissionDiagnostic.Hooks;
using TAOM.Features.MissionPerf.AnimMemory.Hooks;
using TAOM.Features.MissionPerf.Hooks;
using TAOM.Features.MixedFormations.Hooks;
using TAOM.Features.MountDespawn.Hooks;
using TAOM.Features.Mumakil;
using TAOM.Features.RaceAbilities.Hooks;
using TAOM.Features.ShaderPrecompilation;
using TAOM.Features.SiegeDismount.Hooks;
using TAOM.Features.SiegePropDiagnostics.Hooks;
using TAOM.Features.SignatureStrikes.Hooks;
using TAOM.Features.SkeletonBuffer.Hooks;
using TAOM.Features.StagedSiege.Spike;
using TAOM.Features.SmartCavalryAI.Hooks;
using TAOM.Features.Spider;
using TAOM.Features.TrollBruteForce;
using TAOM.Features.Warg;
using TAOM.Features.WarRam;

namespace TAOM.Features.CoopInterop.Missions;

/// <summary>
/// The co-op role of every TAOM mission behavior (#967, C7), the three added outside
/// <c>AddTaomBehavior</c> included. Keyed by simple type name: the two MissionView behaviors sit on
/// TaleWorlds.MountAndBlade.View, which the unit-test host cannot load, so a typeof key would fail every test.
/// <c>CoopMissionRolePolicyTests</c> compares this table with the behaviors
/// SubModule.cs and the feature modules add, so a new behavior fails a test until it is classified here.
/// The per-behavior reasoning is the "Mission behaviors" table in docs/features/coop-interop.md.
/// </summary>
public static class CoopMissionRoles
{
    public static readonly IReadOnlyDictionary<string, CoopMissionRole> All = new Dictionary<string, CoopMissionRole>
    {
        // Unsafe: dropped from a session.
        [nameof(ShaderPrecompilePlayerAgentGuard)] = CoopMissionRole.Unsafe,
        [nameof(SiegeDismountMissionBehavior)] = CoopMissionRole.Unsafe,
        [nameof(EnlistmentMeritMissionBehavior)] = CoopMissionRole.Unsafe,
        [nameof(FieldCommissionMissionLogic)] = CoopMissionRole.Unsafe,
        // Stand down in a session by Mike's 2026-10-19 decision (FellFoesWiringTests pin it); co-op has no
        // "stopped fleeing" message, so a rally would not replicate. LostFooting reuses FellFoes' gate.
        [nameof(FellFoesMissionLogic)] = CoopMissionRole.Unsafe,
        [nameof(LostFootingMissionLogic)] = CoopMissionRole.Unsafe,
        [nameof(StagedSiegeSpikeMissionBehavior)] = CoopMissionRole.Unsafe,

        // AgentOwner: acts only on agents this peer owns, by a live check or a named invariant (see the doc row).
        [nameof(BehaviorTreeMissionLogic)] = CoopMissionRole.AgentOwner,
        [nameof(AdvancedCombatBehavior)] = CoopMissionRole.AgentOwner,
        [nameof(WargMissionBehavior)] = CoopMissionRole.AgentOwner,
        [nameof(SpiderMissionBehavior)] = CoopMissionRole.AgentOwner,
        [nameof(ElephantMissionBehavior)] = CoopMissionRole.AgentOwner,
        [nameof(MumakilMissionBehavior)] = CoopMissionRole.AgentOwner,
        [nameof(WarRamMissionBehavior)] = CoopMissionRole.AgentOwner,
        [nameof(ElkMissionBehavior)] = CoopMissionRole.AgentOwner,
        [nameof(AnimaliaMissionBehavior)] = CoopMissionRole.AgentOwner,
        [nameof(TrollBruteForceMissionBehavior)] = CoopMissionRole.AgentOwner,
        [nameof(MixedFormationsMissionBehavior)] = CoopMissionRole.AgentOwner,
        [nameof(SmartCavalryAIMissionBehavior)] = CoopMissionRole.AgentOwner,
        [nameof(BannerBearerAssignmentMissionLogic)] = CoopMissionRole.AgentOwner,
        [nameof(DreadAuraMissionLogic)] = CoopMissionRole.AgentOwner,
        [nameof(SignatureStrikesMissionLogic)] = CoopMissionRole.AgentOwner,
        [nameof(CareerPerkMissionBehavior)] = CoopMissionRole.AgentOwner,
        [nameof(CreatureSiegeRoleMissionBehavior)] = CoopMissionRole.AgentOwner,
        [nameof(CreatureBanditMissionBehavior)] = CoopMissionRole.AgentOwner,
        [nameof(RaceAbilitiesMissionLogic)] = CoopMissionRole.AgentOwner,

        // Local: this peer's own view, log or files.
        // TAOM.Client [DefaultView] types, added by the engine (ADR-013); ClientViewRows_MatchTheClientTypeNames pins the strings.
        ["AutonomousMovementPlayerController"] = CoopMissionRole.Local,
        [nameof(SiegePropDiagnosticsMissionBehavior)] = CoopMissionRole.Local,
        [nameof(MountDespawnMissionBehavior)] = CoopMissionRole.Local,
        [nameof(EnlistmentBattleRoleMissionBehavior)] = CoopMissionRole.Local,
        [nameof(EnlistmentBattleFormationMissionBehavior)] = CoopMissionRole.Local,
        [nameof(MissionPerfHeartbeatBehavior)] = CoopMissionRole.Local,
        [nameof(MissionTickProfilerBehavior)] = CoopMissionRole.Local,
        ["BattleActionBarMissionView"] = CoopMissionRole.Local,
        [nameof(MissionDiagnosticBehavior)] = CoopMissionRole.Local,
        [nameof(CrashReportDevTriggerMissionBehavior)] = CoopMissionRole.Local,
        [nameof(HeroKillCounterMissionLogic)] = CoopMissionRole.Local,
        [nameof(AnimVarietyMissionBehavior)] = CoopMissionRole.Local,
        [nameof(GaitVarietyMissionBehavior)] = CoopMissionRole.Local,
        [nameof(BattleCorpseMissionBehavior)] = CoopMissionRole.Local,
        [nameof(CreatureBanditDiagnosticsBehavior)] = CoopMissionRole.Local,
        [nameof(AnimMemoryProbeMissionBehavior)] = CoopMissionRole.Local,
        [nameof(SkeletonBufferWatchMissionBehavior)] = CoopMissionRole.Local,

        // Local, added outside AddTaomBehavior.
        [nameof(BattleLoadPhaseBehavior)] = CoopMissionRole.Local,
        [nameof(CustomBattleTeamFixBehavior)] = CoopMissionRole.Local,
        [nameof(HostReviewMissionLogic)] = CoopMissionRole.Local,

        // Local: tactics move only AI-controlled agents, so on a guest (where the host's AI teams are
        // puppets) it has nothing to move.
        [nameof(CultureDoctrineMissionLogic)] = CoopMissionRole.Local,
    };

    /// <summary>The role of a behavior type; an unlisted type is Local so a session never loses it silently.</summary>
    public static CoopMissionRole Of(Type behaviorType) =>
        All.TryGetValue(behaviorType.Name, out var role) ? role : CoopMissionRole.Local;
}
