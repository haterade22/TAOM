# RCA: deep review of the howdah platform rebuild, its move to the Armory, and its diagnostics (#627)

**Scope.** The war elephant's howdah prefab rebuilt on the vanilla siege-tower and War Sails ship pattern (research
doc [howdah-ship-research-2026-09-18.md](../features/elephant/howdah-ship-research-2026-09-18.md)), moved from the TAOM
module to `LOTRLOME_Armory/Prefabs`, `HowdahPrefabTests`, and the docs that followed the move. While the review ran Mike
asked for comprehensive howdah logging; that work, and the fixes, went through a convergence pass. Seven lenses ran on
the `deep-reviewer` agent, two at a time: data flow and XML first, then engine compatibility and standards,
completeness and efficiency, and design. Five of them died on the usage limit on the first attempt and were rerun.
**No CRITICAL; one HIGH**, fixed. After the fixes the full C# suite is green: 9,849 passed, 2 skipped, 0 failed; the
39 howdah tests and the settings fingerprint tests pass.

Mike's decisions (2026-09-19): rename the prefab; keep four crew frames and settle the count at the first crew test;
file one issue (#627); logging behind an MCM toggle, on by default while the platform is tested.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | Every release from v2.0.22 to v2.0.30 shipped `Modules/TAOM/Prefabs/taom_howdah_agent.xml`, an in-place update keeps it, and the release channel folder is updated in place, so once the Armory ships the rebuilt prefab under the same name players load two; which one the engine instantiates is native and undefined (no prefab duplicate diagnostic exists in `TaleWorlds.Native.dll`) | release / data flow | The move cleaned the repo and the dev install and stopped there. "The deploy never deletes" was already a lesson, but it was read as a dev-machine fact, not as a fact about installs players already have | Renamed to `taom_howdah_platform`; `ElephantConfig.HowdahPrefabName` is the one name the C# instantiates and the tests pin; a test sweeps every installed module for a second declaration. Lesson: `build-tooling-workflow.md` |
| 2 | MED | `TheLiveArmoryCopy_MatchesTheSnapshot` compares bytes, the snapshot was LF only because a tool wrote it, and `core.autocrlf=true` with no `*.xml` rule would check it out CRLF and fail the test | testing | Verified on the file as written, never as git would check it out | `.gitattributes` pins `docs/reference/lotrlome-armory-snapshot/Prefabs/*.xml` to LF (`git check-attr` confirms). Lesson: `testing-qa.md` |
| 3 | MED | The prefab header and the CHANGELOG said the platform was the functional layer of the elite howdah mesh as if bound; no item binds `sk_hd_elep_armor_howdah_*` and the trigger is still the plain `sk_elephant_armor_a` | data flow / claim | The design intent was written as present fact, although the research doc two sections away said "none bound to an item yet" | Reworded in the prefab, CHANGELOG and docs; binding the mesh is an open item in `elephant.md` |
| 4 | MED | "A 0.37 m human capsule just fits": four do not fit the 1.4 m deck clear; side by side they are 0.70 m apart where 0.74 m is needed, and each presses its rail by 2 cm | geometry / claim | Each capsule was checked against the rails, never against its neighbour; the test's 3 cm slack hid the rail press | Corrected everywhere; Mike kept four until the first crew test, and the test names the accepted press. Lesson: `xslt-moduledata.md` |
| 5 | MED | The tests pinned the geometry the rebuild changed and nothing its consumers read: the root name `Instantiate` asks for, the machine and seat scripts it casts to, `visible_only_when_editing`, and the rails' position on the floor's edges | testing | The tests were written from the diff, not from the consumer | Eight tests added (name, scripts, visibility, engine-default tags, rails on the edges, legacy name, module sweep, split live checks). Lesson: `testing-qa.md` |
| 6 | MED | Convergence D1: after an elephant dies, `PropagateRefsToSeats` re-runs every frame, and the new per-seat log tag string allocated four strings a frame for the rest of the mission | efficiency | The allocation was added inside a loop without tracing when the loop re-enters: `ClearSeatRefs` resets `_seatsInitialized`, so the loop runs every frame after a death | Early return when `elephantAgent` is null (parity: the loop was a no-op there) |
| 7 | LOW | The machine's `PilotStandingPointTag`, `AmmoPickUpTag` and `WaitStandingPointTag` were `""`, which reaches the native `HasTag("")` with unknown semantics (if it matched everything, every seat would be an ammo pickup and the last a pilot point) | engine | Carried over from the ADOD_Beasts port without reading `UsableMachine.OnInit` | Rows deleted; the defaults match no child; a test pins it |
| 8 | LOW | 20 seat `<variable>` rows restated engine defaults, burying the one real override (`AutoSheathWeapons="false"`) | design | The rows came over wholesale from the Kit's output | Deleted; parity checked against the `StandingPoint` and `MissionObject` field initialisers |
| 9 | LOW | Stale docs: the June ADOD analysis block in `elephant.md` read as current, the research doc still instructed "add moveable" after the rebuild and hedged the missile mask, `module-armory.md` counted 8 prefabs, the snapshot README had no prefab restore line or dated entry, `release-process.md` never mentioned packaging the Armory, `coop-interop.md` said 59 settings excluded (66 counted; stale before this change) | completeness | The doc sweep followed the moved path string, not the facts that changed with the move | All fixed; the release process now says the Armory ships in the same release as a TAOM build that needs a new Armory file |
| 10 | LOW | Test standards: no nullable annotations under the repo-wide `<Nullable>enable</Nullable>`, two concerns in one live test (the deployed-TAOM check sat behind the Armory-absent Inconclusive), the class doc detached from the class | standards | Written in the style of older sibling tests | Annotated, split, moved to `/// <summary>` |
| 11 | LOW | Convergence N2: the module sweep parsed every installed prefab (TAOM_Map alone is 52 MB), so a malformed third-party prefab would fail a TAOM test | testing | The sweep was written for correctness only | Text scan first; parse only on a hit, and a hit that will not parse counts as declaring |
| 12 | LOW | Convergence N3: the config banner printed floats in the thread culture | standards | `HowdahDiagnostics.Format` existed but the banner was written from the older line style | Formatted invariantly |
| 13 | LOW | Process: the review brief told the lenses that players get the Armory through the lotraom-assets mirror. They get it from the releases folder after Mike packages it in the editor (Mike, mid-review) | process | A memory note about the mirror's sync direction was read as a statement about delivery | The run was stopped and relaunched with the corrected fact; memories `armory-is-shipped-to-players` and `armory-mirror-sync` now say where players get the Armory |

**Reviewer claims corrected by other lenses** (evidence over confidence):

- Data flow said the `TranslateUser` row "can never change" the readonly field. Wrong: the engine sets script variables through `FieldInfo.SetValue`, which writes readonly fields; the engine lens proved it on the desktop CLR. The row was honoured and redundant (default `true`).
- XML flagged the crew frames' rotation sign as the opposite of the old file's. The new signs are right: `Mat3.RotateAboutUp` and vanilla mangonel frames show `rotation_euler` z is right-handed, so the new frames face outward and the old ones faced across the deck.
- Completeness called the launcher's stale-file behaviour unverified. It deletes files absent from a fresh manifest (`UpdateService.cs:178`, `SyncOrchestrator.cs:262`), but the channel folder is updated in place, so the old file stays in the manifest; finding 1 stands.

## Root-cause pattern

Findings 1, 3, 4 and 5 share one shape: **a change checked against what it touched, not against who reads it.** The
move was checked against the dev install, not the players' installs; the header's binding claim against the design,
not the item that makes it; the crew fit against the rails, not the other capsules; the tests against the diff, not
the C# that instantiates, casts and hides. Each has its own lesson; the common question to ask before calling a data
change done is "who reads this, and what do they read?"

## Why each lens missed or caught what it did

- **Data flow** caught 2, 3 and the stale-install shape of 1 on the first wave; it reasoned from a wrong delivery fact
  (13) until the rerun, and it over-claimed the readonly field.
- **XML** caught 3, 4 (pairwise) and 5; its rotation-sign flag was answered by the engine lens.
- **Engine** settled the four open engine questions (duplicate names bounded as native, rotation sign, null on an
  unknown prefab, readonly variables) and found 7. It proposed the rename.
- **Standards** found 10; no production C# was in scope at the time.
- **Completeness** found 1 as HIGH with the channel-folder evidence, and 9. It expanded the logging plan (prefab probe,
  config banner, reading-the-log section, summary).
- **Efficiency** found no cost in the change and shaped the logging: `Agent.Velocity` cannot see a slide, `Agent.Name`
  allocates, INFO at 5 s is right given the synchronous flush.
- **Design** proposed the rename, the variable deletions (8), the engine's real-versus-locomotion velocity pair, one
  summary path and a per-mission serial tag, and argued five plan items down.
- **Convergence** found 6, 11 and 12 in the applied fixes and verified parity and every new engine call.

## Not applied

- Drop `dont_collide_with_camera` from the rails (XML, INFO): parity unproven; `DontCollideWithCamera` is not in
  `CameraCollisionRayCastExludeFlags`, so the native camera path may treat it apart from `Barrier`.
- Move the crew frames to x = +/-0.33 (data flow, LOW): it trades the 2 cm rail press for more neighbour overlap; Mike
  kept four frames until the crew test.
- Slide and drift WARN thresholds, a deduped skip-reason set, and a `taom.howdah_status` console command (plan items
  argued down by design): no measured slide yet to set a threshold against; Mike chose an MCM toggle.

## Follow-ups (pre-existing code the change did not modify; no separate issue, listed in `elephant.md` open items)

- ADR-002: `ElephantMissionBehavior` went from 249 to 293 lines and `TaomHowdahMachine` from 252 to 302. Both were
  already over the 150-line ceiling. The diagnostics were put in `HowdahDiagnosticsReporter` to keep the growth down;
  extracting the howdah spawn path and the bone path is its own refactor.
- Twenty test classes each carry a copy of the repo-root walker; a shared test-path helper is churn to unify.
- The seat's 120-tick status line could fold into the machine's status line when crew return.
- `ElephantConfig.HowdahHeightAboveRider` has no reader.
- `TrySpawnHowdahCrew` (disabled) still spawns above the elephant's origin; it must spawn at the crew frames.
- The physics-shape bounds behind the floor and rail constants come from a scratch dumper; promoting it into `tools/`
  would let anyone reproduce them.
