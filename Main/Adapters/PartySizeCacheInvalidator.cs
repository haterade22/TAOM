using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace TAOM.Adapters;

/// <summary>
/// Each bump raises the roster VersionNo and, through TroopRoster.UpdateVersion, MobileParty.VersionNo,
/// so every cache keyed on either counter recomputes lazily on its next read: PartySizeLimit and
/// PartySizeRatio, plus the horse, tier and strength counts, the roster element list, the carried
/// weight and the base speed. Vanilla calls UpdateVersion after a perk change and after a building
/// alters garrison capacity.
/// </summary>
public class PartySizeCacheInvalidator : IPartySizeCacheInvalidator
{
    /// <summary>Garrisons are MobileParty too, so the garrison multiplier is covered by the same sweep.</summary>
    public void InvalidateAll()
    {
        if (Campaign.Current == null) return;

        foreach (var party in MobileParty.All)
            party?.MemberRoster?.UpdateVersion();
    }

    public void InvalidateLedBy(ICollection<string> heroIds)
    {
        if (Campaign.Current == null || heroIds == null || heroIds.Count == 0) return;

        foreach (var party in MobileParty.All)
        {
            var leaderId = party?.LeaderHero?.StringId;
            if (leaderId == null || !heroIds.Contains(leaderId)) continue;

            party.MemberRoster?.UpdateVersion();
            // An army's speed is the leader party's cached speed, so a member's change must refresh it too.
            party.Army?.LeaderParty?.MemberRoster?.UpdateVersion();
        }
    }
}
