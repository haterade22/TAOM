# RCA: Arthedain deep review (2026-10-07)

**Summary.** The eight-lens `/deep-review` of the new kingdom of Arthedain (uncommitted, on
`bannerlord-1.5.x`) confirmed two HIGH findings. First, the live `settlements.xml` placed Blue Craig's
new town Luinkrag ahead of 34 existing rows. A save made before the change would then have
unregistered those 34 existing settlements, silently, while its parties and heroes still pointed at
them. Second, none of Arthedain's
three careers had an archetype, so their battle abilities showed the toast and applied nothing. The
review also found:
- five MEDIUM gaps: race abilities, both armour quests, the veteran militia's level, 64 dead cloned
  rosters, and a wrong engine claim in the recipe;
- a run of LOW drift in tests, docs and tools.

Every finding below was re-read in the code or the v1.5.4 decompile before it was fixed. The
decisions the review raised went to Mike and are listed after the findings.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | `town_GBC2` and its villages sat at rows 969-972 of 1040. On a pre-change save, `Settlement.Deserialize` throws on the new town's empty alleys, `MBObjectManager.LoadXML` swallows it, and rows 973-1006 (Goblin-town, Lindon, Misty Mountains villages, Blue Craig's castles) are never marked ready, so the load unregisters them (`Campaign.cs:1457`) while the save's parties and heroes still point at them. | Save compatibility, live data | `add_map_fortifications.py` copied `add_map_villages.py`'s "insert after the region's anchor so a block stays contiguous" design, a readability goal no one had weighed against saves. `docs/modding/settlements.md` said such a load "is expected to throw", a v1.4.8 reading that missed the empty `catch`. Earlier batches (Isengard 2026-09-11, #597) had already landed mid-file and shipped as "new campaign only", so the order looked harmless. | Rows moved to the end (`town_GBC2` now row 1003, a town first). Both map tools append (`append_settlements`); `add_map_fortifications.py --check` fails unless the batch is the file's tail under a town (`save_order_findings`, proven on the pre-move backup: 34 findings). Docs: settlements.md "Takes effect", recipe gotcha, arthedain.md "Save Compatibility". In-game check on an old save owed. |
| 2 | HIGH | `ranger_of_the_north`, `warden_of_fornost`, `knight_of_arthedain` missing from `CareerSystemIoC.BuildCareerArchetypeMap`: `NoOpExecutor`, plus no career kit. | Hand-kept C# map keyed on data ids | The #749 sweep grepped for the scaffold culture id; this map is keyed on career ids, which no culture grep finds. `CareerArchetypeServiceTests` spot-checked three Gondor ids. 18 careers from `cc1713eb3` (August) carry the same gap. | Three rows added. `IoCMap_EveryCareerInTheData_HasAnArchetype` walks `taom_careers.xml` (the 18 are a named known-gap set pending Mike, with a test that the set only shrinks). `culture-playability-wiring.md` step 6 and the recipe's new "C# a kingdom touches" table name the map. |
| 3 | MEDIUM | No `arthedain` key in `race_abilities.json` or `RaceAbilityDefaults.cs`: every Arthedain soldier resolved to no ability. | Compound key missed by the sweep | The key is `"gondor,gondor_soldiers"`, so a grep for `"gondor"` as a key misses it. | `arthedain` joined Gondor's key in both; `ShippedFile_Arthedain_SharesGondorsCitadelGuard`; count pin 17 to 18. |
| 4 | MEDIUM | No Armourer's Commission row for Arthedain, and Gondor's Deep Seam row did not list it. | Deliberate exclusion on a wrong reason | The author read `ArmourAcquisitionShippedDataTests`' material rule as covering the quest rows; the file's own comment says a borrowing culture shares its donor's row. No test required every playable culture to be served. | Own commission row (Arnor infantry heavy chest), `gondor,arthedain` on the Deep Seam row. `Commissions_EveryCharacterCreationCultureHasARow` and `DeepSeam_EveryCharacterCreationCultureWithALordsMaterialIsServed` read `cultures.json`. |
| 5 | MEDIUM | Veteran militia at level 21 (tier 4) where every culture's are 16 (tier 3): a tier stronger than anyone's veterans, against "weak by design". | Generator constant | The generator's militia skill rule (L21 baseline) was conflated with the troop level. No test compared militia levels across cultures. | Level 16 and the tier-3 bow; `test_the_veteran_militia_sit_at_every_cultures_level_16`. The armour class table moved six Arnor rows back to HEAD's classes. |
| 6 | MEDIUM | 66 of 74 rosters in `taom_equipment_sets_arthedain.xml` were unused: 64 Gondor named lords' kits under new ids, plus the two `_e` lord templates. | Clone-all generator | `promote_borrowed_cultures.py` clones a whole standalone file; nothing flags unreferenced rosters. | `rosters_kept` keeps the lord template family `a` to `e` the kingdom generator assigns (10; Arthedain's four lords per clan use `a` to `d`), dropping 64; the apply changed only that file and the stale culture start point. |
| 7 | MEDIUM | The recipe said "tier only sets the party limit"; tier also sets party size (+25/+15 per tier), the companion limit and starting renown and influence. | Engine claim | Read from `GetPartyLimitForTier` alone. | Recipe and arthedain.md corrected; spot-checked in `DefaultPartySizeLimitModel` (constants 15 and 25). |
| 8 | MEDIUM | `generate_char_creation_equipment.py` regenerated the whole file on any run without `--append`, `--dry-run` and `--help` included, reverting hand fixes and the starter wiring (pre-existing). | Tool footgun | The README forbade the invocation; the code did not. | The full-regeneration path and `generate_all` deleted; anything but `--append` refuses (verified: file hash unchanged). |
| 9 | LOW | Compiled defaults in `BattleBalanceConfig.cs`, `CombatMechanicsConfig.cs`, `BannerBearerConfig.cs` lacked `arthedain`; no test pinned the JSON rows. | Two copies, no gate | #749 mirrored Lindon by hand and nothing checked it. | Rows added; `CompiledDefaults_*_MatchTheShippedFile` per table; `ShippedConfig_Arthedain_MatchesGondorSurvivalBonus`. |
| 10 | LOW | `EliteEmissaryConfigProvider.KnownCultureIds` dropped `shaghana` and `abanissa` (pre-existing); the resource check already rejects everything it did. `CustomBattleCommandersProvider.KnownCultureIds` and two test copies had drifted to three contents. | Hand-kept culture lists | Each comment said it "mirrors" another list; none was checked. | Elite Emissary set deleted (keep-test for a resourced culture). `CultureDataFixture.MainCultureIds()` reads the data; the custom-battle set is pinned to it; the inert 22-kingdom set and both self-counting tests removed. |
| 11 | LOW | "Arthedainian" (a made-up word) and Osgiliath's "The Captain holds the line" in two career descriptions. | Text substitution order | The careers table mapped `Gondor` before `Gondorian`; the Captain sentence had no remap row. | Text fixed in 14 files; remap rows added so a regeneration writes the same. |
| 12 | LOW | `add_map_fortifications.py`: stale evidence (84/84), a 0.01 tolerance close to what the scene's 3-decimal rotations alone can move a tilted gate (up to about 0.009 for `castle_AN10`; measured worst 0.0065), and the first gate child where the editor keeps the last. | Tool precision | Measured once, at the first batch. | Tolerance 0.02 with the measurement in the comment; last-child rule with a test; README and docstring re-measured (231 gates, worst 0.0065). |
| 13 | LOW | `add_map_villages.py` accepted a village whose `bound` settlement has no row (an NRE on a new campaign). | Missing check | The bound was resolved only on the placeholder path. | `--check` names it; `--apply` refuses it; test added. |
| 14 | LOW | Stale counts and claims: `ConfigIdValidationTests` 22 kingdoms, "fourteen" kingdoms, the `xml-data.md` culture row, army-targeting (80 entries, missing theater and lists), id-cheatsheet, file-catalogue, Blue Craig's town count, armour-acquisition and marketplace "nine", the culture start point after Mike moved Fornost, the duplicate-entity claim in arthedain.md, and docs telling readers to run generators without `--only`. | Doc drift | Counts written by hand; the drift lint checks config examples, not prose counts. | Re-measured and fixed in this change. The catalogue's `SubModule.xml:N` citations were already about 58 lines off before it: follow-up. |

## Root-cause pattern

**A grep sweep cannot find a table keyed on something other than the culture id.** The #749 lesson
said to sweep every table keyed on the scaffold culture. The sweep did cover ModuleData and the
shipped JSON. It still missed three kinds of table:
- a C# map keyed on career ids (#2);
- a compound JSON key (#3);
- a quest file it read and excluded on a misread test (#4).

Each of these is now held by a test that reads coverage from the data, not from a hand list:
- every career in the data has an archetype;
- every character-creation culture has a commission row and a Deep Seam row;
- compiled defaults equal the shipped file;
- the custom-battle culture set equals the playable cultures.

The next kingdom fails those tests instead of relying on a sweep.

**Live-module layout carried no save reasoning.** The settlement tools optimised for a readable file,
and the one doc that touched saves predicted a crash nobody had reproduced. The real behaviour is
quieter and worse: the load continues, and every row after the first new one loses its unsaved
fields. The rule is now enforced by `--check`, not by reading.

## Why each lens caught what it caught

- **Engine compatibility (2)** traced the save chain end to end in the v1.5.4 decompile, which no
  earlier lens of any settlement change had been asked to do.
- **Data flow (5)** and **XML (7)** independently found the career map: one from the ability registry
  inward, one from the career rows outward.
- **Completeness (4)** found the armour quests by comparing every borrowing culture's rows.
- **Tooling** shaped the order rule into an exact invariant: a town heads the tail, and only this
  batch's rows count as new. It also found the char-creation footgun.
- **Standards (1)**, **Efficiency (3)** and **Design (6)** found the stale pins, no performance cost,
  and the data-derived replacements for the hand lists.

Nothing was missed by every lens. The authoring itself missed the HIGHs because the #749 sweep
was the only tool for culture coverage, and the map tools had no save rule.

## Decisions (Mike, 2026-10-07)

| Decision | Outcome |
|---|---|
| Diplomacy breadth | Arthedain Hostile to the six other evil realms too (142 rows; `DiplomacyShippedConfigTests`) |
| Lords' armour | Lord, ruler, heir and enlistment rosters on the Arnor kit through the promoter's `item_map`; the crown on King Arvegil II alone (`leader_head`) |
| The 18 August careers | Mapped by their clone source's archetype; the known-gap set is gone, so the career test has no exceptions |
| Market weapons | No change; follow-up issue (Lindon has the same shape) |
| Ranger bow names | `names` override on a ranged ladder line; "[Arnor] Ranger's Bow II" to "VI" in the live Armory |
| Dead policy ids (all 15 kingdoms) | No change; follow-up issue |
| Village bindings | Emyn Forn to Annúminas, Combe to Emyn Beraid (nearest fortification) |

## Lessons appended

- `lessons/state-lifecycle-save.md`: append new settlement rows at the end of the live file, a town
  first.
- `lessons/data-content-cultures.md`: hold a new culture's coverage with tests that read the data,
  not with a sweep.
