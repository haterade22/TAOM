<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
	<xsl:output omit-xml-declaration="no" indent="yes"/>

	<!-- Identity transformation - copies everything by default -->
	<xsl:template match="@*|node()">
		<xsl:copy>
			<xsl:apply-templates select="@*|node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- ============================================================ -->
	<!-- Lord self-introductions: "the" removal before faction and    -->
	<!-- clan names, and Middle-earth lore for the retagged cultures  -->
	<!-- (Dale, Khand, Rohan, Harad, Rhun: taom_lord_intro_* keys)    -->
	<!-- ============================================================ -->

	<xsl:template match="string[@id='str_comment_vassal_introduces_self.default']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=L2GbDVja}I am {CONVERSATION_CHARACTER.LINK} of {CLAN_NAME}, my {?PLAYER.GENDER}lady{?}lord{\?}, and I am honored to serve you.</xsl:attribute>
		<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_comment_noble_introduces_self.sturgia']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=dC8acIya}I am {CONVERSATION_CHARACTER.LINK} of {CLAN_NAME}.</xsl:attribute>
		<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_comment_noble_introduces_self_and_clan.sturgia']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=taom_lord_intro_dale_1}I am {CONVERSATION_CHARACTER.LINK}, of {CLAN_NAME}, one of the old houses of Dale.</xsl:attribute>
		<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_comment_noble_introduces_self_and_clan.sturgia_boasting']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=taom_lord_intro_dale_2}I am {CONVERSATION_CHARACTER.LINK}, of {CLAN_NAME}. Our name is known in every hall of Dale and on every quay of the Long Lake.</xsl:attribute>
		<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_comment_noble_introduces_self_and_clan.sturgia_boasting_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=taom_lord_intro_dale_3}I am {CONVERSATION_CHARACTER.LINK}, of {CLAN_NAME}. My forefathers came home to Dale when the dragon was slain, and swore to hold the vale beneath the Mountain. We have kept that oath.</xsl:attribute>
		<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_comment_noble_introduces_self.battania']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=3UucAOBW}I am {CONVERSATION_CHARACTER.LINK} of {CLAN_NAME}.</xsl:attribute>
		<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_comment_noble_introduces_self_and_clan.battania']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=taom_lord_intro_khand_1}I am {CONVERSATION_CHARACTER.LINK}, of {CLAN_NAME}, one of the old blood-lines of Khand.</xsl:attribute>
		<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_comment_noble_introduces_self_and_clan.battania_boasting']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=taom_lord_intro_khand_2}I am {CONVERSATION_CHARACTER.LINK}, of {CLAN_NAME}. No clan of the Variags is more feared, from the borders of Mordor to the southern sands.</xsl:attribute>
		<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_comment_noble_introduces_self_and_clan.battania_boasting_harsh']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=taom_lord_intro_khand_3}I am {CONVERSATION_CHARACTER.LINK}, of {CLAN_NAME}. There is hardly a tribe in Khand that has not left a chieftain's skull in our tents.</xsl:attribute>
		<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_comment_noble_introduces_self_and_clan.battania_boasting_ironic']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=taom_lord_intro_khand_4}I am {CONVERSATION_CHARACTER.LINK}, of {CLAN_NAME}. The tale goes that our first chieftain rode out of the east on a horse that never tired. If every clan that tells that tale were telling the truth, the plains would have run out of horses long ago.</xsl:attribute>
		<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_comment_noble_introduces_self.vlandia']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=WH8P3Y5F}I am {CONVERSATION_CHARACTER.LINK} of {CLAN_NAME}.</xsl:attribute>
		<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_comment_noble_introduces_self_and_clan.vlandia']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=taom_lord_intro_rohan_1}I am {CONVERSATION_CHARACTER.LINK}, of {CLAN_NAME}. My house has ridden for the Kings of the Mark since the days of Eorl.</xsl:attribute>
		<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_comment_noble_introduces_self_and_clan.vlandia_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=cMO9JXvk}I am {CONVERSATION_CHARACTER.LINK}, of {CLAN_NAME}. We hold our lands in fief to {LIEGE_TITLE}, and we honor our oaths of fealty.</xsl:attribute>
		<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_comment_noble_introduces_self_and_clan.vlandia_boasting']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=taom_lord_intro_rohan_2}I am {CONVERSATION_CHARACTER.LINK}, of {CLAN_NAME}, a house well remembered in the songs of the Riddermark.</xsl:attribute>
		<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_comment_noble_introduces_self_and_clan.vlandia_boasting_ironic']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=taom_lord_intro_rohan_3}I am {CONVERSATION_CHARACTER.LINK}, of {CLAN_NAME}. They say our founder won his lands by outriding a king's messenger to the Golden Hall. He never did tell us what the message was.</xsl:attribute>
		<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_comment_noble_introduces_self.aserai']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=l2nEIZV5}I am {CONVERSATION_CHARACTER.LINK} of {CLAN_NAME}.</xsl:attribute>
		<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_comment_noble_introduces_self_and_clan.aserai']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=taom_lord_intro_harad_1}I am {CONVERSATION_CHARACTER.LINK}, of {CLAN_NAME}, a house of the Haradrim.</xsl:attribute>
		<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_comment_noble_introduces_self_and_clan.aserai_boasting']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=taom_lord_intro_harad_2}I am {CONVERSATION_CHARACTER.LINK}, of {CLAN_NAME}, the oldest and proudest line under the sun of Harad.</xsl:attribute>
		<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_comment_noble_introduces_self.khuzait']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=U4p70l9B}I am {CONVERSATION_CHARACTER.LINK} of {CLAN_NAME}.</xsl:attribute>
		<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_comment_noble_introduces_self_and_clan.khuzait_boasting']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=taom_lord_intro_rhun_1}I am {CONVERSATION_CHARACTER.LINK}, of {CLAN_NAME}. Our name is sung at every wagon-fire in Rhun, from the shores of the inland sea to the far plains.</xsl:attribute>
		<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_comment_noble_introduces_self_and_clan.khuzait']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=taom_lord_intro_rhun_2}I am {CONVERSATION_CHARACTER.LINK}, of {CLAN_NAME}. My clan has served the lords of Rhun for generations.</xsl:attribute>
		<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- ============================================================ -->
	<!-- Dunland (empire) self-introductions: the last 5 of the 19        -->
	<!-- taom_lord_intro_* rows. Vanilla rows named Calradian peoples or  -->
	<!-- places. New TAOM keys, so no language keeps the old Calradian    -->
	<!-- translation (CommentStringLoreTests).                            -->
	<!-- ============================================================ -->

	<xsl:template match="string[@id='str_comment_noble_introduces_self_and_clan.empire']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=taom_lord_intro_dunland_1}I am {CONVERSATION_CHARACTER.LINK}, of the house of {CLAN_NAME}. My kin held these hills long before the horse-lords came.</xsl:attribute>
		<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_comment_noble_introduces_self_and_clan.empire_honor']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=taom_lord_intro_dunland_2}I am {CONVERSATION_CHARACTER.LINK}, of the house of {CLAN_NAME}. My family keeps its word and its feuds alike, as the old ways of Dunland demand.</xsl:attribute>
		<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_comment_noble_introduces_self_and_clan.empire_mercy']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=taom_lord_intro_dunland_3}I am {CONVERSATION_CHARACTER.LINK}, of the house of {CLAN_NAME}. I take no great pride in my lineage, but we have always stood between our hill-folk and those who would harm them.</xsl:attribute>
		<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_comment_noble_introduces_self_and_clan.empire_boasting']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=taom_lord_intro_dunland_4}I am {CONVERSATION_CHARACTER.LINK}, of the house of {CLAN_NAME}, one of the proudest clans of Dunland.</xsl:attribute>
		<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_comment_noble_introduces_self_and_clan.empire_boasting_cruel']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=taom_lord_intro_dunland_5}I am {CONVERSATION_CHARACTER.LINK}, of the house of {CLAN_NAME}, one of the proudest clans of Dunland. No house has put more horse-thieves of the Mark into the ground.</xsl:attribute>
		<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Default noble and minor-faction introductions: "the" removal only (vanilla keys) -->

	<xsl:template match="string[@id='str_comment_noble_introduces_self_and_clan.default']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=62vceTar}I am {CONVERSATION_CHARACTER.LINK}, of {CLAN_NAME}.</xsl:attribute>
		<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_comment_minor_faction_leader_introduces_self.default']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=99gE1ZoY}I am {CONVERSATION_CHARACTER.LINK}, of {FACTION}.</xsl:attribute>
		<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- ============================================================ -->
	<!-- Liege titles - "I am Théoden, {LIEGE_TITLE}."                -->
	<!--                                                              -->
	<!-- Two lookups read these rows. The dialog path (ConversationManager. -->
	<!-- FindMatchingTextOrNull) ignores the id suffix and selects by the   -->
	<!-- <tags> child (VlandianTag -> Culture.StringId == "vlandia"), NOT   -->
	<!-- by kingdom name, so the Rohan rename in spkingdoms.xslt never      -->
	<!-- reaches this line; a tag-stripped row matches every culture at     -->
	<!-- once, so each template MUST re-emit node(). The coronation title   -->
	<!-- and other GameTexts.FindText(id, variation) callers select by the  -->
	<!-- id suffix instead. The TAOM-only cultures get their rows, each  -->
	<!-- with its own taom_culture_<id> tag, in taom_module_strings.xml.     -->
	<!-- ============================================================ -->

	<xsl:template match="string[@id='str_liege_title.empire']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_liege_dunland}Brenin of Dunland</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_liege_title_female.empire']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_liege_dunland_female}Brenhines of Dunland</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_liege_title.sturgia']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_liege_dale}King of Dale</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_liege_title_female.sturgia']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_liege_dale_female}Queen of Dale</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_liege_title.aserai']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_liege_harad}Taskral of Harad</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_liege_title_female.aserai']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_liege_harad_female}Taskral of Harad</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_liege_title.battania']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_liege_khand}Khudriag of Khand</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_liege_title_female.battania']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_liege_khand_female}Khudriag of Khand</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_liege_title.vlandia']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_liege_rohan}King of the Mark</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_liege_title_female.vlandia']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_liege_rohan_female}Queen of the Mark</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_liege_title.khuzait']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_liege_rhun}Loke-Kan of Rhun</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_liege_title_female.khuzait']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_liege_rhun_female}Loke-Kan of Rhun</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

</xsl:stylesheet>
