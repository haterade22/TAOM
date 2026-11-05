using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.MarshalsWord;

/// <summary>
/// Registers Marshal's Word's keys as native, rebindable game keys, so they appear in Options > Keybindings under "Action" beside the
/// vanilla combat keys, built the way <c>TaomSeatHotKeyCategory</c> is. Three keys are held: "Give the order in turn" (540, default Left
/// Shift), "Mark, do not move" (541, default Left Ctrl) and "Keep the widths" (542, default Left Alt). Three are pressed: "Change the archers'
/// fire" (543, default Comma), "Give the word" (544, default M) and "Hold the line" (545, default Period). The ids come from build-spec section 3, and each stage raises
/// <see cref="RegisteredSlotCount"/>.
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
    public const int MarkKeyId = 541;
    public const int KeepWidthsKeyId = 542;
    public const int FireModeKeyId = 543;
    public const int WordKeyId = 544;
    public const int HoldLineKeyId = 545;

    // RegisterGameKey is an INDEXED write into a list the ctor pre-fills with this many nulls, so this must stay greater than the largest
    // id above or construction throws ArgumentOutOfRangeException. Raise it with each stage's key.
    private const int RegisteredSlotCount = HoldLineKeyId + 1;

    public TaomMarshalHotKeyCategory()
        : base(CategoryId, RegisteredSlotCount, GameKeyContextType.Default)
    {
        // The held keys are read only while the order panel is open, and the feature doc names their vanilla side effects (build-spec section 3).
        // Comma, M and Period are in none of the eight categories the mission's scene layer reads.
        RegisterGameKey(new GameKey(QueueKeyId, "TaomMarshalQueue", CategoryId, InputKey.LeftShift, GameKeyMainCategories.ActionCategory));
        RegisterGameKey(new GameKey(MarkKeyId, "TaomMarshalMark", CategoryId, InputKey.LeftControl, GameKeyMainCategories.ActionCategory));
        RegisterGameKey(new GameKey(KeepWidthsKeyId, "TaomMarshalKeepWidths", CategoryId, InputKey.LeftAlt, GameKeyMainCategories.ActionCategory));
        RegisterGameKey(new GameKey(FireModeKeyId, "TaomMarshalFireMode", CategoryId, InputKey.Comma, GameKeyMainCategories.ActionCategory));
        RegisterGameKey(new GameKey(WordKeyId, "TaomMarshalWord", CategoryId, InputKey.M, GameKeyMainCategories.ActionCategory));
        RegisterGameKey(new GameKey(HoldLineKeyId, "TaomMarshalHoldLine", CategoryId, InputKey.Period, GameKeyMainCategories.ActionCategory));
    }

    /// <summary>
    /// Adds this category to the engine's keybinding registry. <c>HotKeyManager.RegisterContext</c> is idempotent and flags the manager for
    /// a reload, so a context registered this late still has the player's saved bindings applied on the next tick. Never call
    /// <c>RegisterInitialContexts</c> instead: it CLEARS every category first, vanilla's included.
    /// </summary>
    public static void Register() => HotKeyManager.RegisterContext(new TaomMarshalHotKeyCategory());
}
