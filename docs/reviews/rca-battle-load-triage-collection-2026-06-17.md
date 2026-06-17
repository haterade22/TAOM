# RCA — battle-load triage tool + stall-marker collection (2026-06-17)

Deep-review (6 agents: Standards, API-compat, Efficiency, Completeness, Data-flow, + a focused
Python-tool agent) of the Tier A (stall marker + next-session notice) and Tier B
(`tools/triage_battle_load.py`) work for the intermittent infinite-battle-load investigation
(issue #262). No HIGH findings. Standards/compat/efficiency/completeness all PASS. All confirmed
findings were LOW/MED on the Python tool's `--bundle` file-selection path; two data-flow notes
resolved as non-defects. All fixed or documented before the closing commit.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|------------|-------------------|
| 1 | MED | `_read_from_bundle` picked the log via substring `"taom_debug" in name`, which also matches a decoy `not_taom_debug.log`; the `or _pick(("taom_debug.log",))` fallback was unreachable dead code. | Tooling — file selection | Tests fed loose log/rgl paths directly; no test exercised the `--bundle` zip path with a decoy member. | FIXED: `_pick_log` anchors on basename `startswith("taom_debug") and endswith(".log")`. New test builds a zip with a `not_taom_debug.log` decoy + real log and asserts the real one is parsed. |
| 2 | LOW | Bundle rgl selection took the first `rgl_log*` member; could grab a sparse `rgl_log_*.txt` over the denser `rgl_log_errors_*.txt`. | Tooling — file selection | Same gap — no `--bundle` multi-rgl test. | FIXED: prefer `rgl_log_errors` then fall back to `rgl_log`. Same new test asserts the errors variant confirms the verdict. |
| 3 | LOW | `BattleLoadPhase.StallWatchdog` (enum member) would fall through to `PRE_SCENE` if it ever appeared as a phase event. | Tooling — robustness | The watchdog writes a raw `WATCHDOG` line (captured as `tl.watchdog`), never an `Emit()` phase marker, so the case is unreachable today — not exercised. | Clarifying comment added at the `PRE_SCENE` fallback documenting why it's unreachable and why `PRE_SCENE` is still the correct fallback if that changes. No code path change (YAGNI). |
| 4 | LOW | A watchdog status line of `phase=<none>` (initial value) parses to `last_phase=""` because `<none>` isn't `\w+`. | Tooling — robustness | Benign — `last_detail` captures the raw status; the watchdog only fires after a phase is logged in practice. | No change. Documented here; `last_detail` preserves the information. |
| — | n/a | Data-flow: marker-write gate (`svc.IsEnabled` in the Mission.Initialize prefix) vs behavior-add gate (`IsEnabled` in `OnMissionBehaviorInitialize`) read the same singleton at two points — flagged INCONSISTENT. | Data flow | Agent didn't know the resolution chain. | NON-DEFECT: both gates resolve `IsEnabled` through the SAME provider/singleton (`BattleLoadDiagnosticsService.IsEnabled` → `IBattleLoadDiagnosticsSettingsProvider` → `BattleLoadDiagnosticsSettings.Instance`). The value is read LIVE on each access (not cached — an in-game MCM toggle takes effect immediately), but because both gates read the one provider they always see the identical value at any instant and move together — they cannot diverge from each other. (Round 2 corrected the earlier "constant within a session / next-launch" wording, which was imprecise; the non-defect conclusion is unchanged.) No code change. |
| — | n/a | `_playableLogged = false` reset in `BattleLoadPhaseBehavior.OnEndMissionInternal` is a no-op (new instance per mission). | Dead code | — | PRE-EXISTING line, not part of this changeset. Left per edit-scope discipline (don't touch unrelated code in a feature PR). |

## Root-cause pattern

Both real findings (1, 2) are the **same class**: the Python tool's pure-function core (parse →
classify → cross-check) was thoroughly unit-tested with synthetic strings, but the **I/O edge — the
`--bundle` zip member selection** — had zero tests, so its substring-matching heuristic went
unscrutinised. This mirrors the long-standing C# lesson that data-flow/integration seams hide the
bugs unit tests miss — here applied to a tool's file-ingestion boundary rather than a C# cross-file
data flow. The fix in both cases is the same: test the boundary with a realistic, adversarial input
(a zip containing a decoy + multiple rgl variants), not just the happy path.

## Why each agent missed these (the two real ones)

- **Standards / API-compat / Efficiency / Data-flow agents** were C#-scoped and didn't review the
  Python tool — correct division of labour; that's why a dedicated Python-tool agent was launched.
- The **Python-tool agent caught both** (Findings 1–3) — the adaptive-expansion call to add a
  focused tool agent worked as intended. The gap was upstream: the **original test suite** (written
  before review) covered only loose-file inputs, not the `--bundle` path. The review closed it.

## Feedback memories to codify

No new always-on rule warranted — this is an instance of the existing "test the integration/boundary,
not just the pure core" principle (already encoded for C# data flow in the deep-review Agent 5 prompt
and `feedback_end_to_end_xml_to_guard_smoke_required.md`). The transferable note: **a CLI tool's
file-ingestion path (zip member picking, newest-file globbing, encoding) is a boundary and needs an
adversarial-input test**, the same way a C# config→consumer chain does. Captured here rather than as a
standalone memory to avoid rule bloat; if a second tool ships a file-selection bug, promote it.

## Round 2 — exhaustive adversarial re-review (2026-06-17)

A second, larger pass on the user's explicit request: a Workflow of **49 agents** — 10 independent
review dimensions (architecture, API-compat, concurrency, lifecycle, failure-modes, py-parsing,
py-classify, test-quality, player-facing, doc-accuracy), each finding handed to an independent
adversarial verifier told to refute it by re-reading the code, plus a completeness critic. **38
findings raised, 20 confirmed real after verification, 18 refuted as NOT_A_BUG, 0 HIGH.** (A first
dispatch died entirely to a server-side burst rate-limit — all 11 agents failed; re-run batched into
waves of 3 with partial-failure tolerance. The rate-limited run produced no findings and was not
mistaken for a clean pass.)

The 18 refuted findings were correctly dismissed: static-field service injection IS the established
Harmony-patch pattern (a `static` class cannot have a constructor); `System.IO` in an IoC service is
fine (`FileLogger` precedent — BCL statics aren't ADR-007 sealed-TaleWorlds types); the ungated
next-session consume is the *safe* choice; the watchdog provably never touches the marker; all marker
I/O is main-thread-serial; cross-process double-consume is harmless; the non-atomic marker write
survives a torn read via the tolerant `Parse`.

| # | Sev | Finding | Fix |
|---|-----|---------|-----|
| R1 | **MED** (completeness critic — missed by all 10 dimensions) | `FileLogger.LogFilePath` is cwd-**relative** (`Logs\…`); it flowed into the marker, the notice body, and `StallReportNotifier`'s `explorer.exe /select` call. `explorer.exe` (a separate process) won't resolve a relative path → the "Open log folder" button (the whole point of the notice) failed. The mirrored precedents diverge: `IncompatibleModDetector` stores ABSOLUTE paths; `CrashNotifier`'s button works only because it's handed the absolute *bundle* path. | `BattleLoadStallMarker.MarkInflight` now `Path.GetFullPath`-resolves the log path at capture (the hung session's cwd = game dir). Test asserts the stored path is rooted. |
| R2 | LOW | `TryConsumeStaleMarker` read→delete→**parse**: if `File.Delete` threw (read-only Logs / AV lock), the already-read content was discarded and the notice never fired. | Parse BEFORE delete; delete is best-effort (a duplicate soft notice beats a dropped hang report). Test holds the marker open without delete-share and asserts the info still surfaces. |
| R3 | LOW | rgl cross-check used an exact set intersection; a missing-**material** line carries the engine-appended `.lodN`, so a base-named suspect mesh never matched → no `EQUIPMENT_CONFIRMED` upgrade. | Normalize `.lodN` (reuse `validate_mesh_refs._base_mesh_name`) on the material side; bodies still match raw. Test drives `Unable to find material for mesh X.lod0`. |
| R4 | LOW | `_EQUIP_OK_RE` truncated apostrophe names (`'Sauron's Lieutenant'` → `Sauron`); `_STATUS_RE` blanked `last_phase` on the `phase=<none>` sentinel. | End-anchor the equip-ok name regex; make the status phase token `(\S+)` with optional seq. Tests for both. |
| R5 | LOW | `_pick` (bundle rgl picker) matched the needle anywhere in the full path (incl. a directory component), unlike the decoy-hardened `_pick_log`. | Anchor `_pick` on the basename. `PickerTests` covers the directory-component decoy. |
| R6 | INFO×several / LOW | The `extra` rgl note ignored materials (now unioned); unused `using TaleWorlds.Core;` (removed); `WrittenUtc` parsed-but-unused (comment clarifies it's informational provenance); misfiled test relocated to `ClassifyTests`; untested `MarkInflight` dir-creation + `PhaseEvent` seq/ms + stray-slot drop (tests added). | All applied. |
| R7 | MED→LOW (doc-accuracy) | Feature-doc test count stale (26 → now 36) + missing `BattleLoadStallMarkerTests` bullet; CHANGELOG "20 tests" → 28; issue-template rgl path (Documents-only) and Logs-folder location wrong for ProgramData/OneDrive installs; feature absent from `docs/INDEX.md`. | Doc counts/inventory corrected; template names both rgl locations + the correct `bin\Win64_Shipping_Client\Logs` + the in-game button; INDEX entry added. |
| R8 | LOW (related, separate feature) | `CrashReport.LogTailCollector.FindLatestRglLog` probed MyDocuments only → empty `rgl_log.txt` in the crash bundle on ProgramData/OneDrive-redirected installs (verified: MyDocuments is OneDrive-redirected here with no `logs` subdir). | ProgramData-first, then MyDocuments, newest across whichever exists. Out of the original changeset but directly serves the diagnosis goal; called out as a related fix. |

**Round-2 root-cause pattern.** The single material defect (R1) was a **cross-cutting path-correctness** issue — a relative path flowing unchanged from `FileLogger` through three consumers to an out-of-process `explorer.exe` call — that no single-dimension lens caught precisely because each dimension reviewed its own slice; only the completeness critic, asked "what did all 10 miss," traced the value end-to-end. Lesson reinforced: **a value that crosses a process boundary (handed to `explorer.exe`, a shell, another exe) must be absolute**; mirror the precedent's path handling (`IncompatibleModDetector` absolute), don't half-inherit it. Everything else was robustness/accuracy/test-coverage polish. Still 0 HIGH; the diagnostic log itself is always captured correctly — only the convenience of locating it from the popup was degraded, now fixed.

Localization debt (R7-adjacent): the 3 new `taom_bld_stall_*` keys still owe a `tools/translate_with_claude.py` propagation run to the 11 AI-translated languages (graceful English fallback until then) — tracked, not yet run.
