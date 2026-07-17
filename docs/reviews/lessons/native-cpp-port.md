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

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/LESSONS-LEARNED.md](../LESSONS-LEARNED.md)

<!-- backlinks-end -->
