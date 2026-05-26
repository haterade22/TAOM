#include "pch.h"
#include "FaceMeshObserveHook.h"
#include "CoversHeadHook.h"
#include "HairClothHook.h"
#include "Logging.h"
#include "Signatures.h"
#include "SignatureScanner.h"
#include "MinHook.h"

// ----------------------------------------------------------------------------
// Face_mesh render list suppression hook
// ----------------------------------------------------------------------------
//
// PROBLEM (why previous approaches failed):
//   Face_mesh::ctor builds a render list at +0xE0 from sub-meshes at
//   +0x100..+0x118. Post-ctor, a separate update path reloads meshes from
//   skin data and calls render_list_build again, REBUILDING the list. Any
//   attempt to null +0x110 during the ctor or in the cloth factory hook is
//   undone by this rebuild.
//
// FIX:
//   Hook render_list_build. Every time it's called (from ctor, from face
//   update, from async face generation tasks), we temporarily null +0x110
//   if +0x1A0 has a cloth component, and likewise null +0x108 if beard
//   cloth is registered. The render list is always built WITHOUT static
//   hair/beard meshes. Restore the slots after — the meshes stay alive for
//   refcount purposes, just not in the render list.
//
//   For covers_head Face_mesh, null all four slots (+0x100..+0x118) so
//   nothing renders under the helmet — animated hair/beard from the cloth
//   factory hook are also suppressed for hidden heads.
// ----------------------------------------------------------------------------

typedef void(__fastcall* RenderListBuild_t)(
    uintptr_t param_1,
    uintptr_t param_2,
    uintptr_t param_3,
    void* param_4);

static RenderListBuild_t g_original = nullptr;
static void* g_targetFnPtr = nullptr;
static bool g_installed = false;

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
    // Access-violation only — see HairClothHook for rationale. Heap corruption
    // / stack overflow must propagate so the crash dump is honest.
    __except (GetExceptionCode() == EXCEPTION_ACCESS_VIOLATION
                  ? EXCEPTION_EXECUTE_HANDLER
                  : EXCEPTION_CONTINUE_SEARCH)
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

static uintptr_t ResolveTarget()
{
    const auto& sig = TAOM::NativeHooks::Signatures::kRenderListBuild;
    if (!TAOM::NativeHooks::Signatures::IsAuthored(sig))
    {
        TAOM::NativeHooks::Logging::LogLine(
            "[RenderList] signature '%s' not authored for this build (stub) — hook will not install",
            sig.name);
        return 0;
    }

    size_t moduleSize = 0;
    uintptr_t base = TAOM::NativeHooks::Scanner::GetModuleBase(L"TaleWorlds.Native.dll", &moduleSize);
    if (base == 0)
    {
        TAOM::NativeHooks::Logging::LogLine("[RenderList] TaleWorlds.Native.dll not loaded");
        return 0;
    }

    uintptr_t match = TAOM::NativeHooks::Scanner::FindPattern(base, moduleSize, sig.pattern);
    if (match == 0 && sig.fallbackPattern != nullptr)
        match = TAOM::NativeHooks::Scanner::FindPattern(base, moduleSize, sig.fallbackPattern);

    if (match == 0)
    {
        TAOM::NativeHooks::Logging::LogLine(
            "[RenderList] pattern for '%s' did not match TaleWorlds.Native.dll (size=%zu)",
            sig.name, moduleSize);
        return 0;
    }

    uintptr_t target = match + sig.byteOffsetFromMatch;
    TAOM::NativeHooks::Logging::LogLine(
        "[RenderList] resolved '%s' at %p (base=%p, RVA=0x%llx)",
        sig.name, (void*)target, (void*)base, (unsigned long long)(target - base));
    return target;
}

bool __cdecl FaceMeshObserveHook_Install()
{
    if (g_installed) return true;

    uintptr_t targetAddr = ResolveTarget();
    if (targetAddr == 0) return false;
    void* targetFnPtr = reinterpret_cast<void*>(targetAddr);

    MH_STATUS s = MH_Initialize();
    if (s != MH_OK && s != MH_ERROR_ALREADY_INITIALIZED)
    {
        TAOM::NativeHooks::Logging::LogLine("[RenderList] MH_Initialize failed: %d", (int)s);
        return false;
    }

    s = MH_CreateHook(
        targetFnPtr,
        reinterpret_cast<void*>(&HookedRenderListBuild),
        reinterpret_cast<void**>(&g_original));
    if (s != MH_OK)
    {
        TAOM::NativeHooks::Logging::LogLine("[RenderList] MH_CreateHook failed: %d", (int)s);
        return false;
    }

    s = MH_EnableHook(targetFnPtr);
    if (s != MH_OK)
    {
        TAOM::NativeHooks::Logging::LogLine("[RenderList] MH_EnableHook failed: %d", (int)s);
        MH_RemoveHook(targetFnPtr);
        return false;
    }

    g_targetFnPtr = targetFnPtr;
    g_installed = true;
    TAOM::NativeHooks::Logging::LogLine("[RenderList] hook installed");
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

    g_targetFnPtr = nullptr;
    g_original = nullptr;
    g_installed = false;

    TAOM::NativeHooks::Logging::LogLine("[RenderList] hook removed");
}
