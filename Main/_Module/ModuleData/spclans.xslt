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
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_empire_north_1}Blaidd-luth</xsl:attribute>
			<xsl:attribute name="banner_key">11.24.4.4922.4922.764.764.1.0.0.529.5.0.152.152.872.873.0.0.0.419.5.0.510.510.764.764.0.0.0.526.5.0.152.152.656.873.0.0.0.527.5.0.152.152.651.654.0.0.0.528.5.0.152.152.877.654.0.0.0</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_empire_north_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_empire_north_2}Turch-luth</xsl:attribute>
			<xsl:attribute name="banner_key">11.24.4.4922.4922.764.764.1.0.0.142.5.0.502.502.764.676.0.0.1.142.5.0.502.502.764.852.0.0.179</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_empire_north_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_empire_north_3}Uch-luth</xsl:attribute>
			<xsl:attribute name="banner_key">11.24.4.4345.4345.764.764.1.0.0.302.5.0.294.294.802.604.0.0.327.302.5.0.294.294.726.604.0.0.32.202.5.0.366.366.854.864.0.0.47.202.5.0.371.382.674.864.0.1.316.320.5.0.315.315.764.715.0.0.0</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_empire_north_4']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'initial_home_settlement' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_empire_north_4}Arth-luth</xsl:attribute>
			<xsl:attribute name="banner_key">11.24.4.4345.4345.764.764.1.0.0.304.5.0.454.454.914.764.0.0.0.443.5.0.510.510.764.764.0.0.0.304.5.0.426.426.614.764.0.0.0</xsl:attribute>
			<xsl:attribute name="initial_home_settlement">Settlement.castle_EN6</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_empire_north_5']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'initial_home_settlement' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_empire_north_5}Cigfran-luth</xsl:attribute>
			<xsl:attribute name="banner_key">11.24.4.8690.8690.590.796.1.0.0.202.5.0.368.368.854.864.0.0.55.202.5.0.370.389.674.864.0.1.303.331.5.0.368.368.764.699.0.0.0</xsl:attribute>
			<xsl:attribute name="initial_home_settlement">Settlement.castle_EN5</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_empire_north_6']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'initial_home_settlement' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_empire_north_6}Hebog-luth</xsl:attribute>
			<xsl:attribute name="banner_key">11.24.4.4345.4345.764.764.1.0.0.502.5.0.487.487.764.764.0.0.0.300.5.0.447.447.764.764.0.0.0</xsl:attribute>
			<xsl:attribute name="initial_home_settlement">Settlement.castle_EN4</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_empire_north_7']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_empire_north_7}Draig-luth</xsl:attribute>
			<xsl:attribute name="banner_key">11.24.4.4345.4345.764.764.1.0.0.423.5.0.506.506.764.764.0.0.0.529.5.0.261.261.764.764.0.0.0</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_empire_north_8']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_empire_north_8}Caru-luth</xsl:attribute>
			<xsl:attribute name="banner_key">11.24.4.4345.4345.764.764.1.0.0.502.5.0.510.510.764.764.0.0.0.338.5.0.487.487.764.764.0.0.0.503.5.0.109.109.764.624.0.0.0</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_empire_north_9']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_empire_north_9}Avanc-luth</xsl:attribute>
			<xsl:attribute name="banner_key">11.24.4.8690.8690.764.764.1.0.0.401.5.0.287.287.764.764.0.0.15.401.5.0.145.145.764.764.0.0.29.503.5.0.50.48.764.764.0.0.15.423.5.0.428.438.764.764.0.0.0</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- ==================== GONDOR CLANS (Empire West) ==================== -->
	<xsl:template match="Faction[@id='clan_empire_west_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_empire_west_1}House of Húrinionath</xsl:attribute>
			<xsl:attribute name="banner_key">11.24.4.4922.4922.776.777.1.0.0.203.5.0.305.305.764.774.0.0.0.323.5.0.509.509.764.764.0.0.0</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_empire_west_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_empire_west_2}House of Imrazôrionath</xsl:attribute>
			<xsl:attribute name="banner_key">11.24.4.4922.4922.764.764.1.0.0.337.5.0.272.272.764.672.0.0.0.301.5.0.292.292.764.916.0.0.90.301.5.0.292.292.764.844.0.0.270</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_empire_west_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_empire_west_3}House of Eärnurionath</xsl:attribute>
			<xsl:attribute name="banner_key">11.24.4.4922.4922.764.764.1.0.0.103.5.0.454.454.764.764.0.0.0</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_empire_west_4']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_empire_west_4}House of Molorion</xsl:attribute>
			<xsl:attribute name="banner_key">11.24.4.4922.4922.764.764.1.0.0.201.5.0.454.454.914.764.0.0.180.201.5.0.454.454.764.764.0.0.0.201.5.0.454.454.614.764.0.0.180</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_empire_west_5']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_empire_west_5}House of Ausirionath</xsl:attribute>
			<xsl:attribute name="banner_key">11.24.4.4922.4922.776.777.1.0.0.418.5.0.285.285.764.764.0.0.0.500.5.0.454.454.764.764.0.0.0.500.5.0.380.380.764.762.0.0.0</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_empire_west_6']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_empire_west_6}House of Halboronionath</xsl:attribute>
			<xsl:attribute name="banner_key">11.24.4.4922.4922.764.764.1.0.0.336.5.0.454.454.764.764.0.1.180.340.5.0.171.171.764.564.0.0.0</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_empire_west_7']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_empire_west_7}House of Malandilionath</xsl:attribute>
			<xsl:attribute name="banner_key">11.24.4.4922.4922.764.764.1.0.0.423.5.0.454.454.764.764.0.0.1.340.5.0.300.300.764.754.0.0.0</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_empire_west_8']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_empire_west_8}House of Olindurionath</xsl:attribute>
			<xsl:attribute name="banner_key">11.24.4.4922.4922.764.764.1.0.0.419.5.0.454.454.764.764.0.0.0.335.5.0.143.143.873.642.0.0.325.335.5.0.143.143.655.642.0.0.42.335.5.0.143.143.651.882.0.0.134.335.5.0.143.143.877.882.0.0.214</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_empire_west_9']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_empire_west_9}House of Danuhirionath</xsl:attribute>
			<xsl:attribute name="banner_key">11.24.4.4922.4922.776.777.1.0.0.343.5.0.419.348.764.910.0.0.0.106.5.0.422.411.764.651.0.0.0.505.5.0.87.47.764.805.0.0.0</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- ==================== MORDOR CLANS (Empire South) ==================== -->
	<xsl:template match="Faction[@id='clan_empire_south_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_empire_south_1}Dulguzagar</xsl:attribute>
			<xsl:attribute name="banner_key">11.24.4.4922.4922.764.764.1.0.0.200.5.0.237.237.683.905.0.0.0.200.5.0.247.237.844.906.0.1.0.326.5.0.215.206.764.714.0.1.0</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_empire_south_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_empire_south_2}Arnediadgae</xsl:attribute>
			<xsl:attribute name="banner_key">11.24.4.4922.4922.764.764.1.0.0.214.5.0.463.463.764.764.0.0.0</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_empire_south_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_empire_south_3}Môrgul-Log</xsl:attribute>
			<xsl:attribute name="banner_key">11.24.4.4922.4922.764.764.1.0.0.108.5.0.485.485.764.644.0.0.0.325.5.0.454.454.764.844.0.0.0</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_empire_south_4']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_empire_south_4}Yí-Mûmakan</xsl:attribute>
			<xsl:attribute name="banner_key">11.24.4.4922.4922.764.764.1.0.0.418.5.0.147.147.604.882.0.0.0.107.5.0.454.454.764.764.0.0.0.418.5.0.147.147.924.882.0.0.0</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_empire_south_5']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'initial_home_settlement' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_empire_south_5}Al-Khey-Sârt</xsl:attribute>
			<xsl:attribute name="banner_key">11.24.4.4922.4922.764.764.1.0.0.119.5.0.344.344.764.658.0.0.0.217.5.0.303.303.764.924.0.0.0.510.5.0.243.505.774.787.0.0.0</xsl:attribute>
			<xsl:attribute name="initial_home_settlement">Settlement.castle_ES2</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_empire_south_6']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_empire_south_6}Ôvathanid</xsl:attribute>
			<xsl:attribute name="banner_key">11.24.4.4922.4922.764.764.1.0.0.304.5.0.366.366.763.844.0.0.0.423.5.0.420.420.763.764.0.1.0</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_empire_south_7']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_empire_south_7}Abrakhân</xsl:attribute>
			<xsl:attribute name="banner_key">11.24.4.4922.4922.764.764.1.0.0.510.5.0.507.55.764.1025.0.0.0.459.5.0.507.507.764.764.0.0.0.510.5.0.507.171.764.997.0.0.0.510.5.0.507.171.764.532.0.0.0.510.5.0.507.55.764.501.0.0.0</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_empire_south_8']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_empire_south_8}Khôrahîm</xsl:attribute>
			<xsl:attribute name="banner_key">11.24.4.4922.4922.764.764.1.0.0.147.5.0.454.454.764.765.0.0.0</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_empire_south_9']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_empire_south_9}Waw</xsl:attribute>
			<xsl:attribute name="banner_key">11.24.4.4922.4922.764.764.1.0.0.505.5.0.460.50.764.764.0.0.0.459.5.0.150.150.660.658.0.0.0.459.5.0.150.150.871.658.0.0.0.459.5.0.150.150.871.870.0.0.0.459.5.0.150.150.660.870.0.0.0.505.5.0.460.50.765.764.0.0.10</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- ==================== DALE CLANS (Sturgia) ==================== -->
	<xsl:template match="Faction[@id='clan_sturgia_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_sturgia_1}House of Girion</xsl:attribute>
			<xsl:attribute name="banner_key">11.167.12.4922.4922.764.764.1.0.0.333.111.0.454.454.764.764.0.0.0</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_sturgia_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_sturgia_2}House of Bard</xsl:attribute>
			<xsl:attribute name="banner_key">11.12.167.4922.4922.764.764.1.0.0.420.111.0.586.586.764.764.0.0.180.459.111.171.215.215.767.762.0.0.1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_sturgia_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_sturgia_3}House of Brand</xsl:attribute>
			<xsl:attribute name="banner_key">11.167.12.4922.4922.764.764.1.0.0.347.111.0.325.325.764.685.0.0.0.302.111.0.408.408.669.890.0.0.60.302.111.0.408.408.859.890.0.0.300</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_sturgia_4']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_sturgia_4}House of Bain</xsl:attribute>
			<xsl:attribute name="banner_key">11.12.167.4922.4922.733.752.1.0.0.307.111.0.488.488.713.806.0.0.29.307.111.0.499.499.827.796.0.1.340</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_sturgia_5']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'initial_home_settlement' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_sturgia_5}House of Esgaroth</xsl:attribute>
			<xsl:attribute name="banner_key">11.167.12.4922.4922.764.764.1.0.0.456.111.0.443.443.764.728.0.0.359.444.111.0.102.102.764.972.0.0.179.454.111.0.102.102.868.972.0.0.179.454.111.0.102.102.659.972.0.0.359</xsl:attribute>
			<xsl:attribute name="initial_home_settlement">Settlement.castle_S7</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_sturgia_6']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'initial_home_settlement' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_sturgia_6}House of the River</xsl:attribute>
			<xsl:attribute name="banner_key">11.167.12.4922.4922.764.764.1.0.0.441.111.0.454.454.764.764.0.0.359</xsl:attribute>
			<xsl:attribute name="initial_home_settlement">Settlement.castle_S6</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_sturgia_7']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_sturgia_7}House of the Archers</xsl:attribute>
			<xsl:attribute name="banner_key">11.12.167.4922.4922.764.764.1.0.11.421.111.0.518.518.761.795.0.1.90.419.111.171.141.141.764.764.0.0.0</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_sturgia_8']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_sturgia_8}House of Long Lake</xsl:attribute>
			<xsl:attribute name="banner_key">11.167.12.4922.4922.764.764.1.0.0.138.111.0.509.509.764.764.0.0.359</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_sturgia_9']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_sturgia_9}House of the Dragon-slayer</xsl:attribute>
			<xsl:attribute name="banner_key">11.167.12.4922.4922.764.764.1.0.0.142.29.149.331.331.764.665.0.0.0.146.29.149.311.311.764.863.0.1.0</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- ==================== HARAD CLANS (Aserai) ==================== -->
	<xsl:template match="Faction[@id='clan_aserai_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_aserai_1}Tribe of the Beljahar</xsl:attribute>
			<xsl:attribute name="banner_key">11.156.75.4345.4345.764.764.1.0.0.212.1.0.454.454.764.764.0.0.359</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_aserai_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_aserai_2}Tribe of the Tasharûn</xsl:attribute>
			<xsl:attribute name="banner_key">11.156.75.4345.4345.764.764.1.0.0.223.1.0.454.454.764.764.0.0.359.515.1.0.177.177.764.764.0.0.0.500.1.0.358.358.764.764.0.0.0</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_aserai_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_aserai_3}Tribe of the Dânakar</xsl:attribute>
			<xsl:attribute name="banner_key">11.156.75.4345.4345.764.764.1.0.0.446.1.0.381.381.764.764.0.0.359.309.1.0.325.325.627.764.0.0.320.309.1.0.323.323.901.764.0.0.146</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_aserai_4']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_aserai_4}Tribe of the Nafârrat</xsl:attribute>
			<xsl:attribute name="banner_key">11.156.75.4345.4345.764.764.1.0.0.427.1.0.415.388.695.764.0.0.270.521.1.0.293.298.822.764.0.0.324</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_aserai_5']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_aserai_5}Tribe of the An-Maresh</xsl:attribute>
			<xsl:attribute name="banner_key">11.156.75.4345.4345.764.764.1.0.0.425.1.0.416.416.764.782.0.0.359.400.1.0.120.120.764.937.0.0.0.411.1.0.120.120.764.614.0.0.0</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_aserai_6']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_aserai_6}Tribe of the Zahîrun</xsl:attribute>
			<xsl:attribute name="banner_key">11.156.75.4345.4345.764.764.1.0.0.222.1.0.454.454.764.764.0.0.359.411.1.0.220.220.764.764.0.0.0</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_aserai_7']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_aserai_7}Tribe of the Kharâim</xsl:attribute>
			<xsl:attribute name="banner_key">11.156.75.4345.4345.764.764.1.0.0.426.1.0.397.397.764.688.0.0.270.208.1.0.332.332.764.873.0.0.0</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_aserai_8']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_aserai_8}Tribe of the Arjarar</xsl:attribute>
			<xsl:attribute name="banner_key">11.156.75.4345.4345.764.764.1.0.0.428.1.0.454.454.764.764.0.0.359.321.1.0.354.354.764.721.0.0.359</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_aserai_9']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_aserai_9}Tribe of the Pezarjin</xsl:attribute>
			<xsl:attribute name="banner_key">11.156.75.4345.4345.776.777.1.0.0.427.1.0.415.388.764.811.0.0.0.436.1.0.359.366.764.706.0.0.0</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- ==================== ROHAN CLANS (Vlandia) ==================== -->
	<xsl:template match="Faction[@id='clan_vlandia_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_vlandia_1}House of Eorling</xsl:attribute>
			<xsl:attribute name="banner_key">11.184.14.4922.4922.764.764.1.0.0.346.15.0.208.208.764.764.0.0.0.424.15.0.213.213.585.764.0.0.0.424.15.0.213.213.943.764.0.0.0</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_vlandia_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_vlandia_2}House of Cerdicing</xsl:attribute>
			<xsl:attribute name="banner_key">11.184.14.4922.4922.764.764.1.0.0.419.15.0.454.454.764.764.0.0.0.512.15.0.125.125.653.887.0.0.0.512.15.0.125.125.875.641.0.0.0.512.15.0.125.125.869.887.0.0.0.512.15.0.125.125.653.641.0.0.0</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_vlandia_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_vlandia_3}House of Grimingas</xsl:attribute>
			<xsl:attribute name="banner_key">11.184.14.4922.4922.764.764.1.0.0.424.15.0.377.377.764.764.0.0.0.220.15.0.138.132.884.939.0.0.0.220.15.0.138.138.644.939.0.0.0</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_vlandia_4']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_vlandia_4}House of Felánding</xsl:attribute>
			<xsl:attribute name="banner_key">11.184.14.4922.4922.764.764.1.0.0.207.15.0.267.238.863.918.0.1.332.207.15.0.238.238.651.918.0.0.28.220.15.0.408.408.757.646.0.0.0.512.15.0.111.111.757.879.0.0.1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_vlandia_5']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_vlandia_5}House of Oscyteling</xsl:attribute>
			<xsl:attribute name="banner_key">11.184.14.4922.4922.764.764.1.0.0.159.15.0.452.452.764.644.0.0.0.418.15.0.270.270.764.864.0.0.0</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_vlandia_6']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'initial_home_settlement' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_vlandia_6}House of Ordlacing</xsl:attribute>
			<xsl:attribute name="banner_key">11.184.14.4922.4922.764.764.1.0.0.336.15.0.382.382.763.764.0.0.0.424.15.0.183.183.903.774.0.0.0.424.15.0.183.183.624.774.0.0.0.424.15.0.183.183.763.904.0.0.0</xsl:attribute>
			<xsl:attribute name="initial_home_settlement">Settlement.castle_V6</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_vlandia_7']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_vlandia_7}House of Æthellafing</xsl:attribute>
			<xsl:attribute name="banner_key">11.184.14.4922.4922.764.764.1.0.0.141.15.0.454.454.764.764.0.0.0</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_vlandia_8']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'initial_home_settlement' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_vlandia_8}House of Grimmóding</xsl:attribute>
			<xsl:attribute name="banner_key">11.184.14.4922.4922.764.764.1.0.0.315.15.0.462.462.790.771.0.0.311.315.15.0.462.462.740.758.0.0.49.424.15.0.144.144.753.650.0.0.0</xsl:attribute>
			<xsl:attribute name="initial_home_settlement">Settlement.castle_V3</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_vlandia_9']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_vlandia_9}House of Dúning</xsl:attribute>
			<xsl:attribute name="banner_key">11.184.14.4922.4922.764.764.1.0.0.335.15.0.499.499.764.764.0.0.0.336.15.0.186.186.764.744.0.0.0</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_vlandia_10']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_vlandia_10}House of Eoforing</xsl:attribute>
			<xsl:attribute name="banner_key">11.184.14.4922.4922.764.764.1.0.0.305.15.0.444.444.764.764.0.0.0.206.15.0.480.480.658.764.0.0.17.206.15.0.480.480.870.764.0.0.194</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_vlandia_11']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_vlandia_11}House of Tordaging</xsl:attribute>
			<xsl:attribute name="banner_key">11.184.14.4922.4922.764.764.1.0.0.505.15.0.60.60.583.911.0.0.134.505.15.0.60.60.668.909.0.0.134.505.15.0.60.60.666.827.0.0.134.505.15.0.60.60.751.827.0.0.134.505.15.0.60.60.751.744.0.0.134.505.15.0.60.60.833.745.0.0.134.505.15.0.60.60.833.663.0.0.134.505.15.0.60.60.916.663.0.0.134.505.15.0.60.60.916.580.0.0.134.102.15.0.188.188.638.664.0.0.0.102.15.0.188.188.894.898.0.1.0</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- ==================== KHAND CLANS (Battania) ==================== -->
	<xsl:template match="Faction[@id='clan_battania_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'initial_home_settlement' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_battania_1}Vângulis</xsl:attribute>
			<xsl:attribute name="banner_key">11.23.75.4345.4345.764.764.1.0.0.456.39.0.382.382.764.834.0.0.180.413.39.0.224.224.764.614.0.0.0</xsl:attribute>
			<xsl:attribute name="initial_home_settlement">Settlement.town_K1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_battania_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'initial_home_settlement' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_battania_2}Sûrket</xsl:attribute>
			<xsl:attribute name="banner_key">11.23.75.4345.4345.764.764.1.0.0.412.39.0.454.454.764.764.0.0.180</xsl:attribute>
			<xsl:attribute name="initial_home_settlement">Settlement.town_K2</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_battania_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'initial_home_settlement' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_battania_3}Orazân</xsl:attribute>
			<xsl:attribute name="banner_key">11.23.75.4345.4345.764.764.1.0.0.427.39.0.261.261.764.869.0.0.359.133.39.0.380.380.764.696.0.0.0</xsl:attribute>
			<xsl:attribute name="initial_home_settlement">Settlement.town_K3</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_battania_4']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'initial_home_settlement' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_battania_4}Khârnamud</xsl:attribute>
			<xsl:attribute name="banner_key">11.23.75.4345.4345.764.764.1.0.0.132.39.0.438.438.764.814.0.0.0.302.39.0.438.438.764.714.0.0.0</xsl:attribute>
			<xsl:attribute name="initial_home_settlement">Settlement.town_K4</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_battania_5']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'initial_home_settlement' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_battania_5}Delmuran</xsl:attribute>
			<xsl:attribute name="banner_key">11.23.75.4345.4345.764.764.1.0.0.535.39.0.189.178.814.920.0.1.0.535.39.0.178.178.692.919.0.0.0.151.39.0.425.425.764.732.0.0.0</xsl:attribute>
			<xsl:attribute name="initial_home_settlement">Settlement.castle_K1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_battania_6']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'initial_home_settlement' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_battania_6}Baqtâr</xsl:attribute>
			<xsl:attribute name="banner_key">11.23.75.4345.4345.764.764.1.0.0.504.39.0.90.90.764.942.0.0.180.503.39.0.84.84.764.760.0.0.0.406.39.0.455.455.764.764.0.0.0.504.39.0.90.90.764.587.0.0.0</xsl:attribute>
			<xsl:attribute name="initial_home_settlement">Settlement.castle_K3</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_battania_7']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'initial_home_settlement' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_battania_7}Tûrmak</xsl:attribute>
			<xsl:attribute name="banner_key">11.23.75.4345.4345.764.764.1.0.0.504.39.96.80.80.764.978.1.0.180.407.39.0.368.368.764.757.0.0.0.504.39.96.110.110.764.535.1.0.180</xsl:attribute>
			<xsl:attribute name="initial_home_settlement">Settlement.castle_K2</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_battania_8']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'initial_home_settlement' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_battania_8}Eshtârul</xsl:attribute>
			<xsl:attribute name="banner_key">11.23.75.4345.4345.764.764.1.0.0.415.39.0.391.391.764.764.0.0.0</xsl:attribute>
			<xsl:attribute name="initial_home_settlement">Settlement.castle_K4</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- ==================== PLAYER FACTION ==================== -->
	<xsl:template match="Faction[@id='player_faction']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'initial_home_settlement']"/>
			<xsl:attribute name="initial_home_settlement">Settlement.town_K1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- ==================== MINOR FACTIONS (settlement remaps) ==================== -->
	<xsl:template match="Faction[@id='skolderbrotva']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'initial_home_settlement']"/>
			<xsl:attribute name="initial_home_settlement">Settlement.castle_S1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='company_of_the_boar']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'initial_home_settlement']"/>
			<xsl:attribute name="initial_home_settlement">Settlement.castle_V3</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='wolfskins']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'initial_home_settlement']"/>
			<xsl:attribute name="initial_home_settlement">Settlement.castle_K5</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='embers_of_flame']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'initial_home_settlement']"/>
			<xsl:attribute name="initial_home_settlement">Settlement.castle_EN5</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- ==================== RHUN CLANS (Khuzait) ==================== -->
	<xsl:template match="Faction[@id='clan_khuzait_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_khuzait_1}Zhamian</xsl:attribute>
			<xsl:attribute name="banner_key">11.25.10.4345.4345.764.764.1.0.0.449.11.0.327.349.764.649.0.0.0.449.11.0.319.339.764.879.0.0.180.510.11.0.351.240.634.764.0.0.180.510.11.0.351.240.894.764.0.0.180</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_khuzait_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_khuzait_2}Salurian</xsl:attribute>
			<xsl:attribute name="banner_key">11.25.10.4345.4345.764.764.1.0.0.128.11.0.454.454.764.764.0.0.0</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_khuzait_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_khuzait_3}Nikathian</xsl:attribute>
			<xsl:attribute name="banner_key">11.25.10.4345.4345.764.764.1.0.0.446.11.0.438.438.764.764.0.0.269.503.11.0.164.164.754.767.0.0.315.509.11.0.133.133.767.915.0.0.315.503.11.0.95.95.948.760.0.0.315.503.11.0.95.95.581.763.0.0.315.509.11.0.133.133.762.613.0.0.315</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_khuzait_4']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_khuzait_4}Karmian</xsl:attribute>
			<xsl:attribute name="banner_key">11.25.10.4345.4345.780.802.1.0.0.503.11.0.164.164.904.913.0.0.0.314.11.0.508.508.764.764.0.0.0.503.11.0.164.164.624.913.0.0.0</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_khuzait_5']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'initial_home_settlement' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_khuzait_5}Amdûrid</xsl:attribute>
			<xsl:attribute name="banner_key">11.25.10.4345.4345.780.802.1.0.0.427.11.0.401.401.764.822.0.0.359.503.11.0.193.193.764.781.0.0.358.218.11.0.222.222.764.612.0.0.0</xsl:attribute>
			<xsl:attribute name="initial_home_settlement">Settlement.castle_K6</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_khuzait_6']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_khuzait_6}Khundolar</xsl:attribute>
			<xsl:attribute name="banner_key">11.25.10.4345.4345.764.764.1.0.0.218.11.0.194.194.807.631.0.0.359.150.11.0.444.444.764.775.0.0.0</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_khuzait_7']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_khuzait_7}Kuzaithian</xsl:attribute>
			<xsl:attribute name="banner_key">11.25.10.4345.4345.764.764.1.0.0.117.11.0.346.346.841.838.0.0.179.508.11.0.110.110.638.876.0.0.0.117.11.0.342.342.692.687.0.0.0.508.11.0.108.108.890.648.0.0.0</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_khuzait_8']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_khuzait_8}Mashakian</xsl:attribute>
			<xsl:attribute name="banner_key">11.25.10.4345.4345.764.764.1.0.0.427.11.0.236.236.726.768.0.0.273.427.11.0.236.236.802.760.0.0.93.503.11.0.80.80.764.764.0.0.0.510.11.0.236.165.869.644.0.0.0.510.11.0.236.165.869.884.0.0.0.510.11.0.236.165.656.884.0.0.0.510.11.0.236.165.656.644.0.0.0</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Faction[@id='clan_khuzait_9']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'initial_home_settlement' and local-name() != 'banner_key']"/>
			<xsl:attribute name="name">{=TAOM_clan_khuzait_9}Bozorganith</xsl:attribute>
			<xsl:attribute name="banner_key">11.25.10.4345.4345.764.764.1.0.0.427.11.0.206.176.764.667.0.0.0.503.11.0.92.87.764.642.0.0.0.218.11.0.109.90.764.572.0.0.0.510.11.0.250.300.641.862.0.0.90.504.11.0.155.92.764.769.0.0.180.510.11.0.249.300.887.862.0.0.90.504.11.0.155.92.764.957.0.0.180.503.11.0.92.87.764.859.0.0.0.510.11.0.136.131.764.814.0.0.0.510.11.0.136.131.764.907.0.0.0</xsl:attribute>
			<xsl:attribute name="initial_home_settlement">Settlement.castle_K7</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

</xsl:stylesheet>
