# Cluster: Harmony Patches — Phase 4

Last updated: 2026-05-13
Inputs: [feature-manifest.md](feature-manifest.md), [wiring-matrix.md](wiring-matrix.md), [phase-4-kickoff.md](phase-4-kickoff.md)
Method: 5 parallel `feature-dev:code-reviewer` agents over 134 patch files / 35 categories / 7 manual `_harmony.Patch(...)` sites.

## Executive summary

| Severity | Count | Net new vs. Phase 1 |
|---|---|---|
| **P1** | **2** | 2 |
| **P2** | 13 | 13 (Phase 1 already filed #122 separately) |
| **P3** | 11 | 11 |

**P1 findings (feature broken / silent inert):**
1. **CompanionTactics `Patch35_Formation_SetMovementOrder`** — no team filter on the Postfix; `TroopStanceManager._stances` Dictionary is mutated from the async AI tick (`doAsyncAITick → TickAgentsAndTeamsAsync → BehaviorXxx.TickOccasionally → Formation.SetMovementOrder`) concurrently with main-thread reads in `GetStance` / writes from `SetStance`. .NET Framework 4.7.2 `Dictionary` is not concurrent-safe; symptoms range from `KeyNotFoundException` to silent bucket-chain corruption. The fix is one-line: a team filter `if (__instance?.Team != Mission.Current?.PlayerTeam) return;` before `ClearStance`.
2. **BannerColorPersistence `MapConversationTableau_SpawnOpponentLeader_Patch` / `MapConversationTableau_SpawnOpponentBodyguard_Patch`** — Postfixes mutate `AgentVisualsData.ClothColor1/2` AFTER the native entity has already been constructed via `MBAgentVisuals.CreateAgentVisuals(...)`. The colors are pushed to native only at ctor time, so post-construction C# writes never reach the renderer. Conversation tableau leader / bodyguard colors silently fall back to vanilla. Fix: hook the Prefix path on `AgentVisuals.Create` (Site 5 of Cluster C is already structured this way) instead of mutating after the fact.

**Cross-cutting observations:**
- Phase 1's wiring matrix already covered orphan-category / hook-balance / inline-model-deps checks — Phase 4 did NOT re-verify those.
- All 6 early-phase (`OnSubModuleLoad`) categories were cleared for `cctor` NRE risk in Phase 1; Cluster D confirmed the same with a fresh pass and noted the MixedFormations `Mission.Current?.Scene` smell flagged in wiring-matrix.md is benign (null-coalesced).
- The **v1.3.15-vs-v1.4 signature verification residual gap is real**: the cluster agents largely worked from `E:\Decompiled_Bannerlord\` (v1.4) rather than running `ilspycmd` on installed DLLs per file. The Cluster C agent explicitly flagged this. Sites where v1.3.15 verification is genuinely load-bearing (private-method patches whose names could have drifted) are marked **`v1.3.15-unverified`** below; queue an ilspycmd sweep in Phase 9 before fixing those.
- Cluster E's mandatory "target-method-exists" check surfaced no missing methods, but did **promote ~17 patches from Cluster E into Cluster A's scope** (Prefix-returning-bool patches that the original 24-file `return false;` grep missed because they delegate the bool to a hook service or short-circuit return). Cluster A's safety-gate analysis was NOT re-run on those — Phase 9 should triage them.

## Per-cluster findings

### Cluster A — Prefix-returns-false / vanilla-skip patches

23 candidate files inspected; 16 confirmed Prefix-skip and audited against vanilla v1.4 source; 5 discarded as void-Prefix / Postfix-only false positives; 2 disabled (`Patch0_BattleScenes`).

| # | Patch | Severity | Issue | Fix sketch |
|---|---|:---:|---|---|
| A1 | `HeroRace/Hooks/ActionSetCode_GenerateActionSetNameWithSuffix_Patch.cs` | **P2** | Patch class is `public class` (non-static). Harmony 2 attribute-based patches require `public static class` — non-static can cause unpredictable application behavior. All other TAOM patches use static. | `public class` → `public static class` at line 11. **NB:** Cluster D also flagged this same patch as a possible functional no-op duplicate of vanilla; investigate both before fixing. |
| A2 | `Diplomacy/Hooks/AllianceCampaignBehavior_EndAlliance_Patch.cs` | **P2** | Prefix blocks `EndAlliance` but vanilla callers (`OnAllianceTimerExpired`, `OnWarDeclared`) still proceed to call `AddAllianceDecision` AFTER. Result: a "propose new alliance" event for kingdoms that are still allied — duplicate alliance proposal. | Either (a) ensure `ShouldPreventAllianceEnd` is gated on alliance-expiry triggers only (not war-declaration), or (b) suppress the downstream `AddAllianceDecision` in the same prefix via flag. |
| A3 | `Diplomacy/Hooks/DeclareWarAction_ApplyInternal_Patch.cs` | **P2** | When the patch blocks `ApplyInternal`, vanilla's `OnWarDeclared` event dispatch (line 54) is skipped. Any campaign behavior listening to `OnWarDeclared` (including `AllianceCampaignBehavior.OnWarDeclared`) won't know war was attempted-and-blocked, leaving downstream state inconsistent. | Document the suppression explicitly; if TAOM later "force-declares" a war through its own path, it must manually dispatch `OnWarDeclared` or use `DeclareWarAction.ApplyByX` (not `ApplyInternal`). |
| A4 | `FiefManagement/Hooks/Patch36_GameStateScreenManager.cs` | **P2** | The Prefix replaces vanilla `CreateScreen` for `FiefManagementGameState` and returns `GauntletFiefManagementScreen`. The CALLER (`OnCreateState`) then runs `gameState.RegisterListener((IGameStateListener)(val is IGameStateListener ? val : null))` — if `GauntletFiefManagementScreen` does NOT implement `IGameStateListener`, the screen receives no lifecycle callbacks. Memory `feedback_gamestate_listener.md`: this caused a crash in a prior feature. | Verify `GauntletFiefManagementScreen : IGameStateListener`. Add interface if missing. |
| A5 | `BannerColorPersistence/Hooks/Banner_GetFirstIconColor_Patch.cs` | P3 | When `BannerDataList[1].ColorId == 0` (default, second layer not yet configured), patch reads `BannerManager.GetColor(0)` which returns transparent/black — corrupts secondary icon color. Vanilla never reads index 1 here, so this case was previously unreachable. | Add `if (__instance.BannerDataList[1].ColorId == 0) return true;` before patch logic. |
| A6 | `LocalizationOverride/Hooks/MBTextManager_GetLocalizedText_Patch.cs` | P3 | Fires on every text render (very hot path). `_overrides` is a non-thread-safe `Dictionary<string,string>`. Vanilla uses `[ThreadStatic]` for its hot path. Safe today (writes only at startup) but fragile if hot reload is added. | Comment the thread-safety assumption; switch to `ConcurrentDictionary` if reload is ever added. |
| A7 | `Siege/Hooks/BesiegerCamp_GetSiegeCampPartyPosition_Patch.cs` | P3 | Patch generates a gate-ring fallback position using `settlement.GatePosition.IsOnLand`, which lazy-resolves via `Campaign.Current.MapSceneWrapper` (memory `feedback_campaign_coupled_property_in_editor.md`). Risk is editor-mode only; in campaign, `Campaign.Current` is non-null. | Defensive: wrap `gate.IsOnLand` access or use `true` static fallback. |
| A8 | `HeroRace/Hooks/CharacterSpawner_InitWithCharacter_Patch.cs` | P3 | Prefix-skip delegates to `service.InitWithCharacter(...)`. Vanilla method body is IL-heavy (horse-spawn, animation, AgentVisuals creation). Service must replicate all side effects — but the service implementation was not inspected by this cluster. | Audit `ICharacterSpawnerService.InitWithCharacter` against vanilla IL — separate Phase 9 task. |
| — | `SpecialResources/Hooks/PartyScreenLogic_AddCommand_Patch.cs` | (note) | Prefix correctly returns false in resource-exhausted branch; no missing safety gate found. Style note: method lacks `[HarmonyPrefix]` attribute (relies on naming convention only). Inconsistent with rest of codebase. | Add `[HarmonyPrefix]` for explicitness. |
| — | Pos. 30 (`MixedFormations/Hooks/Patch30_FormationGetOrderPositionOfUnit.cs`) | (clean) | Codex review #36 navmesh gate (`IsFormationUnitPositionAvailable`) correctly replicated lines 54-55. Post-fix verified. |  |
| — | `QuickActions/Hooks/Patch34_SellAllItemsMenu.cs` | (clean) | Thread-static bypass for vanilla re-entry is correct; all vanilla logic preserved on bypass path. |  |
| — | `CharacterCreation/Hooks/CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs` | (also see Cluster E E4) | 3 Prefix-skip sub-patches + 1 void Prefix + 1 Finalizer. Finalizer flagged separately in Cluster E. |  |
| — | `BattleScenes/Hooks/Campaign_InitializeScenes_Patch.cs` | (dormant) | Always returns false. Skips vanilla's `LoadSiegeScenes` — if re-enabled, siege missions would use empty scene data. `Patch0_BattleScenes` category currently commented out in `SubModule.cs:117`. | **If re-enabled later:** also load siege scenes. |

**Discarded false positives:** `Patch36_MapScreenF6.cs` (Postfix-only), `KillCharacterAction_ApplyInternal_Patch.cs` (void Prefix + Finalizer; not a skip), `CharacterCreationNarrativeStageView_RefreshAgentVisuals_Patch` and `..._SpawnNonHuman_Patch` sub-patches inside the GetYouthMenuArgs file (void Prefix + Finalizer), `MapScene_Load_DiagnosticPatch.cs` (void Prefix).

**Residual gap:** Cluster E promoted **~17 additional Prefix-returning-bool patches** into Cluster A scope that this cluster did NOT analyze for safety-gate replication: `Clan_UpdateBannerColorsAccordingToKingdom_Patch`, `Banner_GetFirstIconColor_Patch` (already done above), ~~`PartyVM_PopulatePartyListLabel_Patch`~~ (**gone: deleted by the 2026-07-11 count→limit rework, and deliberately NOT reinstated by the 2026-09-06 usage-frame change, which postfixes the caller `PartyVM.RefreshPartyInformation` instead precisely so no vanilla code is suppressed; nothing to sweep**), `TrySwitchToNextMenu_Patch` (already done), `PartyScreenLogic_AddCommand_Patch` (already done), `GuardsCampaignBehavior_TakeGuardAgentData_Patch` (already done), `GuardsCampaignBehavior_GetSuitableSpear_Patch` (already done), `CustomBattleSideVM_OnCharacterSelection_Patch`, `CustomBattleSideVM_UpdateCharacterVisual_Patch`, `CustomBattleData_Factions_Patch`, `CustomBattleData_Characters_Patch` (already done), `TraitLevelingHelper_OnLordExecuted_Patch` (already done), `MakePeaceAction_ApplyInternal_Patch` (already done). Most are null-guard short-circuits (legitimate NRE prevention) rather than full vanilla replacements. Queue a follow-up Cluster A sweep in Phase 9 for the remaining ~5 not already covered above.

### Cluster B — Formation/Mission/Scene threading audit

18 files inspected.

| # | Patch | Severity | Issue | Fix sketch |
|---|---|:---:|---|---|
| B1 | `CompanionTactics/BattleActionBar/Hooks/Patch35_Formation_SetMovementOrder.cs` + `TroopStanceManager.cs` | **P1** | **No team filter** on the Postfix — `ClearStance` is invoked for every formation on every `SetMovementOrder` call, including enemy AI formations whose movement orders are issued from the async AI tick. `_stances` `Dictionary<int, TroopStance>` has no lock. Concurrent `Remove` (from worker thread) racing `TryGetValue` (from main thread) → `KeyNotFoundException` / corruption. | Add `if (__instance?.Team != Mission.Current?.PlayerTeam) return;` BEFORE the `_stances.ClearStance(...)` call. Alternatively add `lock` to `TroopStanceManager`. The team filter is simpler since stances are player-team-only semantically. |
| B2 | `SmartCavalryAI/CavalryChargeService.cs` | P2 | `_states` Dictionary has no lock. In current code Patch31's team filter prevents enemy-team threads from reaching the service, so the race is structurally unreachable today — but the absence of locking is fragile and `FormationLayoutService` (sister service) explicitly locks for this reason with an inline comment acknowledging the risk. | Add `private readonly object _lock = new();` and wrap `_states` accesses in `HandleChargeOrder`, `Tick`, `GetState`, `OnMissionEnd`. Mirror `FormationLayoutService`'s pattern. |
| B3 | `BattleScenes/Hooks/MBMapScene_GetBattleSceneIndexMap_Patch.cs` | P2 (dormant) | `_isRetrying` is plain `static bool` (not `volatile`, not `[ThreadStatic]`). `Thread.Sleep(250ms)` inside a Prefix that fires from a rendering thread = main-thread stall. Category `Patch0_BattleScenes` currently commented out in `SubModule.cs`, so the issue is inert until re-enabled. | Mark `volatile static bool _isRetrying` or use `[ThreadStatic]`. Move retry to background thread. If category stays permanently disabled, comment the safety issue inline. |
| — | `SmartCavalryRecursionGuard.Reset()` semantics caveat | (note) | `[ThreadStatic] private static int _depth` — `Reset()` zeros only the main thread's copy. If worker-thread `Enter()` increments and exception bypasses `Dispose`, worker copy stays elevated. In practice the `using` scope guarantees `Dispose`, so this is theoretical. Comment in `Reset()` documents only main-thread semantics. | Update Reset() comment to acknowledge the per-thread scope, or move to `ThreadLocal<int>` if cross-thread reset is needed. |
| — | `??=` lazy static field init across Patch31/Patch35/Patch35_Mission_OnTick | (note) | Double-resolve possible under concurrent invocation; DryIoc returns same singleton so no corruption, just wasted work. | Switch to upfront `Initialize()` call pattern (already used in `Mission_SpawnAgent_Patch` etc.) or `Volatile.Read + Interlocked.CompareExchange`. |

Clean (no findings, threading-safe):
- `Patch31_FormationSetMovementOrder` (team filter correctly placed before service calls)
- `MixedFormations Patch30 + FormationLayoutService` (Codex review #35 lock pattern correctly applied)
- `Mission_SpawnAgent_Patch`, `AgentVisuals_Create_Patch`, `Agent_EquipItemsFromSpawnEquipment_Patch` (main-thread spawn sequence)
- `AtmospherePersistence Mission_Initialize_Patch` (mission setup, main-thread)
- `SiegeDismountMissionBehavior` (`MissionBehavior` lifecycle, main-thread)

### Cluster C — Manual `_harmony.Patch(...)` signature drift (7 sites)

(Phase 4 originally targeted 8 sites; Phase 1 found SiegeDismount is NOT a manual patch — it's a `MissionBehavior`. The shared deferred `Patch_MissionTime_SetMovementOrder` category is reviewed in Cluster D, not C.)

| # | Site | Severity | Issue | Fix sketch |
|---|---|:---:|---|---|
| C1 | `MapConversationTableau.SpawnOpponentLeader` / `SpawnOpponentBodyguardCharacter` (sites 6 & 7) | **P1** | Postfix retrieves last `AgentVisuals` from `_agentVisuals`, reads its `_data` (an `AgentVisualsData`), and writes `ClothColor1/2`. But `MBAgentVisuals.CreateAgentVisuals` already ran at ctor time and pushed the initial colors to native. C# writes after ctor never reach the renderer. Feature silently appears to succeed but tableau colors fall back to vanilla. | Move color injection into a Prefix on `AgentVisuals.Create` (Site 5 already does this), OR call a native `SetClothColor` API on `_data.AgentVisuals` if one exists in v1.3.15. |
| C2 | `GuardsCampaignBehavior.TakeGuardAgentDataFromGarrisonTroopList` (site 2) | P2 (`v1.3.15-unverified`) | Patch reflects `PrepareGuardAgentDataFromGarrison` and calls `_prepareMethod.Invoke(null, ...)` assuming static. v1.4 decompile shows it IS `private static`; v1.3.15 not verified via ilspycmd. If v1.3.15 changed it to instance, `Invoke(null, ...)` throws `TargetException` → silent fallback because of catch{} (C4 below). | Verify with `ilspycmd "<SandBox.dll>" -t "SandBox.CampaignBehaviors.GuardsCampaignBehavior"`. If instance: change Prefix signature to accept `GuardsCampaignBehavior __instance` and pass to Invoke. |
| C3 | `AgentVisuals.Create` (site 5) | P2 | No `else IoC.Resolve<IModLogger>().LogWarning(...)` fallback in `SubModule.cs` if `TargetMethod()` returns null. Sites 4, 6, 7 all have such warnings; site 5 is missing one. If TaleWorlds View assembly isn't loaded yet or the 5-param overload changes, the patch silently does not apply with zero diagnostic. | Add `else IoC.Resolve<IModLogger>().LogWarning("[BannerColor] AgentVisuals.Create not found — clan color randomness suppression will not apply");` after the patch call. |
| C4 | `GuardsCampaignBehavior_TakeGuardAgentData_Patch.cs` (site 2 sibling) | P2 | Bare `catch {} return true;` swallows any reflection-Invoke exception silently, returning to vanilla fallback. No log. | `catch (Exception ex) { _service?.Log(..., ex.Message); }` or one-time log. |
| C5 | `MobilePartyVisual.AddCharacterToPartyIcon` (site 4) | P2 (`v1.3.15-unverified`) | Param types include `typeof(ActionIndexCache).MakeByRefType()` for two `in` params. Harmony 2.x has known inconsistencies binding `in` modreq parameters. Method has one unique-name overload — `AccessTools.Method` could resolve by name only. | Drop the param type array and let Harmony find the unique-name overload, or runtime-test that the LogWarning never fires. **NB:** Phase 1 issue #122 covers this patch's other issue (`Initialize` never called → static fields null → silent no-op even when correctly bound). |
| C6 | `Patch_MissionTime_SetMovementOrder` — site 8 in original count, owned by Cluster D | (deferred) | See Cluster D D1. |  |
| — | Site 1 (`OrderOfBattleHeroItemVM.GetCaptainTooltip`) | P3 | Bare `catch {}` in Postfix swallows decorator exceptions silently. | Log exceptions. |
| — | Sites 6 & 7 (`MapConversationTableau.Spawn*`) — missing LogWarning fallback | P3 | Same pattern as C3; SubModule.cs lines 461-471 lack `else` branches. | Add LogWarning fallbacks. |
| — | Site 8 / Patch31 — missing param type array on `[HarmonyPatch]` | P3 | Patch31 uses `nameof(Formation.SetMovementOrder)` without explicit type array. Only one overload in v1.4 — but defensively, Patch35 specifies `new[] { typeof(MovementOrder) }`. Inconsistent. | Add `new[] { typeof(MovementOrder) }` to Patch31's `[HarmonyPatch]` for consistency. |

**Caveat — v1.3.15 verification residual gap:** Cluster C agent used v1.4 decompiled source for structural analysis rather than running `ilspycmd` on installed v1.3.15 DLLs per file (despite the prompt instructing otherwise). Findings C2 and C5 are flagged `v1.3.15-unverified`. Other sites (`OrderOfBattleHeroItemVM.GetCaptainTooltip`, `AgentVisuals.Create`, `MapConversationTableau.SpawnOpponent*`) need similar verification but the agent found no evidence of v1.4-vs-v1.3.15 drift in the names/visibility/param-counts referenced. Queue an ilspycmd sweep in Phase 9 before fixing C1 (the P1) — its fix changes architecture, so confirming v1.3.15 has the same `AgentVisuals` ctor/`_data` shape is mandatory.

### Cluster D — Shared deferred categories + lifecycle

(Phase 1's wiring matrix already cleared the 6 early-phase categories for cctor-NRE; Cluster D confirmed independently and focused budget on cross-feature handshake + transpiler / late-category review.)

| # | Subject | Severity | Issue | Fix sketch |
|---|---|:---:|---|---|
| D1 | `Patch_MissionTime_SetMovementOrder` — SmartCavalryAI Patch31 + CompanionTactics Patch35 handshake | (cleared P3) | Two Postfixes on `Formation.SetMovementOrder(MovementOrder)`. No `[HarmonyPriority]` / `[HarmonyBefore]` / `[HarmonyAfter]`. **However:** Patch31 mutates the cavalry charge state machine; Patch35 clears a display-only stance (TroopStanceManager doc line 7: "Display-only: stances do NOT alter formation behavior in v1.3.15"). Non-overlapping state domains. Order is irrelevant in current code. | No fix needed for current code. If a 3rd registrant with ordering requirements is added, declare priority explicitly. |
| D2 | `Late_ActionSetOverride` — HeroRace `ActionSetCode_GenerateActionSetNameWithSuffix_Patch` | **P2** | Patch Prefix logic is bit-for-bit equivalent to vanilla `ActionSetCode.GenerateActionSetNameWithSuffix` in `TaleWorlds.Core`. The method is pure string ops with no LOTRLOME-specific behavior; vanilla's `Monster.BaseMonster` resolution already handles custom races. The patch adds per-call overhead (called on every agent spawn) for no behavioral gain. | Investigate whether LOTRLOME has a `BaseMonster` quirk that vanilla can't handle. If not, DELETE per simplicity-criterion ("deletion that holds parity always wins"). **Also see Cluster A A1**: this patch is `public class` (non-static); fix together. |
| D3 | `Late_Transpiler` — CharacterSelection `RefreshCharacterEntityAuxPatch` | **P2** | Throws `ArgumentException` on failure to find the `AgentVisualsData.Newobj` IL pattern (lines 49, 31, 34). Applied in `OnGameInitializationFinished` — a throw at this point crashes the mod during game init. v1.4 decompile confirms the `Newobj AgentVisualsData` pattern exists; v1.3.15 likely identical but not verified. | Run `ilspycmd "TaleWorlds.MountAndBlade.GauntletUI.dll" -t "BodyGeneratorView"` to confirm IL in v1.3.15. Replace throw with soft-fail: log error, return `instructions` unmodified. |
| — | `Patch35_Formation_SetMovementOrder` — bare `catch {}` | P3 | Catch swallows all exceptions during mission teardown. Feature stops silently with no diagnostic. | `catch (Exception ex) { logger?.LogError(...); }` or replace try/catch with explicit null guards. |
| — | `Patch35_Formation_SetMovementOrder` — missing `?` on nullable static fields | P3 | `_settings` and `_stances` declared without `?` despite lazy `??=` init. Patch31 (sibling) correctly uses `?`. | Add `?`. |
| — | Patch30_MixedFormations `Mission.Current?.Scene` early-phase read | P3 (cosmetic) | Phase 1 flagged as "graceful no-op via `?.`, not a crash." Cluster D confirms. Could defer to `OnMissionBehaviorInitialize` like `Patch_MissionTime_SetMovementOrder` for clarity. | Optional refactor; not a bug. |

**Cleared (no findings):**
- All 6 early-phase (`OnSubModuleLoad`) patch target types — `MBTextManager`, `Campaign`, `BannerlordMissions`/`CustomBattleData`/`CustomBattleSideVM`/`CustomBattleHelper`, `LoadingWindowViewModel`, `AiMilitaryBehavior`, `Formation` — none have static fields that read `Mission.Current` / `Campaign.Current` / `MBObjectManager.Instance` in their cctor (Phase 1 verified, Cluster D re-verified).
- `Late_Transpiler` is single-registrant (no cross-patch conflict).
- `Late_ActionSetOverride` is single-registrant.

### Cluster E — Postfix-only / Transpiler-only sanity sweep

83 patch files inspected (out of 134 total minus 37 non-patch hooks/interfaces/services). Target-method-exists check ran on each via decompiled v1.4 source (residual gap: v1.3.15 verification not done — same caveat as Cluster C).

| # | Patch | Severity | Issue | Fix sketch |
|---|---|:---:|---|---|
| E1 | `ArmyTargeting/Hooks/AiMilitaryBehavior_CalculateDistanceScoreForBesieging_Patch.cs` | P2 (performance) | Three `IoC.Resolve<...>()` calls per Postfix invocation, uncached. Feature doc says ~500-2000 calls/AI cycle. Hot path. Violates `.claude/rules/harmony-patches.md`: "Before `IoC.Resolve` in hot path, use lazy cache." Early exit `if (bestDistanceScore > 0f) return;` reduces but doesn't eliminate. | `private static IXxx? _xxx;` + `_xxx ??= IoC.Resolve<IXxx>();` for all three. |
| E2 | `CustomBattles/Hooks/CustomBattleSideVM_OnCultureSelection_Patch.cs` | P2 (`v1.3.15-unverified`) | Target is private `OnCultureSelection(BasicCultureObject)` patched by string. v1.4 decompile did not surface the method under this name. If v1.3.15 renamed/removed it, Harmony throws at patch application — entire `Patch19_CustomBattles` category may fail to apply. | Add `static MethodBase TargetMethod()` returning `AccessTools.Method(...)` with null-guard + LogWarning, or verify the name via ilspycmd before next ship. |
| E3 | `CharacterCreation/Hooks/CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs` (`SpawnNonHuman` sub-patch) | P2 | `[HarmonyFinalizer]` swallows ALL `NullReferenceException` unconditionally (not just from the horse-data path). Real bugs in `SpawnNonHumanNarrativeMenuCharacter` are masked. If the primary defense `RemoveHorseCharacters` works, this Finalizer is dead code. | Tighten scope: only swallow exceptions when the primary defense flag indicates failure. Or remove if the primary defense is sufficient. |
| E4 | 3 missing `[HarmonyPostfix]` attributes | P3 | `Campaign_InitializeDefaultCampaignObjects_Patch.cs`, `PartyCharacterVM_InitializeUpgrades_Patch.cs`, `PartyScreenLogic_UpgradeTroop_Patch.cs` — Postfix methods named by convention only (no attribute). Works in Harmony 2.4.2 today but fragile if methods are ever renamed. | Add `[HarmonyPostfix]` attribute to each. |

**Promoted to Cluster A** (Prefix-skip patches that the original `return false;` grep missed because they return a hook-service bool or short-circuit): Cluster A's residual-gap section lists all of them.

**Promoted to Cluster D** (Transpilers): `CampaignSceneNotificationHelper_CreateNotificationCharacter_Transpiler`, `Banner_TryGetBannerDataFromCode_Transpiler`. Quick v1.3.15 IL re-verification recommended in Phase 9.

**Cleared (clean Postfix patches):** ~60 files including all TroopWeight hook patches, FactionMap UI lifecycle patches, BannerColorPersistence value-fix patches (the non-tableau ones), ShaderPrecompilation, WeatherBoundsGuard, CompanionTactics Roles patches, EquipPresets, QuickActions VM observers, CharacterCreation Patch29 patches, HeroRace `CharacterTableau_*` and `FaceGen_GetBaseMonsterFromRace`, Diplomacy Postfix paths, Execution context-setter pattern.

## Cross-cluster summary

The audit confirms that TAOM's Harmony patching is **predominantly correct** — 134 patches reviewed, only 2 P1 findings, both with concrete fix sketches. The recurring failure mode is not signature drift (Cluster C's biggest worry) but **post-construction state mutation that doesn't reach the engine layer** (C1) and **missing team filters on patches that fire from worker threads** (B1). Both are the kind of bug that gives "passes deep-review, fails in-game" — invisible at the source level, visible only at runtime.

Three structural patterns recur across findings:
1. **Bare `catch {}` swallows real bugs** — Patch35_Formation_SetMovementOrder (D), GuardsCampaignBehavior_TakeGuardAgentData (C4), CharacterCreationNarrativeStageView_SpawnNonHuman Finalizer (E3), OOBHeroItem_GetCaptainTooltip (Cluster C P3). Establish a TAOM convention: catch logs.
2. **`??=` lazy static service caching is not thread-safe** but used in patches that fire from worker threads. Switch to upfront `Initialize()` pattern.
3. **Missing `else LogWarning(...)` on manual `_harmony.Patch(...)` null-guards** — Cluster C C3 and the missing fallbacks on Sites 6/7. Establish convention: every manual patch's null-guard MUST log a warning.

## GitHub issues to open (`audit-impl` label)

Surfacing here for user review BEFORE running `gh issue create`. Each row is one issue.

| # | Severity | Title (≤80 chars) | Primary file |
|---|:---:|---|---|
| 1 | P1 | CompanionTactics Patch35 SetMovementOrder: no team filter → TroopStanceManager race | `Patch35_Formation_SetMovementOrder.cs` |
| 2 | P1 | BannerColor MapConversationTableau leader/bodyguard color writes are silent no-ops | `MapConversationTableau_SpawnOpponent{Leader,Bodyguard}_Patch.cs` |
| 3 | P2 | HeroRace ActionSetCode patch is non-static class + likely a no-op duplicate of vanilla | `ActionSetCode_GenerateActionSetNameWithSuffix_Patch.cs` (Cluster A A1 + D D2) |
| 4 | P2 | Diplomacy EndAlliance block leaves caller in inconsistent state (duplicate alliance proposal) | `AllianceCampaignBehavior_EndAlliance_Patch.cs` (A2) |
| 5 | P2 | Diplomacy DeclareWarAction block skips OnWarDeclared event dispatch | `DeclareWarAction_ApplyInternal_Patch.cs` (A3) |
| 6 | P2 | FiefManagement: verify GauntletFiefManagementScreen implements IGameStateListener | `Patch36_GameStateScreenManager.cs` (A4) |
| 7 | P2 | SmartCavalryAI CavalryChargeService._states has no lock (structural fragility) | `CavalryChargeService.cs` (B2) |
| 8 | P2 | BattleScenes _isRetrying non-volatile + Thread.Sleep in Prefix (dormant; disabled feature) | `MBMapScene_GetBattleSceneIndexMap_Patch.cs` (B3) |
| 9 | P2 | SettlementGuards PrepareGuardAgentDataFromGarrison: v1.3.15 staticness unverified + bare catch{} | `GuardsCampaignBehavior_TakeGuardAgentData_Patch.cs` (C2+C4) |
| 10 | P2 | BannerColor AgentVisuals.Create manual patch: missing LogWarning fallback | `SubModule.cs:454-458` (C3) |
| 11 | P2 | BannerColor MobilePartyVisual.AddCharacterToPartyIcon: `in` modreq binding uncertainty | `MobilePartyVisual_AddCharacterToPartyIcon_Patch.cs` (C5) |
| 12 | P2 | CharacterSelection RefreshCharacterEntityAuxPatch throws hard on IL mismatch | `RefreshCharacterEntityAuxPatch.cs` (D3) |
| 13 | P2 | ArmyTargeting Patch22: 3 uncached IoC.Resolve calls in hot path (~500-2000 calls/cycle) | `AiMilitaryBehavior_CalculateDistanceScoreForBesieging_Patch.cs` (E1) |
| 14 | P2 | CustomBattles Patch19_OnCultureSelection: private method patch, v1.3.15 unverified | `CustomBattleSideVM_OnCultureSelection_Patch.cs` (E2) |
| 15 | P2 | CharacterCreation SpawnNonHuman Finalizer swallows all NREs unconditionally | `CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs` (E3) |
| 16 | P3 | Misc cleanups: bare catch{}, missing [HarmonyPostfix] attrs, missing LogWarnings (consolidated) | Multiple — see Cluster A/B/C/D/E P3 rows |

Total: 15 issues to open (one consolidates all P3 cleanups). **Do not open issue #16 separately if user prefers per-file issues.**

## Cross-references to Phase 1 / 2 / 3 outputs

Phases 1, 2, 3 ran concurrently with Phase 4 (separate sessions, separate output docs). Cross-cuts that connect Phase 4 findings to those phases' issues:

### From Phase 1 ([wiring-matrix.md](wiring-matrix.md))

- **Issue #122** (`audit-wiring`) — `MobilePartyVisual_AddCharacterToPartyIcon_Patch` Initialize never called → static fields null → silent no-op even when correctly bound. **Phase 4 finding C5** (`in` modreq binding uncertainty, issue #159) is on the SAME patch. Phase 9 should fix both together: confirm binding works, then ensure `Initialize(...)` actually runs.
- **MixedFormations early-phase `Mission.Current?.Scene` smell** (Phase 1 Probe 5 note) — Phase 4 Cluster D confirmed it's benign (null-coalesced, graceful no-op) but cosmetic refactor opportunity. Documented in Phase 4 D row P3 (cosmetic).
- **SiegeDismount manifest classification correction** (Phase 1 P3 doc note) — flowed into Phase 4 Cluster C: SiegeDismount has no manual `_harmony.Patch(...)` site, so the original "8 manual sites" count became "7 sites" in Cluster C. The manifest correction is a Phase N+2 docs job, not in scope for Phase 4 fixes.

### From Phase 2 ([cluster-gamemodels.md](cluster-gamemodels.md))

- **Pattern C — service-locator inside model bodies** (3 instances Phase 2 found in models) — **Phase 4 finding E1 (issue #161, ArmyTargeting Patch22 uncached IoC.Resolve in hot path) is the same pattern manifesting in a Postfix instead of a model body.** Phase 9 should batch the fix approach: extract to constructor-injected service or add lazy static caching with thread-safety considerations. Same `harmony-patches.md` rule ("Before `IoC.Resolve` in hot path, use lazy cache") applies.
- **Pattern A — rule-4 inline branching** (44+ instances Phase 2 found in models) — Phase 4 did NOT find equivalent in patch bodies (patches are typically thin entry points per ADR-002). No new Phase 4 contribution to this pattern.
- **No GameModel duplicates** — none of Phase 4's 16 issues overlap with the 10 issues Phase 2 opened (#134, #135, #137, #138, #140, #142, #144, #145, #147, #148). The patch and model layers were correctly partitioned.

### From Phase 3 ([cluster-campaign-behaviors.md](cluster-campaign-behaviors.md))

Phase 3 identified 5 recurring patterns (R1-R5). Phase 4 contributions:

- **R1 (Singleton service field never reset on new-campaign-in-same-process)** — Phase 4 found a related but DIFFERENT pattern: `??=` lazy static service caching in patch classes (Patch31, Patch35, `Patch35_Mission_OnTick`). It's not a per-campaign reset issue — it's a thread-safety question (double-resolve under concurrent invocation, no corruption but wasted work). Documented in Cluster B notes; not separately issued.
- **R2 (SyncData empty/wrong)** — N/A to patch layer.
- **R3 (Config provider validation gaps)** — N/A to patch layer.
- **R4 (Lookup-with-fallback without validation)** — N/A to patch layer.
- **R5 (Load-path mutation lacks Entity State Matrix)** — N/A to patch layer.

**Same-feature cross-cuts (issues to triage together in Phase 9):**

| Feature | Phase 3 issue | Phase 4 issue | Co-fix opportunity |
|---|---|---|---|
| **CompanionTactics** | #139 (SaveableTypeDefiner container collision risk + silent SyncData reset) | #149 (Patch35 SetMovementOrder no team filter → TroopStanceManager race) | Both touch `CompanionTactics` infra. Phase 9: review them in one PR window so CompanionTactics state-management story is cohesive (SyncData + thread-safety together). |
| **FiefManagement** | #143 (silent swap-restore failure + presenter Reset() incomplete) | #154 (verify GauntletFiefManagementScreen implements IGameStateListener) | Both touch `FiefManagement` UI/state lifecycle. Phase 9: fix as a bundle so the screen lifecycle is reviewed end-to-end (state restore + listener registration). |
| **CharacterCreation** | #125 (CC ADR violations only) | (no direct overlap; Cluster A reviewed Patch20_NarrativeHorseGuard separately) | — |
| **HeroRace** | #130 (HeroRace stale singleton race map) | #151 (ActionSetCode patch non-static + likely no-op duplicate) | Different concerns. **HOWEVER:** the 6 HeroRace patch categories (Patch1/2/3/4/5_FirstTimeInit/RefreshTableau/SetRace/CharacterSpawner/FaceGen + Late_ActionSetOverride) all consume `IRaceXxxService`. Phase 4 reviewed those patches clean, but Phase 3 #130 says the SERVICES have stale state on campaign-2-in-same-process. Result: the (correct) patches will silently return campaign-1's race assignments. Phase 9: fix #130 first; Phase 4's HeroRace patch findings will then be acting on fresh service state. |
| **CharacterCreation** | #125 (CC ADR violations: 7 sealed-type sites + 2 service-locator sites) | #163 (SpawnNonHuman Finalizer swallows all NREs) | Same feature, partly overlapping touch surface. The Finalizer (Phase 4) is in `CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs`; the ADR violations (Phase 3) are in `CharacterCreationContentService.cs` + `Patch20_NarrativeHorseGuard`. Phase 9: tackle #125 first to extract the adapters; #163 may become easier to scope (the Finalizer can use the new adapter to detect "horse removed" state explicitly instead of catch-all NRE swallow). |
| **CareerSystem** | #128 (CareerSystem behavior — AbilityService stale R1 + SyncData mutates on save) | (no direct Phase 4 P1/P2; Patch27_CareerSystem reviewed in Cluster E with one P3 missing-attribute) | Cross-cut: `Patch27_CareerSystem` (V-key ability activation) and `ViewModel.ExecuteCommand` Postfix BOTH read the AbilityService. If service has stale state (#128 R1), V-key may activate a campaign-1 ability for a campaign-2 hero. Phase 9: when fixing #128, runtime-test that Patch27's ability resolution is correct on a fresh-load campaign-2. |
| **Diplomacy** | #129 (WarOfTheRing CurrentPhase unsaved + config validation gaps) | #152 (EndAlliance block) + #153 (DeclareWar block) | Phase 9: when fixing the diplomacy event-suppression bugs, verify they don't interact with WoR's transient state. |
| **SpecialResources** | #133 (SyncData wrong cap + ScreenManager event leak) | (no direct Phase 4 issue; Patch26_SpecialResources patches reviewed clean) | — |

**No duplicate issues opened.** Phase 4's 16 issues are fully orthogonal to Phase 1/2/3's 27 issues — total `audit-impl` queue across phases is 43 issues (plus #122 with `audit-wiring` label).

### Phase 4 contributes 3 new TAOM-wide patterns (not in Phase 2/3 R-tables)

| # | Pattern | Phase 4 instances | Recommended Phase 9 batch |
|---|---|---|---|
| **P-cat1** | Bare `catch {}` swallows real bugs in patch bodies | 4 sites: Patch35_Formation_SetMovementOrder, GuardsCampaignBehavior_TakeGuardAgentData, CharacterCreationNarrativeStageView_SpawnNonHuman Finalizer, OOBHeroItem_GetCaptainTooltip | Single sweep: convert all to `catch (Exception ex) { logger?.LogError(...); }`. |
| **P-cat2** | Missing `else LogWarning(...)` fallback on manual `_harmony.Patch(...)` null-guards | 3 sites: AgentVisuals.Create + 2 MapConversationTableau Spawn* sites | Single sweep: add LogWarning fallbacks. |
| **P-cat3** | `??=` lazy static service caching used in patches that may fire from worker threads | 3 sites in CompanionTactics + SmartCavalryAI patches | Switch to upfront `Initialize()` pattern (already used in BannerColorPersistence patches) or `Volatile.Read + Interlocked.CompareExchange`. |

Issue #164 (consolidated P3) tracks the cleanup. Phase 9 should batch P-cat1 + P-cat2 + the missing-attribute fixes in a single mechanical PR.

## Residual gaps (queued for Phase 9 / future audit)

### Cluster F closure (2026-05-13) — v1.3.15 ilspycmd verification done

A 6th `taleworlds-researcher` agent ran `ilspycmd` against installed v1.3.15 DLLs for the 12 high-risk targets that Clusters C/D/E left unverified. **Result: no genuine signature drift on any target.** Specifically cleared:

| Target | TAOM finding | v1.3.15 verification result |
|---|---|---|
| `GuardsCampaignBehavior.PrepareGuardAgentDataFromGarrison` | C2 / issue #157 (`v1.3.15-unverified` flag) | Confirmed `private static AgentData(CharacterObject, bool, bool)` — `Invoke(null, ...)` is correct. **Flag CLEARED.** Issue #157's "staticness" concern is resolved; the bare-catch concern in the same issue remains. |
| `MobilePartyVisual.AddCharacterToPartyIcon` | C5 / issue #159 (`v1.3.15-unverified` flag) | Signature matches TAOM expectation exactly (`in ActionIndexCache` ×2, single overload). **Flag CLEARED on signature.** The Harmony `in`-modreq runtime binding concern remains a separate runtime question (still in scope for #159). |
| `CustomBattleSideVM.OnCultureSelection(BasicCultureObject)` | E2 / issue #162 (`v1.3.15-unverified` flag) | Method exists with expected signature in `Modules/CustomBattle/bin/.../TaleWorlds.MountAndBlade.CustomBattle.dll`. **Flag CLEARED.** The defensive null-guard recommendation in #162 remains a good idea but is no longer load-bearing. |
| `BodyGeneratorView.RefreshCharacterEntityAux` | D3 / issue #160 (IL pattern unverified) | `newobj AgentVisualsData::.ctor()` confirmed in v1.3.15 IL at offset `IL_003e`. Transpiler will find its insertion point. **The hard-throw concern in #160 still applies** (defensive soft-fail recommendation remains valid). |
| `Banner.TryGetBannerDataFromCode` | Cluster D transpiler IL verification | Confirmed transpilable; the `bannerDataList.Count > 32` cap lift (Patch15_BannerLayerLimit) has a clean IL target. |
| `CampaignSceneNotificationHelper.CreateNotificationCharacterFromHero` | Cluster D transpiler IL verification | TAOM transpiler uses the correct `nameof(...)` reference. Two `Hero::get_MapFaction` callvirts confirmed at IL offsets `IL_0006` and `IL_002e` (transpiler substitutes both). |
| `OrderOfBattleHeroItemVM.GetCaptainTooltip` | Site 1 / Cluster C P3 | Confirmed `private List<TooltipProperty>()`. No drift. |
| `GuardsCampaignBehavior.TakeGuardAgentDataFromGarrisonTroopList` | Site 2 / Cluster C C2 | Confirmed `private (CultureObject, bool, bool) → AgentData`. No drift. |
| `GuardsCampaignBehavior.GetSuitableSpear(CultureObject)` | Site 3 | Confirmed `private static (CultureObject) → ItemObject`. No drift. |
| `AgentVisuals.Create` | Site 5 | Confirmed `public static (AgentVisualsData, string, bool, bool, bool) → AgentVisuals`. Single overload. No drift. |
| `MapConversationTableau.SpawnOpponentLeader()` | Site 6 / C1 | Confirmed `private void()`. No drift. |
| `MapConversationTableau.SpawnOpponentBodyguardCharacter` | Site 7 / C1 | Confirmed `private void(CharacterObject, int, PartyBase)`. TAOM patch already specifies the 3-param signature explicitly. |

**Implication for issue #150 (Cluster C C1, P1 — MapConversationTableau color writes are silent no-ops):** the proposed fix (move color injection into a Prefix on `AgentVisuals.Create` instead of mutating `AgentVisualsData` post-ctor) needs a follow-up verification before implementation: re-check the `AgentVisuals` ctor body, `MBAgentVisuals.CreateAgentVisuals`, and `AgentVisualsData.ClothColor1/2` setter behaviors in v1.3.15 to confirm the Prefix-injection approach actually pushes colors to native. Cluster F did not include those targets. **Phase 9 should run a `Cluster F2` pass before #150's fix lands.**

### Other residual gaps still queued

1. **Cluster A safety-gate sweep on Cluster E's ~5 unanalyzed promotions** — Prefix-returning-bool patches (CustomBattleSideVM_OnCharacterSelection, CustomBattleSideVM_UpdateCharacterVisual, CustomBattleData_Factions, Clan_UpdateBannerColorsAccordingToKingdom, PartyVM_PopulatePartyListLabel) where the bool comes from a hook service or null-guard short-circuit. Most are likely benign null-guard skips; sweep for confirmation in Phase 9.
2. **DLL location knowledge gap** — Cluster F discovered that several view-layer DLLs live under `Modules/Native/bin/Win64_Shipping_Client/` and `Modules/CustomBattle/bin/Win64_Shipping_Client/`, NOT the root `bin/Win64_Shipping_Client/`. Update CLAUDE.md or `taleworlds-research-guide.md` so future ilspycmd lookups don't waste time searching the wrong path.

## Phase 4 complete

- 134 patch files reviewed
- 35 patch categories covered
- 7 manual `_harmony.Patch(...)` sites covered (Phase 1's site count, after SiegeDismount manifest correction)
- 2 P1 findings, 13 P2, 11 P3
- 15 GitHub issues queued (pending user approval before `gh issue create`)

## Phase log

| Date | Phase | Session | Output | Findings count |
|---|---|---|---|---|
| 2026-05-13 | 4 | initial | `cluster-harmony-patches.md`, `phase-4-kickoff.md` (updated by user during run) | 2 P1 + 13 P2 + 11 P3 |

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/audits/phase-5-kickoff.md](./phase-5-kickoff.md)
- [docs/audits/phase-6-kickoff.md](./phase-6-kickoff.md)
- [docs/audits/phase-7-kickoff.md](./phase-7-kickoff.md)
- [docs/audits/phase-8-kickoff.md](./phase-8-kickoff.md)

<!-- backlinks-end -->
