# Advanced Start Options (ASO) adaptation

**Status:** written for the v1.5.0 engine bump (2026-08-19, archived branch), landed on the v1.5.x line against
Bannerlord v1.5.2 on 2026-09-14. In-game check 2026-09-15 on v1.5.3 found the menu strings in the wrong
file (#604, fixed the same day, see section 3); the picker and the Escape-menu summary smoke are owed again.
**Code:** [`Main/Features/AdvancedStartOptions/TaomStartOptionsProvider.cs`](../../Main/Features/AdvancedStartOptions/TaomStartOptionsProvider.cs) ·
[`Main/Features/LocalizationOverride/GlobalStringsOverrides.cs`](../../Main/Features/LocalizationOverride/GlobalStringsOverrides.cs)
**Tests:** `TaomStartOptionsProviderTests` (the kingdom list matches `taom_spkingdoms.xml`; the provider binds the
way `AdvancedStartOptionsManager.Initialize` binds it, on the installed engine; each string family sits in the
file its reader loads) · `GlobalStringsOverridesTests` (the append-first-wins fact and the replace, on the installed engine).
**Related:** [`rca-v1.5.2-compile-2026-09-14.md`](../reviews/rca-v1.5.2-compile-2026-09-14.md) · the v1.5.2 entries in `CHANGELOG.md` · #604

## What ASO is

Bannerlord v1.5.0 added a pre-campaign options screen: a **campaign scenario** (Default, United
Empire, Invasion, Last Stand, 2 Faction War), a **player start** (Default, King, Vassal, Mercenary,
Trader, Outlaw, Beggar), stacking **modifiers** (Bandit Surge, Civil Unrest, Recruit Shortage, Swift
Travel), and a shareable **seed**. It runs from the main menu before the campaign exists, so it does
not interact with TAOM's character-creation stage sequence at all (verified at v1.5.0:
`InitializeCharacterCreationStages` was byte-identical between v1.4.8 and v1.5.0; the v1.5.2 re-port re-verified the
extension point, `[StartOptionsProvider]` on a static `void (AdvancedStartOptions)` bound by
`Delegate.CreateDelegate`, unchanged).

## Why TAOM needs to adapt it

**ASO's faction pickers are hardcoded, not data-driven.** `SandBoxStartOptionsProvider.GetCultureItems()`
returns a literal list of the eight vanilla StringIds. TAOM keeps those eight ids and renames the
kingdoms in place via `spkingdoms.xslt`, then adds fourteen more in `taom_spkingdoms.xml`. Left
alone, the menu therefore offered "Western Empire" and dropped the player into Gondor, and TAOM's
fourteen LOTR kingdoms were invisible.

| ASO showed | Actually is |
|---|---|
| Northern Empire (`empire`) | Dunland |
| Western Empire (`empire_w`) | Gondor |
| Southern Empire (`empire_s`) | Mordor |
| Sturgia / Aserai / Vlandia / Battania / Khuzait | Dale / Harad / Rohan / Khand / Rhun |

## What TAOM does

`TaomStartOptionsProvider` uses the engine's own extension point rather than a Harmony patch.
`AdvancedStartOptionsManager.Initialize()` reflects over every active game assembly with
`BindingFlags.Static | Public | NonPublic` and binds any method carrying `[StartOptionsProvider]`
whose signature is `void (AdvancedStartOptions)`.

### 1. The United Empire scenario is removed

Two independent reasons, either sufficient:

- It builds a kingdom from a hardcoded `TextObject` literally named **"Calradian Empire"** with
  StringId `calradian_empire`, merging whichever factions hold the three imperial ids. In TAOM that
  is Gondor, Mordor and Dunland in one realm.
- **Two of its three unifier choices are broken.** Both `HandleKingdomCleanup` and `ResolveKingdom`
  branch on `Culture.StringId == "empire"`. In vanilla all three imperial kingdoms satisfy that; in
  TAOM only Dunland does, because `spkingdoms.xslt` gives `empire_w` the `gondor` culture and
  `empire_s` the `mordor` culture. Picking Gondor or Mordor as unifier therefore skips both the
  deactivation and the redirect, leaving the player ruling an empty, fief-less shell beside the real
  merged kingdom.

### 2. TAOM's fourteen kingdoms are added to every faction picker

`KingdomId`, `LastStandKingdomId`, `InvasionScenarioFactionId`, `TwoFactionWarFaction1Id`,
`TwoFactionWarFaction2Id`.

Small kingdoms are added too. `GiveStartingFiefs` degrades rather than throwing: it falls through to
`FindFallbackStartingTown`, which guards every step with `if (list.Count > 0)`. **Lindon** and
**Goblin-town** own one town and no castle, so a Vassal start there takes the fallback instead of
receiving a castle. That is a gameplay quirk, not a crash.

### 3. The menu is re-localised, in two files, and one of them needs a runtime re-apply

49 rows, two id families, two readers, two files. Which file a row sits in is the whole of #604.

| Family | Reader | Manager it reads | File |
|---|---|---|---|
| `str_campaign_starting_options_item_name.<id>` (the list entry, and the `{Token}` interpolation in scenario text via `AdvancedStartOptions.SetTextVariables`), `_description.<option>`, `_item_description.<item>` | the main-menu screen: `AdvancedStartOptions.cs:62/73`, `AdvancedStartOption.cs:77`, `ListAdvancedStartOption.cs:64/70`, the category and locked-reason lookups in `SandBox.ViewModelCollection` | `Module.CurrentModule.GlobalTextManager` | **`global_strings.xml`** (26 rows) |
| `str_advanced_start_value_name.<id>` (the in-game Escape menu's starting-options summary, `AdvancedStartData.GetDisplayName`) | `GameTexts.FindText` | `Game.GameTextManager` | **`taom_module_strings.xml`** (23 rows) |

**The two managers are different objects with different loaders.** `Module.CurrentModule.GlobalTextManager`
(Module.cs:110) is filled once by `GameTextManager.LoadDefaultTexts` (Module.cs:278, before any submodule
loads) from the literal path `ModuleData/global_strings.xml` of every module, plus Native's consoles.xml,
and from nothing else (GameTextManager.cs:132-138). Every `<XmlName id="GameText">` in SubModule.xml feeds
`Game.GameTextManager` (Game.cs:297, `LoadGameTexts`), which does not exist at the main menu. The 26
menu-facing rows spent 2026-09-14 to 2026-09-15 in `taom_module_strings.xml`, and the screen showed
`ERROR: Text with id str_campaign_starting_options_item_name doesn't exist! Variation: rivendell` for every
TAOM kingdom and "Sturgia" / "Khuzait" / "Aserai" for the renamed vanilla ones. `global_strings.xml`'s
header had documented the rule for keybinding names since the time-acceleration work; the ASO block did
not follow it.

**Moving the rows was necessary, not sufficient.** `LoadDefaultTexts` opens each file raw
(`StreamReader` + `XmlDocument.LoadXml` + the private `LoadFromXML`): no `MBObjectManager` merge, no
XSLT, no id-keyed override. Each row goes through `GameText.AddVariationWithId`, which APPENDS a same-id
variation whose text differs, and `GameText.GetVariation` is a first-match scan. Native loads first, so
TAOM's `sturgia` -> "Dale" would trail Native's "Sturgia" and never be returned; the 14 TAOM-only ids would
resolve and the 8 renamed kingdoms plus the 4 Calradia rewrites would not. The engine's replace primitive
is the public `GameText.SetVariationWithId`, which overwrites the first same-id variation in place.
`GlobalStringsOverrides` parses TAOM's own `global_strings.xml` the way `LoadFromXML` does (id split once
on `.`) and re-applies every row through it, called once from `SubModule.OnSubModuleLoad` right after the
Patch25 override loader. `GlobalStringsOverridesTests` pins both facts on the installed DLLs, so if
TaleWorlds ever makes `AddVariationWithId` replace, the pin fails and the class becomes dead weight to
delete rather than unexplained belt-and-braces.

**The per-Game manager needs none of this.** Its `GameText` XMLs go through `MBObjectManager.MergeElements`,
keyed on `@id` per `GameText.xsd`, and `MergeElementAttributes` overwrites `text`, so the 23 value-name
rows override vanilla's by the ordinary merge and stay where they were. That is also why an XSLT is the
right tool for in-game string overrides (`module_strings.xslt`) and the wrong one here.

Scenario text that names Calradia directly is retitled to Middle-earth (the four rewrite rows, also in
`global_strings.xml`). Translations are unaffected by the split: `{=taom_aso_*}` keys resolve through the
language files regardless of which source XML declares them, and `LanguageFileCoverageTests` unions per
language for exactly that reason. The rows still sit in `std_taom_module_strings_*.xml`; the next
`translate_with_claude.py --sync-ids` run re-homes them into `std_taom_keybind_strings_*.xml` on its own
(`rebuild_translation_files.py` does not list `global_strings.xml`, a pre-existing gap noted in the
2026-09-15 CHANGELOG entry).

## Gotchas

**The item-condition delegate answers "is this DISABLED", not "is it enabled".** Vanilla's
always-available helper is named `GetNeverDisabledItem` and returns **`false`**. Returning `true`
from a condition greys the item out. This is the single easiest thing to invert here.

**`RemoveItem` exists but is easy to miss.** `ListAdvancedStartOption` exposes `GetItems()` returning
an `IReadOnlyList`, which reads as immutable, but `AddItem` and `RemoveItem` both mutate the private
backing list. `AddItem` overwrites an existing entry with the same identifier rather than duplicating.

**Adding a picker item without a string leaves the ERROR sentence on screen.** ASO looks each item up by
id through `GetListItemName`, which is `FindText`, so a new kingdom needs both string rows or the menu
shows `ERROR: Text with id str_campaign_starting_options_item_name doesn't exist! Variation: erebor`, and
the scenario description interpolates that whole sentence where the kingdom name should be.

**Put the menu-facing row in `global_strings.xml`, never in a GameText XML, and reuse a vanilla variation
id only knowing `GlobalStringsOverrides` is what makes it win.** A `str_campaign_starting_options_*` row in
`taom_module_strings.xml` looks right, passes every scan of that file, and is never read.
`TaomStartOptionsProviderTests.NoMenuFacingAsoRow_LivesInTheGameTextXml` fails on one. Audit of every
other id family vanilla reads through `GlobalTextManager` (`str_key_*`, `str_hotkey_*`, `str_options_*`,
`str_ok`, `str_option_*`, `str_gpu_*`, `str_dlc_*`, `str_content_*`): none is overridden from a GameText
XML, so the ASO family was the only misplacement (2026-09-15).

## Interactions with the rest of TAOM

- **Player start applies at character-creation phase 8.** TAOM's phase-9 handlers
  (`SpecialResourcesBehavior`, `PlayerPossessionBehavior`, `StartupResourcesBehavior`) deliberately run
  after it. Verified: across CampaignSystem, SandBox and StoryMode the only indices any subscriber
  uses are 1 and 8, so 9 is both reachable and last.
- **King / Vassal / Trader / Beggar starts overwrite equipment and gold.** TAOM's career starting
  equipment survives only on Default, Mercenary and Outlaw. The startup-gold re-apply is explicitly
  gated on the default start so ASO's own values are not clobbered.
- **Civil Unrest** (the engine calls it High Rebellion) works by swinging vanilla's loyalty thresholds,
  which `TaomSettlementLoyaltyModel` overrides. The high-rebellion pair lives in
  `revolt_tuning_config.json` (see [`configs-balance.md`](../modding/configs-balance.md)) so the modifier
  stays meaningful under TAOM's lower thresholds.
- **Recruit Shortage** and **Swift Travel** both land, because `TaomVolunteerModel` and
  `TaomPartySpeedModel` call `base.` first.

## Not supported

**War Sails / NavalDLC.** Its start-options provider is what registers the Nord Invasion scenario and
the Fleet Admiral / Merchant Venturer starts, and `OnNordInvasionScenarioSelected` dereferences a
`"nord"` kingdom TAOM does not have. `Main/_Module/SubModule.xml` declares an `<IncompatibleModules>`
block, so the pairing is refused rather than left to chance.

## Owed

- In-game (owed again after #604): open the ASO screen, pick Last Stand, and confirm the Last Standing
  Faction list reads Dunland / Gondor / Mordor / Dale / Harad / Rohan / Khand / Rhun then Erebor through
  Goblins of Blue Craig with no ERROR row, the description names the chosen kingdom, the Invasion and
  Randomized texts say Middle-earth, and United Empire is absent. Then start that campaign and open the
  Escape menu's starting-options summary to confirm the in-game value names still resolve.
- A translator run for the 49 new string rows (they exist in all 12 languages as English fallback; parked
  with the other pending runs in #579).
- Decide whether Lindon and Goblin-town should be excluded from the Vassal start rather than falling
  back, once the fallback has been seen in play.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reference/feature-map.md](../reference/feature-map.md)

<!-- backlinks-end -->
