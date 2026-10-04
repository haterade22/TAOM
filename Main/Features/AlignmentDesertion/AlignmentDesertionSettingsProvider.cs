using TAOM.Features;

namespace TAOM.Features.AlignmentDesertion;

/// <summary>
/// Merges MCM live values (<c>TaomSettings.Instance</c>) over JSON defaults. Mirrors
/// <c>RecruitmentAlignmentSettingsProvider</c>. <c>TaomSettings.Instance</c> can be null very early
/// in startup or if MCM fails to load — the <c>?? default</c> fallback keeps every read safe.
/// </summary>
public sealed class AlignmentDesertionSettingsProvider : IAlignmentDesertionSettingsProvider
{
    private readonly AlignmentDesertionConfig _defaults;

    // Read on a campaign hot path (per party, per score, per day or every map frame). Resolving
    // TaomSettings.Instance walks MCM's settings containers, so the reference is cached on its first
    // non-null read and read THROUGH, never snapshotted: MCM edits its one registered instance in place
    // (reset and presets copy values into it), so live MCM edits still apply. Lazy, not in the
    // constructor, so a resolve before MCM is up cannot pin the fallbacks. Same contract as
    // BattleBalanceSettingsProvider.
    private TaomSettings? _settings;
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public AlignmentDesertionSettingsProvider(IAlignmentDesertionConfigProvider configProvider)
    {
        _defaults = configProvider.GetConfig();
    }

    internal AlignmentDesertionSettingsProvider(IAlignmentDesertionConfigProvider configProvider, TaomSettings settings)
        : this(configProvider) => _settings = settings;

    public bool IsEnabled => Settings?.EnableAlignmentDesertion ?? _defaults.Enabled;

    public float Rate => Settings?.AlignmentDesertionRate ?? _defaults.Rate;

    public bool ApplyToAi => Settings?.EnableAlignmentDesertionAi ?? _defaults.ApplyToAi;

    public bool ApplyToPlayer => Settings?.EnableAlignmentDesertionPlayer ?? _defaults.ApplyToPlayer;

    public bool ApplyToParties => Settings?.EnableAlignmentDesertionParties ?? _defaults.ApplyToParties;

    public bool ApplyToGarrisons => Settings?.EnableAlignmentDesertionGarrisons ?? _defaults.ApplyToGarrisons;
}
