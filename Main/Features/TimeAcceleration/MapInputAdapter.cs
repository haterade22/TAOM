using SandBox.View.Map;
using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.TimeAcceleration;

/// <summary>
/// Reads the campaign-map time controls through whatever keys the player has bound in
/// Options > Keybindings, rather than the hardcoded Space / E / Ctrl+Space this used to assume.
/// </summary>
public class MapInputAdapter : IMapInputAdapter
{
    private GameKey _fastForward;
    private GameKey _extraFastForward;
    private GameKey _turbo;
    private GameKey _vanillaTimeToggle;
    private bool _resolved;

    public bool IsMapActive => MapScreen.Instance != null;

    public bool IsControlDown =>
        MapScreen.Instance?.Input?.IsControlDown() ?? false;

    public bool IsFastForwardPressed
    {
        get { EnsureResolved(); return IsPressed(_fastForward); }
    }

    public bool IsExtraFastForwardPressed
    {
        get { EnsureResolved(); return IsPressed(_extraFastForward); }
    }

    public bool IsTurboPressed
    {
        get { EnsureResolved(); return IsPressed(_turbo); }
    }

    public bool IsTurboReleased
    {
        get
        {
            EnsureResolved();
            // An unbound key can never produce a key-up edge, so without this an active turbo would
            // stay latched (with the engine parked at the boosted multiplier) for as long as Ctrl is
            // held after the player clears the turbo binding in Options. Reporting "released" is
            // safe: the service only consults this while turbo is already active, and the opener
            // requires IsTurboPressed, which an unbound key never raises.
            if (BoundKey(_turbo) == InputKey.Invalid) return true;
            return IsReleased(_turbo);
        }
    }

    public bool FastForwardOwnsTimeMode
    {
        get
        {
            EnsureResolved();
            var ours = BoundKey(_fastForward);
            // Unbound: the branch cannot fire anyway, so claiming ownership would be meaningless.
            if (ours == InputKey.Invalid) return false;
            // Sharing vanilla's MapTimeTogglePause key means vanilla's own handler is about to change
            // the mode for this same press. Taking it over as well would break the vanilla toggle:
            // every press would force fast-forward and none would ever toggle back.
            return ours != BoundKey(_vanillaTimeToggle);
        }
    }

    /// <summary>
    /// Caches the three GameKey objects the first time they are available. The REFERENCE is cached,
    /// never the InputKey behind it: HotKeyManager.LoadAsync applies a saved binding by MUTATING
    /// KeyboardKey (Key.ChangeKey) when one already exists and by REPLACING it when it is null, so
    /// only re-reading the property each frame observes both cases and lets a mid-session rebind
    /// take effect. GetGameKey is a linear scan over 503 slots, which is exactly why it runs once
    /// here instead of per frame.
    /// </summary>
    private void EnsureResolved()
    {
        if (_resolved) return;

        // HotKeyManager.GetCategory is a raw dictionary indexer and throws KeyNotFoundException on
        // a missing category, so scan instead. This runs exactly once per session.
        foreach (var category in HotKeyManager.GetAllCategories())
        {
            if (category.GameKeyCategoryId == MapHotKeyCategory.CategoryId)
            {
                _vanillaTimeToggle = category.GetGameKey(MapHotKeyCategory.MapTimeTogglePause);
                continue;
            }

            if (category.GameKeyCategoryId != TaomTimeControlHotKeyCategory.CategoryId) continue;

            _fastForward = category.GetGameKey(TaomTimeControlHotKeyCategory.FastForwardKeyId);
            _extraFastForward = category.GetGameKey(TaomTimeControlHotKeyCategory.ExtraFastForwardKeyId);
            _turbo = category.GetGameKey(TaomTimeControlHotKeyCategory.TurboKeyId);
        }

        if (_fastForward != null || _extraFastForward != null || _turbo != null)
        {
            _resolved = true;
            return;
        }

        // Latch even when the category is absent. Registration happens in SubModule.OnSubModuleLoad,
        // which the engine completes for every module before the first OnApplicationTick, so a miss
        // here means registration threw and the keys are never coming. Retrying would re-scan every
        // category on every property read, every frame, forever, for a result that cannot change.
        // The three fields stay null and BoundKey reports Invalid, so the keys read as unpressed.
        _resolved = true;
    }

    private static bool IsPressed(GameKey gameKey)
    {
        var key = BoundKey(gameKey);
        return key != InputKey.Invalid && (MapScreen.Instance?.Input?.IsKeyPressed(key) ?? false);
    }

    private static bool IsReleased(GameKey gameKey)
    {
        var key = BoundKey(gameKey);
        return key != InputKey.Invalid && (MapScreen.Instance?.Input?.IsKeyReleased(key) ?? false);
    }

    // KeyboardKey is left null by GameKey's ctor when the binding is Invalid, and a player can clear
    // a binding in Options, so both the key and its holder are guarded. Invalid must read as "not
    // pressed" rather than reaching IsKeyPressed.
    private static InputKey BoundKey(GameKey gameKey) =>
        gameKey?.KeyboardKey?.InputKey ?? InputKey.Invalid;
}
