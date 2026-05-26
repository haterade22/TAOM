#pragma once

namespace TAOM { namespace NativeHooks { namespace Logging {

// Initializes the unified log file under the player's Documents folder:
//   %USERPROFILE%\Documents\Mount and Blade II Bannerlord\Logs\TAOM\NativeSkinFixes.log
//
// Falls back to OutputDebugString-only if the file can't be opened (e.g.
// game running under a restricted account). Idempotent — calling twice is
// safe; the second call is a no-op.
void Init();

// Closes the log file. Safe to call even if Init failed. After Close(),
// LogLine() falls back to OutputDebugString only until Init() is called
// again.
void Close();

// Writes a line to the log file (if open) AND OutputDebugString. printf-style
// format. Thread-safe — multiple hook threads can call concurrently.
void LogLine(const char* fmt, ...);

}}}  // namespace TAOM::NativeHooks::Logging
