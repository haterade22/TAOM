# RCA: stuck AI battle guard, deep review (#748, 2026-10-06)

**Summary.** A player reported AI battles frozen at 8000 against 0 and sent a workaround module, Stuck
Battle Guard v1.0.0, with source. TAOM's guard (an hourly sweep that ends battles v1.5.4 can never
finish) went to a seven-lens `/deep-review` before any commit, with the Standards CRITICAL escalated to an
adversarial pass. The review confirmed 1 CRITICAL, 8 MEDIUM and a run of LOWs. Every one was fixed or
decided before commit. Mike approved six behaviour changes one at a time: the sweep stands down in co-op,
both sides at 0 means the defenders hold, one write per battle per pass, the resolve console command is
dropped, the parts mirroring the player's module are rewritten, and a follow-up issue (#750) for the
TAOM-side trigger. Full suite after the fixes: 15,002 passed, 1 failed
(`EveryLanguage_DeclaresARowForEveryEnglishKey`, the race-ability translation rows owed by #731,
unrelated).

## Findings

Each row was re-read against the TAOM source or the v1.5.4 decompile before entry
(`.claude/rules/evidence-over-claims.md`).

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | CRITICAL | `IStuckBattleAdapter` returned `IReadOnlyList<MapEvent>` and took `MapEvent` in every member, so the sealed type reached `StuckBattleSweep`, a DI singleton holding the detach, re-judge and verdict logic; its tests passed `null!` handles and could not tell two events apart. | ADR-007, sealed type in a service | The builder copied `AutoResolveDiagnostics`, where an entry point holds the `MapEvent`, then moved the orchestration into an injected, unit-tested singleton under `Hooks/` for testability. That move turned an entry point into a service without re-checking the sealed-type rule. `lessons/adapters-taleworlds-api.md` "A precedent is not an exemption" (2026-09-27) names this exact shape and was not read before writing the adapter. **Second occurrence in nine days.** | Fixed: `LiveBattles()` hands out one `IStuckBattleEventAdapter` per event; the sweep folded into `StuckBattleService`. Lesson extended (a type with no id gets a per-object adapter). The gate ADR-007 cites, `TAOM.Tests/Architecture/AdapterPatternArchitectureTests.cs`, does not exist; an adapter-return-type scan with an allowlist would have caught this (proposed below). |
| 2 | MEDIUM | Under BannerlordCoop, a battle holding a remote player's party read as an AI battle. Coop's `MapEvent.Update` prefix returns false for any non-raid event with a party this instance does not control (fork `MapEventPatches.cs:487-498`), so an award would commit results and never finish. | Co-op, shared-entity ownership | The behavior copied the sibling hourly gate, `IsAuthority`, which answers "may this peer tick" and not "whose battle is this". `IsAuthority` also fails open under BannerlordTogether. | Fixed: no sweep while `IsSessionActive \|\| ShouldDeferToHost`; both directions tested; row added to `coop-interop.md`. Co-op support recorded in #748. Lesson in `lessons/campaign-mechanics.md`. |
| 3 | MEDIUM | Both sides at 0 ended through `DiplomaticallyFinished`. With no winner, `MapEventSide.HandleMapEventEndForPartyInternal` destroys every active mobile party with 0 healthy on both sides and makes each leader a fugitive (v1.5.4 `MapEventSide.cs:438-446`). The doc said only "no winner". | Engine consequence not traced | The no-winner exit was taken from the player's module as "the gentle path" and its end-of-event handling was never read. | Fixed (Mike): the defenders hold, the engine's own out-of-round rule (`MapEvent.CheckIfOneSideHasLost`). |
| 4 | MEDIUM | While enlisted, `IsCommanderParty` (a full commander read through `CommanderLordAdapter.GetSnapshot`) ran for every party of every battle every sweep, before the age gate. | Hot-path cost | The `IsEnlisted` short-circuit covered only the not-enlisted case; the order of the gates was not weighed against the hourly frequency at high campaign speed. | Fixed: the commander veto is asked last, only for a verdict the sweep would act on; tested. |
| 5 | MEDIUM | The battle label (up to five `TextObject.ToString()` renders) was built for every event every sweep and thrown away unless the sweep acted. | Hot-path cost | The description was put on the snapshot for convenience. | Fixed: `Label()` on the per-event adapter, called only when a line is written, before the write; tested. |
| 6 | MEDIUM | The provenance register said "No code was copied", while the adapter mirrored the player's detach loop (read-back check, `-1` sentinel), its player-battle predicate's name and first condition, a fallback string, its detach-then-judge order and its module Id. | Provenance | The builder read the player's source while planning, then wrote the register from memory of what was "taken", not from a comparison against the source. | Fixed (Mike): all of it rewritten from TAOM's spec, the register corrected and the source named in a code comment. Lesson in `lessons/misc.md`. |
| 7 | MEDIUM | The enlisted-commander check's "proceeds" branch had no test: a mutant that skipped every battle while enlisted passed all 19 tests. | Skip-guard coverage | Both enlisted tests put the commander inside the battle; `tests.md` "Skip-Guard Exhaustion" was not applied. | Fixed: `Decide_EnlistedCommanderElsewhere_StillResolves`. |
| 8 | MEDIUM | Nothing tested the co-op stand-down, and `coop-interop.md`'s gated table had no row. | Coverage, doc | The gate sat in the behavior, which `CoopAuthorityGateTests` covers only with a game host. | Fixed: the gate moved into the service, tested without the game in both directions; row added. |
| 9 | LOW | The refused-detach branch (`-1`) could never run: the `PartyBase.MapEventSide` setter always assigns after `RemovePartyInternal` returns (`PartyBase.cs:306-310`). | Dead branch | Carried over from the player's module without reading the setter. | Deleted with its test and doc line. |
| 10 | LOW | Destroyed quest parties were detached, though vanilla `DestroyPartyAction` leaves a party a quest is using attached on purpose. | Engine intent | The detach predicate was written from the symptom, not from vanilla's own detach. | Fixed: `IsCurrentlyUsedByAQuest` excluded, as vanilla does. |
| 11 | LOW | The resolve command's dry run printed `DetachDestroyed` where `confirm` would award a winner. | Dry run under-reports | The dry run printed the first verdict of a two-verdict pass. | Gone with decisions 3 and 4 (one write per pass; command dropped). |
| 12 | LOW | Four sweep tests passed with the guard they were named for deleted; three branches had no test. | Tests that cannot fail | Each stubbed one snapshot that stopped the flow before the guard. | Rewritten with the restructure: per-event substitutes, label-before-write order, one-of-two-battles, throw from a real write path. |
| 13 | LOW | The doc named vanilla starvation as a cause; vanilla's healing, starvation wounds and desertion all skip a party in a map event (`PartyHealCampaignBehavior.TryHealOrWoundParty`, `DesertionCampaignBehavior.DailyTickParty`). The commander-skip comment named a reason that does not hold (a stuck battle never raises `MapEventEnded`). | Doc and comment accuracy | Written from inference, not read. | Fixed: doc names the vanilla gates and TAOM's desertion (#750); comment gives the real reason (enlistment's later hourly join). |
| 14 | LOW | Conventions: console class outside `Cheats/`, module Id unlike its class, test names without the method, a duplicated `IsPlayerMapEvent` clause, `ElapsedDaysUntilNow * 24` for `ElapsedHoursUntilNow`, an NRE caught as control flow. | Conventions | Not checked against siblings. | Fixed. |
| 15 | MEDIUM | Convergence pass: the row-6 rewrite had not fully landed. The method name `DetachDestroyedParties`, the predicate's terms in order, the `isRaidLike` name and expression, the `IsPlayerBattle` member, a doc phrase and the "detached N" log wording still matched the module. | Provenance, second pass | The first rewrite targeted the matches the Completeness lens listed, and nobody ran the new `lessons/misc.md` check (diff names, literals and loop shapes) over the result. | Fixed: renamed (`DetachWrecks`, `InvolvesPlayer`, `IsVillageHostileAction`), reworded, loop reshaped; the register names what was matched and what stays shared because it is the engine's. |
| 16 | LOW | Regression introduced by the fix for row 14: replacing the NRE catch with an `EncounteredParty` null check left `EncounteredBattle`'s settlement branch able to throw on a leaderless besieger camp (`BesiegerCamp._leaderParty` can be re-picked to null while besiegers remain). The throw depends only on the player's state, so every event's `Read` would throw: no sweep, one warning per event per hour, and the console claiming no battles exist. | Regression from a review fix | The replacement guarded the outer object and trusted the getter's inner branch, the trap `adapters.md` "a computed getter throws before your guard" names. | Fixed: the encountered-battle lookup is rebuilt null-safe from public members (`PartyBase.SiegeEvent`, `SiegeEvent.BesiegerCamp`, `BesiegerCamp.LeaderParty`). Owed in game: `taom.print_stuck_battles` while waiting inside a besieged town. |
| 17 | LOW | Convergence pass: the console row promised "the verdict the hourly sweep will act on" though the sweep stands down in co-op; the log section said a failing battle was "left alone" though a write can fail part-way; the feature-map row said every stuck battle goes "to the healthy side". | Doc claims vs code | Written before decisions 1 to 4 changed the behaviour. | Fixed: the console says when the sweep is standing down and no longer claims "no map events" for unreadable ones; docs corrected. |

**Not applied.** Efficiency F4, a per-battle log de-duplication: after findings 9, 12 and 16 only the
two failure warnings can repeat each hour, per battle that keeps failing, and that repetition is the
signal someone needs (simplicity criterion: about 12 lines for a rare path).

**Convergence pass** (one reviewer on the applied fixes, Step 4.6): no CRITICAL or HIGH; rows 15 to 17
came from it and were fixed in the fix loop.

## Root-cause pattern

Rows 1, 3, 6, 9 and 13 share one cause: **code shaped by an outside source was trusted where the
engine and the repo's own rules should have been read.** The player's module supplied the no-winner
exit (3), the dead sentinel (9) and loop details (6); a TAOM precedent supplied the sealed-type flow (1);
inference supplied the starvation claim (13). In every row the authoritative text (the setter, the
end-of-event handler, the lesson file, vanilla's behaviors) was one read away.

## Why each lens caught what it caught

All rows came from this review; none shipped. The Standards lens alone matched row 1 to its lesson; the
Engine and Data-flow lenses independently found row 2 in the Coop fork; Data flow traced the destroy rule
(3) and the quest parties (10); Completeness compared the module source (6) and ran the mutants (7, 12);
Efficiency computed the sweep frequency at each campaign speed (4, 5). The design lens turned three
findings (dry run, re-judge, the console command) into deletions.

## Proposed, not filed

- An adapter-return-type gate: scan `Main/Adapters/I*.cs` public members, generic arguments included,
  for ADR-007's sealed types, with an allowlist for the five existing hits the adversarial pass found
  (`ILordKitDonorAdapter`, `IObjectManagerAdapter`, `ISettlementOwnershipAdapter`,
  `IRemoteFiefSettlementSwapper`, `IVolunteerContextAdapter`). ADR-007 line 798 cites a test file that
  does not exist. Needs an issue on Mike's word.

## Lessons appended

- `lessons/adapters-taleworlds-api.md`: an engine type with no id gets a per-object adapter, and a class
  that gains injection and unit tests is a service.
- `lessons/campaign-mechanics.md`: a sweep that decides other parties' outcomes asks who controls each,
  not only whether this peer may tick.
- `lessons/misc.md`: write a provenance claim from a comparison against the source, not from memory.
