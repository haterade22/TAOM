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
			banner_key="11.4.4.4345.4345.764.764.1.0.0.163.5.5.512.512.764.764.1.0.0"
			primary_banner_color="0xff793191"
			secondary_banner_color="0xffFCDE90"
			color="FF39223F"
			color2="FFDE9953"
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
			banner_key="11.8.8.4345.4345.764.764.1.0.0.164.9.9.512.512.764.764.1.0.0"
			primary_banner_color="0xff591645"
			secondary_banner_color="0xffFFAD54"
			color="FF9E5072"
			color2="FFDE9953"
			culture="Culture.empire"
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
			owner="Hero.lord_1_14"
			banner_key="11.6.6.4345.4345.764.764.1.0.0.162.7.7.512.512.764.764.1.0.0"
			primary_banner_color="0xff382188"
			secondary_banner_color="0xffDEA940"
			color="FF9382D0"
			color2="FFDE9953"
			culture="Culture.empire"
			settlement_banner_mesh="encounter_flag_a"
			flag_mesh="info_screen_flags_b"
			name="{{=TAOM_mordor}}Mordor"
			short_name="{{=TAOM_mordor_short}}Mordor"
			title="{{=TAOM_mordor_title}}Dominion of Mordor"
			ruler_title="{{=TAOM_mordor_ruler}}Mouth of Sauron"
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
			banner_key="11.12.12.4345.4345.764.764.1.0.0.462.13.13.512.512.764.764.1.0.0"
			primary_banner_color="0xff224277"
			secondary_banner_color="0xffCEDAE7"
			color="FF1C2A50"
			color2="FF949CCC"
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
			banner_key="11.0.0.4345.4345.764.764.1.0.0.463.1.1.466.466.764.764.1.0.0"
			primary_banner_color="0xffB57A1E"
			secondary_banner_color="0xff4E1A13"
			color="FF965228"
			color2="FF4F2212"
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
			banner_key="11.14.14.1536.1536.764.764.1.0.0.160.15.15.497.497.764.764.1.0.0"
			primary_banner_color="0xff8D291A"
			secondary_banner_color="0xffF7BF46"
			color="FF5C2017"
			color2="FFECBA44"
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
			initial_home_settlement="Settlement.town_B1"
			owner="Hero.lord_5_1"
			banner_key="11.2.2.6296.6296.764.764.1.0.0.460.3.3.457.457.764.764.1.0.0"
			primary_banner_color="0xff284E19"
			secondary_banner_color="0xffB4F0F1"
			color="FF2D3F1D"
			color2="FFBFCBB0"
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
			initial_home_settlement="Settlement.town_K3"
			owner="Hero.lord_6_1"
			banner_key="11.10.10.1536.1536.764.764.1.0.0.461.11.11.497.497.764.764.1.0.0"
			primary_banner_color="0xff429081"
			secondary_banner_color="0xffEFC990"
			color="FF468C7C"
			color2="FFCCBB89"
			culture="Culture.khuzait"
			settlement_banner_mesh="encounter_flag_f"
			flag_mesh="info_screen_flags_b"
			name="{{=TAOM_rhun}}Rhun"
			short_name="{{=TAOM_rhun_short}}Rhun"
			title="{{=TAOM_rhun_title}}Golden Realm of Rhun"
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
