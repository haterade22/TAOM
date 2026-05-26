#include "pch.h"
#include "CoversHeadHook.h"
#include "Logging.h"
#include "Signatures.h"
#include "SignatureScanner.h"
#include "MinHook.h"
#include <unordered_set>

// ----------------------------------------------------------------------------
// covers_head morph fix — force Face_mesh creation for morph pipeline
// ----------------------------------------------------------------------------
//
// PROBLEM:
//   When covers_head="true", SkinMask.HeadVisible (bit 0x01) is cleared.
//   add_skin_meshes_to_agent_entity checks this bit and skips Face_mesh
//   creation when it's missing. Without the Face_mesh, the GPU morph init
//   functions never run, which freezes hand grip morphs.
//
// FIX:
//   Hook the function. Before calling the original, force bit 0x01 ON in the
//   visibility mask. This creates the Face_mesh and initializes morphs.
//   Track the Face_mesh pointer in a set so FaceMeshObserveHook can suppress
//   all face components from the render list.
// ----------------------------------------------------------------------------

// add_skin_meshes_to_agent_entity signature:
//   void __fastcall(AgentVisuals* self, SkinGenParams* params,
//                   BodyProperties* body, bool useGpuMorph, bool useFaceCache)
typedef void(__fastcall* AddSkinMeshes_t)(
    uintptr_t param_1,      // rcx: AgentVisuals
    uint32_t* param_2,      // rdx: SkinGenerationParams (+0x00 = visibilityMask)
    void*     param_3,      // r8:  BodyProperties
    bool      param_4,      // r9:  useGpuMorph
    bool      param_5);     // stack: useFaceCache

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

// Resolves the target function by signature scan. Returns 0 on failure
// (signature unauthored OR pattern not found in this engine version).
static uintptr_t ResolveTarget()
{
    const auto& sig = TAOM::NativeHooks::Signatures::kAddSkinMeshes;
    if (!TAOM::NativeHooks::Signatures::IsAuthored(sig))
    {
        TAOM::NativeHooks::Logging::LogLine(
            "[CoversHead] signature '%s' not authored for this build (stub) — hook will not install",
            sig.name);
        return 0;
    }

    size_t moduleSize = 0;
    uintptr_t base = TAOM::NativeHooks::Scanner::GetModuleBase(L"TaleWorlds.Native.dll", &moduleSize);
    if (base == 0)
    {
        TAOM::NativeHooks::Logging::LogLine("[CoversHead] TaleWorlds.Native.dll not loaded");
        return 0;
    }

    uintptr_t match = TAOM::NativeHooks::Scanner::FindPattern(base, moduleSize, sig.pattern);
    if (match == 0 && sig.fallbackPattern != nullptr)
        match = TAOM::NativeHooks::Scanner::FindPattern(base, moduleSize, sig.fallbackPattern);

    if (match == 0)
    {
        TAOM::NativeHooks::Logging::LogLine(
            "[CoversHead] pattern for '%s' did not match TaleWorlds.Native.dll (size=%zu)",
            sig.name, moduleSize);
        return 0;
    }

    uintptr_t target = match + sig.byteOffsetFromMatch;
    TAOM::NativeHooks::Logging::LogLine(
        "[CoversHead] resolved '%s' at %p (base=%p, RVA=0x%llx)",
        sig.name, (void*)target, (void*)base, (unsigned long long)(target - base));
    return target;
}

bool __cdecl CoversHeadHook_Install()
{
    if (g_installed) return true;

    uintptr_t targetAddr = ResolveTarget();
    if (targetAddr == 0) return false;
    void* skinMeshesPtr = reinterpret_cast<void*>(targetAddr);

    MH_STATUS s = MH_Initialize();
    if (s != MH_OK && s != MH_ERROR_ALREADY_INITIALIZED)
    {
        TAOM::NativeHooks::Logging::LogLine("[CoversHead] MH_Initialize failed: %d", (int)s);
        return false;
    }

    s = MH_CreateHook(
        skinMeshesPtr,
        reinterpret_cast<void*>(&HookedAddSkinMeshes),
        reinterpret_cast<void**>(&g_original));
    if (s != MH_OK)
    {
        TAOM::NativeHooks::Logging::LogLine("[CoversHead] MH_CreateHook failed: %d", (int)s);
        return false;
    }

    s = MH_EnableHook(skinMeshesPtr);
    if (s != MH_OK)
    {
        TAOM::NativeHooks::Logging::LogLine("[CoversHead] MH_EnableHook failed: %d", (int)s);
        MH_RemoveHook(skinMeshesPtr);
        return false;
    }

    g_hookTarget = skinMeshesPtr;
    g_installed = true;
    TAOM::NativeHooks::Logging::LogLine("[CoversHead] hook installed");
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

    TAOM::NativeHooks::Logging::LogLine("[CoversHead] hook removed");
}
