# Seat of Seeing

## Overview

The Seat is a commander's view that any mission with a main screen can use. One key (Take the Seat, default Q, rebindable) lifts the
player's view off the hero over about 0.7 seconds. The view then flies freely over the field with the engine's own movement keys, and
the same key brings it back to the hero: with the engine's short return glide from third person, at once from first person. The hero
stands fast while the view is up. This is stage S1 of [build-spec.md](seat-of-seeing/build-spec.md); the pointer and panel (S2),
keeping to a soldier (S3), the hero fighting on (S4), taking a soldier after a fall (S5), mission time (S6) and rising to give orders
(S7) are not built.

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

Entry checks: the mode is not Deployment, Conversation, Barter or CutScene; photo mode is off; `MissionScreen.CustomCamera` is null (a
custom camera is a siege engine or a board game, and the message says the Seat cannot be taken from this view); the mission has not
ended; no warg rage holds the hero. A battle that begins in the Seat (`Begin Battles in the Seat`) enters Seated with no blend at the
current camera frame when Deployment gives way to Battle.

### Component Diagram

```
 MCM: SeatEnabled, BeginBattlesInSeat          Options > Keybindings > Action: Take the Seat (id 530)
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

Both are new settings. Never flip a default under an unchanged name once it has shipped: the json2 file keeps the saved value.

### Key: `TaomSeatHotKeyCategory`

One game key, "Take the Seat (TAOM)", id 530, default Q, under Options > Keybindings > Action. The strings are
`str_key_name.TaomSeatHotKeyCategory_530` and `str_key_description.TaomSeatHotKeyCategory_530` in `Main/_Module/ModuleData/global_strings.xml`.
A default stays clear of every key the mission's scene layer reads. `MissionScreen` registers six categories on that layer, and one
physical key then acts twice: `B` was key 36 of `GenericCampaignPanelsGameKeyCategory`, so a Seat on `B` printed "Can't access banner
window right now!" on each press in a campaign mission. A test pins the default against `CombatHotKeyCategory`,
`GenericCampaignPanelsGameKeyCategory`, `ScoreboardHotKeyCategory` and `MissionOrderHotkeyCategory`. It leaves
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
| `Client/Features/SeatOfSeeing/Hooks/` | The two Patch140 classes and `SeatOrderRay` |
| `Client/Adapters/SeatScreenAdapter.cs` | `MissionScreen` and controller reads and writes |
| `Client/Adapters/ISeatScreenAdapter.cs` | The interface the session drives (a client type, with no null) |
| `Main/Adapters/ISeatHeroAdapter.cs`, `SeatHeroAdapter.cs` | Stand fast |
| `Main/Features/SeatOfSeeing/SeatOfSeeingModule.cs` | DI, the patch category, the key registration |
| `Main/Features/SeatOfSeeing/TaomSeatHotKeyCategory.cs`, `SeatHotKeys.cs` | The rebindable key and its reader |
| `Main/Features/SeatOfSeeing/SeatSettingsProvider.cs` | MCM provider with the lazy accessor |
| `Main/Features/SeatOfSeeing/SeatPresence.cs` | The one static the patches read |
| `tools/ingame_checks/0.toml` | The in-game verdict manifest (a draft with `issue = 0`, see below) |

## Dependencies

- `ISeatSettingsProvider` (feature): the two MCM values.
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
  fake log keeps the real log's `(id, result, key)` dedup.
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
- `tools/tests/test_ingame_verdict.py`: `SeatOfSeeingManifestTests` runs the real manifest against ten fixtures (a full visit, a
  fault, a missing elevation setter, an enter only, a leave with no agent to follow, refused key presses, an enter that could not read,
  and each of the three failure lines of the feature).

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
5. `taom.seat-of-seeing.fault` must stay absent. A fault line fails all four checks in the manifest.
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
11. From the Seat press the drop and equip key, the career ability key (V): both still act.
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

An unreachable step is reported with the command tried and the unmet precondition. None of the 16 steps has been run: no game was
launched while building S1.

## Known limits

- `OrderController.GetOrderFormCustomWidth` measures from `Agent.Main.Position`, so a form-width drag from the Seat measures from the
  hero. No patch (contract 5.8).
- The glide starts with `_cameraApplySpecialMovementsInstantly` set in a mission that has a `DeploymentMissionController` whose player
  side could not deploy with Order of Battle at the first deployment plan (`_playerDeploymentCancelled`, MissionScreen:4562, 4660-4661,
  4706); in that mission the return snaps. Which battles do so is UNVERIFIED.
- Stand fast has three readers it does not stop, each on a key the Seat does not use: the hero takes no movement or combat input, but
  the drop and equip key (game key 22), the career ability key (`V`) and the engine's perspective toggle (game key 27) still act. Each
  needs a deliberate press, and a patch on each would not pay for itself (contract 5.5).
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
3. Read it in `SeatView.OnMissionScreenTick` through `SeatHotKeys`, never in `OnMissionTick`.

## Performance

A player who never presses the key pays one `GameKey` read and one static `Input.IsKeyPressed` in each mission screen tick, plus one
bool read; the view's own context test (which allocates) runs only when the static one says pressed, and no setting and no mission
fact is read except on a key press or a stored deployment edge. While seated,
`ReadFacts` runs twice a frame, once in the screen tick and once in `OnFrame`; the input is read once a frame, and the engine's ground
height and boundary tests are called once a frame each (two ground reads while rising). `OutcomeCounter` counts owned frames with one
increment.

## Changelog

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

- **Issue:** none yet. Open one before the first commit; `tools/ingame_checks/0.toml` (`issue = 0`) is renamed to `<issue>.toml`, with
  `issue` set, when it exists. Add a `triage-needs-ingame` label on close: the in-game steps above are owed.
- **Status:** S1 built, not run in the game.
