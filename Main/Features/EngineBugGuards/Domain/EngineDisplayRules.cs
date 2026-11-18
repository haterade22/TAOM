using System;

namespace TAOM.Features.EngineBugGuards.Domain;

/// <summary>
/// Pure decisions for the #1444 display fixes (ported from Silas He's v1.5.6 fix package). No TaleWorlds types, so
/// each rule is tested without the engine; the patches in <c>Hooks/</c> only read and write the engine values.
/// </summary>
public static class EngineDisplayRules
{
    /// <summary>The highest tier native draws a badge for (v1.5.7 CampaignUIHelper.GetCharacterTierData :1380).</summary>
    public const int NativeMaxBadgeTier = 7;

    /// <summary>TAOM's own tier ceiling (TaomCharacterStatsModel.MaxCharacterTier).</summary>
    public const int TaomMaxTier = 10;

    private const string TierIconPrefix = @"General\TroopTierIcons\icon_tier_";

    /// <summary>
    /// The badge sprite for a tier native leaves blank: native's tier 7 glyph, the highest it ships. Null for a tier
    /// native draws itself (1 to 7) or never draws (0 or less).
    /// </summary>
    public static string? EliteTierIcon(int tier, bool isBig)
    {
        if (tier <= NativeMaxBadgeTier)
            return null;
        return TierIconPrefix + NativeMaxBadgeTier + (isBig ? "_big" : "");
    }

    /// <summary>
    /// Native's tier formula (DefaultCharacterStatsModel.GetTier :18-25) with TAOM's ceiling, for custom battle, whose
    /// copy of it clamps to 7 (CustomBattleTroopTypeVM.GetCharacterTier :223-229). A hero has no tier.
    /// </summary>
    public static int TroopTier(int level, bool isHero)
    {
        if (isHero)
            return 0;
        int tier = (int)Math.Ceiling((level - 5f) / 5f);
        return Math.Min(Math.Max(tier, 0), TaomMaxTier);
    }

    /// <summary>
    /// Whether a kingdom being created should take the name the player chose as its formal name, instead of native's
    /// "Kingdom of {founder clan}" (KingdomManager.CreateKingdom :127-131). Only the player's own founding, and only
    /// when the caller gave no formal name; a rebel clan's kingdom keeps native's naming.
    /// </summary>
    public static bool ShouldUseChosenFormalName(bool founderIsPlayer, bool formalNameGiven, bool rebel)
        => founderIsPlayer && !formalNameGiven && !rebel;

    /// <summary>
    /// The kit a dead hero keeps: the one it wore before <c>Hero.OnDeath</c> emptied it, unless something set a kit
    /// after death, which wins.
    /// </summary>
    public static T? KitAfterDeath<T>(T? beforeDeath, T? afterDeath) where T : class
        => afterDeath ?? beforeDeath;
}
