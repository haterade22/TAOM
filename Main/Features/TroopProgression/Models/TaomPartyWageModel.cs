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
    private readonly IWageModifierService _wageModifiers;

    public TaomPartyWageModel(
        ITroopCostService costService,
        ICareerPassiveService careerPassives,
        IWageModifierService wageModifiers)
    {
        _costService = costService;
        _careerPassives = careerPassives;
        _wageModifiers = wageModifiers;
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

        var garrisonInputs = ResolveGarrisonInputs(mobileParty);
        var partyCulture = mobileParty.Party?.Owner?.Culture;
        var partyInputs = ResolvePartyInputs(partyCulture);
        float rohanMountedWageBonus = ResolveRohanMountedWageBonus(partyCulture);
        float mountedWageShare = ComputeMountedWageShare(rohanMountedWageBonus, result.BaseNumber, troopRoster);

        _wageModifiers.ApplyWageModifiers(
            ref result, garrisonInputs, partyInputs, rohanMountedWageBonus, mountedWageShare, CultureText);

        _careerPassives.ApplyFactor(mobileParty.LeaderHero?.StringId, ref result, PassiveEffectType.TroopWages);
        return result;
    }

    public override ExplainedNumber GetTroopRecruitmentCost(
        CharacterObject troop, Hero buyerHero, bool withoutItemCost = false)
    {
        var feats = ResolveMountedCostFeats(buyerHero?.Culture, troop.IsMounted);
        var buyerPerks = ResolveBuyerRecruitmentPerks(buyerHero, troop);
        int cost = _wageModifiers.CalculateRecruitmentCost(
            troop.Level,
            troop.IsMounted,
            IsMercenaryOccupation(troop.Occupation),
            withoutItemCost,
            feats,
            CultureText,
            buyerPerks);
        return new ExplainedNumber(cost, includeDescriptions: false);
    }

    private static WageFeatInputs ResolveGarrisonInputs(MobileParty mobileParty)
    {
        if (!mobileParty.IsGarrison || mobileParty.CurrentSettlement?.Town == null)
            return WageFeatInputs.None;

        var garrisonCulture = mobileParty.CurrentSettlement.Owner?.Culture;
        if (garrisonCulture == null)
            return WageFeatInputs.None;

        return new WageFeatInputs(
            isApplicable: true,
            ereborGarrisonBonus: BonusIfHas(garrisonCulture, TaomCulturalFeats.EreborGarrisonWageFeat),
            lothlorienGarrisonBonus: BonusIfHas(garrisonCulture, TaomCulturalFeats.LothlorienGarrisonWageFeat),
            isengardGarrisonBonus: BonusIfHas(garrisonCulture, TaomCulturalFeats.IsengardGarrisonWageFeat),
            gondorGarrisonBonus: BonusIfHas(garrisonCulture, TaomCulturalFeats.GondorGarrisonWageFeat));
    }

    private static WageFeatInputs ResolvePartyInputs(CultureObject? partyCulture)
    {
        if (partyCulture == null)
            return WageFeatInputs.None;

        return new WageFeatInputs(
            isApplicable: true,
            gundabadWageBonus: BonusIfHas(partyCulture, TaomCulturalFeats.GundabadWageFeat),
            umbarWageBonus: BonusIfHas(partyCulture, TaomCulturalFeats.UmbarWageFeat),
            mordorWageBonus: BonusIfHas(partyCulture, TaomCulturalFeats.MordorWageFeat));
    }

    private static float ResolveRohanMountedWageBonus(CultureObject? partyCulture)
        => BonusIfHas(partyCulture, TaomCulturalFeats.RohanMountedWageFeat);

    private float ComputeMountedWageShare(float rohanMountedWageBonus, float baseWageTotal, TroopRoster troopRoster)
    {
        if (rohanMountedWageBonus == 0f || troopRoster == null || baseWageTotal <= 0f)
            return 0f;

        float mountedWageTotal = 0f;
        foreach (var element in troopRoster.GetTroopRoster())
        {
            if (element.Character?.IsMounted == true)
                mountedWageTotal += GetCharacterWage(element.Character) * element.Number;
        }
        return mountedWageTotal / baseWageTotal;
    }

    private static MountedCostFeatInputs ResolveMountedCostFeats(CultureObject? buyerCulture, bool isMounted)
    {
        if (!isMounted || buyerCulture == null)
            return MountedCostFeatInputs.None;

        return new MountedCostFeatInputs(
            isengardMountedCostBonus: BonusIfHas(buyerCulture, TaomCulturalFeats.IsengardCheaperRecruitsFeat),
            rohanMountedCostBonus: BonusIfHas(buyerCulture, TaomCulturalFeats.RohanMountedCostFeat));
    }

    // Vanilla DefaultPartyWageModel.GetTroopRecruitmentCost applies the buyer hero's personal
    // skill-perk recruitment discounts (orthogonal to TAOM's extended cost table + cultural feats).
    // The full-replacement override dropped them; this restores them. KhuzaitRecruitUpgradeFeat is
    // intentionally NOT resolved here — TAOM replaces it with the Isengard/Rohan mounted-cost feats.
    private static RecruitmentPerkInputs ResolveBuyerRecruitmentPerks(Hero? buyerHero, CharacterObject troop)
    {
        if (buyerHero == null)
            return RecruitmentPerkInputs.None;

        return new RecruitmentPerkInputs(
            hasBuyer: true,
            tierAtLeast2: troop.Tier >= 2,
            isInfantry: troop.IsInfantry,
            isRanged: troop.IsRanged,
            isPartyLeader: buyerHero.IsPartyLeader,
            isMercenary: IsMercenaryOccupation(troop.Occupation),
            headHunterBonus: SecondaryPerkBonus(buyerHero, DefaultPerks.Throwing.HeadHunter),
            chinkInTheArmorBonus: SecondaryPerkBonus(buyerHero, DefaultPerks.OneHanded.ChinkInTheArmor),
            showOfStrengthBonus: SecondaryPerkBonus(buyerHero, DefaultPerks.TwoHanded.ShowOfStrength),
            hardyFrontlineBonus: SecondaryPerkBonus(buyerHero, DefaultPerks.Polearm.HardyFrontline),
            renownedArcherBonus: SecondaryPerkBonus(buyerHero, DefaultPerks.Bow.RenownedArcher),
            piercerBonus: SecondaryPerkBonus(buyerHero, DefaultPerks.Crossbow.Piercer),
            frugalBonus: SecondaryPerkBonus(buyerHero, DefaultPerks.Steward.Frugal),
            swordForBarterBonus: PrimaryPerkBonus(buyerHero, DefaultPerks.Trade.SwordForBarter),
            slickNegotiatorBonus: PrimaryPerkBonus(buyerHero, DefaultPerks.Charm.SlickNegotiator));
    }

    private static float SecondaryPerkBonus(Hero hero, PerkObject perk)
        => hero.GetPerkValue(perk) ? perk.SecondaryBonus : 0f;

    private static float PrimaryPerkBonus(Hero hero, PerkObject perk)
        => hero.GetPerkValue(perk) ? perk.PrimaryBonus : 0f;

    private static float BonusIfHas(CultureObject? culture, FeatObject feat)
        => culture?.HasFeat(feat) == true ? feat.EffectBonus : 0f;

    private static bool IsMercenaryOccupation(Occupation occupation)
    {
        return occupation == Occupation.Mercenary
            || occupation == Occupation.Gangster
            || occupation == Occupation.CaravanGuard;
    }
}
