using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;

namespace TAOM.Features.TroopProgression;

/// <summary>
/// Pure decision functions for party-wage and troop-recruitment cost modifiers,
/// extracted from <c>TaomPartyWageModel</c> per gamemodels.md rule 4 (thin override
/// body). The service operates on primitives only — the model is responsible for
/// translating sealed TaleWorlds types (CultureObject, FeatObject, CharacterObject)
/// at the boundary and resolving <c>HasFeat</c> + <c>EffectBonus</c> into the
/// supplied structs.
/// </summary>
public interface IWageModifierService
{
    /// <summary>
    /// Apply garrison-wage feats (when the party is an actual garrison — resolved from the
    /// fief owner's culture) and party-wage feats (resolved from the party's culture via the
    /// leader-first <c>CultureFeatAdapter.ResolvePartyCulture</c> chokepoint, including the Rohan
    /// mounted-wage share) to <paramref name="result"/>.
    /// </summary>
    void ApplyWageModifiers(
        ref ExplainedNumber result,
        in WageFeatInputs garrison,
        in WageFeatInputs party,
        float rohanMountedWageBonus,
        float mountedWageShare,
        TextObject? cultureText);

    /// <summary>
    /// Compute the full troop-recruitment cost including horse-cost (when mounted +
    /// !withoutItemCost), the Isengard/Rohan mounted-cost feats, and the buyer hero's
    /// vanilla recruitment-cost perk discounts. Returns the final <c>ResultNumber</c> as int.
    /// </summary>
    int CalculateRecruitmentCost(
        int level,
        bool isMounted,
        bool isMercenary,
        bool withoutItemCost,
        in MountedCostFeatInputs mountedCostFeats,
        TextObject? cultureText,
        in RecruitmentPerkInputs buyerPerks = default);

    /// <summary>Horse-cost lookup: 500 for level &gt;= 26, else 150.</summary>
    int CalculateHorseCost(int troopLevel);
}

/// <summary>
/// Pre-resolved feat-bonus inputs for a single AddFactor pass. Each pair is
/// (apply, bonus): when <c>apply</c> is true the bonus is applied with AddFactor.
/// The model resolves <c>CultureObject.HasFeat(feat) ? feat.EffectBonus : 0f</c>
/// into these primitives at the boundary, keeping the service free of TaleWorlds
/// sealed types per ADR-007.
/// </summary>
public readonly struct WageFeatInputs
{
    public readonly bool IsApplicable;
    public readonly float EreborGarrisonBonus;
    public readonly float LothlorienGarrisonBonus;
    public readonly float IsengardGarrisonBonus;
    public readonly float GondorGarrisonBonus;
    public readonly float GundabadWageBonus;
    public readonly float UmbarWageBonus;
    public readonly float MordorWageBonus;

    public WageFeatInputs(
        bool isApplicable,
        float ereborGarrisonBonus = 0f,
        float lothlorienGarrisonBonus = 0f,
        float isengardGarrisonBonus = 0f,
        float gondorGarrisonBonus = 0f,
        float gundabadWageBonus = 0f,
        float umbarWageBonus = 0f,
        float mordorWageBonus = 0f)
    {
        IsApplicable = isApplicable;
        EreborGarrisonBonus = ereborGarrisonBonus;
        LothlorienGarrisonBonus = lothlorienGarrisonBonus;
        IsengardGarrisonBonus = isengardGarrisonBonus;
        GondorGarrisonBonus = gondorGarrisonBonus;
        GundabadWageBonus = gundabadWageBonus;
        UmbarWageBonus = umbarWageBonus;
        MordorWageBonus = mordorWageBonus;
    }

    public static WageFeatInputs None => new WageFeatInputs(isApplicable: false);
}

/// <summary>
/// Pre-resolved mounted-recruit-cost feat bonuses. Zero means the feat does not apply
/// (either the buyer's culture lacks the feat, or the troop is not mounted).
/// </summary>
public readonly struct MountedCostFeatInputs
{
    public readonly float IsengardMountedCostBonus;
    public readonly float RohanMountedCostBonus;

    public MountedCostFeatInputs(float isengardMountedCostBonus, float rohanMountedCostBonus)
    {
        IsengardMountedCostBonus = isengardMountedCostBonus;
        RohanMountedCostBonus = rohanMountedCostBonus;
    }

    public static MountedCostFeatInputs None => new MountedCostFeatInputs(0f, 0f);
}

/// <summary>
/// Pre-resolved buyer-hero recruitment-cost perk discounts, mirroring the
/// <c>if (buyerHero != null)</c> block in vanilla
/// <c>DefaultPartyWageModel.GetTroopRecruitmentCost</c>. Each bonus float is resolved at the
/// boundary as <c>buyerHero.GetPerkValue(perk) ? perk.SecondaryBonus : 0f</c> (PrimaryBonus for
/// the two mercenary trade perks). The service owns the troop-type / tier / leader / mercenary
/// gating so the decision logic stays unit-testable, and keeps no TaleWorlds sealed types per
/// ADR-007. Vanilla's <c>KhuzaitRecruitUpgradeFeat</c> is deliberately omitted — TAOM replaces
/// the mounted-recruit cultural feat with the Isengard/Rohan mounted-cost feats (see
/// <see cref="MountedCostFeatInputs"/>). Bonuses are signed (negative = discount); when the buyer
/// lacks a perk the corresponding bonus is 0 and contributes nothing.
/// </summary>
public readonly struct RecruitmentPerkInputs
{
    public readonly bool HasBuyer;
    public readonly bool TierAtLeast2;
    public readonly bool IsInfantry;
    public readonly bool IsRanged;
    public readonly bool IsPartyLeader;
    public readonly bool IsMercenary;
    public readonly float HeadHunterBonus;
    public readonly float ChinkInTheArmorBonus;
    public readonly float ShowOfStrengthBonus;
    public readonly float HardyFrontlineBonus;
    public readonly float RenownedArcherBonus;
    public readonly float PiercerBonus;
    public readonly float FrugalBonus;
    public readonly float SwordForBarterBonus;
    public readonly float SlickNegotiatorBonus;

    public RecruitmentPerkInputs(
        bool hasBuyer,
        bool tierAtLeast2 = false,
        bool isInfantry = false,
        bool isRanged = false,
        bool isPartyLeader = false,
        bool isMercenary = false,
        float headHunterBonus = 0f,
        float chinkInTheArmorBonus = 0f,
        float showOfStrengthBonus = 0f,
        float hardyFrontlineBonus = 0f,
        float renownedArcherBonus = 0f,
        float piercerBonus = 0f,
        float frugalBonus = 0f,
        float swordForBarterBonus = 0f,
        float slickNegotiatorBonus = 0f)
    {
        HasBuyer = hasBuyer;
        TierAtLeast2 = tierAtLeast2;
        IsInfantry = isInfantry;
        IsRanged = isRanged;
        IsPartyLeader = isPartyLeader;
        IsMercenary = isMercenary;
        HeadHunterBonus = headHunterBonus;
        ChinkInTheArmorBonus = chinkInTheArmorBonus;
        ShowOfStrengthBonus = showOfStrengthBonus;
        HardyFrontlineBonus = hardyFrontlineBonus;
        RenownedArcherBonus = renownedArcherBonus;
        PiercerBonus = piercerBonus;
        FrugalBonus = frugalBonus;
        SwordForBarterBonus = swordForBarterBonus;
        SlickNegotiatorBonus = slickNegotiatorBonus;
    }

    public static RecruitmentPerkInputs None => new RecruitmentPerkInputs(hasBuyer: false);
}
