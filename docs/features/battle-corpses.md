# Battle Corpses

## Overview

Each field battle, siege and sally-out fades fallen soldiers after a set time (default 60 s) and keeps at most a set number of bodies (default 25). Each battle logs one `[BattleSettings]` line with the player's own ragdoll, corpse and battle-size options. On the first main menu after the game starts, TAOM offers to lower the player's Number of Ragdolls and Number of Corpses options when they are above the recommendation, and an MCM button does the same at any time.

## Why This Exists

Players on 1.4.8 and 2.0.x reported mid-battle freezes that stopped when they lowered the vanilla Performance options Number of Ragdolls and Number of Corpses (#701).

- **Vanilla behavior:** a body stays until the corpse cap pushes it out. The cap is the player's option: None, 25, 75, 125, 250 or Unlimited (1,021). The engine's per-body timeout is 3,600 s, so in practice only the cap removes anything. Ragdolls are capped by a separate native option (0, 1, 3, 5, 10, Unlimited).
- **TAOM requirement:** large battles with heavy creature rigs. The elephant ragdoll has 60 bodies, the mumak is that rig at 3x scale and the spider has 62, against a human's 26.
- **Without this feature:** a player on Unlimited keeps up to 1,021 bodies in a 1,000-man battle, and every death can start a full physics ragdoll.

What is verified: the mission tick (native `FUN_1805aefc0`, reached from `IMBMission.Tick`) walks the whole corpse list every frame, so a long list costs every frame. What is still a hypothesis: that this, or a mass of simultaneous ragdolls, is what freezes the game. It fits the unexplained freeze in #634. The `[BattleSettings]` line, read beside `[MissionPerf]` and `[MissionStall]`, is what can confirm it from a player's log. #701 planned a `corpses=` field in `[MissionPerf]`; it was dropped because `agents` minus `active` is already logged, with the caveat that the difference also counts dead mounts and agents still fading out.

## Architecture

### Design Challenge

Corpse removal is entirely native, and the managed side has no per-body removal API except `Agent.FadeOut`. The two mission-level setters were read from the v1.5.3 `TaleWorlds.Native.dll` with `tools/native_decompile.py` and a byte scan for the field offsets:

| Managed call | Native behaviour |
|---|---|
| `Mission.SetMissionCorpseFadeOutTimeInSeconds(float)` | Writes the mission's fade time. -1 (within 0.01) means the 3,600 s default; anything else is clamped to 1 to 86,400 s. The mission tick removes every corpse whose stamp plus this time has passed. |
| `Mission.SetOverrideCorpseCount(int)` | Writes a raw count, -1 meaning no override, clamped to 1,021. The corpse limiter uses it **instead of** the player's option, then removes the oldest until the list fits. The limiter runs when a body becomes a corpse. |
| Mission reset (`FUN_1805bac50`, from the native Mission constructor, destructor and battle-scene setup) | Restores both defaults (3,600 s and -1). |

- **Single-player is covered.** The fade loop and the limiter run when `MBCommon.CurrentGameType` is neither MultiClient (1) nor SingleReplay (4); `MissionState` sets Single or SingleRecord for every single-player mission.
- **The fade clock starts when a body becomes a corpse,** not at death: the corpse entry is stamped at conversion, which follows the ragdoll settling or a dead-agent timer. "60 s" is 60 s after the body settles.
- Nothing in vanilla single-player calls either setter; multiplayer duels call the fade time with 1 s.
- **Ragdolls have no per-mission override.** Number of Ragdolls is a native option no managed code reads, so the only lever is the player's saved option, written the way the vanilla options screen writes it: `NativeOptions.SetConfig` then `NativeOptions.SaveConfig`. It is in none of the `NativeOptions.Apply` flags, and `IConfig.Apply`'s unconditional job never touches it. For corpses, `ManagedOptions.SetConfig` then `ManagedOptions.SaveConfig`, whose `BannerlordConfig.Save` calls `ValidateOptions` and so refreshes the native copy the limiter reads in the same session.

### Solution Approach

- `BattleCorpseMissionBehavior` (added to every mission by `BattleCorpsesModule`, a fresh instance each mission) checks once a second. When `MountDespawnMissionGate` admits the mission (field battle, siege or sally-out, Battle mode, combat, no network session), it resolves the limits and calls both setters, again only when the limits change. It applies from `OnMissionTick` because the gate admits only Battle mode, so the first apply lands within a second of deployment ending. Any exception disables it for the mission. The native writes carry the `MissionThreadGuard` tripwire.
- `BattleCorpsePolicy` owns every decision and takes only ints and floats. The override replaces the player's cap, so the cap passed is the lower of TAOM's and the player's own; TAOM never raises it. Turning the toggle off mid-battle passes -1 to both, handing the battle back to native.
- `BattleSettingsAdvisor` recommends Ragdolls 5 (option 3) and Corpses Low (option 1), Mike's values of 2026-10-01. It judges and lowers each option on its own: an option already at or below the recommendation is kept, an unreadable option never triggers the offer, and Apply writes the recommendation over an unreadable one.
- `BattleSettingsAdviceNotifier` shows the inquiry on the first main menu of the process (the module's `ApplyPhase.MainMenu`), where the stall and patch-failure notices already show theirs. Nothing is written unless the player clicks Apply. "Keep mine" saves nothing, so the offer returns at the next launch until the player applies or turns off "Recommend Battle Settings", which is the "don't ask again". The option names, "Low (25)" and "Performance" are filled from vanilla's own keys (`{=1awQTVqN}`, `{=h6wUbray}`, `{=hdLs35aB}`, `{=fM9E7frB}`), so every language names them exactly as its Options screen does.
- Stealth missions keep their corpses: prison break and hideout ambush (SandBox) and Sneak Into the Villa (StoryMode) set `DisableCorpseFadeOut`, and the first two drag corpses (`CorpseDraggingMissionLogic`). The gate never admits them, nor towns, arenas or hideouts.

### Component Diagram

```
TaomSettings (Performance/Battle Corpses)
        |
  BattleCorpseSettingsProvider
        |                      \
  BattleCorpsePolicy         BattleSettingsAdvisor ---- IGraphicsOptionsAdapter
        |                          |                    (NativeOptions, ManagedOptions,
  BattleCorpseMissionBehavior  BattleSettingsAdviceNotifier   BannerlordConfig)
  (Mission setters, [BattleSettings])  (main-menu inquiry, MCM button)
```

## Configuration

MCM, Performance / Battle Corpses.

| Setting | Default | Range | Effect |
|---|---|---|---|
| Clean Up Battle Corpses | on | | Master switch for the per-battle fade time and cap |
| Corpse Fade Time (seconds) | 60 | 10-300 | Seconds a settled body stays; outside the range (or NaN) falls back to 60 with one warning |
| Maximum Corpses | 25 | 0-250 | TAOM's cap; the player's lower option wins |
| Recommend Battle Settings | on | | Main-menu offer to lower the player's options |
| Apply Recommended Battle Settings | button | | Lowers Ragdolls to 5 and Corpses to Low now; a lower setting is kept |

The three cleanup settings apply mid-battle without a restart: the mission behavior re-reads them once a second. "Recommend Battle Settings" is read only at the first main menu after launch. The slider ranges equal the policy's valid range (`BattleCorpseSettingsProviderTests`).

The three cleanup settings count for co-op settings parity; the advice toggle (player-local) and the button (an action) are excluded in `CoopSettingsRelevance`.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/BattleCorpses/BattleCorpsePolicy.cs` | Limits per battle, validation, the `[BattleSettings]` line |
| `Main/Features/BattleCorpses/BattleCorpseLimits.cs` | The fade time and cap one battle runs with |
| `Main/Features/BattleCorpses/BattleSettingsAdvisor.cs` | Which options to recommend, and the lowering write |
| `Main/Features/BattleCorpses/BattleSettingsAdviceNotifier.cs` | Main-menu inquiry and result message (UI boundary) |
| `Main/Features/BattleCorpses/Hooks/BattleCorpseMissionBehavior.cs` | Calls the two Mission setters |
| `Main/Features/BattleCorpses/IBattleCorpseSettingsProvider.cs`, `BattleCorpseSettingsProvider.cs` | MCM values, passed through raw |
| `Main/Features/BattleCorpses/BattleCorpsesModule.cs` | Registration, mission behavior, main-menu phase |
| `Main/Composition/FeatureModules.cs` | Lists the module |
| `Main/Adapters/IGraphicsOptionsAdapter.cs`, `GraphicsOptionsAdapter.cs` | Reads and writes the vanilla options |
| `Main/Features/TaomSettings.cs` | The MCM group and the button lambda |
| `Main/Features/CoopInterop/CoopSettingsRelevance.cs` | Co-op classification of the five settings |
| `Main/_Module/ModuleData/taom_module_strings.xml` | The six `taom_bc_advice_*` strings |

## Dependencies

- `MountDespawnMissionGate` (MountDespawn): the battle allowlist, shared so both cleanups agree on which missions they touch. Its summary names this feature, so a change made for mounts is not made blind.
- `IGraphicsOptionsAdapter` (Adapters): `NativeOptions`, `ManagedOptions`, `BannerlordConfig`.

## Tests

- `BattleCorpsePolicyTests`: native's option table, the lower-of-two cap, toggle-off defaults, fade and cap validation including NaN, infinities and both bounds, warn-once and re-arm for both values, limits equality, the log line.
- `BattleSettingsAdvisorTests`: classification at and around the recommendation, one unknown option beside a high one, never raising, unknown options, a failed save, the recommended values.
- `BattleCorpseSettingsProviderTests`: provider fallbacks equal the compiled MCM defaults; slider ranges equal the policy's range.
- `GraphicsOptionsAdapterTests`: the float to option-index cast for NaN, infinities and out-of-range values.
- `BattleCorpsesWiringTests`: the module is listed once, declares the behavior, registers everything it resolves; the MCM button and the thread tripwire are wired.

The mission behavior's apply-on-change needs a live `Mission` and is covered by the in-game checks below.

## Performance

- **The poll:** once a second in an eligible battle, the gate (one native `CombatType` call), three MCM reads and a struct compare. Nothing allocates except the log line on apply. Ineligible missions stop at the mode or team-AI check.
- **Native removal is unbudgeted.** Every expired body goes in one tick, and a lowered cap is trimmed at the next corpse conversion. At the defaults that is at most 25 bodies. The worst case is turning cleanup off and on again in one battle on Unlimited, which can retire about 1,000 bodies at once. Unmeasured; see check 5. A step-down ramp is the follow-up if that shows a stall.
- **The cap shortens native's per-frame walk** of the corpse list, from up to 1,021 entries to 25.

## How to Change the Recommended Values

1. Change `RecommendedRagdollOption` / `RecommendedCorpseOption` in `BattleSettingsAdvisor.cs` (option indices, not counts).
2. Update the literal "5" in `taom_bc_advice_body` (C# and `taom_module_strings.xml` together), the `LOW` key in `WithVanillaLabels` (vanilla's `str_options_type_NumberOfCorpses_<n>` key), and the MCM hints, then run `/localize`.
3. Update `RecommendedValues_AreFiveRagdollsAndLowCorpses`.

## In-Game Checks

1. Custom Battle at the largest size with Unlimited ragdolls and corpses: the log shows `[BattleSettings] ... taomOverride=on fadeSeconds=60 corpseCap=25`, and bodies fade about 60 s after they settle.
2. Toggle cleanup off mid-battle: a second `[BattleSettings]` line with `taomOverride=off`.
3. **Does removing a corpse delete its agent?** Unverified from the native code. Watch `[MissionPerf]` `agents` against `active` as bodies fade. If `agents` falls with each fade, corpses held agent slots, reinforcements now arrive sooner (as MountDespawn records for mounts), and the MCM hint must say so.
4. A town fight and a hideout keep their bodies (no `[BattleSettings]` line).
5. Burst: cap 250, fade 300, wait for about 250 bodies, drop the fade to 10, then toggle cleanup off and on. Read `[MissionStall]` and `[MissionPerf]` around each change.
6. With high options, the main menu offers the change once; Apply rewrites `number_of_ragdolls` in `engine_config.txt` and `NumberOfCorpses` in `BannerlordConfig.txt` (back both up first), and Options, Performance shows 5 and Low (25). Whether the ragdoll limit takes effect before a restart is unverified.
7. Ragdolls 1 and corpses High: Apply keeps ragdolls at 1. Turn "Recommend Battle Settings" off and restart: no inquiry.
8. After the translation run, one non-English language: the inquiry names the options as that language's Options screen does.

## 1.4.8 Backport

A port, not a cherry-pick. `bannerlord-1.4.5` has no feature-module plumbing (`Main/Composition/`), so the registrations and the mission behavior are hand-wired into `IoC.cs` and `SubModule.cs` there, and its fingerprint pins (229/180) become 234/183. The managed APIs exist in v1.4.8; the native clamps, reset and gate have only been read on v1.5.3, so re-run `native_decompile.py --engine-method` for both setters against the 1.4.8 DLL first.

## Changelog

- 2026-10-01: feature added (#701).

## GitHub Issue

- **Issue:** #701 ([Mid-battle freezes with high ragdoll and corpse settings](https://github.com/haterade22/TAOM/issues/701))
- **Status:** Open
