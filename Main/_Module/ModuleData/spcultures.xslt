<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
	<!-- Identity transformation - copies everything by default -->
	<xsl:output omit-xml-declaration="no" indent="yes"/>

	<xsl:template match="@*|node()">
		<xsl:copy>
			<xsl:apply-templates select="@*|node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Remove base 6 cultures that we're replacing with LOTR equivalents -->
	<!-- Note: empire culture covers all 3 empire factions (Dunland, Gondor, Mordor) -->
	<xsl:template match="Culture[@id='empire']"/>
	<xsl:template match="Culture[@id='sturgia']"/>
	<xsl:template match="Culture[@id='aserai']"/>
	<xsl:template match="Culture[@id='vlandia']"/>
	<xsl:template match="Culture[@id='battania']"/>
	<xsl:template match="Culture[@id='khuzait']"/>
</xsl:stylesheet>
