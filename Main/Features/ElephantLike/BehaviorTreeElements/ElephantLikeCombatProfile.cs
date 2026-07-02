using System;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.ElephantLike.BehaviorTreeElements;

/// <summary>
/// Per-creature boundary tuning consumed by the shared elephant-like BT nodes: scan ranges, blow magnitude, the
/// four attack-clip caches, and the creature's service resolver. One static instance per feature
/// (<c>ElephantCombat.Profile</c> / <c>MumakilCombat.Profile</c>) so <c>ActionIndexCache.Create</c> keeps its
/// resolve-once semantics (EAGER in v1.4.5 — no lazy retry). The clip names live in the EXTERNAL LOTRLOME_Armory
/// action_types.xml; each feature's MissionBehavior.Initialize logs an error when <see cref="AnyUnresolved"/>
/// (the bad-name → channel-0 locomotion-kill "slide" failure class — see docs/features/elephant.md
/// "Action code correction").
/// </summary>
public sealed class ElephantLikeCombatProfile
{
    public ElephantLikeCombatProfile(
        float trampleTriggerRange,
        float trampleRadius,
        float trampleBlowMagnitude,
        string trampleActionName,
        string trampleAltActionName,
        string sideAttackLeftActionName,
        string sideAttackRightActionName,
        Func<IElephantLikeAttackService> resolveService)
    {
        TrampleTriggerRange = trampleTriggerRange;
        TrampleRadius = trampleRadius;
        TrampleBlowMagnitude = trampleBlowMagnitude;
        Trample = ActionIndexCache.Create(trampleActionName);
        TrampleAlt = ActionIndexCache.Create(trampleAltActionName);
        SwingLeft = ActionIndexCache.Create(sideAttackLeftActionName);
        SwingRight = ActionIndexCache.Create(sideAttackRightActionName);
        ResolveService = resolveService;
    }

    /// <summary>Proximity gate: an attack only fires when a live enemy is within this distance of the creature's
    /// CENTER and in front of it. Must stay ≤ <see cref="TrampleRadius"/>.</summary>
    public float TrampleTriggerRange { get; }

    /// <summary>Radius around the creature inside which enemies are trampled (also the single scan radius).</summary>
    public float TrampleRadius { get; }

    /// <summary>Blow magnitude passed to the damage primitive — knockback impulse, independent of inflicted HP.</summary>
    public float TrampleBlowMagnitude { get; }

    /// <summary>The trample (double-sweep thrash) animation.</summary>
    public ActionIndexCache Trample { get; }

    /// <summary>Trample variant (near-identical double sweep) — alternated randomly with the primary for variety.</summary>
    public ActionIndexCache TrampleAlt { get; }

    /// <summary>Left tusk-swing animation (strike sweeps toward the creature's LEFT — played for a left-bearing target).</summary>
    public ActionIndexCache SwingLeft { get; }

    /// <summary>Right tusk-swing animation (strike sweeps toward the creature's RIGHT — played for a right-bearing target).</summary>
    public ActionIndexCache SwingRight { get; }

    /// <summary>Lazy resolver for the creature's registered attack service (nodes resolve once, on first use).</summary>
    public Func<IElephantLikeAttackService> ResolveService { get; }

    /// <summary>True when <paramref name="current"/> is one of this creature's attack clips (Index compare —
    /// zero-alloc, no per-eval native GetName() marshal, immune to other action names containing "attack").</summary>
    public bool IsAttack(ActionIndexCache current)
        => current == Trample || current == TrampleAlt || current == SwingLeft || current == SwingRight;

    /// <summary>True when any attack name failed to resolve against the loaded action data (Armory drift).</summary>
    public bool AnyUnresolved()
        => Trample == ActionIndexCache.act_none
        || TrampleAlt == ActionIndexCache.act_none
        || SwingLeft == ActionIndexCache.act_none
        || SwingRight == ActionIndexCache.act_none;
}
