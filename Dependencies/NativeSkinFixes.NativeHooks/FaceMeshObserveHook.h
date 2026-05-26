#pragma once

extern "C"
{
    // Hooks the Face_mesh render list builder. Two suppression modes:
    //   1. covers_head: suppresses ALL face components (+0x100..+0x118) when
    //      CoversHeadHook marks the Face_mesh as hidden.
    //   2. cloth hair: suppresses only hair (+0x110) when cloth exists at +0x1A0,
    //      so animated hair from HairClothHook renders instead of static hair.
    //
    // The target function is found by signature scan (Signatures::kRenderListBuild).
    // Returns true on success, false on signature-not-found / hook-create-failed
    // / install error. The game keeps running on false — the hook is inert.
    __declspec(dllexport) bool __cdecl FaceMeshObserveHook_Install();

    // Removes the hook. Safe to call even if Install failed.
    __declspec(dllexport) void __cdecl FaceMeshObserveHook_Uninstall();
}
