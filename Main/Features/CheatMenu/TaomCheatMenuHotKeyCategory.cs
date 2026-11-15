using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CheatMenu;

/// <summary>
/// The cheat menu's campaign-map key, as a native rebindable game key under Options > Keybindings > Campaign Map, the
/// same way <c>TaomRealmBordersHotKeyCategory</c> registers the realm border keys (MCM v5 has no keybind widget). An id
/// inside the engine's 0..115 <c>GameKeyDefinition</c> range would render as a vanilla key's name; 550 follows the time
/// controls (500s), the career key (510), the realm borders (520s), the Seat (530s) and Marshal's Word (540s), so no two
/// TAOM categories interleave in BannerlordGameKeys.xml. The names shown in Options come from
/// <c>ModuleData/global_strings.xml</c>.
/// </summary>
public sealed class TaomCheatMenuHotKeyCategory : GameKeyContext
{
    public const string CategoryId = "TaomCheatMenuHotKeyCategory";
    public const int OpenCheatMenuKeyId = 550;

    // RegisterGameKey is an indexed write into a list pre-filled with this many nulls.
    private const int RegisteredSlotCount = OpenCheatMenuKeyId + 1;

    public TaomCheatMenuHotKeyCategory()
        : base(CategoryId, RegisteredSlotCount, GameKeyContextType.Default)
    {
        // H is free on the v1.5.3 campaign map: MapHotKeyCategory and GenericCampaignPanelsGameKeyCategory do not bind
        // it, and no TAOM key uses it. In battle vanilla binds H only in CheatsHotKeyCategory (Ctrl combos in game cheat
        // mode); DualWield uses N. The RTS Camera and Command System defaults could not be checked (their code is not
        // on the build PC), so the key is rebindable.
        RegisterGameKey(new GameKey(OpenCheatMenuKeyId, "TaomOpenCheatMenu", CategoryId,
            InputKey.H, GameKeyMainCategories.CampaignMapCategory));
    }

    /// <summary>Idempotent (HotKeyManager.RegisterContext no-ops on a present id); never RegisterInitialContexts.</summary>
    public static void Register() => HotKeyManager.RegisterContext(new TaomCheatMenuHotKeyCategory());
}
