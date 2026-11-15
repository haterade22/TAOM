using SandBox.View.Map;
using TaleWorlds.InputSystem;

namespace TAOM.Features.CheatMenu.UI;

/// <summary>
/// Reads the cheat menu key through whatever the player has bound in Options, like <c>RealmBordersKeys</c>: the GameKey
/// reference is cached once (a rebind mutates or replaces the key inside it, so reading the property each frame follows it),
/// and an unbound key never reads as pressed. A press with Ctrl or Alt held does not count, so Ctrl or Alt combinations
/// on the same key stay with whatever else binds them.
/// </summary>
internal sealed class CheatMenuKeys
{
    private GameKey? _open;
    private bool _resolved;

    public bool OpenPressed
    {
        get
        {
            var input = MapScreen.Instance?.Input;
            var key = Resolve()._open?.KeyboardKey?.InputKey ?? InputKey.Invalid;
            if (input == null || key == InputKey.Invalid || !input.IsKeyPressed(key))
                return false;
            return !input.IsKeyDown(InputKey.LeftControl) && !input.IsKeyDown(InputKey.RightControl)
                && !input.IsKeyDown(InputKey.LeftAlt) && !input.IsKeyDown(InputKey.RightAlt);
        }
    }

    private CheatMenuKeys Resolve()
    {
        if (_resolved)
            return this;
        _resolved = true; // latched even on a miss: registration runs at process load, before any map
        foreach (var category in HotKeyManager.GetAllCategories())
        {
            if (category.GameKeyCategoryId == TaomCheatMenuHotKeyCategory.CategoryId)
                _open = category.GetGameKey(TaomCheatMenuHotKeyCategory.OpenCheatMenuKeyId);
        }
        return this;
    }
}
