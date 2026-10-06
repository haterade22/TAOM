/* Test host: stands in for Bannerlord.exe. Loads xinput9_1_0.dll from its own folder (the proxy) the way a program
 * with a static import would, calls the two XInput functions TaleWorlds.Native.dll imports through it and prints the
 * results. Upstream: yotthani/bannerlord HoN/ShaderCacheKeeper test/host.c (MIT); XInputSetState added for TAOM. */
#include <windows.h>
#include <stdio.h>

typedef DWORD (WINAPI *GetState)(DWORD, void *);
typedef DWORD (WINAPI *SetState)(DWORD, void *);

int main(void)
{
    char state[64];
    WORD vibration[2] = { 0, 0 };
    HMODULE proxy = LoadLibraryW(L"xinput9_1_0.dll");
    GetState get = proxy ? (GetState)GetProcAddress(proxy, "XInputGetState") : NULL;
    SetState set = proxy ? (SetState)GetProcAddress(proxy, "XInputSetState") : NULL;
    wchar_t path[1024];
    if (!proxy || !get || !set) { printf("proxy not loaded (error %lu)\n", GetLastError()); return 2; }
    GetModuleFileNameW(proxy, path, 1023);
    /* user index 4 does not exist: Windows' own XInput answers ERROR_BAD_ARGUMENTS (160), so this line proves the call
     * reached it and not a fallback */
    printf("loaded %ls; XInputGetState(4) = %lu; XInputGetState(0) = %lu; XInputSetState(0) = %lu\n", path,
           get(4, state), get(0, state), set(0, vibration));
    return 0;
}
