using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CareerSystem.Abilities;

/// <summary>
/// Registers the in-mission career ability activation as a native, rebindable game key, so it shows
/// up in Options > Keybindings under "Action" alongside the vanilla combat keys it used to fight
/// with. The key shipped hardcoded to V (AbilityInputAdapter), which a player could neither change
/// nor see conflicting with anything else they had bound. The default is unchanged, so an existing
/// player notices nothing until they go looking.
///
/// This deliberately uses the plain TaleWorlds API rather than MCM or ButterLib, for the reasons
/// already argued on TaomTimeControlHotKeyCategory: MCM v5 has no keybind widget at all, and its
/// dropdowns persist by index, so a reordered key list would silently move every player's binding.
/// </summary>
public sealed class TaomCareerHotKeyCategory : GameKeyContext
{
    public const string CategoryId = "TaomCareerHotKeyCategory";

    // KeyOptionVM builds each key's localization id from ((GameKeyDefinition)Id).ToString() rather
    // than from StringId. GameKeyDefinition is a plain (non-Flags) enum running 0..115, so an id in
    // that range renders as a vanilla key's NAME and would reuse vanilla's string; at or above 116
    // it renders as the bare number, giving us str_key_name.TaomCareerHotKeyCategory_510.
    // 510 rather than 503 leaves the time-control category room to grow without the two features'
    // ids interleaving in BannerlordGameKeys.xml.
    public const int AbilityActivationKeyId = 510;

    // RegisterGameKey is an INDEXED write into a list the ctor pre-fills with this many nulls, so
    // this must stay greater than the largest id above or construction throws
    // ArgumentOutOfRangeException. The Options screen null-guards the unused slots.
    private const int RegisteredSlotCount = AbilityActivationKeyId + 1;

    public TaomCareerHotKeyCategory()
        : base(CategoryId, RegisteredSlotCount, GameKeyContextType.Default)
    {
        // MainCategoryId must be one of the fixed set OptionsProvider.GetGameKeyCategoriesList
        // returns or the key is registered but never rendered. ActionCategory is the in-mission
        // group on that allowlist, which is why this needs no Harmony patch.
        RegisterGameKey(new GameKey(
            AbilityActivationKeyId, "TaomCareerAbility", CategoryId,
            InputKey.V, GameKeyMainCategories.ActionCategory));
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
        HotKeyManager.RegisterContext(new TaomCareerHotKeyCategory());
    }
}
