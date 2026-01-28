<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
	<!-- Identity transformation - copies everything by default -->
	<xsl:output omit-xml-declaration="no" indent="yes"/>

	<xsl:template match="@*|node()">
		<xsl:copy>
			<xsl:apply-templates select="@*|node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- ==================== DUNLAND CLANS (Empire North) ==================== -->
	<xsl:template match="Faction[@id='clan_empire_north_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_empire_north_1}Blaidd-luth</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_empire_north_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_empire_north_2}Turch-luth</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_empire_north_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_empire_north_3}Uch-luth</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_empire_north_4']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_empire_north_4}Arth-luth</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_empire_north_5']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_empire_north_5}Cigfran-luth</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_empire_north_6']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_empire_north_6}Hebog-luth</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_empire_north_7']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_empire_north_7}Draig-luth</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_empire_north_8']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_empire_north_8}Caru-luth</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_empire_north_9']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_empire_north_9}Avanc-luth</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- ==================== GONDOR CLANS (Empire West) ==================== -->
	<xsl:template match="Faction[@id='clan_empire_west_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_empire_west_1}House of Húrinionath</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_empire_west_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_empire_west_2}House of Imrazôrionath</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_empire_west_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_empire_west_3}House of Eärnurionath</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_empire_west_4']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_empire_west_4}House of Molorion</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_empire_west_5']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_empire_west_5}House of Ausirionath</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_empire_west_6']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_empire_west_6}House of Halboronionath</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_empire_west_7']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_empire_west_7}House of Malandilionath</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_empire_west_8']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_empire_west_8}House of Olindurionath</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_empire_west_9']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_empire_west_9}House of Danuhirionath</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- ==================== MORDOR CLANS (Empire South) ==================== -->
	<xsl:template match="Faction[@id='clan_empire_south_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_empire_south_1}Dulguzagar</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_empire_south_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_empire_south_2}Arnediadgae</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_empire_south_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_empire_south_3}Môrgul-Log</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_empire_south_4']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_empire_south_4}Yí-Mûmakan</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_empire_south_5']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_empire_south_5}Al-Khey-Sârt</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_empire_south_6']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_empire_south_6}Ôvathanid</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_empire_south_7']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_empire_south_7}Abrakhân</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_empire_south_8']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_empire_south_8}Khôrahîm</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_empire_south_9']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_empire_south_9}Waw</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- ==================== DALE CLANS (Sturgia) ==================== -->
	<xsl:template match="Faction[@id='clan_sturgia_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_sturgia_1}House of Girion</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_sturgia_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_sturgia_2}House of Bard</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_sturgia_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_sturgia_3}House of Brand</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_sturgia_4']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_sturgia_4}House of Bain</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_sturgia_5']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_sturgia_5}House of Esgaroth</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_sturgia_6']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_sturgia_6}House of the River</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_sturgia_7']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_sturgia_7}House of the Archers</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_sturgia_8']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_sturgia_8}House of Long Lake</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_sturgia_9']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_sturgia_9}House of the Dragon-slayer</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- ==================== HARAD CLANS (Aserai) ==================== -->
	<xsl:template match="Faction[@id='clan_aserai_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_aserai_1}Tribe of the Beljahar</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_aserai_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_aserai_2}Tribe of the Tasharûn</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_aserai_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_aserai_3}Tribe of the Dânakar</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_aserai_4']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_aserai_4}Tribe of the Nafârrat</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_aserai_5']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_aserai_5}Tribe of the An-Maresh</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_aserai_6']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_aserai_6}Tribe of the Zahîrun</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_aserai_7']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_aserai_7}Tribe of the Kharâim</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_aserai_8']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_aserai_8}Tribe of the Arjarar</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_aserai_9']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_aserai_9}Tribe of the Pezarjin</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- ==================== ROHAN CLANS (Vlandia) ==================== -->
	<xsl:template match="Faction[@id='clan_vlandia_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_vlandia_1}House of Eorling</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_vlandia_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_vlandia_2}House of Cerdicing</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_vlandia_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_vlandia_3}House of Grimingas</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_vlandia_4']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_vlandia_4}House of Felánding</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_vlandia_5']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_vlandia_5}House of Oscyteling</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_vlandia_6']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_vlandia_6}House of Ordlacing</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_vlandia_7']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_vlandia_7}House of Æthellafing</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_vlandia_8']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_vlandia_8}House of Grimmóding</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_vlandia_9']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_vlandia_9}House of Dúning</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_vlandia_10']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_vlandia_10}House of Eoforing</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_vlandia_11']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_vlandia_11}House of Tordaging</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- ==================== KHAND CLANS (Battania) ==================== -->
	<xsl:template match="Faction[@id='clan_battania_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_battania_1}Vângulis</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_battania_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_battania_2}Sûrket</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_battania_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_battania_3}Orazân</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_battania_4']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_battania_4}Khârnamud</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_battania_5']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_battania_5}Delmuran</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_battania_6']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_battania_6}Baqtâr</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_battania_7']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_battania_7}Tûrmak</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_battania_8']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_battania_8}Eshtârul</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- ==================== RHUN CLANS (Khuzait) ==================== -->
	<xsl:template match="Faction[@id='clan_khuzait_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_khuzait_1}Zhamian</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_khuzait_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_khuzait_2}Salurian</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_khuzait_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_khuzait_3}Nikathian</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_khuzait_4']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_khuzait_4}Karmian</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_khuzait_5']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_khuzait_5}Amdûrid</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_khuzait_6']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_khuzait_6}Khundolar</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_khuzait_7']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_khuzait_7}Kuzaithian</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_khuzait_8']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_khuzait_8}Mashakian</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_khuzait_9']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_clan_khuzait_9}Bozorganith</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

</xsl:stylesheet>
