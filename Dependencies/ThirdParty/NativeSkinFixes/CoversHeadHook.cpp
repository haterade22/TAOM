#include "pch.h"
#include "CoversHeadHook.h"
#include "MinHook.h"
#include <stdint.h>
#include <stdio.h>
#include <unordered_set>

// ----------------------------------------------------------------------------
// covers_head morph fix — force Face_mesh creation for morph pipeline
// ----------------------------------------------------------------------------
//
// PROBLEM:
//   When covers_head="true", SkinMask.HeadVisible (bit 0x01) is cleared.
//   add_skin_meshes_to_agent_entity (RVA 0x617B50) checks this bit and
//   skips Face_mesh creation when it's missing. Without the Face_mesh,
//   FUN_180620050 and FUN_18061e3a0 (GPU morph init) never run, which
//   freezes hand grip morphs.
//
// FIX:
//   Hook 0x617B50. Before calling the original, force bit 0x01 ON in the
//   visibility mask. This creates the Face_mesh and initializes morphs.
//   Track the Face_mesh pointer in a set so FaceMeshObserveHook can
//   suppress all face components from the render list.
// ----------------------------------------------------------------------------

// add_skin_meshes_to_agent_entity signature:
//   void __fastcall(AgentVisuals* self, SkinGenParams* params,
//                   BodyProperties* body, bool useGpuMorph, bool useFaceCache)
typedef void(__fastcall* AddSkinMeshes_t)(
    uintptr_t param_1,      // rcx: AgentVisuals
    uint32_t* param_2,      // rdx: SkinGenerationParams (+0x00 = visibilityMask)
    void*     param_3,      // r8:  BodyProperties
    bool      param_4,      // r9:  useGpuMorph
    bool      param_5       // stack: useFaceCache
);

static AddSkinMeshes_t g_original = nullptr;
static void* g_hookTarget = nullptr;
static bool g_installed = false;

// Thread-safe set of Face_mesh pointers whose heads should be hidden
static SRWLOCK g_lock = SRWLOCK_INIT;
static std::unordered_set<uintptr_t> g_hiddenFaces;

// Thread-local flag: true while we're inside a covers_head skin mesh call
static __declspec(thread) bool g_tls_creatingHidden = false;

bool CoversHeadHook_ShouldHideFace(uintptr_t faceMeshPtr)
{
    AcquireSRWLockShared(&g_lock);
    bool found = g_hiddenFaces.count(faceMeshPtr) > 0;
    ReleaseSRWLockShared(&g_lock);
    return found;
}

bool CoversHeadHook_IsCreatingHidden()
{
    return g_tls_creatingHidden;
}

static void __fastcall HookedAddSkinMeshes(
    uintptr_t param_1,
    uint32_t* param_2,
    void*     param_3,
    bool      param_4,
    bool      param_5)
{
    // AgentVisuals+0x830 = Face_mesh pointer
    uintptr_t oldFaceMesh = *(uintptr_t*)((uint8_t*)param_1 + 0x830);

    // Remove old entry from set (handles equipment changes)
    if (oldFaceMesh != 0)
    {
        AcquireSRWLockExclusive(&g_lock);
        g_hiddenFaces.erase(oldFaceMesh);
        ReleaseSRWLockExclusive(&g_lock);
    }

    // Check if HeadVisible (bit 0x01) is missing
    uint32_t origMask = param_2[0];
    bool headWasHidden = (origMask & 0x01) == 0;

    if (headWasHidden)
    {
        // Force HeadVisible ON so Face_mesh is created with morph pipeline
        param_2[0] |= 0x01;
        g_tls_creatingHidden = true;
    }

    // Call original — Face_mesh is now always created
    g_original(param_1, param_2, param_3, param_4, param_5);

    g_tls_creatingHidden = false;

    // Update the hidden set with the new Face_mesh
    uintptr_t newFaceMesh = *(uintptr_t*)((uint8_t*)param_1 + 0x830);
    if (newFaceMesh != 0)
    {
        AcquireSRWLockExclusive(&g_lock);
        if (headWasHidden)
            g_hiddenFaces.insert(newFaceMesh);
        else
            g_hiddenFaces.erase(newFaceMesh);  // defense against address reuse
        ReleaseSRWLockExclusive(&g_lock);
    }
}

bool __cdecl CoversHeadHook_Install(void* skinMeshesPtr)
{
    if (g_installed) return true;
    if (!skinMeshesPtr) return false;

    OutputDebugStringA("[TAOM.NativeSkinFixes] CoversHead: Installing hook...\n");

    MH_STATUS s = MH_Initialize();
    if (s != MH_OK && s != MH_ERROR_ALREADY_INITIALIZED)
    {
        OutputDebugStringA("[TAOM.NativeSkinFixes] CoversHead: MH_Initialize failed\n");
        return false;
    }

    s = MH_CreateHook(
        skinMeshesPtr,
        reinterpret_cast<void*>(&HookedAddSkinMeshes),
        reinterpret_cast<void**>(&g_original));
    if (s != MH_OK)
    {
        OutputDebugStringA("[TAOM.NativeSkinFixes] CoversHead: MH_CreateHook failed\n");
        return false;
    }

    s = MH_EnableHook(skinMeshesPtr);
    if (s != MH_OK)
    {
        OutputDebugStringA("[TAOM.NativeSkinFixes] CoversHead: MH_EnableHook failed\n");
        MH_RemoveHook(skinMeshesPtr);
        return false;
    }

    g_hookTarget = skinMeshesPtr;
    g_installed = true;
    OutputDebugStringA("[TAOM.NativeSkinFixes] CoversHead: Hook installed\n");
    return true;
}

void __cdecl CoversHeadHook_Uninstall()
{
    if (!g_installed) return;

    if (g_hookTarget)
    {
        MH_DisableHook(g_hookTarget);
        MH_RemoveHook(g_hookTarget);
    }

    AcquireSRWLockExclusive(&g_lock);
    g_hiddenFaces.clear();
    ReleaseSRWLockExclusive(&g_lock);

    g_hookTarget = nullptr;
    g_original = nullptr;
    g_installed = false;

    OutputDebugStringA("[TAOM.NativeSkinFixes] CoversHead: Hook removed\n");
}
