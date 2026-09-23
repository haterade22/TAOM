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
  `MapConversationTextureProvider.Clear` on a provider that is still ticking. TAOM's
  `Native2ManagedPatcher` finalizers would log and swallow such a throw rather than crash.
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

- [docs/INDEX.md](../../INDEX.md)

<!-- backlinks-end -->
