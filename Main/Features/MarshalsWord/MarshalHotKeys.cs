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
    private GameKey? _mark;
    private GameKey? _keepWidths;
    private GameKey? _fireMode;
    private GameKey? _word;
    private GameKey? _holdLine;
    private bool _resolved;

    /// <summary>The key bound to "Give the order in turn", or <see cref="InputKey.Invalid"/>.</summary>
    public InputKey Queue => Read(ref _queue);

    /// <summary>The key bound to "Mark, do not move", or <see cref="InputKey.Invalid"/>.</summary>
    public InputKey Mark => Read(ref _mark);

    /// <summary>The key bound to "Keep the widths", or <see cref="InputKey.Invalid"/>.</summary>
    public InputKey KeepWidths => Read(ref _keepWidths);

    /// <summary>The key bound to "Change the archers' fire", or <see cref="InputKey.Invalid"/>.</summary>
    public InputKey FireMode => Read(ref _fireMode);

    /// <summary>The key bound to "Give the word", or <see cref="InputKey.Invalid"/>.</summary>
    public InputKey Word => Read(ref _word);

    /// <summary>The key bound to "Hold the line", or <see cref="InputKey.Invalid"/>.</summary>
    public InputKey HoldLine => Read(ref _holdLine);

    private InputKey Read(ref GameKey? key)
    {
        Resolve();
        return key?.KeyboardKey?.InputKey ?? InputKey.Invalid;
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
            _mark = category.GetGameKey(TaomMarshalHotKeyCategory.MarkKeyId);
            _keepWidths = category.GetGameKey(TaomMarshalHotKeyCategory.KeepWidthsKeyId);
            _fireMode = category.GetGameKey(TaomMarshalHotKeyCategory.FireModeKeyId);
            _word = category.GetGameKey(TaomMarshalHotKeyCategory.WordKeyId);
            _holdLine = category.GetGameKey(TaomMarshalHotKeyCategory.HoldLineKeyId);
            break;
        }

        _resolved = true;
    }
}
