# Batch C verification (#165–#175 + #196–#199)

Verified by: general-purpose agent, Phase 9a, 2026-05-13
Inputs: triage-input-batch-C.json + cluster-ui.md + cluster-cross-feature.md + docs-gaps.md
HEAD: b4b4de1 fix(messengers): wire IoC + CampaignBehavior (#121)

## Summary table

| Verdict | Count | Issues |
|---|---|---|
| VALID | 13 | #165, #166, #168, #169, #170, #171, #172, #173, #174, #175, #196, #198, #199 |
| VALID with FALSE-POSITIVE sub-finding | 1 | #167 (P2 #8 ordering claim is FP; other 5 sub-findings VALID) |
| VALID with SEVERITY-DRIFT | 1 | #197 (doc bug exists but underlying premise is stale — severity drops P2 → P3) |
| STALE | 0 | — |
| FALSE-POSITIVE | 0 | — |
| DUPLICATE | 0 | — |

## Per-issue verification table

| # | Verdict | Severity confirmed | Key evidence |
|---|---|---|---|
| 165 | VALID | 4 P1 + 2 P3 | 21 portraits present (29 missing of 50); 9 ability sprites (41 missing); 0 `career_choice_*` sprites; CareerChoiceObjectVM:43 uses raw `_choice.Id`; CareerButtonPrefab:34 hardcoded `Text="Career"` |
| 166 | VALID | 1 P1 + 1 P2 | Lines 143/160/174 all `ViewModel?.OnPropertyChangedWithValue(...)` (notifies host, not mixin); `SendMessengerCost` declared line 149, no matching `@SendMessengerCost` binding in MessengerEncyclopediaPrefabExtension.cs |
| 167 | VALID (P2 #8 sub-finding is FP) | 1 P1 + 2 P2 + 2 P3 (sub-finding #8 P2 invalid) | 3 of 11 sprites present (8 missing); SecondaryInfoItems.Add at line 55 confirmed; `_loggedOnce` shared at lines 45/60/71; log path string says MapBar/ at line 62; **BUT** ordering #8 is FALSE — Value (line 64) is already set before IntValue (line 65) |
| 168 | VALID | 1 P2 + 1 P3 | Line 54: `IsExtraFastForwardActive = Campaign.Current.SpeedUpMultiplier > 4f;` — `SpeedUpMultiplier` never raised by `ExecuteTimeControlChange(2)`; Line 18: hardcoded English literal |
| 169 | VALID | 6 P2 + 3 P3 | PolygonWidget:378, 409, 432 per-frame `CreateSimpleMaterial()`; BannerWidget:223 inside 8-iteration loop; PolygonWidget:685 `RemoveAt` while OnRender indexes; PolygonWidget:639/647 static write from OnRender; SpecialResourceSpriteWidget:39-40 IoC.Resolve in OnLateUpdate |
| 170 | VALID | 2 P2 | FormationLayoutService:74 + :191 cavalry guards present with cross-feature comments; FormationLayoutServiceTests.cs has zero `RepresentativeIsCavalry`/cavalry tests; CavalryChargeService:33 plain Dictionary, no lock |
| 171 | VALID | 2 P2 | RacePersistenceService:30 only `> 0` guard, no validity check vs RaceManager; Two unrelated Prefixes on `RefreshAgentVisuals` from Patch20 + Patch29 with no ordering attribute |
| 172 | VALID | 3 P2 | Clan_UpdateBannerColorsAccordingToKingdom_Patch:18 Prefix has no `__instance`, blocks all clans; line 15 `AccessTools.Method` with no null-log; BannerInjectionBehavior:16-19 registers both event listeners — ordering depends on Patch24 activation timing |
| 173 | VALID | 1 P1 + 3 P2 | CareerPassiveHelper:27/32 static IoC.Resolve cache; CareerPassiveService:11-12 plain Dictionary + :21 Clear; TaomPartyWageModel:65-79 inline foreach in GameModel body; TaomSmithingModel:46 truncates to int before ApplyFactor at :51 |
| 174 | VALID | 1 P2 | SpecialResourceService:200 `QueueUpgradeSpend` uses base `cost.UpgradeCost * count`; SpecialResourceService:217 `ClampUpgradeCount` uses `GetEffectiveUpgradeCost` (with career discount) — debit ignores discount |
| 175 | VALID | 2 P2 | CultureStageViewCreatedHook:25 `_factionVM` static, :56 ResetSession before Cleanup; PolygonWidget:85 `_pendingPins` static; ResetSession at :120-130 does NOT include `_pendingPins.Clear()` |
| 196 | VALID | P1 | `docs/features/execution.md` does NOT exist (glob finds only `alignment-aware-execution.md`); `Main/Features/Execution/` has 12 C# files including TaomExecutionRelationModel, AlignmentService, Patch14_Execution hooks |
| 197 | VALID with SEVERITY-DRIFT | Drops P2 → P3 | `companion-tactics.md:185` disclosure exists. **However:** csproj no longer excludes CompanionTactics (line 67: "Sibling parallel-ports restored 2026-05-07"); SubModule.cs:361, 409, 418, 496 actively register CompanionTactics integration. Fix is to **remove** the stale disclosure entirely, not "move to Overview." Doc-code drift only — feature works. |
| 198 | VALID | P2 | `advanced-combat.md:71` says "No unit tests exist for AdvancedCombat services"; `BoneCollisionServiceTests.cs` is 252 lines with 11 `[TestMethod]` attributes |
| 199 | VALID | P2 | `warg-combat.md:117` says "No dedicated test files (ported from LOTRAOM without TDD)"; `WargAttackServiceTests.cs` exists with 7 `[TestMethod]` |

---

## Per-issue detailed verification

### #165 — CareerSystem UI sprite + localization gaps — VALID

**Source files quoted (current HEAD):**

`Main/_Module/GUI/SpriteParts/ui_taom_career_system/CareerSystem/Portraits/` contains 21 PNGs (counted via Glob). 50 careers defined in `taom_careers.xml` ⇒ 29 missing. Audit list checked against actual files — every named missing portrait (e.g., `mulkerhili_cultist`, `snaga_rider`) is absent.

`Main/_Module/GUI/SpriteParts/ui_taom_career_system/CareerSystem/Abilities/` contains 9 PNGs:
```
captain_of_osgiliath_ability.png, crossbow_master_ability.png,
eotheod_windrider_ability.png, ironguard_ability.png,
knight_of_belfalas_ability.png, marksman_of_aldburg_ability.png,
ram_rider_ability.png, ranger_of_ithilien_ability.png,
watchman_of_stangard_ability.png
```
Matches the 9 registered list from audit; 41 missing.

`Main/Features/CareerSystem/UI/CareerScreenVM.cs:101` & `:107`:
```csharp
CareerPortraitSprite = $"CareerSystem\\Portraits\\{career.PortraitSprite}";
...
AbilitySpriteName = $"CareerSystem\\Abilities\\{career.AbilityTemplateId}";
```

`CareerChoiceObjectVM.cs:43` & `:49`:
```csharp
public string Name => new TextObject(_choice.Id).ToString();
...
public string IconSprite => _choice.IconSprite;
```
No `career_choice_*` PNGs found anywhere under SpriteParts.

`CareerButtonPrefab.cs:34`: `"Text=\"Career\" "` — raw English, no `{=key}`.

**Fix (short):** add 29 portrait PNGs + 41 ability PNGs + N career_choice_* PNGs + `<SpritePart>` entries; add `DisplayName` field to `CareerChoiceDefinition` (P3 #18); add `{=taom_career_button}Career` binding to Career button (P3 #19).
**Severity confirmed:** 4 P1 + 2 P3 as audit.

---

### #166 — Messengers mixin notification bug — VALID

`MessengerEncyclopediaMixin.cs:12` extends `BaseViewModelMixin<EncyclopediaHeroPageVM>`. Lines 143/160/174 all use:
```csharp
ViewModel?.OnPropertyChangedWithValue(value, nameof(...));
```
This notifies the host `EncyclopediaHeroPageVM`, not the mixin. Sibling mixins `TimeAccelerationMixin.cs:30` correctly call `OnPropertyChanged(nameof(...))` on `this`.

`SendMessengerCost` declared at line 149 with `[DataSourceProperty]`. Grep across `Main/Features/Messengers/` finds zero `@SendMessengerCost` references — only `@SendMessengerActionName`, `{SendMessengerHint}` are bound in `MessengerEncyclopediaPrefabExtension.cs:41,48`.

**Fix:** replace 3× `ViewModel?.OnPropertyChangedWithValue(...)` with `OnPropertyChangedWithValue(...)` (on `this`); decide whether to bind `@SendMessengerCost` to a TextWidget or remove the property.
**Severity confirmed:** 1 P1 + 1 P2.

---

### #167 — SpecialResources UI — VALID with one FALSE-POSITIVE sub-finding

**P1 #6 — VALID.** `Main/_Module/GUI/SpriteParts/ui_taom/SpecialResources/` contains exactly 3 PNGs: `taom_caster_icon.png`, `taom_gems_icon.png`, `taom_marks_icon.png`. 8 missing per audit confirmed.

**P2 #7 SecondaryInfoItems.Add — VALID.** `SpecialResourceMapBarMixin.cs:55`:
```csharp
mapInfo.SecondaryInfoItems.Add(_resourceInfo);
```
Plus `_baseInitialized` guard at line 70 set AFTER the Add block (line 53 reads it).

**P2 #8 property assignment ordering — FALSE-POSITIVE.** Audit claims `IntValue (line 65) set before Value (line 64)`. Current code at lines 64-65:
```csharp
_resourceInfo.Value = intAmount.ToString();
_resourceInfo.IntValue = intAmount;
```
Value (line 64) is set FIRST, then IntValue (line 65). Pre-audit commit `6b6ec06` shows identical order. The audit's recommended fix ("assign Value first, then IntValue") matches current code. No commits to this file post-audit. The audit misread the ordering.

**P2 #9 PrefabExtension XPath — VALID.** `SpecialResourcePrefab.cs:12` 6-hop XPath as quoted in audit.

**P3 #20 — VALID.** `SpecialResourceMapBarMixin.cs` lines 98, 104, 105, 112-117, 121-124 use raw English strings ("Tier", "Next tier at", "Daily Change", "Income", "Net", "Per battle") without `TextObject` wrap.

**P3 #21 — VALID.** `SpecialResourceSpriteWidget.cs` lines 45, 60, 71 share `_loggedOnce` flag across three distinct outcomes. Line 62 log message says `SpriteParts/ui_taom/MapBar/` (wrong path — should be `SpecialResources/`).

**Severity confirmed:** drop the P2 #8 — overall: 1 P1 + 2 P2 + 2 P3.

---

### #168 — TimeAcceleration wrong state signal + hardcoded tooltip — VALID

`TimeAccelerationMixin.cs:54`:
```csharp
IsExtraFastForwardActive = Campaign.Current.SpeedUpMultiplier > 4f;
```
`TimeAccelerationMixin.cs:18`:
```csharp
_extraFastForwardHint = new BasicTooltipViewModel(() => "Extra Fast Forward (E)");
```
Nothing in the mixin or button command path raises `SpeedUpMultiplier` above its default 4f. Vanilla `ExecuteTimeControlChange(2)` touches `TimeFlowState`/`TimeControlMode`, not `SpeedUpMultiplier`. P2 confirmed.

**Fix:** decide intent (Option A use `TimeControlMode == StoppableFastForward`; Option B add service call that raises `SpeedUpMultiplier`); wrap tooltip in `TextObject("{=taom_extra_fast_forward_hint}...")`.

---

### #169 — Custom widget allocations + threading concerns — VALID

`PolygonWidget.cs:378, 409, 432` each call `drawContext.CreateSimpleMaterial()` per render frame, with line 409 inside a `for (float y = edgePx; y >= 1f; y -= 1f)` loop (up to 7 iterations).
`BannerWidget.cs:223` inside nested `foreach (float oy in offsets)` loop = up to 8 `CreateSimpleMaterial()` per frame per banner.
`PolygonWidget.cs:685`:
```csharp
_allInstances.RemoveAt(i);
```
Static-list mutation in ResolveGlobalHover() (called from OnLateUpdate).
`PolygonWidget.cs:639, 647`: `HoveredFactionName = _factionDisplayName;` — static write from OnRender path.
`SpecialResourceSpriteWidget.cs:39-40`:
```csharp
_config ??= IoC.Resolve<ISpecialResourceConfigProvider>();
_logger ??= IoC.Resolve<IModLogger>();
```
Inside `OnLateUpdate(float dt)`. P3 #24 dead code confirmed at PolygonWidget:253-263 + 1001-1032.

**Note:** #175 below notes Gauntlet threading is single-threaded for TAOM widgets (confirmed via v1.4 decomp). That downgrades #15 and #16 to P3. The other findings in #169 remain at audit severity.

---

### #170 — SmartCavalryAI × MixedFormations test gap + lock asymmetry — VALID

**F1 (P2)** Code guards present:
- `FormationLayoutService.cs:74`: `if (formation.RepresentativeIsCavalry) return null;` (in ComputeUnitPlanePosition)
- `FormationLayoutService.cs:191`: `if (formation.RepresentativeIsCavalry) return false;` (in IsMixedFormationInternal)

Both have inline comments referencing the 2026-05-06 Codex finding. `TAOM.Tests/Features/MixedFormations/` has 2 test files; grep for `RepresentativeIsCavalry`/`cavalry`/`Cavalry` returns zero matches. Test gap confirmed.

**F2 (P2)** `CavalryChargeService.cs:33`:
```csharp
private readonly Dictionary<object, CavalryFormationState> _states = new();
```
No lock declared anywhere in the file. Sibling `FormationLayoutService.cs` holds a `_lock` for `_layoutByFormation`. Asymmetry confirmed.

---

### #171 — CharacterCreation × HeroRace × RaceAge — VALID

**F1 (P2)** `RacePersistenceService.cs:30-34`:
```csharp
if (hero.Race > 0 && !_heroRaceMap.ContainsKey(hero.StringId))
{
    _heroRaceMap[hero.StringId] = hero.Race;
}
```
Only positivity guard; no validity check vs `IRaceManager.IsValidRaceId`. `RestoreHeroRaces` at lines 50-57 writes the saved int directly via `_heroRosterAdapter.SetHeroRace`.

**F2 (P2)** Two Prefixes on same target method:
- `CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:179-205` (Patch20)
- `CharacterCreationNarrativeStageView_RefreshAgentVisuals_BodySync_Patch.cs:33-64` (Patch29)

Different target field domains (Race vs BodyProperties), no `[HarmonyBefore]`/`[HarmonyAfter]`, no cross-reference comments. Implicit ordering relied upon.

---

### #172 — Banner triplet — VALID

**F2 (P2)** `Clan_UpdateBannerColorsAccordingToKingdom_Patch.cs:17-18`:
```csharp
[HarmonyPrefix]
public static bool Prefix() => !(_service?.IsDriftGuardEnabled() ?? false);
```
No `Clan __instance` parameter → blocks every clan, not just player.

**F3 (P2)** Same file, line 14-15:
```csharp
public static MethodBase TargetMethod() =>
    AccessTools.Method(typeof(Clan), "UpdateBannerColorsAccordingToKingdom");
```
No null-check, no logger injection. Silent failure mode if TaleWorlds renames private method.

**F5 (P2)** `BannerInjectionBehavior.cs:16-19`:
```csharp
CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(
    this, _ => _service.InjectBanners());
CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(
    this, _ => _service.InjectBanners());
```
Asymmetry between new-game and load paths re: Patch24 active state is real and undocumented in the file. Valid P2.

---

### #173 — CareerSystem × TroopProgression via CareerPassiveHelper — VALID

**F1 (P2)** `CareerPassiveHelper.cs:25-33`:
```csharp
private static ICareerPassiveService GetService()
{
    return _cachedService ?? (_cachedService = IoC.Resolve<ICareerPassiveService>());
}
private static IModLogger GetLogger()
{
    return _cachedLogger ?? (_cachedLogger = IoC.Resolve<IModLogger>());
}
```
Static service locator. Static caches are non-volatile.

**F2 (P2)** `CareerPassiveService.cs:11-12`:
```csharp
private Dictionary<string, Dictionary<PassiveEffectType, float>> _cache
    = new Dictionary<string, Dictionary<PassiveEffectType, float>>();
```
Line 21: `_cache.Clear()` then in-place rebuild. Line 72: `_cache.TryGetValue` from GameModel hot paths.

**F3 (P1)** `TaomPartyWageModel.cs:65-79`:
```csharp
if (partyCulture.HasFeat(TaomCulturalFeats.RohanMountedWageFeat) && troopRoster != null)
{
    float baseWageTotal = result.BaseNumber;
    if (baseWageTotal > 0f)
    {
        float mountedWageTotal = 0f;
        foreach (var element in troopRoster.GetTroopRoster())
        {
            if (element.Character?.IsMounted == true)
                mountedWageTotal += GetCharacterWage(element.Character) * element.Number;
        }
        float mountedWageShare = mountedWageTotal / baseWageTotal;
        result.AddFactor(...);
    }
}
```
Inline foreach + multi-step computation in GameModel body. Violates `gamemodels.md` rule 4 (no inline branching, no matter how short).

**F4 (P2)** `TaomSmithingModel.cs:46-52`:
```csharp
int featResult = factor != 0f ? (int)(baseCost * (1f + factor)) : baseCost;
if (hero != null)
{
    var explained = new ExplainedNumber(featResult, false);
    CareerPassiveHelper.ApplyFactor(hero, ref explained, PassiveEffectType.EnchantmentCostReduction);
    return (int)explained.ResultNumber;
}
```
Intermediate `(int)` cast at line 46 truncates before career factor composition at line 51.

---

### #174 — SpecialResources × CareerSystem inventory upgrade — VALID

**F2 (P2)** `SpecialResourceService.cs:195-203` (QueueUpgradeSpend):
```csharp
var cost = _config.GetTroopCost(troopId);
if (cost == null) return;

var added = cost.UpgradeCost * count;
_pendingSpend += added;
```
Uses base `cost.UpgradeCost`, no `GetEffectiveUpgradeCost` call.

`SpecialResourceService.cs:212-228` (ClampUpgradeCount):
```csharp
var effectivePerUnit = GetEffectiveUpgradeCost(heroId, cost.UpgradeCost, 1);
```
Applies career discount for capacity decision. Asymmetry → player overpays.

**Fix:** `QueueUpgradeSpend` should compute `_pendingSpend += GetEffectiveUpgradeCost(heroId, cost.UpgradeCost, 1) * count`.

---

### #175 — FactionMap × CC widget lifecycle — VALID

**F6 (P2)** `CultureStageViewCreatedHook.cs:25`:
```csharp
private static FactionSelectionVM? _factionVM;
```
Line 130 `Cleanup()` sets it to null. `OnCreated` at line 52 starts with `PolygonWidget.ResetSession()` (line 56) but does NOT call `Cleanup()` first. If engine sequences construct-new before finalize-old, the old `_factionVM` survives one extra frame after the new session's `ResetSession`.

**F7 (P2)** `PolygonWidget.cs:85-86`:
```csharp
private static readonly System.Collections.Generic.List<PinRenderData> _pendingPins
    = new System.Collections.Generic.List<PinRenderData>();
```
`ResetSession()` at lines 120-130 clears `_allInstances`, `_globalHovered`, `_nextPlayableIndex`, `_totalPlayable`, `_globalPulseTimer/Index/Alpha`, `HoveredFactionName` — but NOT `_pendingPins`. Audit fix is correct (add `_pendingPins.Clear()`).

---

### #196 — Execution doc missing — VALID

`Glob docs/features/execution*.md` returns no match. `Glob docs/features/*execution*.md` returns only `alignment-aware-execution.md` (an Execution subsystem doc). `Main/Features/Execution/` exists with 12 C# files (AlignmentService, TaomExecutionRelationModel, KillCharacterAction_ApplyInternal_Patch, TraitLevelingHelper_OnLordExecuted_Patch, etc.). The `detect-docs-gaps.sh` SessionStart hook still fires this gap.

P1 confirmed. Fix per audit Phase 9 sketch: create `docs/features/execution.md` as primary doc with cross-link to `alignment-aware-execution.md`.

---

### #197 — CompanionTactics doc disclosure — VALID with SEVERITY-DRIFT

`docs/features/companion-tactics.md:185` still contains the build-disabled disclosure as audit describes.

**However**, the underlying premise is stale. Current HEAD shows CompanionTactics is fully active:
- `Main/TAOM.csproj:67`: `<!-- Sibling parallel-ports restored 2026-05-07: SmartCavalryAI, CompanionTactics. -->` — no `<Compile Remove>` line for CompanionTactics
- `Main/SubModule.cs:361, 409, 418, 496`: all active integration sites (FormationPresetCampaignBehavior register, Patch35 PatchCategory, manual GetCaptainTooltip patch, BattleActionBarMissionView)

Commit `0cc457f` (2026-05-07 09:07:11) restored Patch35 integration. The doc text at line 185 was already stale by audit date (2026-05-13).

**Severity drop P2 → P3.** The bug class is "stale doc claim" same as #198/#199, not "feature is broken and reader is misled." Fix should be **remove the disclosure entirely** (Phase 9 sketch step 3), not "move to Overview." This is one line of housekeeping, not an Overview restructure.

---

### #198 — AdvancedCombat stale "no tests" claim — VALID

`docs/features/advanced-combat.md:71`:
> No unit tests exist for AdvancedCombat services in `TAOM.Tests/Features/`.

`TAOM.Tests/Features/AdvancedCombat/BoneCollisionServiceTests.cs` exists at 252 lines with 11 `[TestMethod]` attributes. Glob confirms the file is the only test file in that folder — `SpatialGridDebugService` remains uncovered, but `BoneCollisionService` has 11 tests. The doc is stale. P2 confirmed.

---

### #199 — Warg stale "no tests" claim — VALID

`docs/features/warg-combat.md:117`:
> - **Current:** No dedicated test files (ported from LOTRAOM without TDD)

`TAOM.Tests/Features/Warg/WargAttackServiceTests.cs` exists with 7 `[TestMethod]`. Doc is stale. P2 confirmed.

---

## New findings during verification

None.

## Notes

- Severity calls follow audit conventions (P1 = silent player-impact / crash class, P2 = degradation or smell, P3 = polish).
- All STALE-checks done via `git log --since="2026-05-13"` per file. No relevant post-audit commits found for any cited file path.
- #197 is the only severity adjustment — the disclosure exists (doc bug VALID) but the underlying state changed pre-audit (`0cc457f`, 2026-05-07). Phase 9b fix is a 1-line removal, not a restructure.
- #167's P2 #8 ordering finding is the only confirmed false-positive sub-finding; the other 5 sub-findings in that issue are VALID and the issue stays VALID overall.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/audits/triage-results.md](./triage-results.md)

<!-- backlinks-end -->
