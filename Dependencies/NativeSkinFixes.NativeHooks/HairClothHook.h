#pragma once

extern "C"
{
    // Hooks the cloth factory inside TaleWorlds.Native.dll. Two cases:
    //   1. Hair cloth: rescued from Face_mesh+0x1A0 (created by ctor, never registered)
    //   2. Beard cloth: created by calling the cloth factory directly with the beard
    //      mesh at +0x108 (the factory normally skips Face_mesh internals)
    //
    // The target function and three helper functions (AddToList, GpuInit,
    // HasClothData) are all found by signature scan; see Signatures.h.
    // Returns true on success, false on signature-not-found / hook-create-failed
    // / install error. The game keeps running on false — the hook is inert.
    __declspec(dllexport) bool __cdecl HairClothHook_Install();

    // Removes the detour. Safe to call even if Install failed or was never called.
    __declspec(dllexport) void __cdecl HairClothHook_Uninstall();

    // Returns true if the given Face_mesh has beard cloth registered via the factory.
    // Called by FaceMeshObserveHook to suppress static beard from the render list.
    __declspec(dllexport) bool __cdecl HairClothHook_HasBeardCloth(uintptr_t faceMeshPtr);
}
