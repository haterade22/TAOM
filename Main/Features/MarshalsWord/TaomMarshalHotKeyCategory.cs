using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.MarshalsWord;

/// <summary>
/// Registers Marshal's Word's keys as native, rebindable game keys, so they appear in Options > Keybindings under "Action" beside the
/// vanilla combat keys, built the way <c>TaomSeatHotKeyCategory</c> is. Stages M1 and M2 declare two held keys: "Give the order in turn"
/// (540, default Left Shift) and "Keep the widths" (542, default Left Alt). The ids come from build-spec section 3; 541 (mark) and 543 to
/// 545 (the pressed keys) join with their stages, and each stage raises <see cref="RegisteredSlotCount"/>.
///
/// <b>Never register this context on a layer.</b> <c>InputContext.RegisterHotKeyCategory</c> sizes its slot list from the first category
/// on that layer (116 entries), so an id of 540 would throw. The view holds the <see cref="GameKey"/> reference
/// (<see cref="MarshalHotKeys"/>), reads its key each frame (a rebind mutates it) and tests it with the static
/// <c>Input.IsKeyDown</c>, only while the order panel is open.
/// </summary>
public sealed class TaomMarshalHotKeyCategory : GameKeyContext
{
    public const string CategoryId = "TaomMarshalHotKeyCategory";

    // GameKeyOptionVM builds a key's localization id from ((GameKeyDefinition)Id).ToString(): at or above 116 the enum renders as the bare
    // number, giving str_key_name.TaomMarshalHotKeyCategory_540.
    public const int QueueKeyId = 540;
    public const int KeepWidthsKeyId = 542;

    // RegisterGameKey is an INDEXED write into a list the ctor pre-fills with this many nulls, so this must stay greater than the largest
    // id above or construction throws ArgumentOutOfRangeException. Raise it with each stage's key.
    private const int RegisteredSlotCount = KeepWidthsKeyId + 1;

    public TaomMarshalHotKeyCategory()
        : base(CategoryId, RegisteredSlotCount, GameKeyContextType.Default)
    {
        // Left Shift is also game key 24 (Zoom) and Left Alt game key 5 (ShowIndicators); both are read only while the order panel is open
        // and the feature doc names the side effects (build-spec section 3).
        RegisterGameKey(new GameKey(QueueKeyId, "TaomMarshalQueue", CategoryId, InputKey.LeftShift, GameKeyMainCategories.ActionCategory));
        RegisterGameKey(new GameKey(KeepWidthsKeyId, "TaomMarshalKeepWidths", CategoryId, InputKey.LeftAlt, GameKeyMainCategories.ActionCategory));
    }

    /// <summary>
    /// Adds this category to the engine's keybinding registry. <c>HotKeyManager.RegisterContext</c> is idempotent and flags the manager for
    /// a reload, so a context registered this late still has the player's saved bindings applied on the next tick. Never call
    /// <c>RegisterInitialContexts</c> instead: it CLEARS every category first, vanilla's included.
    /// </summary>
    public static void Register() => HotKeyManager.RegisterContext(new TaomMarshalHotKeyCategory());
}
