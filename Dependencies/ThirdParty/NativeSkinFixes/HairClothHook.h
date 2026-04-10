#pragma once

extern "C"
{
    // Hooks the cloth factory (TaleWorlds.Native.dll RVA 0x359C10) to rescue
    // orphaned cloth from Face_mesh and register it for rendering/simulation.
    // Handles two cases:
    //   1. Hair cloth: rescued from Face_mesh+0x1A0 (created by ctor, never registered)
    //   2. Beard cloth: created by calling the cloth factory directly with the beard
    //      mesh at +0x108 (the factory normally skips Face_mesh internals)
    //
    //   clothFactoryPtr  — nativeBase + 0x359C10  (cloth factory)
    //   addToListPtr     — nativeBase + 0x0C4040  (list insertion helper)
    //   gpuInitPtr       — nativeBase + 0x292570  (GPU init, may be NULL)
    //   hasClothDataPtr  — nativeBase + 0x2C3420  (cloth data check, may be NULL)
    __declspec(dllexport) bool __cdecl HairClothHook_Install(
        void* clothFactoryPtr,
        void* addToListPtr,
        void* gpuInitPtr,
        void* hasClothDataPtr);

    // Removes the detour. Safe to call even if Install failed or was never called.
    __declspec(dllexport) void __cdecl HairClothHook_Uninstall();

    // Returns true if the given Face_mesh has beard cloth registered via the factory.
    // Called by FaceMeshObserveHook to suppress static beard from the render list.
    __declspec(dllexport) bool __cdecl HairClothHook_HasBeardCloth(uintptr_t faceMeshPtr);
}
