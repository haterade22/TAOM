using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem;

public static class CareerPassiveHelper
{
    private static ICareerPassiveService _cachedService;
    private static IModLogger _cachedLogger;
    private static bool _enableDebugLog;
    private static TextObject _careerText;
    private static TextObject CareerText => _careerText ?? (_careerText = new TextObject("{=taom_career}Career"));

    /// <summary>
    /// Set to true to enable per-call debug logging in hot path. Off by default.
    /// </summary>
    public static bool EnableDebugLog
    {
        get => _enableDebugLog;
        set => _enableDebugLog = value;
    }

    private static ICareerPassiveService GetService()
    {
        return _cachedService ?? (_cachedService = IoC.Resolve<ICareerPassiveService>());
    }

    private static IModLogger GetLogger()
    {
        return _cachedLogger ?? (_cachedLogger = IoC.Resolve<IModLogger>());
    }

    public static void ApplyFactor(Hero hero, ref ExplainedNumber result, PassiveEffectType type)
    {
        if (hero == null) return;

        var passiveService = GetService();
        if (passiveService == null) return;

        var magnitude = passiveService.GetPassiveMagnitude(hero.StringId, type);
        if (magnitude != 0f)
        {
            result.AddFactor(magnitude, CareerText);
            if (_enableDebugLog)
                GetLogger()?.LogDebug($"CareerSystem: ApplyFactor hero='{hero.StringId}' type={type} magnitude={magnitude}");
        }
    }

    public static void ApplyFlat(Hero hero, ref ExplainedNumber result, PassiveEffectType type)
    {
        if (hero == null) return;

        var passiveService = GetService();
        if (passiveService == null) return;

        var magnitude = passiveService.GetPassiveMagnitude(hero.StringId, type);
        if (magnitude != 0f)
        {
            result.Add(magnitude, CareerText);
            if (_enableDebugLog)
                GetLogger()?.LogDebug($"CareerSystem: ApplyFlat hero='{hero.StringId}' type={type} magnitude={magnitude}");
        }
    }
}
