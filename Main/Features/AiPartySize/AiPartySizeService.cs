using TAOM.Core.Validation;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace TAOM.Features.AiPartySize;

public class AiPartySizeService : IAiPartySizeService
{
    private static readonly TextObject LordHostText = new("{=taom_ai_party_size}Warlord's host");
    private static readonly TextObject GarrisonText = new("{=taom_ai_garrison_size}Fortified realm");
    private static readonly TextObject ForageText = new("{=taom_ai_forage}Campaign foraging");
    private static readonly TextObject LevyText = new("{=taom_ai_levy}Levied service");

    // Below this the accumulated factor frame has cancelled the value to nothing (or flipped its
    // sign) and scaling it is meaningless. Same gate, same reasoning, as
    // TroopWeightService.MinFactorScale — a positive requirement, so NaN fails it.
    private const float MinFactorScale = 0.01f;

    // Validation ceilings. Deliberately looser than the MCM sliders: the sliders are the intended
    // range, these reject a persisted settings file that has drifted outside it.
    public const float MaxFactor = 100f;
    public const float MaxFlatBonus = 100000f;
    public const float MaxRelief = 0.99f;

    // Shipped defaults for the two lord knobs. These are the single source: TaomSettings uses them
    // as its property initializers and the fallbacks below use them when MCM is absent. Before this
    // each number was written twice and nothing would have caught the two drifting apart, because
    // every unit test passes the values in as literal arguments.
    // Halved 2026-09-01 from 10f/300f. Both had to move together: the flat bonus is deliberately
    // kept out of the factor frame by AddResultFrameBonus, so halving the multiplier alone lands at
    // 60% of the old limit rather than 50%. A tier-4 lord goes from 1560 to 780.
    public const float DefaultLordFactor = 5f;
    public const float DefaultLordFlatBonus = 150f;

    public void ApplyAiLordScaling(PartyBase party, ref ExplainedNumber limit)
    {
        if (!IsEnabled())
            return;

        var mobileParty = party?.MobileParty;
        if (mobileParty == null)
            return;

        // party.LeaderHero rather than MobileParty.LeaderHero for consistency with the shed hook,
        // which uses the same property to decide a party has a real size limit worth enforcing.
        if (!IsScalableAiLordParty(
                mobileParty.IsMainParty, mobileParty.IsLordParty, party.LeaderHero != null, IsPlayerClanParty(mobileParty)))
            return;

        var settings = TaomSettings.Instance;
        ApplyPartySizeScaling(
            ref limit,
            settings?.AiLordPartySizeFactor ?? DefaultLordFactor,
            settings?.AiLordPartySizeFlatBonus ?? DefaultLordFlatBonus,
            LordHostText);
    }

    public void ApplyGarrisonScaling(ref ExplainedNumber limit)
    {
        if (!IsEnabled())
            return;

        ApplyPartySizeScaling(ref limit, TaomSettings.Instance?.AiGarrisonSizeFactor ?? 3f, 0f, GarrisonText);
    }

    public void ApplyAiFoodRelief(MobileParty party, ref ExplainedNumber consumption)
    {
        if (!IsEnabled() || party == null)
            return;

        if (!IsScalableAiLordParty(party.IsMainParty, party.IsLordParty, party.LeaderHero != null, IsPlayerClanParty(party)))
            return;

        ApplyRelief(ref consumption, TaomSettings.Instance?.AiFoodConsumptionRelief ?? 0.9f, ForageText);
    }

    public void ApplyAiWageRelief(MobileParty party, ref ExplainedNumber wage)
    {
        if (!IsEnabled() || party == null)
            return;

        if (!IsScalableAiLordParty(party.IsMainParty, party.IsLordParty, party.LeaderHero != null, IsPlayerClanParty(party)))
            return;

        ApplyRelief(ref wage, TaomSettings.Instance?.AiWageRelief ?? 0.9f, LevyText);
    }

    private static bool IsEnabled() => TaomSettings.Instance?.EnableAiPartyScaling ?? true;

    /// <summary>
    /// Which parties this feature may touch. Non-lord parties (caravans, villagers, militia,
    /// patrols, garrisons) are excluded because vanilla sizes each of them from a different branch
    /// entirely, and a lord party with no leader has no meaningful cap to scale.
    ///
    /// **Both player exclusions are required, and the second is easy to miss.** Excluding the main
    /// party is not enough: a party the player raises for a companion is a `LordPartyComponent`
    /// (vanilla's own `LordPartyComponent` branches on `owner.Clan == Clan.PlayerClan`), so it is
    /// `IsLordParty` and is NOT `IsMainParty`. Without the clan test it would collect the full AI
    /// treatment, including the food and wage relief, which is a large and unintended economic gift.
    /// Engine-free; unit-tested.
    /// </summary>
    public static bool IsScalableAiLordParty(
        bool isMainParty, bool isLordParty, bool hasLeaderHero, bool isPlayerClan)
        => !isMainParty && !isPlayerClan && isLordParty && hasLeaderHero;

    /// <summary>
    /// Whether this party belongs to the player's clan. `ActualClan` is a plain field read and is
    /// assigned from the component owner on creation. `Clan.PlayerClan` dereferences
    /// <c>Campaign.Current</c>, so it is guarded: outside a campaign there is no player clan to
    /// match and the party is treated as AI, which is the pre-existing behaviour.
    /// </summary>
    private static bool IsPlayerClanParty(MobileParty mobileParty)
        => Campaign.Current != null
           && mobileParty?.ActualClan != null
           && mobileParty.ActualClan == Clan.PlayerClan;

    /// <summary>
    /// Multiplies the limit by <paramref name="factor"/> and then adds <paramref name="flatBonus"/>
    /// as a RESULT-frame bonus, so the result is <c>base * factor + flatBonus</c>.
    ///
    /// The two knobs answer different halves of the mismatch. The factor preserves clan-tier
    /// progression (a tier-4 lord still outgrows a tier-1). The flat bonus exists because template
    /// spawn is tier-INDEPENDENT: every lord of a culture draws from the same stacks, so under a
    /// pure factor the low-tier lords still shed while the high-tier ones sit under their cap.
    ///
    /// Both are validated rather than clamped: an out-of-range or non-finite knob is ignored, which
    /// leaves vanilla behaviour, instead of being silently coerced into a plausible-looking value.
    /// Engine-free; unit-tested.
    /// </summary>
    public static void ApplyPartySizeScaling(
        ref ExplainedNumber limit, float factor, float flatBonus, TextObject? text = null)
    {
        if (FiniteFloatValidator.IsFiniteInRange(factor, 1f, MaxFactor) && factor > 1f)
            limit.AddFactor(factor - 1f, text);

        AddResultFrameBonus(ref limit, flatBonus, text);
    }

    /// <summary>
    /// Adds an absolute body count to <paramref name="value"/>'s RESULT frame.
    /// <see cref="ExplainedNumber.Add"/> mutates <c>BaseNumber</c> and the result is
    /// <c>BaseNumber * (1 + SumOfFactors)</c>, so a raw <c>Add(bonus)</c> would be amplified by
    /// every factor in play — including this feature's own multiplier, making a "+300" knob mean
    /// +3000 at factor 10. Dividing the factor back out makes the bonus cost exactly
    /// <paramref name="bonus"/> slots. Same idiom as TroopWeightService.SubtractResultFramePenalty.
    /// Engine-free; unit-tested.
    /// </summary>
    public static void AddResultFrameBonus(ref ExplainedNumber value, float bonus, TextObject? text = null)
    {
        if (!FiniteFloatValidator.IsFiniteInRange(bonus, 0f, MaxFlatBonus) || bonus <= 0f)
            return;

        float scale = 1f + value.SumOfFactors;
        if (!(scale > MinFactorScale))
            return;

        value.Add(bonus / scale, text);
    }

    /// <summary>
    /// Reduces <paramref name="value"/> by <paramref name="relief"/> as a fraction (0.9 = pay or eat
    /// a tenth). Works for both wages (positive) and food consumption (negative), because a factor
    /// scales magnitude and preserves sign.
    ///
    /// The gate is on the PROJECTED factor frame, not the incoming one: other factors are already in
    /// play (culture wage feats, food feats) and factors SUM rather than compose, so a relief applied
    /// on top of an existing negative factor could drive the frame to zero or flip its sign, turning
    /// a wage bill into a wage rebate. Written as a positive requirement so NaN fails it.
    /// Engine-free; unit-tested.
    /// </summary>
    public static void ApplyRelief(ref ExplainedNumber value, float relief, TextObject? text = null)
    {
        if (!FiniteFloatValidator.IsFiniteInRange(relief, 0f, MaxRelief) || relief <= 0f)
            return;

        float projectedScale = 1f + value.SumOfFactors - relief;
        if (!(projectedScale > MinFactorScale))
            return;

        value.AddFactor(-relief, text);
    }
}
