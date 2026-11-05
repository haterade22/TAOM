# Seat of Seeing

## Overview

The Seat is a commander's view that any mission with a main screen can use. One key (Take the Seat, default Q, rebindable) lifts the
player's view off the hero over about 0.7 seconds. The view then flies freely over the field with the engine's own movement keys, and
the same key brings it back to the hero: with the engine's short return glide from third person, at once from first person. The hero
stands fast while the view is up. Stages S1 (the camera), S2 (the pointer, the panel and the screen) and S3 (keeping to a soldier or a
company) of [build-spec.md](seat-of-seeing/build-spec.md) are built; the hero fighting on (S4), taking a soldier after a fall (S5), mission
time (S6) and rising to give orders (S7) are not.

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
  type is `SeatMissionClassifier.OwnsTheCamera`). `SeatMissionClassifier.ReadFacts(mission, logger)` takes the logger from its caller.
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

- `SeatKeys` (client) tests the keys beyond "Take the Seat" once a screen tick, so `SeatView` stays under its 150 lines: keys 531 and 536 only while Seated (key 532's wider test comes with stage S5), each with the static `Input.IsKeyPressed` first and the view's own context only on a real press. `PressSeatKey`, `PressPending` and `TrialInput` stay on the view.
- The cursor: while Seated with the order panel open (`Mission.IsOrderMenuOpen`, a plain field read through the adapter) and `Show a cursor with the order panel` on, the adapter shows a small `GauntletLayer` that only asks for a visible cursor (`SetMouseVisibility(true)`, mask `Invalid`, so it takes no click). The adapter builds that one layer on the first show and after that only toggles its mouse visibility, the way vanilla keeps a cursor layer (the banner editor and the face generator); it never removes it, because `RemoveLayer` finalizes a layer, `AddLayer` refuses a finalized one, and the screen finalizes its layers itself at teardown. Hiding is a plain setter, so it is safe on every teardown path. Holding the middle mouse button hides the cursor and looks (`ISeatScreenAdapter.LookHeld`, shared with `ReadInput`); each frame the look is held, `MBWindowManager.DontChangeCursorPos` keeps the cursor where it was: a read of the native function shows that it stores the cursor's present position in a native lock and logs "Cursor already locked to different position" if a later call differs, so the call is backed. What clears that lock is not read, so the middle-button row stays a person's. The session derives the cursor once a frame: it is wanted when the panel is open and the setting is on, shown unless the look is held, and the look key is read only when it is wanted.
- Keep the order panel open: the session compares `Mission.IsOrderMenuOpen` with last frame's value, and a closing edge reopens the panel through the public `MissionOrderVM.OpenToggleOrder(false, false)`, unless a close route of the player was seen on this frame or the previous one (Escape as the engine's `ToggleEscapeMenu` hot key, the right mouse button, game key 68 pressed, game key 87 released, and game key 77, which closes the panel at the root of a layout that has a Return order and shares the view-orders bit; a signal counts only on a closing edge), or the troop transfer menu is open (the Transfer order closes the panel on purpose, and `OpenToggleOrder` clears `IsTransferActive`, so a reopen would cancel the transfer; `Mission.IsTransferMenuOpen` is read on the edge only). The signals are game keys that only the order handler registers on the scene layer, and a read of a key nobody registered throws, so the session reads them only while the panel is open or was open on the last frame: a town, an indoor scene, a village, an arena practice, a duel and an alley build no handler and still give the Seat its camera. The reopen is decided before the frame's cursor step, so an order never hides the cursor for a frame, and it is a pass only when `Mission.IsOrderMenuOpen` reads open after the call: the engine can refuse without a word when the hero or the last troop is gone, and that writes a skip line with `reason=refused`. The view model comes from `GauntletOrderUIHandler._dataSource` (protected), one reflection site. The reopen runs after the session's own tick and only if the state is still Seated. The pure rule is `SeatPanelRule`.
- The formation focus: `Patch140_FormationFocusPoint` sets the handler's `_centerOfScreen` to the pointer, in the handler's own coordinates (the ranged mouse position times the resolution, less half the usable area's margin: `SeatPointer`), while the Seat shows a cursor. Without it a left release charges the company at the screen centre, not the one pointed at. The patch marks the focus on every frame the field is set, and the session writes the check line for the first mark only, so the line costs one call a mission.
- The screen: key 536 sets `MBDebug.DisableAllUI` (the engine's own `toggle_ui` flag; `TwoDimensionEnginePlatform` draws nothing under it) and saves the value it replaced. It restores that value, and only if its own hide is in force, on the next press, at once when the order panel opens, a dialog shows or another layer takes focus (`SeatHideRule`; the dialog and the layer are read only while hidden), and in the shared cleanup. Photo mode is a second engine writer of the flag (`MissionGauntletPhotoMode.cs:118, 139, 183, 192, 200, 317`), so no Seat key acts in photo mode: the hide and keep presses are dropped there. Seven patches in `Patch140_SeatOfSeeing` remove what the sky view should not show or let the hero do: the crosshair, the attack direction arrows and the reload display (`GetShouldArrowsBeVisible`), the hit marker, the equip and drop wheel, the cheer wheel, the walk mode control, and the drop and equip keys (`DropWeaponTick`, `EquipWeaponTick`); the two tick prefixes also clear the view's pending press and hold time (injected by `ref`), because the view clears them only in its else branch, so a press under half a second made just before entering would otherwise drop or pick up on the first tick after the Seat. The registry section has the targets, the callers and the stance of each.
- The entry checks gain one fact: a radial menu on the mission screen (`MissionScreen.IsRadialMenuActive`) refuses entry silently, because a wheel left open would stay on screen and stop the order panel's input.
- The shared cleanup is one private session method that `Leave` and `Fault` both call (a fault runs each step through `Swallow`): it clears `SeatPresence` (and with it the cursor mark), gives the hero's controller back, removes the cursor, restores the hidden interface and drops the kept target. `OnMissionTick` tests each owed job on its own: the stand-fast write and a stored keep press.

**Stage S3: keep to a soldier or a company** (contract section 7). Key 531 stores a press in the screen tick; the session acts on it in the next `OnMissionTick`, because nothing proves a native ray is safe while the agent jobs run. With the order panel open and exactly one company selected the key keeps to that company; otherwise the adapter casts `Mission.RayCastForClosestAgent` from the pointer (the cursor when shown, else the screen centre), takes a mount's rider (a riderless mount is a miss), and compares the distance with `Scene.RayCastForClosestEntityOrTerrain` using the order placer's thickness and flags, so a soldier behind a hill does not count and one in a wood does (`SeatKeepRule.AgentCounts`, written to hold for the terrain distance's starting NaN). The adapter holds the kept `Agent` or `Formation` and reads it as occupant first (`AgentSlotIdentity.IsCurrentOccupant`), then `IsActive()`; a company with no units is gone. The Seat keeps its own camera while it keeps (`SeatOrbit`): the mouse turns the orbit with the pitch between 0.05 and 1.45, the wheel changes the distance between 4 and 90 m from 16 m, and the point eases to the target with `1 - exp(-5 dt)`. A soldier is looked at by the chest, a company by its ground median. The orbit pose goes through the boundary clamp, the height cap and the floor like a flight pose, and the stored flight follows the pose, so letting go continues from where the view is. Pressing the key again, or any movement, rise or sink input, lets go (the mouse and the wheel do not); a gone soldier or an emptied company lets go with one message. The shared cleanup drops the target.

Entry checks: the mode is not Deployment, Conversation, Barter or CutScene; photo mode is off; no radial menu is open; `MissionScreen.CustomCamera` is null (a
custom camera is a siege engine or a board game, and the message says the Seat cannot be taken from this view); the mission has not
ended; no warg rage holds the hero. A battle that begins in the Seat (`Begin Battles in the Seat`) enters Seated with no blend at the
current camera frame when Deployment gives way to Battle.

### Component Diagram

```
 MCM: the four Seat settings                   Options > Keybindings > Action: the Seat's keys (530, 531, 536)
              |                                              |
   SeatSettingsProvider (lazy)                          SeatHotKeys (GameKey reference)
              \                                              /
               SeatView  ([DefaultView], TAOM.Client)
                  |   OnMissionScreenTick: the key        OnMissionModeChange: the Deployment to Battle edge
                  |   UpdateOverridenCamera: the frame    OnMissionTick: the stand-fast write (the only hero write)
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

All are new settings. Never flip a default under an unchanged name once it has shipped: the json2 file keeps the saved value.

### Key: `TaomSeatHotKeyCategory`

Three game keys under Options > Keybindings > Action: "Take the Seat (TAOM)", id 530, default Q; "Keep to this soldier or company (TAOM)", id 531, default Insert; and "Hide the interface (TAOM)", id 536, default End. Keys 532 to 535 belong to stages S5 and S6. The strings are
`str_key_name.TaomSeatHotKeyCategory_<id>` and `str_key_description.TaomSeatHotKeyCategory_<id>` for each of the three ids, in `Main/_Module/ModuleData/global_strings.xml`.
A default stays clear of every key the mission's scene layer reads. `MissionScreen` registers six categories on that layer, and one
physical key then acts twice: `B` was key 36 of `GenericCampaignPanelsGameKeyCategory`, so a Seat on `B` printed "Can't access banner
window right now!" on each press in a campaign mission. A test loops over every registered key and pins each default against the generic context, `GenericCampaignPanelsGameKeyCategory`, `ScoreboardHotKeyCategory`, `CombatHotKeyCategory`, the cheats category, `MissionOrderHotkeyCategory` and `ChatLogHotKeyCategory`. It leaves
`GenericPanelGameKeyCategory` out: that category holds `Q` and `E` for a panel's tab change, but only panel layers read them, and
vanilla's own use key is `E`, so `Q` is free in a mission.
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
| `Client/Features/SeatOfSeeing/Hooks/` | The ten Patch140 classes and `SeatOrderRay` |
| `Client/Features/SeatOfSeeing/SeatKeys.cs` | The keys beyond "Take the Seat" |
| `Main/Features/SeatOfSeeing/Domain/SeatPanelRule.cs`, `SeatPointer.cs`, `SeatHideRule.cs`, `SeatKeepRule.cs`, `SeatOrbit.cs` | The pure rules of stages S2 and S3 |
| `Client/Adapters/SeatScreenAdapter.cs` | `MissionScreen` and controller reads and writes, the cursor layer, the panel site and the kept target |
| `Client/Adapters/ISeatScreenAdapter.cs` | The interface the session drives (a client type, with no null) |
| `Main/Adapters/ISeatHeroAdapter.cs`, `SeatHeroAdapter.cs` | Stand fast |
| `Main/Features/SeatOfSeeing/SeatOfSeeingModule.cs` | DI, the patch category, the key registration |
| `Main/Features/SeatOfSeeing/TaomSeatHotKeyCategory.cs`, `SeatHotKeys.cs` | The rebindable key and its reader |
| `Main/Features/SeatOfSeeing/SeatSettingsProvider.cs` | MCM provider with the lazy accessor |
| `Main/Features/SeatOfSeeing/SeatPresence.cs` | The one static the patches read |
| `tools/ingame_checks/0.toml` | The in-game verdict manifest (a draft with `issue = 0`, see below) |
| `Main/Features/SeatOfSeeing/Domain/SeatTrialRows.cs`, `SeatTrialSteps.cs`, `SeatTrialRules.cs`, `SeatTrialRoster.cs` | The field trial's Seat rows, the step table joined from them, pass rules and armies (pure) |
| `Client/Features/SeatOfSeeing/Trial/` | The field trial: `CommandTrialOptions`, `CommandTrialSettings`, `CommandTrialRun`, `CommandTrialAutostart` over `ICommandTrialGameHost` (`CommandTrialGameHost`), `CommandTrialGameManager`, `CommandTrialLauncher`, `CommandTrialScript` (the driver) with `CommandTrialScript.Seat.cs` (the thirteen steps), and `ICommandTrialHost` with `CommandTrialHost`, each with a `.Seat.cs` part |
| `tools/run_command_trial.py` | The runner and judge of the field trial |

## Dependencies

- `ISeatSettingsProvider` (feature): the four MCM values (the Seat, Begin Battles in the Seat, the cursor with the panel, keep the panel open).
- `ISeatScreenAdapter` (TAOM.Client, built per mission): the screen and the mission camera. `ISeatHeroAdapter` (Adapters): the agent.
- `IOutcomeLog` and `IModLogger` (Core): the check lines and the fault log.
- `FordsCameraMath` and `Vec3f` (Fords of War): the camera basis and the pan speed, by direct call.

### Co-op: the four questions

`docs/features/coop-interop.md` "Four questions, not one":

1. **Is a co-op module enabled** (`ICoopPresenceProvider.IsCoopActive`)? Not asked. Nothing here is a startup decision, and the Seat
   neither needs co-op nor conflicts with it.
2. **Is a session live, and do I own the simulation** (`ICoopSessionProvider`)? `GameNetwork.IsSessionActive` is the `NetworkSession`
   fact of the policy. In a session the Seat keeps the camera and the orders its kind allows, and loses the hero hand-off and mission
   time (the engine turns scene time speed off in a session). The Seat writes no campaign or battle state. Stand fast writes agent state
   (movement flags, movement input, event control flags and the head camera mode), but only on this peer's own player agent and only
   from the mission tick, and the camera and the controller gate are this peer's own, so it needs no authority. `SeatView` is a
   `Local` row in `CoopMissionRoles`, and a `FightOn` hand-off never runs in a session.
3. **Is this the headless server** (`IDedicatedServerProvider`)? Not asked. A dedicated server builds no screen: TAOM.Client is not
   loaded there, so no `SeatView` exists, and the only reader of the mission facts is in TAOM.Client.
4. **Is this party or hero driven by a human** (`IPlayerControlQuery`)? Not asked. The Seat acts on `Mission.MainAgent`, which in a
   mission is this peer's own player agent; it credits, charges and rewards nothing.

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
  `SeatOfSeeingManifestTests` in `tools/tests/test_ingame_verdict.py` reads, and the three failure lines of the feature in the code.
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
- `tools/tests/test_ingame_verdict.py`: `SeatOfSeeingManifestTests` runs the real manifest against eighteen fixtures (a full visit, a
  fault, a missing elevation setter, an enter only, a leave with no agent to follow, refused key presses, an enter that could not read,
  each of the three failure lines of the feature, a full visit of stages S2 and S3, a reopen that cannot reach the view model, a reopen
  the engine refused, a keep that finds nobody, an interface hidden and never restored, a keep then a leave, a keep then a flight let-go,
  and a Seat patch that failed to apply).
- Stages S2 and S3: `SeatPanelRulesTests` and `SeatKeepRulesTests` (the pure rules at their edges, the orbit's signs and its continuity with
  the camera), `SeatSessionPanelTests` and `SeatSessionKeepTests` (the cursor, the hidden interface, the reopen with each close route on this
  frame and the previous one, the shared cleanup with a throwing step, the mission tick's separate jobs, the keep press, the orbit, each let-go
  reason), `SeatScreenPatchWiringTests` (the eight targets, the crosshair call, no override, the injected field), `SeatPanelWiringTests`
  (source pins of the view's key reader, the focus patch's mark and the adapter, and that no other TAOM code writes `MBDebug.DisableAllUI`; the pins of `SeatSession`'s own text went where a behaviour test covers the case),
  `CommandTrialSeatStepsTests` (each new step passes on a working stand-in and fails with its own facts when its one feature is broken),
  `SeatTrialAimTests`, and the widened marker gate in `CommandTrialWiringTests`.
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
5. `taom.seat-of-seeing.fault` must stay absent. A fault line fails all ten checks in the manifest.
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

An unreachable step is reported with the command tried and the unmet precondition. No person has run the 22 steps. The [field trial](#field-trial) covers, without a person: the camera half of steps 1, 2 and 3, the Deployment half of step 8, and the Seat's side of steps 17 to 20.
Step 17 is covered for the cursor with the panel and the focus point, not the middle-button look or the charge itself; step 18 for a reopen after a close with no player key and a close the trial marks as the player's, not the real Escape, right mouse button, panel key or hold key; step 19 for the key and the restore when the panel opens, not a dialog, another layer or `toggle_ui`; step 20 for the pick from the screen centre, the orbit distance and the let-go by key and by death, not the mouse, the wheel, a movement key or a hill and a wood.
It feeds the flight input itself, so it leaves the engine's input reads, the look and the hero's head to a person.

## Field trial

Unit tests cannot show that a camera flies over a real battle. The field trial (contract section 12) is a script inside the game: it
opens one real custom battle with nobody at the keyboard, presses the Seat's own controls in order, and writes one log line per fact.
It runs thirteen steps: stage S1's six, and seven for stages S2 and S3. Each later stage adds its steps as rows, and Marshal's Word adds eight of its own after the Seat's, in its own files (`Main/Features/MarshalsWord/Domain/MarshalTrialRows.cs`, and `Client/Features/MarshalsWord/Trial/` for the script, the host and the interface); [marshals-word.md](marshals-word.md) lists them. The wrapper's `STEPS` joins the Seat's tuple and Marshal's. Its history is below, run by run.

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
json2 keeps saved values), with the Seat on, Begin Battles in the Seat off, the cursor with the panel on and Keep the order panel open on. An
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

Each line reads `[SeatTrial] step=<name> result=<PASS|FAIL> <facts>`, for example `step=fly result=PASS moved=60.0 expected=60.1
height=22.0 seconds=2.0 dot=1.0` (numbers from the test stand-in). The facts are what a person reads when a step fails. `leave_glide` writes `after=`, the distance 0.1 s after
the leave, so a glide that eases (large) and one that snaps (small, the known limit below) can be told apart; a snap does not fail the step.
Pictures: `hud_enter.bmp` two seconds after the entry, `hud_fly.bmp` 1.5 s into the flight, `hud_hide_bars_restored_hidden.bmp` and `hud_hide_bars_restored_back.bmp`, and `hud_keep_to_company.bmp`, which the runner turns into PNG files. Nothing judges a picture by machine: a person or a session looks at them.

Every agent, formation or mission flag write in a step (opening or closing the panel, selecting a company, `Mission.KillAgentCheat`) runs after `yield return Verdict.InMissionTick`; a wiring gate reads the host for such members and fails any step that calls one without the marker. The one exception is the release after a failed step, which closes a panel the step left open from whichever tick reported the failure, because vanilla closes this panel from its own screen tick (`GauntletOrderUIHandler.OnMissionScreenTick`, through `MissionOrderVM.TryCloseToggleOrder`). The last row kills soldiers, so run it after the others or as a subset: `--steps enter,keep_to_soldier,keep_to_company,let_go_on_death` proves stage S3 and `--steps enter,cursor_with_panel,focus_follows_cursor,keep_panel_reopens,hide_bars_restored` proves stage S2.

**How the key gets pressed.** `SeatView.PressSeatKey()` sets the flag that the real key test sets in `OnMissionScreenTick`, and the
session reads that one flag, so the trial and a player take one path. `SeatView.TrialInput` replaces `ISeatScreenAdapter.ReadInput` in
`OnFrame` while it is set, and `PressPending` is a read-only view of the flag. The host reads `State`, `Frames`, `GlideBegan` and
`Faulted` from the session, and its ground reads go through the view's one `SeatScreenAdapter`, so a later stage's cursor and kept
target are the state the trial sees.

**How the script is built.** `CommandTrialScript.cs` is the driver and `CommandTrialScript.Seat.cs` holds the thirteen steps and their
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
one failed or skipped class is up to ten Seat patches off), the thread guard's `[TAOM]` error line for a trial or Seat site (`CommandTrialHost`,
`SeatHeroAdapter`; another feature's guard line does not count), a log with no
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
- A banner bearer's effect and the panel's soldier count assume a player-controlled hero; the hero stays player-controlled in S1, so
  neither is affected yet. They matter in S4.
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
fact is read except on a key press or a stored deployment edge. Each of the ten `Patch140` hooks tests one volatile bool whenever its target
runs, and the formation focus target runs per enemy formation. While seated,
`ReadFacts` runs twice a frame, once in the screen tick and once in `OnFrame`; the input is read once a frame, and the engine's ground
height and boundary tests are called once a frame each (two ground reads while rising). `OutcomeCounter` counts owned frames with one
increment. Stage S2 adds, while Seated: one field read (`Mission.IsOrderMenuOpen`) a frame, and a closed panel reads nothing else. With the
panel open it also reads the cursor setting and, with that on, the middle button. The Keep the order panel open setting is read only while the
panel is open and on the frame it closes, and with it on the close routes are read then (five reads at most), and the Transfer flag on that frame only. The dialog and the focused
layer are read only while the interface is hidden. Stage S3 adds, while keeping, one target read a frame: an orbit frame replaces the flight
frame and reads the ground once, as the flight does. The two extra key tests a screen tick are one static read each, and run only while Seated.
The rays of a pick are cast once, in the mission tick after the press. The cursor layer stays: once shown it is an empty `GauntletLayer` that
ticks for the rest of the mission, and its cost is unmeasured (a frame capture is the way to find out if it matters).

## Changelog

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

- **Issue:** none yet; it waits for the maintainer's word, and two commits of the branch (`f2ee9a16f`, `8b041f03e`) exist without one. `tools/ingame_checks/0.toml` (`issue = 0`) is renamed to `<issue>.toml`, with
  `issue` set, when it exists. Add a `triage-needs-ingame` label on close: the in-game steps above are owed.
- **Status:** S1 to S3 built. Stages S2 and S3 ran once before their review fixes, and the fixed build has not run in the game. For S1, the field trial ran once in the game (2026-11-04, build `1735f2add.dirty`, six steps passed, exit 0); what it did
  not cover is listed under Field trial, and the 22 steps for a person are still owed.
