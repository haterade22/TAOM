<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
	<xsl:output omit-xml-declaration="yes"/>
	<xsl:template match="@*|node()">
		<xsl:copy>
			<xsl:apply-templates select="@*|node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Remove vanilla culture descriptions (replaced with LOTR lore) -->
	<xsl:template match="string[@id='str_culture_description.empire']"/>
	<xsl:template match="string[@id='str_culture_description.vlandia']"/>
	<xsl:template match="string[@id='str_culture_description.battania']"/>
	<xsl:template match="string[@id='str_culture_description.khuzait']"/>
	<xsl:template match="string[@id='str_culture_description.aserai']"/>
	<xsl:template match="string[@id='str_culture_description.sturgia']"/>

	<!-- Remove vanilla culture rich names -->
	<xsl:template match="string[@id='str_culture_rich_name.empire']"/>
	<xsl:template match="string[@id='str_culture_rich_name.vlandia']"/>
	<xsl:template match="string[@id='str_culture_rich_name.battania']"/>
	<xsl:template match="string[@id='str_culture_rich_name.khuzait']"/>
	<xsl:template match="string[@id='str_culture_rich_name.aserai']"/>
	<xsl:template match="string[@id='str_culture_rich_name.sturgia']"/>

	<!-- Remove vanilla culture adjectives -->
	<xsl:template match="string[@id='str_adjective_for_culture.empire']"/>
	<xsl:template match="string[@id='str_adjective_for_culture.vlandia']"/>
	<xsl:template match="string[@id='str_adjective_for_culture.battania']"/>
	<xsl:template match="string[@id='str_adjective_for_culture.khuzait']"/>
	<xsl:template match="string[@id='str_adjective_for_culture.aserai']"/>
	<xsl:template match="string[@id='str_adjective_for_culture.sturgia']"/>

	<!-- Remove vanilla faction strings for empire (remapped to Dunland) -->
	<xsl:template match="string[@id='str_faction_official.empire']"/>
	<xsl:template match="string[@id='str_faction_official.empire_f']"/>
	<xsl:template match="string[@id='str_faction_ruler.empire']"/>
	<xsl:template match="string[@id='str_faction_ruler.empire_f']"/>
	<xsl:template match="string[@id='str_faction_ruler_term_in_speech.empire']"/>
	<xsl:template match="string[@id='str_faction_ruler_name_with_title.empire']"/>
	<xsl:template match="string[@id='str_faction_noble_name_with_title.empire']"/>
	<xsl:template match="string[@id='str_adjective_for_faction.empire']"/>
	<xsl:template match="string[@id='str_adjective_for_faction.empire_w']"/>
	<xsl:template match="string[@id='str_adjective_for_faction.empire_s']"/>
	<xsl:template match="string[@id='str_short_term_for_faction.empire']"/>
	<xsl:template match="string[@id='str_short_term_for_faction.empire_w']"/>
	<xsl:template match="string[@id='str_short_term_for_faction.empire_s']"/>
	<xsl:template match="string[@id='str_faction_formal_name_for_culture.empire']"/>
	<xsl:template match="string[@id='str_faction_informal_name_for_culture.empire']"/>
	<xsl:template match="string[@id='str_neutral_term_for_culture.empire']"/>

	<!-- Remove vanilla faction strings for vlandia (remapped to Rohan) -->
	<xsl:template match="string[@id='str_faction_official.vlandia']"/>
	<xsl:template match="string[@id='str_faction_official.vlandia_f']"/>
	<xsl:template match="string[@id='str_faction_ruler.vlandia']"/>
	<xsl:template match="string[@id='str_faction_ruler.vlandia_f']"/>
	<xsl:template match="string[@id='str_faction_ruler_term_in_speech.vlandia']"/>
	<xsl:template match="string[@id='str_faction_ruler_name_with_title.vlandia']"/>
	<xsl:template match="string[@id='str_faction_noble_name_with_title.vlandia']"/>
	<xsl:template match="string[@id='str_adjective_for_faction.vlandia']"/>
	<xsl:template match="string[@id='str_short_term_for_faction.vlandia']"/>
	<xsl:template match="string[@id='str_faction_formal_name_for_culture.vlandia']"/>
	<xsl:template match="string[@id='str_faction_informal_name_for_culture.vlandia']"/>
	<xsl:template match="string[@id='str_neutral_term_for_culture.vlandia']"/>

	<!-- Remove vanilla faction strings for battania (remapped to Khand) -->
	<xsl:template match="string[@id='str_faction_official.battania']"/>
	<xsl:template match="string[@id='str_faction_official.battania_f']"/>
	<xsl:template match="string[@id='str_faction_ruler.battania']"/>
	<xsl:template match="string[@id='str_faction_ruler.battania_f']"/>
	<xsl:template match="string[@id='str_faction_ruler_term_in_speech.battania']"/>
	<xsl:template match="string[@id='str_faction_ruler_name_with_title.battania']"/>
	<xsl:template match="string[@id='str_faction_noble_name_with_title.battania']"/>
	<xsl:template match="string[@id='str_adjective_for_faction.battania']"/>
	<xsl:template match="string[@id='str_short_term_for_faction.battania']"/>
	<xsl:template match="string[@id='str_faction_formal_name_for_culture.battania']"/>
	<xsl:template match="string[@id='str_faction_informal_name_for_culture.battania']"/>
	<xsl:template match="string[@id='str_neutral_term_for_culture.battania']"/>

	<!-- Remove vanilla faction strings for khuzait (remapped to Rhun) -->
	<xsl:template match="string[@id='str_faction_official.khuzait']"/>
	<xsl:template match="string[@id='str_faction_official.khuzait_f']"/>
	<xsl:template match="string[@id='str_faction_ruler.khuzait']"/>
	<xsl:template match="string[@id='str_faction_ruler.khuzait_f']"/>
	<xsl:template match="string[@id='str_faction_ruler_term_in_speech.khuzait']"/>
	<xsl:template match="string[@id='str_faction_ruler_name_with_title.khuzait']"/>
	<xsl:template match="string[@id='str_faction_noble_name_with_title.khuzait']"/>
	<xsl:template match="string[@id='str_adjective_for_faction.khuzait']"/>
	<xsl:template match="string[@id='str_short_term_for_faction.khuzait']"/>
	<xsl:template match="string[@id='str_faction_formal_name_for_culture.khuzait']"/>
	<xsl:template match="string[@id='str_faction_informal_name_for_culture.khuzait']"/>
	<xsl:template match="string[@id='str_neutral_term_for_culture.khuzait']"/>

	<!-- Remove vanilla faction strings for aserai (remapped to Harad) -->
	<xsl:template match="string[@id='str_faction_official.aserai']"/>
	<xsl:template match="string[@id='str_faction_official.aserai_f']"/>
	<xsl:template match="string[@id='str_faction_ruler.aserai']"/>
	<xsl:template match="string[@id='str_faction_ruler.aserai_f']"/>
	<xsl:template match="string[@id='str_faction_ruler_term_in_speech.aserai']"/>
	<xsl:template match="string[@id='str_faction_ruler_name_with_title.aserai']"/>
	<xsl:template match="string[@id='str_faction_noble_name_with_title.aserai']"/>
	<xsl:template match="string[@id='str_adjective_for_faction.aserai']"/>
	<xsl:template match="string[@id='str_short_term_for_faction.aserai']"/>
	<xsl:template match="string[@id='str_faction_formal_name_for_culture.aserai']"/>
	<xsl:template match="string[@id='str_faction_informal_name_for_culture.aserai']"/>
	<xsl:template match="string[@id='str_neutral_term_for_culture.aserai']"/>

	<!-- Remove vanilla faction strings for sturgia (remapped to Dale) -->
	<xsl:template match="string[@id='str_faction_official.sturgia']"/>
	<xsl:template match="string[@id='str_faction_official.sturgia_f']"/>
	<xsl:template match="string[@id='str_faction_ruler.sturgia']"/>
	<xsl:template match="string[@id='str_faction_ruler.sturgia_f']"/>
	<xsl:template match="string[@id='str_faction_ruler_term_in_speech.sturgia']"/>
	<xsl:template match="string[@id='str_faction_ruler_name_with_title.sturgia']"/>
	<xsl:template match="string[@id='str_faction_noble_name_with_title.sturgia']"/>
	<xsl:template match="string[@id='str_adjective_for_faction.sturgia']"/>
	<xsl:template match="string[@id='str_short_term_for_faction.sturgia']"/>
	<xsl:template match="string[@id='str_faction_formal_name_for_culture.sturgia']"/>
	<xsl:template match="string[@id='str_faction_informal_name_for_culture.sturgia']"/>
	<xsl:template match="string[@id='str_neutral_term_for_culture.sturgia']"/>

</xsl:stylesheet>
