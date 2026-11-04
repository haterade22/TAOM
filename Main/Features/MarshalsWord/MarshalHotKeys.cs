using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.MarshalsWord;

/// <summary>
/// Marshal's Word's keys as the player has bound them in Options > Keybindings > Action. It holds the <see cref="GameKey"/> REFERENCES and
/// re-reads the <see cref="InputKey"/> behind each on every call, because <c>HotKeyManager.LoadAsync</c> applies a saved binding by
/// mutating <c>KeyboardKey</c> when one exists and by replacing it when it is null, so only a fresh read sees both. An unbound or
/// unregistered key reads <see cref="InputKey.Invalid"/>, which the view treats as not down. The scan runs once, on the first read.
/// </summary>
internal sealed class MarshalHotKeys
{
    private GameKey? _queue;
    private GameKey? _keepWidths;
    private bool _resolved;

    /// <summary>The key bound to "Give the order in turn", or <see cref="InputKey.Invalid"/>.</summary>
    public InputKey Queue
    {
        get
        {
            Resolve();
            return _queue?.KeyboardKey?.InputKey ?? InputKey.Invalid;
        }
    }

    /// <summary>The key bound to "Keep the widths", or <see cref="InputKey.Invalid"/>.</summary>
    public InputKey KeepWidths
    {
        get
        {
            Resolve();
            return _keepWidths?.KeyboardKey?.InputKey ?? InputKey.Invalid;
        }
    }

    private void Resolve()
    {
        if (_resolved)
            return;

        // GetCategory is a raw dictionary indexer that throws on a missing id, so scan.
        foreach (var category in HotKeyManager.GetAllCategories())
        {
            if (category.GameKeyCategoryId != TaomMarshalHotKeyCategory.CategoryId)
                continue;
            _queue = category.GetGameKey(TaomMarshalHotKeyCategory.QueueKeyId);
            _keepWidths = category.GetGameKey(TaomMarshalHotKeyCategory.KeepWidthsKeyId);
            break;
        }

        _resolved = true;
    }
}
