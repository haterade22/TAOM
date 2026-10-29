# Codex pre-review: Feral Orcs bandit faction (#1063)

You are a reviewer under `.ai/roles/reviewer.md`: read-only, no edits, no fixes. Report actionable defects with file and line, impact, and the code or data that proves them. Try to refute each claim before you report it. Missing engine evidence stays UNVERIFIED. Engine signatures and behaviour come from the installed game DLLs under `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client` and `Modules\*\bin` (decompile with ilspycmd, or read `E:\Decompiled_Bannerlord\` as a browse aid that can lag the install).

## The change

A new hideout bandit faction, data only, modelled on `gundabad_raiders`. Uncommitted in the working tree of `E:\repos\TAOM` (`git diff`, plus the untracked files below), and live edits in the unversioned `TAOM_Map` module.

- Culture `feral_orcs` (`taom_spcultures.xml`), clan `feral_orcs` (`characters/clans.xml`, home `Settlement.hideout_feral_1`), party templates `feral_orcs_raider_party_template` and `feral_orcs_boss_party_template` (`taom_partyTemplates.xml`).
- Five troops in the new `Main/_Module/ModuleData/troops/troops_feral_orcs.xml` (registered in `Main/_Module/SubModule.xml`): `feral_orcs_snaga`, `_archer`, `_raider`, `_brute`, `_boss`. All five carry `occupation="Bandit"` and `culture="Culture.feral_orcs"`; the other bandit cultures reuse kingdom troops with `occupation="Soldier"` and only their boss is `Bandit`.
- Strings in `taom_module_strings.xml`, `taom_troop_name_strings.xml`, and English seeded rows in `Languages/*/std_taom_module_strings_*.xml` and `std_taom_troop_name_strings_*.xml`.
- List entries: `Main/Features/BanditManagement/HideoutDescriptionService.cs`, `Main/Features/VoiceAccents/VoiceAccentMap.cs` + `voice_accents/voice_accents.json`, `Main/Features/BannerBearers/Domain/BannerBearerConfig.cs` + `banner_bearers/banner_bearers_config.json`, `clan_heraldry/bandits.json`.
- Regenerated `armour_acquisition/armour_classes.xml`; `tools/ranged_ladders.json` puts the archer on the goblin bow line.
- Tests: new `TAOM.Tests/Features/BanditManagement/BanditHideoutLiveDataTests.cs`; changed `TAOM.Tests/Core/CultureDataFixture.cs`, `HideoutBossPartyTemplateTests.cs`, `HideoutDescriptionServiceTests.cs`, `TroopProgression/VolunteerRecruitmentServiceTests.cs`.
- Docs: `docs/features/feral-orcs.md` (new), `bandit-management.md`, `docs/modding/id-cheatsheet.md`, `docs/reference/feature-map.md`.
- Live: 23 `<Settlement id="hideout_feral_N" type="Hideout" culture="Culture.feral_orcs">` blocks inserted after `hideout_mirkwood_10` in `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml`, positions copied from the map scene entities in `TAOM_Map\SceneObj\Main_map\scene.xscene`; 23 name rows in each `TAOM_Map\ModuleData\Languages\<LANG>\loc_settlements.xml`.

**Not in scope (another session's work, must not be reported as this change's):** the `*_DO*` and `*_SRH*` town, castle and village rows in the live `settlements.xml`, and the live Armory files changed at 12:59 today.

## Known suspects (verify or refute each)

1. **Old save, new data.** A player loads a save made before this change. The save has no `feral_orcs` clan, while the XML now defines the culture, the clan and 23 hideouts of that culture. Trace what the engine does with XML clans and settlements that are absent from the save, and whether `BanditSpawnCampaignBehavior` or any hideout code then indexes a culture or clan dictionary that lacks the key. Crash, silent no-op, or half state?
2. **Module coupling.** The clan row ships in the TAOM module; the dens ship in `TAOM_Map`. A clan whose culture owns no hideout throws `KeyNotFoundException` in `GetInfestedHideoutCount` on a new game. Is there any runtime guard? Can a player end up with the new TAOM and an older `TAOM_Map` (see `docs/reference/release-process.md` and how the three modules are packaged)?
3. **All-Bandit troop line.** What changes when every troop of a bandit culture has `occupation="Bandit"` rather than `Soldier`: prisoner recruitment, ransom, conversations, the hideout boss selection (`SelectBossAgent`, `Patch86`), volunteer recruitment, upgrades, and every TAOM service that branches on occupation.
4. **Culture attribute parity.** Compare the `feral_orcs` culture element attribute by attribute with `gundabad_raiders`. Name any attribute an engine or TAOM consumer dereferences without a null check that `feral_orcs` lacks.
5. **Culture- and clan-keyed consumers.** Every TAOM map keyed by culture or clan id that does not list `feral_orcs` (race abilities, culture doctrines, fire and tribute, lord banners, menu link colours, custom battles, culture conversion, alignment). Each must fail closed for an unknown key. Name any indexer that throws.
6. **`armour_classes.xml` regeneration.** Is every changed row explained by the five new troops, or did the regeneration also pick up unrelated live Armory drift? Do the new wearers change which pieces markets and loot hand out?
7. **Hideout rows.** `map_icon`, `wait_mesh`, `background_mesh`, scene names and the settlement distance cache for 23 new settlements. Does a settlement absent from the distance cache crash, or only degrade?
8. **The new live gate.** `BanditHideoutLiveDataTests` filters clans through the hand-kept `CultureDataFixture.HideoutOnlyCultures`. Can a future hideout bandit culture escape it? Does each test fail on the states it guards (dens removed, clan home renamed, scene missing, a language row missing)?

## Output

One section per finding: severity (CRITICAL, HIGH, MEDIUM, LOW), `file:line`, the defect, the evidence you read, a reproduction or proving reference. Then a list of the suspects you refuted, with the evidence. No fixes.
