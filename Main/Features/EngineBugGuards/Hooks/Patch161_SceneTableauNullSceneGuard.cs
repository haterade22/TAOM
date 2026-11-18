using HarmonyLib;
using TAOM.Core.Logging;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade.View.Tableaus;

namespace TAOM.Features.EngineBugGuards.Hooks;

/// <summary>
/// Patch161 (#1429; ported from Silas He's v1.5.6 fix package, his crash a0dfdff3): a 3D scene widget that is sized
/// before it has a scene closes the game.
///
/// v1.5.7 <c>SceneTableau.SetTargetSize</c> (SceneTableau.cs:64-83) always ends in <c>CreateTexture()</c>.
/// <c>CreateTexture</c> (:53-62) allocates a render target and a scene view, then passes <c>_tableauScene</c> to
/// <c>SceneView.SetScene</c>, which reads <c>scene.Pointer</c> and throws a managed NullReferenceException
/// (SceneView.cs:20-23). Silas saw it when the wedding notification opened. When the scene is null this prefix skips
/// the texture. The skipped tableau is a throwaway: when the scene arrives, <c>SceneTextureProvider</c> builds a new
/// tableau for it, and the widget's next size update makes the texture on that one. <c>OnTick</c> (:134-143) returns
/// early while <c>View</c> is null, so a skipped texture leaves nothing for the tick to read.
///
/// Callers of <c>CreateTexture</c> (v1.5.7): <c>SetTargetSize</c> and <c>SetScene</c> only; <c>SetScene</c> calls it
/// with a scene it has just stored, so the guard can only stop the <c>SetTargetSize</c> path.
/// </summary>
[HarmonyPatch(typeof(SceneTableau), "CreateTexture")]
[HarmonyPatchCategory(Category)]
[ClientOnly]
public static class Patch161_SceneTableauNullSceneGuard
{
    internal const string Category = "Patch161_SceneTableauNullSceneGuard";

    private static IModLogger? _logger;
    // Per process: Initialize runs once, in OnSubModuleLoad, so the warning is written once per game process.
    private static bool _warned;

    internal static void Initialize(IModLogger? logger)
    {
        _logger = logger;
        _warned = false;
    }

    internal static bool ShouldCreateTexture(bool hasScene) => hasScene;

    // FOUR underscores: Harmony strips three, and the engine field is "_tableauScene". A reference test: Scene is a
    // NativeObject with its own == overload, and the plain reference test needs no engine type initializer.
    [HarmonyPrefix]
    public static bool Prefix(Scene ____tableauScene)
    {
        if (ShouldCreateTexture(!ReferenceEquals(____tableauScene, null)))
            return true;

        if (!_warned)
        {
            _warned = true;
            _logger?.LogWarning("[EngineBugGuards] a scene widget was sized before it had a scene; its texture waits for the scene (Patch161)");
        }
        return false;
    }
}
