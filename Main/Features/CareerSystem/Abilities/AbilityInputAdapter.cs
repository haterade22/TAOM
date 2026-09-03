using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CareerSystem.Abilities;

/// <summary>
/// Reads the career ability activation through whatever key the player has bound in
/// Options > Keybindings > Action, rather than the hardcoded V this used to assume. Still the single
/// source for both the poll and the key-chip label (Issue #382), so a rebind moves both together.
/// </summary>
public class AbilityInputAdapter : IAbilityInputAdapter
{
    private GameKey _activation;
    private bool _resolved;

    // The label is read once per FRAME (MissionAgentStatusCareerMixin is a [ViewModelMixin("Tick")]
    // so the bar animates), but it only changes when the player rebinds. Caching it against the key
    // it was built for keeps the rebind responsive and takes the lookup off the frame budget:
    // GameTextManager.TryGetText ends in text.CopyTextObject() + AddIDToValue, so every uncached
    // read allocated a TextObject plus three strings (the enum name, its lowercase form, and the
    // rendered result). Invalid is the correct seed because it pairs with the empty label an unbound
    // key produces, so the unbound case never recomputes either.
    private InputKey _labelledKey = InputKey.Invalid;
    private string _label = "";

    public bool IsActivationKeyPressed()
    {
        EnsureResolved();
        var key = BoundKey(_activation);

        // Input.IsKeyPressed, not a screen layer's input, for two reasons. It is what already works
        // inside a mission, and it is edge-triggered, which the activation state machine depends on
        // (see the 2026-06-02 Codex review of the Career 102 refactor). Polling through a layer
        // would also be unsafe: GameKeyContextsInputManager.RegisterHotKeyCategory sizes its slot
        // list from the FIRST category registered on that layer (116 entries), so this category's
        // id of 510 would throw. Never register this context on a layer.
        return key != InputKey.Invalid && Input.IsKeyPressed(key);
    }

    public string ActivationKeyName
    {
        get
        {
            EnsureResolved();
            var key = BoundKey(_activation);
            if (key == _labelledKey) return _label;

            if (key == InputKey.Invalid)
            {
                _labelledKey = key;
                _label = "";
                return _label;
            }

            var name = key.ToString();

            // Same lookup the Options row uses, so the chip shows the engine's own glyph text for
            // the key rather than the raw enum name.
            var texts = Module.CurrentModule?.GlobalTextManager;
            if (texts == null)
            {
                // No module yet, so the glyph text cannot be resolved. Return the enum name WITHOUT
                // caching it: caching here would pin the fallback for the rest of the session and
                // the chip would never pick up the localized form once the module exists.
                return name;
            }

            // TryGetText rather than the GetHotKeyGameTextFromKeyID extension, which routes through
            // FindText. FindText NEVER returns null or empty for a missing entry: it hands back a
            // TextObject rendering as "ERROR: Text with id str_game_key_text doesn't exist!
            // Variation: <key>", so a null-or-empty guard can never fire and the chip would print
            // that sentence. Vanilla's own GameKeyOptionVM calls FindText unguarded and has the same
            // hole; it is invisible there only because Native ships str_game_key_text for the whole
            // standard keyboard. InputKey.Extended is one that it does not ship. TryGetText is the
            // same lookup with an honest success flag, so the fallback to the enum name is reachable.
            _labelledKey = key;
            _label = texts.TryGetText("str_game_key_text", name.ToLower(), out var glyph)
                ? glyph.ToString()
                : name;
            return _label;
        }
    }

    /// <summary>
    /// Caches the GameKey the first time it is available. The REFERENCE is cached, never the
    /// InputKey behind it: HotKeyManager.LoadAsync applies a saved binding by MUTATING KeyboardKey
    /// (Key.ChangeKey) when one already exists and by REPLACING it when it is null, so only
    /// re-reading the property each frame observes both cases and lets a mid-session rebind take
    /// effect. GetGameKey is a linear scan over 511 slots, which is exactly why it runs once here
    /// instead of per frame.
    /// </summary>
    private void EnsureResolved()
    {
        if (_resolved) return;

        // HotKeyManager.GetCategory is a raw dictionary indexer and throws KeyNotFoundException on
        // a missing category, so scan instead. This runs exactly once per session.
        foreach (var category in HotKeyManager.GetAllCategories())
        {
            if (category.GameKeyCategoryId != TaomCareerHotKeyCategory.CategoryId) continue;

            _activation = category.GetGameKey(TaomCareerHotKeyCategory.AbilityActivationKeyId);
            break;
        }

        // Latch even when the category is absent. Registration happens in SubModule.OnSubModuleLoad,
        // which the engine completes for every module before the first mission tick, so a miss here
        // means registration threw and the key is never coming. Retrying would re-scan every
        // category on every poll, every frame, forever, for a result that cannot change. The field
        // stays null and BoundKey reports Invalid, so the key reads as unpressed.
        _resolved = true;
    }

    // KeyboardKey is left null by GameKey's ctor when the binding is Invalid, and a player can clear
    // a binding in Options (which instead calls ChangeKey(Invalid) on the existing Key), so both the
    // key and its holder are guarded. Invalid must read as "not pressed" rather than reaching
    // Input.IsKeyPressed.
    private static InputKey BoundKey(GameKey gameKey) =>
        gameKey?.KeyboardKey?.InputKey ?? InputKey.Invalid;
}
