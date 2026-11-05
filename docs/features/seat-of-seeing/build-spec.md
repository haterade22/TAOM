# Seat of Seeing: build specification

This file is the contract for the Seat of Seeing feature. It pins names, folders, default numbers
and engine seams, so that stages written by different agents fit together. When code and this file
disagree, fix the code, or record the change under "Deviations" at the end with the reason.

The orders half of the commander feature, Marshal's Word, has its own contract in
[../marshals-word/build-spec.md](../marshals-word/build-spec.md).

## 1. What the feature is

**The Seat** is a commander's view that any battle or other mission can use. One key lifts the
player's view off the hero. The view then flies freely over the field, and the player gives orders
from it. The same key brings the view back to the hero with a short glide.

- Every mission with a main screen gets it, by mission policy (section 4). A mode with its own
  camera (Fords of War) stands down.
- It costs nothing until the key is pressed. A player who never presses the key sees vanilla
  behaviour in every mission.
- The hero does not vanish. In the Seat the hero either **stands fast** (holds in place, takes no
  input) or **fights on** (the engine AI drives the agent inside its formation). The mission policy
  and a setting pick between them.
- After the hero falls, the player keeps the Seat and may **take a soldier**, who then carries the
  player's voice to the formations.
- In single player the Seat also holds mission time: pause, slow motion and fast time.

Dropped from the first version, and named here so that nobody builds them by accident: a distance
limit on the view, skill experience for using the view, a team swap, a death switch, a camera API for
other mods, and depth of field.

## 2. Layers and folders

| Layer | Folder | May reference |
|---|---|---|
| Pure rules | `Main/Features/SeatOfSeeing/Domain/` | BCL only. No `using TaleWorlds.*`. A gate test enforces it |
| Pure camera arithmetic | `Client/Features/SeatOfSeeing/SeatCameraMath.cs` | `FordsCameraMath` and `Vec3f`, no engine type |
| Core wiring | `Main/Features/SeatOfSeeing/` (feature module, settings, hot key category) | Domain, adapter interfaces, `TAOM.Core.Logging` |
| Agent adapter | `Main/Adapters/ISeatHeroAdapter.cs`, `SeatHeroAdapter.cs` | Server-safe TaleWorlds assemblies only |
| Screen adapter | `Client/Adapters/ISeatScreenAdapter.cs`, `SeatScreenAdapter.cs` | Anything client-only (ADR-013). No core caller exists, so core has no null for it |
| Client | `Client/Features/SeatOfSeeing/` (views, cursor layer) | Anything client-only (ADR-013) |
| Patches | `Client/Features/SeatOfSeeing/Hooks/`, or `Main/Features/SeatOfSeeing/Hooks/` with `[ClientOnly]` when a client-only assembly is named | Thin, under 150 lines (ADR-002) |
| Tests | `TAOM.Tests/Features/SeatOfSeeing/` | MSTest and NSubstitute |

Namespaces: `TAOM.Features.SeatOfSeeing`, `TAOM.Features.SeatOfSeeing.Domain`,
`TAOM.Features.SeatOfSeeing.Hooks`, `TAOM.Client.Features.SeatOfSeeing`,
`TAOM.Client.Features.SeatOfSeeing.Hooks`, `TAOM.Adapters`. File-scoped namespaces, `internal sealed`
by default, nullable enabled, C# 10. Test names: `Method_State_Expected`.

Project rules that apply (read the files, this list does not restate them): `AGENTS.md` (TDD,
architecture, banned constructs), `.claude/rules/csharp-architecture.md` (agent slots, threads,
`RunOrDefer`), `.claude/rules/harmony-patches.md` (thread table, mission behaviour lifecycle),
`docs/reviews/lessons/harmony-il.md` before any patch, ADR-002, ADR-007, ADR-013.

Builds and tests never deploy: `dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=` and
`dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`. Never `./build.ps1`.

Before you change an existing TAOM type, run
`python tools/graphify_taom.py affected "<Type>" --depth 2`, after `refresh --if-stale`.

Signatures come from the installed engine: `pwsh tools/taom-src.ps1 path <Type>`. The line numbers in
this file are from the v1.5.6 decompile that command reads. If a member differs, trust the engine and
record the deviation.

## 3. Keys

Every key is a rebindable game key in `TaomSeatHotKeyCategory`, built the same way as
`Main/Features/CareerSystem/Abilities/TaomCareerHotKeyCategory.cs`. Ids start at 530. Main category
`GameKeyMainCategories.ActionCategory`. Strings are `str_key_name.TaomSeatHotKeyCategory_<id>` in
`Main/_Module/ModuleData/global_strings.xml`, with the `(TAOM)` suffix of the sibling rows.

**Never register this context on a layer.** `RegisterHotKeyCategory` sizes its slot list from the
first category on that layer, so an id above 115 throws (`AbilityInputAdapter.cs:32-37`). Hold the
`GameKey` reference, read its `KeyboardKey.InputKey` each frame (a rebind mutates it), and test the
key with the view's own `Input.IsKeyPressed(key)`, which is the scene layer's context and so goes
quiet behind a menu. Test the static `TaleWorlds.InputSystem.Input.IsKeyPressed(key)` first: the context's test runs `CanUse`, which
gets a new click key array from `Input.GetClickKeys()` on each call (`InputContext.cs:310-312`, 498-505), and every mission pays this
each frame. An unbound key reads as not pressed.

| Id | Name | Default | Stage |
|---|---|---|---|
| 530 | Take the Seat | `Q` | S1 |
| 531 | Keep to this soldier or company | `Insert` | S3 |
| 532 | Take this soldier | `Delete` | S5 |
| 533 | Quicken time | `PageUp` | S6 |
| 534 | Slow time | `PageDown` | S6 |
| 535 | Stay time | `Home` | S6 |
| 536 | Hide the interface | `End` | S2 |

**A default stays clear of every key the mission's scene layer reads.** Eight categories reach that layer, and the same physical key then acts twice:

- `MissionScreen` registers six itself (389-394): `Generic`, `GenericPanelGameKeyCategory`, `GenericCampaignPanelsGameKeyCategory`, `ScoreboardHotKeyCategory`, `CombatHotKeyCategory` and `Cheats`.
- `MissionGauntletSingleplayerOrderUIHandler.OnMissionScreenInitialize` adds `MissionOrderHotkeyCategory`, the order panel's keys (BackSpace, F1 to F9, D0 to D8).
- `GauntletChatLogView` adds `ChatLogHotKeyCategory` on that same input at run time (199-203).

What that means for a default:

- `CombatHotKeyCategory` is the fighting keys. `ScoreboardHotKeyCategory` and `Cheats` both name `H`, so `H` is out.
- `GenericCampaignPanelsGameKeyCategory` is the panels, read by `MissionSingleplayerViewHandler` in every campaign mission (37-127): `B` is
  key 36 there, so a Seat on `B` would also print "Can't access banner window right now!" on each press, and `I` and `N` are out for the
  same reason.
- `GenericPanelGameKeyCategory` holds `Q` and `E` for a panel's tab change. Only panel layers read those (`SwitchToPreviousTab`), and
  vanilla's own use key is `E`, so `Q` is free in a mission.
- **`T` and `Y` are out.** `GauntletChatLogView` reads keys 6 (`T`) and 7 (`Y`) from the mission's scene layer in single player too (241-249, with `_canFocusWhileInMission` true by default at 24, and
  `BannerlordConfig.EnableSingleplayerChatBox` true at 474). On release the chat box starts typing and takes the keyboard (304-309), so the flight keys would type letters until Escape.
- `PageUp`, `PageDown`, `Home`, `End` and `Insert` have no reader on the scene layer outside the debug category. `Delete` is only in `GenericPanelGameKeyCategory`. `Home`, `End` and `Delete` also act in a text widget, and a text
  widget holds the focus while it does. So the six non-flight Seat keys sit together in one cluster on the keyboard.
- TAOM's own defaults: career `V`, time control `Space` and `E`, Realm Borders `M` and `G` on the campaign map, Fords `Q`, `E`, `T`,
  `H`, `N`, `M` in its own view, where the Seat stands down.

A test pins each Seat default, looping over every registered key, against those categories, the chat category included: plain if their constructors run in the test host, else `RequiresGame`.
It leaves `GenericPanelGameKeyCategory` out, with a comment that says why.

These keys read through the engine's existing game keys, so the player's rebinds keep working:

| Action in the Seat | Source |
|---|---|
| Fly along the ground | `Input.GetGameKeyAxis("MovementAxisX")`, `"MovementAxisY"`, with the engine's 0.2 dead zone (MissionScreen:1781-1790) |
| Rise, sink | Game keys 14 (`GameKeyDefinition.Jump`) and 15 (`GameKeyDefinition.Crouch`) (MissionScreen:1794-1801) |
| Fly twice as fast | Game key 24 (`GameKeyDefinition.Zoom`) (MissionScreen:1765) |
| Change height | Mouse wheel, `Input.GetDeltaMouseScroll() / 120` |
| Look, cursor hidden | `Input.GetMouseMoveX()`, `GetMouseMoveY()` scaled by `Input.GetMouseSensitivity()` and 0.0035, honouring `NativeConfig.InvertMouse`, the same as MissionScreen:2971-3002. With no active mouse add `GetGameKeyAxis("CameraAxisX")`, `"CameraAxisY"` with the engine's `dt / 0.0009` scale (2942-2958) |
| Look, cursor shown | Hold the middle mouse button |

Game keys 14 and 15 must read from the scene layer's input, as the engine does. Right mouse keeps its
vanilla meaning inside the order panel (back).

## 4. Mission policy

One pure function decides what the Seat may do in a mission. Everything else asks it. It lives in
`Main/Features/SeatOfSeeing/Domain/SeatMissionPolicy.cs`.

```csharp
internal enum SeatMissionKind { Battle, Hideout, Tournament, Arena, Settlement, Other }

internal readonly record struct SeatMissionFacts(
    SeatMissionKind Kind,
    bool NetworkSession,          // GameNetwork.IsSessionActive
    bool ForeignCamera);          // section 5.6

internal readonly record struct SeatPermissions(bool Camera, bool FightOn, bool Orders, bool Time);

internal static class SeatMissionPolicy { internal static SeatPermissions Decide(in SeatMissionFacts facts); }
```

| Kind | Camera | FightOn | Orders from the Seat | Time |
|---|---|---|---|---|
| Battle: field, siege, staged siege, raid, ambush, custom battle, Long Defeat | yes | yes | yes | yes |
| Hideout | yes | no | yes | yes |
| Tournament, Arena | yes | no | no | no |
| Settlement: town, village, indoor | yes | no | no | no |
| Other | yes | no | no | no |
| Any kind with `NetworkSession` | yes | no | as the kind says | no |
| Any kind with `ForeignCamera` | no | no | no | no |

A dedicated server never asks: the only reader of the facts is in `TAOM.Client`, which does not load there.

**A native multiplayer game mode gets no rule, because TAOM does not run there.** `MissionMainAgentController.EarlyStart` subscribes its own
`Disable` and `Enable` to the round component's `OnRoundStarted` and `OnPreparationEnded` when the mission holds a
`MissionMultiplayerGameModeBaseClient` with a round component (lines 189-193). In such a mode, the hero would walk on the flight keys when
preparation ends under a Seat that is up, and a leave during preparation would turn the controller on early. `Main/_Module/SubModule.xml:8`
makes TAOM a `Singleplayer` category module, no TAOM mission holds that behaviour, and neither does the co-op module. So this is a known
limit and not a guard. If TAOM ever runs in such a mode, refuse the Seat there with one more fact in `SeatMissionFacts`, because a free view
in a versus round is also an unfair view.

"Orders from the Seat" means the order tools the Seat adds. Vanilla's own panel stays wherever vanilla builds it (`AlleyFight` is a
Settlement kind that has one), and the S1 ray patches serve it there.

**Read the kind fresh.** `SeatMissionClassifier` reads `Mission.Mode`, so `Kind` depends on when it is asked. S1 reads only `Camera`,
which no kind changes, so `SeatView` may cache that answer. Any later reader of `FightOn`, `Orders`, `Time` or `TakeSoldier` caches only
the foreign-camera walk (5.6) and asks for the rest when it acts. S4 is the first reader that needs this, and S1's seam does not allow it: `SeatView` caches the whole answer, and `ReadFacts`
walks and logs on every call. So S4 splits it: `SeatMissionClassifier` gains a public `ForeignCameraOwns(Mission, IModLogger)` beside `ReadFacts(Mission, ICoopSessionProvider, bool foreignCamera)`, the view caches
only the bool from the walk, built at the first ask, and `Camera`'s result stays as it is. `ReadFacts(Mission, ICoopSessionProvider, IModLogger)` stays as a one-line call of the two, because Marshal's Word calls it.

Stage S5 adds a fifth permission, `TakeSoldier`: only the Battle row with no `NetworkSession`. A hideout refuses it, because nothing
yet proves how a stand-in ends the duel (`HideoutMissionController.cs:727-732`), and the ambush has its own controller class and its
own mode flow (`HideoutAmbushMissionController.cs:25`).

Three missions get an answer here, so that nobody asks again:

- **An enlisted soldier stripped of command** keeps the camera and gets no orders. `Main/Features/Enlistment/BattleCommandPolicy.cs` decides
  command ("one predicate, three consumers"), and vanilla's panel refuses by itself. Anything that widens `MissionOrderVM.CheckCanBeOpened`
  (8.2) keeps that policy's tests.
- **Host Review** keeps the Seat. It is a battle kind with `DisableDying` set, and `docs/features/host-review.md` asks only a behaviour that
  must not run there to gate on `HostReviewMissionLogic.IsActiveIn`. Nothing in this contract writes `DisableDying`.
- **A tournament spectator** keeps the camera. `TournamentBehavior` reports its own camera lock mode through `ICameraModeLogic`, and the
  Seat does not report one. `UpdateOverridenCamera` runs before that mode matters, so the Seat owns the frame while it is up.

`SeatMissionClassifier` in `Client/Features/SeatOfSeeing/` has one entry until S4 splits it as above, `ReadFacts(Mission, ICoopSessionProvider, IModLogger)`, which returns
`SeatMissionFacts`. `SeatView` calls it, and the later feature Marshal's Word will. It is a static helper, so its caller passes the logger and the co-op provider (row 97). It turns a live mission into `SeatMissionKind`
from presence tests, never from mission name strings (names rot): a `HideoutMissionController`,
a `TournamentBehavior`, an `ArenaPracticeFightMissionController`, then `Mission.CombatType` and
`Mission.Mode`. `Mission.Mode` is `StartUp` in towns, so Mode alone proves nothing. Read
`docs/features/coop-interop.md` "Four questions, not one" and answer them in the feature doc.

Add the view names to `Main/Features/CoopInterop/Missions/CoopMissionRoles.cs` as `Local` rows, with
the matching row in `ClientViewRows_MatchTheClientTypeNames` and in the doc's table. A view is
one peer's own screen, and a `FightOn` hand-off never runs in a session.

## 5. Stage S1: the camera, the hero stands fast

### 5.1 Entry point

`Client/Features/SeatOfSeeing/SeatView.cs`, a `MissionView` with `[DefaultView]`. A view TAOM adds in
`OnMissionBehaviorInitialize` never registers and never ticks (#970, see
`BattleActionBarMissionView.cs:13-16`). The default view is built in every mission that has a screen,
so its fields stay null-safe in every lifecycle hook, and it does no work before the key is pressed
beyond one key test a frame (section 3 says how, with no allocation) and one bool.

Keep it under 150 lines. Everything with a rule goes into pure types:

| Type | Assembly | Job |
|---|---|---|
| `SeatCameraMath` | Client | Flight step, height clamp, ground smoothing, blends, listener height |
| `SeatMissionPolicy` | Core | Section 4 |
| `SeatSession` | Client | The state machine (5.3) over an interface for the engine calls, so a test can drive it |
| `SeatHeroAdapter : ISeatHeroAdapter` | Core `Main/Adapters/` | Every write to the `Agent` itself (5.5). It holds no state, and it is a singleton |
| `SeatScreenAdapter : ISeatScreenAdapter` | Client | Every read and write on `MissionScreen` (5.4), and the `MissionMainAgentController` calls (5.5), because that type lives in the client-only `TaleWorlds.MountAndBlade.View` assembly (ADR-013). `SeatView` builds one per mission with `new SeatScreenAdapter(MissionScreen, Mission)`, as `BattleActionBarMissionView.cs:178` does with its adapter. Its fields are readonly and never null, it has no `Bind`, and nothing registers it |

`SeatSession` takes everything at construction: `new SeatSession(screen, hero, settings, Permissions, outcomes, logger, serial)`, where
`settings` is `ISeatSettingsProvider` and `Permissions` is the view's `Func<SeatPermissions>`. The screen tick is then `OnScreenTick(bool keyPressed)`,
and the session reads a setting only past its early return, so a frame with no key and no stored edge reads no setting.

### 5.2 Camera arithmetic

`SeatCameraMath` reuses `FordsCameraMath.Forward`, `PanSpeed`, `Clamp`, `WrapAngle` and a new `Pan(x, y, yaw, forward, right, distance)`
by direct call, and `SeatScreenAdapter.ApplyFrame` calls `FordsCameraMath.Basis`. Make `Clamp` and `WrapAngle` internal. `Pan` holds the
normalise and the move relative to the yaw, and both `Step` methods call it, so the Seat flies like the Fords camera by construction.
Conventions are the same: X and Y are ground, Z is up, yaw 0 looks along +Y, positive yaw turns
left, pitch is the angle below the horizon, and the frame is `(side, up, back)`.

```csharp
internal readonly record struct SeatFlight(float X, float Y, float Height, float Yaw, float Pitch);
internal readonly record struct SeatInput(float Right, float Forward, float Lift, float ZoomNotches,
    float LookYaw, float LookPitch, bool Fast);
internal readonly record struct SeatPose(float X, float Y, float Z, float Yaw, float Pitch);
```

| Constant | Value | Note |
|---|---|---|
| `MinHeight`, `MaxHeight` | 1.5 m, 110 m above the ground | |
| `MinPitch`, `MaxPitch` | -0.35 rad, 1.45 rad | Below the horizon is positive, so -0.35 looks up a little |
| `PanSpeed(height)` | `FordsCameraMath.PanSpeed(height)` | Same feel as the Fords camera |
| `Fast` | twice the speed | Same as the Fords camera |
| `LiftSpeed` | 8 m/s plus 0.5 per metre of height | |
| `ZoomPerNotch` | 0.88, multiplies the height | Same as the Fords camera |
| `LookPerUnit` | 0.0035 | The engine's scale, MissionScreen:2985 |
| `EntryHeight` | 22 m | The first time in a mission. Later entries keep the last height and pitch |
| `EntryPitch` | 0.60 rad | The first time in a mission |
| `EntryBackstep` | 9 m | Along the ground, against the look direction |
| `EntryBlendSeconds` | 0.7 s, smoothstep | Position and angles, shortest arc for yaw |
| `GroundSmoothing` | `1 - exp(-6 dt)` | Applied to the ground height under the camera |
| `GroundClearance` | 0.5 m | The view never goes lower than this above the raw ground. The engine keeps its own camera the same distance off the ground (MissionScreen:1885-1888) |
| `ListenerCeiling` | 16 m | The sound listener stays at most this far above the ground, so a high view still hears the battle |

Rules: `Step` normalises diagonal movement, moves relative to the yaw, multiplies the height for zoom,
adds lift, clamps height and pitch and wraps yaw. A positive notch is the wheel rolled away, and it
lowers the view, the same as `FordsCameraInput`. Every float from the engine passes
`FiniteFloatValidator` before it enters the state, and a non-finite `dt` leaves the state unchanged.
Unit tests pin each constant and rule, including yaw wrap on the blend.

**The floor.** Smoothing lags on a slope: at 1.5 m height, fast flight over a 45 degree hill leaves the smoothed ground about 3.8 m
behind, so the view would go under the terrain. Keep the smoothing, because it stops the view from bouncing over small bumps, and add
one pure rule: `AboveGround(pose, rawGround)` returns the pose with `Z` at least `rawGround + GroundClearance`, and returns the pose
as it is when `rawGround` is not finite. `SeatSession.OnFrame` applies it last, to the pose it gives to the screen, with the raw ground
under that pose's own X and Y. While Rising that point differs from the flight's, so read the ground there too. The blend is the only
case with a second read.

### 5.3 State machine

`SeatSession` has three states: `Off`, `Rising`, `Seated`. It owns the mission's last height and pitch.

- **Off to Rising** on the key, when `SeatPermissions.Camera` holds and the entry checks pass.
  **Entry checks:** `Mission.Mode` is not Deployment, Conversation, Barter or CutScene; photo mode is
  off; `MissionScreen.CustomCamera == null` (a custom camera is a siege engine or a board game, and
  the message says the Seat cannot start from this view); `Mission.MissionEnded` is false; no warg rage
  holds the hero (5.5, and the message says so). S2 adds: no radial menu is active (6.0).
- **Rising to Seated** when the blend finishes. Input does nothing during the blend.
- **Rising or Seated to Off** on the key, on a mode change to Conversation, Barter or CutScene, when a
  warg rage takes the hero (5.5), and on any fault (5.7).
- **Deployment.** The Seat refuses during Deployment, because vanilla's deployment camera already
  flies. When a mission moves from Deployment to Battle and the setting `BeginBattlesInSeat` holds,
  enter Seated with no blend at the current camera frame. A mission with no Deployment phase does not
  begin in the Seat: the player presses the key. The edge comes from the engine, not from a latch that
  the session keeps: `SeatView.OnMissionModeChange(oldMode, atStart)` (Mission:1630-1633 raises it on every
  behaviour) calls `_session?.OnDeploymentEnded()` when `oldMode` is Deployment and `Mission.Mode` is
  Battle. The session stores one bool. `OnScreenTick` reads and clears it before the state branch, in any state, so an edge that
  arrives while the Seat is up is dropped and cannot enter after the player leaves. The automatic entry also needs `facts.BattleMode` on
  the frame that consumes the edge. The session reads no facts on a frame with no key and no stored edge. A key pressed in Deployment
  with the setting off, and the setting turned on later in the battle, does not enter.
- **Teardown.** Both teardown paths finish the session: `OnMissionScreenFinalize` (the end-mission path, MissionScreen:4241-4245) and
  `OnRemoveBehavior` (`MissionState.OnFinalize` to `Mission.OnMissionStateFinalize` to `RemoveMissionBehavior`, which needs no
  `EndMission`; `docs/reference/engine/mission-and-missionbehavior-lifecycle.md` lines 100-123). `SeatPresence` is a static that the patches
  read, so a missed finish would leave them live in the next mission. **Finishing is terminal.** Each hook calls `_session?.Finish()`, then
  sets `_session` to null and removes the view's `Mission.OnMainAgentChanged` handler (5.5). `RemoveMissionBehavior` does not take the view out
  of the screen's collection (only `UnregisterView` does, MissionScreen:3688), so a screen tick can still run after it, and it must not re-enter.
- **Every later stage:** any state that outlives a frame or that a patch reads (a cursor layer, a held key, a saved value, a static) clears
  in one private session method that `Leave` and `Fault` both call. `Finish` reaches it through `Leave`, so both teardown paths and a fault
  are covered. State that lives while the Seat is Off (mission time in S6) is reset by `Finish` and by `Fault` directly, each step through `Swallow`, and `Finish` does that outside its
  `if (State != Off)` test and after its try, so a throw in `Leave` cannot skip it. The panel lift in S7 is zeroed by `Fault`, and it needs nothing from `Finish` (section 11). Never put a cleanup in only one of these places.

The view reads the state keys in `OnMissionScreenTick`, once per real frame. It never reads a key in
`OnMissionTick`, because fast time repeats that tick several times in one frame
(MissionState.cs:171-198). It reads the flight input in `UpdateOverridenCamera`, which runs once a frame
on real time (Mission.cs:3746-3749), so the view still flies when mission time stands still. Native
code sets the order of the screen tick and `Mission.OnTick`, so no rule may depend on that order.

No Seat key acts while photo mode runs. The state waits and carries on when photo mode ends.

The first entry in a mission shows one message that says the same key returns to the hero.

### 5.4 The frame

`UpdateOverridenCamera(float dt)` is the seat of the feature. `MissionScreen.CheckForUpdateCamera`
calls each view in behaviour order, and the first `true` wins (MissionScreen:858-906). Default views
come from a `HashSet` (`ViewCreatorManager`), so no rule may depend on the Seat's place in that order.
`CustomCamera` beats any override, so the Seat does nothing when it is set. Return `false` while `Off`,
while photo mode runs, and after a fault.

Finish the frame the way `FordsCommanderCameraView.Apply` does, with these differences:

1. `MissionScreen.CombatCamera.Frame = frame` and `MissionScreen.CameraBearing = yaw`. **Do not call
   `UpdateFreeCamera`.** It writes `CameraElevation` with the opposite sign of the one `UpdateCamera`
   uses (compare MissionScreen:547-563 with 2087-2099), and the return glide reads that value.
2. `SetFovVertical` from the view angle the player had at entry, floored at 50 degrees. `UpdateCamera`
   is what refreshes `CameraViewAngle` (1680-1687), so it stops changing while the Seat holds the frame.
3. `SceneView.SetCamera(camera)`.
4. `Mission.SetCameraFrame(ref frame, 65f / viewAngle, ref listener)`, with the listener at the camera's
   X and Y and at `min(camera Z, ground + ListenerCeiling)`.
5. `scene.SetDepthOfFieldParameters(0f, 0f, false)` and `scene.SetDepthOfFieldFocus(0f)`, the two calls of
   the tail of `UpdateCamera` (2352-2356), which does not run on a Seat frame. Without them the view
   blurs. `HandleUserInput` keeps running: while the followed agent is the player's and game key 24 is
   down it raises `_zoomAmount` with any weapon on keyboard and mouse (a gamepad needs a ranged weapon
   out) and writes depth of field (3074-3078, 3119-3142, 3298-3303), and key 24 is the Seat's fast key.
   An entry made while zoomed in would also keep the blur for the whole stay.
   `HandleUserInput` runs before `UpdateCamera`, so this write wins. This keeps the view sharp, and it is
   not the depth of field effect that section 1 drops.

Ground height: `Scene.GetGroundHeightAtPosition` with `BodyFlags.CommonCollisionExcludeFlags` (`BodyFlags:47`, the same value as the
544321929 at MissionScreen:1885; a test pins it), the larger of that and the water level, as at line 1762. A null scene gives NaN. The
native probe starts 1.5 m above the asked Z and casts down, with a terrain fallback, and it returns 0 when nothing answers (0x4C5A20),
so vanilla's `num9 < 9999f` guard at 1886 never fails and the Seat has no such guard. A mesh surface more than 1.5 m above the asked Z
is unseen, the same as in vanilla's free camera.

Boundaries: `Mission.IsPositionInsideBoundaries`, then `GetClosestBoundaryPosition` (1864-1867). `OnFrame` and `Enter` pass the result
through `FiniteFloatValidator` (5.2), and a non-finite X or Y keeps the old value.

**The height limit.** Vanilla's free camera stops at the Z of the entity tagged `camera_height_limiter`, when the scene has one and the Z
is above 0 (MissionScreen:1894-1897, 4601-4605). Six SandBox arenas and nine TAOM_Map scenes carry the tag. `SeatScreenAdapter` reads the
tag's Z once, at `Enter`, and no tag or a Z at or below 0 means no limit. A pure rule in `SeatCameraMath` caps `pose.Z` at the limit, and
the session also caps `_flight.Height` at `limit - _ground`, floored at `MinHeight`, so the wheel does not wind up past the limit.
`AboveGround` still goes last, so a limit below the ground cannot put the view under it; vanilla caps after its floor, and the Seat
differs on purpose.

**Skipping `UpdateCamera` also skips** the per-frame `IsDisabled` write on the main agent controller
(1688-1699), the drag data (1669) and the focus upkeep. Section 5.5 covers the controller.

**The return glide.** The engine already has one, used when Deployment ends, and it needs no private
field from the caller. `MissionScreen` implements `IMissionListener.OnMissionModeChange(oldMode,
atStart)` as an explicit interface method (4511). `TaleWorlds.MountAndBlade.IMissionListener` is a
public interface, so call it through a cast. With `Mission.Mode == Battle` and `oldMode == Deployment` it arms
the special movement, seeds the position gap from the camera to the followed agent, seeds the bearing and
elevation gaps, and lets `UpdateCamera` ease them away (4559-4583).

On every leave except `Finish`, in any mode, `ISeatScreenAdapter.WriteLook(yaw, pitch)`. A parameter of `Leave` says so, never the text of
the log reason:

1. Set `CameraBearing = yaw`.
2. Set `CameraElevation` to the vanilla sign of the current look, which is minus `Pitch`. The setter
   is private (197), so this is one reflection site: cache it once, add the row to
   `docs/reference/taleworlds-api-snapshot/reflection-sites.md` Category B and the `[DataRow]` to
   `ReflectionSiteBindingTests`. A failed lookup logs once. Missing: `CameraElevation` keeps what
   `HandleUserInput` integrated from the mouse during the stay (MissionScreen:3051-3053, 3249-3251), so the
   glide's vertical angle starts from an unrelated value (4569), a visible jump. `WriteLook` returns whether
   the setter exists.

Then, in Battle mode only, `ISeatScreenAdapter.BeginGlide()`:

3. If `MissionScreen.GetSpectatingData(CombatCamera.Position).AgentToFollow` is null, return false and do not
   call the listener. With no agent to follow, the listener's branch zeroes the offset, the bearing and the
   elevation (4576-4582), and the free camera then looks along +Y, level. The look that step 1 and 2 wrote stays.
4. Else call `((IMissionListener)MissionScreen).OnMissionModeChange(MissionMode.Deployment, false)` and return true.

Return `false` from `UpdateOverridenCamera` from this frame on. The session writes the check lines: `glide` pass with
`elevation=set`, `glide` fail with `elevation=missing`, and `Skip(GlideId, ..., reason=no-agent)`.

In any other mode the branch at 4559 does not run, so the view snaps to the hero. That is acceptable.

**From first person the return is at once, not a glide.** The first-person path of `UpdateCamera` (1676) takes the eye position and
ignores the position offset the listener seeds. A glide there would need a return state of its own, which fails the simplicity table.
The feature doc and the manifest say so.

### 5.5 The hero while seated

`ISeatHeroAdapter` owns every write to the agent, and `ISeatScreenAdapter` owns the controller calls.
Stage S1 has one behaviour, **stand fast**:

- `MissionMainAgentController.Disable()` on entry and `Enable()` on leaving (1256-1263), through
  `ISeatScreenAdapter`. This is the `_activated` gate at line 264. Do not use `IsDisabled`:
  `UpdateCamera` overwrites it each frame whenever it runs (1694-1699), and the Seat must stay safe on
  any frame that returns `false`.
- Stand fast (`ISeatHeroAdapter.StandFast`) clears what `ControlTick` last wrote, because nothing rewrites it while the
  gate is shut: `MovementFlags`, `MovementInputVector` to zero, and `EventControlFlags` to zero. Confirm the member
  names in `Agent`.
- Stand fast also sets `HeadCameraMode = false`. `LookTick` writes it (419), and a first-person player's hero
  would otherwise hang in the sky view with no head.
- **These writes run only from the mission tick.** The key arrives in the screen tick, which may overlap the agent
  tick (`docs/reference/engine/mission-frame-threads-and-native-costs.md`), and vanilla writes these four fields
  only from `OnPreMissionTick` (239, 419, `ControlTick`). `Enter` sets `private volatile bool _standFastOwed` in the
  session. `SeatSession.OnMissionTick`, in a try that latches on a throw: while the state is not Off and the flag
  is set, clear it and call `_hero.StandFast()`. `StandFast` calls `MissionThreadGuard.NoteCall`. A paused mission
  still reaches `OnMissionTick`, with a `dt` of 0.
- The agent stays `AgentControllerType.Player`, so `Agent.IsCameraAttachable` and the order panel's
  `CheckCanBeOpened` keep passing (MissionOrderVM:750-757). The gate's `else` branch clears interaction
  focus each frame (274), so the use key cannot act from the air.
- `SeatView` subscribes to `Mission.OnMainAgentChanged` once, for the whole mission, right after it builds the session (the shape of
  MissionScreen:614), and its handler calls `_session?.OnMainAgentChanged()`, which only sets `_standFastOwed`. A raise while Off is
  harmless: `OnMissionTick` does nothing while Off, and `Enter` sets the flag anyway. The event may run inside a native callback on a
  chosen thread, and a bool write needs no queue, so `SeatHeroAdapter` has only `StandFast`: no `DeferredCallbackQueue`, no `Pump`, no held
  mission. The engine raises this event again for the same agent on any change to Player (the `MainAgent` setter has no test,
  Mission:1345-1354), so a raise does not prove a new agent, and the writes must stay safe to repeat.

**What stand fast does not cover.** The gate stops vanilla's movement, combat and look input. Six other readers stay live in S1, each on
a key the Seat does not use: the drop and equip key (game keys 13 and 22, `MissionGauntletMainAgentEquipmentControllerView:120-133`), the equip wheel (game key 34), the cheer wheel (game key 31),
the walk mode control (the hot key `ControlModeToggle`, the controller's left pad down), the career ability key (`V`, `AbilityInputAdapter`), and the engine's perspective toggle (game key 27, in `HandleUserInput`). Each needs a deliberate
press, and a patch on each would not pay for itself in S1, so this stage accepts them. Stage S2 closes the first four as part of the screen work (6.4) and updates the feature doc and the manifest;
the last two stay. S4's boundary prefix (8.2) also covers a stand-fast hero left outside the line, who retreats the mission after 10 s until then.

**Warg rage.** `Client/Features/AdvancedCombat/AutonomousMovementPlayerController.cs` is TAOM's second
controller of the hero. A hit on the player's warg calls `SwitchMainAgentController(true)`, which turns
vanilla's controller off and that one on, and the end of the rage calls `SwitchMainAgentController(false)`,
which reverses it (`PrepareOnFirstCall`, `FinishRageMode`). `OnWargDied` also calls the swap back with no rage
running (`OnWargDied.cs:38-39`, `FinishRageMode.cs:36`), for example when an arrow kills a calm warg. The controller reads game keys
while it holds the hero, and in it key 15, the Seat's sink key, dismounts (592-610). So the Seat and a rage do not mix. The rage is the
game saying "you lost control", and it already shows its own message.

- Add `bool HoldsHero { get; }` to `IAutonomousMovementController`. `SwitchMainAgentController` sets it, true
  in the first branch and false in the second. **The swap back starts vanilla's controller only when the Seat is not up:**

  ```csharp
  if (switchToSpecialController)
  {
      baseController.Disable();
      Enable();
      HoldsHero = true;
  }
  else
  {
      if (!SeatPresence.IsSeated)
          baseController.Enable();
      Disable();
      HoldsHero = false;
  }
  ```

  Three routes reach the swap back with the Seat up: a calm warg's death, a rage that starts and ends between two screen ticks (fast time
  runs several mission ticks in one frame, so the Seat never sees it), and a rider who fell during a rage, took the Seat and then lost the
  warg (`OnWargDied` reads a `Rider` set once at `WargBehaviorTree.cs:30`). Each would turn vanilla's controller on under the Seat, and the hero
  would walk on the flight keys. With this form the Seat's own leave turns vanilla's controller on, because `RageHoldsHero` is false by then.
  Outside the Seat the method does what it did before this feature, including a `Disable()` of a controller that the multiplayer preparation
  turned on directly (`EarlyStart`, `OnPreparationEnded += Enable`), so do not test `HoldsHero` in this branch.
  **Do not read `_activated` for the flag.** That field defaults to true, and `WargMissionBehavior.Initialize` only turns it off at the first
  mission tick.
- The file is on the shrink-only baseline in `TAOM.Tests/Infrastructure/EntryPointLineCountBaseline.txt`. Pay for these lines with a deletion,
  never by putting several statements on one line. The events `OnLockedAgentChanged` and `OnPotentialLockedAgentChanged`, their two raises and
  their two delegates have no reader in `Main`, `Client`, `TAOM.Tests` or the docs, and `docs/reference/external-mod-couplings.md` does not name them,
  so delete them and lower the baseline number as the gate's own summary says.
- `SeatEngineFacts` gains `RageHoldsHero`, which `SeatScreenAdapter.ReadFacts` reads through a cached lookup, the
  same shape as `_controller`. It is `_rage?.HoldsHero == true && _mission?.MainAgent?.MountAgent != null`, the same test as the
  controller's own line 193: a rider who fell or dismounted during a rage is not held, and nothing else clears `HoldsHero` then.
- Off: the key does not enter while the rage holds, and one message says why. A battle that begins in the Seat
  checks this too.
- Rising or Seated: when the rage starts, leave at once, with the glide in Battle mode.
- `SetHeroController(true)` does nothing while the rage holds, so that leaving the Seat does not start a second
  controller. The rage's own end turns vanilla's controller back on.
- **Open, and outside the Seat:** `PrepareOnFirstCall.cs:34` tests the warg's own `IsPlayerControlled`, and `Agent.Build` gives a
  non-humanoid an AI controller, so the swap in may never run in single player (not proved in game). Then `HoldsHero` stays false and these
  rage rules are inert and harmless. Do not change that test. The feature doc and the warg doc record the question, and the manifest's
  rage claims are conditional. If the swap in does run, one rare state stays open and accepted: a rider who fell, took the Seat, left it and
  mounted the same warg again has both controllers on, because the leave saw no mount. Nothing in the Seat can see that remount without a per
  frame check, and no in-game evidence shows that it can happen.

### 5.6 Another camera in the mission

Before entry, and only then, walk `Mission.MissionBehaviors` for a `MissionView` whose
`UpdateOverridenCamera` is declared on a type outside the `TaleWorlds` and `SandBox` assemblies, other
than `SeatView`. A match means another camera owns the mission: `ForeignCamera` is true, the Seat stays
out, and one log line names the type. This one rule covers `FordsCommanderCameraView` and any other
mod's view, with no list of names. Cache the answer per mission. The reflection on one view type is
`internal static bool OwnsTheCamera(Type view)`, so a test can give it `FordsCommanderCameraView` (true) and
`SeatView`, `BattleActionBarMissionView`, `AutonomousMovementPlayerController` (false); the walk is private and returns a bool.

### 5.7 Fault latch

Every engine call in the camera path sits in one try block. A throw latches the Seat off for the
mission, re-enables the hero, logs once and shows one message, the same shape as
`FordsCommanderCameraView.UpdateOverridenCamera`.

### 5.8 Order panel from the Seat

Vanilla's panel works with no patch while the hero stands fast. Three gaps need one small patch each,
all in `Patch140_SeatOfSeeing`, all a no-op while the Seat is off.

| Target | Kind | Why |
|---|---|---|
| `OrderFlag.GetCollidedEntity` (private) | Prefix, replaces while seated | It starts the ray at `Agent.Main.GetEyeGlobalPosition()` (OrderFlag:341), so from the Seat it tests a wrong line, and with no main agent it throws. Seated, use `ScreenPointToWorldRay` for both ends: the cursor point when `MissionScreen.MouseVisible`, else the screen centre. Keep the 10000 m extension, the flags and the parent walk |
| `MissionScreen.ScreenPointToWorldRay` | Postfix, while seated | It clamps the ray to the largest boundary radius, or 30 m with no boundaries (3525-3540). Extend `rayEnd` to 1000 m along the same line while seated |
| `MissionFormationTargetSelectionHandler` | See 6.3 | Stage S2 |

Known limit, for the feature doc, with no patch: `OrderController.GetOrderFormCustomWidth` measures from
`Agent.Main.Position` (1621-1624), so a form-width drag from the Seat measures from the hero.

### 5.9 Settings

A provider with a lazy accessor that joins `HotPathSettingsProvidersTests`. MCM group "Seat of Seeing"
in `Main/Features/TaomSettings.cs`. Never flip a default under an unchanged name once it has shipped.

| Setting | Default | Stage |
|---|---|---|
| `SeatEnabled` | true | S1 |
| `BeginBattlesInSeat` | false | S1 |
| `SeatCursorWithPanel` | true | S2 |
| `SeatKeepPanelOpen` | false | S2 |
| `SeatHeroFightsOn` | false until in-game proof exists | S4 |
| `SeatTakeSoldierAfterFall` | true | S5 |
| `SeatRiseWithPanel` | false | S7 |

### 5.10 Log lines and manifest

Every in-game check gets a line that proves pass or fail. Use `IOutcomeLog` with `tools/ingame_checks/0.toml` (Deviations row 8), in the
shape of its siblings, and `OutcomeCounter` on any hot path. S1 checks: entered, left, frame owned for N
frames, glide armed, fault latched (must stay zero).

A refused key press writes a line too, so a refusal never reads as "nobody played":

| When | Line |
|---|---|
| `Enter` cannot read the camera, or the ground is not finite | `Fail(EnterId, key + ":" + reason, "mission=<key> reason=no-camera")`, `reason=no-ground` |
| A rule refuses, on a key press only | `Skip(EnterId, key + ":" + reason, "mission=<key> reason=setting-off")`, `reason=denied`, `reason=rage`, `reason=custom-camera` |
| Every leave | `Pass(LeaveId, key + ":" + reason, "mission=<key> frames=<n> glide=<0\|1> reason=<reason>")`, where the reason is `key`, `setting-off`, `interlude`, `rage` or `finish` |

`IOutcomeLog` writes a line the first time it sees `(id, result, key)`, so the reason goes in the key, and each reason logs once a mission.

Deployment, an interlude, photo mode and a mission that has ended stay silent: those are not refusals the player can act on. The
manifest's `fail` lists also name the feature's own failure lines: the fault line, `[SeatOfSeeing] the Seat could not start`,
`[SeatOfSeeing] hotkey registration failed` and `[SeatOfSeeing] order ray failed`. A fixture and a test class pin every pattern
(`SeatOfSeeingManifestTests` in `tools/tests/test_ingame_verdict.py`), and a C# render test pins the rendered lines against those fixtures.

### 5.11 Definition of done for S1

- Pure tests for `SeatCameraMath`, `SeatMissionPolicy`, `SeatSession` go first and stay green.
- A gate test: no `using TaleWorlds` under `Main/Features/SeatOfSeeing/Domain/`.
- `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` is green, including the composition
  and binding gates.
- `docs/features/seat-of-seeing.md` from `TEMPLATE.md`, a feature-map row, a registry section for
  `Patch140_SeatOfSeeing`, and localized strings through the registration steps of
  `.claude/skills/localize/SKILL.md`, with translation left to the maintainer's backlog.

## 6. Stage S2: the pointer, the panel and the screen

### 6.0 What S1's shapes become

S1 was built for one key and one mission tick job. This stage changes these first, and later stages follow the same shapes:

- **`SeatKeys`**, a small client type, holds the hot keys and every key test beyond "Take the Seat", so `SeatView` stays under its 150 lines
  (`EntryPointLineCountGateTests`) with one call per screen tick. It tests keys 531 and 536 only while the state is Seated, and key 532 only
  while the state is not Off, so a player who never enters still pays one key test a frame. S6's time keys are the only exception (section 10).
  `PressSeatKey`, `PressPending` and `TrialInput` stay on the view; the trial host reads the rest from the session (section 12).
- **Each extra key has one session method:** `PressHide()`, `PressKeep()`, `PressTake()`, `PressTime(SeatTimeKey)`. `OnScreenTick(bool keyPressed)` keeps
  its signature and its tests. The trial calls the same methods.
- **`SeatHotKeys`** gains one property per key.
- **One private cleanup.** This stage writes the session method that 5.3 requires: `Leave` and `Fault` both call it, and `SeatPresence.Set(false)` moves
  into it. In `Fault`, each step of it goes through `Swallow`.
- **`OnMissionTick` tests each owed job separately.** Today it returns early on `!_standFastOwed`, so a stored press handled below that line would never run.
- **`TaomSeatHotKeyCategoryTests` loops over every registered key.** `RegisteredKeys.Single()` throws once a second key exists. The test also gains the
  categories that section 3 names.
- **The entry checks of 5.3 gain one fact:** `MissionScreen.IsRadialMenuActive` is false, read through `SeatEngineFacts`, silent like the other interface
  states. A wheel left open when the Seat comes up would stay on screen and stop the order panel's `TickInput` (MissionGauntletSingleplayerOrderUIHandler:30-32).

### 6.1 Cursor

While seated and `SeatCursorWithPanel` holds, the cursor shows when the order panel is open. "Open" is the plain field `Mission.IsOrderMenuOpen` (Mission:982,
written at MissionOrderVM:720, 777 and 920), read through `ISeatScreenAdapter` each frame only while the state is not Off.

A small `GauntletLayer` carries the cursor: `InputRestrictions.SetMouseVisibility(true)` with the mask left at `Invalid`. `ScreenManager.UpdateMouseVisibility`
shows the cursor when any active layer asks (ScreenManager:763-774), so this needs no patch on the panel's own layer, and vanilla's calls to
`ResetInputRestrictions` on that layer cannot fight it. **The adapter owns the layer**, because 5.1 gives it every write on `MissionScreen`:
`ISeatScreenAdapter.SetCursor(bool)` builds the layer on the first show and after that only toggles `InputRestrictions.SetMouseVisibility(show)` on it, the way vanilla keeps a
cursor layer (`GauntletBannerBuilderScreen` and `BodyGeneratorView`, in `TaleWorlds.MountAndBlade.GauntletUI.dll`, each toggle one layer). It never calls `RemoveLayer` and needs no
try: `ScreenBase` finalizes its layers at teardown (ScreenBase:60-63). The cost is one empty layer that ticks for the rest of the mission once used. The shared cleanup (5.3) calls `SetCursor(false)`.

The session shows the cursor while seated with the panel open and the setting on, unless the look is held. It reads the look key only when the first three hold.

With the cursor shown, the rest follows from the engine:

- `OrderTroopPlacer.GetScreenPoint` uses the cursor when `MouseVisible` (239-246).
- `OrderFlag.GetFlagPosition` goes through `ProjectedMousePositionOnGround`, which takes `MouseVisible`
  (MissionScreen:3543-3547).
- The placer's own 10 pixel rule and 0.3 s dwell apply (362-375). Keep them.

Holding the middle mouse button hides the cursor and looks. While the look is held, call `MBWindowManager.DontChangeCursorPos()` each frame before the cursor
layer's `SetMouseVisibility(false)`, the shape of the banner editor and the face generator (`GauntletBannerBuilderScreen` and `BodyGeneratorView` in the GauntletUI
assembly). Store no pixel. If the game shows that the cursor still jumps on release, record it in Deviations and say what fixed it.

Known limits of this stage, each with a line in the feature doc and a `[[human]]` row:

- **The panel's buttons take no clicks until Marshal's Word stage M7.** The layer's mask stays `Invalid`, so a left click passes through the panel to the placer
  (`OrderTroopPlacer:593-601`) and puts a Move order on the ground behind the button. `SeatCursorWithPanel` still defaults to true, because the pointer is the
  stage's purpose, and a later flip would need a rename under the persisted MCM rule.
- **A gamepad has no pointer path.** The Seat's keyboard keys have no controller binding, so this needs a player who mixes both.

### 6.2 Keep the panel open

With `SeatKeepPanelOpen`, the panel reopens after an order. Do not patch the panel's close paths, and do not subscribe to `MissionPlayerToggledOrderViewEvent`:
the radial menu and the troop transfer gate its raises (MissionGauntletSingleplayerOrderUIHandler:212, GauntletOrderUIHandler:640), the end of a transfer raises a
false close with no panel open (189), and a subscription would keep a dead view on the Game's event table.

While Seated, the session compares `Mission.IsOrderMenuOpen` with last frame's value. A closing edge reopens the panel unless the player made the close. A close
is the player's when any of these was seen on this frame or the previous one, because an Escape close runs after the view ticks (MissionScreen:597-602) and view
order is not fixed:

- Escape released.
- Right mouse released.
- Game key 68 pressed (`ViewOrders`, default BackSpace, GauntletOrderUIHandler:564-566).
- Game key 87 released (`HoldOrder`, the controller's left bumper, GauntletOrderUIHandler:299-304).
- Game key 77 pressed, at the root of a layout that has a Return order (GauntletOrderUIHandler:449-452, 484-492; the legacy layout of `DefaultVisualOrderProvider`). It shares the view-orders signal, and a signal counts only on a closing edge, so a Return press in a sub-menu suppresses nothing.

The reopen runs after the session's own tick on that frame and only if the state is still Seated, so a conversation that closes the panel cannot reopen it over the
interlude. A pure `SeatPanelRule` holds this, with one test per close route. The reopen needs one reflection site for the view model
(`GauntletOrderUIHandler._dataSource`, protected), gated as in 5.4, and a call to the public `MissionOrderVM.OpenToggleOrder(false, false)`. A reopen that fails with
no main agent returns silently at line 716 and raises nothing, so it cannot loop. `MissionOrderVM.OnTroopFormationSelected` closes and opens in one call (905-912), so
the field shows no edge from one frame to the next.

**Read the signals only around an open panel.** Game keys 68, 77 and 87 are registered on the scene layer only by `MissionGauntletSingleplayerOrderUIHandler:95-97`, and
`InputContext:226-233` dereferences a null slot for a key that no category registered. A town, an indoor scene, a village, an arena practice, a duel and an alley build no such handler
(`SandBoxMissionViews`) and still give the Seat its camera, so a read there throws and faults the Seat. The session reads the signals only when the panel is open or was open on the last frame. A
mission with no handler has no panel, and a closing edge always has the panel open on the frame before it, so no edge is lost.

**Transfer closes the panel on purpose.** The Transfer order runs and then closes the panel (GauntletOrderUIHandler:467-479), the transfer sets `Mission.IsTransferMenuOpen` (Mission:984, MissionOrderTroopControllerVM:112),
and `OpenToggleOrder` clears `IsTransferActive` (MissionOrderVM:723), so a reopen would cancel the transfer. A closing edge with `Mission.IsTransferMenuOpen` true never reopens. The session reads that
property on the edge only.

**The reopen comes before the frame's cursor step.** The session decides the reopen first, then reads `IsPanelOpen()` for the cursor step and for the next frame's edge test, so an order never
hides the cursor for a frame. The state test still holds, because the panel work runs after the interlude tests.

**A refused reopen is not a reopen.** `OpenToggleOrder` can refuse without a word (MissionOrderVM:716-719, reached when the last troop or the hero is gone at MissionOrderVM:821-824 and 920). After the call
the session tests the field: open writes the `Pass`, closed writes a `Skip` with `reason=refused`. A missing panel handler stays a `Fail` with `reason=site`.

**Time.** `BannerlordConfig.SlowDownOnOrder` defaults to true, and an open panel then holds a 0.25 time request (MissionGauntletSingleplayerOrderUIHandler:215-218). So a
player with this setting on runs the whole stay at quarter speed apart from one frame per order. Accept that. The setting's hint text and the feature doc's Known limits
say so.

### 6.3 Pointing at an enemy company

Vanilla's panel charges the focused enemy formation on a left release (GauntletOrderUIHandler:333-345),
and `MissionFormationTargetSelectionHandler` picks the focus from the screen centre. With a cursor shown
this charges something the player did not point at. **This is a real bug to prevent, so S2 does not ship
without it.**

The handler keeps its reference point in the field `_centerOfScreen`, writes it at the top of
`OnPreDisplayMissionTick`, and reads it only in the private `TryGetFormationDistanceToCenter`. It compares
that point with the output of `MBWindowManager.WorldToScreenInsideUsableArea`. So one Harmony prefix on
`TryGetFormationDistanceToCenter`, with the field injected by reference, is the whole change. The prefix reads one more static beside
`SeatPresence`, "the cursor shows", which the shared cleanup clears (5.3).

While the cursor shows, set the field to the pointer in the handler's own coordinates. `WorldToScreenInsideUsableArea` subtracts the usable area's margins
(MBWindowManager:17-18), and `Input.MousePositionRanged` does not hold them, so subtract them here: `x = Ranged.x * W - (W - U.X * W) / 2`, and likewise `y`, where `W` and
`H` are `Screen.RealScreenResolutionWidth` and `Height`, and `U` is `ScreenManager.UsableArea`. This is a pure function with a test at `U` = 1 and at `U` = 0.9. Vanilla's own
centre is off by that margin, and the Seat does not copy it. UI scale (`ScreenManager.Scale`) is not in that formula. The 70 scaled pixel limit and the 8 to 1000 m range
stay as the engine has them. Whether a player option can move `UsableArea` below 1 on a PC is not proved: the in-game row runs at a value below 1 if one can be reached, and
the feature doc says which.

### 6.4 The screen

All in `Patch140_SeatOfSeeing`, each a no-op while the Seat is off. The first field trial's pictures show the need for the first row: the attack direction arrow stands at the
bottom of the sky view.

| Target | Kind | Why |
|---|---|---|
| `MissionGauntletCrosshair.GetShouldArrowsBeVisible` (protected virtual, 209-229) | Postfix false while seated | Crosshair, attack direction arrows and reload all follow it. `GetShouldCrosshairBeVisible` begins with a virtual call to it, and no override exists, so one patch is enough. A test pins that call, so a later engine change shows |
| `CrosshairVM.ShowHitMarker` | Prefix, skip while seated | `OnCombatLogGenerated` calls it with no visibility test (MissionGauntletCrosshair:236-245), and in the prefab the marker is a sibling of the crosshair, not its child (Crosshair.xml:12-41, 73). An arrow loosed before entry can land after it, and a hero who fights on would flash it at the centre of the sky view |
| `MissionGauntletMainAgentEquipDropView.IsMainAgentAvailable` (private) | Postfix false while seated | It reads key 34 on the scene layer and opens the wheel. A false answer also closes an open wheel (116) |
| `MissionGauntletMainAgentCheerControllerView.IsMainAgentAvailable` (private) | Postfix false while seated | Key 31 (`O`) opens the cheer wheel, and the hero cheers on release |
| `MissionGauntletMainAgentControlModeView.IsMainAgentAvailable` (private) | Postfix false while seated | The hot key `ControlModeToggle`, the controller's left pad down (CombatHotKeyCategory:133), opens the walk mode control (MissionGauntletMainAgentControlModeView:247, 257). Game key 30 (CapsLock) is read by `MissionMainAgentController:811`, which the shut controller already stops, so this patch serves a player who mixes a gamepad with the Seat |
| `MissionGauntletMainAgentEquipmentControllerView.DropWeaponTick(float)`, `EquipWeaponTick(float)` (private) | Prefix, skip while seated, each classified in `CoopVetoClassificationTests` | Keys 13 and 22 act on the main agent, so the drop key would drop a weapon from the air. Do not patch this view's `IsMainAgentAvailable`: its body is 17 IL bytes (`return Agent.Main?.IsActive() ?? false;`), a JIT inlining candidate that a patch might never reach (`docs/features/hidden-hosts.md:609` chose the longer method for the same reason), and its false branch (130) leaves a radial registered |

After this stage two of the readers in 5.5 stay live, the career key and game key 27. Update the 5.5 paragraph's claim in the feature doc and turn the matching line of
`tools/ingame_checks/0.toml` into a `[[human]]` row that presses each closed key from the Seat (13, 22, 31, 34, and left pad down on a gamepad) and expects nothing, because only a press proves that a
private patch is reached.

Key 536 hides the whole interface, not only the bars. `MBDebug.DisableAllUI` backs the engine's own `toggle_ui` command (MBDebug:69), and `TwoDimensionEnginePlatform` (155, 199)
stops every Gauntlet draw under it, so the code answers the old question of whether it still works; the trial's two pictures prove the effect. The key is read only while the
state is Seated, through one adapter member, because no other TAOM code writes this flag. Photo mode is the engine's second writer of the flag (MissionGauntletPhotoMode:118, 139, 183, 192, 200, 317), so no Seat key acts in photo mode (5.3). The session restores it only if its own hide is in force, to the value it saved, so a
player who ran `toggle_ui` before entering keeps that choice. It restores at once when the order panel opens (`Mission.IsOrderMenuOpen`), a dialog shows
(`GetDisplayDialog`, read through the adapter) or a layer other than the scene layer takes focus (`ScreenManager.FocusedLayer`), each read once a frame only while hidden, because an
invisible panel still slows time, and a left click still gives an order. The shared cleanup restores it too, so a leave, a fault and `Finish` all reach it.

### 6.5 Definition of done for S2

- Pure tests first: `SeatPanelRule`, the pointer function, the hide rule.
- Check lines, each through `IOutcomeLog`: `taom.seat-of-seeing.cursor` (first show), `.focus` (the prefix first set the field), `.panel-reopened`, `.hide` (hidden, and restored with the
  reason). A `Fail` on the reflection site, gated as in 5.4.
- `[[human]]` rows in `0.toml`: the middle mouse look and the cursor's place after release, keep open with each close route, the closed keys, a click on a panel button, each new default
  key leaving chat shut, key 536 and the restore.
- `SeatOfSeeingManifestTests` and the C# render test pin the new lines.
- A registry section line per target in `docs/reference/harmony-patch-registry.md`, the coop classification of each skip prefix, one row in `reflection-sites.md` Category B with its
  `[DataRow]` in `ReflectionSiteBindingTests`, and `pwsh tools/snapshot_api_surface.ps1` run with the result committed.
- `ISeatSettingsProvider` gains `CursorWithPanel` and `KeepPanelOpen`, each with an MCM row, `Presentation` in `CoopSettingsRelevance`, and the numbers that count them.
- New text through the registration steps of `.claude/skills/localize/SKILL.md`, with translation left to the maintainer's backlog.
- Section 12's S2 steps, and the feature doc and feature map updated.

## 7. Stage S3: keep to a soldier or a company

Key 531. With no target, a ray from the pointer picks the soldier: `Mission.RayCastForClosestAgent` (1695-1698), which can return a mount, so take its `RiderAgent`. A riderless mount counts
as a miss. Compare the distance with `Scene.RayCastForClosestEntityOrTerrain`, with the order placer's thickness and flags (0.3, `BodyFlags.CommonFocusRayCastExcludeFlags | BodyFlags.BodyOwnerFlora`,
OrderTroopPlacer:205), so that a soldier behind a hill does not count and a soldier in a wood does. The soldier counts when the terrain ray returns false or the agent distance is not above the
terrain distance: the out distance starts as NaN (Scene:1320), and a plain comparison against it is false.

The view stores the key press in `OnMissionScreenTick`, and the session casts these rays in the next `OnMissionTick`, through the adapter, because nothing proves a native ray is safe while the
agent jobs run. With the order panel open and one company selected, the key keeps to that company instead. A press that finds nobody shows one message and writes one `Skip` line. Pressing the key
again lets go, and so does any flight input: the movement keys, rise and sink. The mouse and the wheel do not let go.

The Seat keeps its own camera while it keeps to a target. **Do not hand the frame to vanilla's follow.** `GetSpectatingData` follows the living main agent unless a mission camera lock mode applies, so
setting `LastFollowedAgent` does not move the view, and handing the frame back would also run `UpdateDragData` and the follow camera's writes.

Pure rule: the camera orbits the target point. The mouse turns the orbit, with the pitch held between 0.05 and `MaxPitch`, because `MinPitch` would put the camera below a target at 16 m and look at the
sky. The wheel changes the distance (4 m to 90 m, starting at 16 m), and the target point eases with `1 - exp(-5 dt)`. A company's target is `Formation.CachedMedianPosition.GetGroundVec3()`, the read
`MissionFormationTargetSelectionHandler` uses (176, 181), which needs no probe of the Seat's own: it validates the stored Z natively when needed and can answer NaN, which `SeatOrbit.Step` handles. It stays under a gatehouse roof. The orbit gives a pose like any other: the boundary clamp, `CapHeight` and
`AboveGround` apply in the same order as 5.4, with the orbit's floor at the raw ground plus `MinHeight`, because a free flight's height starts at `MinHeight` and a lower orbit would jump on the let-go. A non-finite target position keeps the last one. The stored flight follows the pose each frame, so letting go continues from where the view is, with no jump.

**Who holds the target.** `SeatScreenAdapter` holds the kept `Agent` or `Formation`, the adapter's second cached reference on the shape of `_controller`, because `Agent` is sealed and the session runs over
fakes (ADR-007). `ISeatScreenAdapter` exposes one read that returns the position and a gone flag. The test is occupant first (`AgentSlotIdentity.IsCurrentOccupant`), then `IsActive()`, because the first says
nothing about being alive (AgentSlotIdentity:19-26). Never key on `Agent.Index`. A gone soldier lets go with one message. A company with `Formation.CountOfUnits` at 0 lets go with the same message, because
an empty `Formation` stays in `Team.FormationsIncludingEmpty` (Team:280-287), and no read of its position says that it emptied. The shared cleanup (5.3) clears the target.

**Definition of done:** pure orbit tests first. Check lines `taom.seat-of-seeing.keep` (target kind) and `.let-go` (reason: key, flight, gone, leave), and a `Skip` for a miss. `[[human]]` rows for the orbit, the
wheel and a soldier behind a hill. The manifest test class and render test. Section 12's S3 steps. New text localized. Feature doc updated.

## 8. Stage S4: the hero fights on

`SeatHeroFightsOn` with `SeatPermissions.FightOn`, read fresh at each entry (section 4). This is the state with the most engine surprises, so
every guard below is required before the setting can default to true, and the setting stays false until an
in-game run proves the state.

### 8.1 Hand-off

`ISeatHeroAdapter.BeginFighting()` and `EndFighting()` read `Mission.Current.MainAgent` at call time, as `StandFast` does. Nothing holds an agent, and the session stays free of engine types.

**The tick compares, and it runs first.** `wanted` is true when the state is not Off, no fault has latched and fight-on was chosen at this entry. `actual` is a session bool, "the hero was handed to AI", set only
when `BeginFighting` succeeded. `SeatSession.OnMissionTick` acts only when the two differ, before its early returns on Off and on a fault, so a leave or a fault still finishes and a paused mission still reaches it
(dt 0). A single owed flag cannot say which of the two calls is owed, and an entry and a leave with no tick between them would reverse a hero who was never handed over.

**Thread.** These two calls write the agent's controller and fields, so they run only from that tick. `Finish` writes no agent, because no mission tick follows it. The one engine read of the main agent's controller
outside the Seat's targets is `BattleEndLogic:300`, which falls through for an AI hero who is not running; the feature doc records this.

**The gate.** `SeatPresence.HeroFightsOn` is a second volatile static, and every patch in 8.2 and every other "while the hero fights on" reads it, never `IsSeated`. A successful `BeginFighting` sets it. `EndFighting`, `Finish`, `Fault`
and the fall rule below clear it. The shared cleanup that `Leave` calls does not clear it, because the hero stays on AI for up to one more tick.

**Begin.** Refuse when `Mission.MissionEnded` or `Mission.IsMissionEnding` holds, or there is no active main agent. A refusal clears the choice for this entry, so that the tick does not ask again.

1. When `IsUsingGameObject` holds, `agent.StopUsingGameObject`, so that the hero is not added to an arrangement twice.
2. Set `agent.Controller = AgentControllerType.AI` (Agent:1239-1289). **Read and write no morale around the flip.** A Player hero has no `CommonAIComponent` (AgentCommonAILogic:10, 25-27), so `GetMorale` returns -1
   (AgentComponentExtensions:9), a write of that value clamps to 0 (CommonAIComponent:47), and 0 panics the hero in that frame's parallel tick (84-88). The flip adds a new component holding morale 50 (21), with
   `_initialMorale` and `_recoveryMorale` at 0, because `AddComponent` does not call `Initialize` (Agent:4409-4420). Never call `CommonAIComponent.Initialize`, because it re-rolls morale (60-76).
   **Never set `Agent.Controller` inside a `Mission.OnMainAgentChanged` handler.** On a change to Player the setter raises that event from its own body (1252-1256), so a flip to AI there re-enters the setter. The outer
   setter then adds `CanRide` back, undoing step 3, and it raises `OnAgentControllerChanged` with the controller already AI, so `AgentHumanAILogic` adds a second `HumanAIComponent` (`AddComponent` only appends). Defer
   the flip to the next main-thread tick.
3. The setter adds `AgentFlag.CanRide` on the way to Player and never removes it on the way to AI, so a foot hero would hunt for horses. Remove the flag unless the hero is mounted or has a `Horse` slot item.
4. Alarmed watch state only when `Mission.Mode` is Battle (leave it alone in any other mode), AI unpaused, no scripted movement, riding order set, and the formation told that the unit joined: confirm each call in
   `Agent` and `Formation`. Read what vanilla's own hand-over does (`ResetEnemyCaches` and `SyncBehaviorParamsIfNecessary`, DeploymentMissionController:61-67), and say in the feature doc which of them this needs and why.
5. **Panic.** `TaomBattleMoraleModel.CanPanicDueToMorale` (31) and `TaomCustomBattleMoraleModel.CanPanicDueToMorale` (27) gain one more refusal beside `RaceAbilityHooks.HoldsNerve`:
   `!(SeatPresence.HeroFightsOn && agent.IsMainAgent)`. These run on worker threads, so the test reads only the volatile static and the agent. A refused panic sets morale to 0.01 and carries on (CommonAIComponent:84-93).
6. **A retreat order.** Morale is one route to a rout, not the only one: `MovementOrder` (454-459, 764-768) retreats every AI unit of a formation given the Retreat order, the hero on AI included, and a rout wounds a hero
   (MapEventParty:316-328). While the hero is handed, the mission tick checks once a real second: when `agent.IsRetreating()` (Agent:3333) holds, call `agent.StopRetreatingMoraleComponent()` (AgentComponentExtensions:40).

**End.** Act only when the main agent is active and `Controller` is AI. Clear `actual` before the calls, so a throw from `EndFighting` does not re-arm itself on every tick. The order matters, because the setter removes the AI
components as it runs (Agent:1268-1271, AgentHumanAILogic:25-27), and `MoveToClear` and `IsDefending` then throw (AgentComponentExtensions:67, 87):

1. Clear the two delegates below.
2. Under the AI controller, call `agent.StopUsingGameObject` with auto-attach off (read Agent:4025-4045 for the flag). `StopUsingGameObjectAux` already stops a use, disables a walk or a defend and calls
   `DisableScriptedMovement` (3982-4030). If the AI hero is manning an engine and this is skipped, the hero becomes a Player pilot and `RangedSiegeWeaponView:81-86` sets `CustomCamera`.
3. Set the controller to Player. The setter clears the speed limits and sets run for a humanoid.
4. Clear the AI state flags and the target frame.

`AgentHumanAILogic` adds a `HumanAIComponent` on a change to AI and removes it on a change back (16-30), and the component's constructor subscribes to two agent delegates that its removal never clears
(`OnAgentWieldedItemChange` and `OnAgentMountedStateChanged`, HumanAIComponent:136-139, 386-393). Toggling the Seat would pile up handlers from dead components. They have no other subscriber, so set both to null before
the change back, through the smallest seam, and pin the behaviour with a binding test.

**A hero in a held line.** Marshal's Word's hold the line writes `AgentFlag.CanAttack` on soldiers. Its Qualifies test excludes the main agent, and its driver restores any soldier it touched who is no longer AI controlled (Marshal contract 9.2). That covers this stage and S5's take.

**The hero falls while on AI.** While `actual` holds, the tick also reads whether the main agent is active with `Controller` AI. If not, clear `actual`, the gate and the choice for this entry, and owe nothing. A setter call on a
removed agent would make the corpse `Mission.MainAgent` (Agent:1255). After S5's take, this rule also keeps the patches from serving a Player soldier.

### 8.2 Guards an AI main agent needs

Each is one patch in `Patch140_SeatOfSeeing`, a no-op unless its gate holds.

| Target | Problem | Answer |
|---|---|---|
| `MissionOrderVM.CheckCanBeOpened` (private, 740) | Refuses a main agent that is not player controlled (750-757) | Skip only that test while the hero fights on. Tests 742 (no main agent), 758 (no troops) and 766-769 (mission ending) still run. A postfix that flips the result would drop them, and would show vanilla's refusal message, so use a prefix or a narrow transpiler |
| `MissionBoundaryCrossingHandler.HandleAgentStateChange` (private, 222) | A hero left out of bounds retreats the player from the battle, and skipping the method would only freeze a countdown that keeps ticking (199-204, 232-234) | Prefix: when `SeatPresence.IsSeated` holds and the agent is the main agent, set `ref bool isAgentOutside` to false. Line 226 then clears the timer and the warning, and a hero still outside after the leave gets a fresh 10 s. This reads `IsSeated` on purpose, because a stand-fast hero outside the line has the same problem |
| `RangedSiegeWeaponView.HandleUserInput` (79-99, 120-124) | An AI hero who mans an engine makes the view set `CustomCamera`, and that beats the Seat | Skip while the hero fights on |
| `LadderQueueManager` (`_userAgents`, 212-238, 434) | A true engine bug with a player unit on a ladder, on the parallel tick. Read `docs/features/creature-siege-role.md` first | Decide after reading. The answer must be parallel-safe, and record it. `OnTickParallelAux` holds the pair lock when a neighbour exists (396-407), and vanilla writes the same per-manager list and flag on that path (235-237). Keep vanilla's speed limit reset at 235 |

Report, do not patch: a banner bearer inside a formation always gives that formation its effect, and only a bearer outside it needs `IsPlayerControlled` (`CustomBattleBannerBearersModel.cs:33-44` and its sandbox twin), so a hero on
AI loses the effect on other formations only. `OrderTroopItemVM.FormationOnOnUnitCountChanged` subtracts one for the player (383-387), so the panel count is one low. When the last soldier dies while the hero fights on, line 758
no longer refuses (Team:148 makes the AI hero a bot), so the panel opens on a company that shows 0 men and commands only the hero. Nothing crashes, and nobody should read 758 as a guarantee.

Before this stage, grep TAOM for readers of `IsPlayerControlled`, `IsAIControlled`, `IsMine`, `Agent.Main` and `MainAgent` (run the grep; the list is longer than the bulleted files), and read each one that assumes the main agent is
the player-driven hero. The result goes in the feature doc, one line per file. At the least, read these, because each one answers differently once the hero is on AI:

- `Client/Features/AdvancedCombat/AutonomousMovementPlayerController.cs` and `Main/Features/Warg/WargRiderHandManager.cs` (mounted input and look writes).
- `Main/Features/LostFooting/LostFootingRules.cs:39`, which exempts `IsPlayerControlled`, so a hero on AI loses the exemption.
- `Main/Features/Enlistment/Hooks/EnlistmentBattleRoleMissionBehavior.cs` and `EnlistmentBattleFormationMissionBehavior.cs` (`ReclaimAfterDeployment`).
- `Main/Features/AdvancedCombat/BaseBehaviorTree/IsPlayerControlledDecorator.cs`, `Main/Adapters/GaitVarietyAgent.cs`.
- `Main/Features/DreadAura/Hooks/DreadAgentGate.cs`, `Main/Features/CombatMechanics/Hooks/CombatMechanicsHooks.cs`,
  `Main/Features/RaceAbilities/Hooks/RaceAbilityActivator.cs`, `Main/Features/CreatureSiegeRole/CreatureSiegeRoleService.cs`.
- `Main/Features/CultureDoctrine/Hooks/Tactics/TaomTacticBase.cs`, `Main/Features/SmartCavalryAI/CavalryChargeService.cs`,
  `Main/Features/StagedSiege/Hooks/TacticStagedObjective.cs`, `Main/Adapters/StagedSiegeMissionAdapter.cs`.

A banner bearer model gate is `!IsMainAgent && !IsHero && IsAIControlled` (`TaomBattleBannerBearersModel`), so the hero stays out of that role on AI. The engine also calls `MissionBehavior.OnAgentControllerSetToPlayer` on the way
back (Agent:1276): no TAOM or engine override exists today, and the grep says so if that changes.

**Open, for the first run:** where an AI hero stands. `Agent.IsPlayerUnit` stays true through `IsPlayerTroop` (Agent:1221-1230), and `Formation.TryRelocatePlayerUnit` (2902-2912) tests `IsPlayerTroopInFormation`, so the arrangement may keep
moving the hero to the back rank. `fight_on` reports the distance moved.

**Definition of done:** pure tests of the comparison first, including an entry and a leave with no tick between them and a hero who falls while handed. Check lines `taom.seat-of-seeing.fight-on` (begin, end, refused, fell) and a `Fail` on a throw
from either call. `[[human]]` rows for a hero on a ladder and on an engine. The grep's result in the feature doc. The registry, coop classification, snapshot and manifest work as in 6.5. Section 12's S4 steps.

## 9. Stage S5: take a soldier after the fall

Build on `Mission.TakeControlOfAgent` (6407-6414), which sets the controller and cancels fast forward. Call it on the main thread.

1. **Never call `Mission.SetPlayerCanTakeControlOfAnotherAgentWhenDead` (6402).** That flag arms vanilla's held Kick key on `LastFollowedAgent` (MissionScreen:2890-2893), so from the Seat it would take a soldier the player did not point at.
   `TakeControlOfAgent` does not read the flag. Four openers set the flag themselves (`BannerlordMissions.cs:155` and `:237`, `SandBoxMissions.cs:713` and `:1461`: custom and campaign field battles and sieges with deployment), and so does
   TAOM's `LongDefeatMissionLauncher.cs:89`. So a postfix on `Mission.CanTakeControlOfAgent` (6393) in `Patch140_SeatOfSeeing` answers false while seated, and key 532 is the only way out of the sky. Outside the Seat those missions keep vanilla's key.
   Vanilla's prompt on the Kick key reads the flag for its relevance and the postfix for its enabled state, so it may show disabled: the feature doc says so. On the fall, while seated, one message names key 532.
2. When `SeatPermissions.TakeSoldier` (section 4) and `SeatTakeSoldierAfterFall` hold, key 532 in the Seat takes the soldier at the pointer. The panel cannot be open after a fall (MissionOrderVM:742-748, 915-921), so the pointer is the screen
   centre. If the view keeps to a company (section 7), the key takes the nearest eligible soldier of that company instead. No soldier refuses with one message and one `Skip` line. No mark shows the centre in this version: that is a known limit with a
   `[[human]]` row. Cast the pointer rays as in section 7, and make the take in that same mission tick. TAOM's own eligibility rule: human, active, the player's team, not using an object, not retreating and not fading out, a quarter of health or more.
   A hero only in a campaign, when `agent.Origin?.BattleCombatant == PartyBase.MainParty`; with no `Campaign.Current`, refuse any hero (custom battle has no party, and vanilla refuses every hero, Mission:6395). Refuse a soldier the player could not
   play: a creature race that `docs/features/troll-race.md` and `Main/Features/PlayerSwitcher/` treat as unplayable, and crew on a moving platform (`Main/Features/Elephant/TaomHowdahStandingPoint.cs`, `docs/features/mumakil.md`), whose deck rules assume
   AI. The main agent must be null. A living hero does not leave the body, so two player units never exist in one formation (`SwitchUnitLocationsWithBackMostUnit` loops on that state through `OnShapeChanged`, ColumnFormation:808, Formation:2902-2912, 2967-2975).
3. **Hand the command over in that same tick, and only after a take the Seat itself made with key 532.** For each formation of the player team whose `PlayerOwner` is not null and not active, set `PlayerOwner` to the new agent. Then set
   `Mission.PlayerTeam.PlayerOrderController.Owner` to the new agent (the controller vanilla sets, AssignPlayerRoleInTeamMissionController:54, not `MasterOrderController`). The setter also takes the formation off AI (`SetControlledByAI(value == null)`,
   Formation:283-294), undoing `Team.DelegateCommandToAI`. A null value hands the formation to the AI, so test the new agent for null first. Do not save and restore any AI flag around it: the plain setter with a real owner is the whole change. `PlayerOwner`
   survives the fall (Team:479-485), so this needs no captured list and handles a second fall. Do not run this from `Mission.OnMainAgentChanged`: that event raises on every write of `MainAgent` (the first spawn, the end of deployment, `EndFighting` and
   vanilla's own Kick take), and a player who never uses the Seat must see vanilla (section 1). The hand-over to the AI cannot be stopped from `OnMainAgentChanged(null)`: `Mission` reads "was this the main agent" at 3017, before it clears `MainAgent` at
   3021, and it still delegates at 3039-3042. So this stage lets the hand-over run and undoes it on the take.
4. The Seat touches no scoreboard state. Vanilla's own handler resets the score screen on a take (`MissionGauntletBattleScore:99-101`, `ScoreboardBaseVM:744`). Toggling `IsOver` would mark a live battle as over (SPScoreboardVM:515). A `[[human]]` row checks
   that the quit text reads Retreat after a take.
5. Seated, the view's `OnMainAgentChanged` handler already owes stand fast (5.5), so the new agent stands. Not seated, the player simply plays the soldier.

Campaign results need nothing, with one exception. Wounds, deaths and experience flow through `Agent.Origin`, not the controller (BattleAgentLogic:132-181). The exception is the Tactics share: `Team.Leader` returns `Agent.Main` first (Team:157-178), and
`BattleAgentLogic` (229, 233) grants the share to the leader's party. So after a take, a plain soldier leaves no commander with a party, and a hero of the player's party becomes the commander. State this in the feature doc as a known effect. A hero's
formation keeps no captain and the engine has no successor; leave that alone. The field trial proves a custom battle only, and custom battle has its own agent logic and origin type (`CustomBattleAgentOrigin`), so the manifest also owes one check in a campaign field battle.

TAOM's own readers treat the main agent as the hero, and a stand-in breaks that for some of them. Read each and state the result in the feature doc: `Main/Features/ArmourAcquisition/Hooks/HeroKillCounterMissionLogic.cs:49-52` (caches the first main agent, and its
comment already expects a later takeover), `Main/Features/CareerSystem/Abilities/CareerHeroIdentityGate.cs` and `AbilityActivationController.cs:46-51` (abilities need the career hero, #377), `Main/Features/CareerSystem/CareerPerkMissionBehavior.cs`,
`Main/Adapters/FellFoesAdapter.cs:37-38` (reports the player alive from the main agent), `Main/Features/Enlistment/Hooks/EnlistmentMeritMissionBehavior.cs:98`, `Client/Features/CareerSystem/UI/CareerEnergyBarPrefab.cs` and `MissionAgentStatusCareerMixin.cs`.

`Mission.InitialPlayerAgent` is null after the first fall (Mission:3023-3026), and its readers run at start, so this is harmless. Record any TAOM reader that disagrees.

**Definition of done:** pure tests of the eligibility rule first. Check lines `taom.seat-of-seeing.take` (the new agent's kind, and the number of formations handed over) and a `Skip` for each refusal reason. The registry, coop classification, snapshot and manifest
work as in 6.5. Section 12's S5 steps.

## 10. Stage S6: mission time

Reuse `Main/Adapters/FordsMissionSpeed.cs` and `Main/Features/FordsOfWar/Domain/FordsGameSpeed.cs`, which need no transpiler. The session calls new `ISeatScreenAdapter` members (read fast forward, set the level, stay time), and the adapter calls `FordsMissionSpeed` with a
null logger, so a Seat battle writes no `[FordsOfWar][Speed]` lines (`tools/tests/test_fords_match_review.py` reads that prefix). The ladder is a pure type in `Domain/`, `SeatTimeLadder`. At levels 2 and 4 each frame takes one fixed step capped at 0.1 s
(FordsGameSpeed:37), so the rate is `min(level, 0.1 / real frame time)`: level 4 holds to about 40 FPS and falls below that. Level 10 is the engine's fast forward. Promise no other speed: a step above 0.1 s is untested.

- **Levels.** Keys 533 and 534 step through 0.25, 0.5, 1, 2, 4, 10. Above 1 uses the Fords speed path. Below 1 uses `Mission.AddTimeSpeedRequest` under one constant TAOM id, clear of the engine's 813, 624, 864 and 1121, so that other slow-downs still win
  (Mission:2070-2098): the engine takes the minimum of all requests starting from 1, so a request can only slow. **The only legal write:** if `GetRequestedTimeSpeed(id, out _)` holds, `RemoveTimeSpeedRequest(id)`; then add when the new level is below 1.
  Removing an absent id throws (Mission:2115-2125), and adding does not replace (2100-2103). The clear uses the same test. Each mode cancels the other before it starts.
- **The per-frame rule.** Other writers of fast forward exist: while the main character is dead, vanilla's score view toggles it on `F` (`MissionGauntletBattleScore`:176-180, through `SPScoreboardVM`:252-268 or `CustomBattleScoreboardVM`:86-92), and `Mission.TakeControlOfAgent`
  clears it (6409-6412). `FordsMissionSpeed.Apply` asserts the flag every frame (32-34), so a blind call fights them. A mission where no time key was pressed runs none of this. After the first key:
  - At levels 2 and 4, test `Mission.IsFastForward` first. True means another writer won: set the stored level to 10 and write nothing. False: call `Apply`.
  - At level 10, set the flag once on the key step. Each frame read it. False means someone else cancelled: set the stored level to 1 and call `Stop` once.
  - At level 1 or below, write nothing per frame, and a true flag sets the stored level to 10.
  - Each key step reads the flag before it steps, so the ladder starts from what is true.
- **Level 10 needs the Seat up or no main agent.** With a living main agent outside the Seat, the ladder stops at 4, and a leave at level 10 steps down to 4. Fast forward repeats the mission tick several times a frame (MissionState:171-198), and `ControlTick` reads pressed keys in
  each (MissionMainAgentController:268, 811), so one press of a key that sets a flag could act more than once. Vanilla offers it only with the main character dead.
- **Key 535** flips `MissionState.Current.Paused`, after it cancels fast time. The view still flies (Mission:3746-3749 passes real time), and `OnMissionScreenTick` still runs (MissionScreen:595-600). The trial's first run with this key owes the native and sound behaviour under this flag:
  vanilla reaches it only by a cheat key and the error report.
- **Gates.** `SeatPermissions.Time`, read fresh when a time key is pressed (section 4), so a network session never runs it (scene time speed is off in a session, MissionState:159). `SeatEnabled` off also stops keys 533 to 535 and cancels any level or pause, read only when a time key is
  pressed, and the setting's hint says so. A time key acts only in `Mission.Mode` Battle and stays silent otherwise, the way 5.10 treats an interlude. An existing level stays until a key or teardown changes it. A faulted session ignores these keys.
- **Taking a soldier** cancels fast time. `Mission.TakeControlOfAgent` already clears the engine's flag, and the per-frame rule follows it. The take also stops the fixed step.
- **Teardown.** `Finish` and `Fault` both cancel mission time directly: pause off, fixed step off, fast forward off, the request cleared, each through `Swallow`. `Finish` does this outside its `if (State != Off)` test and after its try, so a throw in `Leave` cannot skip it. Fast forward
  must be cleared at teardown: its setter writes a process-global byte through native `IMBMission_OnFastForwardStateChanged` (Mission:1268-1279), and the state-finalize path has no `EndMission`. The time state is per mission and is never saved. A RED test pauses, forces a fault and asserts unpaused.
- **Text and lines.** Each step and each pause flip shows one message through `ISeatScreenAdapter.Say` (new `SeatMessage` values `TimeLevel`, `TimeStayed`, `TimeResumed`, with localized rows), because a battle that stops with nothing on screen reads as a hang. Each writes one `IOutcomeLog` line,
  `taom.seat-of-seeing.time` with the level, with its fixture and manifest entry in the shape of 5.10.
- **Without the Seat.** Mission time works from the hero's eyes when the mission allows it. It stays off until a key is pressed.

The time keys are the one thing `SeatKeys` tests in every mission, each with the static test first (section 3), so they allocate nothing.

## 11. Stage S7: rise to give orders

`SeatRiseWithPanel`, default false, because section 1 promises vanilla behaviour to a player who never asks for this. With the hero's view in third person and the order panel open, the view lifts so that the player can see over the ranks, and it sinks when the panel closes. The Seat itself does not use this.

- **Seam.** `Mission.SetCustomCameraTargetLocalOffset(Vec3)` (Mission:2025). `UpdateCamera` adds it to the view's target in third person only (MissionScreen:2226-2228), and its box cast still keeps the view out of walls at the lifted height (2415-2483). The cast does not test the segment from the
  hero up to the lifted point (2378-2382, 2447), so under a roof the view rises through it: a known limit, in the feature doc. This needs no patch. No engine or TAOM writer of this property exists on v1.5.6 (searched the worktree, the view decompiles and the dump), and its base value is zero, so sink to
  `Vec3.Zero` and save nothing. Another mod's writer was not searched.
- **The write.** Write on every frame the lift changes, including exactly `Vec3.Zero` on the frame it reaches zero, and write nothing while the lift was and stays zero. `IsThereAnyCustomCameraAddition` tests exact zero (Vec3:199-207, MissionMainAgentController:1190, 1300), so a small leftover would block
  the target lock for the rest of the mission. A test pins that the last write is exactly zero. A lift drops the target lock (756), and no new lock starts until the view has sunk: the doc and the setting's hint say so.
- **Reads.** One small adapter member, from `OnMissionScreenTick`, reads `Mission.IsOrderMenuOpen`, `Mission.CameraIsFirstPerson` and `Mission.Mode`. It does not go through `ReadFacts`, which does several reads, and it does not sit behind the session's fault return. Read the setting and
  `SeatPermissions.Orders` only on the panel's open edge. Lift only in Mode Battle.
- **Pure rule in `SeatCameraMath`.** `PanelProgress(progress, panelOpen, dt)` moves a raw value towards 1 in 0.5 s and towards 0 in 0.4 s, and the session stores that value. `PanelLiftAmount(progress)` returns the smoothstep used for the write. Each new direction starts from the current value, so a quick
  close and open does not jump.

| Constant | Value |
|---|---|
| `PanelLiftHeight` | 7 m, when `Mission.IsFieldBattle` holds |
| `PanelLiftHeightInSiege` | 3 m, when `Mission.IsSiegeBattle` or `Mission.IsSallyOutBattle` holds, because streets and gatehouses leave no room |

Any other mission gets no lift (a hideout's caves have no room). Off in a mission with `SeatPermissions.Orders` false, read fresh when the panel opens (section 4). Zero it on a fault and when the Seat is taken. `Finish` writes nothing, because the offset lives on the `Mission` instance and `MissionState` builds a new
one per mission (248).

**Definition of done:** pure tests first. Check line `taom.seat-of-seeing.rise` (height, first lift a mission). A `[[human]]` row for a lift under a gatehouse. Section 12's S7 steps.

## 12. The field trial: proof in a real battle

Unit tests cannot show that a camera flies or that archers hold a draw. The trial plays one real custom battle with nobody at the
keyboard, presses the feature's own controls in order, and writes one log line per fact. It follows the sibling
`tools/run_long_defeat.py` and `docs/reference/unattended-game-runs.md`: same permission, same gate, same launch recipe. Each
later stage, in this contract and in Marshal's Word, adds its steps and its evidence lines.

**Arming.** Environment variable `TAOM_COMMAND_TRIAL` holds the absolute path of an arm file (`key=value`, LF), with no file fallback.
An unarmed session pays one environment read per process, one static read per mission, one null check per application tick and per mission tick, one null check, a bool read and a bool write per screen tick, one `SeatInput?` copy per camera frame, and one static read per focus loss. It allocates nothing per frame. Keys: `out` (required, absolute folder), `quit`,
`scene` (default `battle_terrain_a`), `live_limit_seconds` (default 420), `steps` (a comma list, default all).

**Files**, all under `Client/Features/SeatOfSeeing/Trial/`:

| Type | Job | Shape to read |
|---|---|---|
| `CommandTrialOptions` | Parses the arm file | `LongDefeatAutorunPlan` |
| `CommandTrialAutostart`, `ICommandTrialGameHost`, `CommandTrialGameHost` | Waits for 4 s at the main menu, hides inquiries, starts the game, and ends the run. The phases and exits are pure over the narrow host, so a fake drives every exit. One field and one tick line in `Client/ClientSubModule.cs` | `FordsAutorunDriver` over `IFordsAutorunHost` |
| `CommandTrialGameManager` | A `CustomGameManager` that opens the mission in `OnLoadFinished`, and goes back to the menu on a failure | `LongDefeatGameManager` |
| `CommandTrialScript` | The driver. `SeatView` holds it in one nullable field, built only when armed, and calls it from `OnMissionTick` and `OnMissionScreenTick`. The steps are iterators in a partial file per feature, `CommandTrialScript.Seat.cs` here | Pure step order in `Main/Features/SeatOfSeeing/Domain/`, with a unit test |
| `CommandTrialSettings` | An `ISeatSettingsProvider` wrapper the session gets when armed (S2 onward) | "Settings in a run" below |

**The mission** is the real thing: `BannerlordMissions.OpenCustomBattleMission(scene, player, playerParty, enemyParty, isPlayerGeneral: true, null, "", "", 12f)`
(BannerlordMissions.cs:109). The player side defends. `CustomBattleCombatant.AddCharacter` fills the two parties.

| Side | Culture | Troops |
|---|---|---|
| Player | `gondor`, hero `lord_1_9` | 40 `fow_gondor_militia_spearman`, 40 `fow_gondor_ano_archer_militia`, 20 of a mounted `fow_` troop from `ModuleData` (find one, and name it in the feature doc) |
| Enemy | `mordor` | 120 `fow_mordor_orc_fighter`, 30 `fow_mordor_orc_hunter` |

Refuse to start on an unknown id, as `LongDefeatMissionLauncher` does, so that a bad id fails loudly.

**Deployment.** `Mission.Mode` starts at Deployment. Five seconds in, call `Mission.GetMissionBehavior<DeploymentHandler>().FinishDeployment()` from `OnMissionScreenTick`. It removes
two behaviours (DeploymentMissionController:80, BattleDeploymentMissionController:51-55), so it must never run inside `OnMissionTick` (`docs/reference/engine/mission-and-missionbehavior-lifecycle.md`). This also proves that the Seat refuses during Deployment:
a press there leaves the state Off, and the step's own line says so. Deployment writes no `[Check]` refusal (5.10). The direct call skips `OrderOfBattleVM.ExecuteBeginMission`, the route a player takes, so the banner handlers that method removes (OrderOfBattleVM:2059-2063) stay subscribed. A banner bearer's change then expires formation queries, and deselects and reselects a selected formation (:846-866). S1 selects nothing, but the panel steps of S2 and S3 run on this route, so read a selection that flickers in a panel step with this in mind.

**Pressing the controls.** A step never fakes a key at the OS level. The real key test and the step call the same method, so a step exercises the same path a player does. S1's members on `SeatView`, used only by the real key test and the trial, are `PressSeatKey()`, the read-only `PressPending`, and `TrialInput` (a
nullable `SeatInput` that `OnFrame` uses in place of `ISeatScreenAdapter.ReadInput` when it is set). The host gets the session and the view's one `SeatScreenAdapter`, and reads `State`, `Frames`, `GlideBegan` and `Faulted` from the session, so a trial read sees the real cursor and target state. Later keys live in `SeatKeys` (6.0), and `ICommandTrialHost` grows by one press member per key, so `SeatView` stays
under its 150 lines. Four controls are not Seat keys:

- **The order panel.** One trial member opens and closes it through the 6.2 site (`MissionOrderVM.OpenToggleOrder`, `TryCloseToggleOrder`).
- **Aiming.** `TrialInput` steers the look onto a chosen agent before `keep_to_soldier` and `take_soldier`.
- **The cursor.** `focus_follows_cursor` calls `Input.SetMousePosition` and reads back the pixel. It fails with a clear line if the window has no focus.
- **Steps outside the Seat.** S7's steps first press the Seat key and pass only with the state Off.

**Clocks and ticks.** Every step timer uses real time, summed from `OnMissionScreenTick`'s `dt`, which runs under pause (MissionScreen:595-600), because mission time stands still under a pause and repeats under fast time. A step reads mission time only as a measured fact, and the status
file's timer is real time too. S2 moves S1's steps to this. A step is an iterator: its state is local, a wait is a `yield`, and the driver stops at the first verdict that is done. A step that writes an agent, a formation or a mission flag first yields `Verdict.InMissionTick`, and the code after that marker runs inside the script's `OnMissionTick`. A step that removes a behaviour runs in the screen tick. Each row in `SeatTrialSteps` names what it requires and its time budget.

**One file per feature.** Two features add steps in parallel, so each keeps its rows, handlers and host members in its own files. `SeatTrialSteps.All` concatenates each feature's row list, the script's static constructor calls each feature's registration, and `ICommandTrialHost`, `CommandTrialHost` and the test world are `partial`. The wrapper keeps one tuple per feature, each tied to its table by a test.

**Prove a stage with `--steps`.** S3 and S5 kill soldiers and the hero, so a later row cannot assume a fresh battle in a full run. Run each stage's rows as a subset, with what `Requires` names.

**Settings in a run.** Some steps need a setting that defaults to off. When armed, the session gets `CommandTrialSettings`, a wrapper over the plain provider with `SeatEnabled` on, `BeginBattlesInSeat` off, and `SeatKeepPanelOpen`, `SeatHeroFightsOn` and `SeatRiseWithPanel` forced on, in memory only. A saved `BeginBattlesInSeat` would otherwise make `enter` fail with facts that never name the setting, because the entry at the deployment edge turns the script's press into a leave. It never writes the MCM file, because json2
keeps saved values. A test pins that an unarmed session gets the plain provider. `back_to_stand` turns its fight value off.

Every step ends with a line:

`[SeatTrial] step=<name> result=<PASS|FAIL> <facts>`

| Stage | Steps and pass rules |
|---|---|
| S1 | Built; Deviations row 19 holds the rules. `refuse_in_deployment`; `enter`, whose frame rule becomes "owned frames above 30 per real second" with S2; `fly`; `hero_stands`; `leave_glide` (`GlideBegan`, state Off, the engine's camera within 10 m of the hero inside 4 s); `reenter_keeps_height` |
| S2 | `cursor_with_panel` (panel open: `MouseVisible` true; panel closed: false); `focus_follows_cursor` (the handler's field holds the pointer function's value at the pixel read back, and the focused formation is a fact on the line); `keep_panel_reopens` (the close closes the panel; with no player key it reopens within a named limit of 3 frames, counted by the step's own waits; the trial's own close through the player's route stays closed; the line states `closed=` and `frames=`); `hide_bars_restored` (a picture with the flag on, one after, and the flag false after the panel opens) |
| S3 | `keep_to_soldier` (the kept agent is the one aimed at, and the camera is at the orbit distance from that soldier after 2 s: 16 m, within 2 m on either side); `keep_to_company` (the panel open and one company selected; the target is that company; the step lets go with the key and judges `letgo=`); `let_go_on_death` (the trial kills the soldier through `Mission.KillAgentCheat`, whose blow names the main agent or the victim as its owner, because the custom battle's morale model reads the attacker with no null test: no target, one message, and that message is the target-gone message; then it keeps to the smallest player company and kills every unit: the same) |
| S4 | `fight_on` (the trial charges the hero's formation; controller AI, distance moved stated and above zero, `IsRetreating` and `IsPanicked` false after 5 s, the panel opens); `morale_held` (the trial writes morale 0 on the hero from the mission tick; `IsPanicked` false and `IsRetreating` false after 2.5 s real time); `back_to_stand` (the trial stops the hero's company and reads his speed on the AI under 0.5 m/s, else `reason=ground-not-quiet`; then leave, setting off, enter: controller Player, speed under 0.5 m/s) |
| S5 | `hero_falls` (fight-on set, entered, the view keeps to the largest company, the trial kills the hero through `Mission.KillAgentCheat` from the mission tick: `Agent.Main` null, `HeroFightsOn` false, the Seat still up); `take_soldier` (the company is still kept, no aim; `Agent.Main` is a soldier of that company, `PlayerOrderController.Owner` is the new agent, every formation the fallen agent owned has `PlayerOwner` the new agent and `IsAIControlled` false, the panel opens) |
| S6 | `pause_flies` (mission time stands, the view moves); `slow` (mission seconds over real seconds is 0.5 within 25 percent, with no other request present); `fast` (at 2 and 4 the ratio matches `min(level, 0.1 / mean real dt)` within 25 percent; at 10 it is above 5); `speed_cleared` (after the key cancels, `FixedDeltaTimeMode` and `IsFastForward` are false and no request holds the id; teardown stays a unit test) |
| S7 | `rise_with_panel` (the Seat is down and the view third person; `CombatCamera.Position.z` is at least half the constant above its value before opening, after 1 s); `sink_on_close` (back within 1 m, the property exactly zero) |

Stages in Marshal's Word add theirs (`queue_two_moves`, `array_keeps_offsets`, `volley_holds`, `volley_looses`, `mark_keeps_order`, `square_side_kept`, and so on).

**Pictures.** `Utilities.TakeScreenshot` saves `hud_<step>.bmp` into the run folder at each `enter`, `fly`, `keep_to_company` and `hide_bars_restored` step, as the Fords autorun does, so that a session can look at the frame. The runner turns each into a PNG. A person or a session reads them: nothing judges them by machine, so the first run's stray arrow passed every step.

**Ending.** The script reports `final`. The autostart then calls `MBGameManager.EndGame` from the application tick, waits for the main menu, writes the done file, and calls `Utilities.QuitGame` when `quit=1`. An armed run answers no to `IsOpeningEscapeMenuOnFocusChangeAllowed`. Files: `command_trial_status.txt` every 5 s,
and `command_trial_done.txt` with `reason`, `run`, `report` (always `none`, for the runner it reuses), `log`, `live_seconds`, `wall_seconds`, `started`, `ended`. A failed write of either file logs a warning and the run goes on, because the runner reads the status file while the game replaces it, and the quit must still follow a lost done file. `reason=final` is the only success. The other reasons are `start_refused`, `start_failed`, `script_failed`, `never_live`, `wall_limit`, `mission_closed`, `live_limit` and `autostart_failed`.

**The wrapper**, `tools/run_command_trial.py`, is the same shape as `tools/run_long_defeat.py`, with `RUNS_ROOT = E:\Temp\command-trial-runs` and tests in `tools/tests/test_run_command_trial.py`. Its judge fails a run with any `result=FAIL` line, a missing `[SeatTrial]` line for a step in `steps`, a `[CrashReport]` block, no `[Shutdown] clean module unload` line, a failed `taom.seat-of-seeing.*` check line, or a `reason` other than `final`. It also fails a log whose `[BuildStamp]` is not this checkout: a sha other than `HEAD`, a `.dirty` stamp with no uncommitted file or a clean stamp with one, or an uncommitted file newer than the build. It watches the build's own pathspec (`Main`, `Client`, `Dependencies`, `Stubs`, `Directory.Build.props`, `GameReferences.targets`). Otherwise a run of an old deploy reports PASS for code it never ran. From S2 onward it also fails on a line that matches `\] \[ERROR\] (?:\[Seat(?:OfSeeing|Trial)\]|\[PatchApply\] \S*Patch140_)`, so a Seat patch that failed to apply fails the run, and on the thread guard's line for a trial or Seat site. The hot key failure logs at `LogError` for the same reason. Its PASS text reports the `[Session]` swallowed counts.

A builder writes all of this and never launches it. The orchestrator deploys and runs it under the gate in `docs/reference/unattended-game-runs.md`.

## Deviations

Record each change to this contract here with its reason. Stage S1, built 2026-11-03:

1. **The three camera records live in core.** `SeatFlight`, `SeatInput` and `SeatPose` are `readonly record struct` types in
   `Main/Features/SeatOfSeeing/Domain/SeatCameraTypes.cs`, not in `Client/Features/SeatOfSeeing/`. `TAOM.Client` declares no records
   (its csproj: a second `IsExternalInit` is CS8356 in the tests), and `ISeatScreenAdapter` is a core interface (so core can register
   its null, ADR-013) that passes `SeatPose`. `SeatCameraMath` stays in the client. Row 13 moved the interface to the client; the records
   stay in core because of the csproj rule.
2. **`ISeatScreenAdapter` was in `Main/Adapters/`, with a core null replaced in `TaomClientComposition`.** Row 13 removed that: the review
   found no core caller and no server path, because `TAOM.Client` does not load on a server.
3. **The patch classes are in `Client/Features/SeatOfSeeing/Hooks/` but their namespace is `TAOM.Features.SeatOfSeeing.Hooks`**, not
   `TAOM.Client.Features.SeatOfSeeing.Hooks`. `PatchCategoryNumberTests.OwnerOf` takes the owning feature from a `TAOM.Features.<X>`
   namespace, and a `TAOM.Client...` one would make Patch140 name two features (as `ManyHammers` does).
4. **The key category is registered by the module's `OnPhase(ProcessLoad)`**, as `RealmBordersModule` does, so `Main/SubModule.cs`
   needs no line for it.
5. **`SeatFlight.Height` is above the ground and `Enter` takes no ground argument.** `SeatCameraMath.Enter(camera, last)` returns the
   flight; `Hold(camera, groundZ)` is the no-blend entry for `BeginBattlesInSeat`. `Fast` doubles the lift speed as well as the pan
   (the engine multiplies its whole speed vector). Pan and lift speed read the height before the step's zoom. `SeatInput.LookYaw`
   is in look units with a positive value turning left, and `LookPitch` a positive value looking down; `SeatCameraMath.Look` turns
   the mouse and sticks into them with the signs of `MissionScreen.UpdateDragData` (2936-3010). A negative `dt` leaves the state
   unchanged like a non-finite one.
6. **`SeatMissionClassifier` adds two presence tests the contract did not name**: `HideoutAmbushMissionController` (the sneak-in
   route is also a hideout) and `CampaignMission.Current?.Location != null` for Settlement. The decision is the pure
   `SeatMissionKindRules.Classify(SeatMissionProbe)`, so the order is tested: hideout, tournament, arena, campaign location, then
   combat with a team AI or a Battle or Deployment mode, else Other.
7. **`WriteLook` reports a missing `CameraElevation` setter as a `fail` check line** (`taom.seat-of-seeing.glide`, `elevation=missing`)
   instead of a separate log line; the session writes it.
8. **The manifest is `tools/ingame_checks/0.toml` with `issue = 0`**, because no issue exists yet. The gate in
   `tools/tests/test_ingame_verdict.py` stays as it was: `--issue 0` reads the file, `--owed` never reaches it, and `--post` stops at `gh`. One
   manifest serves both commander features, because one held issue covers both. When the issue exists, rename the file to `<issue>.toml` and set
   `issue` (and the two constants in `SeatOfSeeingManifestTests`). `fixed_in` is `7a4c4c00c`, on trunk before the feature, so it survives
   any way of landing and needs no change.
9. **Both S1 settings are `Presentation` in `CoopSettingsRelevance`** (a camera is one peer's view), so the simulation-relevant count
   stays 331 and the total goes from 509 to 511; the numbers in `coop-interop.md`, `bannerlord-together-compat.md` and three tests
   moved with it.
10. **Section 12 (the field trial) was added to this contract while S1 was being built and is not built.** The brief for S1 named
    sections 2 to 5. The trial needs `SeatView` to expose one internal method per control and a nullable input override; S1 leaves
    room for both (`SeatSession` takes its input from `ISeatScreenAdapter.ReadInput`) and builds neither.
11. **S1 review fixes (floor, sharp view, warg rage).** `SeatEngineFacts.RageHoldsHero` has no default value, so every producer of the
    facts states it. The rage refusal says its message each time the key is refused, and the first-entry message is not used up by a
    refusal. A rage that starts while seated leaves through the same path as a conversation, so the glide runs in Battle mode only.
    The property and its two writes are pinned by source-text gates in `WargControllerSwapTests` because the controller is a `MissionView`
    that needs a live mission (row 13 says how the file stays under its baseline). The depth of field calls are pinned
    the same way (`SeatOfSeeingWiringTests`), beside a game-assembly test of the two `Scene` signatures (MissionScreen.cs:2354-2355 agrees).
12. **S1 review corrections, made in this contract before the first commit.** The sections above now say: key 530 is `Q` (section 3, with the
    rule for choosing a default); the kind is read fresh by later stages (section 4); the deployment edge comes from `OnMissionModeChange`
    and teardown has two paths (5.3); the mask is the named engine flag, the boundary result is gated, and `camera_height_limiter` is honoured
    (5.4); `SuppressSpectatorCyclingThisFrame` is gone, because the flag it sets is cleared before any reader runs (MissionScreen:567, 3838);
    the look is written on every leave except the end-of-mission one, and the glide needs an agent to follow (5.4); stand fast runs only from
    the mission tick, with no queue, and three other input readers are accepted (5.5); a fallen rider is not held (5.5); refusals and
    the reason for a leave write check lines (5.10). `OnSiegeEngine` became `OtherView`, because a board game also sets a custom camera.
13. **The second review wave's corrections, also before the first commit.** The swap back no longer tests `HoldsHero`: it skips vanilla's
    `Enable` while the Seat is up (5.5), which closes three routes and restores the old behaviour outside the Seat. A finished session is
    terminal, and the deployment edge clears in any state and needs Battle mode (5.3). Simplifications with no behaviour change: the screen
    adapter and its interface are client types, built per mission with no null and no registration (5.1); the hero adapter holds no state
    and the view owns the one event subscription (5.5); the session takes its settings and permissions at construction (5.1); the classifier takes
    the logger and exposes `OwnsTheCamera` (section 4, 5.6); `DedicatedServer` is gone (section 4); the math shares `Clamp`, `WrapAngle` and
    `Pan` with Fords (5.2). The swap's lines are paid for by deleting two unused events, so the file keeps one statement per line. The engine's
    own round events in a native multiplayer game mode are a named limit with no guard (section 4), because TAOM does not run there.

Field trial (section 12) for stage S1, built 2026-11-04. Row 10 no longer holds: the trial and the members on `SeatView` it needed are built,
and the first field trial below ran them.

14. **More files than the table names.** `Client/Features/SeatOfSeeing/Trial/` holds the four contract types plus `CommandTrialRun` (the
    process-wide state: armed once by the client submodule, the run's outcome, the step for the status file), `CommandTrialLauncher` (opens
    the mission, as `FordsMissionLauncher` does for Fords), `ICommandTrialHost` and `CommandTrialHost` (every engine and Seat read the script
    makes, so a test runs a whole trial against a stand-in). `Domain/` holds `SeatTrialSteps` (the step order), `SeatTrialRules` (each pass
    rule and the line shape) and `SeatTrialRoster` (the two armies and the id check). The pure step order the contract asks for is the first
    of the three.
15. **Four read-only members on `SeatView` beyond the three named.** The trial needs `PressPending` (to know the session took a press, or the
    deployment step could pass because the key never arrived), `Frames`, `GlideBegan` and `Faulted`. `SeatSession` gains `Frames` and
    `GlideBegan` to serve them, and `OnFrame(dt, input = null)` takes the trial's input. `SeatOfSeeingWiringTests` pinned the text
    `_session.OnFrame(dt)`; it now pins `_session.OnFrame(dt, TrialInput)`.
16. **The real key goes through `PressSeatKey()` itself.** `OnMissionScreenTick` calls it when the two-part key test holds and then reads and
    clears one flag for the session, so the trial and a player share the one path and the session has one caller. The escape-menu answer
    for an armed run is one override on `SeatView` (`!CommandTrialRun.IsArmed && base...`), because the trial adds no view; the contract
    pointed at `LongDefeatUnattendedView`, which is a separate view in a view list of its own.
17. **Three lines in `ClientSubModule`, not two.** The field, the tick line, and the arming call in the existing first-tick block, which is
    what makes the environment variable a single read. The comment above `OnApplicationTick` now says three null checks. An unarmed session
    also pays one null check in the view's screen tick, because the contract has the script run from both ticks.
18. **The trial ends from the application tick.** The script reports `final` and the autostart calls `MBGameManager.EndGame`, waits for the
    main menu, writes the done file and quits, in the shape of `FordsOfWarGameManager.ReturnToMainMenu`. The Seat is still up at that
    moment, so the `Finish` path runs with the Seat up. The done file's reasons beyond the contract's: `start_refused` (an id the game lacks,
    or a step list the game refuses), `start_failed`, `script_failed`, `never_live`, `wall_limit` (a wall clock past the load allowance and the live limit, so a mission clock
    that stopped cannot hold the run), `mission_closed`, `live_limit`, `autostart_failed`, and any of them joined to `exit_stalled`.
19. **Pass rules the table left open.** `refuse_in_deployment` presses one second in and passes when the session took the press
    (`PressPending` cleared), the mode is still Deployment and the state is Off. `leave_glide` cannot count "one `OnMissionModeChange` call":
    `BeginGlide` calls the screen's listener, not `Mission.SetMissionMode`, so `SeatView.OnMissionModeChange` never sees it. Its evidence is
    `GlideBegan` (the call returned true), the state Off, and the engine's camera within 10 m of the hero inside 4 s; whether the glide
    eased or snapped is a fact (`after=`), not a failure, because the snap is a known limit. `fly` compares the distance with
    `PanSpeed(height above ground at the start)` times the elapsed mission time. `hero_stands` is ground speed over 1.5 s of mission time,
    sampled in the mission tick. `reenter_keeps_height` compares the height above ground before the leave with the height one second after
    the Seated state returns, within a metre or 10 percent. `enter` takes the contract's "more than 100 frames after 3 s" literally, which is
    about 34 frames per second.
20. **Order of the deployment and of the steps.** The deployment is finished from the screen tick at 5 s of mission time, but not while the
    refusal step is still waiting for its press. A list of steps in `steps` or `--steps` is refused when a step lacks one it needs
    (`fly` needs `enter`, `reenter_keeps_height` needs `leave_glide`), so a subset never asks a step to start from a state nobody made. A
    list that holds only the refusal step ends the run before the deployment is finished.
21. **The judge is a little stricter than the contract.** Besides the four failures it names, it fails a failed `taom.seat-of-seeing.*`
    `[Check]` line (the fault latch must stay silent), and it treats a `result=FAIL` line of any tag as a failure.
22. **Two pictures, named by step.** `hud_enter.bmp` two seconds after the entry and `hud_fly.bmp` 1.5 s into the flight; `keep_to_company`
    belongs to S3.
23. **The enemy side has no general.** The contract's table names none, and vanilla's custom battle gives the enemy one lord character. The
    launcher sets none, so `CustomBattleCombatant.General` is null on that side, as it is in vanilla, which adds the lord but never calls `SetGeneral` for the enemy
    (CustomBattleHelper:236-237). The real difference is no hero: `Team.GeneralAgent` stays null (GeneralsAndCaptainsAssignmentLogic:180-204), so `CanTeamHaveGeneralsFormation` returns false (:120-129). The first run passed that way.

First field trial, 2026-11-04 03:11 (`E:\Temp\command-trial-runs\20261104-031119`, build `1735f2add.dirty`): six steps passed in 50 s of wall time, exit 0, `[Session] checks_pass=7 checks_fail=0 swallowed=0`. It settles these facts: mission time advances in Deployment (the finish fired at `mt=5.0`); about 84 frames a second with 272 agents; `EndGame` with the Seat up
passes through `Finish` cleanly; a null enemy general caused no fault; `moved=59.7 expected=59.7`; the glide closed from 55 m to under 10 m in 0.5 s of mission time. Real keys, axes, the wheel and the mouse are still not exercised. The pictures show the vanilla attack direction arrow in the sky view, which 6.4 removes.

Contract corrections after a read of the unbuilt stages against the engine, before any of them was built, 2026-11-04:

24. **Keys 531 and 532 moved from `T` and `Y` to `Insert` and `Delete`** (section 3). `GauntletChatLogView` reads `T` and `Y` from the mission's scene layer in single player, so the old text "no reader in a single player mission" was false, and the chat box would take the flight keys. The list of categories also named the wrong six.
25. **S2 rewritten.** The panel state is `Mission.IsOrderMenuOpen`, not the toggle event, whose raises are gated, and which would leave a dead view on the Game's event table (6.2). A close counts as the player's for four routes, not two. The adapter owns the cursor layer (6.1). The pointer subtracts the usable area's margins (6.3). The look uses
    `DontChangeCursorPos` with no stored pixel (6.1). One crosshair patch is enough, the cheer and walk mode views join the table, and the equipment view takes prefixes on its two tick methods, because its `IsMainAgentAvailable` can be inlined (6.4). Key 536 hides the whole interface and restores only its own hide (6.4). A radial menu refuses entry (6.0). The shapes S1 must change are listed in 6.0, and each stage has a definition of done.
26. **S3 corrected.** The terrain ray's flags and its NaN case are named, a company that empties lets go, the rule is occupant then active, the adapter holds the target, rise and sink let go, and the orbit pitch has a floor (section 7).
27. **S4 rewritten around one defect.** The old text read the morale before the flip and wrote it back. A Player hero has no `CommonAIComponent`, so that write set morale 0, and the hero would have panicked and routed on entry. The new text reads and writes no morale, refuses panic in the two morale models, and handles a retreat order (8.1). The tick compares wanted against actual, which replaces one owed flag.
    `BeginFighting` and `EndFighting` take no agent, the fall while on AI has a rule, `EndFighting` has an order that does not throw, and `SeatPresence.HeroFightsOn` is the named gate. The boundary patch sets `isAgentOutside` false rather than skip, because a skip freezes a countdown that keeps ticking (8.2).
28. **S5 corrected.** The hand-over runs only after the Seat's own take, in the same tick, with no captured list and no event, because that event raises on every write of `MainAgent` and would change vanilla for a player who never used the Seat (step 3). Step 4 is deleted: toggling `IsOver` marked a live battle as over, and vanilla already resets the score screen. The selected-company clause is gone, because a panel
    cannot be open after a fall (step 2). The hero rule has a test.
29. **S6 corrected.** A fault cancels mission time, so a throw cannot leave a battle paused with dead keys (5.3). The per-frame rule replaces "read it when a key steps", because `FordsMissionSpeed.Apply` asserts the flag every frame. Level 10 needs the Seat up or no main agent. Removing an absent time request throws, so the only legal write is stated. `SeatEnabled` off stops the keys. Each step shows a message and writes a line.
    The rate is `min(level, 0.1 / frame time)`; the old "about 6 times" could not occur.
30. **S7 corrected.** The last write is exactly zero, or the target lock stays blocked. A sally out gets the small lift, and other missions get none. The progress and its smoothstep are two functions, because one return value cannot be both. No writer of the property exists, so `Finish` writes nothing and nothing is saved.
31. **Section 12 corrected.** Step timers run on real time, because a timer in mission time never fires under a pause. Armed runs get a settings wrapper, because `fight_on` and `rise_with_panel` cannot pass on default settings, and writing the MCM file would change the player's saved value. Each row has a pass rule. `morale_held` writes a low morale first and runs while the hero fights on, or it would pass with the guard deleted.
    The four controls that are not Seat keys have a named path. Row 19's rule for `enter` moves to a rate.
32. **Decisions taken with these corrections, each reversible:** `SeatKeepPanelOpen` accepts the quarter speed of `SlowDownOnOrder` and says so (6.2); `SeatCursorWithPanel` stays true with the pass-through click named as a limit until M7 (6.1); after a fall the pointer is the screen centre with no mark in this version (section 9); the boundary prefix also serves a stand-fast hero (8.2).
33. **Trial harness after its review, 2026-11-04.** The wrapper passed on a stale deploy, because it never compared the log's `[BuildStamp]` with the checkout, so it now refuses one (section 12). A status write that failed ended a healthy run as `autostart_failed` and could overwrite a passing `final`, and a failed done write lost the quit, so a write now logs a warning and the run goes on, as `FordsAutorunOutput` does. The script releases the trial's input on an abort as well as on completion, and an armed process with an outcome builds no second script. `SeatTrialStep` drops `Stage` and `PassRule`, because nothing reads them and section 12 holds the rules. `CommandTrialHost.GroundHeight` calls the adapter's probe instead of a copy of it. Not changed, because none has a known producer: a process that never reaches the main menu ends at the runner's limit, about 55 minutes; a bad `--scene` reaches the native loader; a failed picture ends the run as `script_failed`.
34. **Trial harness after its second review wave, 2026-11-04.** The script's steps become iterators, because six steps already needed eleven shared fields and a reset list, and sections 6 to 11 and Marshal's section 11 add at least 28 more. `Verdict.InMissionTick` gives section 12's tick rule one marker line, where each builder would otherwise have invented a seam. Each feature keeps its steps in its own files, so two builders edit two shared lines. The host gets the session and the one adapter, because S2 and S3 put the cursor layer and the kept target in that adapter, and a second instance would see neither. The autostart's phases sit behind `ICommandTrialGameHost`, because the exit matrix is the only thing that guarantees an unattended run ends, and it had no test. The build check now also compares the stamp's `.dirty` with the tree, and reads the build's own pathspec. Rejected: reading the wrapper's step list from the C# table, because a table that only partly matches would silently drop a row, and a tuple with its tie test fails loudly.
35. **Trial harness, second wave: what the builder chose where this contract was silent.** The Seat's rows are in
    `Main/Features/SeatOfSeeing/Domain/SeatTrialRows.cs`, and `SeatTrialSteps.All` is that list until a second feature joins one. The
    interface, the host and the test world each have a `.Seat.cs` part with the Seat's members (`PressSeatKey`, `PressPending`,
    `SetInput`, `State`, `Frames`, `GlideBegan`, `Faulted`); the rest stay in the base file. `CommandTrialScript` takes an optional handler
    table as its last constructor argument, the one test seam that drives `Verdict.InMissionTick` and the no-verdict rule, because no S1
    step uses the marker. A wait has a name (`Verdict.Waiting("battle")`), the timeout fact is `reason=timeout waiting=<name>`, and a step
    that ends without a verdict fails with `reason=no-verdict`. The autostart's clock is `ICommandTrialGameHost.WallSeconds`, a stopwatch
    in the host, and not the sum of the ticks' `dt`, so a long load still counts in full. `CommandTrialRun.Script` takes the host as a
    `Func<ICommandTrialHost>`, and `SeatView` passes a lambda, which costs one closure per unarmed mission; a static lambda with a state
    tuple would avoid it but adds a generic parameter and a five-element tuple to the view. `FinishDeployment` is guarded by
    `InDeployment` alone, because the engine changes the mode inside the call (`DeploymentHandler.FinishDeployment`, then
    `DeploymentMissionController.FinishDeployment` line 79 to `BattleDeploymentMissionController.AfterDeploymentFinished` lines 51-55,
    `Mission.RemoveMissionBehavior` lines 4708-4710, `DeploymentHandler.OnRemoveBehavior` lines 44-48, `Mission.SetMissionMode` lines
    1618-1625): the `_deploymentFinished` flag and the empty-list branch are gone. A mission whose `DeploymentHandler` is missing would
    log the finish line every tick; no producer of that is known. In the wrapper a path that no longer exists (a deletion, or the old
    name of a rename) is a changed path with no time, and the Seat's step tuple is `SEAT_STEPS`.

Stages S2 and S3 and their field trial steps, built 2026-11-04. Each row is a difference from the contract above, with the reason.

36. **Reversed by row 57: each cursor show built a new layer.** Section 6.1 said the adapter adds the layer on the first show and removes it on false. `ScreenBase.RemoveLayer` finalizes a layer (`ScreenBase.cs:327`) and `AddLayer` refuses a finalized one (`:299-301`), so `SeatScreenAdapter.SetCursor(true)` builds a fresh `GauntletLayer` each time and `SetCursor(false)` removes it. A middle-button look therefore removes and rebuilds the layer; a panel opens a few times a battle, so the cost is small.
37. **"Escape released" is read as the engine's `ToggleEscapeMenu` hot key.** `MissionScreen.HandleInputs` tests that hot key (`MissionScreen.cs:4203`) and then runs `OnEscape` on each view, so the hot key is what closes the panel. A player's rebind of it is followed, which a raw `InputKey.Escape` test would miss. The other three routes are read as the contract says.
38. **A keep press acts only while Seated.** Section 6.0 reads key 531 while the state is not Off, and `SeatKeys` does. `SeatSession.PressKeep` stores the press only when Seated, because section 5.3 says input does nothing during the blend, so a press in the 0.7 s of Rising is dropped.
39. **`SeatEngineFacts.RadialMenuActive` has no default value**, as `RageHoldsHero` has not (row 11), so every producer of the facts states it.
40. **A kept soldier is looked at by the chest, a company by its ground median.** The contract names the company's `CachedMedianPosition.GetGroundVec3()` and no point for a soldier. The adapter reads `Agent.GetChestGlobalPosition()`, so the orbit looks at the body, not the feet.
41. **The orbit starts where the camera is.** The eased point begins on the camera's look line at the start distance, and `SeatOrbit.Pose(SeatOrbit.Begin(pose))` returns that pose, so pressing the key does not move the view on the first frame; the point then eases to the target with `1 - exp(-5 dt)`. A pitch below 0.05 is held at 0.05 from the first frame, which can lift the camera a little.
42. **The orbit and the keep rules are in `Domain/`, not in `SeatCameraMath`.** `SeatOrbit`, `SeatKeepRule`, `SeatCursorRule`, `SeatPanelRule`, `SeatPointer`, `SeatHideRule`, `SeatKeyScope` and `SeatTrialAim` need no engine type and sit under the Domain gate. `SeatCameraMath` gains only `FlightOf(pose, ground)`, which needs `FordsCameraMath.Clamp`.
43. **The orbit reads the ground at the pose it is about to show**, once a frame, with the same mask as a flight frame. The order is the contract's: boundary clamp, height cap, floor. The ground is smoothed from that raw read, and the stored flight takes the pose's height above the smoothed ground, so letting go continues without a jump.
44. **The focus check line is written by the session.** `Patch140_FormationFocusPoint` sets a volatile mark (`SeatPresence.MarkFocusSet`) and the session's next panel frame writes `taom.seat-of-seeing.focus` once, so the patch calls no `IOutcomeLog`.
45. **`SeatCloseSignals` are read only while `SeatKeepPanelOpen` is on and the panel is open or was open on the last frame.** The previous frame's signals are kept for the same reason, so a player with the setting off pays no close-route reads, and a mission with no order handler never reads a key it did not register (6.2).
46. **The trial reads the focus handler's `_centerOfScreen` by reflection.** Section 12 asks for "the handler's field". That is a private field, so the trial host looks it up once (`CommandTrialHost.Seat.cs:21`, one row in Category B of `reflection-sites.md` and one `[DataRow]`). It is read by the trial only. The trial subscribes to the handler's public `OnFormationFocused` event for the focused company's name.
47. **`CommandTrialSettings` holds no inner provider and also forces `SeatCursorWithPanel` on.** All four members are forced in memory, so there is nothing to pass through. `SeatCursorWithPanel` is added to the contract's list because a saved off value would fail `cursor_with_panel` with facts that never name the setting, the case row 33 describes for `BeginBattlesInSeat`. `SeatHeroFightsOn` and `SeatRiseWithPanel` do not exist yet, so nothing forces them.
48. **The live clock of the autostart is real time.** `live_seconds` in the done file, the live limit and the status file now sum the application tick's `dt` while a mission is open. The status key is `live=`, not `mt=`. `ICommandTrialGameHost.MissionSeconds` stays only to say whether a mission is open. `wall_limit` now bounds application ticks that stop feeding the clock, and a paused mission ends at the live limit as it should.
49. **`hero_stands` uses a real-time window and divides by the mission seconds it measured.** The window is 1.5 real seconds. The hero's ground distance is divided by the mission time that passed in it, which is the one measured fact left on mission time; a paused mission reads as a failed step with `mission_seconds=0.0`.
50. **Picture names.** `hud_hide_bars_restored_hidden.bmp` and `hud_hide_bars_restored_back.bmp` for the two pictures of `hide_bars_restored`, and `hud_keep_to_company.bmp`, all `hud_*.bmp` so the runner's existing conversion picks them up.
51. **How the trial chooses.** `keep_to_soldier` and `let_go_on_death` keep to the player-team soldier nearest to the camera (a human, active, not the hero, not a mount), so nothing is likely to stand between the ray and him. `keep_to_company` keeps to the company with the most units, and `let_go_on_death` kills every unit of the smallest company that holds no hero (`SeatTrialRules.SmallestKillable`), so the hero stays alive for later stages. The aim (`SeatTrialAim`) turns 0.7 of the remaining angle per tick and calls the view on target within 0.01 rad for three ticks.
52. **The marker gate finds the host's writers itself.** `TheTrial_NeverRemovesABehaviourOrWritesAnAgent_ExceptAfterTheMissionTickMarker` reads the host file for members that call `Agent.Die`, the selection writes or the panel's open and close, follows a private helper they use, and then checks every step iterator: the nearest earlier `yield return` that continues the step must be `Verdict.InMissionTick`. A `Failed` or `Passed` verdict ends the step, so it does not count. The choose members (a list read) also run after the marker, though the gate does not require it.
53. **The judge reports `[Session]` counts from the last summary line and does not fail on a missing one.** A log with no summary line passes with "no [Session] summary line in the log" in its text; the contract asks the PASS text to report the counts, not to require them.
54. **Trial-only members on the production adapter.** `SeatScreenAdapter` has `TrialPlayerClose` (a close the trial makes look like a player's route), `Said` and `LastSaid` (messages shown), `KeepsAgent` and `KeepsFormation` (reference tests), `MouseVisible`, `OpenPanel` and `ClosePanel`. The host holds the view's one adapter (row 29), so a second instance would not see the cursor layer or the kept target.
55. **Two messages.** `SeatMessage.KeepNobody` ("There is nobody there to keep to.") and `KeepGone` ("The one you were keeping to is gone."), with the localization rows seeded in all 12 languages.
56. **No engine disagreement was found in sections 6 and 7**, except that the contract names `GauntletBannerBuilderScreen` and `BodyGeneratorView` as the shape of `DontChangeCursorPos`; the decompiled `MapScreen` (`MapScreen.cs:1142`, `:1637`) and the character creation naming view use it the same way, each frame while a middle button look is held, and the Seat does the same. `GauntletBannerBuilderScreen` and `BodyGeneratorView` match too, in `TaleWorlds.MountAndBlade.GauntletUI.dll`.
57. **Corrections from the first review of S2 and S3.** The contract now says what the engine does in these places: the walk mode control's hot key (5.5, 6.4), one toggled cursor layer (6.1, reversing row 36), key 77 as a fifth close route, the signals read only around an open panel, Transfer, the reopen before the cursor step and a refused reopen (6.2), the hide flag's second writer and what an invisible panel does (6.4), the orbit's read and floor (7), an empty company (7), and the S2 and S3 steps and the judge (12).

Second pass over S2 and S3, 2026-11-04: what the builder chose where the review's decisions left the form open.

58. **The orbit's floor is a clearance argument.** `SeatCameraMath.AboveGround(pose, rawGround, clearance = GroundClearance)` keeps one method; a flight frame calls it with the default half metre and an orbit frame passes `MinHeight`, so a low orbit over flat ground lets go at the height the flight starts from. The smoothed ground can still lag the raw read under a moving company; that is a flight frame's own behaviour, and the feature doc's Known limits name it.
59. **A failed step releases what it held, and holds its verdict for the mission tick when a panel is open.** `CommandTrialScript.Settle` sets the look input to the zero input (not to null, which would hand the camera back to the real input) on any failed verdict. When the panel is still open, closing it is a mission flag write, so the failed verdict waits for the next mission tick, closes the panel as the player does there, and is reported then. A step that fails inside a mission tick closes the panel at once. The next step therefore starts with the panel shut, and its own fact names its own failure.
60. **The marker gate classifies every host member.** `HostMembers` finds methods, constructors and properties with or without an access modifier, generic or not, and a member is a writer (found by the engine verbs it names, or by calling one that does) or is on `HostMembersThatWriteNothing`, which a person extends after reading the new member. A name on the list that no longer exists fails the gate, and so does any `set`. The member pattern does not read a return type that contains a space (a tuple, a generic with two arguments); no host member has one today, and a new one would show as unclassified or as a missing name, never as a pass.
61. **`leave_glide` rises for half a second before it measures.** `SeatTrialRules.LiftSeconds` is 0.5, which at the 22 m entry height is about 10 m of lift (`LiftSpeed` is 8 m/s plus half the height). `SeatTrialRules.HeightKept` takes the entry height as a third argument, and the world's `ReenterHeight` is null by default, which means the re-entry keeps the last flight's height as the Seat does.
62. **`fly` reads the direction through a host member.** `ICommandTrialHost.ViewForward()` returns the ground direction of the camera's yaw (`FordsCameraMath.Forward`), the direction a forward flight input follows (`SeatCameraMath.Step`). The rule is the dot of the displacement with it at 0.9 or more (`SeatTrialRules.ForwardDot`, `FlewForward`), and the dot is a fact on the line as `dot=`.
63. **The two equipment tick prefixes clear exactly the pending press and the hold time.** `_prevDropKeyDown` and `_dropHoldTime`, `_prevEquipKeyDown` and `_equipHoldTime` are the fields the view clears in its else branch (`MissionGauntletMainAgentEquipmentControllerView.cs:128-132`). The equip tick's `_firstFrameOfEquipDownHandled` and `_equipmentWasInFocusFirstFrameOfEquipDown` are not cleared: an equip press that began before the entry makes the next press read the focus flag of the old one. At worst that picks up the weapon in focus at once; nothing proves it in game.
64. **`keep_panel_reopens` counts frames.** The step counts its own waits after the close (`SeatTrialRules.ReopenFrameLimit`, 3). A waiting step is resumed once per screen tick (`CommandTrialScript.cs:78`) and a step parked on the mission-tick marker once per mission tick (`:96`), and the view ticks the session before the trial (`SeatView.cs:81-84`), so the session's reopen on its next screen tick shows to the step's first resume: 1 is the usual answer and 3 leaves room for view order, which is not fixed. The test world reopens after a number of screen ticks instead of seconds.
65. **Corrections from the second review of S2 and S3, 2026-11-04.**
   - `SeatCursorRule` and `SeatKeyScope` are gone (rows 38 and 42 name them). The session derives the cursor once and reads the look key only when it can matter. `SeatKeys` reads keys 531 and 536 only while Seated, because a press during the rise was dropped anyway. Stage S5 adds key 532's wider test when that key exists.
   - The trial kills through `Mission.KillAgentCheat` (Mission.cs:6216), so the engine picks the blow's owner and the trial carries no copy of the blow. That blow goes through `Agent.RegisterBlow` and `HandleBlow`, which kills at `Health < 1` and gives an `Immortal` agent no damage, so stage S5 checks its hero's state. It also raises `OnAgentHit`, plays a hit sound, and reads game keys 0 to 3, which nobody presses in a run. The engine's guard does nothing in CutScene, Conversation or Barter (Mission.cs:6218), and `Agent` zeroes the damage for those first two modes, `Immortal` and `DisableDying`, which only `HostReviewMissionLogic.cs:48` sets. A unit that keeps its health stays in its company, so the step then reads `company_gone=0`.
   - Row 59's held verdict is gone. A failed step closes a panel left open at once, because vanilla closes this panel from the screen tick (`GauntletOrderUIHandler.TickInput`, through `MissionOrderVM.cs:777`), so the next step starts with the panel shut as before.
   - Row 44's check line is written once a mission, and the session now also calls `Pass` once. The patch's mark is set again on every frame, so a per-show mark called `Pass` on every frame.
66. **The keep rule is a pure function, and `TryKeep` reads the panel itself.** `SeatKeepRule.KeepsCompany(panelOpen, selectedCount, firstUnits)` holds the choice of section 7 between the selected company and the soldier under the pointer. `ISeatScreenAdapter.TryKeep()` takes no argument: the adapter reads `Mission.IsOrderMenuOpen`, the selected formations and the first one's unit count, and asks the rule. It now reads the selected formations on every keep press, also with the panel closed, where it read them only with the panel open; that is a list read on one key press, in the mission tick.
67. **One name for opening the panel.** `ISeatScreenAdapter.OpenPanel()` replaces `ReopenPanel()`, which called the same method under another name. `ClosePanel` on the adapter and on the trial host returns nothing, because no step read its result and each step reads `PanelOpen` instead.
68. **The order ray and the keep pick share one pointer.** `SeatOrderRay.PointerRay`, `RayThickness` and `RayFlags` hold the pointer choice (the cursor when shown, else the screen centre), the 0.3 m thickness and the focus ray flags, and `SeatScreenAdapter` uses them, so the two rays cannot disagree about where the pointer is.
69. **The trial host takes the logger.** `CommandTrialHost` gets an `IModLogger` in its constructor from `SeatView`, and `NoteWrite` passes `_logger.LogError` to `MissionThreadGuard.NoteCall`, as `SeatHeroAdapter` does, where it resolved the logger from the container inside a lambda. `ServiceLocatorGateTests` reads only files in a `Services`, `Engine` or `Engines` folder or named like a service, engine or view model, so it never read `Client/Features/**/Trial/`. No `IoC.Resolve` is left in that folder, so adding `Trial` to the gate's folders would fail nothing today and would keep the call from returning; the gate is unchanged.
70. **The marker gate's verb is `KillAgentCheat`.** Row 52's list of engine verbs now names `.KillAgentCheat(` where it named `.Die(`, and the kill tests pin that the host builds no blow of its own and keys nothing on an agent index.
71. **The judge reads git itself, every time, and reads one stamp line.** `judge` has no `head` or `changed` parameter, and the tests answer `git_state` instead. The `MISMATCH` test reads the line the sha was read from, so a stamp line with no sha ahead of the real one can no longer hide a `MISMATCH` on it; a log with such a line is judged stricter than before. The reverse also holds: a `MISMATCH` on a line with no sha, ahead of a clean stamp line, is no longer stale. The stamp has one format (`BuildStampReport.cs:149`), so neither order has a known producer.

Stages S4 and S5 and their field trial steps, built 2026-11-04. Each row is a difference from the contract above, with the reason.

72. **Line numbers of the contract's engine citations moved in the installed decompile; the behaviour is the same.** The held Kick key reads `Mission.CanTakeControlOfAgent(LastFollowedAgent)` at `MissionScreen.cs:1810-1812` (the contract says 2890-2893). The openers that arm the flag are `SandBoxMissions.cs:458` and `:872` (the contract says 713 and 1461); `BannerlordMissions.cs:155` and `:237` and `LongDefeatMissionLauncher.cs:89` match. The engine's own nulling of the two agent delegates on removal is `Mission.cs:3027-3028`; the contract does not cite it. `StopUsingGameObjectAux` is `Agent.cs:3980-4045` (the contract says 3982-4030 and 4025-4045). The feature doc and the registry quote the installed lines.
73. **The trial's value of `SeatHeroFightsOn` starts off, not on.** Section 12 forces it on for a whole run. With it on from the first entry the hero is on the AI one mission tick after `enter`, so `enter` reads `controller=Player` three seconds later and fails on a correct build, and `hero_stands` measures a walking hero. `CommandTrialSettings.HeroFightsOn` is a plain property; `fight_on`, `hero_falls` and `back_to_stand` set it through `ICommandTrialHost.SetFightOn` and enter again, so the Seat reads it at a new entry. `back_to_stand` turns it off as the contract says.
74. **`fight_on` asks for more than "distance moved above zero".** A hero standing in a charging company can drift a few centimetres. `SeatTrialRules.FoughtOnMetres` is 1 m, and the distance is a fact on the line. It is a first number for a run to confirm, like the other windows.
75. **No refusal by race.** Section 9 refuses "a creature race that `troll-race.md` and `Main/Features/PlayerSwitcher/` treat as unplayable". `troll-race.md:5` calls the troll "playable/NPC", and `PlayerSwitcher` holds no race rule (its only `race` is a preview index). Non-humanoids fail the human test already. No race rule was built; the platform crew refusal (`HowdahCrewAgentOrigin` and `MumakilCrewAgentOrigin`, row 91) was, because crew are not "using" their standing point (`HowdahCrewSpawner.cs:158` seats them without `UseGameObject`), so the object test cannot find them.
76. **The take is on `ISeatScreenAdapter.TryTake`, not on `ISeatHeroAdapter`.** The pointer ray, the kept company and the cached references are in the screen adapter, and the take must run in the same tick as the pick. `SeatScreenAdapter` takes an `IModLogger` for the thread guard (`new SeatScreenAdapter(MissionScreen, mission, logger)`). `ISeatHeroAdapter` holds the hand-off, the hand-back, `HeroActive`, `HeroOnAi` and `StopHeroRetreat`.
77. **`BeginFighting` refuses a hero who is already on the AI** (`reason=not-player`), beside the contract's two refusals. `LongDefeatBattlefieldAdapter.AdoptMain(aiDriven: true)` flips the hero to the AI for a bot run, and the hand-back would take him from that feature. `BeginFighting` returns the refusal (`None` means handed) and a refusal is a `fight-on` skip with `why=` and the word.
78. **Begin step 4 as built.** Alarmed watch state, AI unpaused, scripted movement disabled and the formation's riding order are set (Battle mode only). "The formation told that the unit joined" is `MovementOrder.OnUnitJoinOrLeave(formation, agent, true)` through `GetReadonlyMovementOrderReference()`, which `Formation.AddUnit` and `AttachUnit` call; it is skipped for a Retreat order, which would retreat him at once. `ResetEnemyCaches` and `SyncBehaviorParamsIfNecessary`, which vanilla's deployment hand-over calls, are not called, for the reasons in the feature doc.
79. **A throw is a `fight-on` fail and a fault.** `BeginFighting` puts the controller back before it rethrows, `EndFighting` lowers the gate in a `finally`, and the session writes `result=fail error=<type>` and latches the Seat off, as every throw in the camera path does. The fault path hands the hero back from the next mission tick, because `FightTick` runs before the early return on a fault; a hand-back that itself throws is a debt of up to three tries, with the controller written in a `finally` of the adapter (row 88).
80. **The order panel's patch is a transpiler, and its logger comes from the module.** The contract allows a prefix or a narrow transpiler. A transpiler keeps tests 742, 758 and 766-769 without copying them. `FeatureModulesTests.IoCResolver_IsReadOnlyByTheFeatureModuleHooks` forbids a patch reading the container, so `SeatOfSeeingModule.InitializeStatics` hands the logger to the core static `SeatPatchLog`, and a drifted anchor logs at error level, which fails a field run.
81. **The ladder queue is not patched.** Section 8.2 calls it "a true engine bug with a player unit on a ladder". Reading `LadderQueueManager.cs` shows the engine re-checks `IsAIControlled` for a queued agent at its next pass (`:317-319`, `:559-566`), resets the speed limit through `?.` (`:235`) and clears limits in the controller setter (`Agent.cs:1259-1262`). The remaining defect is a Player agent that stays listed as a climber until it leaves the zone, and one unguarded `HumanAIComponent` read (`:390`) that fails only if the main thread flips the agent between two lines of one call; whether that parallel tick overlaps the mission tick is UNVERIFIED. No producer of a crash was found, so there is no patch, only a limit and a `[[human]]` row. Any patch would have to hold the pair lock the engine takes (`:396-407`).
82. **The two checks' passes.** `fight-on` passes only when the log holds both `event=begin` and `event=end` (every pass pattern must match), so a stay with no hand-back reads NOT REACHED, and a hand-off whose mission ended inside the stay is not proved. `take` passes on `formations=[1-9][0-9]*`; a take that handed nothing over reads NOT REACHED, and the human row decides. The fixtures are in `tools/tests/fixtures/ingame_verdict/`, and `seat_panel_visit_pass.txt` now holds the lines of both checks, so the shared two-session test still exits 0.
83. **More than the contract names in the take.** The press is also accepted while Rising. The company's nearest soldier is measured from the kept target point. A health share that is NaN is refused as wounded. `SeatMessage` gains `HeroFell`, `TakeNobody`, `TakeRefused` and `TakeHeroAlive` (localized, seeded in 12 languages with the free path). The message at the fall is written when the main agent changes with no active main agent left, and only when the take is allowed.
84. **Both new settings are `Presentation` in `CoopSettingsRelevance`.** The hand-off and the take write the controller and the order owner, but the policy denies both in a network session, and the network fact includes a co-op session (row 90), so no value reaches a co-op simulation. The simulation-relevant count stays 331; `TaomSettings` is 483 properties and the total 518.
85. **`Patch140_RangedSiegeInput` skips the whole of `HandleUserInput`** while the hero fights on, as the contract's table says. Its other work (`HandleUserAiming`, the camera rotation for a forced use) serves a player pilot, and an AI hero has none. The skipped body also gives the weapon its aim (`HandleUserAiming` at `RangedSiegeWeaponView.cs:98` reaches `RangedSiegeWeapon.GiveInput` at `:227`) and writes `Agent.Main.IsLookDirectionLocked` (`:123`), so what keeps the skip out of a network session is the policy's `FightOn` permission, not the method writing only the view's camera. Before a skip prefix on a tick the skipped body's clears were read: the view's `_isInWeaponCameraMode` reset at `:93-97` matters only when the view is already in weapon camera mode, which the entry check refuses (`CustomCamera` must be null).
86. **The first S4 and S5 run: `hero_falls` read the fall too early.** The step lines were `main_null=1 gate=1 messages=0 message_fell=0` and the check `fight-on result=skip reason=fell` came after them. The engine clears the main agent inside the kill (`Mission.cs:3021`) and the session reacts at its next mission tick (`SeatSession.FightTick`, then `NoteTheFall`, one `OnMissionTick`), so the step's read had no mission tick in between. The Seat's code was right. Section 12's S5 row now means: after the kill the step waits for the gate to be down and a message to be said, within `SeatTrialRules.FallWaitSeconds` (3 real seconds), then judges the same facts, and adds `waited=`. The test world no longer lowers the gate in the kill's own tick (`FallReactionSeconds`, default the next mission tick), which is how the failure reproduces, and tests pin a late reaction that passes, a reaction past the limit that fails, and a gate that never drops that fails only after the limit.
87. **The fall message is pinned for a hero on the AI.** `SeatSessionFightTests` gains a test that a handed hero's fall lowers the gate and says the fall once in the same mission tick; the stand-fast case was already pinned in `SeatSessionTakeTests`. The handler that raises the message, `OnMainAgentChanged`, runs for both, because the engine raises the event for any controller when `MainAgent` becomes null.
88. **A failed hand-back is a debt that the faulted session keeps paying (corrects row 79).** Section 8.1 clears `actual` before `EndFighting` so a throw does not re-arm the end on every tick. Built so, a throw from a step before the controller write left the hero on the AI with the gate down and nothing to try again. `HandBack` now writes `Controller = Player` and what follows it in a `finally`, so the throw still reaches the session, and `SeatSession` keeps `_handBackOwed` and `_handBackTries`: `FightTick` runs before the early return on a fault, so a faulted session retries on later mission ticks, `SeatFightRule.HandBackTries` (3) in all with the first included, while the hero is active and still on the AI. The gate stays down through the retries, so a hero still on the AI can panic until a try works or the tries end (a named limit). After the last try one `fight-on` fail line is written (`reason=hand-back-failed tries=3 error=<type>`) and the retries stop. A hero who fell, or who is already the player's, ends the debt with no line, and `Finish` drops it. Tests: `SeatSessionFightTests` (the fake throws before its controller write and the test reads the controller) and `SeatFightWiringTests` (the `finally`).
89. **The boundary prefix holds only where the entry's policy allowed `FightOn`.** Section 8.2 and row 32 read `IsSeated` alone. The engine's server path (`TickForAgentAsServer`, `MissionBoundaryCrossingHandler.cs:188-190, 215-219`) calls the same handler for an agent with a `MissionPeer`, and a false `isAgentOutside` removes that agent's timer (`:98`) and the punishment behind it. `SeatPresence.Set(seated, holdsBoundary)` takes the entry's `FightOn` permission and `HoldsBoundaryCountdown` needs it, so a network session never changes the answer. A hideout, tournament or arena has no `FightOn` either, so a hero left outside the line there keeps the engine's ten second countdown while the Seat is up. Tests: `SeatFightPatchWiringTests` and `SeatSessionFightTests`, both ways.
90. **The network fact includes a co-op session (corrects row 84 and section 4's comment on `NetworkSession`).** `GameNetwork.IsSessionActive` derives from `MBCommon.CurrentGameType` and is true only for the engine's own multiplayer game types (`GameNetwork.cs:338-392`). BannerlordCoop's source never reads `CurrentGameType` (every `*.cs` in `E:\repos\BannerlordCoop` searched), and its siege patches replace the engine's `IsClientOrReplay` read with their own check (`SiegeLadderAuthorityPatches.cs:33`). So in a co-op battle the flag reads false, and the policy would have given `FightOn`, `Time` and `TakeSoldier`, including a take of a puppet the peer does not own. `SeatMissionClassifier.InNetworkSession` is now the engine's flag or `ICoopSessionProvider.IsSessionActive` (true on a host and on a client), resolved at each read as `FieldCampMapView` resolves it. `MarshalView.MayRun` takes `facts.NetworkSession` from the same read, so Marshal's Word is off in a co-op battle too. A co-op battle has not been run: UNVERIFIED in the game. Tests: `SeatNetworkSessionFactTests`.
91. **The take refuses mumak crew and a soldier of no formation.** Section 9 names crew on a moving platform and row 75 built the howdah test only. `PlatformCrew` is now `agent.Origin is HowdahCrewAgentOrigin or MumakilCrewAgentOrigin`; the mumak crew are seated by a direct `OnUse`, so the object test cannot find them either (`MumakilCrewSpawner.cs:189-190`). `SeatSoldierFacts.InFormation` adds `reason=no-formation`, which the contract does not list: the Fords take refuses a soldier of no formation too (`FordsAgentControl.CanTake`). A rider of a creature stays takeable, because the Seat's take asks for no plain mount (`FordsControlPick.MayRide`); that is a known limit.
92. **The retreat is stopped after the hand-back, and the check answers what the agent reads.** Section 8.1 End lists no retreat step. `IMBAgent.SetController` (decompiled with `native_decompile.py`) writes the controller fields and never the field that `IMBAgent.SetRetreatMode` sets, though the three helpers it calls were not decompiled. A Retreat order that reached the hero while he was on the AI (given in the gap of up to one real second before the next check, or by the re-attach of a detached hero under a Retreat order) can therefore outlive the flip, and the next hand-off would log a stop it did not make. `HandBack` calls `Agent.StopRetreating` right after the controller write when `IsRetreating()` reads true, and `StopHeroRetreat` calls it after the component's stop and answers `!IsRetreating()`. The trade-off is two conditional lines against a hero who runs after a re-entry. The once-a-second cadence is the contract's, so a hero near his retreat point or the line can fade out between two checks (the fade timer runs every 0.5 to 0.6 s, `CommonAIComponent.cs:54, 99-106`, and a fade-out cannot be undone): a named limit, UNVERIFIED in the game. Tests: `SeatFightWiringTests`.
93. **`morale_held` reads the morale back.** Section 12 says to write 0 and wait. A hero who is not on the AI has no `CommonAIComponent`, so the write does nothing and the later facts read well. The step now reads the morale at once after its write and fails with `reason=morale-not-written written=<n>` unless it lies between 0 and 0.01 (`SeatTrialRules.AtMoraleFloor`); its line prints `written=` and the final `morale=`. The stand-in has a write that takes no effect, and the step fails on it. The sixth run of the feature doc ran the step before this check.
94. **The take's skip lines do not carry every refusal reason.** Section 9's definition of done asks for a skip line for each refusal reason. `SoldierAtPointer` drops a non-human and an inactive soldier before the rule runs, so `not-human` and `not-active` never reach a line (the press answers `nobody`), and a kept company whose every unit is refused answers `nobody` with no soldier's reason. The rule still reports every reason, and `SeatTakeRuleTests` and `SeatSessionTakeTests` pin each one; the manifest's comment says which reach a log.
95. **The first-entry message no longer says the key returns to the hero (section 5.3).** An entry made after the fall has no hero to return to, and the next tick says he fell, so the two lines contradicted each other. The text is `Press the Seat key to leave the Seat.` in the code, the English row and the 12 language rows. The texts corrected in the same pass are the take refusal while the hero stands, the stand-fast hint, the fight-on and take hints and the fall message; `SeatPlayerTextTests` pins the take refusal, the first-entry text and the fall message.
96. **Engine claims in sections 8 and 9 were wrong in their detail; the behaviour they back is unchanged.** Checked against the installed decompile this round. (a) Section 8.1 End says `MoveToClear` and `IsDefending` throw after the flip. They do not throw there: the AI cleanup in `StopUsingGameObjectAux` sits behind `IsAIControlled` tests (`Agent.cs:3989, 4004, 4014, 4027`), so after the flip it would skip that cleanup; the order stays right. (b) Section 8.1 Begin says a write of 0 "panics the hero in that frame's parallel tick". The morale refusal is up by then (it rises in the same mission tick, before the agent tick) and pins him at 0.01 instead of the 50 that the new component holds (`CommonAIComponent.cs:84-93`); the no-morale-write rule stays right. (c) Section 8.1's thread paragraph calls `BattleEndLogic:300` the one engine read of the main agent's controller outside the Seat's targets. `MissionMainAgentController.cs:226` (no input read for an AI hero) and `MissionScreen.cs:1622, 1938, 2554` (the followed agent's controller, for the camera) read it too. (d) Section 9's Tactics share cites `BattleAgentLogic` 229 and 233 and 132-181. The installed file ends at line 211: `BattleAgentLogic.cs:159` takes the leader's hero, `:163` passes him to `OnCombatHit` as the commander, `DefaultSkillLevelingManager.cs:88-90` gives the share to his party, and wounds, deaths and routs flow through `Agent.Origin` (`BattleAgentLogic.cs:94-125`) with experience through `Agent.Origin` and `Agent.Character` (`:52, 149-180`). The feature doc quotes the installed lines.
97. **The classifier takes the co-op provider as a parameter (corrects row 90).** Row 90's `ReadFacts` resolved `ICoopSessionProvider` through `IoC` inside a static helper. `SeatView` and `MarshalView` now resolve the provider once in `OnMissionScreenInitialize`, hold the provider and not its answer (the provider is read live at each decision), and pass it to `ReadFacts(Mission, ICoopSessionProvider, IModLogger)` and `ReadFacts(Mission, ICoopSessionProvider, bool)`; `InNetworkSession` takes a provider that is never null. A failed resolve lands in the view's could-not-start catch, where the old shape faulted the Seat at the first key press. Tests: `SeatNetworkSessionFactTests` (source pins on the classifier and both views; the test of a null provider is gone) and `MarshalsWordWiringTests`.
98. **`back_to_stand` stops the company and reads the hero before it leaves (corrects section 12's S4 row).** The ninth run (`20261104-184455`) read `speed=2.0` after the sixth run's `speed=0.0` with the same step, and the company still held the Charge order of `fight_on`, so the line could not tell a hero who walks on after the hand-back from one carried by his charging company. The step now gives the company the Stop order from the mission tick, waits 2 s, reads the hero's speed on the AI (`before=`) and fails with `reason=ground-not-quiet` without leaving when it is not under the limit. The limit of 0.5 m/s is unchanged. The cause is not proved: the next run decides, and a failure with `before=0.0` is a defect of the hand-back. Tests: `CommandTrialFightStepsTests` (the stop, the `before=` fact, a company that ignores the stop, a hero who walks only after the hand-back).
99. **`take_soldier` takes through a kept company, and `hero_falls` makes the keep (corrects section 12's S5 rows).** The tenth run (`20261104-184608`) skipped the take with `reason=nobody`: `SeatScreenAdapter.TryTake` answers `Nobody` only when the pointer ray at the screen centre found no soldier and no refusal was set (the platform and no-formation refusals answer `Refused`), so the aim decided the result. `hero_falls` now keeps the view to the largest company before the kill (select, open the panel, press the keep key, close the panel as the player does; `kept=Company` on its line), because the panel cannot open after the fall. `take_soldier` fails with `reason=no-kept-company` when the keep is gone, and otherwise presses the take key with no aim, so the take picks the nearest eligible unit of the kept company. `TookChosen` became `TookFromKeptCompany`. The pointer path of the take is proved by unit tests and the existing `[[human]]` row only. Tests: `CommandTrialFightStepsTests`.
100. **The begin path carries no hand-back debt.** If the controller write of `BeginFighting` throws, the recovery runs the same write while `actual` is still false; a second failure of that setter leaves the hero on the AI with the gate up and nothing owed. The session writes `fight-on result=fail` and latches off. No producer of two failures in a row is known, so this is a named limit and not code (feature doc, Known limits).

Stages S6 and S7 and their field trial steps, built 2026-11-04. Each row is a difference from the contract above, with the reason.

101. **Engine line numbers of sections 10 and 11 moved in the installed decompile; the behaviour is the same.** Read again this round: `Mission.cs:1268-1279` (the flag's setter writes a native byte), `:2025` (the offset), `:2070-2098`, `:2100-2103` and `:2115-2125` (the request list, its add and its remove that throws), `:3746-3749` (the camera takes real time) and `:6407-6414` (the take clears the flag) are as cited, and `MissionState.cs:151-158` (the pause and the fixed step), `:159-164` (the time speed, off in a session) and `:171-198` (the fast forward loop) too. Moved: the screen tick that runs under a pause is `MissionScreen.cs:519-557` (the contract says 595-600, which is now a different method); the offset is added to the target at `:1481-1483` and the target and look-at point are raised at `:1513-1514` (the contract says 2226-2228 and 2415-2483); the target lock tests `IsThereAnyCustomCameraAddition` at `MissionMainAgentController.cs:592` and `:868-876` (the contract says 756, 1190 and 1300); the key reads that fast forward would repeat are in `OnPreMissionTick` at `:203` (the contract says 268 and 811, which are now `LookTick` and another branch); `FordsGameSpeed.cs:35` holds the 0.1 s cap (the contract says 37). The cast's behaviour under a lifted target (the contract's "2378-2382, 2447") was not re-read, and the feature doc marks it UNVERIFIED.
102. **The lift's rules are in `Domain/`, not in `SeatCameraMath`.** `SeatPanelLift` holds `PanelProgress`, `PanelLiftAmount`, `Lift` and `HeightFor`, for the reason of row 42: they need no engine type, so they sit under the Domain gate. `SeatCameraMath` is unchanged. `Lift` and `HeightFor` are added to the two functions the contract names, so the exact zero and the heights have a test of their own.
103. **Mission time and the lift live in two classes beside the session.** `SeatTimeControl` and `SeatRiseControl`, held by `SeatSession`, keep the 791-line session from growing; the session gains `PressTime`, `OnFrameTick`, `TimeLevel` and `ResetTime` (the trial's release after a failed step), and calls `Cancel` and `Zero` from `Finish`, `Fault`, `Leave`, `Enter` and the take. `SeatKeys.Tick` takes the frame's `dt`, tests the three time keys first in every state, and calls `OnFrameTick`; `SeatView` changes by one argument. The pins in `SeatPanelWiringTests` that quoted the old `Tick` follow.
104. **The members of `ISeatScreenAdapter` for stages S6 and S7.** `ReadTime` (a `SeatTimeState` of the fast forward flag, the fixed step, the pause and whether the Seat's request is held), `ApplyFastTime`, `StopFastTime`, `AddSlowRequest`, `RemoveSlowRequest`, `SetPaused`, `ClockOwnedElsewhere`, `ReadRise`, `LiftHeight`, `SetPanelLift`, and `Say(message, argument)` for the level in the message. The only legal write of the request is in `SeatTimeControl.Start` and `ClearRequest`, over the primitives, so the stand-in can throw where the engine throws; the adapter holds none of the decision.
105. **Long Defeat writes the same clock every frame, so the Seat holds the ladder at 1 there.** The contract's table gives Time to a Long Defeat battle. `LongDefeatHudView.cs:67` calls `LongDefeatMissionSpeed.Apply` on every screen tick, and `LongDefeatMissionSpeed.cs:31-41` writes the fast forward flag and the fixed step off at speed 1 too, so a fixed step or fast forward of the Seat would be written back at once, in an order that depends on the views. `ClockOwnedElsewhere` is the presence of `LongDefeatMissionLogic` in the mission. The ceiling is then level 1: the quicken key shows `TimeOtherClock` and writes a `time` skip with `reason=other-clock`; slow time and the pause work, because Long Defeat writes no request and no pause. The cleanliness test of a teardown ignores the flag and the fixed step in such a mission. Fords of War's own panel needs no rule: its camera makes `ForeignCamera` true and the policy gives the Seat nothing there. A new `SeatMessage` value and a localized row came with it.
106. **The level after a pause, a take and a leave.** A pause cancels fast time first and so leaves level 1 (it does not restore the old level); a take at 2, 4 or 10 sets level 1 and stops the fixed step, and a take at a slow level changes nothing; a leave at 10 with a living hero steps down to 4 and shows the level. The contract states the first and the third and leaves the second to "the per-frame rule follows it", which a level of 2 or 4 would not.
107. **A cancel that leaves time on is a debt.** `Cancel` reads the state after its three steps; a state that reads on (a pause, the request, or fast forward or the fixed step unless another view owns the clock) is retried on later screen ticks, three tries in all with the first included, and then one `time` fail line (`reason=cancel-failed tries=3`) is written. A teardown cannot reach a later tick after `Finish`, so only a fault pays its debt. A mission where no time key was pressed writes nothing at its end (`Cancel` is gated on a key having acted), so the vanilla behaviour of "no key, no change" holds to the last frame.
108. **Presses are not stored.** The time keys call the session from the screen tick and act there, as the score screen's key and the order panel's time request do, so the rule that a stored press is tested at the press and at the tick has nothing to test. The gate for photo mode, which the other Seat keys drop, does not apply: time is not a Seat view effect, and the pause is the engine's own flag. The mode test is `Mission.Mode` Battle through `ReadFacts().BattleMode`, read before any setting.
109. **`SeatRiseWithPanel` is a new setting; the Seat's own hint changed.** Order 6 of the group, `Presentation` in `CoopSettingsRelevance`, default off. The simulation-relevant count stays 331; `TaomSettings` is 485 properties and the total 520 (`ServerSettingsSchemaTests`, `SettingsFingerprintTests`, `coop-interop.md` and `bannerlord-together-compat.md` moved with it). The English of `taom_seat_mcm_enabled_hint` gained a clause, as the contract asks of the Seat setting's hint; no translation or cache entry existed for it (the 12 rows were the English seed), so the 13 rows were changed together and no stale translation was left. Six new key rows, four message rows and two setting rows were seeded in the 12 languages with the free path.
110. **The lift does not stop after a fault, and it is zeroed in three places.** The contract says the member does not sit behind the session's fault return, and also says to zero it on a fault. `SeatSession.OnFrameTick` runs ahead of any fault return, a fault writes the exact zero through `Zero`, and the next open edge lifts again; a panel that stays open lifts again from zero over half a second. A throw inside the lift sets `_riseBroken`, so a deterministic throw is not repeated every frame, and faults the Seat. `Enter` also zeroes (the contract's "when the Seat is taken"), and `Finish` writes nothing. A view in the Seat counts as a closed panel in the lift's own test.
111. **Where the S6 and S7 rows sit.** After `back_to_stand` and before `hero_falls`, not after the S5 rows. `TheKillSteps_AreTheLastSeatRows_SoAnEarlierSeatRowNeverMeetsADeadBattle` keeps the kill steps last, and a row after them would meet a dead hero. `sink_on_close` takes the Seat again after it reads its facts, because the steps that follow it (`hero_falls`) start from a Seat that is up and its step passes only with the state Off. The stand-in's hero camera now includes the hero's drift, so the glide in a whole-table run ends near the hero.
112. **What each new step reads, where the contract left the form open.** `pause_flies` reads the pause flag, the mission clock over 1.5 real seconds (under 0.05 s) and the flight against the Pan speed, and presses Stay time again to resume. `slow` fails with `reason=other-request` when one of the engine's four ids (813, 624, 864, 1121) is held, because an open order panel holds a quarter request on the default option, and reads the ratio against 0.5 within a quarter. `fast` measures 2, 4 and 10 one after the other, taking the mean real frame time from the step's own screen ticks, and passes 2 and 4 against `min(level, 0.1 / mean frame time)` within a quarter and 10 against more than 5. `speed_cleared` builds the states it then reads (fast forward at 10, the fixed step at 4, the request at 0.5) so a flag that never rose reads as a failed control, uses the pause as the key that cancels fast time, and ends at level 1 with nothing on. `rise_with_panel` measures the camera's Z against its own value before the panel opens, after the Seat has been left and the glide has rested, and `sink_on_close` reads the offset on all three axes for an exact zero. A failed step calls `ReleaseTime`, so the next step starts from level 1.
113. **`CommandTrialSettings` forces `SeatRiseWithPanel` on**, as row 47 expected once the setting existed. The saved value does not matter in a run, and the MCM file is never written.
114. **The follow-up of the thread guard is built.** A Python test (`SourceTieTests.test_every_thread_guard_site_of_the_seat_is_one_the_judge_reads`) reads every `NoteCall` and `NoteWrite` site of the Seat's adapters and the trial host out of the code and requires `SEAT_ERROR` to match each, with known sites as the positive control; stages S6 and S7 added five sites of `SeatScreenAdapter`, which was the condition the feature doc set for building it. `SEAT_ERROR` itself is unchanged.
115. **Section 12 named the Long Defeat spike, which no longer exists in this tree.** The contract's shapes are now `tools/run_long_defeat.py` for the wrapper, `LongDefeatAutorunPlan` for the arm file parser (the environment variable `TAOM_LONGDEFEAT_AUTORUN` names the file) and `LongDefeatMissionLauncher` for the launcher that refuses an unknown id and returns false after one error line. The four lines that said `tools/run_long_defeat_spike.py`, `LongDefeatSpikeOptions` and `LongDefeatSpikeLauncher` now name them. The wrapper itself still copies the Fords runner, as before.

The review of stages S6 and S7, applied 2026-11-04. Each row is a difference from the contract above or from an earlier row, with the reason.

116. **Another writer's fast forward makes the Seat give up its request and fixed step (corrects section 10's per-frame rule).** The contract says a raised flag at level 2 or 4 sets the stored level to 10 and writes nothing, and a raised flag at level 1 or below only sets level 10. `MissionState.TickMission` takes the fixed step as the frame (`MissionState.cs:155-158`), multiplies the time speed request into it (`:159-164`) and runs nine more steps of that in fast forward (`:173`) before the frame itself (`:196`). A step or a request left in place therefore multiplies the other writer's fast forward: level 4 at 60 frames a second ran about 40 times, level 2 about 20, and a half-speed request ran it at 5. A request left behind also outlived the flag's fall, so the stored level read 1 while the battle ran at 0.5. `SeatTimeLadder.Frame` now returns `SeatTimeAction.Adopt` from a slow level or the fixed step. The controller clears the request and, from the fixed step, writes level 10 through `ApplyFastTime`, which sets the flag only when it differs (`FordsMissionSpeed.Set`), so their flag stays and the step goes off. The stop is not used, because it would clear their flag. The same runs in `Reconcile`, ahead of a key's step. Tests: `SeatSessionTimeTests` (a flag raised at 4 stops the step and keeps the flag, a flag raised at 0.5 clears the request and nothing is held when the flag falls, and the same through a key) and the rows of `SeatTimeLadderTests`.
117. **The clock-owner rule covers every read of the flag (corrects row 105).** Row 105 gated the keys only, so under the Long Defeat HUD's own 10x the per-frame rule and a key step read the HUD's flag as the Seat's level 10: a Slow press said 1x and the next frame read 10 again, and with the hero fighting on a leave acted on a level the Seat never set. `SeatTimeControl` now ignores the flag while `ClockOwnedElsewhere` holds, in the frame and in a key step, so the stored level stays at 1 or below and a leave or a take at that level does nothing. The lookup runs after the first key and only while the flag is up. Row 105's sentence that slow time and the pause work is true again, with one addition: the Seat's half-speed request multiplies into that view's own speed (`MissionState.cs:159-164`), so at the HUD's 10x a half reads about 5 times.
118. **A pause is resumed in any mode (corrects section 10's mode test).** The mode test came first, so a pause that landed just before a change to a cut scene (the hideout ambush changes the mode in a tick that runs at a delta of 0, `Mission.cs:3751-3754` and `HideoutAmbushMissionController.cs:616-628`) left the battle stopped with only Escape to end it. Stay now resumes a held pause before the mode test, whatever the setting and the permission. A new pause still needs Battle mode. Test: `Stay_OutsideBattleMode_ResumesAPause_ButNeverStartsOne`.
119. **The `fast` step expects the sum of each frame's capped step, and reads the fixed step as a fact (corrects section 12's S6 row and row 112).** The contract's `min(level, 0.1 / mean frame time)` caps the average, and the engine caps each frame (`FordsGameSpeed.cs:35`), so uneven frames read a different rate: frames of 5 ms and 45 ms at level 4 give 2.4 where the old rule expected 4, and at steady 100 ms frames a clock still at normal speed passed at 2 and at 4. `MeasureRatio` now adds `min(level x frame, 0.1 s)` for each screen tick of its window, between its two clock reads, and divides by the window; `FastRatio` compares with that. When the expected ratio is within a quarter of 1 (`CanTellFromNormal`) the step fails with `reason=level-indistinguishable` instead of passing. The line also carries `expected`, `fixed_step` for each level and `fast_forward10`, and the step passes only with the fixed step on at 2 and 4, off at 10, and fast forward on at 10, the state the ratio alone could not show. The stand-in's clock multiplies the fixed step into the fast forward as the engine does. The one frame between the write of a step and the mission tick that reads it is not modelled; it moves the window by at most one frame. Pure rules: `FixedStepSeconds`, `CanTellFromNormal` and `FastRatio`.
120. **Texts changed with the fixes, in 14 places each (corrects row 109 and the hints section 10 names).** The Seat's enable hint now says the next press of a time key cancels a change, because the setting is read only at a press (section 10 says the hint says so, and it did not). The Quicken key's description says level 10 needs the Seat up or nobody left for you to fight as, because the ceiling tests the main agent and not the hero (`SeatHeroAdapter.cs:69-73`), and after a take the soldier is the main agent. The pause message names its key: Stay time (Home unless rebound). The rise hint says the lift works with the Seat turned off too, because `SeatRiseControl` reads its own setting and never `SeatEnabled`, and section 11 names no other gate. Each row holds the seeded English in the 12 languages, so none was translated, and the paid translator was not run. `SeatPlayerTextTests` pins the literal, the English row and each language row, and a new data-driven test reads the six rows of keys 533 to 535 in `global_strings.xml` and in each keybind language file.
121. **The Seat's take stops the fixed step; the vanilla take does not (narrows row 106 and section 10).** `Mission.TakeControlOfAgent` clears the flag only (`Mission.cs:6407-6414`). The Seat's own take calls `SeatTimeControl.OnTake`, which stops the fixed step. The vanilla Kick take does not reach it, so a level of 2 or 4 survives that take, which is legal because 4 is allowed with a living main agent.
122. **The cancel's retries need a later tick, so a mission's end writes no fail line (narrows row 107).** `SeatView.EndSession` drops the session right after `Finish`, so no `OnFrameTick` follows and the debt is never paid there. A fault, or a time key pressed with the Seat off, has later ticks and pays it. The manifest comment and the feature doc say so. Two tests that ran `Finish` and then ten frames now run a fault, and one test states the mission-end case.
123. **The pause's writers (corrects section 10).** Section 10 says vanilla reaches the flag by a cheat key and the error report only. The managed writers are the cheat key (`MissionScreen.cs:2191`), the error report mode (`:527`) and a false when the screen starts (`:357`). The native `PauseMission` callback (`Mission.cs:6895-6898`) has a caller that was not read. The native and sound behaviour under the flag stays owed to the game.
124. **Engine cites and the career key (corrects section 10 and row 101).** Request id 813 is at `MissionGauntletMainAgentControlModeView.cs:18, 228, 239`, not `:299`. The contract's reads at `MissionMainAgentController.cs:268, 811` are `LookTick` (`:262`) and `HandleRangedWeaponAttackAlternativeAiming` (`:800`), and `OnPreMissionTick:226-230` runs `ControlTick` only while the controller is enabled and the hero is not on the AI, which keeps those repeats out in the Seat. The career ability key has no such gate: `CareerPerkMissionBehavior.OnMissionTick` runs `AbilityActivationController.Tick` in each repeat of level 10, so a V press that casts can be followed in the same frame by a false "still charging" message. That is a known limit in the feature doc and a clause of a human row, with no code change, because only a message is wrong.
125. **The score screen's F key and the Seat's level 10.** The key flips the view model's own bool (`MissionGauntletBattleScore.cs:178`) and then writes it to the mission (`SPScoreboardVM.cs:268`, `CustomBattleScoreboardVM.cs:90`), so after the Seat's own 10 the first F writes true and changes nothing. The manifest said F "still toggles"; its row now says this, and that the effect in the game is unverified. Turning the Seat off while at level 10 in the Seat steps down to 4 and says so, because the setting is read at a press: a named limit.
126. **Hideout Time (corrects section 4's table).** The table gives a hideout Time yes. A regular hideout sets Stealth on its first tick (`HideoutMissionController.cs:454`) and only saves and restores the mode afterwards (`:548, :700`), so the keys, which act in Battle mode only, are silent there. Only the ambush sets Battle (`HideoutAmbushMissionController.cs:574, 755`). The policy stays as it is, and the keys' mode test decides.
127. **Native fast forward is one global byte (confirms section 10).** `python tools/native_decompile.py --engine-method OnFastForwardStateChanged` shows `IMBMission_OnFastForwardStateChanged` at 0x6F9E90 storing its flag argument in `DAT_180db2a21` and ignoring the mission pointer. The teardown cancel and the name of `Finish_AtTen_ClearsTheProcessGlobalFlag` stay.
128. **Tooling and docs.** The wrapper's help and three docs say to prove each stage by its subset, because the 24 rows have not run in one battle. `--live-limit` is in real seconds, which differ tenfold from mission seconds at level 10. The thread guard's line for the Seat's two adapters is also in the manifest's `enter` fail list, so the manifest and the trial judge agree. The S6 and S7 human rows have a fragment test, and a row for the Options screen was added. Test names that counted twelve checks no longer count. One test that repeated the order of the checks was deleted: `test_it_names_the_checks_the_session_writes` pins the first fourteen ids in order, and `test_the_seats_checks_come_before_marshals_word_and_its_rows_stay_in_order` pins the Seat before Marshal's Word. The collision test of key ids lists keys 533 to 535.
129. **Run history.** Run `20261104-220808` passed `enter` and the six steps of S6 and S7 (the feature doc's thirteenth run). It ran the code before the fixes of rows 116 to 119.

The second review round of stages S6 and S7, applied 2026-11-05. Each row is a difference from the contract above or from an earlier row, with the reason.

130. **A held pause is resumed whatever the setting and the permission, and with the Seat off the resume also cancels (corrects section 10's gate sentence and row 118).** Section 10 says that `SeatEnabled` off stops keys 533 to 535 and cancels any level or pause at the next press. Row 118 let Stay time resume a held pause ahead of the mode test, and the code resumed it ahead of the setting and the permission as well, so with the Seat off the press ended the pause and left the Seat's half-speed request on, with no `setting-off` line, against the setting's hint. `SeatTimeControl.Press` still resumes first (`Stay_WithoutThePermission_StillResumes`), and with the setting off it then runs the same cancel as every other press (`Stay_WithTheSeatOff_ResumesAndCancelsTheRequest`). It writes the resume line and no `setting-off` skip, because the press did resume a pause. A new pause still needs Battle mode, the setting and the permission. The feature doc's gate sentences and human step 32, and the manifest's claim for the setting-off case, say so.

131. **A cancel's debt is not a level the keys set (corrects row 107).** After a cancel that left time on, the controller kept its "a key acted" flag set so that the per-frame rule ran during the retries. That rule read a flag the cancel could not clear as another writer's fast forward and raised the stored level to 10 while the Seat was cancelling, and a leave or a take in that state could start level 4 or show a message on a clock the Seat had just given up. A cancel now clears the flag whatever it found, and the debt alone drives the retries (`ACancelThatCannotStopFastTime_KeepsTheStoredLevelAtOne_WhileItRetries`). The rest of row 107 stands.

132. **The `fast` step judges fast forward at every level and prints its facts by level (corrects rows 112 and 119).** At 2 and 4 the step now fails when fast forward reads on, as well as when the fixed step reads off, because a flag raised at those levels gives a ratio that the state facts must explain; at 10 it fails when the fixed step reads on or fast forward reads off. The line carries `ratio`, `fixed_step` and `fast_forward` for 2, 4 and 10, and `expected` and `frame_ms` for 2 and 4 only. `SeatTrialRules.FixedStepSeconds` asks `FordsGameSpeed` for the step, so levels 1 and 10 expect none (the first version printed `expected10` of about 6 at 60 frames a second beside a `ratio10` of 10, a number nothing read). `CanTellFromNormal` is also true below 0.8, which is a frame over 125 ms, because a clock at normal speed then misses the expected ratio; the cap makes 2 and 4 read as normal speed for frames of about 75 to 125 ms, and the feature doc says that range, not "75 ms or longer". Tests: `Fast_WhenFastForwardReadsOnAtAFixedStepLevel_FailsOnThatLevelsFact` and the rows of `SeatTrialTimeRulesTests`.

133. **A trial step for another writer's fast forward, `adopt_flag` (adds to section 12's S6 row).** Row 116's fix had no proof in a real battle: the stand-in's time keys never ran the controller, and `fast` at 10 has only a floor, so a 40 times clock would have passed its ratio. At 4, and again at 0.5, the step raises the engine's fast forward flag from the mission tick through `Mission.SetFastForwardingFromUI` as the score screen's key does, waits up to one real second (`SeatTrialRules.AdoptWaitSeconds`) for the Seat to give up its fixed step or request, measures the ratio over two real seconds, lowers the flag from the mission tick, and waits up to the same limit for the Seat's level to read 1. It passes when the fixed step and the request read off, their flag still reads on, the ratio is 10 within a quarter (`SeatTrialRules.AdoptedRatio`) and the level reads 1 after the flag falls. The bound follows the engine: `MissionState.TickMission` takes the fixed step as the frame (`MissionState.cs:155-158`), multiplies the time speed request into it (`:159-164`) and runs nine more steps of it in fast forward (`:173`) before the frame itself (`:196`), so the ratio with nothing of the Seat's left is 10 at any frame time, a fixed step of 4 left on reads 40 at 60 frames a second, a step of 2 reads 20 and a half-speed request reads 5. The row sits after `fast` and has its own subset, `--steps enter,adopt_flag`. The host gains one member, `SetOtherFastForward`, which the marker gate classes as a writer, so a step must yield `Verdict.InMissionTick` before it; the test that lists the writers of the flag lists the host's file beside the Seat's adapter, the Fords speed writer and the Long Defeat one. The stand-in applies the answer of `SeatTimeLadder.Frame` to its own clock, so the rule under test is the real one, and its `Adoption` knob breaks that answer four ways (the Seat ignores the flag, the state still reads the step, the state still reads the request, the level stays at 10), each with a test that fails on its own fact. The step has not run in the game.

134. **Adopt means one thing at every level, and the flag has one reader (changes row 116).** `SeatTimeLadder.Frame` now returns `Adopt` for a raised flag from every level but 10, level 1 included, where it used to store 10 and return nothing. `Adopt` always clears the request and writes level 10, which at level 1 is an idle write: the flag already reads up, so only the fixed step and its time are written, both off. A key step and the per-frame rule read the flag through one method, and a take and a pause drop fast time through one (level 1 above it, slow time as it was; `AfterTake` is now `WithoutFastTime`). The ladder test row for level 1 with the flag up reads `Adopt`. The stand-in records one more `apply:10` at level 1, which no test counts.

135. **Pins and small changes.** The two writes that make level 10 an adoption and not a stop are pinned by source text: `FordsMissionSpeed` writes the flag only when it differs and the fixed step on every call (`TheFordsSpeedWriter_WritesTheFlagOnlyWhenItDiffers_AndTheFixedStepEveryCall`), and `FordsGameSpeedTests` reads level 10 at a frame time of 0, the value the Seat passes. The screen adapter keeps its thread-guard report delegate in a field, because `_logger.LogError` as a method group allocates a delegate on every call and `ApplyFastTime` runs every frame at 2 and 4 (`TryTake` runs once per take and keeps its literal). `StopFastTime` no longer sets the Fords level to 1, a write nothing read. `SeatRiseRead` moved to the lift's file. `RatioMatches` is the one definition of "matches within a quarter" for the slow, fast and normal-speed rules. `EngineRequestIds` has a test of its four values, the clock owner's lookup has a test that it runs only after the first key and only while the flag is up, and the failing `fast` step has an assert that the driver's release left level 1 and nothing on. The two Seat presses that leave the Seat and take it again share `LeaveIfUp` and `TakeTheSeatAgain` with `ReEnter`, whose failure text for a press not taken now ends in `state=Off`. Comments off the keep-list were deleted, and a `using` replaces the full namespace of the Long Defeat logic.

136. **Counts and the Performance section.** The feature doc and the wrapper's help said "twenty-four steps" and "the 24 rows", and a count the code does not compute goes stale with the next row, so the counts were removed and not raised. The doc said 28 numbered steps where its list has 36, and now says 36. The Performance section says what the time read costs (the flag, the fixed-step flag, the pause and a walk of the mission's time requests, a `GameKey` read and a static test for each key) and that the first paragraph's claim of no mission fact read holds only before stages S6 and S7.

137. **The runs that closed S6 and S7, 2026-11-05.** Run `20261105-003400` passed `enter`, the steps of S6 and S7 and `adopt_flag`, which read a ratio of 10.0 with no fixed step and no request after the flag rose at 4 and at 0.5. Run `20261105-003513` passed all 25 Seat rows in one battle, so the sentence of row 128 now reads that the rows ran together once. Each stage is still proved by its subset first.
