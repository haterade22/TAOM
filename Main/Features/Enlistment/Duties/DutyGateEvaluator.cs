using System.Collections.Generic;
using System.Linq;
using TAOM.Features.Enlistment.Content.Domain;

namespace TAOM.Features.Enlistment.Duties;

/// <summary>
/// Pure gate/weight logic shared by field duties, interactive duties, and incidents — all
/// three data rows carry the same <see cref="GateSpec"/> shape. No engine access, no
/// randomness; <see cref="DutySelector"/> and <see cref="DutyRotationPolicy"/> are the only
/// callers.
/// </summary>
public static class DutyGateEvaluator
{
    /// <summary>Preferred-assignment weight vs. baseline weight for the weighted-random pick in <see cref="DutySelector"/>.</summary>
    public const int PreferredAssignmentWeight = 3;
    public const int BaselineWeight = 1;

    public static bool IsEligible(GateSpec gates, ServiceRank rank, int trust, ISet<string> activeContexts)
    {
        if (gates == null)
            return true;

        if (rank < gates.MinRank)
            return false;

        if (trust < gates.MinTrust || trust > gates.MaxTrust)
            return false;

        var required = gates.RequiredContexts;
        if (required != null && required.Count > 0 && !required.Any(activeContexts.Contains))
            return false;

        var excluded = gates.ExcludedContexts;
        if (excluded != null && excluded.Count > 0 && excluded.Any(activeContexts.Contains))
            return false;

        return true;
    }

    /// <summary>Selection weight for the given assignment — preferred (in <c>AssignmentAffinity</c>) beats baseline. Empty affinity list = every assignment is baseline (no preference).</summary>
    public static int AffinityWeight(GateSpec gates, ServiceAssignment assignment)
    {
        var affinity = gates?.AssignmentAffinity;
        if (affinity == null || affinity.Count == 0)
            return BaselineWeight;
        return affinity.Contains(assignment) ? PreferredAssignmentWeight : BaselineWeight;
    }

    /// <summary>Builds the active-context set the scheduler's <c>GateSpec.RequiredContexts</c>/<c>ExcludedContexts</c> compare against. Known set: siege, naval, blockade, army, garrison, march.</summary>
    public static HashSet<string> ActiveContexts(ArmyRhythmSnapshot rhythm)
    {
        var contexts = new HashSet<string>(System.StringComparer.Ordinal);
        if (rhythm == null)
            return contexts;

        if (rhythm.SiegePressure) contexts.Add("siege");
        if (rhythm.Naval) contexts.Add("naval");
        if (rhythm.Blockade) contexts.Add("blockade");
        if (rhythm.InArmy) contexts.Add("army");
        if (rhythm.QuietGarrison) contexts.Add("garrison");
        if (rhythm.Marching) contexts.Add("march");
        return contexts;
    }
}
