using SandBox.View.Map;
using TaleWorlds.InputSystem;

namespace TAOM.Features.RealmBorders.UI;

/// <summary>
/// Reads the two realm-border keys through whatever the player has bound in Options, like
/// <c>MapInputAdapter</c>: the GameKey references are cached once (a rebind mutates or replaces the
/// key inside them, so reading the property each frame follows it), and an unbound key never reads
/// as pressed.
/// </summary>
internal sealed class RealmBordersKeys
{
    private GameKey? _toggle;
    private GameKey? _cycle;
    private bool _resolved;

    public bool TogglePressed => IsPressed(Resolve()._toggle);

    public bool CycleModePressed => IsPressed(Resolve()._cycle);

    private RealmBordersKeys Resolve()
    {
        if (_resolved)
            return this;
        _resolved = true; // latched even on a miss: registration runs at process load, before any map
        foreach (var category in HotKeyManager.GetAllCategories())
        {
            if (category.GameKeyCategoryId != TaomRealmBordersHotKeyCategory.CategoryId)
                continue;
            _toggle = category.GetGameKey(TaomRealmBordersHotKeyCategory.ToggleBordersKeyId);
            _cycle = category.GetGameKey(TaomRealmBordersHotKeyCategory.CycleMapModeKeyId);
        }
        return this;
    }

    private static bool IsPressed(GameKey? gameKey)
    {
        var key = gameKey?.KeyboardKey?.InputKey ?? InputKey.Invalid;
        return key != InputKey.Invalid && (MapScreen.Instance?.Input?.IsKeyPressed(key) ?? false);
    }
}
