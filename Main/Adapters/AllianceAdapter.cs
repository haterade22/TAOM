using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace TAOM.Adapters;

// MakePeaceAction is in TaleWorlds.CampaignSystem.Actions — same namespace as
// DeclareWarAction. No extra using needed.

public class AllianceAdapter : IAllianceAdapter
{
    public IReadOnlyList<string> GetAllKingdomIds()
    {
        return Kingdom.All
            .Where(k => !k.IsEliminated)
            .Select(k => k.StringId)
            .ToList();
    }

    public bool AreAllied(string kingdomAId, string kingdomBId)
    {
        var kingdomA = FindKingdom(kingdomAId);
        var kingdomB = FindKingdom(kingdomBId);
        if (kingdomA == null || kingdomB == null) return false;

        return kingdomA.IsAllyWith(kingdomB);
    }

    public void StartAlliance(string kingdomAId, string kingdomBId)
    {
        var kingdomA = FindKingdom(kingdomAId);
        var kingdomB = FindKingdom(kingdomBId);
        if (kingdomA == null || kingdomB == null) return;

        var allianceBehavior = Campaign.Current?.GetCampaignBehavior<IAllianceCampaignBehavior>();
        allianceBehavior?.StartAlliance(kingdomA, kingdomB);
    }

    public bool AreAtWar(string kingdomAId, string kingdomBId)
    {
        var kingdomA = FindKingdom(kingdomAId);
        var kingdomB = FindKingdom(kingdomBId);
        if (kingdomA == null || kingdomB == null) return false;

        return kingdomA.IsAtWarWith(kingdomB);
    }

    public void DeclareWar(string kingdomAId, string kingdomBId)
    {
        var kingdomA = FindKingdom(kingdomAId);
        var kingdomB = FindKingdom(kingdomBId);
        if (kingdomA == null || kingdomB == null) return;

        if (!kingdomA.IsAtWarWith(kingdomB))
        {
            DeclareWarAction.ApplyByDefault(kingdomA, kingdomB);
        }
    }

    public void MakePeace(string kingdomAId, string kingdomBId)
    {
        var kingdomA = FindKingdom(kingdomAId);
        var kingdomB = FindKingdom(kingdomBId);
        if (kingdomA == null || kingdomB == null) return;

        if (kingdomA.IsAtWarWith(kingdomB))
        {
            MakePeaceAction.Apply(kingdomA, kingdomB);
        }
    }

    private static Kingdom FindKingdom(string stringId)
    {
        return Kingdom.All.FirstOrDefault(k => k.StringId == stringId);
    }
}
