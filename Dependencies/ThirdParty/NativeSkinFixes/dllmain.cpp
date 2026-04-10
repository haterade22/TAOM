#include "pch.h"
#include "HairClothHook.h"
#include "FaceMeshObserveHook.h"
#include "CoversHeadHook.h"

BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID lpReserved)
{
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
        DisableThreadLibraryCalls(hModule);
        break;
    case DLL_PROCESS_DETACH:
        // Only clean up on dynamic unload (lpReserved == nullptr).
        // During process termination (lpReserved != nullptr), other DLLs
        // may already be unloaded — running MinHook teardown under loader
        // lock risks deadlock or access violations.
        if (lpReserved == nullptr)
        {
            CoversHeadHook_Uninstall();
            HairClothHook_Uninstall();
            FaceMeshObserveHook_Uninstall();
        }
        break;
    }
    return TRUE;
}
