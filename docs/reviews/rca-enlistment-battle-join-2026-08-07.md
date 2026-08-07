# RCA — Enlistment battle-join never worked (2026-08-07)

**Issue:** [#406](https://github.com/haterade22/TAOM/issues/406) · **Feature:** [#375](https://github.com/haterade22/TAOM/issues/375) Enlistment · **Deferred follow-up:** [#408](https://github.com/haterade22/TAOM/issues/408)

## Top line

A player enlisted under a Mirkwood lord and never joined a single battle. The lord marched in an
army, stormed a town and fought in the field while the player's party trailed alongside; he then
left the army and appeared unable to start battles at all.

Three reported symptoms, **one bug**: the enlisted battle-join never worked under any circumstance.
Army membership and the siege were coincidences, not causes.

The feature shipped with a green 5448-test suite. `docs/features/enlistment.md` already listed
"encounter join from parked state" under *"nothing below has run in a live game"* — the entry was
correct, and the feature shipped anyway.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|------------|-------------------|
| 1 | HIGH | Join gated on `MapEvent.CanPartyJoinBattle`, a **diplomacy** test requiring every party on the opposing side to be at war with the joiner's `MapFaction`. An enlisted player keeps their own clan and is at war with nobody → `false` for every battle | Adapter/engine seam | The method name reads mechanical; nobody decompiled it. Tests stubbed it `true` | Doc-comment on `IsCommanderBattleJoinable` forbidding reinstatement; lesson in `lessons/testing-qa.md` |
| 2 | HIGH | `IEnlistmentReconciler.BattleJoinRequested` had **zero subscribers** — the only recovery path for a missed `MapEventStarted` did nothing | Dead wiring | Nothing tests that an event has a listener; it compiles fine | `EnlistmentContainerWiringTests` (DryIoc `Validate()`) + a reconciler test pinning the raise |
| 3 | HIGH | Siege encounters could never be seeded: built from `MobileParty` ids, but a settlement defender has no `MobileParty` → null id → `JoinBattle` threw on null `Current` | Cross-entity data flow | The siege path had no test and no in-game run; the null-id case is invisible in a field-battle mental model | Seed from `MapEvent.AttackerSide/DefenderSide.LeaderParty` (`PartyBase`); two siege tests |
| 4 | HIGH | `JoinBattle` reported success on failure — `JoinBattleInternal` silently calls `Finish()` when `EncounteredBattle` is null, and the adapter returned `true` because nothing threw | Verify-the-outcome | "No exception" was treated as "it worked" | Adapter verifies `MainParty.MapEvent != null`; regression test |
| 5 | HIGH | Rollback leaked the `PlayerEncounter`. `EncounterManager` gates the main party on `Current == null`, and a `MapEventSide` without a live encounter freezes that map event forever | Lifecycle cleanup | Rollback was written for state, not for engine-side resources | `Finish(false)` before re-park; regression test |
| 6 | HIGH | **Introduced by the fix.** `SwitchTo("encounter")` return value discarded. A verified join with no menu freezes the map event — and the recovery goes blind, because `Assess` sees `player.IsInMapEvent == true` and reports `Attached`, not `BattleJoinRequired` | Verify-the-outcome (repeat of #4) | I added outcome-verification to `JoinBattle` for exactly this reason, then ignored the very next call's return | Check the return, roll back on false; `BattleStarted_MenuSwitchFails_RollsBack...` test |
| 7 | MED | `GetSnapshot` called per map event world-wide, walking Culture/Clan/MapFaction/Settlement and allocating via `Name.ToString()` for a caller that reads only `PartyId` | Efficiency | Pre-existing; the cheap accessor did not exist | Added `ICommanderLordAdapter.GetPartyId` |
| 8 | LOW | Rollback's `TryTransition` return discarded — a transition-table regression would strand `EnlistedBattle` with no signal | Defensive coding | Return values of "always legal today" calls go unchecked | Log + force the state |
| 9 | LOW/MED | Unbounded hourly retry can pop the player's menu once per hour via `Finish`→`ExitToLast` | UX churn | New recovery path had no retry bound | **Deferred** → [#408](https://github.com/haterade22/TAOM/issues/408) |
| 10 | **P1 (Codex)** | **The fix to #6 was itself incomplete.** `GameMenu.SwitchToMenu` silently no-ops when `CurrentMenuContext == null` — it only `Debug.FailedAssert`s, inert in release — so the adapter returned `true` after doing nothing, and the freeze survived | Verify-the-outcome (3rd repeat) | I checked the return of `SwitchTo` without checking whether `SwitchTo` itself could lie. Fixing one layer, trusting the next | New `IGameMenuAdapter.EnsureMenuOpen`: switch → verify → activate → verify, against the observable `CurrentMenuId` |
| 11 | **P1 (Codex)** | `LeaveSettlementIfUnderSiege` ran before EVERY join. `MapEvent.AddInvolvedPartyInternal` rewrites a siege **assault** to `SiegeOutside` when a defender joins with `CurrentSettlement == null` — corrupting the battle type for every participant | Cross-entity engine side effect | The siege fix was reasoned from the attacker's perspective only; nobody asked what leaving means for a defender | Attacker-only; two tests splitting the sides |

## Root-cause pattern

Findings 1, 4 and 6 are one pattern: **an engine call's success was inferred rather than observed.**

- #1 inferred from a *method name* what question the engine was answering.
- #4 inferred from *absence of an exception* that the call did something.
- #6 inferred from *the previous line succeeding* that the sequence completed.

Findings 2 and 5 are the same failure one level up — wiring and resources that were *declared*
rather than *verified*: an event with no subscriber, and a rollback that restored our own state
while leaving the engine's.

The unifying rule: **at an adapter boundary, verify the observable outcome, never the absence of
an error.** Everything on the TAOM side of that boundary was well-tested and correct. Every single
defect lived in the seam.

Finding 6 is the sharpest evidence that the lesson does not generalise itself. I wrote the comment
*"a non-throwing call proves nothing"* directly above the `JoinBattle` verification, then discarded
`SwitchTo`'s return three lines later — in the same function, in the same sitting.

**Finding 10 is sharper still.** Having been shown finding 6, I fixed it by checking `SwitchTo`'s
return — and never asked whether `SwitchTo` itself could lie. It can: `GameMenu.SwitchToMenu`
no-ops silently when there is no current menu context. So the corrected code still reported success
for a menu that never opened, and the freeze survived a fix written specifically to prevent it.

That is four instances of one pattern in a single changeset, each found only by an independent
reviewer, each after the previous one had supposedly taught the lesson. The conclusion is not
"try harder": **at an adapter boundary, checking a return value is not verification unless you have
read what produces that return value.** `EnsureMenuOpen` is the shape that actually holds — assert
against observable state (`CurrentMenuId`), not against a bool someone else computed.

## Why each agent missed the original five

These shipped before the review existed, so this section is about why the *test suite and process*
missed them, plus how the review performed on re-examination.

| Agent | Why it missed the originals | Performance on the fix |
|---|---|---|
| Standards | ADR compliance was never violated — the code was clean and wrong | Correctly passed; usefully vetted the singleton event-subscription pattern |
| API compat | Would have caught #1 and #4 had it run pre-ship, by decompiling the two methods | Caught two real load-bearing couplings (`AttachedTo` precondition, `LeaveSettlement` NRE guard) |
| Efficiency | Out of scope — the path was correct-looking, just never executed | Found #7; also produced one false positive (claimed `?.` does not short-circuit argument evaluation — it does) |
| Completeness | The feature doc *did* flag the gap; nothing enforced it as a blocker | Found the missing siege test and the missing RCA |
| Data flow | Not run pre-ship | **Found #6**, the HIGH introduced by the fix — the single highest-value Claude-side result |
| Codex (independent) | n/a | **Found #10 and #11**, both P1, both after five Claude agents had passed the same code. #10 is the incomplete fix for the HIGH the data-flow agent had just found |

The process lesson: the feature doc's "not verified in a live game" list was accurate and was
treated as informational. It should be a **ship blocker**, not a note.

The second process lesson: Codex earned its cost here. It found two P1s in code that five Claude
agents had just cleared — including a defect in a fix written minutes earlier in direct response to
one of those agents. Independent adversarial review is not redundant with parallel review; the
Claude agents and Codex failed on different things.

## Lessons codified

- `docs/reviews/lessons/testing-qa.md` — *"A mocked adapter cannot test the engine precondition
  behind it"*. Covers the mock-hides-the-seam pattern, verifying observable outcomes over silence,
  and adding DI `Validate()` guards for critical-path wiring.

## Residual risk

Nothing here has run in a live game. The fix is verified only by unit tests, which is precisely the
evidence class that failed to catch the original bug. The four in-game cases in #406 (field solo,
field in-army, siege assault, save-load mid-service) remain the real gate, and every distinct
failure branch now logs a unique line so the next report is a log grep rather than a re-investigation.
