#include "pch.h"
#include "HairClothHook.h"
#include "CoversHeadHook.h"
#include "Logging.h"
#include "Signatures.h"
#include "SignatureScanner.h"
#include "MinHook.h"
#include <unordered_set>

// ----------------------------------------------------------------------------
// Hair cloth hook — orphaned cloth rescue
// ----------------------------------------------------------------------------
//
// Hair meshes are wrapped in Face_mesh (vtable[0xA0] returns 6). The cloth
// factory creates cloth for MetaMesh (type 0) but skips Face_mesh.
//
// Face_mesh::ctor DOES create an rglCloth_simulator_component at
// Face_mesh+0x1A0 via init_mesh_data, but never registers the cloth in any
// list — it's orphaned.
//
// This hook detours the cloth factory. After the original runs on a Face_mesh,
// we rescue the orphaned cloth from +0x1A0 and register it in BOTH:
//   - ENTITY LIST (factory+0x1E8) — so cloth+0x48 gets RENDERED
//   - SIM LIST (factory+0x208) — so cloth gets SIMULATED
// Then complete the cloth's initialization (scene, GPU init, active flag).
//
// Static hair suppression is handled separately by FaceMeshObserveHook, which
// hooks the render list builder to exclude +0x110 from the render list
// whenever cloth exists at +0x1A0.
// ----------------------------------------------------------------------------

typedef void(__fastcall* ClothFactory_t)(
    uintptr_t param_1,      // rcx: cloth factory
    uintptr_t* param_2,     // rdx: mesh (MetaMesh or Face_mesh)
    uint64_t param_3,       // r8
    uint64_t param_4);      // r9

typedef void(__fastcall* AddToList_t)(
    uintptr_t* container,   // rcx: vector at factory+0x1E8 or +0x208
    uintptr_t* item_ptr,    // rdx: pointer to the item pointer
    uint64_t ctx1,          // r8
    uint64_t ctx2);         // r9

typedef void(__fastcall* GpuInit_t)(
    void* cloth,            // rcx: cloth component
    uintptr_t gpuResource,  // rdx: from scene+0x20
    uint64_t ctx1,          // r8
    uint64_t ctx2);         // r9

// vtable dispatch helpers
typedef int(__fastcall* VtableRetInt_t)(void*);
typedef char(__fastcall* VtableRetChar_t)(void*);
typedef void(__fastcall* VtableVoid1_t)(void*);
typedef void(__fastcall* VtableVoid2_t)(void*, uintptr_t);
typedef void(__fastcall* VtableSetRender_t)(void*, uint32_t, int);

// Notify scene physics
typedef void(__fastcall* NotifyPhysics_t)(void*);

// has_cloth_data — checks vertex buffer flags for cloth data
typedef bool(__fastcall* HasClothData_t)(void* mesh);

static ClothFactory_t  g_originalClothFactory = nullptr;
static AddToList_t     g_addToList            = nullptr;
static GpuInit_t       g_gpuInit              = nullptr;
static NotifyPhysics_t g_notifyPhysics        = nullptr;
static HasClothData_t  g_hasClothData         = nullptr;
static void*           g_hookTarget           = nullptr;
static bool            g_installed            = false;

// Thread-safe set of Face_mesh pointers whose beards use cloth physics
static SRWLOCK g_beardLock = SRWLOCK_INIT;
static std::unordered_set<uintptr_t> g_beardClothFaces;

// Hot-path log debouncing. Logging is the dominant cost per Face_mesh in this
// hook — every call takes SRWLock + fputs + fflush. Battle scenes create
// thousands of Face_meshes; per-call logging is a measurable frame-stutter
// source (deep-review Agent 3 HIGH 2026-05-26). We log only the first
// `kHotPathLogSampleLimit` invocations of each event type as diagnostic
// samples, then increment a counter and emit a single summary in Uninstall.
constexpr LONG64 kHotPathLogSampleLimit = 3;
static volatile LONG64 g_hairProcessCount = 0;
static volatile LONG64 g_hairSuccessCount = 0;
static volatile LONG64 g_beardSuccessCount = 0;
static volatile LONG64 g_exceptionCount = 0;

// Returns true if the caller is within the first `kHotPathLogSampleLimit`
// observations for the given counter. Atomic; thread-safe.
static inline bool ShouldSampleLog(volatile LONG64* counter)
{
    LONG64 n = InterlockedIncrement64(counter);
    return n <= kHotPathLogSampleLimit;
}

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

            // Hot path — sample-log only the first kHotPathLogSampleLimit
            // observations to avoid per-Face_mesh I/O cost during battle load.
            bool sampleLog = ShouldSampleLog(&g_hairProcessCount);
            if (sampleLog)
            {
                uintptr_t renderable = *(uintptr_t*)(cloth + 0x48);
                TAOM::NativeHooks::Logging::LogLine(
                    "[HairCloth] sample-processing Face_mesh=%p cloth=%p factory=%p scene=%p renderable=%p",
                    (void*)param_2, (void*)cloth, (void*)param_1, (void*)scene, (void*)renderable);
            }

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

            if (ShouldSampleLog(&g_hairSuccessCount))
            {
                TAOM::NativeHooks::Logging::LogLine(
                    "[HairCloth] sample-success hair cloth %p registered (Face_mesh %p)",
                    (void*)cloth, (void*)param_2);
            }
        }
        // ===== BEARD CLOTH =====
        // The cloth factory skips Face_mesh internals (type 6).
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

                    if (ShouldSampleLog(&g_beardSuccessCount))
                    {
                        TAOM::NativeHooks::Logging::LogLine(
                            "[HairCloth] sample-beard cloth registered Face_mesh=%p beard=%p",
                            (void*)param_2, (void*)beardMesh);
                    }
                }
            }
        }
    }
    // Narrow the SEH catch to access-violations only. Heap corruption, stack
    // overflow, and similar lethal exceptions must propagate to the OS so the
    // crash dump captures real state — they are NOT recoverable here.
    __except (GetExceptionCode() == EXCEPTION_ACCESS_VIOLATION
                  ? EXCEPTION_EXECUTE_HANDLER
                  : EXCEPTION_CONTINUE_SEARCH)
    {
        if (ShouldSampleLog(&g_exceptionCount))
        {
            TAOM::NativeHooks::Logging::LogLine(
                "[HairCloth] sample-AV processing Face_mesh %p (access violation, suppressed)",
                (void*)param_2);
        }
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

// Resolves one signature via the scanner. Returns 0 on stub-pattern or
// not-found. Logs are written from the caller so the failure list is grouped.
static uintptr_t ResolveOne(uintptr_t base, size_t moduleSize,
                            const TAOM::NativeHooks::Signatures::TargetSignature& sig,
                            bool* outAuthored)
{
    if (outAuthored) *outAuthored = TAOM::NativeHooks::Signatures::IsAuthored(sig);
    if (!TAOM::NativeHooks::Signatures::IsAuthored(sig)) return 0;

    uintptr_t match = TAOM::NativeHooks::Scanner::FindPattern(base, moduleSize, sig.pattern);
    if (match == 0 && sig.fallbackPattern != nullptr)
        match = TAOM::NativeHooks::Scanner::FindPattern(base, moduleSize, sig.fallbackPattern);
    if (match == 0) return 0;
    return match + sig.byteOffsetFromMatch;
}

bool __cdecl HairClothHook_Install()
{
    if (g_installed) return true;

    size_t moduleSize = 0;
    uintptr_t base = TAOM::NativeHooks::Scanner::GetModuleBase(L"TaleWorlds.Native.dll", &moduleSize);
    if (base == 0)
    {
        TAOM::NativeHooks::Logging::LogLine("[HairCloth] TaleWorlds.Native.dll not loaded");
        return false;
    }

    // Resolve all 5 targets. The cloth factory + AddToList are MANDATORY;
    // the other three are nice-to-have (graceful degradation).
    bool authored[5] = { false, false, false, false, false };
    uintptr_t clothFactoryAddr = ResolveOne(base, moduleSize, TAOM::NativeHooks::Signatures::kClothFactory,    &authored[0]);
    uintptr_t addToListAddr    = ResolveOne(base, moduleSize, TAOM::NativeHooks::Signatures::kAddToList,       &authored[1]);
    uintptr_t gpuInitAddr      = ResolveOne(base, moduleSize, TAOM::NativeHooks::Signatures::kGpuInit,         &authored[2]);
    uintptr_t hasClothDataAddr = ResolveOne(base, moduleSize, TAOM::NativeHooks::Signatures::kHasClothData,    &authored[3]);
    uintptr_t notifyPhysAddr   = ResolveOne(base, moduleSize, TAOM::NativeHooks::Signatures::kNotifyPhysics,   &authored[4]);

    // Report each authoring / scan outcome up-front so failure is diagnosable.
    auto report = [&](const TAOM::NativeHooks::Signatures::TargetSignature& sig, uintptr_t addr, bool isAuth) {
        if (!isAuth)
            TAOM::NativeHooks::Logging::LogLine("[HairCloth] '%s' signature not authored for this build (stub)", sig.name);
        else if (addr == 0)
            TAOM::NativeHooks::Logging::LogLine("[HairCloth] '%s' pattern did not match TaleWorlds.Native.dll", sig.name);
        else
            TAOM::NativeHooks::Logging::LogLine("[HairCloth] '%s' resolved at %p (RVA=0x%llx)",
                sig.name, (void*)addr, (unsigned long long)(addr - base));
    };
    report(TAOM::NativeHooks::Signatures::kClothFactory,  clothFactoryAddr, authored[0]);
    report(TAOM::NativeHooks::Signatures::kAddToList,     addToListAddr,    authored[1]);
    report(TAOM::NativeHooks::Signatures::kGpuInit,       gpuInitAddr,      authored[2]);
    report(TAOM::NativeHooks::Signatures::kHasClothData,  hasClothDataAddr, authored[3]);
    report(TAOM::NativeHooks::Signatures::kNotifyPhysics, notifyPhysAddr,   authored[4]);

    // Mandatory pair gating
    if (clothFactoryAddr == 0 || addToListAddr == 0)
    {
        TAOM::NativeHooks::Logging::LogLine(
            "[HairCloth] mandatory targets missing (clothFactory/AddToList) — hook will not install");
        return false;
    }

    void* clothFactoryPtr = reinterpret_cast<void*>(clothFactoryAddr);

    g_addToList     = reinterpret_cast<AddToList_t>(addToListAddr);
    g_gpuInit       = gpuInitAddr      ? reinterpret_cast<GpuInit_t>(gpuInitAddr)             : nullptr;
    g_hasClothData  = hasClothDataAddr ? reinterpret_cast<HasClothData_t>(hasClothDataAddr)   : nullptr;
    g_notifyPhysics = notifyPhysAddr   ? reinterpret_cast<NotifyPhysics_t>(notifyPhysAddr)    : nullptr;

    MH_STATUS mhStatus = MH_Initialize();
    if (mhStatus != MH_OK && mhStatus != MH_ERROR_ALREADY_INITIALIZED)
    {
        TAOM::NativeHooks::Logging::LogLine("[HairCloth] MH_Initialize failed: %d", (int)mhStatus);
        return false;
    }

    mhStatus = MH_CreateHook(
        clothFactoryPtr,
        reinterpret_cast<void*>(&HookedClothFactory),
        reinterpret_cast<void**>(&g_originalClothFactory));
    if (mhStatus != MH_OK)
    {
        TAOM::NativeHooks::Logging::LogLine("[HairCloth] MH_CreateHook failed: %d", (int)mhStatus);
        return false;
    }

    mhStatus = MH_EnableHook(clothFactoryPtr);
    if (mhStatus != MH_OK)
    {
        TAOM::NativeHooks::Logging::LogLine("[HairCloth] MH_EnableHook failed: %d", (int)mhStatus);
        MH_RemoveHook(clothFactoryPtr);
        return false;
    }

    g_hookTarget = clothFactoryPtr;
    g_installed = true;
    TAOM::NativeHooks::Logging::LogLine(
        "[HairCloth] hook installed at %p, trampoline at %p",
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

    // Emit hot-path summary so the suppressed per-Face_mesh logs are
    // recoverable as aggregate counts. Read once each — these counters are
    // only updated from the hot path and we're past the last possible hit.
    TAOM::NativeHooks::Logging::LogLine(
        "[HairCloth] hook removed (lifetime totals: hair-processed=%lld hair-success=%lld beard-success=%lld AV-suppressed=%lld)",
        (long long)g_hairProcessCount,
        (long long)g_hairSuccessCount,
        (long long)g_beardSuccessCount,
        (long long)g_exceptionCount);
}
