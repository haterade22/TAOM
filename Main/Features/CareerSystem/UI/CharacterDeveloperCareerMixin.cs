using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.CharacterDeveloper;
using TaleWorlds.Library;
using TAOM.Core.UI;
using TAOM.Core.Logging;

namespace TAOM.Features.CareerSystem.UI;

/// <summary>
/// Injects career-related data binding properties into CharacterDeveloperVM
/// using direct Harmony patches and ViewModelMixinSupport instead of UIExtenderEx.
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory("Patch27_CareerSystem")]
internal static class CharacterDeveloperCareerMixin
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(CharacterDeveloperVM), MethodType.Constructor,
        new[] { typeof(Action) })]
    private static void Postfix_Constructor(CharacterDeveloperVM __instance)
    {
        var mixin = ViewModelMixinSupport.GetOrCreate(__instance, () => new CareerMixinData(__instance));
        mixin.RefreshCareerState();
        ViewModelMixinSupport.InjectBindings(__instance, mixin, typeof(CareerMixinData));
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CharacterDeveloperVM), nameof(CharacterDeveloperVM.RefreshValues))]
    private static void Postfix_RefreshValues(CharacterDeveloperVM __instance)
    {
        if (ViewModelMixinSupport.TryGet<CareerMixinData>(__instance, out var mixin))
            mixin.RefreshCareerState();
    }
}

/// <summary>
/// Holds mixin state for a single CharacterDeveloperVM instance.
/// Properties marked with [DataSourceProperty] are injected into the VM's Gauntlet bindings.
/// </summary>
internal sealed class CareerMixinData
{
    private readonly WeakReference<CharacterDeveloperVM> _vmRef;
    private bool _hasCareer;

    public CareerMixinData(CharacterDeveloperVM vm)
    {
        _vmRef = new WeakReference<CharacterDeveloperVM>(vm);
    }

    internal void RefreshCareerState()
    {
        var hero = Hero.MainHero;
        if (hero == null)
        {
            HasCareer = false;
            return;
        }

        var dataService = IoC.Resolve<ICareerDataService>();
        HasCareer = dataService?.HasCareer(hero.StringId) ?? false;
        IoC.Resolve<IModLogger>()?.LogDebug($"CareerSystem: RefreshCareerState — hero='{hero.StringId}' HasCareer={HasCareer}");
    }

    [DataSourceProperty]
    public bool HasCareer
    {
        get => _hasCareer;
        set
        {
            if (_hasCareer != value)
            {
                _hasCareer = value;
                if (_vmRef.TryGetTarget(out var vm))
                    vm.OnPropertyChanged(nameof(HasCareer));
            }
        }
    }

    public void ExecuteOpenCareerScreen()
    {
        IoC.Resolve<IModLogger>()?.LogInfo("CareerSystem: ExecuteOpenCareerScreen triggered from character developer");
        GauntletCareerScreen.OpenCareerScreen();
    }
}
