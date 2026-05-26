#include "pch.h"
#include "SignatureScanner.h"
#include <psapi.h>

#pragma comment(lib, "psapi.lib")

namespace TAOM { namespace NativeHooks { namespace Scanner {

namespace {

// Parses one or two hex chars (e.g. "48", "8", "5C") starting at *cursor.
// Advances *cursor past the digits. Returns false if the next char isn't
// a hex digit. Whitespace must be stripped by the caller before this is
// invoked.
bool ParseHexByte(const char** cursor, unsigned char* out)
{
    auto hexVal = [](char c, int* v) -> bool {
        if (c >= '0' && c <= '9') { *v = c - '0';      return true; }
        if (c >= 'a' && c <= 'f') { *v = c - 'a' + 10; return true; }
        if (c >= 'A' && c <= 'F') { *v = c - 'A' + 10; return true; }
        return false;
    };
    int hi = 0, lo = 0;
    if (!hexVal((*cursor)[0], &hi)) return false;
    // Second digit is optional — "8" == 0x08, "48" == 0x48.
    if (hexVal((*cursor)[1], &lo)) { *out = (unsigned char)((hi << 4) | lo); *cursor += 2; }
    else                            { *out = (unsigned char)hi;              *cursor += 1; }
    return true;
}

// Skips ASCII whitespace at *cursor.
void SkipSpace(const char** cursor)
{
    while (**cursor == ' ' || **cursor == '\t') ++(*cursor);
}

// Parses an IDA-style hex pattern into a flat byte buffer. Wildcards become
// kWildcardSentinel. Returns the count of bytes consumed (or 0 on failure).
// The output buffer must be at least 256 bytes — patterns longer than that
// indicate authoring error.
size_t ParsePattern(const char* pattern, unsigned char* outBytes, size_t maxBytes)
{
    const char* cursor = pattern;
    size_t count = 0;
    while (*cursor != '\0' && count < maxBytes)
    {
        SkipSpace(&cursor);
        if (*cursor == '\0') break;
        if (*cursor == '?')
        {
            outBytes[count++] = kWildcardSentinel;
            ++cursor;
            // "??" is also valid.
            if (*cursor == '?') ++cursor;
            continue;
        }
        unsigned char byte = 0;
        if (!ParseHexByte(&cursor, &byte)) return 0;
        outBytes[count++] = byte;
    }
    return count;
}

}  // namespace

uintptr_t GetModuleBase(const wchar_t* moduleName, size_t* outSize)
{
    if (outSize) *outSize = 0;
    HMODULE hMod = GetModuleHandleW(moduleName);
    if (hMod == nullptr) return 0;

    MODULEINFO info;
    ZeroMemory(&info, sizeof(info));
    if (!GetModuleInformation(GetCurrentProcess(), hMod, &info, sizeof(info))) return 0;

    if (outSize) *outSize = info.SizeOfImage;
    return reinterpret_cast<uintptr_t>(info.lpBaseOfDll);
}

uintptr_t FindPattern(uintptr_t moduleBase, size_t moduleSize, const char* pattern)
{
    if (moduleBase == 0 || moduleSize == 0 || pattern == nullptr || *pattern == '\0') return 0;

    unsigned char bytes[256];
    size_t patternLen = ParsePattern(pattern, bytes, sizeof(bytes));
    if (patternLen == 0 || patternLen > moduleSize) return 0;

    const unsigned char* haystack = reinterpret_cast<const unsigned char*>(moduleBase);
    size_t limit = moduleSize - patternLen;

    for (size_t i = 0; i <= limit; ++i)
    {
        bool match = true;
        for (size_t j = 0; j < patternLen; ++j)
        {
            unsigned char expected = bytes[j];
            if (expected == kWildcardSentinel) continue;
            if (haystack[i + j] != expected) { match = false; break; }
        }
        if (match) return moduleBase + i;
    }
    return 0;
}

}}}  // namespace TAOM::NativeHooks::Scanner
