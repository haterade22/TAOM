# Batch B verification (#149–#164)

Verified by: general-purpose agent, Phase 9a, 2026-05-13
Inputs: triage-input-batch-B.json + cluster-harmony-patches.md
HEAD: b4b4de1 fix(messengers): wire IoC + CampaignBehavior (#121)

## Summary

| Verdict | Count | Issues |
|---|---|---|
| VALID | 14 | #149, #150, #151, #152, #153, #155, #156, #157, #158, #159, #160, #161, #162, #163, #164 (15 minus 1 STALE) |
| STALE | 1 | #154 (FiefManagement screen already implements IGameStateListener) |
| FALSE-POSITIVE | 0 | — |
| DUPLICATE | 0 | — |
| SEVERITY-DRIFT | 0 | — |

Net: 15 VALID + 1 STALE = 16. (#164 is a consolidated multi-cleanup tracker; sub-items mostly VALID — see detail.)

## Per-issue verification table

| # | Title (short) | Verdict | Severity (re-confirmed) | Notes |
|---|---|---|---|---|
| 149 | Patch35 SetMovementOrder no team filter | VALID | P1 | Team filter absent at HEAD; `ClearStance` mutates non-concurrent Dictionary |
| 150 | MapConversationTableau leader/bodyguard color writes are no-ops | VALID | P1 | Postfixes still mutate `_visData` after ctor; Cluster F2 still queued |
| 151 | HeroRace ActionSetCode patch non-static + no-op | VALID | P2 | `public class` confirmed line 10; Prefix logic mirrors vanilla |
| 152 | EndAlliance block leaves caller in inconsistent state | VALID | P2 | Prefix returns false unconditionally on hook signal; no `AddAllianceDecision` suppression |
| 153 | DeclareWarAction block skips OnWarDeclared dispatch | VALID | P2 | Prefix returns false without dispatching event |
| 154 | FiefManagement: verify GauntletFiefManagementScreen IGameStateListener | STALE | (n/a) | Interface ALREADY implemented — verify request satisfied at source |
| 155 | SmartCavalryAI `_states` no lock | VALID | P2 | `Dictionary<object, CavalryFormationState>` declared without lock |
| 156 | BattleScenes `_isRetrying` non-volatile + Thread.Sleep (dormant) | VALID | P2 dormant | Category still commented out in SubModule.cs:117 |
| 157 | SettlementGuards staticness unverified + bare catch{} | VALID (partly cleared) | P2 | Cluster F closure cleared staticness; bare-catch concern remains |
| 158 | BannerColor AgentVisuals.Create manual patch: missing LogWarning fallback | VALID | P2 | SubModule.cs:454-458 lacks `else` branch |
| 159 | MobilePartyVisual.AddCharacterToPartyIcon `in` modreq binding uncertainty | VALID (partly cleared) | P2 | Cluster F cleared signature; runtime binding still untested at HEAD |
| 160 | RefreshCharacterEntityAuxPatch throws hard on IL mismatch | VALID | P2 | 3× `throw new ArgumentException(...)` confirmed; soft-fail not applied |
| 161 | ArmyTargeting Patch22 uncached IoC.Resolve | VALID | P2 perf | 3 IoC.Resolve calls per invocation, no caching |
| 162 | CustomBattles Patch19_OnCultureSelection v1.3.15 unverified | VALID (cleared) | P2 → P3 | Cluster F confirmed method exists; defensive null-guard now low-priority |
| 163 | SpawnNonHuman Finalizer swallows NREs unconditionally | VALID | P2 | Finalizer at lines 148-156 swallows ALL NRE + key-named ANE |
| 164 | Consolidated P3 cleanups | VALID | P3 | All sub-items still apply — see detail |

## Per-issue detail

### #149 — Patch35 SetMovementOrder: no team filter — **VALID (P1)**

Current code at `Main/Features/CompanionTactics/BattleActionBar/Hooks/Patch35_Formation_SetMovementOrder.cs:24-37`:

```csharp
[HarmonyPostfix]
public static void Postfix(Formation __instance)
{
    try
    {
        _settings ??= IoC.Resolve<ICompanionTacticsSettingsProvider>();
        if (_settings == null || !_settings.CancelStanceOnMove) return;
        if (__instance == null) return;

        _stances ??= IoC.Resolve<ITroopStanceManager>();
        _stances?.ClearStance((int)__instance.FormationIndex);
    }
    catch { }
}
```

No team filter present. The Postfix fires for every formation across all teams whenever vanilla `Formation.SetMovementOrder` is invoked (which includes async AI tick paths). `ClearStance` mutates `Dictionary<int, TroopStance>` — not concurrent-safe on .NET Framework 4.7.2.

Sister patch `Patch31_FormationSetMovementOrder.cs:53-54` correctly has the player-team gate (`if (team == null || __instance.Team != team) return;`) — Patch35 is missing the same. **Re-confirmed P1.**

**Fix:** insert `if (__instance?.Team != Mission.Current?.PlayerTeam) return;` before line 33.

### #150 — MapConversationTableau leader/bodyguard color writes are silent no-ops — **VALID (P1)**

Current code at `MapConversationTableau_SpawnOpponentLeader_Patch.cs:69-97` (Bodyguard Postfix is identical pattern at lines 54-73). Postfixes pull `lastVisual` from the `_agentVisuals` list, retrieve `_visData`, and invoke `ClothColor1`/`ClothColor2` methods on the data. `AgentVisuals` has already been constructed by the time the Postfix runs; per Cluster C1 the native renderer was already pushed colors at ctor time.

Cluster F closure (cluster-harmony-patches.md line 229) explicitly says: *"Phase 9 should run a `Cluster F2` pass before #150's fix lands."* — i.e., re-check the `AgentVisuals` ctor / `MBAgentVisuals.CreateAgentVisuals` / `ClothColor1/2` setter behavior in v1.3.15 to confirm the proposed Prefix-injection actually pushes colors to native. **Severity P1 stands.**

### #151 — HeroRace ActionSetCode patch non-static + duplicate of vanilla — **VALID (P2)**

`ActionSetCode_GenerateActionSetNameWithSuffix_Patch.cs:10` confirms `public class` (not `public static class`):

```csharp
[HarmonyPatch(typeof(ActionSetCode), "GenerateActionSetNameWithSuffix")]
[HarmonyPatchCategory("Late_ActionSetOverride")]
public class ActionSetCode_GenerateActionSetNameWithSuffix_Patch
```

Prefix logic (lines 12-35) is bit-for-bit equivalent to vanilla: `"as_" + (BaseMonster ?? StringId) + (isFemale ? "_female" : "") + suffix`. No LOTRLOME-specific behavior. **VALID.**

### #152 — EndAlliance block leaves caller in inconsistent state — **VALID (P2)**

`AllianceCampaignBehavior_EndAlliance_Patch.cs:34-37`:

```csharp
if (_hook.ShouldPreventAllianceEnd(kingdom1.StringId, kingdom2.StringId))
{
    return false;
}
```

Returns false without any compensating logic on the caller side. Audit description verified verbatim. **VALID.**

### #153 — DeclareWarAction block skips OnWarDeclared dispatch — **VALID (P2)**

`DeclareWarAction_ApplyInternal_Patch.cs:43-46`:

```csharp
if (_hook.ShouldPreventWarDeclaration(faction1.StringId, faction2.StringId))
{
    return false;
}
```

Returns false; vanilla's `OnWarDeclared` dispatch is skipped along with all other vanilla logic. **VALID.**

### #154 — FiefManagement: verify GauntletFiefManagementScreen IGameStateListener — **STALE**

Current code at `Main/Features/FiefManagement/UI/GauntletFiefManagementScreen.cs:16`:

```csharp
public class GauntletFiefManagementScreen : ScreenBase, IGameStateListener
```

Lines 34-37 implement the listener methods:

```csharp
void IGameStateListener.OnActivate() { }
void IGameStateListener.OnDeactivate() { }
void IGameStateListener.OnInitialize() { }
void IGameStateListener.OnFinalize() { }
```

`git log --oneline` shows the file has only one commit, `1cad3a7 feat(fiefmanagement): port LOTRAOM remote-fief manage screen (Patch36)` — the interface was implemented from the initial port (verified with `git log -S "IGameStateListener"` returning the same single commit). The audit asked to "verify" implementation, which is already satisfied. **STALE.**

**Closing comment draft:**
> Verification at `b4b4de1`: `GauntletFiefManagementScreen.cs:16` already declares `public class GauntletFiefManagementScreen : ScreenBase, IGameStateListener` and lines 34-37 implement `OnActivate`, `OnDeactivate`, `OnInitialize`, `OnFinalize`. The interface has been present since the file was added in `1cad3a7` (the LOTRAOM port commit). The audit's "verify" request is satisfied — no code change needed. Closing as stale.

### #155 — SmartCavalryAI `_states` no lock — **VALID (P2)**

`CavalryChargeService.cs:33`:

```csharp
private readonly Dictionary<object, CavalryFormationState> _states = new();
```

No `_lock` field; `_states` mutations in `OnMissionEnd` (line 51), `GetOrCreateState` (line 251), and reads in `GetState`/`Tick`/`HandleChargeOrder` are unguarded. Patch31 player-team filter does prevent worker-thread reach, but the structural fragility flagged by audit + Codex review #35 remains. **VALID.**

### #156 — BattleScenes `_isRetrying` non-volatile + Thread.Sleep (dormant) — **VALID (P2 dormant)**

`MBMapScene_GetBattleSceneIndexMap_Patch.cs:16`:

```csharp
private static bool _isRetrying;
```

Plain `static bool` (not `volatile`, not `[ThreadStatic]`). `Thread.Sleep(RetryDelayMs)` at line 45. Category `Patch0_BattleScenes` is commented out in `SubModule.cs:117` (`// _harmony.PatchCategory("Patch0_BattleScenes");`) — issue is dormant but valid for re-enable. **VALID.**

### #157 — SettlementGuards staticness + bare catch{} — **VALID (partly cleared via Cluster F)**

`GuardsCampaignBehavior_TakeGuardAgentData_Patch.cs:64` confirms `Invoke(null, ...)`; catch{} at lines 68-71:

```csharp
__result = (AgentData)_prepareMethod.Invoke(null, new object[] { character, overrideWeaponWithSpear, unarmed });
...
catch
{
    // Degrade gracefully — let vanilla run
}
```

Per the cluster-harmony-patches.md "Cluster F closure" section (lines 214-216), v1.3.15 ilspycmd verification confirmed `PrepareGuardAgentDataFromGarrison` is `private static (CharacterObject, bool, bool) → AgentData`, so `Invoke(null, ...)` is correct — the staticness concern is **cleared**. The bare-catch concern at line 68 remains VALID. **Severity P2 stands** (just for the bare catch).

### #158 — AgentVisuals.Create manual patch: missing LogWarning fallback — **VALID (P2)**

`SubModule.cs:453-458`:

```csharp
// Manual patch for AgentVisuals.Create (TaleWorlds.MountAndBlade.View.dll)
var agentVisualsCreateTarget = AgentVisuals_Create_Patch.TargetMethod();
if (agentVisualsCreateTarget != null)
    _harmony.Patch(agentVisualsCreateTarget, prefix: new HarmonyMethod(
        typeof(AgentVisuals_Create_Patch),
        nameof(AgentVisuals_Create_Patch.Prefix)));
```

No `else` branch — sibling sites at lines 433, 442, 451 have `LogWarning` fallbacks. **VALID.**

### #159 — MobilePartyVisual.AddCharacterToPartyIcon `in` modreq binding uncertainty — **VALID (partly cleared via Cluster F)**

`MobilePartyVisual_AddCharacterToPartyIcon_Patch.cs:23-40` includes `typeof(ActionIndexCache).MakeByRefType()` twice in the param type array. Cluster F closure (line 217) cleared the signature ("matches TAOM expectation exactly"). The Harmony `in`-modreq runtime binding question remains — `AccessTools.Method` may or may not resolve a method whose parameters are `in` (`modreq InAttribute`) rather than plain `ref`. **VALID** as a runtime-binding diagnostic issue (separate from the `Initialize`-never-called issue #122).

### #160 — RefreshCharacterEntityAuxPatch throws hard on IL mismatch — **VALID (P2)**

`RefreshCharacterEntityAuxPatch.cs:29-30, 33-34, 48-49`:

```csharp
if (ctor == null)
    throw new ArgumentException("Cannot find AgentVisualsData parameterless constructor. Patch: RefreshCharacterEntityAuxPatch");
...
if (actionSetMethod == null)
    throw new ArgumentException("Cannot find AgentVisualsData.ActionSet method. Patch: RefreshCharacterEntityAuxPatch");
...
if (insertionIndex < 0)
    throw new ArgumentException("Cannot find AgentVisualsData Newobj in IL. Patch: RefreshCharacterEntityAuxPatch");
```

All three throws still present in the transpiler, applied at `OnGameInitializationFinished` via category `Late_Transpiler`. Cluster F closure (line 219) confirmed the IL pattern exists in v1.3.15 — but the hard-throw concern (defensive soft-fail) remains a valid recommendation. **VALID P2.**

### #161 — ArmyTargeting Patch22 uncached IoC.Resolve — **VALID (P2 perf)**

`AiMilitaryBehavior_CalculateDistanceScoreForBesieging_Patch.cs:22-40`:

```csharp
try
{
    var service  = IoC.Resolve<IArmyTargetingService>();
    var settings = IoC.Resolve<IArmyTargetingSettingsProvider>();
    ...
    IoC.Resolve<IModLogger>().LogDebug(...);
}
```

Three uncached `IoC.Resolve` calls per Postfix invocation. The early-exit at line 20 (`if (bestDistanceScore > 0f) return;`) reduces but doesn't eliminate impact for the zero-score path the patch targets. **VALID.**

### #162 — CustomBattles Patch19_OnCultureSelection v1.3.15 unverified — **VALID (cleared via Cluster F → P3)**

`CustomBattleSideVM_OnCultureSelection_Patch.cs:9`:

```csharp
[HarmonyPatch(typeof(CustomBattleSideVM), "OnCultureSelection", new[] { typeof(BasicCultureObject) })]
```

Cluster F closure (line 218) confirmed the method exists with the expected signature in v1.3.15. The original "patch may fail to apply" concern is **cleared**. The defensive null-guard recommendation (add `static MethodBase TargetMethod()` with LogWarning) remains valid as low-priority hardening. **Severity drops to P3-ish; keep open as a defensive hardening item.**

### #163 — SpawnNonHuman Finalizer swallows all NREs — **VALID (P2)**

`CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:148-156`:

```csharp
[HarmonyFinalizer]
static Exception Finalizer(Exception __exception)
{
    if (__exception is ArgumentNullException ane && ane.ParamName == "key")
        return null;
    if (__exception is NullReferenceException)
        return null;
    return __exception;
}
```

`NullReferenceException` is swallowed unconditionally (no horse-data specific filter). Audit description verified verbatim. **VALID.**

### #164 — Consolidated P3 cleanups — **VALID (each sub-item)**

Bare `catch {}` sub-items (re-confirmed at HEAD):
- `Patch35_OOBHeroItem_GetCaptainTooltip.cs:28` — `catch { }` confirmed
- `Patch35_Formation_SetMovementOrder.cs:36` — `catch { }` confirmed
- (`GuardsCampaignBehavior_TakeGuardAgentData_Patch.cs:68-71` — covered in #157)

Missing `[HarmonyPostfix]` attribute (verified at HEAD):
- `Campaign_InitializeDefaultCampaignObjects_Patch.cs:11` — `public static void Postfix()` has no attribute
- `PartyCharacterVM_InitializeUpgrades_Patch.cs:21` — `public static void Postfix(...)` has no attribute
- `PartyScreenLogic_UpgradeTroop_Patch.cs:21` — `public static void Postfix(...)` has no attribute

Missing `else LogWarning(...)` (verified at SubModule.cs:461-471):
```csharp
var leaderTarget = MapConversationTableau_SpawnOpponentLeader_Patch.TargetMethod();
if (leaderTarget != null)
    _harmony.Patch(leaderTarget, postfix: new HarmonyMethod(...));

var bodyguardTarget = MapConversationTableau_SpawnOpponentBodyguard_Patch.TargetMethod();
if (bodyguardTarget != null)
    _harmony.Patch(bodyguardTarget, postfix: new HarmonyMethod(...));
```
No `else` branches on either. Confirmed.

Missing `?` on nullable static fields:
- `Patch35_Formation_SetMovementOrder.cs:21-22` — `private static ICompanionTacticsSettingsProvider _settings;` / `private static ITroopStanceManager _stances;` — no `?` despite `??=` init. Confirmed.

Missing param type array on Patch31:
- `Patch31_FormationSetMovementOrder.cs:20` — `[HarmonyPatch(typeof(Formation), nameof(Formation.SetMovementOrder))]` — no `new[] { typeof(MovementOrder) }`. Confirmed.

Missing `[HarmonyPrefix]` attribute:
- `PartyScreenLogic_AddCommand_Patch.cs:27` — `public static bool Prefix(...)` has no `[HarmonyPrefix]`. Confirmed.

All sub-items VALID. **VALID P3.**

## Closing comment drafts (STALE only)

### #154 closing comment

> Verification at HEAD `b4b4de1`: `Main/Features/FiefManagement/UI/GauntletFiefManagementScreen.cs:16` already declares the class as `public class GauntletFiefManagementScreen : ScreenBase, IGameStateListener` and lines 34-37 implement `OnActivate`/`OnDeactivate`/`OnInitialize`/`OnFinalize`. The interface has been present since `1cad3a7` (the initial Patch36 port commit) — `git log -S "IGameStateListener" -- Main/Features/FiefManagement/UI/GauntletFiefManagementScreen.cs` returns that single commit. The audit's verification request is satisfied with no code change needed. Closing as stale.

## Blockers / notes

- No ilspycmd runs needed in this batch — Cluster F closure section of `cluster-harmony-patches.md` (lines 210-228) already resolved all v1.3.15-unverified flags for issues #157, #159, #160, #162. Cluster F2 (re-verifying `AgentVisuals` ctor + `MBAgentVisuals.CreateAgentVisuals` + `ClothColor1/2` setter behavior in v1.3.15) is queued for Phase 9 BEFORE issue #150's fix lands — not blocking this triage.
- No new findings filed inline. One drive-by note: the bare `catch { }` on `Patch31_FormationSetMovementOrder.cs` ❌ — actually Patch31 has no try/catch; the Postfix relies on cheap null-checks. So Patch31 is not part of the bare-catch family despite proximity to Patch35.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/audits/triage-results.md](./triage-results.md)

<!-- backlinks-end -->
