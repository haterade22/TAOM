using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;
using TAOM.Features.CultureConversion;
using TAOM.Features.TroopProgression;

namespace TAOM.Adapters;

// Focused adapter for volunteer recruitment context extraction.
// Hero.CurrentSettlement can be null (hero not at settlement).
// Settlement.Village is null for towns/castles.
// Settlement.OwnerClan is nullable (hideouts, unowned).
// Source: Decompiled TaleWorlds.CampaignSystem - Hero, Settlement, Village classes
public class VolunteerContextAdapter : IVolunteerContextAdapter
{
    private readonly ICultureConversionStore _conversionStore;

    public VolunteerContextAdapter(ICultureConversionStore conversionStore)
    {
        _conversionStore = conversionStore;
    }

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

        // Owner culture is read live (no caching) so kingdom flips take effect for the next volunteer pick.
        string ownerCultureId = settlement.OwnerClan?.Culture?.StringId;

        // CultureConversion: the settlement's CURRENT culture (reflects any applied override) + whether
        // this settlement — or, for villages, its bound parent — has been converted. When converted,
        // recruitment resolves troops from the converted culture's pool instead of the stale settlement pool.
        string settlementCultureId = settlement.Culture?.StringId;
        bool isConverted = _conversionStore.IsConverted(settlementId)
                           || (boundSettlementId != null && _conversionStore.IsConverted(boundSettlementId));

        return new VolunteerContext(
            settlementId, boundSettlementId, ownerClanId, cultureId, ownerCultureId, settlementCultureId, isConverted);
    }

    public CharacterObject ResolveCharacter(string characterId)
    {
        return MBObjectManager.Instance?.GetObject<CharacterObject>(characterId);
    }
}
