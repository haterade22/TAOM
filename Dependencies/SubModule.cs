using System.Reflection;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.MountAndBlade;

namespace TAOM.Dependencies;

/// <summary>
/// Pre-Native module that applies UIExtenderEx system patches before any game UI loads.
/// Must load before Native so patches are in place before any prefabs/brushes are loaded.
/// </summary>
public class SubModule : MBSubModuleBase
{
    static SubModule()
    {
        // Force GauntletUI assembly load before UIExtenderEx static ctor fires
        Assembly.Load("TaleWorlds.Engine.GauntletUI");

        // Force XML prefab parsing — without this, the game uses pre-generated prefabs
        // that don't contain TAOM's custom brushes/sprites. Must be set BEFORE any
        // prefab is loaded (i.e., before Native). The UIConfigPatch Harmony prefix
        // then blocks anything from setting it back to false.
        UIConfig.DoNotUseGeneratedPrefabs = true;
    }

    protected override void OnSubModuleLoad()
    {
        base.OnSubModuleLoad();

        // Touching the UIExtender type triggers its static constructor, which applies:
        // 1. UIConfigPatch — blocks DoNotUseGeneratedPrefabs setter (keeps it true)
        // 2. ViewModelPatch — patches ViewModel ctor + ExecuteCommand
        // 3. WidgetPrefabPatch — transpiles WidgetPrefab.LoadFrom for XML injection
        // 4. BrushFactoryManager — patches GetBrush/Brushes for custom brushes
        // 5. WidgetFactoryManager — patches CreateBuiltinWidget/GetCustomType for custom widgets
        _ = typeof(Bannerlord.UIExtenderEx.UIExtender);
    }
}
