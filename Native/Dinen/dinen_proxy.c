/*
 * Dinen start-up proxy: an xinput9_1_0.dll for the Modding Kit's bin\Win64_Shipping_wEditor folder (#1629).
 *
 * TaleWorlds.Native.dll imports xinput9_1_0.dll, so Windows loads this file from the bin folder when the engine comes
 * into the process, before the engine loads any resource. This file points TaleWorlds.Native.dll's MessageBoxA import
 * at answer_box, which answers the same boxes as Dependencies/Foundation/DinenPolicy.cs (docs/features/dinen.md) and
 * passes every other box to the real MessageBoxA. The managed Dinen in TAOM.Dependencies hooks the same slot later and
 * chains to this hook, so both layers give the same answers.
 *
 * The slot is bound by the loader. When DllMain finds it bound, it patches at once; otherwise the proxy's thread, which
 * starts only after the loader lock is released, waits for the binding (at most 30 s) and patches then. The same
 * thread then answers Watchdog.exe's "*_*" question, which another process owns (answer_watchdog).
 *
 * The XInput stubs follow ShaderCacheKeeper's (Native/ShaderCacheKeeper/keeper.c, MIT, (c) 2026 yotthani).
 * Off switch: a file named dinen.off beside this DLL. Log: dinen_startup.log beside this DLL, at most 2000 entries, one
 * line each. The log is written with WriteFile, not CRT streams: under /MT a thread ended by ExitProcess while it holds
 * a stream lock would hang the CRT's flush at process exit.
 */
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <tlhelp32.h>
#include <stdio.h>
#include <string.h>

#define MAX_LOG_LINES 2000
#define LOG_LINE_BYTES 512

typedef int (WINAPI *message_box_a)(HWND, LPCSTR, LPCSTR, UINT);

static HMODULE g_self;
static message_box_a g_original;
static volatile LONG g_hooked;
static volatile LONG g_log_lines;
static SRWLOCK g_log_lock = SRWLOCK_INIT;
static wchar_t g_log_path[MAX_PATH];

/* ---- answer table: keep equal to DinenPolicy.AnswerFor (test/run_tests.ps1 checks the same cases) ---- */

static const char *const g_captions[] = { "ASSERT", "SAFE ASSERT", "Always Ignore?", "Safe Mode", "*_*" };

/* The whitespace string.Trim() removes from an ANSI (cp1252) caption: tab to carriage return, space, no-break space.
 * Under another ANSI code page a caption padded with that page's own spaces is shown, not answered (a safe failure). */
static int is_trimmed(char c)
{
    return (c >= '\t' && c <= '\r') || c == ' ' || (unsigned char)c == 0xA0;
}

static int same(const char *text, size_t length, const char *caption)
{
    return strlen(caption) == length && memcmp(text, caption, length) == 0;
}

/* The button MessageBoxA returns for a box with this caption and style, or 0 to show the box. */
int WINAPI DinenAnswerFor(const char *caption, UINT style)
{
    const char *title;
    size_t length, i;
    int known = 0;
    if (!caption) return 0;
    title = caption;
    length = strlen(caption);
    while (length && is_trimmed(*title)) { title++; length--; }
    while (length && is_trimmed(title[length - 1])) length--;
    for (i = 0; i < sizeof g_captions / sizeof g_captions[0]; i++)
        if (same(title, length, g_captions[i])) known = 1;
    for (i = 0; !known && i + 2 < length; i++)
        if ((title[i] | 0x20) == 'r' && (title[i + 1] | 0x20) == 'g' && (title[i + 2] | 0x20) == 'l') known = 1;
    if (!known) return 0;
    switch (style & 0xF)
    {
    case 0: case 1: return IDOK;
    case 2: return IDIGNORE;
    case 3: case 4: return same(title, length, "Always Ignore?") ? IDYES : IDNO;
    case 6: return IDCONTINUE;
    default: return 0;
    }
}

/* ---- log ---- */

/* One log entry on one line: the time, then format with a and b (each a %s). Line breaks in a and b become spaces. */
static void log_line(const char *format, const char *a, const char *b)
{
    char line[LOG_LINE_BYTES];
    SYSTEMTIME now;
    int head, body, i;
    HANDLE file;
    DWORD written;
    if (!g_log_path[0] || InterlockedIncrement(&g_log_lines) > MAX_LOG_LINES) return;
    GetLocalTime(&now);
    head = _snprintf_s(line, sizeof line, _TRUNCATE, "%04u-%02u-%02u %02u:%02u:%02u.%03u ", now.wYear, now.wMonth,
                       now.wDay, now.wHour, now.wMinute, now.wSecond, now.wMilliseconds);
    if (head < 0) return;
    body = _snprintf_s(line + head, sizeof line - head - 2, _TRUNCATE, format, a ? a : "", b ? b : "");
    body = body < 0 ? (int)strlen(line + head) : body;
    for (i = head; i < head + body; i++)
        if (line[i] == '\r' || line[i] == '\n') line[i] = ' ';
    line[head + body] = '\r';
    line[head + body + 1] = '\n';
    AcquireSRWLockExclusive(&g_log_lock);
    file = CreateFileW(g_log_path, FILE_APPEND_DATA, FILE_SHARE_READ | FILE_SHARE_WRITE, NULL, OPEN_ALWAYS,
                       FILE_ATTRIBUTE_NORMAL, NULL);
    if (file != INVALID_HANDLE_VALUE)
    {
        WriteFile(file, line, (DWORD)(head + body + 2), &written, NULL);
        CloseHandle(file);
    }
    ReleaseSRWLockExclusive(&g_log_lock);
}

static int WINAPI answer_box(HWND owner, LPCSTR text, LPCSTR caption, UINT style)
{
    int answer = DinenAnswerFor(caption, style);
    if (answer == 0) return g_original(owner, text, caption, style);
    log_line("answered '%s': %.300s", caption, text);
    return answer;
}

/* ---- import table ---- */

/* The MessageBoxA slot of the loaded TaleWorlds.Native.dll's import address table, or NULL. Only descriptors with an
 * import lookup table are read: without one, a loaded image's address table holds addresses, not names. */
static FARPROC *find_slot(BYTE *image)
{
    IMAGE_NT_HEADERS64 *nt = (IMAGE_NT_HEADERS64 *)(image + ((IMAGE_DOS_HEADER *)image)->e_lfanew);
    DWORD rva = nt->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_IMPORT].VirtualAddress;
    IMAGE_IMPORT_DESCRIPTOR *d;
    if (!rva) return NULL;
    for (d = (IMAGE_IMPORT_DESCRIPTOR *)(image + rva); d->Name; d++)
    {
        IMAGE_THUNK_DATA64 *names, *slots;
        if (_stricmp((char *)(image + d->Name), "user32.dll") != 0 || !d->OriginalFirstThunk) continue;
        names = (IMAGE_THUNK_DATA64 *)(image + d->OriginalFirstThunk);
        slots = (IMAGE_THUNK_DATA64 *)(image + d->FirstThunk);
        for (; names->u1.AddressOfData; names++, slots++)
            if (!IMAGE_SNAP_BY_ORDINAL64(names->u1.Ordinal)
                && strcmp((char *)((IMAGE_IMPORT_BY_NAME *)(image + names->u1.AddressOfData))->Name, "MessageBoxA") == 0)
                return (FARPROC *)&slots->u1.Function;
    }
    return NULL;
}

/* 1 when the slot is patched (now or earlier), 0 when it is not bound yet, -1 when it cannot be patched. */
static int try_hook(void)
{
    HMODULE native = GetModuleHandleW(L"TaleWorlds.Native.dll");
    HMODULE user32 = GetModuleHandleW(L"user32.dll");
    FARPROC real, *slot;
    DWORD previous;
    int swapped;
    if (g_hooked) return 1;
    if (!native || !user32) return 0;
    real = GetProcAddress(user32, "MessageBoxA");
    slot = find_slot((BYTE *)native);
    if (!real || !slot) return -1;
    if (*slot != real) return 0;
    g_original = (message_box_a)real;
    if (!VirtualProtect(slot, sizeof *slot, PAGE_READWRITE, &previous)) return -1;
    /* only while the slot still holds the real function: never over a hook that another layer wrote meanwhile */
    swapped = InterlockedCompareExchangePointer((PVOID volatile *)slot, (PVOID)answer_box, (PVOID)real) == (PVOID)real;
    /* The page stays writable. Restoring its old protection could make it read-only again while the managed hook in
     * TAOM.Dependencies writes the same slot (Dinen.cs), and that write would fault in managed code. */
    if (!swapped) return -1;
    InterlockedExchange(&g_hooked, 1);
    return 1;
}

/* a bad read of memory must never keep the engine from starting; anything else goes on to the system's crash handling */
static int expected_fault(DWORD code)
{
    return code == EXCEPTION_ACCESS_VIOLATION || code == EXCEPTION_IN_PAGE_ERROR ? EXCEPTION_EXECUTE_HANDLER : EXCEPTION_CONTINUE_SEARCH;
}

static int guarded_try_hook(void)
{
    __try { return try_hook(); }
    __except (expected_fault(GetExceptionCode())) { return -1; }
}

/* ---- Watchdog's crash question ---- */

static wchar_t g_bin[MAX_PATH]; /* this DLL's folder, with the trailing backslash */

/* The executable name of a process, from a process snapshot (csrss.exe cannot be opened from here), or "". */
static void process_name(DWORD pid, char *name, size_t size)
{
    PROCESSENTRY32 entry;
    HANDLE snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
    name[0] = 0;
    if (snapshot == INVALID_HANDLE_VALUE) return;
    entry.dwSize = sizeof entry;
    if (Process32First(snapshot, &entry))
        do
            if (entry.th32ProcessID == pid) { strncpy_s(name, size, entry.szExeFile, _TRUNCATE); break; }
        while (Process32Next(snapshot, &entry));
    CloseHandle(snapshot);
}

/* 1 when the process's executable lies under this DLL's folder (the Kit's own Watchdog\Watchdog.exe), else 0. */
static int under_bin(DWORD pid)
{
    wchar_t path[MAX_PATH];
    DWORD size = MAX_PATH;
    size_t length = wcslen(g_bin);
    HANDLE process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, pid);
    int inside = 0;
    if (!process) return 0;
    if (length && QueryFullProcessImageNameW(process, 0, path, &size)) inside = _wcsnicmp(path, g_bin, length) == 0;
    CloseHandle(process);
    return inside;
}

/* Watchdog.exe asks "*_*" (Yes/No: upload crash files?) after the engine sends it a warning dump. The box is a system
 * message box, so csrss.exe owns it, not this process; MessageBoxA's hook never sees it. Answered No, as
 * tools/assert_auto_ignore.py answers it: a posted WM_COMMAND for the No button, with no window activation. A box that
 * Watchdog.exe shows itself is answered only when that Watchdog.exe is the one under this bin folder. A csrss.exe box
 * names no process: one that another program raises with the same title and buttons gets No too. */
static BOOL CALLBACK answer_watchdog(HWND window, LPARAM parameter)
{
    char title[8], kind[16], owner[MAX_PATH];
    DWORD pid = 0;
    HWND no;
    (void)parameter;
    GetWindowThreadProcessId(window, &pid);
    if (pid == GetCurrentProcessId()) return TRUE; /* never send WM_GETTEXT to the Kit's own, maybe busy, windows */
    if (!IsWindowVisible(window) || GetWindowTextA(window, title, sizeof title) != 3 || strcmp(title, "*_*") != 0) return TRUE;
    if (!GetClassNameA(window, kind, sizeof kind) || strcmp(kind, "#32770") != 0) return TRUE;
    if (!(no = GetDlgItem(window, IDNO))) return TRUE;
    process_name(pid, owner, sizeof owner);
    if (_stricmp(owner, "csrss.exe") != 0 && !(_stricmp(owner, "Watchdog.exe") == 0 && under_bin(pid))) return TRUE;
    if (PostMessageW(window, WM_COMMAND, MAKEWPARAM(IDNO, BN_CLICKED), (LPARAM)no))
        log_line("answered '*_*' of %s: No", owner, NULL);
    return TRUE;
}

/* The proxy's one thread: waits for the MessageBoxA slot when DllMain could not hook it, then looks for Watchdog's
 * question every 250 ms for the life of the process. */
static DWORD WINAPI watch(LPVOID need_hook)
{
    int i, result = 0;
    if (need_hook)
    {
        for (i = 0; i < 3000 && result == 0; i++)
        {
            result = guarded_try_hook();
            if (result == 0) Sleep(10);
        }
        log_line(result == 1 ? "MessageBoxA hooked in TaleWorlds.Native.dll (after the loader)%s%s"
                             : "MessageBoxA not hooked: slot not found or not bound in 30 s%s%s", NULL, NULL);
    }
    for (;;)
    {
        EnumWindows(answer_watchdog, 0);
        Sleep(250);
    }
}

static void start(void)
{
    wchar_t off[MAX_PATH];
    DWORD n = GetModuleFileNameW(g_self, g_log_path, MAX_PATH);
    wchar_t *slash;
    HANDLE thread;
    HMODULE pinned;
    int result;
    if (n == 0 || n >= MAX_PATH - 32 || !(slash = wcsrchr(g_log_path, L'\\'))) { g_log_path[0] = 0; return; }
    slash[1] = 0;
    wcscpy_s(g_bin, MAX_PATH, g_log_path);
    wcscpy_s(off, MAX_PATH, g_log_path);
    wcscat_s(off, MAX_PATH, L"dinen.off");
    wcscat_s(g_log_path, MAX_PATH, L"dinen_startup.log");
    if (GetFileAttributesW(off) != INVALID_FILE_ATTRIBUTES) { log_line("off: dinen.off is present%s%s", NULL, NULL); return; }
    result = guarded_try_hook();
    if (result == 1) log_line("MessageBoxA hooked in TaleWorlds.Native.dll (in DllMain)%s%s", NULL, NULL);
    if (result < 0) log_line("MessageBoxA not hooked: slot not found%s%s", NULL, NULL);
    /* the thread never ends, so the DLL must stay mapped for the life of the process, even after a FreeLibrary */
    if (!GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_PIN | GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS, (LPCWSTR)watch, &pinned))
    {
        log_line("Watchdog answer off: the DLL could not be pinned%s%s", NULL, NULL);
        return;
    }
    thread = CreateThread(NULL, 0, watch, result == 0 ? (LPVOID)1 : NULL, 0, NULL);
    if (thread) CloseHandle(thread);
}

/* ---- XInput, passed on to Windows' own xinput9_1_0.dll, loaded on the first call (never in DllMain) ---- */

enum { XI_GET_STATE, XI_SET_STATE, XI_GET_CAPABILITIES, XI_GET_DSOUND_GUIDS, XI_COUNT };
static const char *const g_xinput_names[XI_COUNT] = { "XInputGetState", "XInputSetState", "XInputGetCapabilities", "XInputGetDSoundAudioDeviceGuids" };
static FARPROC g_xinput[XI_COUNT];
static INIT_ONCE g_xinput_once = INIT_ONCE_STATIC_INIT;

static BOOL CALLBACK load_system_xinput(PINIT_ONCE once, PVOID parameter, PVOID *context)
{
    wchar_t path[MAX_PATH];
    UINT n = GetSystemDirectoryW(path, MAX_PATH);
    HMODULE system;
    int i;
    (void)once; (void)parameter; (void)context;
    if (n == 0 || n >= MAX_PATH - 20) return TRUE;
    wcscat_s(path, MAX_PATH, L"\\xinput9_1_0.dll");
    system = LoadLibraryW(path);
    if (system) for (i = 0; i < XI_COUNT; i++) g_xinput[i] = GetProcAddress(system, g_xinput_names[i]);
    return TRUE;
}

static FARPROC system_xinput(int which)
{
    InitOnceExecuteOnce(&g_xinput_once, load_system_xinput, NULL, NULL);
    return g_xinput[which];
}

DWORD WINAPI XInputGetState(DWORD user, void *state)
{
    FARPROC f = system_xinput(XI_GET_STATE);
    return f ? ((DWORD (WINAPI *)(DWORD, void *))f)(user, state) : ERROR_DEVICE_NOT_CONNECTED;
}

DWORD WINAPI XInputSetState(DWORD user, void *vibration)
{
    FARPROC f = system_xinput(XI_SET_STATE);
    return f ? ((DWORD (WINAPI *)(DWORD, void *))f)(user, vibration) : ERROR_DEVICE_NOT_CONNECTED;
}

DWORD WINAPI XInputGetCapabilities(DWORD user, DWORD flags, void *capabilities)
{
    FARPROC f = system_xinput(XI_GET_CAPABILITIES);
    return f ? ((DWORD (WINAPI *)(DWORD, DWORD, void *))f)(user, flags, capabilities) : ERROR_DEVICE_NOT_CONNECTED;
}

DWORD WINAPI XInputGetDSoundAudioDeviceGuids(DWORD user, GUID *render, GUID *capture)
{
    FARPROC f = system_xinput(XI_GET_DSOUND_GUIDS);
    return f ? ((DWORD (WINAPI *)(DWORD, GUID *, GUID *))f)(user, render, capture) : ERROR_DEVICE_NOT_CONNECTED;
}

BOOL WINAPI DllMain(HINSTANCE self, DWORD reason, LPVOID reserved)
{
    (void)reserved;
    if (reason == DLL_PROCESS_ATTACH)
    {
        g_self = self;
        DisableThreadLibraryCalls(self);
        start();
    }
    return TRUE;
}
