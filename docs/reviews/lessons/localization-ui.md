# Lessons — Localization & UI

> Category file of the master lessons record — index + house shape: [LESSONS-LEARNED.md](../LESSONS-LEARNED.md). **Append new Localization & UI lessons HERE** (`### rule` → `**Why missed:**` → `**Prevent:**` → `**Source:**`).

### Any `[GameStateScreen]` class MUST implement `IGameStateListener`
A class decorated with `[GameStateScreen(typeof(XxxState))]` must implement `IGameStateListener` or it NREs on open. Without it, `GameStateScreenManager.OnCreateState()` registers null as the listener via `(IGameStateListener)(object)((val is IGameStateListener) ? val : null)`, causing an NRE in `GameState.HandleInitialize()`. Always add `IGameStateListener` with empty `OnInitialize()`/`OnFinalize()`/`OnActivate()`/`OnDeactivate()` methods.
- **Why missed:** `GauntletCareerScreen` extended `ScreenBase` but missed `IGameStateListener` — the career screen crashed every time it opened from the character developer. All vanilla screens (e.g. `GauntletCharacterDeveloperScreen`) implement it.
- **Prevent:** When creating any new `[GameStateScreen]` class, always add `IGameStateListener` to the declaration with the four empty interface methods.
- **Source:** memory/feedback_gamestate_listener.md

### Degraded / partial-success runtime state needs a DISTINCT user-facing string, not just a color swap
Every TAOM feature with a "fully working" vs "degraded but loaded" runtime state must have TWO localization keys — one for "active" and one for "degraded" — and the visible strings must be substantively different. Color alone is insufficient (no signal for color-blind users; and "loaded" is technically true for the DLL but functionally misleading when every hook is inert). Prefer "active" over "loaded" in success banners — "loaded" describes the DLL state, "active" describes the function.
- **Why missed:** RCA NativeSkinFixes port LOW UX finding — the banner read "NativeSkinFixes loaded — covers_head morph fix + hair/beard cloth simulation" in amber when patterns were stubbed and every hook was inert. Sighted users got a misleading "loaded"; color-blind users got no signal at all.
- **Prevent:** Pattern: `LoadedMessageKey => "{=taom_xxx_loaded}"` / `DegradedMessageKey => "{=taom_xxx_degraded}"`, select by `AllSubsystemsActive`. Test requirement: assert the two banners are distinct AND the degraded banner contains a degraded-state word (`degraded`/`unauthored`/`unavailable`/`partial`, OrdinalIgnoreCase). Add the second loc key up-front for any feature that can run partially-installed.
- **Source:** memory/feedback_degraded_state_distinct_banner.md + docs/reviews/rca-native-skin-fixes-port-2026-05-26.md finding #4

### Interactive Gauntlet overlay MUST call `SetInputRestrictions()` or mouse clicks pass through silently
When adding a custom `GauntletLayer` overlay with interactive widgets (buttons, click bindings, drag handles) to EITHER a `ScreenBase` (Harmony postfix on `OnInitialize`) OR a `MissionScreen` (via `MissionView.OnMissionScreenInitializeFirstTime` or any overlay-attach path), the layer must call `_layer.InputRestrictions.SetInputRestrictions()` after construction or it paints but is invisible to the input dispatcher — clicks are silent no-ops. Pair with `_layer.InputRestrictions.ResetInputRestrictions()` immediately before `RemoveLayer`. The v1.4.5/v1.3.15 dispatcher does NOT distinguish `ScreenBase` from `MissionScreen` for this. Do NOT set `IsFocusLayer = true` on a parasitic overlay (reserve it for full-screen replacements like `GauntletCareerScreen`); the overlay's parent widget should carry `DoNotAcceptEvents="true"`. Display-only HUDs with zero interactive widgets (verify by grep for `ButtonWidget`/`Command.Click`/`AcceptEvents`) are the only exception — e.g. CareerSystem `AbilityHUD.xml` (hotkey-driven).
- **Why missed:** Two shipping bugs in 6 days. (1) EquipPresets "Presets" button (#202, commit `d141304`) — `ScreenBase` overlay rendered with active bindings but never registered; `/deep-review` + Codex review #28 both missed it because they focused on service-layer correctness and TAOM had no other `ScreenBase` overlay to compare against. (2) CompanionTactics OOB "Assign Heroes"/"Presets"/BattleActionBar clicks (#225, commit `28c8d1e`) — two `MissionScreen` overlays with the same defect, shipped past bug #1's rule because that rule was wrongly scoped to ScreenBase-only. The inference that `BattleActionBar` "worked" without it was wrong — only the hotkey path worked (`HandleHotkeyInput` polls `Mission.InputManager` directly, bypassing the Gauntlet dispatcher), masking the broken mouse path.
- **Prevent:** Rendering ≠ live — input wiring is upstream of the data flow agents trace. When classifying a sibling as a working precedent, verify it works via the SAME input path you care about (a working hotkey path is not evidence the mouse path works). When codifying a rule from a single instance, immediately sweep the codebase for other instances. `taom-src`-verified: `GauntletLayer.InputRestrictions` is `TaleWorlds.ScreenSystem.InputRestrictions` (on base `ScreenLayer`); `SetInputRestrictions(bool isMouseVisible = true, InputUsageMask mask = InputUsageMask.All)` — parameterless call valid in v1.3.15 + v1.4.5.
- **Source:** memory/feedback_gauntlet_overlay_input_wiring.md + docs/reviews/rca-equippresets-presets-button-silent-2026-05-19.md, docs/reviews/rca-companiontactics-overlay-input-2026-05-25.md; rule `.claude/rules/gui-ui.md` "Custom GauntletLayer Input Wiring"

### A hotkey-category string is only correct against the specific key you QUERY — verify the queried key id exists in the registered category
When a custom `GauntletLayer` registers a hotkey category (`Input.RegisterHotKeyCategory(HotKeyManager.GetCategory("SomeCategory"))`) and then polls a key (`Input.IsHotKeyReleased("Exit")`), the two must AGREE: the queried key id must actually be defined in that category. `IsHotKeyReleased(id)` does a `TryGetValue` and returns `false` when the id isn't registered — no error, no log, the key is just permanently dead. `GenericCampaignPanelsGameKeyCategory` (window-toggle keys: BannerWindow/CharacterWindow/…) does NOT contain `"Exit"`; `"Exit"` lives in `GenericPanelGameKeyCategory` (singular "Panel"). TAOM precedent for Escape-close: `GauntletFiefManagementScreen` registers `GenericPanelGameKeyCategory` then queries `IsHotKeyReleased("Exit")`.
- **Why missed:** WotR-momentum port (#327, 2026-07-03) copied the category string verbatim from LOTRAOM's `MomentumInterface`, which was close-button-only — the donor never exercised the "Exit" key, so its wrong category was latent. The Escape poll is NEW port code. Caught by the compatibility agent decompiling both categories; standards + data-flow can't see a runtime string binding.
- **Prevent:** When wiring a hotkey poll on a custom layer, confirm (decompile or grep the category's `RegisterHotKey` calls) that the queried key id is in the registered category. Faithful UI ports especially: the donor's category may have been latent because the donor never queried that key.
- **Source:** docs/reviews/rca-wotr-momentum-2026-07-03.md finding #1

### MCM-screen bottom-to-top layout lives in MCM's EMBEDDED prefabs — UIExtenderEx `[PrefabExtension]` can't patch them; use a Harmony Postfix on `WidgetFactoryManager.CreateAndRegister`
When the in-game MCM/MBOptionScreen options screen renders settings bottom-to-top (group headers below members, reverse `Order`, reverse `GroupOrder`), it is the v1.4.0 `VerticalBottomToTop` layout regression (same class as commit `ad836d1`) but inside MCM's embedded prefabs, NOT TAOM's loose XML. MCM v5.11.4 ships `LayoutImp.LayoutMethod="VerticalBottomToTop"` in `SettingsView.xml`, `SettingsPropertyGroupView.xml`, `ModOptionsView.xml`, `ModOptionsPageView.xml` (embedded resources in `Bannerlord.MBOptionScreen.v1.4.x.dll`). The C# `[SettingProperty(Order=…)]`/`GroupOrder` values are NOT the bug. A UIExtenderEx `[PrefabExtension]`/`PrefabExtensionSetAttributePatch` CANNOT fix it — UIExtenderEx applies patches only via the `ProcessMovie` call it transpiles into the engine's disk-file loader `WidgetPrefab.LoadFrom(…, string path)`, but MCM loads from embedded streams through `Bannerlord.UIExtenderEx.ResourceManager.WidgetFactoryManager.CreateAndRegister(name, XmlDocument)` → `LoadFromDocument` (a Harmony reverse-patch that omits `ProcessMovie`), so the extension compiles, registers, enables, and silently no-ops.
- **Why missed:** `ad836d1`'s repo grep returned "zero VerticalBottomToTop remain" while the MCM screen stayed broken — the grep-and-flip reflex gives a false all-clear for library-embedded prefabs.
- **Prevent:** Fix = `Patch41_McmLayoutFix` — a thin Harmony Postfix on `WidgetFactoryManager.CreateAndRegister(string, XmlDocument)` that mutates the `XmlDocument` in place (the registered lazy `Func` closes over the same doc reference, parsed later at first screen-open). Pure logic in `Main/Features/Mcm/McmLayoutRewriter.cs` (flips `LayoutImp.LayoutMethod`/`StackLayout.LayoutMethod` `VerticalBottomToTop`→`VerticalTopToBottom`, scoped to MCM prefab names); entry `Main/Features/Mcm/Hooks/Patch41_McmLayoutFix.cs`; registered in `SubModule.OnSubModuleLoad` BEFORE MCM's `ResourceInjector.Inject()` at `OnBeforeInitialModuleScreenSetAsRoot`. In-game test requires a full game RESTART — MCM's `Inject()` runs once per process at startup. Extract embedded prefabs via `[Reflection.Assembly]::Load([IO.File]::ReadAllBytes($dll)).GetManifestResourceStream($name)`.
- **Source:** memory/feedback_mcm_embedded_prefab_layout_regression.md

### Grep the WHOLE shared strings XML for a `<string id>` before adding it — duplicate ids shadow silently
Before adding a `<string id="X" text="{=X}...">` entry to a shared module-strings XML (e.g. `Main/_Module/ModuleData/taom_module_strings.xml`, ~820 lines), grep the ENTIRE file for `id="X"` AND the `{=X}` key first. Thematic-cluster proximity is not evidence the id is free — vanilla-harvested strings and earlier features scatter ids across the file. Duplicate `<string id>` shadow each other; one wins at load with NO build error and NO test failure, so the dialog may render the wrong text with unset variables. Prefer a feature-prefixed id (`taom_player_alliance_formed`, not the generic `taom_alliance_formed`).
- **Why missed:** Player-alliance-freedom (2026-06-16) added `<string id="taom_alliance_formed">` near the other `taom_alliance_*` keys at line ~816, not noticing a pre-existing `taom_alliance_formed` at line ~371 (a harvested vanilla notification with `{KINGDOM1_LINK}/{KINGDOM2_LINK}` + key `{=0cdXddA9}`). Caught by the deep-review data-flow agent's "string key → consumption" trace, NOT the completeness agent (which checks presence, not uniqueness). A wrong-id duplicate is invisible to build + unit tests.
- **Prevent:** Localization extension of the CLAUDE.md "Verify Before Reference" rule. When adding any new `{=key}`: (1) `Grep id="<key>"` across the strings file — expect zero hits; (2) confirm the `{=key}` in the C# `TextObject` matches the new XML id exactly; (3) prefer a feature-prefixed id.
- **Source:** memory/feedback_verify_string_id_unique_before_add.md + docs/reviews/rca-player-alliance-freedom-2026-06-16.md

### Filtering a UI control to a config subset — preserve config ORDER and config DEFAULT
When filtering a UI control (dropdown, list, picker) to a config-defined subset, make two decisions consciously. **(1) Order:** iterate the source whose ordering is canonical. To preserve the config's intended order, iterate the CONFIG list and resolve each entry to its universe position via a name→index dictionary — do NOT iterate the engine universe and keep allowed entries (that produces engine-ordered output). **(2) Default:** the engine's default selection was set against the FULL universe, so after filtering it may land on a non-canonical position. On first encounter with a culture/filter, snap to the config-canonical default (Races[0]) unless the engine-default already matches — track per-VM-instance "first encounter" with a `ConditionalWeakTable<TVM, Session>` (not a static singleton — reopened VMs need independent sessions), and don't force-switch on subsequent Apply (gender/age change must preserve the player's choice).
- **Why missed:** Trap 1 — `BuildGlobalIndexMap` iterated `allRaces` and kept allow-list members, so for Mordor's `["uruk","orc","human"]` the engine's index-0 `human` appeared first despite config listing `uruk` first; "iterate the universe, keep what's allowed" is correct for the SET but wrong for the ORDER. Trap 2 — vanilla `FaceGenVM.Refresh(bool)` sets `_selectedRace = CurrentRace` = `0` (human) regardless of culture; for Isengard's `[uruk_hai, berserker, human]`, `human` IS allowed at filtered position 2, so the force-switch was skipped and FaceGen opened on human even though config listed `uruk_hai` first. Both reviewers (incl. Codex) verified mechanical correctness; neither traced default-state expectations to the UX outcome.
- **Prevent:** Test order explicitly (config-first item NOT first in engine universe → expect it in position 1). Test default with engine-default state. Track first-encounter via `ConditionalWeakTable`. Reference impl `Main/Features/CharacterCreation/FaceGenRaceSelectorRebuilder.cs` (`BuildGlobalIndexMap`, `ShouldForceSwitchToDefault`, `Apply`); regression tests `TAOM.Tests/Features/CharacterCreation/FaceGenRaceSelectorRebuilderTests.cs` (`BuildGlobalIndexMap_Mordor_UrukFirstNotHuman`, `ShouldForceSwitchToDefault_FirstApply_NonDefaultRace_Switches`, `ShouldForceSwitchToDefault_SubsequentApply_PreservesPlayerChoice`).
- **Source:** memory/feedback_filter_order_and_default.md

### Replacing a TaleWorlds VM property's value — use the public setter, never reflected field-set + reflected `OnPropertyChangedWithValue`
When code REPLACES a TaleWorlds VM property's value (not just mutates the existing object) and a public property wraps the private field, always use the public property setter — it handles both the field assignment AND the correctly-typed change notification. Do NOT reflect the field-set then `AccessTools.Method(typeof(VM), "OnPropertyChangedWithValue", new[]{ typeof(object), typeof(string) })` — that method is generic (`protected void OnPropertyChangedWithValue<T>(T value, [CallerMemberName] string propertyName = null) where T : class`), so the lookup by `(object, string)` returns null and the `?.Invoke` fails silently. Generic methods cannot be looked up by `AccessTools.Method(type, name, paramTypes)` with concrete params; if you must reflect one, get it from `GetMethods()` filtered by `IsGenericMethodDefinition` then `MakeGenericMethod(...)` — but the property setter is almost always the correct call site.
- **Why missed:** Codex review #33 (CharacterCreation race-filter, 2026-05-06) caught a HIGH bug Claude missed. `FaceGenRaceSelectorRebuilder.Apply` reflected `_raceSelectorField.SetValue(faceGenVM, newSelector)` then tried to fire the notification via the null `MethodInfo`. The trap: initial construction masks it because `LoadMovie` reads the field directly; any post-construction `Refresh(true)` re-creates the value via vanilla code (which uses the public setter + notifies correctly), silently rebinding the UI to vanilla's value — manifests as "filter works once, then resets back to vanilla on next interaction."
- **Prevent:** Before reflecting on a private field, search the decompiled vanilla for a public property that wraps it (`grep -n "public.*<TypeName>.*<FieldName>\|return _<fieldName>;"`); if it exists, use the setter. Reference impl `Main/Features/CharacterCreation/FaceGenRaceSelectorRebuilder.cs:71` uses `faceGenVM.RaceSelector = newSelector` (removed `_raceSelectorField` + `_onPropertyChangedWithValueMethod` caches). Rule `.claude/rules/gui-ui.md` "prefer public setter over reflected field+notify (MANDATORY)" auto-loads on `*VM.cs`/`*Mixin*.cs`/`*Widget*.cs`/`GUI/**`. Sister memory: `feedback_taleworlds_vm_setter_decompile.md` (same-value no-op early-return case).
- **Source:** memory/feedback_prefer_public_setter_over_reflected_notify.md

### A new GUI sprite has TWO+ failure modes: not baked (regen the atlas) and baked-but-invisible (fix the prefab); plus stale-atlas-in-running-game after a re-bake
TAOM's GUI sprite categories are a baked atlas, packed offline by `bin/Win64_Shipping_wEditor/TaleWorlds.TwoDimension.SpriteSheetGenerator.exe`; the loose `GUI/SpriteParts/<category>/**.png` are the SOURCE, never read directly by the player client. There is NO `pack0.tpac` for UI sprites — atlases are per-category `<category>_<n>.png` (in `AssetSources/GauntletUI/`) + `<category>_<n>_tex.tpac` pairs, with the manifest at `GUI/<ModuleName>SpriteData.xml` (e.g. `TAOMSpriteData.xml`) carrying `SheetID`+`SheetX/SheetY`+`Width/Height`. Three failure modes: **(1) Not baked** — a new loose PNG has no pixels in the compiled sheet until the generator runs; hand-editing `TAOMSpriteData.xml` does nothing (the generator overwrites the whole manifest). **(2) Baked but invisible** — the PREFAB renders it too small / too faint (career_point_pip drawn at `22×28px` with `Color="#FFFFFF45"` = 27% alpha read as faint embossing on a near-black node; fixed by `38×38` + brighter opacities `#FFFFFFFF`/`#FFFFFFE0`/`#FFFFFF78`, prefab-only no regen). **(3) Stale atlas in a running game after a re-bake** — a re-bake re-bin-packs the WHOLE category so existing sprites move to new `SheetX/SheetY`; a game left running keeps the pre-bake texture in memory but reads the new manifest rects on screen re-open → moved sprites sample empty/garbage texture (career screen `-` button went invisible, `+` went dead) while unmoved sprites render fine. This is NEITHER a code nor asset regression — it's a runtime cache mismatch.
- **Why missed:** Reviews cannot confirm a sprite is visible. `/deep-review` + Codex verified only the manifest SHAPE and wrongly assumed a runtime-build model + a `pack0.tpac`; the first RCA wrongly concluded "regen will fix the blank pip" (it fixed the bake but not the render). Mode 3 is invisible to `/investigate` + `/deep-review` because the committed files are correct + consistent.
- **Prevent:** Adding/replacing a GUI sprite = (1) drop the loose PNG at a sane size; (2) RUN the generator to bake; (3) verify the PREFAB render (widget size readable, `Color` alpha high vs. background, sprite-capable widget). Treat "does a new sprite render" as an in-game-only check and say so in the CHANGELOG `Not-tested:` line. To check what's baked: read the `<SpritePart>` coords in `TAOMSpriteData.xml`, then crop that rect from `AssetSources/GauntletUI/<category>_<n>.png`. After ANY re-bake that can reposition existing sprites, fully exit and relaunch the game before judging the result (a clean relaunch fixed mode 3 with zero file changes — issue #290).
- **Source:** memory/feedback_sprite_atlas_baked_regen_required.md + docs/features/gui-sprite-system.md "The sprite-bake pipeline"; related memory/feedback_sprite_dimensions.md

### TAOM ships full `<Prefab>` clones of vanilla GUI prefabs — diff them against installed vanilla before editing or after a version bump
~32 of TAOM's 48 GUI prefabs are full clones of vanilla prefabs (override-by-filename, under `Main/_Module/GUI/PreFabs/**`; git-tracked casing, on-disk dir is lowercase `Prefabs` — git pathspecs are case-sensitive so `git status -- .../Prefabs/` reads falsely empty). Vanilla RENAMES widget attributes between engine versions, so a clone frozen at an older version keeps the obsolete attribute → the engine silently ignores it → the widget mis-renders or never renders, with no log and no crash, visible only in-game. This is the GUI-prefab instance of the stale-vs-vanilla failure class governed for data XML by `.claude/rules/vanilla-data-comparison.md`; it is invisible to static review and unit tests (rendering ≠ live).
- **Why missed:** The v1.3.15→v1.4.5 migration scoped itself to C# API drift + equipment XML and never audited GUI prefab clones, so every Party-screen troop thumbnail shipped stuck on the loading spinner — TAOM's clone bound the renamed `ImageTypeCode` instead of `TextureProviderName` on `ImageIdentifierWidget`/`MaskedTextureWidget` (backing `ImageIdentifierVM`). Found 2026-05-31 by user report, not by review. The audit itself produced confident FALSE positives (caught only by re-checking vanilla with an attribute-NAME regex + decompile, NOT a substring grep, scoped to true vanilla SandBox/SandBoxCore/Native): `EaseIn` is NOT a vanilla attribute at all (parser silently ignores `EaseIn="true"`; the "18×" was a substring miscount of `EaseType="EaseInOut"`/`IsEaseInOutEnabled`, fabricated TWICE); and the `AutoScroll*Offset`↔`ScrollYOffset` rename DIRECTION was inverted (`ScrollYOffset` is the stale one).
- **Prevent:** Before editing — or after any version bump touching — a TAOM GUI prefab, check if vanilla `Modules/{SandBox,SandBoxCore,Native}/GUI/Prefabs/` ships the same filename; if so `diff -w --strip-trailing-cr <vanilla> <taom>` first and classify each delta as rename-casualty (fix to match vanilla) vs intentional customization (keep). VERIFY each suspected rename against installed vanilla — don't trust a list. Verified v1.4.5 stale→current renames: `ImageTypeCode`→`TextureProviderName`; `LayoutImp.LayoutMethod`→`StackLayout.LayoutMethod` (but `LayoutImp.Horizontal/VerticalLayoutMethod` are STILL valid — leave them); `ScrollYOffset`→`AutoScrollTopOffset/BottomOffset` on `NavigationAutoScrollWidget`; `RichTextWidget=`→`TextWidget=` on `DropdownWidget`. After a bump, audit ALL clones (the rename hits every clone using that attribute) — planned mechanization `tools/audit_gui_prefab_clones.py`. Commit prefab fixes promptly (uncommitted GUI edits were silently discarded by a concurrent external `git reset`/`stash`).
- **Source:** memory/feedback_gui_prefab_clones_stale_across_versions.md + docs/reviews/rca-party-troop-thumbnail-stale-prefab-clone-2026-05-31.md; rule `.claude/rules/vanilla-data-comparison.md`, pointer in `gui-ui.md`

### Resize AI-generated images to target dimensions BEFORE placing in SpriteParts — oversized PNGs corrupt the whole atlas
AI-generated images (Midjourney 1024×1024, DALL-E various sizes) must be resized to target widget dimensions (2× for sharpness) BEFORE placing them in the SpriteParts folder. Oversized images overflow the sprite atlas and corrupt ALL UI elements in the game.
- **Why missed:** Added 1024×1024 career images to the `ui_taom` atlas; the atlas overflowed, breaking every UI panel. Required creating a dedicated `ui_taom_career_system` atlas and resizing all images.
- **Prevent:** Resize with PIL: `Image.open(f).resize((width, height), Image.LANCZOS).save(f)`. Career portraits 800×400, ability icons 256×256. Verify dimensions before committing.
- **Source:** memory/feedback_sprite_dimensions.md

### User-facing strings (MCM hints, dropdown labels) are a contract — they MUST match what the code does
When a feature exposes multiple modes/options/toggles via MCM hints, dropdown labels, tooltips, or in-game messages, those strings are a contract with the player. Read every user-visible string and trace it to the implementation; if the promise doesn't match the code, fix one or the other — never ship the mismatch silently. When porting code another developer wrote and tested, do NOT assume "they tested it, so it works as advertised" — verify the user-visible promise (an inherited bug can hide behind a promising-sounding dropdown option for releases).
- **Why missed:** SiegeDismount mode 1 (`DismountKeepOnMap`) had an MCM hint promising "Mount spawns nearby on the map but player is on foot," but the implementation was a silent no-op (same as Vanilla). The original developer's decompiled module had the same pre-existing bug; it was ported verbatim without challenging the promise-vs-code match. Codex review #34 flagged it.
- **Prevent:** Trace every user-visible string: (1) grep MCM `[SettingPropertyDropdown]`/`[SettingPropertyBool]`/`[SettingPropertyInteger]` `HintText`, dropdown labels, `InformationManager.DisplayMessage`, `TextObject` templates, game-menu titles; (2) name the expectation each creates; (3) trace it to the fulfilling code path — a dead-end at `case X: log("..."); break;` is a lie; (4) pick one fix: implement the behavior, rewrite the string to match ("currently equivalent to Vanilla — full implementation deferred"), or remove the option. Fix applied: mode 1 labelled "Reserved (currently equivalent to Vanilla — full implementation deferred)", logs `LogWarning` if picked, enum value retained for save-compat.
- **Source:** memory/feedback_user_facing_promise_must_match_code.md

---

### Wrap `{=key}` localization strings in `new TextObject(...).ToString()` before binding to a VM
A ViewModel string property that displays config text carrying `{=key}Fallback` tags must wrap it: `Property = new TextObject(rawValue).ToString()`. `TextWidget` does NOT process `{=key}` tags — only `TextObject` does — so an unwrapped value renders the raw `{=taom_career_knight_of_belfalas}Knight of Belfalas`.
- **Why missed:** The career screen showed raw `{=key}…` text; the fix touched 3 VM classes (`CareerScreenVM`, `CareerChoiceObjectVM`, `CareerChoiceGroupObjectVM`).
- **Prevent:** Any VM property bound to text that originates from XML with `{=key}` tags — display_name, description, tooltip, any user-facing config string — wraps in `new TextObject(rawValue).ToString()`.
- **Source:** memory/feedback_localization_textobject.md

---

### A GauntletUI style that redefines an INHERITED name does not regain the brush Default
In a `BaseBrush` chain, `BrushFactory.cs:560` assigns `style.DefaultStyle = brush.DefaultStyle`, which reads like every style falls back to the brush `Default` for unset attributes. That only holds for style names the base brush did **not** already define. For inherited names, `Style.FillFrom` (`Style.cs:564`) assigns through the property setters, each of which latches `_isXChanged = true` — so the base value is baked in before your redefinition is parsed and `DefaultStyle` is never consulted again. TAOM's retinted `Link.*` fallback styles silently kept vanilla's `TextGlowColor="#111111FF"` dark halo on a pale parchment.
- **Why missed:** the fallback claim was verified for the *new* style names and then generalised to the *redefined* ones. It was literally true for 60 of 81 styles — and the 21 exceptions were exactly the group the change existed to fix.
- **Prevent:** when overriding an inherited style, state **every** attribute you depend on, even ones you expect to inherit; never mix "rely on the fallback" and "override explicitly" in one block. Pin it with a test asserting the attribute is present in the shipped XML.
- **Source:** MenuLinkColors deep review 2026-07-26 (MEDIUM). RCA: `docs/reviews/rca-menu-link-colors-2026-07-26.md`

### Moving sprites between atlas categories — delete the old PNGs from the GAME INSTALL, not just the repo
When migrating sprites from one atlas category to another (e.g. `ui_taom` → `ui_taom_career_system`): (1) move in repo, (2) delete the old category folder from the game install (`E:\Steam\...\Modules\TAOM\GUI\SpriteParts\<old_category>\...`), (3) then run the sprite generator. The build only copies new files — it never deletes removed ones — and the generator scans ALL category folders on disk, so duplicate PNGs across categories crash `AddSpritePart` with "duplicate key".
- **Why missed:** 3 commits spent debugging a clean repo — the stale PNGs lived only in the game install, invisible to git.
- **Prevent:** Treat a category move as a three-step (repo move → install delete → regen); the repo being clean proves nothing about the install.
- **Source:** memory/feedback_sprite_atlas_cleanup.md (recovered 2026-08-05 from the stale pre-move project slug — the only fact in it not already migrated)

### Verify `Brush="X"` names like `Sprite="X"` names — BrushFactory nulls silently
`BrushFactory.GetBrush(name)` returns null for an unregistered brush name with no exception, no assert, no log (verified `TaleWorlds.GauntletUI.BrushFactory.cs:934-940`, installed v1.4.7) — the widget renders with default styling and nothing tells you. `Brush="ButtonBrush1.Text"` shipped in `AbilityHUD.xml`, was copy-inherited into the #379 badge, and `CharacterDeveloper.SkillNameText`/`.DescriptionText` (14 uses in `CareerScreen.xml`) have been silently unregistered since May — the vanilla `"<X>.Text"` pattern requires each `.Text` brush to be its OWN `<Brush Name=...>` declaration, never auto-derived from `X`.
- **Why missed:** `gui-ui.md` mandated verifying every `Sprite=` against the sprite registry but said nothing about `Brush=` — the rule scope was one asset category narrower than the failure class (the NaN-gate scope-gap shape, again). A shipped prefab using the bad name made it look legitimate.
- **Prevent:** before writing any `Brush="X"`, grep `Main/_Module/GUI/Brushes/*.xml` + the relevant vanilla `Modules/*/GUI/Brushes/*.xml` for `<Brush Name="X"`. Rule widened in `gui-ui.md` "Sprite References" (now sprites AND brushes).
- **Source:** career UX arc deep review 2026-08-05 (compatibility agent). RCA: `docs/reviews/rca-career-ux-arc-2026-08-05.md`

### There is no English language folder, so the inline `{=key}` fallback IS the English text
`Main/_Module/ModuleData/Languages/` holds 12 locales and no `EN`; `language_data.xml` is an empty
`<LanguageData id="English">`. A `name="{=aom_lord_X_name}Literal"` reference therefore resolves from
a language file in the other 12 locales and from the literal in English. Rename a lord in
`taom_xslt_strings.xml` and run the translator, and every locale on earth shows the new name while
English keeps the old one. Thirteen lords had drifted this way, `lord_WE8_c` for long enough that
English players saw "Icratia" while the registry, all 12 translations, the encyclopedia bio and the
parent wiring said Pelendur.

- **Why missed:** the rename looks complete from every angle a reviewer checks. The registry has the
  new name, `LanguageFileCoverageTests` is green, and spot-checking any translated locale in game
  shows the new name. English is the only locale that can exhibit the bug and the only one with no
  file to inspect. `LanguageFileCoverageTests` cannot help by design: it asserts a key HAS a row, not
  what the row says, so renaming under an existing key never turns it red.
- **Prevent:** `TAOM.Tests/Core/LordNameAndSexConsistencyTests.cs` asserts every inline fallback in
  `characters/lords.xml` equals the registered English text, with a small documented exemption list.
  `tools/oneoff/sync_lord_name_fallbacks.py` does the repair. Treat the English fallback as a
  thirteenth translation that no tool updates for you. Related trap in the same direction:
  `translate_with_claude.py` refills only rows whose target text still equals the English source and
  keys its cache by string id alone, so **changing** English under an existing id propagates to
  nothing. A rename is only safe when the registry, the 12 language files, the cache and the inline
  fallback all move together, and picking a name the registry does not already hold means editing all
  of them by hand.
- **Source:** 2026-08-28, `a00086da`.

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/modding/editing-safely.md](../../modding/editing-safely.md)
- [docs/modding/strings-and-localization.md](../../modding/strings-and-localization.md)
- [docs/reviews/LESSONS-LEARNED.md](../LESSONS-LEARNED.md)

<!-- backlinks-end -->
### A Gauntlet screen with fixed-width children must be FIXED WIDTH and CENTRED, never StretchToParent

A container set to `StretchToParent` becomes as wide as the player's monitor. Every fixed-width
child inside it then clusters at the left and the remainder is dead space — invisible at 16:9,
where the numbers were tuned, and severe at 32:9. The career screen (#388) showed all three
failure shapes at once on an ultrawide: the header bar ran off into empty space, its ability
description drifted past centre and clipped mid-sentence, and the right-hand summary column
stranded far from the grid it belonged to.

- **Why missed:** every width was chosen while looking at one 16:9 screenshot, so the layout
  was correct *at that width* and the reviews (which read markup, not pixels) had nothing to
  catch. Aspect ratio is a dimension no static check and no unit test covers.
- **Prevent:** give any screen whose children are fixed-width a `WidthSizePolicy="Fixed"` root
  with `HorizontalAlignment="Center"`, sized to the sum of its columns, and write that sum in a
  comment next to it so the next person changing a column width knows the budget they are
  spending. Reserve `StretchToParent` for children that genuinely fill their parent. Sibling
  case: the retired AbilityHUD had to be right-anchored for the same reason — a `Center`-anchored
  panel drifted unreachable on ultrawide.
- **Source:** career UX arc 2026-08-06 (user report with three stitched ultrawide screenshots);
  fix `9284a5e8`, feature doc `docs/features/career-system.md`.

### Gauntlet tints sprites MULTIPLICATIVELY — a mid-tone colour darkens, only a near-white one highlights

A plain `Widget` multiplies its `Sprite` by its `Color` (an `ImageWidget` ignores `Color`
entirely — a separate trap). So a tint is a *filter*, not a paint: `#AEB6BE` on a light sprite
does not produce "silver", it produces a dimmed version of whatever the sprite already was. The
career screen's tier-2 diamond rims (#388) were authored at that value and read as washed-out
grey next to bronze and gold, until the ramp moved to `#6E767E / #E8EFF7 / #FFFFFF`.

- **Why missed:** the hexes were picked as if choosing a paint colour — a plausible "silver" on
  a colour wheel. Nothing in the markup says the value will be multiplied, and the result only
  looks wrong next to the other tiers, which is a comparison no static review performs.
- **Prevent:** when tinting a sprite to read as a bright material, start from near-white and
  pull back, rather than starting from the material's mid-tone. Anything below roughly `#C0C0C0`
  will read as "darker sprite", not "brighter metal". Related durable fact worth keeping in the
  same breath: `ImageWidget` ignores `Color` outright, so a state tint on one silently does
  nothing — use a plain `Widget`.
- **Source:** career UX arc 2026-08-06, commits `060e65e9` (per-tier metals) and `dee4d12f`
  (brightened silver); `docs/features/career-system.md`.

### Editing an existing English string does not invalidate its cached translation

**Trap:** `tools/translate_with_claude.py` resolves each entry override → cache → LLM → English, and
the cache step is `elif e.string_id in cache` — it matches on the **key alone** and never compares the
English source the cached translation was made from. So changing the *text* of an already-translated
key leaves the cache authoritative: the next `/localize` run writes the OLD translation back into all
12 language files, silently reverting the edit. Nothing warns, and at edit time the language files
look correct — the revert only lands on the next translation run, which may be weeks later and in
someone else's session.

Found 2026-08-06 (#388): 165 career health strings went from "+75 max health" to "+9 max health".
The English sources, the 12 language files and the descriptions were all updated correctly — and
1,967 cache entries still said "+75 points de vie maximum.", primed to undo the whole retune.

**Prevent:** any change that rewrites EXISTING English source text must also update (or delete) those
keys in `tools/translation_cache/<lang>.json` in the same commit — deleting forces a re-translation,
updating preserves the human wording when only a number or placeholder changed. Adding a NEW key is
unaffected: a key absent from the cache always reaches the API. Worked example of the update path:
`tools/retune_career_health.py`'s third pass (`sync_cache`), which points each cached entry at the
language file's post-edit text.

**Generalises to:** any key-addressed memo cache over content that can change — the cache key must
either include a hash of the input or the writer must invalidate explicitly. TAOM's does neither, so
the burden is on the caller until that changes.

**Source:** #388 career health retune, 2026-08-06; full write-up in
`lessons/build-tooling-workflow.md` (same entry covers the `\r\r\n` line-ending trap in the language
XMLs) and `docs/reference/localization-map.md`.

### Need a tintable shape and the art is the wrong colour? Layer an already-baked NEUTRAL sprite instead of adding a new one

Gauntlet tints multiplicatively, so a coloured sprite can never be tinted to a different hue —
and adding a neutral replacement means the full sprite-bake pipeline (generator + texture
compile), which can fail in ways that are invisible until you look in-game. The career screen's
per-tier diamond rims (#388) were blocked exactly there: `clan_diamond_border` is bronze art, and
its desaturated replacement registered in the manifest but never packed into the atlas (#392).

The fix used no new asset at all. `clan_diamond_mask` was already baked and neutral grey
(`#707070`), so the rim was rebuilt by layering it: the mask at 78px tinted the tier metal, then
the mask again at 70px tinted the dark fill, covering the centre. The ~4px of the first layer
still visible IS the rim, in any colour. Children draw in document order, so the coloured layer
must come first and the centre second.

- **Why missed:** the instinct on "this sprite is the wrong colour" is to author a new sprite,
  which quietly commits you to a bake. Two sessions went into the generator before anyone asked
  whether an existing neutral sprite could be composed into the same shape.
- **Prevent:** before adding a sprite purely to get a tintable version of a shape you already
  have, check the atlas for a neutral one you can layer (a filled shape can become an outline by
  drawing a larger tinted copy behind a smaller fill). Zero bake risk, instant iteration. Reserve
  new art for genuinely new geometry.
- **Source:** career UX arc 2026-08-06, commit `505bb6e1`; confirmed in-game (bronze/silver/gold
  rendering distinctly). Underlying packer bug remains open as #392.

### For a `{=key}default` TextObject, the English XML row is never rendered — edit the C#

`MBTextManager.GetLocalizedText` (v1.4.7, `TaleWorlds.Localization.MBTextManager.cs:264-268`)
short-circuits on the active language before it ever looks up a registered translation:

```csharp
if (_activeTextLanguageId == "English")
{
    text2 = _targetStringBuilder.ToString();   // the inline default
    return RemoveComments(text2);              // registered row never consulted
}
```

So for the `new TextObject("{=key}English fallback")` form, an English player always sees the
string literal in the C#. The English row in `taom_enlistment_strings.xml` (or any sibling) is
**translator source material and the id registry** — not a render path. The 11 non-English
languages are the ones the registration actually feeds.

Two practical consequences:

1. **An English copy edit made only in the XML will not appear in game.** Edit the C# literal.
2. **Changing a template's SHAPE requires a NEW key id**, because the 11 translated rows WILL win
   for their languages — reusing the id renders the old sentence to every non-English player and
   silently drops any new `{TOKEN}` the new template introduced, with no error anywhere. English
   looks correct throughout, which is exactly what makes it easy to ship.

- **Why missed:** the mental model "registered translation beats inline default" is true for the
  languages you are usually thinking about, so it gets generalised to all 12. Nobody re-reads the
  loader because the rule feels settled, and English — the language the developer plays in — is
  structurally immune to the bug, so local testing cannot surface it.
- **Prevent:** treat a template-shape change as needing a new key id, and verify English copy
  changes at the C# literal rather than in XML. When reasoning about localization precedence,
  name which languages you mean; "all 12" is the tell that the English carve-out was forgotten.
- **Source:** enlistment status-board v2, 2026-08-08. The `_v2` key decision was independently
  correct; the comment justifying it overstated the scope, caught by the deep-review API agent
  and confirmed by reading the loader.

### A localization key composed at runtime is unfindable — the generator or a test must own the family

The entry above explains why an unregistered key is invisible **in English**. Compose the key from
data and it becomes invisible to the *author* too:

```csharp
new TextObject("{=taom_enlist_duty_" + duty.Id + "_success}...")            // FieldDutyRuntime
new TextObject($"{{=taom_cc_{definition.StringId}_text}}{definition.Text}") // NarrativeMenuBuilder
```

`taom_cc_taom_parent_goblin_1_text` appears in no source file, so a `{=key}` grep — the normal way
anyone audits registration — returns nothing at all. Combine that with the English short-circuit and
the defect has no observer: not the compiler, not the suite, not the developer playing in English.
Only a player reading one of the other eleven languages ever sees it.

It has now shipped twice in three days. 26 enlistment duty-result toasts (#428), then all 96
character-creation narrative rows for `goblin` and `mistymountainorcs` (#432) — two entire cultures,
while the other sixteen were complete.

**The fix is structural, and there are exactly two acceptable owners for a composed-key family:**

1. **A generator** that walks the data and emits every row, hard-failing on an unauthored one rather
   than emitting a placeholder (`tools/harvest_duty_strings.py`, which reads the JSON rows). A generator that
   *silently defaults* is worse than none — it converts a loud gap into a raw id in the UI.
2. **A test** that cross-references the data source against the registry
   (`NarrativeStringRegistrationTests`). Pin **value drift as well as presence**: a registered row
   whose default no longer matches its JSON is worse than a missing one, because English renders the
   JSON via the short-circuit while the translations were made from the stale text, so the same
   option says different things per language and neither half looks broken.

- **Why missed:** every registration audit anyone runs is a grep, and the whole point of a composed
  key is that it is not a literal. The audit is not weak — it is structurally incapable of seeing
  this family, and it reports clean while doing so.
- **Prevent:** grep for the *composition sites* instead — `"{=" +`, `"{=prefix" +`, and `$"{{=` are
  the three forms — and require each one to name its generator or its coverage test. TAOM has four
  such sites; the sweep that found #432 enumerated all four, which is the only way to know a sweep
  is complete rather than a sample. Adding a fifth without an owner should be a review finding.
- **`$"{{=` is two different bugs wearing one shape, and it is easy to check only the first.** The
  interpolation may be in the KEY (`$"{{=taom_cc_{StringId}_text}}"` — a composed key, unfindable)
  or only in the DEFAULT (`$"{{=taom_res_desertion}}{count} troops deserted"` — a literal key, but
  the runtime value is baked into the template). The second is worse than an unregistered key,
  because registering it does not help: the row a translator works from has no slot for the number,
  so **no translation can ever carry it**. Use `SetTextVariable`. Found by Codex 2026-08-09 after I
  classified that line as "literal key, fine" and moved on (#434).
- **A scanner written to find unregistered keys can miss them in exactly the form it was written
  for.** The first version of `UnregisteredLocalizationKeyBaselineTests` required a quote
  immediately before `{=`, so it saw `"{=taom_x}"` and skipped every `$"{{=taom_x}}"` — including
  the one live unregistered interpolated key in the tree. The regex must accept `\{\{?=`, and must
  still require the closing `\}` so that genuinely composed keys are excluded rather than truncated
  into a bogus key name.
- **Also:** the two cultures missing here (`goblin`, `mistymountainorcs`) rhyme with `shaghana` /
  `abanissa` missing careers (review #24) and enlistment rosters (#431). **The non-vanilla cultures
  fall out of every coverage sweep**, because they were added after the tables that enumerate
  cultures were written. Any per-culture invariant deserves a test driven off the culture list
  itself, never a hand-maintained one.
- **Source:** #428 duty toasts + #432 narrative strings, 2026-08-09;
  `rca-duty-autoresolve-2026-08-09.md` finding 9.

### An unloaded sprite category fails as a SILENT BLANK — and the `Sprite`-level null check that looks like a guard is not one
Before removing `<AlwaysLoad/>` from any sprite category, prove an explicit `LoadSpriteCategory` call
runs on every path that displays it — **in game, not by inspection.** The failure mode is not an
exception or a log line; it is a rectangle that draws nothing.

The chain, verified in the v1.4.7 dump:
`SpriteData.GetSprite` resolves from a flat dict built from the **manifest**, so it returns a
**non-null** `Sprite` for a category that is not loaded. `SpritePart.Texture` then returns **null**
while `!category.IsLoaded`. Consumers that guard on `sprite == null` — `LoadingWindowWidget.UpdateImage`
is the worked example — see a perfectly good `Sprite`, skip their fallback, and draw a
textured-with-nothing sprite. No exception, no log, no clue.

- **Why missed:** an analysis concluded `ui_loading` was "the safest drop of the five" on the theory
  that `GauntletDefaultLoadingWindowManager` drives the category through
  `InitializePartialLoad`/`PartialLoadAtIndex`, so `<AlwaysLoad/>` was defeating the engine's own
  one-image-at-a-time design and was actively harmful. **Both halves are false.** Those two methods
  are referenced *only inside `SpriteCategory.cs` itself* — no caller exists anywhere in the dump —
  and the literal `"ui_loading"` appears **nowhere** in the decompile, so no engine code loads that
  category by name. The reasoning was internally coherent and had a plausible mechanism, which is
  exactly why it survived until someone grepped the dump for callers rather than for definitions.
- **Prevent:** when a plan claims the engine drives something, grep the decompile for **callers of**
  that method and for the **literal category/asset name**, not just for the method's definition. A
  method that exists is not a method that runs. And when the proposed failure mode is "renders blank",
  treat in-game verification as mandatory rather than as confirmation — static review cannot see it.
- **Source:** native-commit-audit L2, 2026-08-08. `ui_loading` drop REJECTED after the supporting
  analysis was refuted against the v1.4.7 dump; `docs/investigations/native-commit-audit-2026-08.md`
  carries the full refutation and the general rule for the remaining `AlwaysLoad` levers.

### Registering a localization key is not the same as propagating it
A `{=key}` row in the English source makes a string *registered*. It does not make it
*translatable*. `write_back` in `translate_with_claude.py` substitutes **by id** — it rewrites the
`text` of an existing `<string id="KEY">` and has nowhere to put a key the per-language file does
not already declare. So a registered-but-unpropagated key is translated, paid for, and discarded,
and the player sees English forever in all eleven non-English languages.

`--sync-ids` is the step that closes this, and it must run **before** the translation, not after.
Prefer it over `generate_translation_template.py --apply`, which reaches the same end state by
overwriting each per-language file with a fresh English template — that discards every translation
in the file, including PL's hand-written ones, and only the git-tracked cache makes the AI half
recoverable.

- **Why missed:** all three gaps looked identical from the developer's chair — perfect English,
  green suite, no warning. `MBTextManager.GetLocalizedText` short-circuits on English and returns
  the inline default, so the registered row only ever feeds the other eleven languages, and nobody
  who plays in English can see it fail. `LanguageDataXmlTests` checked the *shape* of the
  `Languages/` tree exhaustively — dirs, file counts, well-formedness, every row has id and text —
  and never once compared an id set against the English source, which is the only check that would
  have caught any of it.
- **Prevent:** `LanguageFileCoverageTests.EveryLanguage_DeclaresARowForEveryEnglishKey` pins it —
  every key the English side declares must exist as a row in all twelve language files. It is a
  presence check on purpose: asserting difference-from-English would permanently report proper nouns
  and the four vanilla nested-gender strings that fall back by design, and a check that reports
  mostly noise gets ignored — the same failure that let #434 sit for two years.
- **Source:** #434, 2026-08-09. 317 keys never registered, 96 registered but never propagated
  (#432), one late `taom_res_desertion` row — 414 per language, all fixed in one pass.

### A translation tool that only fills blanks cannot repair a CHANGED English source

`_diff_files` in `tools/translate_with_claude.py:296` selects a row for translation only when
`cur_text == eng_text`, that is, only while the target file still holds the English. The moment a row
is translated it leaves the work set permanently. So editing an English string's TEXT (as opposed to
adding a key) leaves all eleven AI languages and the hand-written PL holding a translation of the OLD
sentence, indefinitely, and nothing reports it. `--sync-ids` does not help: it seeds MISSING ids only.
The cache is keyed by string id rather than by source text, so a changed source does not invalidate
its own cache entry either.

- **Why missed:** this is a different failure from the #434 pair above (never registered, registered
  but never propagated), and the test that closed those cannot catch it.
  `LanguageFileCoverageTests.EveryLanguage_DeclaresARowForEveryEnglishKey` is a presence check on
  purpose, and a stale translation is present. Meanwhile the English player sees the new text at once,
  because `MBTextManager.GetLocalizedText` short-circuits on English, so the change looks finished
  from the developer's chair.
- **Prevent:** treat "I edited an English string" as its own task with its own decision, never as a
  free edit. Three cases, and pick one out loud. A mechanical change such as a numeral (10% to 20%) is
  best done as a targeted per-row substitution across the twelve files: exact, free, and it preserves
  each language's phrasing. A NEW key whose English is verbatim identical to an existing key should
  COPY that key's rows rather than pay for an LLM pass. Anything else owes a real translator run and
  stays on the owed list until it happens. The durable fix is a source-text hash stored beside each
  cache entry so a changed English string re-enters the work set; not built as of 2026-08-14.
- **Source:** 2026-08-14 cultural-feats strings. `{=taom_feat_mor_ps_desc}`'s numeral updated in all
  12 languages by substitution; the new `{=taom_feat_bcg_ps_desc}` copied from the goblin row
  (identical English); the new `{=taom_feat_bcg_ps}` name seeded as English in all 12 and still owing
  a translator run.

### Verify WHICH text manager renders a string before verifying its id

Bannerlord has two, and they are populated by completely different mechanisms:

| Manager | Populated by | Reaches |
|---|---|---|
| `Module.CurrentModule.GlobalTextManager` | `LoadDefaultTexts()`, which walks every installed module and opens the LITERAL path `ModuleData/global_strings.xml` | Main-menu screens, including Options > Keybindings |
| per-`Game` `GameTextManager` | `SubModule.xml` `<XmlNode id="GameText" path="..."/>`, consumed at `Game.Initialize` | In-campaign text only |

A correct string id in the wrong manager does not fall back and does not go blank. It renders
`ERROR: Text with id str_key_name doesn't exist!` on the screen.

- **Why missed:** the review verified the id SHAPE exhaustively (character-by-character against
  `GameKeyOptionVM`'s `((GameKeyDefinition)Id).ToString()` construction, all 12 translations present,
  XML parse test, invariant test) and never asked which manager performed the lookup. The data-flow
  agent traced the same id end-to-end and returned CONNECTED. Codex answered it in one step by opening
  `GameKeyOptionVM.RefreshValues`.
- **The plausible wrong fix, for the record:** noticing that `taom_module_strings`'s node is
  `<IncludedGameTypes>`-gated to Campaign and removing the gate. The gate IS a real problem for
  main-menu text, but ungating a `GameText` node does not promote it to the global manager, so the
  bug survives a fix that looks like it addressed it. Verifying that a condition matters is not the
  same as verifying that removing it is sufficient.
- **NavalDLC is not counter-evidence.** Its own `module_strings` node is ungated, but its Options
  labels come from Native's `global_strings.xml`, which independently ships all six Naval key records.
  ButterLib takes the third route: `GlobalTextManager.AddGameText(...)` at runtime.
- **Prevent:** for any engine-rendered string, read the VM/screen that renders it and follow the
  manager back to its loader before trusting the id. Pin the requirement with a test that asserts the
  file's name and location, not just its contents, when the loader keys on a literal path.
- **Source:** 2026-08-22 rebindable Time Acceleration keys.
  `docs/reviews/rca-timeacceleration-keybinds-2026-08-22.md`.

### A localization key prefix is an ownership claim; grep before you take one (camps port, 2026-08-23)

The camps batch generated 161 registrations under taom_sl_/taom_fc_/taom_rf_; the 70 taom_fc_
rows collided with FieldCommission's already-registered prefix:
10 of FieldCommission's keys got a second registration in taom_module_strings.xml (one copy
double-escaped, able to shadow the correct row) and two review rounds missed it because the
round-trip gate let one registration XML vouch for another as a "code default". Before a new
feature claims a prefix, grep every ModuleData *_strings.xml for it; and a round-trip gate's
code-default scan must exclude ALL registration XMLs, or the gate is circular. The renaming fix
(taom_fc_ -> taom_fcamp_) was free only because the keys were still untranslated English
fallbacks; after a translator run the same mistake costs 12 languages of churn.
- **Recurrence (2026-10-26, Harvest Road):** the builder took `taom_hr_` (an acronym of the feature name), which Hearth
  Roll already owned. Renamed to `taom_hroad_` while the language rows were English. A two-letter acronym is the likely
  clash: spell a distinct word of the feature name, then grep. Source: [rca-harvest-road-2026-10-26.md](../rca-harvest-road-2026-10-26.md) finding 3.

### A literal {=key} in a Gauntlet prefab renders the raw token; label text lives on the VM (supply order screen, 2026-08-25)

Gauntlet localizes only VM-bound strings (they pass through TextObject.ToString in the VM);
a literal Text="{=key}Label" attribute in a prefab is rendered verbatim, token and all. Six
Supply Order buttons shipped that way and every review round missed it because the keys WERE
registered and the round-trip gate compares registered rows against inline defaults, not
against how a prefab consumes them. Rule: prefab text is @Property or {=!}{VARIABLE}, never a
literal key; the prefab sweep test now enforces it module-wide.

### The localization gate is one-directional; assert that every declared key is actually rendered

TAOM's localization suite proves every English key has a row in all twelve language files. Nothing proved the reverse. A feature shipped fifteen keys of which seven were referenced by nothing, and they were not merely wasted translation: three were the player-facing outcome messages (`switched`, `failed`, `unavailable`), specified in the implementation plan and then never wired, so a failed handover told the player nothing at all while the exact string for it sat translated in twelve languages.
- **Why missed:** the completeness checklist asks whether localization is PRESENT, never whether it is REACHABLE. Presence is what the existing tests enforce, so a dead key looks identical to a healthy one at every gate.
- **Prevent:** for each feature strings file, assert every declared `{=key}` is referenced from C# or a prefab. A dead key is usually the fossil of a specified step that was never built, which makes this test a spec-compliance check wearing a tidiness costume.
- **Source:** docs/reviews/rca-player-switcher-2026-08-27.md finding 2 (#514).

### Rewording an English string leaves the OLD translation in the cache, keyed by id, and the next run puts it back (#525, 2026-09-01)

`taom_enlist_reassign_cav` was reworded from "I can ride. Give me a horse." to something that
does not promise a mount, because the service kit ships without one. The known trap is that
`translate_with_claude.py` only fills rows that still contain English, so the twelve translations
silently keep describing the old behaviour. The trap underneath it is worse: resetting each row
to the new English does NOT re-open it, because `tools/translation_cache/<lang>.json` is keyed on
the **string id**, not on the English text. A dry run reported the entry as resolved "from cache"
at an estimated cost of $0.0000, and `--apply` would have written "Gebt mir ein Pferd." straight
back over the reset row, with no warning and no API call.
- **Why missed:** every existing note about this pipeline describes the *file* side ("the
  translator only fills untranslated rows"). Nothing recorded that the cache is a second store
  with its own staleness, and its behaviour on a reworded string is indistinguishable from a
  correct cache hit: same key, plausible text, zero cost.
- **Prevent:** editing the English text of an existing key is a THREE-file operation, not one.
  Update the English source, reset the twelve language rows, and **evict the key from all twelve
  caches**, then confirm the dry run reports `needs LLM: 1` rather than a cache hit. A dry run
  that still says `$0.0000` after a reword is the tell that the eviction did not happen.
- **Also:** `--apply` writes every cached row it finds, not just yours. On this run that was 1,103
  unrelated entries per language, which would have buried a one-line change under a four-figure
  diff. Check the "from cache" count before applying, and keep an edit to one string scoped to
  that string.
- **Source:** #525 enlistment weapons.


### A fixed-size widget wrapping a bound string needs its own `IsVisible` gate for the empty case

The career energy bar draws the activation key in a fixed 30x22 `Widget` carrying a `BlankWhiteSquare_9` sprite, with a `TextWidget Text="@ActivationKeyText"` inside it. Clearing the keybind in Options is a supported state and empties that string, but only the TEXT vanishes: the dark box stays on the HUD with nothing in it. The sibling career-glyph medallion three lines above in the same prefab is gated on `IsVisible="@HasCareerGlyph"`; the chip had no gate at all.
- **Why missed:** the empty-string case was actually identified while planning and written down as an in-game smoke step ("check the empty chip does not leave a stray box, if it does add an `IsVisible` binding"), then left for the smoke run instead of being answered by opening the prefab already sitting in the repo.
- **Prevent:** two habits. First, when adding or gating a bound string, read the sibling widgets in the same prefab: if one of them is gated and yours is not, that asymmetry is the bug. Second, and more general, never defer to an in-game smoke a question that a file in the repo can answer by being read. Deferring does not just postpone the answer, it downgrades a certainty to a maybe and puts the defect on the ship path.
- **Source:** #533 rebindable career ability key, 2026-09-03; RCA `docs/reviews/rca-career-keybind-2026-09-03.md` finding 3.

### A game-menu option condition runs when the menu is built or refreshed, not every frame
`GameMenuVM.OnFrameTick` calls `GameMenuItemVM.Refresh()` each frame, and that method only re-reads the
cached `GameMenuOption.IsEnabled`, tooltip and wait state. The condition delegate itself runs from
`GameMenuVM.Refresh(bool)` (which walks `GameMenuManager.GetVirtualMenuOptionConditionsHold`), reached on
`OnActivate`, `OnResume`, `OnMenuContextRefreshed` (an explicit `MenuContext.Refresh`) and a menu switch
(`UpdateMenuContext`). Verified on the installed v1.4.8 `SandBox.GauntletUI.Menu.GauntletMenuBaseView`
plus `GameMenuVM` and `GameMenuManager`. A settlement-menu condition that scans a small list is therefore a
per-menu-open cost, not a hot path.
- **Why missed:** a review agent rated such a condition HIGH while stating in the same report that the
  refresh frequency was unverified, exactly what its prompt forbids. The claim was plausible because wait
  menus DO tick, but a wait menu re-runs only `RunWaitMenuCondition`, and town, castle and village menus are
  not wait menus.
- **Prevent:** before rating any `AddGameMenuOption` condition a hot path, cite the driver (`GameMenuVM.Refresh`
  versus `OnFrameTick`). Per-frame work in a menu belongs to `GameMenuItemVM.Refresh` and progress items,
  nowhere else.
- **Source:** `docs/reviews/rca-field-commission-dismiss-2026-09-04.md` finding 3 (refuted HIGH, #540).

### When you change what a displayed number means, grep every consumer of the value it was paired with
A UI number rarely travels alone. Vanilla keeps a label and its red over-capacity tint coherent by feeding
both from ONE source; redefining what one half means silently breaks that, and the break lives in the
consumer you did not open, never in the file you edited. The 2026-09-06 TroopWeight usage-frame reframe
gave the party-screen header a new denominator (the true base) and left vanilla's tint on the old one
(`RightPartyMembersSizeLimit`, frozen at screen-open by `PartyScreenLogic.cs:491`). For a party whose
weight penalty had been clamped, dragging the heavy troops off then rendered a comfortable `30 / 100`
directly beside a red over-capacity warning, reachable by the exact remediation workflow the feature
exists to prompt. Fix shape: return the over-capacity verdict alongside the label and let the caller CLEAR
a stale tint, downgrade-only, so no vanilla mode gate can be bypassed and no warning fabricated.
- **Why missed:** the label assignment and the tint assignment are two lines apart in the same method
  (`PartyVM.RefreshPartyInformation`, `PartyVM.cs:3069`). Reading far enough to find what to change is not
  the same as reading far enough to find what else consumes it. Four of five deep-review agents passed;
  only the cross-system data-flow agent, which opens engine consumers, caught it.
- **Prevent:** after changing a displayed value's meaning, list every OTHER property assigned from the same
  source in the same method and confirm each still agrees. The existing "Parallel Method Consistency" check
  covers method families (CanAfford/Spend/Display); this is its display-value sibling and was uncovered.
- **Source:** #545 TroopWeight usage frame, 2026-09-06; RCA `docs/reviews/rca-troopweight-usage-frame-2026-09-06.md` finding 1.

### A ViewModel property is not display-only because its name looks like a label
`CurrentPartySize`, `PartyCapacity` and `IsMainTroopsLimitWarningEnabled` all read like presentation state.
All three gate vanilla confirmation dialogs: `RecruitmentVM.ExecuteDone` shows its "Over Limit" inquiry when
`CurrentPartySize > PartyCapacity`, and the party screen's done-path branches on the troop-limit warning
flags. A hook that rewrites them is changing when a player gets prompted, not just what they read.
- **Why missed:** the properties were classified from their names and the change was documented in its own
  docstring as "nothing here may feed a gameplay decision": an invariant asserted, never grepped.
- **Prevent:** before writing "display-only" or "cosmetic" in a comment, grep the property's consumers. If
  the rewrite is intended, pin the equivalence with a test rather than a claim, `WeightedFrameIdentityTests`
  reads the real `troop_weights.xml` so a weight-table change cannot silently move a confirmation threshold.
- **Source:** #545 TroopWeight usage frame, 2026-09-06; same RCA, finding 2.

### Recovering a patch target from git history is not evidence the patch does what you now want
Reinstating a target deleted in an earlier rework proves the METHOD still exists. It does not prove the
method produces the thing your new hook consumes. `CampaignUIHelper.GetPartyHealthTooltip(PartyBase)` was
recovered from the 2026-07-11 deletion set, where it had rewritten Battle Ready / Wounded rows, which that
method does emit. The new hook wanted the `{=ZgYAGfbD}Land Troop Capacity` row, which exists only in the
parameterless `GetMainPartyHealthTooltip()`. The patch shipped into review looping for a label that could
never be present, on a method with no caller in any shipped client assembly, while the feature doc,
CHANGELOG and issue all advertised the any-party rewrite as working.
- **Why missed:** "it was a live TAOM patch until commit X" was treated as verification. It verifies the
  signature, nothing else.
- **Prevent:** re-read the target body for the SPECIFIC row/field/property your hook consumes, and confirm
  the method has a caller, before reinstating a recovered patch.
- **Source:** #545 TroopWeight usage frame, 2026-09-06; same RCA, finding 3.

### A UI prompt that gates persistence has two branches; "cosmetic" is a verdict on both
The 2026-09-06 sweep found 158 settings without `RequireRestart = false` and ruled them cosmetic:
the value is read live, so only the prompt misleads. That read one branch of MCM's "Game Needs to
Restart" inquiry. `ModOptionsVM.ExecuteDone` (decompiled MBOptionScreen v1.4.5) saves and quits on
Yes; on Cancel it runs an empty delegate and `return`s past the save loop, so nothing reaches
`TAOM.json`. The change still holds for the session because `UndoRedoStack.Do` writes through to the
live instance as the slider moves, which is what made "read live" look like the whole story. Five days
later a player reported bandit settings that "never applied on disk", with 166 settings in that state.
- **Why missed:** the sweep verified the consumer side (every read is live) and inferred the producer
  side (MCM persists on Done) from the affirmative path only. A prompt with a Cancel button is a
  branch, and the branch that does nothing is the one that loses data.
- **Prevent:** before ruling a UI flag harmless, decompile the handler and read every delegate the
  inquiry takes, including the empty ones. Then pin the posture with a test that fails on the next
  omission (`SettingRequireRestartPostureTests` reflects over all four settings classes) rather than
  with a sweep that is stale the day after it runs.
- **Source:** #559 bandit scaling MCM, 2026-09-11.

### A shipped config file that MCM shadows is a document players will read as the truth
`bandit_scaling_config.json` shipped for 107 days with values MCM overrode on every install
(`SettingClamp.Clamp(TaomSettings.Instance?.Knob, json, ...)` is `value ?? default`, and
`GlobalSettings<T>.Instance` is non-null whenever MCM is loaded). Nothing read it; nothing pinned it to
the C# defaults; the docs called it the fallback for corrupted MCM values, which `SettingClamp` does only
for non-finite floats. A player opened it, saw 1.5 everywhere, and reported that TAOM ignores MCM.
Twelve more features ship the same shape.
- **Why missed:** the JSON-first pattern was copied from features that predate their MCM knobs, and the
  no-MCM fallback it serves does not exist for a mod that ships MCM as a hard dependency. Each copy
  looked like validated config; none was ever on a read path.
- **Prevent:** when a knob is exposed in MCM, the compiled default is the only fallback that runs. Keep
  it as a constant in the settings provider and pin it against `new TaomSettings()`
  (`BanditScalingSettingsProviderTests` is the pattern). A JSON earns its place only for
  fields MCM does not expose, and the doc for it must say which those are.
- **Source:** #559 bandit scaling MCM, 2026-09-11.

### A changed MCM default is announced in the HintText, because the tooltip is what the player reads
MCM writes every property to the player's `TAOM.json` on first save and loads that over the compiled
default from then on, so a moved default reaches fresh installs only. Twice on the same group
(2026-05-29 caps and initial hideouts, 2026-09-11 `BanditInitialHideoutsPerFaction` 14 to 7 and
`BanditMaxPartiesPerHideout` 3 to 6) the caveat went into the feature doc and the CHANGELOG and not
into the setting's own tooltip, and the second time the CHANGELOG even said the hints carried it.
- **Why missed:** the caveat was written where the author reads (docs, CHANGELOG) and the claim
  about the hints was written from intent, not from re-reading the attribute. The completeness
  check confirmed the docs agreed with each other and never opened the third surface.
- **Prevent:** when a compiled MCM default changes, the `HintText` says so in one sentence
  ("played an earlier build? MCM saved the old value; reset this group to pick up the new one").
  A claim of the form "X and Y both say Z" is verified by opening X and Y, not by confirming Z.
- **Source:** #559 bandit scaling MCM, deep-review 2026-09-11, `docs/reviews/rca-bandit-scaling-mcm-2026-09-11.md`.

### `{newline}` is a GameTexts variable that exists only once a Game has initialized
`{newline}` is not a localization token; it is a text variable that `GameTexts.InitializeGlobalTags()` registers from `Game.Initialize`. A `TextObject` rendered at the cold main menu (before any campaign or custom game exists) evaluates it to an empty string, so a multi-paragraph inquiry body runs together with no error. Vanilla menu code binds the variable locally for that reason.
- **Why missed:** the review traced `{newline}` to `InitializeGlobalTags` and reported it "bound at early bootstrap" without checking the caller, and the text was only ever exercised in a running game.
- **Prevent:** for any player-facing text built before a `Game` exists (main-menu options, inquiries, launcher-time messages), call `SetTextVariable("newline", "\n")` on the `TextObject` before `ToString()`, or avoid the token. In-game text needs nothing.
- **Source:** Codex adversarial pass on #560 (GPT-6-Astra, ultra), finding 5, reproduced in a fresh process against the installed localization assemblies; `docs/reviews/rca-shader-precompile-reenable-2026-09-11.md`.

### A mod control bound straight to a vanilla command inherits vanilla's semantics; a "known limitation" comment on a shipped control is an open bug (#574, 2026-09-12)
The map bar Extra Fast Forward button bound `Command.Click="ExecuteTimeControlChange"` with parameter 2, vanilla's own
fast-forward handler. Vanilla sets the time MODE; the speed lives in `Campaign.SpeedUpMultiplier`, which nothing on that
path writes, so the button did exactly what vanilla's did and the MCM slider meant nothing. #168 saw it in May, wrote
"Option A: redundant with vanilla, tracked as a future enhancement" into the mixin and closed; four months later it came
back as a player report, with a second player saying it worked (they used the E key, or turbo on Left Control).
- **Why missed:** the button was authored by copying vanilla's FastForward button XML and changing its id and tooltip,
  so its handler was vanilla's; the multiplier lived in a service the prefab never reached. The audit that found it
  framed the defect as a design choice ("redundant with vanilla") and the framing survived two later reviews of the same
  feature, both scoped to their own diffs. Mixin tests mock the adapter, so the single shared engine property was
  invisible to them.
- **Prevent:** a TAOM control binds a TAOM command that applies the mod's state and then delegates to vanilla, never
  vanilla's command directly; for every `Command.Click` in a TAOM prefab, trace the handler to a mod-owned write. A
  comment that says a shipped control does not do what its tooltip says is an issue, not a note. Before choosing a
  mixin refresh method, grep the VM for that method's call sites (`RefreshValues` on `MapTimeControlVM` is constructor
  and gamepad change only; `Tick` is per frame). `ViewModel.ExecuteCommand` silently skips a method whose parameter
  count differs from the prefab's `CommandParameter` count, so a mixin command rebound from a vanilla button takes the
  same parameters vanilla's did.
- **Source:** #574, deep-review 2026-09-12, `docs/reviews/rca-time-acceleration-button-2026-09-12.md`.

### A VM property bound to `IsHidden` or `IsVisible` is get-only, and a two-way text box is notified with the raw value
`Widget.IsHidden`'s setter raises `OnPropertyChanged` for both `IsHidden` and `IsVisible`, and `GauntletView.OnViewPropertyChanged` pushes every widget-side change back into the VM through `ViewModel.SetPropertyValue`, which invokes `PropertyInfo.GetSetMethod()` (public setters only). A bound `bool` with a public setter therefore takes write-backs from the widget's own visibility change; get-only, notified with `OnPropertyChanged(name)`, the write-back is a no-op (v1.4.8, `Widget.cs:1082-1119`, `ViewModel.cs:472-479`). `EditableTextWidget.Text` writes every keystroke into the bound setter the same way; if the VM notifies back a trimmed or folded string, the widget's `Text` setter re-enters mid-update and its visible and real text drift apart, so notify the value you were given (`EncyclopediaNavigatorVM.SearchText` stores lowercase, notifies raw). Two layout facts from the same screen: `DefaultLayout.MeasureChildren` measures every child against the incoming spec, so a `CoverChildren` button holding a `StretchToParent` overlay inflates to the whole column (fixed-height rows instead); and a `ScrollablePanel` keeps BOTH its scrollbar offset and its wheel momentum across an `MBBindingList` clear-and-rebuild (`ScrollablePanel.cs:588-598`: the velocity is cleared only when the new content does not overflow), so a row promoted to the top can sit above the viewport unless the VM resets a two-way `ValueFloat="@X"` on the scrollbar AND the screen hands it the panel's own `ResetTweenSpeed()` (found by widget id after `LoadMovie`; the VM never holds a widget).
- **Why missed:** none shipped; a Plan agent asked to break the design before implementation found the first four, and the Codex pass found the momentum half of the scroll reset by executing the panel's update equations. Each would have passed the unit tests as written and shown only in the smoke.
- **Prevent:** any new Gauntlet binding that the WIDGET can write (`Text`, `ValueFloat`, `IsHidden`, `IsVisible`, `IsSelected` on focusable widgets) gets the question "what does the VM do when the widget writes this back?" answered in the code comment; run a design stress test on a first-of-its-kind widget before writing it.
- **Source:** #587 supply search, `docs/reviews/rca-supply-search-2026-09-13.md` design-time findings.

### A prefab clone that moves the sprite off the widget the engine writes to silently drops every engine-driven Color and alpha write (#591, 2026-09-13)
Vanilla signals settlement relation and plate transparency by writing `Color`, `AlphaFactor` and `ColorFactor` on the `SettlementNameplateItemWidget`, whose own `Sprite` is the plate. `Widget.Render` (v1.4.8 `TaleWorlds.GauntletUI.cs:23451`) passes none of those to children; each widget's `OnRender` uses only its own values for its own sprite. TAOM's redesigned nameplate clones (2026-04-08) removed the item widget's `Sprite` and drew the bar, text and diamond on descendants, so every plate rendered opaque and uncoloured for five months, and the Patch38 distance fade had nothing to fade: on this prefab the only alpha-driven elements were the text and banner, which vanilla lerps toward full alpha regardless.
- **Why missed:** the stale-clone rule looks for renamed or obsolete ATTRIBUTES; here every attribute was valid and the loss was an absence, a sprite that used to sit on the widget the engine writes to. The fade feature's in-game check could only have watched elements the alpha never reached.
- **Prevent:** when a clone re-parents visuals, list every property the engine writes on the original element (decompile the owning widget's update methods) and make sure something still consumes each; a custom container widget that mirrors them is the mechanism (`TaomSettlementPlateWidget`). Verify a fade by watching the element the alpha lands on.
- **Source:** #591, `docs/features/settlement-nameplate-relation.md`.

### When a change makes one element of a composite fade or hide, enumerate every sibling the engine leaves opaque (#591, 2026-09-13)
Mirroring the item widget's alpha onto the parchment bar and diamond made the plate fade with distance for the first time. Vanilla never fades the tracked ring (`IsVisible="@IsTracked"`, alpha 1, and tracking bypasses the VM's distance cull), and it hands the party icon grid and the event icon list its own pre-late-update lerp value every frame. Result: a far tracked settlement kept an opaque ring after its plate, name and banner had gone, and the icons floated at partial alpha. Codex found it by executing the engine's update equations for one frame.
- **Why missed:** the design traced the four visuals it changed and stopped there. The engine's per-frame writes to the grid and events were read for the text alpha value they used, not for what ELSE they drive, and the ring has no alpha binding for a binding review to see.
- **Prevent:** before making any element of a composite widget fade, hide or tint, list every sibling under the same root and ask what drives its alpha; anything the engine leaves at 1 needs to follow, or the change creates an orphan. Read the engine's update method for its full write list, not just the value you wanted.
- **Source:** #591 Codex review F1, `docs/reviews/rca-settlement-nameplate-relation-2026-09-13.md` finding 4.

### A slider that widens an input range must be traced to every threshold that assumed the old range (#596, 2026-09-13)
`PlateAlphaPolicy.TextAlphaFor` kept the settlement name at full alpha until the plate fell under vanilla's 0.35, written in #591 when the distance fade was the only way a plate went that low. #596 added opacity sliders reaching 10 with no fade at all; a 10% plate settled at 0.10 and the name rendered at 29% at close range, on every slider value from 10 to 34. The constant read as an engine fact, so nobody asked what else could push the input under it. The deep review's data-flow agent and Codex found it independently; no test composed a configured opacity with the curve.
- **Why missed:** the new sliders were traced to the alpha service and the tint compare they fed directly, not to the downstream curve that consumed the alpha they changed. Same shape as the fourth NaN-gate category (a new input invalidating an existing gate), with a threshold instead of NaN.
- **Prevent:** when a setting widens the range of a value, grep for every constant or threshold the value is compared against downstream and ask whether it encoded the OLD range; write one test that composes the new extreme with each consumer. Anchor such thresholds on the configured value, not on the engine constant the configuration replaced.
- **Source:** #596 second deep review (data flow) and Codex F1, `docs/reviews/rca-settlement-nameplate-relation-2026-09-13.md` finding 10.

### A prefab clone inherits nothing: pin it to the vanilla file it hides (v1.5.3, 2026-09-15)
Bannerlord v1.5.3 added ship-banner widgets to the party nameplate prefabs and `PartyNameplateWidget.OnUpdate` dereferences `ShipBannerContainerWidget` on its first frame, unguarded. TAOM's two nameplate clones, re-based on v1.5.2 the day before, replace the vanilla files outright, so the property stayed null and every nameplate threw on the campaign map. The same failure shipped on v1.5.0 with `BloodFeudIconWidget`, and the clone carried a comment saying so.
- **Why missed:** every other layered surface inherits engine additions (an additive model calls `base`, a stylesheet sees the merged document, a postfix runs after the new body); a cloned prefab is the one full replacement, and the three prefab gates checked element types, extension XPaths and the snapshot, never the clone against the file it hides. The v1.5.0 lesson went into a comment, not a test.
- **Prevent:** `PrefabCloneWidgetReferenceTests` pairs each TAOM prefab with the vanilla prefab of the same basename and requires every attribute that names a Widget-typed property on the element's widget class to survive in the clone; value attributes TAOM changes on purpose are ignored. When a bump touches a vanilla prefab TAOM clones, re-base the clone on the installed file and re-apply TAOM's edits, never hand-merge the attribute you noticed.
- **Source:** `docs/reviews/rca-v1.5.3-nameplate-clone-2026-09-15.md` finding 1.

### A disabled button is not a gate; gate the commit method every input reaches
Patch51 greyed the recruit screen's Done button (`IsDoneEnabled = false` from a postfix on `RecruitmentVM.RefreshPartyProperties`) when the cart was unaffordable in the player's special resource, mirroring vanilla's gold gate, which sets the same flag. v1.5.3's `GauntletMenuRecruitVolunteersView.OnFrameTick` sends the Confirm hotkey straight to `RecruitmentVM.ExecuteDone`, which checks party capacity only, and `OnDone` rechecks gold only (with a `Debug.FailedAssert` saying the checks happen before). Vanilla protects its own requirement twice; the mirrored gate protected TAOM's once. With 40 Castar and a 45 Ranger in the cart the hotkey recruited the Ranger and the charge floored at 40; at zero it recruited for nothing. Live for every `recruit_cost` troop (rams, elephant, spider, Mumakil) from 2026-06-19 to 2026-09-15.
- **Why missed:** the patch copied the flag vanilla's gate sets and stopped where vanilla's gate stopped, without reading where vanilla enforces the same requirement a second time or what else calls the commit method; six deep-review agents read the VM and the patch and none opened the view that owns the hotkey.
- **Prevent:** when mirroring a vanilla UI gate, find the commit method (the one that mutates state and raises events) and gate THERE, as a prefix that returns false, with the disabled control as presentation only; list every caller of that method (button command, hotkey handler, inquiry callback, quit flow) from the installed decompile and state which are covered. `RecruitmentVM_ExecuteDone_Patch` is the shape; `RecruitGatePatchTests` pins its category and target.
- **Source:** Codex review 112 (GPT-6-Astra ultra) of #600, `docs/reviews/rca-gondor-castar-costs-2026-09-15.md` finding 5.

### A string is only "registered" in the manager its reader loads; the main menu has its own, fed by one file, first row wins (#604, 2026-09-15)
The 49 Advanced Starting Options rows went into `taom_module_strings.xml` and the menu showed `ERROR: Text with id str_campaign_starting_options_item_name doesn't exist! Variation: rivendell` for every TAOM kingdom and "Sturgia" for Dale. `Module.CurrentModule.GlobalTextManager` (what every pre-campaign screen reads) is a different `GameTextManager` from `Game.GameTextManager`, filled once by `LoadDefaultTexts` from the literal path `ModuleData/global_strings.xml` of each module, raw file after raw file, with no merge and no XSLT; SubModule.xml `GameText` nodes never reach it. And `GameText.AddVariationWithId` appends a same-id row whose text differs while `GetVariation` returns the first match, so even a correctly placed TAOM row that reuses a vanilla variation id loses to Native's. `global_strings.xml`'s own header had said the first half since the keybinding work.
- **Why missed:** the rows were authored against the in-game reader's semantics (merge by id, later module wins) and tested by scanning the file they were in; nobody opened the ASO screen until a day later, and no test asked which manager the reader used. The XSLT reflex ("override vanilla strings with a transform") is right for the per-Game manager and silently inert here.
- **Prevent:** before writing a `str_*` row, grep the decompile for the reader and note whether it calls `GameTexts.FindText` (per-Game, GameText XML, merge by id, XSLT works) or `Module.CurrentModule.GlobalTextManager` (main menu, `global_strings.xml` only, append-first-wins, needs `GlobalStringsOverrides` to replace a vanilla id). Pin the placement with a test that reads the specific file (`TaomStartOptionsProviderTests.NoMenuFacingAsoRow_LivesInTheGameTextXml`), and pin the engine semantics on the installed DLLs (`GlobalStringsOverridesTests`) so a TaleWorlds change to `AddVariationWithId` is a failing test, not a silent redundancy.
- **Source:** #604, the in-game ASO screen on v1.5.3, `docs/features/advanced-start-options.md` section 3.

### An add-only id sync reads a rename as an addition (#617, 2026-09-18)
#617 replaced the ranged ladder's 130 band items (`ladder_<line>_<cls>_<e..c>`) with 123 per-tier items (`..._t<tier>`). The generator rewrote the English rows; `translate_with_claude.py --sync-ids` only appends ids a language lacks and never removes one. So all 12 translated languages kept 130 dead rows and had none of the new ids, `check_external_loc_coverage.py` failed at +132 to +137 per language, and every non-English player saw English bow names. The plan had filed "translator run owed" as a follow-up, as if the change were only additions.
- **Why missed:** the id change was treated as new text needing translation, not as a rename whose translations already existed under the old ids. The loc gate was not run before commit.
- **Prevent:** when a generator changes ids, decide per id whether it is new or renamed. For a rename, carry the existing translation across by construction (`tools/sync_ranged_ladder_translations.py` derives each tier name from the retired band row the same donor produced) and remove the retired rows in the same pass; reserve the machine translator for genuinely new text. Run `python tools/check_external_loc_coverage.py` before committing any generator that writes loc rows.
- **Source:** #617 deep review, `docs/reviews/rca-ranged-rebalance-2026-09-18.md` item 1.
- **Second half, found by the second review:** the fix moved the rows and left `tools/translation_cache/<lang>.json` keyed on the retired ids, so `rebuild_translation_files.py` (override, then cache, then English) would still have written English over all 1,476 carried names. `docs/reference/localization-map.md` already said a tool that rewrites translated text must update the cache; it was not read, because nothing loads it when a tool is written. A rename moves the rows AND the cache in one pass, and the tool's `--verify` checks both. `docs/reviews/rca-ranged-rebalance-second-review-2026-09-18.md` finding 2.

### Nothing receives a chat message before the initial screen; report a startup problem in an inquiry (plan 009, 2026-09-24)
`InformationManager.DisplayMessage` is `DisplayMessageInternal?.Invoke(message)` with no queue. Its single-player subscribers, `MPChatVM` (the chat log) and `ChatLogMessageManager` (sound only), are built by `GauntletChatLogView` in Native's `OnBeforeInitialModuleScreenSetAsRoot`, after every module's `OnSubModuleLoad`. So a message sent from `OnSubModuleLoad` goes nowhere, silently. A message sent from the first `OnBeforeInitialModuleScreenSetAsRoot` does reach the chat log, but on first launch the splash video hides it and `GauntletInitialScreen.OnInitialize` then calls `ClearAllMessages()`. Plan 009's failure notice for 28 module-load patch categories was sent from `OnSubModuleLoad` and cleared its list as it did, and the green "TAOM loaded successfully!" line beside it has never displayed.
- **Why missed:** the plan took the existing green line as precedent and never asked who subscribes to the event, or when; `DisplayMessage` compiles and never throws, so nothing failed.
- **Prevent:** a notice raised during startup waits for TAOM's `OnBeforeInitialModuleScreenSetAsRoot` (Native's runs first) and goes through `InformationManager.ShowInquiry`, which `GauntletQueryManager` queues and the initial screen does not clear (precedent: `StallReportNotifier`). Gate: `PatchCategoryApplierTests.SubModuleSource_OnSubModuleLoad_DoesNotReportPatchFailures`. For any on-screen notice, trace the event to its subscriber and to whatever clears it before the player looks.
- **Source:** `docs/reviews/rca-guarded-patch-category-apply-2026-09-24.md` finding 1.
- **Second instance (#916, 2026-10-19):** the shader-source copy warned with a red `DisplayMessage` from the main-menu
  hook, so the initial screen cleared it. The cause was the brief, which said "show one InformationManager message",
  and the gate above reads `SubModule.cs` only. A brief for any startup notice names `ShowInquiry`.
  `ShaderSourceInstallWiringTests` pins the inquiry for that feature's two files only; a startup notice in
  any other feature folder is still unchecked, so review for it.
  Source: [rca-bow-shader-invisible-2026-10-19.md](../rca-bow-shader-invisible-2026-10-19.md) finding 1.

### Check a translated row where the engine reads it, not by its presence in the file (plan 009, 2026-09-24)
`LocalizedTextManager.LoadLanguage` (v1.5.3) reads only `<string>` children of a `<strings>` child of the root, and skips any other `<string>` without a log line, so the player sees the English default. Plan 009 seeded five keys into the 12 `std_taom_module_strings_*.xml` files with `tools/translate_with_claude.py` `sync_missing_ids`, which wrote all 60 rows after `</strings>`: it detects CRLF because `"\r\n" in raw` is also true of these files' `\r\r\n` endings, their bare-LF tail rows and `</strings>` then split as one line, and the insert lands after it. The CHANGELOG said the notice was translated into 12 languages; in game it was translated into none.
- **Why missed:** every check measured presence. `LanguageFileCoverageTests`, `LanguageTextIntegrityTests` and the translator's own `tree.iter("string")` all walk descendants, so a row at any depth counted as translated, and the builder quoted the green coverage test as proof of delivery.
- **Prevent:** gate `LanguageDataXmlTests.AllTranslationFiles_StringRowOutsideRootStrings_IsNeverPresent` fails any row outside `<base>/<strings>`. A test that proves text reaches the player reads it along the engine's load path, not with `Descendants()`. Until `sync_missing_ids` anchors on the `</strings>` line, check a seeded language file with that gate before committing it.
- **Source:** `docs/reviews/rca-guarded-patch-category-apply-decisions-2026-09-24.md` findings 1 and 2.

### Translate a fragment that fills a sentence variable with the sentence, and read the rendered result (plan 009, 2026-09-24)
The patch-failure notice builds "... during {PHASE} ..." and fills `{PHASE}` with a separately translated phase name. Translated alone, the fragments broke three languages: German "während Start" (no genitive article), French "lors de le démarrage" (must contract to "du"), Japanese "起動時中に" (時 and 中 stacked). Polish and Russian happened to store genitive forms and read correctly.
- **Why missed:** each key went to the translator on its own, and the check was that `{PHASE}` and `{GROUPS}` survived, not that the filled sentence reads.
- **Prevent:** when a key's text is a fragment for another key's variable, send both to the translator together with the host sentence as context, and read the rendered sentence once per language for each fragment. Where the grammar cannot be made to agree, use one whole-sentence key per case. A hand fix goes into both the XML row and `tools/translation_cache/<lang>.json`: the translator re-translates only a row that still equals the English, so a fixed row stays fixed.
- **Source:** `docs/reviews/rca-guarded-patch-category-apply-decisions-2026-09-24.md` finding 3.
- **Second occurrence (#769 E9, 2026-10-14):** the career Active Effects class line was built from a line key,
  three class nouns and a join word, and the number, sign and effect noun were glued in English order. Fixed
  with one whole-phrase key per class set (`{EFFECT} for infantry`) and a `{VALUE}` variable in every effect
  label. A brief that asks for localized text names this lesson (`docs/reviews/rca-career-e8e10-2026-10-14.md`).
### A seeded language row takes its file's own terminator, and a placement gate reads what the engine reads (plan 022, 2026-09-24)
Plan 022 seeded three rows into the 12 `std_taom_module_strings_*.xml` files, which use `\r\r\n`. The rows ended in
a bare LF. `translate_with_claude.py sync_missing_ids` splits on one terminator, so a trailing run of LF lines fuses
with `</strings>` into one "line" and the next seeded row lands after `</strings>`, where
`LocalizedTextManager.LoadLanguage` never reads it. This branch's rows were misplaced that way and moved back by hand;
the terminators were not restored. `LanguageFileCoverageTests` counts `string` rows anywhere in the file, so it passes
a misplaced row.
- **Why missed:** the hand fix checked placement, not bytes, and no gate reads rows the way the engine does.
- **Prevent:** after any seeding, check the new lines' terminators against the file's (`git diff | cat -A`) and fix
  them with a binary round-trip. Follow-ups: `sync_missing_ids` should insert before the `</strings>` token by
  position, and the coverage test should read `base/strings/string` only.
- **Source:** `docs/reviews/rca-order-of-battle-auto-assign-2026-09-24.md` row 11 (XML lens).

### A language row kept identical to the English is rewritten from the cache on every run: pin it in the overrides (hill troll, 2026-09-25)
The Italian editions keep `Dale` untranslated, and the IT row for `taom_aso_kingdom.sturgia` held `Dale` by hand.
`translate_with_claude.py` discovers a row when `cur_text == eng_text`, so that row counted as untranslated on the
hill troll's translator run and was rewritten from the cache, which held `Valle`. Nothing in the run's output
distinguished it from the 26 rows the run was meant to fill.
- **Why missed:** the discovery gate cannot tell "not yet translated" from "translated as the English word"; the
  guide documented the stale-English direction of this trap, not its mirror image.
- **Prevent:** a canonical rendering that equals the English goes in `tools/translation_overrides/<lang>.json`, and
  the cache gets the same text (`it.json` now pins `Dale`). After any translator run, diff the changed rows against
  HEAD by id, not by line, and read every row that changed from a non-English value.
- **Source:** the hill troll translator run, 2026-09-25 (`docs/localization/TRANSLATOR_GUIDE.md` "mirror-image trap").

### A documented checklist did not stop 351 keys going stale: sweep the history, then gate it (2026-09-25)
The TRANSLATOR_GUIDE had described the stale-English trap since #388 (2026-08-06). A sweep on
2026-09-25 still found 351 keys whose English had changed while every language kept the old text:
renamed lords, 249 career tooltips with old numbers, rewritten faction lore, an empty promotion offer.
- **Why missed:** nothing reports it. Discovery skips any row that differs from the English, so a
  run over a stale file prints "0 untranslated" exactly like a clean one; the checklist relies on the
  author of the English edit remembering 12 other files.
- **Prevent:** after an English edit, reset the rows and the cache in the same change. Periodically,
  walk each English source's `git log`, find each key's last text change, and flag rows byte-identical
  to their value at that commit's parent (the method in the guide). Follow-up: turn that sweep into a
  `tools/` check so the count is computed, not discovered.
- **Source:** the 2026-09-25 localization sweep (CHANGELOG `fix(loc)`).

### A character copied with its localization key shows the original's name in every other language (2026-09-25)
Lindon's 21 troops were copied from Rivendell's and kept the Rivendell `{=KEY}`s with new English
defaults; six Umbar clans shared one key; two quest templates reused a third's fallback keys. English
looked right everywhere, because English reads the inline default. The other 12 languages look up the
key alone, so they showed one text for both.
- **Why missed:** no gate compares the inline defaults of one key across files, and every English
  surface a reviewer checks renders correctly.
- **Prevent:** a character's name key is `aom_<id>_name`, changed whenever the character is copied;
  a C# fallback key belongs to one class. When splitting a shared key, give the copy the new key, carry
  the existing translations onto it, and translate only the reset original.
- **Source:** the 2026-09-25 localization sweep (TRANSLATOR_GUIDE "One key, one character").

### An override is a translation of one English text and goes stale with it (2026-09-25)
`tools/translation_overrides/ru.json` pinned `taom_precompile_hint` to a translation of its July
English. The English was rewritten for #560; the override outranked the cache and the re-translation,
so Russian kept describing the old behaviour.
- **Why missed:** overrides are written for names that never change, and nothing flags an override
  whose key's English moved afterwards.
- **Prevent:** keep sentences out of the override files; when editing English, grep the override
  files for the key (TRANSLATOR_GUIDE checklist step 5).
- **Source:** the 2026-09-25 localization sweep.

### Seed a translation row by ownership: a second row for the same id in one language replaces the first (2026-09-25)
`global_strings.xml` declares 26 `taom_aso_*` keys, 23 of them also in `taom_module_strings.xml`, and the translator maps it to the
keybind file, so a `--sync-ids` run seeded all 26 into every keybind file. The engine keeps the row a language loads
last (`LocalizedTextManager.DeserializeStrings` assigns by indexer; files load in `language_data.xml` order), and the
keybind file loads after the module file: 22 copies replaced curated rows in 11 languages, the Italian "Dale" among them.
- **Why missed:** the run printed "26 ids seeded" and nobody asked whether those ids already lived in another file of
  the language; every check read one file at a time.
- **Prevent:** `translate_with_claude.py` now skips a key another English source owns (`key_owners`) or another file
  of the language carries (`skip_ids_for`); `LocalizationKeyConsistencyTests` fails on one id with two texts in one
  language. When two English sources declare a key, treat that as a smell, not a feature.
- **Source:** `docs/reviews/rca-hill-troll-and-loc-sweep-2026-09-25.md` finding 1.

### Read the script a translation is written in: the model returns words from other languages (2026-09-25)
Korean 돌진 and Chinese 氏族 inside Turkish sentences, "[Ривенделл]新obranец" and Latin look-alike letters inside
Russian words ("Брандa", "Бûрзгâш"), katakana in the Korean "Imladris", "黑numenor" in Chinese: about 120 rows across
nine languages, each also in the cache, so every re-run served it back. The placeholder gate passed all of them.
- **Why missed:** every check on translator output compared ids, counts and placeholders, never the characters.
- **Prevent:** `LanguageTextIntegrityTests.NoTranslatedString_MixesWritingSystems` (per-language allowed scripts, no
  word gluing two scripts in Russian, no lowercase Latin run glued to CJK or Hangul). Repair a flagged row and its cache
  entry together: a letter map for Latin look-alikes in Cyrillic, a reset plus re-run for the rest.
- **Source:** `docs/reviews/rca-hill-troll-and-loc-sweep-2026-09-25.md` finding 2.

### The seeding terminator bug came back: fix the tool the first time a lesson names it (2026-09-25)
Plan 022's lesson (2026-09-24, above) named `sync_missing_ids` giving seeded rows the wrong terminator in `\r\r\n`
files and left the fix as a follow-up. A day later the hill troll's translator runs seeded 384 more such rows (156 in
the repo, 228 in the live Armory), and the builder had written that the translator preserved terminators, having
checked `write_back` only.
- **Why missed:** a follow-up with no owner and no failing test is a note, not a fix.
- **Prevent:** `sync_missing_ids` now keeps each line's own terminator, with a `\r\r\n` test. A lesson that ends in
  "follow-up" gets an issue or a failing test the same day.
- **Source:** `docs/reviews/rca-hill-troll-and-loc-sweep-2026-09-25.md` finding 10.
- **Recurrence (2026-10-21):** the tool gap behind the next class was marked follow-up twice with no issue:
  MS7-4 in `rca-mouth-of-sauron-gear-2026-10-04.md` and finding 11 in `rca-generated-lord-kits-2026-10-06.md`
  (`validate_xml_schemas.py` printing NOT REGISTERED for files the engine does load). The 2026-10-19
  line-ending follow-up was half done: the merge `85cbd20c8` rewrote the 12 module strings files and the
  RCA noted it, but the guide never described the blank line after almost every row until this date. Rule: a
  follow-up with no issue is a note; file the issue the day the RCA is written.
- **Recurrence (2026-10-23, #980):** the 2026-10-21 bullet above shipped in a commit (`48d66b59a`) whose RCA again
  left three items as follow-ups with no issue; #980 was filed two days later. The rule has now failed in prose three
  times, so it becomes a machine check: #984 makes `lint_docs.py` report a new RCA line with `FOLLOW-UP` that cites no
  issue and no reason. Source: `docs/reviews/rca-schema-check-2026-10-23.md` root-cause pattern.

### Gate the translation cache, not only the rows (2026-09-25)
The writing-system gate read the repo's language files, but `tools/translation_cache/<lang>.json` holds the
translator's output for all three modules, the unversioned Armory and TAOM_Map included, and a re-run writes
its values back. Damage fixed in the rows but left in the cache returns on the next run.
- **Why missed:** the gate was written against the files the repo ships, and the cache is not shipped.
- **Prevent:** a content rule on translations runs over the cache too
  (`NoCachedTranslation_MixesWritingSystems`); a word rule judges each hyphen-separated part of a name, so
  "Bahr al-Yeshm" and "Cigfran-lûth" stay clean.
- **Source:** `docs/reviews/rca-hill-troll-and-loc-sweep-2026-09-25.md` finding 36.

### Read the View and its hotkeys before claiming a UI has no action (2026-09-28)
`taom.print_face` was built because "the face editor has no copy or export action": `FaceGenVM` has no clipboard
code. The copy lives in the view that hosts it: `BodyGeneratorView.TickInput` puts
`CurrentBodyProperties.ToString()` on the clipboard on Ctrl+C (hotkeys from `FaceGenHotkeyCategory`), in character
creation and the barber. The command, its tests and its doc rows were removed.
- **Why missed:** the search stopped at the view model; Gauntlet screens put input handling in the view and the
  hotkey category, which were never opened.
- **Prevent:** before stating that a vanilla screen lacks an action, read its VM, its View (`TickInput`, `OnTick`)
  and the hotkey categories it registers.
- **Source:** `docs/reviews/rca-saruman-lord-and-faces-2026-09-28.md`.

### Fade a TextWidget through `Brush.GlobalAlphaFactor`, never `AlphaFactor` (2026-09-30)
In v1.5.3 `Widget.AlphaFactor` is read only on a widget's own sprite path (`Widget.cs:2306`); a `TextWidget`
renders through `BrushWidget.OnRender`, and its text alpha comes from the brush (`BrushRenderer.CreateTextMaterial`:
style `TextAlphaFactor` times `Brush.GlobalAlphaFactor`). Bound on a text widget, `AlphaFactor` binds cleanly and
does nothing. Vanilla fades text with `Brush.GlobalAlphaFactor="@Alpha"` (`SPChatLog.xml:84`).
- **Why missed:** the realm names bound `AlphaFactor` by analogy with plain widgets; the view-model tests were
  green because the view model was right, and nothing read the prefab against what the renderer applies.
- **Prevent:** fade text through its brush; a prefab test can forbid `AlphaFactor` on any `TextWidget`
  (`RealmNamesPrefabTests`).
- **Source:** `docs/reviews/rca-realm-borders-2026-09-30.md` finding 1.

### A result message states what the action did on every branch, not what was recommended (2026-10-01)
The #701 advisor told every player "saved: Number of Ragdolls 5, Number of Corpses Low", but Apply only lowers each
option (`Math.Min`), so a player already below the recommendation was told a value that was never written.
- **Why missed:** the never-raise rule was added to the code after the text was written from the recommendation;
  nothing compared the message with each branch of the action it reports.
- **Prevent:** before registering a result or confirmation string, list the action's branches and check the text
  is true on each; when it can only be true on some, make it value-neutral or format in the values written. Name
  vanilla's options through vanilla's own keys (`{=1awQTVqN}Number Of Ragdolls`), so a translation matches the
  Options screen.
- **Source:** `docs/reviews/rca-battle-corpses-2026-10-01.md` finding 1.
- **Recurrence (2026-10-14):** the career tree-version migration said "your career choices were refunded" when it
  had refunded nothing (a hero holding only the root). It now returns the count, and the message needs a count
  above zero. In the same change, an inquiry's enable gate returned "You don't have enough denars." on both
  branches, and `SingleQueryPopUpVM` shows any non-empty reason as Confirm's hint, so a player who could pay
  read that they could not; return an empty reason on the allowed branch. `docs/reviews/rca-career-e4e7-2026-10-14.md`
  findings 3 and C1.

### A ButtonWidget with a bound IsEnabled needs `UpdateChildrenStates="true"` (2026-10-14)
The career screen's Retrain button bound `IsEnabled="@CanRetrain"`. Disabled, its frame dimmed and its label stayed
bright, because `Widget.SetState` passes the Disabled state to children only when `UpdateChildrenStates` is set
(installed v1.5.5), so the label brush's own Disabled style never applied.
- **Why missed:** it was the prefab's first bound `IsEnabled`; the markup was copied from a button that is never
  disabled. Of vanilla's 14 buttons on this brush, 11 set `UpdateChildrenStates`, and 6 of the 7 that bind
  `IsEnabled` do.
- **Prevent:** whenever a prefab binds `IsEnabled` on a `ButtonWidget` with child text or icons, set
  `UpdateChildrenStates="true"` on it.
- **Source:** `docs/reviews/rca-career-e4e7-2026-10-14.md` finding 8.

### Never put `\n` in a `{=KEY}` default; use `{newline}` (2026-10-01)
`harvest_literal_loc_keys.py` writes a C# `\n` into the XML `text` attribute as a raw line break, which XML parses
back as a space, so the registered English silently differs from the inline default and
`LocalizationKeyConsistencyTests` fails far from the cause (#702).
- **Why missed:** a two-paragraph inquiry body was written with `\n\n`, the C# habit; the house idiom
  (`taom_precompile_inquiry_body`) was not looked up.
- **Prevent:** one paragraph, or `{newline}`. Until #702 lands, read the harvested row back before translating.
- **Source:** `docs/reviews/rca-battle-corpses-2026-10-01.md` finding 3.

### An element's own attributes bind in the scope its `DataSource` sets (2026-10-01)
`TFGDetails.xml` put `IsEnabled="@IsEyesEnabled"` on the element whose own `DataSource` is `{EyebrowTypes}`. Gauntlet resolves an element's own bindings against the view model its `DataSource` selects, so the lookup ran on a `FaceGenPropertyVM`, read null, and the bool setter took false: the eyebrow and teeth steppers inside could never be clicked, because `EventManager` does not descend into a disabled widget. On an element whose `DataSource` is a list the same binding is simply never applied. Kysaro's faction screen records the mirror case in a comment: a `Command.Click` on an element given `DataSource="{Selected}"` resolved against the sub-view-model and never fired.
- **Why missed:** no test walked the ported prefabs against their view models, and the scoping rule is in none of `gui-ui.md`'s binding rules.
- **Prevent:** a binding meant for the outer view model goes on a wrapper widget with no `DataSource`. A prefab TAOM ships gets a type-aware binding test: `FactionScreenPrefabBindingTests` follows each `DataSource` and `ItemTemplate` scope in both directions, and its reverse direction found two dead view-model properties on its first run.
- **Source:** `docs/reviews/rca-faction-ui-2026-10-01.md` row D1.

### A data key two characters share with different English needs splitting before it is registered (2026-10-01)
Registering the inline text of `characters/heroes.xml` and `npcs_*.xml` found ten keys given to two characters with
different English: six Gundabad and Dol Guldur hero bios (`ulgrim_description` and five more) and four Lindon caravan
names that reused Rivendell's `aom_rv_*` keys. English hid it, because each site carries its own inline default;
one registered row per key would have shown one hero's bio on the other in every other language.
- **Why missed:** the one-default-per-key test covered only the name generator's first four sources.
- **Prevent:** `EveryNameKey_InTheNameGeneratorsSources_HasOneEnglishDefault` now scans every generator source;
  split a shared key before registering, never after translating. **For text the engine saves with the object**
  (`Hero.EncyclopediaText`, hero names: `[SaveableProperty]`, and heroes.xml is read only for a new campaign), give
  BOTH sites new keys and retire the old one: renaming only the second site left the old key registered with the
  first hero's English, so old saves show that bio on the second hero in twelve languages (accepted as a known
  limitation this time; `0bd6abf3`).
- **Source:** `0bd6abf3`; `docs/reviews/rca-full-translation-run-2026-10-02.md` finding 6.

### The translator model keeps Latin circumflex vowels inside Russian orc names (2026-10-01)
Out of about 55,500 new rows (4,627 keys in 12 languages), 95 failed the writing-system gates; re-translating fixed
most, but 33 Russian rows came back with the same `Гробûрз`, `Скарнâк`, `Маугâш` on every attempt, the source's
circumflex vowels copied into Cyrillic, so a further paid retry would not have helped. The gate itself could not see
a Devanagari or Arabic letter (`Воргराш`, `Korساrenrouten`): `Script()` classified only five scripts, so five such
rows shipped (one new, four older); any other letter now counts as foreign in every language.
- **Why missed:** the placeholder check passes these rows; only `LanguageTextIntegrityTests` sees them.
- **Prevent:** after a large run, run `FullyQualifiedName~MixesWritingSystems`, reset and re-translate the listed rows
  once (the test lists at most 60 at a time), then fix what remains deterministically: inside a Cyrillic word,
  `â û ô î ê` become `а у о и е`, in the row and its cache value together.
- **Source:** `0bd6abf3`; `docs/reviews/rca-full-translation-run-2026-10-02.md` findings 3 and 4.

### A Batches API job can sit unprocessed for hours; the translator writes nothing until it ends (2026-10-01)
Twelve batches went in together: two finished, ten showed `processing=122 succeeded=0` more than two hours later.
The key was invalidated mid-poll, and the ten translator processes died with a 401 before their write-back, so
nothing on disk changed. Cancelling a batch with nothing processed costs nothing; the ten languages ran live instead.
- **Why missed:** `--batch` was chosen for price alone; the Batches API promises completion only within 24 hours.
- **Prevent:** record each batch id as it is submitted (the log prints it), and for a run that must finish in one
  sitting, prefer the live path or set a deadline after which unprocessed batches are cancelled. A recovery reads
  results by string id, never by chunk position, because English edits made meanwhile shift the chunks.
- **Source:** the 2026-10-01 run behind `0bd6abf3`; `docs/reviews/rca-full-translation-run-2026-10-02.md`.

### A UI resource refresh rebuilds every open movie under its old identifier, and only while the main menu is active (2026-10-01)
`GauntletUISubModule.RefreshResources(false)` releases every open movie, rebuilds the sprite, font and brush tables (`UIResourceManager.Refresh`), then reloads each movie into a new `UIContext` through `GauntletLayer`'s private `LoadMovie(GauntletMovieIdentifier)`, so a patch on the public `LoadMovie` never sees it and the identifier's `Movie` is a new object. Anything that cached widgets from the old tree (FactionUI's effects classes) keeps animating detached widgets. The refresh runs only when `_areResourcesDirty` is set, only `OnNewModuleLoad` sets it (option and language changes do not), and the engine allows a runtime module load only while the main menu screen is active (`MBInitialScreenBase` calls `SetCanLoadModules(true)` on activate, `false` on deactivate).
- **Why missed:** the refresh postfix restored the resource tables, and nobody asked what else referenced the rebuilt trees, or when a refresh can happen at all.
- **Prevent:** code that caches widget references across frames states which screens it serves; for a main-menu screen, either re-find widgets when the layer's `UIContext` changes or accept and document the gap. Settle reachability from `SetCanLoadModules` before writing re-attachment code for any other screen.
- **Source:** `docs/reviews/rca-faction-ui-2026-10-01.md`, Codex F1.

### A Gauntlet command handler is an entry point: guard it like a patch (2026-10-01)
A card click on the faction screen (`FactionScreenVM.SelectHero`, #704) ran the takeover check, a pass over every living hero, the kingdoms and the uncapturable registry, with nothing to catch a throw, and Gauntlet's command dispatch rethrows. Player Switcher runs the same check inside Patch77's try. The click now falls back to a copy and logs the exception.
- **Why missed:** TAOM's "entry points delegate and never throw" habit is attached to Harmony patches and behaviours; a view model's `Execute`/`Select` method reached from a prefab command did not register as one.
- **Prevent:** any method a prefab command reaches, which does more than set a view-model property, wraps the work so a failure degrades the screen's action (here: copy instead of take over) and logs `{ex}`, with a test that makes the dependency throw.
- **Source:** `docs/reviews/rca-faction-ui-takeover-2026-10-01.md` T6.

### Never register a vanilla key a stylesheet keeps; vanilla's translation is the better one (2026-10-02)
The 2026-10-01 run registered 36 keys `comment_strings.xslt` and `action_strings.xslt` write, believing them TAOM's own.
They were SandBox's: the stylesheets keep each key and only drop "the". TAOM's language files load after SandBox's and
`LocalizedTextManager.DeserializeStrings` keeps the last row for an id, so 432 machine rows replaced TaleWorlds' curated
ones, which carry the engine's grammar tokens: Turkish lost `'{.e}`/`'{.den}` (players would read "Gondor'e"), and in
DE, FR, BR and IT a male player read "meine mein Herr". Reverted on 2026-10-02.
- **Why missed:** the registration script filtered on "no TAOM English row", never on "vanilla defines it", and the
  research agent that judged the keys TAOM-authored had searched `SandBoxCore`, while the strings live in `SandBox`.
- **Prevent:** `VanillaKeyOverrideTests` (LiveInstall) fails on any TAOM language row whose id vanilla's English data
  declares, outside an allowlist with reasons. A stylesheet that keeps a vanilla key and its meaning registers nothing;
  a change of meaning takes a TAOM key (`TAOM_liege_*`).
- **Source:** `docs/reviews/rca-full-translation-run-2026-10-02.md` finding 1.

### A delegated search's "not found" is checked against the module list before it is relayed (2026-10-02)
The same run's planning relayed an agent's claim that the 36 XSLT keys were "absent from vanilla" after checking two
other claims by hand. The agent had grepped one of the three vanilla modules that ship ModuleData. Two lenses of the
deep review found the error in minutes by searching all of them.
- **Why missed:** the spot-check covered the claims that looked load-bearing for scope (a dead file, a shared key),
  not the one that decided whether to register 36 keys.
- **Prevent:** a negative finding ("X does not exist in vanilla") is relayed only with the paths it searched; check
  them against Native, SandBoxCore, SandBox, StoryMode and CustomBattle (and the DLC modules) before acting on it.
- **Source:** `docs/reviews/rca-full-translation-run-2026-10-02.md` finding 1.

### The loading-screen horse and first background are native: find the name in the installed packages, then shadow it (2026-10-03)
The horse in the corner of every loading screen and the first screen's background are drawn by the native
`rglLoading_screen_view`, not Gauntlet, so no prefab or patch reaches them. A TAOM texture carrying the vanilla
name (`loading_sprite_bannerlord`, `loadingscreen_default`, `loadingscreen_naval`) replaces it, even when a
material binds it. Vanilla still shows for about a second at launch, before TAOM's packages load.
- **Why missed:** two false leads cost time. A search subagent reported the horse as part of the 1920 x 1080
  loading paintings; the first native string hit, `warrider_logo` ("loading game logo"), is the game wordmark. The
  community guide's background name, `loadingscreen_texture_1`, exists in no v1.5.3 package.
- **Prevent:** for anything drawn natively, list the installed packages' textures and materials by name
  (`tools/audit_map_scene_memory.py`'s TOC reader), decode the candidate (it decodes DXT5 and BC7 with Pillow in
  memory) and read the material's `MeshVectorArgument` before authoring art. Treat a guide's resource name as
  version-bound.
- **Source:** `docs/features/native-loading-screen.md`.

### Player-typed text never goes into a TextObject template with its braces (formation presets, 2026-10-09)
The localized preset messages pass the preset's name through `SetTextVariable("NAME", name)`. The string is stored
as it is, but at render time `TextObject.GetVariableValue` wraps it in a new `TextObject` and the text processor
reads it again as markup, so a name with braces renders with that part empty, and a `{=key}` prefix is taken as a
localization key. The old code interpolated the name raw, so the localization pass brought the bug in.
- **Why missed:** a variable value reads as data; nothing at the call site shows that it is parsed again.
- **Prevent:** remove `{` and `}` from player-typed text (preset names, party names, search text) before it reaches
  a template, or keep that text out of the template.
- **Source:** `docs/reviews/rca-formation-presets-2026-10-09.md` finding 7; `TextObject.cs:149-151, 319-381` and
  `TextProcessingContext.GetVariableValue` (v1.5.4).

### A tool that writes a named Armory item writes its English loc row too (#794, 2026-10-15)
`apply_wildmesh_armory.py` wrote five Horse items with `{=taom_wildmesh_<animal>_a}<name>` and no row in the Armory's
`Languages/loc_LOTRAOM_horses.xml`. The same gap shipped with the Animalia elk and moose (#646 row 20) and was fixed
there by hand during review, so the recipe the new writer was cloned from still had no loc step.
`check_external_loc_coverage.py` passed both times, because it counts only rows that already exist in the root file.
- **Why missed:** a fix made by hand at review time never reached the tool, and the gate cannot see a key that has
  no row at all.
- **Prevent:** a writer that adds a named item writes its English row in the same run (the WildMesh writer's loc
  step), and its parity test checks the row; then the coverage gate shows the 12 languages as owed. A gate that
  also counts inline Armory keys with no root row is a follow-up.
- **Source:** [rca-wildmesh-mounts-2026-10-15.md](../rca-wildmesh-mounts-2026-10-15.md) row 5.

### A name generator that globs a folder registers whatever lands in it (Harad repair, 2026-10-15)
`generate_name_localization_strings.py` reads every `characters/*.xml` for its `character_name` category. `08b30bfc4`
added `taom_vanilla_troop_obsolete_overrides.xml`, 40 retired vanilla troops under their vanilla `{=KEY}` names, so
the generator wanted to register 40 vanilla keys as TAOM rows, which would load later and replace vanilla's own
translations, and its shipped-tree test went red.
- **Why missed:** the category excluded the files other categories own by name, and nothing asked whether a new file
  in the folder carries vanilla keys.
- **Prevent:** exclude a file that keeps vanilla `{=KEY}` names from every TAOM name category, with a test naming it
  (`test_CharacterNameSources_ExcludeFilesOtherCategoriesOwn`). Before an `--apply`, read what it adds: a key without
  the `aom_`/`taom_` shape is a vanilla key.
- **Source:** [rca-harad-repair-2026-10-15.md](../rca-harad-repair-2026-10-15.md) finding 11.

### Deferring the paid translation never defers the free seed (Gondor scenes, 2026-10-17)
Two Custom Battle names were registered in English while the paid translation waited for Mike's word, and no row
reached the 12 language files, so `LanguageFileCoverageTests` went red: the #434 "registered but never propagated"
class again.
- **Why missed:** the builder read "no paid run" as "skip `/localize`", and the test that would have shown it could
  not run because the main checkout's test project did not compile (another session's work).
- **Prevent:** after registering any key, run `python tools/translate_with_claude.py --lang <L> --module TAOM
  --sync-ids --seed-only --apply` for all 12 languages (free; it returns before any provider call), dry-run first so
  another session's keys are not seeded with yours. When the main checkout cannot build the tests, run the
  `Infrastructure.Localization` filter in an isolated copy (`git archive HEAD` plus your files).
- **Source:** [rca-gondor-field-scenes-2026-10-17.md](../rca-gondor-field-scenes-2026-10-17.md) finding 1.

### A timing or effect claim about prefab XML comes from the engine method that reads it (Shadowfax Screens, 2026-10-18)
The feature's comment, doc and issue said the character screen's top extension "waits 0.7 s and then slides for
0.3 s, about 1 s". The values were read correctly from the installed prefab, but `Widget.UpdateVisualDefinitions`
counts `DelayOnBegin` inside `TransitionDuration` and snaps at the duration, so that definition could never slide,
and no widget uses it. The same assumption kept dead `DelayOnBegin` handling in the code.
- **Why missed:** reading attribute values felt like verification; the method that interprets them was not read.
- **Prevent:** before writing what a GUI attribute does (a duration, a delay, an alignment), read its consumer in the
  installed DLL (`pwsh tools/taom-src.ps1 path Widget`) and grep for a widget that uses the definition; state the
  engine version and the method in the doc.
- **Source:** `docs/reviews/rca-shadowfax-screens-2026-10-18.md` F2, F6.

### Player text that names a key binds the key (Host Review, 2026-10-19)
The Host Review start message said "Hold Tab to leave." Leaving is game key 4, Tab only on a keyboard with default
bindings and `ControllerRRight` on a gamepad. Vanilla binds the name: `{leave_key}` in
`Native/ModuleData/module_strings.xml`.
- **Why missed:** the builder copied the default key from the engine code that reads it.
- **Prevent:** never write a key name in player text. Use a `{UPPERCASE}` variable set from
  `HyperlinkTexts.GetKeyHyperlinkText(HotKeyManager.GetHotKeyId(<context>, <id>))` (uppercase, so the translator
  protects it), and show it with `MBInformationManager.AddQuickInformation`, which renders the key image.
- **Source:** `docs/reviews/rca-host-review-2026-10-19.md` row 1.

### One-time player text describes only what ships in the same build

The Hidden Hosts help popup, shown once per campaign with its flag saved, explained a `~` symbol that
only a later phase draws and claimed a horizon host "cannot even be named" while its tooltip still
named it. A player who reads a save-flagged popup never sees a corrected one. The same text had
already been seeded into all 12 language files, and a seed row whose English changes before the paid
run is never re-queued (the translator picks rows that still equal the current English), so a late
fix would have left the old sentence in 12 languages for good.
- **Why missed:** the plan pre-announced a later phase to avoid a second popup.
- **Prevent:** text shown once (a saved flag, a one-time inquiry, a tutorial) describes only behaviour
  in the same build; a later phase adds its own key and flag. Fix English before the paid translation
  run; after seeding, re-seed the changed rows (delete them, then `--sync-ids --seed-only`). Literal
  glyphs the code also renders (`1??`, `...`) go in as `{VARIABLES}` filled from the code, so the
  translator's placeholder gate protects them.
- **Source:** `docs/reviews/rca-hidden-hosts-2026-10-19.md` finding 1.

### UIExtenderEx InsertType.Child inserts before Index, which defaults to 0: set Index to append (Telchar's Mark, 2026-10-19)
`PrefabExtensionInsertPatch.Index` is a virtual that defaults to 0, and `PrefabComponent.InsertAsChild` inserts before
the child at that index, appending only when `index >= ChildNodes.Count` (UIExtenderEx 2.13.3). A `Child` insert with no
override therefore lands FIRST and draws under every sibling. Telchar's Mark's checkbox went under the inventory's
tab-bar art while its comment and doc said "last child". A Codex prompt had noted the default once (2026-08-05).
- **Why missed:** "Child" was read as "append"; nobody opened `InsertAsChild`.
- **Prevent:** every `InsertType.Child` patch states its position: `public override int Index => int.MaxValue;` to
  append, or an explicit index with a comment naming the sibling it must precede. Pin it in the feature's wiring test.
  `PrefabInsertPositionTests.EveryInsertPatch_LandsWhereItsInsertTypeSays` fails a Child insert without its own Index,
  a Child insert whose XPath does not end in `/Children`, and an Append or Prepend on `/Children`.
- **Source:** `docs/reviews/rca-telchars-mark-2026-10-19.md` row 1.

### A "shown" flag is set by the code that wrote the text, never by the getter that formatted it

Hidden Hosts' memory popup is armed the first time a "~" count is drawn. The service method that formatted the
settlement's "~" defenders text also set the drawn flag, before the tooltip hook had looked for the defenders row;
with the Alt view, a hideout or hidden information there is no such row, so the popup and the "Counted ... ago" line
fired with no "~" on screen. The flag is now set only by the code that wrote the "~".
- **Why missed:** formatting the text and drawing it looked like one step.
- **Prevent:** a one-time UI trigger or a "seen" flag follows the write that the player can see, and a test covers
  the case where the write finds nothing to write into.
- **Source:** `docs/reviews/rca-hidden-hosts-p23-2026-10-19.md` finding 3.
### Check new player text for speaker and player gender before the paid translation run (#863, 2026-10-19)
Tokens of Esteem wrote six dialog lines that read neutral in English ("This is just what I would have chosen", "You
carry nothing I could accept", "What have you brought?") but take a gendered verb or adjective in Russian and Polish.
The translation pipeline cannot add a `{?..GENDER}` conditional, so a literal translation would fix one gender for
every speaker.
- **Why missed:** the wording was checked in English only.
- **Prevent:** before seeding new player text, reword any line whose verb or adjective agrees with the speaker or the
  player (past tense, "would have", "could", predicate adjectives) into a present-tense or verb-led form, and re-seed the
  language rows so they equal the new English.
- **Source:** `docs/reviews/rca-tokens-of-esteem-2026-10-19.md` finding 8.

### A hint on a disabled button needs an enabled parent

Gauntlet only hovers enabled widgets: `EventManager.CollectEnableWidgetsAt` returns early for a widget whose
`IsEnabled` is false and skips its disabled children, and a `HintWidget` has no hover of its own (it relays its
parent's `HoverBegin`). So a `HintWidget` inside a `ButtonWidget` with a bound `IsEnabled` shows its hint only
while the button is enabled; a "why is this greyed out" hint there can never appear. Vanilla's governor button
(SandBox `TownManagement.xml:105-111`) puts the disabled-state hint on an always-enabled wrapper around the button.
- **Why missed:** Reeve's Ledger copied an existing button's shape, and its comment claimed that shape shows a hint
  when disabled; nobody read the hover path. TAOM's own prefabs hold many vanilla clones with the inner-hint shape,
  so a repo-wide scan would be noisy.
- **Prevent:** when a hint must explain a disabled state, wrap the button in a plain, enabled widget that carries
  the hint, keep the button's inner hint for the enabled state, and pin the shape with a structural test (the hint's
  parent has no `IsEnabled` binding). Check the hover in game with the button disabled.
- **Source:** `docs/reviews/rca-reeves-ledger-2026-10-19.md` finding 1.
- **Repeat (Messengers, #898, 2026-10-23):** the Send Messenger button lost its inner hint in #944, which fixed only
  the disabled state. Its new "about N days" hint was then bound to the outer one and never showed on the enabled
  button. The structural test pinned the outer hint and asserted that no inner hint exists. So a fix for one state
  needs a test for both: a hint inside the button for the enabled state, and one on the wrapper for the disabled
  state (`docs/reviews/rca-messenger-pricing-2026-10-23.md` finding 1).
- **Repeat (Smith's Measure, #910, 2026-10-26):** the third time. The Make Tools card put its hint under a root with
  `DoNotAcceptEvents="true"`, so it never showed, and the first planned fix (hint inside the button) would have lost
  the disabled state. A rule and two lessons did not stop it, so it is now a gate: `PrefabHintParentGateTests` builds
  every in-code prefab extension and fails a hint whose parent refuses events. Source: [rca-smiths-measure-2026-10-26.md](../rca-smiths-measure-2026-10-26.md) finding 1.
### Any English edit on a seeded key edits all 12 rows by hand, and a per-feature wording test pins them (#864, 2026-10-20)
Good Deeds changed "honour" to "honor" after its keys were seeded. Harvest skips registered keys and `--sync-ids` only
appends, so no tool rewrites an existing row: the change was 13 hand edits (English plus 12 languages). The paid
translator queues a row only while it equals the English, so a row left on the old English would ship that way for good.
This extends the Hidden Hosts entry above, which covers re-seeding by deleting rows.
- **Why missed:** the seed tooling has no rewrite mode and nothing compares a seeded row with the English it came from.
- **Prevent:** treat an English edit on a seeded key as an edit of every language file. Add a wording test for the
  feature (`GoodDeedsWordingTests`): each language row equals the current English or is a real translation. Settle
  spelling variants (US or UK) before seeding.
- **Source:** `docs/reviews/rca-good-deeds-2026-10-20.md` finding 3.

### Gendered wording shipped again, in MCM labels and hints (#864, 2026-10-20)
Good Deeds' settings text said "his spouse" and "his leader". This is the same defect as the Tokens of Esteem entry above, one
build later; that lesson sits in this file and the feature's build never loaded it.
- **Why missed:** the lesson is filed by subsystem and Good Deeds touched no localization code path that points here.
- **Prevent:** words for a hero of unknown gender use "their" or restructure; run the gender check on settings labels
  and hints as well as dialog lines. Add a test that each hint quotes the compiled default.
- **Source:** `docs/reviews/rca-good-deeds-2026-10-20.md` finding 6.
- **Repeat (#874, 2026-10-24):** the King's Muster hint said "he calls" and "his hosts" for a ruler; fixed in review
  (`docs/reviews/rca-kings-muster-2026-10-24.md` finding 4).

### UIExtenderEx InsertType.Append inserts a sibling, never a child (Namegiving, 2026-10-20)

`PrefabExtensionInsertPatch` with `InsertType.Append` places the new node directly after the node the XPath
selects, as its next sibling (`PrefabComponent` :382, UIExtenderEx 2.13.3). Namegiving's Rename block targeted
`ListPanel[@Id='ActionButtonsContainer']` with `Append`, so it landed beside the Annex column in a horizontal row
and outside the action buttons' gamepad navigation scope, while its comments and doc said "into the container".
- **Why missed:** "Append into" was read as "append a child", and the prefab test asserted the XPath and the
  type, not the parent of the inserted node. The inverse trap, `Child` without an `Index`, was recorded the day
  before (Telchar's Mark).
- **Prevent:** to add a last child, select the parent's `Children` node, use `InsertType.Child` and
  `Index => int.MaxValue`. Use `Append` or `Prepend` only for a sibling, with a comment that names it. The
  feature's prefab test asserts the type, the index and the XPath. The repo-wide gate
  `PrefabInsertPositionTests.EveryInsertPatch_LandsWhereItsInsertTypeSays` catches a Child without an Index, a Child not
  on `/Children`, and an Append or Prepend on `/Children`. It would not have caught this defect (an Append on the
  container itself, no `/Children`): whether an Append beside a container was meant to go inside it is intent, and
  stays a reading check.
- **Source:** `docs/reviews/rca-namegiving-2026-10-20.md` finding 1.

### A past-tense verb with the player as its subject, again (Long Memory, #865, 2026-10-21)
Long Memory's coercion line read "The people of {VILLAGE} will long remember what you took by force." The verb "took" has the
player as its subject, and a language that inflects past tense for the subject's gender fixes one gender for every player. This is
the same defect as the Tokens of Esteem entry above (#863), two features later.
- **Why missed:** the doc claimed the line was neutral because it avoided "he" and "she", and the check stopped at pronouns.
- **Prevent:** a line whose subject is the villagers or the world ("what was taken from them by force") needs no gender at all.
  Test the verb's subject, not only the pronouns: any past tense, "would have" or predicate adjective whose subject is the player
  gets reworded before the English is seeded, and the language rows are re-seeded to equal it.
- **Source:** `docs/features/long-memory.md`; the `/deep-review` of #865 (lens 7); `LongMemoryWordingTests`.

### A number that a vanilla screen also shows uses vanilla's format expression (Bill's Burden, 2026-10-20)
The cargo tooltip printed the party's weight with `(int)Math.Round(weight)`. C#'s `Math.Round` rounds
half to even by default, while the inventory screen prints the same `TotalWeightCarried` with
`ToString("#0")` (`SPInventoryVM.cs:3973`), which rounds half away from zero, so 12.5 showed 12 in the
tooltip and 13 in the inventory.
- **Why missed:** the builder wrote its own rounding for a figure vanilla already displays.
- **Prevent:** when a TAOM row repeats a number a vanilla screen shows, find vanilla's expression in the
  decompile and use it verbatim (format string, cast, rounding). For TAOM's own maths, pass
  `MidpointRounding.AwayFromZero` explicitly; never rely on the default.
- **Source:** `docs/reviews/rca-bills-burden-2026-10-20.md` finding 2.

### A new UI wiring pin copies its sibling: GenericSprite asserted, layer named by the C# constant (Bill's Burden, 2026-10-20)
The cargo row's sprite pin merged SpritePart and GenericSprite names into one list, so a manifest with
only the SpritePart passed, while `SpriteData.GetSprite` reads only the sprite table (GenericSprite and
NineRegionSprite entries) and a brush
layer then draws nothing. Its brush test matched the layer by a string literal, so renaming the mixin's
item id would blank the icon with every test green. The sibling `FiefsNavButtonWiringTests` already
did both correctly.
- **Why missed:** the new test was written without the nearest sibling pin open.
- **Prevent:** before writing a brush or sprite pin, open the newest sibling wiring test and copy its
  shape: assert the SpritePart and the GenericSprite separately, and match the brush layer through the
  `internal const` id the C# binds (a const inlines, so the test loads no engine type).
- **Source:** `docs/reviews/rca-bills-burden-2026-10-20.md` finding 3.
### A UIExtenderEx mixin constructor never calls OnRefresh (Lines of Descent, 2026-10-23)
UIExtenderEx 2.13.3 creates a mixin at the end of the view model's constructor and replays the constructor's
own refresh call on it as `OnRefresh`. Two new mixins also called `OnRefresh()` in their constructors, so each
clan member row ran its relation search three times when the screen opened.
- **Why missed:** the plan cited a precedent for the explicit call; the rule against it existed only as a code
  comment (`Client/Features/Namegiving/UI/FiefNamingMixin.cs:46`).
- **Prevent:** do not call `OnRefresh()` from a mixin constructor; `LinesOfDescent` pins this with a source test.
  When a plan cites a precedent for a framework call, read the precedent and the framework's code, not only the
  plan.
- **Source:** [rca-lines-of-descent-2026-10-23.md](../rca-lines-of-descent-2026-10-23.md) finding 5.

### A child list under a wider parent row needs HorizontalAlignment="Center" (Lines of Descent, 2026-10-23)
The family tree's child row had no `HorizontalAlignment`, and Gauntlet defaults to Left. Vanilla's troop tree
never shows this because its parent row is a fixed 150 px, never wider than a child; the TAOM couple row is
190 px per card, so an only child under a married couple sat at the left with its connector line dropping into
empty space. This is the same default-left class as the fixed-width columns entry above.
- **Why missed:** the node template was adapted from vanilla's, whose geometry hid the default.
- **Prevent:** when a copied layout changes a size, check every container's alignment default against the new
  sizes; pin a needed `HorizontalAlignment` with a source test.
- **Source:** [rca-lines-of-descent-2026-10-23.md](../rca-lines-of-descent-2026-10-23.md) finding 4.

### Option B of a two-option inquiry is the Escape key: put the safe choice there (End of the term, 2026-10-23)
The end-of-term offer put "Sign on again" in the first button and "Leave with honor" in the second. The
engine runs the negative option (B) on Escape and the gamepad B button, whatever the button state, so a
player who pressed Escape to dismiss the popup would have been discharged (caught in review, before any commit). Enter
runs option A, the deliberate confirm.
- **Why missed:** the button order followed the reading order of the text ("sign on, or leave"), and nobody
  asked what Escape does on a modal two-option inquiry.
- **Prevent:** before wiring a `ShowTwoOptionInquiry`, decide which choice is harmless when dismissed and
  make that option B. Name the Escape option in the feature doc and pin which callback is B in a test.
- **Source:** `docs/reviews/rca-enlistment-term-end-2026-10-23.md` F1.
### A StretchToParent child of a CoverChildren panel fills the whole offered space (fief-vote count row, #866, 2026-10-23)
The fief-count row was a `CoverChildren` `ListPanel` holding a `TextWidget` and a `HintWidget` with
`StretchToParent` on both axes. A `StretchToParent` child of a `CoverChildren` stack panel is measured against the space the
parent is offered, so the row can grow to fill the space the card layout offers. A `HintWidget` only relays
its parent's hover, so it needs no size at all.
- **Why missed:** the prefab test read bindings and attribute names, never size policies.
- **Prevent:** use vanilla's shape (`Inventory.xml:471-475`): one `TextWidget` with a size-less `HintWidget` child. A test asserts
  that no inserted element is `StretchToParent` and that the `HintWidget` has no size policy.
- **Source:** `/deep-review` of #866; `DecisionOptionFiefCountPrefabTests`.

### A hover target lies inside every ancestor's rectangle: Gauntlet hit-tests by containment (Loremaster's Line, 2026-10-23)
`EventManager.CollectEnableWidgetsAt` (v1.5.5 EventManager.cs:880-901) descends into a child only when the
pointer is inside that child's own `AreaRect`, and a `HintWidget` relays only its parent's hover. A widget placed
outside its parent (a negative `PositionYOffset`, an offset past the parent's edge) still draws, because nothing
clips it, but no pointer ever reaches it. The skill-bar arrow sat 30 px above the bar, so its "Skill Limit"
tooltip could never open; vanilla's own indicator, which the markup copied, is a sibling inside the container.
- **Why missed:** the markup was copied without its place, and nobody walked the hit test. This is the second
  hover-path miss after "A hint on a disabled button needs an enabled parent" (Reeve's Ledger).
- **Prevent:** put a hint on an enabled widget that lies inside every ancestor's rectangle, give that widget
  `DoNotPassEventsToChildren` when it has enabled children (children are collected first), and pin it with a
  structural test (the hint's parent, its events flags, no offset out of the parent). Hover it in game.
- **Source:** `docs/reviews/rca-loremasters-line-2026-10-23.md` finding 1.

### Before writing a widget that sizes or places a child from numbers, look for the engine's (Loremaster's Line, 2026-10-23)
The first build placed a band and an arrow on the skill bar from a custom widget that copied the bar's private
maths every frame. The engine's `FillBarHorizontalWidget` (TaleWorlds.GauntletUI.ExtraWidgets) already sizes a
child from `InitialAmount / MaxAmount` of its parent's width, right- or left-anchored, and vanilla uses it with a
child riding the fill's edge (`DeclareWarDecisionPanel.xml:129-136`). The swap deleted a 143-line widget, its
copied maths and every per-frame TAOM path. A TAOM widget redeclaring a sprite also matters: `TAOMSpriteData.xml`
redeclares vanilla names in `ui_taom`, and that re-skin is what draws, so the art (and its direction) comes from
TAOM's sheet, not SandBox's.
- **Why missed:** the plan searched for a seam on the bar (a patch or a child widget), not for an engine widget,
  and read only SandBox's sprite sheet although `.claude/rules/gui-ui.md` says to read TAOM's first.
- **Prevent:** rung 1 of `think-before-coding.md` covers widgets too: grep the installed ExtraWidgets and
  vanilla prefabs for a widget that does the layout before writing one. Pin sprites against `TAOMSpriteData.xml`
  (SpritePart and GenericSprite).
- **Source:** `docs/reviews/rca-loremasters-line-2026-10-23.md` findings 7 and 13.

### An unknown brush name draws the default brush with no log: check names against the loaded brushes (#989, 2026-10-24)
The Messengers hero-page button named two brushes no module defines. `BrushFactory.GetBrush` returns null and
`BrushWidget` falls back to the UI's default brush, so the button drew with no art and no state looks, and nothing was
logged. Separately, a `ButtonWidget` passes its enabled, hover and disabled state to child widgets only with
`UpdateChildrenStates="true"`; without it the label keeps full brightness on a disabled button.
- **Why missed:** a wrong brush name or a missing state attribute draws something plausible, so no run fails.
- **Prevent:** `PrefabExtensionBrushTests` checks every brush-typed attribute in a PrefabExtension against the
  brushes the engine loads; a button whose label should follow its state sets `UpdateChildrenStates="true"`, as vanilla
  and the Family Tree buttons do.
- **Source:** [rca-messengers-brushes-2026-10-24.md](../rca-messengers-brushes-2026-10-24.md) findings 0 and 1.
### A CoverChildren TextWidget never wraps: give a text that may grow a fixed width (Bond of Hire, 2026-10-24)

Gauntlet's `TextLayout.MeasureChildren` treats a TextWidget as fixed-width only when its width policy is not
CoverChildren or it has a `MaxWidth`. A CoverChildren text therefore measures as one unbounded line. The Clans panel
contract line fitted in English (about 453 px of a column about 600 px wide) and a translation about 35% longer would
have run under the next column.
- **Why missed:** the #866 rule (no StretchToParent inside a CoverChildren parent) was applied, and the opposite
  question, whether this text must wrap, was not asked.
- **Prevent:** a sentence of player-facing text gets `WidthSizePolicy="Fixed"` with a `SuggestedWidth` (or a
  `MaxWidth`) and `HeightSizePolicy="CoverChildren"`, as vanilla's explanation texts do; keep CoverChildren width for
  short labels only.
- **Source:** `docs/reviews/rca-bond-of-hire-2026-10-24.md` finding 2.
### Follow a UI value to what renders it before you write it (#993, #994, 2026-10-24; repeat of #591)
Five BannerColorPersistence patches set `Color1` and `Color2` on a `CharacterCode` after
`CreateFrom` had built its `Code` string. `Code` has a private setter and is the only field the
portrait pipeline keeps and parses, so no clan colour reached a portrait since the feature shipped,
and the feature doc told the next author to do the same. In the same review, Refuge wrote a "zero
wage" line that no v1.5.5 prefab binds, and decorated clan rows at list build when every row's own
refresh reset the name a moment later.
- **Why missed:** each write looked correct against the property it set; nobody traced the value to
  the widget binding or the renderer's input, and nothing in game or in a test compared the result.
  This is the class of the nameplate redesign that lost relation colour for five months (#591).
- **Prevent:** before writing a UI value, find who reads it on the installed version (the prefab's
  `@Binding`, or the method that builds the renderer's input) and when that reader runs; list every
  place the engine builds the object (a constructor can bypass the method you patch). For a built
  value object such as `CharacterCode`, rebuild it, never mutate a field the builder already
  consumed. Pin the chain with a test, and a gate where a pattern is known bad (here a source scan for
  `.Color1 =` in the BannerColorPersistence hooks).
- **Source:** `docs/reviews/rca-banner-colors-refuge-rows-2026-10-24.md` findings 1 to 4.
### Write a co-op hint from the gate's truth table, not from a summary of it (#890, 2026-10-26)
A fix round rewrote three Seasoned Hosts MCM hints to say the settings are "off in a co-op session". The code defers only on
clients (a BannerlordCoop host applies them), and the excluded mod is one TAOM detects but cannot probe, not one it "cannot
detect". The wrong text went into the C# attribute, the strings file and 12 language seeds.
- **Why missed:** the hint was written from the brief's one-line description of `ShouldDeferToHost`, not from
  `CoopSessionPolicy.ShouldDeferToHost`'s rows.
- **Prevent:** before writing co-op player text, read the gate's rows and reuse the sibling wording in `TaomSettings.cs`
  ("Applies on the host in co-op."; "a co-op mod TAOM cannot probe").
- **Source:** `docs/reviews/rca-seasoned-hosts-2026-10-26.md` finding 5.

### A margin that ends a column belongs to the column's last block: move it when the blocks move (#907, 2026-10-24)
Moving the hero page's Family block above Allies carried vanilla's `MarginBottom="50"` with the Family grid, so the
Enemies grid, now last, touched the page end. Separately, the harvest tool writes a blank line before `</strings>` on
purpose; removing it by hand makes the next harvest produce a whitespace diff (done in #905, #906 and #907).
- **Why missed:** the layout test pinned the block order, not the end margin; the blank line looked like noise.
- **Prevent:** when a prefab change reorders sibling blocks, list each block's margins and decide which belong to a
  position (first, last) rather than to the block; pin the last block's bottom margin. Leave generator output in a
  strings file as the generator writes it.
- **Source:** [rca-encyclopedia-filters-2026-10-26.md](../rca-encyclopedia-filters-2026-10-26.md) findings 4 and 6.
### A registered key does not reach English: the inline text after `{=key}` is what English players read (#845, 2026-10-26)
Field duties composed `{=taom_enlist_duty_<id>_title}a task for the company` from the row id. The key was
registered and translated, so every gate passed, yet English players read the generic inline text for every
duty: for English, `MBTextManager.GetLocalizedText` returns the inline text and never looks the key up
(v1.5.5 `MBTextManager.cs:264-268`).
- **Why missed:** the localization gates check that a key has a row and a translation, not that the inline
  English at the call site is the row's English.
- **Prevent:** a composed key must carry the row's own English inline (read it from the data that owns it);
  a generic inline fallback is for a row that no longer exists, never the normal path. Test the English
  path by asserting the source string, not only the key.
- **Source:** [rca-enlistment-duty-text-2026-10-26.md](../rca-enlistment-duty-text-2026-10-26.md) F1, F2.

### Player text names game things as the game shows them, not by their code ids (#910, 2026-10-26)
Make Tools said "Crafting experience", "the Refinement tab" and "one iron". The game shows the skill as Smithing
(`DefaultSkills.cs:96`), the tab as Refine, and the recipe takes one grade, named Iron, of six iron grades. A player
holding Steel was told to get iron. It also added its own "too tired" line where vanilla's translated one says the same.
- **Why missed:** the strings were written from `DefaultSkills.Crafting`, `IsInRefinementMode` and `CraftingMaterials.Iron3`.
- **Prevent:** before writing a string that names a skill, tab, item or rule, find the game's own text for it (the
  `{=key}` in the engine source or the vanilla prefab) and use that name; if vanilla already has a translated line for
  the same message on the same screen, use its key and register nothing.
- **Source:** [rca-smiths-measure-2026-10-26.md](../rca-smiths-measure-2026-10-26.md) findings 3 and 4.

### A line a hostile NPC says to the player must not name the player's people (Bandit Voices, #900, 2026-10-26)
The Blacklocks dwarves called the player "tall-folk", Gundabad orcs called them "softskin", and Dunlendings
"lowlander". A TAOM player can be a dwarf or an orc, robbed by the bandits of their own people, and every such line then
reads wrong. Vanilla's robbery lines never name the listener's people.
- **Why missed:** each line was written in the speaker's voice, with a Man of the West in mind as the listener.
- **Prevent:** write a line spoken to the player in the speaker's voice, but name the player only in words that fit
  every playable race and gender ("stranger", "traveller", an insult that names no people). Add a pattern test over
  the strings file for listener names and gendered words, as `BanditVoicesDataTests` does.
- **Source:** `docs/reviews/rca-bandit-voices-2026-10-26.md` finding 1.

### Before deleting a translation row as redundant, render the vanilla row it falls back to (2026-10-26)
TAOM's language rows for 177 vanilla keys looked redundant: each only reworded vanilla's English, and the rule says a
rewording takes no TAOM row. Deleting them all would have shown blank kingdom names and gold amounts in 35 pairs, where
vanilla's own translation uses a variable the game never sets, and wrong articles or dropped prepositions in 334 more, where vanilla's
grammar functions need a gender tag or a `{@Type}` attribute that TAOM's names lack.
- **Why missed:** the triage compared English text and meaning, not what each language's vanilla row renders with
  TAOM's data.
- **Prevent:** for each (language, id) a deletion hands back to vanilla, check every variable in vanilla's row against
  what the v1.5.5 call site sets, and every grammar function or token (`{#GENDER}`, `{#TYPE}`, `{.cl}`, Italian `{.di}`/`{.a}`/`{.da}`) against the tags
  on TAOM's names. Keep TAOM's row where vanilla's would render wrong, and record the pair with its reason in
  `kept-vanilla-key-rows.txt`. Decode vanilla's language files by their BOM: the TR files declare utf-16 but are UTF-8.
- **Source:** `docs/reviews/rca-english-only-overrides-2026-10-26.md` findings 2 and 3.

### A prefab gate resolves names the way the engine does: on the runtime type, and for every element kind it reads (#1024, 2026-10-26)
Gauntlet resolves `A.B` on the runtime value of `A`, so a gate that judged `B` on `A`'s declared type had to skip
interface-typed `LayoutImp` and accepted any misspelled suffix there. The gate also skipped `VisualDefinition` elements,
whose loader reads only five attribute names, so four dead `EaseIn` attributes passed.
- **Why missed:** "not judged" and "structural" looked safe, but each is an unchecked hole, not a pass.
- **Prevent:** map a known interface property to the type the widget's constructor assigns, fail any other unknown, and
  check every element kind against the loader that reads it.
- **Source:** [rca-prefab-attribute-gate-2026-10-26.md](../rca-prefab-attribute-gate-2026-10-26.md) findings 2 and 3.

### A prefab element is live only if its consumer is: trace who reads it before you edit it (#1027, 2026-10-26)
A recommendation to ease the character selection screen's `LeftPanel` panel rested on its `VisualDefinition`, but no
widget in that prefab names it, and Gauntlet resolves a definition only from a widget in the same prefab, so the edit
changed nothing. The file is a clone of vanilla `ClanMembers.xml` whose panel widget dropped `VisualDefinition="LeftPanel"`.
The same shape sat behind the dead prefabs of #1022 and #1024.
- **Why missed:** the definition looked complete, and nothing checks that a definition is used.
- **Prevent:** before changing a definition, constant, brush or prefab, find the element or code that consumes it (a
  `VisualDefinition="X"` attribute, a `!Constant` reference, a tag, a movie load); if there is none, delete it instead.
- **Source:** [rca-panel-ease-2026-10-26.md](../rca-panel-ease-2026-10-26.md) finding 1.
### A language_data.xml with a name and no text_processor resets the language's grammar processor (#1026, 2026-10-26)
`LanguageData.Deserialize` assigns `TextProcessor` whenever `name` is non-empty, and takes it from the `text_processor`
attribute, which is null when absent. The live `TAOM_Map` and `LOTRLOME_Armory` files carried `name` and no processor, so
wherever one of them loads after Native it resets the language to the default processor, whose `ProcessToken` prints
nothing. Eight languages were affected: English (lords' parties read "Aragorn Party" for "Aragorn's Party", from the live
Armory root file), and Italian `{.di}` and `{.a}`, French `{.cl}`, Turkish `'{.e}`, German, Polish, Russian and Spanish.
TAOM's own files had only `id`, so nothing restored it. TAOM's `SubModule.xml` pins no load order against these modules, but TAOM_Map's `SubModule.xml:19` pins TAOM before TAOM_Map
(`id="TAOM" order="LoadBeforeThis"`), so the
rule is not "TAOM loads last": every module's file with a `name` must carry the processor. `TAOM.Dependencies` is the one exemption: its install-only files carry a `name` and no processor, and are safe only because its `SubModule.xml` lists Native in `ModulesToLoadAfterThis`, so it always sorts before Native.
- **Why missed:** `strings-and-localization.md` said an absent `text_processor` keeps whatever another module set, and no
  test read the attribute. The missing words only show in a running game.
- **Prevent:** give every `language_data.xml` that has a `name`, the root English file included, the `text_processor`
  Native declares for that id, and none where Native declares none. `LanguageProcessorTests` checks TAOM's 13 files, and
  `tools/check_external_loc_coverage.py` fails on a live `TAOM_Map` or `LOTRLOME_Armory` file (root or per language)
  that has a `name` and a processor different from Native's, and `LiveTaomMapAndArmoryLanguageData_InstalledNative_CarryTheSameProcessor`
  guards the same files. Players get the fix only from a release with new TAOM_Map and Armory packages. Read the engine's `Deserialize` before writing a rule about an absent
  attribute.
- **Source:** issue #1026. Known effects, not fixed here: an untagged TAOM name can take the gender of a word tagged
  earlier in the same text in IT, SP, DE, PL and RU (Spanish "en el Minas Tirith"), which is #1023's work; French
  `{.dl}`/`{.cl}` put a bare "de" or "à" before an untagged name ("de Le Poing de Fer"), also #1023; Turkish `{._}`
  lowercases with Turkish rules ("ısengard"), as the engine's processor does for vanilla names.
### Before tagging a translated name, check what the engine's grammar does with the tag in that language (#1023, 2026-10-26)
Wave 1 tagged every Italian kingdom and clan name `{.FN}` because `{.FN}` and `{.MN}` both print the plain preposition. Italian
`GENDER` maps MN to 4 and FN to 5, and 29 of the 30 vanilla selections differ between the two: `{.FN}` gave "Erebor è stata distrutta".
- **Why missed:** the check covered the tag's effect on the preposition and stopped; the agreement code reads the same tag.
- **Prevent:** before a tag goes on a name class, read the language processor and the function file for every place that reads
  the tag, and tag by the gender of the implied noun ("il regno", "il clan"), not by the one effect you were fixing.
- **Source:** `docs/features/name-grammar-tags.md` (Solution Approach), #1023 review wave 1.
### Flattening a TextObject to a string freezes every global it reads at that moment (#1019, 2026-10-27)
The enlistment board was turned into a plain string once per status change so the wait menu would not rebuild a nested text every frame. The board and its nested lines use `{NEWLINE}`, a global TAOM never sets: the engine sets it (`GameTexts.SetVariable("NEWLINE", "\n")`) lazily inside its UI builders, and ending a campaign clears all globals. Rendered every frame, the board picked `NEWLINE` up whenever the engine set it. Flattened once, a board written while it was unset kept its lines run together until the next status change.
- **Why missed:** before the flatten the board re-rendered every frame and picked `NEWLINE` up later, so the unset state had no visible effect, and the render test never started from it.
- **Prevent:** before `TextObject.ToString()` freezes a text, set every global the text reads (here `MBTextManager.SetTextVariable("NEWLINE", "\n")`), and test the frozen result with those globals removed first.
- **Source:** #1019; [rca-enlistment-board-render-and-probes-2026-10-26.md](../rca-enlistment-board-render-and-probes-2026-10-26.md).
### Copy vanilla's view model arguments and sprite size with its widget, not only the names (#902, 2026-10-27)
A banner on `Flat.Tuple.Banner.Small.Hero` was built as `new BannerImageIdentifierVM(banner)`. The brush draws its texture at
2.5 times the widget's short side, and vanilla feeds it only nine-grid banners; a plain banner texture is the centre third of
the banner, so the corner showed a slice of the emblem. The same change drew the 58x40 `General\Mission\noble` sprite in a
20x20 box, and a plain `Widget` stretches its sprite to the box.
- **Why missed:** the plan confirmed that the brush, the sprite and the constructor exist, and stopped there. A default
  argument and a sprite's size do not fail any build or test.
- **Prevent:** before binding a vanilla brush or sprite, find two vanilla sites that use it and copy what they pass to the
  view model (`nineGrid: true` for every banner `MaskedTextureWidget`) and the widget's size, or read the sprite's
  `<Width>` and `<Height>` in `NativeSpriteData.xml` and keep its aspect. Prisoner Banners pins both in its wiring tests.
- **Source:** [rca-prisoner-banners-2026-10-27.md](../rca-prisoner-banners-2026-10-27.md) findings 1 and 2.
### A clan name is saved, and English never looks its key up (Tharbad swap, 2026-10-27)
After a clan rename, a save started earlier keeps the old name in English and shows the new one in the 12 other languages.
`Clan.Name` is a `[SaveableProperty]`, `clans.xml` loads only for a new campaign (`SandBoxManager.InitializeSandboxXMLs`),
and `MBTextManager.GetLocalizedText` returns the saved inline text in English. A town name is not saved, so it changes in
every save.
- **Why missed:** the clan behaviour was inferred from the town behaviour.
- **Prevent:** before telling Mike what an old save shows, check whether the field is saveable and whether its XML loads
  for a saved game.
- **Source:** `docs/reviews/rca-tharbad-swap-2026-10-27.md` row 3.
### A rename must evict the translation cache; the bulk settlement renamer did not until #1042 (Lindon expansion, 2026-10-27)
`tools/Apply-MapVillageNames.py` rewrote all 438 of its `NAMES` in the master and in all 12 `loc_settlements.xml`, with
no dry run, no backup and no cache eviction. The translation cache keys on the string id alone, so the next translator run
wrote the old wording back: on 2026-10-27, 421 to 438 rows per language (PL: 5) showed pre-rename text, for example DE
"Dorf E2_1" where the master says Linenhold. The Tharbad RCA (row 7) called the script safe after comparing only the
master file, and this session first misread those rows as good translations.
- **Why missed:** each check measured one output file, and a row that differs from the English was taken for a translation
  without reading what it says.
- **Prevent:** after any rename, remove the renamed ids from `tools/translation_cache/<lang>.json`, and read a sample of
  every output file a writer touches before calling it safe or unsafe.
- **Source:** #1039.
- **Fixed (#1042):** the renamer now writes only the ids whose English changed and evicts them from the cache; `tools/oneoff/reset_stale_village_loc_rows.py` reset the 4,633 stale rows to the English for #763.
### List a shared key's readers before re-keying it (map-tool follow-ups, 2026-10-27)
16 villages borrowed another village's description key (`castle_village_EN3_2` read
`Settlements.Settlement.text.village_EN1_4`). The #1042 one-off gave each its own key and left the borrowed rows "for
the village that owns them", but 15 of the 16 owners either were not on the map or had no `text=`, so 180 rows in 12
languages lost their last reader and would have been paid for in the translation backlog. Four Dunland fortifications
outside the renamer's table had the same stale names as the villages and were left out because the repair took its
ids from that table.
- **Why missed:** the owner of a borrowed key was assumed, and the repair was scoped by the tool that caused most of
  the defect rather than by the defect's signature across the whole file.
- **Prevent:** before moving a settlement off a shared key, list every master row that reads the key and every row
  that defines it; delete the rows no settlement reads (and refuse while one still does). Scope a data repair by
  scanning every row for the defect, then name the extra ids in the tool.
- **Source:** `docs/reviews/rca-map-tool-followups-2026-10-27.md` findings 6 and 7; #1042.
### Pin a GUI prefab copy to the vanilla file it replaces (#1031, 2026-10-27)
TAOM's copy of `PartyTroopTuple.xml` replaces vanilla's by file name, so a widget that vanilla adds after TAOM's last re-sync
never reaches players. The Execute button lost vanilla v1.5.5's blood-feud mark this way, and kept a 1.3-era `ValueFactor`.
The v1.5.2 changeset listed the file as changed, but that bump re-based only `PartyScreen.xml` and the nameplates, and
`PrefabCloneWidgetReferenceTests` checks only reference attributes, so a missing decorative widget passed every gate.
- **Why missed:** no gate compares a whole TAOM copy with the vanilla file it hides, and the engine-bump step re-based only
  the copies someone remembered.
- **Prevent:** give every TAOM copy of a vanilla prefab a LiveInstall test that compares it with the installed file element
  for element, excluding only TAOM's documented edits (`PartyTupleVanillaParityTests` is the model). A copy with no TAOM
  edit should not exist: delete it and let vanilla load.
- **Source:** [rca-tuple-blood-feud-2026-10-27.md](../rca-tuple-blood-feud-2026-10-27.md).

### Give every concrete TacticComponent in TAOM a sergeant popup row (#1054, 2026-10-29)
`TeamAIComponent.MakeDecision` shows a sergeant player `GameTexts.FindText("str_team_ai_tactic_text", <tactic type name>)`
when the team's tactic changes (TeamAIComponent.cs:330-334), and a missing variation renders "ERROR: Text with id
str_team_ai_tactic_text doesn't exist!". Culture Doctrine added its rows, but `DoctrinePopupStringsTests` scanned only
`TaomTacticBase` subclasses, so Staged Siege's `TacticStagedObjective` (a direct `TacticComponent`) shipped without one.
- **Why missed:** the gate was scoped to one base class, narrower than the engine rule it protects.
- **Prevent:** `EveryConcreteTacticComponentInTaom_HasASergeantPopupRow` now covers every concrete `TacticComponent` in
  TAOM.dll; add the `str_team_ai_tactic_text.<TypeName>` row in `taom_module_strings.xml` with any new tactic.
- **Source:** [rca-staged-siege-2026-10-29.md](../rca-staged-siege-2026-10-29.md) finding 4.
