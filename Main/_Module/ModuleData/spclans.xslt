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
      <xsl:attribute name="banner_key">11.181.166.1528.1528.764.764.1.0.0.26004.2000.171.650.650.765.800.0.0.0</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="Faction[@id='clan_empire_north_2']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name']"/>
      <xsl:attribute name="name">{=TAOM_clan_empire_north_2}Turch-luth</xsl:attribute>
      <xsl:attribute name="banner_key">11.181.166.1528.1528.764.764.1.0.0.26004.2000.171.650.650.765.800.0.0.0</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="Faction[@id='clan_empire_north_3']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name']"/>
      <xsl:attribute name="name">{=TAOM_clan_empire_north_3}Uch-luth</xsl:attribute>
      <xsl:attribute name="banner_key">11.181.166.1528.1528.764.764.1.0.0.26004.2000.171.650.650.765.800.0.0.0</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="Faction[@id='clan_empire_north_4']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'initial_home_settlement']"/>
      <xsl:attribute name="name">{=TAOM_clan_empire_north_4}Arth-luth</xsl:attribute>
      <xsl:attribute name="initial_home_settlement">Settlement.castle_EN6</xsl:attribute>
      <xsl:attribute name="banner_key">11.181.166.1528.1528.764.764.1.0.0.26004.2000.171.650.650.765.800.0.0.0</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="Faction[@id='clan_empire_north_5']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'initial_home_settlement']"/>
      <xsl:attribute name="name">{=TAOM_clan_empire_north_5}Cigfran-luth</xsl:attribute>
      <xsl:attribute name="initial_home_settlement">Settlement.castle_EN5</xsl:attribute>
      <xsl:attribute name="banner_key">11.181.166.1528.1528.764.764.1.0.0.26004.2000.171.650.650.765.800.0.0.0</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="Faction[@id='clan_empire_north_6']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'initial_home_settlement']"/>
      <xsl:attribute name="name">{=TAOM_clan_empire_north_6}Hebog-luth</xsl:attribute>
      <xsl:attribute name="initial_home_settlement">Settlement.castle_EN4</xsl:attribute>
      <xsl:attribute name="banner_key">11.181.166.1528.1528.764.764.1.0.0.26004.2000.171.650.650.765.800.0.0.0</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="Faction[@id='clan_empire_north_7']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name']"/>
      <xsl:attribute name="name">{=TAOM_clan_empire_north_7}Draig-luth</xsl:attribute>
      <xsl:attribute name="banner_key">11.181.166.1528.1528.764.764.1.0.0.26004.2000.171.650.650.765.800.0.0.0</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="Faction[@id='clan_empire_north_8']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name']"/>
      <xsl:attribute name="name">{=TAOM_clan_empire_north_8}Caru-luth</xsl:attribute>
      <xsl:attribute name="banner_key">11.181.166.1528.1528.764.764.1.0.0.26004.2000.171.650.650.765.800.0.0.0</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="Faction[@id='clan_empire_north_9']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name']"/>
      <xsl:attribute name="name">{=TAOM_clan_empire_north_9}Avanc-luth</xsl:attribute>
      <xsl:attribute name="banner_key">11.181.166.1528.1528.764.764.1.0.0.26004.2000.171.650.650.765.800.0.0.0</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <!-- ==================== GONDOR CLANS (Empire West) ==================== -->
  <xsl:template match="Faction[@id='clan_empire_west_1']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'culture']"/>
      <xsl:attribute name="name">{=TAOM_clan_empire_west_1}House of Húrinionath</xsl:attribute>
      <xsl:attribute name="culture">Culture.gondor</xsl:attribute>
      <xsl:attribute name="banner_key">11.149.149.1528.1528.764.764.1.0.0.10000.172.2000.580.580.765.825.0.0.0</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="Faction[@id='clan_empire_west_2']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'culture']"/>
      <xsl:attribute name="name">{=TAOM_clan_empire_west_2}House of Imrazôrionath</xsl:attribute>
      <xsl:attribute name="culture">Culture.gondor</xsl:attribute>
      <xsl:attribute name="banner_key">11.202.149.1528.1528.764.732.1.0.0.10015.172.2000.600.600.764.726.0.0.0</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="Faction[@id='clan_empire_west_3']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'culture']"/>
      <xsl:attribute name="name">{=TAOM_clan_empire_west_3}House of Eärnurionath</xsl:attribute>
      <xsl:attribute name="culture">Culture.gondor</xsl:attribute>
      <xsl:attribute name="banner_key">11.292.149.1528.1528.764.732.1.0.0.10022.172.2000.600.600.764.760.0.0.0</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="Faction[@id='clan_empire_west_4']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'culture']"/>
      <xsl:attribute name="name">{=TAOM_clan_empire_west_4}House of Barahirionath</xsl:attribute>
      <xsl:attribute name="culture">Culture.gondor</xsl:attribute>
      <xsl:attribute name="banner_key">11.240.149.1528.1528.764.732.1.0.0.10019.172.2000.700.700.764.760.0.0.0</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="Faction[@id='clan_empire_west_5']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'culture']"/>
      <xsl:attribute name="name">{=TAOM_clan_empire_west_5}House of Ausirionath</xsl:attribute>
      <xsl:attribute name="culture">Culture.gondor</xsl:attribute>
      <xsl:attribute name="banner_key">11.2.149.1528.1528.764.732.1.0.0.10024.172.2000.650.650.764.760.0.0.0</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="Faction[@id='clan_empire_west_6']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'culture']"/>
      <xsl:attribute name="name">{=TAOM_clan_empire_west_6}House of Halboronionath</xsl:attribute>
      <xsl:attribute name="culture">Culture.gondor</xsl:attribute>
      <xsl:attribute name="banner_key">11.126.149.1528.1528.764.732.1.0.0.10026.172.2000.700.700.764.820.0.0.0</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="Faction[@id='clan_empire_west_7']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'culture']"/>
      <xsl:attribute name="name">{=TAOM_clan_empire_west_7}House of Malandilionath</xsl:attribute>
      <xsl:attribute name="culture">Culture.gondor</xsl:attribute>
      <xsl:attribute name="banner_key">11.161.166.1528.1528.764.764.1.0.0.10027.172.171.650.600.765.760.0.0.0</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="Faction[@id='clan_empire_west_8']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'culture']"/>
      <xsl:attribute name="name">{=TAOM_clan_empire_west_8}House of Olindurionath</xsl:attribute>
      <xsl:attribute name="culture">Culture.gondor</xsl:attribute>
      <xsl:attribute name="banner_key">11.242.262.1528.1528.764.764.1.0.0.10011.35.171.600.600.760.775.0.0.0</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="Faction[@id='clan_empire_west_9']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'culture']"/>
      <xsl:attribute name="name">{=TAOM_clan_empire_west_9}House of Danuhirionath</xsl:attribute>
      <xsl:attribute name="culture">Culture.gondor</xsl:attribute>
      <xsl:attribute name="banner_key">11.172.149.1528.1528.764.732.1.0.0.503.2000.2000.377.377.764.760.0.0.0.503.172.2000.342.342.764.760.0.0.0.503.2000.2000.300.300.764.760.0.0.0</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <!-- ==================== MORDOR CLANS (Empire South) ==================== -->
  <xsl:template match="Faction[@id='clan_empire_south_1']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name']"/>
      <xsl:attribute name="name">{=TAOM_clan_empire_south_1}Dôlgubêth</xsl:attribute>
      <xsl:attribute name="banner_key">11.328.166.1528.1528.764.764.1.0.0.19012.149.116.700.700.755.705.1.0.0</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="Faction[@id='clan_empire_south_2']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name']"/>
      <xsl:attribute name="name">{=TAOM_clan_empire_south_2}Goearnediad</xsl:attribute>
      <xsl:attribute name="banner_key">11.116.166.1528.1528.764.764.1.0.0.19002.31.116.500.500.674.751.1.0.0.427.31.116.654.579.816.750.0.0.90</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="Faction[@id='clan_empire_south_3']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name']"/>
      <xsl:attribute name="name">{=TAOM_clan_empire_south_3}Melkondili</xsl:attribute>
      <xsl:attribute name="banner_key">11.2000.166.1528.1528.764.764.1.0.0.19015.142.116.800.800.759.755.1.0.0</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="Faction[@id='clan_empire_south_4']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name']"/>
      <xsl:attribute name="name">{=TAOM_clan_empire_south_4}Môrgukar</xsl:attribute>
      <xsl:attribute name="banner_key">11.211.166.1528.1528.764.764.1.0.0.19011.2000.171.900.900.756.818.0.0.0</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="Faction[@id='clan_empire_south_5']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'initial_home_settlement']"/>
      <xsl:attribute name="name">{=TAOM_clan_empire_south_5}Bolgrûkig</xsl:attribute>
      <xsl:attribute name="initial_home_settlement">Settlement.castle_ES2</xsl:attribute>
      <xsl:attribute name="banner_key">11.149.166.1528.1528.764.764.1.0.0.18000.172.171.650.600.764.743.0.0.10</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="Faction[@id='clan_empire_south_6']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name']"/>
      <xsl:attribute name="name">{=TAOM_clan_empire_south_6}Kâm Lagúrz</xsl:attribute>
      <xsl:attribute name="banner_key">11.96.166.1528.1528.764.764.1.0.0.19008.191.171.1000.1000.764.824.0.0.0</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="Faction[@id='clan_empire_south_7']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name']"/>
      <xsl:attribute name="name">{=TAOM_clan_empire_south_7}Bragzûkish</xsl:attribute>
      <xsl:attribute name="banner_key">11.149.166.1528.1528.764.764.1.0.0.19003.172.171.650.600.764.743.0.0.0</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="Faction[@id='clan_empire_south_8']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name']"/>
      <xsl:attribute name="name">{=TAOM_clan_empire_south_8}Khôrahîm</xsl:attribute>
      <xsl:attribute name="banner_key">11.2001.2001.1528.1528.764.764.1.0.0.19015.2002.171.700.700.764.764.0.0.0</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="Faction[@id='clan_empire_south_9']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name']"/>
      <xsl:attribute name="name">{=TAOM_clan_empire_south_9}Wâwrim</xsl:attribute>
      <xsl:attribute name="owner">Hero.lord_1_48_3</xsl:attribute>
      <xsl:attribute name="banner_key">11.2001.2001.1528.1528.764.764.1.0.0.19015.2002.171.700.700.764.764.0.0.0</xsl:attribute>
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
      <xsl:attribute name="banner_key">11.312.166.1528.1528.764.764.1.0.0.27000.238.171.900.900.765.790.0.0.0</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="Faction[@id='clan_sturgia_3']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name']"/>
      <xsl:attribute name="name">{=TAOM_clan_sturgia_3}House of Brand</xsl:attribute>
      <xsl:attribute name="banner_key">11.312.166.1528.1528.764.764.1.0.0.27000.238.171.900.900.765.790.0.0.0</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="Faction[@id='clan_sturgia_4']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name']"/>
      <xsl:attribute name="name">{=TAOM_clan_sturgia_4}House of Bain</xsl:attribute>
      <xsl:attribute name="banner_key">11.312.166.1528.1528.764.764.1.0.0.27000.238.171.900.900.765.790.0.0.0</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="Faction[@id='clan_sturgia_5']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'initial_home_settlement']"/>
      <xsl:attribute name="name">{=TAOM_clan_sturgia_5}House of Esgaroth</xsl:attribute>
      <xsl:attribute name="initial_home_settlement">Settlement.castle_S7</xsl:attribute>
      <xsl:attribute name="banner_key">11.312.166.1528.1528.764.764.1.0.0.27000.238.171.900.900.765.790.0.0.0</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="Faction[@id='clan_sturgia_6']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'initial_home_settlement']"/>
      <xsl:attribute name="name">{=TAOM_clan_sturgia_6}House of the River</xsl:attribute>
      <xsl:attribute name="initial_home_settlement">Settlement.castle_S6</xsl:attribute>
      <xsl:attribute name="banner_key">11.312.166.1528.1528.764.764.1.0.0.27000.238.171.900.900.765.790.0.0.0</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="Faction[@id='clan_sturgia_7']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name']"/>
      <xsl:attribute name="name">{=TAOM_clan_sturgia_7}House of the Archers</xsl:attribute>
      <xsl:attribute name="banner_key">11.312.166.1528.1528.764.764.1.0.0.27000.238.171.900.900.765.790.0.0.0</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="Faction[@id='clan_sturgia_8']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name']"/>
      <xsl:attribute name="name">{=TAOM_clan_sturgia_8}House of Long Lake</xsl:attribute>
      <xsl:attribute name="banner_key">11.312.166.1528.1528.764.764.1.0.0.27000.238.171.900.900.765.790.0.0.0</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="Faction[@id='clan_sturgia_9']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name']"/>
      <xsl:attribute name="name">{=TAOM_clan_sturgia_9}House of the Dragon-slayer</xsl:attribute>
      <xsl:attribute name="banner_key">11.312.166.1528.1528.764.764.1.0.0.27000.238.171.900.900.765.790.0.0.0</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <!-- ==================== HARAD CLANS (Aserai) ==================== -->
  <xsl:template match="Faction[@id='clan_aserai_1']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name']"/>
      <xsl:attribute name="name">{=TAOM_clan_aserai_1}Ayerikkä</xsl:attribute>
      <xsl:attribute name="banner_key">11.149.166.1528.1528.764.764.1.0.0.504.142.171.813.2468.764.599.0.0.178.504.116.171.771.2278.764.599.0.0.178.504.142.171.733.2225.764.599.0.0.178.505.142.171.813.2468.205.600.0.0.0.505.116.171.813.2468.196.600.0.0.0.505.142.171.813.2468.189.600.0.0.0.505.142.171.813.2468.1322.600.0.1.0.505.116.171.813.2468.1331.600.0.1.0.505.142.171.813.2468.1338.600.0.1.0.503.116.171.133.133.763.512.0.1.0.503.142.171.118.118.763.512.0.1.0.504.142.171.49.49.744.564.0.1.314.504.142.171.49.49.783.564.0.0.44.503.116.171.33.71.763.559.0.1.0.503.116.171.106.106.763.512.0.1.0.503.142.171.84.84.763.512.0.1.0.503.116.171.71.71.763.512.0.1.0.503.142.171.49.49.763.512.0.1.0.503.116.171.21.21.763.604.0.1.0.402.116.171.48.48.501.1028.0.1.89.402.116.171.48.48.501.959.0.1.89.402.116.171.48.48.501.892.0.1.89.402.116.171.48.48.501.824.0.1.89.402.116.171.48.48.501.754.0.1.89.402.116.171.48.48.501.690.0.1.89.402.116.171.48.48.501.621.0.1.89.402.116.171.48.48.501.552.0.1.89.402.116.171.48.48.501.486.0.1.89.402.116.171.48.48.501.417.0.1.89.402.116.171.48.48.501.354.0.1.89.402.116.171.48.48.1025.1028.0.1.89.402.116.171.48.48.1025.959.0.1.89.402.116.171.48.48.1025.892.0.1.89.402.116.171.48.48.1025.824.0.1.89.402.116.171.48.48.1025.754.0.1.89.402.116.171.48.48.1025.690.0.1.89.402.116.171.48.48.1025.621.0.1.89.402.116.171.48.48.1025.552.0.1.89.402.116.171.48.48.1025.486.0.1.89.402.116.171.48.48.1025.417.0.1.89.402.116.171.48.48.1025.354.0.1.89.505.116.171.813.2468.109.600.0.0.0.505.142.171.813.2468.103.600.0.0.0.505.116.171.813.2468.1418.600.0.1.0.505.142.171.813.2468.1424.600.0.1.0.504.116.171.26.139.764.408.0.1.0.504.116.171.26.139.681.574.0.1.127.504.116.171.26.139.847.574.0.0.230.504.116.171.26.139.866.521.0.0.269.504.116.171.26.139.662.521.0.1.89.504.116.171.26.139.862.473.0.0.285.504.116.171.26.139.821.423.0.0.327.504.116.171.26.139.707.423.0.1.30.504.116.171.26.139.666.473.0.1.71.13000.2000.116.300.300.755.797.0.0.0</xsl:attribute>
      <xsl:attribute name="tier">5</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="Faction[@id='clan_aserai_2']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name']"/>
      <xsl:attribute name="name">{=TAOM_clan_aserai_2}Khadnomäa</xsl:attribute>
      <xsl:attribute name="banner_key">11.138.166.1528.1528.764.764.1.0.0.503.143.171.679.679.764.764.0.0.44.503.142.171.662.662.764.764.0.0.44.503.143.171.591.591.764.764.0.0.44.503.142.171.573.573.764.764.0.0.44.503.143.171.20.20.1030.764.0.0.44.503.143.171.20.20.1025.704.0.0.44.503.143.171.20.20.1007.651.0.0.44.503.143.171.20.20.978.605.0.0.44.503.143.171.20.20.764.498.0.0.44.503.143.171.20.20.823.505.0.0.44.503.143.171.20.20.881.525.0.0.44.503.143.171.20.20.932.556.0.0.44.503.143.171.20.20.823.1023.0.0.44.503.143.171.20.20.932.972.0.0.44.503.143.171.20.20.764.1030.0.0.44.503.143.171.20.20.881.1003.0.0.44.503.143.171.20.20.978.923.0.0.44.503.143.171.20.20.1007.877.0.0.44.503.143.171.20.20.1025.824.0.0.44.503.143.171.20.20.499.764.0.1.314.503.143.171.20.20.504.704.0.1.314.503.143.171.20.20.522.651.0.1.314.503.143.171.20.20.551.605.0.1.314.503.143.171.20.20.765.498.0.1.314.503.143.171.20.20.706.505.0.1.314.503.143.171.20.20.648.525.0.1.314.503.143.171.20.20.597.556.0.1.314.503.143.171.20.20.706.1023.0.1.314.503.143.171.20.20.597.972.0.1.314.503.143.171.20.20.765.1030.0.1.314.503.143.171.20.20.648.1003.0.1.314.503.143.171.20.20.551.923.0.1.314.503.143.171.20.20.522.877.0.1.314.503.143.171.20.20.504.824.0.1.314.427.143.171.140.140.624.630.0.1.44.427.143.171.140.140.624.898.0.0.134.427.143.171.140.140.904.898.0.1.224.427.143.171.140.140.904.630.0.0.-44.515.143.171.75.75.765.592.0.0.0.515.143.171.75.75.765.592.0.0.44.515.142.171.60.60.764.592.0.0.0.515.142.171.60.60.764.592.0.0.44.515.143.171.75.75.765.936.0.0.0.515.143.171.75.75.765.936.0.0.44.515.142.171.60.60.764.936.0.0.0.515.142.171.60.60.764.936.0.0.44.515.143.171.75.75.936.764.0.0.0.515.143.171.75.75.936.764.0.0.44.515.142.171.60.60.935.764.0.0.0.515.142.171.60.60.935.764.0.0.44.515.143.171.75.75.592.764.0.0.0.515.143.171.75.75.592.764.0.0.44.515.142.171.60.60.591.764.0.0.0.515.142.171.60.60.591.764.0.0.44.503.143.171.269.269.764.764.0.0.44.503.142.171.258.258.764.764.0.0.44.503.143.171.217.217.764.764.0.0.44.503.138.171.205.205.764.764.0.0.44.512.143.171.30.30.764.663.0.0.0.512.143.171.30.30.764.865.0.0.0.512.143.171.30.30.663.764.0.0.89.512.143.171.30.30.665.744.0.0.76.512.143.171.30.30.671.725.0.0.60.512.143.171.30.30.680.709.0.0.52.512.143.171.30.30.691.693.0.0.41.512.143.171.30.30.705.681.0.0.29.512.143.171.30.30.722.672.0.0.17.512.143.171.30.30.742.666.0.0.6.512.143.171.30.30.742.862.0.1.171.512.143.171.30.30.722.856.0.1.160.512.143.171.30.30.705.847.0.1.148.512.143.171.30.30.691.835.0.1.136.512.143.171.30.30.680.819.0.1.124.512.143.171.30.30.671.803.0.1.117.512.143.171.30.30.665.784.0.1.101.512.143.171.30.30.765.663.0.1.0.512.143.171.30.30.765.865.0.1.0.512.143.171.30.30.866.764.0.1.269.512.143.171.30.30.864.744.0.1.280.512.143.171.30.30.858.725.0.1.297.512.143.171.30.30.849.709.0.1.304.512.143.171.30.30.838.693.0.1.316.512.143.171.30.30.824.681.0.1.328.512.143.171.30.30.807.672.0.1.340.512.143.171.30.30.787.666.0.1.351.512.143.171.30.30.787.862.0.0.185.512.143.171.30.30.807.856.0.0.197.512.143.171.30.30.824.847.0.0.209.512.143.171.30.30.838.835.0.0.221.512.143.171.30.30.849.819.0.0.232.512.143.171.30.30.858.803.0.0.240.512.143.171.30.30.864.784.0.0.256.19007.31.116.200.200.767.755.1.0.0</xsl:attribute>
      <xsl:attribute name="tier">4</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="Faction[@id='clan_aserai_3']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name']"/>
      <xsl:attribute name="name">{=TAOM_clan_aserai_3}Kamalakä</xsl:attribute>
      <xsl:attribute name="banner_key">11.231.166.1528.1528.764.764.1.0.0.504.3.171.771.2278.764.599.0.0.179.504.142.171.733.2225.764.599.0.0.179.13003.88.116.300.300.761.504.1.0.0.13000.88.116.300.300.755.803.0.0.0</xsl:attribute>
      <xsl:attribute name="tier">4</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="Faction[@id='clan_aserai_4']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name']"/>
      <xsl:attribute name="name">{=TAOM_clan_aserai_4}Calmacilioni</xsl:attribute>
      <xsl:attribute name="banner_key">11.149.166.1528.1528.764.764.1.0.0.505.266.171.483.483.764.764.0.0.44.505.202.171.451.451.764.764.0.0.44.505.266.171.483.483.764.764.0.0.0.505.202.171.471.471.764.764.0.0.0.503.172.171.40.40.764.532.0.0.0.503.172.171.40.40.764.995.0.0.0.503.172.171.40.40.995.764.0.0.0.503.274.171.40.40.533.756.0.0.0.505.266.116.400.400.764.764.0.0.0.505.2000.116.381.381.764.764.0.0.0.505.266.116.359.359.764.764.0.0.0.505.100.116.347.347.764.764.0.0.0.515.172.116.40.40.640.638.0.0.0.515.172.116.40.40.640.638.0.0.44.12005.172.116.300.300.764.764.1.0.0.515.172.116.40.40.888.638.0.0.0.515.172.116.40.40.888.638.0.0.44.515.172.116.40.40.888.890.0.0.0.515.172.116.40.40.888.890.0.0.44.515.172.116.40.40.640.890.0.0.0.515.172.116.40.40.640.890.0.0.44</xsl:attribute>
      <xsl:attribute name="tier">4</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="Faction[@id='clan_aserai_5']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name']"/>
      <xsl:attribute name="name">{=TAOM_clan_aserai_5}Tacfimin</xsl:attribute>
      <xsl:attribute name="banner_key">11.0.166.1528.1528.764.764.1.0.0.505.116.171.450.450.764.764.0.0.44.505.0.171.440.440.764.764.0.0.44.505.116.171.413.413.764.764.0.0.44.505.0.171.376.376.764.764.0.0.44.505.116.171.361.361.764.764.0.0.44.505.116.171.409.409.764.764.0.0.0.505.0.171.397.397.764.764.0.0.0.505.116.171.372.372.764.764.0.0.0.505.0.171.336.336.764.764.0.0.0.505.116.171.320.320.764.764.0.0.0.13004.88.116.300.300.765.765.1.0.0</xsl:attribute>
      <xsl:attribute name="tier">4</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="Faction[@id='clan_aserai_6']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name']"/>
      <xsl:attribute name="name">{=TAOM_clan_aserai_6}Medjanomä</xsl:attribute>
      <xsl:attribute name="banner_key">11.138.166.1528.1528.764.764.1.0.0.503.143.171.421.421.763.631.0.0.0.503.148.171.418.418.763.637.0.0.0.505.138.171.1430.418.763.746.0.0.179.504.143.171.379.990.764.851.0.0.179.504.148.171.371.970.764.842.0.0.179.504.143.171.56.175.764.398.0.0.0.504.143.171.49.171.840.410.0.0.-19.504.143.171.46.161.902.445.0.0.-34.504.143.171.39.145.946.497.0.0.-52.504.143.171.49.171.688.410.0.1.18.504.143.171.46.161.626.445.0.1.33.504.143.171.39.145.582.497.0.1.50.505.148.171.366.18.765.565.0.0.179.13003.143.116.200.200.771.618.1.0.0.19013.143.116.175.175.764.780.0.0.0</xsl:attribute>
      <xsl:attribute name="tier">3</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="Faction[@id='clan_aserai_7']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name']"/>
      <xsl:attribute name="name">{=TAOM_clan_aserai_7}Ziyalokkä</xsl:attribute>
      <xsl:attribute name="banner_key">11.149.166.1528.1528.764.764.1.0.0.505.143.171.475.1556.764.764.0.0.0.505.148.171.419.1556.764.764.0.0.0.411.119.171.24.24.571.1120.0.0.0.411.119.171.24.24.571.1098.0.0.0.411.119.171.24.24.571.1077.0.0.0.411.119.171.24.24.571.1056.0.0.0.411.119.171.24.24.571.1034.0.0.0.411.119.171.24.24.571.1013.0.0.0.411.119.171.24.24.571.991.0.0.0.411.119.171.24.24.571.970.0.0.0.411.119.171.24.24.571.949.0.0.0.411.119.171.24.24.571.927.0.0.0.411.119.171.24.24.571.908.0.0.0.411.119.171.24.24.571.886.0.0.0.411.119.171.24.24.571.865.0.0.0.411.119.171.24.24.571.844.0.0.0.411.119.171.24.24.571.822.0.0.0.411.119.171.24.24.571.802.0.0.0.411.119.171.24.24.571.780.0.0.0.411.119.171.24.24.571.759.0.0.0.411.119.171.24.24.571.738.0.0.0.411.119.171.24.24.571.716.0.0.0.411.119.171.24.24.571.695.0.0.0.411.119.171.24.24.571.673.0.0.0.411.119.171.24.24.571.652.0.0.0.411.119.171.24.24.571.631.0.0.0.411.119.171.24.24.571.609.0.0.0.411.119.171.24.24.571.591.0.0.0.411.119.171.24.24.571.569.0.0.0.411.119.171.24.24.571.548.0.0.0.411.119.171.24.24.571.527.0.0.0.411.119.171.24.24.571.505.0.0.0.411.119.171.24.24.571.486.0.0.0.411.119.171.24.24.571.465.0.0.0.411.119.171.24.24.571.443.0.0.0.411.119.171.24.24.571.424.0.0.0.411.119.171.24.24.571.406.0.0.0.411.119.171.24.24.957.1120.0.0.0.411.119.171.24.24.957.1098.0.0.0.411.119.171.24.24.957.1077.0.0.0.411.119.171.24.24.957.1056.0.0.0.411.119.171.24.24.957.1034.0.0.0.411.119.171.24.24.957.1013.0.0.0.411.119.171.24.24.957.991.0.0.0.411.119.171.24.24.957.970.0.0.0.411.119.171.24.24.957.949.0.0.0.411.119.171.24.24.957.927.0.0.0.411.119.171.24.24.957.908.0.0.0.411.119.171.24.24.957.886.0.0.0.411.119.171.24.24.957.865.0.0.0.411.119.171.24.24.957.844.0.0.0.411.119.171.24.24.957.822.0.0.0.411.119.171.24.24.957.802.0.0.0.411.119.171.24.24.957.780.0.0.0.411.119.171.24.24.957.759.0.0.0.411.119.171.24.24.957.738.0.0.0.411.119.171.24.24.957.716.0.0.0.411.119.171.24.24.957.695.0.0.0.411.119.171.24.24.957.673.0.0.0.411.119.171.24.24.957.652.0.0.0.411.119.171.24.24.957.631.0.0.0.411.119.171.24.24.957.609.0.0.0.411.119.171.24.24.957.591.0.0.0.411.119.171.24.24.957.569.0.0.0.411.119.171.24.24.957.548.0.0.0.411.119.171.24.24.957.527.0.0.0.411.119.171.24.24.957.505.0.0.0.411.119.171.24.24.957.486.0.0.0.411.119.171.24.24.957.465.0.0.0.411.119.171.24.24.957.443.0.0.0.411.119.171.24.24.957.424.0.0.0.411.119.171.24.24.957.406.0.0.0.13002.149.2000.400.400.775.804.1.0.0.19006.149.116.350.350.755.530.1.0.0</xsl:attribute>
      <xsl:attribute name="tier">3</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="Faction[@id='clan_aserai_8']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name']"/>
      <xsl:attribute name="name">{=TAOM_clan_aserai_8}Awzalikä</xsl:attribute>
      <xsl:attribute name="banner_key">11.149.166.1528.1528.764.764.1.0.0.503.3.171.679.679.764.764.0.0.44.503.142.171.662.662.764.764.0.0.44.503.3.171.345.345.764.764.0.0.44.503.100.171.319.319.764.764.0.0.44.503.3.171.268.268.764.764.0.0.44.503.100.171.253.253.764.764.0.0.44.512.3.171.36.36.764.638.0.0.0.512.3.171.36.36.764.889.0.0.0.512.3.171.36.36.638.764.0.0.89.512.3.171.36.36.641.739.0.0.76.512.3.171.36.36.648.716.0.0.60.512.3.171.36.36.660.695.0.0.52.512.3.171.36.36.673.676.0.0.41.512.3.171.36.36.691.661.0.0.29.512.3.171.36.36.711.650.0.0.17.512.3.171.36.36.736.642.0.0.6.512.3.171.36.36.736.885.0.1.171.512.3.171.36.36.711.878.0.1.160.512.3.171.36.36.691.867.0.1.148.512.3.171.36.36.673.851.0.1.136.512.3.171.36.36.660.832.0.1.124.512.3.171.36.36.648.812.0.1.117.512.3.171.36.36.641.788.0.1.101.512.3.171.36.36.764.638.0.1.0.512.3.171.36.36.764.889.0.1.0.512.3.171.36.36.889.764.0.1.269.512.3.171.36.36.886.739.0.1.280.512.3.171.36.36.879.716.0.1.297.512.3.171.36.36.867.695.0.1.304.512.3.171.36.36.854.676.0.1.316.512.3.171.36.36.836.661.0.1.328.512.3.171.36.36.816.650.0.1.340.512.3.171.36.36.791.642.0.1.351.512.3.171.36.36.791.885.0.0.185.512.3.171.36.36.816.878.0.0.197.512.3.171.36.36.836.867.0.0.209.512.3.171.36.36.854.851.0.0.221.512.3.171.36.36.867.832.0.0.232.512.3.171.36.36.879.812.0.0.240.512.3.171.36.36.886.788.0.0.256.427.3.171.100.100.765.577.0.0.0.515.3.171.60.60.765.544.0.0.0.515.3.171.60.60.765.544.0.0.44.427.3.171.100.100.765.951.0.1.179.515.3.171.60.60.765.984.0.1.179.515.3.171.60.60.765.984.0.1.134.427.3.171.100.100.952.764.0.0.269.515.3.171.60.60.985.763.0.0.269.515.3.171.60.60.985.763.0.0.314.427.3.171.100.100.575.764.0.1.88.515.3.171.60.60.542.763.0.1.88.515.3.171.60.60.542.763.0.1.43.427.3.171.100.100.901.634.0.0.315.515.3.171.60.60.923.610.0.0.315.515.3.171.60.60.923.610.0.0.0.427.3.171.100.100.626.634.0.1.42.515.3.171.60.60.604.610.0.1.42.515.3.171.60.60.604.610.0.1.357.427.3.171.100.100.626.634.0.1.42.515.3.171.60.60.604.610.0.1.42.515.3.171.60.60.604.610.0.1.357.427.3.171.100.100.901.893.0.1.222.515.3.171.60.60.923.917.0.1.222.515.3.171.60.60.923.917.0.1.177.427.3.171.100.100.626.893.0.0.135.515.3.171.60.60.604.917.0.0.135.515.3.171.60.60.604.917.0.0.180.13001.31.116.200.200.767.762.0.0.0</xsl:attribute>
      <xsl:attribute name="tier">3</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="Faction[@id='clan_aserai_9']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name']"/>
      <xsl:attribute name="name">{=TAOM_clan_aserai_9}Ikendakä</xsl:attribute>
      <xsl:attribute name="banner_key">11.142.166.1528.1528.764.764.1.0.0.503.143.171.679.679.764.764.0.0.44.503.116.171.656.656.764.764.0.0.44.503.143.171.584.584.764.764.0.0.44.503.116.171.564.564.764.764.0.0.44.427.143.171.75.75.764.600.0.0.0.427.143.171.75.75.764.928.0.1.179.427.143.171.75.75.928.764.0.0.269.427.143.171.75.75.600.764.0.1.89.439.143.171.55.125.504.764.0.1.0.439.143.171.55.125.520.673.0.1.-18.439.143.171.55.125.568.590.0.1.-35.439.143.171.55.125.639.534.0.1.-58.439.143.171.55.125.726.503.0.1.-79.439.143.171.55.125.815.505.0.1.-100.439.143.171.55.125.898.535.0.1.-115.439.143.171.55.125.963.592.0.1.-138.439.143.171.55.125.1008.670.0.1.-158.439.143.171.55.125.1027.761.0.1.-178.439.143.171.55.125.1013.851.0.1.-195.439.143.171.55.125.970.931.0.1.-214.439.143.171.55.125.903.986.0.1.-235.439.143.171.55.125.818.1019.0.1.-255.439.143.171.55.125.726.1022.0.1.-276.439.143.171.55.125.639.992.0.1.-299.439.143.171.55.125.567.935.0.1.-321.439.143.171.55.125.520.853.0.1.-337.503.143.171.273.273.764.764.0.0.44.503.116.171.263.263.764.764.0.0.44.503.143.171.221.221.764.764.0.0.44.503.142.171.211.211.764.764.0.0.44.439.143.171.21.49.661.764.0.1.0.439.143.171.21.49.667.729.0.1.-18.439.143.171.21.49.686.696.0.1.-35.439.143.171.21.49.714.674.0.1.-58.439.143.171.21.49.748.662.0.1.-79.439.143.171.21.49.783.663.0.1.-100.439.143.171.21.49.816.674.0.1.-115.439.143.171.21.49.841.697.0.1.-138.439.143.171.21.49.859.727.0.1.-158.439.143.171.21.49.866.763.0.1.-178.439.143.171.21.49.861.798.0.1.-195.439.143.171.21.49.844.830.0.1.-214.439.143.171.21.49.818.851.0.1.-235.439.143.171.21.49.784.864.0.1.-255.439.143.171.21.49.748.866.0.1.-276.439.143.171.21.49.714.854.0.1.-299.439.143.171.21.49.686.831.0.1.-321.439.143.171.21.49.667.799.0.1.-337.19005.143.116.175.175.762.765.1.0.0</xsl:attribute>
      <xsl:attribute name="tier">4</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <!-- ==================== ROHAN CLANS (Vlandia) ==================== -->
  <xsl:template match="Faction[@id='clan_vlandia_1']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'banner_key']"/>
      <xsl:attribute name="name">{=TAOM_clan_vlandia_1}House of Eorling</xsl:attribute>
      <xsl:attribute name="banner_key">11.157.166.1528.1528.764.764.1.0.0.21004.172.36.550.550.780.765.1.0.0</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="Faction[@id='clan_vlandia_2']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'banner_key']"/>
      <xsl:attribute name="name">{=TAOM_clan_vlandia_2}House of Cerdicing</xsl:attribute>
      <xsl:attribute name="banner_key">11.272.272.1528.1528.764.764.1.0.0.21007.172.171.700.700.764.764.0.0.0</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="Faction[@id='clan_vlandia_3']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'banner_key']"/>
      <xsl:attribute name="name">{=TAOM_clan_vlandia_3}House of Grimingas</xsl:attribute>
      <xsl:attribute name="banner_key">11.272.272.1528.1528.764.764.1.0.0.21007.172.171.700.700.764.764.0.0.0</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="Faction[@id='clan_vlandia_4']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'banner_key']"/>
      <xsl:attribute name="name">{=TAOM_clan_vlandia_4}House of Felánding</xsl:attribute>
      <xsl:attribute name="banner_key">11.272.272.1528.1528.764.764.1.0.0.21007.172.171.700.700.764.764.0.0.0</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="Faction[@id='clan_vlandia_5']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'banner_key']"/>
      <xsl:attribute name="name">{=TAOM_clan_vlandia_5}House of Oscyteling</xsl:attribute>
      <xsl:attribute name="banner_key">11.272.272.1528.1528.764.764.1.0.0.21007.172.171.700.700.764.764.0.0.0</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="Faction[@id='clan_vlandia_6']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'initial_home_settlement' and local-name() != 'banner_key']"/>
      <xsl:attribute name="name">{=TAOM_clan_vlandia_6}House of Ordlacing</xsl:attribute>
      <xsl:attribute name="initial_home_settlement">Settlement.castle_V6</xsl:attribute>
      <xsl:attribute name="banner_key">11.272.272.1528.1528.764.764.1.0.0.21007.172.171.700.700.764.764.0.0.0</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="Faction[@id='clan_vlandia_7']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'owner' and local-name() != 'banner_key']"/>
      <xsl:attribute name="name">{=TAOM_clan_vlandia_7}House of Æthellafing</xsl:attribute>
      <xsl:attribute name="owner">Hero.lord_4_23_1</xsl:attribute>
      <xsl:attribute name="banner_key">11.272.272.1528.1528.764.764.1.0.0.21007.172.171.700.700.764.764.0.0.0</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="Faction[@id='clan_vlandia_8']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'initial_home_settlement' and local-name() != 'banner_key']"/>
      <xsl:attribute name="name">{=TAOM_clan_vlandia_8}House of Grimmóding</xsl:attribute>
      <xsl:attribute name="initial_home_settlement">Settlement.castle_V3</xsl:attribute>
      <xsl:attribute name="banner_key">11.272.272.1528.1528.764.764.1.0.0.21007.172.171.700.700.764.764.0.0.0</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="Faction[@id='clan_vlandia_9']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'banner_key']"/>
      <xsl:attribute name="name">{=TAOM_clan_vlandia_9}House of Dúning</xsl:attribute>
      <xsl:attribute name="banner_key">11.272.272.1528.1528.764.764.1.0.0.21007.172.171.700.700.764.764.0.0.0</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="Faction[@id='clan_vlandia_10']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'owner' and local-name() != 'banner_key']"/>
      <xsl:attribute name="name">{=TAOM_clan_vlandia_10}House of Eoforing</xsl:attribute>
      <xsl:attribute name="owner">Hero.lord_4_28_1</xsl:attribute>
      <xsl:attribute name="banner_key">11.149.166.1528.1528.764.764.1.0.0.21004.172.36.550.550.780.765.1.0.0</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="Faction[@id='clan_vlandia_11']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'owner' and local-name() != 'banner_key']"/>
      <xsl:attribute name="name">{=TAOM_clan_vlandia_11}House of Tordaging</xsl:attribute>
      <xsl:attribute name="owner">Hero.lord_V11_u</xsl:attribute>
      <xsl:attribute name="banner_key">11.272.272.1528.1528.764.764.1.0.0.21007.172.171.700.700.764.764.0.0.0</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <!-- ==================== KHAND CLANS (Battania) ==================== -->
  <xsl:template match="Faction[@id='clan_battania_1']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'initial_home_settlement']"/>
      <xsl:attribute name="name">{=TAOM_clan_battania_1}Vangaris</xsl:attribute>
      <xsl:attribute name="banner_key">11.14.166.1528.1528.764.764.1.0.0.503.131.171.585.585.764.764.0.0.0.503.148.171.566.566.764.764.0.0.43.512.131.171.26.34.764.496.0.0.0.512.131.171.26.34.1031.764.0.0.89.512.131.171.26.34.1030.740.0.0.89.512.131.171.26.34.1025.714.0.0.97.512.131.171.26.34.1019.689.0.0.101.512.131.171.26.34.1014.666.0.0.107.512.131.171.26.34.1004.644.0.0.112.512.131.171.26.34.788.497.0.0.0.512.131.171.26.34.811.498.0.0.-9.512.131.171.26.34.832.504.0.0.-12.512.131.171.26.34.853.511.0.0.-18.512.131.171.26.34.874.521.0.0.-24.512.131.171.26.34.895.532.0.0.-29.512.131.171.26.34.914.545.0.0.-34.512.131.171.26.34.932.559.0.0.-40.512.131.171.26.34.993.622.0.0.121.512.131.171.26.34.979.602.0.0.127.512.131.171.26.34.965.585.0.0.132.512.131.171.26.34.949.572.0.0.135.512.131.171.26.34.764.496.0.1.0.512.131.171.26.34.497.764.0.1.269.512.131.171.26.34.498.740.0.1.269.512.131.171.26.34.503.714.0.1.260.512.131.171.26.34.509.689.0.1.256.512.131.171.26.34.514.666.0.1.250.512.131.171.26.34.524.644.0.1.245.512.131.171.26.34.740.497.0.1.0.512.131.171.26.34.717.498.0.1.9.512.131.171.26.34.696.504.0.1.12.512.131.171.26.34.675.511.0.1.18.512.131.171.26.34.654.521.0.1.22.512.131.171.26.34.633.532.0.1.29.512.131.171.26.34.614.545.0.1.33.512.131.171.26.34.596.559.0.1.39.512.131.171.26.34.535.622.0.1.235.512.131.171.26.34.549.602.0.1.230.512.131.171.26.34.563.585.0.1.225.512.131.171.26.34.579.572.0.1.222.512.131.171.26.34.764.1032.0.0.179.512.131.171.26.34.497.764.0.0.-89.512.131.171.26.34.498.788.0.0.-89.512.131.171.26.34.503.814.0.0.-81.512.131.171.26.34.509.839.0.0.-77.512.131.171.26.34.514.862.0.0.-71.512.131.171.26.34.524.884.0.0.-66.512.131.171.26.34.740.1031.0.0.179.512.131.171.26.34.717.1030.0.0.169.512.131.171.26.34.696.1024.0.0.166.512.131.171.26.34.675.1017.0.0.160.512.131.171.26.34.654.1007.0.0.154.512.131.171.26.34.633.996.0.0.148.512.131.171.26.34.614.983.0.0.144.512.131.171.26.34.596.969.0.0.138.512.131.171.26.34.535.906.0.0.-56.512.131.171.26.34.549.926.0.0.-50.512.131.171.26.34.563.943.0.0.-45.512.131.171.26.34.579.956.0.0.-43.512.131.171.26.34.764.1032.0.1.179.512.131.171.26.34.1031.764.0.1.89.512.131.171.26.34.1030.788.0.1.89.512.131.171.26.34.1025.814.0.1.80.512.131.171.26.34.1019.839.0.1.76.512.131.171.26.34.1014.862.0.1.70.512.131.171.26.34.1004.884.0.1.65.512.131.171.26.34.788.1031.0.1.179.512.131.171.26.34.811.1030.0.1.189.512.131.171.26.34.832.1024.0.1.192.512.131.171.26.34.853.1017.0.1.198.512.131.171.26.34.874.1007.0.1.203.512.131.171.26.34.895.996.0.1.209.512.131.171.26.34.914.983.0.1.213.512.131.171.26.34.932.969.0.1.219.512.131.171.26.34.993.906.0.1.56.512.131.171.26.34.979.926.0.1.50.512.131.171.26.34.965.943.0.1.45.512.131.171.26.34.949.956.0.1.42.13003.238.116.610.619.765.775.1.0.10.13003.230.116.610.610.765.775.1.0.0.13003.31.116.450.450.765.775.1.1.0</xsl:attribute>
      <xsl:attribute name="tier">5</xsl:attribute>
      <xsl:attribute name="initial_home_settlement">Settlement.town_K1</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="Faction[@id='clan_battania_2']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'initial_home_settlement']"/>
      <xsl:attribute name="name">{=TAOM_clan_battania_2}Araz</xsl:attribute>
      <xsl:attribute name="banner_key">11.100.166.1528.1528.764.764.1.0.0.503.143.171.679.679.764.764.0.0.42.503.138.171.656.656.764.764.0.0.42.503.143.171.584.584.764.764.0.0.42.503.100.171.564.564.764.764.0.0.42.439.143.171.55.125.504.764.0.1.0.439.143.171.55.125.520.673.0.1.-16.439.143.171.55.125.568.590.0.1.-33.439.143.171.55.125.639.534.0.1.-54.439.143.171.55.125.726.503.0.1.-77.439.143.171.55.125.815.505.0.1.-97.439.143.171.55.125.898.535.0.1.-112.439.143.171.55.125.963.592.0.1.-136.439.143.171.55.125.1008.670.0.1.-156.439.143.171.55.125.1027.761.0.1.-176.439.143.171.55.125.1013.851.0.1.-193.439.143.171.55.125.970.931.0.1.-211.439.143.171.55.125.903.986.0.1.-232.439.143.171.55.125.818.1019.0.1.-253.439.143.171.55.125.726.1022.0.1.-274.439.143.171.55.125.639.992.0.1.-297.439.143.171.55.125.567.935.0.1.-319.439.143.171.55.125.520.853.0.1.-335.13003.76.116.600.600.765.775.0.1.0.13003.131.116.600.600.765.775.0.1.9</xsl:attribute>
      <xsl:attribute name="tier">4</xsl:attribute>
      <xsl:attribute name="initial_home_settlement">Settlement.town_K2</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="Faction[@id='clan_battania_3']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'initial_home_settlement']"/>
      <xsl:attribute name="name">{=TAOM_clan_battania_3}Torabai</xsl:attribute>
      <xsl:attribute name="banner_key">11.148.166.1528.1528.764.764.1.0.0.503.132.116.600.600.764.764.0.1.0.503.145.116.576.576.764.764.0.1.0.503.132.116.467.467.764.764.0.1.0.503.145.116.455.455.764.764.0.1.0.426.132.116.50.54.627.652.0.1.201.510.132.116.24.54.623.655.0.1.122.426.132.116.50.54.643.682.0.1.22.510.132.116.24.54.647.678.0.1.303.426.132.116.50.54.648.716.0.1.202.510.132.116.24.54.644.720.0.1.123.426.132.116.50.54.661.746.0.1.9.510.132.116.24.54.666.744.0.1.290.426.132.116.50.54.648.641.0.1.252.510.132.116.24.54.648.647.0.1.173.426.132.116.50.54.681.648.0.1.73.510.132.116.24.54.681.643.0.1.354.426.132.116.50.54.711.666.0.1.253.510.132.116.24.54.712.671.0.1.174.426.132.116.50.54.743.674.0.1.60.510.132.116.24.54.744.669.0.1.341.426.132.116.50.54.900.652.0.0.158.510.132.116.24.54.904.655.0.0.237.426.132.116.50.54.884.682.0.0.337.510.132.116.24.54.880.678.0.0.56.426.132.116.50.54.879.716.0.0.157.510.132.116.24.54.883.720.0.0.236.426.132.116.50.54.866.746.0.0.350.510.132.116.24.54.861.744.0.0.69.426.132.116.50.54.879.641.0.0.107.510.132.116.24.54.879.647.0.0.186.426.132.116.50.54.846.648.0.0.286.510.132.116.24.54.846.643.0.0.5.426.132.116.50.54.816.666.0.0.106.510.132.116.24.54.815.671.0.0.185.426.132.116.50.54.784.674.0.0.299.510.132.116.24.54.783.669.0.0.18.426.132.116.50.54.900.875.0.1.21.510.132.116.24.54.904.872.0.1.-58.426.132.116.50.54.884.845.0.1.-158.510.132.116.24.54.880.849.0.1.123.426.132.116.50.54.879.811.0.1.22.510.132.116.24.54.883.807.0.1.-57.426.132.116.50.54.866.781.0.1.-171.510.132.116.24.54.861.783.0.1.110.426.132.116.50.54.879.886.0.1.72.510.132.116.24.54.879.880.0.1.-7.426.132.116.50.54.846.879.0.1.-107.510.132.116.24.54.846.884.0.1.174.426.132.116.50.54.816.861.0.1.73.510.132.116.24.54.815.856.0.1.-6.426.132.116.50.54.784.853.0.1.-120.510.132.116.24.54.783.858.0.1.161.426.132.116.50.54.627.875.0.0.338.510.132.116.24.54.623.872.0.0.57.426.132.116.50.54.643.845.0.0.157.510.132.116.24.54.647.849.0.0.236.426.132.116.50.54.648.811.0.0.337.510.132.116.24.54.644.807.0.0.56.426.132.116.50.54.661.781.0.0.170.510.132.116.24.54.666.783.0.0.249.426.132.116.50.54.648.886.0.0.287.510.132.116.24.54.648.880.0.0.6.426.132.116.50.54.681.879.0.0.106.510.132.116.24.54.681.884.0.0.185.426.132.116.50.54.711.861.0.0.286.510.132.116.24.54.712.856.0.0.5.426.132.116.50.54.743.853.0.0.119.510.132.116.24.54.744.858.0.0.198.504.38.116.100.288.878.764.0.0.270.504.132.116.89.255.872.764.0.0.270.504.38.116.66.191.861.764.0.0.270.504.132.116.47.136.854.764.0.0.270.504.38.116.100.288.649.764.0.1.89.504.132.116.89.255.655.764.0.1.89.504.38.116.66.191.666.764.0.1.89.504.132.116.47.136.673.764.0.1.89.504.38.116.100.288.765.650.0.1.0.504.132.116.89.255.765.656.0.1.0.504.38.116.66.191.765.667.0.1.0.504.132.116.47.136.765.674.0.1.0.504.38.116.100.288.765.877.0.0.180.504.132.116.89.255.765.871.0.0.180.504.38.116.66.191.765.860.0.0.180.504.132.116.47.136.765.853.0.0.180.503.38.116.141.141.764.764.0.1.0.503.132.116.132.132.764.764.0.1.0.503.38.116.100.100.764.764.0.1.0.503.132.116.89.89.764.764.0.1.0.511.132.116.53.66.764.542.0.1.270.511.132.116.53.66.787.542.0.1.270.511.132.116.53.66.809.546.0.1.263.511.132.116.53.66.832.551.0.1.258.511.132.116.53.66.854.559.0.1.253.511.132.116.53.66.874.571.0.1.243.511.132.116.53.66.894.583.0.1.243.511.132.116.53.66.912.598.0.1.234.511.132.116.53.66.928.614.0.1.223.511.132.116.53.66.943.634.0.1.217.511.132.116.53.66.957.653.0.1.212.511.132.116.53.66.968.674.0.1.205.511.132.116.53.66.977.696.0.1.202.511.132.116.53.66.982.721.0.1.190.511.132.116.53.66.986.744.0.1.190.511.132.116.53.66.986.766.0.1.184.511.132.116.53.66.985.790.0.1.179.511.132.116.53.66.982.813.0.1.174.511.132.116.53.66.974.835.0.1.163.511.132.116.53.66.965.856.0.1.163.511.132.116.53.66.954.877.0.1.155.511.132.116.53.66.941.896.0.1.144.511.132.116.53.66.925.914.0.1.138.511.132.116.53.66.908.931.0.1.133.511.132.116.53.66.890.946.0.1.125.511.132.116.53.66.870.957.0.1.122.511.132.116.53.66.848.969.0.1.117.511.132.116.53.66.824.976.0.1.106.511.132.116.53.66.802.983.0.1.106.511.132.116.53.66.779.985.0.1.99.511.132.116.53.66.756.987.0.1.94.511.132.116.53.66.733.985.0.1.89.511.132.116.53.66.710.980.0.1.79.511.132.116.53.66.687.974.0.1.79.511.132.116.53.66.666.964.0.1.71.511.132.116.53.66.646.953.0.1.59.511.132.116.53.66.626.938.0.1.54.511.132.116.53.66.607.924.0.1.49.511.132.116.53.66.591.907.0.1.41.511.132.116.53.66.578.888.0.1.38.511.132.116.53.66.566.867.0.1.32.511.132.116.53.66.557.843.0.1.20.511.132.116.53.66.549.821.0.1.20.511.132.116.53.66.544.799.0.1.14.511.132.116.53.66.541.776.0.1.9.511.132.116.53.66.540.753.0.1.4.511.132.116.53.66.545.730.0.1.-6.511.132.116.53.66.549.707.0.1.-6.511.132.116.53.66.556.685.0.1.-15.511.132.116.53.66.565.664.0.1.-26.511.132.116.53.66.579.643.0.1.-32.511.132.116.53.66.592.623.0.1.-37.511.132.116.53.66.609.604.0.1.-44.511.132.116.53.66.628.588.0.1.-51.511.132.116.53.66.649.573.0.1.-59.511.132.116.53.66.672.562.0.1.-64.511.132.116.53.66.693.553.0.1.-69.511.132.116.53.66.716.546.0.1.-77.511.132.116.53.66.739.544.0.1.-80</xsl:attribute>
      <xsl:attribute name="tier">4</xsl:attribute>
      <xsl:attribute name="initial_home_settlement">Settlement.town_K3</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="Faction[@id='clan_battania_4']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'initial_home_settlement']"/>
      <xsl:attribute name="name">{=TAOM_clan_battania_4}Khorvash</xsl:attribute>
      <xsl:attribute name="banner_key">11.99.166.1528.1528.764.764.1.0.0.504.144.116.240.106.849.679.0.1.237.504.144.116.240.106.799.648.0.1.57.504.144.116.240.106.890.735.0.0.358.504.144.116.240.106.888.795.0.0.178.504.144.116.240.106.680.679.0.0.122.504.144.116.240.106.731.648.0.0.302.504.144.116.240.106.638.735.0.1.2.504.144.116.240.106.641.795.0.1.-178.504.144.116.240.106.848.848.0.0.-58.504.144.116.240.106.798.880.0.0.122.504.144.116.240.106.679.848.0.1.57.504.144.116.240.106.729.880.0.1.-123</xsl:attribute>
      <xsl:attribute name="tier">4</xsl:attribute>
      <xsl:attribute name="initial_home_settlement">Settlement.town_K4</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="Faction[@id='clan_battania_5']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'initial_home_settlement']"/>
      <xsl:attribute name="name">{=TAOM_clan_battania_5}Khurdan</xsl:attribute>
      <xsl:attribute name="banner_key">11.331.167.1528.1528.742.764.1.0.0.14006.2014.116.600.600.755.764.1.1.179.14006.2014.116.600.600.755.764.1.1.0.14006.2000.116.600.600.755.764.1.1.89.515.2000.116.110.110.755.765.0.0.0.515.2014.116.100.100.755.765.0.0.0</xsl:attribute>
      <xsl:attribute name="tier">4</xsl:attribute>
      <xsl:attribute name="initial_home_settlement">Settlement.castle_K1</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="Faction[@id='clan_battania_6']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'initial_home_settlement']"/>
      <xsl:attribute name="name">{=TAOM_clan_battania_6}Urdash</xsl:attribute>
      <xsl:attribute name="banner_key">11.51.166.1528.1528.764.764.1.0.0.14007.256.171.600.600.764.764.0.0.0.10500.256.116.500.500.765.765.1.0.0</xsl:attribute>
      <xsl:attribute name="tier">3</xsl:attribute>
      <xsl:attribute name="initial_home_settlement">Settlement.castle_K3</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="Faction[@id='clan_battania_7']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'initial_home_settlement']"/>
      <xsl:attribute name="name">{=TAOM_clan_battania_7}Karhatai</xsl:attribute>
      <xsl:attribute name="banner_key">11.159.166.1528.1528.764.764.1.0.0.503.131.171.585.585.764.764.0.0.0.503.100.171.566.566.764.764.0.0.42.512.131.171.26.34.764.496.0.0.0.512.131.171.26.34.1031.764.0.0.88.512.131.171.26.34.1030.740.0.0.88.512.131.171.26.34.1025.714.0.0.96.512.131.171.26.34.1019.689.0.0.100.512.131.171.26.34.1014.666.0.0.105.512.131.171.26.34.1004.644.0.0.111.512.131.171.26.34.788.497.0.0.0.512.131.171.26.34.811.498.0.0.-8.512.131.171.26.34.832.504.0.0.-11.512.131.171.26.34.853.511.0.0.-17.512.131.171.26.34.874.521.0.0.-23.512.131.171.26.34.895.532.0.0.-28.512.131.171.26.34.914.545.0.0.-33.512.131.171.26.34.932.559.0.0.-39.512.131.171.26.34.993.622.0.0.120.512.131.171.26.34.979.602.0.0.125.512.131.171.26.34.965.585.0.0.131.512.131.171.26.34.949.572.0.0.134.512.131.171.26.34.764.496.0.1.0.512.131.171.26.34.497.764.0.1.268.512.131.171.26.34.498.740.0.1.268.512.131.171.26.34.503.714.0.1.259.512.131.171.26.34.509.689.0.1.255.512.131.171.26.34.514.666.0.1.248.512.131.171.26.34.524.644.0.1.243.512.131.171.26.34.740.497.0.1.0.512.131.171.26.34.717.498.0.1.8.512.131.171.26.34.696.504.0.1.11.512.131.171.26.34.675.511.0.1.17.512.131.171.26.34.654.521.0.1.21.512.131.171.26.34.633.532.0.1.28.512.131.171.26.34.614.545.0.1.32.512.131.171.26.34.596.559.0.1.38.512.131.171.26.34.535.622.0.1.234.512.131.171.26.34.549.602.0.1.229.512.131.171.26.34.563.585.0.1.224.512.131.171.26.34.579.572.0.1.221.512.131.171.26.34.764.1032.0.0.178.512.131.171.26.34.497.764.0.0.-88.512.131.171.26.34.498.788.0.0.-88.512.131.171.26.34.503.814.0.0.-80.512.131.171.26.34.509.839.0.0.-76.512.131.171.26.34.514.862.0.0.-70.512.131.171.26.34.524.884.0.0.-65.512.131.171.26.34.740.1031.0.0.178.512.131.171.26.34.717.1030.0.0.168.512.131.171.26.34.696.1024.0.0.165.512.131.171.26.34.675.1017.0.0.158.512.131.171.26.34.654.1007.0.0.153.512.131.171.26.34.633.996.0.0.146.512.131.171.26.34.614.983.0.0.142.512.131.171.26.34.596.969.0.0.137.512.131.171.26.34.535.906.0.0.-54.512.131.171.26.34.549.926.0.0.-48.512.131.171.26.34.563.943.0.0.-44.512.131.171.26.34.579.956.0.0.-42.512.131.171.26.34.764.1032.0.1.178.512.131.171.26.34.1031.764.0.1.88.512.131.171.26.34.1030.788.0.1.88.512.131.171.26.34.1025.814.0.1.79.512.131.171.26.34.1019.839.0.1.75.512.131.171.26.34.1014.862.0.1.69.512.131.171.26.34.1004.884.0.1.64.512.131.171.26.34.788.1031.0.1.178.512.131.171.26.34.811.1030.0.1.187.512.131.171.26.34.832.1024.0.1.190.512.131.171.26.34.853.1017.0.1.197.512.131.171.26.34.874.1007.0.1.202.512.131.171.26.34.895.996.0.1.208.512.131.171.26.34.914.983.0.1.211.512.131.171.26.34.932.969.0.1.218.512.131.171.26.34.993.906.0.1.54.512.131.171.26.34.979.926.0.1.48.512.131.171.26.34.965.943.0.1.44.512.131.171.26.34.949.956.0.1.41.13003.238.116.500.500.765.775.1.0.0.503.172.116.150.150.765.765.0.0.0</xsl:attribute>
      <xsl:attribute name="tier">3</xsl:attribute>
      <xsl:attribute name="initial_home_settlement">Settlement.castle_K2</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="Faction[@id='clan_battania_8']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'initial_home_settlement']"/>
      <xsl:attribute name="name">{=TAOM_clan_battania_8}Varyash</xsl:attribute>
      <xsl:attribute name="banner_key">11.99.166.1528.1528.742.764.1.0.0.14003.226.171.600.600.755.764.0.0.0</xsl:attribute>
      <xsl:attribute name="tier">3</xsl:attribute>
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

  <!-- ==================== MERCENARY MINOR FACTIONS ==================== -->

  <!-- Ghilman → Serpent Guard (Haradrim mounted mercenaries) -->
  <xsl:template match="Faction[@id='ghilman']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'short_name' and local-name() != 'text']"/>
      <xsl:attribute name="name">{=TAOM_ghilman_name}Serpent Guard</xsl:attribute>
      <xsl:attribute name="short_name">{=TAOM_ghilman_short}Serpent Guard</xsl:attribute>
      <xsl:attribute name="text">{=TAOM_ghilman_text}The Serpent Guard are mounted soldiers in the service of the Serpentlord of Near Harad. Bred in the saddle on the rolling plains south of the Ephel Dúath, they are unmatched as horse-archers in all of Middle-earth's southern lands. In times of peace between Harad and Mordor, the Guard hires its services to Haradrim lords and to any other power rich enough to afford their golden fee.</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <!-- Legion of the Betrayed → The Grey Company (Dúnedain Rangers) -->
  <xsl:template match="Faction[@id='legion_of_the_betrayed']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'short_name' and local-name() != 'text']"/>
      <xsl:attribute name="name">{=TAOM_legion_name}The Grey Company</xsl:attribute>
      <xsl:attribute name="short_name">{=TAOM_legion_short}Grey Company</xsl:attribute>
      <xsl:attribute name="text">{=TAOM_legion_text}The Grey Company are the last organised brotherhood of the Dúnedain Rangers, descendants of the kings of Arnor who lost their kingdom ages ago. They ride across the North without a home of their own, keeping the peace in lands that have forgotten their names. Though their first loyalty is to the line of Elendil, they take contracts from lords across Middle-earth when their wandering brings them through civilised lands.</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <!-- Skolderbrotva → Axemen of Erebor (Dwarven mercenaries) -->
  <xsl:template match="Faction[@id='skolderbrotva']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'short_name' and local-name() != 'text' and local-name() != 'initial_home_settlement']"/>
      <xsl:attribute name="name">{=TAOM_skolderbrotva_name}Axemen of Erebor</xsl:attribute>
      <xsl:attribute name="short_name">{=TAOM_skolderbrotva_short}Axemen</xsl:attribute>
      <xsl:attribute name="text">{=TAOM_skolderbrotva_text}The Axemen of Erebor are veterans of King Thorin's battles who found peacetime ill-suited to their temperament. Trained in the close-quarter fighting of the Dwarven halls — shield wall, short axe, mattock — they are formidable on any terrain but excel in the mountain passes that connect Erebor to the kingdoms of men. They take contracts from any who can afford their fee, but will not raise an axe against Erebor or Rivendell.</xsl:attribute>
      <xsl:attribute name="initial_home_settlement">Settlement.castle_S1</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <!-- Company of the Boar → Corsair Blades (Umbar ex-pirates) -->
  <xsl:template match="Faction[@id='company_of_the_boar']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'short_name' and local-name() != 'text' and local-name() != 'initial_home_settlement']"/>
      <xsl:attribute name="name">{=TAOM_company_name}Corsair Blades</xsl:attribute>
      <xsl:attribute name="short_name">{=TAOM_company_short}Corsairs</xsl:attribute>
      <xsl:attribute name="text">{=TAOM_company_text}The Corsair Blades are veteran sea-fighters from the harbours of Umbar who have taken to selling their skills on land. They fight as crossbowmen — the preferred arm of shipboard warfare — and are as comfortable in a siege as they are on a rolling deck. Their captain, a scarred Númenórean exile, keeps them to their contracts with iron discipline, knowing that their reputation is their most valuable cargo.</xsl:attribute>
      <xsl:attribute name="initial_home_settlement">Settlement.castle_V3</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <!-- ==================== MAFIA MINOR FACTIONS ==================== -->

  <!-- Beni Zilal → The Blind Eye (Haradrim smuggler-assassins) -->
  <xsl:template match="Faction[@id='beni_zilal']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'short_name' and local-name() != 'text']"/>
      <xsl:attribute name="name">{=TAOM_beni_zilal_name}The Blind Eye</xsl:attribute>
      <xsl:attribute name="short_name">{=TAOM_beni_zilal_short}Blind Eye</xsl:attribute>
      <xsl:attribute name="text">{=TAOM_beni_zilal_text}The Blind Eye is a brotherhood of smugglers, fences, and hired killers operating across the coasts of Harad and the deep desert. They are said to descend from a cult of assassins who served a forgotten Haradrim king, and their methods — poison, shadow, and the leveraging of secrets — remain unchanged. Lords who wish to quietly remove a rival, or merchants who need cargo moved without customs inspection, know where to leave a gold coin and a sealed letter.</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <!-- Wolfskins → Variag Ravagers (Khand raider outlaws) -->
  <xsl:template match="Faction[@id='wolfskins']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'short_name' and local-name() != 'text' and local-name() != 'initial_home_settlement']"/>
      <xsl:attribute name="name">{=TAOM_wolfskins_name}Variag Ravagers</xsl:attribute>
      <xsl:attribute name="short_name">{=TAOM_wolfskins_short}Ravagers</xsl:attribute>
      <xsl:attribute name="text">{=TAOM_wolfskins_text}The Variag Ravagers are war-bands of young Variag warriors from the steppes of Khand who spend their early manhood in a state of ritual warfare: they may own no cooked food, sleep under no permanent roof, and take no mercy. While most eventually return to the settled life of their clans, some never do, and these form the Ravagers — outlaws who prey on the roads of Khand and raid across its borders into Harad and Gondor.</xsl:attribute>
      <xsl:attribute name="initial_home_settlement">Settlement.castle_K5</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <!-- Brotherhood of the Woods → Dunlending Reavers (culture change: vlandia → empire) -->
  <xsl:template match="Faction[@id='brotherhood_of_woods']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'short_name' and local-name() != 'text' and local-name() != 'culture' and local-name() != 'initial_home_settlement']"/>
      <xsl:attribute name="name">{=TAOM_brotherhood_name}Dunlending Reavers</xsl:attribute>
      <xsl:attribute name="short_name">{=TAOM_brotherhood_short}Reavers</xsl:attribute>
      <xsl:attribute name="text">{=TAOM_brotherhood_text}The Dunlending Reavers are the broken remnant of a hill-clan that once dwelt in the valleys now ruled by the Rohirrim. Their grandfathers were driven from fertile land into the rocky heights, and they have nursed that bitterness into a raider's philosophy: everything taken from a southron or a king's man is merely the restoration of what was stolen. In practice they are brigands, but dangerous ones who know every goat-track through the hills.</xsl:attribute>
      <xsl:attribute name="culture">Culture.empire</xsl:attribute>
      <xsl:attribute name="initial_home_settlement">Settlement.castle_EN3</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <!-- Hidden Hand → The Mouth's Servants (Mordor intelligence network) -->
  <xsl:template match="Faction[@id='hidden_hand']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'short_name' and local-name() != 'text']"/>
      <xsl:attribute name="name">{=TAOM_hidden_hand_name}The Mouth's Servants</xsl:attribute>
      <xsl:attribute name="short_name">{=TAOM_hidden_hand_short}Servants</xsl:attribute>
      <xsl:attribute name="text">{=TAOM_hidden_hand_text}The Mouth's Servants are the Eyes of Barad-dûr operating beyond the Black Gate — informants, enforcers, and spies embedded among the peoples bordering Mordor. Where Sauron's armies cannot yet reach, these agents collect tribute and silence dissent. They work through bribery, blackmail, and quiet murder, ensuring that no conspiracy against the Dark Lord takes root before it is uprooted and crushed.</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <!-- Lake Rats → Wreckers of the Long Lake (Esgaroth criminals) -->
  <xsl:template match="Faction[@id='lakepike']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'short_name' and local-name() != 'text']"/>
      <xsl:attribute name="name">{=TAOM_lakepike_name}Wreckers of the Long Lake</xsl:attribute>
      <xsl:attribute name="short_name">{=TAOM_lakepike_short}Wreckers</xsl:attribute>
      <xsl:attribute name="text">{=TAOM_lakepike_text}The Wreckers of the Long Lake live in the reed-beds and rotting old jetties of the Long Lake's shadowed shores, far from the respectable lights of Esgaroth. They make their living by luring barges onto hidden shoals with false lanterns, then stripping the cargo and selling it through fences in the town itself. Despite the efforts of the Lake-town Master and the Dale merchants, the Wreckers have too many friends in the dockside taverns to be easily driven out.</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <!-- ==================== SECT MINOR FACTION ==================== -->

  <!-- Embers of the Flame → Cult of the Lidless Eye (Black Númenórean sect) -->
  <xsl:template match="Faction[@id='embers_of_flame']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'short_name' and local-name() != 'text' and local-name() != 'initial_home_settlement']"/>
      <xsl:attribute name="name">{=TAOM_embers_name}Cult of the Lidless Eye</xsl:attribute>
      <xsl:attribute name="short_name">{=TAOM_embers_short}Eye Cult</xsl:attribute>
      <xsl:attribute name="text">{=TAOM_embers_text}The Cult of the Lidless Eye is a secret society rooted in the Black Númenórean tradition, venerating Sauron as a god-king who will restore his faithful to glory when he rises again. They recruit among the dispossessed of Dunland and the forgotten corners of the realm, promising divine favour and earthly power. In truth, their leaders answer to agents of Barad-dûr, and their cell-based structure makes them nearly impossible for any lord's justice to eradicate.</xsl:attribute>
      <xsl:attribute name="initial_home_settlement">Settlement.castle_EN5</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <!-- ==================== NOMAD MINOR FACTIONS ==================== -->

  <!-- Jawwal → The Sand-Riders (Haradrim desert nomads) -->
  <xsl:template match="Faction[@id='jawwal']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'short_name' and local-name() != 'text']"/>
      <xsl:attribute name="name">{=TAOM_jawwal_name}The Sand-Riders</xsl:attribute>
      <xsl:attribute name="short_name">{=TAOM_jawwal_short}Sand-Riders</xsl:attribute>
      <xsl:attribute name="text">{=TAOM_jawwal_text}The Sand-Riders are the nomadic desert clans of the deep Harad interior, masters of the waterless wastes between the Ephel Dúath and the distant southern sea. They range across routes known only to themselves, demanding tribute from any caravan bold enough to cross their territory. The settled lords of Near Harad treat them as a nuisance; the Sand-Riders consider the settled lords thieves who stole what the desert had already given freely.</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <!-- Karakhergit → Wild Easterlings (Rhun steppe nomads) -->
  <xsl:template match="Faction[@id='karakhuzaits']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'short_name' and local-name() != 'text' and local-name() != 'initial_home_settlement']"/>
      <xsl:attribute name="name">{=TAOM_karakhuzaits_name}The Wild Easterlings</xsl:attribute>
      <xsl:attribute name="short_name">{=TAOM_karakhuzaits_short}Wild Ones</xsl:attribute>
      <xsl:attribute name="text">{=TAOM_karakhuzaits_text}The Wild Easterlings are the unconquered steppe clans of the Sea of Rhûn, who have resisted the authority of every lord who has tried to bring them to heel — Rhûn chieftain, Mordor lieutenant, and King of Dale alike. They range across the grasslands east of the Celduin, rounding up horses, raiding flocks, and collecting road tribute from any caravan foolish enough to cross their steppe without an armed escort.</xsl:attribute>
      <xsl:attribute name="initial_home_settlement">Settlement.castle_RU10</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <!-- Forest People → The Drúedain / Woses (ancient forest dwellers) -->
  <xsl:template match="Faction[@id='forest_people']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'short_name' and local-name() != 'text']"/>
      <xsl:attribute name="name">{=TAOM_forest_people_name}The Drúedain</xsl:attribute>
      <xsl:attribute name="short_name">{=TAOM_forest_people_short}Woses</xsl:attribute>
      <xsl:attribute name="text">{=TAOM_forest_people_text}The Drúedain, known to Men as Woses or the Wild Men of the Woods, are an ancient people who dwell in the deep forest of Drúadan and the hidden valleys of the White Mountains. They clear ground with fire, grow what they need on the ash-rich soil, and move on when the land is spent — a way of life the settled peoples of Middle-earth have never understood and never bothered to. The Drúedain want no lord, pay no tax, and ask only to be left to the forest's silence.</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <!-- Eleftheroi → The Beornings (skin-changers of the Anduin vale) -->
  <xsl:template match="Faction[@id='eleftheroi']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'short_name' and local-name() != 'text' and local-name() != 'initial_home_settlement']"/>
      <xsl:attribute name="name">{=TAOM_eleftheroi_name}The Beornings</xsl:attribute>
      <xsl:attribute name="short_name">{=TAOM_eleftheroi_short}Beornings</xsl:attribute>
      <xsl:attribute name="text">{=TAOM_eleftheroi_text}The Beornings are the great woodmen who dwell in the vale of the Anduin between the Misty Mountains and Mirkwood, led by the skin-changers of Beorn's line. They allow no Orc to cross their territory, charge a toll at the Ford of Carrock to fund their watch, and answer to no king in Middle-earth. Though they will trade and talk with Elves and Dwarves, any army that marches through their forest without leave will find that the Beornings have very large teeth.</xsl:attribute>
      <xsl:attribute name="initial_home_settlement">Settlement.castle_M1</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

  <!-- ==================== RHUN CLANS (Khuzait) ==================== -->
  <xsl:template match="Faction[@id='clan_khuzait_1']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name']"/>
      <xsl:attribute name="name">{=TAOM_clan_khuzait_1}Hûz</xsl:attribute>
      <xsl:attribute name="banner_key">11.2014.166.1528.1528.764.764.1.0.0.14010.131.116.700.700.765.765.0.0.90</xsl:attribute>
      <xsl:attribute name="tier">6</xsl:attribute>
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
      <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'initial_home_settlement']"/>
      <xsl:attribute name="name">{=TAOM_clan_khuzait_5}Amdûrid</xsl:attribute>
      <xsl:attribute name="initial_home_settlement">Settlement.castle_K6</xsl:attribute>
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
      <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'initial_home_settlement']"/>
      <xsl:attribute name="name">{=TAOM_clan_khuzait_9}Bozorganith</xsl:attribute>
      <xsl:attribute name="initial_home_settlement">Settlement.castle_K7</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>

</xsl:stylesheet>
