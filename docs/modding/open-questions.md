# What the handbook could not answer

Every chapter states, in its own words, where TAOM has never worked something out. This is the same
list gathered in one place, as the authors recorded it while writing on 2026-09-05.

It is here for two reasons. A reader who hits one of these questions should learn quickly that the
answer does not exist rather than hunting for it. And each row names the file or the experiment that
would settle it, so anyone with the game open can close one.

Nothing here is a defect in the handbook. A guess in place of any of these rows would be.

Several rows name files in the game install rather than the repo (`TAOM_Map`,
`LOTRLOME_Armory`). Those live in the game install, not the repo; a module reinstall reverts
hand edits, so land a repo-side validator gate with any fix.


## items-armor.md

- Where armour goes for the eight TAOM cultures with no LOTRLOME_items folder (abanissa, bluecraig, goblin, lindon, lothlorien, mistymountainorcs, shaghana, umbar). Adding a folder also needs its own <XmlName id="Items"> row in LOTRLOME_Armory/SubModule.xml. No doc in the repo settles this.
- What a save that already holds a deleted item does on load. TAOM has tested only the reference side (rca-armoury-keyforce-cleanup-2026-09-01.md, 212 BROKEN_ITEM_REF across 159 consumers); the save side is untested.
- Whether the ten <Armor> attributes with zero uses in the armoury (body_mesh_type, body_deform_type, stealth_factor, no_slim, tail_cover_type, reins_mesh, maneuver_bonus, speed_bonus, charge_bonus, tier_override) are deliberately unused. ArmorComponent.cs:146-217 is the only description of what they would do.
- Which item_category an armour piece should carry. Ten armoury items set one and the rest let the engine auto-classify; DefaultItemCategories.cs is the id registry and nothing in docs/ picks a convention.
- tail_cover_type and lod_atlas_index are read and stored by the engine with no managed consumer in the v1.4.8 dump, so their effect could not be determined from the decompile (marked as such in the tables and in inert=).

## items-shields.md

- Marker choice: the two attribute tables use `engine-ref`, not `engine-table`. `check_handbook_attributes.py` raises a GAP for every attribute the cited Deserialize reads that the table omits, and `ItemObject.Deserialize` reads 35 attributes plus 11 child elements while `WeaponComponentData.Deserialize` reads 29 plus 1. A shield-subset table under `engine-table` would have produced roughly 50 GAP rows. If the handbook wants `engine-table` on every items-* chapter, the complete surfaces have to live in items-armor.md and items-weapons-and-crafting.md (which appear to own them) and this decision needs confirming across the four items-* chapters.
- Upstream correction for `docs/reference/armory-shield-audit.md` (last section, 'Reproducing this audit'): it says `validate_all_troop_refs.py` 'skips shields by design (its ARMOR_PREFIX_RE matches sk_*/ar_* only, never wm_*/sm_*)'. The regex at `tools/validate_all_troop_refs.py:39-40` now includes `sm_[a-z]+_`, so 80 of the 224 shield ids fall inside the sweep and 144 do not. The chapter states the measured split instead of repeating the doc.
- Upstream correction for the same audit's count table: it still prints 226 shields / 115 shield / 111 hand_shield in the table body with a 2026-09-01 note above it. Parsed 2026-09-05: 224 / 115 / 109.
- `docs/modding/items-armor.md` and `docs/modding/items-weapons-and-crafting.md` exist on disk but are untracked, so `lint_docs.py --quick` lists my two links to them under 'Link targets present but UNTRACKED', not under dead links. That clears when the orchestrator commits the chapter set. I cannot run git.
- Left unanswered on purpose, and the chapter says so and points at the audit: the 12 Rhun and Dol Guldur tower shields that carry a `hand_shield` rotation on the `shield` bone have no derivable correct value (their siblings use six different rotations), so it needs an in-game look.

## items-weapons-and-crafting.md

- Ten attributes across these five deserializers are read and stored but have no managed consumer anywhere in the v1.4.8 shipping-client dump: CraftingPiece @CraftingCost / @is_unique / @required_skill_value, BladeData @holster_mesh_length, CraftingTemplate @always_show_holster_with_weapon / @rotate_weapon_in_holster / @piece_type_to_scale_holster_with / @hidden_piece_types_on_holster, WeaponDescription @rotated_in_hand / @use_center_of_mass_as_hand_base, ItemObject @lod_atlas_index / @using_arm_band, WeaponComponentData @fire_damage. The chapter says so per row rather than guessing; proving any of them needs an in-game A/B, not the decompile.
- The legal `item_category` ids are still not enumerated anywhere in TAOM docs, and this chapter does not enumerate them either (it says to omit the attribute and let the engine classify). The registry is `Core/TaleWorlds.Core/TaleWorlds.Core/DefaultItemCategories.cs`; a future items chapter or the balance-levers chapter should list them.
- A live defect is documented but not filed: `wm_cave_troll_1h_mace_a` writes `modifier_group="false"` at `LOTRAOM_weapons.xml:931`, which resolves to null so that mace never rolls a quality prefix. No GitHub issue exists for it.
- `tools/rebalance_weapons.py` reads and writes a sibling `taommod/src/data/armory` working copy plus one hard-coded install path, and never writes `LOTRAOM_weapons.xml` at all (WEAPONS_XML is read-only, used only to map blade to culture). Whether that sibling copy is still the intended source of truth is undocumented; the two crafting-piece copies happen to match today (both 672 pieces).
- Four Armory `<Flag name>` values are spelled `CanKnockdown` beside 141 spelled `CanKnockDown`. Harmless, because `Enum.Parse` runs with ignoreCase at `CraftingPiece.cs:272` and `:277`, but nothing records whether the inconsistency was deliberate.

## items-mounts-and-harness.md

- Gotcha 18 stays open: whether an incomplete rein surface actually regresses ridden death on v1.4.8 is untested in game and no tool gates it. Measured declared rein counts are spider 5, warg 5, fell warg 5, elephant 0, mumakil 0, chariot 12, war ram 0 declared plus 12 inherited from the horse.
- Nothing validates a `monster="Monster.x"` reference. tools/taom_schema.py:161-167 resolves only Item./NPCCharacter./Culture./PartyTemplate. prefixes, and the engine registers a placeholder Monster rather than failing (MBObjectManager.cs:718-731), so a typo ships as a broken mount.
- tools/verify_mount_assets.py knows only spider, elephant and mumakil (its CREATURES dict, :37-68). The warg, fell warg, chariot and war ram have no asset gate at all, and tools/audit_mount_parity.py never exits non-zero.
- num_paces, arm_length, arm_weight, jump_speed_limit vs jump_acceleration, the walking-speed units and standing_chest/pelvis_height have no managed consumer: their runtime effect is native and unverified. The chapter says so rather than guessing.
- skins.xml has no managed deserializer, so what a rider race needs beyond `race=` cannot be settled from the decompile. The chapter points at docs/features/hero-race.md and says the answer must come from experiment.
- Upstream correction candidate: docs/features/war-ram.md:57-59 and lotrlome-war-ram-changes.md:61 say the war ram is 'the only TAOM mount carrying vanilla's complete rein surface', but Monsters/LOTR/lotr_monster_chariot.xml declares all 12 rein attributes itself (measured). The chapter states the measured counts instead of repeating the claim.
- Research-JSON counts that disagree with disk (not shipped docs): engine-Monster.json says LOTRLOME_Armory/ModuleData/monsters.xml holds 71 Monster entries and Native 17; ElementTree counts 70 and 16. The chapter uses the measured numbers.

## npcs-notables-and-townsfolk.md

- Whether a running save ever adopts a newly added notable template. The engine only draws from the pool when it creates a notable, so existing notables plainly never change, but the top-up timing after a notable dies is untested anywhere in docs/.
- Whether the four cultures that ship notables but no service NPCs (abanissa, lothlorien, shaghana, umbar) are borrowing another culture's townsfolk deliberately. Umbar points all 35 townsfolk and shop roles at vanilla Calradian ids in SandBoxCore/ModuleData/spnpccharacters.xml and no doc records the decision.
- Why six cultures now ship a third RuralNotable template. docs/features/cultural-feats.md names four (isengard, mordor, dolguldur, gundabad); measured today it is seven, adding bluecraig, goblin and mistymountainorcs, with no record of why.
- Whether the six unreachable gear_practice_dummy entries (dale, dunland, harad, khand, rhun, rohan) are a known defect or dead data. The id the engine composes uses the culture StringId, which for those six is the vanilla one.
- Whether pointing all 112 tournament team-template rows at vanilla's tournament_template_empire_* / _aserai_* characters is deliberate. No TAOM culture fields its own tournament fighters.

## wanderers-and-named-companions.md

- Does a named companion's backstory dialogue ever fire in game? The chain is gated by HasMet, which TAOM sets at placement (NamedCompanionService.cs:54, LordConversationsCampaignBehavior.cs:1222-1226), and it keys off Hero.Template.StringId, which is null for a heroes.xml hero (Hero.cs:298, CharacterObject.cs:419). named-companions.md claims the flow triggers; no in-game check is on record. Settle it by talking to a companion in game.
- Can a named companion added after a save was made ever appear in that save? EnsureCompanionsPlaced re-places only heroes that already exist (NamedCompanionAdapter.cs:10-14); whether a new heroes.xml row reaches an existing save is undocumented in TAOM. The chapter states 'new campaign only' as the safe answer.
- Why do goblin, mistymountainorcs, lindon and bluecraig ship 40 wanderers with no backstory strings and borrowed skill sets? No doc records whether that is deliberate or an unfinished batch; generate_batch2_wanderers.py's KINGDOMS map is the place to look.
- Nothing in TAOM.Tests reads the shipped companion data (both test files use mocks and a temp dir), so the 17/17/17/119 agreement across named_companions.xml, heroes.xml, named_companion_config.json and the strings file is held by hand. It agrees today; there is no gate.

## troops.md

- What happens when a troop's race= and its <face_key_template> disagree (race="dwarf" with BodyProperty.fighter_gondor): is race authoritative for the mesh and the BodyProperty only for morphs, or is it a defect? No TAOM doc settles it and the face path bottoms out in native code. Start from Main/_Module/ModuleData/TAOM_bodyproperties.xml (no race attribute on <BodyProperty>) and docs/features/hero-race.md.
- Which integer a given race name resolves to, and whether inserting a race renumbers the ones after it. The table is built at runtime from the merged skins.xml list via a native call, so 'index 0 is human' stays strong inference, not decompile proof (docs/features/black-numenorean.md:162-176). The authoring comment above the sauron block in LOTRLOME_Armory/ModuleData/skins.xml records the merge-order dependency.
- Whether the <Resistances> knockback/knockdown/dismount numbers do anything in a campaign. Only MultiplayerAgentStatCalculateModel was found reading them off the character; the singleplayer path derives dismount resistance from Riding. Zero TAOM troops set them, so nothing has ever tested it.
- What names default_equipment_set may legally take. The pool is filled in code by Game.SetDefaultEquipments and the callers were not traced. No TAOM content uses it, so there is no example to work from.
- tools/validate_all_troop_refs.py still skips 6 of the 16 troop files (dunland, goblin, harad, mirkwood, rivendell, rohan). That is a real gate hole, not a doc error, and the chapter documents it rather than fixing it.

## equipment-rosters.md

- Does slot="Item4" / ExtraWeaponSlot work from a shipped roster? The engine maps it (Equipment.cs:225-236) and it is the only slot a DropOnWeaponChange/DropOnAnyAction item such as a banner fits (Equipment.cs:445-506), but TAOM ships 0 uses, no doc describes one, and tools/audit_enlistment_roster_coverage.py bans it from its slot allowlist. Untested here.
- What does equipmentType="Stealth" do in TAOM? Legal in the enum (Equipment.cs:14-20), 0 occurrences across Main/_Module/ModuleData. No TAOM evidence for when a stealth set is worn.
- Does <Equipment slot="Horse" id=""/> actually clear a slot? docs/features/career-system.md:361 documents the technique but there are 0 id="" rows in equipmentsets/, so it has never been exercised in shipped data.
- UPSTREAM FIX NEEDED: docs/features/career-system.md:354 states 'FillFrom does NOT clear unspecified slots'. Equipment.FillFrom (Equipment.cs:184-194) copies all 12 slots unconditionally, so an omitted slot is emptied. tools/wire_career_starter_armor.py's header states it correctly. Not in the critic notes; new finding.
- UPSTREAM FIX NEEDED: .claude/rules/xml-data.md cites memory file feedback_equipmenttype_civilian_required.md, which does not exist in the repo or the project memory store (only feedback_hand_or_script_both_valid.md is there). Repoint or drop the citation.

## skill-sets.md

- Whether trait values written outside a trait's declared MinValue/MaxValue are clamped by any consumer after load. They are provably NOT clamped at load (PropertyOwner.Deserialize never reads TraitObject.MinValue/MaxValue), so <Trait id="Honor" value="50"/> is stored as 50. Start from DefaultTraits.cs:164-188.
- What the hidden 0-to-20 trait `Tracking` does. DefaultTraits.cs:142 registers the id but the v1.4.8 dump has no accessor and no consumer for it, so it may be dead data.
- Whether the schema validation pass rejects a capitalised <Skill> or a duplicate SkillSet id. The deserializer provably does not care (PropertyOwner.cs:73-75 never checks the element name) and GetMergedXmlForManaged runs with skipValidation:false (MBObjectManager.cs:789), but nobody has read the validation routine to see whether a violation aborts the load or only logs. Note sandboxcore_skill_sets.xml itself ships a duplicate id (spc_wanderer_khuzait_8_skills) and the game runs.
- Whether TAOM_Map or LOTRLOME_Armory contribute SkillSets. Neither SubModule.xml was enumerated in full this task; the manifest row did not list them as sources, and all TAOM skill sets found came from Main/_Module.

## lords-and-heroes.md

- How to author a banner_key from scratch: the number-group grammar is decoded nowhere in TAOM. Chapter points at Banner.cs (Deserialize at :240, TryGetBannerDataFromCode at :576, both confirmed present), docs/reference/banner-icon-generation.md, and the working clan_erebor_1 key.
- What the minimum viable kingdom is (clans, lords, settlements) before it stops crashing. kingdom-creation.md lists crash classes and culture-playability-wiring.md has a 14-row checklist, neither states a floor.
- What clan `tier` legal values are and what tier changes. Chapter says so plainly and points at Clan.Deserialize (Clan.cs:859) and the future clans.md chapter.
- Whether preferred_upgrade_formation reaches a formation several tiers down an upgrade tree. CharacterHelper.SearchForFormationInTroopTree (CharacterHelper.cs:600) was not read; no TAOM lord sets the attribute, so nothing depends on it today.

## cultures.md

- How to author a faction_banner_key from scratch. The number-group grammar is decoded nowhere in TAOM, every Kingdom and Faction row needs one, and placeholder keys ship as a visible in-game defect. Stated plainly in the chapter's 'What TAOM has not answered' section, pointing at Core/TaleWorlds.Core/TaleWorlds.Core/Banner.cs (Deserialize / TryGetBannerDataFromCode), docs/reference/banner-icon-generation.md and Erebor's working 20-group key.
- What a non-human culture needs beyond race= on its troops. The race registry is skins.xml in the live LOTRLOME_Armory module and it has no managed deserializer in the v1.4.8 decompile, so its attribute meanings cannot be read out of the engine at all. Chapter points at docs/features/hero-race.md and row 14 of the culture-playability-wiring checklist (missing as_<race>_facegen is a fatal T-pose).
- Whether XSLT is applied before or after the SPCultures merge for a module that ships only a stylesheet was not resolved by this chapter. The engine research flagged it unresolved and I did not verify it; the chapter states the merge rules and the passthrough rules separately rather than asserting an interaction between them.
- docs/features/kingdom-creation.md's required-child-elements block writes <name id="..."/> for male_names / female_names / clan_names. CultureObject.cs:381 reads Attributes["name"], so an id= entry is a null dereference inside the swallowed catch. The chapter flags it as a doc bug in Gotchas; the upstream doc still needs the correction.

## body-properties.md

- What the native face generator does with a tag name that matches no mesh on the consuming race, and what an empty tag string means. The live instance is fighter_erebor: it lists empire, sturgia, battania, khuzait and Cleanface, none of which is among the 7 style tags the dwarf race declares, yet every dwarf troop points at it. MBBodyProperty only concatenates the string and BasicCharacterObject.cs:221 hands it to MBAPI.IMBFaceGen, which is not in the dump.
- Whether a 128-hex key authored on one race reads correctly on another. race= and the preset are independent inputs to one call, so nothing managed treats them as agreeing or disagreeing. Needs an in-game test.
- What each of the 512 key bits encodes. Managed code decodes exactly one field, the six-bit height multiplier in KeyPart8 (BodyProperties.cs:194-195); the rest is unpacked in TaleWorlds.Native.dll.
- Whether version= reaches the native decoder by some other route. Provably unread by every managed deserializer in the v1.4.8 dump (the only Attributes["version"] read anywhere is HotKeyManager.cs), always written as 4, never tested with another value.

## party-templates.md

- kingdom_hero_party_erebor_template sums to max 225 against the tool's 220 target, the only template in the file off its band. A dry run says 5 stacks would change. Nothing records whether the file or the target is the intended number, so I documented both rather than picking one.
- No doc catalogues the MCM-only balance knobs. The party-size cap is seven knobs in the 'AI Party Size' group with no JSON or XML surface, and Main/Features/TaomSettings.cs is the only list of the whole MCM surface (docs/features/mcm.md is just the Patch41 layout fix). Someone should decide whether that catalogue belongs in the handbook's balance-levers chapter.
- Whether NavalDLC still calls GetUpperTroopLimit / GetLowerTroopLimit in v1.4.8 cannot be checked from the _categories_v1.4.8 tree, which has no _modules_build aggregate. I verified only that one file in that dump mentions either name (their own definitions), so the 'informational' claim is proven for the base campaign and left unstated for NavalDLC.
- kingdom_hero_party_gondor_ithilien_template and kingdom_hero_party_gondor_belfalas_template are bound by no culture and no clan, still true today. Whether they are meant to be wired up or deleted is undecided in every doc I read.

## clans.md

- The banner_key number-group grammar is still undecoded. I measured that every shipped key is a multiple of ten numbers (background group plus one group per icon layer) and that 79 of 145 rows carry the two-group minimum, but which slot inside a group is pattern id vs colour id vs size vs position is documented nowhere in TAOM. That belongs in banners-and-heraldry.md and needs a pass over Banner.cs TryGetBannerDataFromCode plus docs/reference/banner-icon-generation.md.
- No minimum viable clan count is documented anywhere. kingdom-creation.md:249 says '5-10 clans per kingdom is typical' and gives a tier shape, but nothing states the floor below which a kingdom misbehaves. I said nothing about a floor rather than invent one; recipe-add-a-kingdom.md will hit the same gap.
- What settlement_banner_mesh and flag_mesh ever did cannot be recovered from the shipping-client decompile: both are declared in Factions.xsd:123-124, present on 22 vanilla and 8 TAOM rows, and return zero hits across the whole v1.4.8 dump. I state definitively that v1.4.8 ignores them and do not guess at history.
- The five bandit strip templates are duplicated verbatim in spclans.xslt at lines 27-31 and again at 1160-1164, each under the same comment block. I proved the stylesheet still produces the expected 90 rows by running it over the installed vanilla file with lxml, but I did not verify how .NET XslCompiledTransform resolves two equal-priority templates, so the chapter only states the duplication and the editing hazard. Worth a cleanup issue.
- Running the documented tool `python tools/clan_registry.py` rewrote `docs/reviews/_clan_registry.json` (its documented scratch output, not gitignored). I did not edit it by hand; flagging it so the orchestrator can decide whether it belongs in the commit.

## settlements.md

- Which module wins when two ship the same SceneObj/<name>: the last-active-module rule is documented only for the main map via MapScene.GetMainMapModule; ordinary settlement scenes are unstated. Pointed at worldmap-battle-scene-grid.md lines 69-71 and tools/audit_scene_names.py.
- Why TAOM_Map's Main_map carries two settlements_scripts entities with SettlementPositionScript where vanilla has one, and whether the duplicate double-registers the distance-cache system. Pointed at the two scene.xscene files and editor-cache-rebuild.md lines 203-204.
- How to paint campaign-map navmesh for new land: TAOM records only shore/ocean/bridge tile values (warsails-custom-map-guide.md lines 25-34) and the FaceGroupIndex to TerrainType mapping (worldmap-battle-scene-grid.md lines 60-63); land painting is undocumented and the baked result is TAOM_Map/SceneObj/Main_map/navmesh.bin.
- What background_crop_position is measured in: it is handed straight to the UI widget and every shipped value is 0.0.
- What gate_rotation and map_icon were for: no managed reader exists in either the shipping or editor build of v1.4.8, so the chapter refuses to claim they rotate or draw anything.
- Whether a settlement added to settlements.xml is safe on an in-progress save: the chapter says new campaign only and flags the CommonAreas index behaviour (Settlement.cs:1024-1031) as evidence the load path is not save-neutral, but TAOM has never tested the add case on an old save.

## kingdoms.md

- What settlement_banner_mesh and flag_mesh originally drove: 0 hits in the whole v1.4.8 managed decompile, and the shipping-client dump cannot prove native code does not consume them. Stated in the chapter as not determined from the engine.
- Whether isAtWar tolerates anything other than true/false. Kingdom.cs:792 uses Convert.ToBoolean(string), whose tolerance for \"True\" or \"1\" was not tested. The chapter types the attribute as true or false and claims nothing more.
- The icon and colour id pools inside a banner_key. The grouping is now decoded (Banner.cs:576-593, ten numbers per layer), but no TAOM doc maps icon id to sprite, and docs/reference/banner-icon-generation.md covers the sprite atlas pipeline, not the code grammar.
- The minimum viable kingdom (how many clans, lords and settlements before it stops crashing) is still undocumented. The chapter points at docs/features/kingdom-creation.md Known Crashes and the 14-row checklist in docs/features/culture-playability-wiring.md rather than inventing a floor.
- Nothing validates diplomacy.json, execution/alignment.json or siege/siege_defense_config.json ids against the live kingdom list. Only army_targeting.json is test-gated. Whether that gap has ever bitten in play is not recorded anywhere I read.

## configs-balance.md

- No catalogue exists of the MCM-only balance knobs, their defaults or their legal ranges. Main/Features/TaomSettings.cs (432 [SettingProperty declarations across 55 groups) is the only source, and docs/features/mcm.md is the Patch41 layout fix, not a settings list. The chapter says so and points at the file.
- Nothing measures which troops have no troop_weights.xml row or no troop_resource_costs.xml row. The fallbacks are documented (weight 1.0 at TroopWeightService.cs:43-45; no cost charged), but the coverage gap itself is unmeasured anywhere in the repo.
- There is no external validation of any balance config: tools/validate_moduledata.py and tools/taom_schema.py contain zero references to all fifteen filenames, and tools/schemas/ holds only three schemas (taom_npccharacter.json, taom_spcultures.json, taom_equipmentsets.json). The only value check is the provider's own warning lines in Logs/taom_debug_*.log at launch.

## strings-and-localization.md

- TAOM has never localized a troop name. All 836 distinct {=key} values across the 16 files in Main/_Module/ModuleData/troops/ are unregistered (0 hits against every <string id> in the 13 key-bearing strings XMLs), so every troop name ships English in all 12 languages. No tool covers it: harvest_literal_loc_keys.py scans Main/**/*.cs for taom_* literals only (tools/harvest_literal_loc_keys.py:38,87) and LanguageFileCoverageTests.LoadEnglishKeys collects <string> rows only (:82-109). The chapter says this plainly and points at the Add recipe as the manual route; whether the harvester should be extended to XML attributes is a decision TAOM has not made.
- The manifest's worked example does not exist on disk. `aom_gondor_loss_lumberman_name` appears in exactly one file, Main/_Module/ModuleData/troops/troops_gondor.xml:10, with no taom_module_strings.xml row and no RU twin (rg across the repo returns that file plus two docs). I substituted a real registered trio: lords.xml `lord_1_1_10` / taom_xslt_strings.xml:1483 / std_taom_xslt_strings_rus-RU.xml, all three diffed verbatim against their files.
- docs/reference/localization-map.md is stale in a second way beyond the DO-NOT-LIFT counts: it says 11 per-language files and cites `AllLanguageDirs_HaveExactlyElevenLanguageFiles` asserting 11 at :117. On disk each of the 12 language dirs holds 14 files (1 language_data.xml + 13 std_taom_*), every language_data.xml declares 13 <LanguageFile> rows, and the test is now LanguageDataXmlTests.AllLanguageDirs_HaveExactlyThirteenLanguageFiles asserting 13 at :149.
- docs/localization/TRANSLATOR_GUIDE.md:10-24 lists 8 TAOM language files at ~8,157 strings. There are 13 per language, and 12 GameText-registered source files plus global_strings.xml carrying 9,134 distinct keys. The guide's per-file entry counts (~2,104 for module_strings, ~1,431 for xslt_strings) also disagree with the files (2618 and 1449).
- docs/features/localization.md:31-40 presents the prefix convention as two prefixes in two files (taom_str_* and aom_*). Thirteen files carry keys and the dominant prefixes are taom_faction_, taom_career_, taom_cc_, aom_lord_, aom_backstory_, nc_backstory_ and others; the chapter replaces that table with a measured one.
- The chapter is 265 lines against the manifest's 170 estimate, over the contract's 30 percent band. The overrun is six engine tables (GameTextManager.LoadFromXML, LocalizedTextManager.LoadLanguage, LanguageData.LoadFromXml, LanguageData.Deserialize, split across Attributes and Child elements) plus the 15-row prefix-ownership table the goal column asks for. I found nothing to cut that was not carrying evidence, so I left it and am flagging it rather than trimming citations.

## banners-and-heraldry.md

- What is_base_background actually does: it calls BannerManager.SetBaseBackgroundId, and my own grep of the v1.4.8 managed decompile for BaseBackgroundId returns 4 hits (the property, the setter, the assignment inside it, and the one caller in BannerIconGroup). Nothing reads it back, so its consumer is native or UI side and is not determinable from the dump.
- Whether Icon/@texture_index may exceed 15. Nothing in managed code clamps or validates it (Module.cs:684-686 just concatenates it into an asset name); vanilla and TAOM both stop at 15 because their sheets are 4x4. Untested for a larger sheet.
- Whether the 9 shipped entries whose keys name undefined ids are a visible in-game defect. Measured: clan_khuzait_16, clan_lothlorien_2, clan_mirkwood_5, clan_mirkwood_6, clan_rivendell_2 and the rivendell/mirkwood/lothlorien/lindon cultures name 5 icon ids (17104, 17281, 17299, 17358, 17371) and 2 colour ids (124, 128) that exist in no active module. No GitHub issue exists and tools/validate_moduledata.py has no banner check at all, so nothing gates it. Worth an issue.
- Whether GUI/SpriteParts/ui_taom_bannericons/22004.png (a sprite with no matching <Icon id>) and the 39 ui_taom_bannericons_*.png sheets on disk that TAOMSpriteData.xml does not reference are safe to delete, or are pending a sheet that was never wired.
- Which module's colour block is genuinely last in the merged palette at runtime. The merge appends later modules' new rows (MBObjectManager.cs:820-873) and TAOM pins Native to LoadBeforeThis (SubModule.xml:25), so TAOM's <Color id="333"> should be last, but nobody has confirmed it in a running game and it silently sets the outline colour of every auto-generated banner (Banner.cs:349 and five siblings).

## configs-factions-and-world.md

- Nothing reads the shipped `diplomacy/diplomacy.json` in any test: `DiplomacyConfigProviderTests` only writes temp files. A missing or stale pair silently resolves `AllianceTier.Neutral` (`DiplomacyService.cs:46`) and no gate catches it. Whether that is a deliberate choice is not recorded anywhere.
- `culture_marketplace_config.xml` ships with zero `<Culture>` blocks, so the `<Blacklist>` and `<Boost>` sections that `culture-marketplace.md:60-82` documents have no shipped example. Whether they have ever been exercised in play is undocumented; the file's own header comment is the only reference.
- The `<Clan>` tier of the settlement-guard fallback chain is parsed (`SettlementGuardConfigProvider.cs:89-95`) but has zero shipped blocks, so the middle rung is untested on shipped data. Same for `<PrisonGuard>`, which is parsed at `:159` and appears nowhere in the file.
- `settlement-guards.md:88` says '14 settlements configured'; the file holds 16 `<Settlement>` blocks plus 1 `<Culture>` block. The doc was not updated when the last two were added, and no test pins the count.

## module-armory.md

- How cloth_bodies.xml and cloth_materials.xml are loaded: neither is in SubModule.xml nor project.mbproj, and 'cloth_bodies' appears nowhere in the shipping-client dump. Search the editor build, not _categories_v1.4.8.
- Which module-root folders the engine discovers with no registration at all (Languages/, Prefabs/, ModuleSounds/, SceneObj/, SceneEditData/ are all present and unregistered); the discovery code path was not found.
- Whether the Armory's stale <DependedModuleMetadata id="Native" version="v1.4.5.*"/> is enforced against the installed v1.4.8 or advisory; the launcher's version-constraint code was not read.
- How far docs/reference/lotrlome-armory-snapshot/ has drifted from the live files: the ledger stops 2026-08-20, live timestamps run to 2026-09-02, and contents were never diffed.
- Which hair_tag / beard_tag / tattoo_tag names are legal per race: declared in the per-race skins.xml blocks, matched natively, so the decompile cannot answer it. Working examples in Main/_Module/ModuleData/TAOM_bodyproperties.xml.
- The Armory ships no THIRD-PARTY-LICENSES.txt although it now redistributes Byak0's Alliance.Wargs assets and package_release.py ships the module by default. Stated as an unmet obligation, not invented as resolved.

## recipe-add-a-culture.md

- There is no documented minimum viable kingdom (how many clans, lords, heroes, settlements before it stops crashing). Only the failure list exists: kingdom-creation.md 'Known Crashes' plus the 14-row checklist. The single hard floor is one settlement, from LANDLESS_CULTURE.
- No required-versus-optional split exists for the <Culture> attributes. Three docs give three different counts (~50, ~80+, 92) and none marks which are mandatory. The chapter points at the deserializer-sourced table in docs/modding/cultures.md instead.
- banner_key / faction_banner_key grammar is undecoded. Keys under 100 characters are placeholders; the only documented practice is to copy a working key from the source clan. Banner.cs Deserialize / TryGetBannerDataFromCode was not opened this task.
- docs/cultures.md is stale in three places (equipment-set file location, the 'Gaps' table for Umbar/Dale/Lothlorien, and '12 wanderers'). Corrected in the chapter with measured evidence, but the source doc still needs an upstream fix.
- tools/validate_all_troop_refs.py sweeps a hardcoded list of 10 cultures while 16 troops_*.xml files exist. dunland, goblin, harad, mirkwood, rivendell and rohan are never swept. This is a real coverage hole, not just a doc error.
- LOTRLOME_Armory skins.xml registers 14 races but only 13 facegen pairs exist in action_sets.xml (sauron has none). Whether that is deliberate (sauron is never a player race) was not determined.

## recipe-add-a-kingdom.md

- Is the XSLT-row-before-plain-XML-row order inside Main/_Module/SubModule.xml load-bearing, or habit? Only the shipped positions are observable (Kingdoms at :70 and :130, NPCCharacters at :96 and :157) and no TAOM doc states whether the merge depends on it. Said so plainly in the chapter and pointed at load-order-and-dependencies.md.
- Is the four newest kingdoms' absence from special_resources_config.xml (18 of 22 kingdom ids named) and from the ten older kingdoms' <relationships> blocks (0 rows naming them) deliberate or an authoring gap? No issue, no doc, no test covers either. Recorded as measured facts, not as verdicts.
- Which <Culture> attributes are mandatory for a playable kingdom versus optional? Three docs give three different counts (~50, ~80+, 92) and none gives a required/optional split. Deferred to cultures.md and recipe-add-a-culture.md rather than guessed at.

## module-map.md

- How to paint a campaign-map navmesh for a LAND map: TAOM records only water tile numbers (shore 7, shallow 18, deep 19, under-bridge 25, river 11, unnavigable 10) in docs/warsails-custom-map-guide.md; face-group painting is the battle-scene TerrainType fallback (GetFaceTerrainType = (TerrainType)FaceGroupIndex) and is undocumented. Baked answer is SceneObj/Main_map/navmesh.bin.
- How the 1600 by 1600 campaign terrain was authored and imported: AssetSources/Support/ holds the candidate PNGs, minas-tirith-plan.md 1.3 covers a battle scene only. No doc names the live height source, its resolution, or the import step.
- The authoritative table of required scene tags and child entities per settlement kind. Measured tag counts (town 78, castle 193, village 609, wm_hideout 160) do not line up with the XML (78/143/607/159), so some entities carry more than one kind tag and nothing says which are mandatory. Would come from reading the live scene.xscene against SettlementVisual.OnStartup.
- Which of the two settlements_scripts entities carrying SettlementPositionScript the engine binds (vanilla SandBox has exactly one). Harmless or double-registering the distance-cache system is unknown.
- What gate_rotation (221 Town, 159 Hideout, 131 Village nodes) and the settlement-level type attribute do. Declared in XmlSchemas/Settlements.xsd, read by no managed code in either the shipping-client or editor v1.4.8 dump.
- How the 8 Atmospheres/ presets bind to the 46 SceneObj folders. Every per-scene atmosphere.xml is named scene_atmosphere; the preset filenames still carry the retired lotraom_/lotrtaom_ prefixes.
- Which module wins when two ship the same SceneObj/<name>/. The last-active-module rule is documented for Main_map only (MapScene.GetMainMapModule); audit_scene_names.py proves cross-module resolution works but no rule is written.
- What the Modding Kit New Module wizard actually scaffolds and in what order a new modder drives it. Nearest evidence is the 16 stub XMLs this module still carries.
- Runtime effect of the NavalDLC cache path: with NavalDLC active, SettlementPositionScript.OnInit also reads navigation types 2 and 3, which resolve to NavalDLC's caches keyed by settlement ids absent from this map. Stated in the chapter as UNVERIFIED.
