using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapBar;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TAOM.Core.UI;

namespace TAOM.Features.TimeAcceleration.UI;

/// <summary>
/// Holds per-instance mixin state for MapTimeControlVM.
/// Properties marked [DataSourceProperty] are injected into Gauntlet's
/// binding dictionary by ViewModelMixinSupport.InjectBindings().
/// </summary>
internal sealed class TimeAccelerationMixinData
{
    private bool _isExtraFastForwardActive;
    private BasicTooltipViewModel _extraFastForwardHint;
    private readonly WeakReference<MapTimeControlVM> _vmRef;

    public TimeAccelerationMixinData(MapTimeControlVM vm)
    {
        _vmRef = new WeakReference<MapTimeControlVM>(vm);
        _extraFastForwardHint = new BasicTooltipViewModel(() => "Extra Fast Forward (E)");
    }

    [DataSourceProperty]
    public bool IsExtraFastForwardActive
    {
        get => _isExtraFastForwardActive;
        set
        {
            if (_isExtraFastForwardActive != value)
            {
                _isExtraFastForwardActive = value;
                if (_vmRef.TryGetTarget(out var vm))
                    vm.OnPropertyChanged(nameof(IsExtraFastForwardActive));
            }
        }
    }

    [DataSourceProperty]
    public BasicTooltipViewModel ExtraFastForwardHint
    {
        get => _extraFastForwardHint;
        set
        {
            if (_extraFastForwardHint != value)
            {
                _extraFastForwardHint = value;
                if (_vmRef.TryGetTarget(out var vm))
                    vm.OnPropertyChanged(nameof(ExtraFastForwardHint));
            }
        }
    }

    public void OnRefresh()
    {
        if (Campaign.Current == null)
            return;

        IsExtraFastForwardActive = Campaign.Current.SpeedUpMultiplier > 4f;
    }
}

/// <summary>
/// Harmony patches that replace the UIExtenderEx ViewModelMixin for MapTimeControlVM.
/// Constructor postfix injects mixin bindings; RefreshValues postfix drives OnRefresh().
/// Applied under category "Patch29_TimeAcceleration" in SubModule.OnGameInitializationFinished.
/// </summary>
[HarmonyPatchCategory("Patch29_TimeAcceleration")]
internal static class TimeAccelerationMixinPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(MapTimeControlVM), MethodType.Constructor)]
    static void CtorPostfix(MapTimeControlVM __instance)
    {
        var data = ViewModelMixinSupport.GetOrCreate(
            __instance,
            () => new TimeAccelerationMixinData(__instance));

        ViewModelMixinSupport.InjectBindings(__instance, data, typeof(TimeAccelerationMixinData));
        data.OnRefresh();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MapTimeControlVM), nameof(MapTimeControlVM.RefreshValues))]
    static void RefreshValuesPostfix(MapTimeControlVM __instance)
    {
        if (ViewModelMixinSupport.TryGet<TimeAccelerationMixinData>(__instance, out var data))
            data.OnRefresh();
    }
}
