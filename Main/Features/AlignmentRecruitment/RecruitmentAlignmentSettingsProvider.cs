using TAOM.Features;

namespace TAOM.Features.AlignmentRecruitment;

/// <summary>
/// Merges MCM live values (<c>TaomSettings.Instance</c>) over JSON defaults. Mirrors
/// <c>CastleRecruitmentSettingsProvider</c>. <c>TaomSettings.Instance</c> can be null very early in
/// startup or if MCM fails to load, so the <c>?? default</c> fallback keeps every read safe.
/// </summary>
public sealed class RecruitmentAlignmentSettingsProvider : IRecruitmentAlignmentSettingsProvider
{
    private readonly RecruitmentAlignmentConfig _defaults;

    // Read per foreign candidate per AI lord think (Patch114) and per notable per party-hour
    // (TaomVolunteerModel). Resolving TaomSettings.Instance walks MCM's settings containers, so the
    // reference is cached on its first non-null read and read THROUGH, never snapshotted: MCM edits its
    // one registered instance in place (reset and presets copy values into it), so live MCM edits still
    // apply. Lazy, not in the constructor, so a resolve before MCM is up cannot pin the fallbacks. Same
    // contract as CastleRecruitmentSettingsProvider.
    private TaomSettings? _settings;
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public RecruitmentAlignmentSettingsProvider(IRecruitmentAlignmentConfigProvider configProvider)
    {
        _defaults = configProvider.GetConfig();
    }

    public bool IsEnabled => Settings?.EnableAlignmentRecruitment ?? _defaults.Enabled;

    public bool ApplyToAi => Settings?.EnableAlignmentRecruitmentAi ?? _defaults.ApplyToAi;

    public bool ApplyToPlayer => Settings?.EnableAlignmentRecruitmentPlayer ?? _defaults.ApplyToPlayer;

    public bool GoodRejectsEvilOnly =>
        Settings?.AlignmentRecruitmentGoodRejectsEvilOnly ?? _defaults.GoodRejectsEvilOnly;
}
