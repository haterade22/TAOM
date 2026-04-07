using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem;

public static class CareerPassiveHelper
{
    private static ICareerPassiveService _cachedService;
    private static TextObject _careerText;
    private static TextObject CareerText => _careerText ?? (_careerText = new TextObject("{=taom_career}Career"));

    private static ICareerPassiveService GetService()
    {
        return _cachedService ?? (_cachedService = IoC.Resolve<ICareerPassiveService>());
    }

    public static void ApplyFactor(Hero hero, ref ExplainedNumber result, PassiveEffectType type)
    {
        if (hero == null) return;

        var passiveService = GetService();
        if (passiveService == null) return;

        var magnitude = passiveService.GetPassiveMagnitude(hero.StringId, type);
        if (magnitude != 0f)
            result.AddFactor(magnitude, CareerText);
    }

    public static void ApplyFlat(Hero hero, ref ExplainedNumber result, PassiveEffectType type)
    {
        if (hero == null) return;

        var passiveService = GetService();
        if (passiveService == null) return;

        var magnitude = passiveService.GetPassiveMagnitude(hero.StringId, type);
        if (magnitude != 0f)
            result.Add(magnitude, CareerText);
    }
}
