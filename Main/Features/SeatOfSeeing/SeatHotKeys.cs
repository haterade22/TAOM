using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.SeatOfSeeing;

/// <summary>
/// The Seat's keys as the player has bound them in Options > Keybindings > Action. It holds the <see cref="GameKey"/> REFERENCE and
/// re-reads the <see cref="InputKey"/> behind it on every call, because <c>HotKeyManager.LoadAsync</c> applies a saved binding by
/// mutating <c>KeyboardKey</c> when one exists and by replacing it when it is null, so only a fresh read sees both. An unbound or
/// unregistered key reads <see cref="InputKey.Invalid"/>, which the view treats as not pressed. The scan runs once, on the first
/// read, and latches a miss: registration happens in OnSubModuleLoad, before any mission, so a miss means it threw.
/// </summary>
internal sealed class SeatHotKeys
{
    private GameKey? _takeTheSeat;
    private bool _resolved;

    /// <summary>The key bound to "Take the Seat", or <see cref="InputKey.Invalid"/>.</summary>
    public InputKey TakeTheSeat
    {
        get
        {
            Resolve();
            return _takeTheSeat?.KeyboardKey?.InputKey ?? InputKey.Invalid;
        }
    }

    private void Resolve()
    {
        if (_resolved)
            return;

        // GetCategory is a raw dictionary indexer that throws on a missing id, so scan.
        foreach (var category in HotKeyManager.GetAllCategories())
        {
            if (category.GameKeyCategoryId != TaomSeatHotKeyCategory.CategoryId)
                continue;
            _takeTheSeat = category.GetGameKey(TaomSeatHotKeyCategory.TakeTheSeatKeyId);
            break;
        }

        _resolved = true;
    }
}
