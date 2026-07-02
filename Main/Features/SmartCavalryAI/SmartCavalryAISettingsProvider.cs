using TAOM.Core.Validation;
using TAOM.Features;

namespace TAOM.Features.SmartCavalryAI;

public sealed class SmartCavalryAISettingsProvider : ISmartCavalryAISettingsProvider
{
    public bool IsEnabled => TaomSettings.Instance?.EnableSmartCavalryAI ?? false;

    public bool AvoidFriendlies => TaomSettings.Instance?.SmartCavalryAvoidFriendlies ?? true;

    public float ChargeFormationStrictness =>
        SettingClamp.Clamp(TaomSettings.Instance?.SmartCavalryChargeStrictness, 0.7f, 0.0f, 1.0f);

    public float ReformDistanceAfterCharge =>
        SettingClamp.Clamp(TaomSettings.Instance?.SmartCavalryReformDistance, 25f, 10f, 80f);

    public float ChargeLineSpacing =>
        SettingClamp.Clamp(TaomSettings.Instance?.SmartCavalryLineSpacing, 1.2f, 0.8f, 3.0f);

    public bool IsDebugMode => TaomSettings.Instance?.SmartCavalryDebug ?? false;
}
