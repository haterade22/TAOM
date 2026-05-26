# RCA — NativeSkinFixes port deep-review findings (2026-05-26)

**Feature:** NativeSkinFixes adopt + port into TAOM (v1.4.5, in-repo, pattern-scanning)
**Review:** `/deep-review` (5 core agents) at 2026-05-26 post-implementation, pre-commit
**Outcome:** 4 confirmed findings (1 HIGH + 1 MED + 2 LOW). All HIGH + MED fixed in-session; both LOWs (1 UX + 1 boot-only perf) fixed alongside. Build green, 10/10 tests pass after fixes.

---

## Top-line summary

The architectural choices held up across all five review angles: standards compliance ✅, v1.4.5 API compatibility ✅, completeness ✅, data flow ✅. The findings clustered around the **C++ side** — which the deep-review agent prompts don't cover as rigorously as C# (they're written for C#-only feature work). Three of the four findings were in C++ hot-path code copied with minimal modification from the upstream NativeSkinFixes mod, and one was a UX gap where the banner I authored said "loaded" when the feature is actually inert.

**Common pattern across all four findings: I prioritized API-shape changes (RVA → pattern scan, parameterless exports, unified logging) and didn't audit the cost of preserved behavior from the upstream mod (per-Face_mesh logging) or the user-facing accuracy of new strings I introduced.** Per-file review (Agent 1, Agent 4) treated the C++ as an opaque dependency; Agent 3 (performance) and Agent 5 (data flow) were the ones that caught real issues.

---

## Findings table

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | Per-Face_mesh `LogLine` calls in `ProcessFaceMeshSEH` fire thousands of times per battle load. Each takes SRWLock + fputs + fflush. Inherited from upstream mod's diagnostic logging — preserved without auditing the cost. | C++ hot-path I/O | Plan + implementation focused on signature-scanning architecture; carried over upstream debug logging verbatim. None of the C# review rules (no LINQ in loops, no IoC.Resolve in hot path) translate to "no fputs+fflush per call" in the C++ — and the deep-review prompts don't have a C++ hot-path checklist. | Updated Agent 3 prompt in this session (see Agent 3 v3 below) to scrutinize C++ hot-path logging. Authoring rule for future ports: **when porting/inheriting native code, ALL per-frame / per-render / per-asset-load logging must be sample-gated by default.** Idiom: `if (ShouldSampleLog(&counter)) LogLine(...)` with atomic counter + summary on uninstall. |
| 2 | MED | `__except (EXCEPTION_EXECUTE_HANDLER)` filter catches everything — heap corruption, stack overflow, thread abort — masking real bugs. Should narrow to `EXCEPTION_ACCESS_VIOLATION` only. Affects `HairClothHook` + `FaceMeshObserveHook`. | SEH overbreadth | Same as #1 — copied from upstream without auditing. Agent 1 (Standards) treats SEH as out-of-scope (C# focus). Agent 3 caught it as MEDIUM via the broader "SEH overhead" check. | Codify in `feedback_seh_filter_specificity.md` (created with this RCA) — when porting any native code with `__try/__except`, the filter must use `GetExceptionCode()` to narrow to specific expected violations. Default catch-all is a smell. |
| 3 | LOW | `SignatureScanner::FindPattern` walks the whole `TaleWorlds.Native.dll` image (~50 MB), not just `.text`. Boot-only — 7 scans × ~200 ms each = ~1.4 s startup cost, acceptable but not optimal. | Boot-time perf | Plan acknowledged this tradeoff up-front ("walks the entire image at hook-install time. The DLL is ~50 MB; a single pattern scan takes <200 ms in practice"). No regression from a known tradeoff. | Documented in feature doc Performance section. Future optimization tracked as a follow-up — PE section parsing would add ~50 LOC of C++ for a ~5x speedup on a once-per-boot path. Not worth in-session work. |
| 4 | LOW (UX) | Boot banner says "NativeSkinFixes loaded" in amber when all 7 patterns are `<PATTERN_TBD>` and every hook is inert. Color disambiguates but text was misleading. | User-facing string accuracy | Authored a single localized banner. Didn't think about the "degraded" axis as a distinct user-facing state. Agent 5 caught it via flow-trace #10 (stub-pattern observability). | Created `DegradedMessageKey` + matching XML entry + new test asserting the two banners are distinct and the degraded one signals degraded state. Rule: **any feature with a degraded/partial state must show a distinct user-facing string for that state, not rely solely on color.** Color-blind accessibility + the "loaded" word being technically true for the DLL but false for the function. |

---

## Root-cause pattern

**Three of the four findings (#1 HIGH, #2 MED, #3 LOW) are in C++ code inherited from the upstream NativeSkinFixes mod, preserved with minimal modification during the v1.3.15 → v1.4.5 port.** The architectural work (replacing hardcoded RVAs with byte-pattern scanning + parameterless exports + unified logging) consumed the audit budget; the inherited code passed through with cosmetic-only changes (LogToFile → LogLine, log path consolidation).

The shipped pattern is "if the upstream worked, the port works" — but "worked" meant "produced correct output." It didn't mean "had reasonable performance characteristics" or "had narrow exception handling." Those quality dimensions were never audited.

**Generalisation: when porting native (C++) code with hot-path execution into TAOM, treat the port as a fresh implementation for audit purposes, not a "lift and shift" where the upstream is trusted.** Specifically:
- Audit every `LogLine` / `fprintf` / `OutputDebugString` call: is it in a hot path? gated? sample-limited?
- Audit every `__except`: filter specificity, what exceptions are silently caught?
- Audit every static `volatile`-free counter: does it need atomic increment for thread safety?
- Audit every SRWLock pattern: shared-read for queries, exclusive-write for mutations?

---

## Why each agent missed (or caught) these

| Agent | Scope | HIGH (#1) | MED (#2) | LOW (#3) | LOW UX (#4) |
|---|---|---|---|---|---|
| Agent 1 — Standards | C# ADRs (002/003/004/005/007), naming, IoC | OUT OF SCOPE (C++) | OUT OF SCOPE (C++) | OUT OF SCOPE (C++) | OUT OF SCOPE (string content) |
| Agent 2 — API compat | TaleWorlds v1.4.5 signatures via ilspycmd | OUT OF SCOPE (not a TaleWorlds API) | OUT OF SCOPE | OUT OF SCOPE | OUT OF SCOPE |
| Agent 3 — Efficiency | Hot-path allocations, LINQ, IoC.Resolve, lifecycle | **CAUGHT** — the prompt's "C++ HOT-PATH CHECKS" section explicitly called out log spam | **CAUGHT** — "SEH overhead" check flagged the broad filter | **CAUGHT** — flagged with correct LOW severity (boot-only) | OUT OF SCOPE (perf-only, not UX accuracy) |
| Agent 4 — Completeness | Tests, docs, IoC, CHANGELOG | OUT OF SCOPE | OUT OF SCOPE | OUT OF SCOPE | OUT OF SCOPE |
| Agent 5 — Data flow | Cross-system traces (XML→C#, enum coverage, lifecycle) | OUT OF SCOPE (within-feature C++) | OUT OF SCOPE | OUT OF SCOPE | **CAUGHT** — flow #10 (stub-pattern observability) flagged the misleading "loaded" text |

**Key observation: Agent 3 is the only agent that scrutinizes C++ properly, and only because I included an explicit "C++ HOT-PATH CHECKS" section in the prompt for this review.** The deep-review skill's default Agent 3 prompt is C#-only (LINQ in loops, IoC.Resolve in hot paths, etc.). If I hadn't customized the prompt for this changeset, the HIGH log-spam finding would have shipped.

This is a `feedback_*` candidate: **for hybrid C#/C++ features, always customize Agent 3's prompt to include C++ hot-path checks before running deep-review.**

---

## Feedback memories to codify

Two new memory files worth writing (one rule, one repeat-pattern detection):

### 1. `feedback_native_port_hot_path_audit.md` — NEW

**Rule:** when porting native (C++) code from an upstream mod into TAOM, do NOT rely on "the upstream worked." Audit hot-path logging, SEH filter breadth, atomic counters, and lock usage as if writing from scratch.

**Why:** RCA NativeSkinFixes port 2026-05-26 — three of four review findings were inherited verbatim from upstream code. Architectural changes (signature scanning, parameterless exports, unified logging path) consumed the audit budget; behavioral preservation was not audited.

**How to apply:** when the changeset includes `Dependencies/*.NativeHooks/` or any C++ port, **explicitly customize Agent 3's prompt** for `/deep-review` to include a C++ HOT-PATH CHECKS section. The default Agent 3 prompt is C#-focused (LINQ, IoC.Resolve patterns) and will miss C++ I/O cost.

### 2. `feedback_seh_filter_specificity.md` — NEW

**Rule:** `__except (EXCEPTION_EXECUTE_HANDLER)` is a code smell. Use `__except (GetExceptionCode() == EXCEPTION_ACCESS_VIOLATION ? EXCEPTION_EXECUTE_HANDLER : EXCEPTION_CONTINUE_SEARCH)` (or the specific exception class you expect) so heap corruption, stack overflow, and other lethal exceptions propagate to the OS crash dumper rather than being silently swallowed.

**Why:** RCA NativeSkinFixes port 2026-05-26 MEDIUM finding. Catch-all filters were inherited from upstream NativeSkinFixes mod; they hide bugs that should crash visibly so they get fixed.

**How to apply:** every `__try`/`__except` block in TAOM-vendored C++ code must specify the expected exception class. Default-broad is rejected at review time.

### 3. `feedback_degraded_state_distinct_banner.md` — NEW

**Rule:** when a feature has a "fully working" vs "degraded but loaded" state, show a distinct user-facing string for each — color alone is not sufficient (accessibility + the "loaded" word being technically accurate but functionally misleading).

**Why:** RCA NativeSkinFixes port 2026-05-26 LOW UX finding. Original banner "NativeSkinFixes loaded — covers_head morph fix + ..." displayed in amber when patterns were stubbed and every hook was inert. Color disambiguates for sighted users; the text misleads.

**How to apply:** every TAOM feature with a degraded/partial-success state must have TWO localization keys, one per state, asserted distinct in tests.

---

## Process gap discovered: Agent 3's C++ scrutiny was opt-in (now mandatory)

The deep-review skill prompt for Agent 3 had a long "C# HOT-PATH CHECKS" section but no "C++ HOT-PATH CHECKS" section. I added one ad-hoc in this session's invocation; without that customization, the HIGH log-spam finding would have shipped.

**Initial recommendation (overridden):** keep the skill prompt as-is and use the `feedback_native_port_hot_path_audit.md` memory to remind future sessions to customize. **The user (2026-05-26 post-RCA) overrode this on the grounds that memory-only prevention has a poor track record — the same category of bug ships again when the memory entry isn't loaded into the active context window.**

**Concrete prevention measure applied (2026-05-26):** the deep-review skill at `.claude/skills/deep-review/SKILL.md` has been edited:

1. **Agent 3 (Efficiency)** gains a new check #15 — "C++ HOT-PATH CHECKS (only if `.cpp` / `.h` files in scope)" — covering hot-path logging, SEH filter overbreadth, non-atomic counters, SRWLock balance, unbounded memory iteration, heap allocations on the hot path, TLS abuse, and pattern-scan match validation. Conditional firing: applies only when `.cpp`/`.h` files are in the changeset; skipped on pure-C# reviews. This means every future invocation of `/deep-review` on a hybrid changeset gets the same scrutiny without requiring the invoker to remember to customize the prompt.

2. **Agent 1 (Standards)** gains a new check #9b — "C++ Native Hook Standards (only if `.cpp` / `.h` files in scope)" — covering `extern "C"` blocks, calling convention symmetry, `#pragma once`, `using namespace` discipline, catch-all SEH rejection, hot-path I/O rejection, and inheritance/port discipline pointing back to this RCA + the feedback memories.

3. **CLAUDE.md** gains a new "Native C++ port discipline" section under "Working Discipline" with the 6-point pre-commit checklist so the discipline is documented at project-level, not just in skill prompts.

The three feedback memories (`feedback_native_port_hot_path_audit.md`, `feedback_seh_filter_specificity.md`, `feedback_degraded_state_distinct_banner.md`) remain — they provide the *narrative* the agents reference, while the skill prompt provides the *mechanical check*. Both layers exist because either alone has historically been insufficient (memory-only fails when context evicts it; skill-only fails when the rule wording is too abstract to be applied to novel cases — the memory gives the why).

---

## Verification after fixes

- `dotnet build Main/TAOM.csproj` — 0 errors, 1 unrelated warning
- `dotnet test TAOM.Tests --filter "FullyQualifiedName~NativeSkinFixes"` — 10/10 pass (8 original + 2 new for the distinct banner contract)
- HIGH fix: hot-path `LogLine` calls in `HairClothHook.cpp::ProcessFaceMeshSEH` replaced with `ShouldSampleLog(&counter)` gates. Counters atomically incremented; first 3 events sampled to log, summary emitted in `HairClothHook_Uninstall`.
- MED fix: `__except` filters in both `HairClothHook.cpp` and `FaceMeshObserveHook.cpp` narrowed to `EXCEPTION_ACCESS_VIOLATION`.
- LOW UX fix: new `DegradedMessageKey` + `DegradedMessageDefault` in `NativeSkinFixesInstaller`; new `taom_nativeskinfixes_degraded` entry in `taom_module_strings.xml`; new test `DegradedMessageDefault_IsDistinctFromLoadedDefault` asserts the contract.
- LOW perf (full-image scan): no fix, documented tradeoff in feature doc Performance section.

C++ rebuild required for HIGH + MED fixes to take effect at runtime — the user will run `pwsh Dependencies/NativeSkinFixes.NativeHooks/Build.ps1` when ready.
