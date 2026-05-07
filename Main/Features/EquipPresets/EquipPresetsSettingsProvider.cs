namespace TAOM.Features.EquipPresets;

public sealed class EquipPresetsSettingsProvider : IEquipPresetsSettingsProvider
{
    public const int DefaultMaxPresets = 10;
    public const int MinMaxPresets = 1;
    public const int MaxMaxPresets = 20;

    public bool IsEnabled => TaomSettings.Instance?.EnableEquipmentPresets ?? true;

    /// <summary>
    /// Range-clamped per the "Config Providers MUST Validate" rule
    /// (<c>.claude/rules/csharp-architecture.md</c>). MCM enforces [1,20] via
    /// <c>SettingPropertyInteger</c>, but a stale save or a manual JSON edit could ship a
    /// value outside that range — the provider clamps + falls back to default rather than
    /// trusting the input blindly.
    /// </summary>
    public int MaxPresetsPerCharacter
    {
        get
        {
            var raw = TaomSettings.Instance?.MaxPresetsPerCharacter ?? DefaultMaxPresets;
            if (raw < MinMaxPresets || raw > MaxMaxPresets) return DefaultMaxPresets;
            return raw;
        }
    }

    public bool IsDebugMode => TaomSettings.Instance?.EquipPresetsDebug ?? false;
}
