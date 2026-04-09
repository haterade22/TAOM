using System.Reflection;
using TaleWorlds.MountAndBlade;

namespace TAOM.Dependencies;

/// <summary>
/// Pre-Native module that applies UIExtenderEx system patches before any game UI loads.
/// The 5 system patches (UIConfig, ViewModel, WidgetPrefab, BrushFactory, WidgetFactory)
/// fire in UIExtender's static constructor when the type is first referenced.
/// TAOM's own SubModule calls UIExtender.Create/Register/Enable after this module loads.
/// </summary>
public class SubModule : MBSubModuleBase
{
    static SubModule()
    {
        // Force GauntletUI assembly load before UIExtenderEx static ctor fires
        Assembly.Load("TaleWorlds.Engine.GauntletUI");
    }

    protected override void OnSubModuleLoad()
    {
        base.OnSubModuleLoad();

        // Touching the UIExtender type triggers its static constructor, which applies:
        // 1. UIConfigPatch — blocks DoNotUseGeneratedPrefabs from being set to true
        // 2. ViewModelPatch — patches ViewModel ctor + ExecuteCommand
        // 3. WidgetPrefabPatch — transpiles WidgetPrefab.LoadFrom for XML injection
        // 4. BrushFactoryManager — patches GetBrush/Brushes for custom brushes
        // 5. WidgetFactoryManager — patches CreateBuiltinWidget/GetCustomType for custom widgets
        _ = typeof(Bannerlord.UIExtenderEx.UIExtender);
    }
}
