using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Party;

namespace TAOM.Features.SpecialResources.Hooks;

/// <summary>
/// The upgrades a party screen still holds, one entry per target troop and count. A pop-up Cancel restores the
/// screen as saved before the pop-up, upgrades included, so the session queues these again (#1492).
/// </summary>
public static class PartyScreenHeldUpgrades
{
    public static IReadOnlyList<RecruitCartEntry> Read(PartyScreenLogic logic)
    {
        var history = logic?.CurrentData?.UpgradedTroopsHistory;
        var entries = new List<RecruitCartEntry>(history?.Count ?? 0);
        if (history == null) return entries;
        foreach (var upgrade in history)
        {
            if (upgrade?.Item2 != null) entries.Add(new RecruitCartEntry(upgrade.Item2.StringId, upgrade.Item3));
        }
        return entries;
    }
}
