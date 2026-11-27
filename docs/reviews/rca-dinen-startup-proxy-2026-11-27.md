# RCA: Dinen start-up proxy review (2026-11-27)

The tier 1 review of the start-up proxy (#1629, `Native/Dinen/`) found two P1 defects before the first commit: the
proxy's background thread could run after its DLL was unmapped, and its CRT file logging could hang process exit. Both
come from one source. The proxy added a thread that never ends to a template (ShaderCacheKeeper) that has none, and the
port audit checked the template's rules but not the rules a long-lived thread brings with it. No player and no Kit run
met either defect: the Kit loads the proxy as a static import, which is never unloaded, and the 2026-11-27 Kit run
ended normally. Both are fixed in the same change, with four P2 and P3 findings.

## Findings

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| 1 | P1 (Codex) | `watch` loops forever and nothing pinned the DLL: after a `FreeLibrary` (a dynamic load, or a failed load of the importing DLL) the sleeping thread returns into unmapped code (`dinen_proxy.c` `start`) | native lifetime | The port checklist (`native-cpp-ports.md`) covers logging, SEH, offsets, counters and locks; it has no item for a thread a DLL starts. The tests load the stand-in once and exit, so no unload happens | Pin with `GetModuleHandleExW(PIN \| FROM_ADDRESS)` before `CreateThread`; lesson below |
| 2 | P1 (Codex) | `log_line` wrote with `fprintf` under `/MT`. `ExitProcess` ends the thread wherever it stands; if it held the stream lock, the CRT's `_flushall` at DLL teardown waits on it for ever | native lifetime | Same gap: CRT streams were safe in ShaderCacheKeeper, which logs only from the loader's thread, and the new thread changed that without a re-check. No test ends the process while the thread writes | `CreateFileW(FILE_APPEND_DATA)` + `WriteFile` under the SRW lock, no CRT streams; lesson below |
| 3 | P2 | A check-then-write on the `MessageBoxA` slot could overwrite the managed hook if both installed at once | native hook | The two layers were reviewed apart; the managed hook was written before the proxy existed | `InterlockedCompareExchangePointer`; the proxy loses to any hook already there |
| 4 | P2 | The Watchdog answer accepted any `Watchdog.exe`, while `tools/assert_auto_ignore.py` accepts only the game folder's | port drift | The filter was written from the tool's title rule, not from its process rule | Image path must lie under the proxy's bin folder; the `csrss.exe` limit is documented |
| 5 | P2 | The C answer table trimmed fewer characters than `string.Trim()` and had a 95-byte cap | port drift | `host.c` copied only part of `DinenPolicyTests` | Same trim set, no cap, the missing rows and a 16-style sweep in `host.c` |
| 6 | P2 | With the late hook, a box opened before the thread hooks is not answered | design limit | Accepted: the real Kit hooks in `DllMain` | Documented in `docs/features/dinen.md` |
| 7 | P3 | The direct test accepted a hook from the wait thread | test strength | The check matched either log text | `host.c` checks the slot as `LoadLibrary` returns; the log must say "(in DllMain)" |
| 8 | P1 (Codex re-review) | Fix 3's CAS guarded the slot value, not the page: the proxy's restore of read-only protection could land between the managed hook's `VirtualProtect` and its write, which then faults in managed code | native hook | The first fix treated the slot as the shared state; the page protection is shared too | The proxy leaves the page writable (one line removed); no restore can race the managed write |
| 9 | P2 (Codex re-review) | The C trim follows cp1252; under another ANSI code page (cp932) `Trim()` removes that page's spaces and C does not | port drift | The parity work assumed the machine's code page | Documented limit: such a box is shown, a safe failure |

## Root-cause pattern

Findings 1 and 2 share one cause: a DLL that starts a thread takes on lifetime rules that a DLL without one does not
have. The thread can outlive the module's code (an unload) and the process's orderly shutdown (`ExitProcess` ends it
at any instruction, locks held). Anything it calls that takes a process-wide lock, the CRT stream lock above all, can
then hang the exit. The port audit asked "does the template do this safely?", and the template never had the thread.

## Why each agent missed these

- **Lens 5, Data flow:** it found the missing pin (its finding 11, LOW, for the dynamic-load case), but it judged it
  safe for the Kit's static import. It did not trace the CRT lock through `ExitProcess`.
- **Lens 2, Engine compatibility:** it checked the loader-lock work in `DllMain` and the five port rules, which have no
  thread or exit item, and passed them.
- **Codex (xhigh):** it found both, by reading the installed CRT sources (`output.cpp`, `initialization.cpp`,
  `fflush.cpp`).

## Feedback memories to codify

None beyond the lessons entry: the rule belongs in the native port checklist's area (`lessons/native-cpp-port.md`),
where the next native review reads it.
