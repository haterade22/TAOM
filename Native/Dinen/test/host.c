/* Test host for the Dinen proxy. Mode "direct": loads the stand-in TaleWorlds.Native.dll, which brings the proxy in.
 * Mode "late": loads the proxy first, then the stand-in, so the proxy's wait thread must do the hook. Mode "off": as
 * direct, but expects the slot unhooked. Mode "watch": loads the stand-in and stays 3 s, for the Watchdog check. Prints one line per check; exit code = number of failed checks. */
#include <windows.h>
#include <stdio.h>
#include <string.h>

typedef void *(*slot_fn)(void);
typedef int (*box_fn)(const char *, UINT);
typedef DWORD (*pad_fn)(void);
typedef int (WINAPI *answer_fn)(const char *, UINT);

static int failed;
static void check(const char *what, int ok) { printf("  %s %s\n", ok ? "ok  " : "FAIL", what); if (!ok) failed++; }

int main(int argc, char **argv)
{
    const char *mode = argc > 1 ? argv[1] : "direct";
    HMODULE native, proxy;
    slot_fn slot;
    box_fn box;
    pad_fn pad;
    answer_fn answer;
    void *real;
    int i;
    if (strcmp(mode, "late") == 0 && !LoadLibraryW(L"xinput9_1_0.dll")) { printf("proxy not loaded\n"); return 90; }
    native = LoadLibraryW(L"TaleWorlds.Native.dll");
    if (!native) { printf("stand-in not loaded (error %lu)\n", GetLastError()); return 91; }
    proxy = GetModuleHandleW(L"xinput9_1_0.dll");
    slot = (slot_fn)GetProcAddress(native, "box_slot");
    box = (box_fn)GetProcAddress(native, "call_box");
    pad = (pad_fn)GetProcAddress(native, "pad_state");
    answer = proxy ? (answer_fn)GetProcAddress(proxy, "DinenAnswerFor") : NULL;
    real = (void *)GetProcAddress(GetModuleHandleW(L"user32.dll"), "MessageBoxA");
    check("the proxy is the xinput9_1_0.dll in the process", proxy && answer);
    if (!proxy || !answer) return 92;
    if (strcmp(mode, "direct") == 0) check("hooked when LoadLibrary returns (in DllMain, no wait)", slot() != real);
    for (i = 0; i < 200 && slot() == real; i++) Sleep(10);
    if (strcmp(mode, "watch") == 0) { Sleep(3000); return failed; } /* the proxy's thread answers the stand-in Watchdog */
    if (strcmp(mode, "off") == 0) { check("dinen.off: the slot still holds the real MessageBoxA", slot() == real); return failed; }
    check("the MessageBoxA slot is hooked", slot() != real);
    if (slot() == real) return failed; /* a call_box would open a real box */
    check("ASSERT 0x52012 answered Ignore", box("ASSERT", 0x52012) == IDIGNORE);
    check("SAFE ASSERT 0x52012 answered Ignore", box(" SAFE ASSERT\n", 0x52012) == IDIGNORE);
    check("Always Ignore? 0x40001 answered OK", box("Always Ignore?", 0x40001) == IDOK);
    check("RGL CONTENT WARNING 0x41030 answered OK", box("RGL CONTENT WARNING", 0x41030) == IDOK);
    check("Safe Mode 0x40004 answered No", box("Safe Mode", 0x40004) == IDNO);
    check("*_* 0x40004 answered No", box("*_*", 0x40004) == IDNO);
    check("XInput reaches Windows (user 4: ERROR_BAD_ARGUMENTS)", pad() == ERROR_BAD_ARGUMENTS);
    /* the table, case by case, as DinenPolicyTests checks AnswerFor */
    check("Always Ignore? YesNo -> Yes", answer("Always Ignore?", MB_YESNO) == IDYES);
    check("Always Ignore? YesNoCancel -> Yes", answer("Always Ignore?", MB_YESNOCANCEL) == IDYES);
    check("ASSERT YesNo -> No", answer("ASSERT", MB_YESNO) == IDNO);
    check("ASSERT CancelTryContinue -> Continue", answer("ASSERT", MB_CANCELTRYCONTINUE) == IDCONTINUE);
    check("ASSERT OK -> OK", answer("ASSERT", MB_OK) == IDOK);
    check("ASSERT RetryCancel -> show", answer("ASSERT", MB_RETRYCANCEL) == 0);
    check("ASSERT style 7 -> show", answer("ASSERT", 7) == 0);
    check("rgl in any case -> OK", answer("my rGl box", MB_OK) == IDOK);
    check("unknown caption -> show", answer("Save changes?", MB_YESNO) == 0);
    check("assert in lower case -> show", answer("assert", MB_ABORTRETRYIGNORE) == 0);
    check("null caption -> show", answer(NULL, MB_OK) == 0);
    check("empty caption -> show", answer("", MB_OK) == 0);
    /* parity with DinenPolicyTests and with string.Trim() (review 2026-11-27) */
    check("' ASSERT ' 0x12 -> Ignore", answer(" ASSERT ", 0x12) == IDIGNORE);
    check("Safe Mode YesNoCancel -> No", answer("Safe Mode", MB_YESNOCANCEL) == IDNO);
    check("RGL CONTENT ERROR OK -> OK", answer("RGL CONTENT ERROR", MB_OKCANCEL) == IDOK);
    check("Error OK -> show", answer("Error", MB_OK) == 0);
    check("vertical tab and form feed trimmed", answer("\vASSERT\f", MB_ABORTRETRYIGNORE) == IDIGNORE);
    check("no-break space (cp1252 0xA0) trimmed", answer("\xA0" "ASSERT\xA0", MB_ABORTRETRYIGNORE) == IDIGNORE);
    check("RGL with 93 more characters -> OK (no length cap)",
          answer("RGLxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx", MB_OK) == IDOK);
    check("'rg' at the end is no RGL", answer("xrg", MB_OK) == 0);
    for (i = 0; i < 16; i++)
    {
        int a = answer("ASSERT", (UINT)i), b = answer("Always Ignore?", (UINT)i);
        if (a == IDABORT || a == IDRETRY || b == IDABORT || b == IDRETRY) { check("style sweep: Abort or Retry answered", 0); break; }
    }
    check("style sweep: Abort and Retry never answered (16 styles)", i == 16);
    return failed;
}
