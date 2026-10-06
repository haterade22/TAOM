/*
 * ShaderCacheKeeper - a proxy xinput9_1_0.dll for Mount & Blade II Bannerlord.
 *
 * Copyright (c) 2026 yotthani, MIT (see LICENSE). Vendored from yotthani/bannerlord HoN/ShaderCacheKeeper at commit
 * 1bbdc17d7602d55eee4050f921b762333f38c99c and changed for TAOM (docs/reviews/adopt-yotthani-shadercachekeeper-2026-10-06.md):
 *   - XInput is reached through exported stubs that load Windows' own xinput9_1_0.dll from the system directory on
 *     first use, instead of forwarders to C:\Windows\System32 (the engine failed to load where Windows is not on C:);
 *   - the signature is noted whenever the cache is this list's or the engine is left to compile, not only after a
 *     rewrite, so a shader module updated in between is noticed;
 *   - LauncherData.xml counts only for a single player start;
 *   - a signature that does not fit means nothing is done, never a shortened comparison;
 *   - the exception filter in DllMain catches access violations and in-page errors only.
 *
 * The engine stores the game build and the sorted module list as text in
 *   %ProgramData%\Mount and Blade II Bannerlord\Shaders\CoreShaders\D3D11\shader_mapping.bin
 * and throws all its compiled shaders away when the list of a start differs from it - even by a module without any
 * shader content (about 1340 shaders, a minute and much more at full CPU load). It does so about two seconds after
 * the process starts, before any mod is loaded. Measured 06.10.2026 (MithrilForge docs/engine/perf.md): the engine
 * accepts a cache whose list text was rewritten to the list it is started with.
 *
 * TaleWorlds.Native.dll imports xinput9_1_0.dll, so Windows loads this file from the game's bin folder at the
 * moment the engine comes into the process - with every launcher, 2-3 s before the cache is checked. Every XInput
 * call is passed on to the system's own xinput9_1_0.dll (the stubs below, exported through keeper.def). When it is
 * loaded, this file does one thing:
 *
 *   module list of this start (command line "_MODULES_*a*b*_MODULES_", or the launcher's LauncherData.xml when the
 *   launcher and the game are one process) differs from the list in shader_mapping.bin
 *   AND the SIGNATURE is the same  ->  rewrite the list text in shader_mapping.bin.
 *
 * Signature: id, version and newest file time of the 'Shaders' folder of those modules of a list that have one.
 * Modules without shader content and the order of the list do not matter. If the signature differs, or there is
 * no cache, nothing is touched and the engine compiles as it always did.
 *
 * Nothing stays running, nothing is installed elsewhere. Off switch: a file named ShaderCacheKeeper.off beside
 * this DLL. Log: %ProgramData%\Mount and Blade II Bannerlord\Shaders\ShaderCacheKeeper.log
 */
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <stdarg.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <wchar.h>

#define KEEPER_VERSION "1.1.0"   /* keep in step with keeper.rc */
#define MAX_MODULES 512
#define PATH_CHARS 1024

static wchar_t g_bin[PATH_CHARS], g_game[PATH_CHARS], g_shaders[PATH_CHARS];
static LARGE_INTEGER g_start, g_frequency;

static void note(const char *format, ...)
{
    wchar_t path[PATH_CHARS];
    char text[2600], line[2700];
    SYSTEMTIME t;
    HANDLE file;
    DWORD written;
    LARGE_INTEGER size;
    va_list args;
    va_start(args, format);
    _vsnprintf_s(text, sizeof text, _TRUNCATE, format, args);
    va_end(args);
    GetLocalTime(&t);
    _snprintf_s(line, sizeof line, _TRUNCATE, "%02u.%02u. %02u:%02u:%02u.%03u [" KEEPER_VERSION "] %s\r\n", t.wDay, t.wMonth, t.wHour, t.wMinute, t.wSecond, t.wMilliseconds, text);
    _snwprintf_s(path, PATH_CHARS, _TRUNCATE, L"%s\\ShaderCacheKeeper.log", g_shaders);
    file = CreateFileW(path, FILE_APPEND_DATA | GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, NULL, OPEN_ALWAYS, FILE_ATTRIBUTE_NORMAL, NULL);
    if (file == INVALID_HANDLE_VALUE) return;
    if (GetFileSizeEx(file, &size) && size.QuadPart > 256 * 1024) { CloseHandle(file); file = CreateFileW(path, GENERIC_WRITE, FILE_SHARE_READ, NULL, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, NULL); if (file == INVALID_HANDLE_VALUE) return; }
    WriteFile(file, line, (DWORD)strlen(line), &written, NULL);
    CloseHandle(file);
}

/* whole file, with two zero bytes behind it; NULL if it cannot be read or is larger than 64 MB */
static char *read_file(const wchar_t *path, DWORD *size)
{
    HANDLE file = CreateFileW(path, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, NULL, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, NULL);
    LARGE_INTEGER length;
    char *data;
    DWORD read = 0;
    if (file == INVALID_HANDLE_VALUE) return NULL;
    if (!GetFileSizeEx(file, &length) || length.QuadPart > 64 * 1024 * 1024) { CloseHandle(file); return NULL; }
    data = (char *)malloc((size_t)length.QuadPart + 2);
    if (data && (!ReadFile(file, data, (DWORD)length.QuadPart, &read, NULL) || read != (DWORD)length.QuadPart)) { free(data); data = NULL; }
    CloseHandle(file);
    if (!data) return NULL;
    data[read] = 0; data[read + 1] = 0;
    if (size) *size = read;
    return data;
}

static int compare_names(const void *a, const void *b) { return strcmp(*(const char *const *)a, *(const char *const *)b); }

/* sorted the way the engine does (by character code) and joined with ';'. Returns a new string or NULL. */
static char *join_sorted(char **names, int count)
{
    size_t total = 1;
    char *joined, *at;
    int i;
    if (count <= 0) return NULL;
    qsort(names, (size_t)count, sizeof *names, compare_names);
    for (i = 0; i < count; i++) total += strlen(names[i]) + 1;
    joined = at = (char *)malloc(total);
    if (!joined) return NULL;
    for (i = 0; i < count; i++) { size_t n = strlen(names[i]); if (i) *at++ = ';'; memcpy(at, names[i], n); at += n; }
    *at = 0;
    return joined;
}

/* "_MODULES_*a*b*_MODULES_" in the command line -> "a;b" sorted; NULL if there is none */
static char *list_from_command_line(void)
{
    const wchar_t *line = GetCommandLineW(), *from, *to;
    char *copy, *names[MAX_MODULES], *part, *next = NULL, *joined;
    int count = 0, i, n;
    from = wcsstr(line, L"_MODULES_*");
    if (!from) return NULL;
    from += 10;
    to = wcsstr(from, L"*_MODULES_");
    if (!to || to == from) return NULL;
    n = (int)(to - from);
    copy = (char *)malloc((size_t)n + 1);
    if (!copy) return NULL;
    for (i = 0; i < n; i++) { if (from[i] > 126 || from[i] < 32) { free(copy); return NULL; } copy[i] = (char)from[i]; }
    copy[n] = 0;
    for (part = strtok_s(copy, "*", &next); part && count < MAX_MODULES; part = strtok_s(NULL, "*", &next)) if (*part) names[count++] = part;
    joined = join_sorted(names, count);
    free(copy);
    return joined;
}

/* the modules selected for single player in the text of a LauncherData.xml -> "a;b" sorted; NULL unless the launcher
 * is set to a single player start (its multiplayer selection is a different list). Writes into xml. */
static char *list_from_launcher_xml(char *xml)
{
    char *at, *end, *names[MAX_MODULES];
    int count = 0;
    if (!strstr(xml, "<GameType>Singleplayer</GameType>")) return NULL;
    at = strstr(xml, "<SingleplayerData>");
    end = at ? strstr(at, "</SingleplayerData>") : NULL;
    if (!at || !end) return NULL;
    *end = 0;
    while ((at = strstr(at, "<UserModData>")) != NULL && count < MAX_MODULES)
    {
        char *close = strstr(at, "</UserModData>"), *id, *id_end;
        if (!close) break;
        *close = 0;
        id = strstr(at, "<Id>");
        id_end = id ? strstr(id, "</Id>") : NULL;
        if (id && id_end && strstr(at, "<IsSelected>true</IsSelected>")) { *id_end = 0; names[count++] = id + 4; }
        at = close + 1;
    }
    return join_sorted(names, count);
}

/* the launcher's LauncherData.xml, read as above; NULL if not readable */
static char *list_from_launcher_data(void)
{
    wchar_t documents[PATH_CHARS], path[PATH_CHARS];
    DWORD bytes = sizeof documents, size = 0;
    char *xml, *joined;
    if (RegGetValueW(HKEY_CURRENT_USER, L"Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\User Shell Folders", L"Personal",
                     RRF_RT_REG_SZ | RRF_RT_REG_EXPAND_SZ, NULL, documents, &bytes) != ERROR_SUCCESS) return NULL;
    _snwprintf_s(path, PATH_CHARS, _TRUNCATE, L"%s\\Mount and Blade II Bannerlord\\Configs\\LauncherData.xml", documents);
    xml = read_file(path, &size);
    if (!xml) return NULL;
    joined = list_from_launcher_xml(xml);
    free(xml);
    return joined;
}

/* newest write time of the files below a folder, *.log left out (the game rewrites its compile reports) */
static ULONGLONG newest_below(const wchar_t *folder, int depth)
{
    wchar_t pattern[PATH_CHARS], child[PATH_CHARS];
    WIN32_FIND_DATAW found;
    HANDLE find;
    ULONGLONG newest = 0;
    _snwprintf_s(pattern, PATH_CHARS, _TRUNCATE, L"%s\\*", folder);
    find = FindFirstFileW(pattern, &found);
    if (find == INVALID_HANDLE_VALUE) return 0;
    do
    {
        ULONGLONG time;
        size_t n = wcslen(found.cFileName);
        if (!wcscmp(found.cFileName, L".") || !wcscmp(found.cFileName, L"..")) continue;
        if (found.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY)
        {
            if (depth >= 6) continue;
            _snwprintf_s(child, PATH_CHARS, _TRUNCATE, L"%s\\%s", folder, found.cFileName);
            time = newest_below(child, depth + 1);
        }
        else
        {
            if (n > 4 && !_wcsicmp(found.cFileName + n - 4, L".log")) continue;
            time = ((ULONGLONG)found.ftLastWriteTime.dwHighDateTime << 32) | found.ftLastWriteTime.dwLowDateTime;
        }
        if (time > newest) newest = time;
    } while (FindNextFileW(find, &found));
    FindClose(find);
    return newest;
}

/* "id@version@time ..." for the modules of a ';' list that have a Shaders folder. Returns a new string, or NULL when
 * it does not fit (a shortened signature could hide a difference). */
static char *signature_of(const char *list)
{
    size_t capacity = strlen(list) + 64 * MAX_MODULES / 8 + 256, used = 0;
    char *signature = (char *)malloc(capacity), *copy = _strdup(list), *id, *next = NULL;
    if (!signature || !copy) { free(signature); free(copy); return NULL; }
    signature[0] = 0;
    for (id = strtok_s(copy, ";", &next); id; id = strtok_s(NULL, ";", &next))
    {
        wchar_t folder[PATH_CHARS], sub[PATH_CHARS];
        char version[64] = "", *xml, *at;
        DWORD attributes;
        int written;
        _snwprintf_s(folder, PATH_CHARS, _TRUNCATE, L"%s\\Modules\\%hs\\Shaders", g_game, id);
        attributes = GetFileAttributesW(folder);
        if (attributes == INVALID_FILE_ATTRIBUTES || !(attributes & FILE_ATTRIBUTE_DIRECTORY)) continue;
        _snwprintf_s(sub, PATH_CHARS, _TRUNCATE, L"%s\\Modules\\%hs\\SubModule.xml", g_game, id);
        xml = read_file(sub, NULL);
        if (xml && (at = strstr(xml, "<Version")) != NULL && (at = strstr(at, "value")) != NULL && (at = strchr(at, '"')) != NULL)
        {
            char *close = strchr(at + 1, '"');
            if (close && close - at - 1 < (ptrdiff_t)sizeof version) { memcpy(version, at + 1, (size_t)(close - at - 1)); version[close - at - 1] = 0; }
        }
        free(xml);
        if (used + strlen(id) + 100 > capacity) { free(copy); free(signature); return NULL; }
        written = _snprintf_s(signature + used, capacity - used, _TRUNCATE, "%s%s@%s@%llx", used ? " " : "", id, version, newest_below(folder, 0));
        if (written > 0) used += (size_t)written;
    }
    free(copy);
    return signature;
}

/* the signature noted beside the cache for exactly this list, as a new string; NULL if none is */
static char *recorded_signature(const wchar_t *sidecar, const char *list)
{
    char *recorded = read_file(sidecar, NULL), *line_end, *second, *second_end, *signature = NULL;
    size_t length = strlen(list);
    if (!recorded) return NULL;
    line_end = strchr(recorded, '\n');
    if (line_end && (size_t)(line_end - recorded) == length && !strncmp(recorded, list, length))
    {
        second = line_end + 1;
        second_end = strchr(second, '\n');
        if (second_end) *second_end = 0;
        signature = _strdup(second);
    }
    free(recorded);
    return signature;
}

/* notes "list\nsignature\n" beside the cache: the shader modules the cache of that list was built or kept for */
static void note_signature(const wchar_t *sidecar, const char *list, const char *signature)
{
    DWORD written;
    HANDLE file = CreateFileW(sidecar, GENERIC_WRITE, 0, NULL, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, NULL);
    if (file == INVALID_HANDLE_VALUE) return;
    WriteFile(file, list, (DWORD)strlen(list), &written, NULL);
    WriteFile(file, "\n", 1, &written, NULL);
    WriteFile(file, signature, (DWORD)strlen(signature), &written, NULL);
    WriteFile(file, "\n", 1, &written, NULL);
    CloseHandle(file);
}

static void keep(void)
{
    wchar_t exe[PATH_CHARS], program_data[PATH_CHARS], mapping[PATH_CHARS], temporary[PATH_CHARS], sidecar[PATH_CHARS], off[PATH_CHARS];
    wchar_t *name, *slash;
    char *wanted = NULL, *data = NULL, *on_disk = NULL, *old_signature = NULL, *new_signature = NULL, *fresh = NULL;
    const char *source;
    DWORD size = 0, length1, length2, written;
    size_t wanted_length, fresh_size, rest_at;
    HANDLE file;
    HMODULE self = NULL;
    LARGE_INTEGER now;
    BOOL from_launcher_data = FALSE;

    QueryPerformanceFrequency(&g_frequency);
    QueryPerformanceCounter(&g_start);
    if (!GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT, (LPCWSTR)(void *)&keep, &self)) return;
    if (!GetModuleFileNameW(self, g_bin, PATH_CHARS - 1)) return;
    if ((slash = wcsrchr(g_bin, L'\\')) == NULL) return;
    *slash = 0;                                             /* ...\bin\Win64_Shipping_Client */
    wcscpy_s(g_game, PATH_CHARS, g_bin);
    if ((slash = wcsrchr(g_game, L'\\')) == NULL) return;
    *slash = 0;
    if ((slash = wcsrchr(g_game, L'\\')) == NULL) return;
    *slash = 0;                                             /* the game folder */
    if (!GetEnvironmentVariableW(L"ProgramData", program_data, PATH_CHARS - 1)) return;
    _snwprintf_s(g_shaders, PATH_CHARS, _TRUNCATE, L"%s\\Mount and Blade II Bannerlord\\Shaders", program_data);

    /* only the game itself; any other program that happens to load XInput from this folder is left alone */
    if (!GetModuleFileNameW(NULL, exe, PATH_CHARS - 1)) return;
    name = wcsrchr(exe, L'\\');
    name = name ? name + 1 : exe;
    if (_wcsnicmp(name, L"Bannerlord", 10) != 0) return;
    _snwprintf_s(off, PATH_CHARS, _TRUNCATE, L"%s\\ShaderCacheKeeper.off", g_bin);
    if (GetFileAttributesW(off) != INVALID_FILE_ATTRIBUTES) return;

    _snwprintf_s(mapping, PATH_CHARS, _TRUNCATE, L"%s\\CoreShaders\\D3D11\\shader_mapping.bin", g_shaders);
    _snwprintf_s(temporary, PATH_CHARS, _TRUNCATE, L"%s\\CoreShaders\\D3D11\\shader_mapping.bin.keeper", g_shaders);
    _snwprintf_s(sidecar, PATH_CHARS, _TRUNCATE, L"%s\\CoreShaders\\D3D11\\keeper_signature.txt", g_shaders);

    wanted = list_from_command_line();
    /* without a list on the command line only the launcher that becomes the game itself (BLSE) is understood */
    if (!wanted && wcsstr(name, L"Launcher")) { wanted = list_from_launcher_data(); from_launcher_data = TRUE; }
    source = from_launcher_data ? "launcher selection" : "command line";
    if (!wanted) { note("%ls: no module list found (command line, LauncherData.xml) - nothing done", name); goto done; }

    data = read_file(mapping, &size);
    if (!data || size < 16)
    {
        /* the engine builds the cache for this list: note what it is built for */
        if ((new_signature = signature_of(wanted)) != NULL) note_signature(sidecar, wanted, new_signature);
        note("%ls: no shader cache on disk - the engine compiles", name);
        goto done;
    }
    memcpy(&length1, data + 4, 4);
    if (length1 > 256 || 12 + length1 > size) { note("%ls: shader_mapping.bin not understood (build text) - nothing done", name); goto done; }
    memcpy(&length2, data + 8 + length1, 4);
    if (length2 > 65536 || 12 + length1 + length2 > size) { note("%ls: shader_mapping.bin not understood (module list) - nothing done", name); goto done; }
    on_disk = (char *)malloc((size_t)length2 + 1);
    if (!on_disk) goto done;
    memcpy(on_disk, data + 12 + length1, length2);
    on_disk[length2] = 0;
    /* the signature the cache was built or last kept for, if noted for exactly this list */
    old_signature = recorded_signature(sidecar, on_disk);
    if (!strcmp(on_disk, wanted))
    {
        /* note today's signature only when none is noted: one noted earlier still describes what the cache holds */
        if (!old_signature && (new_signature = signature_of(wanted)) != NULL) note_signature(sidecar, wanted, new_signature);
        note("%ls: the shader cache is this module list's (%s)", name, source);
        goto done;
    }

    /* nothing noted for the cache's list (first start with the keeper): what its list gives today */
    if (!old_signature) old_signature = signature_of(on_disk);
    new_signature = signature_of(wanted);
    if (!old_signature || !new_signature)
    {
        note("%ls: the shader modules do not fit the signature (too many) - nothing done", name);
        goto done;
    }
    if (strcmp(old_signature, new_signature) != 0)
    {
        /* the engine builds the cache for this list: note what it is built for */
        note_signature(sidecar, wanted, new_signature);
        note("%ls: modules with shader content changed - the engine compiles. Before: [%s] now: [%s]", name, old_signature, new_signature);
        goto done;
    }

    wanted_length = strlen(wanted);
    rest_at = 12 + (size_t)length1 + length2;
    fresh_size = 8 + (size_t)length1 + 4 + wanted_length + (size - rest_at);
    fresh = (char *)malloc(fresh_size);
    if (!fresh) goto done;
    memcpy(fresh, data, 8 + (size_t)length1);
    { DWORD n = (DWORD)wanted_length; memcpy(fresh + 8 + length1, &n, 4); }
    memcpy(fresh + 12 + length1, wanted, wanted_length);
    memcpy(fresh + 12 + length1 + wanted_length, data + rest_at, size - rest_at);
    file = CreateFileW(temporary, GENERIC_WRITE, 0, NULL, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, NULL);
    if (file == INVALID_HANDLE_VALUE) { note("%ls: cannot write beside shader_mapping.bin (error %lu) - nothing done", name, GetLastError()); goto done; }
    if (!WriteFile(file, fresh, (DWORD)fresh_size, &written, NULL) || written != (DWORD)fresh_size) { CloseHandle(file); DeleteFileW(temporary); note("%ls: writing failed - nothing done", name); goto done; }
    CloseHandle(file);
    if (!MoveFileExW(temporary, mapping, MOVEFILE_REPLACE_EXISTING)) { DWORD error = GetLastError(); DeleteFileW(temporary); note("%ls: could not replace shader_mapping.bin (error %lu) - nothing done", name, error); goto done; }
    note_signature(sidecar, wanted, new_signature);
    QueryPerformanceCounter(&now);
    note("%ls: module list changed (%s), same shader modules [%s] - list text in the shader cache rewritten, %.1f ms",
         name, source, new_signature, (double)(now.QuadPart - g_start.QuadPart) * 1000.0 / (double)g_frequency.QuadPart);

done:
    free(wanted); free(data); free(on_disk); free(old_signature); free(new_signature); free(fresh);
}

/* XInput: TaleWorlds.Native.dll imports XInputGetState and XInputSetState from this file. Each call goes to Windows' own
 * xinput9_1_0.dll, loaded from the system directory on the first call (never in DllMain, under the loader lock).
 * Exported by name and by the system file's ordinals through keeper.def. */
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

/* without Windows' XInput every call answers "no controller" */
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

/* a bad read of a file or of memory must never keep the game from starting; anything else (heap corruption, a stack
 * overflow) goes on to the system's crash handling */
static int expected_fault(DWORD code)
{
    return code == EXCEPTION_ACCESS_VIOLATION || code == EXCEPTION_IN_PAGE_ERROR ? EXCEPTION_EXECUTE_HANDLER : EXCEPTION_CONTINUE_SEARCH;
}

BOOL WINAPI DllMain(HINSTANCE self, DWORD reason, LPVOID reserved)
{
    (void)reserved;
    if (reason == DLL_PROCESS_ATTACH)
    {
        DisableThreadLibraryCalls(self);
        __try { keep(); }
        __except (expected_fault(GetExceptionCode())) { }
    }
    return TRUE;
}
