using System;
using TAOM.Adapters;
using TAOM.Core.Validation;

namespace TAOM.Features.WarChronicle.Effects;

/// <summary>
/// The extra escape roll for the captured lords of a kingdom with a <see cref="WarEffectKind.PrisonerEscape"/>
/// boost (docs/features/war-chronicle.md, Part A). Vanilla's daily roll is private and has no model, so
/// this mirrors its chance: <c>p = 0.04 x (a mobile captor in the field ? 5 - min(81, healthy)^0.25 : 1)
/// x (player-held ? 0.5 : 1) x perk factor</c> (v1.5.4 PrisonerReleaseCampaignBehavior.cs:213-245), and the
/// boost adds a second roll at <c>p x (multiplier - 1)</c>. The perk factor is vanilla's relative terms
/// (the captor's anti-escape perks, the captive's Fleet Footed, the captor leader's Valor) as one number
/// the adapter reads; at or below zero vanilla's chance is zero, and so is the extra one. It mirrors the
/// <c>CanHeroBeReleased</c> veto too. Pure: the roll is a parameter.
/// </summary>
public sealed class WarEscapeService
{
    private const float BaseChance = 0.04f;
    private const float MobileBase = 5f;
    private const double MobileExponent = 0.25d;
    private const int HealthyCap = 81;
    private const float PlayerHeldFactor = 0.5f;

    /// <summary>
    /// True when the lord is eligible for the extra roll and <paramref name="roll"/> is below the extra
    /// chance. A non-finite multiplier or roll fails closed.
    /// </summary>
    public bool ShouldEscape(PrisonerEscapeSnapshot? snapshot, float multiplier, float roll)
    {
        if (!FiniteFloatValidator.IsFinite(roll))
            return false;
        return roll < ExtraChance(snapshot, multiplier);
    }

    /// <summary>
    /// The lord could take the extra roll at all: a living prisoner who is not the main hero or of the
    /// player's clan, whose captor is not in a battle or siege, whom vanilla may release, under a finite
    /// multiplier above one.
    /// </summary>
    public bool IsEligible(PrisonerEscapeSnapshot? snapshot, float multiplier)
    {
        if (snapshot == null || !FiniteFloatValidator.IsFinite(multiplier) || !(multiplier > 1f))
            return false;
        if (!snapshot.IsAlive || !snapshot.IsPrisoner || snapshot.IsMainHero || snapshot.IsPlayerClan)
            return false;
        return !snapshot.CaptorInMapEventOrSiege && snapshot.CanBeReleased;
    }

    /// <summary>The probability of the extra roll; 0 for an ineligible lord.</summary>
    public float ExtraChance(PrisonerEscapeSnapshot? snapshot, float multiplier)
    {
        if (!IsEligible(snapshot, multiplier))
            return 0f;

        // A positive requirement, so NaN fails: anti-escape perks summing to -1 or less zero vanilla's
        // chance (ExplainedNumber has no floor), and an unset factor (0) fails closed.
        if (!(snapshot!.EscapeFactor > 0f))
            return 0f;

        var chance = BaseChance * snapshot.EscapeFactor;
        if (snapshot.CaptorIsMobile && !snapshot.CaptorInSettlement)
        {
            var healthy = Math.Max(0, Math.Min(HealthyCap, snapshot.CaptorHealthyMembers));
            chance *= MobileBase - (float)Math.Pow(healthy, MobileExponent);
        }

        if (snapshot.PlayerHeld)
            chance *= PlayerHeldFactor;

        var extra = chance * (multiplier - 1f);
        return FiniteFloatValidator.IsFinite(extra) ? extra : 0f;
    }
}
