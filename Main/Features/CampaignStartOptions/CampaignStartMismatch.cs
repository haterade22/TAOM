namespace TAOM.Features.CampaignStartOptions;

/// <summary>
/// Pure: does any value this campaign pinned differ from what MCM (else JSON) gives a new campaign now?
/// Only pinned values count; a value the campaign never pinned already follows MCM.
/// </summary>
public static class CampaignStartMismatch
{
    public static bool Differs(ICampaignStartOptions campaign, StartDefaults current)
    {
        if (!campaign.IsPinned) return false;

        return Differs(campaign.WarOfTheRing, current.WarOfTheRing)
            || (campaign.WarPhaseDays is { } days
                && (days.Phase1Day != current.Phase1Day || days.Phase2Day != current.Phase2Day))
            || Differs(campaign.TroopWeight, current.TroopWeight)
            || Differs(campaign.MomentumVictory, current.MomentumVictory)
            || Differs(campaign.EyeOfSauron, current.EyeOfSauron)
            || Differs(campaign.AiPartyScaling, current.AiPartyScaling)
            || (campaign.AlignmentRules is { } rules && !rules.Matches(current.Alignment));
    }

    private static bool Differs(bool? pinned, bool current) => pinned.HasValue && pinned.Value != current;
}
