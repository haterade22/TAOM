using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CompanionTactics.BattleActionBar.Hooks;

/// <summary>
/// Postfix on <c>Formation.SetMovementOrder(MovementOrder)</c> — implements the previously-dead
/// MCM setting <c>CancelStanceOnMove</c>. When enabled, any movement order change clears the
/// formation's recorded stance.
///
/// v1.3.15 verified signature (ilspycmd):
///   public void SetMovementOrder(MovementOrder input)
/// </summary>
[HarmonyPatch(typeof(Formation), nameof(Formation.SetMovementOrder), new[] { typeof(MovementOrder) })]
[HarmonyPatchCategory("Patch35_CompanionTactics")]
public static class Patch35_Formation_SetMovementOrder
{
    private static ICompanionTacticsSettingsProvider _settings;
    private static ITroopStanceManager _stances;

    [HarmonyPostfix]
    public static void Postfix(Formation __instance)
    {
        try
        {
            _settings ??= IoC.Resolve<ICompanionTacticsSettingsProvider>();
            if (_settings == null || !_settings.CancelStanceOnMove) return;
            if (__instance == null) return;

            _stances ??= IoC.Resolve<ITroopStanceManager>();
            _stances?.ClearStance((int)__instance.FormationIndex);
        }
        catch { }
    }
}
