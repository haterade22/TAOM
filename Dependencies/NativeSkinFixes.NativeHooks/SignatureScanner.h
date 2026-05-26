#pragma once

#include <cstdint>
#include <cstddef>

namespace TAOM { namespace NativeHooks { namespace Scanner {

// Sentinel byte used internally by the parser to represent a '?' wildcard byte
// in an IDA-style pattern string. 0xCC is the INT 3 opcode; in normal x86-64
// function bodies it would only appear as legitimate code in extremely unusual
// circumstances, but the parser strips it from the comparison so any compiled
// instruction byte (including a real 0xCC) at a wildcard position still matches.
constexpr unsigned char kWildcardSentinel = 0xCC;

// Resolves the base address and size of a loaded module by name (e.g.
// L"TaleWorlds.Native.dll"). Returns 0 + *outSize=0 on failure. The caller
// should not free the returned base — modules stay loaded for the process
// lifetime.
uintptr_t GetModuleBase(const wchar_t* moduleName, size_t* outSize);

// Scans the given memory range for the first occurrence of the IDA-style
// hex pattern (e.g. "48 89 5C 24 ? 48 89 74 24 ?"). Returns the absolute
// address of the FIRST byte of the match, or 0 if no match.
//
// Patterns:
//   - Bytes separated by whitespace, hex digits not case-sensitive.
//   - '?' or '??' = wildcard (any byte). Internally stored as 0xCC, which the
//     comparison loop skips.
//   - Returns the first match; ambiguity should be resolved by the pattern
//     itself (make it long enough to be unique).
//
// Bounds: scans [moduleBase, moduleBase + moduleSize). The .text section
// always lives inside the module's image — the caller can pass the whole
// module range; matches outside .text are vanishingly unlikely with a
// well-chosen pattern.
uintptr_t FindPattern(uintptr_t moduleBase, size_t moduleSize, const char* pattern);

}}}  // namespace TAOM::NativeHooks::Scanner
