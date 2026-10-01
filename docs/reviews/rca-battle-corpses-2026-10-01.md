# RCA: Battle Corpses (#701) deep review, 2026-10-01

## Top-line

`/deep-review` ran seven lenses in two waves (standards, engine compatibility, data flow and XML; then efficiency, completeness and design) on the uncommitted #701 change in the worktree `E:/repos/taom-battle-corpses`. No CRITICAL or HIGH. Two MEDIUM findings and a set of LOW ones, all fixed in the same session before the first commit, except the harvester tool defect, filed as #702. The engine lens confirmed every native claim the feature rests on (the two Mission setters, the per-mission reset, the corpse limiter table, the single-player gate) and corrected three doc statements.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| F1 | MED (XML, data flow) | The result message said "Number of Ragdolls 5, Number of Corpses Low" after every Apply, but Apply only lowers (`Math.Min`), so a player on 1 ragdoll, or anyone pressing the MCM button at 0/0, was told a value that was never written. The inquiry body never said a lower setting is kept. | Player copy disagrees with the code path | The "never raise" rule was added to the advisor after the text was written from the recommendation; nothing compared each message against each branch of the action it reports. | Lesson in `lessons/localization-ui.md`. Fixed: the done text is value-neutral, the body says "A setting already lower is kept." |
| F2 | MED (completeness) | Issue #701, filed before implementation, promised a `[MissionPerf] corpses=` field and a campaign-map advisor. The field was dropped mid-build (it duplicates `agents` minus `active`) and the advisor moved to the main menu; the issue was not updated. | Plan drift | The simplicity call was made correctly but recorded only in chat and the plan file, not on the issue that quoted the dropped item. | Lesson in `lessons/build-tooling-workflow.md`. Fixed at close: the issue body is synced. |
| F3 | LOW (XML) | A `\n\n` in the notice's inline default was harvested into the XML attribute as raw newlines, which parse back as spaces, so the registered English differed from the C# default. | Localization tooling | `harvest_literal_loc_keys.py` converts `\n` and does not escape it; the author did not know `{newline}` is the house idiom. | #702 for the tool. Lesson in `lessons/localization-ui.md`. Fixed here by removing the line breaks. |
| F4 | LOW (engine) | Three doc and comment claims were wrong: towns do not set `DisableCorpseFadeOut` (only three stealth missions do); the team-AI type is assigned in `EarlyStart`, before `AfterStart`, so the real reason to apply from `OnMissionTick` is Deployment mode; the fade clock starts when a body becomes a corpse, not at death. | Engine claim copied, not read | The first claim was copied from `MountDespawnMissionGate`'s summary and `mount-despawn.md`, which carry the same error; the second was inferred, not read from `Mission.AfterStart`. | Existing rule (`lessons/state-lifecycle-save.md`: a lifecycle virtual's firing set is read from its caller) applied. Fixed in this change's files; the pre-existing copy in MountDespawn is a follow-up. |
| F5 | LOW (standards, completeness) | No wiring test, no provider-default test, no test for the float-to-index cast, two cap validation branches untested. | Test gaps | Tests were written for the two services only; the module wiring and the cast regression test that `csharp-architecture.md` mandates were skipped. | No new rule. Fixed: `BattleCorpsesWiringTests`, `BattleCorpseSettingsProviderTests`, `GraphicsOptionsAdapterTests`, four policy tests. |
| F6 | LOW (standards) | The new native writes lacked the `MissionThreadGuard.NoteCall` tripwire. | Rule not applied | The rule sits at the end of the mission-thread section of `csharp-architecture.md`; the writes run on the main thread, so it felt redundant. | No new rule. Fixed, and pinned by a wiring test. |
| F7 | LOW (data flow, design) | The offer required both options readable while Apply lowers each on its own, so an unreadable ragdoll value hid a corpse option on Unlimited. | One predicate, two expressions | The offer and the write were written as separate expressions of "above the recommendation". | Fixed: one `IsAbove` helper used per option; tests pin the one-unknown case. |
| F8 | LOW (standards, data flow) | Adapter doc promised -1 for an unreadable Battle Size and "false on any failure" from `SaveOptions`; neither held. | Interface doc written ahead of code | Contract written before the implementation settled. | Fixed: the docs state what the code does. |
| F9 | LOW (standards, completeness) | `coop-interop.md` still said "one action button" and quoted the stale counts 175 and 183. | Doc drift | The count edit touched the sentence above but not its neighbours. | Fixed. |

## Root-cause pattern

F1, F2 and F8 share one shape: **a statement written before the code settled (a message, an issue, an interface doc) and not re-read against the final code.** Each was true of the design at the time it was written. The fix in each case was to re-read the statement against the shipped branches; the lessons below make that a step rather than a review catch.

## Why each lens caught or missed what it did

- **Standards** caught the struct placement, the adapter doc and the stale co-op line; it does not read player copy against code paths, so F1 fell to the XML and data-flow lenses.
- **Engine compatibility** caught F4 by reading `SandBoxMissions`, `StoryModeMissions` and `Mission.AfterStart`; no other lens opens the engine for prose claims.
- **Data flow** caught F1, F7 and the restart-scope error in the doc, and settled the MCM load-order question.
- **XML** caught F1 and F3 by comparing the registered strings with what Apply does.
- **Efficiency** found nothing to change and bounded the native removal burst, now an in-game check.
- **Completeness** caught F2 and F5; issue text is outside every other lens.
- **Design** turned F7 and the redundant per-mission resets into deletions.

## Follow-ups

- `MountDespawnMissionGate`'s summary and `mount-despawn.md` still say towns set `DisableCorpseFadeOut`. That is pre-existing text outside this change; fix it the next time MountDespawn is touched.
- #702: the harvester defect.
- The in-game checks in `docs/features/battle-corpses.md`, above all check 3 (does a corpse removal delete the agent) and check 5 (the removal burst).
