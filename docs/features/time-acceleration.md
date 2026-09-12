# Time Acceleration

## Overview

Configurable campaign map time acceleration with three speed tiers and a visible Extra Fast-Forward button on the MapBar. Native replacement for the BetterTime mod (Nexus #2849).

All three tiers are **rebindable in the game's own Options > Keybindings > Campaign Map** screen. They default to Space, E, and Ctrl+Space, which is what the feature originally hardcoded.

## Why This Exists

- **Vanilla behavior:** Bannerlord's fast-forward (Space/Ctrl+Space) uses fixed speed multipliers with no configurability and only two tiers
- **TAOM requirement:** Players need faster map traversal across the larger Middle-earth map; configurable multipliers let players tune speed to their hardware
- **Without this feature:** Players must install BetterTime as a separate dependency, which may conflict with other TAOM systems and lacks MCM integration

## Architecture

### Design Challenge

Time acceleration must run every frame via `OnApplicationTick` (not campaign tick). The input system (`MapScreen.Instance.Input`) and campaign state (`Campaign.Current`) are sealed TaleWorlds types that must be wrapped. The Extra Fast-Forward button requires UIExtenderEx prefab patches and a ViewModel mixin to inject into the vanilla MapBar UI.

### Solution Approach

- **MBSubModuleBase.OnApplicationTick** — per-frame input polling (not a Harmony patch or CampaignBehavior)
- **UIExtenderEx prefab patches** — shift existing time buttons and insert a new Extra Fast-Forward button
- **UIExtenderEx ViewModel mixin**: hooks `MapTimeControlVM.Tick()` (per frame) for the button's lit state, and exposes the two button commands
- **MCM settings** — three integer sliders for multiplier configuration

### Component Diagram

```
TaomSettings (MCM)
        |
  SettingsProvider (reads MCM values)
        |
  TimeAccelerationService (OnTick logic)
       / \
      /   \
IMapInputAdapter   ITimeControlAdapter
(MapScreen.Input)  (Campaign.Current speed/mode)

UIExtenderEx:
  TimeAccelerationMixin → MapTimeControlVM.Tick() (lit state)
                       → ExecuteExtraFastForward / ExecuteFastForward → TimeAccelerationService
  TimeAccelerationPrefab → MapBar XML (5 patch classes; both fast-forward buttons bound to the mixin)
```

## Configuration

### MCM Settings (TaomSettings.cs)

MCM tunes the **multipliers only**. The keys themselves are not MCM settings, because MCM v5 has no
keybind widget at all (its whole vocabulary is Bool/Int/Float/String/Dropdown/Button, and the string
`InputKey` does not occur anywhere in `MCMv5.dll`).

| Setting | Range | Default | Description |
|---------|-------|---------|-------------|
| Fast Forward Multiplier | 1-128 | 4 | Speed on the fast-forward key (Space by default) |
| Extra Fast Forward Multiplier | 1-128 | 8 | Speed on the extra fast-forward key (E by default) and the map bar Extra Fast Forward button. Floored at the Fast Forward Multiplier by the provider |
| Turbo Multiplier (hold Ctrl) | 1-128 | 16 | Speed while holding Ctrl plus the turbo key (Space by default) |

### Keybindings (Options > Keybindings > Campaign Map)

`TaomTimeControlHotKeyCategory` registers three native `GameKey`s, so they are rebound in the game's
own controls screen and sit next to the vanilla keys they used to collide with.

| GameKey id | StringId | Default | Notes |
|-----------|----------|---------|-------|
| 500 | `TaomFastForward` | Space | Shares Space with vanilla `MapTimeTogglePause` (id 63) on purpose: on that shared key TAOM sets only the multiplier and lets vanilla own the mode. Rebound away from it, TAOM takes the mode over (see below) |
| 501 | `TaomExtraFastForward` | E | E is also vanilla `MapRotateRight` (id 59). This collision is why the feature became rebindable; rebind either side to separate them |
| 502 | `TaomTurbo` | Space | Only fires while Ctrl is held, which is how it stays distinct from fast-forward on the same default key |

Three engine constraints govern those numbers, and all three are pinned by
`TaomTimeControlHotKeyCategoryTests`:

- **Ids must be at or above `GameKeyDefinition.TotalGameKeyCount` (116).** `KeyOptionVM` builds each
  label's localization id from `((GameKeyDefinition)Id).ToString()`, not from `StringId`. Below 116
  the cast yields a vanilla key's *name* and reuses vanilla's string; at or above it, the bare number
  is used. Hence `str_key_name.TaomTimeControlHotKeyCategory_500` in `global_strings.xml`.
- **The slot count must exceed the largest id.** `RegisterGameKey` is an indexed write into a list
  pre-filled with `gameKeysCount` nulls, so too small a count throws at construction. The Options
  screen null-guards the unused slots, so a sparse range is safe.
- **`MainCategoryId` must be `GameKeyMainCategories.CampaignMapCategory`.** `GameKeyOptionCategoryVM`
  renders only keys whose category is on the fixed allowlist from
  `OptionsProvider.GetGameKeyCategoriesList`. CampaignMapCategory is already on it, which is why none
  of this needs a Harmony patch.

A fourth constraint is about WHERE the strings live, and it caught this feature twice during review.
The key names must be in **`Main/_Module/ModuleData/global_strings.xml`**, under that exact filename
at the ModuleData root. Nothing else works:

- The Options keybinding screen resolves labels through `Module.CurrentModule.GlobalTextManager`
  (`GameKeyOptionVM.RefreshValues`).
- `GlobalTextManager.LoadDefaultTexts()` fills that manager by walking every installed module and
  opening the literal path `ModuleData/global_strings.xml`. It never consults `SubModule.xml`.
- A `<XmlNode id="GameText">` declaration feeds a **different** manager, the per-`Game`
  `GameTextManager` built at `Game.Initialize`. Removing its `<IncludedGameTypes>` does not promote
  it to the global one, which was the first, wrong fix attempted here.
- Wrong placement is not silent-but-harmless: the row renders as
  `ERROR: Text with id str_key_name doesn't exist!`.

NavalDLC looks like a counter-example and is not. Its own `module_strings` node is ungated, but its
Options labels actually come from Native's `global_strings.xml`, which independently ships all six
Naval key records. ButterLib takes the third route, calling
`Module.CurrentModule.GlobalTextManager.AddGameText(...)` at runtime.

The `{=key}` indirection still works normally in `global_strings.xml` (vanilla's own uses it ~1,037
times), so the 12 `std_taom_keybind_strings_*.xml` translations resolve as usual.
`LanguageDataXmlTests.KeybindNames_LiveInGlobalStringsXml_TheOnlyFileTheOptionsScreenReads` pins it.

### Fast-forward and the time mode

`Campaign.TickMapTime` applies `SpeedUpMultiplier` **only** in the three fast-forward modes; in
`StoppablePlay`, `UnstoppablePlay`, `Stop` and `FastForwardStop` it is ignored outright. Fast-forward
therefore has to be in a fast-forward mode before the multiplier means anything.

On the shipped default it already is, because the key IS vanilla's `MapTimeTogglePause` and vanilla's
handler performs the mode transition for that same press. That coupling was invisible until the key
became rebindable, at which point a rebound fast-forward key set the multiplier onto a Play or Stop
mode and did nothing at all.

`IMapInputAdapter.FastForwardOwnsTimeMode` resolves it: it is true only when TAOM's fast-forward key
differs from vanilla's time-toggle key, and only then does the service call `SetTimeSpeed(2)` itself.
Claiming the mode unconditionally would be wrong in the other direction, since on the shared default
every press would force fast-forward and the vanilla toggle would never toggle back.

### The map bar buttons (#574)

Until 2026-09-12 the Extra Fast Forward button bound `Command.Click="ExecuteTimeControlChange"` with
parameter 2, which is vanilla's own fast-forward handler. That handler sets the time MODE and never
touches `Campaign.SpeedUpMultiplier`, the only thing `TickMapTime` scales by, so the button was
vanilla fast-forward under a different tooltip whatever the slider said, and it returned early when
the game was already fast-forwarding. Only the E key wrote the multiplier. #168 had recorded this in
May as "Option A, tracked as a future enhancement"; it came back as a player report.

Both map bar fast-forward buttons now go through the service, so a click and a key press share one
path:

| Button | `Command.Click` | What it does |
|---|---|---|
| TAOM `FastFastForwardButton` (inserted) | `ExecuteExtraFastForward` on the mixin | `ITimeAccelerationService.EnterExtraFastForward()`, then vanilla `ExecuteTimeControlChange(2)` |
| vanilla `FastForwardButton` | `ExecuteFastForward` on the mixin, set by the existing `PrefabFastForwardButton` attribute patch | `EnterFastForward()`, then the same vanilla call |

`CommandParameter.Click` stays vanilla's `"2"` on both and is forwarded as the mixin methods' `int`
parameter (`ViewModel.ExecuteCommand` converts the string when the parameter counts match; a
parameterless method would be silently skipped). The vanilla call after the service keeps
`TimeFlowState` and the `_onTimeFlowStateChange` callback in step and no-ops harmlessly when already
fast-forwarding, because the multiplier write is what changes the pace.

Rebinding vanilla's button closes a second hole: after one E press the engine kept the extra
multiplier, and vanilla's mode-only handler ran at it until Space happened to restore the normal
value. The service's two commands run behind vanilla's own gate (no menu open, or a wait menu that
is not time-locked, read through `ITimeControlAdapter.IsWaitMenuActive`) rather than the key path's,
so the buttons keep working inside a wait menu where vanilla's do. The gate runs before the write:
a multiplier written while vanilla refuses the mode change would sit latent for the next fast-forward.

**Lit state.** `RefreshValues` on `MapTimeControlVM` runs only from its constructor and on a
gamepad state change, so the old `[ViewModelMixin("RefreshValues")]` never followed a key press.
The mixin now hooks `Tick`, which `MapBarVM.Tick` calls every frame, and `OnRefresh` copies
`ITimeAccelerationService.IsExtraFastForwardActive`: a fast-forward mode running ABOVE the normal
multiplier (turbo lights it too while Ctrl is held). The engine's `MapCurrentTimeVisualWidget`
writes vanilla's `FastForwardButton.IsSelected` itself for every fast-forward mode, so during extra
fast-forward BOTH buttons are lit: vanilla's alone is normal speed, both is extra. Unlighting
vanilla's would need a Harmony patch on a widget for a cosmetic gain; rejected.

**Known limitations (pre-existing, recorded by the #574 review).** `Campaign.OnLoad` resets
`SpeedUpMultiplier` to 4 on every save load regardless of the configured Fast Forward Multiplier, so
after a load the vanilla "3" key (`MapTimeFastForward`, mode only) fast-forwards at 4 until Space or
either map bar button writes the configured value. The turbo multiplier has no floor against the
extra one; a player may set turbo below extra, and turbo still lights the extra button while held.

**Floor.** Both sliders range 1 to 128 independently, so `TimeAccelerationSettingsProvider` floors
the extra multiplier at the fast-forward one (`ClampExtra`); without it "extra" could be slower than
"fast" and the lit state unreachable.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/TimeAcceleration/ITimeAccelerationService.cs` | Service interface |
| `Main/Features/TimeAcceleration/TimeAccelerationService.cs` | Core tick logic: input detection, multiplier application, Ctrl+Space save/restore; the two button commands and the lit-state query |
| `Main/Features/TimeAcceleration/TaomTimeControlHotKeyCategory.cs` | `GameKeyContext` publishing the three rebindable keys; static `Register()` called from `SubModule.OnSubModuleLoad` |
| `Main/Features/TimeAcceleration/IMapInputAdapter.cs` | Input abstraction (no TaleWorlds types), named per ACTION rather than per key |
| `Main/Features/TimeAcceleration/MapInputAdapter.cs` | Wraps `MapScreen.Instance.Input`; resolves the bound `GameKey`s once, then reads their current binding each frame |
| `Main/Features/TimeAcceleration/ITimeControlAdapter.cs` | Time control abstraction (no TaleWorlds types) |
| `Main/Features/TimeAcceleration/TimeControlAdapter.cs` | Wraps `Campaign.Current` speed/mode/lock and the wait-menu flag |
| `Main/Features/TimeAcceleration/ITimeAccelerationSettingsProvider.cs` | Settings interface |
| `Main/Features/TimeAcceleration/TimeAccelerationSettingsProvider.cs` | Reads `TaomSettings.Instance`; floors extra at fast |
| `Main/Features/TimeAcceleration/TimeAccelerationIoC.cs` | DryIoc registration (4 singletons) |
| `Main/Features/TimeAcceleration/UI/TimeAccelerationMixin.cs` | ViewModel mixin on `Tick`: `IsExtraFastForwardActive`, tooltip, `ExecuteExtraFastForward` / `ExecuteFastForward` |
| `Main/Features/TimeAcceleration/UI/TimeAccelerationPrefab.cs` | 5 prefab patches: widen panel, shift buttons (and rebind vanilla's FF button to the mixin), insert EFF button |
| `Main/_Module/ModuleData/global_strings.xml` | Key names/descriptions for the Options screen. The filename is a hard engine contract: `GlobalTextManager.LoadDefaultTexts()` opens this literal path and reads nothing else |
| `Main/_Module/ModuleData/Languages/*/std_taom_keybind_strings_*.xml` | The 12 translations of the above, listed in each `language_data.xml` |

## Dependencies

- `Bannerlord.UIExtenderEx` — Direct DLL reference from installed module; `SubModule.xml` declares `LoadBeforeThis`
- `TaomSettings` (MCM) — Provides the three multiplier values
- `MapScreen` (SandBox.View.dll) — Per-frame input polling
- `Campaign.Current` — Speed multiplier and time control mode

## Tests

- `TAOM.Tests/Features/TimeAcceleration/TimeAccelerationServiceTests.cs` carries 42 tests covering:
  - Co-op deferral, including a toggle-on mid-turbo that must restore rather than latch
  - Guard conditions (campaign inactive, map inactive, menu open)
  - Menu open + locked bypass
  - Fast-forward: sets multiplier, no SetTimeSpeed call
  - Extra fast-forward: sets multiplier + forces fast-forward
  - Turbo: saves state, applies turbo, restores on Ctrl release and on key release
  - Turbo priority over fast-forward
  - Configurable multiplier values
  - Rebinding: the shared Space default without Ctrl must fast-forward and not turbo; turbo rebound
    to its own key fires independently; Ctrl plus the fast-forward key must NOT turbo once turbo has
    moved elsewhere
  - The map bar commands: extra and normal multipliers written with the mode change, the configured
    values used, co-op deferral, campaign inactive, and the vanilla gate (non-wait menu blocks,
    unlocked wait menu proceeds, locked wait menu blocks)
  - The lit state: true in modes 2, 4 and 5 above the normal multiplier; false at the normal
    multiplier, in the play and stop modes, and with no campaign
- `TAOM.Tests/Features/TimeAcceleration/TimeAccelerationSettingsProviderTests.cs` carries 5 tests: the
  no-MCM fallbacks equal the compiled defaults, the defaults are ordered fast below extra below turbo,
  and `ClampExtra` floors extra at fast without touching a larger value
- `TAOM.Tests/Features/TimeAcceleration/TaomTimeControlHotKeyCategoryTests.cs` carries 10 tests pinning the
  three engine contracts above (id floor, slot count, MainCategoryId), plus context type, GroupId,
  id uniqueness, the shipped defaults, and the null-`KeyboardKey` premise the adapter's guard relies on

## How to Add a New Key Binding

The binding lives in the engine's keybinding registry, not in MCM.

1. Add an id constant to `TaomTimeControlHotKeyCategory` at or above `GameKeyDefinition.TotalGameKeyCount`
   (the existing block starts at 500), and bump `RegisteredSlotCount` so it still exceeds the largest id.
2. Register a `GameKey` for it in that class's constructor with
   `GameKeyMainCategories.CampaignMapCategory` as the `mainCategoryId`, or the key never renders in Options.
3. Add `str_key_name.TaomTimeControlHotKeyCategory_<id>` and `str_key_description.…_<id>` to
   `Main/_Module/ModuleData/taom_module_strings.xml`, then run `/localize` for the 12 languages.
4. Add an action-named property to `IMapInputAdapter` (`IsSomethingPressed`, never `IsXKeyPressed`)
   and resolve its `GameKey` alongside the others in `MapInputAdapter.EnsureResolved`.
5. Add the logic branch in `TimeAccelerationService.OnTick()`.
6. Add an MCM setting only for a tunable VALUE, never for the key itself. Note that any new
   `TaomSettings` property moves the pin in `SettingsFingerprintTests` and needs a
   `CoopSettingsRelevance` classification.
7. Write tests, including one that pins the new id against the three engine constraints.

## Performance

`OnTick()` runs every frame (~60+ fps) and is allocation-free: no LINQ, no collections, no string
operations. The three `GameKey` objects are resolved once per session by
`MapInputAdapter.EnsureResolved` (a scan over the category dictionary, then three linear `GetGameKey`
lookups), and the latch is set even when the category is absent so a failed registration cannot
re-scan every frame forever. After that, each read is `gameKey?.KeyboardKey?.InputKey`, two property
reads off a cached reference. Caching the reference rather than the resolved `InputKey` is deliberate:
a rebind replaces `GameKey.KeyboardKey`, so re-reading it per frame is what makes a mid-session rebind
take effect.

## Changelog

- 2026-09-12: the map bar Extra Fast Forward button applies the extra multiplier (#574). It had
  bound vanilla `ExecuteTimeControlChange(2)` directly, mode only, since the feature landed; #168
  recorded that as a known limitation. Vanilla's fast-forward button is rebound through the mixin to
  restore the normal multiplier, the lit state refreshes per frame off `Tick`, and the provider
  floors extra at fast.
- 2026-08-22: the three tiers became rebindable native game keys in Options > Keybindings > Campaign
  Map (`TaomTimeControlHotKeyCategory`, ids 500/501/502). Motivation: the extra fast-forward key was
  hardcoded to E, which is also vanilla `MapRotateRight` (GameKey 59), so pressing E accelerated time
  AND rotated the camera with no way to change it. Defaults are unchanged. `IMapInputAdapter` members
  were renamed from key names to action names, and no MCM property was added or removed.
- 2026-04-06 — Adversarial-review fix: turbo restore now runs before the early returns in `OnTick()`.
- 2026-04-05 — Feature landed: configurable campaign-map speed (BetterTime replacement) — Space/E/Ctrl+Space tiers, Extra Fast-Forward MapBar button via UIExtenderEx, 3 MCM multiplier sliders, 14 unit tests.

## GitHub Issue

- **Issue:** #181 — Time acceleration feature
- **Status:** Closed

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/modding/file-catalogue.md](../modding/file-catalogue.md)
- [docs/modding/strings-and-localization.md](../modding/strings-and-localization.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)

<!-- backlinks-end -->
