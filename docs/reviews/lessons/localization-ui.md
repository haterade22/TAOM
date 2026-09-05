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
   than emitting a placeholder (`generate_enlistment_duty_strings.py`). A generator that
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
