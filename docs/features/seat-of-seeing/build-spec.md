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
walks and logs on every call. So S4 splits it: `SeatMissionClassifier` gains a public `ForeignCameraOwns(Mission, IModLogger)` beside `ReadFacts(Mission, bool foreignCamera)`, the view caches
only the bool from the walk, built at the first ask, and `Camera`'s result stays as it is. `ReadFacts(Mission, IModLogger)` stays as a one-line call of the two, because Marshal's Word calls it.

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

`SeatMissionClassifier` in `Client/Features/SeatOfSeeing/` has one entry until S4 splits it as above, `ReadFacts(Mission, IModLogger)`, which returns
`SeatMissionFacts`. `SeatView` calls it, and the later feature Marshal's Word will. It is a static helper, so its caller passes the logger. It turns a live mission into `SeatMissionKind`
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
the walk mode control (game key 30), the career ability key (`V`, `AbilityInputAdapter`), and the engine's perspective toggle (game key 27, in `HandleUserInput`). Each needs a deliberate
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
  (`EntryPointLineCountGateTests`) with one call per screen tick. It tests key 536 only while the state is Seated, and keys 531 and 532 only
  while the state is not Off, so a player who never enters still pays one key test a frame. S6's time keys are the only exception (section 10).
  `PressSeatKey`, `TrialInput`, `State` and the read-only trial members stay on the view.
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
`ISeatScreenAdapter.SetCursor(bool)` adds the layer on the first show, and removes it on false, in a try, because a removal during teardown can meet a screen that
is going away. Read how `BattleActionBarMissionView` and `StagedSiegeMissionView` handle their layers first. The session runs a pure rule, and the shared cleanup
(5.3) calls `SetCursor(false)`:

```csharp
internal static class SeatCursorRule
{
    internal static bool Show(bool seated, bool panelOpen, bool lookHeld, bool settingOn)
        => seated && panelOpen && settingOn && !lookHeld;
}
```

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

The reopen runs after the session's own tick on that frame and only if the state is still Seated, so a conversation that closes the panel cannot reopen it over the
interlude. A pure `SeatPanelRule` holds this, with one test per close route. The reopen needs one reflection site for the view model
(`GauntletOrderUIHandler._dataSource`, protected), gated as in 5.4, and a call to the public `MissionOrderVM.OpenToggleOrder(false, false)`. A reopen that fails with
no main agent returns silently at line 716 and raises nothing, so it cannot loop. `MissionOrderVM.OnTroopFormationSelected` closes and opens in one call (905-912), so
the field shows no edge from one frame to the next.

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
| `MissionGauntletMainAgentControlModeView.IsMainAgentAvailable` (private) | Postfix false while seated | Key 30 (CapsLock) opens the walk mode control |
| `MissionGauntletMainAgentEquipmentControllerView.DropWeaponTick(float)`, `EquipWeaponTick(float)` (private) | Prefix, skip while seated, each classified in `CoopVetoClassificationTests` | Keys 13 and 22 act on the main agent, so the drop key would drop a weapon from the air. Do not patch this view's `IsMainAgentAvailable`: its body is 17 IL bytes (`return Agent.Main?.IsActive() ?? false;`), a JIT inlining candidate that a patch might never reach (`docs/features/hidden-hosts.md:609` chose the longer method for the same reason), and its false branch (130) leaves a radial registered |

After this stage two of the readers in 5.5 stay live, the career key and game key 27. Update the 5.5 paragraph's claim in the feature doc and turn the matching line of
`tools/ingame_checks/0.toml` into a `[[human]]` row that presses each closed key from the Seat (13, 22, 30, 31, 34) and expects nothing, because only a press proves that a
private patch is reached.

Key 536 hides the whole interface, not only the bars. `MBDebug.DisableAllUI` backs the engine's own `toggle_ui` command (MBDebug:69), and `TwoDimensionEnginePlatform` (155, 199)
stops every Gauntlet draw under it, so the code answers the old question of whether it still works; the trial's two pictures prove the effect. The key is read only while the
state is Seated, through one adapter member, because no other TAOM code writes this flag. The session restores it only if its own hide is in force, to the value it saved, so a
player who ran `toggle_ui` before entering keeps that choice. It restores at once when the order panel opens (`Mission.IsOrderMenuOpen`), a dialog shows
(`GetDisplayDialog`, read through the adapter) or a layer other than the scene layer takes focus (`ScreenManager.FocusedLayer`), each read once a frame only while hidden, because an
invisible panel still slows time and takes clicks. The shared cleanup restores it too, so a leave, a fault and `Finish` all reach it.

### 6.5 Definition of done for S2

- Pure tests first: `SeatCursorRule`, `SeatPanelRule`, the pointer function, the hide rule.
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
`MissionFormationTargetSelectionHandler` uses (176, 181), which needs no ground probe and stays under a gatehouse roof. The orbit gives a pose like any other: the boundary clamp, `CapHeight` and
`AboveGround` apply in the same order as 5.4, and a non-finite target position keeps the last one. The stored flight follows the pose each frame, so letting go continues from where the view is, with no jump.

**Who holds the target.** `SeatScreenAdapter` holds the kept `Agent` or `Formation`, the adapter's second cached reference on the shape of `_controller`, because `Agent` is sealed and the session runs over
fakes (ADR-007). `ISeatScreenAdapter` exposes one read that returns the position and a gone flag. The test is occupant first (`AgentSlotIdentity.IsCurrentOccupant`), then `IsActive()`, because the first says
nothing about being alive (AgentSlotIdentity:19-26). Never key on `Agent.Index`. A gone soldier lets go with one message. A company with `Formation.CountOfUnits` at 0 lets go with the same message, because
`Formation.CurrentPosition` stays finite with no units (552-568) and an empty `Formation` is never removed. The shared cleanup (5.3) clears the target.

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
   TAOM's `LongDefeatSpikeLauncher.cs:52`. So a postfix on `Mission.CanTakeControlOfAgent` (6393) in `Patch140_SeatOfSeeing` answers false while seated, and key 532 is the only way out of the sky. Outside the Seat those missions keep vanilla's key.
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
`tools/run_long_defeat_spike.py` and `docs/reference/unattended-game-runs.md`: same permission, same gate, same launch recipe. Each
later stage, in this contract and in Marshal's Word, adds its steps and its evidence lines.

**Arming.** Environment variable `TAOM_COMMAND_TRIAL` holds the absolute path of an arm file (`key=value`, LF), with no file fallback.
An unarmed session costs one null check per application tick and one per mission tick. Keys: `out` (required, absolute folder), `quit`,
`scene` (default `battle_terrain_a`), `live_limit_seconds` (default 420), `steps` (a comma list, default all).

**Files**, all under `Client/Features/SeatOfSeeing/Trial/`:

| Type | Job | Shape to read |
|---|---|---|
| `CommandTrialOptions` | Parses the arm file | `LongDefeatSpikeOptions` |
| `CommandTrialAutostart` | Waits for 4 s at the main menu, hides inquiries, starts the game. One field and one tick line in `Client/ClientSubModule.cs` | `LongDefeatSpikeAutostart` |
| `CommandTrialGameManager` | A `CustomGameManager` that opens the mission in `OnLoadFinished`, and goes back to the menu on a failure | `LongDefeatGameManager` |
| `CommandTrialScript` | The steps. `SeatView` holds it in one nullable field, built only when armed, and calls it from `OnMissionTick` and `OnMissionScreenTick` | Pure step order in `Main/Features/SeatOfSeeing/Domain/`, with a unit test |
| `CommandTrialSettings` | An `ISeatSettingsProvider` wrapper the session gets when armed (S2 onward) | "Settings in a run" below |

**The mission** is the real thing: `BannerlordMissions.OpenCustomBattleMission(scene, player, playerParty, enemyParty, isPlayerGeneral: true, null, "", "", 12f)`
(BannerlordMissions.cs:109). The player side defends. `CustomBattleCombatant.AddCharacter` fills the two parties.

| Side | Culture | Troops |
|---|---|---|
| Player | `gondor`, hero `lord_1_9` | 40 `fow_gondor_militia_spearman`, 40 `fow_gondor_ano_archer_militia`, 20 of a mounted `fow_` troop from `ModuleData` (find one, and name it in the feature doc) |
| Enemy | `mordor` | 120 `fow_mordor_orc_fighter`, 30 `fow_mordor_orc_hunter` |

Refuse to start on an unknown id, as `LongDefeatSpikeLauncher` does, so that a bad id fails loudly.

**Deployment.** `Mission.Mode` starts at Deployment. Five seconds in, call `Mission.GetMissionBehavior<DeploymentHandler>().FinishDeployment()` from `OnMissionScreenTick`. It removes
two behaviours (DeploymentMissionController:80, BattleDeploymentMissionController:51-55), so it must never run inside `OnMissionTick` (`docs/reference/engine/mission-and-missionbehavior-lifecycle.md`). This also proves that the Seat refuses during Deployment:
a press there leaves the state Off, and the step's own line says so. Deployment writes no `[Check]` refusal (5.10). The direct call skips `OrderOfBattleVM.ExecuteBeginMission`, the route a player takes, so the banner handlers that method removes (OrderOfBattleVM:2059-2063) stay subscribed. A banner bearer's change then expires formation queries, and deselects and reselects a selected formation (:846-866). S1 selects nothing, but the panel steps of S2 and S3 run on this route, so read a selection that flickers in a panel step with this in mind.

**Pressing the controls.** A step never fakes a key at the OS level. The real key test and the step call the same method, so a step exercises the same path a player does. S1's members on `SeatView`, used only by the real key test and the trial, are `PressSeatKey()`, `TrialInput` (a
nullable `SeatInput` that `OnFrame` uses in place of `ISeatScreenAdapter.ReadInput` when it is set), `State`, and the read-only `PressPending`, `Frames`, `GlideBegan` and `Faulted`. Later keys live in `SeatKeys` (6.0), and `ICommandTrialHost` grows by one press member per key, so `SeatView` stays
under its 150 lines. Four controls are not Seat keys:

- **The order panel.** One trial member opens and closes it through the 6.2 site (`MissionOrderVM.OpenToggleOrder`, `TryCloseToggleOrder`).
- **Aiming.** `TrialInput` steers the look onto a chosen agent before `keep_to_soldier` and `take_soldier`.
- **The cursor.** `focus_follows_cursor` calls `Input.SetMousePosition` and reads back the pixel. It fails with a clear line if the window has no focus.
- **Steps outside the Seat.** S7's steps first press the Seat key and pass only with the state Off.

**Clocks and ticks.** Every step timer uses real time, summed from `OnMissionScreenTick`'s `dt`, which runs under pause (MissionScreen:595-600), because mission time stands still under a pause and repeats under fast time. A step reads mission time only as a measured fact, and the status
file's timer is real time too. S2 moves S1's steps to this. A step that writes an agent, a formation or a mission flag runs in the script's `OnMissionTick` handler. A step that removes a behaviour runs in the screen tick. Each row in `SeatTrialSteps` names what it requires and its time budget.

**Settings in a run.** Some steps need a setting that defaults to off. When armed, the session gets `CommandTrialSettings`, a wrapper over the plain provider with `SeatEnabled` on, `BeginBattlesInSeat` off, and `SeatKeepPanelOpen`, `SeatHeroFightsOn` and `SeatRiseWithPanel` forced on, in memory only. A saved `BeginBattlesInSeat` would otherwise make `enter` fail with facts that never name the setting, because the entry at the deployment edge turns the script's press into a leave. It never writes the MCM file, because json2
keeps saved values. A test pins that an unarmed session gets the plain provider. `back_to_stand` turns its fight value off.

Every step ends with a line:

`[SeatTrial] step=<name> result=<PASS|FAIL> <facts>`

| Stage | Steps and pass rules |
|---|---|
| S1 | Built; Deviations row 19 holds the rules. `refuse_in_deployment`; `enter`, whose frame rule becomes "owned frames above 30 per real second" with S2; `fly`; `hero_stands`; `leave_glide` (`GlideBegan`, state Off, the engine's camera within 10 m of the hero inside 4 s); `reenter_keeps_height` |
| S2 | `cursor_with_panel` (panel open: `MouseVisible` true; panel closed: false); `focus_follows_cursor` (the handler's field holds the pointer function's value at the pixel read back, and the focused formation is a fact on the line); `keep_panel_reopens` (a close with no player key reopens by the next frame; the trial's own close through the player's route stays closed); `hide_bars_restored` (a picture with the flag on, one after, and the flag false after the panel opens) |
| S3 | `keep_to_soldier` (the kept agent is the one aimed at, and the camera is within the orbit distance of that soldier after 2 s); `keep_to_company` (the panel open and one company selected; the target is that company); `let_go_on_death` (the trial kills the soldier through `Agent.Die`: no target, one message; then it keeps to the smallest player company and kills every unit: the same) |
| S4 | `fight_on` (the trial charges the hero's formation; controller AI, distance moved stated and above zero, `IsRetreating` and `IsPanicked` false after 5 s, the panel opens); `morale_held` (the trial writes morale 0 on the hero from the mission tick; `IsPanicked` false and `IsRetreating` false after 2.5 s real time); `back_to_stand` (leave, setting off, enter: controller Player, speed under 0.5 m/s) |
| S5 | `hero_falls` (fight-on set, entered, the trial kills the hero through `Agent.Die` from the mission tick: `Agent.Main` null, `HeroFightsOn` false, the Seat still up); `take_soldier` (`TrialInput` aims; `Agent.Main` set, `PlayerOrderController.Owner` is the new agent, every formation the fallen agent owned has `PlayerOwner` the new agent and `IsAIControlled` false, the panel opens) |
| S6 | `pause_flies` (mission time stands, the view moves); `slow` (mission seconds over real seconds is 0.5 within 25 percent, with no other request present); `fast` (at 2 and 4 the ratio matches `min(level, 0.1 / mean real dt)` within 25 percent; at 10 it is above 5); `speed_cleared` (after the key cancels, `FixedDeltaTimeMode` and `IsFastForward` are false and no request holds the id; teardown stays a unit test) |
| S7 | `rise_with_panel` (the Seat is down and the view third person; `CombatCamera.Position.z` is at least half the constant above its value before opening, after 1 s); `sink_on_close` (back within 1 m, the property exactly zero) |

Stages in Marshal's Word add theirs (`queue_two_moves`, `array_keeps_offsets`, `volley_holds`, `volley_looses`, `mark_keeps_order`, `square_side_kept`, and so on).

**Pictures.** `Utilities.TakeScreenshot` saves `hud_<step>.bmp` into the run folder at each `enter`, `fly`, `keep_to_company` and `hide_bars_restored` step, as the Fords autorun does, so that a session can look at the frame. The runner turns each into a PNG. A person or a session reads them: nothing judges them by machine, so the first run's stray arrow passed every step.

**Ending.** The script reports `final`. The autostart then calls `MBGameManager.EndGame` from the application tick, waits for the main menu, writes the done file, and calls `Utilities.QuitGame` when `quit=1`. An armed run answers no to `IsOpeningEscapeMenuOnFocusChangeAllowed`. Files: `command_trial_status.txt` every 5 s,
and `command_trial_done.txt` with `reason`, `run`, `log`, `live_seconds`, `wall_seconds`, `started`, `ended`. A failed write of either file logs a warning and the run goes on, because the runner reads the status file while the game replaces it, and the quit must still follow a lost done file. `reason=final` is the only success. The other reasons are `start_refused`, `start_failed`, `script_failed`, `never_live`, `wall_limit`, `mission_closed`, `live_limit` and `autostart_failed`.

**The wrapper**, `tools/run_command_trial.py`, is the same shape as `tools/run_long_defeat_spike.py`, with `RUNS_ROOT = E:\Temp\command-trial-runs` and tests in `tools/tests/test_run_command_trial.py`. Its judge fails a run with any `result=FAIL` line, a missing `[SeatTrial]` line for a step in `steps`, a `[CrashReport]` block, no `[Shutdown] clean module unload` line, a failed `taom.seat-of-seeing.*` check line, or a `reason` other than `final`. It also fails a log whose `[BuildStamp]` is not this checkout: a sha other than `HEAD`, or an uncommitted file under `Main/`, `Client/` or `Dependencies/` newer than the build. Otherwise a run of an old deploy reports PASS for code it never ran. From S2 onward it also fails on a line that matches `\] \[ERROR\] \[Seat(?:OfSeeing|Trial)\]`, and its PASS text reports the `[Session]` swallowed counts.

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
