using HarmonyLib;
using TaleWorlds.MountAndBlade.View.MissionViews.SiegeWeapon;

namespace TAOM.Features.SeatOfSeeing.Hooks;

/// <summary>Patch140 (build-spec 8.2): prefix on <c>RangedSiegeWeaponView.HandleUserInput</c> that skips it while the hero fights on; the registry holds why.</summary>
[HarmonyPatch(typeof(RangedSiegeWeaponView), "HandleUserInput")]
[HarmonyPatchCategory(SeatOfSeeingModule.PatchCategory)]
public static class Patch140_RangedSiegeInput
{
    [HarmonyPrefix]
    public static bool Prefix() => !SeatPresence.HeroFightsOn;
}
