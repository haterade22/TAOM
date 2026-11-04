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
| Adapters | `Main/Adapters/ISeat*Adapter.cs`, `Seat*Adapter.cs` | Server-safe TaleWorlds assemblies only |
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
quiet behind a menu. An unbound key reads as not pressed.

| Id | Name | Default | Stage |
|---|---|---|---|
| 530 | Take the Seat | `B` | S1 |
| 531 | Keep to this soldier or company | `T` | S3 |
| 532 | Take this soldier | `Y` | S5 |
| 533 | Quicken time | `PageUp` | S6 |
| 534 | Slow time | `PageDown` | S6 |
| 535 | Stay time | `Home` | S6 |
| 536 | Hide the banners and bars | `End` | S2 |

These keys read through the engine's existing game keys, so the player's rebinds keep working:

| Action in the Seat | Source |
|---|---|
| Fly along the ground | `Input.GetGameKeyAxis("MovementAxisX")`, `"MovementAxisY"`, with the engine's 0.2 dead zone (MissionScreen:1781-1790) |
| Rise, sink | Game keys 14 (Jump) and 15 (Crouch) (MissionScreen:1794-1801) |
| Fly twice as fast | Game key 24 (Zoom) (MissionScreen:1765) |
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
    bool ForeignCamera,           // section 5.6
    bool DedicatedServer);

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
| Any kind with `ForeignCamera` or `DedicatedServer` | no | no | no | no |

`SeatMissionClassifier` in `Client/Features/SeatOfSeeing/` has one entry, `ReadFacts(Mission)`, which returns
`SeatMissionFacts` and is what `SeatView` and Marshal's Word both call. It turns a live mission into `SeatMissionKind`
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
beyond one key test a frame.

Keep it under 150 lines. Everything with a rule goes into pure types:

| Type | Assembly | Job |
|---|---|---|
| `SeatCameraMath` | Client | Flight step, height clamp, ground smoothing, blends, listener height |
| `SeatMissionPolicy` | Core | Section 4 |
| `SeatSession` | Client | The state machine (5.3) over an interface for the engine calls, so a test can drive it |
| `SeatHeroAdapter : ISeatHeroAdapter` | Core `Main/Adapters/` | Every write to the `Agent` itself (5.5) |
| `SeatScreenAdapter : ISeatScreenAdapter` | Client | Every read and write on `MissionScreen` (5.4), and the `MissionMainAgentController` calls (5.5), because that type lives in the client-only `TaleWorlds.MountAndBlade.View` assembly (ADR-013) |

### 5.2 Camera arithmetic

`SeatCameraMath` reuses `FordsCameraMath.Forward`, `LookDirection`, `Basis` and `ClampInside` by direct
call. Conventions are the same: X and Y are ground, Z is up, yaw 0 looks along +Y, positive yaw turns
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
| `ListenerCeiling` | 16 m | The sound listener stays at most this far above the ground, so a high view still hears the battle |

Rules: `Step` normalises diagonal movement, moves relative to the yaw, multiplies the height for zoom,
adds lift, clamps height and pitch and wraps yaw. A positive notch is the wheel rolled away, and it
lowers the view, the same as `FordsCameraInput`. Every float from the engine passes
`FiniteFloatValidator` before it enters the state, and a non-finite `dt` leaves the state unchanged.
Unit tests pin each constant and rule, including yaw wrap on the blend.

### 5.3 State machine

`SeatSession` has three states: `Off`, `Rising`, `Seated`. It owns the mission's last height and pitch.

- **Off to Rising** on the key, when `SeatPermissions.Camera` holds and the entry checks pass.
  **Entry checks:** `Mission.Mode` is not Deployment, Conversation, Barter or CutScene; photo mode is
  off; `MissionScreen.CustomCamera == null` (a player on a siege engine cannot leave it this way, and
  the message says so); `Mission.MissionEnded` is false.
- **Rising to Seated** when the blend finishes. Input does nothing during the blend.
- **Rising or Seated to Off** on the key, on a mode change to Conversation, Barter or CutScene, and on
  any fault (5.7).
- **Deployment.** The Seat refuses during Deployment, because vanilla's deployment camera already
  flies. When a mission moves from Deployment to Battle and the setting `BeginBattlesInSeat` holds,
  enter Seated with no blend at the current camera frame.

The view reads the state keys in `OnMissionScreenTick`, once per real frame. It never reads a key in
`OnMissionTick`, because fast time repeats that tick several times in one frame
(MissionState.cs:171-198). It reads the flight input in `UpdateOverridenCamera`, which runs once a frame
on real time (Mission.cs:3746-3749), so the view still flies when mission time stands still. Native
code sets the order of the screen tick and `Mission.OnTick`, so no rule may depend on that order.

No Seat key acts while photo mode runs. The state waits and carries on when photo mode ends.

The first entry in a mission shows one message that says the same key returns to the hero.

### 5.4 The frame

`UpdateOverridenCamera(float dt)` is the seat of the feature. `MissionScreen.CheckForUpdateCamera`
calls each view in behaviour order, and the first `true` wins (MissionScreen:858-906). `CustomCamera`
beats any override, so the Seat does nothing when it is set. Return `false` while `Off`, while photo
mode runs, and after a fault.

Finish the frame the way `FordsCommanderCameraView.Apply` does, with these differences:

1. `MissionScreen.CombatCamera.Frame = frame` and `MissionScreen.CameraBearing = yaw`. **Do not call
   `UpdateFreeCamera`.** It writes `CameraElevation` with the opposite sign of the one `UpdateCamera`
   uses (compare MissionScreen:547-563 with 2087-2099), and the return glide reads that value.
2. `SetFovVertical` from the view angle the player had at entry, floored at 50 degrees. `UpdateCamera`
   is what refreshes `CameraViewAngle` (1680-1687), so it stops changing while the Seat holds the frame.
3. `SceneView.SetCamera(camera)`.
4. `Mission.SetCameraFrame(ref frame, 65f / viewAngle, ref listener)`, with the listener at the camera's
   X and Y and at `min(camera Z, ground + ListenerCeiling)`.

Ground height: `Scene.GetGroundHeightAtPosition` with the engine's body flag mask (544321929, MissionScreen:1885,
named as a constant with that citation), the larger of that and the water level, as at line 1762.
Boundaries: `Mission.IsPositionInsideBoundaries`, then `GetClosestBoundaryPosition` (1864-1867).

`MissionScreen.SuppressSpectatorCyclingThisFrame()` each tick while the Seat holds the frame.

**Skipping `UpdateCamera` also skips** the per-frame `IsDisabled` write on the main agent controller
(1688-1699), the drag data (1669) and the focus upkeep. Section 5.5 covers the controller.

**The return glide.** The engine already has one, used when Deployment ends, and it needs no private
field from the caller. `MissionScreen` implements `IMissionListener.OnMissionModeChange(oldMode,
atStart)` as an explicit interface method (4511). `TaleWorlds.MountAndBlade.IMissionListener` is a
public interface, so call it through a cast. With `Mission.Mode == Battle` and `oldMode == Deployment` it arms
the special movement, seeds the position gap from the camera to the followed agent, seeds the bearing and
elevation gaps, and lets `UpdateCamera` ease them away (4559-4583).

On leaving the Seat in Battle mode:

1. Set `CameraBearing = yaw`.
2. Set `CameraElevation` to the vanilla sign of the current look, which is minus `Pitch`. The setter
   is private (197), so this is one reflection site: cache it once, add the row to
   `docs/reference/taleworlds-api-snapshot/reflection-sites.md` Category B and the `[DataRow]` to
   `ReflectionSiteBindingTests`. A failed lookup logs once and the glide starts one frame off.
3. Call `((IMissionListener)MissionScreen).OnMissionModeChange(MissionMode.Deployment, false)`.
4. Return `false` from `UpdateOverridenCamera` from this frame on.

In any other mode the branch at 4559 does not run, so the view snaps to the hero. That is acceptable.

### 5.5 The hero while seated

`ISeatHeroAdapter` owns every write to the agent, and `ISeatScreenAdapter` owns the controller calls.
Stage S1 has one behaviour, **stand fast**:

- `MissionMainAgentController.Disable()` on entry and `Enable()` on leaving (1256-1263), through
  `ISeatScreenAdapter`. This is the `_activated` gate at line 264. Do not use `IsDisabled`:
  `UpdateCamera` overwrites it each frame whenever it runs (1694-1699), and the Seat must stay safe on
  any frame that returns `false`.
- On entry clear what `ControlTick` last wrote, because nothing rewrites it while the gate is shut:
  `MovementFlags`, `MovementInputVector` to zero, and `EventControlFlags` to zero. Confirm the member
  names in `Agent`.
- On entry set `HeadCameraMode = false`. `LookTick` writes it (420), and a first-person player's hero
  would otherwise hang in the sky view with no head.
- The agent stays `AgentControllerType.Player`, so `Agent.IsCameraAttachable` and the order panel's
  `CheckCanBeOpened` keep passing (MissionOrderVM:750-757). The gate's `else` branch clears interaction
  focus each frame (274), so the use key cannot act from the air.
- A `Mission.OnMainAgentChanged` handler re-applies the entry writes to the new main agent while
  seated. The handler may run inside a native callback on a chosen thread: route the writes through
  `DeferredCallbackQueue.RunOrDefer`. Unsubscribe in `OnMissionScreenFinalize`.

### 5.6 Another camera in the mission

Before entry, and only then, walk `Mission.MissionBehaviors` for a `MissionView` whose
`UpdateOverridenCamera` is declared on a type outside the `TaleWorlds` and `SandBox` assemblies, other
than `SeatView`. A match means another camera owns the mission: `ForeignCamera` is true, the Seat stays
out, and one log line names the type. This one rule covers `FordsCommanderCameraView` and any other
mod's view, with no list of names. Cache the answer per mission.

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

Every in-game check gets a line that proves pass or fail. Use `IOutcomeLog` with `tools/ingame_checks/seat-of-seeing.toml`, in the
shape of its siblings, and `OutcomeCounter` on any hot path. S1 checks: entered, left, frame owned for N
frames, glide armed, fault latched (must stay zero).

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
`ResetInputRestrictions` on that layer cannot fight it. A pure rule decides the state:

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
`TryGetFormationDistanceToCenter`, with the field injected by reference, is the whole change: while the
cursor shows, set the field to `Input.MousePositionRanged` multiplied by
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

Key 536 hides the mission's bars and banners. Use `MBDebug.DisableAllUI`, which backs the engine's own
`toggle_ui` command. Restore it when a menu takes focus, on leaving the Seat and in
`OnMissionScreenFinalize`, so it can never stay stuck. Confirm on v1.5.6 that the flag still has that
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
`Formation.CurrentPosition` at its ground height, sampled at most 10 times a second. Never key on
`Agent.Index`. Hold the `Agent` reference and test `AgentSlotIdentity.IsCurrentOccupant`, so a recycled
slot lets go, and a dead target lets go with one message.

## 8. Stage S4: the hero fights on

`SeatHeroFightsOn` with `SeatPermissions.FightOn`. This is the state with the most engine surprises, so
every guard below is required before the setting can default to true, and the setting stays false until an
in-game run proves the state.

### 8.1 Hand-off

`ISeatHeroAdapter.BeginFighting(agent)`, main thread only. Refuse when `Mission.MissionEnded` or
`Mission.IsMissionEnding` holds, or the agent is not active.

1. Stop a machine use. `agent.StopUsingGameObject` for an object the hero cannot carry, so that the hero
   is not added to an arrangement twice.
2. Read the morale. Set `agent.Controller = AgentControllerType.AI` (Agent:1239-1289). Write the morale
   back. Never call `CommonAIComponent.Initialize`, because it re-rolls morale (60-76) and a camera toggle
   must not become a morale cheat.
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

Report, do not patch: a banner bearer's effect tests `IsPlayerControlled`, so it stops while the hero is on
AI. `OrderTroopItemVM.FormationOnOnUnitCountChanged` subtracts one for the player (383-387), so the panel
count is one low.

Before this stage, grep TAOM for readers of `IsPlayerControlled`, `IsMine`, `Agent.Main` and `MainAgent`.
`Client/Features/AdvancedCombat/AutonomousMovementPlayerController.cs`,
`Main/Features/Enlistment/Hooks/EnlistmentBattleRoleMissionBehavior.cs` and
`Main/Features/LostFooting/` need a read, and the result goes in the feature doc.

## 9. Stage S5: take a soldier after the fall

Build on `Mission.TakeControlOfAgent` (6407-6414), which sets the controller and cancels fast forward. Call
it on the main thread.

1. **Never call `Mission.SetPlayerCanTakeControlOfAnotherAgentWhenDead` (6402).** That flag arms vanilla's held
   Kick key on `LastFollowedAgent` (MissionScreen:2890-2893), so from the Seat it would take a soldier the player
   did not point at. `TakeControlOfAgent` does not read the flag. A mission that sets the flag itself keeps its
   vanilla key.
2. When the mission allows Orders and `SeatTakeSoldierAfterFall` holds, key 532 in the Seat takes the soldier under
   the pointer, or the nearest eligible soldier of the selected company. Cast the pointer rays as in section 7. TAOM's own eligibility rule: human, active, the player's team, not using an object,
   a quarter of health or more, and a hero only when the hero belongs to the player's party. The main
   agent must be null. A living hero does not leave the body, so two player units never exist in one
   formation (`SwitchUnitLocationsWithBackMostUnit` recurses on that state, ColumnFormation:788-826).
3. A `Mission.OnMainAgentChanged` handler, deferred to the next main-thread `OnMissionTick`, does what
   vanilla leaves undone: `OrderController.Owner` goes to the new agent, and every formation whose
   `PlayerOwner` was the fallen agent gets `PlayerOwner = new agent`. That setter also takes the formation
   off AI (`SetControlledByAI(value == null)`, Formation:283-294), undoing `Team.DelegateCommandToAI`.
   Capture the fallen agent's formation list on `OnAgentRemoved`, with no write, because that runs inside a
   native callback.
4. Reset the score screen: `MissionGauntletBattleScore.DataSource` `IsMainCharacterDead`, `RefreshValues`,
   and `IsOver` toggled off and on so that the quit text updates.
5. Seated, re-apply the stand-fast writes to the new agent. Not seated, the player simply plays the soldier.

Campaign results need nothing: wounds, deaths and experience flow through `Agent.Origin`, not the
controller (BattleAgentLogic:132-181). A hero's formation keeps no captain and the engine has no
successor; leave that alone.

`Mission.InitialPlayerAgent` is null after the first fall (Mission:3023-3026), and its readers run at start,
so this is harmless. Record any TAOM reader that disagrees.

## 10. Stage S6: mission time

Reuse `Main/Adapters/FordsMissionSpeed.cs` and `Main/Features/FordsOfWar/Domain/FordsGameSpeed.cs` by direct
call. They already reach any speed with no transpiler. Levels are 1, 2, 4 and 10.

- Keys 533 and 534 step through a ladder: 0.25, 0.5, 1, 2, 4, 10. Below 1 uses
  `Mission.AddTimeSpeedRequest` under a TAOM id, so that other slow-downs still win
  (Mission:2070-2098). The engine takes the minimum of all requests starting from 1, so it can only slow.
  Above 1 uses the Fords speed path. Each cancels the other before it starts.
- Key 535 flips `MissionState.Current.Paused`, after it cancels fast time.
- `SeatPermissions.Time` gates all of it, so a network session never runs it (scene time speed is off in a
  session, MissionState:159).
- Taking a soldier cancels fast time. `Mission.TakeControlOfAgent` already does this for the engine's
  fast-forward; the fixed step needs the same.
- Leaving the mission clears what the Seat set. The time state is per mission and is never saved.
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

Off in a mission with `SeatPermissions.Orders` false. Reset to zero at `OnMissionScreenFinalize` and when the Seat is taken.

## Deviations

None yet. Record each change to this contract here with its reason.
