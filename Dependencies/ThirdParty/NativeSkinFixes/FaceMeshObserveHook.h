#pragma once

extern "C"
{
    // Hooks FUN_18061fe20 — the Face_mesh render list builder.
    // Two suppression modes:
    //   1. covers_head: suppresses ALL face components (+0x100..+0x118) when
    //      CoversHeadHook marks the Face_mesh as hidden.
    //   2. cloth hair: suppresses only hair (+0x110) when cloth exists at +0x1A0,
    //      so animated hair from HairClothHook renders instead of static hair.
    //
    //   targetFnPtr — absolute address of FUN_18061fe20
    //                 = GetModuleHandle("TaleWorlds.Native.dll") + 0x61FE20
    __declspec(dllexport) bool __cdecl FaceMeshObserveHook_Install(void* targetFnPtr);

    // Removes the hook. Safe to call even if Install failed.
    __declspec(dllexport) void __cdecl FaceMeshObserveHook_Uninstall();
}
