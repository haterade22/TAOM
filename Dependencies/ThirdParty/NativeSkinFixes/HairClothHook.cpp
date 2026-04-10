#include "pch.h"
#include "HairClothHook.h"
#include "CoversHeadHook.h"
#include "MinHook.h"
#include <stdint.h>
#include <stdio.h>
#include <unordered_set>

// ----------------------------------------------------------------------------
// Hair cloth hook — orphaned cloth rescue
// ----------------------------------------------------------------------------
//
// Hair meshes are wrapped in Face_mesh (vtable[0xA0] = 6). The cloth factory
// (RVA 0x359C10) creates cloth for MetaMesh (type 0) but skips Face_mesh.
//
// Face_mesh::ctor (RVA 0x61C8F0) DOES create an rglCloth_simulator_component
// at Face_mesh+0x1A0 via init_mesh_data, but never registers the cloth in any
// list — it's orphaned.
//
// This hook detours the cloth factory. After the original runs on a Face_mesh,
// we rescue the orphaned cloth from +0x1A0 and register it in BOTH:
//   - ENTITY LIST (factory+0x1E8) — so cloth+0x48 gets RENDERED
//   - SIM LIST (factory+0x208) — so cloth gets SIMULATED
// Then complete the cloth's initialization (scene, GPU init, active flag).
//
// Static hair suppression is handled separately by FaceMeshObserveHook, which
// hooks the render list builder (RVA 0x61FE20) to exclude +0x110 from the
// render list whenever cloth exists at +0x1A0.
// ----------------------------------------------------------------------------

typedef void(__fastcall* ClothFactory_t)(
    uintptr_t param_1,      // rcx: cloth factory
    uintptr_t* param_2,     // rdx: mesh (MetaMesh or Face_mesh)
    uint64_t param_3,       // r8
    uint64_t param_4        // r9
);

typedef void(__fastcall* AddToList_t)(
    uintptr_t* container,   // rcx: vector at factory+0x1E8 or +0x208
    uintptr_t* item_ptr,    // rdx: pointer to the item pointer
    uint64_t ctx1,          // r8
    uint64_t ctx2           // r9
);

typedef void(__fastcall* GpuInit_t)(
    void* cloth,            // rcx: cloth component
    uintptr_t gpuResource,  // rdx: from scene+0x20
    uint64_t ctx1,          // r8
    uint64_t ctx2           // r9
);

// vtable dispatch helpers
typedef int(__fastcall* VtableRetInt_t)(void*);
typedef char(__fastcall* VtableRetChar_t)(void*);
typedef void(__fastcall* VtableVoid1_t)(void*);
typedef void(__fastcall* VtableVoid2_t)(void*, uintptr_t);
typedef void(__fastcall* VtableSetRender_t)(void*, uint32_t, int);

// Notify scene physics — FUN_18034a570
typedef void(__fastcall* NotifyPhysics_t)(void*);

static ClothFactory_t g_originalClothFactory = nullptr;
static AddToList_t g_addToList = nullptr;
static GpuInit_t g_gpuInit = nullptr;
static NotifyPhysics_t g_notifyPhysics = nullptr;
static void* g_hookTarget = nullptr;
static bool g_installed = false;
static FILE* g_logFile = nullptr;

// has_cloth_data — checks vertex buffer flags for cloth data (RVA 0x2C3420)
typedef bool(__fastcall* HasClothData_t)(void* mesh);
static HasClothData_t g_hasClothData = nullptr;

// Thread-safe set of Face_mesh pointers whose beards use cloth physics
static SRWLOCK g_beardLock = SRWLOCK_INIT;
static std::unordered_set<uintptr_t> g_beardClothFaces;

// noinline helpers — keep C++ STL calls out of SEH functions (MSVC C2712)
static __declspec(noinline) void BeardTrack_Erase(uintptr_t faceMesh)
{
    AcquireSRWLockExclusive(&g_beardLock);
    g_beardClothFaces.erase(faceMesh);
    ReleaseSRWLockExclusive(&g_beardLock);
}

static __declspec(noinline) void BeardTrack_Insert(uintptr_t faceMesh)
{
    AcquireSRWLockExclusive(&g_beardLock);
    g_beardClothFaces.insert(faceMesh);
    ReleaseSRWLockExclusive(&g_beardLock);
}

static void LogToFile(const char* fmt, ...)
{
    char buf[1024];
    va_list args;
    va_start(args, fmt);
    vsnprintf(buf, sizeof(buf), fmt, args);
    va_end(args);

    // Always write to OutputDebugString
    OutputDebugStringA(buf);
    OutputDebugStringA("\n");

    // Also write to file for easy checking
    if (g_logFile)
    {
        fprintf(g_logFile, "%s\n", buf);
        fflush(g_logFile);
    }
}

// SEH wrapper — MSVC C2712 forbids __try in functions that need C++ unwinding.
// This helper uses only POD types so __try/__except is allowed.
static void ProcessFaceMeshSEH(
    uintptr_t param_1,
    uintptr_t* param_2,
    uint64_t param_3,
    uint64_t param_4)
{
    __try
    {
        // ===== GATE: Is this a Face_mesh (type 6)? =====
        uintptr_t vtable = *(uintptr_t*)param_2;
        int type = ((VtableRetInt_t)(*(uintptr_t*)(vtable + 0xA0)))(param_2);
        if (type != 6) return;

        // ===== CLEANUP: Remove stale beard cloth tracking (handles address reuse) =====
        BeardTrack_Erase((uintptr_t)param_2);

        // ===== GATE: Skip all cloth rescue for covers_head faces =====
        // When covers_head is active, the Face_mesh exists only for morphs.
        // Rescuing cloth would render animated hair/beard under the helmet.
        if (CoversHeadHook_IsCreatingHidden()) return;

        // ===== HAIR CLOTH RESCUE =====
        uintptr_t cloth = *(uintptr_t*)((uint8_t*)param_2 + 0x1A0);
        if (cloth != 0)
        {
            uintptr_t clothVtable = *(uintptr_t*)cloth;
            uintptr_t scene = *(uintptr_t*)((uint8_t*)param_1 + 0x10);

            LogToFile("[HairCloth] === Processing Face_mesh %p ===", (void*)param_2);
            LogToFile("[HairCloth]   cloth=%p, factory=%p, scene=%p",
                      (void*)cloth, (void*)param_1, (void*)scene);

            uintptr_t renderable = *(uintptr_t*)(cloth + 0x48);
            LogToFile("[HairCloth]   cloth internal_renderable=%p", (void*)renderable);

            // STEP 1: AddRef the cloth component
            VtableVoid1_t addRef = (VtableVoid1_t)(*(uintptr_t*)(clothVtable + 0x28));
            addRef((void*)cloth);

            // STEP 2: Set cloth's scene pointer
            *(uintptr_t*)(cloth + 0x28) = scene;

            // STEP 3: Scene registration via vtable[0x1F0]
            VtableVoid2_t setParentScene = (VtableVoid2_t)(*(uintptr_t*)(clothVtable + 0x1F0));
            setParentScene((void*)cloth, scene);

            // STEP 4: Add cloth to ENTITY LIST (factory+0x1E8) — for RENDERING
            if (g_addToList != nullptr)
            {
                uintptr_t clothRef1 = cloth;
                g_addToList(
                    (uintptr_t*)((uint8_t*)param_1 + 0x1E8),
                    &clothRef1,
                    param_3,
                    param_4);
            }

            // STEP 5: Add cloth to SIM LIST (factory+0x208) — for SIMULATION
            VtableRetChar_t hasClothSim = (VtableRetChar_t)(*(uintptr_t*)(clothVtable + 0xA8));
            char canSimulate = hasClothSim((void*)cloth);
            if (canSimulate != 0 && g_addToList != nullptr)
            {
                uintptr_t clothRef2 = cloth;
                g_addToList(
                    (uintptr_t*)((uint8_t*)param_1 + 0x208),
                    &clothRef2,
                    param_3,
                    param_4);
            }

            // STEP 6: Scene bookkeeping
            if (scene != 0)
            {
                uint32_t renderFlags = *(uint32_t*)(scene + 0x2D4);
                VtableSetRender_t setRenderFlags = (VtableSetRender_t)(*(uintptr_t*)(clothVtable + 0xF0));
                setRenderFlags((void*)cloth, renderFlags, -1);
                *(int16_t*)(scene + 0x2D8) += 1;
                uintptr_t physics = *(uintptr_t*)(scene + 0x170);
                if (physics != 0 && g_notifyPhysics != nullptr)
                    g_notifyPhysics((void*)physics);
            }

            // STEP 7: Factory flags and timing
            *(uint16_t*)((uint8_t*)param_1 + 0xA8) |= 0x40;
            if (*(float*)((uint8_t*)param_1 + 0x3C) < 0.0f)
                *(float*)((uint8_t*)param_1 + 0x34) = 0.1f;

            // STEP 8: Set bookkeeping on cloth component
            *(uint32_t*)(cloth + 0x10) = 1;
            *(uintptr_t*)(cloth + 0x18) = param_1;
            *(uintptr_t*)(cloth + 0x20) = 0;

            // STEP 9: GPU resource initialization
            if (scene != 0 && g_gpuInit != nullptr)
            {
                uintptr_t gpuResource = *(uintptr_t*)(scene + 0x20);
                if (gpuResource != 0)
                    g_gpuInit((void*)cloth, gpuResource, param_3, param_4);
            }

            // STEP 10: Release our AddRef from step 1
            VtableVoid1_t release = (VtableVoid1_t)(*(uintptr_t*)(clothVtable + 0x38));
            release((void*)cloth);

            LogToFile("[HairCloth] === SUCCESS — hair cloth %p registered ===", (void*)cloth);
        }
        // ===== BEARD CLOTH =====
        // The cloth factory (0x359C10) skips Face_mesh internals (type 6).
        // If the beard mesh at +0x108 has cloth data, call the original factory
        // directly with it (MetaMesh, type 0) — the factory handles allocation,
        // construction, list registration, and all bookkeeping.
        {
            uintptr_t beardMesh = *(uintptr_t*)((uint8_t*)param_2 + 0x108);
            if (beardMesh != 0 && g_hasClothData != nullptr)
            {
                if (g_hasClothData((void*)beardMesh))
                {
                    g_originalClothFactory(param_1, (uintptr_t*)beardMesh, param_3, param_4);

                    BeardTrack_Insert((uintptr_t)param_2);

                    LogToFile("[HairCloth] Beard cloth registered for Face_mesh %p (beard=%p)",
                              (void*)param_2, (void*)beardMesh);
                }
            }
        }
    }
    __except (EXCEPTION_EXECUTE_HANDLER)
    {
        LogToFile("[HairCloth] !!! EXCEPTION processing Face_mesh %p !!!", (void*)param_2);
    }
}

static void __fastcall HookedClothFactory(
    uintptr_t param_1,      // rcx: cloth factory
    uintptr_t* param_2,     // rdx: mesh
    uint64_t param_3,       // r8
    uint64_t param_4)       // r9
{
    // Call original cloth factory — handles all normal cases
    g_originalClothFactory(param_1, param_2, param_3, param_4);

    if (param_2 != nullptr)
        ProcessFaceMeshSEH(param_1, param_2, param_3, param_4);
}

bool __cdecl HairClothHook_HasBeardCloth(uintptr_t faceMeshPtr)
{
    AcquireSRWLockShared(&g_beardLock);
    bool found = g_beardClothFaces.count(faceMeshPtr) > 0;
    ReleaseSRWLockShared(&g_beardLock);
    return found;
}

bool __cdecl HairClothHook_Install(void* clothFactoryPtr, void* addToListPtr, void* gpuInitPtr, void* hasClothDataPtr)
{
    if (g_installed) return true;
    if (!clothFactoryPtr || !addToListPtr) return false;

    g_hasClothData = hasClothDataPtr ? (HasClothData_t)hasClothDataPtr : nullptr;

    // Open log file in the TAOM module directory
    g_logFile = fopen("..\\..\\Modules\\TAOM\\HairClothHook.log", "w");
    if (!g_logFile)
    {
        g_logFile = fopen("HairClothHook.log", "w");
    }

    g_addToList = (AddToList_t)addToListPtr;
    g_gpuInit = gpuInitPtr ? (GpuInit_t)gpuInitPtr : nullptr;

    // Resolve notify physics function (0x34a570 RVA)
    // clothFactoryPtr = nativeBase + 0x359C10
    // notifyPhysics   = nativeBase + 0x34A570
    // offset = 0x34A570 - 0x359C10 = -0xF6A0
    g_notifyPhysics = (NotifyPhysics_t)((uint8_t*)clothFactoryPtr - 0xF6A0);

    LogToFile("[HairCloth] Installing hook...");
    LogToFile("[HairCloth]   clothFactory=%p, addToList=%p, gpuInit=%p, notifyPhysics=%p",
             clothFactoryPtr, addToListPtr, gpuInitPtr, (void*)g_notifyPhysics);

    MH_STATUS mhStatus = MH_Initialize();
    if (mhStatus != MH_OK && mhStatus != MH_ERROR_ALREADY_INITIALIZED)
    {
        LogToFile("[HairCloth] MH_Initialize failed: %d", (int)mhStatus);
        return false;
    }

    mhStatus = MH_CreateHook(
        clothFactoryPtr,
        reinterpret_cast<void*>(&HookedClothFactory),
        reinterpret_cast<void**>(&g_originalClothFactory));
    if (mhStatus != MH_OK)
    {
        LogToFile("[HairCloth] MH_CreateHook failed: %d", (int)mhStatus);
        return false;
    }

    mhStatus = MH_EnableHook(clothFactoryPtr);
    if (mhStatus != MH_OK)
    {
        LogToFile("[HairCloth] MH_EnableHook failed: %d", (int)mhStatus);
        MH_RemoveHook(clothFactoryPtr);
        return false;
    }

    g_hookTarget = clothFactoryPtr;
    g_installed = true;
    LogToFile("[HairCloth] Hook installed at %p, trampoline at %p",
             clothFactoryPtr, (void*)g_originalClothFactory);
    return true;
}

void __cdecl HairClothHook_Uninstall()
{
    if (!g_installed) return;

    if (g_hookTarget)
    {
        MH_DisableHook(g_hookTarget);
        MH_RemoveHook(g_hookTarget);
    }

    g_hookTarget = nullptr;
    g_originalClothFactory = nullptr;
    g_addToList = nullptr;
    g_gpuInit = nullptr;
    g_notifyPhysics = nullptr;
    g_hasClothData = nullptr;
    g_installed = false;

    AcquireSRWLockExclusive(&g_beardLock);
    g_beardClothFaces.clear();
    ReleaseSRWLockExclusive(&g_beardLock);

    LogToFile("[HairCloth] Hook removed");

    if (g_logFile)
    {
        fclose(g_logFile);
        g_logFile = nullptr;
    }
}
