using HarmonyLib;
using TaleWorlds.Engine.GauntletUI;

namespace TAOM.Features.FactionMap.Hooks;

public class CultureStageViewTickHook : IOnCultureStageViewTick
{
    public void OnTick(object viewInstance, float dt)
    {
        var vm = CultureStageViewCreatedHook.CurrentVM;
        if (vm == null) return;

        vm.Tick();

        var layerField = AccessTools.Field(viewInstance.GetType(), "GauntletLayer");
        var gauntletLayer = layerField?.GetValue(viewInstance) as GauntletLayer;
        if (gauntletLayer == null) return;

        if (gauntletLayer.Input.IsHotKeyReleased("Confirm") && vm.CanConfirm)
            vm.ExecuteConfirm();
    }
}
