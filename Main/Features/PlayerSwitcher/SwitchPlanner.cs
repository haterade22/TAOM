using TAOM.Features.PlayerSwitcher.Domain;

namespace TAOM.Features.PlayerSwitcher;

/// <inheritdoc cref="ISwitchPlanner"/>
public class SwitchPlanner : ISwitchPlanner
{
    public SwitchPlan Plan(HeroPickRow row, PlayerSwitchPolicy policy, string careerId)
    {
        if (!policy.Enabled || row.IsEmpty)
            return SwitchPlan.None;

        // The only branch in the whole feature. A hero who already belongs to a clan is taken
        // over wholesale, clan and fiefs and kingdom included. A clanless hero (in practice a
        // wanderer) is instead adopted into the clan the player named during creation, so the
        // player keeps their own clan name and the banner they designed.
        var path = row.HasClan ? SwitchPath.AssumeIdentity : SwitchPath.AdoptIntoPlayerClan;

        return new SwitchPlan(
            row.HeroId,
            path,
            policy.TransferStartingGold,
            careerId ?? string.Empty);
    }
}
