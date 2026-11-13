# RCA: Possession check at the player switch, review (2026-11-13)

**Issue:** #1177. **Branch:** `fix/1177-possession-at-switch` from trunk `4b7a84af4`.
**Review:** `/deep-review` at risk tier 1 (a campaign event listener that runs inside Coop's join
hand-off): Agent 5 Data flow and Agent 2 Engine compatibility (`deep-reviewer`, effort max), and Codex
(`gpt-6-astra`) at `ultra` (prompt `docs/reviews/codex-prereview-possession-switch-2026-11-13.prompt.md`).
Codex reported a HIGH, so a second wave ran: Agents 1, 3, 4 and 6 at effort high.

## Top-line

A Coop client got its character-creation package (race, startup gold, career, special-resource seed)
only at the next `HourlyTickEvent`, and a joining client's clock can stand still for minutes, so the
first battle could start without the career. The change also runs the check on
`OnPlayerCharacterChangedEvent`, which Coop's prefix on `ChangePlayerCharacterAction.Apply` raises
inside `HeroInterface.SwitchToPlayer` (Coop `ChangePlayerCharacterActionPatches.cs:57`). All three
first-wave reviewers confirmed the join path. All three found one new gap: TAOM's own Player Switcher
raises the same event inside character creation, before the new choices are captured, so a process
that still held an earlier creation's choices handed them to the chosen lord. The fix drops those
choices when a new character creation starts.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH (Codex), MED (Agent 5, Agent 2) | With a co-op module enabled, a second new campaign in one process whose player picks a lord through the Player Switcher: `HeroSwitchService.cs:116` raises the event inside `CharacterCreationManager.ApplyFinalEffects` (`:108`, handlers at `:339`), before phase 9 captures the new choices (`CharacterCreationState.cs:62`). `_choices` still held the first creation's picks (kept across a new campaign on purpose, `PlayerPossessionService.cs` `ResetForNewCampaign`), the lord differed from both stored ids, and the first creation's race, career and resource seed went onto the lord. The hourly check never reached that moment: phase 9 runs in the same call. | A synchronous listener reaches a moment the deferred check never reached | I named the Player Switcher's order as an open question in the brief (question 3) and left it to the reviewers. I traced the state at Coop's raiser, not at TAOM's own raiser, and took the hourly check's safety as the new listener's. | `IPlayerPossessionService.BeginCharacterCreation`, wired to `OnCharacterCreationInitializedEvent`, drops the old choices. `TryConsumePossession_SecondCampaignsPlayerSwitcherBeforeItsCapture_DoesNotFire` fails with the drop removed (mutation run) and `TryConsumePossession_JoinAfterACompletedCharacterCreation_StillFires` keeps the join. Lesson bullet in `lessons/campaign-mechanics.md`. |
| 2 | LOW (Codex) | The source pin searched the whole file, so it passed with both registrations moved out of `RegisterEvents` into an unused method (Codex mutated it in memory). | Gate checks text, not placement | I copied the `BondOfHireWiringTests` shape, which pins a call that has one possible place. | The pin cuts out the `RegisterEvents` body and asserts all three registrations inside it. |
| 3 | LOW (Codex) | The guard test accepted any `[Possession]` error, and the handler logged only `ex.Message`, which hides the stack that the hourly path would surface as a crash. | Assertion too broad; log too thin | The handler's message was new, and I asserted only its tag. | The test asserts the full message prefix; the handler logs `{ex}`. |
| 4 | LOW (Agent 2, Agent 5, Codex) | The doc, the comment and the test summary cited vanilla `ChangePlayerCharacterAction.cs:53` as the raise site. On a Coop client that body never runs: Coop's prefix returns false (`:65`) and raises the event from its own copy (`:57`). If Coop drops that call, the switch-time check stops with no error. | Engine line cited where a mod's patch replaces it | I read vanilla first and read Coop's patch only for the ordering. | All three texts cite Coop's `:57` and name the dependency. |
| 5 | LOW (Agent 2, Codex) | "`Campaign.Tick` raises no campaign tick event while the clock stands still" ignored the first ticks (`_dt > 0f || CurrentTickCount < 3`). | Claim too broad | Summarised the condition from its first term. | Doc reads "only when `_dt > 0f` or in the first two ticks of a session (`Campaign.cs:977-981`)". |
| 6 | LOW (Agent 2, Agent 5, Codex) | The in-game check placed the TAOM log line "at" Coop's `Player switch after` line; it is written inside `Apply`, between `Player switch before` (`HeroInterface.cs:191`) and `after` (`:219`). | Wrong position in a check | Wrote the check from the issue's timeline, not from `SwitchToPlayer`. | Doc corrected. The #1177 issue text has the same wording; the close comment corrects it. |
| 7 | LOW (Agent 5) | The catch comment said only "no exception may leave it". An escaping exception would end `SwitchToPlayer`, Coop's broker would log it, and `EnterCampaignState()` (`LoadingState.cs:81`) would never run, so the client stays in `LoadingState`. | Comment understates the reason | | Comment and doc name the effect. |
| 8 | Older (Agent 5 flows 4 to 6, Codex Q3) | With a co-op module enabled and no session, a solo heir (`ApplyHeirSelectionAction.cs:68`) or a hero loaded after a character creation can receive the package; the reconnect marker is never fed on a BannerlordCoop client (it never saves). Three texts said presence keeps solo play out "entirely". | Guard documented as stronger than it is | Older than this change; the new listener only moves the heir case from the next hour to the hand-over. | Filed #1183 (`triage-blocked-decision`: a session gate turns the re-grant off under Bannerlord Together). The texts in `player-possession.md`, `coop-interop.md` and `TryConsumePossession` now say what holds. |

## Disagreement

Codex rated finding 1 HIGH by its effect: a lore lord's race is overwritten and then saved. Agent 5 and
Agent 2 rated it MED by its likelihood: it needs a co-op module enabled, a second new campaign in one
process, and the Player Switcher. Both readings agree it is real and new, and it was fixed before the
commit, so the rating changed nothing but the second wave.

Agent 2 offered a second fix: skip the switch-time check while `CharacterCreationState` is active. Agent 5
recommended the drop at the source, and Codex recommended invalidating stale choices when a new
character creation begins. The drop fixes the bad value where it is produced
(`working-discipline.md`), and it needs no read of the game state stack.

## Second wave

Agents 1, 3, 4 and 6 at effort high, on the first-wave fix.

- **Agent 3 Efficiency:** no issue. Both new listeners fire once per hand-off or per character
  creation. Keep the hourly listener: removing it saves nothing measurable and loses the fallback.
- **Agent 6 Design:** no simpler design. The drop at the source also covers the hourly path and reads no
  engine state; `OnCharacterCreationInitializedEvent` is raised only by the `CharacterCreationManager`
  constructor (`:48`), and Coop's join raises it nowhere, so a join keeps its captured choices. One
  KEEP: the behavior's class summary still said `Hero.MainHero` never changes in single-player. Applied.
- **Agent 1 Standards:** four LOW comment items under ADR-009 (incident history and a cost
  justification in comments, external line numbers in a test summary). Applied: the service comment and
  the first service test's comment are gone, the listener comments keep only the Coop and clock facts,
  and the test class summary is one line.
- **Agent 4 Completeness:** code, tests and registrations complete; docs outside the diff were stale.
  Applied: `coop-interop.md` (the possession row said "hourly tick" and "grants the start gold");
  `player-switcher.md` (the possession interaction and smoke step 8 now cover the hand-over event and a
  second campaign); `character-creation.md` (both listeners); `feature-map.md` (the guard claim, and its
  dashes); `coop-compat-gap-ledger.md` (line numbers the insert moved, now method names); the Coop cites
  name `bannerlord-1.5.x` 995517e60, because the fork's older branch raises the event at line 77; the
  #1177 issue body (its Analysis said Coop does not patch `Apply`, and "first three ticks"); and an
  in-game check for the second-creation case.

## Fixes

- `Main/Features/PlayerPossession/IPlayerPossessionService.cs`, `PlayerPossessionService.cs`:
  `BeginCharacterCreation` drops the choices and logs whose they were.
- `Main/Features/PlayerPossession/PlayerPossessionBehavior.cs`: the `OnCharacterCreationInitializedEvent`
  listener; the handler logs `{ex}`; the comments cite Coop's raise site and say why the arguments are
  ignored (under Coop both are the joined hero, `HeroInterface.cs:199`, prefix `:22`, `:57`).
- `TAOM.Tests/Features/PlayerPossession/PlayerPossessionServiceTests.cs`: findings 1's two tests.
- `TAOM.Tests/Features/PlayerPossession/PlayerPossessionSwitchTests.cs`: findings 2 and 3.
- `docs/features/player-possession.md`, `docs/features/coop-interop.md`: findings 4 to 8.
- `docs/features/player-switcher.md`, `character-creation.md`, `docs/reference/feature-map.md`,
  `docs/research/coop-compat-gap-ledger.md`: the second wave's Agent 4 items.

## Feedback memories to codify

- Lesson, `lessons/campaign-mechanics.md`: before a deferred check also runs on a synchronous event,
  list every raiser of that event and read the check's state at each one. The deferred path's safety
  came from what ran between the raiser and the check.
