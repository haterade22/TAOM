# Seat of Seeing

## Overview

The Seat is a commander's view that any mission with a main screen can use. One key (Take the Seat, default Q, rebindable) lifts the
player's view off the hero over about 0.7 seconds. The view then flies freely over the field with the engine's own movement keys, and
the same key brings it back to the hero: with the engine's short return glide from third person, at once from first person. The hero
stands fast while the view is up, or, with a setting that defaults off, fights on under the engine's AI. Stages S1 (the camera), S2 (the
pointer, the panel and the screen), S3 (keeping to a soldier or a company), S4 (the hero fights on) and S5 (taking a soldier after a fall) of
[build-spec.md](seat-of-seeing/build-spec.md) are built; mission time (S6) and rising to give orders (S7) are not.

## Why This Exists

- **Vanilla behavior:** in a battle the camera follows the hero, in third or first person. Only the deployment phase has a camera
  that flies (`MissionScreen.UpdateCamera`, Deployment mode); once the fighting starts the player has no free camera.
- **TAOM requirement:** TAOM fields large Middle-earth battles, and the player commands them. Seeing the whole line, a flank or the
  gate while keeping the hero alive needs a camera that leaves the hero without losing the hero.
- **Without this feature:** the player either rides blind into the press or stays at the edge of it, and the order panel (which works
  from the hero's position) is aimed at whatever the hero's eyes see.

## Architecture

### Design Challenge

- The camera frame belongs to the engine's `MissionScreen`, which asks each mission view in turn whether it owns the frame
  (`UpdateOverridenCamera`) and falls back to `UpdateCamera` when none does. The Seat has to own the frame cleanly and hand it back
  with no pop, and a mode with a camera of its own (Fords of War) has to be left alone.
- The hero is the player's agent. Leaving it standing with its controller running would walk it into the enemy on stale input, and
  switching its controller to AI is a larger and riskier state (S4), so S1 only shuts the controller's gate.
- A default view is built in every mission with a screen. A frame with no key press and no stored deployment edge reads the key and
  one bool, and no setting and no mission fact, so a player who never presses the key pays one key test a frame.

### Solution Approach

`SeatView` is a `[DefaultView]` in TAOM.Client. It reads the key in `OnMissionScreenTick` (once per real frame; never in
`OnMissionTick`, which fast time repeats), and the flight input in `UpdateOverridenCamera`, which runs once a frame on real time.
It takes the Deployment to Battle edge from `OnMissionModeChange`, and subscribes once to `Mission.OnMainAgentChanged` right after it
builds the session. Both teardown paths, `OnMissionScreenFinalize` and `OnRemoveBehavior`, end the session: `Finish()`, then the session
is dropped and the handler removed, because a screen tick can still run after `RemoveMissionBehavior` and must not re-enter. Every rule is
in a pure type.

- `SeatMissionPolicy` (core, `Domain/`): what the Seat may do in a mission kind (camera, hero hand-off, orders, mission time). A battle
  gets everything; a hideout loses the hand-off; a tournament, arena, town or other mission gets the camera only; a network session
  keeps the camera and the kind's orders and loses the hand-off and time; another camera gets nothing. A dedicated server never asks:
  the only reader of the facts is in TAOM.Client, which does not load there.
  `SeatMissionKindRules` decides the kind from presence tests, and `SeatForeignCameraRule` decides whether another mod's view owns the
  camera (a view whose `UpdateOverridenCamera` is declared outside the TaleWorlds and SandBox assemblies; the reflection on one view
  type is `SeatMissionClassifier.OwnsTheCamera`). `SeatMissionClassifier.ReadFacts(mission, coop, logger)` takes the logger and the co-op provider from its caller; each view resolves the provider once at start.
- `SeatCameraMath` (client): the flight step, height and pitch clamps, the entry pose, the 0.7 s smoothstep blend with a shortest-arc
  yaw, the ground smoothing, the height limit and the listener height. It reuses `FordsCameraMath` for the camera basis, the pan
  speed, the pan itself (`Pan`, with the diagonal normalised inside), `Clamp` and `WrapAngle`, so the Seat flies like the Fords camera.
- `SeatSession` (client): Off, Rising and Seated, over `ISeatScreenAdapter` and `ISeatHeroAdapter`, so a test drives it. It takes
  everything at construction (`new SeatSession(screen, hero, settings, permissions, outcomes, logger, serial)`), and a screen tick is
  `OnScreenTick(keyPressed)`; a setting is read only past the early return of a frame with no key and no stored edge. Any throw latches
  the Seat off for the mission, gives the hero's controller back, logs once and shows one message. The deployment edge is one stored bool
  (`OnDeploymentEnded`), read and cleared at the start of every screen tick in any state, so an edge that arrives while the Seat is up is
  dropped and cannot enter after the leave. The automatic entry also needs Battle mode on the frame that consumes the edge, and a setting
  turned on later in the battle does not enter on an old edge.
- `SeatScreenAdapter` (client) owns every read and write on `MissionScreen` and `MissionMainAgentController`, including the controller
  gate. `SeatView` builds one per mission with `new SeatScreenAdapter(MissionScreen, Mission)`; its fields are readonly and never null,
  nothing registers it, and a server has no null for it because no core code calls it and TAOM.Client does not load there. The flight keys
  are the engine's named `GameKeyDefinition.Jump`, `Crouch` and `Zoom`. The frame ends the way the tail of `MissionScreen.UpdateCamera` ends (camera frame and bearing, vertical field of view from the
  view angle the player had at entry, floored at 50 degrees, `SceneView.SetCamera`, `Mission.SetCameraFrame` with the listener at most
  16 m above the ground), then keeps the view sharp: `scene.SetDepthOfFieldParameters(0f, 0f, false)` and
  `scene.SetDepthOfFieldFocus(0f)`, the two calls of the tail of `UpdateCamera`, which a Seat frame skips. Without them the view stays
  as blurred as the last frame that set depth of field. This is not the depth of field effect the contract drops.
  `UpdateFreeCamera` is never called: it writes `CameraElevation` with the opposite sign of the one `UpdateCamera` uses.
- The ground is `Scene.GetGroundHeightAtPosition` with `BodyFlags.CommonCollisionExcludeFlags` (the 544321929 that `MissionScreen`
  casts for its own free camera; a test pins the value), the larger of that and the water level. The native probe answers 0 when
  nothing is there, so vanilla's `num9 < 9999f` guard never fails and the Seat has none; a null scene gives NaN. Boundary positions pass
  `FiniteFloatValidator`, and a position that is not finite keeps the old X and Y.
- The floor: the ground under the camera is smoothed, and smoothing lags on a slope (at 1.5 m height, fast flight over a 45 degree
  hill leaves the smoothed ground about 3.8 m behind). `SeatCameraMath.AboveGround(pose, rawGround)` puts the pose at least 0.5 m
  (`GroundClearance`) above the raw ground and leaves it alone when the raw ground is not a real number. `SeatSession.OnFrame` applies
  it last, to the pose the screen gets, with the raw ground under that pose's own X and Y. While rising that point differs from the
  flight's, so the ground is read a second time there; a seated frame reads it once.
- The height limit: vanilla's free camera stops at the Z of the entity tagged `camera_height_limiter` (`MissionScreen.cs:1894-1897`,
  4601-4605). `SeatScreenAdapter.ReadHeightLimit` reads the tag's Z once at entry, and no tag or a Z at or below 0 means no limit. The
  session caps the flight height at the limit minus the ground, floored at the minimum height, so the wheel does not wind up past the
  limit, and `SeatCameraMath.CapHeight` caps the pose. The order in `OnFrame` is step, smooth, flight cap, pose, blend, pose cap, and
  the floor last, so a limit below the ground cannot put the view under it. Vanilla caps after its floor; the Seat differs on purpose.
- The return glide is the engine's own, the one it uses when Deployment ends: `IMissionListener.OnMissionModeChange(Deployment, false)`
  seeds the position, bearing and elevation gaps from the camera to the followed agent. Every leave except the mission's end
  (`Finish`) first writes the look: `WriteLook` sets `CameraBearing` and, through one cached reflection site (the private
  `CameraElevation` setter), the elevation, and says whether the setter exists. In Battle mode `BeginGlide` then calls the listener,
  unless the camera has no agent to follow (`GetSpectatingData(CombatCamera.Position).AgentToFollow` is null): its branch would zero the
  bearing and elevation just written, so the listener is not called and the look stays. In any other mode the view snaps to the hero.
  From first person the return is at once, not a glide: the first-person path of `UpdateCamera` takes the eye position and ignores the
  position offset the listener seeds, and a glide there would need a return state of its own.
- Warg rage: `AutonomousMovementPlayerController` is TAOM's second controller of the hero. A hit on the player's warg swaps vanilla's
  controller out for it, and it reads game keys, among them key 15, the Seat's sink key, as a dismount. The Seat and a rage do not mix.
  `IAutonomousMovementController.HoldsHero` is true from the swap into the rage until the swap back (set only by
  `SwitchMainAgentController`; the controller's `_activated` flag is the wrong signal, because it defaults to true and turns off only at
  the first mission tick). `SeatEngineFacts.RageHoldsHero` carries it, read by `SeatScreenAdapter.ReadFacts` through a cached lookup
  of the core interface, and it also needs the main agent to have a mount (the controller's own line 193 returns without one, so a
  rider who fell or dismounted during a rage is not held, and nothing else clears `HoldsHero` then). Off: the key does not enter and one
  message says why (`{=taom_seat_rage}`), and a battle that begins in the Seat checks it too, with no message. Rising or Seated: when the
  rage starts, the Seat leaves at once, with the glide in Battle mode. `SeatScreenAdapter.SetHeroController(true)` does nothing while the
  rage holds, so leaving does not start vanilla's controller beside the rage's own. The rage's own end turns vanilla's controller back
  on, except while the Seat is up: the swap back skips vanilla's `Enable()` when `SeatPresence.IsSeated`, and the Seat's own leave turns
  it on, because `RageHoldsHero` is false by then. Three routes reach the swap back with the Seat up: `OnWargDied` with no rage running
  (`OnWargDied.cs:38-39`, `FinishRageMode.cs:36`), for example when an arrow kills a calm warg; a rage that starts and ends between two
  screen ticks (fast time runs several mission ticks in one frame); and a rider who fell during a rage, took the Seat and then lost the
  warg. Each would turn vanilla's controller on under the Seat, and the hero would walk on the flight keys. Outside the Seat the swap does
  what it did before the Seat, so it does not test `HoldsHero`. The file is on the shrink-only line baseline, so two events with no reader
  (`OnLockedAgentChanged`, `OnPotentialLockedAgentChanged`) were deleted to pay for the swap's lines.
- `SeatScreenAdapter.SetHeroController` shuts the controller's gate with `MissionMainAgentController.Disable()` (not `IsDisabled`,
  which `UpdateCamera` overwrites each frame it runs), and `Enable()` opens it. `SeatHeroAdapter` (core, a singleton that holds no
  state, with `StandFast` as its only member) is stand fast: it clears what
  `ControlTick` last wrote (`MovementFlags`, `MovementInputVector`, `EventControlFlags`) with `HeadCameraMode` off, because nothing
  rewrites them while the gate is shut. The agent stays `AgentControllerType.Player`, so `IsCameraAttachable` and the order panel's open
  check keep passing. These four fields are written from the mission tick only, as vanilla writes them: entering and a main agent change
  each set one flag (`_standFastOwed`), and `SeatSession.OnMissionTick` does the write once, in a try that latches through the fault
  path. The engine may raise `OnMainAgentChanged` inside a native callback on a chosen thread, and again for the same agent on any change
  to Player, so the view's handler only calls `SeatSession.OnMainAgentChanged`, which sets the flag, and the write is safe to repeat.
  `SeatHeroAdapter.StandFast` marks itself with `MissionThreadGuard.NoteCall`.
- Two Harmony patches in `Patch140_SeatOfSeeing` (registry section holds the callers): the order flag's ray starts from the screen
  while the Seat is up (vanilla starts it at the hero's eyes, and throws with no main agent), and the screen-to-world ray is lengthened
  to 1000 m while the Seat is up (vanilla clamps it to 30 m with no boundaries). Both are no-ops while `SeatPresence.IsSeated` is
  false.
- Check lines (`tools/ingame_checks/0.toml`): `enter` pass on entry; a refused key press writes an `enter` skip line with its reason
  (`setting-off`, `denied`, `rage`, `custom-camera`) and an entry that cannot read the camera or the ground writes an `enter` fail line
  (`no-camera`, `no-ground`); Deployment, an interlude, photo mode and an ended mission stay silent. Every leave writes a `leave` pass
  line with `reason=` `key`, `setting-off`, `interlude`, `rage` or `finish`. The reason goes in the dedup key, so each reason logs once a
  mission. `glide` passes with `elevation=set`, fails with `elevation=missing` when the setter is missing, and skips with
  `reason=no-agent` when there was no agent to follow.

**Stage S2: the pointer, the panel and the screen** (contract section 6).

- `SeatKeys` (client) tests the keys beyond "Take the Seat" once a screen tick, so `SeatView` stays under its 150 lines: keys 531 and 536 only while Seated (key 532, stage S5's, is tested in any state but Off), each with the static `Input.IsKeyPressed` first and the view's own context only on a real press. `PressSeatKey`, `PressPending` and `TrialInput` stay on the view.
- The cursor: while Seated with the order panel open (`Mission.IsOrderMenuOpen`, a plain field read through the adapter) and `Show a cursor with the order panel` on, the adapter shows a small `GauntletLayer` that only asks for a visible cursor (`SetMouseVisibility(true)`, mask `Invalid`, so it takes no click). The adapter builds that one layer on the first show and after that only toggles its mouse visibility, the way vanilla keeps a cursor layer (the banner editor and the face generator); it never removes it, because `RemoveLayer` finalizes a layer, `AddLayer` refuses a finalized one, and the screen finalizes its layers itself at teardown. Hiding is a plain setter, so it is safe on every teardown path. Holding the middle mouse button hides the cursor and looks (`ISeatScreenAdapter.LookHeld`, shared with `ReadInput`); each frame the look is held, `MBWindowManager.DontChangeCursorPos` keeps the cursor where it was: a read of the native function shows that it stores the cursor's present position in a native lock and logs "Cursor already locked to different position" if a later call differs, so the call is backed. What clears that lock is not read, so the middle-button row stays a person's. The session derives the cursor once a frame: it is wanted when the panel is open and the setting is on, shown unless the look is held, and the look key is read only when it is wanted.
- Keep the order panel open: the session compares `Mission.IsOrderMenuOpen` with last frame's value, and a closing edge reopens the panel through the public `MissionOrderVM.OpenToggleOrder(false, false)`, unless a close route of the player was seen on this frame or the previous one (Escape as the engine's `ToggleEscapeMenu` hot key, the right mouse button, game key 68 pressed, game key 87 released, and game key 77, which closes the panel at the root of a layout that has a Return order and shares the view-orders bit; a signal counts only on a closing edge), or the troop transfer menu is open (the Transfer order closes the panel on purpose, and `OpenToggleOrder` clears `IsTransferActive`, so a reopen would cancel the transfer; `Mission.IsTransferMenuOpen` is read on the edge only). The signals are game keys that only the order handler registers on the scene layer, and a read of a key nobody registered throws, so the session reads them only while the panel is open or was open on the last frame: a town, an indoor scene, a village, an arena practice, a duel and an alley build no handler and still give the Seat its camera. The reopen is decided before the frame's cursor step, so an order never hides the cursor for a frame, and it is a pass only when `Mission.IsOrderMenuOpen` reads open after the call: the engine can refuse without a word when the hero or the last troop is gone, and that writes a skip line with `reason=refused`. The view model comes from `GauntletOrderUIHandler._dataSource` (protected), one reflection site. The reopen runs after the session's own tick and only if the state is still Seated. The pure rule is `SeatPanelRule`.
- The formation focus: `Patch140_FormationFocusPoint` sets the handler's `_centerOfScreen` to the pointer, in the handler's own coordinates (the ranged mouse position times the resolution, less half the usable area's margin: `SeatPointer`), while the Seat shows a cursor. Without it a left release charges the company at the screen centre, not the one pointed at. The patch marks the focus on every frame the field is set, and the session writes the check line for the first mark only, so the line costs one call a mission.
- The screen: key 536 sets `MBDebug.DisableAllUI` (the engine's own `toggle_ui` flag; `TwoDimensionEnginePlatform` draws nothing under it) and saves the value it replaced. It restores that value, and only if its own hide is in force, on the next press, at once when the order panel opens, a dialog shows or another layer takes focus (`SeatHideRule`; the dialog and the layer are read only while hidden), and in the shared cleanup. Photo mode is a second engine writer of the flag (`MissionGauntletPhotoMode.cs:118, 139, 183, 192, 200, 317`), so no Seat key acts in photo mode: the hide and keep presses are dropped there. Seven patches in `Patch140_SeatOfSeeing` remove what the sky view should not show or let the hero do: the crosshair, the attack direction arrows and the reload display (`GetShouldArrowsBeVisible`), the hit marker, the equip and drop wheel, the cheer wheel, the walk mode control, and the drop and equip keys (`DropWeaponTick`, `EquipWeaponTick`); the two tick prefixes also clear the view's pending press and hold time (injected by `ref`), because the view clears them only in its else branch, so a press under half a second made just before entering would otherwise drop or pick up on the first tick after the Seat. The registry section has the targets, the callers and the stance of each.
- The entry checks gain one fact: a radial menu on the mission screen (`MissionScreen.IsRadialMenuActive`) refuses entry silently, because a wheel left open would stay on screen and stop the order panel's input.
- The shared cleanup is one private session method that `Leave` and `Fault` both call (a fault runs each step through `Swallow`): it clears `SeatPresence` (and with it the cursor mark), gives the hero's controller back, removes the cursor, restores the hidden interface and drops the kept target. `OnMissionTick` tests each owed job on its own: the stand-fast write and a stored keep press.

**Stage S3: keep to a soldier or a company** (contract section 7). Key 531 stores a press in the screen tick; the session acts on it in the next `OnMissionTick`, because nothing proves a native ray is safe while the agent jobs run. With the order panel open and exactly one company selected the key keeps to that company; otherwise the adapter casts `Mission.RayCastForClosestAgent` from the pointer (the cursor when shown, else the screen centre), takes a mount's rider (a riderless mount is a miss), and compares the distance with `Scene.RayCastForClosestEntityOrTerrain` using the order placer's thickness and flags, so a soldier behind a hill does not count and one in a wood does (`SeatKeepRule.AgentCounts`, written to hold for the terrain distance's starting NaN). The adapter holds the kept `Agent` or `Formation` and reads it as occupant first (`AgentSlotIdentity.IsCurrentOccupant`), then `IsActive()`; a company with no units is gone. The Seat keeps its own camera while it keeps (`SeatOrbit`): the mouse turns the orbit with the pitch between 0.05 and 1.45, the wheel changes the distance between 4 and 90 m from 16 m, and the point eases to the target with `1 - exp(-5 dt)`. A soldier is looked at by the chest, a company by its ground median. The orbit pose goes through the boundary clamp, the height cap and the floor like a flight pose, and the stored flight follows the pose, so letting go continues from where the view is. Pressing the key again, or any movement, rise or sink input, lets go (the mouse and the wheel do not); a gone soldier or an emptied company lets go with one message. The shared cleanup drops the target.

**Stage S4: the hero fights on** (contract section 8). With `Your hero fights on in the Seat` on (`SeatHeroFightsOn`, default off until a run in the game proves it) and a mission whose policy allows `FightOn` (a battle with no network session), the hero is handed to the engine's AI while the Seat is up and takes the controls back after the leave.

- **The tick compares.** `SeatSession.OnMissionTick` calls `FightTick` first, before its early return on a fault. `wanted` is `SeatFightRule.Wanted(state, faulted, chosen)`: the Seat is up, no fault has latched and this entry chose to fight on (`_fightChosen`, set at `Enter` from `Permissions().FightOn && settings.HeroFightsOn`, so a setting turned on while seated waits for the next entry). `actual` is `_handed`, set only after `BeginFighting` succeeded. `SeatFightRule.Owed(wanted, actual)` names Begin, End or nothing, so an entry and a leave with no mission tick between them touch no agent, and an entry that follows a leave with no tick between keeps the hero on the AI with neither call. A player who never asks pays three bool reads a mission tick (`_handed`, `_fightChosen` and the hand-back debt).
- **The gate.** `SeatPresence.HeroFightsOn` is a second volatile static. `BeginFight` raises it after the hand-off succeeded; `EndFight` (in a `finally`), `Finish`, `Fault` and the fall rule lower it. The shared cleanup that `Leave` calls does not, because the hero stays on the AI for up to one more tick. Every patch for an AI main agent and the two morale models read the gate, never `IsSeated`; the boundary prefix is the exception and reads `IsSeated` with the policy's permission (see the guards below). A hand-back that failed keeps the gate down while its debt is retried (End, below), so a hero who is still on the AI in those ticks is open to the morale panic until a try works or the tries end. That is a named limit.
- **Begin, in `SeatHeroAdapter.HandToTheAi`** (refused first by `SeatFightRule.BeginRefusal`: the mission ended or is ending, no active main agent, or the main agent is not player controlled, which is what a Long Defeat bot run has): (1) `StopUsingGameObject(isSuccessful: false)` when he uses an object (a use cut short is not a success: `SpawnedItemEntity.OnUseStopped` grants the item at once on a success, `SpawnedItemEntity.cs:510-529`, past the engine's own gate at `:270-272`, and vanilla's interrupt of a player's use passes false, `Agent.cs:4070`); (2) `Controller = AI`, with no morale read or written (a Player hero has no `CommonAIComponent`, so `GetMorale` answers -1 and a write clamps to 0, so the morale refusal would then pin him at 0.01 instead of the 50 that the component the flip adds holds, `AgentCommonAILogic.cs:21-28`, `CommonAIComponent.cs:21`), made from the mission tick and never inside a `Mission.OnMainAgentChanged` handler (the setter raises that event from its own body, `Agent.cs:1252-1256`); (3) `CanRide` removed unless he is mounted or has a Horse item in his spawn equipment (the setter adds the flag on the way to Player and never removes it, `Agent.cs:1256`); (4) in Battle mode only: Alarmed watch state, AI unpaused, scripted movement disabled, the formation's riding order, and the join step of the formation's movement order (not for a Retreat order). A step that throws after the flip puts the controller back before it rethrows. (5) The morale refusal: `TaomBattleMoraleModel` and `TaomCustomBattleMoraleModel` ask `SeatPresence.RefusesPanic(agent.IsMainAgent)` first, so `CanPanicDueToMorale` answers false for the handed hero and `CommonAIComponent` sets morale to 0.01 and carries on (`CommonAIComponent.cs:84-93`). (6) The retreat check: while handed, the mission tick asks `SeatFightRule.RetreatCheckDue` (once a real second, from `Time.ApplicationTime`) and calls `StopRetreatingMoraleComponent` on a retreating hero, then the agent's own `StopRetreating` when he still reads as retreating (the component's stop acts only on its own flag); `retreat-stopped` is written only when he reads as stopped afterwards. A Retreat order retreats every AI unit of a company (`MovementOrder.cs:448-459, 764-768`), and a rout wounds a hero. The cadence is the contract's (8.1), so a hero near his retreat point or the line can fade out between two checks: the fade timer runs every 0.5 to 0.6 s (`CommonAIComponent.cs:54, 99-106`) and a fade-out cannot be undone. That is a named limit, UNVERIFIED in the game.
- **Which of vanilla's hand-over steps this needs.** Vanilla's own hand-over at the end of deployment alarms every AI agent, unpauses it, resets its enemy caches for a weapon user and syncs its behaviour parameters (`DeploymentMissionController.cs:57-69`). The Seat does the first two and adds a scripted-movement clear, because a hero who just left a use or a walk may hold a scripted position. `ResetEnemyCaches` is not called. Its native body (`IMBAgent.ResetEnemyCaches`, read with `native_decompile.py`) runs only when the agent's controller is the AI and then zeroes a block of fields in the agent's AI state (ids set to -1, counters and two timestamps reset); the decompile does not say which of those hold a target, so whether this hero carries a stale target out of the flip is UNVERIFIED, and the earlier claim that he has none to clear is withdrawn. `SyncBehaviorParamsIfNecessary` is not called: the join step of the formation's movement order refreshes his behaviour values (`MovementOrder.OnUnitJoinOrLeave` to `RefreshBehaviorValues`), which is what the engine does when a unit joins a company. Neither is proved in the game; `fight_on` reports the distance he moved.
- **End, in `SeatHeroAdapter.HandBack`**, in this order because the setter removes the AI components as it runs (`AgentHumanAILogic.cs:25-27`), and `StopUsingGameObjectAux` ends an AI walk or defence, disables the AI use and disables scripted movement only while `IsAIControlled` holds (`Agent.cs:3989, 4004, 4014, 4027`), so after the flip it would skip that cleanup (it would not throw): the two delegates the `HumanAIComponent` constructor subscribed (`OnAgentWieldedItemChange`, `OnAgentMountedStateChanged`, `HumanAIComponent.cs:136-139`) are set to null, since the component's removal never clears them and a second hand-off would pile up handlers (the engine nulls both when an agent is removed, `Mission.cs:3027-3028`); `StopUsingGameObject(false, None)` under the AI controller, which also ends a walk to an object or a defence and disables scripted movement (`Agent.cs:3980-4045`); `Controller = Player`; `StopRetreating` when the agent still reads as retreating (the flip leaves the native retreat mode alone: `IMBAgent.SetController` writes the controller fields and never the field `IMBAgent.SetRetreatMode` sets, though the three helpers it calls were not decompiled); the AI state flags cleared and the target frame cleared. The first three steps are in a `try` and the controller write and what follows are in its `finally`: a step before the write that throws still reaches the session, and the hero is not left on the AI by it. The retreat stop costs two lines against a hero who keeps running after a re-entry (a Retreat order given in the gap of up to a real second before the next check, or the re-attach of a detached hero under a Retreat order). `actual` is cleared before the calls so a throw does not re-arm the end on every tick, and the call is a debt: the faulted session keeps retrying it on later mission ticks (`FightTick` runs before the fault's early return), `SeatFightRule.HandBackTries` (3) tries in all with the first included, while the hero is active and still on the AI. After the last try one `fight-on` fail line (`reason=hand-back-failed tries=3 error=<type>`) is written and the retries stop. A hero who fell or is already the player's ends the debt with no line, and `Finish` drops it.
- **The fall.** While handed, the tick reads whether the main agent is active with the AI's controller. If not, it clears `actual`, the gate and the choice for this entry and owes nothing, because a controller write on a removed agent would make the corpse `Mission.MainAgent` (`Agent.cs:1255`). A `fight-on` skip line with `reason=fell` records it.
- **Guards an AI main agent needs**, all in `Patch140_SeatOfSeeing` (registry section): `Patch140_OrderPanelCheck` (a transpiler that swaps the one `Agent.IsPlayerControlled` read in `MissionOrderVM.CheckCanBeOpened` for `SeatOrderPanelGate.IsPlayerControlled`, so the no-main-agent test, the no-troops test and the mission-ending tests still run), `Patch140_BoundaryCountdown` (a prefix on `MissionBoundaryCrossingHandler.HandleAgentStateChange` that passes `isAgentOutside` false for the main agent while the view is in the Seat and the entry's policy allowed `FightOn`; it reads the seated flag and not the hand-off gate on purpose, because a stand-fast hero outside the line has the same ten second countdown, `MissionBoundaryCrossingHandler.cs:199-204, 222-236`. In a network session the policy allows no `FightOn`, so the prefix never changes the answer there: the server's own path calls the same handler for an agent with a `MissionPeer` (`:188-190, 215-219`), and clearing its timer (`:98`) would spare that agent the engine's punishment. A hideout, tournament or arena has no `FightOn` either, so a hero left outside the line there keeps the engine's countdown; `SeatPresence.HoldsBoundaryCountdown` holds the rule), and `Patch140_RangedSiegeInput` (a prefix that skips `RangedSiegeWeaponView.HandleUserInput` while he fights on: it sets `CustomCamera` when `PilotAgent.IsMainAgent`, and a custom camera beats the Seat, `RangedSiegeWeaponView.cs:81-86, 120-124`. The skipped body also gives the weapon its aim (`HandleUserAiming`, `:98`, reaches `RangedSiegeWeapon.GiveInput`, `:227`) and writes `Agent.Main.IsLookDirectionLocked` (`:123`), so network safety rests on the policy's `FightOn` permission and not on the method writing only the view's camera).
- **The ladder queue is not patched.** `LadderQueueManager` recruits an agent only while `IsAIControlled` (`ConditionsAreMet`, `LadderQueueManager.cs:434`), so a hero on the AI can be queued or listed as a climber, and the hand-back turns him into a Player agent in the lists. The engine's own passes handle that: a queued agent that is no longer AI controlled fails `ConditionsAreMet` at its next check and `RemoveAgentFromQueueAtIndex` guards its `AIMoveToGameObjectIsEnabled` read with `!IsAIControlled ||` (`:317-319, 559-566`); the climber loop resets the speed limit through `HumanAIComponent?.` (`:235`), and the controller setter clears the limits for a non-AI humanoid anyway (`Agent.cs:1259-1262`). What stays is a Player agent that remains in `_userAgents`, flagged and counted as a climber, until it leaves the zone (`:212-238`); it holds a climber slot for that time. The one dereference without a guard is `_queuedAgents[...].HumanAIComponent.AdjustSpeedLimit` at `:390`, reached for an agent that passed `ConditionsAreMet` at `:317` in the same call, so it fails only if the main thread flips that agent between the two lines, and whether the manager's parallel tick can overlap `OnMissionTick` is UNVERIFIED (`mission-frame-threads-and-native-costs.md` lists the agent tick only). A patch here would have to hold the pair lock the engine takes (`:396-407`); a guard for an unproved race fails the simplicity table, so this is a named limit with a `[[human]]` row and the decision stays reversible. `LadderQueueManager` is on no TAOM patch (`patch-targets.md`, the registry, `Main`, `Client` and `Dependencies` searched by type and method name).
- **Reported, not patched.** A banner bearer inside a formation always gives that formation its effect, and only a bearer outside it needs `IsPlayerControlled` (`CustomBattleBannerBearersModel.cs:33-44` and its sandbox twin), so a hero on the AI loses the effect for other formations only; `TaomBattleBannerBearersModel` keeps the hero out of that role on AI (`!IsMainAgent && !IsHero && IsAIControlled`). `OrderTroopItemVM.FormationOnOnUnitCountChanged` subtracts one for the player (`:383-387`), so the panel's count is one low. When the last soldier dies while the hero fights on, line 758 of `MissionOrderVM` no longer refuses (`Team.cs:148` makes the AI hero a bot), so the panel can open on a company that shows 0 men and commands only the hero. Nobody should read that line as a guarantee. Outside the Seat's targets, `BattleEndLogic.cs:300` falls through for an AI hero who is not running away (`_playerSideNotYetRetreatingTime` is then reset by the friendly agents that are not running), `MissionMainAgentController.cs:226` stops reading the player's input for an AI hero (the hand-off relies on it), and `MissionScreen.cs:1622, 1938, 2554` read the followed agent's controller for the camera, which the Seat owns while it is up.
- **Open, for the first run:** where an AI hero stands. `Agent.IsPlayerUnit` stays true through `IsPlayerTroop` (`Agent.cs:1221-1230`) and `Formation.TryRelocatePlayerUnit` (`Formation.cs:2902-2912`) tests `IsPlayerTroopInFormation`, so the arrangement may keep moving him to the back rank.
- **Marshal's Word.** The room that contract 8.1 leaves for a held line is kept: the hand-off writes only the controller, the `CanRide` agent flag and the AI state flags, never `AgentFlag.CanAttack`, so Marshal's hold-the-line `Qualifies` test and driver (not built yet) can exclude the main agent and restore any soldier who is no longer AI controlled without meeting this code.
- **Check lines:** `taom.seat-of-seeing.fight-on` with `event=begin`, `event=end` and `event=retreat-stopped` as passes, `reason=refused why=<mission-ended|no-hero|not-player>` and `reason=fell` as skips, and a fail line with the exception type when either call throws (the Seat latches off; the adapter writes the Player controller even when an earlier step throws, and the faulted session retries the hand-back on later mission ticks, three tries in all, then writes one more fail line, `reason=hand-back-failed tries=3`).

**Stage S5: take a soldier after the fall** (contract section 9). Key 532, `Take this soldier (TAOM)`, default `Delete`, read by `SeatKeys` whenever the state is not Off. `SeatSession.PressTake` stores the press (dropped in photo mode, and again at the tick, because photo mode can start or end between the two), and `OnMissionTick` acts on it through `ISeatScreenAdapter.TryTake`:

- **Policy and setting.** `SeatPermissions.TakeSoldier` is true only for the Battle kind with no network session (a hideout's duel has no known end for a stand-in, `HideoutMissionController.cs:727-732`, and the ambush has its own controller), and `SeatTakeSoldierAfterFall` (default on) must hold; each is read when the press acts. A refusal writes a `take` skip line with `reason=denied` or `reason=setting-off` and shows no message.
- **The pick.** With the main agent null (`SeatTakeResult.HeroAlive` otherwise, one message), the view keeps to a company: the nearest eligible unit of that company to the point the view orbits (`SeatTakeRule.Nearest`); else the soldier at the screen centre, cast as in section 7 (a panel cannot be open after a fall, `MissionOrderVM.cs:742-748`, so the pointer is the centre, and no mark shows the centre in this version). The eligibility is `SeatTakeRule.Check`: human, active (the slot's current occupant and `IsActive`), on the player's team, not using an object, not retreating, not fading out, not platform crew (`HowdahCrewAgentOrigin` or `MumakilCrewAgentOrigin`, whose deck rules assume the AI and who are not "using" their standing point, `HowdahCrewSpawner.cs:158`; the mumak crew are seated by a direct `OnUse`, `MumakilCrewSpawner.cs:189-190`), in a formation (`reason=no-formation`; the Fords take refuses a soldier of no formation too, `FordsAgentControl.cs:75-77`), a hero only in a campaign and only when his origin is the player's own party (`Campaign.Current?.MainParty?.Party`, never `PartyBase.MainParty`; with no campaign every hero is refused, as the engine's own take does, `Mission.cs:6395`), and a quarter of his health or more (`Mission.cs:6397`, NaN refused). A creature race is not refused by race: no rule in `troll-race.md` or `Main/Features/PlayerSwitcher/` marks one unplayable (the troll is "playable/NPC", `troll-race.md:5`), and non-humanoids fail the human test already. Known limits of the pick: a rider of a creature stays takeable, because unlike the Fords take this one asks for no plain mount (`FordsControlPick.MayRide`); `not-human` and `not-active` are dropped by the pointer ray before the rule runs (`SoldierAtPointer`), so those soldiers answer `nobody`, and a kept company whose every unit is refused answers `nobody` with no soldier's reason; and a soldier of another party stands on the player's team when his party is under the player's command or in his army (`Mission.cs:5220-5236`, `DefaultEncounterModel.cs:520-537`), so he can be taken (only a hero of another party is refused).
- **The take.** `Mission.TakeControlOfAgent` (`Mission.cs:6407-6414`: it cancels fast forward and sets the controller). The Seat never calls `SetPlayerCanTakeControlOfAnotherAgentWhenDead`, which arms vanilla's held Kick key (game key 16) on the followed agent (`MissionScreen.cs:1810-1812` reads `CanTakeControlOfAgent(LastFollowedAgent)`, and that method reads the flag, `Mission.cs:6395`). Four mission openers set the flag themselves (`BannerlordMissions.cs:155, 237`, `SandBoxMissions.cs:458, 872`) and so does `LongDefeatMissionLauncher.cs:89`, so `Patch140_TakeControlOfAgent` answers false from `Mission.CanTakeControlOfAgent` while the view is in the Seat; outside the Seat those missions keep vanilla's key, and vanilla's prompt on it reads the flag for its relevance and the postfix for its enabled state, so it may show disabled.
- **The command, in the same tick and only after a take the Seat made.** For each formation of the player team (`FormationsIncludingEmpty`, the list `AssignPlayerRoleInTeamMissionController` fills) whose `PlayerOwner` is not null and is no longer the slot's active occupant, `PlayerOwner` is set to the new agent (the setter takes the formation off the AI, `Formation.cs:283-294`, which undoes the `Team.DelegateCommandToAI` the fall caused, `Mission.cs:3039-3042`); then `Team.PlayerOrderController.Owner` is set (a public field, the controller vanilla sets, `AssignPlayerRoleInTeamMissionController.cs:54`; not `MasterOrderController`). A null value would hand a formation to the AI, so the new agent is never null there. This is not run from `Mission.OnMainAgentChanged`, which raises on every write of `MainAgent` and would change vanilla for a player who never used the Seat. The check line `taom.seat-of-seeing.take` carries the new agent's kind and the number of formations handed over.
- **After the take.** The view's `OnMainAgentChanged` handler already owes stand fast, so the new agent stands while the Seat is up; after the leave the player plays him. The Seat touches no scoreboard state: vanilla's own handler resets the score screen on a take (`MissionGauntletBattleScore:99-101`, `ScoreboardBaseVM:744`), and toggling `IsOver` would mark a live battle as over (`SPScoreboardVM:515`).
- **The message at the fall.** When the main agent changes and no active main agent remains while the Seat is up, and the take is allowed, one message names the `Take this soldier` key. A second fall (of the taken soldier) says it again.
- **Known effects.** `Team.Leader` returns `Agent.Main` first (`Team.cs:157-178`). `BattleAgentLogic.cs:159` takes the leader's hero, `:163` passes him to `OnCombatHit` as the commander, and `DefaultSkillLevelingManager.cs:88-90` gives the Tactics share to his party, so after a take a plain soldier leaves no commander with a party, and a hero of the player's party becomes the commander; a hero's formation keeps no captain and the engine has no successor, which is left alone. Wounds, deaths and routs flow through `Agent.Origin` (`BattleAgentLogic.cs:94-125`), and experience through `Agent.Origin` and `Agent.Character` (`:52, 149-180`), not the controller. `Mission.InitialPlayerAgent` is null after the first fall (`Mission.cs:3023-3026`), and its readers run at start. The field trial proves a custom battle only, whose logic and origin type differ from a campaign's (`CustomBattleAgentOrigin`), so the manifest owes one check in a campaign field battle.
- **TAOM's own readers of the main agent.** `HeroKillCounterMissionLogic` caches the first main agent and its comment already expects a later takeover (his kills are not the hero's): unchanged. `CareerHeroIdentityGate`, `AbilityActivationController` and `CareerPerkMissionBehavior` key career abilities and perks on the career hero's agent (#377), so a taken soldier gets none. `MissionAgentStatusCareerMixin` shows the energy bar only when the controlled agent is the career hero, so it hides for a soldier. `FellFoesAdapter` reports the player alive from the main agent (`:37-38`), so a taken soldier counts as the player. `EnlistmentMeritMissionBehavior.cs:98` treats a main agent whose character is not the player's as the player being down, which is right: the hero is down. `CareerEnergyBarPrefab` is the bar's markup, which the mixin shows or hides. None of these disagreed with the take on reading; none was run with a take. `CareerHeroIdentityGate` compares the agent's character with the hero's, and `AbilityActivationController` lets a V press fall through while the player controls another agent (`:46-51`, #377).

**Who reads the main agent as a player-driven hero** (contract 8.2: the grep, one line per file). Searched `Main`, `Client` and `Dependencies` for `IsPlayerControlled`, `IsAIControlled`, `IsMine`, `Agent.Main` and `MainAgent` in `*.cs`: 85 files, 7 of them the Seat's own. A known hit proved the search (`LostFootingRules.cs:39`, which the contract names). Formation-level `IsAIControlled` is a different flag (`Formation.IsAIControlled`): S4 never changes it, and S5 turns it false again on the formations it hands over, as before the fall.

| File | Reads | Result with the hero on the AI |
|---|---|---|
| `Client/Features/AdvancedCombat/AutonomousMovementPlayerController.cs` | the main agent's mount and velocity during a warg rage | the rage's swap-in tests the warg's own `IsPlayerControlled` (`PrepareOnFirstCall.cs:34`), which an AI-driven mount never meets; unchanged |
| `Client/Features/CareerSystem/UI/MissionAgentStatusCareerMixin.cs` | the main agent against the career hero | an AI hero is still the main agent, so the bar shows; harmless |
| `Client/Features/FordsOfWar/FordsControlView.cs` | `agent.IsAIControlled` in a log line | Fords has its own camera, the Seat stands down there, no hand-off |
| `Client/Features/FordsOfWar/FordsOfWarMissionViews.cs` | names `Agent.Main` in a comment | none |
| `Client/Features/LongDefeat/LongDefeatHudView.cs` | `Mission.MainAgent == null` for the orders delay | an AI hero is not null; unchanged |
| `Main/Adapters/AgentAdapter.cs` | `IsAIControlled` for the adapter | reports true for the handed hero, as it is |
| `Main/Adapters/CreatureSiegeAgentAdapter.cs` | `IsAIControlled` of an agent and of a formation | the siege role treats a handed hero as AI, which he is |
| `Main/Adapters/FellFoesAdapter.cs` | the main agent's liveness and position | an AI hero is alive; unchanged |
| `Main/Adapters/FordsAgentControl.cs` | `MainAgent`, `TakeControlOfAgent` | Fords only |
| `Main/Adapters/FormationAdapter.cs` | `Formation.IsAIControlled` | unchanged by S4 |
| `Main/Adapters/GaitVarietyAgent.cs` | `_agent.IsMainAgent \|\| _agent.IsPlayerControlled` | the main agent is excluded from gait variety either way |
| `Main/Adapters/IAgentAdapter.cs` | declares `IsAIControlled` | none |
| `Main/Adapters/ICreatureSiegeAgentAdapter.cs` | declares `IsAIControlled` | none |
| `Main/Adapters/IFormationAdapter.cs` | declares `IsAIControlled` | none |
| `Main/Adapters/IGaitVarietyAgent.cs` | declares `IsPlayerControlled` | none |
| `Main/Adapters/ISeatHeroAdapter.cs` | names `MainAgent` in a comment | the Seat's own |
| `Main/Adapters/LongDefeatBattlefieldAdapter.Diagnostics.cs` | the main agent and `Controller` for a log line | Long Defeat's own mission; the hand-off refuses an agent that is already AI |
| `Main/Adapters/LongDefeatBattlefieldAdapter.cs` | `AdoptMain(aiDriven)` flips the hero to the AI itself | the hand-off refuses (`not-player`), so the Seat never takes that hero back |
| `Main/Adapters/LostFootingAdapter.cs` | `IsPlayerControlled` for the census | see `LostFootingRules.cs` |
| `Main/Adapters/MarshalOrderAdapter.cs` | `Formation.IsAIControlled` | unchanged by S4; S5 turns it false again on handed formations |
| `Main/Adapters/StagedSiegeMissionAdapter.cs` | `target.IsAIControlled` of a formation | unchanged |
| `Main/Features/AdvancedCombat/BaseBehaviorTree/IsAiAgentDecorator.cs` | `IsActive() && IsAIControlled` | a handed hero passes as AI, which he is |
| `Main/Features/AdvancedCombat/BaseBehaviorTree/IsAiControlledDecorator.cs` | the mounted agent's `IsAIControlled` | the same |
| `Main/Features/AdvancedCombat/BaseBehaviorTree/IsPlayerControlledDecorator.cs` | `Agent.Main == mountedAgent` | the warg tree treats the main agent's mount as the player's; an AI hero's warg still runs the player branch, UNVERIFIED |
| `Main/Features/AdvancedCombat/CustomAttacksUtils.cs` | `IsMine` of the attacker and the victim | `IsMine` is the engine's own flag; unchanged |
| `Main/Features/AdvancedCombat/Services/SpatialGridDebugService.cs` | `Agent.Main` for a debug draw | none |
| `Main/Features/AnimationVariety/GaitVarietyService.cs` | skips `IsPlayerControlled` agents at build | read once at build, before any hand-off |
| `Main/Features/ArmourAcquisition/Hooks/HeroKillCounterMissionLogic.cs` | caches the first main agent | unchanged (S5 section) |
| `Main/Features/BannerBearers/Models/TaomBattleBannerBearersModel.cs` | names the vanilla gate in a comment | the hero stays out of the bearer role on AI |
| `Main/Features/BlowDiagnostics/Hooks/Agent_HandleBlowAux_BlowDiag_Patch.cs` | `victim == Agent.Main` | diagnostics only |
| `Main/Features/CareerSystem/Abilities/AbilityEffectExecutor.cs` | the main agent as the ability's context | the career key still acts from the Seat (a named S1 limit); an AI hero is the main agent |
| `Main/Features/CareerSystem/Abilities/IAbilityEffectExecutor.cs` | names the main agent in a comment | none |
| `Main/Features/CareerSystem/CareerPerkMissionBehavior.cs` | the main agent against the career hero | an AI hero is still the career hero |
| `Main/Features/CareerSystem/Diagnostics/CareerPerkMissionProbe.cs` | the main agent | diagnostics only |
| `Main/Features/CombatMechanics/Hooks/CombatMechanicsHooks.cs` | `attackerAgent.IsAIControlled` for the combat rules | a handed hero fights by the AI branch of the rules, as any AI unit |
| `Main/Features/CrashReport/Collectors/MissionStateCollector.cs` | the main agent for the crash report | none |
| `Main/Features/CreatureBandits/BehaviorTreeElements/IsCreatureBanditDecorator.cs` | `IsAIControlled` of a creature | not a hero |
| `Main/Features/CreatureBandits/Diagnostics/CreatureBanditDiagTicker.cs` | `IsAIControlled` for diagnostics | none |
| `Main/Features/CreatureBandits/Diagnostics/CreatureDiagSpawn.cs` | the main agent and `IsAIControlled` for diagnostics | none |
| `Main/Features/CreatureBandits/Hooks/CreatureBanditSpawner.cs` | `IsAIControlled` of a spawned creature | not a hero |
| `Main/Features/CreatureSiegeRole/CreatureSiegeRoleService.cs` | excludes navmesh faces for AI-controlled creatures | a handed hero is not a creature of that role |
| `Main/Features/CreatureSiegeRole/Domain/CreatureSiegeRules.Roles.cs` | releases a role when not AI controlled | the hero has none |
| `Main/Features/CreatureSiegeRole/Domain/SiegeRole.cs` | declares `IsAIControlled` | none |
| `Main/Features/CultureDoctrine/Hooks/RoutedFormationGuard.cs` | `Formation.IsAIControlled` | unchanged |
| `Main/Features/CultureDoctrine/Hooks/Tactics/FormationSlots.cs` | `Formation.IsAIControlled` | unchanged |
| `Main/Features/CultureDoctrine/Hooks/Tactics/TaomTacticBase.cs` | `Formation.IsAIControlled` before a tactic moves it | unchanged by S4; formations handed over by S5 are player run again |
| `Main/Features/CultureDoctrine/Hooks/Tactics/VolleyControl.cs` | the archers' `Formation.IsAIControlled` | unchanged |
| `Main/Features/CultureDoctrine/Hooks/TeamTacticProbe.cs` | `Formation.IsAIControlled` in a log line | none |
| `Main/Features/DevConsole/Cheats/AgentDamageCheats.cs` | `mission.MainAgent` as target and source | an AI hero is still the main agent |
| `Main/Features/DevConsole/Cheats/AgentDiagnosticCheats.cs` | `mission.MainAgent` | the same |
| `Main/Features/DevConsole/Cheats/LongDefeatCheats.cs` | `mission.MainAgent` position | the same |
| `Main/Features/DevConsole/Cheats/MissionSpawnCheats.cs` | `Mission.Current.MainAgent` | the same |
| `Main/Features/DevConsole/Cheats/StagedSiegeCheats.cs` | `mission.MainAgent` position | the same |
| `Main/Features/DreadAura/Hooks/DreadAgentGate.cs` | `IsAIControlled` and a `CommonAIComponent` | a handed hero is affected by dread like any AI unit (the component exists after the flip, with morale 50) |
| `Main/Features/Enlistment/BattleFormationPolicy.cs` | names `MainAgent` in a comment | none |
| `Main/Features/Enlistment/Hooks/EnlistmentBattleFormationMissionBehavior.cs` | `ReclaimAfterDeployment(MainAgent)` once, after deployment | before any hand-off |
| `Main/Features/Enlistment/Hooks/EnlistmentBattleRoleMissionBehavior.cs` | names `MainAgent` in a comment | none |
| `Main/Features/Enlistment/Hooks/EnlistmentMeritMissionBehavior.cs` | a main agent that is not the player's character | right for a taken soldier (S5 section) |
| `Main/Features/Enlistment/Models/TaomBattleInitializationModel.cs` | names `IsAIControlled` in a comment | none |
| `Main/Features/LostFooting/Domain/AgentProbe.cs` | holds `IsPlayerControlled` | see `LostFootingRules.cs` |
| `Main/Features/LostFooting/LostFootingRules.cs` | exempts `IsPlayerControlled` agents from the census (`:39`) | a handed hero loses the exemption and is counted like any AI unit |
| `Main/Features/MarshalsWord/Hooks/MarshalArrayLayout.cs` | `Formation.IsAIControlled` and `PlayerOwner` | the player's formations are found by owner or by a non-AI flag, so S5's hand-over keeps them in the array |
| `Main/Features/RaceAbilities/Hooks/RaceAbilityActivator.cs` | `IsAIControlled` of a kin for a rally | a handed hero can be rallied like an AI unit |
| `Main/Features/ShaderPrecompilation/ShaderPrecompilePlayerAgentGuard.cs` | names the player-controlled main agent in a comment | none |
| `Main/Features/SiegePropDiagnostics/Hooks/SiegePropDiagnosticsMissionBehavior.cs` | `Agent.Main` for a probe | diagnostics only |
| `Main/Features/SiegePropDiagnostics/Models/SiegePropSnapshot.cs` | names `Agent.Main` in a comment | none |
| `Main/Features/SmartCavalryAI/CavalryChargeService.cs` | `Formation.IsAIControlled` | unchanged |
| `Main/Features/SmartCavalryAI/Hooks/Patch31_FormationSetMovementOrder.cs` | `Formation.IsAIControlled` | unchanged |
| `Main/Features/SmartCavalryAI/Hooks/Patch31b_FormationSetTargetFormation.cs` | `Formation.IsAIControlled` | unchanged |
| `Main/Features/SmartCavalryAI/Hooks/SmartCavalryAIMissionBehavior.cs` | `Formation.IsAIControlled` | unchanged |
| `Main/Features/StagedSiege/Hooks/TacticStagedObjective.cs` | `Formation.IsAIControlled` | unchanged |
| `Main/Features/Warg/BehaviorTreeElements/CleanIfEnemyDied.cs` | the warg's `IsPlayerControlled` | a mount is never player controlled |
| `Main/Features/Warg/BehaviorTreeElements/FinishRageMode.cs` | `Agent.Main != null` | an AI hero is not null |
| `Main/Features/Warg/BehaviorTreeElements/OnWargDied.cs` | the rider's `IsPlayerControlled` (`:38`) | an AI hero takes the AI rider branch (`ResetAiControlledAgent`), which is the right one |
| `Main/Features/Warg/BehaviorTreeElements/PrepareOnFirstCall.cs` | the warg's own `IsPlayerControlled` (`:34`) | unchanged (the open question in the warg doc) |
| `Main/Features/Warg/WargBehaviorTree.cs` | the player-controlled branch of the tree | see the decorator above |
| `Main/Features/Warg/WargMissionBehavior.cs` | `agent == Agent.Main` on a dismount | an AI hero is still `Agent.Main` |
| `Main/Features/Warg/WargRiderHandManager.cs` | `Agent.Main` and its mount, hand and look writes | writes the main agent's rider hand for a warg mount; an AI hero on a warg keeps the engine's own AI hand, and the manager's writes on a controller it does not own are UNVERIFIED |

The engine's `MissionBehavior.OnAgentControllerSetToPlayer` (called on the way back, `Agent.cs:1276`) has no TAOM or engine override today.

**Interference reads** (for each new patch target, the snapshot, the registry, `Main`, `Client` and `Dependencies` searched by type and by method name; `CheckCanBeOpened` and `TakeControlOfAgent` were the known hits and each found one): `MissionOrderVM.CheckCanBeOpened` is named only in a comment of `EnlistmentBattleRoleMissionBehavior` (the engine refusal that the enlisted fantasy relies on, the `Team.IsPlayerGeneral` test, which the transpiler leaves alone). `Mission.CanTakeControlOfAgent`, `MissionBoundaryCrossingHandler.HandleAgentStateChange`, `RangedSiegeWeaponView.HandleUserInput` and `LadderQueueManager` are on no other TAOM patch. `Mission.TakeControlOfAgent` is called by `FordsAgentControl.Take` only, in Fords of War, where the Seat stands down. `CanPanicDueToMorale` is a model method, and the two Seat conditions sit beside the creature bandit, race ability and Long Defeat refusals. PatchShield can strip a TAOM patch on a missing API and is off with a co-op module active; each new patch stays shielded (the registry section), and a stripped transpiler leaves vanilla's refusal, a stripped skip leaves vanilla's input, and a stripped postfix leaves vanilla's Kick key.

**Conflict reads.** Marshal's Word's hooks: none reads the main agent or writes an agent flag; the room for a held line is kept (S4 section). `Client/Features/HostReview/*` and `Main/Features/HostReview/*`: Host Review is a battle with `DisableDying`, set by `HostReviewMissionLogic`; it neither reads the controller nor takes control, so the Seat's hand-off and take work in it as in any battle (a hero who cannot die cannot fall). `Client/Features/FordsOfWar/`: its own camera makes `ForeignCamera` true, so the policy gives the Seat nothing there, and `FordsAgentControl` is the only other caller of `TakeControlOfAgent`. `Main/Features/PlayerSwitcher/`: switches the campaign hero outside a mission and names no race rule for a take. `Main/Features/DevConsole/Cheats/*`: read the main agent only; none writes a controller. The one other writer of the hero's controller in TAOM is `LongDefeatBattlefieldAdapter.AdoptMain`, which the hand-off's `not-player` refusal respects.

Entry checks: the mode is not Deployment, Conversation, Barter or CutScene; photo mode is off; no radial menu is open; `MissionScreen.CustomCamera` is null (a
custom camera is a siege engine or a board game, and the message says the Seat cannot be taken from this view); the mission has not
ended; no warg rage holds the hero. A battle that begins in the Seat (`Begin Battles in the Seat`) enters Seated with no blend at the
current camera frame when Deployment gives way to Battle.

### Component Diagram

```
 MCM: the six Seat settings                   Options > Keybindings > Action: the Seat's keys (530, 531, 532, 536)
              |                                              |
   SeatSettingsProvider (lazy)                          SeatHotKeys (GameKey reference)
              \                                              /
               SeatView  ([DefaultView], TAOM.Client)
                  |   OnMissionScreenTick: the key        OnMissionModeChange: the Deployment to Battle edge
                  |   UpdateOverridenCamera: the frame    OnMissionTick: the hand-off tick, the stand-fast write, the take (the hero writes)
                  |   Mission.OnMainAgentChanged: a flag  OnMissionScreenFinalize, OnRemoveBehavior: Finish, then drop the session
              SeatSession  (Off / Rising / Seated, fault latch)
               /          \
   ISeatScreenAdapter      ISeatHeroAdapter
   (SeatScreenAdapter,     (SeatHeroAdapter, core,
    TAOM.Client, built      a singleton)
    per mission)
```

## Configuration

### Settings: MCM group "Seat of Seeing"

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `SeatEnabled` | bool | on | The Seat key acts. Off, the key does nothing and a Seat that is up comes down. |
| `BeginBattlesInSeat` | bool | off | When deployment ends, the view goes into the Seat at the camera in use, with no blend. |
| `SeatCursorWithPanel` | bool | on | While in the Seat with the order panel open, a cursor shows and orders land where it points. A click on one of the panel's buttons does not press it yet: it gives a Move order on the ground behind it. |
| `SeatKeepPanelOpen` | bool | off | While in the Seat, the order panel opens again after an order. With the game option that slows time (on by default) while orders are given, the stay runs at quarter speed apart from one frame per order. |
| `SeatHeroFightsOn` | bool | off | While in the Seat in a battle, the hero fights on with the company under the engine's AI and you get the controls back after the leave. It acts the next time you take the Seat. Off until a run in the game proves it. |
| `SeatTakeSoldierAfterFall` | bool | on | After the hero falls in a battle, the key Take this soldier in the Seat takes the soldier at the middle of the screen, or the nearest one of the company you keep to, who then carries your orders. Not in an online session. |

All are new settings. Never flip a default under an unchanged name once it has shipped: the json2 file keeps the saved value.

### Key: `TaomSeatHotKeyCategory`

Four game keys under Options > Keybindings > Action: "Take the Seat (TAOM)", id 530, default Q; "Keep to this soldier or company (TAOM)", id 531, default Insert; "Take this soldier (TAOM)", id 532, default Delete; and "Hide the interface (TAOM)", id 536, default End. Keys 533 to 535 belong to stage S6. The strings are
`str_key_name.TaomSeatHotKeyCategory_<id>` and `str_key_description.TaomSeatHotKeyCategory_<id>` for each of the four ids, in `Main/_Module/ModuleData/global_strings.xml`.
A default stays clear of every key the mission's scene layer reads. `MissionScreen` registers six categories on that layer, and one
physical key then acts twice: `B` was key 36 of `GenericCampaignPanelsGameKeyCategory`, so a Seat on `B` printed "Can't access banner
window right now!" on each press in a campaign mission. A test loops over every registered key and pins each default against the generic context, `GenericCampaignPanelsGameKeyCategory`, `ScoreboardHotKeyCategory`, `CombatHotKeyCategory`, the cheats category, `MissionOrderHotkeyCategory` and `ChatLogHotKeyCategory`. It leaves
`GenericPanelGameKeyCategory` out: that category holds `Q` and `E` for a panel's tab change, but only panel layers read them, and
vanilla's own use key is `E`, so `Q` is free in a mission. `Delete` is in `GenericPanelGameKeyCategory` only (the text widget's own key), and no scene layer category binds it.
The category is registered at process load by the module's `OnPhase`, and is never registered on a layer: `InputContext.RegisterHotKeyCategory`
sizes its slot list from the first category on that layer, so an id above 115 throws. The view holds the `GameKey` reference and tests
the view's own `Input.IsKeyPressed(key)`, which is the scene layer's context and goes quiet behind a menu, after the static
`TaleWorlds.InputSystem.Input.IsKeyPressed(key)`: the context's test builds a new click key array on each call (`InputContext.cs:310-312`),
and every mission would pay that each frame. An unbound key reads as not pressed.

### Flight controls (the engine's own keys, so rebinds keep working)

| Action | Source |
|--------|--------|
| Fly along the ground | movement axes, with the engine's 0.2 dead zone |
| Rise, sink | game keys 14 (jump, default Space) and 15 (crouch, default Z), rebindable |
| Fly twice as fast | game key 24 (zoom); doubles the ground speed and the rise speed |
| Change height | mouse wheel, a notch away from you lowers the view by 12 percent |
| Look | the mouse, with the engine's sensitivity and invert setting; the sticks when no mouse is active; with a cursor shown, the middle button |

### Constants

Minimum height 1.5 m above the ground and maximum 110 m; pitch from -0.35 to 1.45 rad (below the horizon is positive). Ground speed is
the Fords camera's `10 + 0.9 h` m/s; lift speed is `8 + 0.5 h` m/s. The first entry in a mission rises to 22 m at pitch 0.60 behind the
camera by 9 m; later entries keep the last height and pitch. The blend is 0.7 s of smoothstep. The ground under the camera is smoothed
with `1 - exp(-6 dt)`, and the view never goes below the raw ground plus 0.5 m (`GroundClearance`).

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/SeatOfSeeing/Domain/SeatMissionPolicy.cs` | Section 4 of the contract: kind, facts and permissions |
| `Main/Features/SeatOfSeeing/Domain/SeatMissionKindRules.cs` | Kind from presence tests |
| `Main/Features/SeatOfSeeing/Domain/SeatForeignCameraRule.cs` | Another mod's camera |
| `Main/Features/SeatOfSeeing/Domain/SeatCameraTypes.cs`, `SeatEngineFacts.cs`, `SeatState.cs`, `SeatMessage.cs`, `SeatChecks.cs` | Pure value types and check ids |
| `Client/Features/SeatOfSeeing/SeatCameraMath.cs` | Flight, blend, ground, height limit and listener arithmetic |
| `Client/Features/SeatOfSeeing/SeatSession.cs` | The state machine and the fault latch |
| `Client/Features/SeatOfSeeing/SeatView.cs` | The `[DefaultView]` entry point |
| `Client/Features/SeatOfSeeing/SeatMissionClassifier.cs` | Live mission to `SeatMissionFacts` |
| `Client/Features/SeatOfSeeing/Hooks/` | The fourteen Patch140 classes and `SeatOrderRay` |
| `Client/Features/SeatOfSeeing/SeatKeys.cs` | The keys beyond "Take the Seat" |
| `Main/Features/SeatOfSeeing/Domain/SeatPanelRule.cs`, `SeatPointer.cs`, `SeatHideRule.cs`, `SeatKeepRule.cs`, `SeatOrbit.cs` | The pure rules of stages S2 and S3 |
| `Client/Adapters/SeatScreenAdapter.cs` | `MissionScreen` and controller reads and writes, the cursor layer, the panel site and the kept target |
| `Client/Adapters/ISeatScreenAdapter.cs` | The interface the session drives (a client type, with no null) |
| `Main/Adapters/ISeatHeroAdapter.cs`, `SeatHeroAdapter.cs` | Stand fast, the hand-off to the AI and the hand-back, the retreat stop |
| `Main/Features/SeatOfSeeing/Domain/SeatFightRule.cs`, `SeatTakeRule.cs` | The pure rules of stages S4 and S5: what is wanted against what was done, the refusal of a hand-off, the retreat clock, who may be taken |
| `Client/Features/SeatOfSeeing/Hooks/Patch140_OrderPanelCheck.cs`, `SeatOrderPanelGate.cs`, `Patch140_BoundaryCountdown.cs`, `Patch140_RangedSiegeInput.cs`, `Patch140_TakeControlOfAgent.cs` | The four patches of stages S4 and S5 |
| `Main/Features/SeatOfSeeing/SeatPatchLog.cs` | The logger a patch in the client reads at patch time (the module hands it over) |
| `Main/Features/SeatOfSeeing/SeatOfSeeingModule.cs` | DI, the patch category, the key registration |
| `Main/Features/SeatOfSeeing/TaomSeatHotKeyCategory.cs`, `SeatHotKeys.cs` | The rebindable key and its reader |
| `Main/Features/SeatOfSeeing/SeatSettingsProvider.cs` | MCM provider with the lazy accessor |
| `Main/Features/SeatOfSeeing/SeatPresence.cs` | The one static the patches read |
| `tools/ingame_checks/0.toml` | The in-game verdict manifest (a draft with `issue = 0`, see below) |
| `Main/Features/SeatOfSeeing/Domain/SeatTrialRows.cs`, `SeatTrialSteps.cs`, `SeatTrialRules.cs`, `SeatTrialRoster.cs` | The field trial's Seat rows, the step table joined from them, pass rules and armies (pure) |
| `Client/Features/SeatOfSeeing/Trial/` | The field trial: `CommandTrialOptions`, `CommandTrialSettings`, `CommandTrialRun`, `CommandTrialAutostart` over `ICommandTrialGameHost` (`CommandTrialGameHost`), `CommandTrialGameManager`, `CommandTrialLauncher`, `CommandTrialScript` (the driver) with `CommandTrialScript.Seat.cs` (the eighteen steps), and `ICommandTrialHost` with `CommandTrialHost`, each with a `.Seat.cs` part |
| `tools/run_command_trial.py` | The runner and judge of the field trial |

## Dependencies

- `ISeatSettingsProvider` (feature): the six MCM values (the Seat, Begin Battles in the Seat, the cursor with the panel, keep the panel open, the hero fights on, take a soldier after the fall).
- `ISeatScreenAdapter` (TAOM.Client, built per mission): the screen and the mission camera. `ISeatHeroAdapter` (Adapters): the agent.
- `IOutcomeLog` and `IModLogger` (Core): the check lines and the fault log.
- `FordsCameraMath` and `Vec3f` (Fords of War): the camera basis and the pan speed, by direct call.

### Co-op: the four questions

`docs/features/coop-interop.md` "Four questions, not one":

1. **Is a co-op module enabled** (`ICoopPresenceProvider.IsCoopActive`)? Not asked. Nothing here is a startup decision, and the Seat
   neither needs co-op nor conflicts with it.
2. **Is a session live, and do I own the simulation** (`ICoopSessionProvider`)? Asked, read live at each use: the `NetworkSession`
   fact of the policy is `SeatMissionClassifier.InNetworkSession`, true for the engine's own network session (`GameNetwork.IsSessionActive`)
   or for `ICoopSessionProvider.IsSessionActive`, which is true on a co-op host and on a client alike. The engine's flag alone reads false in
   a co-op battle: it derives from `MBCommon.CurrentGameType` and is true only for the engine's own multiplayer game types
   (`GameNetwork.cs:338-392`), BannerlordCoop's source never reads `CurrentGameType` (all `*.cs` in `E:\repos\BannerlordCoop` searched), and its
   siege patches replace the engine's `IsClientOrReplay` read with their own check (`SiegeLadderAuthorityPatches.cs:33`). It has not been
   run in a co-op battle: UNVERIFIED in the game. `MarshalView.MayRun` reads the same `facts.NetworkSession`, so Marshal's Word is off in a
   co-op battle too. In a session the Seat keeps the camera and the orders its kind allows, and loses the hero hand-off and mission
   time (the engine turns scene time speed off in a session). The Seat writes no campaign or battle state. Stand fast writes agent state
   (movement flags, movement input, event control flags and the head camera mode), but only on this peer's own player agent and only
   from the mission tick, and the camera and the controller gate are this peer's own, so it needs no authority. `SeatView` is a
   `Local` row in `CoopMissionRoles`, and a `FightOn` hand-off and a take never run in a session: the policy denies both (`SeatPermissions.FightOn` and `TakeSoldier` need `!NetworkSession`), and both settings are `Presentation` in `CoopSettingsRelevance` for that reason.
3. **Is this the headless server** (`IDedicatedServerProvider`)? Not asked. A dedicated server builds no screen: TAOM.Client is not
   loaded there, so no `SeatView` exists, and the only reader of the mission facts is in TAOM.Client.
4. **Is this party or hero driven by a human** (`IPlayerControlQuery`)? Not asked. The Seat acts on `Mission.MainAgent`, which in a
   mission is this peer's own player agent; it credits, charges and rewards nothing. The hand-off and the take write the main agent's
   controller and the player team's order owner, in a single player battle only (the policy), and the take's results flow through
   `Agent.Origin` as for any soldier.

## Tests

- `TAOM.Tests/Features/SeatOfSeeing/SeatMissionPolicyTests.cs`, `SeatMissionKindRulesTests.cs`, `SeatForeignCameraRuleTests.cs`: the
  policy table cell by cell, the presence-test precedence and the assembly rule.
- `SeatCameraMathTests.cs`: every constant and rule of the contract table (`AboveGround` and the height limit rules included), including
  the wheel sign, the diagonal normalisation, the yaw wrap on the step and on the blend (across the pi seam both ways), non-finite and
  negative `dt` leaving the state unchanged, and the look signs.
- `SeatSessionTests.cs` with `SeatSessionFakes.cs`: the state machine against a scripted engine (entry checks, the blend and a bad
  `dt` while rising, the hand-back, the look written on every leave but `Finish`, the glide and its three lines, photo mode, the
  deployment edge stored by `OnDeploymentEnded`, used once and dropped in any other state or mode, an idle frame reading no fact and no
  setting, stand fast written only from the mission tick, the fault latch from the key, the frame, `Finish` and the permissions,
  the floor on a 3 m ground jump at minimum height, the second ground read while rising, the height limit and its floor, a boundary
  position that is not finite, the warg rage rules, `Finish` twice, and a check line for every refusal and every leave reason). The
  fake log keeps the real log's `(id, result, key)` dedup in its lines and counts every call, because a frame pays for the call.
- `SeatSessionPanelTests.cs`, `SeatSessionKeepTests.cs` and `SeatPanelRulesTests.cs` also pin the second pass over S2 and S3: no hide or keep press in
  photo mode, a refused reopen as a skip, the cursor kept across a reopening edge, no close-signal read around a panel that never opened, the
  Transfer order not undone by a reopen, and a low orbit that lets go at the height the flight starts from. `SeatScreenPatchWiringTests.cs` pins the
  injected fields of the two equipment tick prefixes; `CommandTrialSeatStepsTests.cs` has one failing-world test per pass conjunct of the S2 and
  S3 steps, and `CommandTrialWiringTests.cs` classifies every member of the trial host.
- `SeatOfSeeingChecksRenderTests.cs`: the [Check] lines a real `OutcomeLog` writes for the session, compared with the fixtures that
  `SeatOfSeeingManifestTests` in `tools/tests/test_ingame_verdict.py` reads, and the four failure lines of the feature in the code (the view could not start, the key could not register, the order ray threw, the order panel's transpiler found no single read).
- `SeatOfSeeingWiringTests.cs`: the Domain folder names no TaleWorlds type, the module, the container (the adapters are singletons and no
  screen adapter is registered), the key and flight read sites, and (as source-text gates, the view being a MissionView that needs a live
  mission) the two depth of field calls ending `ApplyFrame`, the rage lookup with its mount test, the `SetHeroController` guard, the
  view's two-part key test, its frame and mission tick calls, its one subscription to `OnMainAgentChanged` and the terminal end of the
  session from both teardown paths, the deployment edge from `OnMissionModeChange`, the engine's ground mask, and the hero adapter's four
  writes. The 150 line limit of the view and the patches is `EntryPointLineCountGateTests`'s. `WargControllerSwapTests` pins `HoldsHero`
  on the interface, the swap's two branches (the swap back skipping vanilla's `Enable()` while the Seat is up) and the two deleted events.
- `SeatPatchWiringTests.cs`: both patch targets with the Harmony parameter and field names, the category, the prefix off the Seat and its
  answer when the seated ray throws, and the postfix arithmetic. `SeatMissionClassifierTests.cs`: `OwnsTheCamera` for the Fords view
  (true) and the Seat's own, the action bar's and the rage controller's (false).
- `TaomSeatHotKeyCategoryTests.cs` (default `Q`, unbound in four engine categories, no id collision with the other TAOM keys, Realm
  Borders 520 and 521 included), `SeatSettingsProviderTests.cs`; the provider also joins `HotPathSettingsProvidersTests`, the
  `CameraElevation` setter joins `ReflectionSiteBindingTests` (`reflection-sites.md` Category B), and `Patch140` has its section in
  `harmony-patch-registry.md`.
- `tools/tests/test_ingame_verdict.py`: `SeatOfSeeingManifestTests` runs the real manifest against twenty-seven fixtures (count them with `ls tools/tests/fixtures/ingame_verdict/seat_*.txt`; the eight of stages S4 and S5 are a hand-off visit, a visit with a take, a hand-off with no hand-back, a hero who fell while handed, a refused hand-off, a throw, a take that handed over no formation and refused takes) (a full visit, a
  fault, a missing elevation setter, an enter only, a leave with no agent to follow, refused key presses, an enter that could not read,
  each of the four failure lines of the feature, a full visit of stages S2 and S3, a reopen that cannot reach the view model, a reopen
  the engine refused, a keep that finds nobody, an interface hidden and never restored, a keep then a leave, a keep then a flight let-go,
  and a Seat patch that failed to apply). A sweep test reads every fixed-prefix `LogError` of the Seat out of the code and requires a fail pattern in the enter check for it, so a new failure line cannot be missed.
- Stages S2 and S3: `SeatPanelRulesTests` and `SeatKeepRulesTests` (the pure rules at their edges, the orbit's signs and its continuity with
  the camera), `SeatSessionPanelTests` and `SeatSessionKeepTests` (the cursor, the hidden interface, the reopen with each close route on this
  frame and the previous one, the shared cleanup with a throwing step, the mission tick's separate jobs, the keep press, the orbit, each let-go
  reason), `SeatScreenPatchWiringTests` (the eight targets, the crosshair call, no override, the injected field), `SeatPanelWiringTests`
  (source pins of the view's key reader, the focus patch's mark and the adapter, and that no other TAOM code writes `MBDebug.DisableAllUI`; the pins of `SeatSession`'s own text went where a behaviour test covers the case),
  `CommandTrialSeatStepsTests` (each new step passes on a working stand-in and fails with its own facts when its one feature is broken),
  `SeatTrialAimTests`, and the widened marker gate in `CommandTrialWiringTests`.
- Stages S4 and S5: `SeatFightRuleTests` and `SeatTakeRuleTests` (the pure rules at their edges, NaN included), `SeatSessionFightTests` (the tick that compares wanted with actual: a begin, no second begin, an entry and a leave with no tick between, an entry after a leave with no tick between, a refusal that clears the choice, a throw that latches, a hand-back whose call throws, a fault that lowers the gate and still hands the hero back, the fall, the retreat check once a real second, no clock or controller read when nobody is handed), `SeatSessionTakeTests` (the message at the fall, the stored press dropped in photo mode at the press and at the tick, each outcome with its line and message, the policy and the setting read when the press acts), `SeatFightWiringTests` (the gate, the morale refusal in both models, the hand-back's order, no morale call in the adapter, the tick's place, the take's order and its use of the controller vanilla sets, the two delegates as public `Action` fields) and `SeatFightPatchWiringTests` (the four targets and their parameter names, no subclass override of `HandleUserInput`, the transpiler on the installed engine's IL and on a stream with none and with two, the patch applied through Harmony, and that `LadderQueueManager` is not patched). `SeatTrialFightRulesTests` pins the two judgments of the new steps (`FoughtOn`, `CommandHandedOver`) and their windows, and `CommandTrialFightStepsTests` runs the five new steps against the stand-in and breaks one fact at a time (a hand-off that never comes, a hero who stands still, a retreat or a panic at the hand-off, a gate that never rises or never falls, a panel that refuses him, a hand-back that comes late or never, a morale that panics him, a hero who cannot die, a fall message that is missing, repeated or wrong, a take of the wrong soldier, a command that is not handed over, left on the AI or owned by fewer formations, and a seat that faults). The review round added: `SeatSessionFightTests` for the hand-back debt (three tries in all, a retry that writes the controller, no retry for a hero who fell, one who is already the player's, or after `Finish`) and for the boundary hold by policy; `SeatFightPatchWiringTests` for the hold both ways; `SeatNetworkSessionFactTests` for the network fact and Marshal's Word following it; `SeatPlayerTextTests` for three corrected messages in the code, the English row and the 12 language rows; `SeatTakeRuleTests` for the no-formation refusal; and `morale_held` now reads the morale back right after its write (`SeatTrialRules.AtMoraleFloor`), with a stand-in whose write takes no effect.
- The field trial: `SeatTrialStepsTests.cs` (the table, the choice of steps, each pass rule at its edges, the line shape),
  `SeatTrialRosterTests.cs` (the ids against the repo's own ModuleData, the mounted troop's horse), `CommandTrialOptionsTests.cs`,
  `CommandTrialScriptTests.cs` with `CommandTrialWorld.cs` and `CommandTrialWorld.Seat.cs` (a stand-in for the engine and the Seat: a
  whole trial, each step broken one fact at a time, the deployment finishing only after the refusal step has reported, the times of
  every press and picture, and the driver's own rules: the in-mission-tick marker, a step with no verdict, no code after a verdict),
  `CommandTrialRunTests.cs` (the armed, ended, refused and failed decision of `CommandTrialRun.Script`), `CommandTrialAutostartTests.cs`
  (every exit of the run through a fake game: `start_refused`, the move to ending, `exit_stalled`, a lost done file with the quit still
  following, `autostart_failed`, `never_live`, `wall_limit`, `mission_closed`, `live_limit`; the done and status text, `TryWrite`,
  `UnfinishedReason`, `DoneLine` and `Armed`), `CommandTrialWiringTests.cs` (source-text gates: one path for the key, the flag cleared
  in the screen tick, one screen adapter, one environment read, the null checks, no record, no new view, no agent write in the host or
  the script, no engine type in the autostart), `SeatSessionTests.cs` (the input override, `Frames`, `GlideBegan`), and
  `tools/tests/test_run_command_trial.py` (the judge on a copy of a real run's lines, the build check in each state, a real git
  repository for the changed-file list, the arguments, ties to the C# names, and one whole run with a fake game).

## How to verify in game

**Preconditions:** a Custom Battle or a campaign field battle, the MCM group "Seat of Seeing" on (it is by default), no restart
needed. Not in Fords of War: its own camera makes the Seat stand down. `tools/ingame_checks/0.toml` is a draft manifest with
`issue = 0`: `python tools/ingame_verdict.py --issue 0` reads it. When the issue exists, rename the file to `<issue>.toml` and set
`issue`. The manifest is shared with Marshal's Word, so a session of one feature alone exits 4, because the other feature's checks are
NOT REACHED; `--issue 0 --sessions 2` over one session of each feature exits 0.

1. Press Q after the deployment ends. The view rises and the log shows `[Check] id=taom.seat-of-seeing.enter result=pass`. A result only
   a person can see: the blend is smooth, the hero stands still. The first press in a mission shows one message that the same key
   returns to the hero.
2. Fly: WASD, Space and Z (game keys 14 and 15, rebindable), the wheel, the mouse. After about a second
   `taom.seat-of-seeing.frames result=pass` appears (60 frames).
3. Press Q again. In third person the view glides back to the hero; from first person it is at once. The log shows
   `taom.seat-of-seeing.leave ... reason=key` and, in third person, `taom.seat-of-seeing.glide result=pass ... elevation=set`.
   `elevation=missing` means the `CameraElevation` setter was not found. Missing: `CameraElevation` keeps what `HandleUserInput`
   integrated from the mouse during the stay (MissionScreen:3051-3053, 3249-3251), so the glide's vertical angle starts from an
   unrelated value (4569), a visible jump.
4. Open the order panel from the Seat with its usual key. The order flag lands under the screen centre. A person has to look.
5. `taom.seat-of-seeing.fault` must stay absent. A fault line fails all twelve checks in the manifest.
6. Fly low and fast (hold the zoom key) across a hillside, then over a sharp rise. The view stays above the ground and the picture
   stays sharp; a person has to look. `HandleUserInput` keeps running: while the followed agent is the player's and game key 24 is down
   it raises `_zoomAmount` with any weapon on keyboard and mouse (a gamepad needs a ranged weapon out) and writes depth of field
   (3074-3078, 3119-3142, 3298-3303), and key 24 is the Seat's fast key. With the zoom key down before pressing Q, the view is sharp,
   not blurred.
7. Only if a rage holds the hero in single player at all (see Known limits): get a warg rage (a hit on the player's warg). Press Q:
   nothing happens, the message says the rage holds you, and the log shows `enter result=skip ... reason=rage`. Press Q, then trigger
   the rage: the view returns at once, with `leave ... reason=rage`. Fall during a rage, then press Q: the Seat enters.
8. Press Q during deployment and in photo mode: nothing happens, and no line is written. Press Q in Fords of War: the Seat does not enter
   (Q keeps its Fords camera turn), and the log shows `[SeatOfSeeing] another view owns the camera` and `enter result=skip ... reason=denied`.
9. Press Q on a manned siege engine and at a tavern board game: the message "You cannot take the Seat from this view." appears, the
   view stays, the game continues, and the log shows `enter result=skip ... reason=custom-camera`. In a campaign battle, Q prints no
   banner line.
10. Kill the hero's warg while seated with no rage running: WASD does not move the hero. Let the hero fall while seated and leave the
    Seat more than 2.1 s later: the look stays where it was, and the log shows `glide result=skip ... reason=no-agent`.
11. From the Seat press the drop and equip keys (game keys 13 and 22), the equip wheel key (34) and the cheer wheel key (31): nothing happens
    (stage S2 closed them), and a press under half a second made just before entering does not drop a weapon on the first tick after the Seat. The
    walk mode control opens on the hot key `ControlModeToggle`, the controller's left pad down, so on a gamepad press left pad down: no control
    opens. Game key 30 (CapsLock) is read by `MissionMainAgentController.cs:811`, which the shut controller already stops. The career ability key (V)
    and the perspective toggle (game key 27) still act.
12. Options > Keybindings > Action shows "Take the Seat (TAOM)" with the key Q and no ERROR text; the MCM group renders its rows; with
    the game in Russian the Seat's texts show the English seeds and no raw key.
13. Only if a rage holds the hero in single player at all: fall from the warg during a rage, take the Seat, then kill the warg. The
    flight keys do not move the hero.
14. Turn on Begin Battles in the Seat and start a battle with a deployment: when the deployment ends the battle begins in the Seat, with
    no blend (`enter ... blend=0`). The enter check accepts `blend=0` and `blend=1`, so a key press alone passes it, and only this step
    proves the setting.
15. Turn off the Seat in the MCM group: the key does nothing (`enter result=skip ... reason=setting-off`), and a Seat that is up comes
    down (`leave ... reason=setting-off`).
16. With a first-person hero holding a block or a swing, press Q: the hero stops, and the head shows.

17. Open the order panel from the Seat: a cursor shows and `taom.seat-of-seeing.cursor result=pass` appears. Move it over an enemy company; the
    company under it is the one a left release charges. Hold the middle mouse button: the cursor hides and you look around, and on release it
    is where it was. In the Seat the attack direction arrow, the crosshair and the hit marker are gone, also after loosing an arrow just before entering.
18. Turn on Keep the order panel open, give an order: the panel opens again (`panel-reopened result=pass`). Escape, the right mouse button, the
    panel key and the hold key each leave it closed. A conversation that closes the panel is not followed by a reopen.
19. Press End: the whole interface hides (`hide ... interface=hidden`). Open the panel, a dialog or another screen: it comes back at once
    (`interface=restored reason=panel`, `dialog` or `layer`). A `toggle_ui` typed in the console before entering keeps its choice after you leave.
20. Point at a soldier and press Insert: the view orbits the soldier (`keep result=pass ... kind=Soldier`); the mouse turns the orbit, the wheel changes the
    distance, any movement key lets go with no jump (`let-go ... reason=flight`). With the panel open and one company selected, Insert keeps to
    the company. A soldier behind a hill is not picked. Let the kept soldier die: one message, and the view stays where it was.
21. A click on one of the panel's buttons does not press it yet: it gives a Move order on the ground behind it (contract 6.1 names this until Marshal's Word stage M7).
22. With Keep the order panel open on: in a town, an indoor scene, a village, an arena practice, a duel and an alley the Seat flies and returns and
    nothing faults; a Transfer order opens the transfer window and the panel stays closed after it; after an order given with the cursor the pointer
    stays where it was. Press Insert in the Seat: no screenshot is taken (the engine's debug category binds Insert to its screenshot hot key,
    `DebugHotKeyCategory.cs:598`; no managed reader was found, and a native one is unproved). Pick a soldier behind a hill at long range: the ray does not take that soldier.

23. Turn on "Your hero fights on in the Seat" and take the Seat in a field battle: the log shows `fight-on ... event=begin` once the next mission tick has run. The hero keeps fighting inside his company; he does not stand, and he does not rout when his morale falls. Open the order panel from the Seat: it opens. Give his company a Retreat order: he does not run (`event=retreat-stopped` may appear). Where he stands in the ranks is the open question under "Stage S4".
24. Leave the Seat: the hero stops within a frame or two, the glide returns to him, WASD and the mouse work (`fight-on ... event=end`). Take the Seat and leave it in the same moment: nothing is written for the hero.
25. In a siege with the setting on, let the hero climb a ladder or man an engine, then leave the Seat on it: no error, the camera stays the Seat's while he mans the engine, and the controls come back. Leave the hero outside the mission boundary while seated, for more than ten seconds: the battle does not end, and he gets a fresh ten seconds after the leave.
26. Let the hero fall (while fighting on or standing fast): one message names the Take this soldier key, and `fight-on ... reason=fell` appears if he was handed over.
27. Options > Keybindings > Action shows "Take this soldier (TAOM)" with the key Delete and no ERROR text. After the fall, press Delete with a soldier at the middle of the screen (or keep to a company first): the soldier becomes the player, stands fast while the Seat is up, and plays normally after the leave; every company the hero owned obeys the order panel (`take ... kind=Soldier formations=N`). A press at a soldier who is retreating, wounded below a quarter of his health, an enemy, a howdah or mumak crew member or a soldier of no formation is refused with one message and a `take` skip line with its reason (a soldier the pointer ray cannot reach answers `nobody`).
28. In a custom battle or a campaign field battle with deployment, the vanilla Kick key still takes a soldier outside the Seat, and from the Seat it takes nothing. After a take the escape menu's quit text reads Retreat. In a campaign field battle, check the wounds, the experience and the Tactics share after a take.

An unreachable step is reported with the command tried and the unmet precondition. No person has run the 28 steps. The [field trial](#field-trial) covers, without a person: the camera half of steps 1, 2 and 3, the Deployment half of step 8, and the Seat's side of steps 17 to 20.
Step 17 is covered for the cursor with the panel and the focus point, not the middle-button look or the charge itself; step 18 for a reopen after a close with no player key and a close the trial marks as the player's, not the real Escape, right mouse button, panel key or hold key; step 19 for the key and the restore when the panel opens, not a dialog, another layer or `toggle_ui`; step 20 for the pick from the screen centre, the orbit distance and the let-go by key and by death, not the mouse, the wheel, a movement key or a hill and a wood.
It feeds the flight input itself, so it leaves the engine's input reads, the look and the hero's head to a person.

## Field trial

Unit tests cannot show that a camera flies over a real battle. The field trial (contract section 12) is a script inside the game: it
opens one real custom battle with nobody at the keyboard, presses the Seat's own controls in order, and writes one log line per fact.
It runs eighteen steps: stage S1's six, seven for stages S2 and S3, three for stage S4 and two for stage S5. Each later stage adds its steps as rows, and Marshal's Word adds eight of its own after the Seat's, in its own files (`Main/Features/MarshalsWord/Domain/MarshalTrialRows.cs`, and `Client/Features/MarshalsWord/Trial/` for the script, the host and the interface); [marshals-word.md](marshals-word.md) lists them. The wrapper's `STEPS` joins the Seat's tuple and Marshal's. Its history is below, run by run.

**The first run** (2026-11-04, build `1735f2add.dirty`, folder `E:\Temp\command-trial-runs\20261104-031119`) passed all six steps and
exited 0. It took 14.8 mission seconds and 50 s of wall time including the load, ran at about 84 frames a second with 272 agents (251
owned frames in the 3-second `enter` window), and `fly` logged `moved=59.7 expected=59.7`. It did not cover real key bindings, the axes,
the wheel and the mouse, because `TrialInput` replaces `ReadInput`. It did not judge how the blend and the glide look. It passed with the
vanilla attack direction arrow on screen, which stage S2 removes.

**Two later runs** the same day, each after a round of review fixes, passed the same six steps and exited 0. Build `1735f2add.dirty`
(folder `E:\Temp\command-trial-runs\20261104-042903`) added the judge's build check and logged `frames=244`, `moved=60.0 expected=59.9`.
Build `1d5d0a561.dirty` (folder `E:\Temp\command-trial-runs\20261104-052645`) ran the script as iterators and the autostart over its host,
and logged `frames=239`, `moved=59.7 expected=59.7`. Both logs hold `checks_fail=0`, `swallowed=0` and a clean module unload.

**The fourth run** (2026-11-04, the committed harness at `8b041f03e` with a clean stamp, folder `E:\Temp\command-trial-runs\20261104-063546`) passed the six steps and exited 0, on engine changeset 125270; the first three ran on 125053. `python tools/ingame_verdict.py --issue 0 --sessions 1` read PASS for `enter`, `frames`, `leave` and `glide`.

**The fifth run** (2026-11-04, the uncommitted S2 and S3 build `8b041f03e.dirty`, folder `E:\Temp\command-trial-runs\20261104-083536`) passed twelve of thirteen steps and exited 1. `let_go_on_death` failed on the trial's own kill: a blow with owner -1 made the custom battle morale model throw, so the company never emptied. The kill now goes through `Mission.KillAgentCheat`, and that fix has not yet run in the game.

**The sixth run** (2026-11-04 15:11, the uncommitted S4 and S5 build on `ca1cba35f.dirty`, folder `E:\Temp\command-trial-runs\20261104-151128`, steps `enter,fight_on,morale_held,back_to_stand`) passed all four steps and exited 0, with `checks_fail=0` and `swallowed=0`. `fight_on` logged `controller=AI moved=2.5 retreating=0 panicked=0 gate=1 panel=1`: the hand-off, the raised gate and an order panel that opens for an AI hero were seen in a custom battle. The 2.5 m is one number, and it cannot tell a hero who charges with his company from one held at the back rank, so where he stands stays open. `morale_held` logged `morale=0.0` after 2.5 s and `back_to_stand` logged `after_leave=Player controller=Player gate=0 speed=0.0`. That build's `morale_held` did not read the morale back after its write, so its pass did not show that the write took effect; the step now does (`written=` on its line).

**The seventh run** (2026-11-04 15:12, the same build, folder `E:\Temp\command-trial-runs\20261104-151235`, steps `enter,hero_falls,take_soldier`) passed `enter` and `take_soldier` and failed `hero_falls` with `main_null=1 gate=1 messages=0 message_fell=0`. The step read too early, and the code was right. The engine clears the main agent inside the kill (`Mission.OnAgentRemoved`, `MainAgent = null`, `Mission.cs:3021`), but the Seat sees the fall at its next mission tick: `SeatSession.FightTick` lowers the gate and writes `fight-on result=skip reason=fell` (`HeroFell`), and `NoteTheFall` says the message. The log shows the order: the `fell` skip line comes after the step's line (log lines 575 and 580), and `take` passed after it with the same permission and setting that the message needs. The step now waits for the gate to be down and for a message, within `SeatTrialRules.FallWaitSeconds` (3 real seconds), and prints `waited=`. A gate that never drops, or a message that never comes, still fails after the wait. The eighth run ran the fixed step. The seventh run's log holds no line for the message itself.

**The eighth run** (2026-11-04 16:28, the uncommitted build `ca1cba35f.dirty`, folder `E:\Temp\command-trial-runs\20261104-162810`, steps `enter,hero_falls,take_soldier`) passed all three steps and exited 0, with `checks_fail=0` and `swallowed=0`. `hero_falls` logged `main_null=1 gate=0 owned=8 messages=1 message_fell=1 waited=0.1`: the Seat lowered the gate and said the fall once, 0.1 s after the kill, which is the first direct evidence that the message shows. `take_soldier` logged `took=1 controller_owner=1 owned_before=8 owned_after=8 still_ai=0 panel=1`. Together the sixth and eighth runs prove the steps of stages S4 and S5 in a custom battle on the build they ran. The setting `SeatHeroFightsOn` stays off: no run played a campaign battle or a siege with it, the hero's stance in his company is unmeasured, and the changes of the review that followed (the hand-back debt, the boundary hold by policy, the co-op network fact, the mumak and no-formation refusals, the use stopped as a failure, the retreat stop after the hand-back and the morale read-back) have not run in the game.

**The ninth run** (2026-11-04 18:44, the uncommitted build on `ca1cba35f.dirty`, folder `E:\Temp\command-trial-runs\20261104-184455`, steps `enter,fight_on,morale_held,back_to_stand`) passed `enter`, `fight_on` (`controller=AI moved=2.0 retreating=0 panicked=0 gate=1 panel=1`) and `morale_held` (`written=0.0 morale=0.0`, the first run of the morale read-back), and failed `back_to_stand` with `after_leave=Player controller=Player gate=0 speed=2.0`, where the sixth run read `speed=0.0`. The hand-back itself worked: the controller was the player's 0.3 s after the leave and the gate was down. The log cannot say why the hero then covered 3 m in 1.5 s. It holds no trace of his position between the hand-back and the window, and his company still held the Charge order that `fight_on` gave it, so a hero who walks on after the hand-back and a hero carried along by his charging company read the same. A player would see the first as a hero walking by himself after the Seat gives him back. The step now stops the company first, reads the hero's speed on the AI before the leave (`before=`) and fails with `reason=ground-not-quiet` when that speed is not under the limit, so a moving company is never charged to the hand-back. The limit of 0.5 m/s stays, because the sixth run stood at 0.0 under it. The cause is open until the next run: a failure with `before=0.0` is a real defect of the hand-back.

**The tenth run** (the same build, folder `E:\Temp\command-trial-runs\20261104-184608`, steps `enter,hero_falls,take_soldier`) passed `enter` and `hero_falls` (`gate=0 owned=8 messages=1 message_fell=1 waited=0.1`) and failed `take_soldier` with `[Check] take result=skip reason=nobody` and `took=0`, where the sixth and eighth runs took `kind=Soldier`. The skip reason is the evidence: `SeatScreenAdapter.TryTake` answers `Nobody` only when the pointer ray found no soldier and no refusal was set, and the two new refusals (platform crew, no formation) answer `Refused` with their own reason, so neither of them took the soldier away. The step aimed the view at the nearest soldier, and whether the ray at the screen centre still met a valid soldier one mission tick later depended on where he and the camera were. The step now keeps to a large company before the hero falls (`hero_falls` prints `kept=Company`) and takes through that company, which picks the nearest eligible soldier to the point the view orbits, so no aim decides it. The pointer path of the take stays covered by its unit tests and by the `[[human]]` row for the soldier at the middle of the screen; no run proves it in the game.

**The eleventh run** (2026-11-04 19:38, the uncommitted build on `ca1cba35f.dirty`, folder `E:\Temp\command-trial-runs\20261104-193822`, steps `enter,fight_on,morale_held,back_to_stand`) passed all four and exited 0, with `checks_fail=0` and `swallowed=0`: `fight_on` read `controller=AI moved=2.2`, `morale_held` read `written=0.0 morale=0.0 panicked=0`, and `back_to_stand` read `before=0.0 speed=0.0`. The cause of the ninth run's `speed=2.0` was not shown; on a quiet ground it did not come back.

**The twelfth run** (the same build, folder `E:\Temp\command-trial-runs\20261104-193933`, steps `enter,hero_falls,take_soldier`) passed all three and exited 0, with `checks_fail=0` and `swallowed=0`: `hero_falls` read `gate=0 owned=8 kept=Company messages=1 message_fell=1 waited=0.1`, and `take_soldier` read `took=1 controller_owner=1 owned_after=8 still_ai=0 panel=1`. Second press after a take, the pointer path of the take, the hand-back's retry, the retreat stop, the boundary hold, the Mumak and no-formation refusals and the co-op fact are not reached by any step: they rest on unit tests and human rows.

**Run it** (a builder never does; the orchestrator deploys the tree, then runs it under the permission and the idle gate of
[unattended-game-runs.md](../reference/unattended-game-runs.md)):

```
./build.ps1
python tools/run_command_trial.py --dry-run
python tools/run_command_trial.py --steps refuse_in_deployment,enter,fly,hero_stands,leave_glide,reenter_keeps_height,cursor_with_panel,focus_follows_cursor,keep_panel_reopens,hide_bars_restored,keep_to_soldier,keep_to_company,let_go_on_death
```

A run with no `--steps` also arms Marshal's Word's steps in the same battle, which they do not all fit; their subsets are in
[marshals-word.md](marshals-word.md).

`--dry-run` prints the arm file, the idle state and the preflight and starts nothing. A subset is `--steps enter,fly` (the game refuses
a step chosen without one it needs, and the done file then says `start_refused`). Of the Fords runner's flags these apply:
`--require-idle-minutes`, `--runs`, `--keep-on-crash <seconds>` (wait for a person to answer the dump dialog), `--runs-root` and
`--dry-run`. `--wall-limit` moves only the runner's own deadline and does not end the match. The Fords match flags (`--west`, `--speed`
and the like) parse and are ignored. Keep `--live-limit` well under the wall limit plus 15 minutes, the runner's deadline (3300 s by
default); at 100000 or more the game keeps its default of 420. By its step timers the run needs about 30 real seconds after the load
(the first run took 14.8 mission seconds and 50 s of wall time with the load; the fifth ran its thirteen steps from 08:36:22 to 08:36:48, 26 s). A build without the trial idles at the main menu and the run ends as a stall
after 10 minutes; the judge reads the copied log's `[BuildStamp]` and refuses a build that is not this checkout.

**Arming.** The environment variable `TAOM_COMMAND_TRIAL` holds the absolute path of an arm file (`key=value` lines, LF). There is no
file fallback. `CommandTrialOptions.FromEnvironment` is the only read of the variable and runs once per process. An unarmed session pays
one environment read per process, one static read per mission, one null check per application tick and per mission tick, one null check,
a bool read and a bool write per screen tick, one `SeatInput?` copy per camera frame, and one static read per focus loss, plus one
closure per mission for `CommandTrialRun.Script`. It allocates nothing per frame, and its behaviour does not change. Keys: `out`
(required, an absolute folder), `quit`, `scene` (default `battle_terrain_a`), `live_limit_seconds` (default 420), `steps` (default
all). The saved MCM values do not matter: an armed session gets `CommandTrialSettings`, in memory only (it never writes the MCM file, because
json2 keeps saved values), with the Seat on, Begin Battles in the Seat off, the cursor with the panel on, Keep the order panel open on and
Take a soldier after the fall on. "Your hero fights on in the Seat" starts off and a step turns it on for its own entry (`ICommandTrialHost.SetFightOn`): with it on from the start the hero is on the
AI one mission tick after the first entry, `enter` would read a controller that is not the player's and `hero_stands` a hero who is walking. An
unarmed session gets the plain provider itself.

**The battle.** The real thing, `BannerlordMissions.OpenCustomBattleMission(scene, hero, playerParty, enemyParty, isPlayerGeneral: true,
null, "", "", 12f)`. The player's side defends.

| Side | Culture | Troops |
|---|---|---|
| Player | `gondor`, hero `lord_1_9` | 40 `fow_gondor_militia_spearman`, 40 `fow_gondor_ano_archer_militia`, 20 `fow_gondor_pg_cavalry` |
| Enemy | `mordor` | 120 `fow_mordor_orc_fighter`, 30 `fow_mordor_orc_hunter` |

The mounted troop is `fow_gondor_pg_cavalry` (Pinnath Gelin Light Horseman), in `ModuleData/fords_of_war/fords_troops.xml`: Culture.gondor,
level 26, `empire_horse` with `gondor_horse_armor_4`. Every id was read from the repo's data (the cultures in `taom_spcultures.xml`, the
hero as a stub in `characters/custom_battle_lords.xml` and listed in `custom_battle/custom_battle_commanders.json`, the five troops in
`fords_troops.xml`), and `SeatTrialRosterTests` fails when a rename strands one. `python tools/validate_moduledata.py` reports 0 errors.
At run time the launcher asks the game's object manager for every id first and refuses to open the mission when one is missing
(`start_refused`, with the ids named in the log), so a bad id fails loudly instead of fielding a short army.

**The steps**, in the order of `SeatTrialSteps.All`. Real time runs every timer (the screen tick's `dt`, summed, which also runs under a pause); mission time is only ever a measured fact. A step that fails does not stop the run; the next one
checks its own precondition and fails at once, so every step reports and nothing hangs. Each step also has a time budget and fails on
`reason=timeout` past it.

| Step | What it does | Passes when |
|---|---|---|
| `refuse_in_deployment` | One second in, presses the Seat key while the mission is in Deployment | the session took the press and the state is still Off (Deployment writes no `[Check]` line, so this step is its evidence) |
| `enter` | Five real seconds in, finishes the deployment (once the step above has reported), waits for Battle mode and a living hero, settles one second, presses the key | the state was Rising and then Seated, more than 30 frames per real second were owned in the 3 s after the press, and the hero's controller is still Player |
| `fly` | Holds forward input for 2 s | the view moved within 25 percent of the Pan speed at its height times the elapsed time, along the view's forward direction (the dot of the displacement with the view's forward vector is on the line as `dot=` and is at least 0.9) |
| `hero_stands` | Measures the hero over 1.5 s while seated | the hero's ground speed is under 0.5 m/s |
| `leave_glide` | Lifts the view for half a second first, so its height is not the entry height, then presses the key again | the state is Off, the glide was armed, and the engine's own camera is within 10 m of the hero inside 4 s |
| `reenter_keeps_height` | Presses the key again | one second after the entry the view stands at the height above ground it left from, within a metre or 10 percent, and that height was not the entry height (a lift that did nothing fails the step, because a re-entry that dropped the last flight comes back at the entry height) |
| `cursor_with_panel` | Opens the order panel, then closes it as the player does | open: the panel is open and the screen shows a cursor; closed: neither |
| `focus_follows_cursor` | Opens the panel, puts the cursor at 30 percent across and 40 percent down, reads the pixel back, the pointer function's value and the focus handler's `_centerOfScreen` | the cursor is within 2 pixels of the pixel asked for and the handler's point is within 2 pixels of the pointer function's value; the focused company is a fact; fails with a clear line when the window has no focus |
| `keep_panel_reopens` | Closes the panel with no player key, then closes it as the player does | the first close really closed the panel (`closed=` is read at once), and it is open again within 3 frames of the step's own waits (`frames=`; the session reopens on its next screen tick and the step resumes after it, so 1 is the usual answer); the second stays closed for a second |
| `hide_bars_restored` | Presses the hide key, takes a picture, opens the panel, takes a picture | the flag is on after the press and off after the panel opens, and it started off |
| `keep_to_soldier` | Aims the view at the nearest player soldier with the flight's look input, presses the keep key, orbits for 2 s, presses it again | the kept soldier is the one aimed at, the camera is 16 m from that soldier, within 2 m on either side (a camera that has collapsed onto that soldier fails too), after 2 s, and the second press lets go |
| `keep_to_company` | Selects the largest company, opens the panel, presses the keep key, takes a picture, lets go | the target is that company, the camera is 16 m from its ground median, within 2 m on either side, and the second press lets go (`letgo=`) |
| `let_go_on_death` | Keeps to a soldier and kills the soldier through `Mission.KillAgentCheat` (the engine names the main agent, else the victim, as the blow's owner, which the custom battle's morale model needs because it reads the attacker's formation with no null test; it fails first when the kept soldier is not the chosen one); then keeps to the smallest company that holds no hero and kills every unit | each time the target is gone and exactly one message was shown, and it says the target is gone |
| `fight_on` | Leaves the Seat if it is up, turns the fight value on, enters again, waits up to 3 s for the hero to be on the AI, gives his company the Charge order from the mission tick, lets it fight for 5 s, opens the order panel and closes it | his controller is the AI's, he moved more than a metre, `IsRetreating` and `IsPanicked` are false, the gate is up and the panel opened (`controller`, `moved`, `retreating`, `panicked`, `gate`, `panel` on the line) |
| `morale_held` | Writes morale 0 on the handed hero from the mission tick, reads the morale back at once, and waits 2.5 real seconds | the morale read between 0 and 0.01 right after the write (`written=`; a write that took no effect fails with `reason=morale-not-written`), then still on the AI, `IsPanicked` false, `IsRetreating` false (a build without the morale refusal reads panicked) |
| `back_to_stand` | Stops the hero's company, waits 2 s, measures the hero on the AI over 1.5 s (`before=`), leaves, turns the fight value off, enters again, and measures the hero over 1.5 s | the speed before the leave was under 0.5 m/s (else `reason=ground-not-quiet`, and the step does not leave), the controller was the player's after the leave and again after the entry, the gate is down, and his speed after the entry is under 0.5 m/s |
| `hero_falls` | Leaves and enters with the fight value on, waits for the hand-off, refuses a hero who cannot die, keeps the view to the largest company (selects it, opens the panel, presses the keep key, closes the panel as the player does; `reason=not-kept` or `reason=no-company` otherwise), kills him through `Mission.KillAgentCheat` from the mission tick, then waits up to 3 real seconds (`SeatTrialRules.FallWaitSeconds`) for the Seat's reaction at its next mission tick: the gate down and a message said | no main agent, the gate down, the Seat still up, exactly one message and it is the one about the fall, and the hero owned at least one formation (`waited=` is how long the Seat took; `kept=Company` is the keep made before the kill) |
| `take_soldier` | Requires the view to still keep to the company (`reason=no-kept-company`), presses the take key (`SeatSession.PressTake`) with no aim, waits for a main agent, opens the panel | the main agent belongs to the kept company, the order controller names him, he owns exactly the formations the hero owned and none is on the AI, and the panel opened |

Each line reads `[SeatTrial] step=<name> result=<PASS|FAIL> <facts>`, for example `step=fly result=PASS moved=60.0 expected=60.1
height=22.0 seconds=2.0 dot=1.0` (numbers from the test stand-in). The facts are what a person reads when a step fails. `leave_glide` writes `after=`, the distance 0.1 s after
the leave, so a glide that eases (large) and one that snaps (small, the known limit below) can be told apart; a snap does not fail the step.
Pictures: `hud_enter.bmp` two seconds after the entry, `hud_fly.bmp` 1.5 s into the flight, `hud_hide_bars_restored_hidden.bmp` and `hud_hide_bars_restored_back.bmp`, and `hud_keep_to_company.bmp`, which the runner turns into PNG files. Nothing judges a picture by machine: a person or a session looks at them.

Every agent, formation or mission flag write in a step (opening or closing the panel, selecting a company, `Mission.KillAgentCheat`) runs after `yield return Verdict.InMissionTick`; a wiring gate reads the host for such members and fails any step that calls one without the marker. The one exception is the release after a failed step, which closes a panel the step left open from whichever tick reported the failure, because vanilla closes this panel from its own screen tick (`GauntletOrderUIHandler.OnMissionScreenTick`, through `MissionOrderVM.TryCloseToggleOrder`). `let_go_on_death` kills soldiers and `hero_falls` kills the hero, so run the kill steps as subsets: `--steps enter,keep_to_soldier,keep_to_company,let_go_on_death` proves stage S3, `--steps enter,cursor_with_panel,focus_follows_cursor,keep_panel_reopens,hide_bars_restored` proves stage S2, `--steps enter,fight_on,morale_held,back_to_stand` proves stage S4 and `--steps enter,hero_falls,take_soldier` proves stage S5. In a full run the two S5 rows are last.

**Subsets.** Every subset names `enter`, because each row lists what it stands on and the game refuses a list without it (`SeatTrialSteps.TryChoose`, `start_refused`). `fight_on` carries `morale_held` and `back_to_stand`, and `hero_falls` carries `take_soldier`. The two subsets that prove the last two stages are `--steps enter,fight_on,morale_held,back_to_stand` (S4) and `--steps enter,hero_falls,take_soldier` (S5). Run them as two runs, because `hero_falls` ends the hero's life and `fight_on` needs a living one. `hero_falls` reads the Seat's reaction a mission tick after the kill, not the kill itself, for the reason given under the seventh run.

**How the key gets pressed.** `SeatView.PressSeatKey()` sets the flag that the real key test sets in `OnMissionScreenTick`, and the
session reads that one flag, so the trial and a player take one path. `SeatView.TrialInput` replaces `ISeatScreenAdapter.ReadInput` in
`OnFrame` while it is set, and `PressPending` is a read-only view of the flag. The host reads `State`, `Frames`, `GlideBegan` and
`Faulted` from the session, and its ground reads go through the view's one `SeatScreenAdapter`, so a later stage's cursor and kept
target are the state the trial sees.

**How the script is built.** `CommandTrialScript.cs` is the driver and `CommandTrialScript.Seat.cs` holds all eighteen steps and their
registration, so each feature adds steps in files of its own (`SeatTrialSteps.All` joins each feature's row list, and the interface,
the host and the test world are `partial`). A step is an iterator: its state is local, a wait is a `yield`, and the driver stops at the
first verdict that is done. A step that ends without a verdict fails (`reason=no-verdict`). A step that must write an agent, a formation
or a mission flag first yields `Verdict.InMissionTick`, and the code after the marker runs inside the script's `OnMissionTick`, which
the screen tick does not resume. A step that outlasts its budget fails with `reason=timeout waiting=<what>`.

**Ending.** The script reports `final` to `CommandTrialRun`; the autostart (on the application tick) calls `MBGameManager.EndGame`, which
ends the mission through the normal teardown while the Seat is still up (so the `Finish` path runs with the Seat up), waits for the main
menu, writes `command_trial_done.txt` (`reason`, `run`, `report`, `log`, `live_seconds`, `wall_seconds`, `started`, `ended`) and quits.
`command_trial_status.txt` is rewritten every 5 s. `reason=final` is the only success; the failures are `start_refused`, `start_failed`,
`script_failed`, `never_live`, `wall_limit` (the mission clock stopped), `mission_closed`, `live_limit`, `autostart_failed`, or one of them
joined to `exit_stalled`. A file write that fails (the runner reads the status file while the game replaces it) is logged as one warning
and does not end the run; a done file that could not be written is logged as lost, and the quit still follows. An armed run
answers no to `IsOpeningEscapeMenuOnFocusChangeAllowed`, which keeps the GauntletUI escape menu shut on a lost window focus; whether
the engine pauses the mission on its own then is UNVERIFIED, and `wall_limit` bounds such a run. The autostart's phases and exits are
pure over `ICommandTrialGameHost`, and `CommandTrialAutostartTests` drives each exit through a fake game with its own clock.

**The judge** (`run_command_trial.py`) fails a run with any `result=FAIL` line, a missing `[SeatTrial]` line for a step in `--steps`, a
`[CrashReport]` block, a failed `taom.seat-of-seeing.*` check line (the fault latch must stay silent), an `[ERROR]` line tagged
`[SeatOfSeeing]` or `[SeatTrial]`, a `[PatchApply]` error line of a `Patch140` class (Harmony stops a category at its first failing class, so
one failed or skipped class can leave every later Seat patch off), the thread guard's `[TAOM]` error line for a trial or Seat site (`CommandTrialHost`,
`SeatHeroAdapter`, `SeatScreenAdapter`; another feature's guard line does not count), a log with no
`[Shutdown] clean module unload` line, a `[BuildStamp]` that is not this checkout, or a `reason` other than `final`. A stamp is not this
checkout when its sha is not HEAD, when the stamp line says `MISMATCH` (the word sits on the tier that was built apart, so the line the sha was read from is read to its end), when its `.dirty` mark disagrees with the tree (dirty with no uncommitted file, or clean with one,
a deletion included), or when an uncommitted file under the build's own pathspec (`Main`, `Client`, `Dependencies`, `Stubs`,
`Directory.Build.props`, `GameReferences.targets`) is newer than the stamp's build time. It cannot tell a dirty deploy of a sibling
worktree at the same HEAD from this one when this tree's edits are older than that build, a deletion made after a dirty build, or a file
restored to HEAD after a dirty build while an older edit stays in the tree. Only a content hash in the stamp could close those three. The checkout is read when the run is judged, after the game exits, and not when it
was launched, so an edit or a merge made in between is seen; a git that fails then is exit 1 with the reason. Its
PASS text names the build, the `[Session]` swallowed counts and `errors=` of the log's last summary line, and every swallow site that was written (a swallowed exception does not fail a run, and the number is beside the PASS).

**What a PASS does not prove**, because only a person or a further run can:

- The physical key and the engine's input reads. `TrialInput` stands in for `ReadInput`, so the game keys 14, 15 and 24, the movement
  axes, the wheel and the mouse are not exercised, and neither is the two-part key test or a rebind.
- How the blend and the glide look, whether the picture stays sharp, and whether the hero shows or hides correctly. The five pictures are
  the only look at the frame.
- Anything outside a custom battle: a campaign field battle, a siege, a hideout, a tournament, Fords of War, photo mode, a warg rage.
- That the frame rate is high enough on another machine: the `enter` rule asks for more than 30 owned frames per real second since the press,
  judged 3 s after it. A slower run fails that step honestly.
- The order panel's own keys and buttons (the steps open and close it through its view model, and the trial's close stands in for Escape, the right mouse button and the panel key), the fault latch under a real throw, and first person (the trial leaves the player's camera mode as the config holds it).
- The hero standing under real movement keys: a trial cannot press real keys, so `hero_stands` only observes the hero's speed while the flight input is
  fed by the script. A `[[human]]` row in the manifest owes it.
- The `hide` check reads a hide without a restore as a FAIL, but it pairs nothing per mission: one mission that restores and a later one that
  does not still reads PASS. `Cleanup` restores on every exit but a crash, which has its own block in the log, so that case has no known producer.

## Known limits

- `OrderController.GetOrderFormCustomWidth` measures from `Agent.Main.Position`, so a form-width drag from the Seat measures from the
  hero. No patch (contract 5.8).
- The glide starts with `_cameraApplySpecialMovementsInstantly` set in a mission that has a `DeploymentMissionController` whose player
  side could not deploy with Order of Battle at the first deployment plan (`_playerDeploymentCancelled`, MissionScreen:4562, 4660-4661,
  4706); in that mission the return snaps. Which battles do so is UNVERIFIED.
- Stand fast has two readers it does not stop, each on a key the Seat does not use: the hero takes no movement or combat input, but the
  career ability key (`V`) and the engine's perspective toggle (game key 27) still act. Each needs a deliberate press, and a patch on each
  would not pay for itself (contract 5.5). Stage S2 closed the drop and equip keys (13 and 22), the equip wheel (34), the cheer wheel (31) and
  the walk mode control, with private patches that only a press proves are reached. The walk mode control opens on the hot key
  `ControlModeToggle`, the controller's left pad down; game key 30 (CapsLock) is read by `MissionMainAgentController.cs:811`, which the shut
  controller already stops, so that patch serves a player who mixes a gamepad with the Seat.
- A press under the hold time at the moment of entry acts once on return in three views the Seat does not clear: the equip and drop view's usage
  toggle (`MissionGauntletMainAgentEquipDropView.cs:159-164`), the walk mode control (`MissionGauntletMainAgentControlModeView.cs:257-262`) and the
  cheer view. It is mild and stays a named limit. The equipment view's own drop and equip gestures are cleared (Patch140).
- A click on one of the panel's buttons does not press it yet, until Marshal's Word stage M7: the cursor layer's mask stays `Invalid`, so a left click passes through
  the panel to the order placer and gives a Move order on the ground behind the button. `Show a cursor with the order panel` still defaults to
  on, because the pointer is the stage's purpose, and a later flip would need a rename under the persisted MCM rule.
- A gamepad has no pointer path: the Seat's keyboard keys have no controller binding, so the cursor needs a player who mixes both.
- `BannerlordConfig.SlowDownOnOrder` defaults to true, and an open panel then holds a 0.25 time request, so a player with Keep the order panel
  open runs the whole stay at quarter speed apart from one frame per order. The setting's hint says so.
- Whether a player option can move `ScreenManager.UsableArea` below 1 on a PC is not proved. The pointer function subtracts the margins at
  any value, and its unit test runs at 1 and 0.9; the in-game row asks for a value below 1 if one can be reached.
- Letting go of a kept target over ground that is changing under a moving company can still differ by what the smoothed ground lags the raw
  read. The orbit is floored at the raw ground plus the flight's minimum height, so a low orbit over flat ground lets go with no jump, and the
  smoothed-ground lag is a flight frame's own behaviour.
- `MBDebug.DisableAllUI` is the engine's `toggle_ui` flag and no other TAOM code writes it (a gate pins this). Photo mode is a second engine
  writer of the flag (`MissionGauntletPhotoMode.cs:118, 139, 183, 192, 200, 317`), so no Seat key acts in photo mode. A player who types `toggle_ui`
  while the Seat's hide is in force changes the flag under the Seat; the leave then writes back the value saved at the hide.
- Leaving photo mode with Escape writes `MBDebug.DisableAllUI` false (`MissionGauntletPhotoMode.cs:317`) while a Seat hide stays in force. The next hide key then only logs a restore, and the press after that hides.
- With Keep the order panel open on, an engine close outside the listed routes can reopen the panel: `MissionOrderVM.OnGamepadActiveChanged` (`:428`) and `GauntletOrderUIHandler.OnSuspendView` (`:664`) close it and write none of the close signals. No harmful sequence is known, and this is not proved.
- `let_go_on_death` kills through the engine's cheat, one call a unit, and does not read each removal. A unit that survives the blow stays in the company, and the step then reads `company_gone=0`.
- After the first cursor of a mission the adapter's empty cursor layer ticks every frame until the mission ends, in the Seat or out of it (`GauntletLayer.cs:173-199` runs the UI context update and render with no movie loaded). Its cost is unmeasured, and nothing deactivates the layer.
- `Mission.RayCastForClosestAgent` and the terrain ray are cast in the mission tick after the key press, so a pick lands one tick after the
  press. Native thread safety of the ray is not proved; the cast is placed where agent writes already happen.
- A kept soldier who is swapped for another by a main agent change is still the same agent object; nothing re-picks.
- From first person the return is at once, not a glide (see the return glide above).
- A hero with a ranged weapon keeps the reticle while seated, and it can use the entry view angle.
- Open, and outside the Seat: `PrepareOnFirstCall.cs:34` tests the warg's own `IsPlayerControlled`, and `Agent.Build` gives a
  non-humanoid an AI controller, so the swap in may never run in single player (not proved in game). Then `HoldsHero` stays false and
  the Seat's rage rules are inert and harmless. The in-game warg Custom Battle owed to [warg-combat.md](warg-combat.md) should settle it.
- `MissionState.TickMissionAux` runs `Mission.Tick(dt)` before `Mission.OnTick`, so a hero keeps its last input for one simulation step
  after entry, until the mission tick does the stand-fast write (visible effect UNVERIFIED).
- A Deployment to Battle edge raised before screen initialization is lost, so a battle that starts that way does not begin in the Seat.
- `HoldsHero` is not reset on `ResetMission` (a restart is hypothetical).
- Leaving with no agent to follow from above 80 m: the engine's free camera puts the view at ground + 80 (`MissionScreen.cs:1719`,
  1890-1893), so the position can drop up to 30 m on the first frame. The look stays.
- `CapFlight` lowers the stored height for good in a scene with a limiter: crossing high ground leaves the view lower in the valley
  beyond and at the next entry, and the wheel recovers it.
- `Enter`'s ground, tag and boundary reads run from the screen tick, which may overlap the agent tick. All are read-only, and
  `BeginGlide`'s listener makes the same tag read from the same tick, but safety is UNVERIFIED.
- If the swap into a rage does run: a rider who fell, took the Seat, left it and mounted the same warg again has both controllers on,
  because the leave saw no mount. Nothing in the Seat can see that remount without a per frame check, and no in-game evidence shows that
  it can happen.
- If the swap into a rage does run, and fast time puts a whole rage between two screen ticks: the rage's last movement input stays on the
  hero until the Seat leaves, because only an entry and a main agent change owe a stand-fast write. The session cannot see a swap that
  starts and ends between its ticks, and both conditions are needed.
- A native multiplayer game mode: `MissionMainAgentController.EarlyStart` subscribes its own `Enable` and `Disable` to the round events
  (lines 189-193), so vanilla's controller would turn on under the Seat when preparation ends. Nothing guards this, because TAOM does not
  run there: `Main/_Module/SubModule.xml:8` makes it a `Singleplayer` category module, and no TAOM or co-op mission holds that behaviour.
  If that changes, refuse the Seat there (contract section 4).
- A banner bearer's effect and the panel's soldier count assume a player-controlled hero. With the hero on the AI (S4) he loses the banner effect on other formations, and the panel's count is one low (reported under "Stage S4", not patched).
- A Player agent that was queued or listed as a climber by the ladder queue stays listed, flagged and counted as a climber until it leaves the zone; the manager is not patched (see "Stage S4"). Whether its parallel tick can overlap the mission tick is UNVERIFIED.
- Where an AI hero stands in his company is open: `Formation.TryRelocatePlayerUnit` may keep moving him to the back rank because `IsPlayerUnit` stays true.
- The hand-off's finish does not hand the hero back: `Finish` writes no agent, because no mission tick follows it, so a mission that ends while he fights on leaves him on the AI of a mission that is ending.
- After a take, a plain soldier leaves no commander with a party, and the Tactics share goes to the leader's party (a hero of the player's party becomes the commander); a campaign field battle is owed one check.
- No mark shows the screen centre for the take; the pointer after a fall is the centre.
- The vanilla Kick key's prompt in a battle that arms it (custom and campaign field battles and sieges with deployment, and Long Defeat) may show disabled while the Seat is up, because the prompt reads the flag for its relevance and the postfix for its enabled state.
- A kept company's nearest soldier to take is measured from the point the view orbits (the company's ground median), not from the camera.
- A rider of a creature (a warg rider, for one) stays takeable: the Seat's take asks for no plain mount, as the Fords take does. A soldier of no formation is refused.
- The hero's retreat stop runs once a real second, as the contract has it, and the fade timer of a retreating hero runs every 0.5 to 0.6 s (`CommonAIComponent.cs:54, 99-106`), so a hero near his retreat point or the line can fade out between two checks. UNVERIFIED in the game.
- A failed hand-back is retried with the gate down, so a hero still on the AI can panic until a try works or the three tries end.
- The begin path carries no hand-back debt. If the controller write of `BeginFighting` throws, its recovery runs the same write while `_handed` is still false, so a second failure of that one setter leaves the hero on the AI with the gate up and nothing owed; the session writes `fight-on result=fail` and latches off. No producer of two failures in a row is known.
- The field trial takes a soldier through a kept company, so the pointer path of the take (the ray at the screen centre) is proved by unit tests and the `[[human]]` row for the soldier at the middle of the screen, not by a run.
- Follow-up, not built: a test that ties every `MissionThreadGuard.NoteCall` site of the Seat to `SEAT_ERROR` in `tools/run_command_trial.py`. Every site today matches (`SeatHeroAdapter` four, `SeatScreenAdapter.TryTake`, the four `CommandTrialHost` writes), so the test would pin a state that already holds; it waits for the first new site.
- `SeatHeroFightsOn` stays off. No run has played a campaign battle or a siege with it. The eleventh and twelfth runs played the changes of the S4 and S5 review in a custom battle.
- Skipping `UpdateCamera` also skips the per-frame `IsDisabled` write on the main agent controller, the drag data and the focus upkeep.
  The gate is shut with `Disable()` for that reason.

## How to add a Seat key for a later stage

1. Add its id to `TaomSeatHotKeyCategory` (next free id from the contract table), raise `RegisteredSlotCount`, and register it. Pick
   a default no category on the mission scene layer binds, and add the category to the unbound-default test.
2. Add its `str_key_name` and `str_key_description` rows to `global_strings.xml` and seed the 12 languages.
3. Read it from `SeatKeys` (contract 6.0), through a property of `SeatHotKeys`, with a session method for the press. Never in `OnMissionTick`.

## Performance

A player who never presses the key pays one `GameKey` read and one static `Input.IsKeyPressed` in each mission screen tick, plus one
bool read; the view's own context test (which allocates) runs only when the static one says pressed, and no setting and no mission
fact is read except on a key press or a stored deployment edge. Each of the fourteen `Patch140` hooks tests one volatile bool whenever its target
runs, and the formation focus target runs per enemy formation. While seated,
`ReadFacts` runs twice a frame, once in the screen tick and once in `OnFrame`; the input is read once a frame, and the engine's ground
height and boundary tests are called once a frame each (two ground reads while rising). `OutcomeCounter` counts owned frames with one
increment. Stage S2 adds, while Seated: one field read (`Mission.IsOrderMenuOpen`) a frame, and a closed panel reads nothing else. With the
panel open it also reads the cursor setting and, with that on, the middle button. The Keep the order panel open setting is read only while the
panel is open and on the frame it closes, and with it on the close routes are read then (five reads at most), and the Transfer flag on that frame only. The dialog and the focused
layer are read only while the interface is hidden. Stage S3 adds, while keeping, one target read a frame: an orbit frame replaces the flight
frame and reads the ground once, as the flight does. The two extra key tests a screen tick are one static read each, and run only while Seated. Stages S4 and S5 add: a third static key read a screen tick while the state is not Off (key 532); three bool reads a mission tick for a player who never fights on; while the hero is handed, one agent controller read and one clock read a mission tick and, once a real second, one retreat read; the order panel's check, the boundary handler and the siege weapon view each pay one volatile read when their target runs, and `CanTakeControlOfAgent` one.
The rays of a pick are cast once, in the mission tick after the press. The cursor layer stays: once shown it is an empty `GauntletLayer` that
ticks for the rest of the mission, and its cost is unmeasured (a frame capture is the way to find out if it matters).

## Changelog

- 2026-11-04: the ninth and tenth runs and the second review of S4 and S5 (Deviations 97 to 100): `back_to_stand` stops the company and reads the hero before it leaves, `take_soldier` takes through a company kept before the fall, the classifier takes the co-op provider as a parameter (each view resolves it once), a retry that throws below the limit is counted as swallowed, the hand-back is named `HandBackOnce`, the adapter's hand-back asks `HeroOnAi`, the boundary prefix and the patch logger have tests, the begin path's missing debt is a named limit, and the issue is waived.
- 2026-11-04: the review of S4 and S5 (Deviations 88 to 96): a failed hand-back is a debt of three tries with the controller written in a `finally`, the boundary hold follows the policy's `FightOn`, the network fact includes a co-op session (and Marshal's Word follows it), the take refuses mumak crew and a soldier of no formation, the retreat stop after the hand-back, `morale_held` reads the morale back, the use stopped as a failure at the hand-off, the Seat's player texts corrected in the code, the English rows and the 12 language rows, the order panel's failure line in the enter and fight-on checks, and the run history names the runs `20261104-151128` and `20261104-162810`.
- 2026-11-04: the first run of S4 and S5 (Deviations 86 and 87): `hero_falls` waits for the Seat's reaction to the fall, within a named limit, instead of reading at the kill; the session's fall message is pinned for a handed hero as well as a stand-fast one.
- 2026-11-04: stages S4 and S5 and their field trial steps: the hero fights on under the engine's AI (a tick that compares wanted with actual, a gate, the morale refusal in both models, a retreat check once a real second, the order panel, boundary and siege view guards), key 532 and the take of a soldier with the command handed over, the postfix on `Mission.CanTakeControlOfAgent`, the two settings, five steps (contract Deviations 72 to 85).
- 2026-11-04: stages S2 and S3 and their field trial steps: the cursor layer, keep the order panel open, the pointer for the formation focus,
  key 536 and its restore, eight screen patches, key 531 and the orbit around a soldier or a company, `SeatKeys`, the shared cleanup,
  `CommandTrialSettings`, real-time step timers, seven new steps and the marker gate (contract Deviations 36 to 56).
- 2026-11-04: the first review of S2 and S3 (Deviations 57 to 64): one toggled cursor layer, key 77 as a fifth close route, the close signals read only
  around an open panel, Transfer, a refused reopen as a skip, a clearance for the orbit's floor, a failed step that releases what it held, a marker
  gate that classifies every host member, the lift in `leave_glide`, the direction of `fly`, the two equipment tick prefixes' injected fields, and
  `keep_panel_reopens` judged in frames.
- 2026-11-04: the second review of S2 and S3 (Deviation 65 and rows 66 to 71): the focus check line calls `Pass` once, `Seated` in every S2 and S3
  pass, a test for photo mode and for re-entry with the panel open, `SeatKeepRule.KeepsCompany`, the kill through `Mission.KillAgentCheat`, a failed
  step that closes its panel at once, `SeatCursorRule`, `SeatKeyScope` and the hide enum gone, one pointer ray for the order and the keep pick, the
  trial host's logger, a judge that always reads git, one stamp regex, and the four injected equipment fields in the reflection catalogue.

- 2026-11-04: the field trial harness after its second review (contract Deviations 33 and 34): the script's steps are iterators, one
  file per feature (`CommandTrialScript.Seat.cs`, `SeatTrialRows.cs`), the host gets the session and the view's one screen adapter, the
  autostart runs over `ICommandTrialGameHost` with a test for every exit, `CommandTrialRun.Script` holds the armed, ended, refused and
  failed decision, and the wrapper refuses a deploy whose `.dirty` mark disagrees with the tree.
- 2026-11-04: the field trial (contract section 12) for stage S1: `Client/Features/SeatOfSeeing/Trial/`, the step table in `Domain/`,
  `PressSeatKey`, `TrialInput` and `PressPending` on `SeatView`, `Frames` and `GlideBegan` on `SeatSession`, and
  `tools/run_command_trial.py`. First run the same day: six steps passed (see Field trial).
- 2026-11-04: S1 second review fixes: the default key is Q (B printed a banner message in campaign missions), the deployment edge comes
  from `OnMissionModeChange` and teardown also runs from `OnRemoveBehavior`, stand fast is written only from the mission tick (the
  queue is gone), the swap back of the warg rage needs a swap in and a rider who fell is not held, the look is written on every leave and
  the glide needs an agent to follow, the engine's named ground mask replaces the literal, the `camera_height_limiter` height is
  honoured, boundary positions are checked, refusals and every leave reason write check lines, the texts and the manifest were
  corrected (the manifest is now `0.toml`).
- 2026-11-04: S1 third review fixes: the swap back of the rage skips vanilla's `Enable()` while the Seat is up (it no longer tests
  `HoldsHero`), a finished session is terminal, the deployment edge clears in any state and needs Battle mode, and simplifications with no
  behaviour change (the screen adapter is a client type built per mission, the hero adapter holds no state, the session takes its settings
  and permissions at construction, the math shares `Pan`, `Clamp` and `WrapAngle` with Fords).
- 2026-11-03: S1 review fixes: the ground clearance floor, the view kept sharp, and the warg rage rule (`HoldsHero`).
- 2026-11-03: stage S1 built from the contract: the key, the camera, the glide, stand fast, the fault latch, the two order-ray
  patches, the settings, and the checks.

## GitHub Issue

- **Issue:** none; the maintainer waived it by his word for this work, and the commits of the branch (`f2ee9a16f`, `8b041f03e` and the later ones) say so in their bodies. `tools/ingame_checks/0.toml` keeps `issue = 0` for that reason. The in-game steps above are owed.
- **Status:** S1 to S5 built. S4 ran in the game (the sixth run, `20261104-151128`: `fight_on`, `morale_held` and `back_to_stand` passed) and S5 ran twice (the seventh run, `20261104-151235`: `take_soldier` passed and `hero_falls` failed by reading too early; the eighth run, `20261104-162810`: `enter`, `hero_falls` and `take_soldier` passed). The ninth run (`20261104-184455`) failed `back_to_stand` on `speed=2.0` and the tenth (`20261104-184608`) failed `take_soldier` on `reason=nobody`; both steps were changed (see the run history), and the eleventh (`20261104-193822`) and twelfth (`20261104-193933`) runs passed all six steps. The setting `SeatHeroFightsOn` stays off. Stages S2 and S3 ran once before their review fixes, and the fixed build has not run in the game. For S1, the field trial ran once in the game (2026-11-04, build `1735f2add.dirty`, six steps passed, exit 0); what it did
  not cover is listed under Field trial, and the 28 numbered steps for a person are still owed.
