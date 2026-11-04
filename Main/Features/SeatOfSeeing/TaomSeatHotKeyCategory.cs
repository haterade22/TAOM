using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.SeatOfSeeing;

/// <summary>
/// Registers the Seat's keys as native, rebindable game keys, so they appear in Options > Keybindings under "Action" beside the
/// vanilla combat keys, built the way <c>TaomCareerHotKeyCategory</c> is. Stage S1 declares one key, "Take the Seat" (default Q).
/// The ids start at 530 (build-spec section 3); the later stages add theirs to this table.
///
/// <b>Never register this context on a layer.</b> <c>InputContext.RegisterHotKeyCategory</c> sizes its slot list
/// from the first category on that layer (116 entries), so an id of 530 would throw. The view holds the <see cref="GameKey"/>
/// reference (<see cref="SeatHotKeys"/>), reads its key each frame (a rebind mutates it) and tests it with the scene layer's own
/// <c>Input.IsKeyPressed</c>, which goes quiet behind a menu.
/// </summary>
public sealed class TaomSeatHotKeyCategory : GameKeyContext
{
    public const string CategoryId = "TaomSeatHotKeyCategory";

    // KeyOptionVM builds a key's localization id from ((GameKeyDefinition)Id).ToString(), not from StringId. The enum runs 0..115,
    // so an id in that range renders as a vanilla key's NAME and would reuse vanilla's string; at or above 116 it renders as the
    // bare number, giving str_key_name.TaomSeatHotKeyCategory_530.
    public const int TakeTheSeatKeyId = 530;

    // RegisterGameKey is an INDEXED write into a list the ctor pre-fills with this many nulls, so this must stay greater than the
    // largest id above or construction throws ArgumentOutOfRangeException. Raise it with each stage's key.
    private const int RegisteredSlotCount = TakeTheSeatKeyId + 1;

    public TaomSeatHotKeyCategory()
        : base(CategoryId, RegisteredSlotCount, GameKeyContextType.Default)
    {
        // ActionCategory is the in-mission group on the fixed list OptionsProvider.GetGameKeyCategoriesList returns, which is why
        // this needs no Harmony patch.
        RegisterGameKey(new GameKey(
            TakeTheSeatKeyId, "TaomSeatTake", CategoryId,
            InputKey.Q, GameKeyMainCategories.ActionCategory));
    }

    /// <summary>
    /// Adds this category to the engine's keybinding registry. <c>HotKeyManager.RegisterContext</c> is idempotent and flags the
    /// manager for a reload, so a context registered this late still has the player's saved bindings applied on the next tick.
    /// Never call <c>RegisterInitialContexts</c> instead: it CLEARS every category first, vanilla's included.
    /// </summary>
    public static void Register() => HotKeyManager.RegisterContext(new TaomSeatHotKeyCategory());
}
