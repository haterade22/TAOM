# GUI & Sprite System

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

### Adding a New Sprite

1. Place PNG in `GUI/SpriteParts/ui_taom/<subfolder>/your_sprite_name.png`
2. Launch the game — sprite compiler picks it up automatically
3. Reference in XML: `Sprite="your_sprite_name"`
4. Reference in C#: `Context.SpriteData.GetSprite("your_sprite_name")`
5. No `TAOMSpriteData.xml` changes needed unless you exceed sheet capacity

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
- `@IsTaken` / `@IsFreeToTake` — different icon colors for taken vs available
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

Injected via `CareerButtonPrefab.cs` → `PrefabExtensionInsertPatch` on `CharacterDeveloper` prefab's `TopPanelParent`. Uses `Sprite="TAOM\CareerSystem\career_button_placeholder"` (233x75). Visibility gated by `@HasCareer` from `CharacterDeveloperCareerMixin`.

### Ability HUD in Battle

`AbilityHUD.xml` displayed via `CareerPerkMissionBehavior` on a `GauntletLayer("CareerAbilityHUD", 50)`. Shows charge percentage and ready state via `CareerAbilityHudVM`.

## Special Resource Map Bar

### Architecture (Post-Fix)

The original implementation added items to `SecondaryInfoItems` which caused `IndexOutOfRangeException` in vanilla's `HandlePanelSwitchingInput` (hardcoded positional indexing).

**Current approach:** ViewModel mixin exposes bound properties, prefab extension injects a widget.

```
SpecialResourceMapBarMixin (ViewModelMixin on MapInfoVM)
├── [DataSourceProperty] ResourceDisplayText   → "Gems: 275 (Journeyman Smith)"
├── [DataSourceProperty] IsResourceVisible     → true/false
├── [DataSourceProperty] HasResourceWarning    → true when balance ≤ 0
└── [DataSourceProperty] ResourceTooltipTitle  → "Gems"

SpecialResourceMapBarPrefab (PrefabExtension on MapBar)
└── Appends TextWidget to BottomInfoBar bound to @ResourceDisplayText
```

**Constraint:** Do NOT add to `SecondaryInfoItems` — vanilla code indexes it by position. Use bound properties + prefab injection instead.

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

### Add a sprite for a new resource
1. Create PNG icon (recommended 45x45 or higher)
2. Place at `GUI/SpriteParts/ui_taom/SpecialResources/taom_{resource_id}_icon.png`
3. Set `icon_sprite="taom_{resource_id}_icon"` in `special_resources_config.xml`
4. Launch game — sprite compiler picks it up

### Add a career portrait
1. Create PNG (400x200)
2. Place at `GUI/SpriteParts/ui_taom/CareerSystem/career_{career_id}_portrait.png`
3. Set `portrait_sprite="career_{career_id}_portrait"` in `taom_careers.xml`

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
