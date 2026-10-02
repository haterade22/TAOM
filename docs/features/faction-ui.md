# Faction UI (Kysaro's themed front end)

## Overview

Kysaro's TAOM_FactionUI module, merged into TAOM (#704): a Middle-earth splash video, an animated main
menu with a Sauron video behind it, painted loading screens, Kysaro's skill icons, his reskins of every
character-creation screen, and his faction and hero picker, which lets a new character start as a named
lord or legend. Kysaro is TAOM's lead scener and lead UI designer; he built the module against TAOM on
Bannerlord v1.5.3 and sent it as a compiled DLL, which was decompiled and ported into TAOM's architecture.

## Why This Exists

- **Vanilla behavior:** Calradia's main menu, menu video and theme, TaleWorlds' splash and the vanilla
  loading paintings.
- **TAOM requirement:** a front end that reads as Middle-earth from the first frame.
- **Without this feature:** the module ships as a second mod beside TAOM that reaches into TAOM's
  internals by reflection and holds every one of its images in memory for the whole session.

## Architecture

### Design Challenge

Kysaro's module loaded all 262 of its runtime images at the first themed screen and kept them until the
game closed: about 337 MB of sprites plus up to 221 MB of loading screens once each had been shown
(uncompressed, measured from the PNG headers). TAOM has a history of memory-pressure crashes, so Mike
decided (2026-10-01) that the images load on demand instead. Gauntlet resolves a sprite name when a
prefab is built or a bound value is set, so an image must be registered before the screen that shows it
loads, and it can only be released once no screen draws it.

### Solution Approach

`FrontEndSpriteService` owns every runtime image. `FrontEndSpriteCatalog` sorts each by name:

| Kind | Names | Lifetime |
|---|---|---|
| Main menu (`MenuChrome`) | `mm_*`, plus the `TAOMMainMenu` brushes | while the themed main menu holds it |
| Character creation (`Chrome`) | `cc_*` and every other `fs_*` (`fs_minimap_big` included), plus the `TAOMCharCreation` and `TAOMFactionScreen` brushes | while any themed character-creation screen or the faction screen holds it |
| Faction art (`Art`) | `fs_portrait_*`, `fs_reveal_*`, `fs_emblem_*`, `fs_territory_*`, `fs_bg_*` | loaded one image at a time as the faction screen shows it; held with the faction screen |
| Resident | `ld_*`, the `TAOMLoading` brushes and the three bitmap fonts | the process: the loading window is built once, and the engine cannot remove a font |
| Skill icons | `gui_skills_icon_*`, registered under vanilla's names from `sprite_overrides.json` | the process |

Each themed movie holds the image groups it draws (`FrontEndImageGroups`): the main menu its own, every
character-creation stage the shared frame, the faction screen that frame plus its art. A group is
released 60 ticks after nothing holds it, so moving between stages does not reload it. Loading screens
are held one at a time, each new one releasing the one before 60 ticks later. Each group's load and
release writes a `[FactionUI] front-end images loaded:` or `released:` line with its image count and
size; faction art joins its group one image at a time without a line of its own, and loading paintings
are not logged.

Release needs no patch. A screen's layer releases its movies through `GauntletLayer.OnFinalize` ->
`ClearContext` -> `movie.Release()`, never through the public `ReleaseMovie`, so the first build's
`ReleaseMovie` prefix never saw a themed screen close and freed nothing. `FrontEndMovieService` records
which movie object holds which groups; `FactionUITicker` asks each one whether it is released once per
tick, and the sprite service frees a group's textures with the engine's own UI-texture release
(`Texture.ReleaseImmediately`). When the engine rebuilds its sprite, font and brush tables (only when the
native side loads a module at runtime), a postfix on `UIResourceManager.Refresh` puts every loaded image,
font and brush back before the open screens are rebuilt.

`FrontEndMovieService` swaps a vanilla movie for a themed one when both the movie name and the view-model
type match: the main menu, the version line, the loading window, the face generator, the backstory,
review, banner, clan-naming and options stages, and the faction screen. The face generator and banner
editor are themed only inside character creation, so the barber and the campaign banner editor stay
vanilla. A themed prefab missing from the install keeps the vanilla screen with one warning, and a
themed movie that throws while building falls back to vanilla's and is not tried again until restart.

### Character creation

- **Faction screen.** `FactionScreenLauncher` plugs into FactionMap's culture stage through
  `ICultureStageMovieOverride` and loads `TAOMFactionScreen` on TAOM's own `FactionSelectionVM` (Kysaro's
  module set a private static and called TAOM's methods by reflection). Confirming a faction selects its
  region and confirms its culture through the faction map's own path. Any failure returns null and the
  faction map shows instead. In a character creation where the screen is shown it takes Player
  Switcher's place (Mike, 2026-10-01: Kysaro's design): `SetSuppressedForCharacterCreation(true)` turns
  the switcher off until the screen declines or the game ends. Quitting to the main menu from the
  screen pops the character-creation state without closing the culture stage, so `SubModule.OnGameEnd`
  calls `ResetForGameEnd`, which lets go of the screen and the pick and gives the switcher back.
- **Hero picks.** `FactionPresetService` keeps Kysaro's outcome (Mike, 2026-10-01): a lord or leader
  ends up on the player with name, body, race, gear and skills; a named card or a wanderer without the
  name. Mike's Option A sets when: the look and gear are copied when the face generator is built (its
  constructor prefix also dresses the model in the pick's gear), and the name and skills only from the
  finalize handler at priority 1060, after TAOM's 1050, where the look is copied again over the stages
  since. Backing out to "Custom Character" or another faction puts back the look the character had
  before the first pick, and nothing else was written. TAOM's culture race filter (Patch9) leaves the
  face generator's race alone while a pick is active. The pick is dropped when a character creation
  starts and once it has been applied, so a second campaign in one process starts clean.
- **Legends.** Eight picker-only characters in `characters/faction_ui_picker_characters.xml`
  (`taom_fui_gandalf`, `_isildur`, `_brand`, `_bard`, `_tauriel`, `_thorin`, `_azog`, `_gilgalad`):
  `is_hero="false"`, occupation `NotAssigned`, hidden from the encyclopedia, never spawned, each with an
  18-skill sheet and its own kit. Saruman (`lord_I1_0`), Bolg (`lord_MM6_5`) and the Nazgul card
  (`lord_1_155`) point at existing lords.
- **Roster.** `FactionRoster` makes every choice about characters on the screen (which troop the 3D
  viewport shows, the alive lords and the ruler of the faction's kingdom, the culture's wanderers, when
  the Leader tab shows, which faction a minimap click lands on) over `IFactionRosterAdapter`.
- **Camera.** `FaceGenCameraService` knows which themed screen owns the camera: the face generator sets
  it in its constructor prefix (it sets up its camera before its movie loads), the other three from
  their movie load (they load the movie first). The `InitCamera` patch moves the camera and widens the
  field of view by that screen's offsets.
- **Widgets.** Gauntlet builds widgets with only a `UIContext`, so the custom ones
  (`UI/Widgets/`) read services from `FactionUIWidgetContext`, set once at load. Fixed labels use
  `FrontEndTextWidget LocalizedText="{=key}English"`, which resolves through `TextObject`; the slider
  filter compares against vanilla's own localized label; the backstory skill icons are keyed on the
  option's localized text and rebuilt when the language changes. Kysaro matched English strings in all
  three places. The backstory Random button picks an option and presses Next once per click.

The patches are Patch95_FactionUI ([registry](../reference/harmony-patch-registry.md)), eight classes,
applied in `OnSubModuleLoad` because the splash, the main menu and the loading window all appear before
any campaign. Two of Kysaro's per-frame patches are gone: the loading window gets its fonts, stone bars
and brushes registered before its movie is built (so its prefab names them directly), and the
menu-music silence is lifted from `FactionUITicker` when the player leaves the main menu, not from a
prefix on `MBMusicManager.Update`.

**Not ported:**

| Kysaro's piece | Why |
|---|---|
| `LoadingQuote_Patch` (a quote on the loading-screen footer) | off in his shipped `mainmenu.json` (`"loading_quotes": false`, "user asked to remove the quotes") |
| `FaceGenScene_Patch` (neutral lights, fog and a backdrop image in the face generator scene) | off in his shipped `facegen_scene.json` (`"enabled": false`) |
| `FaceGenSceneSwap_Patch` (his `character_menu_new` scene) | the scene's prop `kys_mordor_castle_prop_01` and material `t_mordor_tileable_black_iron` are in no installed package; asked of Kysaro |
| The `TAOM.CharacterDisplay` overlay suppressor | no such module in TAOM or its install; asked of Kysaro |
| `DeactivateMenuMode` prefix | vanilla already does what it did (see the registry) |
| `faction_text_overrides.json` | patched TAOM's faction text by array index; the vanilla culture names it covered were fixed in `factionmap/factions.json` itself |

### Component Diagram

```
Main/_Module/GUI/FactionUI/{RuntimeSprites,LoadingScreens,RuntimeFonts}   Main/_Module/Videos/FactionUI/
        |                                                                  |
FrontEndSpriteService (image groups)  <-  FrontEndMovieService (which movie)   MenuMediaService (video, splash, music)
        |                                         |                                  |
IFrontEndResourceAdapter (sprite table,     Hooks/GauntletLayerLoadMoviePatch  Hooks/VideoPlayerViewPlayVideoPatch,
 textures, brushes, fonts)                  Hooks/UIResourceManagerRefreshPatch     VideoPlaybackState*, MBMusicManager*
        |                                         |
LoadingImageService (one loading picture)   FactionUITicker (release, music, effects; SubModule.OnApplicationTick)
        ^
Hooks/LoadingWindowViewModelLoadingImageNamePatch

ICultureStageMovieOverride -> FactionScreenLauncher -> FactionScreenVM -> FactionRoster, FactionPresetService
                                                                               |              |
                                                                 IFactionRosterAdapter   IPresetAppearanceAdapter
```

## Configuration

### MCM: "Menus & Loading Screens"

| Setting | Default | Effect |
|---|---|---|
| Themed Main Menu | on | Kysaro's main menu; the next time the menu opens |
| Hide Game Version | on | hides the version line on the main menu (the TAOM log still records it); after a restart, because the game builds that line once |
| Main Menu Video | on | the Sauron video behind the main menu; the next time the menu opens |
| Mute Menu Music Under The Video | on | keeps the vanilla theme off the main menu while the video plays (needs Main Menu Video); vanilla's music returns once you leave the main menu |
| Themed Loading Screens | on | Kysaro's paintings, never the same twice in a row; from the next loading screen |
| Themed Skill Icons | on | on applies at the next main menu; off applies after a restart |

The splash and the loading window's frame are not settings: both appear before MCM has loaded the
player's choices.

### MCM: "Menus & Loading Screens / Character Creation"

All on by default; each applies the next time its screen opens. Each tooltip says so.

| Setting | Off means |
|---|---|
| Themed Faction & Hero Picker | TAOM's faction map, and Player Switcher stays available |
| Themed Face Generator | vanilla face generator (no camera offsets; a pick still dresses the model) |
| Themed Backstory Screens, Review Screen, Banner Editor, Clan Naming Screen, Options Screen | that stage's vanilla screen |

### Config files: `Main/_Module/ModuleData/FactionUI/`

Every file is read once per launch (restart after editing), keys starting with `_` are notes, and a bad
entry is skipped with a `[FactionUI]` warning, then one summary warning, while the rest load.

| File | Holds |
|---|---|
| `sprite_overrides.json` | each skill-icon image (`gui_skills_icon_*`) and the vanilla sprite it replaces; read when the main menu opens |
| `facegen_camera.json` | per screen (`FaceGen`, `Narrative`, `Review`, `Options`): `x_offset`, `distance_offset`, `fov_offset`, each range-checked |
| `faction_characters.json` | faction key to the character the 3D viewport shows; missing means the culture's elite infantry troop |
| `faction_kingdoms.json` | faction key to the kingdom whose ruler and lords the browse lists show |
| `faction_map_pos.json` | faction key to its minimap pin, `[x, y]` in 0 to 1 (Kysaro placed all 20 in game) |
| `faction_viewport.json` | faction key to a viewport offset, or an object of `offset`, `hide_weapons` and `race` |

A key naming no playable faction is reported once. `FactionUIConfigIdsTests` pins every faction key and
character id in these files against the shipped data.

## Key Files

| File | Purpose |
|---|---|
| `Main/Features/FactionUI/Resources/FrontEndSpriteService.cs` | Image lifetime: groups, holds, deferred release, the refresh re-registration |
| `Main/Features/FactionUI/Resources/FrontEndSpriteCatalog.cs` | Name to kind, `.nine` border parsing |
| `Main/Features/FactionUI/Menus/FrontEndMovieService.cs` | The movie swap table and which movie holds which groups |
| `Main/Features/FactionUI/Menus/MenuMediaService.cs` | Menu video, splash, the menu-music latch |
| `Main/Features/FactionUI/Menus/LoadingImageService.cs` | Loading picture rotation |
| `Main/Features/FactionUI/UI/MainMenuWidgets.cs`, `ShimmerSweep.cs` | Title shimmer; the featured new-game entry, found by `MainMenuCustomizerService.NewGameName` |
| `Main/Features/FactionUI/FactionUITicker.cs` | Once-per-frame work, from `SubModule.OnApplicationTick` |
| `Main/Features/FactionUI/FactionUISettingsProvider.cs` | The thirteen MCM toggles, mapped by name |
| `Main/Features/FactionUI/FactionScreen/` | Launcher, catalog, roster, art table, the four tuning files |
| `Main/Features/FactionUI/Presets/` | The pick and when it is applied; the priority 1060 finalize handler |
| `Main/Features/FactionUI/CharacterCreation/` | Camera offsets, backstory skill icons |
| `Main/Features/FactionUI/UI/` | Live effects, the faction screen's view models, custom widgets |
| `Main/Features/FactionUI/Hooks/` | Patch95_FactionUI entry points |
| `Main/Features/FactionMap/Hooks/ICultureStageMovieOverride.cs` | The culture stage seam the faction screen plugs into |
| `Main/Adapters/FrontEndResourceAdapter.cs`, `FrontEndStateAdapter.cs`, `MenuMusicAdapter.cs` | Sprite table, textures, fonts, prefab lookup; game state and movie release; music mode |
| `Main/Adapters/PresetAppearanceAdapter.cs`, `FactionRosterAdapter.cs`, `TextLocalizerAdapter.cs` | Copying a pick onto the player; heroes, troops and wanderers; the active language |
| `Main/_Module/GUI/Prefabs/FactionUI/` | Kysaro's 23 prefabs |
| `Main/_Module/GUI/Brushes/TAOM{MainMenu,Loading,CharCreation,FactionScreen}.xml` | Kysaro's brushes |
| `Main/_Module/ModuleData/characters/faction_ui_picker_characters.xml` | The eight picker-only legends |

## Dependencies

- `IPathService` (Core): the module root and ModuleData paths.
- `TaomSettings` (MCM), read through `FactionUISettingsProvider`.
- FactionMap: the culture stage, `FactionSelectionVM`, `IFactionSelectionService`.
- Player Switcher: `IPlayerSwitchPolicyProvider.SetSuppressedForCharacterCreation` while the faction
  screen is shown.
- CharacterCreation: the picks are applied after its finalize (1050); its race filter (Patch9) skips a
  face generator showing a pick.

## Data changes made with the port

Kysaro's data asks (`TAOM_special_character_requests.md`), as decided on 2026-10-01:

- Own gear sets for Elrond, Arwen, Haldir, Celeborn and Lurtz, who were dressed from shared faction
  templates; Aragorn now wears Faramir's kit without the helm, keeping his own sword.
- Kysaro's hair and beard tags for eight lords are not applied. A hero's face comes from the fixed key
  in his body properties (`Hero.Deserialize`, `CharacterObject.GetBodyProperties` for a hero), and the
  engine reads hair and beard tags only for characters without one, so tags cannot change a lord's
  look. A one-lord trial on Denethor II was added and then reverted for that reason. If a lord looks
  bald, the hair index inside his key is the cause, which is a face-key edit.
- The Mouth of Sauron keeps TAOM's helm (Mike).
- Eomer (`lord_4_3_1`) is defined in both `lords.xslt` and `characters/lords.xml`. The engine merges the
  two today (same equipment ids, `lords.xml` wins the scalars), but if their equipment ids ever diverge
  the engine would union both kits. Reported, not changed.

## Tests

- `TAOM.Tests/Features/FactionUI/FrontEndSpriteServiceTests.cs`: group holds and delayed releases, art,
  resident assets, skill icons, loading pictures, re-registration after the engine rebuilds its tables.
- `FrontEndMovieServiceTests.cs` (swap table, holds per movie object, missing prefabs),
  `FrontEndSpriteCatalogTests.cs`, `MenuMediaServiceTests.cs`, `LoadingImageServiceTests.cs`,
  `ShimmerSweepTests.cs`, `FrameTimeTests.cs` (a NaN frame time never moves an effect).
- `FactionPresetServiceTests.cs`: Option A (look at the face generator, identity at finalize), the
  restore on un-pick, the second campaign, Custom Character; `FactionPresetHandlerPriorityTests.cs`:
  1050 < 1060 < 1100.
- `FactionScreenLauncherTests.cs` (Player Switcher suppression, a missing prefab, the game-end reset), `FactionRosterTests.cs`,
  `FactionScreenCatalogTests.cs`, `FactionScreenConfigProviderTests.cs` and `FaceGenCameraServiceTests.cs`
  (one test per validation rule), `NarrativeThemeIconMapTests.cs`, `FactionUIConfigIdsTests.cs`.
- `FactionScreenPrefabBindingTests.cs`: every binding in `TAOMFactionScreen.xml` resolves on the view
  model it binds against, and every bound view-model property is bound there.
- `FactionUIPrefabLocalizationTests.cs`: `LocalizedText` only on the two widgets that resolve it, and every
  key registered with the same English.
- `FactionUISettingsProviderTests.cs`: each MCM toggle reaches its own setting and no other.
- `FactionUIWiringTests.cs`: the feature registers every adapter its services take, every service
  SubModule resolves builds from it, the culture-stage seam and the game-end reset share one launcher,
  and `IoC.cs` registers Player Switcher and the faction UI before FactionMap.
- `FactionUIBindingTests.cs`: every Patch95 target and its parameter names against the installed engine,
  the reflected members, the `MusicMode` values, the category on all eight classes, and SubModule's
  registration. `ReflectionSiteBindingTests` carries the four reflected members as gate rows.
- Outside the folder: `CultureStageViewFinalizeHookTests.cs` (FactionMap), the suppression tests in
  `PlayerSwitchPolicyProviderTests.cs`.

## How to add a loading screen

1. Drop a PNG (1920x1080 is enough; 3840x2160 costs 31.6 MB while shown) into
   `Main/_Module/GUI/FactionUI/LoadingScreens/`.
2. No code change: the folder is listed the first time a loading screen opens.

## Performance

In the campaign the resident group stays loaded (the fonts, three 2048x2048 sheets, 48 MB; the
loading-window frame; the skill icons), plus the loading painting shown last, which stays until the next
loading screen replaces it (up to 31.6 MB for a 3840x2160 painting; six of the ten are that size).
Everything else goes 60 ticks after the last themed screen is released; check the
`[FactionUI] front-end images released` log lines after entering the campaign map.

## Status and owed work

Every stage is ported. Mike deployed the first build on 2026-10-01 ("it looks fantastic"). A nine-lens
`/deep-review` plus a deploy audit followed, then a convergence pass (PASS, nine LOW fixed) and a Codex
pass (PASS, two P2: one bounded, one documented below); all their fixes are built and unit-tested but
**not yet deployed**. The new strings were translated into all twelve languages by the same day's
translation run (AI first drafts, like every TAOM language). Owed:

- **In game (Mike):** the checklist below, on the fixed build.
- **Kysaro:** whether the finalize copy should overwrite the name typed on the review screen and the
  player's face edits; what `TAOM.CharacterDisplay` is; the source code; the Ringbearer font licence;
  the mesh `kys_mordor_castle_prop_01` and material `t_mordor_tileable_black_iron` for his replacement
  `character_menu_new` scene; the `cc_section` image (two brushes draw `cc_card` instead). Before he
  tests TAOM, his own TAOM_FactionUI module must be removed: both patch the same targets.

### Known limitations

- **A runtime module load on the main menu.** The engine rebuilds every open movie under its old
  identifier when a module loads at runtime, which it allows only while the main menu is active. The
  main menu's title shimmer and its featured new-game entry then keep pointing at the replaced widgets,
  so they stop until the menu next opens. It cannot happen on the faction screen or the face generator.
- **A themed prefab that throws while building.** The vanilla screen is shown instead, and that themed
  screen is not tried again until the game restarts: the engine keeps a movie that failed to build
  referenced by its resource factories, with no way for TAOM to release it, so at most one is retained
  per broken prefab.
- **After a Custom Battle round trip** the vanilla theme may play under the menu video (step 12).
- **The last loading painting** stays in memory until the next loading screen (see Performance).

### In-game checklist

1. **Before launching the fixed build:** deploy with `./build.ps1`, then delete what earlier deploys
   left behind, since a deploy copies files but deletes none: `Modules/TAOM/Videos/initial_menu/` (vanilla
   plays any module's video from that folder, so this copy would show the Sauron video under the vanilla
   menu even with Main Menu Video off), `Modules/TAOM/Videos/splash/`, and
   `Modules/TAOM/GUI/Prefabs/FactionUI/TFG{Eyes,Face,FacePreset,Mouth,Nose}.xml`.
2. Launch: the splash plays, the themed main menu shows with the video and no menu music, no version
   line, and the log has a `[FactionUI] front-end images loaded: main menu` line.
3. MCM toggles: Themed Main Menu and Main Menu Video off show vanilla's menu and video the next time the
   menu opens; Hide Game Version off brings the version line back after a restart.
4. Loading screens: a painting each time, never the same twice in a row; off shows vanilla's.
5. New campaign: the faction screen replaces the faction map; each faction shows its portrait, pin and
   emblem tile; the minimap opens and its pins select factions.
6. Pick a lord from the Lords list, confirm: the face generator shows him in his gear; finish creation;
   the player has his name, face, race, gear and skills. Repeat with a legend card (Gandalf: his kit and
   skill sheet) and with Custom Character. Check Aragorn (Faramir's kit, no helm, his own sword) and
   the new kits of Elrond, Arwen, Haldir, Celeborn and Lurtz.
7. Back out of the face generator to the faction screen and choose Custom Character: the earlier look
   comes back, and the lord's name and skills never land on the player.
8. Walk every stage with each Character Creation setting on and off: the camera framing on the face
   generator, backstory, review and options screens; on the DETAILS tab, the face, eye, nose and mouth
   sections lock when vanilla locks them and the eyebrow and mouth arrows step through their options;
   the Random button picks an option and moves on one screen per click.
9. In a non-English language: the labels, the Voice Pitch row and the backstory icons all work. Kysaro's
   three bitmap fonts carry Latin-1 only (199 glyphs each), so Polish, Turkish, Cyrillic and CJK text
   falls back per character to the language's default font: mixed typefaces are expected, blanks are not.
10. In the campaign: the barber and the clan banner editor are vanilla; `[FactionUI] front-end images
    released` lines appear; Player Switcher is offered only when the faction screen setting is off.
11. Quit to the main menu from the faction screen, then start again with the faction screen off: Player
    Switcher is offered. Start a second campaign without restarting: no pick carries over.
12. Known limitation to note, not fix: open Custom Battle from the main menu and come back; the vanilla
    theme may now play under the menu video (Kysaro's module behaved the same).

## Changelog

- 2026-10-01: stages 1 and 2 ported from the decompiled TAOM_FactionUI v0.1.0 (#704).
- 2026-10-01: character-creation reskins, the faction and hero picker, hero presets, the eight legends,
  Kysaro's tuning files and the data asks ported (#704).
- 2026-10-01: deep-review fixes: images released per held movie and per tick (the `ReleaseMovie` prefix
  never fired), the engine table refresh handled, Option A for hero picks, Player Switcher suppressed per
  character creation and reset on game end, Patch9 left alone during a pick, the dead `DeactivateMenuMode`
  prefix and five unused prefabs removed, config validation and binding, wiring and reflection gates added.
- 2026-10-01: convergence and Codex fixes: gates that could not fail replaced, the faction screen declines
  when its prefab is missing or after any failure, a themed build that threw is not retried, the camera
  config's summary counts ignored entries, a declined screen clears any pick, memory claims corrected.

## GitHub Issue

- **Issue:** #704, [Merge Kysaro's faction screen and front-end reskin (TAOM_FactionUI) into TAOM](https://github.com/haterade22/TAOM/issues/704)
- **Status:** Open
