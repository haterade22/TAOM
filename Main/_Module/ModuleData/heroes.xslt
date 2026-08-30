<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
	<!-- Identity transformation - copies everything by default -->
	<xsl:output omit-xml-declaration="no" indent="yes"/>

	<xsl:template match="@*|node()">
		<xsl:copy>
			<xsl:apply-templates select="@*|node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- ======================= DEAD LORDS ======================= -->

	<xsl:template match="Hero[@id='dead_lord_2_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_dead_2_1}Olek the Old fell defending Dale against Easterling raiders. His sacrifice secured the mountain passes and is remembered in songs throughout the kingdom.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='dead_lord_2_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father']"/>
			<xsl:attribute name="text">{=TAOM_hero_dead_2_2}Varra, a noblewoman of Dale, perished in the winter plague that followed the war. She was known for her charity to the common folk.</xsl:attribute>
			<xsl:attribute name="father">Hero.dead_lord_2_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='dead_lord_3_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_dead_3_1}Nimr was a Haradrim warlord who led the desert cavalry at the Battle of the Black Gate. He fell to Gondorian arrows but is revered as a martyr in the south.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='dead_lord_6_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_dead_6_1}Urkhun was the great Khan who first united the Easterling tribes under the banner of Rhun. His conquests pushed the borders of the Golden Realm to the shores of the Sea of Rhun.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='dead_lord_6_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_dead_6_2}Solun was a respected noyan who fell in battle against the Free Peoples. His widow Gûrtilm now leads their clan with iron determination.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='dead_lord_6_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_dead_6_3}Kuyug was chief noyan of the Arkit clan. His brilliance with horse archery shaped an entire generation of Easterling warriors.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='dead_lord_6_4']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father']"/>
			<xsl:attribute name="text">{=TAOM_hero_dead_6_4}Undul was a noble of the Arkit clan and son of the great noyan. He died young but showed promise as a warrior.</xsl:attribute>
			<xsl:attribute name="father">Hero.dead_lord_6_3</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- ======================= KINGDOM 1: DUNLAND (Empire North) ======================= -->

	<xsl:template match="Hero[@id='lord_1_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_1}Brenin Wulf rules the wild clans of Dunland with an iron fist. Known as the Ironhand, he united the scattered hill tribes through conquest and cunning. He dreams of reclaiming the lands taken by the Horse-lords of Rohan.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_2</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_2}Freya Wolfheart is the fierce wife of Brenin Wulf. She commands respect among the warriors for her skill with axe and shield.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_41']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_41}Beregund Wolfborn is a trusted captain of Dunland, known for his skill in raiding Rohirric settlements.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_1_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_411']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_411}Grimwulf Ironfang carries on his father's legacy of raiding and warfare against Rohan.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_1_41</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_31']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_31}Zrsa Blackfang is heir to the Ironhand, eager to prove herself worthy of her father's legacy.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_1_1</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_1_2</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_1_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_1_1}Eldith Grey-Claw is a fierce warrior woman of Dunland, renowned for her ferocity in battle.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_41</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_1_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_1_2}Sigga Wyrmbane earned her name slaying a great serpent in the mountain passes.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_21</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_21']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_21}Fenrik the Red Wolf leads raiding parties deep into enemy territory.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_1_1</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_1_2</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Empire North 2 -->
	<xsl:template match="Hero[@id='lord_1_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_3}Gorwulf the Boar is a savage chieftain known for his brutal raids on border settlements.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_4</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_4']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_4}Brunhild Ironclaw is the formidable wife of Gorwulf, equally skilled in battle as her husband.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_3</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_22']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_22}Bjornric Strongarm is a mighty warrior who wields a great war axe in battle.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_1_3</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_42']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_42}Hrodgar Ironhide serves as a champion of the clan, his armor scarred by countless battles.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_1_4</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_32']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_32}Eldra Boarsong carries on the fierce traditions of her parents.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_1_3</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_1_4</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_422']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_422}Drengulf Irontusk is a young warrior eager to prove his worth in battle.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_1_3</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_1_4</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_1_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_1_3}Sigrun Boarfang is known for her fearless charges into enemy ranks.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_22</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_1_4']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_1_4}Thyra Bloodtusk earned her epithet through countless victories.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_42</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_1_5']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_1_5}Hilda Bonecrusher is feared for her devastating strength in combat.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_1_3</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_1_4</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_1_6']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_1_6}Yrsa the Winter Boar leads raids during the harshest seasons when enemies least expect attack.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_1_3</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_1_4</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Empire North 3 -->
	<xsl:template match="Hero[@id='lord_1_5']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_5}Othric the Wild is a chieftain whose unpredictable tactics have won him many victories. His clan claims descent from ancient Dunlending kings.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_1_7</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_1_7']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_1_7}Freydis Oxmane is wife to Othric and a skilled healer as well as warrior.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_5</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_1_8']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_1_8}Eira Shadowclaw moves silently through enemy camps, striking without warning.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_1_5</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_1_1_7</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_6']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_6}Torvald Oxhorn is the heir of Othric, already showing his father's wild spirit.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_1_5</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_1_1_7</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_33']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_33}Brigid the Howling leads war bands through the mountain passes.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_1_5</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_1_1_7</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_43']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_43}Gormund Oxflank serves as a trusted lieutenant to the clan chief.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_1_9</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_1_9']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_1_9}Sifra Bonewalker walks among the fallen after battle, collecting trophies from the slain.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_43</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_1_10']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_1_10}Haldis Redmist leaves a trail of blood wherever she rides into battle.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_1_43</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_1_1_9</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_1_11']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_1_11}Yrla Ghostpelt wears the skin of a white wolf, said to grant her protection in battle.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_1_43</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_1_1_9</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Empire North 4 -->
	<xsl:template match="Hero[@id='lord_1_1_12']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_1_12}Freya Clawrend leads a warband of fierce shield-maidens into battle. She trained as a warrior from childhood and has never known defeat.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_1_13']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_1_13}Gundrun Ironpaw is a veteran warrior with hands scarred by countless sword grips.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_64</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_20']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_20}Astrid Bearclaw is known for her ferocious fighting style and bear-skin cloak.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_64']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_64}Thormund Grizzlyhew cleaves through enemy ranks with his great two-handed axe.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_1_13</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Empire North 5 -->
	<xsl:template match="Hero[@id='lord_1_1_14']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_1_14}Morgith is a mysterious figure who communes with the spirits of the ancient hills.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_50</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_1_17']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_1_17}Branoc Feathershaft is the finest archer among the Dunlendings, able to shoot the eye out of a raven at a hundred paces.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_50']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_50}Corwyn Raveneye sees omens in the flight of birds and counsels the clan on matters of war.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_1_14</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_66']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_66}Talric Crowcall can mimic any bird's cry, using them as signals in battle.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Empire North 6 -->
	<xsl:template match="Hero[@id='lord_1_1_15']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_1_15}Aelwyn Hawkeye commands a company of hill scouts who patrol the borders.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_67</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_1_16']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_1_16}Brianna Wingdart learned archery from her mother and now surpasses her in skill.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_1_67</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_1_1_15</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_51']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_51}Haldric Talonstrike leads ambushes from the high crags.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_67']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_67}Eorwyn Featherbolt prefers the bow to the blade, striking from the shadows.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_1_15</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Empire North 7 -->
	<xsl:template match="Hero[@id='lord_1_58']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_58}Gorvin the Fell is a grim chieftain whose cruelty is legendary even among Dunlendings.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_70']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_70}Veyra the Shadow slips unseen into enemy camps to slit throats in the night.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_NE7_u']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_NE7_u}Haldric the Black Fang advises his clan's chieftain, and earned the name before he was twenty.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Empire North 8 -->
	<xsl:template match="Hero[@id='lord_NE8_l']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_NE8_l}Eadric the Swift is chieftain of a mountain clan, hardened young by border warfare.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_NE8_s</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_NE8_s']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_NE8_s}Aeloria is wife to the chieftain, and skilled in the healing arts and herb lore.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_NE8_l</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_NE8_c1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_NE8_c1}Cerdic, called the Anvil, is firstborn of the clan and has trained for war since childhood.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_NE8_l</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_NE8_s</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_NE8_c2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_NE8_c2}Morwen is second child of the clan leader, known already for cunning and ambition.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_NE8_l</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_NE8_s</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_56']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_56}Tormund the Hammer earned his name through devastating charges with his war hammer.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_56_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_56_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_56_1}Gwenna is wife to Tormund, managing the clan's holdings while he campaigns.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_56</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Empire North 9 -->
	<xsl:template match="Hero[@id='lord_NE9_l']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_NE9_l}Valthor Scalecleaver is a chieftain whose counsel is sought across the Dunlending clans.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_NE9_s']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father']"/>
			<xsl:attribute name="text">{=TAOM_hero_NE9_s}Rurik Fanghew is son of the old chieftain, and eager to prove himself in battle.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_NE9_l</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_NE9_d']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father']"/>
			<xsl:attribute name="text">{=TAOM_hero_NE9_d}Sylva Serpent-Strike is a daughter of the clan, trained in both combat and diplomacy.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_NE9_l</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- ======================= KINGDOM 1: GONDOR (Empire West) ======================= -->

	<xsl:template match="Hero[@id='lord_1_7']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_7}Denethor II, Steward of Gondor, rules from the White Tower of Minas Tirith. Once a man of great wisdom, he has grown proud and unyielding. He resists the return of the king, believing only he can save Gondor from the shadow of Mordor.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_75']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_75}Boromir, eldest son of Denethor, is Captain-General of Gondor. A great warrior of renown, he seeks any weapon to defend his people against the darkness.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_1_7</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_8']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_8}Húrioneth serves the House of Húrinionath, a steadfast keeper of the lore of Gondor's stewards.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_34']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'faction']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_34}Faramir, younger son of Denethor, is Captain of the Rangers of Ithilien. Though overlooked by his father, he is beloved by his men for his wisdom, mercy, and quiet strength.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_1_7</xsl:attribute>
			<xsl:attribute name="faction">Faction.clan_empire_west_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_24']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother' and local-name() != 'faction']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_24}Amrothos, youngest son of Imrahil, is still young but already shows his family's martial spirit and noble character.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_1_9</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_1_9_5</xsl:attribute>
			<xsl:attribute name="faction">Faction.clan_empire_west_2</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_44']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_44}Nemos is a captain of the Tower Guard, sworn to protect the Steward.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Empire West 2 -->
	<xsl:template match="Hero[@id='lord_1_9']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_9}Imrahil II, Prince of Dol Amroth, is the greatest knight of Gondor. His line carries Elvish blood, and he is said to be the fairest lord in all the realm. His Swan Knights are legendary throughout Middle-earth.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_9_5</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_9_5']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse' and local-name() != 'faction']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_9_5}Lothwen, Princess of Dol Amroth, has stood beside Imrahil through decades of service to Gondor. She is known for her grace and quiet wisdom.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_9</xsl:attribute>
			<xsl:attribute name="faction">Faction.clan_empire_west_2</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_10']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_10}Aranel, wife of Elphir, is a noble lady of Gondor known for her dignity and keen mind.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_25</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_35']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_35}Erchirion, second son of Imrahil, serves as a knight of Dol Amroth. Bold in battle and loyal to his house, he rides with his father's Swan Knights.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_1_9</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_1_9_5</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_25']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_25}Elphir, eldest son of Imrahil, is heir to Dol Amroth. He commands the Swan Knights with martial precision and carries his father's noble bearing.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_1_9</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_1_9_5</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_23']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_23}Caladwen, wife of Erchirion, joined the house of Imrazôrionath through their union. She is a woman of quiet courage.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_35</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Empire West 3 -->
	<xsl:template match="Hero[@id='lord_1_11']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_11}Ciryandur commands the northern defenses of Gondor, a seasoned veteran of many orc raids. He is methodical and relentless in defending the borders of the realm.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_12</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_111']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_111}Elarwen, wife of Hirgon, is a woman of the borders known for her resilience and practical wisdom.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_26</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_12']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_12}Findariel, wife of Ciryandur, manages the affairs of their household with care and intelligence.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_11</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_36']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_36}Ivriniel, daughter of Ciryandur, has grown up in the northern marches of Gondor and shows both her father's resilience and her mother's wisdom.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_1_11</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_1_12</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_26']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_26}Hirgon, brother of Ciryandur, serves as an errand-rider of Gondor. Swift and reliable, he carries messages between the lords of the realm.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Empire West 4 -->
	<xsl:template match="Hero[@id='lord_1_40']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_40}Borhador, Lord of Barahirionath, is one of the eldest lords of Gondor. His long years have given him wisdom, though his body weakens with age. His counsel is respected by all.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_40_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_40_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_40_1}Lindariel, wife of Borhador, has been the strength of their house through many long years. She is a gracious and enduring presence in Gondor's nobility.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_40</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_46']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_46}Malrior, son of Borhador, carries on his father's duties as the old lord ages. He is steady and dependable, respected by his clan.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_1_40</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_1_40_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_46_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse' and local-name() != 'father']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_46_1}Thorwen, wife of Malrior, is a capable woman who manages her household with quiet determination.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_46</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Empire West 5 -->
	<xsl:template match="Hero[@id='lord_1_45']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_45}Forlong the Fat, Lord of Lossarnach, leads the largest contingent from the fiefs to the defense of Minas Tirith. Despite his girth, he is a fierce and experienced warrior.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_45_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_45_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_45_1}Berethiel, wife of Forlong, is the Lady of Lossarnach. She oversees the fertile valleys while her husband rides to war.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_45</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_45_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_45_2}Caldamir, young son of Forlong, has recently come of age and rides to war eager to prove himself.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_1_45</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_1_45_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_45_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_45_3}Rúmil, elder brother of Forlong, is a steady presence in the House of Ausirionath. His long years of service have earned him the respect of his kin.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_57']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_57}Baranor serves as a captain in the Lossarnach contingent, a trusted officer of Lord Forlong.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_57_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_57_1}Calathiel, wife of Caldamir, is a young noblewoman of Lossarnach.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_45_2</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_57_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_57_2}Imloth is daughter of Baranor, learning the ways of Gondor's nobility in the valleys of Lossarnach.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_1_57</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Empire West 6 -->
	<xsl:template match="Hero[@id='lord_1_52']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_52}Hirluin the Fair, Lord of Pinnath Gelin, leads his green-clad warriors from the hills of Gondor. He is known for his valor and loyalty to the Steward.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_52_4</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_52_4']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_52_4}Nauriel, wife of Hirluin, is the Lady of Pinnath Gelin, known for her steadfastness and care for the people of the green hills.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_52</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_52_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_52_1}Anariel, daughter of Hirluin, has grown up among the warriors of Pinnath Gelin and can hold her own in battle.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_1_52</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_1_52_4</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_62</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_52_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_52_2}Barandor, son of Hirluin, trains to one day lead the warriors of Pinnath Gelin. He rides with his father's green-clad company.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_1_52</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_1_52_4</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_62']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_62}Oromar, husband of Anariel, joined the house of Halboronionath through their union. He serves the Lord of Pinnath Gelin faithfully.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_52_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_62_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_62_1}Isfin, wife of Barandor, is a woman of the Green Hills known for her grace.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_52_2</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Empire West 7 -->
	<xsl:template match="Hero[@id='lord_1_53']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_53}Angbor the Fearless, Lord of Lamedon, rallied his men even when the Dead marched through his lands. He is bold and quick to act.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_73_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_73']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_73}Narmir, younger brother of Angbor, serves in the defense of Lamedon. He shares his brother's courage if not yet his experience.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_53_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_73_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_73_1}Belwen, wife of Angbor, stands steadfastly by her husband in the defense of Lamedon.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_53</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Empire West 8 -->
	<xsl:template match="Hero[@id='lord_1_71']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_71}Golasgil, Lord of Anfalas, commands the long coastline against Corsair raids. He is a weathered sea-lord who has defended Gondor's shores for decades.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_71_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_71_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_71_1}Laswen, wife of Golasgil, oversees the coastal settlements of Anfalas while her husband guards the sea lanes.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_71</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_WE8_c']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_WE8_c}Pelendur, son of Golasgil, is being trained to one day defend the coast of Anfalas as his father has done.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_1_71</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_1_71_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_WE8_u']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_WE8_u}Barandil, brother of Golasgil, serves as a captain of the coastal watch of Anfalas.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Empire West 9 -->
	<xsl:template match="Hero[@id='lord_WE9_l']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_WE9_l}Duinhir, Lord of Morthond, leads his archers from the Black Root Vale to the defense of Gondor. He is a stern and capable commander.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_WE9_u2</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_WE9_u']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_WE9_u}Duilin, elder son of Duinhir, leads his own company of archers and fights alongside his father in defense of Gondor.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_WE9_l</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_WE9_u2</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_WE9_u2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_WE9_u2}Rosfin, wife of Duinhir, manages the affairs of the Black Root Vale in her husband's absence.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_WE9_l</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- ======================= KINGDOM 1: MORDOR (Empire South) ======================= -->

	<xsl:template match="Hero[@id='lord_1_14']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_14}The Mouth of Sauron speaks with the Dark Lord's authority. Once a Black Numenorean, he has forgotten his own name after ages of service to evil. He commands the garrison of the Black Gate.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_27']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_27_bn}Ugrukhôr holds the wall-walk of the Morannon for the Mouth of Sauron. He is of the old Black Numenorean blood, and he counts the Orcs under his command as tools rather than soldiers.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_27_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_27_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_27_1_bn}Nûlubêth is a Black Numenorean sorceress in service to the Dark Tower. Her name means Night-Word in the old Adunaic speech, and she chose it herself.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_27</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_27_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_27_2_bn}Aganuzîr leads the raiding parties that ride west of the Black Gate. His name means Death-Lover, and his mother intended it as a blessing.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_1_27</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_1_27_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_27_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_27_3_bn}Ugruphel rides with her brother's raiders and keeps the tally of everything they take. She has never seen the sea her ancestors crossed.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_1_27</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_1_27_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_37']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_37_bn}Dolguphel is the daughter of the Mouth of Sauron, though he has long since put aside the title of father. She commands the horse of Carach Angren and studies the arts her house does not name aloud.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_1_14</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_47']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_47_bn}Ulbar keeps the forges beneath Carach Angren and the slaves who feed them. His is one of the few names his people carried out of Numenor unchanged.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_47_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_47_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_47_1_bn}Zimrabêth speaks for her husband's house at the Black Gate, and speaks rather better than he does. The Mouth of Sauron has found her useful more than once.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_47</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_47_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_47_2_bn}Azruphel is fourteen and already rides with the Morannon patrols. Her name means Sea-Daughter, for a sea no living Black Numenorean has laid eyes on.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_1_47</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_1_47_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_47_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_47_3_bn}Abrazîn was named for the old Numenorean virtue of steadfastness. In Mordor the word has come to mean something much closer to obedience.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_1_47</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_1_47_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Empire South 2 -->
	<xsl:template match="Hero[@id='lord_1_15']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_15}The Witch-King of Angmar, Lord of the Nazgûl, is Sauron's most terrible servant. He was once a great king of Men, corrupted by his Ring of Power into an undying wraith.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_155']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_155}The Dark Marshall commands the Nazgûl in battle, second only to the Witch-King.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_16']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_16}The Knight of Umbar was once a lord of the Corsairs before taking the Ring.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_28']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_28}The Betrayer was once a king in the East before his corruption.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_38']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_38}The Undying was a sorcerer-king who sought immortality and found only eternal servitude.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_48']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'faction' and local-name() != 'spouse' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_48}Khamûl the Easterling, second of the Nine, once ruled a great kingdom in Rhun before accepting a Ring of Power.</xsl:attribute>
			<xsl:attribute name="faction">Faction.clan_dolguldur_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_48_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'faction' and local-name() != 'spouse' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_48_1}The Tainted spreads corruption wherever he rides.</xsl:attribute>
			<xsl:attribute name="faction">Faction.clan_dolguldur_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_48_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'faction' and local-name() != 'spouse' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_48_2}The Shadow of the Northmen hunts in the cold lands.</xsl:attribute>
			<xsl:attribute name="faction">Faction.clan_dolguldur_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_48_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'faction' and local-name() != 'spouse' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_48_3}The Shadow of Umbar haunts the southern coasts.</xsl:attribute>
			<xsl:attribute name="faction">Faction.clan_dolguldur_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Empire South 3 -->
	<xsl:template match="Hero[@id='lord_1_177']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_177}Khorgath serves as a high officer in the armies of Mordor, a son of Sauron raised to the muster of war. He drives his legions toward the lands of Men with the cold patience of one who knows the Eye is always watching.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_1_17</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_1_18</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_29']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_29}Herumarth, a Black Númenórean of Sauron's own blood, commands a fortress on the marches of Mordor. Cold and proud, he keeps the old Númenórean cruelty sharpened by the Shadow that sired him.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_1_17</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_1_18</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_17']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_17}Sauron the Great, Dark Lord of Mordor and Lord of the Rings, has risen again. From the black tower of Barad-dûr his Eye sweeps the lands of the West, and the legions of Gorgoroth muster beneath his banner. The Free Peoples reckoned him beaten, but the Shadow only bided its time. Now he marches to break them and bind all of Middle-earth under one dominion. Let the kings of Men despair, for the age of Sauron is come.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_18']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_18}Morgha stands at the Dark Lord's side, mistress of the inner hierarchy of Lugbúrz. Her word carries the weight of the Eye, and the captains of Mordor fear her counsel as much as Sauron's wrath.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_39']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_39}Naktharil, daughter of the Dark Lord, is steeped in the dark arts of Dol Guldur and Lugbúrz. She walks the Shadow's hidden roads, and those who cross her are not heard from again.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_1_17</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_1_18</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Empire South 4 -->
	<xsl:template match="Hero[@id='lord_1_30']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_30}Gothmog, Lieutenant of Morgul, commands the armies of Mordor at the Pelennor Fields. A brutal and cunning Orc, he leads from the front.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_30_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_30_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_30_1}Svala Redfang is an Orc captain under Gothmog's command.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_30</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_49']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_49}Skarnak leads a company of Mordor orcs, a brutal captain who claws his way up the muster by breaking those above him. The lash and the Eye are the only laws he keeps.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_49_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_49_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_49_1}Tristania serves in the dark forces.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_49</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_49_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_49_2}Gordiana dwells in the shadow of the Dark Tower.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_1_49</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_1_49_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_30_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_30_2}Mogra serves the dark powers of Mordor.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_1_30</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_1_30_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_30_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_30_3}Snaga is a servant of the Shadow.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_1_30</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_1_30_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_56_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother' and local-name() != 'faction']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_56_2}Rustica rides to war alongside her father Tormund, a fierce young warrior of the clan.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_1_56</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_1_56_1</xsl:attribute>
			<!-- whole house -> Dunland: move from clan_empire_south_4 (Mordor) to parents' clan_empire_north_8 -->
			<xsl:attribute name="faction">Faction.clan_empire_north_8</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Empire South 5 -->
	<xsl:template match="Hero[@id='lord_1_63_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_63_2}Comatasa is an Orc bred for war.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_1_63</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_1_63_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_63_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_63_3}Elidilea serves in the dark hierarchy.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_1_63</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_1_63_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_63']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_63}Gorbag, Captain of Minas Morgul, leads the garrison of the dead city. He is cunning even by Orc standards.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_63_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_63_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_63_1}Valaria serves in the citadel of the dead.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_63</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_74']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_74}Bûrznak commands a regiment of Mordor orcs, a mounted captain who rides down stragglers and deserters alike.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_74_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_74_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_74_1}Zena serves the dark masters of Mordor.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_74</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Empire South 6 -->
	<xsl:template match="Hero[@id='lord_1_54']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_54}Shagrat, Captain of Cirith Ungol, guards the pass into Mordor. He is known for his cruelty even among Orcs.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_54_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_54_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_54_1}Constalia serves in the tower of Cirith Ungol.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_54</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_68']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_68}Gorthak is an orc captain holding the high pass into Mordor, watchful and merciless to any who would slip past the Eye.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_68_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_68_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_68_1}Silvina serves in the garrison of the spider's pass.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_68</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Empire South 7 -->
	<xsl:template match="Hero[@id='lord_1_69_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_69_2}Dorathila serves in the dark armies.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_1_69</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_1_69_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_69']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_69}Grukhash commands a fortress on the borders of Mordor, a wary keeper of the Shadow's marches.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_69_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_69_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_69_1}Areliana serves in the borderlands.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_69</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_55']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_55}Mathmog is a powerful Uruk-hai war chief.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_55_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_55_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_55_1}Megethia serves the dark powers.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_55</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Empire South 8 -->
	<xsl:template match="Hero[@id='lord_1_72']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_72}Bolg is a powerful Orc chieftain, descended from Azog the Defiler.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_72_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_72_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_72_1}Viviana serves in the dark hierarchy.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_72</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_SE8_c']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_SE8_c}Shagrala was bred in the pits of Mordor and has fought her way up to a command of her own.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Empire South 9 -->
	<xsl:template match="Hero[@id='lord_SE9_l']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_SE9_l_bn}Zagarkhôr, the Sword-Lord, holds Cirith Nargil for the Dark Tower. His house kept the Adunaic tongue when the rest of Middle-earth let it go, and he will correct anyone who names him in Elvish.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_SE9_s</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_SE9_s']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_SE9_s_bn}Pharazîn is a Black Numenorean sorceress, and the real authority at Cirith Nargil whenever her husband takes the field. Her name recalls the last King of Numenor.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_SE9_l</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_SE9_c1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_SE9_c1_bn}Zigûrbêl is heir to Cirith Nargil and trained in the dark arts. His name means Lover of the Wizard, which is what Numenor called Sauron before it learned what he was.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_SE9_l</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_SE9_s</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_SE9_c2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_SE9_c2_bn}Zimrazîn is the second child of the Sword-Lord and the more dangerous of the two. She has her mother's talents and none of her patience.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_SE9_l</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_SE9_s</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- ======================= KINGDOM 2: DALE (Sturgia) ======================= -->

	<xsl:template match="Hero[@id='lord_2_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_2_1}Bard II, King of Dale, is grandson of the legendary Bard the Bowman who slew Smaug. He rules the prosperous kingdom at the foot of Erebor, allied with the Dwarves of the Lonely Mountain.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_2_2</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_2_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_2_2}Asta is Queen of Dale, beloved for her wisdom and grace.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_2_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_2_13_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father']"/>
			<xsl:attribute name="text">{=TAOM_hero_2_13_1}Lilizha is a daughter of a Barding noble house.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_2_13</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_2_13_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father']"/>
			<xsl:attribute name="text">{=TAOM_hero_2_13_2}Andruta serves in the household of the king.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_2_13</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_2_13_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father']"/>
			<xsl:attribute name="text">{=TAOM_hero_2_13_3}Luda is a young noblewoman of Dale.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_2_13</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_2_13_4']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father']"/>
			<xsl:attribute name="text">{=TAOM_hero_2_13_4}Teta is a noble daughter of Dale.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_2_13</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_2_7']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_2_7}Simir is a prince of Dale, heir to his father's throne.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_2_1</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_2_2</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_2_7_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_2_7_1}Mimir is a prince of the royal house of Dale.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_2_1</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_2_2</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_2_10']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_2_10}Valla is a princess of Dale, trained in diplomacy and statecraft.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_2_1</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_2_2</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_2_13']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_2_13}Vidar is a lord of the northern marches, guarding against Easterling incursions.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Sturgia Clan 2 - Dwarves of Erebor -->
	<xsl:template match="Hero[@id='lord_2_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father']"/>
			<xsl:attribute name="text">{=TAOM_hero_2_3}Rand is a lord of Dale, warden of the eastern vales, who keeps the old alliance with the Dwarves of Erebor.</xsl:attribute>
			<xsl:attribute name="father">Hero.dead_lord_2_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_2_4']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father']"/>
			<xsl:attribute name="text">{=TAOM_hero_2_4}Siga is a Dwarf noblewoman of the line of Durin.</xsl:attribute>
			<xsl:attribute name="father">Hero.dead_lord_2_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_2_4_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_2_4_1}Apolanea is a Dwarf maiden of noble birth.</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_2_4</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_2_8']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father']"/>
			<xsl:attribute name="text">{=TAOM_hero_2_8}Urik is a Dwarf prince of Erebor, skilled in smithcraft and warfare.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_2_3</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_2_11']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father']"/>
			<xsl:attribute name="text">{=TAOM_hero_2_11}Idrun is a Dwarf warrior of the royal guard.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_2_3</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_2_111']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father']"/>
			<xsl:attribute name="text">{=TAOM_hero_2_111}Rozhivol is a Dwarf warrior who guards the mountain gates.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_2_3</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Sturgia Clan 3 -->
	<xsl:template match="Hero[@id='lord_2_5']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_2_5}Ask is a lord of Dale who manages the trade routes between the Mountain and the Long Lake.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_2_6</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_2_6']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_2_6}Erta is wife to Ori, known for her skill in gem-cutting.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_2_5</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_2_9']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_2_9}Lek is heir to Ori's holdings, trained in both trade and combat.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_2_5</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_2_6</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_2_12']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_2_12}Svana is a daughter of the trading house.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_2_5</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_2_6</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_2_121']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_2_121}Osven manages caravans between the mountain and the city.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_2_5</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_2_6</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Sturgia Clan 4-8 - More Dale/Dwarf nobles -->
	<xsl:template match="Hero[@id='lord_2_14']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_2_14}Isvan is a lord of the eastern marches, guarding the Long Lake.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_2_14_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_2_14_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_2_14_1}Valkava is wife to Lord Isvan.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_2_14</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_2_14_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_2_14_2}Zaverena is heir to the eastern holdings.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_2_14</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_2_14_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_2_14_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_2_14_3}Vizhduna is a young noble of the eastern marches.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_2_14</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_2_14_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_2_16']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_2_16}Hauk is a lord of Dale, master of the market roads and the winter caravans.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_2_16_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_2_16_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_2_16_1}Tyaska is wife to Hauk, skilled in the healing arts.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_2_16</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_2_21']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_2_21}Svedorn commands the garrison of Laketown.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_2_21_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_2_21_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_2_21_1}Izdenka manages the affairs of Laketown.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_2_21</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Sturgia Clan 5 -->
	<xsl:template match="Hero[@id='lord_2_17']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_2_17}Stein is a lord of Dale, remembered for holding the northern gate in the years after the dragon fell.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_2_17_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_2_17_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_2_17_1}Dracha keeps the memory of the old king alive.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_2_17</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_2_22']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_2_22}Lashonek commands warriors of Dale.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_2_22_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_2_22_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_2_22_1}Zheneva is wife to Lashonek.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_2_22</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_2_24']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_2_24}Alvar is a captain of the Dale Guard.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_2_24_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_2_24_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_2_24_1}Zorina is a noble lady of Dale.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_2_24</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Sturgia Clan 6 -->
	<xsl:template match="Hero[@id='lord_2_18']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_2_18}Ulf is a lord of Dale, captain of the river-watch upon the Running River.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_2_18_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_2_18_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_2_18_1}Zorika is wife to Ulf, and mistress of his hall upon the Running River.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_2_18</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_2_23']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_2_23}Galden commands the southern garrison of Dale.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_2_23_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father']"/>
			<xsl:attribute name="text">{=TAOM_hero_2_23_1}Zlatka is daughter of Galden.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_2_23</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Sturgia Clan 7 -->
	<xsl:template match="Hero[@id='lord_2_15']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_2_15}Ratagost is a wise counselor who advises the king on matters of state.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_2_15_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father']"/>
			<xsl:attribute name="text">{=TAOM_hero_2_15_1}Yachana serves in the royal court.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_2_15</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_2_15_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father']"/>
			<xsl:attribute name="text">{=TAOM_hero_2_15_2}Milanka is a noblewoman of the court.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_2_15</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_2_15_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father']"/>
			<xsl:attribute name="text">{=TAOM_hero_2_15_3}Velina serves the royal household.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_2_15</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_2_15_4']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father']"/>
			<xsl:attribute name="text">{=TAOM_hero_2_15_4}Bovan is a young nobleman of Dale.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_2_15</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_2_19']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_2_19}Geir is a warrior of Dale of great renown, kin to the old line of Girion.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_2_19_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_2_19_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_2_19_1}Vitomira serves alongside Geir.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_2_19</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Sturgia Clan 8 -->
	<xsl:template match="Hero[@id='lord_2_20']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_2_20}Val is an elder of Dale, keeper of lore and of the healing arts.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_2_20_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_2_20_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_2_20_1}Kisha is wife to Val, skilled in herb lore.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_2_20</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_S8_u']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_S8_u}Galyk is a grizzled veteran who guards the mountain halls.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- ======================= KINGDOM 3: HARAD (Aserai) ======================= -->

	<xsl:template match="Hero[@id='lord_3_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_3_1}Khadurak is the Taskral of Harad, supreme commander of the southern armies. He leads the Mumakil war elephants against the enemies of his people, allied with Mordor against Gondor.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_3_2</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_3_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_3_2}A Haradrim noble lady of high standing.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_3_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_3_7']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_3_7}Dhiyul is heir to the Taskral, trained in the ways of the Mumak riders.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_3_1</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_3_2</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_3_10']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_3_10}Anidha is a daughter of the Taskral, versed in court politics.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_3_1</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_3_2</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_3_13']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_3_13}Nuqar commands the elite Serpent Guard of Harad.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_3_13_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_3_13_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_3_13_1}Sira is wife to Nuqar.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_3_13</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_3_13_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_3_13_2}Razana is daughter of the Serpent Guard commander.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_3_13</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_3_13_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Aserai Clan 2 -->
	<xsl:template match="Hero[@id='lord_3_8']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_3_8}Addas is a Haradrim warrior prince.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_3_3</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_3_4</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_3_11']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_3_11}Arwa is a princess of the desert tribes.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_3_3</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_3_4</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_3_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_3_3}Calemir is a chieftain of the eastern Haradrim, lord of the oasis settlements.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_3_4</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_3_3_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_3_3_1}Tariq is heir to the eastern oasis lords.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_3_3</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_3_4</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_3_4']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_3_4}Maraa is wife to Calemir, known for her wisdom.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_3_3</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Aserai Clan 3 -->
	<xsl:template match="Hero[@id='lord_3_5']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_3_5}Haldir commands the coastal Haradrim, allied with the Corsairs of Umbar.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_3_6</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_3_51']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_3_51}Haqan is a sea captain of the coastal tribes.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_3_5</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_3_6</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_3_12']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_3_12}Manan raids the coastal settlements of Gondor.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_3_5</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_3_6</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_3_20']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_3_20}Karith commands raiding parties into enemy lands.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_3_20_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_3_20_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_3_20_1}Judira is wife to Karith.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_3_20</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_3_20_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_3_20_2}Azina is daughter of a raiding captain.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_3_20</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_3_20_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_3_6']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_3_6}Ruma is wife to Haldir, managing their coastal holdings.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_3_5</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_3_9']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_3_9}Usair is heir to the coastal holdings.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_3_5</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_3_6</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Aserai Clan 4-8 - More Haradrim nobles -->
	<xsl:template match="Hero[@id='lord_3_14']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_3_14}Thamza leads caravans across the desert wastes.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_3_14_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_3_14_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_3_14_1}Sasaitha manages the desert trade routes.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_3_14</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_3_15']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_3_15}Ghuzid commands the Mumak riders of the deep south.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_3_15_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_3_15_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_3_15_1}Shimra is wife to the Mumak lord.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_3_15</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_3_15_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_3_15_2}Bushila trains young Mumak riders.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_3_15</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_3_15_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_3_16']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_3_16}Rúmil is a chieftain of the golden banner tribes.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_3_16_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_3_16_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_3_16_1}Farina is wife to the golden banner chief.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_3_16</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_3_17']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_3_17}Orophin commands the serpent riders of the far south.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_3_17_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_3_17_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_3_17_1}Shaima is wife to Orophin.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_3_17</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_3_17_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_3_17_2}Sanit is heir to the serpent riders.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_3_17</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_3_17_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_3_21']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_3_21}Ukhai leads desert raiders against northern caravans.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_3_21_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_3_21_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_3_21_1}Ashisa is wife to the raider captain.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_3_21</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_3_18']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_3_18}Aranthon is a wealthy merchant prince of the south.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_3_18_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father']"/>
			<xsl:attribute name="text">{=TAOM_hero_3_18_1}Farzana is daughter of the merchant prince.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_3_18</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_3_18_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father']"/>
			<xsl:attribute name="text">{=TAOM_hero_3_18_2}Hafisa manages trade connections.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_3_18</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_3_18_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father']"/>
			<xsl:attribute name="text">{=TAOM_hero_3_18_3}Zuad is a young merchant in training.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_3_18</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_3_18_4']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father']"/>
			<xsl:attribute name="text">{=TAOM_hero_3_18_4}Jalfar guards the trade caravans.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_3_18</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_3_19']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_3_19}Aethirion commands a fortress on the northern frontier.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_3_19_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_3_19_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_3_19_1}Salma is wife to the frontier commander.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_3_19</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_3_19_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_3_19_2}Zulaika trains with the frontier garrison.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_3_19</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_3_19_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_3_19_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_3_19_3}Sulhana is a noble of the frontier.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_3_19</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_3_19_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_3_23']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_3_23}Qahin leads scouts into enemy territory.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_3_23_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_3_23_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_3_23_1}Sukayna is wife to the scout commander.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_3_23</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_3_22']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_3_22}Duilin commands archers of the desert tribes.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_3_22_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_3_22_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_3_22_1}Yamina is wife to the archer commander.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_3_22</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_3_22_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_3_22_2}Suna trains young archers.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_3_22</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_3_22_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_3_22_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_3_22_3}Zanuwa is a skilled archer of the tribe.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_3_22</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_3_22_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_3_22_4']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_3_22_4}Hajara guards the desert watchtowers.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_3_22</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_3_22_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- ======================= KINGDOM 4: ROHAN (Vlandia) ======================= -->

	<xsl:template match="Hero[@id='lord_4_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_1}Théoden, King of Rohan, rules from Meduseld in Edoras. Once bewitched by Saruman's servant Gríma, he was freed by Gandalf and led his people to glory at Helm's Deep and the Pelennor Fields.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_4_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_2}Elfhild was queen of Rohan, mother of Théodred. She died in childbirth, a grief that shadowed Théoden's reign.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_4_7']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_7}Théodred was the only son of King Théoden, Prince of Rohan. He fell defending the Fords of Isen against Saruman's Uruk-hai.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_4_1</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_4_2</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_4_10']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_10}Elys is a noble lady of the Rohirrim court.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_4_13']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_13}Romund serves as a captain of the Royal Guard.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_4_14']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_14}Morcon is a trusted captain of the king's household.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_4_141']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_141}Amorcon serves alongside his kinsman in the guard.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_4_15']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_15}Erdurand is a rider of Théoden's household.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Vlandia Clan 2 - Eoforing (Eomer's family) -->
	<xsl:template match="Hero[@id='lord_4_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_3}Aldric holds a hall in the Eastfold and rides in the muster of Edoras. He is a plain horseman of no great house, trusted for it.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_4_3_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_3_1}Éomer Eadig, Third Marshal of the Mark, is nephew to Théoden and heir to the throne. He leads the riders of the Eastfold with fierce loyalty.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_rohan_10_1</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_4_3_3</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_4_4']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_4}Elthild is a noble lady of the Eoforing.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_4_8']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_8}Furnhard serves as a trusted rider under Éomer's command.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_4_11']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_11}Liena serves the royal household.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Vlandia Clan 3 - Grimbold's family -->
	<xsl:template match="Hero[@id='lord_4_6']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_6}Grimbold of Grimslade commands the riders of Westfold. A fierce warrior, he held the Fords of Isen and later died heroically at the Pelennor.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_4_6_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_6_1}Déorwyn is heir to Grimbold's command.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_4_6</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_4_5</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_4_5']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_5}Unthery serves as a captain of the Westfold riders.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_4_9']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_9}Thomund commands a company of Westfold riders.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_4_6</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_4_5</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_4_12']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_12}Silvind is a shieldmaiden of the Westfold.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_4_6</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_4_5</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_4_121']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_121}Lasand serves in the Westfold garrison.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_4_6</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_4_5</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Vlandia Clan 4 - Erkenbrand's family -->
	<xsl:template match="Hero[@id='lord_4_16']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_16}Erkenbrand, Lord of Westfold, commands the fortress of the Hornburg. He arrived with Gandalf at Helm's Deep to turn the tide of battle.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_4_16_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_4_16_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_16_1}Mérthú is wife to Erkenbrand, Lady of the Hornburg.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_4_16</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_4_17']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_17}Elbet serves in the household of Erkenbrand.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_4_18']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_18}Amalgun is heir to Erkenbrand.</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_4_16_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_4_181']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_181}Arthamund serves as a captain of the Hornburg garrison.</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_4_16_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_4_19']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_19}Asela is a shieldmaiden who guards the Deeping Coomb.</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_4_16_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Vlandia Clan 5-10 - More Rohirrim nobles -->
	<xsl:template match="Hero[@id='lord_4_25']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_25}Lucand commands riders of the Eastemnet.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_4_25_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_25_1}Bertliana is wife to Lucand.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_4_21']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_21}Cuthræd Ordlacing patrols the eastern borders.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_4_21_4</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_4_20']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_20}Varmund commands the garrison of Aldburg.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_4_20_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_20_1}Ingeltrud is wife to Varmund, Lady of Aldburg.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_4_22']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_22}Wulf Celmunding leads riders of the northern valleys.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_rohan_13_1</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_4_22_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_4_22_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_22_1}Sunnifa is wife to Wulf.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_rohan_13_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_4_23']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'faction']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_23}Marhath leads horse breeders of the southern grasslands.</xsl:attribute>
			<xsl:attribute name="faction">Faction.clan_vlandia_19</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- lord_4_23_1 stays in clan_vlandia_7 as new owner -->
	<xsl:template match="Hero[@id='lord_4_23_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_23_1}Wulfwynn is daughter of Marhath, a skilled rider.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_4_23_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother' and local-name() != 'faction']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_23_2}Eleduran trains warhorses for the king's army.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_4_23</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_4_23_1</xsl:attribute>
			<xsl:attribute name="faction">Faction.clan_vlandia_19</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_4_23_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'faction']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_23_3}Eleduran the younger serves as a horse warden.</xsl:attribute>
			<xsl:attribute name="faction">Faction.clan_vlandia_19</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_4_24']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_24}Gríma Wormtongue was Théoden's counselor, secretly a spy for Saruman. His whispered lies poisoned the king's mind until Gandalf broke the spell.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_4_24_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_24_1}Adaltrud was wed to Gríma Grimmóding before his counsel turned to Saruman. She keeps a hall at Edoras that the riders no longer visit.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_4_24_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_24_2}Gudonhelda is the eldest daughter of Gríma Grimmóding. She learned early that her father's name opens no doors in Edoras.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_4_24_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_24_3}Ingunde, second daughter of Gríma Grimmóding, tends the horses of a house nobody rides out with.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_4_24_4']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_24_4}Irmgard is the youngest of Gríma Grimmóding's daughters, and the only one too young to remember the hall before the whispering started.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_4_26']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_26}Peric commands riders along the Gap of Rohan.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_4_26_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_26_1}Reingarda is a shieldmaiden of the Gap.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_4_27']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_27}Ælle Æthellafing guards the Folds against Dunlending raids.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_4_27_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_4_27_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_27_1}Wærburg is wife to Ælle.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_4_27</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_V9_u']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_V9_u}Urundulf is a seasoned rider who advises the local thane.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_4_28']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'faction']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_28}Fasthelm Morcargas commands the southern watch posts.</xsl:attribute>
			<xsl:attribute name="faction">Faction.clan_vlandia_20</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- lord_4_28_1 stays in clan_vlandia_10 as new owner -->
	<xsl:template match="Hero[@id='lord_4_28_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_28_1}Morcar is son of Fasthelm.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_4_28</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_4_28_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'faction']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_28_2}Hereswith is daughter of the southern watch commander.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_4_28</xsl:attribute>
			<xsl:attribute name="faction">Faction.clan_vlandia_20</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- ======================= KINGDOM 5: KHAND (Battania) ======================= -->

	<xsl:template match="Hero[@id='lord_5_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_5_1}Vargûl, the High Warlord of Khand, leads the fierce Variag horsemen. His alliance with Mordor brings his warriors against Gondor in the war for Middle-earth.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_5_1_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father']"/>
			<xsl:attribute name="text">{=TAOM_hero_5_1_1}Nyveth is a daughter of the High Warlord's house. She rides with his retinue, though she is still a child and carries no spear.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_5_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_5_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_5_3}Ergeon is a chieftain who leads the eastern Variag clans, known for their swift cavalry tactics.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_5_4</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_5_3_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father']"/>
			<xsl:attribute name="text">{=TAOM_hero_5_3_1}Ranaon is heir to Ergeon's warband.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_5_3</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_5_3_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_5_3_2}Ladogual serves as a captain in Ergeon's host.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_5_4']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_5_4}Nywin is wife to Ergeon, skilled in the arts of healing.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_5_3</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_5_5']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_5_5}Melidir leads raiders into the lands of Gondor.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_5_6</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_5_5_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_5_5_1}Eilidh manages the clan's horse herds.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_5_5</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_5_6</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_5_6']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_5_6}Alcaea is wife to Melidir.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_5_5</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_5_7']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father']"/>
			<xsl:attribute name="text">{=TAOM_hero_5_7}Cadwyr is a Variag warrior chieftain of Khand.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_5_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_5_8']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father']"/>
			<xsl:attribute name="text">{=TAOM_hero_5_8}Sein commands a company of Variag cavalry.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_5_3</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_5_9']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_5_9}Culharn is a Variag warrior of rising reputation.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_5_5</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_5_6</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_5_91']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_5_91}Tegan serves alongside Culharn.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_5_5</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_5_6</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_5_10']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father']"/>
			<xsl:attribute name="text">{=TAOM_hero_5_10}Corein is a Variag noble of the High Warlord's court.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_5_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_5_11']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father']"/>
			<xsl:attribute name="text">{=TAOM_hero_5_11}Alynneth is a Variag noblewoman.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_5_3</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_5_12']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_5_12}Wythuin trains young Variag riders.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_5_5</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_5_6</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_5_13']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_5_13}Muinser commands raiders in the northern reaches.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_5_131']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father']"/>
			<xsl:attribute name="text">{=TAOM_hero_5_131}Rath serves under Muinser's command.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_5_13</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_5_13_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father']"/>
			<xsl:attribute name="text">{=TAOM_hero_5_13_1}Beasag is a Variag warrior of the northern clans.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_5_13</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_5_15']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_5_15}Pryndor guards the mountain passes into Khand.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_5_15_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father']"/>
			<xsl:attribute name="text">{=TAOM_hero_5_15_1}Floraidh is daughter of the mountain guardian.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_5_15</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_5_15_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father']"/>
			<xsl:attribute name="text">{=TAOM_hero_5_15_2}Beitrin patrols the mountain trails.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_5_15</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_5_15_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father']"/>
			<xsl:attribute name="text">{=TAOM_hero_5_15_3}Diarbhain guards the high passes.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_5_15</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_5_14']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_5_14}Luichan commands Variag horsemen of the western plains.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_5_14_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_5_14_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_5_14_1}Eabyr is wife to Luichan.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_5_14</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_5_16']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_5_16}Aeron leads warriors of the central steppes.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_5_16_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_5_16_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_5_16_1}Liasin is wife to Aeron.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_5_16</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_5_16_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_5_16_2}Gawen is heir to Aeron's warband.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_5_16</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_5_16_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_5_17']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_5_17}Aradwyr commands the eastern frontier forces.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_5_17_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_5_17_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_5_17_1}Brighan is wife to Aradwyr.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_5_17</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_5_18']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_5_18}Branoc leads scouts into enemy territory.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_5_15</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_5_18_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_5_18_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_5_18_1}Seonag is wife to Branoc.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_5_18</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_5_19']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_5_19}Fenagan is a Variag champion known for his dueling prowess.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_5_20']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_5_20}Siaramus commands the chariot warriors of Khand.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_5_21']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_5_21}Carfyd leads Variag raiders along the border.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_5_21_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_5_21_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_5_21_1}Beathag is wife to Carfyd.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_5_21</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_5_21_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_5_21_2}Taorse is heir to Carfyd's raiding band.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_5_21</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_5_21_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_5_22']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_5_22}Fiarad guards the southern trade routes.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- ======================= KINGDOM 6: RHUN (Khuzait) ======================= -->

	<xsl:template match="Hero[@id='lord_6_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_6_1}Zhamik Zhamian, Loke-Khan of Rhun, rules the Golden Realm of the Easterlings. His ancestors served Sauron, and he continues that dark alliance, dreaming of conquering the lands of the West.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_6_2</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_6_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_6_2}Maraia is wife to the Loke-Khan, queen of the Golden Realm.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_6_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_6_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_6_3}Bagai serves as a captain of the royal guard.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_6_4']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_6_4}Gûrtilm Salurian commands the western armies of Rhun. After her husband's death, she took command with iron resolve.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_6_5']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_6_5}Irbo Nikathian leads the Arkit clan, famed horse archers of the eastern steppes.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_6_51']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father']"/>
			<xsl:attribute name="text">{=TAOM_hero_6_51}Khada is heir to the Arkit clan.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_6_5</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_6_6']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_6_6}Suran commands warriors of the southern tribes.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_6_7']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_6_7}Chaghan is a prince of Rhun, heir to the Golden Realm.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_6_1</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_6_2</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_6_8']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_6_8}Esur is a warrior of Gûrtilm's host.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_6_3</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_6_4</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_6_81']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_6_81}Nayantai serves as a captain in Gûrtilm's army.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_6_3</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_6_4</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_6_9']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father']"/>
			<xsl:attribute name="text">{=TAOM_hero_6_9}Temun is a warrior of the Arkit clan.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_6_5</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_6_10']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_6_10}Alijin is a prince of the royal house.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_6_1</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_6_2</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_6_101']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_6_101}Bolat serves in the royal household.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_6_1</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_6_2</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_6_11']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_6_11}Yana is a noble of Gûrtilm's host.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_6_3</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_6_4</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_6_12']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father']"/>
			<xsl:attribute name="text">{=TAOM_hero_6_12}Abagai is heir to the Arkit holdings.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_6_5</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_6_13']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father']"/>
			<xsl:attribute name="text">{=TAOM_hero_6_13}Bortu is a noble of the royal house.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_6_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_6_15']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse' and local-name() != 'father']"/>
			<xsl:attribute name="text">{=TAOM_hero_6_15}Oragur leads the Arkit horse archers.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_6_15_1</xsl:attribute>
			<xsl:attribute name="father">Hero.dead_lord_6_3</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_6_15_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_6_15_1}Khorijin is wife to Oragur.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_6_15</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_6_15_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_6_15_2}Sechen is heir to Oragur's command.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_6_15</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_6_15_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_6_16']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_6_16}Akvoth Karmian commands the southern borders of Rhun.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_6_16_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_6_16_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_6_16_1}Chambui is wife to Akvoth.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_6_16</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_6_16_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_6_16_2}Unagen is heir to Akvoth's command.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_6_16</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_6_16_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_6_17']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_6_17}Amdûr Amdûrid leads a powerful clan of the eastern steppes.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_6_17_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_6_17_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_6_17_1}Ergene is wife to Amdûr.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_6_17</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_6_17_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_6_17_2}Yesum is heir to the Amdûrid holdings.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_6_17</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_6_17_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_6_18']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_6_18}Lûthkan Khundolar commands warriors of the western provinces.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_6_18_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_6_18_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_6_18_1}Tilun is wife to Lûthkan.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_6_18</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_6_18_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_6_18_2}Chagun is heir to the Khundolar.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_6_18</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_6_18_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_6_19']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_6_19}Vakheraltan Khundolar is brother to Lûthkan, commanding cavalry scouts.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_6_19_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_6_19_1}Sokhatai is wife to Vakheraltan.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_6_19_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_6_19_2}Korte is heir to Vakheraltan's scouts.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_6_20']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_6_20}Khurubra Mashakian commands the northern tribes of Rhun.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_6_20_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_6_20_1}Jigur is wife to Khurubra.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_K8_u']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_K8_u}Boronchar is a seasoned warrior who advises the northern chieftain.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_6_21']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'faction']"/>
			<xsl:attribute name="text">{=TAOM_hero_6_21}Molluk Illnoria commands a powerful clan of horse warriors.</xsl:attribute>
			<xsl:attribute name="faction">Faction.clan_khuzait_10</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_6_21_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'faction']"/>
			<xsl:attribute name="text">{=TAOM_hero_6_21_1}Esachei is wife to Molluk.</xsl:attribute>
			<xsl:attribute name="faction">Faction.clan_khuzait_10</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_6_22']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'faction']"/>
			<xsl:attribute name="text">{=TAOM_hero_6_22}Shakhal II Shakhalian rules a wealthy trading clan of the Sea of Rhun.</xsl:attribute>
			<xsl:attribute name="faction">Faction.clan_khuzait_11</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_6_22_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'faction']"/>
			<xsl:attribute name="text">{=TAOM_hero_6_22_1}Eselen is wife to Shakhal.</xsl:attribute>
			<xsl:attribute name="faction">Faction.clan_khuzait_11</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_6_23']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'faction']"/>
			<xsl:attribute name="text">{=TAOM_hero_6_23}Hûz-Margôz Huz commands fortress garrisons along the western frontier.</xsl:attribute>
			<xsl:attribute name="faction">Faction.clan_khuzait_12</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_6_24']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'faction']"/>
			<xsl:attribute name="text">{=TAOM_hero_6_24}Nikath Adekig guards the southern passes into Rhun.</xsl:attribute>
			<xsl:attribute name="faction">Faction.clan_khuzait_13</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- ======================= MINOR CLANS ======================= -->

	<!-- Aserai 9 -->
	<xsl:template match="Hero[@id='lord_A9_l']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_A9_l}Caradan is a Haradrim chieftain of the southern wastes, young for the office and watched closely by older men.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_A9_s']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_A9_s}Thiqa is wife to the chieftain of the southern wastes and keeps the water tallies of his camps.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_A9_c']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_A9_c}Dhila is heir to the chieftain of the southern wastes. She has ridden the dune tracks since she could sit a saddle.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_A9_u']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_A9_u}Qaban is a warrior of the southern tribes and rides in the chieftain's van.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Vlandia 11 — lord_V11_l moves to clan_vlandia_18 (Deáfringas) -->
	<xsl:template match="Hero[@id='lord_V11_l']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'faction' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_V11_l}Léomund Deáfringas is a Rohirrim lord of the western vales.</xsl:attribute>
			<xsl:attribute name="faction">Faction.clan_vlandia_18</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_V11_l_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_V11_u']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_V11_u}Voleric is a veteran rider who advises the lord.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_V11_c1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_V11_c1}Elendara is heir to the western vale holdings.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_V11_c2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_V11_c2}Dagunic is the second child of the western vale lord.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Sturgia 9 -->
	<xsl:template match="Hero[@id='lord_S9_l']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_S9_l}Orvar is a noble of Dale who guards the northern approaches.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_S9_m']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_S9_m}Dakhila is matriarch of a northern Barding clan.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_S9_c']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_S9_c}Forim is young heir to the northern holdings.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_S9_u']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_S9_u}Chastimir is a seasoned warrior of the northern garrison.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Khuzait 9 -->
	<xsl:template match="Hero[@id='lord_K9_l']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_K9_l}Valathmir Mashakian is a chieftain of the distant Easterling tribes, and answers to Rhun only when it suits him.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_K9_s']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_K9_s}Sevin is wife to the distant chieftain and holds his camp while he rides.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_K9_c1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_K9_c1}Altu is heir to the tribal holdings east of the inland sea.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_K9_c2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_K9_c2}Mela is the second child of the distant chieftain, and rides better than her brother.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Battania 8 -->
	<xsl:template match="Hero[@id='lord_B8_l']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_B8_l}Maireas is a Variag warlord of the eastern hills, and has held them through three winters of raiding.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_B8_s']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_B8_s}Rodarac is wife to the Variag warlord and speaks for him when he is away at the hosting.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_B8_c']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_B8_c}Guaran is heir to the Variag warlord, and has waited longer for it than he likes.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_B8_l</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_B8_s</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

</xsl:stylesheet>
