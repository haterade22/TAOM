/* Test stand-in for TaleWorlds.Native.dll: imports USER32!MessageBoxA and xinput9_1_0!XInputGetState the way the
 * engine does, so the loader brings the proxy in with it. box_slot returns what its MessageBoxA import slot holds now. */
#include <windows.h>

DWORD WINAPI XInputGetState(DWORD user, void *state);

__declspec(dllexport) void *box_slot(void) { return (void *)MessageBoxA; }
__declspec(dllexport) int call_box(const char *caption, UINT style) { return MessageBoxA(NULL, "test text", caption, style); }
__declspec(dllexport) DWORD pad_state(void) { char state[64]; return XInputGetState(4, state); }
