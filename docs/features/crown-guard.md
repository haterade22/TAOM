# Crown Guard

## Overview

The two guards beside a new ruler in the become-king scene are troops picked for the kingdom's culture and wear its
colours: Gondor crowns its king between Fountain Guards, Mordor between Barad-dur Guards, and every kingdom culture
has a row.

## Why This Exists

- **Vanilla behavior:** `CampaignSceneNotificationHelper.GetBodyguardOfCulture` (public static, v1.5.5) switches on
  seven vanilla culture ids (battania, khuzait, vlandia, aserai, sturgia, empire, nord) and returns
  `new SceneNotificationCharacter(MBObjectManager.Instance.GetObject<CharacterObject>(id))`, with `fighter_sturgia`
  for any other culture.
- **Callers:** the one a TAOM campaign reaches is `BecomeKingSceneNotificationItem`, which asks for two guards of the
  new ruler's kingdom culture. `DefaultCutscenesCampaignBehavior.OnKingdomDecisionConcluded` raises it only when the
  player wins a king-selection vote, and the dev command `taom.play_coronation` (cheat mode) raises it on demand.
  The engine's other caller, `EmpireConspiracySupportsSceneNotificationItemBase`, is raised only by Story Mode's
  `DefeatTheConspiracyQuestBehavior`, whose new game TAOM hides; if it ran, it would get the same mapped guards.
- **TAOM requirement:** the coronation scene shows the new realm's own people.
- **Without this feature:** a Gondor, Mordor or Erebor ruler gets Sturgian fighters, and the reused vanilla ids get
  Calradian elites: Rohan a Vlandian banner knight, Dunland an imperial legionary, Rhun and Harad troops that TAOM
  marks obsolete.

## Architecture

### Design Challenge

The method returns a struct built from a troop lookup, and TAOM cultures have no branch in it. No GameModel offers the
seam (`CutsceneSelectionModel` has only `GetKingdomDestroyedSceneNotification`). A bool prefix could skip vanilla only
for mapped cultures, but it would skip the original for every other patcher and need a co-op veto classification; a
void postfix can leave vanilla's answer alone wherever TAOM has no row.

### Solution Approach

A Harmony postfix, Patch112, category `Patch112_CrownGuard`, applied at GameInit. The hook reads `culture.StringId` and
asks `CrownGuardMap.GuardTroopId` through `CrownGuardConfigProvider`, which validates the JSON once per process and
falls back to the built-in `CrownGuardMap.Default()`. If there is a troop id and the troop is loaded, `__result`
becomes a `SceneNotificationCharacter` of that troop with the crowned culture's `Color` and `Color2`. Otherwise
vanilla's result stands; a mapped troop that is not loaded logs one warning per culture id.

The colours matter for borrowed rows: the scene tints each guard with its own troop culture's colours unless the
character carries custom colours, so a guard borrowed from another culture would otherwise wear that culture's colours.
For a troop of the crowned culture the two are the same, so passing them changes nothing.

### Component Diagram

```
GetBodyguardOfCulture(culture)
   vanilla: id switch, default fighter_sturgia
   postfix: CrownGuardMap.GuardTroopId(culture.StringId) -> troop -> __result (troop, culture colours)
```

## Configuration

### Config File: `Main/_Module/ModuleData/crown_guard/crown_guard_cultures.json`

`{ "cultures": { "<culture id>": "<NPCCharacter id>" } }`. Keys are trimmed and lower-cased; troop ids are trimmed and
keep their case. A row with an empty key, an empty or null troop id, or a key that repeats an earlier one after
normalizing is dropped with a warning. A missing, unparseable or null file gives the built-in map. A JSON edit needs a
game restart.

### Current Values

Each row is a troop from the top tiers of the kingdom's elite tree, infantry where the tree has some. Where a guard
troop sits one tier below the top, the row takes the guard: Rohan's King's Royal Guard (below the Riders of Rohan),
the Gundabad and Goblin Mountain Guards (below Bolg's Ironfang), and Khand's Ash Shieldguard (below the Obsidian
Shieldmaster, which guards Rhun).

- **Borrowed troops:** two cultures own no line troops of their own culture and field another's tree, so their row
  borrows it: Khand (`battania`) guards are `dragon_wrath_ash_shieldguard` (Rhun), Abanissa guards are
  `harad_serp_guard` (Harad). They wear the crowned culture's colours. Lothlorien guards are its own
  `lorien_gl_mallorn_defender` since 2026-11-05.
- **Shared troops:** Harad (`aserai`) and Abanissa share `harad_serp_guard`.
- **Mounted troops on foot:** Rohan's row is a cavalry troop. The scene draws a horse only when the character asks for
  one, and the postfix does not, so every guard stands on foot.
- **No trolls:** Mordor's top troops are the armoured and hill trolls, but Mordor's townsfolk are uruks. A troll guard
  would be the only troll in the scene, a race the become-king render path has never drawn there, while an uruk guard
  matches what the scene already renders. The row uses the Black Uruk Barad-dur Guard, and a shipped-data test rejects
  any troll race.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/CrownGuard/Hooks/CampaignSceneNotificationHelper_GetBodyguardOfCulture_CrownGuard_Patch.cs` | The postfix |
| `Main/Features/CrownGuard/CrownGuardMap.cs` | The map, its lookup and its built-in default |
| `Main/Features/CrownGuard/CrownGuardConfigProvider.cs` | Validating JSON loader |
| `Main/Features/CrownGuard/Cheats/CrownGuardCheats.cs` | `taom.play_coronation`, the on-demand scene |
| `Main/Features/CrownGuard/CrownGuardModule.cs` | The provider registration and the patch category |
| `Main/_Module/ModuleData/crown_guard/crown_guard_cultures.json` | The shipped rows |

## Dependencies

- **Patch56_SceneNotificationVisualGuard** (`Main/Features/HeroRace/Hooks/GauntletSceneNotification_OpenScene_Guard_Patch.cs`):
  the become-king scene's net for a character visual that fails to build. Many guards are not human, but each guard's
  race is the race of its culture's townsfolk, which the scene already renders, so Crown Guard adds no new race to that
  path.

## Tests

- `TAOM.Tests/Features/CrownGuard/CrownGuardMapTests.cs`: mapped, unmapped, null or empty, case and whitespace
- `TAOM.Tests/Features/CrownGuard/CrownGuardConfigProviderTests.cs`: one test per validation rule
- `TAOM.Tests/Features/CrownGuard/ShippedCrownGuardConfigTests.cs`: the shipped JSON equals the default, the map's
  keys are exactly the kingdom cultures, every troop is an `NPCCharacter` in the shipped data, no guard is a troll
- `TAOM.Tests/Features/CrownGuard/CrownGuardWiringTests.cs`: module listing, category id and phase, the hook's
  category, the singleton registration, no behavior, model or save data
- `TAOM.Tests/Features/CrownGuard/CrownGuardBindingTests.cs`: the category applies to the installed engine
- `TAOM.Tests/Features/CrownGuard/CrownGuardCheatsFormatTests.cs`: the report of `taom.play_coronation` (kingdom, culture
  id, the guard troop the scene draws, no troop); its console shape is covered by `ConsoleCommandBindingTests`
- Not unit-tested: the hook's own branch for a mapped troop that is not loaded (it reads the static
  `MBObjectManager.Instance`); the shipped-data test keeps every row's troop present.

## Adding a kingdom culture

Add a row to the JSON and to `CrownGuardMap.Default()` (a test keeps the two equal). The shipped-config test fails
until the map's keys equal the kingdom cultures in `taom_spkingdoms.xml` and `spkingdoms.xslt`.

## In-game check

Run `taom.play_coronation <kingdom>` (a kingdom name or id; no argument means your own kingdom) to open the coronation
scene without an election or a join; repeat per kingdom. The kingdom's ruler stands in the crowned spot and nobody is
crowned, so any save works. Or win a king election and look at the two guards in the coronation scene. Expect:

- troops of the row for that kingdom's culture, wearing its colours (check a Lothlorien, Khand or Abanissa ruler for
  the borrowed rows);
- each guard in its troop's first battle set, on foot, holding the scene's own vanilla spear, which the scene's spawn
  points put in every guard's right hand (vanilla's guards hold it too);
- founding a kingdom shows a different scene, which never calls this method.

Two findings that are not Crown Guard's were fixed beside it, so do not log them against it:

- the title line (#955): every kingdom culture now has a liege title, so the line reads the culture's own title
  ("Lord of Gondor", "Queen of Arthedain", "Dark Lord of Mordor") instead of the engine's "ERROR: Text with id
  str_liege_title doesn't exist!". The rows are `str_liege_title.<culture>` and `str_liege_title_female.<culture>` in
  `taom_module_strings.xml`, each tagged `taom_culture_<culture>` (the classes are in
  `Main/Features/LiegeTitles/TaomCultureTags.cs`). The coronation picks a row by its id suffix, while dialogs pick by
  tag, so a row needs both. The coronation keys on the kingdom's culture and dialogs on the ruler's own culture,
  so a ruler of another culture (an heir of Mordor ruling Dol Guldur) is crowned with one title and addressed with the
  other, as in vanilla. Dunland (`empire`) reads TAOM's "was hailed as Brenin of Dunland" line from
  `str_become_king_empire` instead of the Calradian emperor line;
- Mordor's ruler (#956): the `sauron` race now has `as_sauron_facegen` and `as_sauron_female_facegen` in the Armory
  (copies of the elf pair), so the scene no longer logs `Action set "as_sauron_facegen" could not be found`. His pose
  does not change: the ruler's `act_main_story_become_king` exists only in `as_human_warrior`, which both the engine's
  fallback set and the new set's `base_set` supply. Check the rgl log after `taom.play_coronation mordor`.

Not yet checked in game.

## Changelog

- 2026-10-20: added (#825).
- 2026-10-20: `taom.play_coronation [kingdom]` opens the scene on demand for the in-game check.
- 2026-10-20: liege titles for every kingdom culture and a Dunland coronation line (#955); `as_sauron_facegen` pair (#956).

## GitHub Issue

- **Issue:** #825 Scene popups show Calradian bodyguards beside Middle-earth rulers
- **Status:** Closed, `triage-needs-ingame`

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/dev-console.md](./dev-console.md)
- [docs/features/liege-titles.md](./liege-titles.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)
- [docs/reference/harmony-patch-registry.md](../reference/harmony-patch-registry.md)

<!-- backlinks-end -->
