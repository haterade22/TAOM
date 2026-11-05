# RCA: crash-log readability diagnostics (#481, 2026-08-19)

**Deep review verdict: standards PASS, API compatibility 7/7 verified, efficiency clean, zero
functional data-flow gaps.** All findings were documentation and coverage hygiene, and all were
fixed before commit. This RCA exists because Phase 3e applies to every confirmed finding regardless
of severity, and because the one pattern underneath them is worth naming.

## What the changeset was

A player reported "played for two weeks, now it crashes" and sent `taom_debug_2026-08-19_19-04-50.log`.
The log proved only that the session never started a game: the always-on `[SaveLoad]`, `[MissionDiag]`
and `[Diplomacy]` markers were all absent, so 28 minutes of `[MemSample]` lines were a main menu
sitting idle. It was the relaunch after the crash. Four gaps made it unreadable, and all four were
closed: the engine version was recorded only at `OnGameStart`; nothing marked a clean shutdown, so a
quit and a native CTD produced identical tails; `diag.log` (PatchShield's engine-mismatch evidence)
never travelled in the crash bundle; and log retention of 10 could prune the crash log during the
support round trip.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|-----|---------|----------|------------|-------------------|
| 1 | LOW | `docs/features/crash-report.md` enumerated the bundle as five files; the code now writes six. | Doc drift | The doc was not in the changeset's mental scope. The change was framed as "add a file to the ZIP", and the ZIP's inventory is duplicated in four places: the writer's header comment, the `TryCopyFile` calls, `BuildManifest`, and this doc. Three were updated because they are in the same file; the fourth was not. | When a change alters what an artifact CONTAINS, grep the docs for the artifact's existing inventory before committing. See the lessons entry below. |
| 2 | LOW | Same doc's Logs row described the RGL log as best-effort from `Documents`; the collector has probed `%ProgramData%` first since the dual-root fix. | Doc drift, pre-existing | Not introduced here. Surfaced only because finding #1 put a reviewer on the same table row. | Same as #1. A stale neighbour is evidence the whole section is unverified. |
| 3 | LOW | No test asserted that `report.txt` renders the diag path and tail with real content. `MakeMinimalContext` builds the diag fields as null/empty, so the populated render branch was never exercised. | Test coverage | TDD was followed for the collector and the bundle writer, which is where the new logic lives. The renderer change was two lines mirroring an adjacent block, and mirroring an existing pattern reads as covered by the existing pattern's tests. It is not: the existing tests only assert the `--- Logs ---` header exists. | Added `Render_WithDiagLogContent_SurfacesThePathAndTheSwallowedExceptions`. Generally: a shared minimal-fixture helper that zeroes a field means every test built on it tests the empty branch only. |
| 4 | LOW | My own new comments claimed diag.log is populated when PatchShield swallows something. `Dependencies/SubModule.cs` logs its install sequence unconditionally, so the file exists with content on every healthy session. | Comment accuracy | The comment was written from the file's PURPOSE rather than from its writers. I traced who reads diag.log, never who writes it. | Corrected both comments. An empty diag section is now documented as its own signal. |

## The pattern: an inventory duplicated across files drifts at the copy nobody is editing

Findings 1 and 2 are the same defect. The crash bundle's contents are stated in four places, three of
them inside `CrashBundleWriter.cs`. Editing that file makes three of the four impossible to forget
and does nothing for the fourth. Finding 4 is the same shape one level down: a fact about `diag.log`
stated in two comments, derived from purpose instead of from the code that produces it.

Nothing here was caught by a gate, because TAOM has no gate for this. `lint_docs.py` checks stale
version refs, orphan docs, config drift and dashes; it does not know that a doc sentence enumerates a
ZIP's entries. Building one would mean teaching a linter to parse prose inventories, which is not
worth it. The cheap discipline is the grep, which is why the lesson is a rule rather than a tool.

## Why each review agent missed these

- **Agent 1 (Standards):** correctly scoped to ADRs and architecture. Doc inventories are outside its rule set, and nothing about the changeset breached a standard.
- **Agent 2 (Compatibility):** did its job well, and answered the two load-bearing engine questions the change actually depended on (`ModuleHelper` is populated before `OnSubModuleLoad`; `OnSubModuleUnloaded` is reached only via the engine's orderly `Finalize` callback, so its absence really does mean the process died). Docs are out of scope by design.
- **Agent 3 (Efficiency):** out of scope, and correctly declined to recommend downgrading the new INFO lines to DEBUG once it read `FileLogger`'s durability contract.
- **Agent 4 (Completeness):** CAUGHT findings 1 and 2 with exact line numbers. Working as intended.
- **Agent 5 (Data Flow):** CAUGHT findings 3 and 4, and separately confirmed the highest-risk hypothesis was absent: `JsonCrashReportRenderer` serializes the whole context by reflection, so `report.json` picked the new fields up for free rather than needing a hand-edited field list.

The review did what it exists to do. Two of the five agents found everything, and the two most likely
failure modes for a change like this (a hand-written serializer silently omitting the new fields, and
the retention bump touching crash bundles that share `Logs/`) were both explicitly traced and cleared.

## Feedback memories to codify

One, appended to `docs/reviews/lessons/build-tooling-workflow.md`. No new harness rule: the existing
agent set caught these, and the gap was in the author's pre-review pass, not in the gates.
