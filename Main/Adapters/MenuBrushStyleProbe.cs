using TaleWorlds.Engine.GauntletUI;

namespace TAOM.Adapters;

/// <summary>
/// Reads the live <c>GameMenu.InfoText</c> brush — the one the game-menu body widget renders with
/// (<c>GUI/Prefabs/GameMenu/GameMenu.xml</c>, <c>Brush="GameMenu.InfoText"</c>).
///
/// Uses <c>GetStyle</c>, never <c>GetStyleOrDefault</c>: the latter is exactly the silent fallback
/// this probe exists to detect.
/// </summary>
public sealed class MenuBrushStyleProbe : IMenuBrushStyleProbe
{
    private const string MenuBrushName = "GameMenu.InfoText";

    public bool DefinesStyle(string styleName)
    {
        if (string.IsNullOrEmpty(styleName)) return false;

        try
        {
            return UIResourceManager.BrushFactory?.GetBrush(MenuBrushName)?.GetStyle(styleName) != null;
        }
        catch
        {
            // UI resources not loaded yet, or the brush is absent entirely. Either way the caller
            // must fall back to vanilla styling rather than emit a name nothing can resolve.
            return false;
        }
    }
}
