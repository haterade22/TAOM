using TAOM.Core.Validation;
using TAOM.Features;

namespace TAOM.Features.SmartCavalryAI;

public sealed class SmartCavalryAISettingsProvider : ISmartCavalryAISettingsProvider
{
    // Read every frame, and per cavalry formation, by SmartCavalryAIMissionBehavior: cached on the first
    // non-null read, read through (BattleBalanceSettingsProvider pattern).
    private TaomSettings? _settings;
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public SmartCavalryAISettingsProvider() { }
    internal SmartCavalryAISettingsProvider(TaomSettings settings) => _settings = settings;

    public bool IsEnabled => Settings?.EnableSmartCavalryAI ?? false;

    public bool AvoidFriendlies => Settings?.SmartCavalryAvoidFriendlies ?? true;

    public float ChargeFormationStrictness =>
        SettingClamp.Clamp(Settings?.SmartCavalryChargeStrictness, 0.7f, 0.0f, 1.0f);

    public float ReformDistanceAfterCharge =>
        SettingClamp.Clamp(Settings?.SmartCavalryReformDistance, 25f, 10f, 80f);

    public float ChargeLineSpacing =>
        SettingClamp.Clamp(Settings?.SmartCavalryLineSpacing, 1.2f, 0.8f, 3.0f);

    public float MaxLineUpSeconds =>
        SettingClamp.Clamp(Settings?.SmartCavalryMaxLineUpSeconds, 4f, 1f, 15f);

    public bool IsDebugMode => Settings?.SmartCavalryDebug ?? false;
}
