# UI / Mixin / Prefab Cluster Audit — Phase 5

Last updated: 2026-05-13
Inputs: [feature-manifest.md](feature-manifest.md), [wiring-matrix.md](wiring-matrix.md), [.claude/rules/gui-ui.md](../../.claude/rules/gui-ui.md), [phase-5-kickoff.md](phase-5-kickoff.md)
Scope: 5 review targets — 4 features with `[ViewModelMixin]` / `[PrefabExtension]` (CareerSystem, Messengers, SpecialResources, TimeAcceleration) + 5 custom Widget classes (FactionMap × 4 + SpecialResources × 1)
Method: 5 `feature-dev:code-reviewer` agents in parallel — each applies the 7 Phase-5 gates (sprite verification, PrefabExtension target safety, VM setter no-op guard, VM notification pattern, `@PropertyName` case-sensitivity, localization, `IGameStateListener` gap) against its slice.

## Executive summary

**25 findings across 5 surfaces: 6 P1, 12 P2, 7 P3.** No GameStateScreen crashes (Check 7 passes everywhere it applies). One pre-loaded rule violation confirmed (`SpecialResources` `SecondaryInfoItems.Add`) but downgraded from likely-P1 to **P2** after v1.3.15 decompile showed `HandlePanelSwitchingInput` no longer indexes the list positionally — the specific crash that motivated the rule is not reachable. The rule itself is still violated; the unreliable-feature consequence is different from the originally-documented crash.

**The dominant Phase 5 failure mode is sprite-asset gaps** — not UI logic bugs:
- **CareerSystem:** 29 of 50 portrait sprites missing, 41 of 50 ability sprites missing, **0 of N choice icons registered** (every `career_choice_*` sprite ID is missing). Silent blank UI; no crash, no log.
- **SpecialResources:** 8 of 11 resource icons missing PNGs and `TAOMSpriteData.xml` entries.

These are P1 because they produce permanently broken UI on every affected screen with no diagnostic. They look like a P3 ("nice to have art") at a glance, but the impact is binary: a feature that lists "50 careers" or "11 special resources" actually delivers blank visuals for 60–95% of them.

**Second dominant pattern: VM property notification confusion** (CareerSystem-tier features get this right; Messengers does not):
- **Messengers** notifies `ViewModel?.OnPropertyChangedWithValue(...)` on the HOST `EncyclopediaHeroPageVM` instead of the mixin itself. Gauntlet associates the binding with the mixin's datasource — host-VM notifications never reach the bound widget, so `@IsMessengerAvailable`/`@SendMessengerActionName`/`@SendMessengerHint` never update post-mount.

**Third pattern: custom Widget hot-path discipline** (FactionMap):
- Per-frame `SimpleMaterial` allocations in `PolygonWidget.OnRender` (×7 edge loop) and `BannerWidget.OnRender` (×8 glow loop).
- Static-list mutation in `OnLateUpdate` while `OnRender` indexes it (no lock; assumes serial render).
- `HoveredFactionName` cross-thread write from `OnRender` to game-logic readers.

**TimeAcceleration** has a semantic correctness gap — `IsExtraFastForwardActive` watches `Campaign.Current.SpeedUpMultiplier > 4f` but the button itself fires `ExecuteTimeControlChange(2)` which does NOT modify `SpeedUpMultiplier`. The "active" state is permanently false in normal play; only cheat-console use of `campaign.set_campaign_speed_multiplier` would ever raise it.

## Manifest corrections (carry forward)

- **Sprite atlas naming:** `feature-manifest.md` shows CareerSystem atlas as `ui_taom_career_system`. Confirmed by `Config.xml` registration. Within that atlas, sprite IDs are `CareerSystem\Portraits\<id>` etc. — the subfolder prefix `CareerSystem` is required.
- **Prefab XML location:** the original audit plan assumed `Main/_Module/GUI/PreFabs/` (camel-cased "PreFabs"). Actual path is `Main/_Module/GUI/Prefabs/` (no camel). UIExtenderEx prefab extensions for Messengers + TimeAcceleration + SpecialResources use inline `XmlDocument.LoadXml(...)` strings; standalone prefab XML only exists for CareerSystem (`CareerScreen.xml`, `AbilityHUD.xml`) and a few other features.

## Master findings table (sorted by severity)

| # | Severity | Feature | Component | File:Line | Finding | Issue |
|---|---|---|---|---|---|---|
| 1 | **P1** | CareerSystem | Sprite registration — portraits | `Main/_Module/ModuleData/career_system/taom_careers.xml` (all 50) + `CareerScreenVM.cs:101` | 29 of 50 portrait sprites referenced via `CareerSystem\Portraits\{career.PortraitSprite}` have no PNG and no `TAOMSpriteData.xml` entry. Silent blank portrait panel. | #165 |
| 2 | **P1** | CareerSystem | Sprite registration — ability icons | `CareerScreenVM.cs:107` + `Prefabs/CareerSystem/CareerScreen.xml:73` | 41 of 50 ability sprite IDs constructed as `CareerSystem\Abilities\{AbilityTemplateId}` have no PNG and no `TAOMSpriteData.xml` entry. Silent blank icon. | #165 |
| 3 | **P1** | CareerSystem | Sprite registration — choice icons | `CareerChoiceObjectVM.cs:49` + `CareerScreen.xml:118,120,163,165,204,205` | Zero `career_choice_*` sprite IDs registered anywhere. All choice icons blank for every career, all three tiers. | #165 |
| 4 | **P1** | CareerSystem | Sprite registration — HUD ability | `Prefabs/CareerSystem/AbilityHUD.xml:18` | `Sprite="@AbilitySprite"` resolves to `CareerSystem\Abilities\{ability_template_id}` at runtime — same 41-missing-sprite set as finding #2 → blank HUD icon during missions. | #165 |
| 5 | **P1** | Messengers | VM notification pattern | `MessengerEncyclopediaMixin.cs:143, 160, 174` | All 3 `[DataSourceProperty]` setters call `ViewModel?.OnPropertyChangedWithValue(value, nameof(...))` — firing notification on the HOST `EncyclopediaHeroPageVM`, not the mixin. Bindings never update post-mount. (`TimeAccelerationMixin:30` and `CharacterDeveloperCareerMixin:53` correctly call `OnPropertyChanged(nameof(...))` on `this`.) | #166 |
| 6 | **P1** | SpecialResources | Sprite registration | `special_resources_config.xml:6,60,74,84,98,108,118,128` + `SpecialResourceSpriteWidget.cs:57` | 8 of 11 resource sprites missing PNGs and `TAOMSpriteData.xml` entries (`taom_war_spoils_icon`, `taom_elven_wine_icon`, `taom_lake_fish_icon`, `taom_war_drums_icon`, `taom_tribal_relics_icon`, `taom_dunlending_ale_icon`, `taom_plunder_icon`, `taom_war_banners_icon`). Widget's log message also gives the wrong path (says `MapBar/`, should be `SpecialResources/`). | #167 |
| 7 | P2 | SpecialResources | UIExtenderEx safety / `SecondaryInfoItems.Add` rule violation | `SpecialResourceMapBarMixin.cs:55` | Calls `mapInfo.SecondaryInfoItems.Add(_resourceInfo)` — gui-ui.md "NEVER DO THIS". v1.3.15 decompile shows the documented `HandlePanelSwitchingInput` crash is NOT reachable, but `MapInfoVM.CreateItems()` (called on every `RefreshValues()`) `Clear()`s + repopulates the list. If vanilla refresh fires after TAOM's `Add`, the TAOM item is silently dropped and `_itemAdded` stays true → permanently suppressed re-insertion. Also: `_baseInitialized` guard is set at line 70 *after* the Add block at line 55, so on the very first refresh the item is skipped. | #167 |
| 8 | P2 | SpecialResources | VM setter ordering | `SpecialResourceMapBarMixin.cs:64–66` | `_resourceInfo.IntValue` assigned before `_resourceInfo.Value` — notification on `IntValue` may trigger a one-frame stale render before `Value` updates. Reverse the order. | #167 |
| 9 | P2 | SpecialResources | PrefabExtension target fragility | `SpecialResourcePrefab.cs:11–12` | 6-hop XPath `descendant::ListPanel[@Id='BottomInfoBar']/ItemTemplate/HintWidget/Children/ListPanel/Children/IconBrushWidget` — any vanilla v1.3.15 → next-version restructure of the BottomInfoBar item template silently breaks injection. Verify against installed v1.3.15 `MapBar.xml`. | #167 |
| 10 | P2 | Messengers | Unused `[DataSourceProperty]` | `MessengerEncyclopediaMixin.cs:149` | `SendMessengerCost` declared `[DataSourceProperty]` but never bound by any `@SendMessengerCost` in the inline XML; the button text shows only the action name. Either bind to a visible widget or remove. | #166 |
| 11 | P2 | TimeAcceleration | Semantic correctness — wrong state signal | `TimeAccelerationMixin.cs:54` | `IsExtraFastForwardActive = Campaign.Current.SpeedUpMultiplier > 4f`. But the button's `CommandParameter.Click="2"` fires `ExecuteTimeControlChange(2)` which does NOT raise `SpeedUpMultiplier` (default 4f, only mutated by cheat console). The button's selected-state visual is permanently false. Use `TimeControlMode` or actually raise `SpeedUpMultiplier` from the command. | #168 |
| 12 | P2 | Custom Widgets | Per-frame allocation — PolygonWidget edge loop | `Main/Features/FactionMap/Widgets/PolygonWidget.cs:408–429` | `OnRender` allocates a `SimpleMaterial` per iteration of the edge-thickness loop (up to 7 per render frame per lifted widget). At 60fps × 42 instances → ~17k allocations/sec in render path. Hoist a single material outside the loop. | #169 |
| 13 | P2 | Custom Widgets | Per-frame allocation — PolygonWidget shadow | `Main/Features/FactionMap/Widgets/PolygonWidget.cs:377–397` | Shadow pass `CreateSimpleMaterial()` per frame the widget is in non-zero lift. Same fix. | #169 |
| 14 | P2 | Custom Widgets | Per-frame allocation — BannerWidget glow loop | `Main/Features/FactionMap/Widgets/BannerWidget.cs:216–241` | 8 `SimpleMaterial` allocations per render frame per visible banner with non-neutral side. ~480 allocs/sec/banner. | #169 |
| 15 | P2 | Custom Widgets | Static-list race | `Main/Features/FactionMap/Widgets/PolygonWidget.cs:680–686` | `ResolveGlobalHover()` (from `OnLateUpdate`) mutates `_allInstances` (RemoveAt) while `OnRender` indexes `_allInstances[0]`/`[last]`. Safe only if Gauntlet renders single-threaded per widget — confirm or snapshot count before render pass. | #169 |
| 16 | P2 | Custom Widgets | Cross-thread static write | `Main/Features/FactionMap/Widgets/PolygonWidget.cs:636–648` | `HoveredFactionName` (`public static string`) written from `OnRender`, read by VM `OnLateUpdate`. Move the write to `ResolveGlobalHover()` (already on `OnLateUpdate` path) or use `volatile`/`Interlocked.Exchange`. | #169 |
| 17 | P2 | Custom Widgets | Service locator in widget hot path | `Main/Features/SpecialResources/UI/SpecialResourceSpriteWidget.cs:39–40` | `IoC.Resolve<ISpecialResourceConfigProvider>()` + `IoC.Resolve<IModLogger>()` lazy-init in `OnLateUpdate`. DryIoc Singleton resolve acquires a lock on first call. Add `Initialize(config, logger)` from the prefab-extension owner. | #169 |
| 18 | P3 | CareerSystem | Domain model gap — `DisplayName` missing | `CareerChoiceObjectVM.cs:43` | `Name` property returns `new TextObject(_choice.Id).ToString()` because `CareerChoiceDefinition` has no `DisplayName` field despite `taom_career_choices.xml` having `display_name` attributes. Players see raw IDs like `ranger_of_ithilien_t1_a_key`. | #165 |
| 19 | P3 | CareerSystem | Localization — hardcoded button label | `CareerButtonPrefab.cs:35` | Inline XML `Text="Career"` — no binding, no `{=key}fallback`. Breaks on translation. | #165 |
| 20 | P3 | SpecialResources | Localization — tooltip labels | `SpecialResourceMapBarMixin.cs:98, 104, 105, 112–117, 121–124` | Hardcoded English `"Tier"`, `"Next tier at"`, `"Daily Change"`, `"Income"`, `"Net"`, `"Per battle"`, etc. — no `TextObject` wrap. | #167 |
| 21 | P3 | SpecialResources | Diagnostic flag conflation | `SpecialResourceSpriteWidget.cs:45–51, 60–65, 71` | `_loggedOnce` flag shared across "no resource for kingdom" + "sprite not found" + successful-load paths → success log can be suppressed by an earlier failure log. Split into separate flags. | #167 |
| 22 | P3 | TimeAcceleration | Localization — tooltip | `TimeAccelerationMixin.cs:18` | `BasicTooltipViewModel(() => "Extra Fast Forward (E)")` — hardcoded English. | #168 |
| 23 | P3 | Custom Widgets | Portability — `System.Drawing.Bitmap` | `Main/Features/FactionMap/Widgets/PolygonWidget.cs:915–956` | `LoadAlphaMapFromPng` uses `System.Drawing.Bitmap` (GDI+). Windows-only. TAOM is Windows-only, so not a crash today; flagged for portability tracking. | #169 |
| 24 | P3 | Custom Widgets | Dead code — `Points` property | `Main/Features/FactionMap/Widgets/PolygonWidget.cs:253–263, 1001–1032` | `Points` `[Editor]` property + `_pointsX/Y/_pointCount` arrays + `ParsePoints`/`PointsToString` (~60 lines) never consumed — hit-test uses `IsMouseOnOpaquePixel` (alpha map) instead. | #169 |
| 25 | P3 | Custom Widgets | WidgetFactory registration unverified | `Main/Features/SpecialResources/UI/SpecialResourceSpriteWidget.cs` | Prefab-extension `Replace` patches in the string name `"SpecialResourceSpriteWidget"`. If TAOM assembly isn't scanned by `WidgetFactory.RegisterWidgetTypes`, the Replace silently leaves vanilla `IconBrushWidget` in place. Confirm via runtime check or registration audit. | #169 |

## Per-feature reports

### 1. CareerSystem (1 Mixin + 1 PrefabExtension + 6 VMs + 1 Screen + 1 GameState + 2 prefab XMLs)

**Construction integrity:** ✅ `GauntletCareerScreen` correctly implements `IGameStateListener` with all 4 lifecycle methods (lines 100–103). Check 7 passes — the crash documented in `feedback_gamestate_listener.md` is not reachable here.

**Setter discipline:** ✅ All `[DataSourceProperty]` setters use `if (value != _field) { ... }` guards. Check 3 passes.

**Notification pattern:** ✅ All TAOM VMs call `OnPropertyChanged(nameof(...))` on `this`. No reflected field-set patterns. Check 4 passes.

**Binding case-sensitivity:** ✅ All 30+ `@PropertyName` bindings in `CareerScreen.xml` + `AbilityHUD.xml` + `CareerButtonPrefab.cs` inline XML match a `[DataSourceProperty]` exactly. Check 5 passes.

**PrefabExtension target:** ✅ `CareerButtonPrefab` targets `"CharacterDeveloper"` with `descendant::Widget[@Id='TopPanelParent']`. Confirmed present at line 369 of installed v1.3.15 `CharacterDeveloper.xml`. ID selector (not positional) — safe against child-count changes.

**The systemic failure: sprite registration is incomplete by 60–95%.**

Counts:
- Portraits: 50 careers → 21 registered → **29 missing P1** (finding #1).
- Abilities: 50 ability templates → 9 registered → **41 missing P1** (finding #2).
- Choice icons: every `career_choice_*` value → **0 registered P1** (finding #3).
- HUD: same 41-ability-set as #2 → **P1** in-mission gameplay (finding #4).

All four route through the same fix shape: PNG files + `<SpritePart>` entries in `TAOMSpriteData.xml` under the `ui_taom_career_system` category. The XML data and runtime path are correct; the asset pipeline is incomplete.

**P3 findings (#18, #19):** localization gaps — `CareerChoiceDefinition` lacks a `DisplayName` field (so VM falls back to ID); `CareerButtonPrefab` has a raw `"Career"` string literal.

### 2. Messengers (1 Mixin + 1 PrefabExtension, inline XML only)

**PrefabExtension target:** ✅ `[PrefabExtension("EncyclopediaHeroPage", "descendant::RichTextWidget[@Text='@InformationText']")]`. Verified in installed v1.3.15 `SandBox/GUI/Prefabs/Encyclopedia/EncyclopediaSubPages/EncyclopediaHeroPage.xml:125`. Single match — no ambiguity. Parent container is a non-data-bound `ListPanel`; injection as a sibling of the `RichTextWidget` is safe.

**Setter discipline:** Setters have `if (value != _field) { ... }` guards. Check 3 passes.

**Binding case-sensitivity:** ✅ `@IsMessengerAvailable`, `@SendMessengerActionName`, `{SendMessengerHint}`, `ExecuteSendMessenger` — all match mixin members.

**Sprite verification:** N/A — no sprite references in this surface.

**`IGameStateListener` gap:** N/A — no `GameStateScreen` subclass.

**The P1 bug: VM notification fired on the wrong object (finding #5).**

`MessengerEncyclopediaMixin.cs:143, 160, 174` all do:
```csharp
ViewModel?.OnPropertyChangedWithValue(value, nameof(IsMessengerAvailable));
```
This calls the method on the HOST `EncyclopediaHeroPageVM` (the `ViewModel` property of `BaseViewModelMixin`). Gauntlet's binding system associates `@IsMessengerAvailable` etc. with the mixin's datasource — notifications must fire on the mixin (`this`), not the host VM. Every other TAOM mixin calls `OnPropertyChanged(nameof(X))` (inherited from `ViewModel`) on `this`. Result: bindings show whatever the mixin computed on its first construction-time read, then never update — even though the underlying values do change as the encyclopedia page is re-opened for different heroes.

**P2 #10:** `SendMessengerCost` `[DataSourceProperty]` declared at line 149 but never referenced by any `@SendMessengerCost` binding in the inline XML.

### 3. SpecialResources (1 Mixin + 1 PrefabExtension + 1 Custom Widget)

**PrefabExtension target:** the 6-hop XPath `descendant::ListPanel[@Id='BottomInfoBar']/ItemTemplate/HintWidget/Children/ListPanel/Children/IconBrushWidget` is long and fragile but functional (P2 #9). The inner `Replace` swaps a vanilla `IconBrushWidget` for the TAOM `SpecialResourceSpriteWidget` (P3 #25 — registration unverified).

**Setter discipline:** ✅ guarded by `if (intAmount != _lastAmount)` at line 62. Inner assignment ORDER is P2 #8 — `IntValue` set before `Value`.

**Sprite verification:** ❌ 8 of 11 resource icons missing (P1 #6).

**The pre-loaded `SecondaryInfoItems.Add` finding (P2 #7):**

Confirmed against v1.3.15 decompile. The originally-documented crash (`IndexOutOfRangeException` in `HandlePanelSwitchingInput`) is NOT reachable — that method dispatches navigation hotkeys and does not index `SecondaryInfoItems`. The autogenerated binding code uses `AddChildAtIndex(widget, e.NewIndex)` for `ItemAdded`, which is index-safe.

However, the rule remains violated for a DIFFERENT, equally-real reason: `MapInfoVM.CreateItems()` (called from any `RefreshValues()`) `Clear()`s and re-populates the list with only the 3 vanilla items. If vanilla refresh fires after TAOM's `Add` (which it routinely does on settlement enter/exit, party composition change, etc.), the TAOM item is silently dropped and the mixin's `_itemAdded` guard stays `true` → no re-insertion ever happens. **The feature works on first render and silently loses its UI thereafter.**

Severity downgraded from likely-P1 to **P2** because the symptom is degradation, not crash. The fix (bind via mixin `[DataSourceProperty]` + inject into a non-data-bound container) is the same.

### 4. TimeAcceleration (1 Mixin + 1 PrefabExtension, 5 prefab-extension decorators)

**PrefabExtension targets:** ✅ All 5 XPath selectors verified against installed v1.3.15 `SandBox/GUI/Prefabs/Map/MapBar.xml`:
- `MapCurrentTimeVisualWidget[@Id='CenterPanel']` — line 110
- `ButtonWidget[@Id='FastForwardButton']` — line 124
- `ButtonWidget[@Id='PlayButton']` — line 130
- `ButtonWidget[@Id='PauseButton']` — line 136

The vanilla `MapCurrentTimeVisualWidget.OnUpdate` drives `IsSelected` for the three named buttons by ID resolution at prefab-load time. The TAOM extra button uses `Id="FastFastForwardButton"` (distinct) → no collision with vanilla selection logic.

**Setter discipline:** ✅ Both `IsExtraFastForwardActive` and `ExtraFastForwardHint` setters use `if (_field != value)` guards. Check 3 passes.

**Notification pattern:** ✅ Calls `OnPropertyChanged(nameof(...))` on `this`. Check 4 passes.

**Binding case-sensitivity:** ✅ `IsSelected="@IsExtraFastForwardActive"` and `DataSource="{ExtraFastForwardHint}"` both match mixin members.

**The P2 bug: wrong state signal (finding #11).**

`OnRefresh()` sets `IsExtraFastForwardActive = Campaign.Current.SpeedUpMultiplier > 4f`. The default of `SpeedUpMultiplier` is `4f`; the only vanilla mutation path is the `campaign.set_campaign_speed_multiplier` cheat console command. The TAOM button itself fires `CommandParameter.Click="2"` → vanilla `ExecuteTimeControlChange(2)` which toggles `TimeFlowState` / `TimeControlMode` but does NOT touch `SpeedUpMultiplier`. The mixin's "selected" state therefore never activates in normal play.

**P3 #22:** the tooltip hint is a raw English literal.

### 5. Custom Widgets (FactionMap × 4 + SpecialResources × 1)

**Constructor signatures:** ✅ All 5 widgets have valid `(UIContext context) : base(context)` constructors. Check 1 passes (no `WidgetFactory` instantiation crash).

**v1.3.15 verification:** `Widget` base class, `DrawSprite` signature, `Rectangle2D`/`SimpleMaterial`/`SpriteNinePatchParameters` all confirmed via decompiled v1.4 source (signatures stable across versions for these public types). `RuntimeSprite` is a TAOM-owned `Sprite` subclass with correct override of the 3 abstract members.

**FactionMap widgets are runtime-rendered map decorations** — wired in `Main/_Module/GUI/Prefabs/CharacterCreation/CharacterCreationCultureStage.xml`. They implement a custom alpha-map hit-test + cross-instance hover resolution + texture pinning system. The design is sophisticated but exposes 4 P2 hot-path issues:

- **Per-frame allocations** (findings #12, #13, #14): `SimpleMaterial` is a value-type struct, but `CreateSimpleMaterial()` may still produce per-call cost. `PolygonWidget` allocates up to 7 per render in the edge loop and ≥1 in the shadow loop; `BannerWidget` allocates 8 per visible banner in the glow loop. At 60fps × 42 FactionMap instances on the culture-selection screen, edge loop alone burns ~17k allocations/sec.
- **Static-list race** (#15): `_allInstances.RemoveAt(i)` from `OnLateUpdate` while `OnRender` does `_allInstances[0]` / `_allInstances[last]`. Safe only on the assumption that Gauntlet renders all widgets single-threaded; if any threading model change pushes render to a worker thread, this is an immediate `ArgumentOutOfRangeException`.
- **Cross-thread static write** (#16): `HoveredFactionName` written from `OnRender`, read by VM `OnLateUpdate`. Should be moved to `ResolveGlobalHover()` which already runs on the LateUpdate path.

**SpecialResourceSpriteWidget** has P2 #17 (lazy `IoC.Resolve` in `OnLateUpdate`) + P3 #21 (`_loggedOnce` flag conflation) + P3 #25 (registration assumption).

**P3 #23:** `PolygonWidget.LoadAlphaMapFromPng` uses `System.Drawing.Bitmap` (Windows GDI+ only). TAOM is Windows-only so this is not a crash today.

**P3 #24:** `PolygonWidget.Points` `[Editor]` property + `ParsePoints` + `PointsToString` + `_pointsX/Y/_pointCount` arrays are never consumed — ~60 lines of dead code.

## Cross-cuts

### Sprite registration gaps (P1)

The two cross-cutting P1 categories are **sprite asset gaps** — they break visible UI without crashing, without logging, and without leaving a stack trace. They look like cosmetic issues but make features non-functional in the sense players experience.

The root cause is asset-pipeline coupling: the runtime expects every PNG referenced by XML data to also be (a) present on disk under the matching atlas subfolder and (b) registered in `TAOMSpriteData.xml`. The build doesn't check this. Tests don't check this. The agents who add new careers / resources don't always own the asset side. The diagnostic (silent blank widget) is invisible.

**Recommendation for Phase 9 fix scope:** rather than batch-adding 78 PNGs, consider a `/verify`-time sprite-coverage check that scans XML for `Sprite="..."` / `*Sprite` properties and asserts each has a corresponding entry. Treat missing-sprite as a build warning. This catches the next 50-career-style content drop before it ships blank-icon.

### VM notification anti-patterns

`MessengerEncyclopediaMixin` is the only file in the cluster that gets the mixin notification pattern wrong. Every other mixin uses `OnPropertyChanged(nameof(...))` correctly. The fix is mechanical (delete `ViewModel?.`) but the root cause is worth recording: the author of `MessengerEncyclopediaMixin` confused "the host VM I'm extending" with "the VM the binding is associated with." Gauntlet considers the mixin to BE a ViewModel (via `BaseViewModelMixin : ViewModel`); the host VM is irrelevant to binding routing.

**Phase 6 angle:** the cross-feature handshake review should check whether any TAOM patch or service reads mixin properties externally and would be affected by the post-fix correct-notification behavior (no — mixins are UI-only).

### `SecondaryInfoItems.Add` rule status update

The gui-ui.md rule's "Why" paragraph cites a specific crash (`IndexOutOfRangeException` in `HandlePanelSwitchingInput`). v1.3.15 decompile shows that crash is no longer reachable. The rule itself is still load-bearing (the `CreateItems()` silently-drops-our-item failure mode is just as bad), but the rationale needs an update. Suggest a Phase 9 doc fix: update gui-ui.md's "Why" paragraph to cite the new failure mode (`MapInfoVM.CreateItems()` Clear+repopulate) and note that the historical `HandlePanelSwitchingInput` crash is no longer the controlling case.

### Per-frame allocation discipline in custom widgets

`PolygonWidget` and `BannerWidget` show a pattern of `CreateSimpleMaterial()` in a tight render loop. Even when `SimpleMaterial` is a value type, the helper that creates it may allocate. The fix is mechanical (hoist outside the loop, mutate fields per iteration). Phase 9 should batch these — they all live in `Main/Features/FactionMap/Widgets/` + `Main/Features/SpecialResources/UI/`.

### Custom widget threading assumption

Two P2 findings on `PolygonWidget` (static-list race + cross-thread `HoveredFactionName`) both reduce to the same assumption: **Gauntlet renders widgets sequentially on a single thread**. If true, the findings collapse to P3 (defensive cleanup). If false, they are reproducible crashes / corruption. Phase 6 (cross-feature handshake review) or a one-off `taleworlds-researcher` task should decompile `GauntletLayer.Update` + `GauntletLayer.Render` from `TaleWorlds.GauntletUI.dll` to settle the question.

## GitHub issues opened

| # | Title | Findings | Severity mix |
|---|---|---|---|
| #165 | audit(impl): CareerSystem UI — sprite registration gap (29 portraits + 41 abilities + all `career_choice_*` missing) + 2 localization gaps | #1, #2, #3, #4, #18, #19 | 4 P1, 2 P3 |
| #166 | audit(impl): Messengers UI — mixin notifies wrong VM (host instead of self) + unused [DataSourceProperty] | #5, #10 | 1 P1, 1 P2 |
| #167 | audit(impl): SpecialResources UI — 8 of 11 sprite icons missing + SecondaryInfoItems.Add rule violation + 3 minor | #6, #7, #8, #9, #20, #21 | 1 P1, 3 P2, 2 P3 |
| #168 | audit(impl): TimeAcceleration UI — wrong state signal (SpeedUpMultiplier never raised by button) + hardcoded tooltip | #11, #22 | 1 P2, 1 P3 |
| #169 | audit(impl): Custom Widgets — per-frame allocations + static-list race + cross-thread write + IoC.Resolve in OnLateUpdate (6 P2, 3 P3) | #12–17, #23–25 | 6 P2, 3 P3 |

## Phase 5 complete

- 5 surfaces reviewed.
- 25 findings: 6 P1, 12 P2, 7 P3.
- 5 GitHub issues opened (#165–#169) — one per surface, aggregating findings per the precedent of cluster-gamemodels.md.
- 1 rule-rationale update queued (gui-ui.md's `SecondaryInfoItems` "Why" paragraph — Phase 9 doc fix).
- 1 sprite-coverage-check tooling recommendation queued (Phase 9 / future).
- 1 widget-threading research task queued (Phase 6 or one-off).

Phase 5's hypothesis was "find UI bugs in the 4 mixin/prefab features + custom widgets." Result: the dominant bug class is **silent broken UI from missing sprite assets** (5 of 6 P1s), not VM logic mistakes. The 1 logic P1 (Messengers wrong-VM notification) was a single mechanical author error. The audit's existence is again vindicated — these are real, hard-to-spot, in-game-only bugs that no compiler / test / build gate catches.

Forward to Phase 6 (cross-feature handshake): the FactionMap custom-widget threading question is a natural pickup; the rest of Phase 6 follows the session-prompts.md template (SmartCavalryAI × MixedFormations × CompanionTactics, CulturalFeats × RevoltTuning, etc.).
