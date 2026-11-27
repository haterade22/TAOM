# Codex pre-review: Dinen start-up proxy (#1629)

You are an adversarial senior reviewer of Windows native C. Review only; do not edit files. Report defects with file:line,
severity (P1 crash/hang/data loss, P2 wrong behaviour, P3 minor), a concrete failure scenario, and the evidence you read.

## Scope

Uncommitted, in this worktree: `Native/Dinen/dinen_proxy.c`, `dinen_proxy.def`, `dinen_proxy.rc`, `build.py`,
`test/host.c`, `test/fake_native.c`, `test/watchdog.c`, `test/run_tests.ps1`; doc edits in `docs/features/dinen.md`
("Start-up proxy" section). Compare with the template `Native/ShaderCacheKeeper/keeper.c` and the managed layer
`Dependencies/Foundation/Dinen.cs` and `DinenPolicy.cs`.

## What it does

A proxy `xinput9_1_0.dll` placed in the Bannerlord Modding Kit's `bin/Win64_Shipping_wEditor`. The wEditor
`TaleWorlds.Native.dll` imports `XINPUT9_1_0.dll`, so Windows loads the proxy with it. The proxy:

1. In DllMain, finds `TaleWorlds.Native.dll`'s IAT slot for USER32 `MessageBoxA` and, if the loader has bound it, writes
   `answer_box` there; otherwise a thread waits up to 30 s for the binding.
2. `answer_box` returns a button for known captions (C copy of `DinenPolicy.AnswerFor`) or calls the real MessageBoxA.
3. The same thread then, every 250 ms forever, enumerates top-level windows and posts WM_COMMAND IDNO to a visible
   `#32770` titled `*_*` owned by `csrss.exe` or `Watchdog.exe` (the engine's Watchdog crash-upload question).
4. Forwards the four XInput exports to System32 xinput9_1_0.dll, loaded lazily with InitOnceExecuteOnce.
5. Logs to `dinen_startup.log` beside itself (cap 2000 lines); `dinen.off` beside it disables everything.

The managed `Dinen.cs` later hooks the same slot and stores the slot's then-current value (the proxy's `answer_box`)
as its original, so it chains.

## Known suspects (check each)

- Loader-lock safety: everything DllMain calls (GetModuleHandleW, GetProcAddress on user32, VirtualProtect,
  CreateThread, _wfopen_s and the CRT under /MT, GetFileAttributesW). Is the slot ever written while another thread
  may call through it? Is `*slot != real` a correct "bound" test (could the slot hold a forwarder or a shim address so
  the proxy waits 30 s and never hooks)?
- The new user32 static import in the proxy: any loader-order or DllMain risk from importing user32 in a DLL that
  TaleWorlds.Native.dll loads.
- The Watchdog answer: false positives (another program's `*_*` box owned by csrss), repeated posts while a box is
  closing, log spam, CreateToolhelp32Snapshot cost per match, UIPI failure modes, a thread that never exits during
  process teardown (DLL_PROCESS_DETACH, FreeLibrary).
- Drift between `DinenAnswerFor` (C) and `DinenPolicy.AnswerFor` (C#): trimming rules (C# `Trim()` trims all Unicode
  whitespace; C trims space, tab, CR, LF), caption length limit 95, "RGL" substring search, null/empty.
- `find_slot`: bounds, OriginalFirstThunk == 0, ordinal imports, a malformed image; SEH filter narrowness.
- `log_line` format strings with `%s` from captions (format injection?), text truncation, `g_log_lines` race.
- `build.py --install`: the marker check, a locked file, the path default.
- The tests: do they prove what they claim (e.g. does the "late" mode really exercise the wait thread)?
- Native C++ port rule (`.claude/rules/native-cpp-ports.md`): hot-path logging sample-gated, narrow SEH, no
  inter-function offsets, Interlocked counters, SRWLock balance.

Write the report as a list of findings, most severe first, then a one-line verdict.
