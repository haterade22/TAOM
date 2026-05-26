#include "pch.h"
#include "Logging.h"
#include <shlobj.h>
#include <cstdio>
#include <cstdarg>

#pragma comment(lib, "Shell32.lib")

namespace TAOM { namespace NativeHooks { namespace Logging {

namespace {

FILE*    g_file = nullptr;
SRWLOCK  g_lock = SRWLOCK_INIT;
bool     g_initialized = false;

bool BuildLogPath(wchar_t* outPath, size_t maxChars)
{
    // %USERPROFILE%\Documents\Mount and Blade II Bannerlord\Logs\TAOM\NativeSkinFixes.log
    PWSTR docs = nullptr;
    HRESULT hr = SHGetKnownFolderPath(FOLDERID_Documents, 0, nullptr, &docs);
    if (FAILED(hr) || docs == nullptr) return false;

    // Compose the directory; create each level if missing.
    wchar_t dir[MAX_PATH];
    if (swprintf_s(dir, MAX_PATH,
            L"%s\\Mount and Blade II Bannerlord\\Logs\\TAOM",
            docs) < 0)
    {
        CoTaskMemFree(docs);
        return false;
    }
    CoTaskMemFree(docs);

    // CreateDirectoryW returns success-or-already-exists; we walk only the
    // TAOM-specific leg since "Documents\Mount and Blade II Bannerlord\Logs"
    // is created by the game itself.
    wchar_t parent[MAX_PATH];
    swprintf_s(parent, MAX_PATH, L"%s\\..", dir);
    CreateDirectoryW(parent, nullptr);
    CreateDirectoryW(dir, nullptr);

    return swprintf_s(outPath, maxChars, L"%s\\NativeSkinFixes.log", dir) > 0;
}

}  // namespace

void Init()
{
    AcquireSRWLockExclusive(&g_lock);
    if (g_initialized) { ReleaseSRWLockExclusive(&g_lock); return; }
    g_initialized = true;

    wchar_t path[MAX_PATH];
    if (BuildLogPath(path, MAX_PATH))
    {
        _wfopen_s(&g_file, path, L"w");
    }
    ReleaseSRWLockExclusive(&g_lock);

    if (g_file != nullptr)
    {
        LogLine("[NativeSkinFixes] log open");
    }
    else
    {
        OutputDebugStringA("[NativeSkinFixes] log file unavailable; debug-string only\n");
    }
}

void Close()
{
    AcquireSRWLockExclusive(&g_lock);
    if (g_file != nullptr)
    {
        fputs("[NativeSkinFixes] log close\n", g_file);
        fclose(g_file);
        g_file = nullptr;
    }
    g_initialized = false;
    ReleaseSRWLockExclusive(&g_lock);
}

void LogLine(const char* fmt, ...)
{
    char buf[1024];
    va_list args;
    va_start(args, fmt);
    int n = vsnprintf(buf, sizeof(buf), fmt, args);
    va_end(args);
    if (n < 0) return;

    // Always emit to OutputDebugString for live attach scenarios.
    OutputDebugStringA(buf);
    OutputDebugStringA("\n");

    AcquireSRWLockExclusive(&g_lock);
    if (g_file != nullptr)
    {
        fputs(buf, g_file);
        fputc('\n', g_file);
        fflush(g_file);
    }
    ReleaseSRWLockExclusive(&g_lock);
}

}}}  // namespace TAOM::NativeHooks::Logging
