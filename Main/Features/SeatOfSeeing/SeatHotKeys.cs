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
    private GameKey? _keepTo;
    private GameKey? _hideTheInterface;
    private GameKey? _takeThisSoldier;
    private GameKey? _quickenTime;
    private GameKey? _slowTime;
    private GameKey? _stayTime;
    private bool _resolved;

    /// <summary>The key bound to "Take the Seat", or <see cref="InputKey.Invalid"/>.</summary>
    public InputKey TakeTheSeat => Bound(ref _takeTheSeat);

    /// <summary>The key bound to "Keep to this soldier or company", or <see cref="InputKey.Invalid"/>.</summary>
    public InputKey KeepTo => Bound(ref _keepTo);

    /// <summary>The key bound to "Hide the interface", or <see cref="InputKey.Invalid"/>.</summary>
    public InputKey HideTheInterface => Bound(ref _hideTheInterface);

    /// <summary>The key bound to "Take this soldier", or <see cref="InputKey.Invalid"/>.</summary>
    public InputKey TakeThisSoldier => Bound(ref _takeThisSoldier);

    /// <summary>The key bound to "Quicken time", or <see cref="InputKey.Invalid"/>.</summary>
    public InputKey QuickenTime => Bound(ref _quickenTime);

    /// <summary>The key bound to "Slow time", or <see cref="InputKey.Invalid"/>.</summary>
    public InputKey SlowTime => Bound(ref _slowTime);

    /// <summary>The key bound to "Stay time", or <see cref="InputKey.Invalid"/>.</summary>
    public InputKey StayTime => Bound(ref _stayTime);

    private InputKey Bound(ref GameKey? key)
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
            if (category.GameKeyCategoryId != TaomSeatHotKeyCategory.CategoryId)
                continue;
            _takeTheSeat = category.GetGameKey(TaomSeatHotKeyCategory.TakeTheSeatKeyId);
            _keepTo = category.GetGameKey(TaomSeatHotKeyCategory.KeepToKeyId);
            _hideTheInterface = category.GetGameKey(TaomSeatHotKeyCategory.HideTheInterfaceKeyId);
            _takeThisSoldier = category.GetGameKey(TaomSeatHotKeyCategory.TakeThisSoldierKeyId);
            _quickenTime = category.GetGameKey(TaomSeatHotKeyCategory.QuickenTimeKeyId);
            _slowTime = category.GetGameKey(TaomSeatHotKeyCategory.SlowTimeKeyId);
            _stayTime = category.GetGameKey(TaomSeatHotKeyCategory.StayTimeKeyId);
            break;
        }

        _resolved = true;
    }
}
