# Adversarial review — Enlistment battle-join fix (TAOM, Bannerlord 1.4.7)

You are an independent adversarial reviewer. Your job is to find bugs that a 5-agent Claude review
just missed. Assume the code is wrong and prove it. Do NOT edit anything — report only.

## Scope — review ONLY these files

- `Main/Adapters/EncounterAdapter.cs`
- `Main/Adapters/IEncounterAdapter.cs`
- `Main/Adapters/CommanderLordAdapter.cs` + `ICommanderLordAdapter.cs` (new `GetPartyId`)
- `Main/Features/Enlistment/ServiceBattleService.cs` + `IServiceBattleService.cs`
- `Main/Features/Enlistment/Hooks/EnlistmentBattleBehavior.cs`
- `Main/Features/Enlistment/EnlistmentReconciler.cs`
- `TAOM.Tests/Features/Enlistment/ServiceBattleServiceTests.cs`, `EnlistmentReconcilerTests.cs`, `EnlistmentContainerWiringTests.cs`

**Other modified files in the working tree belong to a CONCURRENT SESSION — ignore them entirely:**
`Main/Features/Arena/**`, `Main/Features/MissionDiagnostic/**`, `Main/Features/HeroRace/**`,
`Main/SubModule.cs`, `docs/reference/lotrlome-armory-snapshot/**`, `tools/audit_skin_mesh_refs.py`.

## Background — the bug being fixed

An enlisted player (party parked: `IsActive=false`, `IsVisible=false`, position-synced to an AI
commander) never joined any of the commander's battles. Root cause: the join was gated on
`MapEvent.CanPartyJoinBattle`, which is a **diplomacy** test — it requires every party on the
opposing side to be at war with the joining party's `MapFaction`. An enlisted player keeps their own
clan and is normally at war with nobody, so it returned false for every battle.

Five further defects were found on the same path (dead recovery event with no subscriber; siege
encounters seeded through `MobileParty` ids when a settlement defender has none; `JoinBattle`
reporting success when `JoinBattleInternal` silently `Finish()`es on a null `EncounteredBattle`;
rollback leaking a `PlayerEncounter`; and a discarded `SwitchTo` return that could freeze the map
event).

## Engine facts established during the fix — RE-VERIFY THESE, do not take them on trust

Decompile the installed 1.4.7 assemblies to check each. The whole design rests on them.

1. `MapEventManager.Tick` skips `MobileParty.MainParty.MapEvent`; the player's map event advances
   ONLY via `PlayerEncounter.Update()`, which runs from the `"encounter"` game menu. Therefore a
   `MapEventSide` acquired without that menu open freezes the event permanently, and the commander
   is left with `MapEventSide != null` and unable to start further encounters
   (`EncounterManager.HandleEncounterForMobileParty` gates on it).
2. `PlayerEncounter.Finish` → `FinalizeBattle` → `LeaveBattle` clears `MainParty.MapEventSide`.
3. `PlayerEncounter.RestartPlayerEncounter(PartyBase defender, PartyBase attacker, bool)` — that
   parameter ORDER. Passing two FOREIGN parties (vanilla always passes MainParty as one) is claimed
   safe only because `MainParty.AttachedTo` is null, which is what makes `SetupFields` resolve
   `_encounteredParty` to one of the two leaders.
4. `MapEvent.IsFinalized` (`_state == WaitingRemoval`) is the right "too late to join" test.
5. `LeaveSettlementAction.ApplyForParty` NREs when the party is not in a settlement, so the
   `InsideSettlement` guard before `LeaveSettlement()` is load-bearing.

## Attack these specifically

- **Ordering.** `TryJoin` does: state→EnlistedBattle, RestorePresence, SyncPositionTo,
  EnsureEncounterAgainst, LeaveSettlementIfUnderSiege, JoinBattle(verified), SwitchTo(verified),
  else rollback (Finish → state→EnlistedAttached → EnsureParked). Find an interleaving, engine
  callback, or re-entrancy where this leaves the player visible-and-active but not in a battle, or
  parked while inside a map event, or with state desynced from engine reality.
- **Re-entrancy.** `EnsureEncounterAgainst` calls `PlayerEncounter.Finish`, which fires menu work
  and campaign events. Can any of those re-enter `MapEventStarted`/`MapEventEnded` and recurse into
  `TryJoin` or `OnCommanderBattleEnded` mid-flight? What breaks if so?
- **Rollback correctness.** After a FAILED `SwitchTo` the join DID succeed, so `MapEventSide` is
  set. Does `Finish(false)` genuinely clear it in that specific state, or does it leave a parked
  inactive party inside a live map event? Note `CanPartyJoinBattle` requires all parties on both
  sides to be `IsActive` — a parked party stuck in an event would poison it for every other joiner.
- **The parked-party hazard.** Is there ANY path where `EnsureParked` (`IsActive=false`) runs while
  MainParty still has a `MapEventSide`?
- **Siege specifics.** Walk a siege assault end to end: which `PartyBase` is each side's
  `LeaderParty`, does `EncounteredBattle` resolve via the `SiegeEvent.BesiegerCamp` fallback, and is
  `LeaveSettlementIfUnderSiege` correct in both attacker and defender cases?
- **The recovery event.** `EnlistmentBattleBehavior.RegisterEvents` does `-=` then `+=` on a
  singleton service's plain C# event. Prove or disprove a leak/double-invoke across campaign
  sessions, save-load, and co-op client/host transitions.
- **Co-op.** Every world mutation must be host-only. Find any path where a client mutates.
- **Tests that lie.** The original bug survived because the suite stubbed the failing engine
  precondition to `true`. Look for remaining tests that assert our own mock's behaviour rather than
  real semantics — especially anything that would still pass if the production code were reverted.

## Output

For each finding: **severity (P1/P2/P3)**, file:line, what breaks, the concrete failure scenario,
and the minimal fix. Cite decompiled engine source for any claim about engine behaviour. If you
verify one of the five engine facts above and find it WRONG, that is the most valuable result you
can return — say so loudly. If you find nothing, say so plainly rather than manufacturing findings.
