using System;
using System.Collections.Generic;
using TAOM.Core.Validation;

namespace TAOM.Features.TournamentRewards;

/// <summary>
/// The tournament reward arithmetic (docs/features/tournament-rewards.md, Mike 2026-10-02). Pure: every engine
/// value arrives as a number, and an invalid factor falls back to vanilla's own answer.
/// </summary>
public static class TournamentRewardRules
{
    /// <summary>Vanilla's cap per round (TournamentBehavior.MaximumBet); Deep Pockets doubles it.</summary>
    public const int VanillaMaxBet = 150;

    /// <summary>
    /// "Unlimited" in practice. A won bet pays odds of up to 4 and the four rounds' payouts add up in an int
    /// (TournamentBehavior.OverallExpectedDenars), so a round cap above about 134 million overflows it; this
    /// keeps a 2.7x margin and is far beyond any campaign purse.
    /// </summary>
    public const int BetCeiling = 50_000_000;

    /// <summary>
    /// The largest renown or influence multiplier. TaomSettings' two multiplier sliders run from 0 to this, the
    /// settings provider clamps to it, and the rules refuse a larger one, so the three cannot drift apart.
    /// </summary>
    public const float MaxMultiplier = 5f;

    /// <summary>The largest bet cap the MCM slider offers; the settings provider clamps to it.</summary>
    public const int MaxBetSetting = 1_000_000;

    public const int XpPerRoundWon = 125;
    public const int XpForTheWin = 250;
    public const int Rounds = 4;
    public const int InfluenceBase = 2;
    public const int HeroesPerInfluencePoint = 4;

    /// <summary>The eight combat skills a player may train at a tournament, as engine SkillObject ids.</summary>
    public static readonly IReadOnlyList<string> CombatSkillIds = new[]
    {
        "OneHanded", "TwoHanded", "Polearm", "Bow", "Crossbow", "Throwing", "Riding", "Athletics",
    };

    /// <summary>
    /// The cap per round. A cap of 0 or less is unlimited (the safety ceiling); a finite cap keeps vanilla's
    /// perk factor (Deep Pockets: vanilla answers 300 instead of 150).
    /// </summary>
    public static int MaximumBet(int vanillaResult, int configuredCap)
    {
        if (configuredCap <= 0)
            return BetCeiling;
        var perkFactor = Math.Max(1L, vanillaResult / (long)VanillaMaxBet);
        return (int)Math.Min(BetCeiling, configuredCap * perkFactor);
    }

    /// <summary>
    /// (vanilla renown + one per hero) x culture x multiplier; vanilla's answer keeps its perks. A multiplier
    /// above <see cref="MaxMultiplier"/>, or a result past what an int holds, gives vanilla's answer.
    /// </summary>
    public static int Renown(int vanillaRenown, int heroCount, float cultureFactor, float multiplier)
    {
        if (!IsFactor(cultureFactor) || !IsMultiplier(multiplier))
            return vanillaRenown;
        var raw = (vanillaRenown + Math.Max(0, heroCount)) * (double)cultureFactor * multiplier;
        return RoundToInt(raw, vanillaRenown);
    }

    /// <summary>
    /// In a town of the winner's own kingdom: (vanilla + 2 + one per four heroes) x culture x multiplier, in whole
    /// points. Vanilla's answer anywhere else, and for a multiplier above <see cref="MaxMultiplier"/> or a result
    /// past what an int holds.
    /// </summary>
    public static int Influence(int vanillaInfluence, int heroCount, bool ownKingdomTown, float cultureFactor, float multiplier)
    {
        if (!ownKingdomTown || !IsFactor(cultureFactor) || !IsMultiplier(multiplier))
            return vanillaInfluence;
        var raw = (float)vanillaInfluence + InfluenceBase + Math.Max(0, heroCount) / HeroesPerInfluencePoint;
        return RoundToInt(raw * cultureFactor * multiplier, vanillaInfluence);
    }

    /// <summary>
    /// 125 per round won plus 250 for the win, times the player's culture factor (none when invalid, and none when
    /// the product is past what an int holds).
    /// </summary>
    public static int SkillXp(int roundsWon, bool wonTournament, float cultureFactor)
    {
        var rounds = Math.Max(0, Math.Min(Rounds, roundsWon));
        var raw = rounds * XpPerRoundWon + (wonTournament ? XpForTheWin : 0);
        var factor = IsFactor(cultureFactor) ? cultureFactor : 1f;
        return RoundToInt(raw * (double)factor, raw);
    }

    private static bool IsFactor(float value) => FiniteFloatValidator.IsFiniteAtLeast(value, 0f);

    private static bool IsMultiplier(float value) => FiniteFloatValidator.IsFiniteInRange(value, 0f, MaxMultiplier);

    /// <summary>
    /// Rounds half away from zero. A value past what an int holds gives <paramref name="fallback"/>, because the
    /// unchecked cast of one is int.MinValue on net472 x64 (csharp-architecture.md, float-to-int casts); a
    /// positive requirement, so NaN fails it too.
    /// </summary>
    private static int RoundToInt(double value, int fallback)
    {
        var rounded = Math.Round(value, MidpointRounding.AwayFromZero);
        return rounded >= int.MinValue && rounded <= int.MaxValue ? (int)rounded : fallback;
    }
}
