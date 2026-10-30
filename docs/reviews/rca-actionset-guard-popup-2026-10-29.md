# RCA: the action set drift guards (#1065) and the party popup re-base (#1051), 2026-10-29

## Top line

`/deep-review` at risk tier 1 (an engine call added to four mission behaviours' start path) ran lenses 5 Data flow,
2 Engine compatibility and 7 XML on the C# and XML of the 2026-10-29 upstream review's two fixes. Codex was not run:
it is a paid dispatch and this session had no separate yes for it. All three lenses passed the change: no CRITICAL,
HIGH or MEDIUM finding, so no second wave. They confirmed seven LOW findings, all fixed beside this RCA except the
two pre-existing gaps recorded as follow-ups. A separate three-finder doc review of the same batch found the larger
miss: the refuted "`MBGlobals.GetActionSet` throws on a miss" claim still stood in nine live docs, four community
guides and two test comments after the fix, because the issue's own sweep named only two files.

The engine facts behind the fix were re-verified on the installed v1.5.6 client by lens 2: a missing id logs
`Action set "%s" could not be found, using default action set!` and returns index 0 (`get_index_with_id` 0x6DFF50,
search 0x5907E0), `IsValid` is `Index >= 0`, `GetName()` returns the declared id (0x6E0010) or "Invalid" without a
native call, and the search compares bytes, case sensitive.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | LOW | `set.IsValid ? set.GetName() : null` at the four call sites is redundant: `GetName()` already returns "Invalid" for an invalid set (v1.5.6 `MBActionSet.cs:36-43`) | simplicity | The builder guarded against the older dumps' throwing `GetName()` without re-reading the v1.5.6 body | Fixed: the call sites pass `set.GetName()`; a characterisation test covers the "Invalid" name. One-off |
| 2 | LOW | Two test comments still said `MBGlobals.GetActionSet` throws on a miss (`AnimaliaMountWiringTests.cs:108`, `CreatureBanditLiveDataTests.cs:54`) | stale claim | #1065's Solution named two docs to correct, so the sweep stopped at them; tests and code comments were never searched for the claim's words | Fixed. Lesson: a refuted claim is swept by its words over docs, code comments and tests (below) |
| 3 | LOW | Nine live docs and four community guides still carried the same claim (found by the doc review, finding F2-1 and its verifier sweep) | stale claim | The same narrowed sweep; the 2026-09-30 review had listed eleven files, but the issue body copied only two | Fixed in the doc batch. Same lesson |
| 4 | LOW | `PrefabAttributeNameBindingTests.cs:43-45` gave a reason for removing `AlwaysRefreshAll` (TAOM's element was a `NavigatableListPanel`) that the re-sync made false | stale comment | The re-sync changed the element type the comment named, and no step re-reads comments that cite a changed file | Fixed: the reason is now "no engine type has it". One-off |
| 5 | LOW | `PrefabCloneWidgetReferenceTests`'s failure message said every offender throws; a dropped `DataSource` binds nothing instead | wrong message | The new `DataSource` rule was added beside the Widget-reference rule without re-reading the shared message | Fixed. One-off |
| 6 | LOW | The `DataSource` check sees only Id-paired elements of the same type: an Id-less element (the popup's `DataSource="{..}"` text) and a prefab-instance tag are not checked, and a repeated Id pairs every vanilla occurrence with the clone's first element of that Id, so the clone's later occurrences are never checked (36 vanilla elements in 7 of the 24 pairs repeat an Id); the engine resolves a binding by the ancestor chain | gate scope | The builder disclosed the Id limit; the doc comment did not, and the first wording of the fix misdescribed the repeated-Id case (the convergence pass caught it) | The limit is now in the test's doc comment. FOLLOW-UP: #1051 (pair the k-th occurrence, count `DataSource` values per pair; lens 5 found ten vanilla bindings missing from five clones, intent unverified) |
| 7 | LOW | `.claude/rules/moduledata-validation.md`'s GUI-prefab gate row named neither `PrefabCloneWidgetReferenceTests` nor `PrefabElementTypeBindingTests`, though the rule calls that table the one register of gates | gate registry | Only `/engine-bump` named the clone gate | Fixed: both are in the row |
| 8 | LOW | Four more TAOM sites use `!IsValid` as the miss signal after a lookup by id, so their error branch cannot fire: `CharacterTableau_RefreshCharacterTableau_Patch.cs:70-83`, `RefreshCharacterEntityAuxPatch.cs:31-41`, `TableauDiagnostics.cs:185` (fed by `CharacterSpawnerService.cs:102`) and `CharacterSpawnerService.cs:257-259` | same defect, outside the change | Outside #1065's scope as filed (the four creature behaviours); two are Harmony patches, a tier 1 change of its own | FOLLOW-UP: #1065 (listed there for a second pass with the same helper; diagnostics only, gameplay unaffected) |

## Root-cause pattern

Findings 2, 3 and 8 share one cause. The 2026-09-30 review refuted a belief that had spread through the repo by
repetition, and it listed the files that carried it. The issue written from that review turned the list into "correct
harmony-patch-registry.md and lotrlome-elk-changes.md", so the fix was complete against the issue and incomplete
against the claim. A file list is a snapshot of one search; the claim's words are the thing to search for, in every
place a claim can live: docs, code comments, tests and other call sites of the same pattern.

This is the second time this class shipped: the rename-sweep lesson in `lessons/build-tooling-workflow.md` ("After a
whole-word identifier rename sweep, run a substring sweep over tests and docs") is the same miss for identifiers.

## Why each lens missed or found these

- **Lens 5 Data flow** found findings 1, 6 and 8 by tracing every `GetActionSet` call site and every `DataSource` in
  the shadowed pairs; it was asked to grep for the same defect class, which is why it saw the four outside sites.
- **Lens 2 Engine compatibility** found 1, 6 and 8 too, from the engine side (the `GetName()` body, `ViewModelPath`
  composition), and verified every engine claim the fix rests on. It had no reason to grep docs: they were out of
  scope by the brief.
- **Lens 7 XML** found 4, 5 and 7: it reads the gates that guard a prefab change, and their comments.
- **None of the code lenses could find 3:** the docs were under a separate review. The doc review found it, which is
  why that review ran.

## Lesson

Appended to `docs/reviews/lessons/build-tooling-workflow.md`: "A fix that refutes a claim sweeps the claim's words,
not a file list".
