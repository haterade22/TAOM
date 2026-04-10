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
        CoversHeadHook_Uninstall();
        HairClothHook_Uninstall();
        FaceMeshObserveHook_Uninstall();
        break;
    }
    return TRUE;
}
