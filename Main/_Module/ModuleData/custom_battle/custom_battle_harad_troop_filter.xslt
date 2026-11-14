<?xml version="1.0" encoding="utf-8"?>
<!--
  Custom Battle only (SubModule.xml registers this for CustomGame, after every NPCCharacters node).
  Custom Battle fills each formation slot from every Soldier-occupation character that is not obsolete,
  of the slot's culture and formation class (ArmyCompositionGroupVM, ArmyCompositionItemVM.IsValidUnitItem).
  TAOM reuses Culture.aserai for Harwan and has Shaghana on Culture.shaghana, so the Harwan/Shaghana
  lists also offered vanilla and TAOM soldiers outside the army, such as Jawwal, Beni Zilal, militia and
  caravan guards. This marks every non-hero Aserai/Shaghana character obsolete unless its id starts
  with "harad_" (all TAOM Harad troops: regular, militia, Serpent, Red Serpent, Pezarsan, noble,
  elephant, howdah, mumakil). Lords and heroes are left alone. Campaigns never load this file.
  The same goes for the four minor clans whose troops lords.xslt retags to their side's culture (#957): the
  legion_of_the_betrayed, company_of_the_boar, eleftheroi and brotherhood_of_woods tier troops now sit on Arthedain,
  Umbar, Sturgia and Dunland, and would otherwise be offered in those cultures' Custom Battle slots. They are
  marked obsolete here by id prefix (CustomBattleTroopFilterTests).
  taom_vanilla_troop_obsolete_overrides.xml already hides the main vanilla Aserai line everywhere.
  Vanilla soldiers of the other reused cultures (empire Dunland, sturgia Dale, vlandia Rohan, khuzait Rhun)
  were still offered in those slots: 88 counted on the v1.5.7 install (main lines, militia, and the vanilla minor clans
  hidden_hand, embers_of_flame, lakepike, karakhuzaits). Training dummies and training-yard opponents
  (gear_practice_dummy_*, gear_dummy_*, weapon_dummy_*, weapon_practice_stage_*) are Soldier-occupation in most
  TAOM cultures and were offered too. Both are marked obsolete here by id. No TAOM troop uses these prefixes;
  campaigns, which read the dummies and stages by id from culture data, never load this file.
  Culture.battania (Khand) is left out of the vanilla-troop rule: TAOM has no Khand soldiers of its own, so its
  Custom Battle slots are still the vanilla Battanian line, and hiding it would leave the ranged and cavalry
  slots empty. Aserai/Shaghana characters stay with the Harad rule below.
  The vanilla CustomBattle module's 24 Calradian commanders (commander_1 to commander_24) sit on reused
  cultures, so the muster screen (TcbVM: IsHero, culture, not obsolete) offered them as commanders of Rohan,
  Rhun, Dale and Harwan next to TAOM's lords. Those are marked obsolete too. Vanilla reads commander_N only by
  id (CustomBattleData.Characters, which TAOM replaces, and the CPU benchmark), and an obsolete object still
  resolves by id. Culture.empire (Dunland) and Culture.battania (Khand) keep theirs: TAOM has no Custom Battle
  heroes of those cultures, so the vanilla commanders are their only ones.
-->
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
	<xsl:output omit-xml-declaration="no" indent="yes"/>

	<xsl:template match="@*|node()">
		<xsl:copy>
			<xsl:apply-templates select="@*|node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[not(@is_hero='true') and (starts-with(@id, 'legion_of_the_betrayed_tier_') or starts-with(@id, 'company_of_the_boar_tier_') or starts-with(@id, 'eleftheroi_tier_') or starts-with(@id, 'brotherhood_of_woods_tier_'))]">
		<xsl:copy>
			<xsl:apply-templates select="@*[name() != 'is_obsolete']"/>
			<xsl:attribute name="is_obsolete">true</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[not(@is_hero='true') and not(@culture='Culture.aserai' or @culture='Culture.shaghana') and (starts-with(@id, 'gear_practice_dummy_') or starts-with(@id, 'gear_dummy_') or starts-with(@id, 'weapon_dummy_') or starts-with(@id, 'weapon_practice_stage_') or (not(@culture='Culture.battania') and (starts-with(@id, 'imperial_') or starts-with(@id, 'sturgian_') or starts-with(@id, 'vlandian_') or starts-with(@id, 'khuzait_') or starts-with(@id, 'hidden_hand_tier_') or starts-with(@id, 'embers_of_flame_tier_') or starts-with(@id, 'lakepike_tier_') or starts-with(@id, 'karakhuzaits_tier_') or @id='bucellarii' or @id='varyag' or @id='varyag_veteran' or @id='druzhinnik' or @id='druzhinnik_champion')))]">
		<xsl:copy>
			<xsl:apply-templates select="@*[name() != 'is_obsolete']"/>
			<xsl:attribute name="is_obsolete">true</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@is_hero='true' and not(@culture='Culture.empire' or @culture='Culture.battania') and starts-with(@id, 'commander_') and string-length(@id) &gt; 10 and translate(substring(@id, 11), '0123456789', '') = '']">
		<xsl:copy>
			<xsl:apply-templates select="@*[name() != 'is_obsolete']"/>
			<xsl:attribute name="is_obsolete">true</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[(@culture='Culture.aserai' or @culture='Culture.shaghana') and not(@is_hero='true') and not(starts-with(@id, 'harad_'))]">
		<xsl:copy>
			<xsl:apply-templates select="@*[name() != 'is_obsolete']"/>
			<xsl:attribute name="is_obsolete">true</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>
</xsl:stylesheet>
