using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;
using TAOM.Features.CulturalFeats;

namespace TAOM.Features.TroopProgression.Models;

public class TaomPartyWageModel : DefaultPartyWageModel
{
    private static TextObject? _cultureText;
    private static TextObject CultureText => _cultureText ??= GameTexts.FindText("str_culture");

    private readonly ITroopCostService _costService;
    private readonly ICareerPassiveService _careerPassives;

    public TaomPartyWageModel(ITroopCostService costService, ICareerPassiveService careerPassives)
    {
        _costService = costService;
        _careerPassives = careerPassives;
    }

    public override int MaxWagePaymentLimit => 20000;

    public override int GetCharacterWage(CharacterObject character)
    {
        int tier = character.Tier;
        bool isMounted = character.IsMounted;
        bool isMercenary = IsMercenaryOccupation(character.Occupation);

        return _costService.GetCharacterWage(tier, isMounted, isMercenary);
    }

    public override ExplainedNumber GetTotalWage(MobileParty mobileParty, TroopRoster troopRoster, bool includeDescriptions = false)
    {
        var result = base.GetTotalWage(mobileParty, troopRoster, includeDescriptions);

        // Garrison wage feats — only apply to actual garrison parties (matches vanilla IsGarrison gate)
        if (mobileParty.IsGarrison && mobileParty.CurrentSettlement?.Town != null
            && mobileParty.CurrentSettlement.Owner?.Culture is { } garrisonCulture)
        {
            ApplyGarrisonWageFeat(ref result, garrisonCulture, TaomCulturalFeats.EreborGarrisonWageFeat);
            ApplyGarrisonWageFeat(ref result, garrisonCulture, TaomCulturalFeats.LothlorienGarrisonWageFeat);
            ApplyGarrisonWageFeat(ref result, garrisonCulture, TaomCulturalFeats.IsengardGarrisonWageFeat);
            ApplyGarrisonWageFeat(ref result, garrisonCulture, TaomCulturalFeats.GondorGarrisonWageFeat);
        }

        // Party wage feats — apply to party owner's culture
        var partyCulture = mobileParty.Party?.Owner?.Culture;
        if (partyCulture != null)
        {
            if (partyCulture.HasFeat(TaomCulturalFeats.GundabadWageFeat))
                result.AddFactor(TaomCulturalFeats.GundabadWageFeat.EffectBonus, CultureText);

            if (partyCulture.HasFeat(TaomCulturalFeats.UmbarWageFeat))
                result.AddFactor(TaomCulturalFeats.UmbarWageFeat.EffectBonus, CultureText);

            if (partyCulture.HasFeat(TaomCulturalFeats.MordorWageFeat))
                result.AddFactor(TaomCulturalFeats.MordorWageFeat.EffectBonus, CultureText);

            // Rohan mounted wage reduction — scale by mounted wage share (matches vanilla pattern)
            // Phase 9b #173 F3 — extracted to private method to satisfy gamemodels.md rule 4 (no inline
            // foreach in GameModel overrides). Full ADR-007 extraction to a service would require an
            // IRosterAdapter; deferred to keep #173 scope bounded.
            ApplyRohanMountedWageFeat(ref result, partyCulture, troopRoster);
        }

        _careerPassives.ApplyFactor(mobileParty.LeaderHero?.StringId, ref result, PassiveEffectType.TroopWages);
        return result;
    }

    public override ExplainedNumber GetTroopRecruitmentCost(
        CharacterObject troop, Hero buyerHero, bool withoutItemCost = false)
    {
        int level = troop.Level;
        bool isMercenary = IsMercenaryOccupation(troop.Occupation);

        int baseCost = _costService.GetTroopRecruitmentCost(level, isMercenary);

        var result = new ExplainedNumber(baseCost, includeDescriptions: false);

        if (!withoutItemCost && troop.IsMounted)
        {
            int horseCost = troop.Level >= 26 ? 500 : 150;
            result.Add(horseCost, null);
        }

        // Mounted recruit cost feats
        if (troop.IsMounted && buyerHero?.Culture?.HasFeat(TaomCulturalFeats.IsengardCheaperRecruitsFeat) == true)
            result.AddFactor(TaomCulturalFeats.IsengardCheaperRecruitsFeat.EffectBonus, CultureText);

        if (troop.IsMounted && buyerHero?.Culture?.HasFeat(TaomCulturalFeats.RohanMountedCostFeat) == true)
            result.AddFactor(TaomCulturalFeats.RohanMountedCostFeat.EffectBonus, CultureText);

        return result;
    }

    private static void ApplyGarrisonWageFeat(
        ref ExplainedNumber result, CultureObject culture, FeatObject feat)
    {
        if (culture.HasFeat(feat))
            result.AddFactor(feat.EffectBonus, CultureText);
    }

    private void ApplyRohanMountedWageFeat(
        ref ExplainedNumber result, CultureObject partyCulture, TroopRoster troopRoster)
    {
        if (!partyCulture.HasFeat(TaomCulturalFeats.RohanMountedWageFeat) || troopRoster == null)
            return;

        float baseWageTotal = result.BaseNumber;
        if (baseWageTotal <= 0f)
            return;

        float mountedWageTotal = 0f;
        foreach (var element in troopRoster.GetTroopRoster())
        {
            if (element.Character?.IsMounted == true)
                mountedWageTotal += GetCharacterWage(element.Character) * element.Number;
        }
        float mountedWageShare = mountedWageTotal / baseWageTotal;
        result.AddFactor(TaomCulturalFeats.RohanMountedWageFeat.EffectBonus * mountedWageShare, CultureText);
    }

    private static bool IsMercenaryOccupation(Occupation occupation)
    {
        return occupation == Occupation.Mercenary
            || occupation == Occupation.Gangster
            || occupation == Occupation.CaravanGuard;
    }
}
