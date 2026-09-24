# PROGRESS: Opus review sprint `2026-09-23-opus`

Baseline commit `b2e387db` (HEAD moved from `c79a5852` while the sprint was being planned: another
session committed "Review 130 follow-ups"). Plan of record:
`C:\Users\mikew\.claude\plans\eager-tickling-pizza.md`. Raw logs live in the session scratchpad
(`...\scratchpad\raw\`), not in the repo.

## Status

| Phase | State | Notes |
|---|---|---|
| 0.1 recon, in-flight list | DONE | 80 dirty paths belong to another live session; listed in BRIEF.md |
| 0.2 migration docs | DONE | v1.5.3 bump green at 9,174 passed / 2 skipped; v1.5.2 "Findings outstanding" treated as known |
| 0.3 baseline (worktree `wt-opus-head` at `b2e387db`) | DONE | `baseline.md`: 10,235/2 failed (live-Armory drift)/2 ignored; 2 build warnings; 2,028 hidden nullable warnings; hooks 13 per Bash call |
| 0.4 reconcile 001 to 005 | DONE | W1 `wf_14b8b050-a61`; README rows corrected (001 PARTIAL, 002 PARTIAL, 003 TODO, 004 DONE, 005 TODO); claims spot-checked by the orchestrator |
| 0.5 June harvest re-triage | DONE | Final after the W1b checker (`wf_24b72ab2-cc2`, 4 of 32 overturned to STILL_VALID: L193 PERF-06, L495/L513 DEPS-04 (duplicates), L748 GAMEDATA-03): **21 FIXED, 7 STALE, 71 STILL_VALID (70 distinct), 0 REFUTED; none HIGH today** (checker calibration: SpecialResources storage leak and unpinned MCP servers are the closest, both MED) |
| 1 lanes 1-4 | DONE | W2 `wf_f24840ec-4c0`: 25 findings (lane-1 6, lane-2 8, lane-3 7, lane-4 4); 2 HIGH (COMP-01 bare PatchCategory, PERF-L4-01 boot +30 s). Orchestrator re-measured PERF-L4-01's gap (29-33 s on 6/6 recent boots) and corrected seed F7 (SDK already appends the SHA) in BRIEF.md |
| 1 lanes 5-6 | DONE | W3 `wf_5a726153-66d`: 17 findings (lane-5 9, lane-6 8); 2 HIGH (TEST-L5-01 silent-green binding gate proven by probe; TEST-L5-07 hosted CI). Orchestrator spot-checked GameAssemblies env-only resolve, validate-push protecting only the old trunk, and the four stderr Stop reminders |
| 2b verify lanes 5-6 | DONE | W4b `wf_daaa66c7-2ef`: 17 CONFIRMED, 1 REFUTED (TEST-L5-08: BUTR builds its packages from Steam builds, so no pre-Steam binding verdict is possible). Recalibrations: TEST-L5-01 LOW today (skip-on-absence is a recorded decision; the fix must name it or scope the runsettings to the binding gate and CI); TEST-L5-07 sharpened (no C# compiled on ANY branch; 0 runners; 1.4.5 CI red 82 runs in a row since 2026-09-01) |
| 2a verify lanes 1-4 | DONE | W4a `wf_b6667768-c9d`: 21 of 21 CONFIRMED (0 refuted), many with corrected evidence and impact recalibrated down (COMP-01 MED: a game-init throw is a loud crash report plus a failed load, not silent). PERF-L4-01 correction: the MCM toggle is dead at OnSubModuleLoad (`Instance` null, `?? true`), so no player can switch the 30 s off today; a default flip is Mike's call (docs lean on the sweep). A refuter surfaced an unreported cost: PatchShield pass 2 about 69 s on first campaign load |
| 2a' follow-up | DONE | `wf_b5d4afb4-284`: PatchShield pass 2 = 69.1-69.4 s on 67/67 first game starts since 2026-09-04 (orchestrator re-read diag.log 13:50:31.642 to 13:51:40.999), 247 of its 372 attaches re-shield the Native2Managed shims; fix = exclude `ManagedCallbacks` (plan 007). Machine is in a slow Harmony mode (~186 ms per Patch vs 5-10 ms) since 2026-06-12, cause unknown |
| 2c critic | DONE | `wf_adf6333c-f3c` -> `critic.md`: coverage gaps listed; 6 lane findings never checked (COMP-04/05/06, ARCH-08, CORRECTNESS-03/06); GitHub issue dedupe (#491, #593, #623, #573/#578, #607, #501); one ranking-changing question |
| 2c' follow-up | DONE | `wf_74cdeeed-731`: players FAST (11 player processes, 247 attaches in 0-1 s; orchestrator re-read `Downloads\taom_crash_20260923_010057_2d446100\taom_debug.log:3-4`); slow mode is this desktop only, likely the VS launch profile's mixed debugger (`launchSettings.json:8,17`, UNVERIFIED). REPORT re-ranked: shim patching moves from #1 to #6 |
| 2d REPORT | DONE (draft) | `REPORT.md` written; README and commit after W5 |
| 3 plans | RUNNING | W5 `wf_4979475d-392`: 14 plans (006-019) write, cold review, revise; briefs in `plan-briefs.json` |
| wrap-up (worktree removed, commit) | TODO | |

## Log

- Created run folder, BRIEF.md, this file. Detached worktree added at the scratchpad, moved to
  `b2e387db`. 13 hook processes per Bash call (10 PreToolUse Bash, 1 universal, 2 PostToolUse).

## Overnight extension (Mike, 2026-09-23 evening): "continue throughout the night; run deep reviews and codex reviews on the changes"

Reading: after the sprint (REPORT, plans, plans commit), EXECUTE the plans in leverage order, each on
its own branch, and put every executed change through `/deep-review` and `/review-codex`. Protocol, so
a resumed session follows it without re-deriving:

1. **Isolation:** per plan, `git worktree add -b improve/NNN-<slug> E:\repos\taom-improve\wt-NNN <plans commit>`
   (dependent plans branch from their prerequisite's branch tip; see item 8). Never edit, commit or build in `E:\repos\TAOM`'s working tree (another
   session's uncommitted work lives there). Never merge, never push, never touch `bannerlord-1.5.x`
   except the one `plans/`-only commit.
2. **Executor:** one Opus agent per plan in its worktree, follows the plan, TDD, runs the
   non-deploying build/test (`-p:DisableModuleCopy=true -p:ModuleId=`) inside the worktree, and on
   green COMMITS on its branch (reviews then run on `base..head`; review fixes are follow-up commits). At most 4 subagents in flight at any time (deep-review waves count toward it).
3. **Deep review** (`.claude/skills/deep-review/SKILL.md`): lenses by scope as `deep-reviewer`
   (never pass `model`), waves of four, over the worktree's diff against its base. Step 4:
   PRESERVING proposals applied; behaviour-CHANGING proposals are NOT applied overnight (they need
   Mike's one question) and are listed for him. Fixes are applied in the worktree by a fixer agent
   (the orchestrator's delegate), then one convergence reviewer. RCA (Step 3e) and lessons entries
   are written on the branch.
4. **Codex** (`.claude/skills/review-codex/SKILL.md`): dispatched by the orchestrator via Bash in the
   background from `E:\repos\TAOM` (trusted path, so the repo model pin and MCP apply), prompt telling
   Codex to review `git diff <base>..improve/NNN-<slug>` read-only; prompt and raw output saved into the
   worktree's `docs/reviews/` so they commit with the branch. Findings verified before any fix
   (Phase 3b), fixed by the fixer agent, REVIEW-LOG entry on the branch.
5. **Commit:** on the branch only, after reviews and fixes, full suite green in the worktree; subject
   `type(scope): v2.0.30 - ...`, explicit paths, no attribution trailer; CHANGELOG entry on the branch.
   Then `git worktree remove` (the branch stays) to cap disk.
6. **Skip or stop:** any plan STOP condition, a product decision, or an outward-facing action
   (GitHub rulesets, issues, pushes) is recorded as BLOCKED for Mike, never improvised.
7. **Disk (environment, found 20:30):** C: was at 100% (2.7 GB free). The orchestrator removed only its
   own scratch (baseline worktree, three git-archive extractions made by audit agents, probe binaries):
   C: now about 7 GB free. Execution worktrees therefore live on E: at `E:\repos\taom-improve\wt-NNN`,
   and agents are told never to write archives or large temp output to C:. Mike: C: needs attention.
8. **Machinery (scratchpad):** `x-exec.js` (executors), `x-review.js` (lenses via a 4-slot pool +
   1-slot convergence checker, review lead folding in Codex, second pass on convergence defects),
   `make_codex_prompt.py` (review-codex Phase 2 prompt from the plan's STOP conditions, run by the
   orchestrator from `E:\repos\TAOM` against branch refs, sentinel `END OF CODEX REVIEW`), `scope.py`
   (lens selection per deep-review Step 2). Batches (re-ranked after the patch-tax answer): B1 008, 009, 011, 012; B2 006, 007, 014, 016;
   B3 010 (on 008's branch), 013, 015, 017; B4 018 (on 009's branch), 019; then June remnants if time.

## Next

W1: reconcile + 3 triage agents, then one checker.

## Blockers

None.
