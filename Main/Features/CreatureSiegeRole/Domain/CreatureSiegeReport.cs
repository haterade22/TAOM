using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TAOM.Features.CreatureSiegeRole.Domain;

/// <summary>
/// The text of the diagnostic lines the creature siege role writes for the in-game spikes (docs/features/creature-siege-role.md
/// "Reading the log"): the one activation line per mission, the reason a mission is inert, the face and truncation warnings,
/// the role-change and re-apply lines and the gate-blow line. Pure strings, built only when a line is going to be written, so
/// nothing here costs a hot path anything while a log call is skipped. Numbers use the invariant culture: a comma decimal
/// separator would make a coordinate read as two.
/// </summary>
public static class CreatureSiegeReport
{
    private const string Tag = "[CreatureSiegeRole]";

    /// <summary>
    /// The one INFO line per active mission: scene, both gates (name, visibility, state, hit points, origin), the damage
    /// multiplier, the exclusion ids in order with anything skipped or dropped, and the hold and courtyard anchors chosen, each
    /// with its XY distance from the outer gate, the doorway the trolls fight at (the check that a scene's anchors sit where
    /// the gate does; an inner-gate anchor reads far from it by design where the two gates are far apart).
    /// </summary>
    public static string Activation(string scene, SiegeGateReading outer, GateLiveState outerState, SiegeGateReading? inner,
        GateLiveState innerState, float multiplier, ExclusionPlan plan, AnchorCandidate? hold, AnchorCandidate? courtyard) =>
        $"{Tag} active on {scene}: outer gate {Gate(outer, outerState)}, {(inner == null ? "no inner gate" : "inner gate " + Gate(inner, innerState))}, " +
        $"gate damage x{F1(multiplier)}, exclusion [{Join(plan.Ids)}], skipped {Skipped(plan.Skipped)}, dropped {Dropped(plan.Dropped)}, " +
        $"hold anchor {Anchor(hold, outer)}, courtyard anchor {Anchor(courtyard, outer)}";

    /// <summary>The line for a mission the role does not act in, and why.</summary>
    public static string Inert(ActivationVerdict verdict, string scene) => $"{Tag} inert on {scene}: {Why(verdict)}";

    /// <summary>One WARNING per id left out of the exclusion list for being 0 or less.</summary>
    public static string SkippedFace(SkippedFace face, string scene) =>
        $"{Tag} skipped navmesh face id {face.Tier} {face.Value} on {scene}: not a usable face id (0 or less), so creatures stay free to walk on it";

    /// <summary>The WARNING when the cap cut valid ids off the exclusion list.</summary>
    public static string Truncated(ExclusionPlan plan, string scene, int cap) =>
        $"{Tag} exclusion list cut at {cap} ids on {scene}; dropped {Join(plan.Dropped)} " +
        "(tower entrances are kept before ladders, ladders before tower bridges)";

    /// <summary>The one WARNING when an excluded creature is seen in a ladder queue: the exclusion did not keep it out (spike RG-A).</summary>
    public static string LadderQueue(string scene, SiegePoint at) =>
        $"{Tag} an excluded creature is in a ladder queue on {scene} at {Point(at)}: " +
        "the per-agent face exclusion did not keep it out of the queue";

    /// <summary>The one WARNING when no strike or stand-off slot could be reached by a creature.</summary>
    public static string NoReachableSlot(string scene, SiegeRole role) =>
        $"{Tag} no reachable {role} slot on {scene} for a creature: it is left to vanilla's AI (this warning is not repeated)";

    /// <summary>The one WARNING when no hold anchor was valid for a creature.</summary>
    public static string NoValidAnchor(string scene, SiegeRole role) =>
        $"{Tag} no valid anchor for {role} on {scene}: every candidate is a wall top or has no path; " +
        "the creature is left to vanilla's AI (this warning is not repeated)";

    /// <summary>The one ERROR when the reconcile threw: what stopped, and that every routed creature was released.</summary>
    public static string Fault(string scene, Exception exception) =>
        $"{Tag} the reconcile failed on {scene} ({exception.GetType().Name}: {exception.Message}); " +
        "every routed creature was released and routing stopped for this battle; the detachment cost and gate damage rules stay on";

    /// <summary>The one ERROR when activation threw in <c>AfterStart</c>: the role is inert for this battle and nothing was published.</summary>
    public static string ActivationFault(string scene, Exception exception) =>
        $"{Tag} activation failed on {scene} ({exception.GetType().Name}: {exception.Message}); " +
        "the role is inert for this battle and the detachment cost and gate damage rules are off";

    /// <summary>
    /// The DEBUG line for a creature whose role changed. <paramref name="slot"/> and <paramref name="target"/> are left out
    /// of a release.
    /// </summary>
    public static string RoleChange(SiegeRole role, RoleReason reason, int slot, SiegePoint? target, SiegePoint agentAt,
        bool inLadderQueue, int suppressed) =>
        $"{Tag} creature {role} ({reason})" +
        (slot >= 0 ? $" slot {slot}" : "") +
        (target.HasValue ? $" at {Point(target.Value)}" : "") +
        $", creature at {Point(agentAt)}, inLadderQueue {inLadderQueue}" + Swallowed(suppressed);

    /// <summary>The DEBUG line when the engine cleared a creature's scripted state and the role set it again.</summary>
    public static string Reapplied(SiegeRole role, bool hasScriptedPosition, bool isAttackingEntity, int suppressed)
    {
        var cleared = !hasScriptedPosition && !isAttackingEntity ? "position and target"
            : !hasScriptedPosition ? "position"
            : "target";
        return $"{Tag} creature {role} re-applied: the engine cleared its scripted {cleared}" + Swallowed(suppressed);
    }

    /// <summary>
    /// The DEBUG line for a creature's blow on a gate: the damage before and after the multiplier, and the gate's hit points
    /// before the blow. There is no "after": the engine applies campaign damage bonuses once the hook has returned, so the
    /// hit points the blow leaves cannot be computed here.
    /// </summary>
    public static string GateBlow(float baseDamage, float scaledDamage, float multiplier, float hitPointBefore, int suppressed) =>
        $"{Tag} creature gate blow: base {F1(baseDamage)}, scaled {F1(scaledDamage)} (x{F1(multiplier)}), " +
        $"gate hp {F1(hitPointBefore)} before the blow" + Swallowed(suppressed);

    private static string Gate(SiegeGateReading gate, GateLiveState state)
    {
        var door = state.Destroyed ? "destroyed" : state.Open ? "open" : "closed";
        return $"\"{gate.Name}\" ({(gate.Visible ? "visible" : "hidden")}, {door}, hp {F0(state.HitPoint)}, origin {F1(gate.OriginX)}, {F1(gate.OriginY)}, {F1(gate.OriginZ)})";
    }

    private static string Anchor(AnchorCandidate? anchor, SiegeGateReading outer)
    {
        if (!anchor.HasValue) return "none";

        var distance = DistanceXY(anchor.Value.Point, outer);
        var text = float.IsNaN(distance) || float.IsInfinity(distance) ? "distance unknown" : F1(distance) + " m from the outer gate";
        return $"{anchor.Value.Source} ({Point(anchor.Value.Point)}) {text}";
    }

    private static float DistanceXY(SiegePoint point, SiegeGateReading gate)
    {
        var dx = point.X - gate.OriginX;
        var dy = point.Y - gate.OriginY;
        return (float)Math.Sqrt(dx * dx + dy * dy);
    }

    private static string Skipped(IReadOnlyList<SkippedFace> skipped) =>
        skipped.Count == 0 ? "none" : "[" + string.Join(", ", skipped.Select(s => $"{s.Tier} {s.Value}")) + "]";

    private static string Dropped(IReadOnlyList<int> dropped) => dropped.Count == 0 ? "none" : "[" + Join(dropped) + "]";

    private static string Join(IReadOnlyList<int> ids) =>
        string.Join(", ", ids.Select(i => i.ToString(CultureInfo.InvariantCulture)));

    private static string Point(SiegePoint point) => $"{F1(point.X)}, {F1(point.Y)}, {F1(point.Z)}";

    private static string Swallowed(int suppressed) => suppressed > 0 ? $" [{suppressed} suppressed]" : "";

    private static string F1(float value) => value.ToString("F1", CultureInfo.InvariantCulture);

    private static string F0(float value) => value.ToString("F0", CultureInfo.InvariantCulture);

    private static string Why(ActivationVerdict verdict) => verdict switch
    {
        ActivationVerdict.Active => "active",
        ActivationVerdict.NotSiege => "not a siege battle",
        ActivationVerdict.SallyOutOrRelief => "a sally-out or relief force, which vanilla plays",
        ActivationVerdict.ClientOrReplay => "this peer does not run the AI (a network client or a replay)",
        ActivationVerdict.Disabled => "the Creature Siege Role setting is off",
        ActivationVerdict.NoCreatureRace => "no creature race is registered in this module set",
        ActivationVerdict.NoOuterGate => "no usable outer gate (none tagged, or every one hidden or disabled)",
        _ => verdict.ToString(),
    };
}
