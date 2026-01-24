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
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_empire_culture}Dunlending</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Rename sturgia to Dalish -->
	<xsl:template match="Culture[@id='sturgia']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_sturgia_culture}Dalish</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Rename aserai to Haradrim -->
	<xsl:template match="Culture[@id='aserai']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_aserai_culture}Haradrim</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Rename vlandia to Rohirrim -->
	<xsl:template match="Culture[@id='vlandia']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_vlandia_culture}Rohirrim</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Rename battania to Variag -->
	<xsl:template match="Culture[@id='battania']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_battania_culture}Variag</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Rename khuzait to Easterling -->
	<xsl:template match="Culture[@id='khuzait']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_khuzait_culture}Easterling</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>
</xsl:stylesheet>
