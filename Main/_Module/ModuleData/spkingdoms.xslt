<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
  <!-- Identity transformation - copies everything by default -->
  <xsl:output omit-xml-declaration="no" indent="yes"/>

  <xsl:template match="@*|node()">
    <xsl:copy>
      <xsl:apply-templates select="@*|node()"/>
    </xsl:copy>
  </xsl:template>

  <!-- Replace empire with Dunland -->
  <xsl:template match="Kingdom[@id='empire']">
    <Kingdom
			id="empire"
			initial_home_settlement="Settlement.town_EN2"
			owner="Hero.lord_1_1"
			banner_key="11.181.166.1528.1528.764.764.1.0.0.26004.2000.171.650.650.765.800.0.0.0"
			primary_banner_color="0xff793191"
			secondary_banner_color="0xffFCDE90"
			color="FF5A3D28"
			color2="FF8C3A2C"
			culture="Culture.empire"
			settlement_banner_mesh="encounter_flag_a"
			flag_mesh="info_screen_flags_b"
			name="{{=TAOM_dunland}}Dunland"
			short_name="{{=TAOM_dunland_short}}Dunland"
			title="{{=TAOM_dunland_title}}Clans of Dunland"
			ruler_title="{{=TAOM_dunland_ruler}}Brenin"
			text="{{=TAOM_dunland_desc}}Dunland, a rugged and untamed land of rolling hills and dense forests, is home to the fierce and independent Dunlendings. Often marginalized and at odds with their neighbors, the people of Dunland are hardened by centuries of struggle and survival. Their chieftains lead through strength and cunning, uniting clans under a shared banner of vengeance and freedom. Known for their ferocious warriors and skilled hunters, the Dunlendings favor axes, spears, and bows, fighting with a tenacity born from their deep connection to their ancestral lands. Though their ways may seem primitive to others, the Dunlendings are proud and resourceful, ready to reclaim their rightful place in Middle-earth.">
      <relationships>
        <relationship
					kingdom="Kingdom.khuzait"
					value="-1"
					isAtWar="true" />
      </relationships>
      <policies>
        <policy
					id="policy_feudal_inheritance" />
      </policies>
    </Kingdom>
  </xsl:template>

  <!-- Replace empire_w with Gondor -->
  <xsl:template match="Kingdom[@id='empire_w']">
    <Kingdom
			id="empire_w"
			initial_home_settlement="Settlement.town_EW2"
			owner="Hero.lord_1_7"
			banner_key="11.149.149.1528.1528.764.764.1.0.0.10000.172.2000.580.580.765.825.0.0.0"
			primary_banner_color="0xff591645"
			secondary_banner_color="0xffFFAD54"
			color="FFB4B9C2"
			color2="FF1C1C22"
			culture="Culture.gondor"
			settlement_banner_mesh="encounter_flag_a"
			flag_mesh="info_screen_flags_a"
			name="{{=TAOM_gondor}}Gondor"
			short_name="{{=TAOM_gondor_short}}Gondor"
			title="{{=TAOM_gondor_title}}Stewardship of Gondor"
			ruler_title="{{=TAOM_gondor_ruler}}Steward"
			text="{{=TAOM_gondor_desc}}Gondor stands as a proud bastion of strength and resilience in Middle-earth, known for its towering white stone cities, ancient lineage, and skilled armies. Founded by Elendil and his sons after the fall of Numenor, Gondor has long guarded the realms of men against the growing darkness in the east. With Minas Tirith as its capital, the kingdom is renowned for its valor, discipline, and loyalty to the Free Peoples. While beset by years of conflict with Mordor, Gondor's warriors and Stewards have kept hope alive, awaiting the return of the king to restore its former glory.">
      <relationships>
        <relationship
					kingdom="Kingdom.battania"
					value="-1"
					isAtWar="true" />
      </relationships>
      <policies>
        <policy
					id="policy_land_grants_for_veterans" />
      </policies>
    </Kingdom>
  </xsl:template>

  <!-- Replace empire_s with Mordor -->
  <xsl:template match="Kingdom[@id='empire_s']">
    <Kingdom
			id="empire_s"
			initial_home_settlement="Settlement.town_ES4"
			owner="Hero.lord_1_17"
			banner_key="11.2000.166.1528.1528.764.764.1.0.0.19015.142.116.800.800.759.755.1.0.0"
			primary_banner_color="0xff382188"
			secondary_banner_color="0xffDEA940"
			color="FF1A1717"
			color2="FF7E1518"
			culture="Culture.mordor"
			settlement_banner_mesh="encounter_flag_a"
			flag_mesh="info_screen_flags_b"
			name="{{=TAOM_mordor}}Mordor"
			short_name="{{=TAOM_mordor_short}}Mordor"
			title="{{=TAOM_mordor_title}}Dominion of Mordor"
			ruler_title="{{=TAOM_mordor_ruler}}Dark Lord"
			text="{{=TAOM_mordor_desc}}Mordor is a dark, desolate realm forged in shadow and ruled by the Dark Lord Sauron. Nestled behind the towering black mountains, this kingdom brims with orc armies, ruthless trolls, and fiery forges crafting weapons of war. From his fortress in Barad-dur, Sauron commands an unstoppable force, poised to spread his dominion across Middle-earth. Mordor's skies are clouded with ash, and its lands are barren, a fitting domain for Sauron's twisted ambition and dark magic.">
      <relationships>
        <relationship
					kingdom="Kingdom.aserai"
					value="-1"
					isAtWar="true" />
      </relationships>
      <policies>
        <policy
					id="policy_royal_privilege" />
      </policies>
    </Kingdom>
  </xsl:template>

  <!-- Replace sturgia with Dale -->
  <xsl:template match="Kingdom[@id='sturgia']">
    <Kingdom
			id="sturgia"
			initial_home_settlement="Settlement.town_S1"
			owner="Hero.lord_2_1"
			banner_key="11.312.166.1528.1528.764.764.1.0.0.27000.238.171.900.900.765.790.0.0.0"
			primary_banner_color="0xff224277"
			secondary_banner_color="0xffCEDAE7"
			color="FF1E3A6E"
			color2="FFD4A53A"
			culture="Culture.sturgia"
			settlement_banner_mesh="encounter_flag_b"
			flag_mesh="info_screen_flags_c"
			name="{{=TAOM_dale}}Dale"
			short_name="{{=TAOM_dale_short}}Dale"
			title="{{=TAOM_dale_title}}Kingdom of Dale"
			ruler_title="{{=TAOM_dale_ruler}}King"
			text="{{=TAOM_dale_desc}}Where the waters of the Running River meet the shadow of the Lonely Mountain, stands Dale. The City of Men had been rebuilt from ruin in the aftermath of the Battle of the Five Armies. Once a place of music and merriment, of trade and golden market halls, Dale prospered under the protection of Erebor. Its merchants sailed swift boats down the Celduin, its streets rang with laughter, and its people knew peace. But peace was shattered when the dragon came. Smaug, the winged death from the North, descended upon Erebor in flame and fury, and Dale was the first to suffer. The city burned, her people fled, and for nearly two centuries, the proud, domed towers stood silent and broken, a monument to sorrow. Yet ruin is not the end of Dale's tale. With the death of the dragon, Bard the Bowman raised his banner once more and called his people home. From ash and stone rose Dale again, not as it once was, but greater. Under King Bard and his son Bain, the city thrives anew, her walls strong, her people proud, and her gaze ever vigilant. The Men of Dale are not mighty in number, nor ancient in magic, but they are steadfast and true.">
      <relationships>
        <relationship
					kingdom="Kingdom.vlandia"
					value="-1"
					isAtWar="true" />
      </relationships>
    </Kingdom>
  </xsl:template>

  <!-- Replace aserai with Harad -->
  <xsl:template match="Kingdom[@id='aserai']">
    <Kingdom
			id="aserai"
			initial_home_settlement="Settlement.town_A1"
			owner="Hero.lord_3_1"
			banner_key="11.149.166.1528.1528.764.764.1.0.0.504.142.171.813.2468.764.599.0.0.178.504.116.171.771.2278.764.599.0.0.178.504.142.171.733.2225.764.599.0.0.178.505.142.171.813.2468.205.600.0.0.0.505.116.171.813.2468.196.600.0.0.0.505.142.171.813.2468.189.600.0.0.0.505.142.171.813.2468.1322.600.0.1.0.505.116.171.813.2468.1331.600.0.1.0.505.142.171.813.2468.1338.600.0.1.0.503.116.171.133.133.763.512.0.1.0.503.142.171.118.118.763.512.0.1.0.504.142.171.49.49.744.564.0.1.314.504.142.171.49.49.783.564.0.0.44.503.116.171.33.71.763.559.0.1.0.503.116.171.106.106.763.512.0.1.0.503.142.171.84.84.763.512.0.1.0.503.116.171.71.71.763.512.0.1.0.503.142.171.49.49.763.512.0.1.0.503.116.171.21.21.763.604.0.1.0.402.116.171.48.48.501.1028.0.1.89.402.116.171.48.48.501.959.0.1.89.402.116.171.48.48.501.892.0.1.89.402.116.171.48.48.501.824.0.1.89.402.116.171.48.48.501.754.0.1.89.402.116.171.48.48.501.690.0.1.89.402.116.171.48.48.501.621.0.1.89.402.116.171.48.48.501.552.0.1.89.402.116.171.48.48.501.486.0.1.89.402.116.171.48.48.501.417.0.1.89.402.116.171.48.48.501.354.0.1.89.402.116.171.48.48.1025.1028.0.1.89.402.116.171.48.48.1025.959.0.1.89.402.116.171.48.48.1025.892.0.1.89.402.116.171.48.48.1025.824.0.1.89.402.116.171.48.48.1025.754.0.1.89.402.116.171.48.48.1025.690.0.1.89.402.116.171.48.48.1025.621.0.1.89.402.116.171.48.48.1025.552.0.1.89.402.116.171.48.48.1025.486.0.1.89.402.116.171.48.48.1025.417.0.1.89.402.116.171.48.48.1025.354.0.1.89.505.116.171.813.2468.109.600.0.0.0.505.142.171.813.2468.103.600.0.0.0.505.116.171.813.2468.1418.600.0.1.0.505.142.171.813.2468.1424.600.0.1.0.504.116.171.26.139.764.408.0.1.0.504.116.171.26.139.681.574.0.1.127.504.116.171.26.139.847.574.0.0.230.504.116.171.26.139.866.521.0.0.269.504.116.171.26.139.662.521.0.1.89.504.116.171.26.139.862.473.0.0.285.504.116.171.26.139.821.423.0.0.327.504.116.171.26.139.707.423.0.1.30.504.116.171.26.139.666.473.0.1.71.13000.2000.116.300.300.755.797.0.0.0"
			primary_banner_color="0xffB57A1E"
			secondary_banner_color="0xff4E1A13"
			color="FF8E1C1C"
			color2="FFC9A227"
			culture="Culture.aserai"
			settlement_banner_mesh="encounter_flag_c"
			flag_mesh="info_screen_flags_a"
			name="{{=TAOM_harad}}Harad"
			short_name="{{=TAOM_harad_short}}Harad"
			title="{{=TAOM_harad_title}}Taskralan of Harwan"
			ruler_title="{{=TAOM_harad_ruler}}Taskral"
			text="{{=TAOM_harad_desc}}Harad, a vast and diverse region south of Gondor, encompasses arid deserts, lush jungles, and fertile oases. Its geography is as varied as its people, with sprawling dunes giving way to dense forests and rivers teeming with life. Harad's history is steeped in ancient lore, with grand cities and fortresses dotting the landscape, built by rulers whose legacies echo through the ages. The Great Desert of Haradwaith separates its northern frontier from the heartland, while its southern coasts are bustling with trade and piracy. United under the banners of Sauron, Harad's many tribes and kingdoms channel their deep-rooted traditions into their war efforts, transforming this vibrant land into a powerhouse of culture and conflict.">
      <relationships>
        <relationship
					kingdom="Kingdom.empire_s"
					value="-1"
					isAtWar="true" />
      </relationships>
    </Kingdom>
  </xsl:template>

  <!-- Replace vlandia with Rohan -->
  <xsl:template match="Kingdom[@id='vlandia']">
    <Kingdom
			id="vlandia"
			initial_home_settlement="Settlement.town_V1"
			owner="Hero.lord_4_1"
			banner_key="11.157.166.1528.1528.764.764.1.0.0.21004.172.36.550.550.780.765.1.0.0"
			primary_banner_color="0xff8D291A"
			secondary_banner_color="0xffF7BF46"
			color="FF35632F"
			color2="FFE0D6A8"
			culture="Culture.vlandia"
			settlement_banner_mesh="encounter_flag_d"
			flag_mesh="info_screen_flags_c"
			name="{{=TAOM_rohan}}Rohan"
			short_name="{{=TAOM_rohan_short}}Rohan"
			title="{{=TAOM_rohan_title}}Kingdom of Rohan"
			ruler_title="{{=TAOM_rohan_ruler}}King"
			text="{{=TAOM_rohan_desc}}Known as the Riddermark, Rohan is a vast and sweeping kingdom of open plains and rolling hills, where the mighty Rohirrim—skilled horsemen and fierce warriors—dwell. Founded by Eorl the Young after his legendary ride to Gondor's aid, Rohan has long stood as a steadfast ally of Gondor against the darkness of Mordor. From the golden halls of Meduseld in Edoras, its kings rule over a proud and resilient people who live by the bond of horse and rider. The Rohirrim are famed for their cavalry charges, their songs of valor, and their unwavering courage. Though the shadow of Saruman has threatened their borders, the sons and daughters of Rohan remain ever ready to answer the call to arms, defending their homeland and the free peoples of Middle-earth.">
      <relationships>
        <relationship
					kingdom="Kingdom.sturgia"
					value="-1"
					isAtWar="true" />
      </relationships>
    </Kingdom>
  </xsl:template>

  <!-- Replace battania with Khand -->
  <xsl:template match="Kingdom[@id='battania']">
    <Kingdom
			id="battania"
			initial_home_settlement="Settlement.town_K1"
			owner="Hero.lord_5_1"
			banner_key="11.14.166.1528.1528.764.764.1.0.0.503.131.171.585.585.764.764.0.0.0.503.148.171.566.566.764.764.0.0.43.512.131.171.26.34.764.496.0.0.0.512.131.171.26.34.1031.764.0.0.89.512.131.171.26.34.1030.740.0.0.89.512.131.171.26.34.1025.714.0.0.97.512.131.171.26.34.1019.689.0.0.101.512.131.171.26.34.1014.666.0.0.107.512.131.171.26.34.1004.644.0.0.112.512.131.171.26.34.788.497.0.0.0.512.131.171.26.34.811.498.0.0.-9.512.131.171.26.34.832.504.0.0.-12.512.131.171.26.34.853.511.0.0.-18.512.131.171.26.34.874.521.0.0.-24.512.131.171.26.34.895.532.0.0.-29.512.131.171.26.34.914.545.0.0.-34.512.131.171.26.34.932.559.0.0.-40.512.131.171.26.34.993.622.0.0.121.512.131.171.26.34.979.602.0.0.127.512.131.171.26.34.965.585.0.0.132.512.131.171.26.34.949.572.0.0.135.512.131.171.26.34.764.496.0.1.0.512.131.171.26.34.497.764.0.1.269.512.131.171.26.34.498.740.0.1.269.512.131.171.26.34.503.714.0.1.260.512.131.171.26.34.509.689.0.1.256.512.131.171.26.34.514.666.0.1.250.512.131.171.26.34.524.644.0.1.245.512.131.171.26.34.740.497.0.1.0.512.131.171.26.34.717.498.0.1.9.512.131.171.26.34.696.504.0.1.12.512.131.171.26.34.675.511.0.1.18.512.131.171.26.34.654.521.0.1.22.512.131.171.26.34.633.532.0.1.29.512.131.171.26.34.614.545.0.1.33.512.131.171.26.34.596.559.0.1.39.512.131.171.26.34.535.622.0.1.235.512.131.171.26.34.549.602.0.1.230.512.131.171.26.34.563.585.0.1.225.512.131.171.26.34.579.572.0.1.222.512.131.171.26.34.764.1032.0.0.179.512.131.171.26.34.497.764.0.0.-89.512.131.171.26.34.498.788.0.0.-89.512.131.171.26.34.503.814.0.0.-81.512.131.171.26.34.509.839.0.0.-77.512.131.171.26.34.514.862.0.0.-71.512.131.171.26.34.524.884.0.0.-66.512.131.171.26.34.740.1031.0.0.179.512.131.171.26.34.717.1030.0.0.169.512.131.171.26.34.696.1024.0.0.166.512.131.171.26.34.675.1017.0.0.160.512.131.171.26.34.654.1007.0.0.154.512.131.171.26.34.633.996.0.0.148.512.131.171.26.34.614.983.0.0.144.512.131.171.26.34.596.969.0.0.138.512.131.171.26.34.535.906.0.0.-56.512.131.171.26.34.549.926.0.0.-50.512.131.171.26.34.563.943.0.0.-45.512.131.171.26.34.579.956.0.0.-43.512.131.171.26.34.764.1032.0.1.179.512.131.171.26.34.1031.764.0.1.89.512.131.171.26.34.1030.788.0.1.89.512.131.171.26.34.1025.814.0.1.80.512.131.171.26.34.1019.839.0.1.76.512.131.171.26.34.1014.862.0.1.70.512.131.171.26.34.1004.884.0.1.65.512.131.171.26.34.788.1031.0.1.179.512.131.171.26.34.811.1030.0.1.189.512.131.171.26.34.832.1024.0.1.192.512.131.171.26.34.853.1017.0.1.198.512.131.171.26.34.874.1007.0.1.203.512.131.171.26.34.895.996.0.1.209.512.131.171.26.34.914.983.0.1.213.512.131.171.26.34.932.969.0.1.219.512.131.171.26.34.993.906.0.1.56.512.131.171.26.34.979.926.0.1.50.512.131.171.26.34.965.943.0.1.45.512.131.171.26.34.949.956.0.1.42.13003.238.116.610.619.765.775.1.0.10.13003.230.116.610.610.765.775.1.0.0.13003.31.116.450.450.765.775.1.1.0"
			primary_banner_color="0xff284E19"
			secondary_banner_color="0xffB4F0F1"
			color="FF8A5A1E"
			color2="FF5A1E18"
			culture="Culture.battania"
			settlement_banner_mesh="encounter_flag_e"
			flag_mesh="info_screen_flags_b"
			name="{{=TAOM_khand}}Khand"
			short_name="{{=TAOM_khand_short}}Khand"
			title="{{=TAOM_khand_title}}Windriders of Khand"
			ruler_title="{{=TAOM_khand_ruler}}Khudriag"
			text="{{=TAOM_khand_desc}}Khand, a rugged land of rolling steppes and arid plains, lies southeast of Mordor and serves as a critical corridor between the east and west. Its people, the Variags, are fierce and nomadic, known for their swift horses and deadly curved blades. Living in a harsh environment, they have honed their skills as raiders and warriors, often clashing with their neighbors and serving as mercenaries for the highest bidder. Khand's allegiance has long been courted by the Dark Lord, and many of its warriors have ridden to war under the shadow of Mordor. Yet the Variags are a proud and independent people, loyal first to their clans and chieftains, who rule from fortified encampments scattered across the windswept plains.">
      <relationships>
        <relationship
					kingdom="Kingdom.empire_w"
					value="-1"
					isAtWar="true" />
      </relationships>
    </Kingdom>
  </xsl:template>

  <!-- Replace khuzait with Rhun -->
  <xsl:template match="Kingdom[@id='khuzait']">
    <Kingdom
			id="khuzait"
			initial_home_settlement="Settlement.town_RU1"
			owner="Hero.lord_6_1"
			banner_key="11.2014.166.1528.1528.764.764.1.0.0.14010.131.116.700.700.765.765.0.0.90"
			primary_banner_color="0xff429081"
			secondary_banner_color="0xffEFC990"
			color="FFC0962E"
			color2="FF5A1A14"
			culture="Culture.khuzait"
			settlement_banner_mesh="encounter_flag_f"
			flag_mesh="info_screen_flags_b"
			name="{{=TAOM_rhun}}Rhun"
			short_name="{{=TAOM_rhun_short}}Rhun"
			title="{{=TAOM_rhun_title}}Golden Realm of Rhûn"
			ruler_title="{{=TAOM_rhun_ruler}}Loke-Kan"
			text="{{=TAOM_rhun_desc}}Far beyond the Sea of Rhûn, past the withered steppes and the red dust of forgotten empires, lies the vast and veiled East. Here dwell the Easterlings, a proud and ancient people whose history predates even the rise of Gondor. Their lands stretch endlessly beneath wide skies, from the shores of the inland sea to the shadowed mountains that mark the edge of the known world. The Easterlings are no single nation, but a tapestry of tribes and kingdoms, united only in their reverence for strength and their long memory of grievance against the West. Some have served the Dark Lord willingly, drawn by promises of power and vengeance; others resist, clinging to old ways and older gods. Their warriors are feared for their discipline and their mastery of chariot and bow, and their golden armor gleams like fire beneath the eastern sun.">
      <relationships>
        <relationship
					kingdom="Kingdom.empire"
					value="-1"
					isAtWar="true" />
      </relationships>
    </Kingdom>
  </xsl:template>
</xsl:stylesheet>
