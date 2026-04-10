#include "pch.h"
#include "FaceMeshObserveHook.h"
#include "CoversHeadHook.h"
#include "HairClothHook.h"
#include "MinHook.h"
#include <stdio.h>
#include <stdint.h>

// ----------------------------------------------------------------------------
// Face_mesh render list suppression hook (RVA 0x61FE20)
// ----------------------------------------------------------------------------
//
// PROBLEM (why previous approaches failed):
//   Face_mesh::ctor builds a render list at +0xE0 from sub-meshes at
//   +0x100..+0x118. Post-ctor, FUN_18061e3a0 reloads meshes from skin
//   data and calls FUN_18061e970 -> FUN_18061fe20, REBUILDING the render
//   list. Any attempt to null +0x110 during the ctor or in the cloth
//   factory hook is undone by this rebuild.
//
// FIX:
//   Hook FUN_18061fe20 (the render list builder). Every time it's called
//   (from ctor, from face update, from async face generation tasks), we
//   temporarily null +0x110 if +0x1A0 has a cloth component. The render
//   list is always built WITHOUT hair. Restore +0x110 after — the mesh
//   stays alive for refcount purposes, just not in the render list.
//
//   The cloth factory hook (HairClothHook.cpp) handles animated hair
//   rendering by registering the cloth from +0x1A0 in the entity/sim lists.
//
// FUN_18061fe20 signature:
//   void render_list_build(
//       Face_mesh* self,    // rcx — Face_mesh base pointer
//       uint64_t   param_2, // rdx — forwarded context
//       uint64_t   param_3, // r8  — forwarded context
//       void*      param_4  // r9  — forwarded context
//   )
//
// Callers (3):
//   0x61c8f0  Face_mesh::ctor
//   0x61d880  (unknown)
//   0x61e970  face update finalizer (called from FUN_18061e3a0)
// ----------------------------------------------------------------------------

typedef void(__fastcall* RenderListBuild_t)(
    uintptr_t param_1,
    uintptr_t param_2,
    uintptr_t param_3,
    void* param_4
);

static RenderListBuild_t g_original = nullptr;
static void* g_targetFnPtr = nullptr;
static bool g_installed = false;
static FILE* g_logFile = nullptr;

static const char* LOG_PATH =
    "C:\\ProgramData\\Mount and Blade II Bannerlord\\logs\\TAOM_NativeSkinFixes_renderlist.log";

static void LogLine(const char* fmt, ...)
{
    char buf[1024];
    va_list args;
    va_start(args, fmt);
    vsnprintf(buf, sizeof(buf), fmt, args);
    va_end(args);
    if (g_logFile)
    {
        fputs(buf, g_logFile);
        fputc('\n', g_logFile);
        fflush(g_logFile);
    }
    OutputDebugStringA(buf);
    OutputDebugStringA("\n");
}

// Slot save/restore for temporary suppression
struct SlotSave {
    uint16_t offset;
    uintptr_t value;
};

static void __fastcall HookedRenderListBuild(
    uintptr_t param_1,
    uintptr_t param_2,
    uintptr_t param_3,
    void* param_4)
{
    SlotSave saves[4];
    int saveCount = 0;

    __try
    {
        // Priority 1: covers_head — suppress ALL face components
        bool hideAll = CoversHeadHook_IsCreatingHidden()
                    || CoversHeadHook_ShouldHideFace(param_1);
        if (hideAll)
        {
            static const uint16_t offsets[] = {0x100, 0x108, 0x110, 0x118};
            for (int i = 0; i < 4; i++)
            {
                uintptr_t* slot = (uintptr_t*)((uint8_t*)param_1 + offsets[i]);
                if (*slot != 0)
                {
                    saves[saveCount].offset = offsets[i];
                    saves[saveCount].value = *slot;
                    *slot = 0;
                    saveCount++;
                }
            }
        }
        // Priority 2: cloth-based suppression (hair and/or beard physics)
        else
        {
            // Hair cloth: suppress static hair at +0x110 when cloth exists at +0x1A0
            uintptr_t cloth = *(uintptr_t*)((uint8_t*)param_1 + 0x1A0);
            if (cloth != 0)
            {
                uintptr_t hair = *(uintptr_t*)((uint8_t*)param_1 + 0x110);
                if (hair != 0)
                {
                    saves[saveCount].offset = 0x110;
                    saves[saveCount].value = hair;
                    *(uintptr_t*)((uint8_t*)param_1 + 0x110) = 0;
                    saveCount++;
                }
            }
            // Beard cloth: suppress static beard at +0x108 when beard cloth is registered
            if (HairClothHook_HasBeardCloth(param_1))
            {
                uintptr_t beard = *(uintptr_t*)((uint8_t*)param_1 + 0x108);
                if (beard != 0)
                {
                    saves[saveCount].offset = 0x108;
                    saves[saveCount].value = beard;
                    *(uintptr_t*)((uint8_t*)param_1 + 0x108) = 0;
                    saveCount++;
                }
            }
        }
    }
    __except (EXCEPTION_EXECUTE_HANDLER)
    {
        saveCount = 0;
    }

    // Call original — builds render list at +0xE0 from +0x100..+0x118
    g_original(param_1, param_2, param_3, param_4);

    // Restore all suppressed slots
    for (int i = 0; i < saveCount; i++)
    {
        *(uintptr_t*)((uint8_t*)param_1 + saves[i].offset) = saves[i].value;
    }
}

bool __cdecl FaceMeshObserveHook_Install(void* targetFnPtr)
{
    if (g_installed) return true;
    if (!targetFnPtr) return false;

    if (fopen_s(&g_logFile, LOG_PATH, "w") != 0 || g_logFile == nullptr)
        g_logFile = nullptr;

    LogLine("[RenderList] Installing hook @ %p", targetFnPtr);

    MH_STATUS s = MH_Initialize();
    if (s != MH_OK && s != MH_ERROR_ALREADY_INITIALIZED)
    {
        LogLine("[RenderList] MH_Initialize failed: %d", (int)s);
        return false;
    }

    s = MH_CreateHook(
        targetFnPtr,
        reinterpret_cast<void*>(&HookedRenderListBuild),
        reinterpret_cast<void**>(&g_original));
    if (s != MH_OK)
    {
        LogLine("[RenderList] MH_CreateHook failed: %d", (int)s);
        return false;
    }

    s = MH_EnableHook(targetFnPtr);
    if (s != MH_OK)
    {
        LogLine("[RenderList] MH_EnableHook failed: %d", (int)s);
        MH_RemoveHook(targetFnPtr);
        return false;
    }

    g_targetFnPtr = targetFnPtr;
    g_installed = true;
    LogLine("[RenderList] Hook installed, trampoline @ %p", (void*)g_original);
    return true;
}

void __cdecl FaceMeshObserveHook_Uninstall()
{
    if (!g_installed) return;

    if (g_targetFnPtr)
    {
        MH_DisableHook(g_targetFnPtr);
        MH_RemoveHook(g_targetFnPtr);
    }

    if (g_logFile)
    {
        LogLine("[RenderList] Hook removed");
        fclose(g_logFile);
        g_logFile = nullptr;
    }

    g_targetFnPtr = nullptr;
    g_original = nullptr;
    g_installed = false;
}
