# Codex review: co-op hideout guest finds (2026-11-22)

You are an adversarial senior reviewer for TAOM, a Bannerlord v1.5.7 mod (.NET Framework 4.7.2) with a co-op
bridge to BannerlordCoop (source at E:/repos/BannerlordCoop, branch bannerlord-1.5.x). Read AGENTS.md first.
Review only; edit nothing; write no file in the repo. Report findings as P1 (wrong behaviour, crash, data loss, a
player able to change another player's or the server's state), P2 (likely defect), P3 (minor), each with
file:line, the failure scenario and the evidence you read. Say UNVERIFIED for any engine or Coop claim you could
not check in the installed DLLs or the Coop source.

## Scope

`git diff` in this checkout (uncommitted, base 4424c3398): three files.

- `Main/Features/CoopInterop/GuestTrade/GuestLadderFindService.cs`: `OnBattleWon` no longer skips a winner whose
  `EarnerRef.IsLocalMainHero` is true.
- `TAOM.Tests/Features/CoopInterop/GuestTrade/GuestLadderFindServiceTests.cs`: the test that expected no roll for a
  local winner now expects a roll.
- `docs/features/coop-interop.md`: the "Finds" paragraph of "Lords' Ladder for co-op players".

The reason for the change (your own earlier finding on this series): Coop's hideout finalize
(`BattleFinalizeHandler.CreateHideoutFinalizeContext`, which builds a `BarterPlayerContext` that sets
`Game.PlayerTroop` to the attacking player's hero) makes `SpecialResourceEventAdapter` mark that co-op player as the
local main hero, so the old skip gave a guest who cleared a hideout no find.

## Questions to answer

1. Double roll. Can one hero now be rolled twice for one battle: once here and once by `LordsLadderBehavior`
   (its `OnPlayerBattleEnd` and any other path that calls `RollMaterialDrop`)? Check where `OnPlayerBattleEnd` is
   raised, whether a Coop server or a non-dedicated "Host Coop Server" process raises it, and how
   `LordsLadderBehavior` gates on the co-op role.
2. The server's own main hero. On a Coop server, can `Hero.MainHero` (or whatever hero the event adapter marks as
   local in a normal field battle) appear as a winner with `PlayerControl.Player`? If so, which hero is it, and does
   it now get a find it should not get?
3. Single player and co-op clients. Confirm `RollsFinds` keeps the change off outside the server of a live session.
4. Every other source of `IsLocalMainHero` in `SnapshotVictory` and `SpecialResourceEventAdapter`: does any other
   Coop finalize context (field battle, siege, raid) set `PlayerTroop` in a way that changes who is marked local?
5. Is the doc paragraph now true, line by line?
