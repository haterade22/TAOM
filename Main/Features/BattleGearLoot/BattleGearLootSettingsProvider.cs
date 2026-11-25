using TAOM.Core.Validation;

namespace TAOM.Features.BattleGearLoot;

public sealed class BattleGearLootSettingsProvider : IBattleGearLootSettingsProvider
{
    public const bool DefaultEnabled = false;
    public const int DefaultChancePercent = 10;
    public const int MinChancePercent = 0;
    public const int MaxChancePercent = 100;

    private TaomSettings? _settings;

    public BattleGearLootSettingsProvider()
    {
    }

    internal BattleGearLootSettingsProvider(TaomSettings settings) => _settings = settings;

    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public BattleGearLootSettings Get() => From(Settings);

    internal static BattleGearLootSettings From(TaomSettings? s) => new(
        s?.EnableBattleGearLoot ?? DefaultEnabled,
        SettingClamp.Clamp(s?.BattleGearLootChancePercent, DefaultChancePercent, MinChancePercent, MaxChancePercent));
}
