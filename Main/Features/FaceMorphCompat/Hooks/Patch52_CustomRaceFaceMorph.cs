using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.FaceMorphCompat.Hooks;

/// <summary>
/// Patch52 — Prefix on the private <c>Agent.AddSkinMeshes(bool prepareImmediately, bool useFaceCache,
/// int faceCacheID)</c> (TaleWorlds.MountAndBlade.Agent, v1.4.5).
///
/// That first bool reaches native <c>add_skin_meshes_to_agent_entity(…, useGPUMorph, …)</c> unchanged
/// (Agent.cs:5410 → MBAgentVisuals.AddSkinMeshes). When it is <c>false</c> the engine builds the face
/// on the CPU/static-morph path, which dereferences a null morph-data pointer for a custom-race head
/// whose face component lacks morph data (the dwarf eye mesh has 0 morph targets; no custom LOTR head
/// carries a <c>face_eyelash_mesh</c> that vanilla heads have) → native AV
/// "No morph data found for face mesh. Can not do static morph." This is the Erebor-arena dwarf crash:
/// arena / town spectators are batched, deferred location-character spawns — the only spawns that
/// arrive with the flag <c>false</c> — so the whole stand renders naked (the aborted AgentVisuals
/// build never attaches equipment) and the game AVs on the morph-less eye.
///
/// For a custom-mesh race this Prefix forces the flag <c>true</c>, routing the agent onto the GPU-morph
/// path the same head already renders correctly with in battles / character-creation (GPU morph
/// tolerates the missing component data). No-op when the flag is already true (main / battle agents)
/// or for the vanilla human race. Decision lives in <see cref="IFaceMorphCompatService"/>; this patch
/// is a thin boundary (ADR-002/007). Lazy IoC resolve mirrors Patch46. See
/// docs/features/face-morph-compat.md.
/// </summary>
[HarmonyPatch(typeof(Agent), "AddSkinMeshes", new[] { typeof(bool), typeof(bool), typeof(int) })]
[HarmonyPatchCategory("Patch52_CustomRaceFaceMorph")]
public static class Patch52_CustomRaceFaceMorph
{
    private static IFaceMorphCompatService _service;

    private static IFaceMorphCompatService GetService() =>
        _service ??= TAOM.IoC.Resolve<IFaceMorphCompatService>();

    // __0 = the first parameter (prepareImmediately, which IS the native useGPUMorph flag). Injected
    // positionally rather than by name so a metadata parameter-name surprise can't silently disable
    // the patch. Modified by ref to flip false -> true for custom-race agents.
    [HarmonyPrefix]
    public static void Prefix(Agent __instance, ref bool __0)
    {
        if (__0) return; // already GPU morph — nothing to do (main / battle agents)

        var character = __instance?.Character; // BasicCharacterObject; defensive null guard
        if (character == null) return;

        var service = GetService();
        if (service != null && service.ShouldForceGpuMorph(character.Race))
            __0 = true;
    }
}
