TAOM is a Lord of the Rings total-conversion mod for Mount & Blade II: Bannerlord (.NET Framework 4.7.2). You are reviewing a small data-and-test fix on branch bannerlord-1.5.x (trunk), uncommitted in the working tree on top of c142ba2c9. Review only; do not edit any file.

## Why this change exists

Trunk at c142ba2c9 failed five tests:

1. `TAOM.Tests` `LanguageFileCoverageTests.EveryLanguage_DeclaresARowForEveryEnglishKey`: 91 English keys had no row in any of the 12 language files.
2. `TAOM.Tests` `LongDefeatHudStringsTests.EveryKey_IsSeededInAllTwelveLanguageFiles`: 14 of those keys (`TAOM_LD_*`).
3. `tools/tests/test_banner_icon_ids.py` `test_every_shipped_banner_icon_exists`: `clans.xml:673` named icons 17006, 17007, 17008 and `clans.xml:1204` named 10505, which `banner_icons.xml` no longer defines.
4. `tools/tests/test_banner_icon_ids.py` `test_registries_are_not_suspiciously_small`: "472 not greater than 500".
5. `tools/tests/test_generate_new_faction_kingdoms.py`: `generate_new_faction_kingdoms.py --only eredluin --check` reported drift ("would change: clans.xml").

Causes (read from git): commit 9a409deac (PR #1265, a contributor) repacked the TAOM banner sheets and deleted 163 `<Icon>` rows it described as unused; four were still used by `clan_mirkwood_4` and `clan_shaghana_3`. Commit f5b97a942 (same PR) changed `clan_eredluin_3`'s `banner_key` in `clans.xml` by hand, inside the region `tools/generate_new_faction_kingdoms.py` generates. The 91 keys came from six commits that added English strings without the seed step.

## Decisions by the maintainer (do not re-litigate)

- Repoint the two clan banners to icons that still exist, instead of restoring the deleted art.
- Seed all 12 languages with English placeholder rows using the free tool path the coverage test's own failure message names: `python tools/translate_with_claude.py --lang <L> --module TAOM --sync-ids --seed-only --apply`. Translating them is backlog #763.

## The diff

Language files: 24 files, rows added only (87 per `std_taom_module_strings_*.xml`, 4 per `std_taom_keybind_strings_*.xml`). One language is shown in full below; the other eleven received the same ids. All other changed files are shown in full.

```diff
diff --git a/Main/_Module/ModuleData/Languages/DE/std_taom_keybind_strings_deu-DE.xml b/Main/_Module/ModuleData/Languages/DE/std_taom_keybind_strings_deu-DE.xml
index fb27cddb2..3a818c263 100644
--- a/Main/_Module/ModuleData/Languages/DE/std_taom_keybind_strings_deu-DE.xml
+++ b/Main/_Module/ModuleData/Languages/DE/std_taom_keybind_strings_deu-DE.xml
@@ -47,5 +47,9 @@
     <string id="taom_key_seat_slow_desc" text="Senkt in einer Schlacht die Missionszeit um eine Stufe, bis auf ein Viertel der normalen Geschwindigkeit. Standard: Bild ab." />
     <string id="taom_key_seat_stay" text="Zeit anhalten (TAOM)" />
     <string id="taom_key_seat_stay_desc" text="Hebt in einer Schlacht die beschleunigte Zeit auf und pausiert die Mission; erneut drücken, um fortzufahren. Der Blick des Sitzes fliegt auch während der Pause weiter. Standard: Pos1." />
+    <string id="taom_key_cheat_menu" text="Open Cheat Menu (TAOM)" />
+    <string id="taom_key_cheat_menu_desc" text="Opens the TAOM cheat menu on the campaign map, like the Cheats button. Works only while Enable Cheat Menu is on in Mod Options, in single player. Defaults to H." />
+    <string id="taom_key_battle_cheat_menu" text="Open Battle Cheat Menu (TAOM)" />
+    <string id="taom_key_battle_cheat_menu_desc" text="Opens the TAOM battle cheat list in a mission, like the Cheats item in the Esc menu. Works only while Enable Cheat Menu is on in Mod Options, in single player. Defaults to H." />
   </strings>
 </base>
diff --git a/Main/_Module/ModuleData/Languages/DE/std_taom_module_strings_deu-DE.xml b/Main/_Module/ModuleData/Languages/DE/std_taom_module_strings_deu-DE.xml
index 9d0ae00c2..8cba307d0 100644
--- a/Main/_Module/ModuleData/Languages/DE/std_taom_module_strings_deu-DE.xml
+++ b/Main/_Module/ModuleData/Languages/DE/std_taom_module_strings_deu-DE.xml
@@ -7949,6 +7949,93 @@
     <string id="taom_realms_ui_primary" text="Make primary title" />
     <string id="taom_realms_ui_primary_ask" text="Be known by the {TITLE} from now on? Your style and party name follow your primary title, and so does your realm's banner where the title has one." />
     <string id="taom_patch_self_disabled" text="TAOM: some fixes turned themselves off: {FEATURES}. They stay off for the rest of this battle or this session; the TAOM log names the cause." />
+    <string id="taom_bugreport_offer_title" text="TAOM: the last game ended unexpectedly" />
+    <string id="taom_bugreport_offer_text" text="Your last game closed without TAOM's normal shutdown. That happens after a crash, after a freeze you had to close, or when the game was stopped some other way.{newline}{newline}Make a bug report now? TAOM puts the logs from that game into one zip file that you can attach to your report." />
+    <string id="taom_bugreport_offer_yes" text="Make bug report" />
+    <string id="taom_bugreport_offer_no" text="No" />
+    <string id="taom_bugreport_done_title" text="TAOM bug report" />
+    <string id="taom_bugreport_done_text" text="Your bug report is ready:{newline}{PATH}{newline}{newline}Attach this file to your bug report and say what you were doing when the problem happened." />
+    <string id="taom_bugreport_failed_text" text="The bug report could not be written. Please attach the newest taom_debug log from this folder instead:{newline}{PATH}" />
+    <string id="taom_bugreport_close" text="Close" />
+    <string id="taom_bugreport_open_folder" text="Open folder" />
+    <string id="taom_cheat_bad_amount" text="That amount is not allowed. The limit is {MAX}." />
+    <string id="taom_cheat_battle_done_heal_self" text="You are back to full health, and so is your mount if you ride one." />
+    <string id="taom_cheat_battle_done_heal_side" text="{NUMBER} soldiers and mounts of your side are back to full health." />
+    <string id="taom_cheat_battle_done_kill" text="{NUMBER} enemies were killed." />
+    <string id="taom_cheat_battle_done_rout" text="{NUMBER} enemies are fleeing." />
+    <string id="taom_cheat_battle_done_spawn_ally" text="{NUMBER} allied troops joined the battle." />
+    <string id="taom_cheat_battle_done_spawn_enemy" text="{NUMBER} enemy troops joined the battle." />
+    <string id="taom_cheat_battle_item" text="Cheats" />
+    <string id="taom_cheat_battle_none" text="Start a battle first. Battle cheats work only while a battle is on." />
+    <string id="taom_cheat_battle_nothing" text="Nothing changed: there is nobody in this battle this cheat can act on." />
+    <string id="taom_cheat_battle_opt_heal_self" text="Heal yourself" />
+    <string id="taom_cheat_battle_opt_heal_side" text="Heal your side" />
+    <string id="taom_cheat_battle_opt_kill" text="Kill all enemies" />
+    <string id="taom_cheat_battle_opt_rout" text="Rout the enemy" />
+    <string id="taom_cheat_battle_opt_spawn_ally" text="+10 allied troops" />
+    <string id="taom_cheat_battle_opt_spawn_enemy" text="+10 enemy troops" />
+    <string id="taom_cheat_battle_text" text="Cheats change this battle. New troops are copies of soldiers already on that side." />
+    <string id="taom_cheat_battle_title" text="Battle cheats" />
+    <string id="taom_cheat_coop" text="Cheats are not available in co-op." />
+    <string id="taom_cheat_failed" text="That cheat failed. The TAOM log has the details." />
+    <string id="taom_cheat_done" text="Done: {NAME} {NUMBER}" />
+    <string id="taom_cheat_mcm_enable" text="Enable Cheat Menu" />
+    <string id="taom_cheat_mcm_enable_hint" text="Adds a Cheats button to the bottom bar of the campaign map. It opens a menu that teleports your party to a settlement and gives gold, troops, your special resource, influence, renown and relations, and heals your party. In a mission, the Esc menu has a Cheats item (or press H) that heals you or your side, routs or kills the enemy, or adds 10 allied or enemy troops. Cheats change your save, and most cannot be undone. The button appears the next time the campaign map opens, for example after loading a save. The H key on the campaign map also opens the menu (change either key under Options, Keybindings). Single-player only. Default: off." />
+    <string id="taom_cheat_mcm_group" text="Cheat Menu" />
+    <string id="taom_cheat_menu_text" text="TAOM cheat menu. Cheats change your save, and most cannot be undone." />
+    <string id="taom_cheat_nav_busy" text="The cheat menu opens from the free campaign map." />
+    <string id="taom_cheat_nav_label" text="Cheats" />
+    <string id="taom_cheat_nothing" text="Nothing changed: your hero has nothing this cheat can give to." />
+    <string id="taom_cheat_opt_back" text="Back" />
+    <string id="taom_cheat_opt_close" text="Close" />
+    <string id="taom_cheat_opt_gold" text="Gold: +10,000 denars" />
+    <string id="taom_cheat_opt_heal" text="Heal your party and your heroes" />
+    <string id="taom_cheat_opt_influence" text="Influence: +100" />
+    <string id="taom_cheat_opt_relation" text="Relations: +20 with every lord of a kingdom..." />
+    <string id="taom_cheat_opt_renown" text="Renown: +100" />
+    <string id="taom_cheat_opt_resource" text="Special resource: +1,000" />
+    <string id="taom_cheat_opt_teleport" text="Teleport to a settlement..." />
+    <string id="taom_cheat_opt_troops" text="Troops..." />
+    <string id="taom_cheat_pick_cancel" text="Cancel" />
+    <string id="taom_cheat_pick_kingdom" text="Choose a kingdom" />
+    <string id="taom_cheat_pick_ok" text="Choose" />
+    <string id="taom_cheat_pick_settlement" text="Choose where to teleport" />
+    <string id="taom_cheat_teleport_text" text="Teleport to a settlement's gate. You arrive outside and enter as usual." />
+    <string id="taom_cheat_tp_army" text="Leave the army first. Only an army's leader can teleport it." />
+    <string id="taom_cheat_tp_camp" text="Break camp first, then teleport." />
+    <string id="taom_cheat_tp_castles" text="Castles..." />
+    <string id="taom_cheat_tp_encounter" text="Finish the battle or encounter first." />
+    <string id="taom_cheat_tp_mission" text="You cannot teleport during a battle or a scene." />
+    <string id="taom_cheat_tp_nocampaign" text="Start or load a campaign first." />
+    <string id="taom_cheat_tp_parked" text="You cannot teleport while you serve in another lord's party." />
+    <string id="taom_cheat_tp_prisoner" text="You cannot teleport while you are a prisoner." />
+    <string id="taom_cheat_tp_sea" text="Go ashore first, then teleport." />
+    <string id="taom_cheat_tp_settlement" text="Leave the settlement first, then teleport." />
+    <string id="taom_cheat_tp_siege" text="End the siege first." />
+    <string id="taom_cheat_tp_towns" text="Towns..." />
+    <string id="taom_cheat_tp_villages" text="Villages..." />
+    <string id="taom_cheat_troops_basic" text="+10 basic recruits" />
+    <string id="taom_cheat_troops_elite" text="+10 elite recruits" />
+    <string id="taom_cheat_troops_text" text="Add recruits of your culture to your party." />
+    <string id="taom_cheat_unknown" text="Nothing called &quot;{NAME}&quot; was found." />
+    <string id="TAOM_LD_Up_Axemen" text="Militia to Westfold Axemen" />
+    <string id="TAOM_LD_Up_VeteranAxemen" text="Axemen to Veteran Axemen" />
+    <string id="TAOM_LD_Up_HeavyAxemen" text="Axemen to Heavy Axemen" />
+    <string id="TAOM_LD_Up_Helmingas" text="Veteran Axemen to Helmingas Axemen" />
+    <string id="TAOM_LD_Up_HelmingasHeavy" text="Heavy Axemen to Helmingas Heavy Axemen" />
+    <string id="TAOM_LD_Up_Bowmen" text="Yeomen to Eastfold Bowmen" />
+    <string id="TAOM_LD_Up_VeteranBowmen" text="Bowmen to Veteran Bowmen" />
+    <string id="TAOM_LD_Up_RoyalGuard" text="King's Knights to the King's Royal Guard" />
+    <string id="TAOM_LD_Up_Riders" text="Royal Guard to the Riders of Rohan" />
+    <string id="TAOM_LD_Hud_UpgradesTitle" text="Upgrade your soldiers" />
+    <string id="TAOM_LD_Hud_UpgradeDetail" text="{STANDING} standing, gold for {AFFORDABLE}" />
+    <string id="TAOM_LD_Hud_UpgradePrice" text="{PRICE} gold each" />
+    <string id="TAOM_LD_Notice_UnknownUpgrade" text="That upgrade is not for sale." />
+    <string id="TAOM_LD_Notice_NoneToUpgrade" text="No soldier of that troop is left to upgrade." />
+    <string id="taom_realms_tree_selected" text="Selected title" />
+    <string id="taom_realms_tree_holder" text="Holder" />
+    <string id="taom_realms_tree_liege" text="Liege" />
+    <string id="taom_realms_tree_subordinates" text="Subordinate titles" />
   </strings>
 
 </base>
diff --git a/Main/_Module/ModuleData/characters/clans.xml b/Main/_Module/ModuleData/characters/clans.xml
index 8bb457130..50d59e2c1 100644
--- a/Main/_Module/ModuleData/characters/clans.xml
+++ b/Main/_Module/ModuleData/characters/clans.xml
@@ -670,7 +670,7 @@
 		color="FF215F32"
 		color2="FFA7C681"
 		default_party_template="PartyTemplate.kingdom_hero_party_mirkwood_mirkwood_4_template"
-		banner_key="11.139.166.1528.1528.764.764.1.0.0.505.44.171.499.499.764.764.0.0.43.505.138.171.476.476.764.764.0.0.43.503.44.116.375.375.765.763.0.0.0.503.44.116.38.53.764.608.0.0.0.503.44.116.38.53.764.920.0.0.0.503.44.116.38.53.920.764.0.0.89.503.44.116.38.53.608.764.0.0.89.503.44.116.38.53.654.655.0.0.44.503.44.116.75.75.765.761.0.0.0.503.44.116.38.53.873.655.0.1.314.503.44.116.38.53.873.873.0.0.-134.503.44.116.38.53.655.873.0.1.134.503.44.116.38.53.707.905.0.1.153.503.44.116.38.53.821.905.0.0.203.503.44.116.38.53.707.623.0.0.24.503.44.116.38.53.821.623.0.1.-25.503.44.116.38.53.906.707.0.0.-65.503.44.116.38.53.904.821.0.1.-116.503.44.116.38.53.621.707.0.1.64.503.44.116.38.53.623.821.0.0.115.17006.293.116.400.400.765.765.1.0.0.17007.297.116.375.375.772.765.1.0.0.17008.293.116.150.150.765.760.0.0.0" />
+		banner_key="11.139.166.1528.1528.764.764.1.0.0.505.44.171.499.499.764.764.0.0.43.505.138.171.476.476.764.764.0.0.43.503.44.116.375.375.765.763.0.0.0.503.44.116.38.53.764.608.0.0.0.503.44.116.38.53.764.920.0.0.0.503.44.116.38.53.920.764.0.0.89.503.44.116.38.53.608.764.0.0.89.503.44.116.38.53.654.655.0.0.44.503.44.116.75.75.765.761.0.0.0.503.44.116.38.53.873.655.0.1.314.503.44.116.38.53.873.873.0.0.-134.503.44.116.38.53.655.873.0.1.134.503.44.116.38.53.707.905.0.1.153.503.44.116.38.53.821.905.0.0.203.503.44.116.38.53.707.623.0.0.24.503.44.116.38.53.821.623.0.1.-25.503.44.116.38.53.906.707.0.0.-65.503.44.116.38.53.904.821.0.1.-116.503.44.116.38.53.621.707.0.1.64.503.44.116.38.53.623.821.0.0.115.15007.293.116.400.400.765.765.1.0.0.10017.297.116.375.375.772.765.1.0.0.15009.293.116.150.150.765.760.0.0.0" />
 
   <!-- Nos Alenwé -->
   <Faction
@@ -1201,7 +1201,7 @@
 		color="FF91371D"
 		color2="FF4C2014"
 		default_party_template="PartyTemplate.kingdom_hero_party_shaghana_shaghana_3_template"
-		banner_key="11.146.166.1528.1528.764.764.1.0.0.503.143.171.316.316.763.615.0.0.0.503.148.171.314.314.763.619.0.0.0.505.146.171.1074.314.763.701.0.0.180.504.131.171.446.177.690.766.0.1.270.504.77.171.409.162.690.766.0.1.270.504.131.171.459.182.834.759.0.0.90.504.77.171.421.167.834.759.0.0.90.504.143.171.284.744.761.780.0.0.180.504.148.171.278.728.761.773.0.0.180.504.143.171.42.131.764.440.0.0.0.504.143.171.36.128.821.449.0.0.-18.504.143.171.34.120.868.475.0.0.-33.504.143.171.29.108.901.514.0.0.-50.504.143.171.36.128.707.449.0.1.17.504.143.171.34.120.660.475.0.1.32.504.143.171.29.108.627.514.0.1.48.505.148.171.270.13.765.565.0.0.180.505.143.171.33.1475.993.767.0.0.180.505.148.171.130.1475.1039.744.0.0.180.505.142.171.474.1475.1270.744.0.0.180.505.143.171.33.1475.534.767.0.1.180.505.148.171.130.1475.489.744.0.1.180.505.142.171.474.1475.258.744.0.1.180.222.131.171.50.50.504.462.0.1.180.222.131.171.50.50.504.1065.0.1.180.222.131.171.50.50.504.617.0.1.180.222.131.171.50.50.504.764.0.1.180.222.131.171.50.50.505.911.0.1.180.222.131.171.50.50.1024.462.0.1.180.222.131.171.50.50.1024.1065.0.1.180.222.131.171.50.50.1024.617.0.1.180.222.131.171.50.50.1024.764.0.1.180.222.131.171.50.50.1025.911.0.1.180.427.131.171.132.132.764.604.0.1.0.515.131.171.97.97.764.546.0.1.0.515.131.171.97.97.764.546.0.1.45.500.91.171.44.44.764.545.0.1.45.10505.88.116.250.250.762.756.1.0.0" />
+		banner_key="11.146.166.1528.1528.764.764.1.0.0.503.143.171.316.316.763.615.0.0.0.503.148.171.314.314.763.619.0.0.0.505.146.171.1074.314.763.701.0.0.180.504.131.171.446.177.690.766.0.1.270.504.77.171.409.162.690.766.0.1.270.504.131.171.459.182.834.759.0.0.90.504.77.171.421.167.834.759.0.0.90.504.143.171.284.744.761.780.0.0.180.504.148.171.278.728.761.773.0.0.180.504.143.171.42.131.764.440.0.0.0.504.143.171.36.128.821.449.0.0.-18.504.143.171.34.120.868.475.0.0.-33.504.143.171.29.108.901.514.0.0.-50.504.143.171.36.128.707.449.0.1.17.504.143.171.34.120.660.475.0.1.32.504.143.171.29.108.627.514.0.1.48.505.148.171.270.13.765.565.0.0.180.505.143.171.33.1475.993.767.0.0.180.505.148.171.130.1475.1039.744.0.0.180.505.142.171.474.1475.1270.744.0.0.180.505.143.171.33.1475.534.767.0.1.180.505.148.171.130.1475.489.744.0.1.180.505.142.171.474.1475.258.744.0.1.180.222.131.171.50.50.504.462.0.1.180.222.131.171.50.50.504.1065.0.1.180.222.131.171.50.50.504.617.0.1.180.222.131.171.50.50.504.764.0.1.180.222.131.171.50.50.505.911.0.1.180.222.131.171.50.50.1024.462.0.1.180.222.131.171.50.50.1024.1065.0.1.180.222.131.171.50.50.1024.617.0.1.180.222.131.171.50.50.1024.764.0.1.180.222.131.171.50.50.1025.911.0.1.180.427.131.171.132.132.764.604.0.1.0.515.131.171.97.97.764.546.0.1.0.515.131.171.97.97.764.546.0.1.45.500.91.171.44.44.764.545.0.1.45.10500.88.116.250.250.762.756.1.0.0" />
   <Faction
 		id="clan_shaghana_4"
 		initial_home_settlement="Settlement.town_A10"
diff --git a/tools/generate_new_faction_kingdoms.py b/tools/generate_new_faction_kingdoms.py
index 517586f01..f77a87690 100644
--- a/tools/generate_new_faction_kingdoms.py
+++ b/tools/generate_new_faction_kingdoms.py
@@ -212,7 +212,7 @@ KINGDOMS = {
         clan_banner_keys={
             "clan_eredluin_1": "11.292.200.1528.1528.764.764.1.0.0.31007.110.110.523.523.764.764.0.0.0",
             "clan_eredluin_2": "11.116.116.1528.1528.764.764.0.0.0.515.173.173.33.33.757.587.0.0.0.515.173.173.33.33.656.622.0.0.0.515.173.173.33.33.858.622.0.0.0.515.173.173.33.33.587.706.0.0.0.515.173.173.33.33.926.706.0.0.0.515.173.173.33.33.556.809.0.0.0.515.173.173.33.33.957.809.0.0.0.503.114.114.350.350.754.791.0.0.0.505.193.193.758.758.754.1159.0.0.45.505.134.134.220.220.754.849.0.0.45.505.193.193.392.392.754.1095.0.0.45",
-            "clan_eredluin_3": "11.149.149.1528.1528.764.764.1.0.0.509.240.240.588.587.764.764.0.0.23.509.222.222.558.557.764.764.0.0.23.500.240.240.407.407.764.764.0.0.0.503.161.161.397.396.764.764.0.0.0.24008.228.228.332.314.764.763.0.0.0.515.228.228.25.25.764.558.0.0.0.515.228.228.25.25.910.618.0.0.0.515.228.228.25.25.970.764.0.0.0.515.228.228.25.25.910.910.0.0.0.515.228.228.25.25.764.970.0.0.0.515.228.228.25.25.618.910.0.0.0.515.228.228.25.25.558.764.0.0.0.515.228.228.25.25.618.618.0.0.0",
+            "clan_eredluin_3": "11.1.1.1528.1528.764.764.1.0.0.503.38.38.532.532.761.756.0.0.0.503.116.116.494.494.761.756.0.0.0.31026.48.48.271.271.764.723.0.0.0.31025.0.0.419.419.764.764.0.0.0.31024.0.0.374.381.762.760.0.0.0.505.123.123.528.528.764.1020.0.0.45.505.123.123.402.402.764.948.0.0.45.24024.123.38.493.493.764.791.1.0.0.510.97.97.411.86.764.882.0.0.0.31028.38.38.77.77.698.918.0.0.0.31029.38.38.77.77.764.918.0.0.0.31030.38.38.77.77.823.918.0.0.0.31027.38.38.123.123.764.680.0.0.315.31027.38.38.118.118.764.680.0.1.45",
             "clan_eredluin_4": "11.100.100.1528.1528.764.764.0.0.0.504.173.173.1104.939.764.765.0.0.180.504.101.101.1104.939.764.661.0.0.180.504.173.173.356.483.765.711.0.0.0.504.173.173.169.241.657.702.0.0.0.504.173.173.169.241.873.702.0.0.0.504.173.173.363.309.764.846.0.0.180.24025.102.173.128.153.661.750.0.0.0.24025.102.173.128.153.870.750.0.0.0.504.173.173.304.402.764.730.0.0.0.24025.102.173.307.339.766.731.0.0.0.505.173.173.114.114.764.835.0.0.45.505.173.173.51.51.657.840.0.0.56.505.173.173.51.51.871.840.0.1.304.505.293.293.63.63.764.814.0.0.45.505.173.173.63.63.764.861.0.0.45.24510.293.293.39.39.555.747.0.0.45.24510.293.293.39.39.974.747.0.0.45.24510.293.293.39.39.623.826.0.0.45.24510.293.293.39.39.906.826.0.0.45.24510.293.293.39.39.696.901.0.0.45.24510.293.293.39.39.833.901.0.0.45.24510.293.293.42.43.764.977.0.0.0.515.20.20.34.34.764.533.0.0.0.515.20.20.34.34.601.588.0.0.0.515.20.20.34.34.928.588.0.0.0.505.101.100.1110.273.765.278.0.0.0",
         },
         clan_colors=[("FF3E5A7A", "FFB8C4CC"), ("FF34495E", "FFC8CCD0"), ("FF6A3A2A", "FFE0B080"),
@@ -278,7 +278,7 @@ KINGDOMS = {
                       "Gaila"],
         party_template="PartyTemplate.kingdom_hero_party_angaladh_template",
         # Icons no other clan flies (Mike 2026-10-28): Silvan 17000 and 17001 and the plain sun 20001.
-        # Mirkwood's Nos Galengrim flies 17006, 17007 and 17008. Off-white (palette 2022, the realm's
+        # Mirkwood's Nos Galengrim flies 15007, 10017 and 15009. Off-white (palette 2022, the realm's
         # color2 #F2F4F7, which the engine paints a clan's sigil in) on forest green (2021).
         banner="11.2021.2021.1528.1528.764.764.1.0.0.{icon}.{fg}.{fg}.700.700.764.764.0.0.0",
         kingdom_icon=17000, clan_icons=[17000, 17001, 20001],
diff --git a/tools/tests/test_banner_icon_ids.py b/tools/tests/test_banner_icon_ids.py
index d94069487..398d0c2b7 100644
--- a/tools/tests/test_banner_icon_ids.py
+++ b/tools/tests/test_banner_icon_ids.py
@@ -121,7 +121,7 @@ class ShippedBannerDataTests(unittest.TestCase):
         cls.keys = {f: parse_keys(t) for f, t in cls.texts.items()}
 
     def test_registries_are_not_suspiciously_small(self):
-        self.assertGreater(len(self.icons), 500, "icon registry suspiciously small: the parse found too few icons")
+        self.assertGreater(len(self.icons), 400,"icon registry suspiciously small: the parse found too few icons")
         self.assertGreater(len(self.colours), 100, "colour registry suspiciously small: the parse found too few colours")
 
     def test_every_banner_key_occurrence_is_parsed(self):
```

Replacement icons were chosen by eye from `Main/_Module/GUI/SpriteParts/ui_taom_bannericons/<id>.png`: 17006 -> 15007, 17007 -> 10017, 17008 -> 15009 (clan_mirkwood_4), 10505 -> 10500 (clan_shaghana_3). In a banner key, the first 10 tokens are the background, then each layer is 10 tokens: icon id, colour 1, colour 2, size x, size y, position x, position y, stroke, mirror, rotation.

## Already confirmed by the implementing session (re-verify; do not take on faith)

- `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`: 0 failed, 40790 passed, 9 skipped.
- `python -m pytest tools/tests`: 9099 passed, 13 skipped, 0 failed.
- `python tools/pack_banner_sheets.py --audit`: `clean`, exit 0.
- `python tools/generate_new_faction_kingdoms.py --only eredluin --check`: exit 0, "unchanged: all four files".
- `python tools/validate_moduledata.py`: exit 0.
- `git diff --stat`: clans.xml 2 lines changed (no whole-file rewrite); the clans.xml and generator edits were made by binary round-trip scripts that keep BOM and CRLF.

## Known Suspects: CONFIRM or DISPUTE each with evidence (file:line, or the installed engine DLLs via `pwsh tools/taom-src.ps1 path <Type>`)

1. **Saves made before the repack.** A clan's banner is stored in the save. Does the engine re-read `banner_key` from `clans.xml` for an existing save, or keep the saved banner? If a pre-v2.0.40 save (or a player-made banner) names an icon id that `banner_icons.xml` no longer defines (17006 to 17008, 10505, or any other of the 163 rows PR #1265 removed), what does the engine do on load, on the map, in the encyclopedia and in the banner editor: skip the layer, draw a default, or throw? Quote the engine line. This exposure is PR #1265's, not this diff's, but it decides whether this fix is complete for players.
2. **English placeholders read as translated.** Do any TAOM tools (`tools/check_stale_loc_rows.py`, `tools/translation_overrides/`, `tools/translate_with_claude.py` discovery, loc length or coverage reports) or C# loc tests treat an English row in a non-English file as already translated, so a later paid translation run would skip these 91 rows?
3. **Floor 400.** `test_registries_are_not_suspiciously_small` guards against a broken parse. With 472 icons now parsed (vanilla plus 235 TAOM), is 400 a sound floor, or does it now pass on a parse that drops a whole group of icons? Is the colour floor of 100 beside it still sound?
4. **Generator as the source.** After the line 215 sync, does a regeneration (`--only eredluin --apply`) reproduce the hand-edited `clan_eredluin_3` banner byte for byte? Is the comment at generator line 281 now true (which clans fly 15007, 10017 and 15009)?
5. **Keybind rows.** Are the 4 seeded ids in each `std_taom_keybind_strings_*.xml` ids the English keybind source declares, in the schema that file uses?
6. **Replacement icons.** Do 15007, 10017, 15009 and 10500 each resolve to a non-pattern `BannerIconGroup` row with a valid sheet and cell, so the engine can draw them as clan sigil layers?

## What NOT to flag

- The visual choice of replacement icons, beyond a real defect (a missing, pattern-only or wrong-sheet icon).
- Translation quality: the seeded rows are English placeholders by design (#763).
- These modified files belong to another session and will not be committed with this change: `TAOM.Tests/Features/ArmyTargeting/WarTheaterConfigInvariantsTests.cs`, `TAOM.Tests/Features/BattleBalance/ShippedBattleBalanceConfigTests.cs`, `TAOM.Tests/Features/BattleBalance/TaomPartyHealingModelTests.cs`, `TAOM.Tests/Features/Diplomacy/DiplomacyShippedConfigTests.cs`, and the live `LOTRLOME_Armory` ModuleData armour files.

Report using your standard P1/P2/P3 severity format, citing exact `file:line` for every claim, and flag disagreement with the "Already confirmed" list explicitly if you find any of it to be wrong.
