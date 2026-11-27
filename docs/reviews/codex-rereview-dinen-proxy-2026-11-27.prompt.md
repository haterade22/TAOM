# Codex re-review: fixes to the Dinen start-up proxy (#1629)

You are an adversarial senior reviewer of Windows native C. Review only; do not edit files. Your earlier pass
(`docs/reviews/raw/codex-prereview-dinen-proxy-2026-11-27.md`, last section) found seven issues in
`Native/Dinen/dinen_proxy.c`. They are now fixed in the working tree. Check each fix, and look for defects the fixes add.

| Earlier finding | Fix to check |
|---|---|
| P1 worker runs after unload | `start()` pins the DLL with `GetModuleHandleExW(PIN \| FROM_ADDRESS)` before `CreateThread` |
| P1 CRT stream lock at ExitProcess | `log_line` formats with `_snprintf_s` into a stack buffer and appends with `CreateFileW(FILE_APPEND_DATA)` + `WriteFile` under the SRW lock; no CRT streams |
| P2 race with the managed hook | `try_hook` swaps with `InterlockedCompareExchangePointer(slot, answer_box, real)` and returns -1 when it loses |
| P2 Watchdog scope | a box owned by `Watchdog.exe` is answered only when its image path (`QueryFullProcessImageNameW`) starts with this DLL's folder; windows of the own process are skipped before `GetWindowTextA`; a `csrss.exe` box is still answered by title and buttons (documented limit) |
| P2 box before the late hook | not changed; documented in `docs/features/dinen.md` |
| P2 C/C# table drift | trims 0x09..0x0D, space, 0xA0; no copy, no length cap; tests in `test/host.c` |
| P3 DllMain proof | `host.c` direct mode checks the slot right after `LoadLibrary`; `run_tests.ps1` needs "(in DllMain)" |

Look in particular at: buffer arithmetic in `log_line` (`head`, `body`, the two bytes for CR LF, `_TRUNCATE` return
values), `under_bin` prefix matching (a sibling folder whose name starts with the same text), the CAS on a slot
whose page protection is changed by `VirtualProtect`, and whether `DinenAnswerFor` now equals
`Dependencies/Foundation/DinenPolicy.cs` `AnswerFor` for every byte string.

Report findings most severe first (P1 crash/hang, P2 wrong behaviour, P3 minor) with file:line, a concrete failure
scenario and the evidence you read, then a one-line verdict.
