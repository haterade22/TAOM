using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem;

/// <summary>
/// Issue #381 — keystone branch exclusivity (career design, adopted 2026-08-05):
/// a tier's ordinary stones stay open on both paths, but its KEYSTONES are mutually
/// exclusive — taking one closes the other keystones in that tier. Completing any full
/// tier-3 group is the endgame reward that reopens every keystone passed over.
///
/// Grandfathering: an already-taken choice is never locked, so a save holding multiple
/// same-tier keystones (pre-rule or post-rebalance) keeps them taken and refundable —
/// the rule gates FUTURE takes only. One rule, three consumers: both CareerScreenVM
/// select paths and the RebuildChoiceGroups display state (a locked keystone dims and
/// loses its + button instead of silently rejecting the click).
/// </summary>
public static class KeystoneExclusivityRule
{
    public static bool IsLocked(
        CareerDefinition career,
        CareerChoiceDefinition choice,
        ICareerRegistry registry,
        HeroCareerData heroData)
    {
        if (career == null || choice == null || registry == null || heroData == null) return false;
        if (choice.Type != ChoiceType.Keystone) return false;
        if (heroData.HasChoice(choice.Id)) return false;

        var group = registry.GetGroup(choice.GroupId);
        if (group == null) return false;

        if (HasCompleteTier3Group(career, registry, heroData)) return false;

        // Locked iff another keystone is already taken in this choice's tier.
        foreach (var groupId in career.ChoiceGroupIds)
        {
            var otherGroup = registry.GetGroup(groupId);
            if (otherGroup == null || otherGroup.Tier != group.Tier) continue;

            foreach (var other in registry.GetChoicesForGroup(groupId))
            {
                if (other.Type == ChoiceType.Keystone
                    && other.Id != choice.Id
                    && heroData.HasChoice(other.Id))
                    return true;
            }
        }

        return false;
    }

    private static bool HasCompleteTier3Group(
        CareerDefinition career,
        ICareerRegistry registry,
        HeroCareerData heroData)
    {
        foreach (var groupId in career.ChoiceGroupIds)
        {
            var group = registry.GetGroup(groupId);
            if (group == null || group.Tier != 3) continue;

            var choices = registry.GetChoicesForGroup(groupId);
            if (choices == null || choices.Count == 0) continue;

            var complete = true;
            foreach (var c in choices)
            {
                if (!heroData.HasChoice(c.Id))
                {
                    complete = false;
                    break;
                }
            }

            if (complete) return true;
        }

        return false;
    }
}
