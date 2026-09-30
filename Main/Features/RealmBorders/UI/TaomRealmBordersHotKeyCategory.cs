using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.RealmBorders.UI;

/// <summary>
/// The realm borders' two campaign-map keys, as native rebindable game keys under Options >
/// Keybindings > Campaign Map, the same way <c>TaomTimeControlHotKeyCategory</c> registers the time
/// controls (MCM v5 has no keybind widget). An id inside the engine's 0..115 <c>GameKeyDefinition</c>
/// range would render as a vanilla key's name (see there); 520 follows the time controls (500s) and
/// the career key (510) so no two TAOM categories interleave in BannerlordGameKeys.xml. The names
/// shown in Options come from <c>ModuleData/global_strings.xml</c>.
/// </summary>
public sealed class TaomRealmBordersHotKeyCategory : GameKeyContext
{
    public const string CategoryId = "TaomRealmBordersHotKeyCategory";
    public const int ToggleBordersKeyId = 520;
    public const int CycleMapModeKeyId = 521;

    // RegisterGameKey is an indexed write into a list pre-filled with this many nulls.
    private const int RegisteredSlotCount = CycleMapModeKeyId + 1;

    public TaomRealmBordersHotKeyCategory()
        : base(CategoryId, RegisteredSlotCount, GameKeyContextType.Default)
    {
        // M and G are free on the v1.5.3 campaign map: MapHotKeyCategory binds arrows, F5, Shift,
        // scroll, Q/E, 1-3, Space and WASD; GenericCampaignPanelsGameKeyCategory binds B (banner), C,
        // I, N, K, L, J, P, V and U. No other TAOM key uses M or G.
        RegisterGameKey(new GameKey(ToggleBordersKeyId, "TaomToggleRealmBorders", CategoryId,
            InputKey.M, GameKeyMainCategories.CampaignMapCategory));
        RegisterGameKey(new GameKey(CycleMapModeKeyId, "TaomRealmBordersMapMode", CategoryId,
            InputKey.G, GameKeyMainCategories.CampaignMapCategory));
    }

    /// <summary>Idempotent (HotKeyManager.RegisterContext no-ops on a present id); never RegisterInitialContexts.</summary>
    public static void Register() => HotKeyManager.RegisterContext(new TaomRealmBordersHotKeyCategory());
}
