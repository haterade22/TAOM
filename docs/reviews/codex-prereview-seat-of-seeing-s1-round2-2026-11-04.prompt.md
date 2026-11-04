# Codex re-review: Seat of Seeing, stage S1, review round 2, 2026-11-04

Role: reviewer (`.ai/roles/reviewer.md`). Review only; make no edits, commits or AI calls. Read
`AGENTS.md` and `.ai/review-reference.md` first. Work only from this worktree and the engine cache: do
not search the rest of the disk. S1 landed as one commit on top of `a8aac6399` on this branch, so these fixes
have no diff of their own: judge them from the code named below. Later commits on this branch may hold later
stages; review S1's files only. The contract is `docs/features/seat-of-seeing/build-spec.md`: sections 2 to 5 are built, and sections 6 to 12
are later stages, so their absence is not a finding. Deviations row 13 lists what this round changed.

Engine source for v1.5.6 is in `C:\Users\mikew\.taom-src\v1.5.6\` (for example
`TaleWorlds.MountAndBlade.View.Screens.MissionScreen.cs`); `pwsh tools/taom-src.ps1 path <FullTypeName>`
decompiles a type that is not cached yet. Mark anything you cannot prove from code as UNVERIFIED. Nothing
here has run in the game. Your two earlier reports are at the end of
`docs/reviews/raw/codex-prereview-seat-of-seeing-s1-2026-11-03.md` and
`docs/reviews/raw/codex-prereview-seat-of-seeing-s1-fixes-2026-11-04.md`. Do not repeat a finding from them
unless the fix did not close it or the fix made it worse. The drag data and the crosshair angle stay
accepted LOW items.

## What changed in this round

1. **Warg swap.** `AutonomousMovementPlayerController.SwitchMainAgentController`: the swap back now skips
   `baseController.Enable()` while `SeatPresence.IsSeated`, and always runs `Disable()` and clears `HoldsHero`.
   `else if (HoldsHero)` is gone. Two unused public events, their raises and their delegates were deleted to stay under
   the line-count baseline.
2. **Terminal session.** `SeatView.OnMissionScreenFinalize` and `OnRemoveBehavior` finish the session, set `_session`
   to null, and remove the view's `Mission.OnMainAgentChanged` handler.
3. **Deployment edge.** `SeatSession.OnScreenTick` reads and clears `_deploymentEnded` before the state branch, in any
   state, and the automatic entry also needs `facts.BattleMode`.
4. **Structure, meant to keep behaviour.** `ISeatScreenAdapter` moved to the client, and `SeatView` builds
   `new SeatScreenAdapter(MissionScreen, Mission)` per mission, with no null adapter and no registration. `ISeatHeroAdapter`
   has only `StandFast`, and the view owns the event subscription. `SeatSession` takes settings and permissions at construction.
   `SeatMissionClassifier.ReadFacts` takes a logger, and `OwnsTheCamera(Type)` holds the reflection. `DedicatedServer` left
   `SeatMissionFacts`. `SeatCameraMath` calls `FordsCameraMath.Clamp`, `WrapAngle` and a new `Pan`, and `FordsCameraMath.Step` calls `Pan` too.
5. **Hot path.** `SeatView` tests the static `TaleWorlds.InputSystem.Input.IsKeyPressed(key)` before the context's test.
   `SeatOrderRay.Collide` reads `Mission.Current.PlayerTeam.Side` inside its lambda, the form of vanilla's `OrderFlag.cs:343`.
6. **Named limits with no code.** Your interim note on the last run found `MissionMainAgentController.EarlyStart` (189-193): the engine subscribes
   `Enable` and `Disable` to the round events. That route has no guard, because `Main/_Module/SubModule.xml:8` makes TAOM a `Singleplayer` category
   module; the contract's section 4 and the feature doc's Known limits say so. Report it only if a mission TAOM does run can hold that behaviour.

## Questions to answer with evidence

- **Swap back.** Trace every caller of `SwitchMainAgentController` and every writer of the vanilla controller's `_activated`.
  Can vanilla's controller now turn on under the Seat by any route? Can the player end with vanilla's controller off and the Seat down
  (a stuck hero)? Walk these orders: rage starts then Seat is refused; Seat up then rage starts and ends before a screen tick; rider falls,
  Seat enters, warg dies, Seat leaves; Seat leaves during a rage then the rage ends; a fault during each of them; a mission that ends during each.
  Is `SeatPresence` set and cleared at the right point in `Enter`, `Leave` and `Fault` for that test? Compare the method outside the Seat
  with `git show a8aac6399:Client/Features/AdvancedCombat/AutonomousMovementPlayerController.cs`.
- **The deleted events.** Does anything reach them by name or reflection, in the repo or in `docs/reference/external-mod-couplings.md`?
- **Terminal session.** After either hook, can anything still reach the session, the screen adapter or the handler? Is a raise of
  `OnMainAgentChanged` on another thread during the null write safe? Does `Finish` still leave the hero's controller on and `SeatPresence` false on both paths?
- **Deployment edge.** Does any path still enter from a stale edge, or lose an edge it should use? What does a key press on the same
  frame as the edge do?
- **Adapter per mission.** Does anything still resolve `ISeatScreenAdapter` from the container? Is a dedicated server still safe (module, hot
  key registration, the hero adapter as a singleton, `IoC.Resolve` of it)? Does the singleton hold anything per mission?
- **Key test.** Is the two-part condition equal to the old test on every frame, including a click key bound to the Seat and a menu layer on top?
- **Fords parity.** Is `FordsCameraMath.Step` unchanged in effect after `Pan`?
- **New defects.** Read the changed files whole: `Client/Features/SeatOfSeeing/SeatSession.cs`, `SeatView.cs`, `SeatCameraMath.cs`,
  `SeatMissionClassifier.cs`, `Hooks/SeatOrderRay.cs`, `Client/Adapters/SeatScreenAdapter.cs`, `Client/Adapters/ISeatScreenAdapter.cs`,
  `Main/Adapters/SeatHeroAdapter.cs`, `Main/Adapters/ISeatHeroAdapter.cs`, `Main/Features/SeatOfSeeing/SeatOfSeeingModule.cs`.
- **Manifest.** Do the new `[[human]]` rows in `tools/ingame_checks/0.toml` state what the code does?

Report each finding with severity (CRITICAL, HIGH, MEDIUM, LOW), file and line, impact, and the proving code
or a reproduction. Say PASS for a question you checked and found sound.
