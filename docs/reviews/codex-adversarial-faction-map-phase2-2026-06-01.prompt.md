# Codex Adversarial Review — Faction-Map CC Page Rewrite Phase 2

## Feature in one paragraph

The CharacterCreation "faction map" stage shows a per-faction sidebar page with Perks / Bonuses / Special Units / Strengths / Weaknesses. The pre-existing content was authored before TAOM's cultural-feats system existed and lied to the player about what the in-game effects were. This rewrite (issue #260) ships in three phases:

- **Phase 1** (commit `53ce308`, already shipped): `FactionDisplayHelper.cs::Localize` wraps every player-facing string flowing to the VM in `new TextObject(s).ToString()` so `{=KEY}default` strings resolve via TaleWorlds.Localization. Plain English passes through unchanged. 6 unit tests in `FactionDisplayHelperTests`.

- **Phase 2 main** (commit `cbbcc41`): full content rewrite of all 16 playable factions in `factions.json`, every player-facing string wrapped in `{=KEY}default` with the convention `taom_faction_<faction_json_key>_<section>_<index>`. Non-playable factions (29 stubs) get name + description + traits keyed in place. 599 new keys harvested into `taom_module_strings.xml` via a new `tools/harvest_factionmap_strings.py` tool. 20 new tests in `FactionMapDataTests`.

- **Phase 2 fix** (commit `7f0de78`, post-deep-review): `FactionSelectionService.FormatDifficultyText` was a Phase 1 holdover with 7 hard-coded English strings — now wrapped in `{=taom_faction_difficulty_N}default`. 7 hand-authored `<string>` entries added to `taom_module_strings.xml` above the auto-harvested block.

- **Phase 3** (next, not yet shipped): translation propagation to 11 AI languages via `tools/translate_with_claude.py`.

This review is adversarial on Phase 2 content + key alignment. Phase 1 helper code already passed prior review.

## TAOM ID CHEATSHEET

Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar

Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, aserai=Harad, khuzait=Easterlings/Rhûn, sturgia=Dale, battania=Khand

NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid — use "dolguldur". The JSON faction keys in factions.json (e.g. `stewardship_of_gondor`) link to the culture StringId via the `game_faction` field.

## READ FIRST

- `Main/_Module/ModuleData/factionmap/factions.json` — the rewritten content (full file).
- `Main/_Module/ModuleData/taom_module_strings.xml` — 599 new auto-harvested keys (in a marked block from `<!-- ===== FactionMap (CC) — auto-harvested ===== -->` to `<!-- ===== END FactionMap (CC) ===== -->`) + 7 hand-authored difficulty keys above the harvested block.
- `Main/Features/CulturalFeats/TaomCulturalFeats.cs` — the 97 FeatObject declarations. Source-of-truth for what each culture's feats actually do.
- `docs/features/cultural-feats.md` — feature doc inventory (97 feats organized by category).
- `Main/Features/FactionMap/FactionDisplayHelper.cs` — Phase 1 helper, `Localize` wraps strings in `new TextObject(s).ToString()`.
- `Main/Features/FactionMap/FactionSelectionService.cs` — Phase 2 fix: `FormatDifficultyText` now returns keyed strings.
- `Main/Features/FactionMap/FactionDataParser.cs` — Newtonsoft JObject-based parser; passes strings through verbatim.
- `tools/harvest_factionmap_strings.py` — the harvester (already reviewed by deep-review tooling agent; CRLF / line-ending H1 finding was a FALSE POSITIVE per the RCA).
- `TAOM.Tests/Features/FactionMap/FactionMapDataTests.cs` — 20 new data tests (per-faction DataRow, key coverage gate).
- `docs/reviews/rca-faction-map-phase2-2026-06-01.md` — RCA for the deep-review fix.

## Known Suspects (please CONFIRM or DISPUTE each)

1. **Content accuracy vs. shipped cultural feats.** For each of 16 playable factions, the Perks / Bonuses / Special Units / Strengths / Weaknesses sections should accurately reflect the `TaomCulturalFeats.cs` feats attached to that culture. Specifically:
   - Verify Isengard mentions: Uruk-hai Legions (+20% party), Industrial Might (+15% construction), Industrial Forges (−20% smithing), Orthanc Quartermasters (20-notable hub: 4M/2A/14GL), Saruman's Grip (+25% relationship penalty, negative).
   - Verify Dol Guldur mentions: Shadow Command (−50% army influence), Dark Conscription (+20% militia veteran), Dark Legions (+20% party), Shadow Captains (20-notable hub: 3M/2A/15GL), Voracious Hordes (+10% food consumption, negative), Ruinous Works (−20% construction, negative).
   - Verify Mordor mentions: The Dark Lord's Will (−60% army influence cost), Sauron's Wrath (+25% raid), Creatures of the Dark (+10% night speed), Dark Tribute (+20% wages, negative).
   - Verify Rohan mentions: Horse-lord Heritage (−15% mounted recruit/upgrade), Riders of the Mark (−15% mounted wages), Cavalry Dependent (−10% speed when >50% infantry, negative).
   - Walk the other 12 factions and flag any feat that ships but is NOT mentioned, OR any UI text that does NOT correspond to a shipped feat.

2. **XSLT-wrapped culture feat inheritance.** Dale (sturgia), Khand (battania), Harad (aserai), Rhûn (khuzait), Dunland (empire), Rohan (vlandia) inherit vanilla DefaultCulturalFeats via the XSLT passthrough in `spcultures.xslt`. The new content should mention BOTH the inherited vanilla feats (player sees them in encyclopedia) AND the TAOM additions. Verify each:
   - Dunland: inherited Battanian +15% forest speed, +20% militia production, −15% construction speed; TAOM addition Hill Marchers (+10% plain), Hill-Tribe Levy (+5% party), Hill-Tribe Recruitment (+10% volunteer respawn).
   - Khand: inherited Battanian feats + TAOM Steppe Charioteers (+10% steppe).
   - Harad: inherited Aserai (caravan + desert hardiness) + TAOM Sons of the Sun (+10% desert), Haradrim Warbands (+5% party).
   - Rhûn: inherited Khuzait + TAOM Easterling Outriders (+10% steppe), Easterling Host (+5% party).
   - Dale: inherited Sturgian + TAOM Vale Traders (+10% plain).
   - Rohan: TAOM-only (vlandia inherited mounted feats overridden by TAOM rewrites).
   - Decompile `pwsh tools/taom-src.ps1 path TaleWorlds.CampaignSystem.GameComponents.DefaultCulturalFeats` to verify what the vanilla cultures actually ship.

3. **Special-units accuracy.** Each `special_units[]` entry's `name` should reference an actual elite troop in `Main/_Module/ModuleData/troops/troops_<culture>.xml`. Names used:
   - Gondor: Citadel Guard, Swan Knight, Ranger of Ithilien.
   - Mordor: Black Uruk, Cave Troll, Morgul Bowman.
   - Isengard: Uruk-hai Berserker, Uruk-hai Crossbowman, Warg Rider.
   - Dol Guldur: Dol Guldur Uruk, Shadow Warg Rider, Black Numenorean Captain.
   - Gundabad: Pale Orc Champion, Gundabad War Troll, Warg Outrider.
   - Umbar: Black Numenorean Captain, Corsair Raider, Haradrim Auxiliary.
   - Erebor: Iron Guard of Erebor, Iron Hills Crossbow, Veteran of Azanulbizar.
   - Rivendell: Noldor Blade-master, Rivendell Sentinel, Imladris Lancer.
   - Mirkwood: Mirkwood Sentinel, Silvan Spearwarden, Wood-elf Scout.
   - Lothlorien: Galadhrim Guard, Lorien Bow-master, Silver Sentinel.
   - Rohan: Rohirrim Royal Guard, Rohirrim Knight, Westfold Foot-archer.
   - Dunland: Dunlending Berserker, Hill-Tribe Spearman, Crebain Scout.
   - Dale: Black Arrow Ranger, Dale Lake-guard, Erebor-trained Smith-warrior.
   - Khand: Variag Horse Archer, Khand Charioteer, Variag Lancer.
   - Harad: Mumakil War Tower, Harad Mahud Bowman, Sun-burnished Horseman.
   - Rhûn: Easterling Kataphract, Easterling Bladelord, Rhun Horsewatch.
   These are lore-appropriate generic names. Grep the matching `troops_<culture>.xml` for each. If a troop name is fabricated and not in any TAOM troop tree, flag as MEDIUM (cosmetic — the UI shows the name as a hint, not an in-game ref — but the player expects to encounter the unit).

4. **JSON ↔ XML key alignment.** For each `{=key}default` token in `factions.json`, verify a matching `<string id="key" text="{=key}default"/>` entry exists in `taom_module_strings.xml`. The test `FactionMapDataTests.EveryFactionMapKey_HasMatchingStringInTaomModuleStrings` enforces this, but verify a sample of 10 keys end-to-end:
   - `taom_faction_stewardship_of_gondor_name`
   - `taom_faction_stewardship_of_gondor_perk_2_desc`
   - `taom_faction_dominion_of_mordor_bonus_4` (the night speed bonus)
   - `taom_faction_dominion_of_isengard_bonus_7` (Isengard 20-notable hub)
   - `taom_faction_overlordship_of_dol_guldur_perk_2_name` (Shadow Captains)
   - `taom_faction_kingdom_of_rohan_bonus_6` (Cavalry Dependent negative)
   - `taom_faction_difficulty_5` (hand-authored difficulty)
   - `taom_faction_havens_of_umbar_special_unit_0_name`
   - `taom_faction_kingdom_of_imladris_weakness_2`
   - `taom_faction_clans_of_dunland_strength_3`

5. **Key naming convention consistency.** The harvester uses keys of the form `taom_faction_<faction_json_key>_<section>_<index>` (or with sub-fields like `_perk_<n>_name` / `_perk_<n>_desc`). Verify no off-by-one underscores, no plural-vs-singular slippage (e.g. `_strengths_0` vs `_strength_0`), no inconsistencies between sibling factions. Compare 3-4 factions side-by-side.

6. **String token escaping safety.** Some bonus strings use Unicode minus (U+2212, "−") instead of ASCII hyphen-minus. The XML harvester's `xml_escape()` handles `& < > "` but NOT Unicode. Verify the file is correctly UTF-8 encoded and the engine's GameTextManager handles `−` (and other non-ASCII like `é`, `ó`, accented Tolkien names) correctly. Spot-check: do any default-text strings contain raw `&`, `<`, `>` characters that need escaping? Run `grep -E '[&<>]' Main/_Module/ModuleData/factionmap/factions.json` and report.

7. **Strength/weakness "+ " / "- " double-prefix safety.** `FactionDisplayHelper.ApplyResult` prepends `"+ "` to strengths and `"- "` to weaknesses AFTER the `Localize` resolution. Verify no strength/weakness STRING DEFAULT in factions.json already starts with `+ ` or `- ` (which would render as `+ + foo` or `- - foo`).

8. **Old hard-coded content fully removed.** The old Gondor page used "Dunedain Blood / Lords gain experience 10% faster", "Defense Boost", "Varies / Elite Units in Specific Regions". Confirm zero occurrences of these legacy strings in factions.json, taom_module_strings.xml, or any C# file (they should all be gone).

9. **Pre-existing helper coverage of new content.** `FactionDisplayHelper.Localize` (Phase 1) is now responsible for resolving 600+ keys per CC faction click. Are there any flows that BYPASS the helper and surface raw `{=KEY}default` strings to the prefab? Trace:
   - `FactionDataParser` → `FactionData` → `FactionSelectionService.BuildFactionResult` → `FactionSelectionResult` → `FactionDisplayHelper.ApplyResult` (wraps in Localize). This is the expected path.
   - Are there OTHER paths? Grep for `FactionData.Name` / `FactionData.Description` / `FactionData.Perks` / etc. read sites — if any access the raw fields without going through `Localize`, the player sees literal `{=...}default` text. Particularly check the `FactionHoverService`, `LandmarkService`, and any prefab-bound mixin VMs.

## File lists

### C# (in scope)

- `Main/Features/FactionMap/FactionDisplayHelper.cs` (Phase 1 helper)
- `Main/Features/FactionMap/FactionSelectionService.cs` (Phase 2 fix: FormatDifficultyText)
- `Main/Features/FactionMap/FactionDataParser.cs` (data shape)
- `Main/Features/FactionMap/Models/{FactionData,FactionSelectionResult,FactionBonus,FactionPerk,FactionSpecialUnit}.cs`
- `Main/Features/FactionMap/ViewModels/{FactionSelectionVM,FactionPerkItemVM,FactionSpecialUnitItemVM,FactionBonusItemVM,FactionTraitItemVM}.cs`
- `Main/Features/FactionMap/FactionHoverService.cs`, `LandmarkService.cs` — possible bypass paths
- `Main/_Module/GUI/Prefabs/CharacterCreation/CharacterCreationCultureStage.xml` — prefab binding

### Data (in scope)

- `Main/_Module/ModuleData/factionmap/factions.json` — full file rewrite
- `Main/_Module/ModuleData/taom_module_strings.xml` — +599 auto-harvested + 7 hand-authored difficulty keys
- `Main/Features/CulturalFeats/TaomCulturalFeats.cs` — source-of-truth for the 97 feats the content should reflect

### Tests

- `TAOM.Tests/Features/FactionMap/FactionMapDataTests.cs` (new, 20 tests)
- `TAOM.Tests/Features/FactionMap/FactionDisplayHelperTests.cs` (Phase 1, 6 tests)
- `TAOM.Tests/Features/FactionMap/FactionSelectionServiceTests.cs` (FormatDifficultyText test updated)

### Tooling

- `tools/harvest_factionmap_strings.py`

### Docs

- `docs/features/cultural-feats.md` (97-feat inventory)
- `docs/reviews/rca-faction-map-phase2-2026-06-01.md` (deep-review RCA, Tooling H1 false positive disposition)
- `CHANGELOG.md` (Phase 2 main + Phase 2 fix entries)
- `feedback_faction_map_update_with_cultural_feats.md` (memory — standing instruction)

## REQUIRED SECTIONS

### 1. Vanilla code (paste decompiled bodies)

Use `pwsh tools/taom-src.ps1 path <Type>` against the installed v1.4.5 DLLs at `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\`. Paste these:

- `TaleWorlds.Localization.TextObject.GetLocalizedText` (or `.ToString()`) — to corroborate the Phase 1 unit tests' assumption about `{=KEY}default` resolution.
- `TaleWorlds.CampaignSystem.GameComponents.DefaultCulturalFeats` — full class. Need to know what inherited feats Dale/Khand/Harad/Rhûn/Dunland/Rohan actually ship.
- `TaleWorlds.Localization.GameTextManager.FindText(string id, string variation)` — does it return a `TextObject`? Does it handle `{=KEY}default` resolution on the fallback path?

### 2. Cultural-feat ↔ faction-content audit

For each of the 16 playable factions, produce a 3-column table:

```
faction | shipped feat | mentioned in CC page? (yes/no/partial) | notes
```

Walk `TaomCulturalFeats.cs` for each culture's feats (the file has 97 properties — use the docs/features/cultural-feats.md inventory to drive). For each feat, find the matching faction in factions.json and check whether the perks/bonuses/strengths/weaknesses sections mention it. Flag any feat shipping but unmentioned (silent improvement, less impact) and any UI claim with no matching feat (false promise, HIGH impact).

### 3. Key-coverage audit

For each `{=key}default` token in factions.json, verify a matching `<string id="key">` exists in taom_module_strings.xml. Spot-check 10 (listed in Known Suspect #4). Report each as MATCHED / MISSING.

### 4. Findings or observations

For each Known Suspect: CONFIRMED / DISPUTED with a one-paragraph reason. Then any ADDITIONAL findings.

## QUALITY GATES

- Paste vanilla code as inline code blocks.
- For each Known Suspect, output an explicit verdict.
- For the cultural-feat audit, output the 16-faction table — do not skip factions.
- Severity scale: CRITICAL / HIGH / MEDIUM / LOW / INFO. Each finding gets exactly one.
- Name file + approximate line for each finding requiring action.

## Prior review lessons

SUCCESSES:
- Config ID cross-ref caught `rohan` (invalid; should be `vlandia`) and `dol_guldur` (should be `dolguldur`) typos.
- Vanilla decompilation caught missing gates that deep-review missed.
- Per-occupation review (review 45) caught a per-cell test gap (Dol Guldur × Artisan) that 5 Claude agents missed.

FAILURES:
- Codex once assumed `empire = Rohan` (it is Dunland). Use the ID cheatsheet.
- Codex sometimes flags vanilla-matching code as a bug — decompile the vanilla target.
- Codex has skipped hard sections; don't skip. If a section has no findings, write "no findings" explicitly.
- Codex tooling agent flagged a Python text-mode CRLF issue as HIGH on 2026-06-01 — was a false positive. Universal-newlines mode handles CRLF transparently on Windows. Empirical test (re-run + git diff) is faster than reasoning about Python I/O semantics.

## Output location

Write your review to `docs/reviews/codex-adversarial-faction-map-phase2-2026-06-01.md`. (This file — the prompt — lives at `docs/reviews/codex-adversarial-faction-map-phase2-2026-06-01.prompt.md`.)
