#pragma once

extern "C"
{
    // Hooks add_skin_meshes_to_agent_entity (TaleWorlds.Native.dll RVA 0x617B50).
    //
    // When the SkinGenerationParams visibility mask has HeadVisible (bit 0x01)
    // cleared (covers_head="true"), the engine skips creating the Face_mesh
    // entirely. Without the Face_mesh, the GPU morph pipeline is never
    // initialized, which freezes hand morphs.
    //
    // This hook forces bit 0x01 ON so the Face_mesh (and morph pipeline) is
    // always created. Hidden faces are tracked in a set so the render list
    // hook can suppress all face components from rendering.
    //
    //   skinMeshesPtr — nativeBase + 0x617B50
    //
    // Returns true on success.
    __declspec(dllexport) bool __cdecl CoversHeadHook_Install(void* skinMeshesPtr);

    // Removes the hook. Safe to call even if Install failed.
    __declspec(dllexport) void __cdecl CoversHeadHook_Uninstall();
}

// Internal API — called from FaceMeshObserveHook and HairClothHook
// Returns true if the given Face_mesh should have all face rendering suppressed.
bool CoversHeadHook_ShouldHideFace(uintptr_t faceMeshPtr);

// Returns true if the current thread is inside a covers_head skin mesh call.
// Used during initial creation when the Face_mesh isn't yet in the set.
bool CoversHeadHook_IsCreatingHidden();
