<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
	<!-- Identity transformation - copies everything by default -->
	<xsl:output omit-xml-declaration="no" indent="yes"/>

	<xsl:template match="@*|node()">
		<xsl:copy>
			<xsl:apply-templates select="@*|node()"/>
		</xsl:copy>
	</xsl:template>


	<!-- Rename empire to Dunlending -->
	<xsl:template match="Culture[@id='empire']">
		<xsl:copy>
			<!-- Copy all vanilla attributes first, then override the ones we change -->
			<xsl:apply-templates select="@*"/>

			<!-- Name and description -->
			<xsl:attribute name="name">{=aom_empire_name}Dunlendings</xsl:attribute>
			<xsl:attribute name="text">{=aom_empire_desc}The Dunlendings are a rugged and fierce people, dwelling in the wild hills and untamed lands west of Rohan. Long resentful of the Rohirrim, whom they see as invaders of their ancestral homes, the Dunlendings are united by their shared heritage and fierce independence. Skilled in guerrilla warfare and survival, they rely on cunning and ferocity to overcome their enemies. Though often dismissed as simple hill folk, the Dunlendings are a proud and enduring people, fiercely protective of their lands and ways of life. Driven by a deep-seated grudge against Rohan, they have allied themselves with the forces of darkness, seeking revenge and the restoration of their lost lands.</xsl:attribute>
			<xsl:attribute name="color">0xFF4A3B2A</xsl:attribute>
			<xsl:attribute name="color2">0xFF7A5A36</xsl:attribute>

			<!-- Troop references -->
			<xsl:attribute name="basic_troop">NPCCharacter.dunland_peasant</xsl:attribute>
			<xsl:attribute name="elite_basic_troop">NPCCharacter.dunland_noble_son</xsl:attribute>
			<xsl:attribute name="melee_militia_troop">NPCCharacter.dunland_peasant</xsl:attribute>
			<xsl:attribute name="ranged_militia_troop">NPCCharacter.dunland_peasant</xsl:attribute>
			<xsl:attribute name="melee_elite_militia_troop">NPCCharacter.dunland_noble_son</xsl:attribute>
			<xsl:attribute name="ranged_elite_militia_troop">NPCCharacter.dunland_noble_son</xsl:attribute>

			<!-- Party templates -->
			<xsl:attribute name="villager_party_template">PartyTemplate.villager_empire_template</xsl:attribute>
			<xsl:attribute name="default_party_template">PartyTemplate.kingdom_hero_party_dunland_template</xsl:attribute>
			<xsl:attribute name="caravan_party_template">PartyTemplate.caravan_template_empire</xsl:attribute>
			<xsl:attribute name="elite_caravan_party_template">PartyTemplate.elite_caravan_template_empire</xsl:attribute>
			<xsl:attribute name="militia_party_template">PartyTemplate.militia_dunland_template</xsl:attribute>
			<xsl:attribute name="rebels_party_template">PartyTemplate.rebels_empire_template</xsl:attribute>
			<xsl:attribute name="vassal_reward_party_template">PartyTemplate.vassal_reward_troops_dunland</xsl:attribute>

			<!-- Encounter/display -->
			<xsl:attribute name="encounter_background_mesh">encounter_empire</xsl:attribute>
			<xsl:attribute name="board_game_type">Tablut</xsl:attribute>

			<!-- Equipment rosters -->
			<xsl:attribute name="default_battle_equipment_roster">EquipmentRoster.dunland_bat_template_medium_a</xsl:attribute>
			<xsl:attribute name="default_civilian_equipment_roster">EquipmentRoster.dunland_civ_template_default_a</xsl:attribute>

			<!-- NPC references -->
			<xsl:attribute name="tournament_master">NPCCharacter.tournament_master_dunland</xsl:attribute>
			<xsl:attribute name="villager">NPCCharacter.villager_dunland</xsl:attribute>
			<xsl:attribute name="caravan_master">NPCCharacter.caravan_master_dunland</xsl:attribute>
			<xsl:attribute name="armed_trader">NPCCharacter.armed_trader_dunland</xsl:attribute>
			<xsl:attribute name="caravan_guard">NPCCharacter.caravan_guard_dunland</xsl:attribute>
			<xsl:attribute name="veteran_caravan_guard">NPCCharacter.veteran_caravan_guard_dunland</xsl:attribute>
			<xsl:attribute name="prison_guard">NPCCharacter.prison_guard_dunland</xsl:attribute>
			<xsl:attribute name="guard">NPCCharacter.guard_dunland</xsl:attribute>
			<xsl:attribute name="blacksmith">NPCCharacter.blacksmith_dunland</xsl:attribute>
			<xsl:attribute name="weaponsmith">NPCCharacter.weaponsmith_dunland</xsl:attribute>
			<xsl:attribute name="townswoman">NPCCharacter.townswoman_dunland</xsl:attribute>
			<xsl:attribute name="townswoman_infant">NPCCharacter.townswoman_infant_dunland</xsl:attribute>
			<xsl:attribute name="townswoman_child">NPCCharacter.townswoman_child_dunland</xsl:attribute>
			<xsl:attribute name="townswoman_teenager">NPCCharacter.townswoman_teenager_dunland</xsl:attribute>
			<xsl:attribute name="townsman">NPCCharacter.townsman_dunland</xsl:attribute>
			<xsl:attribute name="townsman_infant">NPCCharacter.townsman_infant_dunland</xsl:attribute>
			<xsl:attribute name="townsman_child">NPCCharacter.townsman_child_dunland</xsl:attribute>
			<xsl:attribute name="townsman_teenager">NPCCharacter.townsman_teenager_dunland</xsl:attribute>
			<xsl:attribute name="village_woman">NPCCharacter.village_woman_dunland</xsl:attribute>
			<xsl:attribute name="villager_male_child">NPCCharacter.villager_child_dunland</xsl:attribute>
			<xsl:attribute name="villager_male_teenager">NPCCharacter.villager_teenager_dunland</xsl:attribute>
			<xsl:attribute name="villager_female_child">NPCCharacter.village_woman_child_dunland</xsl:attribute>
			<xsl:attribute name="villager_female_teenager">NPCCharacter.village_woman_teenager_dunland</xsl:attribute>
			<xsl:attribute name="ransom_broker">NPCCharacter.ransom_broker_dunland</xsl:attribute>
			<xsl:attribute name="gangleader_bodyguard">NPCCharacter.gangleader_bodyguard_dunland</xsl:attribute>
			<xsl:attribute name="merchant_notary">NPCCharacter.spc_notable_dunland_0</xsl:attribute>
			<xsl:attribute name="artisan_notary">NPCCharacter.spc_notable_dunland_8</xsl:attribute>
			<xsl:attribute name="preacher_notary">NPCCharacter.spc_notable_dunland_5</xsl:attribute>
			<xsl:attribute name="rural_notable_notary">NPCCharacter.spc_notable_dunland_21</xsl:attribute>
			<xsl:attribute name="shop_worker">NPCCharacter.shop_worker_dunland</xsl:attribute>
			<xsl:attribute name="tavernkeeper">NPCCharacter.tavernkeeper_dunland</xsl:attribute>
			<xsl:attribute name="taverngamehost">NPCCharacter.taverngamehost_dunland</xsl:attribute>
			<xsl:attribute name="musician">NPCCharacter.musician_dunland</xsl:attribute>
			<xsl:attribute name="tavern_wench">NPCCharacter.tavern_wench_dunland</xsl:attribute>
			<xsl:attribute name="armorer">NPCCharacter.armorer_dunland</xsl:attribute>
			<xsl:attribute name="horseMerchant">NPCCharacter.horseMerchant_dunland</xsl:attribute>
			<xsl:attribute name="barber">NPCCharacter.barber_dunland</xsl:attribute>
			<xsl:attribute name="merchant">NPCCharacter.merchant_dunland</xsl:attribute>
			<xsl:attribute name="beggar">NPCCharacter.beggar_dunland</xsl:attribute>
			<xsl:attribute name="female_beggar">NPCCharacter.female_beggar_dunland</xsl:attribute>
			<xsl:attribute name="female_dancer">NPCCharacter.female_dancer_dunland</xsl:attribute>
			<xsl:attribute name="gear_practice_dummy">NPCCharacter.gear_practice_dummy_dunland</xsl:attribute>
			<xsl:attribute name="weapon_practice_stage_1">NPCCharacter.weapon_practice_stage_1_dunland</xsl:attribute>
			<xsl:attribute name="weapon_practice_stage_2">NPCCharacter.weapon_practice_stage_2_dunland</xsl:attribute>
			<xsl:attribute name="weapon_practice_stage_3">NPCCharacter.weapon_practice_stage_3_dunland</xsl:attribute>
			<xsl:attribute name="gear_dummy">NPCCharacter.gear_dummy_dunland</xsl:attribute>

			<!-- Vassal reward items -->
			<vassal_reward_items>
				<item id="Item.dunland_caerdh_axe_2h_a" />
				<item id="Item.dunland_caerdh_axe_1h_b" />
			</vassal_reward_items>
			<!-- Banner bearer weapons -->
			<banner_bearer_replacement_weapons>
				<item id="Item.iron_spatha_sword_t2" />
				<item id="Item.empire_sword_1_t2" />
				<item id="Item.empire_sword_2_t3" />
				<item id="Item.empire_sword_6_t5" />
			</banner_bearer_replacement_weapons>
			<!-- Default policies -->
			<default_policies>
				<policy id="policy_council_of_the_commons" />
				<policy id="policy_cantons" />
				<policy id="policy_grazing_rights" />
				<policy id="policy_marshals" />
				<policy id="policy_serfdom" />
				<policy id="policy_road_tolls" />
			</default_policies>

			<!-- LOTR names -->
			<male_names>
				<name name="{{=aom_dunland_male_name_1}}Brodar" />
				<name name="{{=aom_dunland_male_name_2}}Dunric" />
				<name name="{{=aom_dunland_male_name_3}}Eogan" />
				<name name="{{=aom_dunland_male_name_4}}Grimdal" />
				<name name="{{=aom_dunland_male_name_5}}Tharn" />
				<name name="{{=aom_dunland_male_name_6}}Caddoc" />
				<name name="{{=aom_dunland_male_name_7}}Mordran" />
				<name name="{{=aom_dunland_male_name_8}}Fynric" />
				<name name="{{=aom_dunland_male_name_9}}Caedoc" />
				<name name="{{=aom_dunland_male_name_10}}Torlan" />
				<name name="{{=aom_dunland_male_name_11}}Ragnor" />
				<name name="{{=aom_dunland_male_name_12}}Eldric" />
				<name name="{{=aom_dunland_male_name_13}}Halric" />
				<name name="{{=aom_dunland_male_name_14}}Wulric" />
				<name name="{{=aom_dunland_male_name_15}}Cernoc" />
				<name name="{{=aom_dunland_male_name_16}}Brynor" />
				<name name="{{=aom_dunland_male_name_17}}Gorran" />
				<name name="{{=aom_dunland_male_name_18}}Tordoc" />
				<name name="{{=aom_dunland_male_name_19}}Fynric" />
				<name name="{{=aom_dunland_male_name_20}}Harroc" />
				<name name="{{=aom_dunland_male_name_21}}Caelan" />
				<name name="{{=aom_dunland_male_name_22}}Wuldar" />
				<name name="{{=aom_dunland_male_name_23}}Thorran" />
				<name name="{{=aom_dunland_male_name_24}}Grelric" />
				<name name="{{=aom_dunland_male_name_25}}Brador" />
				<name name="{{=aom_dunland_male_name_26}}Morlan" />
				<name name="{{=aom_dunland_male_name_27}}Ardric" />
				<name name="{{=aom_dunland_male_name_28}}Cadroc" />
				<name name="{{=aom_dunland_male_name_29}}Graldor" />
				<name name="{{=aom_dunland_male_name_30}}Farnoc" />
				<name name="{{=aom_dunland_male_name_31}}Ulric" />
				<name name="{{=aom_dunland_male_name_32}}Brendoc" />
				<name name="{{=aom_dunland_male_name_33}}Eadric" />
				<name name="{{=aom_dunland_male_name_34}}Thraldor" />
				<name name="{{=aom_dunland_male_name_35}}Wulgan" />
				<name name="{{=aom_dunland_male_name_36}}Gorran" />
				<name name="{{=aom_dunland_male_name_37}}Ragnald" />
				<name name="{{=aom_dunland_male_name_38}}Dunmor" />
				<name name="{{=aom_dunland_male_name_39}}Thorlic" />
				<name name="{{=aom_dunland_male_name_40}}Fynwald" />
				<name name="{{=aom_dunland_male_name_41}}Bryldan" />
				<name name="{{=aom_dunland_male_name_42}}Granthar" />
				<name name="{{=aom_dunland_male_name_43}}Halmod" />
				<name name="{{=aom_dunland_male_name_44}}Thrydan" />
				<name name="{{=aom_dunland_male_name_45}}Eorlund" />
				<name name="{{=aom_dunland_male_name_46}}Gareth" />
				<name name="{{=aom_dunland_male_name_47}}Harwic" />
				<name name="{{=aom_dunland_male_name_48}}Gildan" />
				<name name="{{=aom_dunland_male_name_49}}Wulfram" />
				<name name="{{=aom_dunland_male_name_50}}Thorag" />
			</male_names>
			<female_names>
				<name name="{{=aom_dunland_female_name_1}}Bryndis" />
				<name name="{{=aom_dunland_female_name_2}}Eydis" />
				<name name="{{=aom_dunland_female_name_3}}Morwyn" />
				<name name="{{=aom_dunland_female_name_4}}Grisla" />
				<name name="{{=aom_dunland_female_name_5}}Halda" />
				<name name="{{=aom_dunland_female_name_6}}Thyra" />
				<name name="{{=aom_dunland_female_name_7}}Caedwyn" />
				<name name="{{=aom_dunland_female_name_8}}Elrin" />
				<name name="{{=aom_dunland_female_name_9}}Fynwyn" />
				<name name="{{=aom_dunland_female_name_10}}Thalda" />
				<name name="{{=aom_dunland_female_name_11}}Grelwyn" />
				<name name="{{=aom_dunland_female_name_12}}Harwyn" />
				<name name="{{=aom_dunland_female_name_13}}Aldis" />
				<name name="{{=aom_dunland_female_name_14}}Wynla" />
				<name name="{{=aom_dunland_female_name_15}}Brythea" />
				<name name="{{=aom_dunland_female_name_16}}Eorra" />
				<name name="{{=aom_dunland_female_name_17}}Thylda" />
				<name name="{{=aom_dunland_female_name_18}}Granna" />
				<name name="{{=aom_dunland_female_name_19}}Elwyn" />
				<name name="{{=aom_dunland_female_name_20}}Morla" />
				<name name="{{=aom_dunland_female_name_21}}Harla" />
				<name name="{{=aom_dunland_female_name_22}}Fynla" />
				<name name="{{=aom_dunland_female_name_23}}Gryssa" />
				<name name="{{=aom_dunland_female_name_24}}Thryssa" />
				<name name="{{=aom_dunland_female_name_25}}Halwyn" />
				<name name="{{=aom_dunland_female_name_26}}Caelin" />
				<name name="{{=aom_dunland_female_name_27}}Grelwyn" />
				<name name="{{=aom_dunland_female_name_28}}Thara" />
				<name name="{{=aom_dunland_female_name_29}}Morra" />
				<name name="{{=aom_dunland_female_name_30}}Bryna" />
				<name name="{{=aom_dunland_female_name_31}}Thilda" />
				<name name="{{=aom_dunland_female_name_32}}Elis" />
				<name name="{{=aom_dunland_female_name_33}}Caelwyn" />
				<name name="{{=aom_dunland_female_name_34}}Aldra" />
				<name name="{{=aom_dunland_female_name_35}}Harra" />
				<name name="{{=aom_dunland_female_name_36}}Wynla" />
				<name name="{{=aom_dunland_female_name_37}}Thylda" />
				<name name="{{=aom_dunland_female_name_38}}Gresla" />
				<name name="{{=aom_dunland_female_name_39}}Fynwyn" />
				<name name="{{=aom_dunland_female_name_40}}Elra" />
				<name name="{{=aom_dunland_female_name_41}}Morwyn" />
				<name name="{{=aom_dunland_female_name_42}}Halda" />
				<name name="{{=aom_dunland_female_name_43}}Caedla" />
				<name name="{{=aom_dunland_female_name_44}}Thanna" />
				<name name="{{=aom_dunland_female_name_45}}Bryndel" />
				<name name="{{=aom_dunland_female_name_46}}Griswyn" />
				<name name="{{=aom_dunland_female_name_47}}Eydis" />
				<name name="{{=aom_dunland_female_name_48}}Fynla" />
				<name name="{{=aom_dunland_female_name_49}}Harwyn" />
				<name name="{{=aom_dunland_female_name_50}}Wynna" />
			</female_names>
			<clan_names>
				<name name="{{=aom_dunland_clan_name_01}}The Red Wolves" />
				<name name="{{=aom_dunland_clan_name_02}}The Iron Fangs" />
				<name name="{{=aom_dunland_clan_name_03}}The Stonebreakers" />
				<name name="{{=aom_dunland_clan_name_04}}The Shadow Hunters" />
				<name name="{{=aom_dunland_clan_name_05}}The Hill Ravagers" />
				<name name="{{=aom_dunland_clan_name_06}}The Wild Blades" />
				<name name="{{=aom_dunland_clan_name_07}}The Stormcallers" />
				<name name="{{=aom_dunland_clan_name_08}}The Blood Ravens" />
				<name name="{{=aom_dunland_clan_name_09}}The Bone Spears" />
				<name name="{{=aom_dunland_clan_name_10}}The Earthshakers" />
				<name name="{{=aom_dunland_clan_name_11}}The Thorned Shields" />
				<name name="{{=aom_dunland_clan_name_12}}The Shadowed Wolves" />
				<name name="{{=aom_dunland_clan_name_13}}The Broken Axes" />
				<name name="{{=aom_dunland_clan_name_14}}The Raven Eyes" />
				<name name="{{=aom_dunland_clan_name_15}}The Frosthowl Clan" />
				<name name="{{=aom_dunland_clan_name_16}}The Ironbound" />
				<name name="{{=aom_dunland_clan_name_17}}The Stone Fists" />
				<name name="{{=aom_dunland_clan_name_18}}The Howling Gale" />
				<name name="{{=aom_dunland_clan_name_19}}The Mountain Shadows" />
			</clan_names>

			<!-- Notable templates (replaces vanilla empire notables with Dunland-specific) -->
			<notable_templates>
				<template name="NPCCharacter.spc_wanderer_dunland_0" />
				<template name="NPCCharacter.spc_wanderer_dunland_1" />
				<template name="NPCCharacter.spc_wanderer_dunland_2" />
				<template name="NPCCharacter.spc_wanderer_dunland_3" />
				<template name="NPCCharacter.spc_wanderer_dunland_4" />
				<template name="NPCCharacter.spc_wanderer_dunland_5" />
				<template name="NPCCharacter.spc_wanderer_dunland_6" />
				<template name="NPCCharacter.spc_wanderer_dunland_7" />
				<template name="NPCCharacter.spc_wanderer_dunland_8" />
				<template name="NPCCharacter.spc_wanderer_dunland_9" />
				<template name="NPCCharacter.spc_notable_dunland_0" />
				<template name="NPCCharacter.spc_notable_dunland_0b" />
				<template name="NPCCharacter.spc_notable_dunland_1" />
				<template name="NPCCharacter.spc_notable_dunland_1b" />
				<template name="NPCCharacter.spc_notable_dunland_2" />
				<template name="NPCCharacter.spc_notable_dunland_2b" />
				<template name="NPCCharacter.spc_notable_dunland_3" />
				<template name="NPCCharacter.spc_notable_dunland_3b" />
				<template name="NPCCharacter.spc_notable_dunland_4" />
				<template name="NPCCharacter.spc_notable_dunland_4b" />
				<template name="NPCCharacter.spc_notable_dunland_5" />
				<template name="NPCCharacter.spc_notable_dunland_6" />
				<template name="NPCCharacter.spc_notable_dunland_7" />
				<template name="NPCCharacter.spc_notable_dunland_8" />
				<template name="NPCCharacter.spc_notable_dunland_9" />
				<template name="NPCCharacter.spc_notable_dunland_gl1" />
				<template name="NPCCharacter.spc_notable_dunland_10" />
				<template name="NPCCharacter.spc_notable_dunland_11" />
				<template name="NPCCharacter.spc_notable_dunland_gl4" />
				<template name="NPCCharacter.spc_notable_dunland_12" />
				<template name="NPCCharacter.spc_notable_dunland_13" />
				<template name="NPCCharacter.spc_notable_dunland_21" />
				<template name="NPCCharacter.spc_notable_dunland_22" />
				<template name="NPCCharacter.spc_dunland_headman_1" />
				<template name="NPCCharacter.spc_dunland_headman_2" />
				<template name="NPCCharacter.spc_dunland_headman_3" />
			</notable_templates>

			<!-- Pass through vanilla child elements we don't override -->
			<xsl:apply-templates select="*[not(self::vassal_reward_items or self::banner_bearer_replacement_weapons or self::default_policies or self::male_names or self::female_names or self::clan_names or self::notable_templates)]"/>
		</xsl:copy>
	</xsl:template>

	<!-- Rename aserai to Haradrim -->
	<xsl:template match="Culture[@id='aserai']">
		<xsl:copy>
			<!-- Copy all vanilla attributes first, then override the ones we change -->
			<xsl:apply-templates select="@*"/>

			<!-- Name and description -->
			<xsl:attribute name="name">{=aom_aserai_name}Haradrim</xsl:attribute>
			<xsl:attribute name="text">{=aom_aserai_desc}The Haradrim are a proud and fierce people hailing from the deserts and savannahs of the distant south. Renowned for their mastery of exotic beasts such as mûmakil, their warriors combine skill, resilience, and deadly precision in battle. Harad is a land of extremes, with its scorching sun, golden sands, and vibrant oases. Its people are united by ancient traditions, loyalty to their tribes, and the call of their chieftains and kings. Often allied with the forces of darkness, the Haradrim are driven by vengeance against Gondor and the promise of power and dominion offered by Sauron.</xsl:attribute>
			<xsl:attribute name="color">0xFF7A1E1E</xsl:attribute>
			<xsl:attribute name="color2">0xFFB28944</xsl:attribute>

			<!-- Troop references -->
			<xsl:attribute name="basic_troop">NPCCharacter.harad_levy</xsl:attribute>
			<xsl:attribute name="elite_basic_troop">NPCCharacter.harad_noble</xsl:attribute>
			<xsl:attribute name="melee_militia_troop">NPCCharacter.harad_levy</xsl:attribute>
			<xsl:attribute name="ranged_militia_troop">NPCCharacter.harad_levy</xsl:attribute>
			<xsl:attribute name="melee_elite_militia_troop">NPCCharacter.harad_noble</xsl:attribute>
			<xsl:attribute name="ranged_elite_militia_troop">NPCCharacter.harad_noble</xsl:attribute>

			<!-- Party templates -->
			<xsl:attribute name="villager_party_template">PartyTemplate.villager_aserai_template</xsl:attribute>
			<xsl:attribute name="default_party_template">PartyTemplate.kingdom_hero_party_harad_template</xsl:attribute>
			<xsl:attribute name="caravan_party_template">PartyTemplate.caravan_template_aserai</xsl:attribute>
			<xsl:attribute name="elite_caravan_party_template">PartyTemplate.elite_caravan_template_aserai</xsl:attribute>
			<xsl:attribute name="militia_party_template">PartyTemplate.militia_aserai_template</xsl:attribute>
			<xsl:attribute name="rebels_party_template">PartyTemplate.rebels_aserai_template</xsl:attribute>
			<xsl:attribute name="vassal_reward_party_template">PartyTemplate.vassal_reward_troops_aserai</xsl:attribute>

			<!-- Encounter/display -->
			<xsl:attribute name="encounter_background_mesh">encounter_aserai</xsl:attribute>
			<xsl:attribute name="board_game_type">Seega</xsl:attribute>

			<!-- Equipment rosters -->
			<xsl:attribute name="default_battle_equipment_roster">EquipmentRoster.harad_bat_template_medium_a</xsl:attribute>
			<xsl:attribute name="default_civilian_equipment_roster">EquipmentRoster.harad_civ_template_a</xsl:attribute>

			<!-- NPC references -->
			<xsl:attribute name="tournament_master">NPCCharacter.tournament_master_harad</xsl:attribute>
			<xsl:attribute name="villager">NPCCharacter.villager_harad</xsl:attribute>
			<xsl:attribute name="caravan_master">NPCCharacter.caravan_master_harad</xsl:attribute>
			<xsl:attribute name="armed_trader">NPCCharacter.armed_trader_harad</xsl:attribute>
			<xsl:attribute name="caravan_guard">NPCCharacter.caravan_guard_harad</xsl:attribute>
			<xsl:attribute name="veteran_caravan_guard">NPCCharacter.veteran_caravan_guard_harad</xsl:attribute>
			<xsl:attribute name="prison_guard">NPCCharacter.prison_guard_harad</xsl:attribute>
			<xsl:attribute name="guard">NPCCharacter.guard_harad</xsl:attribute>
			<xsl:attribute name="blacksmith">NPCCharacter.blacksmith_harad</xsl:attribute>
			<xsl:attribute name="weaponsmith">NPCCharacter.weaponsmith_harad</xsl:attribute>
			<xsl:attribute name="townswoman">NPCCharacter.townswoman_harad</xsl:attribute>
			<xsl:attribute name="townswoman_infant">NPCCharacter.townswoman_infant_harad</xsl:attribute>
			<xsl:attribute name="townswoman_child">NPCCharacter.townswoman_child_harad</xsl:attribute>
			<xsl:attribute name="townswoman_teenager">NPCCharacter.townswoman_teenager_harad</xsl:attribute>
			<xsl:attribute name="townsman">NPCCharacter.townsman_harad</xsl:attribute>
			<xsl:attribute name="townsman_infant">NPCCharacter.townsman_infant_harad</xsl:attribute>
			<xsl:attribute name="townsman_child">NPCCharacter.townsman_child_harad</xsl:attribute>
			<xsl:attribute name="townsman_teenager">NPCCharacter.townsman_teenager_harad</xsl:attribute>
			<xsl:attribute name="village_woman">NPCCharacter.village_woman_harad</xsl:attribute>
			<xsl:attribute name="villager_male_child">NPCCharacter.villager_child_aserai</xsl:attribute>
			<xsl:attribute name="villager_male_teenager">NPCCharacter.villager_teenager_aserai</xsl:attribute>
			<xsl:attribute name="villager_female_child">NPCCharacter.village_woman_child_aserai</xsl:attribute>
			<xsl:attribute name="villager_female_teenager">NPCCharacter.village_woman_teenager_aserai</xsl:attribute>
			<xsl:attribute name="ransom_broker">NPCCharacter.ransom_broker_harad</xsl:attribute>
			<xsl:attribute name="gangleader_bodyguard">NPCCharacter.gangleader_bodyguard_harad</xsl:attribute>
			<xsl:attribute name="merchant_notary">NPCCharacter.spc_notable_harad_0</xsl:attribute>
			<xsl:attribute name="artisan_notary">NPCCharacter.spc_notable_harad_8</xsl:attribute>
			<xsl:attribute name="preacher_notary">NPCCharacter.spc_notable_harad_5</xsl:attribute>
			<xsl:attribute name="rural_notable_notary">NPCCharacter.spc_notable_harad_21</xsl:attribute>
			<xsl:attribute name="shop_worker">NPCCharacter.shop_worker_harad</xsl:attribute>
			<xsl:attribute name="tavernkeeper">NPCCharacter.tavernkeeper_harad</xsl:attribute>
			<xsl:attribute name="taverngamehost">NPCCharacter.taverngamehost_harad</xsl:attribute>
			<xsl:attribute name="musician">NPCCharacter.musician_harad</xsl:attribute>
			<xsl:attribute name="tavern_wench">NPCCharacter.tavern_wench_harad</xsl:attribute>
			<xsl:attribute name="armorer">NPCCharacter.armorer_harad</xsl:attribute>
			<xsl:attribute name="horseMerchant">NPCCharacter.horseMerchant_harad</xsl:attribute>
			<xsl:attribute name="barber">NPCCharacter.barber_harad</xsl:attribute>
			<xsl:attribute name="merchant">NPCCharacter.merchant_harad</xsl:attribute>
			<xsl:attribute name="beggar">NPCCharacter.beggar_harad</xsl:attribute>
			<xsl:attribute name="female_beggar">NPCCharacter.female_beggar_harad</xsl:attribute>
			<xsl:attribute name="female_dancer">NPCCharacter.female_dancer_harad</xsl:attribute>
			<xsl:attribute name="gear_practice_dummy">NPCCharacter.gear_practice_dummy_harad</xsl:attribute>
			<xsl:attribute name="weapon_practice_stage_1">NPCCharacter.weapon_practice_stage_1_harad</xsl:attribute>
			<xsl:attribute name="weapon_practice_stage_2">NPCCharacter.weapon_practice_stage_2_harad</xsl:attribute>
			<xsl:attribute name="weapon_practice_stage_3">NPCCharacter.weapon_practice_stage_3_harad</xsl:attribute>
			<xsl:attribute name="gear_dummy">NPCCharacter.gear_dummy_harad</xsl:attribute>

			<!-- Vassal reward items -->
			<vassal_reward_items>
				<item id="Item.noble_horse_khamseen" />
			</vassal_reward_items>
			<!-- Banner bearer weapons -->
			<banner_bearer_replacement_weapons>
				<item id="Item.aserai_sword_1_t2" />
				<item id="Item.aserai_sword_3_t3" />
				<item id="Item.aserai_sword_4_t4" />
				<item id="Item.aserai_sword_5_t4" />
			</banner_bearer_replacement_weapons>
			<!-- Default policies -->
			<default_policies>
				<policy id="policy_state_monopolies" />
				<policy id="policy_crown_duty" />
				<policy id="policy_road_tolls" />
				<policy id="policy_feudal_inheritance" />
				<policy id="policy_lords_privy_council" />
				<policy id="policy_noble_retinues" />
			</default_policies>

			<!-- LOTR names -->
			<male_names>
				<name name="{{=aom_harad_male_name_1}}Azaran" />
				<name name="{{=aom_harad_male_name_2}}Zamir" />
				<name name="{{=aom_harad_male_name_3}}Khalid" />
				<name name="{{=aom_harad_male_name_4}}Rashid" />
				<name name="{{=aom_harad_male_name_5}}Nazir" />
				<name name="{{=aom_harad_male_name_6}}Jameel" />
				<name name="{{=aom_harad_male_name_7}}Hasim" />
				<name name="{{=aom_harad_male_name_8}}Faraj" />
				<name name="{{=aom_harad_male_name_9}}Sahim" />
				<name name="{{=aom_harad_male_name_10}}Tariq" />
				<name name="{{=aom_harad_male_name_11}}Basim" />
				<name name="{{=aom_harad_male_name_12}}Karim" />
				<name name="{{=aom_harad_male_name_13}}Salim" />
				<name name="{{=aom_harad_male_name_14}}Rayan" />
				<name name="{{=aom_harad_male_name_15}}Zafar" />
				<name name="{{=aom_harad_male_name_16}}Yasir" />
				<name name="{{=aom_harad_male_name_17}}Hakeem" />
				<name name="{{=aom_harad_male_name_18}}Ibrahim" />
				<name name="{{=aom_harad_male_name_19}}Faisal" />
				<name name="{{=aom_harad_male_name_20}}Najeeb" />
				<name name="{{=aom_harad_male_name_21}}Munir" />
				<name name="{{=aom_harad_male_name_22}}Ashraf" />
				<name name="{{=aom_harad_male_name_23}}Zakwan" />
				<name name="{{=aom_harad_male_name_24}}Azeem" />
				<name name="{{=aom_harad_male_name_25}}Suleiman" />
				<name name="{{=aom_harad_male_name_26}}Hakim" />
				<name name="{{=aom_harad_male_name_27}}Jibril" />
				<name name="{{=aom_harad_male_name_28}}Tahir" />
				<name name="{{=aom_harad_male_name_29}}Rafiq" />
				<name name="{{=aom_harad_male_name_30}}Malik" />
				<name name="{{=aom_harad_male_name_31}}Zahir" />
				<name name="{{=aom_harad_male_name_32}}Asim" />
				<name name="{{=aom_harad_male_name_33}}Nabil" />
				<name name="{{=aom_harad_male_name_34}}Omar" />
				<name name="{{=aom_harad_male_name_35}}Qasim" />
				<name name="{{=aom_harad_male_name_36}}Halim" />
				<name name="{{=aom_harad_male_name_37}}Mansoor" />
				<name name="{{=aom_harad_male_name_38}}Yahya" />
				<name name="{{=aom_harad_male_name_39}}Shakir" />
				<name name="{{=aom_harad_male_name_40}}Adil" />
				<name name="{{=aom_harad_male_name_41}}Mahdi" />
				<name name="{{=aom_harad_male_name_42}}Fahim" />
				<name name="{{=aom_harad_male_name_43}}Latif" />
				<name name="{{=aom_harad_male_name_44}}Rameez" />
				<name name="{{=aom_harad_male_name_45}}Tameem" />
				<name name="{{=aom_harad_male_name_46}}Yazid" />
				<name name="{{=aom_harad_male_name_47}}Kamar" />
				<name name="{{=aom_harad_male_name_48}}Hassan" />
				<name name="{{=aom_harad_male_name_49}}Azim" />
				<name name="{{=aom_harad_male_name_50}}Zubaid" />
			</male_names>
			<female_names>
				<name name="{{=aom_harad_female_name_1}}Zahira" />
				<name name="{{=aom_harad_female_name_2}}Nadia" />
				<name name="{{=aom_harad_female_name_3}}Yasmin" />
				<name name="{{=aom_harad_female_name_4}}Amira" />
				<name name="{{=aom_harad_female_name_5}}Layla" />
				<name name="{{=aom_harad_female_name_6}}Rania" />
				<name name="{{=aom_harad_female_name_7}}Jamila" />
				<name name="{{=aom_harad_female_name_8}}Hafsa" />
				<name name="{{=aom_harad_female_name_9}}Zuleika" />
				<name name="{{=aom_harad_female_name_10}}Amina" />
				<name name="{{=aom_harad_female_name_11}}Salima" />
				<name name="{{=aom_harad_female_name_12}}Lamia" />
				<name name="{{=aom_harad_female_name_13}}Samira" />
				<name name="{{=aom_harad_female_name_14}}Nazeera" />
				<name name="{{=aom_harad_female_name_15}}Ruqaya" />
				<name name="{{=aom_harad_female_name_16}}Halima" />
				<name name="{{=aom_harad_female_name_17}}Aziza" />
				<name name="{{=aom_harad_female_name_18}}Khadija" />
				<name name="{{=aom_harad_female_name_19}}Fadwa" />
				<name name="{{=aom_harad_female_name_20}}Safiya" />
				<name name="{{=aom_harad_female_name_21}}Aalia" />
				<name name="{{=aom_harad_female_name_22}}Nawal" />
				<name name="{{=aom_harad_female_name_23}}Bushra" />
				<name name="{{=aom_harad_female_name_24}}Zahra" />
				<name name="{{=aom_harad_female_name_25}}Rihana" />
				<name name="{{=aom_harad_female_name_26}}Sumaya" />
				<name name="{{=aom_harad_female_name_27}}Naima" />
				<name name="{{=aom_harad_female_name_28}}Rashida" />
				<name name="{{=aom_harad_female_name_29}}Zainab" />
				<name name="{{=aom_harad_female_name_30}}Shaima" />
				<name name="{{=aom_harad_female_name_31}}Munira" />
				<name name="{{=aom_harad_female_name_32}}Fatima" />
				<name name="{{=aom_harad_female_name_33}}Shirin" />
				<name name="{{=aom_harad_female_name_34}}Saniya" />
				<name name="{{=aom_harad_female_name_35}}Malika" />
				<name name="{{=aom_harad_female_name_36}}Afiya" />
				<name name="{{=aom_harad_female_name_37}}Zain" />
				<name name="{{=aom_harad_female_name_38}}Farida" />
				<name name="{{=aom_harad_female_name_39}}Amani" />
				<name name="{{=aom_harad_female_name_40}}Nazira" />
				<name name="{{=aom_harad_female_name_41}}Huda" />
				<name name="{{=aom_harad_female_name_42}}Rahma" />
				<name name="{{=aom_harad_female_name_43}}Yamila" />
				<name name="{{=aom_harad_female_name_44}}Shama" />
				<name name="{{=aom_harad_female_name_45}}Tamina" />
				<name name="{{=aom_harad_female_name_46}}Zubaida" />
				<name name="{{=aom_harad_female_name_47}}Rabiya" />
				<name name="{{=aom_harad_female_name_48}}Asma" />
				<name name="{{=aom_harad_female_name_49}}Latifa" />
				<name name="{{=aom_harad_female_name_50}}Saida" />
			</female_names>
			<clan_names>
				<name name="{{=aom_harad_clan_name_01}}The Burning Sands" />
				<name name="{{=aom_harad_clan_name_02}}The Golden Blades" />
				<name name="{{=aom_harad_clan_name_03}}The Desert Suns" />
				<name name="{{=aom_harad_clan_name_04}}The Red Spears" />
				<name name="{{=aom_harad_clan_name_05}}The Dune Riders" />
				<name name="{{=aom_harad_clan_name_06}}The Scorching Wind" />
				<name name="{{=aom_harad_clan_name_07}}The Sandstorm Warriors" />
				<name name="{{=aom_harad_clan_name_08}}The Serpent Fangs" />
				<name name="{{=aom_harad_clan_name_09}}The Black Scorpions" />
				<name name="{{=aom_harad_clan_name_10}}The Mûmakil Keepers" />
				<name name="{{=aom_harad_clan_name_11}}The Oasis Guardians" />
				<name name="{{=aom_harad_clan_name_12}}The Firebrands" />
				<name name="{{=aom_harad_clan_name_13}}The Crimson Shields" />
				<name name="{{=aom_harad_clan_name_14}}The Golden Crescent" />
				<name name="{{=aom_harad_clan_name_15}}The Silent Shadows" />
				<name name="{{=aom_harad_clan_name_16}}The Sunstrikers" />
				<name name="{{=aom_harad_clan_name_17}}The Dune Wardens" />
				<name name="{{=aom_harad_clan_name_18}}The Serpent Riders" />
				<name name="{{=aom_harad_clan_name_19}}The Blazing Hooves" />
				<name name="{{=aom_harad_clan_name_20}}The Twilight Sand" />
			</clan_names>

			<!-- Notable templates (replaces vanilla aserai notables with Harad-specific) -->
			<notable_templates>
				<template name="NPCCharacter.spc_wanderer_harad_0" />
				<template name="NPCCharacter.spc_wanderer_harad_1" />
				<template name="NPCCharacter.spc_wanderer_harad_2" />
				<template name="NPCCharacter.spc_wanderer_harad_3" />
				<template name="NPCCharacter.spc_wanderer_harad_4" />
				<template name="NPCCharacter.spc_wanderer_harad_5" />
				<template name="NPCCharacter.spc_wanderer_harad_6" />
				<template name="NPCCharacter.spc_wanderer_harad_7" />
				<template name="NPCCharacter.spc_wanderer_harad_8" />
				<template name="NPCCharacter.spc_wanderer_harad_9" />
				<template name="NPCCharacter.spc_notable_harad_0" />
				<template name="NPCCharacter.spc_notable_harad_0b" />
				<template name="NPCCharacter.spc_notable_harad_1" />
				<template name="NPCCharacter.spc_notable_harad_1b" />
				<template name="NPCCharacter.spc_notable_harad_2" />
				<template name="NPCCharacter.spc_notable_harad_2b" />
				<template name="NPCCharacter.spc_notable_harad_3" />
				<template name="NPCCharacter.spc_notable_harad_3b" />
				<template name="NPCCharacter.spc_notable_harad_4" />
				<template name="NPCCharacter.spc_notable_harad_4b" />
				<template name="NPCCharacter.spc_notable_harad_5" />
				<template name="NPCCharacter.spc_notable_harad_6" />
				<template name="NPCCharacter.spc_notable_harad_7" />
				<template name="NPCCharacter.spc_notable_harad_8" />
				<template name="NPCCharacter.spc_notable_harad_9" />
				<template name="NPCCharacter.spc_notable_harad_gl1" />
				<template name="NPCCharacter.spc_notable_harad_10" />
				<template name="NPCCharacter.spc_notable_harad_11" />
				<template name="NPCCharacter.spc_notable_harad_gl4" />
				<template name="NPCCharacter.spc_notable_harad_12" />
				<template name="NPCCharacter.spc_notable_harad_13" />
				<template name="NPCCharacter.spc_notable_harad_21" />
				<template name="NPCCharacter.spc_notable_harad_22" />
				<template name="NPCCharacter.spc_harad_headman_1" />
				<template name="NPCCharacter.spc_harad_headman_2" />
				<template name="NPCCharacter.spc_harad_headman_3" />
			</notable_templates>

			<!-- Pass through vanilla child elements we don't override -->
			<xsl:apply-templates select="*[not(self::vassal_reward_items or self::banner_bearer_replacement_weapons or self::default_policies or self::male_names or self::female_names or self::clan_names or self::notable_templates)]"/>
		</xsl:copy>
	</xsl:template>

	<!-- Rename vlandia to Rohirrim -->
	<xsl:template match="Culture[@id='vlandia']">
		<xsl:copy>
			<!-- Copy all vanilla attributes first, then override the ones we change -->
			<xsl:apply-templates select="@*"/>

			<!-- Name and description -->
			<xsl:attribute name="name">{=aom_vlandia_name}Rohirrim</xsl:attribute>
			<xsl:attribute name="text">{=aom_vlandia_desc}The Rohirrim, or Horse-lords of Rohan, are renowned for their unmatched cavalry and deep bond with their steeds. Proud and free-spirited, they dwell in the rolling plains of the Riddermark, living in villages and strongholds like Edoras. Guided by their King and loyal to their people, the Rohirrim prize honor, loyalty, and courage. Their warriors ride fearlessly into battle, forming the backbone of Middle-earth’s resistance against the shadow. Though steadfast in their traditions, the Rohirrim are ever ready to defend their land and allies, wielding spears, swords, and shields with precision.</xsl:attribute>
			<xsl:attribute name="color">0xFF2D4C2A</xsl:attribute>
			<xsl:attribute name="color2">0xFFC0A95D</xsl:attribute>

			<!-- Troop references -->
			<xsl:attribute name="basic_troop">NPCCharacter.rohan_edoras_recruit</xsl:attribute>
			<xsl:attribute name="elite_basic_troop">NPCCharacter.rohan_edoras_golden_hall_rider</xsl:attribute>
			<xsl:attribute name="melee_militia_troop">NPCCharacter.rohan_edoras_recruit</xsl:attribute>
			<xsl:attribute name="ranged_militia_troop">NPCCharacter.rohan_edoras_recruit</xsl:attribute>
			<xsl:attribute name="melee_elite_militia_troop">NPCCharacter.rohan_edoras_golden_hall_rider</xsl:attribute>
			<xsl:attribute name="ranged_elite_militia_troop">NPCCharacter.rohan_edoras_golden_hall_rider</xsl:attribute>

			<!-- Party templates -->
			<xsl:attribute name="villager_party_template">PartyTemplate.villager_vlandia_template</xsl:attribute>
			<xsl:attribute name="default_party_template">PartyTemplate.kingdom_hero_party_rohan_template</xsl:attribute>
			<xsl:attribute name="caravan_party_template">PartyTemplate.caravan_template_rohan</xsl:attribute>
			<xsl:attribute name="elite_caravan_party_template">PartyTemplate.elite_caravan_template_rohan</xsl:attribute>
			<xsl:attribute name="militia_party_template">PartyTemplate.militia_vlandia_template</xsl:attribute>
			<xsl:attribute name="rebels_party_template">PartyTemplate.rebels_vlandia_template</xsl:attribute>
			<xsl:attribute name="vassal_reward_party_template">PartyTemplate.vassal_reward_troops_vlandia</xsl:attribute>

			<!-- Encounter/display -->
			<xsl:attribute name="encounter_background_mesh">encounter_vlandia</xsl:attribute>
			<xsl:attribute name="board_game_type">MuTorere</xsl:attribute>

			<!-- Equipment rosters -->
			<xsl:attribute name="default_battle_equipment_roster">EquipmentRoster.rohan_bat_template_medium_a</xsl:attribute>
			<xsl:attribute name="default_civilian_equipment_roster">EquipmentRoster.rohan_civ_template_default_a</xsl:attribute>

			<!-- NPC references -->
			<xsl:attribute name="tournament_master">NPCCharacter.tournament_master_rohan</xsl:attribute>
			<xsl:attribute name="villager">NPCCharacter.villager_rohan</xsl:attribute>
			<xsl:attribute name="caravan_master">NPCCharacter.caravan_master_rohan</xsl:attribute>
			<xsl:attribute name="armed_trader">NPCCharacter.armed_trader_rohan</xsl:attribute>
			<xsl:attribute name="caravan_guard">NPCCharacter.caravan_guard_rohan</xsl:attribute>
			<xsl:attribute name="veteran_caravan_guard">NPCCharacter.veteran_caravan_guard_rohan</xsl:attribute>
			<xsl:attribute name="prison_guard">NPCCharacter.prison_guard_rohan</xsl:attribute>
			<xsl:attribute name="guard">NPCCharacter.guard_rohan</xsl:attribute>
			<xsl:attribute name="blacksmith">NPCCharacter.blacksmith_rohan</xsl:attribute>
			<xsl:attribute name="weaponsmith">NPCCharacter.weaponsmith_rohan</xsl:attribute>
			<xsl:attribute name="townswoman">NPCCharacter.townswoman_rohan</xsl:attribute>
			<xsl:attribute name="townswoman_infant">NPCCharacter.townswoman_infant_rohan</xsl:attribute>
			<xsl:attribute name="townswoman_child">NPCCharacter.townswoman_child_rohan</xsl:attribute>
			<xsl:attribute name="townswoman_teenager">NPCCharacter.townswoman_teenager_rohan</xsl:attribute>
			<xsl:attribute name="townsman">NPCCharacter.townsman_rohan</xsl:attribute>
			<xsl:attribute name="townsman_infant">NPCCharacter.townsman_infant_rohan</xsl:attribute>
			<xsl:attribute name="townsman_child">NPCCharacter.townsman_child_rohan</xsl:attribute>
			<xsl:attribute name="townsman_teenager">NPCCharacter.townsman_teenager_rohan</xsl:attribute>
			<xsl:attribute name="village_woman">NPCCharacter.village_woman_rohan</xsl:attribute>
			<xsl:attribute name="villager_male_child">NPCCharacter.villager_child_rohan</xsl:attribute>
			<xsl:attribute name="villager_male_teenager">NPCCharacter.villager_teenager_rohan</xsl:attribute>
			<xsl:attribute name="villager_female_child">NPCCharacter.village_woman_child_rohan</xsl:attribute>
			<xsl:attribute name="villager_female_teenager">NPCCharacter.village_woman_teenager_rohan</xsl:attribute>
			<xsl:attribute name="ransom_broker">NPCCharacter.ransom_broker_rohan</xsl:attribute>
			<xsl:attribute name="gangleader_bodyguard">NPCCharacter.gangleader_bodyguard_rohan</xsl:attribute>
			<xsl:attribute name="merchant_notary">NPCCharacter.spc_notable_rohan_0</xsl:attribute>
			<xsl:attribute name="artisan_notary">NPCCharacter.spc_notable_rohan_8</xsl:attribute>
			<xsl:attribute name="preacher_notary">NPCCharacter.spc_notable_rohan_5</xsl:attribute>
			<xsl:attribute name="rural_notable_notary">NPCCharacter.spc_notable_rohan_21</xsl:attribute>
			<xsl:attribute name="shop_worker">NPCCharacter.shop_worker_rohan</xsl:attribute>
			<xsl:attribute name="tavernkeeper">NPCCharacter.tavernkeeper_rohan</xsl:attribute>
			<xsl:attribute name="taverngamehost">NPCCharacter.taverngamehost_rohan</xsl:attribute>
			<xsl:attribute name="musician">NPCCharacter.musician_rohan</xsl:attribute>
			<xsl:attribute name="tavern_wench">NPCCharacter.tavern_wench_rohan</xsl:attribute>
			<xsl:attribute name="armorer">NPCCharacter.armorer_rohan</xsl:attribute>
			<xsl:attribute name="horseMerchant">NPCCharacter.horseMerchant_rohan</xsl:attribute>
			<xsl:attribute name="barber">NPCCharacter.barber_rohan</xsl:attribute>
			<xsl:attribute name="merchant">NPCCharacter.merchant_rohan</xsl:attribute>
			<xsl:attribute name="beggar">NPCCharacter.beggar_rohan</xsl:attribute>
			<xsl:attribute name="female_beggar">NPCCharacter.female_beggar_rohan</xsl:attribute>
			<xsl:attribute name="female_dancer">NPCCharacter.female_dancer_rohan</xsl:attribute>
			<xsl:attribute name="gear_practice_dummy">NPCCharacter.gear_practice_dummy_rohan</xsl:attribute>
			<xsl:attribute name="weapon_practice_stage_1">NPCCharacter.weapon_practice_stage_1_rohan</xsl:attribute>
			<xsl:attribute name="weapon_practice_stage_2">NPCCharacter.weapon_practice_stage_2_rohan</xsl:attribute>
			<xsl:attribute name="weapon_practice_stage_3">NPCCharacter.weapon_practice_stage_3_rohan</xsl:attribute>
			<xsl:attribute name="gear_dummy">NPCCharacter.gear_dummy_rohan</xsl:attribute>

			<!-- Vassal reward items -->
			<vassal_reward_items>
				<item id="Item.eomer_sword" />
			</vassal_reward_items>
			<!-- Banner bearer weapons -->
			<banner_bearer_replacement_weapons>
				<item id="Item.vlandia_sword_1_t2" />
				<item id="Item.vlandia_sword_2_t3" />
				<item id="Item.vlandia_sword_3_t4" />
				<item id="Item.vlandia_sword_5_t5" />
			</banner_bearer_replacement_weapons>
			<!-- Default policies -->
			<default_policies>
				<policy id="policy_grazing_rights" />
				<policy id="policy_marshals" />
				<policy id="policy_noble_retinues" />
				<policy id="policy_council_of_the_commons" />
				<policy id="policy_lawspeakers" />
				<policy id="policy_military_coronae" />
			</default_policies>

			<!-- LOTR names -->
			<male_names>
				<name name="{{=aom_rohan_male_name_1}}Eadred" />
				<name name="{{=aom_rohan_male_name_2}}Eomund" />
				<name name="{{=aom_rohan_male_name_3}}Ceorl" />
				<name name="{{=aom_rohan_male_name_4}}Aethelric" />
				<name name="{{=aom_rohan_male_name_5}}Beorhthelm" />
				<name name="{{=aom_rohan_male_name_6}}Ealdwulf" />
				<name name="{{=aom_rohan_male_name_7}}Wulfgar" />
				<name name="{{=aom_rohan_male_name_8}}Theodric" />
				<name name="{{=aom_rohan_male_name_9}}Ealric" />
				<name name="{{=aom_rohan_male_name_10}}Bryhtwulf" />
				<name name="{{=aom_rohan_male_name_11}}Theodwine" />
				<name name="{{=aom_rohan_male_name_12}}Cynric" />
				<name name="{{=aom_rohan_male_name_13}}Eormenric" />
				<name name="{{=aom_rohan_male_name_14}}Eadhelm" />
				<name name="{{=aom_rohan_male_name_15}}Aldwulf" />
				<name name="{{=aom_rohan_male_name_16}}Frithgar" />
				<name name="{{=aom_rohan_male_name_17}}Oswine" />
				<name name="{{=aom_rohan_male_name_18}}Leofwine" />
				<name name="{{=aom_rohan_male_name_19}}Theobald" />
				<name name="{{=aom_rohan_male_name_20}}Wynfrith" />
				<name name="{{=aom_rohan_male_name_21}}Hereward" />
				<name name="{{=aom_rohan_male_name_22}}Eothain" />
				<name name="{{=aom_rohan_male_name_23}}Guthric" />
				<name name="{{=aom_rohan_male_name_24}}Theowulf" />
				<name name="{{=aom_rohan_male_name_25}}Framric" />
				<name name="{{=aom_rohan_male_name_26}}Eadgar" />
				<name name="{{=aom_rohan_male_name_27}}Heremod" />
				<name name="{{=aom_rohan_male_name_28}}Beornhelm" />
				<name name="{{=aom_rohan_male_name_29}}Sigefred" />
				<name name="{{=aom_rohan_male_name_30}}Theodenric" />
				<name name="{{=aom_rohan_male_name_31}}Ealwulf" />
				<name name="{{=aom_rohan_male_name_32}}Garmund" />
				<name name="{{=aom_rohan_male_name_33}}Hrothgar" />
				<name name="{{=aom_rohan_male_name_34}}Eowald" />
				<name name="{{=aom_rohan_male_name_35}}Ceowulf" />
				<name name="{{=aom_rohan_male_name_36}}Faramund" />
				<name name="{{=aom_rohan_male_name_37}}Aethelhelm" />
				<name name="{{=aom_rohan_male_name_38}}Eofor" />
				<name name="{{=aom_rohan_male_name_39}}Wynstan" />
				<name name="{{=aom_rohan_male_name_40}}Herewine" />
				<name name="{{=aom_rohan_male_name_41}}Guthwine" />
				<name name="{{=aom_rohan_male_name_42}}Cynwine" />
				<name name="{{=aom_rohan_male_name_43}}Aelfric" />
				<name name="{{=aom_rohan_male_name_44}}Wulfstan" />
				<name name="{{=aom_rohan_male_name_45}}Beorhtwine" />
				<name name="{{=aom_rohan_male_name_46}}Theomund" />
				<name name="{{=aom_rohan_male_name_47}}Ceowine" />
				<name name="{{=aom_rohan_male_name_48}}Theodhelm" />
				<name name="{{=aom_rohan_male_name_49}}Leofhelm" />
				<name name="{{=aom_rohan_male_name_50}}Aelfwine" />
				<name name="{{=aom_rohan_male_name_51}}Wulfnoth" />
				<name name="{{=aom_rohan_male_name_52}}Thelward" />
				<name name="{{=aom_rohan_male_name_53}}Thelme" />
				<name name="{{=aom_rohan_male_name_54}}Eormred" />
				<name name="{{=aom_rohan_male_name_55}}Aelwyne" />
				<name name="{{=aom_rohan_male_name_56}}Ilfgar" />
				<name name="{{=aom_rohan_male_name_57}}Alfgar" />
				<name name="{{=aom_rohan_male_name_58}}Ulfar" />
				<name name="{{=aom_rohan_male_name_59}}Leofar" />
				<name name="{{=aom_rohan_male_name_60}}Leofric" />
			</male_names>
			<female_names>
				<name name="{{=aom_rohan_female_name_1}}Eadgifu" />
				<name name="{{=aom_rohan_female_name_2}}Theodwyn" />
				<name name="{{=aom_rohan_female_name_3}}Aethelhild" />
				<name name="{{=aom_rohan_female_name_4}}Cynethryth" />
				<name name="{{=aom_rohan_female_name_5}}Beorthild" />
				<name name="{{=aom_rohan_female_name_6}}Frithwyn" />
				<name name="{{=aom_rohan_female_name_7}}Wynfrith" />
				<name name="{{=aom_rohan_female_name_8}}Eormenhild" />
				<name name="{{=aom_rohan_female_name_9}}Hildith" />
				<name name="{{=aom_rohan_female_name_10}}Eowynne" />
				<name name="{{=aom_rohan_female_name_11}}Leofwyn" />
				<name name="{{=aom_rohan_female_name_12}}Ealhild" />
				<name name="{{=aom_rohan_female_name_13}}Theowyn" />
				<name name="{{=aom_rohan_female_name_14}}Wynfrida" />
				<name name="{{=aom_rohan_female_name_15}}Aldgifu" />
				<name name="{{=aom_rohan_female_name_16}}Herelind" />
				<name name="{{=aom_rohan_female_name_17}}Brynhild" />
				<name name="{{=aom_rohan_female_name_18}}Sigewyn" />
				<name name="{{=aom_rohan_female_name_19}}Eadwyn" />
				<name name="{{=aom_rohan_female_name_20}}Cynwyn" />
				<name name="{{=aom_rohan_female_name_21}}Hildred" />
				<name name="{{=aom_rohan_female_name_22}}Ethelwyn" />
				<name name="{{=aom_rohan_female_name_23}}Ceolwyn" />
				<name name="{{=aom_rohan_female_name_24}}Frithlind" />
				<name name="{{=aom_rohan_female_name_25}}Theodith" />
				<name name="{{=aom_rohan_female_name_26}}Ethelfrida" />
				<name name="{{=aom_rohan_female_name_27}}Beorhild" />
				<name name="{{=aom_rohan_female_name_28}}Eormhild" />
				<name name="{{=aom_rohan_female_name_29}}Leofhild" />
				<name name="{{=aom_rohan_female_name_30}}Aldwyn" />
				<name name="{{=aom_rohan_female_name_31}}Wynhild" />
				<name name="{{=aom_rohan_female_name_32}}Hildwyn" />
				<name name="{{=aom_rohan_female_name_33}}Eadfrida" />
				<name name="{{=aom_rohan_female_name_34}}Theodlin" />
				<name name="{{=aom_rohan_female_name_35}}Cynfrith" />
				<name name="{{=aom_rohan_female_name_36}}Guthwyn" />
				<name name="{{=aom_rohan_female_name_37}}Hildwynne" />
				<name name="{{=aom_rohan_female_name_38}}Ealhfrida" />
				<name name="{{=aom_rohan_female_name_39}}Sigith" />
				<name name="{{=aom_rohan_female_name_40}}Wynfrithia" />
				<name name="{{=aom_rohan_female_name_41}}Eadgitha" />
				<name name="{{=aom_rohan_female_name_42}}Aethelreda" />
				<name name="{{=aom_rohan_female_name_43}}Beorthwyn" />
				<name name="{{=aom_rohan_female_name_44}}Frithgifu" />
				<name name="{{=aom_rohan_female_name_45}}Theodfrith" />
				<name name="{{=aom_rohan_female_name_46}}Wynhilda" />
				<name name="{{=aom_rohan_female_name_47}}Ealhfrith" />
				<name name="{{=aom_rohan_female_name_48}}Cynethild" />
				<name name="{{=aom_rohan_female_name_49}}Ealhwyn" />
				<name name="{{=aom_rohan_female_name_50}}Eadwynne" />
			</female_names>
			<clan_names>
				<name name="{{=aom_rohan_clan_name_01}}Harolding" />
				<name name="{{=aom_rohan_clan_name_02}}Earfening" />
				<name name="{{=aom_rohan_clan_name_03}}Aldoringas" />
				<name name="{{=aom_rohan_clan_name_04}}Hamaingas" />
				<name name="{{=aom_rohan_clan_name_05}}Arngriming" />
				<name name="{{=aom_rohan_clan_name_06}}Sweordbora" />
				<name name="{{=aom_rohan_clan_name_07}}Wundeming" />
				<name name="{{=aom_rohan_clan_name_08}}Marovanding" />
				<name name="{{=aom_rohan_clan_name_09}}Kalmundas" />
				<name name="{{=aom_rohan_clan_name_10}}Earmynding" />
			</clan_names>

			<!-- Notable templates (replaces vanilla vlandia notables with Rohan-specific) -->
			<notable_templates>
				<template name="NPCCharacter.spc_wanderer_rohan_0" />
				<template name="NPCCharacter.spc_wanderer_rohan_1" />
				<template name="NPCCharacter.spc_wanderer_rohan_2" />
				<template name="NPCCharacter.spc_wanderer_rohan_3" />
				<template name="NPCCharacter.spc_wanderer_rohan_4" />
				<template name="NPCCharacter.spc_wanderer_rohan_5" />
				<template name="NPCCharacter.spc_wanderer_rohan_6" />
				<template name="NPCCharacter.spc_wanderer_rohan_7" />
				<template name="NPCCharacter.spc_wanderer_rohan_8" />
				<template name="NPCCharacter.spc_wanderer_rohan_9" />
				<template name="NPCCharacter.spc_notable_rohan_0" />
				<template name="NPCCharacter.spc_notable_rohan_0b" />
				<template name="NPCCharacter.spc_notable_rohan_1" />
				<template name="NPCCharacter.spc_notable_rohan_1b" />
				<template name="NPCCharacter.spc_notable_rohan_2" />
				<template name="NPCCharacter.spc_notable_rohan_2b" />
				<template name="NPCCharacter.spc_notable_rohan_3" />
				<template name="NPCCharacter.spc_notable_rohan_3b" />
				<template name="NPCCharacter.spc_notable_rohan_4" />
				<template name="NPCCharacter.spc_notable_rohan_4b" />
				<template name="NPCCharacter.spc_notable_rohan_5" />
				<template name="NPCCharacter.spc_notable_rohan_6" />
				<template name="NPCCharacter.spc_notable_rohan_7" />
				<template name="NPCCharacter.spc_notable_rohan_8" />
				<template name="NPCCharacter.spc_notable_rohan_9" />
				<template name="NPCCharacter.spc_notable_rohan_gl1" />
				<template name="NPCCharacter.spc_notable_rohan_10" />
				<template name="NPCCharacter.spc_notable_rohan_11" />
				<template name="NPCCharacter.spc_notable_rohan_gl4" />
				<template name="NPCCharacter.spc_notable_rohan_12" />
				<template name="NPCCharacter.spc_notable_rohan_13" />
				<template name="NPCCharacter.spc_notable_rohan_21" />
				<template name="NPCCharacter.spc_notable_rohan_22" />
				<template name="NPCCharacter.spc_rohan_headman_1" />
				<template name="NPCCharacter.spc_rohan_headman_2" />
				<template name="NPCCharacter.spc_rohan_headman_3" />
			</notable_templates>

			<!-- Pass through vanilla child elements we don't override -->
			<xsl:apply-templates select="*[not(self::vassal_reward_items or self::banner_bearer_replacement_weapons or self::default_policies or self::male_names or self::female_names or self::clan_names or self::notable_templates)]"/>
		</xsl:copy>
	</xsl:template>

	<!-- Rename khuzait to Easterling -->
	<xsl:template match="Culture[@id='khuzait']">
		<xsl:copy>
			<!-- Copy all vanilla attributes first, then override the ones we change -->
			<xsl:apply-templates select="@*"/>

			<!-- Name and description -->
			<xsl:attribute name="name">{=aom_khuzait_name}Easterlings</xsl:attribute>
			<xsl:attribute name="text">{=aom_khuzait_desc}The Easterlings are a diverse and fearsome people from the distant lands of Rhûn. Known for their loyalty to Sauron, they march to war with precision and discipline, wielding curved blades, spears, and massive war-drums that echo across the battlefield. Their golden armor and crimson banners reflect their fierce pride and martial prowess. Divided into clans and tribes, the Easterlings unite under powerful chieftains to bring the might of Rhûn against the Free Peoples of Middle-earth.</xsl:attribute>
			<xsl:attribute name="color">0xFF5B1F3A</xsl:attribute>
			<xsl:attribute name="color2">0xFFD4A947</xsl:attribute>

			<!-- Troop references -->
			<xsl:attribute name="basic_troop">NPCCharacter.easterling_recruit</xsl:attribute>
			<xsl:attribute name="elite_basic_troop">NPCCharacter.easterling_cavalry_new</xsl:attribute>
			<xsl:attribute name="melee_militia_troop">NPCCharacter.easterling_recruit</xsl:attribute>
			<xsl:attribute name="ranged_militia_troop">NPCCharacter.easterling_recruit</xsl:attribute>
			<xsl:attribute name="melee_elite_militia_troop">NPCCharacter.easterling_cavalry_new</xsl:attribute>
			<xsl:attribute name="ranged_elite_militia_troop">NPCCharacter.easterling_cavalry_new</xsl:attribute>

			<!-- Party templates -->
			<xsl:attribute name="villager_party_template">PartyTemplate.villager_khuzait_template</xsl:attribute>
			<xsl:attribute name="default_party_template">PartyTemplate.kingdom_hero_party_rhun_template</xsl:attribute>
			<xsl:attribute name="caravan_party_template">PartyTemplate.caravan_template_khuzait</xsl:attribute>
			<xsl:attribute name="elite_caravan_party_template">PartyTemplate.elite_caravan_template_khuzait</xsl:attribute>
			<xsl:attribute name="militia_party_template">PartyTemplate.militia_khuzait_template</xsl:attribute>
			<xsl:attribute name="rebels_party_template">PartyTemplate.rebels_khuzait_template</xsl:attribute>
			<xsl:attribute name="vassal_reward_party_template">PartyTemplate.vassal_reward_troops_khuzait</xsl:attribute>

			<!-- Encounter/display -->
			<xsl:attribute name="encounter_background_mesh">encounter_khuzait</xsl:attribute>
			<xsl:attribute name="board_game_type">Puluc</xsl:attribute>

			<!-- Equipment rosters -->
			<xsl:attribute name="default_battle_equipment_roster">EquipmentRoster.rhun_bat_template_medium_a</xsl:attribute>
			<xsl:attribute name="default_civilian_equipment_roster">EquipmentRoster.rhun_civ_template_a</xsl:attribute>

			<!-- NPC references -->
			<xsl:attribute name="tournament_master">NPCCharacter.tournament_master_rhun</xsl:attribute>
			<xsl:attribute name="villager">NPCCharacter.villager_rhun</xsl:attribute>
			<xsl:attribute name="caravan_master">NPCCharacter.caravan_master_rhun</xsl:attribute>
			<xsl:attribute name="armed_trader">NPCCharacter.armed_trader_rhun</xsl:attribute>
			<xsl:attribute name="caravan_guard">NPCCharacter.caravan_guard_rhun</xsl:attribute>
			<xsl:attribute name="veteran_caravan_guard">NPCCharacter.veteran_caravan_guard_rhun</xsl:attribute>
			<xsl:attribute name="prison_guard">NPCCharacter.prison_guard_rhun</xsl:attribute>
			<xsl:attribute name="guard">NPCCharacter.guard_rhun</xsl:attribute>
			<xsl:attribute name="blacksmith">NPCCharacter.blacksmith_rhun</xsl:attribute>
			<xsl:attribute name="weaponsmith">NPCCharacter.weaponsmith_rhun</xsl:attribute>
			<xsl:attribute name="townswoman">NPCCharacter.townswoman_rhun</xsl:attribute>
			<xsl:attribute name="townswoman_infant">NPCCharacter.townswoman_infant_rhun</xsl:attribute>
			<xsl:attribute name="townswoman_child">NPCCharacter.townswoman_child_rhun</xsl:attribute>
			<xsl:attribute name="townswoman_teenager">NPCCharacter.townswoman_teenager_rhun</xsl:attribute>
			<xsl:attribute name="townsman">NPCCharacter.townsman_rhun</xsl:attribute>
			<xsl:attribute name="townsman_infant">NPCCharacter.townsman_infant_rhun</xsl:attribute>
			<xsl:attribute name="townsman_child">NPCCharacter.townsman_child_rhun</xsl:attribute>
			<xsl:attribute name="townsman_teenager">NPCCharacter.townsman_teenager_rhun</xsl:attribute>
			<xsl:attribute name="village_woman">NPCCharacter.village_woman_rhun</xsl:attribute>
			<xsl:attribute name="villager_male_child">NPCCharacter.villager_child_khuzait</xsl:attribute>
			<xsl:attribute name="villager_male_teenager">NPCCharacter.villager_teenager_khuzait</xsl:attribute>
			<xsl:attribute name="villager_female_child">NPCCharacter.village_woman_child_khuzait</xsl:attribute>
			<xsl:attribute name="villager_female_teenager">NPCCharacter.village_woman_teenager_khuzait</xsl:attribute>
			<xsl:attribute name="ransom_broker">NPCCharacter.ransom_broker_rhun</xsl:attribute>
			<xsl:attribute name="gangleader_bodyguard">NPCCharacter.gangleader_bodyguard_rhun</xsl:attribute>
			<xsl:attribute name="merchant_notary">NPCCharacter.spc_notable_rhun_0</xsl:attribute>
			<xsl:attribute name="artisan_notary">NPCCharacter.spc_notable_rhun_8</xsl:attribute>
			<xsl:attribute name="preacher_notary">NPCCharacter.spc_notable_rhun_5</xsl:attribute>
			<xsl:attribute name="rural_notable_notary">NPCCharacter.spc_notable_rhun_21</xsl:attribute>
			<xsl:attribute name="shop_worker">NPCCharacter.shop_worker_rhun</xsl:attribute>
			<xsl:attribute name="tavernkeeper">NPCCharacter.tavernkeeper_rhun</xsl:attribute>
			<xsl:attribute name="taverngamehost">NPCCharacter.taverngamehost_rhun</xsl:attribute>
			<xsl:attribute name="musician">NPCCharacter.musician_rhun</xsl:attribute>
			<xsl:attribute name="tavern_wench">NPCCharacter.tavern_wench_rhun</xsl:attribute>
			<xsl:attribute name="armorer">NPCCharacter.armorer_rhun</xsl:attribute>
			<xsl:attribute name="horseMerchant">NPCCharacter.horseMerchant_rhun</xsl:attribute>
			<xsl:attribute name="barber">NPCCharacter.barber_rhun</xsl:attribute>
			<xsl:attribute name="merchant">NPCCharacter.merchant_rhun</xsl:attribute>
			<xsl:attribute name="beggar">NPCCharacter.beggar_rhun</xsl:attribute>
			<xsl:attribute name="female_beggar">NPCCharacter.female_beggar_rhun</xsl:attribute>
			<xsl:attribute name="female_dancer">NPCCharacter.female_dancer_rhun</xsl:attribute>
			<xsl:attribute name="gear_practice_dummy">NPCCharacter.gear_practice_dummy_rhun</xsl:attribute>
			<xsl:attribute name="weapon_practice_stage_1">NPCCharacter.weapon_practice_stage_1_rhun</xsl:attribute>
			<xsl:attribute name="weapon_practice_stage_2">NPCCharacter.weapon_practice_stage_2_rhun</xsl:attribute>
			<xsl:attribute name="weapon_practice_stage_3">NPCCharacter.weapon_practice_stage_3_rhun</xsl:attribute>
			<xsl:attribute name="gear_dummy">NPCCharacter.gear_dummy_rhun</xsl:attribute>

			<!-- Vassal reward items -->
			<vassal_reward_items>
				<item id="Item.steppe_war_bow" />
			</vassal_reward_items>
			<!-- Banner bearer weapons -->
			<banner_bearer_replacement_weapons>
				<item id="Item.khuzait_sword_1_t2" />
				<item id="Item.khuzait_sword_2_t3" />
				<item id="Item.khuzait_sword_3_t3" />
				<item id="Item.khuzait_sword_4_t4" />
			</banner_bearer_replacement_weapons>
			<!-- Default policies -->
			<default_policies>
				<policy id="policy_military_coronae" />
				<policy id="policy_marshals" />
				<policy id="policy_cantons" />
				<policy id="policy_noble_retinues" />
				<policy id="policy_feudal_inheritance" />
				<policy id="policy_war_tax" />
			</default_policies>

			<!-- LOTR names -->
			<male_names>
				<name name="{{=aom_rhun_male_name_1}}Khalrud" />
				<name name="{{=aom_rhun_male_name_2}}Torgash" />
				<name name="{{=aom_rhun_male_name_3}}Vaskir" />
				<name name="{{=aom_rhun_male_name_4}}Rhazir" />
				<name name="{{=aom_rhun_male_name_5}}Nazruk" />
				<name name="{{=aom_rhun_male_name_6}}Tharun" />
				<name name="{{=aom_rhun_male_name_7}}Dagrim" />
				<name name="{{=aom_rhun_male_name_8}}Zarnash" />
				<name name="{{=aom_rhun_male_name_9}}Varlek" />
				<name name="{{=aom_rhun_male_name_10}}Fazrik" />
				<name name="{{=aom_rhun_male_name_11}}Orzun" />
				<name name="{{=aom_rhun_male_name_12}}Khulgar" />
				<name name="{{=aom_rhun_male_name_13}}Durzag" />
				<name name="{{=aom_rhun_male_name_14}}Marok" />
				<name name="{{=aom_rhun_male_name_15}}Zahgrim" />
				<name name="{{=aom_rhun_male_name_16}}Narash" />
				<name name="{{=aom_rhun_male_name_17}}Tahrik" />
				<name name="{{=aom_rhun_male_name_18}}Vazrik" />
				<name name="{{=aom_rhun_male_name_19}}Uzrad" />
				<name name="{{=aom_rhun_male_name_20}}Zhoram" />
				<name name="{{=aom_rhun_male_name_21}}Rhashad" />
				<name name="{{=aom_rhun_male_name_22}}Barkhan" />
				<name name="{{=aom_rhun_male_name_23}}Sargul" />
				<name name="{{=aom_rhun_male_name_24}}Thurgan" />
				<name name="{{=aom_rhun_male_name_25}}Varim" />
				<name name="{{=aom_rhun_male_name_26}}Mazrik" />
				<name name="{{=aom_rhun_male_name_27}}Khalzar" />
				<name name="{{=aom_rhun_male_name_28}}Nashor" />
				<name name="{{=aom_rhun_male_name_29}}Gurash" />
				<name name="{{=aom_rhun_male_name_30}}Durzag" />
				<name name="{{=aom_rhun_male_name_31}}Zharkal" />
				<name name="{{=aom_rhun_male_name_32}}Torakh" />
				<name name="{{=aom_rhun_male_name_33}}Farnul" />
				<name name="{{=aom_rhun_male_name_34}}Marzuk" />
				<name name="{{=aom_rhun_male_name_35}}Zharun" />
				<name name="{{=aom_rhun_male_name_36}}Naruk" />
				<name name="{{=aom_rhun_male_name_37}}Sargan" />
				<name name="{{=aom_rhun_male_name_38}}Thurmik" />
				<name name="{{=aom_rhun_male_name_39}}Dazrik" />
				<name name="{{=aom_rhun_male_name_40}}Kharnul" />
				<name name="{{=aom_rhun_male_name_41}}Vashkir" />
				<name name="{{=aom_rhun_male_name_42}}Rhodan" />
				<name name="{{=aom_rhun_male_name_43}}Tazruk" />
				<name name="{{=aom_rhun_male_name_44}}Gorash" />
				<name name="{{=aom_rhun_male_name_45}}Nazrin" />
				<name name="{{=aom_rhun_male_name_46}}Tharkal" />
				<name name="{{=aom_rhun_male_name_47}}Mazhad" />
				<name name="{{=aom_rhun_male_name_48}}Orzrik" />
				<name name="{{=aom_rhun_male_name_49}}Zharnak" />
				<name name="{{=aom_rhun_male_name_50}}Farzuk" />
			</male_names>
			<female_names>
				<name name="{{=aom_rhun_female_name_1}}Rhazira" />
				<name name="{{=aom_rhun_female_name_2}}Tazmina" />
				<name name="{{=aom_rhun_female_name_3}}Vazira" />
				<name name="{{=aom_rhun_female_name_4}}Mazrika" />
				<name name="{{=aom_rhun_female_name_5}}Zahrina" />
				<name name="{{=aom_rhun_female_name_6}}Khalira" />
				<name name="{{=aom_rhun_female_name_7}}Orzila" />
				<name name="{{=aom_rhun_female_name_8}}Durzanna" />
				<name name="{{=aom_rhun_female_name_9}}Narima" />
				<name name="{{=aom_rhun_female_name_10}}Tharila" />
				<name name="{{=aom_rhun_female_name_11}}Varila" />
				<name name="{{=aom_rhun_female_name_12}}Dazira" />
				<name name="{{=aom_rhun_female_name_13}}Rhamira" />
				<name name="{{=aom_rhun_female_name_14}}Fazira" />
				<name name="{{=aom_rhun_female_name_15}}Uzrila" />
				<name name="{{=aom_rhun_female_name_16}}Zhalina" />
				<name name="{{=aom_rhun_female_name_17}}Khulisa" />
				<name name="{{=aom_rhun_female_name_18}}Barkhira" />
				<name name="{{=aom_rhun_female_name_19}}Nariza" />
				<name name="{{=aom_rhun_female_name_20}}Tharnila" />
				<name name="{{=aom_rhun_female_name_21}}Rhashira" />
				<name name="{{=aom_rhun_female_name_22}}Mazhara" />
				<name name="{{=aom_rhun_female_name_23}}Varshira" />
				<name name="{{=aom_rhun_female_name_24}}Zharnira" />
				<name name="{{=aom_rhun_female_name_25}}Sarkira" />
				<name name="{{=aom_rhun_female_name_26}}Farniza" />
				<name name="{{=aom_rhun_female_name_27}}Goriza" />
				<name name="{{=aom_rhun_female_name_28}}Nazira" />
				<name name="{{=aom_rhun_female_name_29}}Kharnila" />
				<name name="{{=aom_rhun_female_name_30}}Torila" />
				<name name="{{=aom_rhun_female_name_31}}Dhurina" />
				<name name="{{=aom_rhun_female_name_32}}Vashila" />
				<name name="{{=aom_rhun_female_name_33}}Nariza" />
				<name name="{{=aom_rhun_female_name_34}}Fazrim" />
				<name name="{{=aom_rhun_female_name_35}}Mazhira" />
				<name name="{{=aom_rhun_female_name_36}}Khalia" />
				<name name="{{=aom_rhun_female_name_37}}Sarniza" />
				<name name="{{=aom_rhun_female_name_38}}Orlina" />
				<name name="{{=aom_rhun_female_name_39}}Ruhana" />
				<name name="{{=aom_rhun_female_name_40}}Tharniza" />
				<name name="{{=aom_rhun_female_name_41}}Zhalira" />
				<name name="{{=aom_rhun_female_name_42}}Rhazina" />
				<name name="{{=aom_rhun_female_name_43}}Durshira" />
				<name name="{{=aom_rhun_female_name_44}}Barkhira" />
				<name name="{{=aom_rhun_female_name_45}}Thariza" />
				<name name="{{=aom_rhun_female_name_46}}Mazina" />
				<name name="{{=aom_rhun_female_name_47}}Narilia" />
				<name name="{{=aom_rhun_female_name_48}}Farnina" />
				<name name="{{=aom_rhun_female_name_49}}Zhalima" />
				<name name="{{=aom_rhun_female_name_50}}Orzina" />
			</female_names>
			<clan_names>
				<name name="{{=aom_rhun_clan_name_01}}Iron Spears" />
				<name name="{{=aom_rhun_clan_name_02}}Golden Scimitars" />
				<name name="{{=aom_rhun_clan_name_03}}Red Banners" />
				<name name="{{=aom_rhun_clan_name_04}}Burning Blades" />
				<name name="{{=aom_rhun_clan_name_05}}Blood of Rhûn" />
				<name name="{{=aom_rhun_clan_name_06}}Crimson Warriors" />
				<name name="{{=aom_rhun_clan_name_07}}Black Shields" />
				<name name="{{=aom_rhun_clan_name_08}}Stormcallers" />
				<name name="{{=aom_rhun_clan_name_09}}Scarlet Guardians" />
				<name name="{{=aom_rhun_clan_name_10}}Golden Horde" />
			</clan_names>

			<!-- Notable templates (replaces vanilla khuzait notables with Rhun-specific) -->
			<notable_templates>
				<template name="NPCCharacter.spc_wanderer_rhun_0" />
				<template name="NPCCharacter.spc_wanderer_rhun_1" />
				<template name="NPCCharacter.spc_wanderer_rhun_2" />
				<template name="NPCCharacter.spc_wanderer_rhun_3" />
				<template name="NPCCharacter.spc_wanderer_rhun_4" />
				<template name="NPCCharacter.spc_wanderer_rhun_5" />
				<template name="NPCCharacter.spc_wanderer_rhun_6" />
				<template name="NPCCharacter.spc_wanderer_rhun_7" />
				<template name="NPCCharacter.spc_wanderer_rhun_8" />
				<template name="NPCCharacter.spc_wanderer_rhun_9" />
				<template name="NPCCharacter.spc_notable_rhun_0" />
				<template name="NPCCharacter.spc_notable_rhun_0b" />
				<template name="NPCCharacter.spc_notable_rhun_1" />
				<template name="NPCCharacter.spc_notable_rhun_1b" />
				<template name="NPCCharacter.spc_notable_rhun_2" />
				<template name="NPCCharacter.spc_notable_rhun_2b" />
				<template name="NPCCharacter.spc_notable_rhun_3" />
				<template name="NPCCharacter.spc_notable_rhun_3b" />
				<template name="NPCCharacter.spc_notable_rhun_4" />
				<template name="NPCCharacter.spc_notable_rhun_4b" />
				<template name="NPCCharacter.spc_notable_rhun_5" />
				<template name="NPCCharacter.spc_notable_rhun_6" />
				<template name="NPCCharacter.spc_notable_rhun_7" />
				<template name="NPCCharacter.spc_notable_rhun_8" />
				<template name="NPCCharacter.spc_notable_rhun_9" />
				<template name="NPCCharacter.spc_notable_rhun_gl1" />
				<template name="NPCCharacter.spc_notable_rhun_10" />
				<template name="NPCCharacter.spc_notable_rhun_11" />
				<template name="NPCCharacter.spc_notable_rhun_gl4" />
				<template name="NPCCharacter.spc_notable_rhun_12" />
				<template name="NPCCharacter.spc_notable_rhun_13" />
				<template name="NPCCharacter.spc_notable_rhun_21" />
				<template name="NPCCharacter.spc_notable_rhun_22" />
				<template name="NPCCharacter.spc_rhun_headman_1" />
				<template name="NPCCharacter.spc_rhun_headman_2" />
				<template name="NPCCharacter.spc_rhun_headman_3" />
			</notable_templates>

			<!-- Pass through vanilla child elements we don't override -->
			<xsl:apply-templates select="*[not(self::vassal_reward_items or self::banner_bearer_replacement_weapons or self::default_policies or self::male_names or self::female_names or self::clan_names or self::notable_templates)]"/>
		</xsl:copy>
	</xsl:template>

	<!-- Rename sturgia to Barding -->
	<xsl:template match="Culture[@id='sturgia']">
		<xsl:copy>
			<!-- Copy all vanilla attributes first, then override the ones we change -->
			<xsl:apply-templates select="@*"/>

			<!-- Name and description -->
			<xsl:attribute name="name">{=TAOM_sturgia_culture}Barding</xsl:attribute>
			<xsl:attribute name="text">{=TAOM_sturgia_desc}The Bardings of Dale, named for Bard the Bowman, are a proud and industrious people who rose to prominence after reclaiming their homeland from the shadow of Smaug. Nestled between the Lonely Mountain and the Long Lake, Dale thrives as a hub of trade and culture. Known for their resilience and craftsmanship, the Bardings excel in forging weapons and armor, rivaling even the Dwarves of Erebor. Their armies, composed of disciplined archers, stalwart swordsmen, and agile skirmishers, defend their lands with fierce determination. United under noble leaders, the Bardings are ever watchful, guarding against the encroaching darkness and preserving their rich heritage.</xsl:attribute>

			<!-- NPC references -->
			<xsl:attribute name="tournament_master">NPCCharacter.tournament_master_dale</xsl:attribute>
			<xsl:attribute name="villager">NPCCharacter.villager_dale</xsl:attribute>
			<xsl:attribute name="caravan_master">NPCCharacter.caravan_master_dale</xsl:attribute>
			<xsl:attribute name="armed_trader">NPCCharacter.armed_trader_dale</xsl:attribute>
			<xsl:attribute name="caravan_guard">NPCCharacter.caravan_guard_dale</xsl:attribute>
			<xsl:attribute name="veteran_caravan_guard">NPCCharacter.veteran_caravan_guard_dale</xsl:attribute>
			<xsl:attribute name="prison_guard">NPCCharacter.prison_guard_dale</xsl:attribute>
			<xsl:attribute name="guard">NPCCharacter.guard_dale</xsl:attribute>
			<xsl:attribute name="blacksmith">NPCCharacter.blacksmith_dale</xsl:attribute>
			<xsl:attribute name="weaponsmith">NPCCharacter.weaponsmith_dale</xsl:attribute>
			<xsl:attribute name="townswoman">NPCCharacter.townswoman_dale</xsl:attribute>
			<xsl:attribute name="townswoman_infant">NPCCharacter.townswoman_infant_dale</xsl:attribute>
			<xsl:attribute name="townswoman_child">NPCCharacter.townswoman_child_dale</xsl:attribute>
			<xsl:attribute name="townswoman_teenager">NPCCharacter.townswoman_teenager_dale</xsl:attribute>
			<xsl:attribute name="townsman">NPCCharacter.townsman_dale</xsl:attribute>
			<xsl:attribute name="townsman_infant">NPCCharacter.townsman_infant_dale</xsl:attribute>
			<xsl:attribute name="townsman_child">NPCCharacter.townsman_child_dale</xsl:attribute>
			<xsl:attribute name="townsman_teenager">NPCCharacter.townsman_teenager_dale</xsl:attribute>
			<xsl:attribute name="village_woman">NPCCharacter.village_woman_dale</xsl:attribute>
			<xsl:attribute name="villager_male_child">NPCCharacter.villager_child_dale</xsl:attribute>
			<xsl:attribute name="villager_male_teenager">NPCCharacter.villager_teenager_dale</xsl:attribute>
			<xsl:attribute name="villager_female_child">NPCCharacter.village_woman_child_dale</xsl:attribute>
			<xsl:attribute name="villager_female_teenager">NPCCharacter.village_woman_teenager_dale</xsl:attribute>
			<xsl:attribute name="ransom_broker">NPCCharacter.ransom_broker_dale</xsl:attribute>
			<xsl:attribute name="gangleader_bodyguard">NPCCharacter.gangleader_bodyguard_dale</xsl:attribute>
			<xsl:attribute name="merchant_notary">NPCCharacter.spc_notable_dale_0</xsl:attribute>
			<xsl:attribute name="artisan_notary">NPCCharacter.spc_notable_dale_8</xsl:attribute>
			<xsl:attribute name="preacher_notary">NPCCharacter.spc_notable_dale_5</xsl:attribute>
			<xsl:attribute name="rural_notable_notary">NPCCharacter.spc_notable_dale_21</xsl:attribute>
			<xsl:attribute name="shop_worker">NPCCharacter.shop_worker_dale</xsl:attribute>
			<xsl:attribute name="tavernkeeper">NPCCharacter.tavernkeeper_dale</xsl:attribute>
			<xsl:attribute name="taverngamehost">NPCCharacter.taverngamehost_dale</xsl:attribute>
			<xsl:attribute name="musician">NPCCharacter.musician_dale</xsl:attribute>
			<xsl:attribute name="tavern_wench">NPCCharacter.tavern_wench_dale</xsl:attribute>
			<xsl:attribute name="armorer">NPCCharacter.armorer_dale</xsl:attribute>
			<xsl:attribute name="horseMerchant">NPCCharacter.horseMerchant_dale</xsl:attribute>
			<xsl:attribute name="barber">NPCCharacter.barber_dale</xsl:attribute>
			<xsl:attribute name="merchant">NPCCharacter.merchant_dale</xsl:attribute>
			<xsl:attribute name="beggar">NPCCharacter.beggar_dale</xsl:attribute>
			<xsl:attribute name="female_beggar">NPCCharacter.female_beggar_dale</xsl:attribute>
			<xsl:attribute name="female_dancer">NPCCharacter.female_dancer_dale</xsl:attribute>
			<xsl:attribute name="gear_practice_dummy">NPCCharacter.gear_practice_dummy_dale</xsl:attribute>
			<xsl:attribute name="weapon_practice_stage_1">NPCCharacter.weapon_practice_stage_1_dale</xsl:attribute>
			<xsl:attribute name="weapon_practice_stage_2">NPCCharacter.weapon_practice_stage_2_dale</xsl:attribute>
			<xsl:attribute name="weapon_practice_stage_3">NPCCharacter.weapon_practice_stage_3_dale</xsl:attribute>
			<xsl:attribute name="gear_dummy">NPCCharacter.gear_dummy_dale</xsl:attribute>

			<!-- Notable templates (replaces vanilla sturgia notables with Dale-specific) -->
			<notable_templates>
				<template name="NPCCharacter.spc_notable_dale_0" />
				<template name="NPCCharacter.spc_notable_dale_0b" />
				<template name="NPCCharacter.spc_notable_dale_1" />
				<template name="NPCCharacter.spc_notable_dale_1b" />
				<template name="NPCCharacter.spc_notable_dale_2" />
				<template name="NPCCharacter.spc_notable_dale_2b" />
				<template name="NPCCharacter.spc_notable_dale_3" />
				<template name="NPCCharacter.spc_notable_dale_3b" />
				<template name="NPCCharacter.spc_notable_dale_4" />
				<template name="NPCCharacter.spc_notable_dale_4b" />
				<template name="NPCCharacter.spc_notable_dale_5" />
				<template name="NPCCharacter.spc_notable_dale_6" />
				<template name="NPCCharacter.spc_notable_dale_7" />
				<template name="NPCCharacter.spc_notable_dale_8" />
				<template name="NPCCharacter.spc_notable_dale_9" />
				<template name="NPCCharacter.spc_notable_dale_gl1" />
				<template name="NPCCharacter.spc_notable_dale_10" />
				<template name="NPCCharacter.spc_notable_dale_11" />
				<template name="NPCCharacter.spc_notable_dale_gl4" />
				<template name="NPCCharacter.spc_notable_dale_12" />
				<template name="NPCCharacter.spc_notable_dale_13" />
				<template name="NPCCharacter.spc_notable_dale_21" />
				<template name="NPCCharacter.spc_notable_dale_22" />
				<template name="NPCCharacter.spc_dale_headman_1" />
				<template name="NPCCharacter.spc_dale_headman_2" />
				<template name="NPCCharacter.spc_dale_headman_3" />
			</notable_templates>

			<!-- Pass through vanilla child elements we don't override -->
			<xsl:apply-templates select="*[not(self::notable_templates)]"/>
		</xsl:copy>
	</xsl:template>

	<!-- Rename battania to Variag -->
	<xsl:template match="Culture[@id='battania']">
		<xsl:copy>
			<!-- Copy all vanilla attributes first, then override the ones we change -->
			<xsl:apply-templates select="@*"/>

			<!-- Name and description -->
			<xsl:attribute name="name">{=TAOM_battania_culture}Variag</xsl:attribute>
			<xsl:attribute name="text">{=TAOM_battania_desc}The Variags of Khand are a fierce and warlike people, hailing from the dry and rugged lands east of Mordor. Known for their mercenary prowess and loyalty to Sauron, the Variags fight with unmatched ferocity. They ride swift warhorses into battle, wielding curved blades and long spears with deadly precision. Their bronze and crimson armor, adorned with intricate designs, reflects their proud and martial heritage. Divided into tribes and clans, the Variags unite under powerful warlords, bringing fear and chaos to the enemies of the Dark Lord.</xsl:attribute>

			<!-- NPC references -->
			<xsl:attribute name="tournament_master">NPCCharacter.tournament_master_khand</xsl:attribute>
			<xsl:attribute name="villager">NPCCharacter.villager_khand</xsl:attribute>
			<xsl:attribute name="caravan_master">NPCCharacter.caravan_master_khand</xsl:attribute>
			<xsl:attribute name="armed_trader">NPCCharacter.armed_trader_khand</xsl:attribute>
			<xsl:attribute name="caravan_guard">NPCCharacter.caravan_guard_khand</xsl:attribute>
			<xsl:attribute name="veteran_caravan_guard">NPCCharacter.veteran_caravan_guard_khand</xsl:attribute>
			<xsl:attribute name="prison_guard">NPCCharacter.prison_guard_khand</xsl:attribute>
			<xsl:attribute name="guard">NPCCharacter.guard_khand</xsl:attribute>
			<xsl:attribute name="blacksmith">NPCCharacter.blacksmith_khand</xsl:attribute>
			<xsl:attribute name="weaponsmith">NPCCharacter.weaponsmith_khand</xsl:attribute>
			<xsl:attribute name="townswoman">NPCCharacter.townswoman_khand</xsl:attribute>
			<xsl:attribute name="townswoman_infant">NPCCharacter.townswoman_infant_khand</xsl:attribute>
			<xsl:attribute name="townswoman_child">NPCCharacter.townswoman_child_khand</xsl:attribute>
			<xsl:attribute name="townswoman_teenager">NPCCharacter.townswoman_teenager_khand</xsl:attribute>
			<xsl:attribute name="townsman">NPCCharacter.townsman_khand</xsl:attribute>
			<xsl:attribute name="townsman_infant">NPCCharacter.townsman_infant_khand</xsl:attribute>
			<xsl:attribute name="townsman_child">NPCCharacter.townsman_child_khand</xsl:attribute>
			<xsl:attribute name="townsman_teenager">NPCCharacter.townsman_teenager_khand</xsl:attribute>
			<xsl:attribute name="village_woman">NPCCharacter.village_woman_khand</xsl:attribute>
			<xsl:attribute name="villager_male_child">NPCCharacter.villager_child_khand</xsl:attribute>
			<xsl:attribute name="villager_male_teenager">NPCCharacter.villager_teenager_khand</xsl:attribute>
			<xsl:attribute name="villager_female_child">NPCCharacter.village_woman_child_khand</xsl:attribute>
			<xsl:attribute name="villager_female_teenager">NPCCharacter.village_woman_teenager_khand</xsl:attribute>
			<xsl:attribute name="ransom_broker">NPCCharacter.ransom_broker_khand</xsl:attribute>
			<xsl:attribute name="gangleader_bodyguard">NPCCharacter.gangleader_bodyguard_khand</xsl:attribute>
			<xsl:attribute name="merchant_notary">NPCCharacter.spc_notable_khand_0</xsl:attribute>
			<xsl:attribute name="artisan_notary">NPCCharacter.spc_notable_khand_8</xsl:attribute>
			<xsl:attribute name="preacher_notary">NPCCharacter.spc_notable_khand_5</xsl:attribute>
			<xsl:attribute name="rural_notable_notary">NPCCharacter.spc_notable_khand_21</xsl:attribute>
			<xsl:attribute name="shop_worker">NPCCharacter.shop_worker_khand</xsl:attribute>
			<xsl:attribute name="tavernkeeper">NPCCharacter.tavernkeeper_khand</xsl:attribute>
			<xsl:attribute name="taverngamehost">NPCCharacter.taverngamehost_khand</xsl:attribute>
			<xsl:attribute name="musician">NPCCharacter.musician_khand</xsl:attribute>
			<xsl:attribute name="tavern_wench">NPCCharacter.tavern_wench_khand</xsl:attribute>
			<xsl:attribute name="armorer">NPCCharacter.armorer_khand</xsl:attribute>
			<xsl:attribute name="horseMerchant">NPCCharacter.horseMerchant_khand</xsl:attribute>
			<xsl:attribute name="barber">NPCCharacter.barber_khand</xsl:attribute>
			<xsl:attribute name="merchant">NPCCharacter.merchant_khand</xsl:attribute>
			<xsl:attribute name="beggar">NPCCharacter.beggar_khand</xsl:attribute>
			<xsl:attribute name="female_beggar">NPCCharacter.female_beggar_khand</xsl:attribute>
			<xsl:attribute name="female_dancer">NPCCharacter.female_dancer_khand</xsl:attribute>
			<xsl:attribute name="gear_practice_dummy">NPCCharacter.gear_practice_dummy_khand</xsl:attribute>
			<xsl:attribute name="weapon_practice_stage_1">NPCCharacter.weapon_practice_stage_1_khand</xsl:attribute>
			<xsl:attribute name="weapon_practice_stage_2">NPCCharacter.weapon_practice_stage_2_khand</xsl:attribute>
			<xsl:attribute name="weapon_practice_stage_3">NPCCharacter.weapon_practice_stage_3_khand</xsl:attribute>
			<xsl:attribute name="gear_dummy">NPCCharacter.gear_dummy_khand</xsl:attribute>

			<!-- Notable templates (replaces vanilla battania notables with Khand-specific) -->
			<notable_templates>
				<template name="NPCCharacter.spc_notable_khand_0" />
				<template name="NPCCharacter.spc_notable_khand_0b" />
				<template name="NPCCharacter.spc_notable_khand_1" />
				<template name="NPCCharacter.spc_notable_khand_1b" />
				<template name="NPCCharacter.spc_notable_khand_2" />
				<template name="NPCCharacter.spc_notable_khand_2b" />
				<template name="NPCCharacter.spc_notable_khand_3" />
				<template name="NPCCharacter.spc_notable_khand_3b" />
				<template name="NPCCharacter.spc_notable_khand_4" />
				<template name="NPCCharacter.spc_notable_khand_4b" />
				<template name="NPCCharacter.spc_notable_khand_5" />
				<template name="NPCCharacter.spc_notable_khand_6" />
				<template name="NPCCharacter.spc_notable_khand_7" />
				<template name="NPCCharacter.spc_notable_khand_8" />
				<template name="NPCCharacter.spc_notable_khand_9" />
				<template name="NPCCharacter.spc_notable_khand_gl1" />
				<template name="NPCCharacter.spc_notable_khand_10" />
				<template name="NPCCharacter.spc_notable_khand_11" />
				<template name="NPCCharacter.spc_notable_khand_gl4" />
				<template name="NPCCharacter.spc_notable_khand_12" />
				<template name="NPCCharacter.spc_notable_khand_13" />
				<template name="NPCCharacter.spc_notable_khand_21" />
				<template name="NPCCharacter.spc_notable_khand_22" />
				<template name="NPCCharacter.spc_khand_headman_1" />
				<template name="NPCCharacter.spc_khand_headman_2" />
				<template name="NPCCharacter.spc_khand_headman_3" />
			</notable_templates>

			<!-- Pass through vanilla child elements we don't override -->
			<xsl:apply-templates select="*[not(self::notable_templates)]"/>
		</xsl:copy>
	</xsl:template>

</xsl:stylesheet>
