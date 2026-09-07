using TAOM.Core.Validation;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace TAOM.Features.AiPartySize;

/// <summary>
/// Whether a PLAYER clan's lord parties collect the party size scaling, and when. Ordering matches
/// the MCM dropdown's option order, because the setting resolves by SelectedIndex.
/// </summary>
public enum PlayerClanScalingMode
{
    Never = 0,
    TakenOverOnly = 1,
    Always = 2,
}

public class AiPartySizeService : IAiPartySizeService
{
    private static readonly TextObject LordHostText = new("{=taom_ai_party_size}Warlord's host");
    private static readonly TextObject GarrisonText = new("{=taom_ai_garrison_size}Fortified realm");
    private static readonly TextObject ForageText = new("{=taom_ai_forage}Campaign foraging");
    private static readonly TextObject LevyText = new("{=taom_ai_levy}Levied service");
    private static readonly TextObject CaravanEscortText = new("{=taom_caravan_escort}Caravan escort");

    // Below this the accumulated factor frame has cancelled the value to nothing (or flipped its
    // sign) and scaling it is meaningless. Same gate, same reasoning, as
    // TroopWeightService.MinFactorScale — a positive requirement, so NaN fails it.
    private const float MinFactorScale = 0.01f;

    // Validation ceilings. Deliberately looser than the MCM sliders: the sliders are the intended
    // range, these reject a persisted settings file that has drifted outside it.
    public const float MaxFactor = 100f;
    public const float MaxFlatBonus = 100000f;
    public const float MaxRelief = 0.99f;

    // The band a lord's own abilities may move the relieved baseline within. Perks and culture feats
    // stay meaningful inside it and cannot escape it, so one relief setting means roughly one
    // outcome across every lord in the world instead of a 9x spread.
    //
    // The FLOOR must stay strictly above zero. It is what replaces the old sign-flip guard: at a
    // floor of 0 an ability set could cancel a wage bill to nothing and then invert it into a
    // rebate, which is the failure the removed skip was written to prevent. Pinned by
    // ReliefBounds_FloorIsStrictlyPositiveAndBelowTheCeiling.
    //
    // Compile-time, not MCM. Two more sliders would be two more numbers nobody tunes guarding a
    // mechanism a player never sees; the numbers come from a measured campaign distribution where
    // the ceiling was never reached and the floor bound about 12% of party-days.
    public const float MinAbilityScale = 0.65f;
    public const float MaxAbilityScale = 1.50f;

    // Shipped defaults. These are the single source: TaomSettings uses them as its property
    // initializers and the fallbacks below use them when MCM is absent, so each number exists once.
    //
    // NEUTRAL BY DEFAULT (2026-09-01). The feature ships doing nothing to party size: 1.0 applies no
    // factor and 0 adds no men, so an out-of-box game keeps vanilla limits and a player opts in
    // through MCM. This is the end of an arc that ran 10f/300f, then 5f/150f, then 2.5f/75f.
    //
    // The garrison factor is neutral for the SAME reason, and the two must be reasoned about
    // together. Vanilla vetoes a siege outright when the attacker is under 2x the defender estimate
    // (DefaultTargetScoreCalculatingModel: `if (ourStrength < num15 * num16) return 0f;` with
    // num16 = 2f for a besieger), and that estimate sums garrison AND militia. Militia is pure
    // vanilla and scales with nothing. So a raised garrison multiplier alongside vanilla-sized lord
    // parties does not make sieges hard, it makes them impossible: the settlement scores zero and is
    // never selected as a target at all. Raise one of these knobs and the other needs to move with
    // it, in roughly the same proportion.
    public const float DefaultLordFactor = 1f;
    public const float DefaultLordFlatBonus = 0f;
    public const float DefaultGarrisonFactor = 1f;

    // Caravan escort cap. Vanilla hands a caravan 20 + (10 | 20 | 30) by its notable owner's Power
    // and nothing else, because the clan-tier and Steward branch is guarded !party.IsCaravan. The
    // parity templates spawn up to 88 bodies, and CaravanPartyComponent adds one CaravanMaster on
    // top, so 89 is the roster the smallest of those three bands has to cover: 30 + 70 = 100 clears
    // it with room for the hourly recruitment vanilla already allows a caravan.
    //
    // Flat rather than a multiplier, so vanilla's 10-man steps between the Power bands survive
    // instead of being stretched. Unconditional and un-knobbed, because it is one half of a change
    // whose other half is XML: see IAiPartySizeService.ApplyCaravanScaling.
    public const float DefaultCaravanFlatBonus = 70f;

    // Relief is neutral for the same reason. It exists ONLY to close the morale-desertion path that
    // a RAISED cap opens: an over-sized party cannot buy 30 days of food anywhere and blows past its
    // clan's wage budget, and both are morale inputs that shed troops the cap alone cannot stop. At
    // vanilla party sizes neither pressure occurs, so a 0.90 relief would be pure unearned AI economy
    // rather than a fix for anything. Raise this only alongside the lord multiplier.
    public const float DefaultFoodRelief = 0f;
    public const float DefaultWageRelief = 0f;

    // Vanilla creates exactly one clan for the player and assigns Campaign.PlayerDefaultFaction to it
    // once, by this id (Campaign.cs, `CampaignObjectManager.Find<Clan>("player_faction")`). Player
    // Switcher reassigns that property to an existing lord's clan, and the property is
    // [SaveableProperty(17)], so the id is the only marker of a takeover that survives a save/load.
    // The switcher itself deliberately persists nothing. Pinned by AiPartySizeTakeoverDetectionTests.
    public const string VanillaPlayerClanId = "player_faction";

    public const PlayerClanScalingMode DefaultPlayerClanScaling = PlayerClanScalingMode.TakenOverOnly;

    public void ApplyAiLordScaling(PartyBase party, ref ExplainedNumber limit)
    {
        // Read the settings singleton ONCE. MCM's GlobalSettings<T>.Instance is not a cached field:
        // each access is a ContainsKey plus an indexer on a static dictionary, then a TryGetValue in
        // the settings provider. This method runs per party whenever a party-size limit recomputes,
        // so three separate reads were three times that cost for one unchanging answer.
        var settings = TaomSettings.Instance;

        if (!(settings?.EnableAiPartyScaling ?? true))
            return;

        var mobileParty = party?.MobileParty;
        if (mobileParty == null)
            return;

        // party.LeaderHero rather than MobileParty.LeaderHero for consistency with the shed hook,
        // which uses the same property to decide a party has a real size limit worth enforcing.
        var isLordParty = mobileParty.IsLordParty;
        var hasLeaderHero = party.LeaderHero != null;

        // Two mutually exclusive branches, deliberately kept apart. The AI predicate is unchanged and
        // still gates the food and wage relief below; only party size may reach a player clan.
        var scalable = IsPlayerClanParty(mobileParty)
            ? IsScalablePlayerLordParty(
                isPlayerClan: true, isLordParty, hasLeaderHero,
                IsTakenOverPlayerClan(CurrentPlayerClanId()),
                ResolvePlayerClanScaling(settings?.AiPlayerClanPartyScaling?.SelectedIndex))
            : IsScalableAiLordParty(
                mobileParty.IsMainParty, isLordParty, hasLeaderHero, isPlayerClan: false);

        if (!scalable)
            return;

        ApplyPartySizeScaling(
            ref limit,
            settings?.AiLordPartySizeFactor ?? DefaultLordFactor,
            settings?.AiLordPartySizeFlatBonus ?? DefaultLordFlatBonus,
            LordHostText);
    }

    public void ApplyCaravanScaling(PartyBase party, ref ExplainedNumber limit)
    {
        // Deliberately no IsEnabled() gate: see the interface doc. The template data and this
        // constant have to move together, so there is nothing here for a player to turn off.
        if (party?.MobileParty?.IsCaravan == true)
            ApplyCaravanCapBonus(ref limit);
    }

    /// <summary>
    /// The pure half of <see cref="ApplyCaravanScaling"/>, split out because <c>PartyBase</c> is
    /// sealed and cannot be constructed in a test. Engine-free; unit-tested.
    /// </summary>
    public static void ApplyCaravanCapBonus(ref ExplainedNumber limit)
        => AddResultFrameBonus(ref limit, DefaultCaravanFlatBonus, CaravanEscortText);

    public void ApplyGarrisonScaling(ref ExplainedNumber limit)
    {
        if (!IsEnabled())
            return;

        ApplyPartySizeScaling(ref limit, TaomSettings.Instance?.AiGarrisonSizeFactor ?? DefaultGarrisonFactor, 0f, GarrisonText);
    }

    public void ApplyAiFoodRelief(MobileParty party, ref ExplainedNumber consumption)
    {
        if (!IsEnabled() || party == null)
            return;

        if (!IsScalableAiLordParty(party.IsMainParty, party.IsLordParty, party.LeaderHero != null, IsPlayerClanParty(party)))
            return;

        ApplyRelief(ref consumption, TaomSettings.Instance?.AiFoodConsumptionRelief ?? DefaultFoodRelief, ForageText);
    }

    public void ApplyAiWageRelief(MobileParty party, ref ExplainedNumber wage)
    {
        if (!IsEnabled() || party == null)
            return;

        if (!IsScalableAiLordParty(party.IsMainParty, party.IsLordParty, party.LeaderHero != null, IsPlayerClanParty(party)))
            return;

        ApplyRelief(ref wage, TaomSettings.Instance?.AiWageRelief ?? DefaultWageRelief, LevyText);
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
    /// The player-clan counterpart, and deliberately a SEPARATE predicate rather than a relaxation of
    /// the one above. A player who takes over an existing lord inherits a clan whose rosters were
    /// filled at world generation against the AI-scaled limit, so without this the cap collapses under
    /// them (#530). Only party size travels across: `ApplyAiFoodRelief` and `ApplyAiWageRelief` keep
    /// calling `IsScalableAiLordParty`, so the relief stays AI-only. That split is a deliberate
    /// balance choice, NOT a claim that the player is immune to the pressures: withholding a 90%
    /// rebate on food and wages is judged too large a gift to hand a player clan.
    ///
    /// Do not restate the older, wrong justification. The AI-specific *mechanisms* do not reach a
    /// player clan (`ClanVariablesCampaignBehavior` guards `clan != Clan.PlayerClan` before setting
    /// the wage cap; `BuyFoodInternal` opens with `if (mobileParty.IsMainParty) return;`) but the
    /// OUTCOMES do. `TryBuyingFood` has no clan gate and `IsMainParty` is false for a player-clan
    /// COMPANION party, so those parties auto-buy food and starve exactly as an AI party does. On the
    /// wage side `AddPartyExpense` skips its cash-poor floor for the player clan, so the full bill is
    /// drawn from clan gold and `HasUnpaidWages` still drives the morale penalty once that runs out.
    /// A scaled player-clan party is therefore expensive to sustain by design. See #532.
    ///
    /// The main party needs no special case: it is a `LordPartyComponent` with a leader hero, so
    /// `isLordParty` and `hasLeaderHero` already admit it. Engine-free; unit-tested.
    /// </summary>
    public static bool IsScalablePlayerLordParty(
        bool isPlayerClan, bool isLordParty, bool hasLeaderHero, bool isTakenOverClan,
        PlayerClanScalingMode mode)
        => isPlayerClan && isLordParty && hasLeaderHero && ModeAllows(mode, isTakenOverClan);

    private static bool ModeAllows(PlayerClanScalingMode mode, bool isTakenOverClan)
        => mode switch
        {
            PlayerClanScalingMode.Always => true,
            PlayerClanScalingMode.TakenOverOnly => isTakenOverClan,
            _ => false,
        };

    /// <summary>
    /// Whether the player is playing an existing lord rather than a clan vanilla created for them.
    /// An absent id means "not in a campaign yet", and the safe answer there is the one that changes
    /// nothing. Engine-free; unit-tested.
    /// </summary>
    public static bool IsTakenOverPlayerClan(string playerClanStringId)
        => !string.IsNullOrEmpty(playerClanStringId) && playerClanStringId != VanillaPlayerClanId;

    /// <summary>
    /// Resolves the MCM dropdown by index, falling through to the compiled default for anything
    /// outside the known set, so a persisted settings file that has drifted cannot silently select a
    /// different branch. Same shape as `CaravanTradeSettingsProvider.ResolveWarPolicy`.
    /// </summary>
    public static PlayerClanScalingMode ResolvePlayerClanScaling(int? selectedIndex)
        => selectedIndex switch
        {
            0 => PlayerClanScalingMode.Never,
            1 => PlayerClanScalingMode.TakenOverOnly,
            2 => PlayerClanScalingMode.Always,
            _ => DefaultPlayerClanScaling,
        };

    private static string CurrentPlayerClanId()
        => Campaign.Current != null ? Clan.PlayerClan?.StringId : null;

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
    /// **The relief COMPOSES with the lord's own abilities rather than competing with them.**
    /// Everything already in the frame when this runs is a perk or a culture feat, both of which use
    /// `AddFactor`, and factors SUM rather than compose. Emitting a plain `AddFactor(-relief)` put
    /// the relief in that same sum, so the result was `1 + Sum - relief`: at a 0.9 relief the
    /// intended residual is 0.10, which handed every ability roughly 10x leverage over the final
    /// number. A +0.20 food feat tripled consumption, and a -0.15 feat drove the projected frame
    /// negative and tripped the old skip, so a food-SAVING culture received no relief at all and ate
    /// 8.5x more than an unperked lord of the same culture. Measured across a campaign, one setting
    /// produced a 9.2x spread and only about a quarter of parties got what the slider said.
    ///
    /// So: read the ability frame, clamp it into [MinAbilityScale, MaxAbilityScale], and land the
    /// frame on `(1 - relief) * clamped`. Abilities become a bounded multiplier ON the relieved
    /// baseline. The clamp floor is strictly positive, which preserves the sign-flip protection the
    /// old skip provided (a wage bill can never reach zero and invert) without the skip's side
    /// effect of silently dropping the relief for exactly the lords who least looked like they
    /// needed it. Non-finite frames return untouched, so an engine NaN stays NaN rather than coming
    /// back out as a number we invented. Engine-free; unit-tested.
    /// </summary>
    public static void ApplyRelief(ref ExplainedNumber value, float relief, TextObject? text = null)
    {
        if (!FiniteFloatValidator.IsFiniteInRange(relief, 0f, MaxRelief) || relief <= 0f)
            return;

        float abilityScale = 1f + value.SumOfFactors;
        if (!FiniteFloatValidator.IsFinite(abilityScale))
            return;

        float clamped = ClampAbilityScale(abilityScale);
        float target = (1f - relief) * clamped;

        value.AddFactor(target - abilityScale, text);
    }

    /// <summary>
    /// Written as two explicit comparisons rather than a Clamp helper so the NaN answer is visible
    /// at the call site: both comparisons are false for NaN and it would fall through unchanged,
    /// which is why <see cref="ApplyRelief"/> gates finiteness before calling this.
    /// </summary>
    private static float ClampAbilityScale(float abilityScale)
    {
        if (abilityScale < MinAbilityScale)
            return MinAbilityScale;
        if (abilityScale > MaxAbilityScale)
            return MaxAbilityScale;
        return abilityScale;
    }
}
