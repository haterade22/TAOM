using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.TimeAcceleration;

/// <summary>
/// Registers TAOM's three campaign-map time controls as native, rebindable game keys, so they show
/// up in Options > Keybindings under "Campaign Map" alongside the vanilla keys they used to fight
/// with. The extra fast-forward key shipped hardcoded to E, which is also vanilla's MapRotateRight
/// (GameKey 59), so pressing E both accelerated time and rotated the camera with no way to change
/// either. Defaults are unchanged, so the collision is now visible and fixable rather than fixed.
///
/// This deliberately uses the plain TaleWorlds API rather than MCM or ButterLib. MCM v5 has no
/// keybind widget at all (its SettingType vocabulary is Bool/Int/Float/String/Dropdown/Button) and
/// its dropdowns persist by index, so a reordered key list would silently move every player's
/// binding. ButterLib has a hotkey wrapper, but TAOM only ever touches ButterLib by reflection and
/// its hotkey subsystem is player-disableable.
/// </summary>
public sealed class TaomTimeControlHotKeyCategory : GameKeyContext
{
    public const string CategoryId = "TaomTimeControlHotKeyCategory";

    // KeyOptionVM builds each key's localization id from ((GameKeyDefinition)Id).ToString() rather
    // than from StringId. GameKeyDefinition is a plain (non-Flags) enum running 0..115, so an id in
    // that range renders as a vanilla key's NAME and would reuse vanilla's string; at or above 116
    // it renders as the bare number, giving us str_key_name.TaomTimeControlHotKeyCategory_500.
    // Starting at 500 rather than 116 leaves room for the engine to add game keys without our ids
    // drifting into the enum's range on a future patch.
    public const int FastForwardKeyId = 500;
    public const int ExtraFastForwardKeyId = 501;
    public const int TurboKeyId = 502;

    // RegisterGameKey is an INDEXED write into a list the ctor pre-fills with this many nulls, so
    // this must stay greater than the largest id above or construction throws
    // ArgumentOutOfRangeException. The Options screen null-guards the unused slots.
    private const int RegisteredSlotCount = TurboKeyId + 1;

    public TaomTimeControlHotKeyCategory()
        : base(CategoryId, RegisteredSlotCount, GameKeyContextType.Default)
    {
        // MainCategoryId must be one of the fixed set OptionsProvider.GetGameKeyCategoriesList
        // returns or the key is registered but never rendered. CampaignMapCategory is already on
        // that allowlist, which is why none of this needs a Harmony patch.
        RegisterGameKey(new GameKey(
            FastForwardKeyId, "TaomFastForward", CategoryId,
            InputKey.Space, GameKeyMainCategories.CampaignMapCategory));

        RegisterGameKey(new GameKey(
            ExtraFastForwardKeyId, "TaomExtraFastForward", CategoryId,
            InputKey.E, GameKeyMainCategories.CampaignMapCategory));

        // Turbo keeps the Ctrl gate it has always had (the service checks IsControlDown), so this
        // shares Space with fast-forward by default and the two rows are told apart by that
        // modifier. A GameKey cannot carry a modifier itself; only HotKey can, and HotKey is not
        // rebindable in the normal Options screen.
        RegisterGameKey(new GameKey(
            TurboKeyId, "TaomTurbo", CategoryId,
            InputKey.Space, GameKeyMainCategories.CampaignMapCategory));
    }

    /// <summary>
    /// Adds this category to the engine's keybinding registry. HotKeyManager.RegisterContext is
    /// already idempotent (it no-ops when the category id is present), and it sets the manager's
    /// _needsLoading flag, so a context registered this late still has the player's saved bindings
    /// applied from BannerlordGameKeys.xml on the next HotKeyManager.Tick.
    ///
    /// Never call RegisterInitialContexts instead: that CLEARS every category first, including
    /// vanilla's.
    /// </summary>
    public static void Register()
    {
        HotKeyManager.RegisterContext(new TaomTimeControlHotKeyCategory());
    }
}
