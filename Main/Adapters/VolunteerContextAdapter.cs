using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;
using TAOM.Features.TroopProgression;

namespace TAOM.Adapters;

// Focused adapter for volunteer recruitment context extraction.
// Hero.CurrentSettlement can be null (hero not at settlement).
// Settlement.Village is null for towns/castles.
// Settlement.OwnerClan is nullable (hideouts, unowned).
// Source: Decompiled TaleWorlds.CampaignSystem - Hero, Settlement, Village classes
public class VolunteerContextAdapter : IVolunteerContextAdapter
{
    public VolunteerContext GetContext(Hero hero)
    {
        var settlement = hero.CurrentSettlement;
        if (settlement == null)
            return new VolunteerContext(null, null, null, hero.Culture?.StringId);

        string settlementId = settlement.StringId;

        // Village.Bound gives the parent town/castle for village settlements
        // Settlement.Village is null for towns and castles
        string boundSettlementId = settlement.Village?.Bound?.StringId;

        // OwnerClan is nullable; for villages it delegates through Village.Bound.OwnerClan
        string ownerClanId = settlement.OwnerClan?.StringId;

        string cultureId = hero.Culture?.StringId;

        return new VolunteerContext(settlementId, boundSettlementId, ownerClanId, cultureId);
    }

    public CharacterObject ResolveCharacter(string characterId)
    {
        return MBObjectManager.Instance?.GetObject<CharacterObject>(characterId);
    }
}
