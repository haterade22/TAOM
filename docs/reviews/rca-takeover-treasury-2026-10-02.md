# RCA: a taken-over lord keeps their treasury (Player Switcher, 2026-10-02)

## Top line

Mike, after the faction-screen takeover went out in `21b01894`: a taken-over lord's "starting gold
should be his own treasury if they can. If they wanted to start with 1K they would've made a character
from scratch." Every takeover started at 1,000 plus the culture's starting gold, because
`CharacterCreationState.FinalizeCharacterCreationState` assigns `Hero.MainHero.Gold = 1000` after every
character-creation handler (by then the main hero is the lord) and StartupResources adds the culture's
player gold at the last `OnCharacterCreationIsOver` phase. "Carry Over Starting Gold" had changed
nothing since v1.5.0 put that assignment after the handlers.

The fix records the lord's gold in the 1100 handler. At phase 9 StartupResources, the one listener that
decides what replaces the engine's 1,000, asks `TakeoverTreasuryService` to put that gold back and
grants a taken-over lord no culture gold. A review in two waves (standards, engine, efficiency and data
flow; then completeness and design) found no HIGH: one MEDIUM, the adoption path, and a LOW tail. The
convergence agent stopped on the weekly usage limit before it reported, so its checks were run by hand:
gold parity for every case, standards, the doc claims against the code and the engine, and stale names.
They found two more LOWs (G13, G14). Every finding was fixed before commit. The two items under
Follow-ups predate this change and are recorded there, to be filed as issues on Mike's word.

## Findings

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| G1 | MED | The restore and the StartupResources skip fired on every handover that took effect, so an adopted wanderer (Player Switcher's panel) got the purses the handover left them (their own and the created character's) in place of the 1,000 plus culture gold a new character gets (Data flow) | Path coverage | The design was written from the takeover ("the lord's treasury"); the session record it read was path-agnostic | The restore acts only on `IPlayerSwitchSession.LordTakenOver` (the takeover path); adoption is back to 1,000 plus the culture gold; tests per path; lesson in `campaign-mechanics.md` |
| G2 | LOW | The record the phase-9 readers use was reset only in the 1100 handler's `InitializeContent`, which does not exist when its registration throws: a later campaign could read an earlier lord's id and gold, and the gold write accepted any hero (Data flow, Efficiency) | Stale state | The reset story was inherited from the selection, whose only readers sat behind the same registration | Reset in `PlayerSwitchRegistrationBehavior` before it registers; the gold write (now `SetPlayerGold`, G12) writes only for the main hero and says whether it wrote; lesson in `state-lifecycle-save.md` |
| G3 | LOW | "The handover took effect" was spelled out in three new places and one old one, `KingdomJoinOfferService` (Standards, Data flow, Design) | Duplication | Each reader was written separately | `SwitchOutcome.TookEffect()` with its truth table, used by all four, `KingdomJoinOfferService` included, with a new test for a partly finished adoption; `LordTakenOver` on the session |
| G4 | LOW | Three test gaps: the empty-id guard untested, StartupResources' skip test failing only through an engine NRE, a wiring test named for more than it checked (Standards, Data flow) | Test meaningfulness | Tests were written for the happy path | Guard test; the takeover test asserts no error is logged (a missing return reaches `Hero.MainHero`, which throws without a game and is logged); the wiring test records through the writer and restores through the service; nine mutations, each caught |
| G5 | LOW | The "Carry Over Starting Gold" hint said the lord "always" keeps the treasury; it did not mention adoption or a non-default start type (Standards, Engine) | Player-facing text | Written for the common case | Reworded (English only: MCM hints carry no loc key); its adoption clause corrected again under G10 |
| G6 | LOW | Comments and docs said "scenario" where the engine's term is start type (`Scenario` is a separate Advanced Starting Options field); `advanced-start-options.md` listed phase 9 as TAOM-only (Engine, Data flow) | Engine terms | Reused a loose word | Reworded; phase-9 list corrected (SandBox.GauntletUI's map views also listen at 9) |
| G7 | LOW | The docs lagged the change: smoke steps 4 and 9 stated no gold, `startup-resources.md` still said its behavior "fires at index 1" and had no takeover exception, the player-switcher test list was short, and two pre-existing cases that act on a taken-over lord were undocumented (Completeness) | Doc coverage | Docs were written for the mechanism, not for the reader running the smoke | Gold expectations in both steps; `startup-resources.md` row, key-files entry, tests and changelog; both cases in player-switcher.md "Owed" and an advanced-start-options.md interaction |
| G8 | LOW | Nothing pinned the engine order the fix rests on (the handlers, then the 1,000, then the phase events), and a lord with zero gold was untested (Completeness) | Engine coupling | The order was read in the decompile once and trusted | `PlayerSwitcherBindingTests.FinalizeCharacterCreationState_AssignsThePlayerGoldAfterTheHandlersAndBeforeThePhaseEvents` (IL, `RequiresGameIL`); a zero-treasury test |
| G9 | LOW | One decision ran in two phase-9 listeners: `TakeoverTreasuryBehavior` copied StartupResources' start-type read, and StartupResources' skip existed only to keep the two from colliding (Design) | Ownership | Built as a Player Switcher change, so it got a Player Switcher listener | StartupResources calls `ITakeoverTreasuryService.RestoreIfTakenOver()` first and grants only when it returns false; `TakeoverTreasuryBehavior`, its registration, its tests and the start-type flag deleted; lesson in `campaign-mechanics.md` |
| G10 | LOW | Since v1.5.0, player-switcher.md and the MCM hint said the created character's purse reaches an adopted wanderer; the engine's 1,000 overwrites it, and the completeness lens's proposed adoption expectation (1,000 plus culture gold plus that purse) repeated the claim (found while checking G7 against the engine) | Stale engine claim | The v1.5.0 migration repaired the reported consumer only | Doc paragraph, hint and smoke step 9 corrected to "what a new character starts with"; lesson in `state-lifecycle-save.md` |
| G11 | LOW | The record was reset twice in one engine call: in the registration behavior and again in the handler's `InitializeContent`, which the engine runs right after the same dispatch (Design) | Duplication | G2 added the earlier reset without removing the later one | The handler's reset removed; the one reset is the one the docs name |
| G12 | LOW | `SetGold` wrote only the player's gold but read as the twin of `GetGold`, which reads any hero's (Design) | Naming | Named after the property, not the rule | Renamed `SetPlayerGold`, matching `ApplyPlayerCharacter` and `ReassignPlayerClan` on the same adapter |
| G13 | LOW | `faction-ui.md` said a taken-over hero arrives "with his clan", though the handover takes over a lady too (Convergence, by hand) | Wording | The line was edited for the treasury without re-reading the rest of it | "their" |
| G14 | LOW | The engine-order gate (G8) pinned `NextStage` calling `ApplyFinalEffects` before the finalize, but not that `ApplyFinalEffects` runs the handlers, so player-switcher.md's "pins the handlers, then the 1,000" claimed more than it checked (Convergence, by hand) | Gate narrower than its claim | The doc sentence was written from the test's name | The gate also requires `ApplyFinalEffects` to call `OnCharacterCreationFinalize` |

## Follow-ups (pre-existing, not in this diff)

- **A Failed outcome can hide a completed swap.** `ChangePlayerCharacterAction.Apply` sets the player
  troop before raising events whose listeners can throw, and `HeroSwitchService` marks the handover
  committed only after `Apply` returns, so such a throw reads as Failed ("continuing as your own
  character") although the player is the lord, who then starts on 1,000 plus the culture gold. Issue to
  file on Mike's word.
- **A takeover with an Advanced Starting Options start type other than "default"** runs that start
  against the lord at phase 8: every such start replaces the gold and clears the party's items, King,
  Vassal and Mercenary move the lord's clan into the chosen kingdom (King makes it the ruling clan),
  and Beggar zeroes the gold and strips the gear. Player Switcher never checks the start type. Decision
  for Mike, then an issue.

## Why each lens caught what it did

- **Data flow** traced the gold through both handover paths and every phase-9 listener (G1, G2).
- **Engine** read every phase-8 and phase-9 listener in the installed DLLs and the start-type names (G5, G6).
- **Standards** mutation-reasoned the tests (G4) and found the duplicated predicate (G3).
- **Efficiency** found no cost and raised the stale-record case for data flow.
- **Completeness** read the change from the smoke runner's and the next maintainer's side (G7, G8).
- **Design** asked who owns the decision the change alters (G9) and found the leftovers of earlier fixes
  (G11, G12, and the last hand-written predicate of G3).
- **G10** came from checking a completeness suggestion against the engine before writing it down.
- **Convergence**, run by hand after the agent stopped on the usage limit, re-read every added line and
  every doc claim against what proves it (G13, G14).

## Lessons appended

- `campaign-mechanics.md`: a fix keyed on an outcome enumerates the paths that produce it (G1); a value
  replaced at one event has one owner (G9).
- `state-lifecycle-save.md`: per-creation state is reset where every creation passes (G2); an engine bump
  that adds a write re-audits every claim about that field (G10).
