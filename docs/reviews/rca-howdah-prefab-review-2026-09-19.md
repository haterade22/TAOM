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
---

# Addendum: the crew and the visible howdah (delta review, same day)

**Scope.** After the first in-game run of the rebuilt platform Mike saw nobody in the howdah and decided two things:
turn crew spawn back on (parked since 2026-06-10 as a slide source) and bind the elite howdah mesh to an item so the
howdah is visible. That work, and everything the review of it changed: `HowdahCrewSpawner`, `HowdahCrewAgentOrigin`,
`HowdahHarness`, `ElephantConfig.HowdahHarnessStringId`, `ElephantMissionBehavior`, the live Armory item
`sk_elephant_armor_howdah_elite` with its 13 name rows, the Harad elephant rider's harness, and the tests and docs.
Seven lenses again, two at a time. **No CRITICAL; one HIGH, fixed.** The last two lenses (Efficiency, Design) ran on
Opus 5 by Mike's decision because the account hit its Fable limit; every other lens ran on Fable at max effort. Final
run: 9,874 passed, 2 skipped; deployed. The first review's work is committed in `83bdad85`, and most of this delta in `925db38d`, both multi-session
commits made while the review ran; the fixes this review produced are the uncommitted remainder.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| D1 | HIGH | All four crew archers were built with the mahout's own origin, so the first crew casualty went through `PartyGroupAgentOrigin.SetKilled`: the Harad elephant rider left the party roster while still riding, the crew's hits paid him XP, and the supplier's `NumRemovedTroops` moved, which `MissionBattleSideSpawnContext` reads as a lost unit (a side could be declared beaten while its elephant fought) | data flow / engine | The line was June's, re-enabled by deleting a comment. A parked path came back as a toggle flip and was reviewed as one. The June port also deviated from its own reference, which built a fresh origin per crewman | `HowdahCrewAgentOrigin`: casualty and score calls do nothing, scoreboard combatant, colours and command forward to the mahout's, per-seat seed. 11 tests. Lesson in `adapters-taleworlds-api.md` |
| D2 | HIGH (found before the smoke, so no cost) | The crew were resolved with `MBObjectManager.GetObject<CharacterObject>`. `GetObject<T>` matches only the exact type when T is sealed, `CharacterObject` is sealed, and Custom Battle registers troops as `BasicCharacterObject`: the lookup returns null there, so the crew would never have spawned in the mode the smoke runs in | engine | The call came from June's campaign-only code, and nothing had exercised it in Custom Battle since | `GetObject<BasicCharacterObject>` (resolves in both modes) plus `HowdahCrewLookupBanTests`, an IL ban that fails if any howdah code asks for the sealed type again |
| D3 | MED | The seat's periodic log line counts FRAMES (`_teleportCount % 120`), so with crew aboard it wrote 405 to 580 lines a minute per elephant at the 202 to 290 fps measured in Mike's own run, each a synchronous flush, all four seats bursting on one frame | efficiency | The line had been dormant since June because no seat ever had an occupant; re-enabling crew woke it | Deleted; the machine's 5 s status line now carries each seated archer's action and its distance from its frame, behind the diagnostics toggle |
| D4 | MED | `ElephantMissionBehavior` grew to 350 lines (ADR-002 ceiling 150), the crew code inline in a `MissionLogic` | standards | The crew work was added where the old code sat | Crew half extracted to `HowdahCrewSpawner`; the behaviour is 238 lines, below the 293 it had at HEAD |
| D5 | MED | The harness gate read `Character.Equipment`, the troop's default roster, not the roster the engine rolled for this agent | data flow | The rider has one roster, so both reads agreed and nothing failed | Reads `agent.SpawnEquipment`, which the engine sets before `OnAgentBuild` and builds the mount from. Lesson in `adapters-taleworlds-api.md` |
| D6 | LOW | The crew got none of what `Mission.SpawnTroop` gives a battle troop: no banner, default clothing colours (which TAOM's colour persistence also skipped), no `NoHorses`, no `WieldInitialWeapons` (archers could stand empty-handed) | engine | The builder was written from what the spawn needed to work, not from what vanilla does | All four added. Lesson in `adapters-taleworlds-api.md` |
| D7 | LOW | The mahout-origin guard sat inside the per-seat loop, after a spawn line had already been logged, and returned rather than continued | standards | Added where it was first needed | Hoisted into the queued spawn's guard chain |
| D8 | LOW | 22 INFO lines per crewed elephant in one drain tick, over half duplicating the layout dump's seat inventory | efficiency | Carried over verbatim from the June method | One pass over the seats; a line only for a skipped seat; the spawn stamp before `SpawnAgent` kept as the crash localiser |
| D9 | LOW | Crew were built into the mahout's formation (Cavalry), where vanilla picks by troop class; a released archer rejoined the cavalry | design | The capture dated from June, when the rider was a horse archer | `Mission.GetAgentTroopClass` picks it, as vanilla does |
| D10 | LOW | Stale statements: the prefab header and two elephant.md spots said no item binds the elite mesh; comments said the crew keep a HorseArcher formation (the rider has been Cavalry since 2026-06-29); the machine's class doc claimed detachment fills the seats although `GetDetachmentWeightAux` returns 0; the research doc still asked for a fix that had landed | standards / completeness | Each was true when written and nobody re-read it against the code | All corrected |
| D11 | LOW | The Turkish item name used the archaic "hevdec" | XML | Hand-written translation | `[Harad] Fil Mahfesi` |
| D12 | LOW | `HowdahCrewAgentOrigin.GetTraitsMask` would throw on the null troop the tests pass | standards | The null seam existed only for tests and was untested itself | Returns `TroopTraitsMask.None` for a null troop, with a test |
| D13 | LOW | Record gaps: no REVIEW-LOG entries, no RCA addendum, CHANGELOG naming deleted code and quoting a half-stated gate failure, `feature-map.md` still saying crew are disabled | completeness | The review's own paperwork lagged the code by a few hours | This addendum, two REVIEW-LOG entries, the CHANGELOG and map corrected |

## Root-cause pattern

**Parked code came back as a flag, and was reviewed as a flag.** D1, D2, D3, D8 and D9 are all the same shape: the
crew path was written in June, disabled in June, and left to rot while the world moved (the rider changed formation,
the platform was rebuilt, the smoke moved to Custom Battle). Nothing in the diff of "re-enable crew" showed any of
them, because the defects were in the lines that did not change. The preventive rule is in
`build-tooling-workflow.md`: when a parked path returns, review it as new code, trace it end to end against today's
data and engine, and compare it with the reference it was ported from.

**Second pattern, smaller: a spawn outside the supplier inherits none of the supplier's contract.** D1 and D6 are the
two halves of that: the origin the engine books casualties against, and the builder calls vanilla makes for every
troop. Both are now lessons in `adapters-taleworlds-api.md`.

## Corrections to the first review's follow-ups

- "TrySpawnHowdahCrew still spawns above the elephant's origin": done, the crew spawn on their frames.
- "Split the howdah spawn path out of ElephantMissionBehavior": the crew half is out (238 lines); `TryInstantiateHowdah`
  and `TaomHowdahMachine` (302) remain.
- "Fold the seat's 120-tick line into the machine status line, when crew return": done (D3), its trigger having arrived.
- `ElephantConfig.HowdahHeightAboveRider` is still unread.

## Which lens caught what

Data flow: D1, D5, and the catalogue drift. XML: D10 (prefab header), D11, and the mirror gap. Engine: D6, and the
verification that the per-seat seed reaches each archer's face. Standards: D4, D7, D12, and the comment corrections.
Completeness: D13, the release pairing for the item, and the scoreboard and merit notes now in the feature doc.
Efficiency: D3, D8, and the agent-budget fact (a crewed elephant occupies six agent slots where the engine reserves
two). Design: D2, D9, and the confirmation that the queue, the origin wrapper and the seat model are the right shapes.

## Not applied

- Collapse the two harness triggers into one and drop the crewless platform for the plain armour (Design P3): Mike
  chose to keep the plain armour working when he approved the item, and the item stays in the Armory for saves that
  hold one as loot.
- Release the crew from `UsableMachine.OnMissionEnded` instead of the two per-tick polls (Design P6): research doc
  step 2, FOLLOW-UP, and the crew smoke is what should settle it.
- A reinforcement headroom guard before spawning four crew (Efficiency F3): the engine's agent cap is native and
  unverified; the A/B with `CrewSpawnEnabled` is the measurement to take first.
- `TaomCombatMechanicsModel.VictimPartyId` reading `BattleCombatant as PartyBase` so refuge reduction reaches the crew:
  another feature's file, FOLLOW-UP, recorded in `elephant.md`.

## Owed in game

The crew smoke in `docs/features/elephant.md`: four archers on a visible howdah, `crew force-spawned: 4 archer(s)`,
`seated=4/4` with their actions in the status line, `carriedV` while the elephants move and turn, one archer killed
without the party losing an Elephant Rider or the battle ending early, and the battle finishing so the `summary` line
prints. Then the Armory mirror commit, and Mike's editor package of the Armory in the same release as this build.

---

# Addendum 2: the crew tests (2026-09-19/20)

**What happened.** Crew spawn went on for the first time since June. Nine in-game rounds followed, two of which froze
the game. The crew now shoot from a moving elephant and a battle ends cleanly with crew aboard.

| # | Sev | Finding | Why missed | Preventive action |
|---|---|---|---|---|
| C1 | CRITICAL | A seat still holding an agent hangs mission end: `IsDeactivated`'s setter spins `while (HasAIMovingTo)` and an agent added by `AddMovingAgent` alone never clears | The loop is vanilla's, reached only through a state vanilla never produces. Two review passes over the managed rout and battle-over paths found nothing | Machine and seat both empty their seats before vanilla deactivates them; `HowdahSeatReleaseTests` pins it AND pins the `AddMovingAgent` registration the guard exists for. Lesson: dump a hang before killing it |
| C2 | HIGH | Per-frame position correction made every archer read as travelling 30 m/s, so no bow draw ever completed | The archer visibly stayed on its seat, so the teleport looked free. Four genuine AI-side bugs in front of it kept the search on the AI | Deadband via the NaN-safe `HowdahSeatMotion.ShouldCorrect`; measure engine-side velocity before theorising about an agent's AI |
| C3 | HIGH | A null `Formation` pins `MissileRangeAdjusted` at 0, so the engine credits the archer with no reach at all | The line was June's, re-enabled as a flag flip, and its comment asserted the opposite ("it auto-fires at nearby enemies instead") | Keep the formation and hold the archer with a scripted position; never null a formation to stop an order |
| C4 | HIGH | `Formation.DetachUnit` stamps `DefaultDetached`, cutting Ranged to a hundredth of Melee, so a bow-only crew chases a melee stance it cannot use | The value set is named for its situation, not its effect, and nothing logs it | The seat overrides Ranged, Melee and GoToPos through `OverrideBehaviorParams` and reasserts them, since `RefreshBehaviorValues` re-stamps the set |
| C5 | MED | Crew frames 0.50 m apart make their occupants shove each other every frame, recreating C2 | Frames were laid out against the visible deck, not against capsule radius | Spacing gate in `HowdahSeatMotion` plus `HowdahPrefabTests`; it is what sizes the mumakil's decks |
| C6 | MED | The platform sat 0.34 m forward of the real deck: the centre had been taken from face CENTRES of a quad grid | A plausible measurement that was never checked against vertex extents | Re-measured from extents, and the wall faces measured separately, because the walls and not the deck are what a body stands inside |
| C7 | LOW | The crew carried a sword they can never use, and archers were seen with it drawn | They were the line troop, `harad_archer` | Their own troop, `harad_howdah_crew`, bow and quivers only, pinned by `HowdahCrewLoadoutTests` |

## Root-cause pattern

**Four of the seven are the same shape: TAOM put an agent in a state vanilla never produces, and the engine answered
with silence rather than an error.** No log line, no exception, no failed call: a formation-less archer simply had
zero range, a detached one simply preferred a weapon it did not carry, a carried one simply never finished a draw,
and a seated one simply hung the mission end. Every one was found by measuring engine state, not by reading TAOM's
own code, which is why the diagnostics line grew from six fields to sixteen over the two days and why it stays.

**The second pattern is cost.** Nine rounds, each one a full game restart and battle. The rounds that moved fastest
were the ones where a single field told two hypotheses apart (`enav` killed the navmesh theory in one run; `prog` and
`restarts` separated "interrupted" from "refused"). The rounds that cost the most were the ones where a plausible
theory was acted on before it was instrumented. Instrument first, then act.

## The review of the fix (2026-09-20, two lenses on Opus after the account hit its Fable limit)

Data flow returned 13 findings, engine compatibility 3 behavioural defects and zero incompatible API usages across 63
members. The ones that mattered, all fixed and pinned:

| # | Sev | Finding | Fix |
|---|---|---|---|
| E1 | MED | `new WorldPosition(Scene, Vec3)` passes `hasValidZ: false`, and `SetScriptedPosition` ends with `if (Mission.IsTeleportingAgents && XY differs) TeleportToPosition(position.GetGroundVec3())`. An invalid Z resolves to the GROUND, so through the deployment phase, the one window that sets that flag, every archer would have been dropped off the howdah into the elephant's own capsule | The four-argument constructor with `hasValidZ: true`, which also skips a native Z resolution every frame |
| E2 | MED | The crew behaviour curves are never restored, so an archer released when its elephant dies rejoins its formation with GoToPos and Melee flat zero and can neither advance nor defend itself until that formation happens to re-apply a movement order | `SetBehaviorValueSet(Default)` in the release, pinned |
| E3 | LOW, latent | `UsableMachine.Disable` is a second copy of the mission-end hang, unreachable today only because the prefab carries no `DestructableComponent` | A three-line `Disable` override, pinned. `Deactivate` has the same shape and is NOT virtual, so it stays a documented risk |
| F-1 | MED | A NaN could still reach native: the teleport was gated, the scripted position two lines below took the same engine-sourced value ungated | One `HowdahSeatMotion.IsPlaceable` above both, with tests |
| F-6 | MED | `SetDetachableFromFormation(true)` was a no-op (it is the engine default) and the wrong polarity: a seated archer must NOT be detachable, or `DetachmentManager` scores it against every detachment its formation holds | `false` on seating, `true` on release |
| F-3 | MED | The hand-rolled re-attach left a detachment holding a slot for an agent it no longer owned | `Agent.TryAttachToFormation`, vanilla's own |
| F-7 | LOW | The death-release sat behind a guard on the machine reference, and it is the ONLY path that empties a seat whose archer died, which is the precondition for the hang | Release moved above the guard |
| F-12 | LOW | The crew loadout test scanned only the Armory, hit Inconclusive on the SandBoxCore arrows and never checked the bow | Scans the vanilla module folders too; zero skips now |

Also corrected: eight comments describing the pre-2026-09-20 design (the `Machine` field as a detachment, the seat
nulling the formation, the firing-order justification, `AutoSheathWeapons`), and a wrong cost claim, since
`OverrideBehaviorParams` makes no native call at all and the engine pushes the array once per parallel tick.

**Owed smoke, specific:** a siege or deployment-enabled field battle, NOT a plain Custom Battle, reading the
`[Howdah#n] status` lines emitted BEFORE deployment ends. That is the run that would have caught E1, and it is the one
kind of battle the crew have never been tested in.

## Not applied

- A deck navmesh (the ship route) and crew as riders of an invisible mount (what ADOD_Beasts does): both would delete
  the teleport entirely and both are recorded in `docs/features/elephant.md` as the fallbacks if the deadband proves
  too coarse. Neither was needed once C2 was fixed.
- Attributing which of the rails, the floor's missile flag and the detachment removal mattered: they shipped in one
  build, and the user judged the rails ruled out rather than spend a round on attribution.
