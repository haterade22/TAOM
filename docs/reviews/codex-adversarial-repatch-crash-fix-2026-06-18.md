# Codex adversarial review -- re-patch crash fix (issue #288)

Scope: `Main/SubModule.cs` and `Main/Features/RaceAge/Hooks/DeliverOffSpring_RaceAssert_Patch.cs`, plus directly related lifecycle, watchdog, PatchShield, and transpiler surfaces. Installed TaleWorlds DLLs were decompiled from `E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/` with `ilspycmd`.

## Findings

No findings.

0 CRITICAL / 0 HIGH / 0 MED / 0 LOW

CRITICAL: 0
HIGH: 0
MEDIUM: 0
LOW: 0

## Known suspects

| # | Verdict | Evidence |
|---|---|---|
| 1 | CONFIRMED clean | `Main/SubModule.cs:536` calls base before the guard. `Main/SubModule.cs:545-546` gates the rest once per process. The guarded body is process-global patch wiring: category patches at `Main/SubModule.cs:548-599`, `606-613`, `615`, `629`; static hook/service initialization at `580`, `601-605`, `614`, `621-628`; manual `AccessTools` plus `_harmony.Patch` calls at `635-697`; watchdog start at `630`. I found no use of the `game` parameter after the base call and no `CampaignGameStarter.AddBehavior` or `AddModel` in the guarded method. Genuine per-game registration is in `OnGameStart`: `Main/SubModule.cs:326-531` adds campaign behaviors/models per `CampaignGameStarter`. Installed v1.4.6 `MBGameManager.OnGameStart` synchronously calls each submodule `OnGameStart(game, gameStarter)`, while `MBGameManager.OnGameInitializationFinished` separately calls each submodule `OnGameInitializationFinished(game)`. No statement in the guarded body captures per-game state or registers a per-game callback. |
| 2 | CONFIRMED clean | `BattleLoadStallWatchdog` is registered as `Reuse.Singleton` at `Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsIoC.cs:10-15`. `Start()` is internally idempotent: `if (_timer != null) return;` before creating the timer at `Main/Features/BattleLoadDiagnostics/BattleLoadStallWatchdog.cs:45-49`. The timer polls static/current loading-window state and resets per loading window, not per game start: `OpenedAtUtc` is read at `61`, closed-window reset at `62-65`, and new-window latch reset at `68-74`. Once per process is the intended lifetime and the new guard does not make it stale. |
| 3 | CONFIRMED clean | `newInstructions` is copied before any mutation at `Main/Features/RaceAge/Hooks/DeliverOffSpring_RaceAssert_Patch.cs:28`. The two soft-fail returns occur before the NOP loop: missing `SilentAssert` returns at `45-52`; missing start index returns at `94-98`; mutation only starts at `100-105`. `LogTranspilerDegradation` wraps logger resolution and logging in `try/catch` at `110-118`, so it cannot surface an exception to Harmony. Installed v1.4.6 `HeroCreator.DeliverOffSpring` starts with `Debug.SilentAssert(mother.CharacterObject.Race == father.CharacterObject.Race, ...)` and then proceeds to `GetCharacterTemplateForOffspring`, hero creation, parent assignment, and initialization. The patch is documented as noise reduction only at `DeliverOffSpring_RaceAssert_Patch.cs:13-20`, so returning original IL preserves vanilla behavior and only restores the harmless assert noise. |
| 4 | DISPUTED as a ship-blocking regression | PatchShield can unpatch a TAOM-applied patch by owner if a runtime `MissingMethodException`, `MissingFieldException`, or `TypeLoadException` escapes a patched method: it catches those at `Dependencies/Foundation/PatchShield.cs:227-242`, gathers owners at `282-286`, and unpatches prefix/postfix/transpiler owners at `301-306`. Nuance: `ProtectedOwnerPrefixes` includes `"TAOM"` at `Dependencies/Foundation/PatchShield.cs:48-64`, but TAOM's main Harmony owner is `"com.taom.mod"` at `Main/SubModule.cs:104`, so the main owner is not protected by that prefix list. That said, the once guard is still acceptable. PatchShield unpatch is a process-level defensive degradation after a version-mismatch class exception, and the pre-fix "reapply every game" behavior was not a valid resilience design because it duplicates every still-applied patch and caused this crash. No mitigation required for issue #288. If future diagnostics require reapply, it should be an explicit reset path that first unpatches `com.taom.mod` and clears the guards, not automatic per-game reapplication. |
| 5 | CONFIRMED clean | `base.OnGameInitializationFinished(game)` remains outside the guard at `Main/SubModule.cs:536`, so it runs on every game init. Installed v1.4.6 `MBSubModuleBase.OnGameInitializationFinished(Game game)` is an empty virtual method, so the base call is contract-preserving and side-effect free. Installed `MBGameManager.OnGameInitializationFinished` synchronously iterates `Module.CurrentModule.CollectSubModules()` and calls each submodule, then performs skeleton-scale setup. Installed `GameLoadingState.OnTick` drives `_gameLoader.DoLoadingForGameManager()` on the normal game-state tick path, with no task/thread handoff in the dispatch path. |

## Additional checks

Other throwing transpilers: DISPUTED. Active hook/patch transpilers are limited to six files. `Main/Features/BannerColorPersistence/Hooks/Banner_TryGetBannerDataFromCode_Transpiler.cs:45-66` soft-fails with `return list`; `Main/Features/BannerColorPersistence/Hooks/CampaignSceneNotificationHelper_CreateNotificationCharacter_Transpiler.cs:19-40` does no throwing anchor validation; `Main/Features/CastleRecruitment/Hooks/Patch42_AiHourlyTick_Transpiler.cs:165-167` and `Patch42_FillSettlements_Transpiler.cs:196-198` delegate to `CastleAiTranspiler`, whose reflection/anchor failures return the original list at `CastleAiTranspiler.cs:34-37`, `50-53`, and `69-72`; `RefreshCharacterEntityAuxPatch.cs:36-66` soft-fails with original instructions; RaceAge is now soft-fail. Focused search found no `throw new ArgumentException`, `throw new InvalidOperationException`, or `throw;` inside active `Hooks/` or `Patches/` transpiler files.

Direct reapplication paths: DISPUTED. A focused `Select-String -SimpleMatch '.PatchCategory('` over active `Main/` found direct calls only in `Main/SubModule.cs`. The game-init block is guarded at `Main/SubModule.cs:545-546`; the mission-time category remains separately guarded at `Main/SubModule.cs:708-712`.

Thread safety and static lifetime: DISPUTED as a practical issue. Installed v1.4.6 `GameLoadingState.OnTick` calls `_gameLoader.DoLoadingForGameManager()` synchronously, and installed `MBGameManager.OnGameInitializationFinished` synchronously iterates submodules. Installed `Module` constructs one `MBSubModuleBase` per `SubModuleInfo` via `constructor.Invoke(new object[0])`, stores it in `_subModuleBases`, and `CollectSubModules()` returns those stored instances. I found no evidence of concurrent `OnGameInitializationFinished` entry. Static vs instance does not change normal game-session behavior; it matches the existing `_missionTimePatchesApplied` process-lifetime guard.

## Verdict

SHIP.

The root-cause fix is clean: process-global Harmony patch wiring now runs once per process, per-game campaign behavior/model registration remains in `OnGameStart`, the watchdog has singleton/idempotent process lifetime, and the RaceAge transpiler now degrades to unmodified IL before any mutation if its anchor is absent.
