# Lessons — Native C++ Port

> Category file of the master lessons record — index + house shape: [LESSONS-LEARNED.md](../LESSONS-LEARNED.md). **Append new Native C++ Port lessons HERE** (`### rule` → `**Why missed:**` → `**Prevent:**` → `**Source:**`).

### Audit a vendored C++ port from scratch — "upstream worked" only means "produced correct output"
When the changeset includes `Dependencies/*.NativeHooks/` or any C++ port from an upstream mod, do NOT rely on "the upstream worked" for perf or safety. Audit four things as if writing from scratch: (1) every `LogLine` / `fprintf` / `OutputDebugString` on a per-frame / per-render / per-asset-load path MUST be sample-gated (atomic counter + summary on uninstall) or removed; (2) every `__try`/`__except` filter must use `GetExceptionCode()` to narrow to the specific expected violation (catch-all `EXCEPTION_EXECUTE_HANDLER` is a code smell); (3) every static counter touched from the hot path must be `volatile LONG64` + `InterlockedIncrement64`; (4) every SRWLock must use shared-read for queries, exclusive-write for mutations (verify reader/writer balance).
- **Why missed:** The architectural work (signature scanning, parameterless exports, unified logging path) consumed the audit budget; behavioral preservation was not audited. Three of four `/deep-review` findings (1 HIGH + 1 MED + 1 LOW) on the NativeSkinFixes port were inherited verbatim from the upstream Nexus mod. The HIGH was per-`Face_mesh` `fputs + fflush` log spam — thousands of writes per battle load. The default deep-review Agent 3 prompt is C#-focused (LINQ-in-loops, IoC.Resolve-in-hot-paths) and will NOT catch C++ I/O cost — it's opt-in scrutiny.
- **Prevent:** When invoking `/deep-review` on a changeset with vendored C++, customize Agent 3's prompt to include a "C++ HOT-PATH CHECKS" section (exact section text in the RCA). Track as a skill improvement: `.claude/skills/deep-review/SKILL.md` Agent 3 prompt could gain a `[IF C++ FILES IN SCOPE]` conditional block.
- **Source:** memory/feedback_native_port_hot_path_audit.md (RCA findings #1 + #2 + #3) + `docs/reviews/rca-native-skin-fixes-port-2026-05-26.md`

### Narrow every SEH filter to the specific expected exception class — never `EXCEPTION_EXECUTE_HANDLER`
Every `__try`/`__except` block in TAOM-vendored C++ must specify the expected exception class via `GetExceptionCode()`; default-broad `EXCEPTION_EXECUTE_HANDLER` is rejected at review time. `EXCEPTION_ACCESS_VIOLATION` is typically the only expected exception in pointer-arithmetic hooks — gate on `GetExceptionCode() == EXCEPTION_ACCESS_VIOLATION ? EXCEPTION_EXECUTE_HANDLER : EXCEPTION_CONTINUE_SEARCH`. Let lethal exceptions (`EXCEPTION_STACK_OVERFLOW`, `EXCEPTION_INT_DIVIDE_BY_ZERO`, `EXCEPTION_FLT_*`, language-level C++ exceptions) propagate to the OS crash dumper so a real crash dump is produced.
- **Why missed:** Upstream NativeSkinFixes used catch-all filters in both `HairClothHook::ProcessFaceMeshSEH` and `FaceMeshObserveHook::HookedRenderListBuild`. Real bugs (heap corruption from a stale `Face_mesh` pointer, stack overflow from recursion in the cloth factory under a custom mod stack) would be silently logged and continued past — Bannerlord limps on with corrupted state and the eventual crash report points at the wrong file. Upstream mods routinely ship catch-all because the author was prototyping; TAOM ships production.
- **Prevent:** Grep for `EXCEPTION_EXECUTE_HANDLER` in any C++ file under `Dependencies/*.NativeHooks/` or future vendored native code — every hit must have an accompanying `GetExceptionCode() == ...` gate. When porting, always check the filter. Test the narrowing: deliberately throw an unexpected exception class inside `__try` and verify the OS crash dumper picks it up.
- **Source:** memory/feedback_seh_filter_specificity.md (RCA finding #2) + `docs/reviews/rca-native-skin-fixes-port-2026-05-26.md`

### Identifying a native function by its BODY is not enough — a shared-body sibling will fool you; verify the ARGUMENT SIGNATURE
When porting a native hook target to a new engine build, a structural body-match (this function has the offsets / call pattern / constants the hook expects) is necessary but NOT sufficient. Optimizing compilers emit near-identical bodies for a family of related functions (a public entry + its inlined/outlined helpers, or per-type specializations), so more than one function can match the body fingerprint. Before shipping a hook, disassemble the candidate's prologue and confirm the ARGUMENT you dereference is actually a pointer of the expected type — i.e. trace which register (rcx/rdx/r8/r9) the function itself dereferences and dispatches on. Prefer INTERIOR BYTE TRIANGULATION (slide a wildcarded window over the whole reference-build function, scan each in the new build, take the mode of `newRVA − windowOffset`) over a single-point body match: it votes across the entire function and is far less ambiguous (166 votes vs a mis-picked sibling).
- **Why missed:** The v1.4.6 NativeSkinFixes port pinned `cloth_factory` at `0x35AF00` because its body replicated the HairCloth hook's exact cloth-registration writes (type dispatch, `+0x1E8/+0x208` lists, cloth-ctor call). But `0x35AF00` is an adjacent sibling that shares that body and takes `rdx` as a BYTE FLAG (`movzx r14d,dl`), not the mesh pointer the 1.3.15 factory (and our hook) expect. In-game the hook received `rdx` = small integers (0x18/0xD/0x1D) where it dereferenced a `Face_mesh*` → per-call AV. The SEH caught them (no CTD) but the feature was inert and spammed `sample-AV`. The real factory `0x35B0C0` does `mov rax,[rdx]; call[rax+0x28]` — it dereferences `rdx` as the mesh. Static verification (patterns single-match at expected RVAs, offsets confirmed) all PASSED for the wrong function, because I never verified the calling signature.
- **Prevent:** (1) For every hook target, add a one-line "signature" assertion to the disasm workflow: which arg register is dereferenced first, and is it the pointer type the hook casts it to? (2) Reach for interior triangulation, not single-point structural matching, whenever a build changes prologues (`tools/native_sig_author.py` has both). (3) Treat "all 7 patterns single-match at expected RVAs" as necessary-not-sufficient — the definitive gate is the in-game log showing `sample-processing` with real pointers, never `sample-AV`. The `Signatures.h` comment for `cloth_factory` carries the full RCA.
- **Source:** `docs/features/native-skin-fixes.md` ("v1.4.6 native port" → RCA) + `Dependencies/NativeSkinFixes.NativeHooks/Signatures.h` (kClothFactory comment), 2026-06-30.

### Name a native budget's trigger and its target separately (2026-10-02)
The engine's on-demand clip budget float is 12 MiB, and the feature doc said the engine holds 12 MiB before evicting. The loader compares the total with `0xF00000` (15 MiB) and only then schedules the eviction pass, so totals between 100% and 125% are normal. The review's fix then said each pass trims the total to 12 MiB or less, which the binary does not guarantee: the pass (`FUN_18021dea0`, entry `0x21DEA0`) measures the excess over the budget once, when it starts, and evicts only idle loaded clips (state 2, no users) until it has freed that much. Clips in use, and loads that finish during the pass, can leave the total above 12 MiB.
- **Why missed:** the doc was written from the constant the eviction pass reads, not from the compare that schedules it, although the engine note recorded both; the correction read the target from the subtraction and did not read the loop's exit and skip conditions.
- **Prevent:** for any native limit, record every constant compared with the same counter (trigger, target, hysteresis) before stating what the limit means to a reader, and state what the enforcing loop skips and when it stops before calling the target a guarantee.
- **Source:** `docs/reviews/rca-anim-memory-probe-2026-10-02.md` C4.

### A diagnostic meant to decide a lever lists what it cannot see (2026-10-03)
The `[AnimMem]` docs and plan said a total far below 12 MiB "rules out" both clip levers. It speaks to the budget lever only. A type 2 clip starts unloaded and loads on its first sample whatever the total (the acquire path, `0x474140`, never reads the counter), and the probe samples once a second in `OnMissionTick`, after the frame's `WaitTickCompletion`, so a load that delayed a frame has ended before `loadingNow` is read.
- **Why missed:** every native claim was checked for truth and none for what the measurement can reject; the stated purpose ("decides between the levers") was copied into the reading guide.
- **Prevent:** when a log is meant to choose between levers, write per lever the observation that would reject it, and mark each lever the log cannot reject. Say where the sample sits in the frame (the engine's tick order): a point sample misses a wait that ended before it.
- **Source:** `docs/reviews/rca-anim-memory-probe-2026-10-02.md` X1.

### A safety comment names what is checked, what is trusted and what is outside the checks (2026-10-03)
The probe's summary said every address is proven inside a mapped section before any read. Only the two targets are checked against the section table; the header page and the code copy are read on trust; the module is assumed to stay mapped; and `IsAnyAnimationLoadingFromDisk` walks a native list with no lock, outside every check (`0x6EAAE0` holds no call, lock-prefixed instruction or thread check; what can grow the list was not traced).
- **Why missed:** the comment was written for the arming path, which the reviews read closely, and it covered a class that also forwards an engine call nobody traced; the rollout cost sat in the review record and not in the doc that ships the default.
- **Prevent:** write a safety comment as three lists (checked, trusted, outside the checks) and give each forwarded engine call its own entry. A doc that ships a diagnostic on by default says what was not measured and how to measure it.
- **Source:** `docs/reviews/rca-anim-memory-probe-2026-10-02.md` X3.

### A native guard covers every reservation of the same shape in the function, not only the one upstream named (2026-10-08)
- **Why missed:** the skeleton-buffer guard was ported from yotthani's fix for one `lock xadd` reservation
  (`0x69D1C`). TAOM re-derived that site's missing bound with Ghidra and stopped there; the same function first
  reserves from a second pool (`0x6AF58`, 262,144 entries) with the identical flaw, which the review's engine lens
  found by disassembling the whole function.
- **Prevent:** before guarding or documenting a native defect, list every instruction of the same shape in the
  function (and in the callees it shares data with), not only the site the source named; record each as guarded,
  watched or out of scope.
- **Source:** `docs/reviews/rca-yotthani-adoption-2026-10-08.md` finding 1.
- **Recurrence (2026-10-08, the same day):** the fix round's survey found that the pool belongs to an allocator: the
  global `0xD9D160` holds at least eleven pools with the same missing bound, two more on the same draw path. Survey the
  allocator the pool belongs to, not only the function (finding 23).

### A native call's side effects decide when it may run: read what the engine checks next on that path (2026-10-08)
- **Why missed:** the map-view release was ported with upstream's timing, on the cover. Nobody read what
  `SceneView.ClearAll` writes besides freeing memory: it zeroes the view's ready word (`0x34B769`), and
  `MapScreen.OnPause`, which runs right after the layers deactivate, reads that word and raises the global loading
  window over the covering screen. The feature doc's own in-game step had flagged the effect as unverified.
- **Prevent:** before calling an engine method from an event, list the state it writes (decompile the native body for
  an engine call) and read the code that runs next on the same path; an effect a doc marks "unverified" is checked
  before the change is called done.
- **Source:** `docs/reviews/rca-yotthani-adoption-2026-10-08.md` finding 19.

### A count in a reference doc names its method and is taken two ways (2026-10-08)
- **Why missed:** a byte scan for RIP-relative references to `0xD9D160` tried every offset with three instruction
  lengths and counted each reference three times (630 instead of 210); the number went into the engine doc straight
  from the script.
- **Prevent:** take a count two independent ways (a disassembler sweep with skipdata and a raw displacement scan)
  and write the method beside the number; a single ad hoc scan is a lead, not a fact.
- **Source:** `docs/reviews/rca-yotthani-adoption-2026-10-08.md` finding 12.
- **Recurrence (2026-10-08, fix round):** the cull's MCM hint gave yotthani's 1,002 settlements as TAOM's count (the
  live `TAOM_Map` file holds 1,040), and both map-view hints stated his measurements as fact. Player-facing text
  carries only counts TAOM took, with their basis, or attributes the figure (finding 26).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/LESSONS-LEARNED.md](../LESSONS-LEARNED.md)

<!-- backlinks-end -->
