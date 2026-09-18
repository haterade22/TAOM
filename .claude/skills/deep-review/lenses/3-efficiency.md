# Agent 3 lens: Efficiency & Performance

Review these files for performance and efficiency issues. Read each file and check:

FILES: the list in your spawn prompt.

**BEFORE YOU ASSERT OR DEFER ON ANY ENGINE CALL'S COST: DECOMPILE IT.** You have the same tools Agent 2 does — `pwsh tools/taom-src.ps1 path <FullTypeName>`, or `ilspycmd "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/<Assembly>.dll" -t "Full.Type.Name"`. A TaleWorlds method that looks like a plain accessor may allocate: `FaceGen.GetRaceNames()` returns `(string[])_raceNamesArray.Clone()` — a fresh array per call — while the adjacent `FaceGen.GetBaseMonsterNameFromRace(int)` indexes the same array for free. Names do not tell you this; bodies do. Look for `.Clone()`, `.ToArray()`, `.ToList()`, `new`, and string building inside anything you are about to call cheap or expensive.
- **An unverified cost claim is reported as UNVERIFIED, never as HIGH.** Severity requires evidence you actually read. Where a log or measurement exists, prefer it over estimation — do not derive a latency figure from an assumed syscall.
- **Before recommending a log-level downgrade (INFO → DEBUG), state what happens to that line in a hard crash.** TAOM's `FileLogger` drains INFO synchronously with a flush on the calling thread and leaves DEBUG on an async queue, deliberately, so a native CTD preserves the tail — and `_logFile` is a `StreamWriter`, whose `Flush()` goes to the OS file cache, NOT to disk (it never calls `FlushFileBuffers`). Read `Main/Core/Logging/FileLogger.cs` before costing any logging change. Downgrading a crash-localisation stamp destroys the stamp's purpose while looking like an optimisation. (2026-08-03: an agent recommended exactly this on the strength of an assumed disk-sync cost; measured reality was 1287 durable stamps in 145 ms, ~0.5% of load. RCA `docs/reviews/rca-battleload-agentbuild-2026-08-03.md`.)

CHECK ALL OF THESE:
1. **Hot Path Allocations:** Any code in DailyTick, HourlyTick, or OnTick handlers — avoid LINQ, avoid allocating lists/arrays per tick, use cached collections
2. **IoC.Resolve in Hot Paths:** Flag ANY IoC.Resolve<T>() call inside per-frame, per-hit, or per-tick methods. These MUST use lazy-cached properties instead.
3. **LINQ in Loops:** Flag .ToList(), .ToArray(), .Where().Select() chains inside loops or frequent callbacks
4. **String Concatenation:** Use string interpolation or StringBuilder, not repeated + concatenation
5. **Dictionary Lookups:** Use TryGetValue instead of ContainsKey + indexer (double lookup)
6. **Unnecessary Boxing:** Watch for value types passed as object parameters
7. **Caching Opportunities:** Repeated expensive lookups that could be cached (e.g., race lookups, config reads)
8. **IEnumerable Multiple Enumeration:** Flag any IEnumerable parameter that's enumerated more than once
9. **Closure Allocations in Loops:** Flag lambda/delegate creation inside per-frame loops (RemoveAll with closure, etc.)
10. **Resource Disposal:** IDisposable types properly disposed or in using blocks
11. **Harmony Patch Overhead:** For every `[HarmonyPatch]` class in changed files:
    - Check if the patch target is a hot method (called per-frame, per-tick, per-hit, per-AI-decision)
    - Flag any `IoC.Resolve` not using lazy-cached `??=` pattern
    - Flag any `new List<>`, `new Dictionary<>`, or LINQ chain inside the patch method body
    - Flag any delegate/closure creation that captures local variables
12. **CampaignBehavior Lifecycle Cleanup:** For every `CampaignBehaviorBase` subclass in changed files:
    - Check that `RegisterEvents` has a corresponding cleanup path
    - Flag behaviors that subscribe to events in `OnSessionLaunched` but don't override `OnFinalize` or `OnGameEnd` to unsubscribe
    - Flag any static fields or `static Dictionary` that are populated at runtime but never cleared on session end
    - Flag any collection (List, Dictionary, HashSet) used for persistence/sync/tracking that grows with game events but has no pruning, eviction, or size cap — especially SyncData stores, buff trackers, and per-hero caches
13. **GameModel Override Weight:** For every `GameModel` override in changed files:
    - Identify the override methods and assess call frequency (per-frame vs per-day vs one-time)
    - Flag any service resolution that isn't constructor-injected or lazy-cached
    - Flag any LINQ chain or collection allocation inside override methods called more than once per game tick
14. **GC Pressure Patterns:** Across all changed C# files:
    - Flag `string.Format` or `$""` interpolation inside loops (prefer StringBuilder for >3 concatenations)
    - Flag `params object[]` calls in tight loops (implicit array allocation)
    - Flag `foreach` over `Dictionary.Keys` or `.Values` when only one is needed and the dictionary is large
    - Flag `Enum.ToString()` or `Enum.Parse()` in hot paths (both allocate; prefer lookup dictionaries)

15. **C++ HOT-PATH CHECKS (only if `.cpp` / `.h` files in scope — e.g., `Dependencies/*.NativeHooks/`, `Main/SceneScripts/`, or any other vendored native code).** TAOM C++ runs alongside the engine's render / asset-load / per-agent / per-frame paths. Hot-path I/O or unbounded loops in C++ are AS DESTRUCTIVE as in C#, but the C# checks above don't translate. Treat the following as analogous to the C# hot-path rules and apply with HIGH severity when they fire on per-frame / per-render / per-asset-load / per-agent / per-Face_mesh callbacks:

    - **Logging on the hot path.** Flag ANY `OutputDebugStringA`, `OutputDebugStringW`, `fprintf`, `fputs`, `fputc`, `fflush`, `printf`, `WriteFile`, or wrapping `LogLine` / `LogToFile` / `Log*` helper call inside a function that fires per-frame or per-engine-callback. Each call typically takes a critical section + writes + flushes — thousands per battle load is a visible frame stutter. **Required pattern:** sample-gate with an atomic counter — `if (ShouldSampleLog(&counter)) Log...(...)` — and emit a single summary in the corresponding `Uninstall` / shutdown path. Whitelist: logging inside `*_Install` / `*_Uninstall` / one-time boot paths is fine; logging inside per-call hook bodies is not. Reference: `feedback_native_port_hot_path_audit.md`, RCA `docs/reviews/rca-native-skin-fixes-port-2026-05-26.md` finding #1 (HIGH).

    - **SEH filter overbreadth.** Flag ANY `__except (EXCEPTION_EXECUTE_HANDLER)` block. The filter MUST narrow to specific expected exception classes via `GetExceptionCode()` — typically `EXCEPTION_ACCESS_VIOLATION` for raw pointer dereferences against engine structs. Catch-all filters silently swallow heap corruption, stack overflow, division by zero, and other lethal exceptions that should propagate to the OS crash dumper so we get a real crash report. **Required pattern:** `__except (GetExceptionCode() == EXCEPTION_ACCESS_VIOLATION ? EXCEPTION_EXECUTE_HANDLER : EXCEPTION_CONTINUE_SEARCH)`. Reference: `feedback_seh_filter_specificity.md`, RCA same file finding #2 (MEDIUM).

    - **Non-atomic counters touched from the hot path.** Any static `int` / `long` / `size_t` incremented from a hook body that can fire on multiple engine threads (render, asset-load, AI) must use `InterlockedIncrement64` against a `volatile LONG64`. Plain `++counter` from multiple threads is a data race — sample-log gating breaks silently if the counter rolls back.

    - **SRWLock reader/writer balance.** Verify shared queries use `AcquireSRWLockShared` + `ReleaseSRWLockShared`; mutations use `AcquireSRWLockExclusive` + `ReleaseSRWLockExclusive`. Flag any query path that takes the exclusive lock (false serialisation) or any mutation path that takes the shared lock (UB on concurrent writes).

    - **Unbounded memory iteration.** Flag any `for` / `while` loop walking module memory or large engine buffers without a known upper bound. Examples: `SignatureScanner::FindPattern` walking the whole loaded DLL is acceptable ONLY at boot (once per signature); a similar walk inside a per-frame callback is not. Confirm the call site fires once per process / once per scene / once per battle, not per-frame.

    - **`new` / `malloc` / unbounded `std::vector::push_back` in hook bodies.** Heap allocations on the hot path are equivalent to GC pressure in C#. Flag any `new`, `malloc`, `std::vector::push_back` (on a vector likely to grow), `std::unordered_set::insert` (rehash on grow), or `std::string` operation inside per-frame / per-engine-callback code.

    - **Thread-local storage abuse.** TLS reads (`__declspec(thread)`) are cheap-but-not-free. Flag a TLS access inside a tight loop where caching the value to a local would avoid repeated lookups.

    - **Pattern-scan match validation.** When a byte-pattern scanner returns a match, the consumer should verify the result lands inside an executable section (`.text`) and inside a function prologue (e.g., bytes that look like `push rbp` / `sub rsp, ...` / `mov [rsp+...]`). A 7-byte match in the middle of a data section will produce a JIT crash when MinHook tries to install a trampoline. Flag any consumer that uses the raw match address without sanity-checking it.

    Apply these C++ checks ONLY when `.cpp` / `.h` files are in the changeset. For pure-C# changesets, skip this entire block.

OUTPUT FORMAT:
For each issue found:
- File path and line number
- Issue type (allocation, LINQ, caching, patch overhead, lifecycle leak, GC pressure, C++ hot-path log spam, SEH overbreadth, etc.)
- Severity: HIGH (hot path / per-frame) / MEDIUM (per-tick / occasional) / LOW (startup only)
- Suggested fix, as a code sketch
- Behaviour: PRESERVING (same results, cheaper) or CHANGING (different outcome), and the test that proves it
- Scope: APPLY (in the changed code) or FOLLOW-UP (pre-existing code the change did not modify)

If no issues found, say "NO PERFORMANCE ISSUES FOUND" with a brief summary.
