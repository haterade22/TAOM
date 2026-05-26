#include "pch.h"
#include "HairClothHook.h"
#include "FaceMeshObserveHook.h"
#include "CoversHeadHook.h"
#include "Logging.h"

BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID lpReserved)
{
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
        DisableThreadLibraryCalls(hModule);
        TAOM::NativeHooks::Logging::Init();
        break;
    case DLL_PROCESS_DETACH:
        CoversHeadHook_Uninstall();
        HairClothHook_Uninstall();
        FaceMeshObserveHook_Uninstall();
        TAOM::NativeHooks::Logging::Close();
        break;
    }
    return TRUE;
}
