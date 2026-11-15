# Translation backlog

Feature work registers English source strings only. Per-language XMLs, translation caches and overrides belong to the project lead. Do not seed, generate or hand-edit them to clear coverage failures. See [the localization rule](../../.claude/skills/localize/SKILL.md).

Record each pending feature's module, source file, exact keys and English text, placeholders, UI context, target languages and status. Update existing entries instead of duplicating keys. The project lead marks entries complete after the translation pass.

## War Chronicle and coronation event window

- Status: pending project-lead translation pass.
- Source: [PR #1203](https://github.com/haterade22/TAOM/pull/1203).
- Module: TAOM.
- English source: `Main/_Module/ModuleData/taom_wotr_strings.xml`.
- Target languages: BR, CNs, CNt, DE, FR, IT, JP, KO, PL, RU, SP, TR.
- Preserve `{DAYS}` exactly; translate only the surrounding text.
- The two note strings are retained in the formatter/model but hidden by the current prefab. They are listed for source completeness, not as currently visible UI.
- The earlier language-file and cache additions have been removed from this PR. The rows exist as English placeholders since 08d76099a; the project lead's pass replaces them.

| Key | English source | Context |
| --- | --- | --- |
| `taom_event_category_chronicle` | War Chronicle | Chronicle category heading |
| `taom_event_category_coronation` | Coronation | Coronation category heading |
| `taom_event_effects` | Effects | Effects section heading |
| `taom_event_effect_days` | For {DAYS} days | Effect duration; preserve {DAYS} |
| `taom_event_side_free` | Free Peoples | Free Peoples target group |
| `taom_event_side_evil` | The Shadow | Shadow target group |
| `taom_event_effect_volunteers` | Volunteer recruitment rate | Recruitment effect label |
| `taom_event_effect_escape` | Additional escape roll | Escape effect label |
| `taom_event_effect_momentum` | War of the Ring momentum | Momentum effect label |
| `taom_event_effect_note` | Values are this event's contributions before caps, and the caps apply even to a single effect. Your own clan's fiefs and lords are never affected. | Note retained in model; currently hidden |
| `taom_event_effect_escape_note` | The additional escape roll's chance is the shown percentage of a lord's usual escape chance. | Note retained in model; currently hidden |

## Realms title tree details panel

- Status: pending project-lead translation pass.
- Source: [issue #1255](https://github.com/haterade22/TAOM/issues/1255).
- Module: TAOM.
- English source: `Main/_Module/ModuleData/taom_module_strings.xml`.
- Target languages: BR, CNs, CNt, DE, FR, IT, JP, KO, PL, RU, SP, TR.
- No placeholders. The Done button reuses the existing `taom_career_done` key.

| Key | English source | Context |
| --- | --- | --- |
| `taom_realms_tree_selected` | Selected title | Heading of the title tree popup's details panel, over the clicked title |
| `taom_realms_tree_holder` | Holder | Label over the name of the hero who holds the selected title |
| `taom_realms_tree_liege` | Liege | Heading over the title directly above the selected title |
| `taom_realms_tree_subordinates` | Subordinate titles | Heading over the list of titles directly below the selected title |

## Bandit foe lines no longer say enemy (#1281)

- Status: pending project-lead translation pass. Tracked in #1294.
- Source: PR #1281 (4548b0587).
- Module: TAOM.
- English source: `Main/_Module/ModuleData/taom_encounter_strings.xml`.
- Target languages: BR, DE, FR, IT, PL, SP, TR (63 rows). CNs, CNt, JP, KO and RU already hold the new English.
- The rows in those seven languages still hold the OLD English, so the translator never picks them up again (it picks a row only while it equals the current English). Reset each row to the new English first. No cache entry exists for these keys.
- The lines are gender-neutral and name nobody's people (the file header's rule); keep that in translation.

| Key | New English | Old English (still in the rows) |
| --- | --- | --- |
| `taom_enc_robbery_start_fight_rhun_raiders_foe_1` | Good. The Warlord pays well for heads, and asks no names. Surround them! | Good. The Warlord pays well for enemy heads. Surround them! |
| `taom_enc_robbery_pay_agreement_rhun_raiders_foe_2` | Then pay as the beaten pay: every coin, and quickly. | Then pay as an enemy pays: every coin, and quickly. |
| `taom_enc_robbery_threat_gondor_soldiers_foe_1` | You there, halt! We may have thrown off the Steward's colours, but we still know a fat purse when we see one. Hand it over, and quickly. | You there, halt! We may have thrown off the Steward's colours, but we still know an enemy when we see one. Your purse, and quickly. |
| `taom_enc_robbery_pay_agreement_gondor_soldiers_foe_1` | You would buy your way past us? Fine, a stranger's coin spends like any other. Make it a heavy offer. | You would buy your way past us? Fine, an enemy's coin spends like any other. Make it a heavy offer. |
| `taom_enc_robbery_start_fight_gondor_soldiers_foe_1` | Good. I have missed a fight with someone who stands their ground. At them! | Good. I have missed fighting an honest enemy. At them! |
| `taom_enc_robbery_threat_gondor_soldiers_foe_2` | Halt. Our banners are gone, but our swords still remember their work. Empty your saddlebags, or we empty them for you. | Halt. Our banners are gone, but our swords still remember the enemy. Empty your saddlebags, or we empty them for you. |
| `taom_enc_bandit_greet_gondor_soldiers_foe_1` | So now strangers come looking for us. We held against worse on the river, and we will hold here. | So the enemy comes looking for us now. We held against worse on the river, and we will hold here. |
| `taom_enc_hideout_boss_gondor_soldiers_foe` | You have come far into our camp, stranger. I will meet you alone, blade to blade, and show you what a captain of the Tower was worth. | You have come far into our camp, enemy. I will meet you alone, blade to blade, and show you what a captain of the Tower was worth. |
| `taom_enc_robbery_pay_agreement_erebor_warriors_foe_2` | Coin from an outsider is still coin. Set it on the rock where we can see it. | Coin from an enemy is still coin. Set it on the rock where we can see it. |

## Dale and Khand given names that matched lord names (#1281)

- Status: pending project-lead translation pass. Tracked in #1294.
- Source: PR #1281 (af1620ecd, for #1169).
- Module: TAOM.
- English source: `Main/_Module/ModuleData/taom_xslt_strings.xml`.
- Target languages: BR, CNs, CNt, DE, FR, IT, JP, KO, PL, RU, SP, TR (84 rows). Every language row still holds the old name, so non-English campaigns still give new heroes a lord's first name. Reset each row to the new English first.
- These are personal names: transliterate, do not translate.

| Key | New English | Old English (still in the rows) |
| --- | --- | --- |
| `aom_dale_male_name_8` | Hrafnkel | Torvald |
| `aom_dale_female_name_1` | Hallveig | Astrid |
| `aom_dale_female_name_5` | Gunnvor | Freydis |
| `aom_dale_female_name_17` | Ingrun | Sigrun |
| `aom_dale_female_name_19` | Asgerd | Svala |
| `aom_khand_male_name_13` | Arslan | Nogai |
| `aom_khand_female_name_17` | Toregene | Qutlugh |

## Placeholder weapon names (#1253)

- Status: pending. The keys are not yet registered in an English master, so no gate or translator sees them. Register them, then translate. Tracked in #1294.
- Source: PR #1253.
- Module: TAOM.
- English source: `Main/_Module/ModuleData/placeholder_weapons/ph_items.xml` and `ph_crafting_pieces.xml` (written by `tools/generate_placeholder_weapons.py`; `tools/_loc_sources.py` lists neither file).
- Target languages: BR, CNs, CNt, DE, FR, IT, JP, KO, PL, RU, SP, TR.

| Key | English source |
| --- | --- |
| `taom_ph_isengard_1h_sword_t7_name` | [Isengard] Orthanc Sword |
| `taom_ph_erebor_spear_a_t4_name` | [Erebor] War Spear I |
| `taom_ph_erebor_spear_b_t4_name` | [Erebor] War Spear II |
| `taom_ph_erebor_spear_a_t8_name` | [Erebor] Royal War Spear I |
| `taom_ph_erebor_spear_b_t8_name` | [Erebor] Royal War Spear II |
| `taom_ph_gondor_spear_b_t5_name` | [Gondor] Heavy Spear, Tempered |
| `taom_ph_gondor_spear_b_gondor_t5_name` | [Gondor] Heavy Spear, Tempered - Gondor |
| `taom_ph_gondor_spear_b_pg_t5_name` | [Gondor] Heavy Spear, Tempered - Pinnath Gelin |
| `taom_ph_gondor_spear_b_swan_t5_name` | [Gondor] Heavy Spear, Tempered - Swan Knight |
| `taom_ph_gondor_spear_b_t8_name` | [Gondor] Guard Spear |
| `taom_ph_gondor_spear_b_gondor_t8_name` | [Gondor] Guard Spear - Gondor |
| `taom_ph_gondor_spear_b_pg_t8_name` | [Gondor] Guard Spear - Pinnath Gelin |
| `taom_ph_gondor_spear_b_swan_t8_name` | [Gondor] Guard Spear - Swan Knight |
| `taom_ph_gondor_spear_a_t8_name` | [Gondor] Fountain Guard Spear, Tempered |
| `taom_ph_mirkwood_sword_t8_name` | [Mirkwood] Guard Sword |
| `taom_ph_mirkwood_sword_t10_name` | [Mirkwood] Royal Sword |
| `taom_ph_gundabad_spear_a02_t4_name` | [Gundabad] War Spear (added by #1296) |
| `taom_ph_gundabad_spear_a03_t5_name` | [Gundabad] Blackshield Spear (added by #1296) |
| `taom_ph_gundabad_spear_a03_t8_name` | [Gundabad] Blackshield Spear, Tempered (added by #1296) |
| `taom_ph_lothlorien_spear_t6_name` | [Lothlorien] Galadhrim Spear (added by #1296) |
| `taom_ph_isengard_1h_sword_t7_blade_name` | Isengard Sword Blade A (placeholder) |
| `taom_ph_erebor_spear_a_t4_blade_name` | Erebor Spear Blade A (placeholder) |
| `taom_ph_erebor_spear_b_t4_blade_name` | Erebor Spear Blade B (placeholder) |
| `taom_ph_erebor_spear_a_t8_blade_name` | Erebor Spear Blade A (placeholder) |
| `taom_ph_erebor_spear_b_t8_blade_name` | Erebor Spear Blade B (placeholder) |
| `taom_ph_gondor_spear_b_t5_blade_name` | Gondor Spear Head C (placeholder) |
| `taom_ph_gondor_spear_b_gondor_t5_blade_name` | Gondor Spear Head C (placeholder) |
| `taom_ph_gondor_spear_b_pg_t5_blade_name` | Gondor Spear Head C (placeholder) |
| `taom_ph_gondor_spear_b_swan_t5_blade_name` | Gondor Spear Head C (placeholder) |
| `taom_ph_gondor_spear_b_t8_blade_name` | Gondor Spear Head C (placeholder) |
| `taom_ph_gondor_spear_b_gondor_t8_blade_name` | Gondor Spear Head C (placeholder) |
| `taom_ph_gondor_spear_b_pg_t8_blade_name` | Gondor Spear Head C (placeholder) |
| `taom_ph_gondor_spear_b_swan_t8_blade_name` | Gondor Spear Head C (placeholder) |
| `taom_ph_gondor_spear_a_t8_blade_name` | Gondor Spear Head D (placeholder) |
| `taom_ph_mirkwood_sword_t8_blade_name` | [Mirkwood] Sword Blade (placeholder) |
| `taom_ph_mirkwood_sword_t10_blade_name` | [Mirkwood] Sword Blade (placeholder) |
| `taom_ph_gundabad_spear_a02_t4_blade_name` | Gundabad Orc Spear Blade II (placeholder) (added by #1296) |
| `taom_ph_gundabad_spear_a03_t5_blade_name` | Gundabad Orc Spear Blade III (placeholder) (added by #1296) |
| `taom_ph_gundabad_spear_a03_t8_blade_name` | Gundabad Orc Spear Blade III (placeholder) (added by #1296) |
| `taom_ph_lothlorien_spear_t6_blade_name` | High Elven Spear Blade (placeholder) (added by #1296) |

## Lord armour variant names (#1333)

- Status: pending. The keys are not yet registered in an English master, so no gate or translator sees them; the same state as the placeholder weapon names above. Register them, then translate. Tracked in #1129, the open backlog after #763 (both #763 and #1294 are closed). Until then the 20 names show in English in all 12 languages; the Mirkwood source pieces beside them show translated names (the Armory's `Languages/<lang>/loc_mirkwood.xml`), while the Armory ships no translation of the Harad source names.
- Source: issue #1333.
- Module: TAOM.
- English source: `Main/_Module/ModuleData/lord_armour/lord_armour_variants.xml` (written by `tools/generate_lord_armour_variants.py`; `tools/_loc_sources.py` does not list it).
- Target languages: BR, CNs, CNt, DE, FR, IT, JP, KO, PL, RU, SP, TR.

| Key | English source |
| --- | --- |
| `taom_lord_mkwd_inf3_chest_name` | [Mirkwood] Lord's Infantry Chestplate I |
| `taom_lord_mkwd_inf3_greaves_name` | [Mirkwood] Lord's Greaves I |
| `taom_lord_mkwd_inf3_chest_bronze_name` | [Mirkwood] Lord's Infantry Chestplate III |
| `taom_lord_mkwd_inf3_pauldrons_bronze_name` | [Mirkwood] Lord's Pauldrons III |
| `taom_lord_mkwd_inf3_greaves_bronze_name` | [Mirkwood] Lord's Greaves III |
| `taom_lord_mirkwood_body_name` | [Mirkwood] Lord's Palace Guard Body Armor |
| `taom_lord_mirkwood_boots_name` | [Mirkwood] Lord's Palace Guard Boots |
| `taom_lord_sk_hr_st_chest_inf_chest_light_a_name` | [Harwan] Lord's Infantry Chest A I |
| `taom_lord_sk_hr_st_chest_inf_chest_med_a_name` | [Harwan] Lord's Infantry Chest A II |
| `taom_lord_sk_hr_st_chest_inf_chest_med_b_name` | [Harwan] Lord's Infantry Chest B I |
| `taom_lord_sk_hr_st_chest_inf_chest_heavy_b_name` | [Harwan] Lord's Infantry Chest B II |
| `taom_lord_sk_hr_st_chest_cav_heavy_b_name` | [Harwan] Lord's Cavalry Chest B |
| `taom_lord_sk_hr_st_pauld_inf_med_a_name` | [Harwan] Lord's Infantry Pauldron A |
| `taom_lord_sk_hr_st_grvs_light_a_name` | [Harwan] Lord's Greaves A I |
| `taom_lord_sk_hr_st_grvs_med_a_name` | [Harwan] Lord's Greaves A II |
| `taom_lord_sk_hr_st_helmet_heavy_c_name` | [Harwan] Lord's Helmet C |
| `taom_lord_sk_hr_st_helmet_heavy_a_name` | [Harwan] Lord's Helmet A |
| `taom_lord_sk_hr_st_helmet_heavy_d_name` | [Harwan] Lord's Helmet D |
| `taom_lord_sk_hr_st_helmet_heavy_b_name` | [Harwan] Lord's Helmet B |
| `taom_lord_sk_sg_st_helmet_heavy_d_name` | [Shaghana] Lord's Helmet D |

## Momentum option hints (#1166)

- Status: pending. The two hints have no `{=KEY}` yet: add one, register it, then translate. Tracked in #1294.
- Source: PR #1166.
- Module: TAOM.
- English source: `Main/Features/TaomSettings.cs` (Momentum group).
- Target languages: BR, CNs, CNt, DE, FR, IT, JP, KO, PL, RU, SP, TR.

| Proposed key | English source | Context |
| --- | --- | --- |
| `taom_momentum_mcm_enabled_hint` | Tracks Evil vs Good war progress from battles, sieges, raids, armies and relative strength. The war ends at the victory threshold only when Enable Victory (End the War) is on; that setting is off by default, so the war is endless. Default: on. | Hint of Enable Momentum Tracking |
| `taom_momentum_mcm_victory_hint` | When OFF (default) the War of the Ring is endless: momentum is tracked but no side ever wins. Turn ON to let a side win at the Victory Threshold (or by eliminating the other), which ends the war and makes peace. Default: off (endless). | Hint of Enable Victory (End the War) |

## Custom Battle muster screen (#1118)

- Status: pending. Every string is hard-coded English: wrap each as `{=taom_tcb_*}`, register it, then translate. Tracked in #1294.
- Source: PR #1118.
- Module: TAOM.Client.
- English source: `Client/Features/CustomBattles/Muster/TcbVM.cs` (screen titles, buttons, popups and Status lines) and `TcbFieldVM.cs` (the Battlefield dropdown labels and choices).
- Target languages: BR, CNs, CNt, DE, FR, IT, JP, KO, PL, RU, SP, TR.
- Placeholders: the Status and total lines build numbers into the text; keep each number a `{VALUE}` placeholder when wrapping.

## Cheat menu results that name what they gave (#1297, #1317)

- Status: pending project-lead translation pass. The English rows are registered in `taom_module_strings.xml`.
- Source: #1297 (gold, influence, renown, heal); #1317 (relation, teleport).
- Module: TAOM.
- English source: `Main/_Module/ModuleData/taom_module_strings.xml` (used by `Main/Features/CheatMenu/CheatMenuTexts.cs`).
- Target languages: BR, CNs, CNt, DE, FR, IT, JP, KO, PL, RU, SP, TR.
- Placeholders: `{NUMBER}` is a formatted number (gold total, influence, renown, soldiers healed or lords changed).
  `{NAME}` is a kingdom's or a settlement's display name.

| Key | English source | Context |
| --- | --- | --- |
| `taom_cheat_done_gold` | Gold given. You now have {NUMBER} denars. | Result of the gold cheat; the number is the new total |
| `taom_cheat_done_influence` | Gained {NUMBER} influence. | Result of the influence cheat |
| `taom_cheat_done_renown` | Gained {NUMBER} renown. | Result of the renown cheat |
| `taom_cheat_done_heal` | Your party is healed. Soldiers healed: {NUMBER}. | Result of the heal cheat |
| `taom_cheat_done_relation` | Lords of {NAME} whose relation changed: {NUMBER}. | Result of the relation cheat; NAME is the kingdom |
| `taom_cheat_done_teleport` | You are at the gate of {NAME}. | Result of the teleport cheat; NAME is the town, castle or village |

## Realms options for realm names and banners (a4834c46c)

- Status: pending project-lead translation pass. The English rows are registered in `taom_module_strings.xml`; no language file has them yet (found by the #1297 review).
- Source: commit a4834c46c.
- Module: TAOM.
- English source: `Main/_Module/ModuleData/taom_module_strings.xml` (MCM options read by the Realms settings).
- Target languages: BR, CNs, CNt, DE, FR, IT, JP, KO, PL, RU, SP, TR.
- Keys: `taom_realms_mcm_rename_own`, `taom_realms_mcm_rename_own_hint`, `taom_realms_mcm_rename_ai`, `taom_realms_mcm_rename_ai_hint`, `taom_realms_mcm_own_banner`, `taom_realms_mcm_own_banner_hint`, `taom_realms_mcm_ai_banners`, `taom_realms_mcm_ai_banners_hint`. Read the English in the source file; the hints are long.
