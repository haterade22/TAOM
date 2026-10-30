# Bannerlord UI — GauntletUI / ViewModel / screen + state (Phase 11)

> **One process, traced from the decompile** (v1.4.5): the UI stack — `ViewModel` (data) ↔ a Gauntlet "movie" (the
> widget tree) bound via `GauntletLayer.LoadMovie`, screens via `ScreenBase`/`GameState`/`GameStateScreen`, and how
> TAOM extends it (subclassing + UIExtenderEx mixins/prefab-extensions). Covers TAOM's career screen, messengers,
> quick actions. Part of the phased engine study; consolidates the many UI gotcha-memories.

## WHAT it is

Bannerlord UI is **MVVM over Gauntlet**: a **`ViewModel`** holds the data (`[DataSourceProperty]` values + `Execute…`
commands); a **movie** (a `.xml` widget tree in `GUI/Prefabs/`) is the view; **`GauntletLayer.LoadMovie(movieName,
vm)`** binds them. A full custom window is a **`GameState`** + a **`GameStateScreen`** (which renders it via Gauntlet
layers). Mods either build their own VM+movie+state, or **extend vanilla UI** with **UIExtenderEx** (mixins +
prefab-extensions). TAOM does both.

## HOW it works

### `ViewModel` (Library/ViewModel.cs:9 — `abstract : IViewModel, INotifyPropertyChanged`)
The data source. You expose **`[DataSourceProperty]`** properties (the widget tree binds to them *by name*) and
**`public void ExecuteFoo()`** methods (bound to button clicks by name). On change you call
**`OnPropertyChanged(nameof(X))`** (or a typed `OnPropertyChangedWithValue(value, name)`) to notify the bound widget;
**`RefreshValues()`** rebuilds all. Bindings are reflection-cached per VM type (`_cachedViewModelProperties`).
TAOM VMs (career, messengers, quick actions) subclass `ViewModel`.

### `GauntletLayer.LoadMovie(string movieName, ViewModel dataSource)` (Engine.GauntletUI/GauntletLayer.cs:119)
Instantiates the named movie (a `.xml` UI prefab under `GUI/Prefabs/`) bound to the VM. The widget tree reads the
VM's `[DataSourceProperty]` values by name and routes `Command.Click="ExecuteFoo"` to the VM method. A `GauntletLayer`
is a `ScreenLayer` added to a `ScreenBase`/`MissionView`.

### Screens + states: `ScreenBase` / `GameState` / `GameStateScreen` / `IGameStateListener`
- **`ScreenBase`** (ScreenSystem/ScreenBase.cs:7) — a screen; holds `ScreenLayer`s (incl. `GauntletLayer`s).
- **`GameState`** (Core/GameState.cs:7 — `: MBObjectBase`) — a game state (e.g. the career screen). **Push it via
  `GameStateManager.CreateState<T>()` + `PushState(state)`** — NOT `new T()` (the manager's `CreateState` runs
  `HandleCreateState` → wires the state to its `GameStateScreen` via `GameStateScreenManager.OnCreateState`; skipping
  it = no screen, silent no-op — `feedback_gamestate_creation_pattern`).
- **`GameStateScreen`** (the screen for a state) — **MUST implement `IGameStateListener`** (IGameStateListener.cs:3 —
  `OnActivate`/`OnDeactivate`/`OnInitialize`/`OnFinalize`). The `GameStateManager` calls these on activation; **a
  GameStateScreen that doesn't implement it crashes on open** (`feedback_gamestate_listener`).
- **Overlay on an existing screen** — add a `GauntletLayer` to the current screen, then call
  `_layer.InputRestrictions.SetInputRestrictions()` (pair `ResetInputRestrictions` on teardown). **Without it the
  widgets render but receive no mouse input** — a silent dead button (`feedback_gauntlet_overlay_input_wiring`; the
  EquipPresets button shipped broken this way).

### UIExtenderEx (3rd-party, vendored) — extending vanilla UI
- **Mixins** (`[ViewModelMixin]`) — attach new `[DataSourceProperty]` + behavior to an *existing* vanilla VM (e.g. add
  a property/button to `SPInventoryVM`). TAOM: QuickActions, career.
- **Prefab extensions** (`[PrefabExtension…]`) — inject widgets into an *existing* vanilla `.xml` prefab at an XPath
  (e.g. Messengers injects into `EncyclopediaHeroPage`). **Decompile the vanilla target prefab first** to verify the
  XPath/child structure (AGENTS.md "Verify before reference").

## WHY it's shaped this way

MVVM lets UI artists author the widget-tree `.xml` independently of the data; the VM is just named properties +
commands. The state/screen stack manages window lifecycle. UIExtenderEx lets mods add to vanilla UI without
replacing whole screens (forward-compatible across patches, as long as the bound names/XPaths hold).

## TAOM relevance + gotchas (the UI memory cluster)
- **Career screen** = a `GameState` + `GameStateScreen : IGameStateListener` (CLAUDE.md CareerSystem); pushed via
  `CreateState`+`PushState`.
- **Messengers** = a UIExtenderEx prefab-extension on `EncyclopediaHeroPage`; **QuickActions** = a mixin on the
  inventory VM. Both vanilla-UI extensions.
- **Mutating a vanilla VM property:** use the **public setter**, never reflected field-set + a reflected
  `OnPropertyChangedWithValue` (the generic-method lookup returns null) — `feedback_prefer_public_setter_over_reflected_notify`.
  And **decompile the setter body before mutating** post-construction (`feedback_taleworlds_vm_setter_decompile`).
- **Player-facing strings in a VM:** `{=key}Text` must go through `new TextObject("{=key}Text").ToString()` — a raw
  string shows the literal `{=key}` (`feedback_localization_textobject`).
- **Custom sprites need a baked atlas** — a loose PNG + manifest entry renders BLANK; the sprite-sheet must be
  regenerated (baked `*_tex.tpac`) — `feedback_sprite_atlas_baked_regen_required`/`feedback_sprite_dimensions` (resize
  before bake). Baked ≠ visible until the render check.
- **Overlay input wiring** + **GameState creation** + **IGameStateListener** — the three crash/no-op gotchas above.
- **Every return to the map leaks GPU memory** [yotthani; not confirmed in focused play]: the engine rebuilds the
  map view's render targets each time a menu closes and 3 colour targets plus 1 depth target of the old set stay
  alive, about 94 MB at a 3413x1440 render resolution and about 40 MB at 1080p.
  `MapScreen.ClearGPUMemory()`, called by `MissionCampaignView` at mission start, should free them (read from
  code, not measured), so the leak likely builds between missions. The engine's own GPU counters do not show it; measure with the Windows `GPU Process Memory`
  counters (Task Manager's dedicated GPU memory). Separately, `ViewSubModule.BannerTexturedMaterialCache` gains
  one entry per character preview, because its key's material is a fresh mesh copy that never matches (clearing
  it freed about 100 MB in one run). (MithrilForge `docs/engine/perf.md`, menu-memory sections: the banner-cache
  run of 2026-10-05 on v1.5.3, the render-target runs of 2026-10-06 on v1.5.4; autopilot runs.)
- **Generated UI prefabs are off with UIExtenderEx loaded; the cost shows per list item** [yotthani]. yotthani read
  `UIConfig.GetIsUsingGeneratedPrefabs()` as False, every movie a `GauntletMovie` (the XML path), and
  `Bannerlord.UIExtenderEx` loaded from TAOM.Dependencies (MithrilForge `docs/engine/perf.md`@0a2f4761 lines
  1612-1613, menu run of 2026-10-05 11:45, in game; v1.5.3 by the date, since the game moved to 1.5.4 at 16:20 that
  day, line 1736). That UIExtenderEx is why the prefabs are
  off, or that the cost is inherent to the XML path, is not in the source; read it as an inference. The figures, in TAOM's translation:
  loading the trade screen's movies took 400 ms (about 1 ms per item, `Inventory` loaded twice; line 1611, 201 goods),
  and about 3000 entries cost 4.4 ms per entry (line 1614, which calls normal lists uncritical). The 3000-entry case was the inventory with
  `cheat_mode = 1`, which fills the left side with every `ItemObject` and took 13 s to open (lines 1590-1593), not a
  normal play state.
- **First-use shader compiles can block one frame for 150 to 290 ms.** One observation, not a rule: yotthani's
  1.5.4 siege session of 2026-10-08 had a long load and single frames of 150 to 290 ms, which yotthani traced to
  shaders the engine builds at first use inside the frame ("Missing shader from sack"), after graphics settings had
  been changed the same day; the run after had 12 compiles instead of about 2,500 (`perf.md` lines 2107-2109). Whether
  a cold cache compiles mid-battle and blocks the frame is still open in
  `plans/_audit/2026-10-02-perf/REPORT.md` lines 121-122, which this note leaves as it is.
- **A second enabled scene view on the campaign map scene loses the map's terrain** [yotthani,
  in game, 1.5.3]: the runs, yotthani's `ClearAll` rule and what they mean for TAOM's `MapViewRelease` are in
  [map-view-release.md](../../features/map-view-release.md).
- **`Texture.CreateFromByteArray` pins the pixels only for the call** (TAOM-verified, v1.5.5 managed).
  `ManagedCallbacks.ScriptingInterfaceOfITexture.cs:260-266` builds the pinned array, makes the native call and
  disposes the pin right after (`Texture.cs:125-128` is the public wrapper). Yotthani saw panels and map layers
  show other pictures and read it as the engine uploading the pixels later, when the memory held something else
  (`RealmUI/Game/Theme/PictureReader.cs`@2ff002be lines 29-32; in game, from screenshots the code comment cites; the native
  side is unverified, so a late upload is a reading, not a finding). A grep of `Main` and `TAOM.Tests`
  (2026-10-16) finds no `CreateFromByteArray` call in TAOM.
- **Texture and first-draw costs** [yotthani; the engine version is not stated for the first two figures]:
  making a texture from pixels stopped a frame for 35 to 120 ms (`RealmUI/Game/Flow/FlowController.cs`@2ff002be lines 162-164,
  "(measured)", no machine stated). The first draw of RealmUI's own interface cost one frame of 1.5 s (measured 2026-10-08), which
  yotthani pays up front by drawing it at alpha 0.02 for 14 frames while the picture stands (`RealmUI/Game/Map/MapCam.cs`@2ff002be
  lines 457-466). A per-frame budget that a single item exceeds is no budget: a pump of "a few milliseconds a frame" read one picture
  every frame and halved the frame rate for seconds (101 to 50, reported to yotthani on 2026-10-13); yotthani's code now reads one
  picture every 0.3 s (`FlowController.cs` lines 195-203; no measurement of the change is given). Loading four 2048 px layers through `LoadTextureFromPath` took 129 ms on vanilla modules and
  153 ms with TAOM's list (`feasibility.md`@2ff002be section 11, 2026-10-04, 1.5.3, one run each).
- **Gauntlet 2D draw facts, read in the 1.5.3 decompile and not run** [yotthani,
  `docs/realm-ui/research/engine-2d-gauntlet.md`@2ff002be lines 9-51]: culling and hit-testing use the widget's
  layout rectangle only (`Widget.cs:2231-2256`, `:2158`), so a visual offset or scale is ignored; draw calls
  batch only for consecutive draws with the same texture pair (`TwoDimensionEnginePlatform.cs:39-57`); and
  pooled `SimpleMaterial`s keep the previous user's values, so set every field you rely on
  (`SimpleMaterial.cs:8-50`). TAOM has not re-read these on v1.5.5.
- **A bool, int or float setter raises only its typed event** [yotthani, decompile reading of 1.5.5; TAOM-verified on
  v1.5.6]. yotthani: "bool/int/float properties raise ONLY their typed PropertyChangedWith*Value event, never PropertyChanged
  (ViewModel.cs:287-330 ...)", and the source corrects it: "some setters still use plain OnPropertyChanged
  (KingdomCategoryVM.Show and .NotificationCount), others the object overload (CharacterDeveloperVM.CurrentCharacter,
  HeroItem.CurrentSkill) ... (audit table: 5 of 39 were wrong)" (`yotthani/bannerlord`
  `docs/realm-ui/research/bigwindows_vm_map.md`@6265f4a2:141, "check each subscribed property's setter"). TAOM read v1.5.6
  `TaleWorlds.Library.ViewModel.cs` [Certain]: a separate event per type, `PropertyChanged` :68, `WithValue` :87, `Bool`
  :106, `Int` :125, `Float` :144, `UInt` :163, `Color` :182, `Double` :201, `Vec2` :220. `OnPropertyChanged` (:274-285)
  invokes only the plain handlers, `OnPropertyChangedWithValue<T>` (:287-298) only the `WithValue` list, the bool overload
  (:300-311) only the bool list, and `SetField` (:263-272) raises the plain event. Every TAOM subscription to a vanilla view
  model, by grep of `Main/` and `Client/`, picks the right event [Certain]: `FiefNamingMixin.cs`:43 and `MercenaryTermMixin.cs`:45
  listen on `PropertyChangedWithValue` for object-typed `CurrentSelectedSettlement` and `CurrentSelectedClan`, whose v1.5.6
  setters call `OnPropertyChangedWithValue` (`KingdomSettlementVM.cs`:65-77 and the `KingdomClanVM` setter); the third,
  `AiPartySizeSettingsWatcher.cs`:70, is a plain `PropertyChanged` on MCM settings, not a vanilla view model. TAOM has no
  subscription to a bool, int or float vanilla property. A mixin that adds one must read that property's setter first.
- **A `DataSource` on the prefab root is ignored** [yotthani, on the party window per the commit message, which names neither a game build nor a test; no code line cited]. The commit c656a782 (2026-10-23): "Gauntlet binds the movie root to the movie VM, so the root's DataSource was
  ignored", and `{..}` on the child "climbed above it to nothing". TAOM has not checked its own movie roots (Career,
  SupplyOrder, the FactionUI movies, the PartyScreen clones) for a root `DataSource`; bind through the view model passed to
  `LoadMovie`.
- **Text shrinks when it runs out of lines, and the line box is taller than the font** [yotthani, screenshots plus code
  comments, one source, version not stated at those lines, not re-read by TAOM]. The test comment
  (`RealmUI/Game/Inventory.Tests/KingdomShapeTests.cs`@6265f4a2:209-210) reports that "the text ran out of its lines and the
  engine shrank it (first card tiny, the next larger)" because the line count "missed the line breaks". The engine "centres a
  line by its line box - the font's baseline plus 5 units (TwoDimension.Text: LineHeight = (Base + 5) x scale)", while the
  ink fills only the lower part of that box, so a face sat some 4 px low at 14 px for Grenze (`RealmUI/Game/Inventory/Core/TextOptics.cs`:6-9).
  The width of a word is "(advance + 0.5 extra padding) x size / face size" (`Inventory.Tests/ThemeData.cs`:135). The workaround is a
  line a little taller than the font's, "so the engine never shrinks a text to fit" (`Inventory/Core/KingdomShape.cs`:367).
- **The tooltip registry is a dictionary set: the last registration for a type wins** [yotthani, code reading of 1.5.5].
  `InformationManager.RegisterTooltip<TRegistered, TVM>(refresh, "Movie")`; "NavalDLC already re-registers Settlement this
  way" (`docs/realm-ui/research/tooltips.md`@6265f4a2:5-6). Campaign types are registered in
  `SandBoxViewSubModule.OnSubModuleLoad` (:395-415), the generic ones (`List<TooltipProperty>`, `string`, `RundownLineVM`) in
  `GauntletUISubModule.OnBeforeInitialModuleScreenSetAsRoot` (:186-195), and the refreshers
  `TooltipRefresherCollection.Refresh*` are public static, so a new movie can keep vanilla's rows. TAOM's reading: TAOM adds
  rows by a Harmony postfix on those refreshers ([harmony-patch-registry.md](../harmony-patch-registry.md)
  `TooltipRefresherCollection_Subject_HiddenHosts_Patch`), which composes with a movie re-registration that keeps vanilla's
  refresher; a mod that registers its own refresher would drop TAOM's rows.
- **Vanilla sprite categories load per screen** [yotthani, code reading of 1.5.5, `bigwindows_vm_map.md`@6265f4a2:140]. The source names the categories
  (`ui_partyscreen`, `ui_quest`, `ui_clan`, `ui_kingdom`, `ui_armymanagement`) and says they "are loaded by the screens before
  LoadMovie and unloaded in OnFinalize; vanilla sprites used by Realm prefabs resolve only while those are loaded".
  TAOM's reading, not tested: a TAOM prefab that names a vanilla sprite from a category its screen has not loaded renders
  blank with no log line. TAOM's own sheets declare `<AlwaysLoad />` (`Main/_Module/GUI/TAOMSpriteData.xml`:6, 14), which is
  probably why this has not shown.
- **Inventory "apply changes" skips `DoneLogicExtrasDelegate`** [yotthani's code comment, `RealmUI/Game/Inventory/InventorySwap.cs`@6265f4a2:17;
  TAOM-verified on v1.5.6]. The delegate runs in one place, `Helpers.InventoryScreenHelper.cs`:167, when `DoneLogic()` succeeds
  on the Done path. `GauntletInventoryScreen.cs`:360-364 implements `IChangeableScreen.ApplyChanges` as
  `ItemPreview.Close(); InventoryLogic.DoneLogic();` with no delegate, and `MapNavigationHelper.ApplyCurrentChanges`
  (:75-88) calls it when the map bar switches screens, so that switch settles the trade and never runs the delegate. v1.5.6 `InventoryNavigationElement.cs`:20-29 locks navigation for any inventory mode but Default, which `MapNavigationHelper.cs`:61-67 honours, so a screen opened in `InventoryMode.Trade` cannot take that exit [Certain]. TAOM's provender sale hangs its town debit on the delegate (`ProvenderSaleAdapter.cs`:124) and relies on that lock ([provender.md](../../features/provender.md) "Remote selling"). Not checked: other exits that skip the delegate, such as a forced
  state clean at game end. Any new TAOM commit hung on that delegate needs the same Trade lock.
- **A 1,416-row party list held some 140,000 widgets (the author's estimate, about 100 a row) and one frame took about 100 s** [yotthani, measured in one probe run
  with TAOM's troop list, version not stated in the commit]. With `cheat_mode = 1` in `engine_config.txt` the party screen opens
  the cheat variant that lists every troop (1,416 rows with TAOM). Each row of that window was "a full kit row (~100 widgets,
  19 texts)", so the window held "some 140,000 widgets" and "the log shows its second frame 100 s after the first"; the probe
  had stalled for 11 minutes (commit c0ffdefa, 2026-10-29). The fix falls back to the game's own window above 400 rows. TAOM's
  reading: vanilla rows are far lighter, and TAOM's party clone is not virtualised, so the cheat-mode party screen is a
  dev-only slow path, like the inventory case above; the useful number is about 100 widgets a row times a row count in the
  thousands.
- **Commands and properties bind by reflection on the owning view model** [yotthani, code reading of 1.5.5,
  `bigwindows_vm_map.md`@6265f4a2:14-15]. `ViewModel.ExecuteCommand` finds methods with `Instance|Public|NonPublic`
  (`ViewModel.cs:497`), so a private `Execute*` binds, but only on the object that owns it. Properties are collected with
  `Dictionary.Add` by name (`:621-624`), so a subclass that re-declares a property name with `new` throws.

### Map conversation + encyclopedia: two layers on one `MapScreen` (read 2026-09-22, #635)
A conversation started from a settlement menu's character overlay, or from a map encounter, is a **map
conversation**: `GauntletMapConversationView` adds a `"MapConversation"` `GauntletLayer` (z 205) to
`MapScreen`, and a `MapConversationTableauWidget` drives `MapConversationTextureProvider`, which ticks
`MapConversationTableau` every frame. Clicking the speaker's name runs
`MissionConversationVM.ExecuteConversedHeroLink` → `EncyclopediaManager.GoToLink` →
`GauntletMapEncyclopediaView.ExecuteLink`. The encyclopedia opens as **another layer on the same
`MapScreen`** (`"EncyclopediaBar"`, z 310). It is not a pushed screen: no `PushScreen` in `rgl_log`, no
TAOM `[MemStation] enter` line, and the active `GameState` does not change.

What that means when you debug this path:
- **Opening or closing the encyclopedia never disables, finalizes or respawns the conversation's
  tableau.** `MapView.OnActivate`/`OnDeactivate` fire only when `MapScreen` itself (de)activates.
- **The map state idles while the encyclopedia is open.** The encyclopedia registers
  `GameStateManager.RegisterActiveStateDisableRequest`, so `GameStateManager.OnTick` calls
  `MapState.OnIdleTick` instead of `OnTick`, and `MapScreen` ticks its views with `OnIdleTick` rather than
  `OnMenuModeTick`. `GauntletMapConversationView` runs its `Tick` from all three (frame, idle,
  menu-mode), so it keeps going, and it re-takes layer focus whenever `MapScreen` is top and its layer
  is not focused (`TrySetFocus(Layer)`). The encyclopedia never re-asserts its own focus.
- **The tableau ticks only while its widget renders.** `MapConversationTableau.OnTick` is driven by
  `TextureWidget.OnUpdate`, which ticks the provider only if the widget rendered and was
  `IsRecursivelyVisible()` the previous frame. Not yet established: whether the encyclopedia page
  stops that widget rendering. If it does, the tableau pauses and catches up on the first frame
  after the page closes. That is when an ended voice clip's lip-sync teardown would run.
- **The two tableaux share nothing but the `ThumbnailCacheManager` bookkeeper.** The conversation uses
  the cached `scn_conversation_tableau` scene; the encyclopedia hero page's `CharacterTableau` uses
  `inventory_character_scene`, or builds its own copy if that one is checked out. Each has its own
  camera, texture and `AgentVisuals`. Closing the page calls `CharacterTableauTextureProvider.Clear` →
  `CharacterTableau.OnFinalize` on the page's tableau only.
- **Voiced lines lip-sync the speaker.** `MapConversationTableau.PlayConversationSoundEvent` plays the
  `.ogg` and calls `_agentVisuals[0].StartRhubarbRecord(xml, soundId)`. When the clip ends, `OnTick`
  calls `RemovePreviousAgentsSoundEvent` → `StartRhubarbRecord("", -1)`. `OnConversationPlay` also sets
  actions and `SetFacialAnimation` on `_agentVisuals[0]`. Mission conversations reach the same native
  call through `CampaignMissionComponent`. TAOM characters do get voiced lines; see
  [kingdom-voices.md](../../features/kingdom-voices.md) "Dialogue voice-over".
- **Latent engine NRE:** `MapConversationTableau.OnTick` checks `_agentVisuals != null && Count > 0`
  before `TickVisuals()`, then reads `_agentVisuals[0].GetEquipment()` on the next statement with no
  check. `OnFinalize` sets `_agentVisuals = null` without clearing `_isEnabled`. The widget's `Data`
  setter flips `IsEnabled` in the same call on every normal path, so it needs a
  `MapConversationTextureProvider.Clear` on a provider that is still ticking. That tick runs inside
  `ScreenManager.Tick` (`GauntletLayer.Tick` → `UIContext.Update` → `EventManager.Update` →
  `Widget.Update` → `TextureWidget.OnUpdate` → `MapConversationTextureProvider.Tick` →
  `MapConversationTableau.OnTick`, v1.5.3), so TAOM's Patch37 finalizer on `ScreenManager.Tick`
  would log and swallow such a throw rather than crash.
- **TAOM patches on this path:** `MapConversationTableau.SpawnOpponentLeader` /
  `SpawnOpponentBodyguardCharacter` postfixes (BannerColorPersistence, manual) call
  `AgentVisuals.Refresh` a second time to push clan colours. The hero page carries the Messengers
  prefab extension plus mixin, and the `CharacterTableau` HeroRace patches (`Patch1`, `Patch2`, `Patch3`, `Patch67`,
  `Patch72`). Nothing TAOM patches `MapConversationTableau.OnTick`/`OnFinalize`, the texture providers,
  `GauntletMapEncyclopediaView` or `EncyclopediaManager`.

## The native boundary
The **Gauntlet renderer + widget tree** are native-backed (the GauntletUI engine renders the movie + dispatches
input); `ViewModel`, the screen/state classes, and your `Execute…`/binding logic are **managed**. The `.xml` movie +
the sprite atlas (tpac) are assets the native UI loads. So UI *layout + rendering* is native/asset; UI *data + logic*
is managed (yours).

## Evidence (file:line, v1.4.5)
- `TaleWorlds.Library/ViewModel.cs`:9 (`abstract : IViewModel, INotifyPropertyChanged`; cached `[DataSourceProperty]` bindings + `PropertyChanged`/typed `…WithValue` events).
- `TaleWorlds.Engine.GauntletUI/GauntletLayer.cs`:13 (`: ScreenLayer`), 119 (`LoadMovie(movieName, ViewModel)`).
- `TaleWorlds.ScreenSystem/ScreenBase.cs`:7; `TaleWorlds.Core/GameState.cs`:7 (`: MBObjectBase`); `IGameStateListener.cs`:3-12 (`OnActivate`/`OnDeactivate`/`OnInitialize`/`OnFinalize`).
- Map conversation + encyclopedia (v1.4.8 `_modules_build_v1.4.8`): `SandBox__SandBox.View.cs` `MapConversationTableau.OnFinalize` :12131-12162, `OnTick` :12164-12208 (unguarded read :12198), `OnConversationPlay` :12585, `RemovePreviousAgentsSoundEvent` :12648, `PlayConversationSoundEvent` :12657-12664; `SandBox__SandBox.GauntletUI.cs` `"MapConversation"` layer :8595, `GauntletMapConversationView.Tick` :8512-8570 (called from `OnFrameTick`/`OnIdleTick`/`OnMenuModeTick` :8679-8695), `EncyclopediaData` layer + `RegisterActiveStateDisableRequest` :10894-10906, `MapConversationTextureProvider` :3346-3420 (`Clear` → `OnFinalize`, `Tick` → `OnTick`), `MapScreen` `OnMenuModeTick` :14239 / `OnIdleTick` :14736; category dump `Core/.../GameStateManager.cs` :186-198 (idle vs tick), `UI/.../TextureWidget.cs` :163-176 (provider ticks only after a visible render); `SandBox__SandBox.cs` `CampaignMissionComponent` lip-sync :16802/:16829. v1.5.3 (`taom-src SandBox.View.Map.MapConversationTableau`): `OnTick` :279 (unguarded read :313), `RemovePreviousAgentsSoundEvent` :763, `PlayConversationSoundEvent` :771.
- TAOM gotcha memories: `feedback_gamestate_listener`, `feedback_gamestate_creation_pattern`, `feedback_gauntlet_overlay_input_wiring`, `feedback_prefer_public_setter_over_reflected_notify`, `feedback_taleworlds_vm_setter_decompile`, `feedback_localization_textobject`, `feedback_sprite_atlas_baked_regen_required`. Feature doc: `docs/features/gui-sprite-system.md`.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/gui-sprite-system.md](../../features/gui-sprite-system.md)
- [docs/features/hero-race.md](../../features/hero-race.md)
- [docs/INDEX.md](../../INDEX.md)

<!-- backlinks-end -->
