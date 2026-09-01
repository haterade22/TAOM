using System;
using System.Collections.Generic;
using TAOM.Features.Enlistment.Content.Domain;

namespace TAOM.Features.Enlistment.Equipment;

/// <summary>
/// Pure roster resolution over <c>enlist_{culture}_{assignment}_{rank}</c>. The chain walks
/// CULTURE first, then assignment, then rank:
/// <code>
/// for culture in [requested, "default"]:
///   for assignment in [requested, Infantry]:     // deduped
///     for rank from requested down to Recruit:
///       probe enlist_{culture}_{assignment}_{rank}
/// </code>
///
/// <para><b>Why culture outranks assignment.</b> Because issuing another faction's kit is the
/// defect players actually report: #427 was "the quartermaster gives me gondor gloves and I'm
/// enlisted under Theoden", and #431 was the same complaint arriving through the neutral fallback,
/// which is tagged <c>Culture.neutral_culture</c> while being Rohan militia in Dunland boots.
/// Keeping the culture and losing the role gives a soldier the wrong job in his own army's gear;
/// keeping the role and losing the culture dresses him as somebody else's soldier.</para>
///
/// <para>An earlier version of this comment justified the ordering as a RENDERING invariant, on
/// the grounds that cross-race armour clips on a custom skeleton. That argument does not hold and
/// is not the reason: the roster is keyed on the COMMANDER's culture, so it cannot know the
/// player's race at all. A human serving a goblin lord draws goblin-rigged gear under either
/// ordering. Clipping is a real hazard (lessons/data-content-cultures.md) but it is orthogonal to
/// this ordering, and it is recorded as a known limitation in docs/features/enlistment.md.</para>
///
/// <para><b>Why rank is innermost.</b> The right role at a lower rank beats the wrong role at
/// the right rank: a lesser version of what the player asked for is a smaller disappointment
/// than a kit for somebody else's job.</para>
///
/// <para>Assignment falls back to Infantry because that is the one assignment every culture
/// authors. Inside the default culture the assignment is preferred again, which is why the
/// culture loop is outside rather than the two being interleaved.</para>
///
/// Existence is delegated to the caller-supplied probe so the chain is fully unit-testable
/// without the engine (the service passes IEquipmentRosterCatalogAdapter.RosterExists).
/// </summary>
public static class EnlistmentRosterResolver
{
    /// <returns>The first existing roster id along the chain, or null when nothing exists.</returns>
    public static string Resolve(
        string cultureId,
        ServiceAssignment assignment,
        EnlistmentRank rank,
        Func<string, bool> rosterExists)
    {
        if (rosterExists == null)
            throw new ArgumentNullException(nameof(rosterExists));

        foreach (var assignmentStep in AssignmentChain(assignment))
        {
            if (string.IsNullOrEmpty(cultureId))
                break;

            var hit = ProbeRanks(
                r => EnlistmentRosterIds.Build(cultureId, assignmentStep, r), rank, rosterExists);
            if (hit != null)
                return hit;
        }

        foreach (var assignmentStep in AssignmentChain(assignment))
        {
            var hit = ProbeRanks(
                r => EnlistmentRosterIds.BuildDefault(assignmentStep, r), rank, rosterExists);
            if (hit != null)
                return hit;
        }

        return null;
    }

    /// <summary>
    /// The requested assignment, then Infantry — deduped, so an Infantry request does not walk
    /// the same ids twice. Compared on the TOKEN rather than the enum value: an ordinal outside
    /// the enum resolves to the infantry token, and comparing ordinals would miss that and probe
    /// the identical ids a second time.
    /// </summary>
    private static IEnumerable<ServiceAssignment> AssignmentChain(ServiceAssignment assignment)
    {
        yield return assignment;

        var requested = EnlistmentRosterIds.AssignmentToken(assignment);
        var fallback = EnlistmentRosterIds.AssignmentToken(ServiceAssignment.Infantry);
        if (!string.Equals(requested, fallback, StringComparison.Ordinal))
            yield return ServiceAssignment.Infantry;
    }

    private static string ProbeRanks(
        Func<EnlistmentRank, string> build, EnlistmentRank rank, Func<string, bool> rosterExists)
    {
        for (var r = (int)rank; r >= (int)EnlistmentRank.Recruit; r--)
        {
            var id = build((EnlistmentRank)r);
            if (rosterExists(id))
                return id;
        }
        return null;
    }
}
