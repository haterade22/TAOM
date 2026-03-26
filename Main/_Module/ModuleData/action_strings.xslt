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
	<!-- Remove "the" before faction/kingdom/clan names               -->
	<!-- Vanilla uses "of the {KINGDOM}" which works for names like   -->
	<!-- "Northern Empire" but not for LOTR names like "Gondor"       -->
	<!-- ============================================================ -->

	<xsl:template match="string[@id='str_destroy_party_news']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=hP57Caab}{VICTOR_PARTY} of {VICTOR_FACTION} defeats {DEFEATED_PARTY} of {DEFEATED_FACTION}.</xsl:attribute>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_factions_make_peace_news']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TsYCa2fZ}{FACTION1} and {FACTION2} have made peace.</xsl:attribute>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_factions_declare_war_news']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=hopvV6Ch}{RULER_NAME} of {FACTION1_NAME} declares war on {FACTION2_NAME}.</xsl:attribute>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_factions_declare_war_news_direct']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=SLAkSrb5}{FACTION1_NAME} declares war on {FACTION2_NAME}.</xsl:attribute>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_party_besieging_news_with_link']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=AxMrFoea}{TOWN_NAME} has been besieged by forces led by {LORD.LINK} of {FACTION_NAME}!</xsl:attribute>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_party_besieging_news']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=AxMrFoea}{TOWN_NAME} has been besieged by forces led by {LORD.LINK} of {FACTION_NAME}!</xsl:attribute>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_army_besieging_news_with_link']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=EHFibjkM}{TOWN_NAME} has been besieged by the army led by {LORD.LINK} of {FACTION_NAME}!</xsl:attribute>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_army_besieging_news']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=kqwNCCbZ}{TOWN_NAME} has been besieged by army led by {LORD.LINK} of {FACTION_NAME}!</xsl:attribute>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_army_raiding_news']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=nbVW2cJ8}{SETTLEMENT} is raided by the army led by {LORD.LINK} of {FACTION_NAME}</xsl:attribute>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_party_raiding_news']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=hbWhfoWH}{SETTLEMENT} is raided by {LORD.LINK} of {FACTION_NAME}</xsl:attribute>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_add_contraband_news']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=!}Trading of {ITEM} is forbidden by {FACTION_NAME_1}.</xsl:attribute>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_notification_secondary_peace']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=hJFCi1zu}{FACTION1} and {FACTION2} have made peace.</xsl:attribute>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_notification_change_faction']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=dfAE3liA}{CLAN_NAME} clan has joined {FACTION}.</xsl:attribute>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_notification_mercenary_contract']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=ExbVzTZA}{CLAN} have contracted to fight alongside {KINGDOM}.</xsl:attribute>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_notification_mercenary_contract_end']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=S0oFKYfB}{CLAN} no longer serve {KINGDOM}.</xsl:attribute>
		</xsl:copy>
	</xsl:template>

</xsl:stylesheet>
