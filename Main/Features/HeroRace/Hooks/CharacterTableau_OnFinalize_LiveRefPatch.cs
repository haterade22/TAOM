using HarmonyLib;
using TaleWorlds.MountAndBlade.View.Tableaus;

namespace TAOM.Features.HeroRace.Hooks;

/// <summary>
/// Drops <see cref="LiveTableauRef"/>'s handle when the tableau it points at is finalized.
///
/// <para>Without this, "finalized" and "collected" are two different moments and the tuner uses the
/// wrong one. <c>CharacterTableauTextureProvider.Clear</c> calls
/// <c>CharacterTableau.OnFinalize</c>, which nulls <c>_agentVisuals</c>, <c>_mountVisuals</c>,
/// <c>_oldAgentVisuals</c> and <c>_oldMountVisuals</c> (read on 1.4.8), but the provider keeps
/// holding the managed tableau. A <c>WeakReference</c> therefore keeps resolving it until a GC
/// happens to run.</para>
///
/// <para>The window that opens is small and entirely misleading: close a dwarf inventory screen,
/// then run <c>taom.nudge_race_offset avatar . v 0.05</c>. The <c>.</c> still resolves to dwarf,
/// <c>print_race_offsets</c> still reports an on-screen tableau, and the command reports success
/// while marking a finalized object dirty, which redraws nothing. The edit is real and can be
/// saved, so the tuner silently attributes it to a race the player is no longer looking at.</para>
///
/// <para>Deliberately its own patch class rather than an addition to
/// <c>Patch67_TableauResidencyDiag</c>, which patches the same method: that category is diagnostic
/// instrumentation whose own header instructs its removal once #389 closes. This is feature
/// behaviour and belongs to the feature's category so it lives and dies with it.</para>
/// </summary>
[HarmonyPatch(typeof(CharacterTableau), nameof(CharacterTableau.OnFinalize))]
[HarmonyPatchCategory("Patch72_TableauRacePosition")]
public static class CharacterTableau_OnFinalize_LiveRefPatch
{
    [HarmonyPostfix]
    public static void Postfix(CharacterTableau __instance)
    {
        // Conditional: tableaux are torn down out of order, so clearing unconditionally would let a
        // closing encyclopedia page blank the handle to an inventory panel still on screen.
        LiveTableauRef.ClearIf(__instance);
    }
}
