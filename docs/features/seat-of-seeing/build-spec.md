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
| 531 | Keep to this soldier or company | `T` | S3 |
| 532 | Take this soldier | `Y` | S5 |
| 533 | Quicken time | `PageUp` | S6 |
| 534 | Slow time | `PageDown` | S6 |
| 535 | Stay time | `Home` | S6 |
| 536 | Hide the banners and bars | `End` | S2 |

**A default stays clear of every key the mission's scene layer reads.** `MissionScreen` registers six categories on that layer
(389-394), and the same physical key then acts twice:

- `CombatHotKeyCategory` is the fighting keys.
- `GenericCampaignPanelsGameKeyCategory` is the panels, read by `MissionSingleplayerViewHandler` in every campaign mission (37-127): `B` is
  key 36 there, so a Seat on `B` would also print "Can't access banner window right now!" on each press, and `I` and `N` are out for the
  same reason.
- `ScoreboardHotKeyCategory`, and `MissionOrderHotkeyCategory`, the order panel's keys (BackSpace, F1 to F9, D0 to D8).
- `GenericPanelGameKeyCategory` holds `Q` and `E` for a panel's tab change. Only panel layers read those (`SwitchToPreviousTab`), and
  vanilla's own use key is `E`, so `Q` is free in a mission.
- `T` and `Y` are `ChatLogHotKeyCategory` keys 6 and 7, with no reader in a single player mission. In a network session a chat view may
  read them (not proved), and `Y`'s action is single player only. `PageUp`, `PageDown`, `Home` and `End` have no reader on the scene
  layer outside the debug category; `Home` and `End` also move a text widget's cursor, and a text widget holds the focus while it does.
- TAOM's own defaults: career `V`, time control `Space` and `E`, Realm Borders `M` and `G` on the campaign map, Fords `Q`, `E`, `T`,
  `H`, `N`, `M` in its own view, where the Seat stands down.

A test pins each Seat default against those engine categories: plain if their constructors run in the test host, else `RequiresGame`.
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
the foreign-camera walk (5.6) and asks for the rest when it acts.

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

`SeatMissionClassifier` in `Client/Features/SeatOfSeeing/` has one entry, `ReadFacts(Mission, IModLogger)`, which returns
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
  holds the hero (5.5, and the message says so).
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
  are covered. State that lives while the Seat is Off (mission time in S6, the panel lift in S7) is reset from `Finish` directly, whatever
  the state. Never put a cleanup in only one of these places.

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

**What stand fast does not cover.** The gate stops vanilla's movement, combat and look input. Three other readers stay live in S1, each on
a key the Seat does not use: the drop and equip key (game key 22, `MissionGauntletMainAgentEquipmentControllerView:120-133`), the career
ability key (`V`, `AbilityInputAdapter`), and the engine's perspective toggle (game key 27, in `HandleUserInput`). Each needs a deliberate
press, and a patch on each would not pay for itself in S1, so this stage accepts them. The feature doc and the manifest say so. Stage S2
closes the drop key as part of the screen work (6.4) and then updates that claim; the other two stay.

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

### 6.1 Cursor

While seated and `SeatCursorWithPanel` holds, the cursor shows when the order panel is open. A small
`GauntletLayer` owned by `SeatView` carries it: `InputRestrictions.SetMouseVisibility(true)` with the
mask left at `Invalid`. `ScreenManager.UpdateMouseVisibility` shows the cursor when any active layer asks
(ScreenManager:763-774), so this needs no patch on the panel's own layer, and vanilla's calls to
`ResetInputRestrictions` on that layer cannot fight it. The view adds the layer at the first need and removes it through the session's cleanup
(5.3), in a try, because a removal during teardown can meet a screen that is going away; read how `BattleActionBarMissionView` and
`StagedSiegeMissionView` handle their layers first. A pure rule decides the state:

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

Holding the middle mouse button hides the cursor and looks. On release put the cursor back with
`Input.SetMousePosition` at the pixel it had, because a hidden cursor is pinned to the screen centre.
Store the pixel from `Input.GetMousePositionPixel()` on press. Open the panel's buttons to clicks only in
Marshal's Word stage M7.

### 6.2 Keep the panel open

With `SeatKeepPanelOpen`, the panel reopens after an order. Do not patch the panel's close paths.
Watch `MissionPlayerToggledOrderViewEvent` (raised on every toggle) from the view. When the panel closes
while seated and the frame saw no Escape and no right mouse release, open it again on the next frame.
This needs one reflection site for the view model (`GauntletOrderUIHandler._dataSource`), gated as in
5.4, and a call to the public `MissionOrderVM.OpenToggleOrder(false, false)`. `MissionOrderVM.OnTroopFormationSelected`
closes and opens in one call (905-912), and this rule treats that as a no-op because the panel is open
again by the next frame.

### 6.3 Pointing at an enemy company

Vanilla's panel charges the focused enemy formation on a left release (GauntletOrderUIHandler:333-345),
and `MissionFormationTargetSelectionHandler` picks the focus from the screen centre. With a cursor shown
this charges something the player did not point at. **This is a real bug to prevent, so S2 does not ship
without it.**

The handler keeps its reference point in the field `_centerOfScreen`, writes it at the top of
`OnPreDisplayMissionTick`, and reads it only in the private `TryGetFormationDistanceToCenter`. It compares
that point with the output of `MBWindowManager.WorldToScreenInsideUsableArea`. So one Harmony prefix on
`TryGetFormationDistanceToCenter`, with the field injected by reference, is the whole change. The prefix reads one more static beside
`SeatPresence`, "the cursor shows", which the session's cleanup clears (5.3). While the cursor shows, set the field to `Input.MousePositionRanged` multiplied by
`(Screen.RealScreenResolutionWidth, Screen.RealScreenResolutionHeight)`. That product is in the same
coordinates as the handler's own centre calculation. The 70 scaled pixel limit and the 8 to 1000 m range
stay as the engine has them. Nothing proves the coordinates match at a UI scale other than the default,
so the in-game check runs at two UI scales.

### 6.4 The screen

All in `Patch140_SeatOfSeeing`, each a no-op while the Seat is off.

| Target | Kind | Why |
|---|---|---|
| `MissionGauntletCrosshair.GetShouldCrosshairBeVisible`, `GetShouldArrowsBeVisible` (protected virtual, 209-229) | Postfix false while seated | One decision feeds crosshair, attack direction arrows and reload |
| `CrosshairVM.ShowHitMarker` | Prefix, skip while seated | `OnCombatLogGenerated` calls it with no visibility test (MissionGauntletCrosshair:236-245), and in the prefab the marker is a sibling of the crosshair, not its child (Crosshair.xml:12-41, 73). A hero who fights on would flash it at the centre of the sky view |
| `MissionGauntletMainAgentEquipDropView.IsMainAgentAvailable` | Postfix false while seated | It reads key 34 on the scene layer and opens the wheel |
| `MissionGauntletMainAgentEquipmentControllerView.IsMainAgentAvailable` | Postfix false while seated | Keys 13 and 22 act on the main agent, so the drop key would drop a weapon from the air |

The drop key row replaces the acceptance in 5.5: this stage updates the manifest claim and the feature doc line that say the drop key
still acts from the Seat.

Key 536 hides the mission's bars and banners. Use `MBDebug.DisableAllUI`, which backs the engine's own
`toggle_ui` command. Restore it when a menu takes focus, on leaving the Seat and in
`SeatSession.Finish` (both teardown paths, 5.3), so it can never stay stuck. Confirm on v1.5.6 that the flag still has that
effect, and record the result.

## 7. Stage S3: keep to a soldier or a company

Key 531. With no target, a ray from the pointer picks the soldier: `Mission.RayCastForClosestAgent`
(1695-1698), which can return a mount, so take its `RiderAgent`, and compare the distance with
`Scene.RayCastForClosestEntityOrTerrain` so that a soldier behind a hill does not count. The view stores
the key press in `OnMissionScreenTick` and casts these rays in the next `OnMissionTick`, because nothing
proves a native ray is safe while the agent jobs run. With the order
panel open and one company selected, the key keeps to that company instead. Pressing it again, or flying
with the movement keys, lets go.

The Seat keeps its own camera while it keeps to a target. **Do not hand the frame to vanilla's follow.**
`GetSpectatingData` returns the living main agent whatever `LastFollowedAgent` says, and `UpdateCamera`
would re-enable the hero's controller on that frame (1694-1699).

Pure rule: the camera orbits the target point. The mouse turns the orbit, the wheel changes the distance
(4 m to 90 m, starting at 16 m), and the target point eases with `1 - exp(-5 dt)`. A company's target is
`Formation.CurrentPosition` at its ground height, sampled at most 10 times a second. The orbit gives a pose like any other: the boundary
clamp, `CapHeight` and `AboveGround` apply in the same order as 5.4, and a non-finite target position keeps the last one. The stored flight
follows the pose each frame, so letting go continues from where the view is, with no jump. Clear the target in the session's cleanup (5.3). Never key on
`Agent.Index`. Hold the `Agent` reference and test `AgentSlotIdentity.IsCurrentOccupant`, so a recycled
slot lets go, and a dead target lets go with one message.

## 8. Stage S4: the hero fights on

`SeatHeroFightsOn` with `SeatPermissions.FightOn`, read fresh at each entry (section 4). This is the state with the most engine surprises, so
every guard below is required before the setting can default to true, and the setting stays false until an
in-game run proves the state.

### 8.1 Hand-off

`ISeatHeroAdapter.BeginFighting(agent)`. Refuse when `Mission.MissionEnded` or
`Mission.IsMissionEnding` holds, or the agent is not active.

**Thread.** `BeginFighting` and `EndFighting` write the agent's controller and fields, so they run only from `SeatSession.OnMissionTick`, behind
a volatile flag, the shape of stand fast (5.5). `Enter` and `Leave` set the flag, and a fault sets it too. The tick does an owed `EndFighting`
before its early returns on Off and on a fault, so a leave or a fault still finishes, and a paused mission still reaches it (dt 0). `Finish`
writes no agent: no mission tick follows it. Confirm in the decompile that nothing read after `OnMissionScreenFinalize` and `OnRemoveBehavior`
needs the hero's controller back, and record the result in the feature doc.

1. Stop a machine use. `agent.StopUsingGameObject` for an object the hero cannot carry, so that the hero
   is not added to an arrangement twice.
2. Read the morale. Set `agent.Controller = AgentControllerType.AI` (Agent:1239-1289). Write the morale
   back. Never call `CommonAIComponent.Initialize`, because it re-rolls morale (60-76) and a camera toggle
   must not become a morale cheat. **Never set `Agent.Controller` inside a `Mission.OnMainAgentChanged`
   handler.** On a change to Player the setter raises that event from its own body (1252-1256), so a flip to
   AI there re-enters the setter. The outer setter then adds `CanRide` back, undoing step 3, and it raises
   `OnAgentControllerChanged` with the controller already AI, so `AgentHumanAILogic` adds a second
   `HumanAIComponent` (`AddComponent` only appends, 4409-4420). Defer the flip to the next main-thread tick.
3. The setter adds `AgentFlag.CanRide` on the way to Player and never removes it on the way to AI, so a
   foot hero would hunt for horses. Remove the flag unless the hero is mounted or has a `Horse` slot item.
4. Alarmed watch state only when `Mission.Mode` is Battle (leave it alone in any other mode), AI unpaused,
   no scripted movement, riding order set, and the formation told that the unit joined: confirm each call
   in `Agent` and `Formation`.
5. While fighting, one main-thread check a second holds the hero's morale at 40 or above, so that a rout
   cannot wound the hero. `CommonAIComponent` panics below its threshold (78-117), and a rout wounds a hero
   (MapEventParty:316-328).

`EndFighting(agent)` reverses it. Set the controller to Player. The setter clears the speed limits and
sets run for a humanoid. Also stop an AI walk to an object, drop any scripted movement, and clear the AI
state flags and the target frame.

`AgentHumanAILogic` adds a `HumanAIComponent` on a change to AI and removes it on a change back (16-30),
and the component's constructor subscribes to two agent delegates that its removal never clears
(HumanAIComponent:128-142, 386-393). Toggling the Seat would pile up handlers from dead components.
Clear those two delegates before the change back, through the smallest seam, and pin the behaviour with a
binding test.

### 8.2 Guards an AI main agent needs

Each is one patch in `Patch140_SeatOfSeeing`, a no-op unless the hero is fighting on from the Seat.

| Target | Problem | Answer |
|---|---|---|
| `MissionOrderVM.CheckCanBeOpened` (private, 740) | Refuses a main agent that is not player controlled (750-757) | Skip only that test while the hero fights on. Tests 742 (no main agent), 758 (no troops) and 766-769 (mission ending) still run. A postfix that flips the result would drop them, and would show vanilla's refusal message, so use a prefix or a narrow transpiler |
| `MissionBoundaryCrossingHandler` (`TickForMainAgent`, `HandleAgentStateChange`; 146-149, 195-236) | An AI hero that drifts out of bounds retreats the player from the battle | Skip for the seated hero |
| `RangedSiegeWeaponView.HandleUserInput` (79-99, 120-124) | An AI hero who mans an engine makes the view set `CustomCamera`, and that beats the Seat | Skip while seated |
| `LadderQueueManager` (`_userAgents`, 212-238, 434) | A true engine bug with a player unit on a ladder, on the parallel tick. Read `docs/features/creature-siege-role.md` first | Decide after reading. The answer must be parallel-safe, and record it. `OnTickParallelAux` holds the pair lock when a neighbour exists (396-407), and vanilla writes the same per-manager list and flag on that path (235-237). Keep vanilla's speed limit reset at 235 |

Report, do not patch: a banner bearer inside a formation always gives that formation its effect, and only a
bearer outside it needs `IsPlayerControlled` (`CustomBattleBannerBearersModel.cs:33-44` and its sandbox twin),
so a hero on AI loses the effect on other formations only. `OrderTroopItemVM.FormationOnOnUnitCountChanged`
subtracts one for the player (383-387), so the panel count is one low.

Before this stage, grep TAOM for readers of `IsPlayerControlled`, `IsAIControlled`, `IsMine`, `Agent.Main` and `MainAgent`
(about 110 matches in 57 files on 2026-11-03), and read each one that assumes the main agent is the player-driven hero. The result goes
in the feature doc, one line per file. At the least, read these, because each one answers differently once the hero is on AI:

- `Client/Features/AdvancedCombat/AutonomousMovementPlayerController.cs` and `Main/Features/Warg/WargRiderHandManager.cs` (mounted input and look writes).
- `Main/Features/LostFooting/LostFootingRules.cs:39`, which exempts `IsPlayerControlled`, so a hero on AI loses the exemption.
- `Main/Features/Enlistment/Hooks/EnlistmentBattleRoleMissionBehavior.cs` and `EnlistmentBattleFormationMissionBehavior.cs` (`ReclaimAfterDeployment`).
- `Main/Features/AdvancedCombat/BaseBehaviorTree/IsPlayerControlledDecorator.cs`, `Main/Adapters/GaitVarietyAgent.cs`.
- `Main/Features/DreadAura/Hooks/DreadAgentGate.cs`, `Main/Features/CombatMechanics/Hooks/CombatMechanicsHooks.cs`,
  `Main/Features/RaceAbilities/Hooks/RaceAbilityActivator.cs`, `Main/Features/CreatureSiegeRole/CreatureSiegeRoleService.cs`.
- `Main/Features/CultureDoctrine/Hooks/Tactics/TaomTacticBase.cs`, `Main/Features/SmartCavalryAI/CavalryChargeService.cs`,
  `Main/Features/StagedSiege/Hooks/TacticStagedObjective.cs`, `Main/Adapters/StagedSiegeMissionAdapter.cs`.

A banner bearer model gate is `!IsMainAgent && !IsHero && IsAIControlled` (`TaomBattleBannerBearersModel`), so the hero stays out of that
role on AI. The engine also raises `Agent.OnAgentControllerSetToPlayer` on the way back (Agent:1272-1278): grep for its listeners too.

## 9. Stage S5: take a soldier after the fall

Build on `Mission.TakeControlOfAgent` (6407-6414), which sets the controller and cancels fast forward. Call
it on the main thread.

1. **Never call `Mission.SetPlayerCanTakeControlOfAnotherAgentWhenDead` (6402).** That flag arms vanilla's held
   Kick key on `LastFollowedAgent` (MissionScreen:2890-2893), so from the Seat it would take a soldier the player
   did not point at. `TakeControlOfAgent` does not read the flag. Four openers set the flag themselves
   (`BannerlordMissions.cs:155` and `:237`, `SandBoxMissions.cs:713` and `:1461`: custom and campaign field
   battles and sieges with deployment), and so does TAOM's `LongDefeatSpikeLauncher.cs:52`. So a postfix on `Mission.CanTakeControlOfAgent`
   (6393) in `Patch140_SeatOfSeeing` answers false while seated, and key 532 is the only way out of the sky.
   Outside the Seat those missions keep vanilla's key.
2. When `SeatPermissions.TakeSoldier` (section 4) and `SeatTakeSoldierAfterFall` hold, key 532 in the Seat takes the soldier under
   the pointer, or the nearest eligible soldier of the selected company. Cast the pointer rays as in section 7, and make the take in that
   same mission tick. TAOM's own eligibility rule: human, active, the player's team, not using an object,
   a quarter of health or more, and a hero only when the hero belongs to the player's party. Refuse a soldier the player could not play:
   a creature race that `docs/features/troll-race.md` and `Main/Features/PlayerSwitcher/` treat as unplayable, and crew on a moving platform
   (`Main/Features/Elephant/TaomHowdahStandingPoint.cs`, `docs/features/mumakil.md`), whose deck rules assume AI. The main
   agent must be null. A living hero does not leave the body, so two player units never exist in one
   formation (`SwitchUnitLocationsWithBackMostUnit` recurses on that state, ColumnFormation:788-826).
3. The view's one `Mission.OnMainAgentChanged` handler (5.5) sets a volatile flag in the session, the same shape as `_standFastOwed`, and
   `SeatSession.OnMissionTick`, before its early return on Off, then does what vanilla leaves undone: `OrderController.Owner` goes to the new agent, and every formation whose
   `PlayerOwner` was the fallen agent gets `PlayerOwner = new agent`. That setter also takes the formation
   off AI (`SetControlledByAI(value == null)`, Formation:283-294), undoing `Team.DelegateCommandToAI`.
   A null value hands the formation to the AI, so test the new agent for null first. Do not save and restore
   any AI flag around it: the plain setter with a real owner is the whole change.
   Capture the fallen agent's formation list on `OnAgentRemoved`, with no write, because that runs inside a
   native callback. The hand-over to the AI cannot be stopped from `OnMainAgentChanged(null)`: `Mission` reads
   "was this the main agent" at 3017, before it clears `MainAgent` at 3021, and it still delegates at 3039-3042.
   So this stage lets the hand-over run and undoes it on the take.
4. Reset the score screen: `MissionGauntletBattleScore.DataSource` `IsMainCharacterDead`, `RefreshValues`,
   and `IsOver` toggled off and on so that the quit text updates.
5. Seated, re-apply the stand-fast writes to the new agent. Not seated, the player simply plays the soldier.

Campaign results need nothing, with one exception. Wounds, deaths and experience flow through `Agent.Origin`, not the
controller (BattleAgentLogic:132-181). The exception is the Tactics share: `Team.Leader` returns `Agent.Main` first
(Team:157-178), and `BattleAgentLogic` (229, 233) grants the share to the leader's party. So after a take, a plain
soldier leaves no commander with a party, and a hero of the player's party becomes the commander. State this in the
feature doc as a known effect. A hero's formation keeps no captain and the engine has no successor; leave that alone.
The field trial proves a custom battle only, and custom battle has its own agent logic and origin type
(`CustomBattleAgentOrigin`), so the manifest also owes one check in a campaign field battle.

TAOM's own readers treat the main agent as the hero, and a stand-in breaks that for some of them. Read each and state the result in the
feature doc: `Main/Features/ArmourAcquisition/Hooks/HeroKillCounterMissionLogic.cs:49-52` (caches the first main agent, and its comment
already expects a later takeover), `Main/Features/CareerSystem/Abilities/CareerHeroIdentityGate.cs` and `AbilityActivationController.cs:46-51`
(abilities need the career hero, #377), `Main/Features/CareerSystem/CareerPerkMissionBehavior.cs`, `Main/Adapters/FellFoesAdapter.cs:37-38`
(reports the player alive from the main agent), `Main/Features/Enlistment/Hooks/EnlistmentMeritMissionBehavior.cs:98`,
`Client/Features/CareerSystem/UI/CareerEnergyBarPrefab.cs` and `MissionAgentStatusCareerMixin.cs`.

`Mission.InitialPlayerAgent` is null after the first fall (Mission:3023-3026), and its readers run at start,
so this is harmless. Record any TAOM reader that disagrees.

## 10. Stage S6: mission time

Reuse `Main/Adapters/FordsMissionSpeed.cs` and `Main/Features/FordsOfWar/Domain/FordsGameSpeed.cs` by direct
call. They need no transpiler, and they reach exactly the levels 1, 2, 4 and 10. Levels 2 and 4 take one fixed step per
frame, capped at 0.1 s, so they reach about 6 times real time at most at 60 FPS. Level 10 is the engine's fast-forward.
Promise no other speed: a step above 0.1 s is untested.

- Keys 533 and 534 step through a ladder: 0.25, 0.5, 1, 2, 4, 10. Below 1 uses
  `Mission.AddTimeSpeedRequest` under a TAOM id, so that other slow-downs still win
  (Mission:2070-2098). The engine takes the minimum of all requests starting from 1, so it can only slow.
  Above 1 uses the Fords speed path. Each cancels the other before it starts.
- **A second writer of fast-forward exists.** While the main character is dead, vanilla's score view toggles its own
  `IsFastForwarding` (`MissionGauntletBattleScore`, about 176-180, behind `CanOpenScoreboard` on v1.5.6), and
  `SPScoreboardVM` (252-268) passes it to `Mission.SetFastForwardingFromUI`. So read `Mission.IsFastForward` each time a
  key steps the ladder. Never trust the Seat's own memory of the level.
- Key 535 flips `MissionState.Current.Paused`, after it cancels fast time.
- `SeatPermissions.Time` gates all of it, so a network session never runs it (scene time speed is off in a
  session, MissionState:159).
- Taking a soldier cancels fast time. `Mission.TakeControlOfAgent` already does this for the engine's
  fast-forward; the fixed step needs the same.
- Leaving the mission clears what the Seat set, on both teardown paths, from `SeatSession.Finish` directly (5.3). The time state is per
  mission and is never saved.
- Mission time works with or without the Seat when the mission allows it, because a player may want it from
  the hero's eyes. It stays off until a key is pressed.

## 11. Stage S7: rise to give orders

`SeatRiseWithPanel`, default false, because section 1 promises vanilla behaviour to a player who never asks for
this. With the hero's view in third person and the order panel open, the view lifts so that the player can see
over the ranks, and it sinks when the panel closes. The Seat itself does not use this.

- **Seam.** `Mission.SetCustomCameraTargetLocalOffset(Vec3)` (Mission.cs:2025). `UpdateCamera` adds it to the view's
  target in third person only, and its box cast still keeps the view out of walls (MissionScreen:2226-2242, 2415-2483).
  This needs no patch. Grep for other writers of this property, restore what you found on the way down, and write
  only while the lift is above zero, so that a player who never opens the panel causes no write at all.
- **Pure rule in `SeatCameraMath`.** `PanelLift(current, panelOpen, dt)` moves a progress value towards 1 in 0.5 s and
  towards 0 in 0.4 s, with smoothstep on the output. Each new direction starts from the current value, so a quick
  close and open does not jump.

| Constant | Value |
|---|---|
| `PanelLiftHeight` | 7 m |
| `PanelLiftHeightInSiege` | 3 m, when `Mission.IsSiegeBattle` holds, because streets and gatehouses leave no room |

Off in a mission with `SeatPermissions.Orders` false, read fresh when the panel opens (section 4). Reset to zero in `SeatSession.Finish` and when the Seat is taken.

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

**The mission** is the real thing: `BannerlordMissions.OpenCustomBattleMission(scene, player, playerParty, enemyParty, isPlayerGeneral: true, null, "", "", 12f)`
(BannerlordMissions.cs:109). The player side defends. `CustomBattleCombatant.AddCharacter` fills the two parties.

| Side | Culture | Troops |
|---|---|---|
| Player | `gondor`, hero `lord_1_9` | 40 `fow_gondor_militia_spearman`, 40 `fow_gondor_ano_archer_militia`, 20 of a mounted `fow_` troop from `ModuleData` (find one, and name it in the feature doc) |
| Enemy | `mordor` | 120 `fow_mordor_orc_fighter`, 30 `fow_mordor_orc_hunter` |

Refuse to start on an unknown id, as `LongDefeatSpikeLauncher` does, so that a bad id fails loudly.

**Deployment.** `Mission.Mode` starts at Deployment. Five seconds in, call `Mission.GetMissionBehavior<DeploymentHandler>().FinishDeployment()` from `OnMissionScreenTick`. It removes a
behaviour, so it must never run inside `OnMissionTick` (`docs/reference/engine/mission-and-missionbehavior-lifecycle.md`). This also proves that the Seat refuses during Deployment:
a press there leaves the state Off, and the step's own line says so. Deployment writes no `[Check]` refusal (5.10).

**Pressing the controls.** A step never fakes a key at the OS level. `SeatView` has one internal method per control, and the real key test calls the same method, so a step exercises the same
path a player does. S1 needs three internal members on `SeatView`, used only by the real key test and the trial: `PressSeatKey()`, which does what the key does at the next screen tick; `TrialInput`,
a nullable `SeatInput` that `OnFrame` uses in place of `ISeatScreenAdapter.ReadInput` when it is set; and `State`, the session's state. Each later stage adds one press method per key of its own. These
keep `SeatView` under its 150 lines; a trial type that would push it over goes in `Trial/`. Steps run in mission time, on the trial's own timers. Every step ends with a line:

`[SeatTrial] step=<name> result=<PASS|FAIL> <facts>`

| Stage | Steps |
|---|---|
| S1 | `refuse_in_deployment`; `enter` (Rising then Seated, frame count above 100 after 3 s, hero controller still Player); `fly` (forward input for 2 s moves the view by the expected distance within 25 percent); `hero_stands` (hero speed under 0.5 m/s while seated); `leave_glide` (one `OnMissionModeChange` call, then a vanilla camera frame); `reenter_keeps_height` |
| S2 | `cursor_with_panel`; `focus_follows_cursor`; `hide_bars_restored` |
| S3 | `keep_to_soldier`; `keep_to_company`; `let_go_on_death` |
| S4 | `fight_on` (controller AI, hero moves, the panel still opens); `back_to_stand`; `morale_held` |
| S5 | `hero_falls` (the trial kills the hero through `Agent.Die`); `take_soldier` (`Agent.Main` set, `OrderController.Owner` is the new agent, the panel opens) |
| S6 | `pause_flies` (mission time stands, the view still moves); `slow`; `fast`; `speed_cleared` |
| S7 | `rise_with_panel`; `sink_on_close` |

Stages in Marshal's Word add theirs (`queue_two_moves`, `array_keeps_offsets`, `volley_holds`, `volley_looses`, `mark_keeps_order`, `square_side_kept`, and so on).

**Pictures.** `Utilities.TakeScreenshot` saves `hud_<step>.bmp` into the run folder at each `enter`, `fly` and `keep_to_company` step, as the Fords autorun does, so that a session can look at the frame. The runner turns each into a PNG.

**Ending.** `MissionState`'s normal end, then the done file, then `Utilities.QuitGame` when `quit=1`. An armed run answers no to `IsOpeningEscapeMenuOnFocusChangeAllowed`, as `LongDefeatUnattendedView` does. Files: `command_trial_status.txt` every 5 s, and `command_trial_done.txt` with `reason`, `run`, `log`, `live_seconds`, `wall_seconds`, `started`, `ended`. `reason=final` is the only success.

**The wrapper**, `tools/run_command_trial.py`, is the same shape as `tools/run_long_defeat_spike.py`, with `RUNS_ROOT = E:\Temp\command-trial-runs` and tests in `tools/tests/test_run_command_trial.py`. Its judge fails a run with any `result=FAIL` line, a missing `[SeatTrial]` line for a step in `steps`, a `[CrashReport]` block, or a `reason` other than `final`.

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
