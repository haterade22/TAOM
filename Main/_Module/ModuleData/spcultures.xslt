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
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'text']"/>
			<xsl:attribute name="name">{=TAOM_empire_culture}Dunlending</xsl:attribute>
			<xsl:attribute name="text">{=TAOM_empire_desc}The Dunlendings are a rugged and fierce people, dwelling in the wild hills and untamed lands west of Rohan. Long resentful of the Rohirrim, whom they see as invaders of their ancestral homes, the Dunlendings are united by their shared heritage and fierce independence. Skilled in guerrilla warfare and survival, they rely on cunning and ferocity to overcome their enemies. Though often dismissed as simple hill folk, the Dunlendings are a proud and enduring people, fiercely protective of their lands and ways of life. Driven by a deep-seated grudge against Rohan, they have allied themselves with the forces of darkness, seeking revenge and the restoration of their lost lands.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Rename sturgia to Barding -->
	<xsl:template match="Culture[@id='sturgia']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'text']"/>
			<xsl:attribute name="name">{=TAOM_sturgia_culture}Barding</xsl:attribute>
			<xsl:attribute name="text">{=TAOM_sturgia_desc}The Bardings of Dale, named for Bard the Bowman, are a proud and industrious people who rose to prominence after reclaiming their homeland from the shadow of Smaug. Nestled between the Lonely Mountain and the Long Lake, Dale thrives as a hub of trade and culture. Known for their resilience and craftsmanship, the Bardings excel in forging weapons and armor, rivaling even the Dwarves of Erebor. Their armies, composed of disciplined archers, stalwart swordsmen, and agile skirmishers, defend their lands with fierce determination. United under noble leaders, the Bardings are ever watchful, guarding against the encroaching darkness and preserving their rich heritage.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Rename aserai to Haradrim -->
	<xsl:template match="Culture[@id='aserai']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'text']"/>
			<xsl:attribute name="name">{=TAOM_aserai_culture}Haradrim</xsl:attribute>
			<xsl:attribute name="text">{=TAOM_aserai_desc}The Haradrim are a proud and fierce people hailing from the deserts and savannahs of the distant south. Renowned for their mastery of exotic beasts such as mumakil, their warriors combine skill, resilience, and deadly precision in battle. Harad is a land of extremes, with its scorching sun, golden sands, and vibrant oases. Its people are united by ancient traditions, loyalty to their tribes, and the call of their chieftains and kings. Often allied with the forces of darkness, the Haradrim are driven by vengeance against Gondor and the promise of power and dominion offered by Sauron.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Rename vlandia to Rohirrim -->
	<xsl:template match="Culture[@id='vlandia']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'text']"/>
			<xsl:attribute name="name">{=TAOM_vlandia_culture}Rohirrim</xsl:attribute>
			<xsl:attribute name="text">{=TAOM_vlandia_desc}The Rohirrim, or Horse-lords of Rohan, are renowned for their unmatched cavalry and deep bond with their steeds. Proud and free-spirited, they dwell in the rolling plains of the Riddermark, living in villages and strongholds like Edoras. Guided by their King and loyal to their people, the Rohirrim prize honor, loyalty, and courage. Their warriors ride fearlessly into battle, forming the backbone of Middle-earth's resistance against the shadow. Though steadfast in their traditions, the Rohirrim are ever ready to defend their land and allies, wielding spears, swords, and shields with precision.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Rename battania to Variag -->
	<xsl:template match="Culture[@id='battania']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'text']"/>
			<xsl:attribute name="name">{=TAOM_battania_culture}Variag</xsl:attribute>
			<xsl:attribute name="text">{=TAOM_battania_desc}The Variags of Khand are a fierce and warlike people, hailing from the dry and rugged lands east of Mordor. Known for their mercenary prowess and loyalty to Sauron, the Variags fight with unmatched ferocity. They ride swift warhorses into battle, wielding curved blades and long spears with deadly precision. Their bronze and crimson armor, adorned with intricate designs, reflects their proud and martial heritage. Divided into tribes and clans, the Variags unite under powerful warlords, bringing fear and chaos to the enemies of the Dark Lord.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Rename khuzait to Easterling -->
	<xsl:template match="Culture[@id='khuzait']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'text']"/>
			<xsl:attribute name="name">{=TAOM_khuzait_culture}Easterling</xsl:attribute>
			<xsl:attribute name="text">{=TAOM_khuzait_desc}The Easterlings are a diverse and fearsome people from the distant lands of Rhun. Known for their loyalty to Sauron, they march to war with precision and discipline, wielding curved blades, spears, and massive war-drums that echo across the battlefield. Their golden armor and crimson banners reflect their fierce pride and martial prowess. Divided into clans and tribes, the Easterlings unite under powerful chieftains to bring the might of Rhun against the Free Peoples of Middle-earth.</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>
</xsl:stylesheet>
