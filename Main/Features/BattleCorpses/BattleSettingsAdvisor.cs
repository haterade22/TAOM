using System;
using TAOM.Adapters;

namespace TAOM.Features.BattleCorpses;

/// <summary>
/// Recommends lower ragdoll and corpse options (#701). Players reported mid-battle freezes that
/// stopped when they lowered both. The engine has no per-battle ragdoll override, so the player's
/// saved option is the only lever, and TAOM pulls it only when the player says so. A value is only
/// ever lowered, never raised.
/// </summary>
public sealed class BattleSettingsAdvisor
{
    /// <summary>Option 3 is 5 simultaneous ragdolls, one below 10 (Mike, 2026-10-01).</summary>
    public const int RecommendedRagdollOption = 3;

    /// <summary>Option 1 is 25 corpses, the lowest non-zero (Mike, 2026-10-01).</summary>
    public const int RecommendedCorpseOption = 1;

    private const int MaxOption = 5;

    private readonly IGraphicsOptionsAdapter _options;
    private readonly IBattleCorpseSettingsProvider _settings;

    public BattleSettingsAdvisor(IGraphicsOptionsAdapter options, IBattleCorpseSettingsProvider settings)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <summary>True when either option is above the recommendation. An unreadable option never nags.</summary>
    public static bool IsAboveRecommended(int ragdollOption, int corpseOption) =>
        IsAbove(ragdollOption, RecommendedRagdollOption) || IsAbove(corpseOption, RecommendedCorpseOption);

    public bool ShouldOffer() =>
        _settings.IsAdviceEnabled && IsAboveRecommended(_options.RagdollOption, _options.CorpseOption);

    /// <summary>
    /// Lowers each option to the recommendation if above it (an unreadable one gets the recommendation),
    /// saves both, and says whether the save worked.
    /// </summary>
    public bool ApplyRecommended() =>
        _options.SaveOptions(
            Lowered(_options.RagdollOption, RecommendedRagdollOption),
            Lowered(_options.CorpseOption, RecommendedCorpseOption));

    private static int Lowered(int current, int recommended) =>
        IsKnown(current) ? Math.Min(current, recommended) : recommended;

    private static bool IsAbove(int option, int recommended) => IsKnown(option) && option > recommended;

    private static bool IsKnown(int option) => option >= 0 && option <= MaxOption;
}
