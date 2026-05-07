# Feature Port Session: SmartCavalryAI

You are porting feature #3 of 7 from the external-developer drop at `Downloads/Features_fixed/SmartCavalryAI/` into TAOM's `Main/Features/SmartCavalryAI/`. The other 6 features are tracked separately. Don't touch them.

## Prerequisites — read before writing any code

1. **The integration plan**: `C:/Users/mikew/.claude/plans/one-of-our-coders-steady-raccoon.md` — overall context for the 7-feature port. Section "3. SmartCavalryAI" has the planned file layout.

2. **This prompt** — end to end. It contains everything specific to this feature.

3. **Pattern templates** — read these to internalize the conventions before coding:
   - [Main/Features/MixedFormations/](../../Main/Features/MixedFormations/) — the feature you're standing on. SmartCavalryAI reuses `IFormationAdapter` from there.
   - [Main/Features/SiegeDismount/](../../Main/Features/SiegeDismount/) — pure mission-state singleton with no Harmony patches; useful for the SiegeDismount-style settings provider.
   - [docs/features/mixed-formations.md](../features/mixed-formations.md) — feature doc template that's been recently updated.
   - [docs/reviews/codex-adversarial-siegedismount-2026-05-06.md](../reviews/codex-adversarial-siegedismount-2026-05-06.md) — what `/review-codex` produces; you'll be running it on this feature too.

4. **The decompiled source you're porting**:
   `C:/Users/mikew/Downloads/Features_fixed/_decompiled/SmartCavalryAI/SmartCavalryAI.decompiled.cs`

   Read it end-to-end before writing the architecture. Critical sections: `CavalryChargeController` (static charge orchestrator using reflection on `Formation.SetPositioning`), `CavalryPathPlanner` (reroute logic), `CavalryFormationState` (per-formation state struct), `SmartCavalryAIBehavior` (state machine driver in `OnMissionTick`), `SmartCavalryPatches` (one Postfix on `Formation.SetMovementOrder`), `SmartCavalryAISettings` (5 MCM settings).

## Goal in one sentence

When the player commands cavalry to charge/move-to-target, intercept the order and orchestrate a coordinated line charge with passthrough + reform behavior, optionally rerouting around friendly infantry.

## Architecture — what to build

Per the integration plan section 3 + the patterns established by features 1–2:

### Files to create

```
Main/Features/SmartCavalryAI/
├── ICavalryChargeService.cs               ← orchestrator interface; owns the per-formation state machine
├── CavalryChargeService.cs                ← state machine: Idle → Forming → Charging → PassingThrough → Reforming → Idle (and Rerouting branch)
├── ICavalryPathPlanner.cs                 ← pure-function reroute math
├── CavalryPathPlanner.cs                  ← port of decompiled CavalryPathPlanner static class verbatim
├── ISmartCavalryAISettingsProvider.cs
├── SmartCavalryAISettingsProvider.cs      ← wraps TaomSettings.Instance for testability
├── Models/
│   ├── CavalryState.cs                    ← enum: Idle, Forming, Charging, PassingThrough, Reforming, Rerouting
│   └── CavalryFormationState.cs           ← per-formation state DTO (current state, charge target, reroute waypoint, start position)
├── SmartCavalryAIIoC.cs                   ← DryIoc registrations (Reuse.Singleton)
└── Hooks/
    ├── SmartCavalryAIMissionBehavior.cs   ← OnMissionTick drives state transitions; OnEndMission clears state
    └── Patch31_FormationSetMovementOrder.cs ← Harmony Postfix; intercepts charge/move-to-target orders on cavalry

Main/Adapters/
├── IBattlefieldQueryAdapter.cs            ← NEW — wraps Mission.GetNearbyAgents, Scene.GetGroundHeightAtPosition
└── BattlefieldQueryAdapter.cs

TAOM.Tests/Features/SmartCavalryAI/
├── CavalryChargeServiceTests.cs           ← state-transition matrix tests
└── CavalryPathPlannerTests.cs             ← pure-function reroute tests
```

### Adapter usage

| Adapter | Source | Why |
|---|---|---|
| `IFormationAdapter` | EXISTING — [Main/Adapters/IFormationAdapter.cs](../../Main/Adapters/IFormationAdapter.cs) | Reuse from feature 2. Already exposes `CountOfUnits`, `OrderPosition`, `Direction`, `Width`, `Interval`, `IsHolding`, `Units`. **You may need to extend it** with cavalry-specific properties: `bool RepresentativeIsCavalry`, `bool IsMoving`, `MovementOrderType CurrentMovementOrderType`. Add ONLY what you actually need; do not over-extend. |
| `IBattlefieldQueryAdapter` | NEW (this feature introduces it) | Wraps `Mission.GetNearbyAgents`, `Scene.GetGroundHeightAtPosition`, `Mission.PlayerTeam`. Service uses this for the friendly-infantry-collision-avoidance + ground-level queries during reroute math. |
| Reflection adapters | KEEP IN BOUNDARY CLASS | The decompiled module reflects on `Formation.SetPositioning` and `Agent.SetMovementDirection` (private/internal methods). The reflection MUST stay inside the Hooks layer (boundary), not in the service. Wrap each reflected call as a method on a small `IFormationCommandAdapter` (new in `Main/Adapters/`) or — since this is the only feature that needs them — keep them as static helpers in the patch class itself. **Log a single warning at startup if either reflection target is null on this Bannerlord build (1.3.15)** — fail loud, not silent. |

### Harmony patch

Reserve **`Patch31_SmartCavalryAI`**. Target: `Formation.SetMovementOrder` (Postfix). Verify the signature first:

```bash
ilspycmd "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.dll" -t "TaleWorlds.MountAndBlade.Formation" 2>&1 | grep -A1 "SetMovementOrder"
```

The Postfix reads the just-applied `MovementOrder` from the formation and decides whether to enter the state machine. If the order is `Charge` or `ChargeToTarget` AND the formation is cavalry AND the feature is enabled, call `_service.OnFormationOrderedToCharge(adapter, ...)`.

Wire in `Main/SubModule.cs` `OnSubModuleLoad`:
```csharp
_harmony.PatchCategory("Patch31_SmartCavalryAI");
```
(Place after `Patch30_MixedFormations`.)

### MCM settings — append to `Main/Features/TaomSettings.cs`

Group: `Battle Tactics/Smart Cavalry`, GroupOrder = 22 (after Mixed Formations at 21).

```csharp
[SettingPropertyGroup("Battle Tactics/Smart Cavalry", GroupOrder = 22)]
[SettingPropertyBool("Enable Smart Cavalry AI", Order = 0,
    HintText = "Master toggle. When off, cavalry uses vanilla charge logic. When on, the player's cavalry formations execute coordinated line charges with passthrough + reform behavior.")]
public bool EnableSmartCavalryAI { get; set; } = true;

[SettingPropertyGroup("Battle Tactics/Smart Cavalry")]
[SettingPropertyBool("Enable Friendly Collision Avoidance", Order = 1,
    HintText = "When charging, cavalry will reroute around friendly infantry within 3m of the charge line. Off = vanilla collision behavior (cavalry trample friendly).")]
public bool SmartCavalryAvoidFriendlies { get; set; } = true;

[SettingPropertyGroup("Battle Tactics/Smart Cavalry")]
[SettingPropertyFloatingInteger("Charge Formation Strictness", 0.0f, 1.0f, "#0.00", Order = 2,
    HintText = "How tightly the cavalry line must form before charging. 0 = launch immediately; 1 = wait until every unit is in perfect line. Default 0.7.")]
public float SmartCavalryChargeStrictness { get; set; } = 0.7f;

[SettingPropertyGroup("Battle Tactics/Smart Cavalry")]
[SettingPropertyFloatingInteger("Reform Distance After Charge", 10f, 80f, "#0", Order = 3,
    HintText = "Meters past the target before cavalry reforms a new line. Larger = wider passthrough sweep. Default 25.")]
public float SmartCavalryReformDistance { get; set; } = 25f;

[SettingPropertyGroup("Battle Tactics/Smart Cavalry")]
[SettingPropertyFloatingInteger("Charge Line Spacing Multiplier", 0.8f, 3.0f, "#0.0", Order = 4,
    HintText = "Multiplier on default unit spacing during line formation. 1.0 = vanilla. 1.2 (default) = slightly wider line for cleaner charge.")]
public float SmartCavalryLineSpacing { get; set; } = 1.2f;

[SettingPropertyGroup("Battle Tactics/Smart Cavalry")]
[SettingPropertyBool("Smart Cavalry Debug Mode", Order = 5,
    HintText = "Show diagnostic [SmartCavalryAI] state-transition messages on the in-game HUD. Off = file log only.")]
public bool SmartCavalryDebug { get; set; } = false;
```

### IoC registration

Add `using TAOM.Features.SmartCavalryAI;` to `Main/IoC.cs`, then in `Configure()`:
```csharp
SmartCavalryAIIoC.RegisterSmartCavalryAIFeature(container);
```

`SmartCavalryAIIoC.cs` registers (Reuse.Singleton):
- `ISmartCavalryAISettingsProvider → SmartCavalryAISettingsProvider`
- `IBattlefieldQueryAdapter → BattlefieldQueryAdapter`
- `ICavalryPathPlanner → CavalryPathPlanner`
- `ICavalryChargeService → CavalryChargeService`

### SubModule.cs MissionBehavior registration

In `OnMissionBehaviorInitialize` after `MixedFormationsMissionBehavior`:
```csharp
mission.AddMissionBehavior(new SmartCavalryAIMissionBehavior());
```

## Cross-session memory rules that apply to THIS feature

| Memory | How it applies here |
|---|---|
| `feedback_substring_keyword_matches_external_data.md` | Does this feature inspect any engine strings (scene names, formation IDs, faction keys)? If YES, grep across all `Main/_Module/ModuleData/*.xml` for substring overlap before shipping. The original SmartCavalryAI module doesn't appear to use string matching, but verify during your read of the decompiled source. |
| `feedback_adapter_modifier_preserving_overload.md` | NOT APPLICABLE — this feature touches no inventory/equipment APIs. |
| `feedback_user_facing_promise_must_match_code.md` | **APPLIES STRONGLY.** The original module has 5 MCM settings (Enable, EnableFriendlyCollisionAvoidance, ChargeFormationStrictness, ReformDistanceAfterCharge, ChargeLineSpacing). Trace each one to the implementation. The MixedFormations port found that `InfantryRowDepth` and `RangedRowDepth` were declared but never read (dead config). Audit each SmartCavalry setting the same way: search for `Strictness`, `ReformDistance`, `LineSpacing` references in the decompiled service code. Any setting that is declared but never consumed must either get a real implementation OR be dropped from the port (don't ship the dead promise). |

## Per-feature gotchas (from the decompiler agent's analysis — do not skip)

1. **Reflection on private TaleWorlds methods.** The original uses `AccessTools` (HarmonyLib) to call `Formation.SetPositioning` and `Agent.SetMovementDirection`. These are private/internal in v1.3.15. Verify both methods exist with `ilspycmd` BEFORE wiring; log a one-time `LogError` at `OnSubModuleLoad` if either reflection target is null. The reflection wrapper should be in `Main/Adapters/` so the service is testable without a live game.
2. **State machine cleanup.** The decompiled `SmartCavalryAIBehavior` has a per-formation state dict cleared on `OnEndMission`. Preserve this; otherwise stale `CavalryFormationState` entries leak across missions in the singleton.
3. **`Mission.Current.PlayerTeam` assumption.** The original is single-player only. Custom battles set `Mission.Current.PlayerTeam` to `null` for spectator missions. The `MissionBehavior` should null-check.
4. **`MovementOrder.OrderType` enum check.** Original compares against `Charge=4` and `ChargeToTarget=5` via `(int)cast`. In v1.3.15, verify the enum values via `ilspycmd` and use the enum names directly, not int casts.
5. **`Formation.SetMovementOrder` Postfix recursion risk.** If the service responds to a charge order by issuing another `SetMovementOrder` (to direct the cavalry to the line-charge formation), that triggers the Postfix recursively. Use a thread-local or instance flag to suppress recursion.

## Verification of v1.3.15 API surface (do this BEFORE writing the patch)

Run these `ilspycmd` checks and confirm each:

```bash
# Verify SetMovementOrder signature
ilspycmd "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.dll" -t "TaleWorlds.MountAndBlade.Formation" 2>&1 | grep -B1 -A3 "SetMovementOrder"

# Verify MovementOrder.OrderType enum values
ilspycmd "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.dll" -t "TaleWorlds.MountAndBlade.MovementOrder" 2>&1 | grep -A 12 "OrderType\|MovementOrderType"

# Verify Formation.SetPositioning is reachable via reflection (it's private — confirm name)
ilspycmd "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.dll" -t "TaleWorlds.MountAndBlade.Formation" 2>&1 | grep -i "SetPositioning"

# Verify Agent.SetMovementDirection (private)
ilspycmd "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.dll" -t "TaleWorlds.MountAndBlade.Agent" 2>&1 | grep -i "SetMovementDirection"

# Verify Mission.GetNearbyAgents
ilspycmd "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.dll" -t "TaleWorlds.MountAndBlade.Mission" 2>&1 | grep -A 1 "GetNearby"

# Verify Scene.GetGroundHeightAtPosition
ilspycmd "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.Engine.dll" -t "TaleWorlds.Engine.Scene" 2>&1 | grep -A 1 "GetGroundHeight"
```

If any reflection target is missing or renamed, **stop and ask the user before proceeding** — the SmartCavalryAI feature depends on those reflective calls.

## Acceptance gates

- Build clean (`./build.ps1` or `dotnet build Main/TAOM.csproj -c Debug`) — 0 errors
- Tests pass: `dotnet test TAOM.Tests/TAOM.Tests.csproj --filter "FullyQualifiedName~SmartCavalryAI"` — at least 25 tests covering: state-machine transitions for all 6 states, all 5 MCM settings (assert each one is read by SOMETHING), reroute path planner (3+ cases), settings out-of-range fallback, mission-end cleanup, suppress-recursion guard
- Full suite passes: `dotnet test --no-build` (currently 1447 — should be 1447 + your test count)
- Doc: `docs/features/smart-cavalry-ai.md` from TEMPLATE; cite the dead-setting audit you performed; cite the reflection targets you verified
- CHANGELOG.md entry at the top, structured like the SiegeDismount + MixedFormations entries
- `/deep-review SmartCavalryAI` run; every HIGH and MEDIUM finding fixed in the same session
- `/review-codex SmartCavalryAI` run; every confirmed finding fixed in the same session; RCA recorded in `docs/reviews/codex-adversarial-smartcavalryai-<date>.md`
- If RCA produced a generalizable lesson: codify it as a new feedback memory in `C:/Users/mikew/.claude/projects/c--Users-mikew-source-repos-TAOM/memory/` and index in `MEMORY.md`

**Do NOT commit** — leave the working tree dirty. The user tests in-game then commits.

## Verification — in-game golden path

1. Start a campaign with cavalry units (e.g., Rohirrim) in the player's army.
2. MCM → TAOM → "Battle Tactics / Smart Cavalry" → confirm `Enable=true`.
3. Enter a battle. Deploy the cavalry as a separate formation (F2 → F1).
4. Order the cavalry to charge enemy infantry (F3 + click on enemy line).
5. Confirm:
   - The cavalry rides in a formed LINE rather than a clump (look at the formation as it advances).
   - On contact, the cavalry passes THROUGH the enemy line rather than stopping in melee.
   - After passthrough (~25m past), the cavalry REFORMS a new line.
   - In `rgl_log.txt`: `[SmartCavalryAI] formation=cav-1 state=Forming → Charging` and similar transitions.
6. Disable round-trip: set `Enable Smart Cavalry AI = false`, reload, charge again — should be vanilla (cavalry stops on contact, no passthrough).
7. Reroute test: deploy infantry between the cavalry and the enemy. Order cavalry to charge through. Confirm the cavalry routes AROUND the friendly infantry (not THROUGH them) when `Enable Friendly Collision Avoidance` is on.

## Final report format

When done, output:
```
SmartCavalryAI port complete.
- Files created: [count] (services, adapters, hooks, tests, doc)
- Files modified: TaomSettings.cs, IoC.cs, SubModule.cs (Patch31 + behavior registration)
- Tests: NN/NN SmartCavalryAI tests pass; XXXX/XXXX total
- /deep-review verdict: [PASS / N findings fixed]
- /review-codex verdict: [PASS / N findings fixed]
- New feedback memories codified: [list]
- Awaiting in-game verification before commit.
```
