# GUI & Sprite System

**Status:** Verified in-game (2026-04-14, Gondor campaign). Career button sprite, map bar resource display with tooltip, and shader precompilation all confirmed working.

## Overview

TAOM's UI layer uses Bannerlord's Gauntlet UI framework with UIExtenderEx for injecting into vanilla screens. Sprites are PNG source images compiled into sprite sheets by the game engine. All UI data binding uses the `@PropertyName` / `{DataSourceName}` pattern from Gauntlet XML prefabs backed by `ViewModel` subclasses with `[DataSourceProperty]` attributes.

## Why This Exists

- **Vanilla behavior:** No career screen, no special resource display, no per-faction UI elements
- **TAOM requirement:** Career choice trees, resource tracking on map bar, faction-specific icons
- **Without this feature:** Players have no way to interact with careers or see resource status

## Sprite Pipeline

### How It Works

```
GUI/SpriteParts/ui_taom/<subfolder>/*.png    ← Source PNGs (any resolution)
        ↓ (game engine sprite compiler)
GUI/SpriteData/ui_taom/                      ← Compiled sprite sheets
        ↓ (referenced by)
GUI/TAOMSpriteData.xml                       ← Declares categories + sheet dimensions
        ↓ (loaded at runtime by)
Context.SpriteData.GetSprite("sprite_name")  ← C# or XML Sprite="sprite_name"
```

### Sprite Name = Filename Without Extension

A PNG at `SpriteParts/ui_taom/SpecialResources/taom_gems_icon.png` becomes sprite ID `taom_gems_icon`. Reference it in XML as `Sprite="taom_gems_icon"` or in C# as `Context.SpriteData.GetSprite("taom_gems_icon")`.

### Folder Structure

```
GUI/
├── Brushes/                          ← Brush XML definitions (colors, fonts, states)
├── Fonts/                            ← Custom fonts
├── Prefabs/                          ← Gauntlet XML UI layouts
│   └── CareerSystem/
│       ├── CareerScreen.xml          ← Main career screen prefab
│       └── AbilityHUD.xml            ← Battle HUD for active ability
├── SpriteData/                       ← Compiled sprite sheets (auto-generated)
├── SpriteParts/                      ← Source PNGs (organized by category)
│   ├── Config.xml                    ← Sprite compiler config
│   └── ui_taom/                      ← TAOM's sprite category
│       ├── CareerSystem/             ← Career UI sprites
│       │   └── career_button_placeholder.png
│       ├── MapBar/                   ← Map bar icons (existing vanilla overrides)
│       ├── SpecialResources/         ← Special resource icons
│       │   ├── taom_gems_icon.png    ← Erebor (gemstone wheel)
│       │   ├── taom_caster_icon.png  ← Gondor (White Tree coin)
│       │   └── taom_marks_icon.png   ← Rohan (horse coin)
│       └── ... (other subfolders)
└── TAOMSpriteData.xml                ← Master sprite category declaration
```

### TAOMSpriteData.xml

Declares the `ui_taom` sprite category with `AlwaysLoad` — all PNGs in `SpriteParts/ui_taom/` are compiled into this category's sprite sheets automatically.

```xml
<SpriteCategory>
  <Name>ui_taom</Name>
  <AlwaysLoad />
  <SpriteSheetCount>4</SpriteSheetCount>
  <SpriteSheetSize ID="1" Width="4096" Height="4096" />
  ...
</SpriteCategory>
```

If adding many new sprites, increase `SpriteSheetCount` or sheet dimensions.

### The sprite-bake pipeline

*(Decompile-verified 2026-05-31 — `TaleWorlds.TwoDimension.SpriteSheetGenerator.exe` + `.Library.dll`.)*

**A new loose PNG renders blank until it is packed** (observed with `career_point_pip` this session — see the worked example below). Each category's sprite-sheet textures are **baked offline**: `TaleWorlds.TwoDimension.SpriteSheetGenerator.exe` packs the loose `SpriteParts/**.png` into atlas sheet PNGs + a manifest, and the editor's downstream **texture-compile pass** turns those into the loadable `_tex.tpac`. The loose PNGs are the **source**, not what the shipping client loads directly. *(The generator decompile confirms the offline pack flow; the exact editor-vs-shipping launch-time behavior was not independently decompiled — but empirically a loose-only PNG does not render.)*

What the generator reads and writes (verified by decompiling `...SpriteSheetGenerator.Library.dll` → `SpriteEditorDomain.Tick()` + `SpriteDataEditor.Save*`, and by inspecting the live output):

| Stage | Path (per module, **Engine** output mode) |
|-------|-------------------------------------------|
| **Input — sprites** | `<Module>/GUI/SpriteParts/<category>/**.png` (recursive; **max folder depth 8**) |
| **Input — pack config** | `<Module>/GUI/SpriteParts/Config.xml` (per-category: `AlwaysLoad`, `EdgeSize`, `PackAllSpritesToUniqueTextures`, `SingleChannel`, `NoAlphaChannel`) |
| **Output — manifest** | `<Module>/GUI/<ModuleName>SpriteData.xml` (e.g. `TAOMSpriteData.xml`) — `<SpriteCategory>` (Name, AlwaysLoad, SpriteSheetCount, per-sheet `SpriteSheetSize`), `<SpritePart>` (SheetID, Name, Width, Height, **SheetX/SheetY**, CategoryName), `<GenericSprite>` (Name → SpritePartName) |
| **Output — packed source sheet** | `<Module>/AssetSources/GauntletUI/<category>_<n>.png` (the bin-packed atlas PNG; this dir is **emptied then rewritten** every run) |
| **Downstream — compiled texture** *(NOT written by the generator)* | `<Module>/Assets/GauntletUI/<category>_<n>_tex.tpac` — a small (~500-byte) descriptor compiled by the editor's **texture-compile pass** that runs *after* the generator (live timestamps: manifest + `AssetSources` PNG at T, `_tex.tpac` ~1–2 min later). |

There is **NO `pack0.tpac`** for UI sprites — UI atlases are per-category `<category>_<n>` pairs (`AssetSources/...png` + `Assets/..._tex.tpac`). (`pack0.tpac` does not exist in the TAOM module at all; an earlier version of this doc was wrong.) The `SpriteSheetGenerator` binary itself writes **only** the manifest + the `AssetSources` PNG (every `SaveSheet` path calls `ImageWriter.WritePng`; it `CreateDirectory`s `Assets/GauntletUI/` but never writes a `.tpac` there) — the `_tex.tpac` is the separate downstream texture-compile. In the editor's normal sprite-generation flow both get produced (which is why running it "just works"); if you ever invoke only the bare generator, the texture compile must also run for the sprite to load.

**Invocation** (CLI; the exe is at `bin/Win64_Shipping_wEditor/TaleWorlds.TwoDimension.SpriteSheetGenerator.exe`):
- No args → packs **all** modules under `Modules/` (default `SourceDirectory=<exe>/../../Modules/`, `OutputType=Engine`, `CollectionType=AllAvailableModules`).
- `SourceDirectory=<full path>` — a `Modules/` folder or a single module dir.
- `OutputType=Engine` (Bannerlord) | `Standalone` (launcher — writes `SpriteSheets/<category>/` instead).
- `CollectionType=SingleModule | AllAvailableModules`.

### Adding a New Sprite

1. Place the PNG in `GUI/SpriteParts/<category>/<subfolder>/your_sprite_name.png` (resize to a sane size first — oversized PNGs corrupt the atlas; see `feedback_sprite_dimensions`). Do NOT hand-edit `TAOMSpriteData.xml` to add the sprite — the generator overwrites the whole manifest, so a hand-added entry (e.g. inventing a new sheet ID) is pointless and can mislead.
2. **Run the sprite generator** (`SpriteSheetGenerator.exe`) — REQUIRED, not optional. It re-packs the loose PNGs and rewrites `<ModuleName>SpriteData.xml` + `AssetSources/GauntletUI/*.png`; the editor's downstream **texture-compile pass** then (re)builds `Assets/GauntletUI/*_tex.tpac` from the new PNG (the generator binary itself does not write the `.tpac` — see the pipeline table). A loose PNG that is never packed renders **blank** in the player client.
3. Reference in XML: `Sprite="<category-path>\your_sprite_name"` (verify the exact registered `<Name>` in `TAOMSpriteData.xml` after regen — see `.claude/rules/gui-ui.md`).
4. Reference in C#: `Context.SpriteData.GetSprite("...")`.
5. **Baked ≠ visible.** A correctly-baked sprite can still render invisibly if the prefab sizes or tints it badly. After regen, verify in-game: widget size large enough to read, `Color` alpha high enough against the background, and a sprite-capable widget (`Widget`/`ImageWidget`/`ButtonWidget` all render `Sprite=`).

**Worked example — `career_point_pip` (career screen, 2026-05-31, TWO distinct causes):**
- **Cause 1 (bake):** the One Ring pip first rendered blank because it was a *new* loose PNG that had never been packed — `Assets/GauntletUI/ui_taom_career_system_1_tex.tpac` + the `AssetSources` sheet had no pip pixels. Running `SpriteSheetGenerator.exe` fixed the bake: the pip landed on sheet 1 at `SheetX=2428 SheetY=1670` (256×256), confirmed by cropping that rect out of the regenerated `AssetSources/GauntletUI/ui_taom_career_system_1.png` and seeing the ring.
- **Cause 2 (render) — the one that survived the regen:** after a correct bake the pip was *still* invisible in-game. Root cause was the **prefab**, not the asset: the pip was drawn at `22×28px` with `Color="#FFFFFF45"` (27% alpha) for the empty state — a thin gold line-art ring at that size/opacity on a near-black node reads as faint embossing. Fix: bump the pip to `38×38` and raise the state opacities (`#FFFFFFFF` taken / `#FFFFFFE0` available / `#FFFFFF78` empty). No regen needed for this — it's prefab-only.
- **Review lesson:** the earlier `/deep-review` + Codex passes verified the manifest *shape* and (wrongly) assumed a runtime-build model + `pack0.tpac`; and the first RCA wrongly concluded "regen will fix the blank pip." Both the asset bake AND the prefab render must be verified, and **only the live game confirms a sprite is visible** — a CLEAN review cannot. Memory: `feedback_sprite_atlas_baked_regen_required`.
- **2026-06-19 follow-up — pip never "lit up" on point-take.** The render fix above raised the empty-state opacity but left all three states on the *same hollow ring*; taken (`#FFFFFFFF`) vs available (`#FFFFFFE0`) was only a ~12% alpha gap, imperceptible. Since Gauntlet `Color` is a multiplicative tint (can't brighten a sprite past its own pixels), the fix needed a brighter *taken* sprite plus a wider alpha gap (`@IsFreeToTake` `#FFFFFF55`, `@IsUnavailable` `#FFFFFF22`) across all 3 tiers of `CareerScreen.xml`. The **first attempt** used a *filled-disc* sprite (`career_point_pip_filled`) — but it both read wrong (user wanted a *brighter ring*, not a disc) **and** rendered blank in-game because the new loose PNG was never baked/synced. That blank was itself diagnostic: when pips were taken the faint rings *disappeared*, proving `@IsTaken` binds correctly and only the asset was missing. **Final fix:** `career_point_pip_lit` — the existing ring brightened toward white + a soft glow halo — for `@IsTaken`; the disc was deleted. Re-confirms the lesson twice over: a hollow ring at +12% alpha "bakes correctly yet reads identical," and a brand-new sprite is blank until the editor bake + sync — visual distinction is a *design* property, and rendering is an *in-game-only* property, neither provable by static review. **Bake completed** via the editor sprite-generation (manifest registers `_lit` on `ui_taom_career_system` sheet 1 at rect (3764,3124); atlas + `_tex.tpac` rebuilt and synced; committed in f221ba37 + 92281887). Deploy footnote: the bare `SpriteSheetGenerator.exe` CLI is awkward for one module — its `CollectionType=SingleModule` branch looks for `Config.xml` under `<dir>\SpriteParts` (missing the `\GUI\`) so it silently no-ops; the working CLI path is `CollectionType=AllAvailableModules` pointed at a folder containing only the target module (e.g. a temp junction to it), and you must let the process run to completion (piping to `Select-Object -First` kills it mid-pack). The editor's own sprite-generation remains the simplest path since it also rebuilds the `_tex.tpac` the bare CLI never writes. **Confirmed working in-game 2026-06-22** (taken pips brighten to the glowing ring; `+`/`-` work). **Restart-after-rebake gotcha:** a re-bake re-bin-packs the whole category, so existing sprites move to new atlas rects (here `plus`/`minus`/`lit` moved while `career_point_pip` happened to stay put). A *running* game holds the pre-bake atlas **texture** but reads the **new** manifest rects on the next screen open → sprites whose rects moved render from empty/garbage texture regions (the `-` button vanished, `+` went dead) while the unmoved one still looked fine. It is not a code/asset bug and a `/deep-review`/`/investigate` is the wrong tool — **fully exit and relaunch the game** so atlas + manifest + tpac reload together. Add "restart the game after a sprite re-bake" to the deploy checklist whenever the bake repositions existing sprites.

### Verifying a sprite (bake + render) — BOTH are required

A sprite has two independent failure modes. Check both; they are diagnosed differently.

**1. Bake — "is the sprite in the compiled sheet?"** (static, scriptable):
- Read the `<SpritePart>` for your sprite in `GUI/<Module>SpriteData.xml`; note `SheetID` + `SheetX`/`SheetY` + `Width`/`Height`.
- Crop that exact rect out of `AssetSources/GauntletUI/<category>_<SheetID>.png` and confirm non-transparent pixels are present:
  ```python
  from PIL import Image
  im = Image.open(r"<game>/Modules/TAOM/AssetSources/GauntletUI/<category>_<SheetID>.png")
  crop = im.crop((X, Y, X+W, Y+H)); crop.save("crop.png")        # then look at crop.png
  print(crop.split()[-1].getextrema())                            # alpha extrema; (0,0) == empty/unbaked
  ```
  If the rect is empty/transparent, the generator has not packed it — re-run the generator.
- Confirm `Assets/GauntletUI/<category>_<SheetID>_tex.tpac` exists. **~500 bytes is normal** — it's a descriptor, not the texture payload, so size is NOT a bake indicator.

**2. Render — "does the prefab actually show it?"** (in-game ONLY):
- Widget large enough to read — a thin line-art sprite at ~22px is effectively invisible.
- `Color` alpha high enough vs. the background — e.g. `#FFFFFF45` (27%) on a near-black node ≈ invisible.
- A sprite-capable widget — `Widget`, `ImageWidget`, and `ButtonWidget` all render `Sprite=`.
- Prefab `Sprite="..."` name exactly matches the registered `<Name>` (case + backslashes, no module prefix) — see `.claude/rules/gui-ui.md`.

Static review (including `/deep-review` + Codex) can confirm bake *shape* but **cannot confirm a sprite renders**. Always flag new-sprite rendering as in-game-only in the CHANGELOG `Not-tested:` line; never let a CLEAN review imply a sprite will display.

### Deploying a prefab/sprite change for in-game testing

- **Prefab XML** is loaded at runtime (not baked). A normal `./build.ps1` copies `Main/_Module/**` to the game install, or — for a fast prefab-only iteration — copy the single file to `<game>/Modules/TAOM/GUI/Prefabs/<Feature>/<Screen>.xml`. No regen needed for a prefab-only change.
- **A new/changed sprite PNG** must be deployed to the game install AND the generator re-run there (it rewrites the install's `GUI/<Module>SpriteData.xml` + `AssetSources/` + `Assets/`). **Then sync the regenerated manifest + `AssetSources/` + `Assets/` back into the repo** so a later `./build.ps1` doesn't clobber the bake with the stale repo manifest. Verify the sync with `diff` — repo and install `<Module>SpriteData.xml` should be byte-identical.

### Sprite gotchas (consolidated — each links a memory)

| Gotcha | Rule | Memory |
|--------|------|--------|
| Oversized source PNG corrupts the atlas | Resize to the target display size (or a sane power-of-two) before packing | `feedback_sprite_dimensions` |
| Baked ≠ visible | A correctly-baked sprite still renders blank if the prefab sizes/tints it badly — verify in-game (render failure mode above) | `feedback_sprite_atlas_baked_regen_required` |
| Moving a sprite between categories leaves a ghost | Delete the old loose PNG from BOTH repo and game install, then regen — else the old atlas keeps the stale copy | `feedback_sprite_atlas_cleanup` |
| Wrong sprite name renders blank silently | Sprite id = path under the category folder with backslashes, NO module prefix; verify against `<Module>SpriteData.xml` after regen | `.claude/rules/gui-ui.md` |

## Gauntlet UI Architecture

### Binding Model

```
Prefab XML (declarative layout)
    @PropertyName    → ViewModel.[DataSourceProperty] string/bool/int
    {CollectionName} → ViewModel.[DataSourceProperty] MBBindingList<T>
    Command.Click    → ViewModel.ExecuteMethodName()
    Command.HoverBegin/End → ViewModel.ExecuteBeginHover()/ExecuteEndHover()
```

### Screen Lifecycle

```
ScreenManager.PushScreen(new GauntletXxxScreen(...))
    → OnInitialize()
        → new GauntletLayer("name", order)
        → layer.LoadMovie("PrefabName", viewModel)
        → AddLayer(layer)
    → OnFrameTick(dt)  [per-frame update]
    → OnFinalize()
        → layer.ReleaseMovie(movie)
        → viewModel.OnFinalize()
        → ScreenManager.PopScreen()
```

### UIExtenderEx Integration

TAOM uses UIExtenderEx for two types of injection:

**1. ViewModel Mixin** — adds properties/methods to existing ViewModels:
```csharp
[ViewModelMixin("RefreshValues")]
internal class MyMixin : BaseViewModelMixin<TargetVM>
{
    [DataSourceProperty]
    public string MyProperty { get; set; }
    
    public void ExecuteMyCommand() { ... }
}
```

**2. Prefab Extension** — injects widgets into existing XML prefabs:
```csharp
[PrefabExtension("TargetPrefab", "descendant::Widget[@Id='Target']")]
internal class MyPrefab : PrefabExtensionInsertPatch
{
    public override InsertType Type => InsertType.Append;
    
    [PrefabExtensionXmlDocument]
    public XmlDocument GetDocument() { ... }
}
```

## Career Screen UI

### Prefab: `CareerScreen.xml`

Modeled on TOR's career screen with expandable choice panels.

**Layout:**
```
┌──────────────────────────────────────────┐
│              Career (title)               │
├────────────┬─────────────────────────────┤
│            │  Tier 1: [group_a] [group_b]│
│  Portrait  │  ─────────────────────────  │
│  Name      │  Tier 2: [group_a] [group_b]│
│  Desc      │  ─────────────────────────  │
│  ───────   │  Tier 3: [group_a] [group_b]│
│  Ability   │                             │
│  Icon      │          Free Points: 5     │
│  Effects   │                             │
├────────────┴─────────────────────────────┤
│                [Done]                     │
└──────────────────────────────────────────┘
```

**Key features:**
- `VisualDefinition="ExtendablePanel"` — choice groups expand 80px→750px on hover
- `@IsTaken` / `@IsFreeToTake` / `@IsUnavailable` — distinct pip art per state: taken draws the brighter **`career_point_pip_lit`** ring (whitened + glow halo) at full opacity; available/locked draw the hollow `career_point_pip` ring at a low/very-low alpha. (Pre-2026-06-19 all three shared the hollow ring at near-identical alpha, so an increased skill didn't visibly "light up" — see the worked example below.)
- `@IsActive` + `@ButtonsVisible` — +/- buttons appear on hover, hidden when locked
- `CareerSystem\locked_chains` sprite overlay on locked tiers
- `<Standard.Background />` and `<Standard.DialogCloseButtons />` for native look

### Binding Chain

```
CareerScreenVM
├── @ScreenTitle, @DoneLbl, @CareerName, @CareerDescription
├── @CareerPortraitSprite (career portrait image)
├── @AbilityName, @AbilitySpriteName, @AbilityLabel
├── @FreeCareerPointsText ("Free Points: 5")
├── @Tier1/2/3Label, @Tier1/2/3Locked
├── {AbilityEffects} → MBBindingList<CareerAbilityEffectVM>
│   └── @LineText
├── {ChoiceGroupsTier1/2/3} → MBBindingList<CareerChoiceGroupObjectVM>
│   ├── @GroupName, @IsActive, @ButtonsVisible, @IsLocked
│   ├── ExecuteBeginHover(), ExecuteEndHover()
│   ├── ExecuteClickIncrease(), ExecuteClickDecrease()
│   └── {Choices} → MBBindingList<CareerChoiceObjectVM>
│       ├── @Description, @IconSprite, @IsKeystone
│       ├── @IsTaken (gold icon), @IsFreeToTake (brown icon)
│       └── ChoiceId (non-binding, used by parent)
└── ExecuteClose()
```

### Career Button on Character Developer

**Status:** Verified in-game (2026-04-14). Dark steel banner sprite with "Career" text overlay.

Injected via `CareerButtonPrefab.cs` → `PrefabExtensionInsertPatch` on `CharacterDeveloper` prefab's `TopPanelParent`. Uses `Sprite="CareerSystem\career_button_placeholder"` (233x75). Visibility gated by `@HasCareer` from `CharacterDeveloperCareerMixin`.

**Opening flow (TOR pattern):** `Patch27` Harmony postfix on `ViewModel.ExecuteCommand` catches `"ExecuteOpenCareerScreen"` → calls `charDevVM.ExecuteDone()` to close Character Developer first → then `Game.Current.GameStateManager.PushState<CareerScreenGameState>()`. The `[GameStateScreen]` attribute on `GauntletCareerScreen` properly deactivates the map bar input layer.

**Critical:** Must close CharacterDeveloper BEFORE pushing career state. Without `ExecuteDone()`, the map bar global layer continues ticking input with invalid context → `IndexOutOfRangeException`.

### Ability HUD in Battle

`AbilityHUD.xml` displayed via `CareerPerkMissionBehavior` on a `GauntletLayer("CareerAbilityHUD", 50)`. Shows charge percentage and ready state via `CareerAbilityHudVM`.

## Special Resource Map Bar

**Status:** Verified in-game (2026-04-14). Gondor Caster showing with tooltip on map bar.

### Architecture (TOR Pattern)

Uses `SecondaryInfoItems.Add()` with proper `MapInfoItemVM` objects — the same approach TOR uses successfully. A `SpecialResourceSpriteWidget` (extends `IconBrushWidget`) replaces the default icon in the item template to dynamically load the resource's sprite.

```
SpecialResourceMapBarMixin (ViewModelMixin on MapInfoVM, hooks "Refresh")
├── Creates MapInfoItemVM("special_resource", GetTooltipProperties)
├── Adds to SecondaryInfoItems once (_baseInitialized guard)
├── Updates Value, IntValue, HasWarning per frame
└── GetTooltipProperties() → rich tooltip with tier, daily change, earning rates

SpecialResourceIconPrefab (PrefabExtension on MapBar)
└── Replaces IconBrushWidget in BottomInfoBar ItemTemplate
    with SpecialResourceSpriteWidget (dynamic icon loading)
```

**Critical:** The mixin MUST hook `"Refresh"` (per-frame), NOT `"RefreshValues"` (one-time init). TOR uses the same pattern.

### Tooltip Content

The hover tooltip shows:
- Resource name + amount/cap (title)
- Current tier name + description (if tier active)
- Next tier threshold (if below all tiers)
- Daily change breakdown: income (N towns) vs elite upkeep
- Per-event earning rates: battle, raid, siege, prisoner

## Sprites Needed (Not Yet Created)

| Sprite ID | Size | Count | Used By |
|-----------|------|-------|---------|
| `career_{id}_portrait` | 400x200 | 50 | CareerScreen left panel |
| `ability_{id}_icon` | 120x120 | 50 | CareerScreen ability section |
| `CareerSystem\locked_chains` | Full tier width | 1 | Tier lock overlay |
| `CareerSystem\plus_sign_icon` | 50x50 | 1 | Choice group add button |
| `CareerSystem\minus_sign_icon` | 50x50 | 1 | Choice group remove button |
| `taom_{resource}_icon` | 45x45 | 8 remaining | Map bar resource icons |

The 3 completed resource icons (gems, caster, marks) are in `SpriteParts/ui_taom/SpecialResources/`. The remaining 8 resources need icons generated (ComfyUI at `E:\ComfyUI_windows_portable_nvidia`).

## Key Files

| File | Purpose |
|------|---------|
| `Main/_Module/GUI/PreFabs/CareerSystem/CareerScreen.xml` | Career screen layout |
| `Main/_Module/GUI/PreFabs/CareerSystem/AbilityHUD.xml` | Battle ability HUD |
| `Main/_Module/GUI/TAOMSpriteData.xml` | Sprite category declaration |
| `Main/Features/CareerSystem/UI/GauntletCareerScreen.cs` | Screen creation + lifecycle |
| `Main/Features/CareerSystem/UI/CareerScreenVM.cs` | Career screen ViewModel (root) |
| `Main/Features/CareerSystem/UI/CareerChoiceGroupObjectVM.cs` | Choice group VM (expandable, hover/click) |
| `Main/Features/CareerSystem/UI/CareerChoiceObjectVM.cs` | Individual choice VM (taken/free state) |
| `Main/Features/CareerSystem/UI/CareerAbilityEffectVM.cs` | Ability effect line item VM |
| `Main/Features/CareerSystem/UI/CareerAbilityHudVM.cs` | Battle HUD VM |
| `Main/Features/CareerSystem/UI/CharacterDeveloperCareerMixin.cs` | Mixin for career button on CharDev |
| `Main/Features/CareerSystem/UI/CareerButtonPrefab.cs` | Prefab injection for career button |
| `Main/Features/SpecialResources/UI/SpecialResourceMapBarMixin.cs` | Mixin for resource on map bar |
| `Main/Features/SpecialResources/UI/SpecialResourcePrefab.cs` | Prefab injection for resource widget |
| `Main/Features/SpecialResources/UI/SpecialResourceSpriteWidget.cs` | Custom widget for dynamic sprite |

## How-To

> Both recipes below are instances of the canonical [Adding a New Sprite](#adding-a-new-sprite) workflow — the same bake (run the generator) + verify (bake-then-render) steps apply. "Launch game — it's automatic" is true ONLY in editor/dev mode, never the player client.

### Add a sprite for a new resource
1. Create PNG icon (recommended 45x45 or higher; resize first — `feedback_sprite_dimensions`)
2. Place at `GUI/SpriteParts/ui_taom/SpecialResources/taom_{resource_id}_icon.png`
3. Set `icon_sprite="taom_{resource_id}_icon"` in `special_resources_config.xml`
4. **Run the sprite generator** (`SpriteSheetGenerator.exe` — see [the bake pipeline](#the-sprite-bake-pipeline)); a loose PNG alone renders blank in the player client. Then [verify bake + render](#verifying-a-sprite-bake--render--both-are-required).

### Add a career portrait
1. Create PNG (400x200; resize first)
2. Place at `GUI/SpriteParts/ui_taom/CareerSystem/career_{career_id}_portrait.png`
3. Set `portrait_sprite="career_{career_id}_portrait"` in `taom_careers.xml`
4. **Run the sprite generator** and [verify](#verifying-a-sprite-bake--render--both-are-required) — same as above.

### Inject a widget into a vanilla screen
1. Create a `PrefabExtensionInsertPatch` class with `[PrefabExtension("TargetPrefab", "xpath")]`
2. Return XML from `GetDocument()` with `@PropertyName` bindings
3. Create a `BaseViewModelMixin<TargetVM>` with `[DataSourceProperty]` for each binding
4. **Do NOT add to collection properties** (SecondaryInfoItems, etc.) — use bound properties + prefab injection

### Debug UI bindings
1. Check Bannerlord's `rgl_log.txt` for Gauntlet binding errors
2. Property name in XML must EXACTLY match `[DataSourceProperty]` name (case-sensitive)
3. `Command.Click="ExecuteX"` requires a public `void ExecuteX()` method on the ViewModel
4. `{CollectionName}` requires `MBBindingList<T>` — not `List<T>` or `IReadOnlyList<T>`

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/career-system.md](./career-system.md)
- [docs/features/special-resources.md](./special-resources.md)
- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
## Changelog

- 2026-06-19 — Career-ability pips now visibly light up on point-take: new `career_point_pip_lit` ring sprite (brightened + glow halo) for the taken state, widened alpha gaps on the other states, baked + committed.
- 2026-05-31 — Decompile-verified GUI sprite-bake pipeline documented end-to-end (`SpriteSheetGenerator.exe` inputs/outputs, no `pack0.tpac` for UI atlases, `_tex.tpac` is a separate downstream texture-compile); added "Verifying a sprite (bake + render)" and "Deploying a prefab/sprite change" sections plus the consolidated gotchas table.
- 2026-05-31 — Career-pip blank-render fix established the "two failure modes" rule: a new loose PNG must be packed by the generator (bake) AND the prefab must size/tint it readably (render); baked != visible, only the live game confirms render.
- 2026-04-14 — Career-screen sprite atlas established: dedicated `ui_taom_career_system` category created to stop oversized career images corrupting the main `ui_taom` atlas; portraits 800x400, ability icons 256x256; in-game sprite-path fixes (removed extra `TAOM\` prefix, added `SpecialResources\` prefix).
