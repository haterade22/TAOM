# RCA: Race Ability glow and sparks, cooldowns x4 (2026-10-04)

**Scope:** branch `feat/race-ability-glow` on `70320bbe`: every Race Ability cooldown times four (Mike: "about 1 to
2 minutes"), and the outline glow with a spark burst Mike picked (option C1). Reviewed by all seven `/deep-review`
lenses in two waves (`wf_79874fe2-572`), then one convergence pass. No CRITICAL or HIGH finding; one MEDIUM test
weakness, one MEDIUM wasted refresh, the rest LOW. A separate research pass for Mike's Khand decision
(`wf_ca7fb9e5-eae`, two refuting checkers) found four errors in the round-2 documentation, recorded below.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| G1 | LOW (lenses 2, 5, 7) | `visuals.burst` reached `ParticleSystemManager.GetRuntimeIdByName`, whose native side copies it into a 64-byte buffer with `strcpy_s`, with no length or character check | Config into native | The provider rule ("Config Providers MUST Validate") names numbers, ordering and strings the code branches on; a string passed straight into a native call is none of those, and I only planned to check the name against the engine on first use | `ValidateVisuals` bounds it to `^[A-Za-z0-9_]{1,63}$` (all 217 registered effect names fit; the longest is 42), with tests at 63, 64 and three bad shapes. The rule gains the string-into-native case |
| G2 | MED (lens 4; lenses 5, 7 LOW) | The live-install test globbed every `particle_systems*.xml` in Native, two of which no `project.mbproj` loads, so a name declared only there would pass while the game shows no sparks | Test universe | I took the folder's files to be the engine's load set; the load set is the module's registration manifest | The test reads the `type="particle_system"` rows of each module's `project.mbproj` |
| G3 | LOW (lens 4) | `GetConfig_GlowWhenNoOutlineIsAllowed_Warns` passed whatever the dwarf's glow was: the test JSON had no `cultures` section, so the compiled culture profiles (six with a glow) answered for it. The silent case had no test | Test isolation | The sibling test `GetConfig_VisualsValues_AreKept` had failed an hour earlier for exactly this leak; I fixed that test's value and did not check its siblings | `WithVisuals` writes `"cultures": {}`; a no-warning test for a cap of 0 with no glow set |
| G4 | LOW (lenses 1, 4) | The two wiring tests checked that guard and call tokens appeared somewhere in a file, not where: a second `SetContourColor(` outside `Paint`, or a visuals call from an any-thread file, would pass | Test strength | `AssertCalls` was reused for a placement claim it cannot make | The tests count the write sites, check the guard inside `Paint`'s body and no `Paint(` in `Clear`, and scan the whole feature for callers |
| G5 | LOW (lenses 2, 4, 5) | "Never from an engine callback" was wrong: on the main thread `RunOrDefer` runs the deaths handler inline inside `Mission.OnAgentRemoved`, so `Forget` clears the outline inside that callback (safely). "After the gameplay writes" was not literal on the death path either | Thread wording | I wrote the call path ("not the callback") when the property that matters is the thread | Code comment, doc and test comment now say "main thread only"; the doc and the test name the inline and the parked case |
| G6 | LOW (lens 2) | "A dead or recycled soldier's visuals can fault natively": a dead (removed, not deleted) soldier's visuals are valid, and `Forget` writes to them on purpose | Lifecycle wording | Removal and deletion were treated as one state | "Deleted or recycled" |
| G7 | LOW (lenses 1, 5) | The MCM hint said a soldier whose ability is "live" glows (only the active phase does; the feature's own console uses "live" for active or spent), and hard-coded "40", which a JSON edit makes wrong | MCM text | Repeat of `localization-ui.md` "User-facing strings are a contract" | "Active", and the field named instead of its value |
| G8 | LOW (lens 2) | The provider's comment said the particle registry is "empty when this file loads"; it is not, by the first mission tick | Copied rationale | Copied from the race resolver, where the race registry really is empty at load | The comment gives the real reason: the provider stays pure and loads in tests |
| G9 | LOW (lenses 1, 6) | A "not looked up yet" marker of -2 sat inside the range the code read as "unknown" (`< 0`) | Sentinel | A sentinel chosen without asking what the callee can return | `int? _burstId` with `??=` (native returns -1 for an unknown name, verified by lenses 2 and 7) |
| G10 | LOW (lens 1) | The same on/off test (MCM switch, Hide Battle UI) written twice | Duplication | Written in two methods at two times | One `Shown` property |
| G11 | MED (lens 3; lens 6) | Every wave re-ran the full outline pass, including the waves of the six dark abilities, which light nobody | Wasted work | "Light the wave at once" was written without asking whether the wave could light anything | The refresh runs only for a profile with a colour |
| G12 | LOW (lens 6) | An outline lasted up to half a second into the spent phase, against the documented "only the active phase glows" | Spec drift | Phase ends were left to the pulse, though the TAOM research had named `Settle` as the change point | `Settle` forgets the soldier the frame his active window ends |
| G13 | LOW (lens 4) | Three branches untested: one soldier offered twice, `"glow": null`, a cap of exactly 200 | Coverage | Repeat of `tests.md` "Skip-Guard Exhaustion" | Three tests |
| G14 | LOW (lenses 4, 5) | Nothing in game showed how many soldiers were outlined, so "the ledger lit nobody" and "the engine drew nothing" looked alike | Observability | The logging table listed failures only | `taom.print_race_abilities` prints `outlined now` |
| G15 | LOW (lens 4) | #730 does not record Mike's C1 choice, the new MCM setting or the cooldown change | Issue record | A public write, waiting on Mike's word | Owed: a #730 comment on Mike's word |

**Found by the Khand research, outside this review** (documentation from round 2, corrected in the same change):

| # | Finding | Why missed | Preventive action |
|---|---|---|---|
| K1 | "Umbar's levies are Harad troops": Umbar's volunteer pool (`aux_basic`, `umbar_elite`) and its six clans' lord templates are its own `umbar` troops; only its militia, patrols, villagers, rebels and starting garrisons are Harad | Round 2 read the culture block's `basic_troop`, not TAOM's volunteer pools or the clans' templates | Lesson in `data-content-cultures.md` (R17 entry, second pass) |
| K2 | Variag Ferocity's carriers left out Khand's caravan masters, its tavern mercenaries (`caravan_guard_khand`, which vanilla hires out from `town.Culture.CaravanGuard`; found by the convergence pass) and the Variag Ravagers (vanilla's Wolfskins, `battania` passed through `spclans.xslt`) | Carriers were enumerated from the culture block and troop files, not minor-faction clans, caravans or taverns | Same lesson |
| K3 | The dismount row said a non-dismounting weapon's dismount "reads knockdown resistance instead": `DecideAgentDismountedByBlow` rolls dismount resistance first and knockdown resistance whenever that roll fails, so both apply to a dismounting weapon | The row was written from the knockdown path, and the dismount path was summarised without reading its fall-through (`MissionCombatMechanicsHelper.cs:36-45`) | Lesson in `adapters-taleworlds-api.md` |
| K4 | The knockdown and knock-back rows had no "on foot" caveat: a rider is never asked either decision (`Mission.cs` mounted branch), and no charge strikes a mounted soldier (static native read) | Same: the mounted branch was not traced | Same lesson |

## Patterns

1. **A claim about the engine's load set, a callee's range or a lifecycle state, taken from what was at hand**
   (G1, G2, G6, G8, G9, K3, K4). The folder was not the load set, the sentinel's range was not the callee's, a
   removed soldier is not a deleted one, the race registry's timing is not the particle registry's, and the
   knockdown path is not the mounted path. In each case the fix was to read the one thing that decides: the
   manifest, the native lookup, the agent's state, the decision helper.
2. **Tests that pass when the thing they name is broken** (G2, G3, G4). Each checked a neighbour of the claim: a
   wider file set, a config that compiled defaults complete, a token anywhere in a file. G3 is the sharper one: the
   same leak had already failed a sibling test, and the fix stopped at that test.
3. **Wording that states the path instead of the condition** (G5, G6, G7): "not the callback" for "the main
   thread", "dead" for "deleted", "live" for "active".

## Why each lens caught or missed what it did

- **Standards (1)** found G4, G7, G9, G10, and flagged that two native claims were stated as fact (later verified
  by lenses 2 and 7). It does not read native code, so it could not see G1.
- **Engine compatibility (2)** found G1, G5, G6 and G8 and verified the native facts the doc now states (the -1
  for an unknown name, setters that check nothing). It does not judge test strength (G2 to G4).
- **Efficiency (3)** found G11. Nothing else was in its lens.
- **Completeness (4)** found G2 (as the MEDIUM), G3, G4, G13, G15. It does not decompile, so G1 was outside it.
- **Data flow (5)** found G1, G2, G7, G14 and the G5 wording. Its trace of the death path confirmed the inline case.
- **Design (6)** found G12 and joined G9 and G11. It proposed moving the `#RRGGBB` parser to `Main/Core/Validation`
  to share it with Realm Borders: a FOLLOW-UP on code this change did not write.
- **XML (7)** found G1 and G2 by checking the burst name against the engine's load set.

## Applied, not applied, follow-up

- **Applied:** G1 to G14 as listed. The convergence pass confirmed each and found four LOW gaps, all fixed: Khand's
  tavern mercenaries (K2), the campaign's perk dismounts in the dismount row, three test holes (the runtime file
  allowed any visuals call, the guard's order inside `Paint` was not pinned, the live test scanned every installed
  module), and four comments that still said "live".
- **Not applied:** a camera half-space test so soldiers behind the camera take no outline (lens 3, changes who
  glows: decide at in-game step 9); one refresh per frame instead of per wave (lens 3, only if step 10 measures a
  spike); hiding the glow in photo mode (lens 2, Mike's taste); renaming the activator's `cry` parameter (lens 1,
  optional).
- **Follow-up:** the shared `#RRGGBB` parser (lens 6), no issue filed: public, Mike's word. G15, a #730 comment.

## Feedback to codify

- `csharp-architecture.md` "Config Providers MUST Validate" gains the string-into-native case (G1).
- `lessons/testing-qa.md`: test the engine's load set, not the folder (G2); after a default leaks into one config
  test, isolate its siblings (G3).
- `lessons/adapters-taleworlds-api.md`: trace the mounted branch before documenting a resistance (K3, K4).
- `lessons/data-content-cultures.md`: the R17 entry's second pass (K1, K2), already appended.
