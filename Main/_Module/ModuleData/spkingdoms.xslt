<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
	<!-- Identity transformation - copies everything by default -->
	<xsl:output omit-xml-declaration="no" indent="yes"/>

	<xsl:template match="@*|node()">
		<xsl:copy>
			<xsl:apply-templates select="@*|node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Remove base 8 kingdoms that we're replacing with LOTR equivalents -->
	<xsl:template match="Kingdom[@id='empire']"/>
	<xsl:template match="Kingdom[@id='empire_w']"/>
	<xsl:template match="Kingdom[@id='empire_s']"/>
	<xsl:template match="Kingdom[@id='sturgia']"/>
	<xsl:template match="Kingdom[@id='aserai']"/>
	<xsl:template match="Kingdom[@id='vlandia']"/>
	<xsl:template match="Kingdom[@id='battania']"/>
	<xsl:template match="Kingdom[@id='khuzait']"/>
</xsl:stylesheet>
