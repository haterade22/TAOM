# Codex pre-review: Seat of Seeing, stage S1, 2026-11-03

Role: reviewer (`.ai/roles/reviewer.md`). Review only; make no edits, commits or AI calls. Read
`AGENTS.md` and `.ai/review-reference.md` first. Work only from this worktree and the engine cache:
do not search the rest of the disk. Scope is the uncommitted working tree of this worktree on top of
`a8aac6399` (`git diff` and `git status`; new files are untracked: `Main/Features/SeatOfSeeing/`,
`Client/Features/SeatOfSeeing/`, `Client/Adapters/SeatScreenAdapter.cs`,
`Main/Adapters/*Seat*Adapter.cs`, `TAOM.Tests/Features/SeatOfSeeing/`, `docs/features/seat-of-seeing.md`,
`tools/ingame_checks/seat-of-seeing.toml`). The contract is `docs/features/seat-of-seeing/build-spec.md`:
sections 2 to 5 are built. Sections 6 to 12 are later stages, so their absence is not a finding. Its
"Deviations" list says where the code differs on purpose.

Engine source for v1.5.6 is in `C:\Users\mikew\.taom-src\v1.5.6\` (for example
`TaleWorlds.MountAndBlade.View.Screens.MissionScreen.cs`, `TaleWorlds.MountAndBlade.Mission.cs`,
`TaleWorlds.MountAndBlade.Agent.cs`); `pwsh tools/taom-src.ps1 path <FullTypeName>` decompiles a type
that is not cached yet. Mark anything you cannot prove from code as UNVERIFIED. Nothing here has run in
the game.

## The change

The Seat is a commander's view. One key lifts the player's view off the hero, the view flies over the
field, and the same key brings it back with the engine's own return glide. `SeatView` is a
`[DefaultView]` in TAOM.Client, so it exists in every mission with a screen. `SeatSession` is the state
machine, `SeatScreenAdapter` owns every `MissionScreen` call, `SeatHeroAdapter` owns every agent write,
and `SeatMissionPolicy` decides what the Seat may do in each mission kind. `Patch140_SeatOfSeeing` holds
two small patches for the order panel while seated.

## Questions to answer with evidence

- **Frame ownership.** `UpdateOverridenCamera` returns true while seated, so `MissionScreen.UpdateCamera`
  does not run. Read `CheckForUpdateCamera` and the whole of `UpdateCamera`. What engine state does
  that method keep current that some other code reads, and does the Seat leave any of it stale in a way
  that shows or throws? Check `LastFollowedAgent`, `CameraViewAngle`, `_cameraSpecial*`, sound, culling
  and the near and far planes. Does `HandleUserInput` still running on a Seat frame change anything that
  matters?
- **Return glide.** `SeatScreenAdapter.BeginGlide` calls `IMissionListener.OnMissionModeChange` on the
  screen with `MissionMode.Deployment`. Read that whole method. What else does the call do besides the
  glide? What happens with a dead hero, with no followed agent, while mounted, in first person, or when
  another listener cares? Is the elevation sign right?
- **The hero.** With `MissionMainAgentController.Disable()` and the writes in `SeatHeroAdapter.StandFast`:
  does anything else still drive the main agent from input? Consider a mounted hero (does the mount
  keep moving?), a hero mid-attack or holding a block, on a ladder, using an object, or swimming. List the
  other TAOM and engine readers of player input on the main agent that the gate does not stop. Does a key
  that the Seat reads (game keys 14, 15, 24, the movement axes) also act on the hero or on a mission view
  while seated?
- **Other controllers.** `AutonomousMovementPlayerController` is a second controller (warg rage). Is the
  `HoldsHero` rule sound, including a rage that ends inside the window between two frames, and a mission
  where `WargMissionBehavior` is absent? Do any other TAOM features turn these controllers on or off, or
  assume the camera follows the hero: enlistment battle roles, the battle action bar, player switcher,
  Fords of War, Long Defeat?
- **Threads.** `SeatPresence` is a static read by two Harmony patches. Which threads reach those patches?
  `Mission.OnMainAgentChanged` goes through `DeferredCallbackQueue.RunOrDefer`: is the pump in
  `OnMissionTick` always reached while seated, including when time stands still?
- **The patches.** `Patch140_OrderFlagGetCollidedEntity` replaces `OrderFlag.GetCollidedEntity` while
  seated: compare it with vanilla line by line, and confirm the Harmony field injection names.
  `Patch140_MissionScreenScreenPointToWorldRay` extends a ray: list every caller of
  `ScreenPointToWorldRay` and say whether a longer ray can hurt any of them. Is the patch class's
  location and application phase right for a TAOM.Client patch?
- **Every mission.** `SeatView` is built everywhere. Judge it in a conversation mission, a town walk, an
  arena, a tournament, a hideout and its ambush, a siege, a replay, a mission with no main agent, a mission
  with no team AI, a co-op client and a dedicated server. Is `SeatMissionKindRules` right for each? Does the
  foreign camera rule (`SeatForeignCameraRule`) give a false match on any view in this repo or in the engine?
- **Lifetime.** Can `SeatPresence` stay true into another mission? Can the hero's controller stay off after
  the mission, after a fault, or after a mode change? Is any event left subscribed?
- **The floor and the view.** Is `SeatCameraMath.AboveGround` applied on every path to the screen? Does
  `Scene.GetGroundHeightAtPosition` with the camera's own Z give the right ground under a bridge or a roof?
  Do the depth of field calls in `ApplyFrame` match the engine's signature and run after the engine's writes?
- **Settings and counts.** Do the settings, the coop relevance rows, the key registration and the strings
  meet the gates? Is any key id or string key already taken?

Report each finding with severity (CRITICAL, HIGH, MEDIUM, LOW), file and line, impact, and the proving
code or a reproduction. Say PASS for a question you checked and found sound.
