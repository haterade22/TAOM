# RCA: Enlistment service menu after a load-time discharge (#828, #573, deep-review, 2026-10-19)

## Top-line

`/deep-review` (seven lenses in two waves: Standards, Engine compatibility, Data flow, XML, then
Efficiency, Completeness and Design) on the #828 change found **1 MEDIUM and 12 LOW** findings, plus
nits. The change makes Enlistment's service wait menu condition `args => true`, gates each option on
`IsServing`, and adds one leave option, "Go on your way", for a record that a load-time discharge has
already ended. Every finding was fixed in the same session before the first commit; one convergence
reviewer then checked the applied fixes (see "Convergence pass").

Two findings mattered. First, the in-game checklist expected the new exit after a load with the MCM
switch off and the column in a town. That never happens: the discharge itself runs
`PlayerEncounter.Finish(true)` during `OnGameLoaded`, and with no menu context `Finish` clears
`MapStateData.GameMenuId` (v1.5.5 `PlayerEncounter.cs:988-991`), so the save loads onto the map with
no service menu. Second, the first exit design had four landing defects, all reachable only after a
failure, and all of the same root: an `isLeave` option on a wait menu has already ended the wait when
its consequence runs, and each failure branch treated "do nothing" or "exit" as safe without asking
what menu state the earlier steps had left.

## Findings

| # | Sev | Finding | Category | Why missed | Fix and preventive action |
|---|---|---|---|---|---|
| F1 | MEDIUM (Engine compat, Data flow) | The owed in-game check expected "Go on your way" after loading in a town with the switch off; the discharge's `Finish(true)` clears the saved menu id at load, so he lands on the map. The town route is reached only through the identity guard from a save with no live encounter. | Test plan not traced against the load path | The plan reasoned from the defect report (#573) forward and never walked the discharge's own release branch during `OnGameLoaded`. | Checklist rewritten per load shape, with the expected ERROR line for the town case. Lesson appended to `lessons/campaign-mechanics.md`. |
| F2 | LOW (Engine compat) | The still-serving branch returned without effect, but the engine had already called `EndWait()` before the isLeave consequence (`GameMenu.cs:284-287`): the wait stopped and the stale exit stayed drawn. | isLeave consequence semantics | The option comment documented `EndWait` for the happy path only. | The branch re-enters the menu with `SwitchTo` (restarts the wait). Same lesson. |
| F3 | LOW (Data flow) | The map route ignored a live battle encounter. | Missing guard | The route was keyed on settlement presence only. | A map event skips the settlement branch; a still-live encounter is logged before the exit. |
| F4 | LOW (Data flow) | The abandon path relied on `Finish(true)` to walk him out, which needs an active party; both sibling paths walk out explicitly. | Inconsistent copy of an existing sequence | The sibling order in `DischargeService` was not reused. | One sequence: finish the encounter we made, then `LeaveSettlement` while still inside. |
| F5 | LOW (Data flow) | A failed walk-out still exited to the map, the stranded-in-a-town shape (#510). | Failure branch exits blindly | Each branch assumed the menu was still up. | On walk-out failure the service menu is re-opened (`EnsureMenuOpen` is a no-op when it is up). |
| F6 | LOW (Design) | The landing was split across an enum, two service methods and a presenter switch, so the two failure paths walked out differently. | Structure hid an inconsistency | The plan placed routing in the presenter by analogy with shore leave. | One action-service method, `LeaveAfterDischarge`, reads top to bottom. |
| F7 | LOW (Standards) | `EnlistmentMenuBehavior.cs` grew from 162 to 164 lines (ADR-002 limit 150). | Entry point size | A two-line explanatory comment. | Trailing comment and an orphan comment moved: 157 lines. The split remains owed. |
| F8 | LOW (Standards) | The new walk-out polarity (`forcePlayerOutFromSettlement: true`) had no service test; one test ran the failure path while named for success (an unstubbed NSubstitute bool returns false). | Test gaps | Default substitute values were not checked against each test's intent. | Branch tests with explicit stubs, including an in-order test. |
| F9 | LOW (Standards) | The wiring test accepted an inverted gate (`!_actions.IsServing` contains `_actions.IsServing`). | Test too loose | A substring check. | The test also forbids the inverted form in service options; the talk option's gate moved to its final return. |
| F10 | LOW (Design) | The body text gated on the store while the options gated on `IsServing`. | Two predicates for one fact | Written in different files. | Both read `IsServing`. |
| F11 | LOW (Completeness) | Stale docs: `doc-lookup.md` still called #573 an open gap; the options table, the #511 analysis and a historical note in `enlistment.md` said the menu has no isLeave option. | Docs not swept | The plan listed only the #573 paragraph. | All four corrected. |
| F12 | LOW (Completeness) | The null settlement-menu branch (a hideout) had no test. | Test gap | Not enumerated. | Tested. |
| F13 | LOW (XML) | The English strings file was written with LF endings in this worktree; the index blob is LF, so the commit is unaffected. | Byte hygiene | An edit tool. | No action; noted. |

Follow-ups, outside this change: four docs (`TRANSLATOR_GUIDE.md`, `.claude/rules/xml-data.md`,
`lessons/xslt-moduledata.md`, `docs/modding/strings-and-localization.md`) say the enlistment language
files use plain CRLF, but every one is `\r\r\n` since 2026-09-17. The ADR-002 split of
`EnlistmentMenuBehavior.cs` still has no issue.

## Convergence pass

One reviewer checked the applied fixes and walked every branch of `LeaveAfterDischarge` against
v1.5.5. It found two LOW text defects, both fixed. A live encounter with no map event was logged as a
harmless warning, but nothing advances it and it blocks new encounters, so that case now logs an
ERROR (the map-event case keeps its warning, with a test for each). Three places claimed that every
path which keeps the menu re-enters it; only the still-serving path does, and a failed walk-out only
makes sure the menu is on screen. Its nits (talk-option comment, interface summary, lesson tense, one
long doc line) were also applied. Whether "Go on your way" should finish a battle-shaped encounter with
no map event is a separate design question, left for the maintainer.

## Root-cause pattern

F2, F4 and F5 share one cause: a menu consequence that can fail part way must know what menu state each
earlier step leaves behind. An isLeave consequence on a wait menu starts with the wait already ended;
`PlayerEncounter.Finish` exits the menu itself when a context exists; `ExitToLast` without a context does
nothing. The first design treated "return" and "exit to the map" as neutral, and they were not. F1 is the
same blindness one level up: the test plan assumed the saved menu survives the load, without tracing
what the discharge does to it.

## Why each lens caught or missed these

- **Standards:** F7, F8, F9 and the doc nits; it does not trace menu state.
- **Engine compatibility:** F1 and F2, by reading the load path and `RunMenuOptionConsequence`.
- **Data flow:** F1 independently, plus F3, F4 and F5, by walking every load-time discharge shape.
- **XML:** F13; all gates passed (the one failing coverage gate is the unrelated TAOM_Map translation backlog).
- **Efficiency:** no findings; it confirmed conditions run per menu refresh, not per frame.
- **Completeness:** F11, F12, and the missing in-game recipe for the town route.
- **Design:** F6, and the shape that fixed F2 to F5 together.

## Feedback memories to codify

None; the lesson below covers the pattern.
