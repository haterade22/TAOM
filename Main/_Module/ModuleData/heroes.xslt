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
			<xsl:attribute name="text">{=TAOM_hero_dead_2_1}The elder Barding lord fell defending Dale against Easterling raiders. His sacrifice secured the mountain passes and is remembered in songs throughout the kingdom.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='dead_lord_2_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father']"/>
			<xsl:attribute name="text">{=TAOM_hero_dead_2_2}A noblewoman of Dale who perished in the winter plague that followed the war. She was known for her charity to the common folk.</xsl:attribute>
			<xsl:attribute name="father">Hero.dead_lord_2_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='dead_lord_3_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_dead_3_1}A Haradrim warlord who led the desert cavalry at the Battle of the Black Gate. He fell to Gondorian arrows but is revered as a martyr in the south.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='dead_lord_6_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_dead_6_1}The great Khan who first united the Easterling tribes under the banner of Rhun. His conquests pushed the borders of the Golden Realm to the shores of the Sea of Rhun.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='dead_lord_6_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_dead_6_2}A respected noyan who fell in battle against the Free Peoples. His widow Gurtilm now leads their clan with iron determination.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='dead_lord_6_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_dead_6_3}Chief noyan of the Arkit clan. His tactical brilliance in horse archery tactics influenced an entire generation of Easterling warriors.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='dead_lord_6_4']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father']"/>
			<xsl:attribute name="text">{=TAOM_hero_dead_6_4}A noble of the Arkit clan, son of the great noyan. He died young but showed promise as a warrior.</xsl:attribute>
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
			<xsl:attribute name="text">{=TAOM_hero_NE7_u}A grizzled warrior who serves as advisor to the clan's chieftain.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Empire North 8 -->
	<xsl:template match="Hero[@id='lord_NE8_l']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_NE8_l}Chieftain of a mountain clan, hardened by years of border warfare.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_NE8_s</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_NE8_s']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_NE8_s}Wife of the chieftain, skilled in the healing arts and herb lore.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_NE8_l</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_NE8_c1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_NE8_c1}Firstborn of the clan, trained in warfare since childhood.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_NE8_l</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_NE8_s</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_NE8_c2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_NE8_c2}Second child of the clan leader, known for cunning and ambition.</xsl:attribute>
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
			<xsl:attribute name="text">{=TAOM_hero_NE9_l}A aged chieftain whose wisdom is sought by all the Dunlending clans.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_NE9_s']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father']"/>
			<xsl:attribute name="text">{=TAOM_hero_NE9_s}Son of the old chieftain, eager to prove himself in battle.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_NE9_l</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_NE9_d']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father']"/>
			<xsl:attribute name="text">{=TAOM_hero_NE9_d}Daughter of the clan, trained in both combat and diplomacy.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_NE9_l</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- ======================= KINGDOM 1: GONDOR (Empire West) ======================= -->

	<xsl:template match="Hero[@id='lord_1_7']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_7}Denethor II, Steward of Gondor, rules from the White Tower of Minas Tirith. Once a man of great wisdom, grief over his wife's death and the loss of his son Boromir have darkened his mind. He resists the return of the king, believing only he can save Gondor.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_75']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_75}Boromir was the eldest son of Denethor, Captain-General of Gondor. He fell defending the halflings at Amon Hen, redeeming himself after briefly succumbing to the Ring's temptation.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_1_7</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_8']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_8}Vendelia was the beloved wife of Denethor, whose death broke his heart and began his descent into despair.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_34']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_34}Faramir is the younger son of Denethor, Captain of Ithilien. Though his father favors his brother, Faramir is beloved by his men for his wisdom and mercy.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_1_7</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_24']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_24}Tadeos serves as a lieutenant in the armies of Gondor.</xsl:attribute>
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
			<xsl:attribute name="text">{=TAOM_hero_1_9}Imrahil, Prince of Dol Amroth, is the greatest knight of Gondor. His line carries Elvish blood, and he is said to be the fairest lord in all the realm. His swan knights are legendary.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_9_5</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_10']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_10}Melkea is the Princess of Dol Amroth, known for her grace and beauty.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_35']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_35}Amenon is a knight of Dol Amroth, trained in the arts of the swan knights.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_25']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_25}Eronyx is heir to Prince Imrahil, already showing his father's martial prowess.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_23']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_23}Jastion serves in the retinue of Prince Imrahil.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Empire West 3 -->
	<xsl:template match="Hero[@id='lord_1_11']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_11}Ciryandur commands the northern defenses of Gondor, a grizzled veteran of many orc raids.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_111']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_111}Casinon serves alongside his father in the northern marches.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_12']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_12}Lysica is a noble lady of Gondor who has learned to ride and fight alongside her husband.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_36']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_36}Phaea is a warrior woman of the border lords, trained to fight since childhood.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_26']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_26}Meritor serves as a captain of rangers in the borderlands.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Empire West 4 -->
	<xsl:template match="Hero[@id='lord_1_40']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_40}Dervorin is Lord of Ringlo Vale, leading his men to the defense of Minas Tirith.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_40_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_40_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_40_1}Catella is the Lady of Ringlo Vale, managing the lands while her husband rides to war.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_40</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_46']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_46}Milos serves as a knight in the service of Ringlo Vale.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_46_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_46_1}Seorgys is a young knight eager to prove himself in battle.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_1_46</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Empire West 5 -->
	<xsl:template match="Hero[@id='lord_1_45']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_45}Forlong the Fat, Lord of Lossarnach, leads the largest contingent from the fiefs. Despite his girth, he is a fierce warrior.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_45_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_45_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_45_1}Vanyalos is the Lady of Lossarnach, known for her generosity to the common folk.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_45</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_45_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_45_2}Brandir is the heir of Lossarnach, trained in both sword and diplomacy.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_1_45</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_1_45_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_45_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_45_3}Borlong carries on his father's martial traditions.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_1_45</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_1_45_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_57']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_57}Altenos serves as a captain in the Lossarnach contingent.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_57_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_57_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_57_1}Sophalia is a noblewoman of Lossarnach.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_57</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_57_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_57_2}Jephalia is daughter of Altenos, learning the ways of nobility.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_1_57</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_1_57_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Empire West 6 -->
	<xsl:template match="Hero[@id='lord_1_52']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_52}Hirluin the Fair, Lord of Pinnath Gelin, leads his green-clad warriors from the hills.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_52_4</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_52_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_52_1}Arador is a knight of Pinnath Gelin, sworn to Lord Hirluin.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_1_52</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_1_52_4</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_52_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_52_2}Arvedui serves in the retinue of the Lord of Pinnath Gelin.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_1_52</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_1_52_4</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_62']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_62}Sejaron commands a company of hill soldiers.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_62_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_62_1}Arytha is a noblewoman of the Green Hills.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Empire West 7 -->
	<xsl:template match="Hero[@id='lord_1_53']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_53}Angbor the Fearless, Lord of Lamedon, rallied his men when the Corsairs attacked.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_73']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_73}Ovagos is a captain in the service of Lord Angbor.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_73_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_73_1}Popilia is a noblewoman of Lamedon.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Empire West 8 -->
	<xsl:template match="Hero[@id='lord_1_71']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_71}Golasgil, Lord of Anfalas, commands the long coastline against Corsair raids.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_71_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_WE8_c']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_WE8_c}Child of Lord Golasgil, learning to defend the coast.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_WE8_u']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_WE8_u}A seasoned captain who advises the Lord of Anfalas.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Empire West 9 -->
	<xsl:template match="Hero[@id='lord_WE9_l']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_WE9_l}A lord of the western fiefs, steadfast in his loyalty to Gondor.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_WE9_u']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_WE9_u}A nobleman known for political cunning and wise counsel.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_WE9_u2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_WE9_u2}Spouse of a western lord, managing the household.</xsl:attribute>
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
			<xsl:attribute name="text">{=TAOM_hero_1_27}Maugrash is an Orc captain who has risen through sheer brutality and cunning.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_27_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_27_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_27_1}Verina is a Black Numenorean sorceress in service to the Dark Tower.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_27</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_27_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_27_2}Throznak leads raiding parties into the western lands.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_1_27</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_1_27_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_27_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_27_3}Vasilia serves the Dark Lord in the shadows.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_1_27</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_1_27_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_37']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_37}Goshank commands a regiment of Mordor orcs with cruel efficiency.</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_1_14</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_47']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_47}Ulbos is an Orc taskmaster, driving slaves in the forges of Mount Doom.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_47_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_47_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_47_1}Mina serves the dark powers of Mordor.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_47</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_47_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_47_2}Casyrea is a servant of the Shadow.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_1_47</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_1_47_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_47_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_47_3}Colambea dwells in the darkness of Mordor.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_1_47</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_1_47_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Empire South 2 -->
	<xsl:template match="Hero[@id='lord_1_15']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_15}The Witch-King of Angmar, Lord of the Nazgul, is Sauron's most terrible servant. He was once a great king of Men, corrupted by his Ring of Power into an undying wraith.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_155']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_155}The Dark Marshall commands the Nazgul in battle, second only to the Witch-King.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_16']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_16}The Knight of Umbar was once a lord of the Corsairs before taking the Ring.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_28']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_28}The Betrayer was once a king in the East before his corruption.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_38']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_38}The Undying was a sorcerer-king who sought immortality and found only eternal servitude.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_48']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_48}Khamul the Easterling, second of the Nine, once ruled a great kingdom in Rhun before accepting a Ring of Power.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_48_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_48_1}The Tainted spreads corruption wherever he rides.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_48_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_48_2}The Shadow of the Northmen hunts in the cold lands.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_48_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_48_3}The Shadow of Umbar haunts the southern coasts.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Empire South 3 -->
	<xsl:template match="Hero[@id='lord_1_177']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_177}Honoratus serves as an officer in the armies of Mordor.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_1_17</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_1_18</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_29']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_29}Sanion is a Black Numenorean captain who commands a fortress.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_1_17</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_1_18</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_17']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_17}Sauron the Great, Dark Lord of Mordor. This entry exists only to display his terrible armor.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_18']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_18}Jathea serves in the dark hierarchy of Mordor.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_39']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_39}Debana is a servant of the Shadow, skilled in dark arts.</xsl:attribute>
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
			<xsl:attribute name="text">{=TAOM_hero_1_49}Obron leads a company of Mordor orcs.</xsl:attribute>
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
			<xsl:attribute name="text">{=TAOM_hero_1_30_2}Callinia serves the dark powers of Mordor.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_1_30</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_1_30_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_30_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_30_3}Synesios is a servant of the Shadow.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_1_30</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_1_30_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_1_56_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_56_2}Rustica serves in the armies of darkness.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_1_56</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_1_56_1</xsl:attribute>
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
			<xsl:attribute name="text">{=TAOM_hero_1_74}Zachanis commands a regiment of orcs.</xsl:attribute>
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
			<xsl:attribute name="text">{=TAOM_hero_1_68}Tharos is an Orc captain serving in the pass.</xsl:attribute>
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
			<xsl:attribute name="text">{=TAOM_hero_1_69}Niphon commands a fortress on the borders of Mordor.</xsl:attribute>
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
			<xsl:attribute name="text">{=TAOM_hero_SE8_c}An Orc bred in the pits of Mordor.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Empire South 9 -->
	<xsl:template match="Hero[@id='lord_SE9_l']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_SE9_l}A Black Numenorean lord who commands a fortress in Mordor.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_SE9_s</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_SE9_s']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_SE9_s}A Black Numenorean sorceress of dark power.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_SE9_l</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_SE9_c1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_SE9_c1}Heir to a Black Numenorean house, trained in dark arts.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_SE9_l</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_SE9_s</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_SE9_c2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_SE9_c2}Second child of the Black Numenorean lord.</xsl:attribute>
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
			<xsl:attribute name="text">{=TAOM_hero_2_3}Thorin III Stonehelm, King under the Mountain, rules the Dwarves of Erebor. He maintains the ancient alliance with Dale and stands ready to defend against the Shadow.</xsl:attribute>
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
			<xsl:attribute name="text">{=TAOM_hero_2_5}Ori is a Dwarf lord descended from one of Thorin Oakenshield's companions. He manages the trade routes between Erebor and Dale.</xsl:attribute>
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
			<xsl:attribute name="text">{=TAOM_hero_2_16}Bofur is a Dwarf lord of Erebor, descendant of the legendary company.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_2_16_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_2_16_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_2_16_1}Tyaska is wife to Bofur, skilled in the healing arts.</xsl:attribute>
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
			<xsl:attribute name="text">{=TAOM_hero_2_17}Thorin II Oakenshield is remembered as the hero who reclaimed Erebor from Smaug. His spirit inspires his descendants.</xsl:attribute>
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
			<xsl:attribute name="text">{=TAOM_hero_2_18}Gloin is a Dwarf lord of Erebor, father to Gimli of the Fellowship.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_2_18_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_2_18_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_2_18_1}Zorika is wife to Gloin, mother of the famous Gimli.</xsl:attribute>
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
			<xsl:attribute name="text">{=TAOM_hero_2_19}Dwalin is a Dwarf warrior of great renown, last survivor of the original company.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_2_19_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_2_19_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_2_19_1}Vitomira serves alongside Dwalin.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_2_19</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Sturgia Clan 8 -->
	<xsl:template match="Hero[@id='lord_2_20']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_2_20}Oin is a Dwarf elder, keeper of ancient lore and healing arts.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_2_20_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_2_20_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_2_20_1}Kisha is wife to Oin, skilled in herb lore.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_2_20</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_S8_u']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_S8_u}A grizzled veteran who guards the mountain halls.</xsl:attribute>
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
			<xsl:attribute name="text">{=TAOM_hero_3_16}Rumil is a chieftain of the golden banner tribes.</xsl:attribute>
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
			<xsl:attribute name="text">{=TAOM_hero_4_1}Theoden, King of Rohan, rules from Meduseld in Edoras. Once bewitched by Saruman's servant Grima, he was freed by Gandalf and led his people to glory at Helm's Deep and the Pelennor Fields.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_4_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_2}Elfhild was queen of Rohan, mother of Theodred. She died in childbirth, a grief that shadowed Theoden's reign.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_4_7']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_7}Theodred was the only son of King Theoden, Prince of Rohan. He fell defending the Fords of Isen against Saruman's Uruk-hai.</xsl:attribute>
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
			<xsl:attribute name="text">{=TAOM_hero_4_15}Erdurand is a rider of Theoden's household.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Vlandia Clan 2 - Eoforing (Eomer's family) -->
	<xsl:template match="Hero[@id='lord_4_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_3}Theodwyn was sister to King Theoden and mother of Eomer and Eowyn. She died of grief after her husband Eomund was slain by Orcs.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_rohan_10_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_4_3_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_3_1}Eomer Eadig, Third Marshal of the Mark, is nephew to Theoden and heir to the throne. He leads the riders of the Eastfold with fierce loyalty.</xsl:attribute>
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
			<xsl:attribute name="text">{=TAOM_hero_4_8}Furnhard serves as a trusted rider under Eomer's command.</xsl:attribute>
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
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_6}Grimbold of Grimslade commands the riders of Westfold. A fierce warrior, he held the Fords of Isen and later died heroically at the Pelennor.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_4_6_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_4_6_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_6_1}Deorwyn is heir to Grimbold's command.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_4_6</xsl:attribute>
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
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_9}Thomund commands a company of Westfold riders.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_4_12']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_12}Silvind is a shieldmaiden of the Westfold.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_4_121']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_121}Lasand serves in the Westfold garrison.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Vlandia Clan 4 - Erkenbrand's family -->
	<xsl:template match="Hero[@id='lord_4_16']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_16}Erkenbrand, Lord of Westfold, commands the fortress of the Hornburg. He arrived with Gandalf at Helm's Deep to turn the tide of battle.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_4_16_2</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_4_16_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_16_1}Merthu is wife to Erkenbrand, Lady of the Hornburg.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_4_16</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_4_17']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_17}Elbet serves in the household of Erkenbrand.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_4_18']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_18}Amalgun is heir to Erkenbrand.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_4_181']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_181}Arthamund serves as a captain of the Hornburg garrison.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_4_19']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_19}Asela is a shieldmaiden who guards the Deeping Coomb.</xsl:attribute>
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
			<xsl:attribute name="text">{=TAOM_hero_4_21}Cuthraed Ordlacing patrols the eastern borders.</xsl:attribute>
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
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
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
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_23}Marhath leads horse breeders of the southern grasslands.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_4_23_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_23_1}Wulfwynn is daughter of Marhath, a skilled rider.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_4_23_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_23_2}Eleduran trains warhorses for the king's army.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_4_23</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_4_23_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_4_23_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_23_3}Eleduran the younger serves as a horse warden.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_4_24']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_24}Grima Wormtongue was Theoden's counselor, secretly a spy for Saruman. His whispered lies poisoned the king's mind until Gandalf broke the spell.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_4_24_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_24_1}Eowyn, Lady of Rohan, is sister to Eomer and niece to Theoden. She slew the Witch-King at the Pelennor, striking down the terror of the ages.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_4_24_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_24_2}Elfgrim serves as a rider of the royal household.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_4_24_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_24_3}Herubrand guards the king's hall.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_4_24_4']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_24_4}Siegeberht patrols the borders of Rohan.</xsl:attribute>
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
			<xsl:attribute name="text">{=TAOM_hero_4_27}Aelle Aethellafing guards the Folds against Dunlending raids.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_4_27_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_4_27_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_27_1}Waerburg is wife to Aelle.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_4_27</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_V9_u']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_V9_u}A seasoned rider who advises the local thane.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_4_28']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_28}Fasthelm Morcargas commands the southern watch posts.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

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
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father']"/>
			<xsl:attribute name="text">{=TAOM_hero_4_28_2}Hereswith is daughter of the southern watch commander.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_4_28</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- ======================= KINGDOM 5: KHAND (Battania) ======================= -->

	<xsl:template match="Hero[@id='lord_5_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_5_1}Vargul, the High Warlord of Khand, leads the fierce Variag horsemen. His alliance with Mordor brings his warriors against Gondor in the war for Middle-earth.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_5_1_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father']"/>
			<xsl:attribute name="text">{=TAOM_hero_5_1_1}A warrior of the High Warlord's retinue.</xsl:attribute>
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
			<xsl:attribute name="text">{=TAOM_hero_5_7}A Variag warrior chieftain of Khand.</xsl:attribute>
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
			<xsl:attribute name="text">{=TAOM_hero_6_4}Gurtilm Salurian commands the western armies of Rhun. After her husband's death, she took command with iron resolve.</xsl:attribute>
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
			<xsl:attribute name="text">{=TAOM_hero_6_8}Esur is a warrior of Gurtilm's host.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_6_3</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_6_4</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_6_81']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_6_81}Nayantai serves as a captain in Gurtilm's army.</xsl:attribute>
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
			<xsl:attribute name="text">{=TAOM_hero_6_11}Yana is a noble of Gurtilm's host.</xsl:attribute>
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
			<xsl:attribute name="text">{=TAOM_hero_6_17}Amdur Amdurid leads a powerful clan of the eastern steppes.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_6_17_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_6_17_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_6_17_1}Ergene is wife to Amdur.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_6_17</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_6_17_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'father' and local-name() != 'mother']"/>
			<xsl:attribute name="text">{=TAOM_hero_6_17_2}Yesum is heir to the Amdurid holdings.</xsl:attribute>
			<xsl:attribute name="father">Hero.lord_6_17</xsl:attribute>
			<xsl:attribute name="mother">Hero.lord_6_17_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_6_18']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_6_18}Luthkan Khundolar commands warriors of the western provinces.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_6_18_1</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_6_18_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_6_18_1}Tilun is wife to Luthkan.</xsl:attribute>
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
			<xsl:attribute name="text">{=TAOM_hero_6_19}Vakheraltan Khundolar is brother to Luthkan, commanding cavalry scouts.</xsl:attribute>
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
			<xsl:attribute name="text">{=TAOM_hero_K8_u}A seasoned warrior who advises the northern chieftain.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_6_21']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_6_21}Molluk Illnoria commands a powerful clan of horse warriors.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_6_21_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_6_21_1}Esachei is wife to Molluk.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_6_22']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_6_22}Shakhal II Shakhalian rules a wealthy trading clan of the Sea of Rhun.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_6_22_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_6_22_1}Eselen is wife to Shakhal.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_6_23']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_6_23}Huz-Margoz Huz commands fortress garrisons along the western frontier.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_6_24']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_6_24}Nikath Adekig guards the southern passes into Rhun.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- ======================= MINOR CLANS ======================= -->

	<!-- Aserai 9 -->
	<xsl:template match="Hero[@id='lord_A9_l']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_A9_l}A Haradrim chieftain of the southern wastes.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_A9_s']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_A9_s}Wife to the chieftain of the southern wastes.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_A9_c']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_A9_c}Heir to the southern waste chieftain.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_A9_u']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_A9_u}A warrior of the southern tribes.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Vlandia 11 -->
	<xsl:template match="Hero[@id='lord_V11_l']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_V11_l}A Rohirrim lord of the western vales.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_V11_u']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_V11_u}A veteran rider who advises the lord.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_V11_c1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_V11_c1}Heir to the western vale holdings.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_V11_c2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_V11_c2}Second child of the western vale lord.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Sturgia 9 -->
	<xsl:template match="Hero[@id='lord_S9_l']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_S9_l}A noble of Dale who guards the northern approaches.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_S9_m']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_S9_m}Matriarch of a northern Barding clan.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_S9_c']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_S9_c}Young heir to the northern holdings.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_S9_u']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_S9_u}A seasoned warrior of the northern garrison.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Khuzait 9 -->
	<xsl:template match="Hero[@id='lord_K9_l']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_K9_l}A chieftain of the distant Easterling tribes.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_K9_s']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_K9_s}Wife to the distant chieftain.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_K9_c1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_K9_c1}Heir to the distant tribal holdings.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_K9_c2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_K9_c2}Second child of the distant chieftain.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Battania 8 -->
	<xsl:template match="Hero[@id='lord_B8_l']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_B8_l}A Variag warlord of the eastern hills.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_B8_s']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_B8_s}Wife to the Variag warlord.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Hero[@id='lord_B8_c']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=TAOM_hero_B8_c}Heir to the Variag warlord.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

</xsl:stylesheet>
