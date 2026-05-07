namespace TAOM.Features.EquipPresets;

public interface IEquipPresetsSettingsProvider
{
    bool IsEnabled { get; }
    int MaxPresetsPerCharacter { get; }
    bool IsDebugMode { get; }
}
