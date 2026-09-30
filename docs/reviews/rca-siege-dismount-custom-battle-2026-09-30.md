# RCA: Custom Battle sieges never finished loading (#699, 2026-09-30)

## Top-line

From v2.0.30 to v2.0.32, a Custom Battle siege assault never started while Siege Dismount was on
in a non-Vanilla mode, the default. `PlayerMountAdapter.HasMount()` read `Hero.MainHero`, which throws inside its own
getter when there is no campaign. The exception left `SiegeDismountMissionBehavior.AfterStart`. Something
above it swallowed it without writing a crash report (TAOM's `Patch37_CrashReport` tick finalizers are the suspect;
the only silent path through them is the service's `_handling` re-entry guard), and because
`MissionState.FinishMissionLoading` had already cleared `_missionInitializing`, the engine ran `LoadMission` again
every frame. The NRE fired once: every later pass through `Mission.AfterStart` failed before TAOM's behaviors ran, on
an exception nothing logged. The siege left 731 `MissionAfterStartBegin` lines and no `MissionAfterStartDone` in
`taom_debug_2026-09-30_12-41-24.log`. The fix guards the reads in both adapters and
moves `HasMount()` inside the service's `try`. Mike confirmed the Edoras Custom Battle siege in game on `cfca3c5f`
(the adapter guards); the `try` move and the test seam are covered by unit tests. A six-lens
`/deep-review` of the fix (XML and Tooling were not in scope) found no CRITICAL or HIGH finding.

The same crash shipped once before: #97 (2026-04-29), `Hero.get_MainHero()` on a Custom Battle launch, from
`CareerPerkMissionBehavior`.

## Findings and root causes

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| B1 | HIGH (shipped) | `PlayerMountAdapter` guarded with `Hero.MainHero?.`, `PartyMountInventoryAdapter` with `MobileParty.MainParty?.`. `Hero.MainHero` is `CharacterObject.PlayerCharacter.HeroObject` and `PlayerCharacter` is `Game.Current.PlayerTroop as CharacterObject`; a Custom Battle registers troops as `BasicCharacterObject` (`CustomGame`) and sets `PlayerTroop` to one (`CustomBattleHelper.StartGame`), so the getter throws before `?.` runs | Missing null guard (computed getter), a repeat | The port (`bc159498`, 2026-05-07) came eight days after #97 was fixed elsewhere, and #97 left no lesson, rule or RCA. The adapters.md computed-getter paragraph (#281, 2026-06-15) named only party getters, and no sweep revisited existing adapters. The code was dead until #606 (`fac17e05`, first released in v2.0.30) moved its start to `AfterStart`; #606's mounted campaign siege check was listed as owed and never run, and the Custom Battle fact lived only in a code comment (`TeamCombatantSelector.cs:78`) | Guarded chains in both adapters; `HeroBattleEquipmentOf(BasicCharacterObject?)` tested with a real `BasicCharacterObject` (a hard cast fails it); adapters.md now names both statics and the safe reads; harmony-patches.md asks for a Custom Battle run when mission code runs for the first time; recurrence under the adapters lesson |
| B2 | MED (shipped) | `SiegeDismountService.OnMissionStart` called `HasMount()` outside the `try` that covered every other adapter call, so one exception became a load that never ends | Error-handling gap | The `try` was written around the mutating calls; the first read was left out and no test threw from it | `HasMount()` inside the `try`; `OnMissionStart_HasMountThrows_LogsErrorAndDoesNotPropagate` (failed first) |
| R1 | LOW | The first regression tests ran with `Game.Current` null, not the Custom Battle state (a `Game` whose `PlayerTroop` is a `BasicCharacterObject`), so a hard cast would have passed them, and their docstring and the feature doc called it "the Custom Battle state" | Test fidelity | The test was written from the failing stack, not from the state that produced it | The extracted helper and its test; the no-Game tests renamed `_NoGame_` and described as "does not throw" only |
| R2 | LOW | The feature doc contradicted itself (the diagram and Key Files still named `Hero.MainHero` and `MobileParty.MainParty`), gave v2.0.29 for #606 (first released in v2.0.30), claimed "every Custom Battle siege" (a sally-out is not an `IsSiegeBattle` mission; the feature must be on), gave a pass criterion the failing run also met, and still read "Issue: TBD" | Stale and imprecise doc | The first doc pass edited new sections, not the ones already describing the old reads | Rewritten; the pass criterion names `MissionAfterStartDone` and `BattlePlayable` |
| R3 | MED | No issue existed, and the code reached `origin` inside another session's commit (`cfca3c5f`, "Add realm fill functionality...") with no release-note entry; the doc commit `9cb6d49e` has no version label | Process | The fix sat uncommitted while a concurrent session committed with a broad stage; neither commit went through the Claude subject gate | #699; this review's commit carries the release-note body; the swept commit is recorded here |

## Root-cause pattern

B1 and #97 are one class: a TaleWorlds campaign static used from code that also runs where there is no campaign.
Every TAOM mission behavior is added to every mission, so every `AfterStart` also runs in a Custom Battle, and the
static getters that read the campaign throw there. The adapters.md rule covered the class in general but its examples
were party getters, and the lesson file had no entry for #97, so nothing tied "Custom Battle" to "Hero.MainHero".
The amplifier is separate: an exception leaving `Mission.AfterStart` did not crash. It was swallowed without a
crash report, and the half-started mission reloaded forever with no exception in the TAOM log. A throw from any
other TAOM `AfterStart` could loop the same way (follow-up below).

## Why each lens caught what it did

- **Engine compatibility (2)** proved the chain equal to the engine getters in every vanilla state, found that #606
  first shipped in v2.0.30, and named `Patch37_CrashReport`'s swallowing tick finalizers; the convergence pass then
  showed from the log that no crash report was written, so which handler swallowed the NRE is still open.
- **Data flow (5)** traced twelve paths, confirmed the singleton state resets between a campaign siege and a Custom
  Battle, and explained the reload through `_missionInitializing`.
- **Standards (1)** found #97 and the missing issue; **Completeness (4)** the missing RCA, the release-note gap, the
  stale doc and the unpromoted rule; **Design (6)** the test seam; **Efficiency (3)** nothing to change.
- Three lenses (2, 4, 5) independently asked for `HasMount()` inside the `try`.

## Follow-ups

- The silent reload loop needs its own issue and `/investigate`: which handler swallowed an exception out of
  `Mission.AfterStart` without a crash report (`Patch37_CrashReport` is the suspect), and what each re-run of
  `Mission.AfterStart` threw before reaching TAOM's behaviors.
- `SiegeDismountService` reads "has a mount" twice (`HasMount()` and `Capture().HasMount`); folding them removes an
  interface member (Design lens, follow-up). `PartyMountInventoryAdapter`'s `Deposit` and `Withdraw` differ only in
  the sign (follow-up).
- The `SiegeDismountDebug` MCM hint promises HUD output the file logger never gives.
- SiegeDismount has no row in `docs/reference/feature-map.md`.
- A campaign siege as a mounted player is still owed in game (from #606).
- Co-op: whether a co-op mod patches `Hero.get_MainHero` (which the new chain would bypass) is UNVERIFIED.

## Feedback memories to codify

None. The rule lives in `.claude/rules/adapters.md` and `.claude/rules/harmony-patches.md`, both path rules that load
for the files where the mistake is made.
