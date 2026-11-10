using TAOM.Core.Validation;

namespace TAOM.Features.SpecialResources;

/// <summary>
/// The two MCM knobs behind gold mode (#1109). <c>TaomSettings.Instance</c> can be null very early in
/// startup or if MCM fails to load, so every read falls back to resource mode at the default rate.
/// The instance is cached on its first non-null read and read THROUGH, never snapshotted: MCM edits
/// its one registered instance in place, so a live toggle applies on the next read (same contract as
/// <c>CastleRecruitmentSettingsProvider</c>).
/// </summary>
public sealed class SpecialResourceGoldSettings
{
    public const int DefaultGoldPerPoint = 100;

    private TaomSettings? _settings;
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public SpecialResourceGoldSettings()
    {
    }

    internal SpecialResourceGoldSettings(TaomSettings settings) => _settings = settings;

    public bool UseSpecialResources => Settings?.UseSpecialResources ?? true;

    public int GoldPerResourcePoint =>
        SettingClamp.Clamp(Settings?.GoldPerResourcePoint, DefaultGoldPerPoint, 1, 1000);
}
