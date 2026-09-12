# RCA: nine Isengard villages and `add_map_villages.py` (#562), deep-review 2026-09-11

**Summary.** Six review agents on the #562 change set: a new table-driven tool that appends village
rows to the LIVE `TAOM_Map/ModuleData/settlements.xml` and one name row per language, its 26 unit
tests, nine name-registry rows, one README row, one handbook line, the CHANGELOG entry. Standards
and completeness passed; the compatibility agent verified all six engine claims against the installed
v1.4.8 DLLs (both `Deserialize` readers, `DefaultVillageTypes`, `SettlementPositionScript`,
`village_complex`, the nine scene folders) and turned the "new campaign only" caveat into a crash
statement; the data-flow agent traced nine flows clean and named one pre-existing module-dependency
gap. Three findings landed in the tool, one from the efficiency agent and two from the tooling
agent. All three fixed before commit, each with a test, the repair path proven end to end on a scratch
copy of the module (`BANNERLORD_GAME_DIR` pointed at it). Suite 30 green, live `--check` exit 0 after
two editor saves.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH (agent) / LOW (impact today) | `scene_position` searched `<game_entity name="X"[^>]*>.*?<transform position=` under `re.DOTALL`. An entity with no transform of its own would resolve to the NEXT entity's numbers. Every entity in the live scene carries a transform, so nothing was mis-positioned. | Regex window | The test for "entity missing" used an entity absent from the whole snippet, never one present without a transform; the author reasoned from the observed file shape ("the transform always follows the tags"), which is a property of today's file, not of the regex. | Window the search to the entity's own element (`find("<game_entity", open_tag.end())`) and pin it with two tests: no-transform-then-neighbour, and own-transform-before-children. Lesson below. |
| 2 | HIGH | `--apply` planned the loc rows from `missing_ids(master)`, and the "nothing to do" early return fired on that list alone. A master-present/loc-missing state (a run that failed part-way through the 12 languages, a reverted language file) was reported as complete forever, while `--check` (which iterates the whole table) kept failing on it. | Idempotency scope | Idempotency was tested per master id only; the loc side was assumed to follow because both were written in one run. The precedent tool (`add_bluecraig_castles.py`) has no loc side, so the copied shape had no place for the question. | `plan_loc_rows` derives each language's missing rows from the whole table; `nothing_to_do` is true only when master AND every language are complete; a scratch-tree run with one DE row removed proves a re-run writes exactly that file. Lesson below. |
| 3 | MED | The summary line printed `loc rows in 12 languages` regardless of how many were written; languages skipped as already present were silent. | Reporting | The author wrote the success line for the happy path (first run, 12 of 12) and never re-read it against the skip branch two lines above it. | Per-language plan lines in dry run and apply, and a summary that counts written rows and names the languages. |
| 4 | Doc | The plan and the first draft of the issue said a pre-batch save "never sees the villages". `Settlement.Deserialize` keys on the campaign-wide `CampaignGameLoadingType` and on a `SavedCampaign` load runs `Alleys[num].Initialize` on a new object's empty `Alleys` (`Settlement.cs:1024-1031`, `:771`), so the expected outcome is an exception. | Prose accuracy | "New campaign only" was carried over from the handbook as a rule without reading the branch it rests on. | CHANGELOG, issue and the handbook recipe now state the mechanism and the expected throw, marked as read in the decompile and not reproduced live. |

## Root-cause pattern

Findings 1 and 2 share one shape: a guard was written for the failure the author could picture (entity
absent; row absent from the master) and not for the adjacent failure one step away (entity present but
incomplete; row absent from one of twelve secondary files). Both adjacent cases are exactly what a
partially-saved scene or a crashed run produces, which is to say they are the cases a repair tool exists
for. The test that would have caught each is the one that starts from "the thing exists but is wrong",
not "the thing is missing".

## Why each agent missed or caught these

- **Standards (Agent 1)** checks conventions and prose tells; both passed. Out of scope by design.
- **Compatibility (Agent 2)** reviewed the data against the engine readers and found finding 4 by
  reading the `CommonAreas` branch instead of accepting the handbook's line. It does not read Python.
- **Efficiency (Agent 3)** found finding 1 while asking whether the lazy `.*?` on a 12 MB file could
  mis-anchor, then built a synthetic scene to prove it rather than reasoning about it. Its aside that
  the nine entities were "not in scene" was wrong (they resolved from the scene in the same run) and
  was discarded on re-verification.
- **Completeness (Agent 4)** listed `main()`, `_write` and the `--check` exit path as untested, which is
  true and accepted (they were exercised by hand and then by the scratch-tree run), but it did not ask
  whether the tested functions were tested against the right failure shapes.
- **Data flow (Agent 5)** traced ids, bindings, loc keys, culture templates, production items,
  volunteer pools, economy floor and module dependencies; nothing in that brief reads the tool's
  control flow, so findings 2 and 3 were structurally outside it.
- **Tooling correctness (Agent 6)** found findings 2 and 3 by reproducing the partial state against
  the real functions and by reading the skip branch next to the summary line. This is the agent the
  deep-review skill adds for scripts that write outside the repo; the three core agents that read code
  are C#-shaped and would not have asked either question.

## Feedback memories to codify

Two lessons, appended to `docs/reviews/lessons/build-tooling-workflow.md`:

1. A parser window is bounded by the element it parses, not by the next thing that usually follows.
2. A repair tool's "nothing to do" must be computed over every file it writes, not over the primary
   one; test idempotency with the secondary file broken.

Pre-existing, recorded here and in the CHANGELOG, not changed: `TAOM_Map/SubModule.xml` lists `TAOM`
under `DependedModuleMetadatas` (`LoadBeforeThis`) but not under `DependedModules`, so the module that
carries 1,012 `Culture.` references does not hard-require the module that defines them. Load order is
right whenever both are enabled; a follow-up issue is the right vehicle, since the file is in the
unversioned install.
