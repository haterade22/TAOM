<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
	<!-- Identity transformation - copies everything by default -->
	<xsl:output omit-xml-declaration="no" indent="yes"/>

	<xsl:template match="@*|node()">
		<xsl:copy>
			<xsl:apply-templates select="@*|node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- ======================= KINGDOM 1: EMPIRE (Dunland/Gondor/Mordor) ======================= -->

	<!-- Empire North (Dunland) -->
	<xsl:template match="NPCCharacter[@id='lord_1_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_1}Brenin Wulf, the Ironhand</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_2}Freya Wolfheart</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_1_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_1_1}Eldith Grey-Claw</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_1_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_1_2}Sigga Wyrmbane</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_1_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_1_3}Sigrun Boarfang</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_1_4']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_1_4}Thyra Bloodtusk</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_1_5']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_1_5}Hilda Bonecrusher</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_1_6']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_1_6}Yrsa, the Winter Boar</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_1_7']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_1_7}Freydis Oxmane</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_1_8']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_1_8}Eira Shadowclaw</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_1_9']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_1_9}Sifra Bonewalker</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_1_10']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_1_10}Haldis Redmist</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_1_11']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_1_11}Yrla Ghostpelt</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_1_12']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_1_12}Freya Clawrend</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_1_13']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_1_13}Gundrun Ironpaw</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_1_14']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_1_14}Morgith</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_1_15']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_1_15}Aelwyn Hawkeye</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_1_16']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_1_16}Brianna Wingdart</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_1_17']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_1_17}Branoc Feathershaft</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_3}Gorwulf, The Boar</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_4']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_4}Brunhild Ironclaw</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_5']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_5}Othric The Wild</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_6']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_6}Torvald Oxhorn</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Empire West (Gondor) -->
	<xsl:template match="NPCCharacter[@id='lord_1_7']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_7}Denethor II, Steward of Gondor</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_8']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_8}Vendelia</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_9']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_9}Imrahil, Prince of Dol Amroth</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_10']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_10}Melkea</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_11']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_11}Ciryandur</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_111']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_111}Casinon</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_12']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_12}Lysica</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Empire South (Mordor) -->
	<xsl:template match="NPCCharacter[@id='lord_1_14']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_14}Mouth of Sauron</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_15']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_15}Witch-King of Angmar</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_155']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_155}Nazgul, The Dark Marshall</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_16']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_16}Nazgul, The Knight of Umbar</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_17']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_17}Sauron - Just To View Armor</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_177']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_177}Honoratus</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_18']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_18}Jathea</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_20']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_20}Astrid Bearclaw</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_21']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_21}Fenrik the Red Wolf</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_22']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_22}Bjornric Strongarm</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_23']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_23}Jastion</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_24']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_24}Tadeos</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_25']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_25}Eronyx</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_26']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_26}Meritor</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_27']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_27}Maugrash</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_27_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_27_1}Verina</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_27_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_27_2}Throznak</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_27_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_27_3}Vasilia</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_28']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_28}Nazgul, The Betrayer</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_29']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_29}Sanion</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_30']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_30}Gothmog, Lieutenant of Morgul</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_30_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_30_1}Svala Redfang</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_30_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_30_2}Callinia</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_30_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_30_3}Synesios</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_31']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_31}Zrsa Blackfang</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_32']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_32}Eldra Boarsong</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_33']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_33}Brigid the Howling</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_34']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_34}Faramir</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_35']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_35}Amenon</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_36']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_36}Phaea</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_37']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_37}Goshank</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_38']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_38}Nazgul, the Undying</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_39']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_39}Debana</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_40']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_40}Dervorin</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_40_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_40_1}Catella</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_41']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_41}Beregund Wolfborn</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_411']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_411}Grimwulf Ironfang</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_42']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_42}Hrodgar Ironhide</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_422']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_422}Drengulf Irontusk</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_43']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_43}Gormund Oxflank</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_44']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_44}Nemos</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_45']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_45}Forlong, Lord of Lossarnach</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_45_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_45_1}Vanyalos</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_45_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_45_2}Brandir</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_45_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_45_3}Borlong</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_46']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_46}Milos</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_46_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_46_1}Seorgys</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_47']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_47}Ulbos</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_47_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_47_1}Mina</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_47_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_47_2}Casyrea</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_47_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_47_3}Colambea</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_48']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_48}Khamul, the Easterling</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_48_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_48_1}Nazgul, the Tainted</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_48_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_48_2}Nazgul, the Shadow of Northmen</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_48_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_48_3}Nazgul, the Shadow of Umbar</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_49']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_49}Obron</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_49_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_49_1}Tristania</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_49_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_49_2}Gordiana</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_50']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_50}Corwyn Raveneye</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_51']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_51}Haldric Talonstrike</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_52']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_52}Hirluin, Lord of Pinnath Gelin</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_52_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_52_1}Arador</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_52_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_52_2}Arvedui</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_53']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_53}Angbor, Lord of Lamedon</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_54']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_54}Shagrat, Captain of Cirith Ungol</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_54_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_54_1}Constalia</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_55']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_55}Mathmog</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_55_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_55_1}Megethia</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_56']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_56}Tormund, the Hammer</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_56_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_56_1}Gwenna</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_56_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_56_2}Rustica</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_57']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_57}Altenos</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_57_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_57_1}Sophalia</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_57_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_57_2}Jephalia</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_58']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_58}Gorvin the Fell</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_62']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_62}Sejaron</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_62_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_62_1}Arytha</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_63']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_63}Gorbag, Captain of Minas Morgul</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_63_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_63_1}Valaria</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_63_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_63_2}Comatasa</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_63_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_63_3}Elidilea</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_64']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_64}Thormund Grizzlyhew</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_66']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_66}Talric Crowcall</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_67']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_67}Eorwyn Featherbolt</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_68']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_68}Tharos</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_68_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_68_1}Silvina</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_69']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_69}Niphon</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_69_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_69_1}Areliana</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_69_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_69_2}Dorathila</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_70']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_70}Veyra the Shadow</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_71']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_71}Golasgil, Lord of Anfalas</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_72']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_72}Bolg</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_72_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_72_1}Viviana</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_73']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_73}Ovagos</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_73_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_73_1}Popilia</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_74']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_74}Zachanis</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_74_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_74_1}Zena</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_75']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_1_75}Boromir</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- ======================= KINGDOM 2: STURGIA (Dale) ======================= -->

	<xsl:template match="NPCCharacter[@id='lord_2_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_2_1}Bard II</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_2_2}Asta</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_2_3}Thorin III Stonehelm</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_4']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_2_4}Siga</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_4_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_2_4_1}Apolanea</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_5']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_2_5}Ori</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_6']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_2_6}Erta</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_7']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_2_7}Simir</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_7_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_2_7_1}Mimir</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_8']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_2_8}Urik</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_9']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_2_9}Lek</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_10']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_2_10}Valla</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_11']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_2_11}Idrun</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_111']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_2_111}Rozhivol</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_12']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_2_12}Svana</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_121']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_2_121}Osven</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_13']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_2_13}Vidar</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_13_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_2_13_1}Lilizha</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_13_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_2_13_2}Andruta</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_13_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_2_13_3}Luda</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_13_4']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_2_13_4}Teta</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_14']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_2_14}Isvan</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_14_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_2_14_1}Valkava</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_14_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_2_14_2}Zaverena</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_14_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_2_14_3}Vizhduna</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_15']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_2_15}Ratagost</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_15_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_2_15_1}Yachana</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_15_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_2_15_2}Milanka</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_15_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_2_15_3}Velina</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_15_4']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_2_15_4}Bovan</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_16']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_2_16}Bofur</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_16_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_2_16_1}Tyaska</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_17']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_2_17}Thorin II Oakenshield</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_17_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_2_17_1}Dracha</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_18']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_2_18}Gloin</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_18_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_2_18_1}Zorika</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_19']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_2_19}Dwalin</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_19_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_2_19_1}Vitomira</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_20']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_2_20}Oin</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_20_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_2_20_1}Kisha</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_21']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_2_21}Svedorn</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_21_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_2_21_1}Izdenka</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_22']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_2_22}Lashonek</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_22_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_2_22_1}Zheneva</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_23']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_2_23}Galden</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_23_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_2_23_1}Zlatka</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_24']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_2_24}Alvar</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_24_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_2_24_1}Zorina</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- ======================= KINGDOM 3: ASERAI (Harad) ======================= -->

	<xsl:template match="NPCCharacter[@id='lord_3_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_3_1}Khadurak</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_3_2}Harad Place Holder</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_3_3}Calemir</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_3_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_3_3_1}Tariq</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_4']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_3_4}Maraa</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_5']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_3_5}Haldir</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_51']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_3_51}Haqan</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_6']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_3_6}Ruma</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_7']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_3_7}Dhiyul</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_8']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_3_8}Addas</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_9']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_3_9}Usair</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_10']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_3_10}Anidha</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_11']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_3_11}Arwa</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_12']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_3_12}Manan</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_13']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_3_13}Nuqar</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_13_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_3_13_1}Sira</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_13_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_3_13_2}Razana</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_14']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_3_14}Thamza</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_14_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_3_14_1}Sasaitha</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_15']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_3_15}Ghuzid</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_15_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_3_15_1}Shimra</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_15_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_3_15_2}Bushila</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_16']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_3_16}Rumil</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_16_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_3_16_1}Farina</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_17']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_3_17}Orophin</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_17_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_3_17_1}Shaima</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_17_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_3_17_2}Sanit</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_18']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_3_18}Aranthon</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_18_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_3_18_1}Farzana</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_18_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_3_18_2}Hafisa</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_18_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_3_18_3}Zuad</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_18_4']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_3_18_4}Jalfar</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_19']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_3_19}Aethirion</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_19_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_3_19_1}Salma</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_19_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_3_19_2}Zulaika</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_19_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_3_19_3}Sulhana</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_20']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_3_20}Karith</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_20_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_3_20_1}Judira</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_20_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_3_20_2}Azina</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_21']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_3_21}Ukhai</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_21_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_3_21_1}Ashisa</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_22']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_3_22}Duilin</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_22_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_3_22_1}Yamina</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_22_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_3_22_2}Suna</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_22_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_3_22_3}Zanuwa</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_22_4']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_3_22_4}Hajara</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_23']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_3_23}Qahin</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_23_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_3_23_1}Sukayna</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- ======================= KINGDOM 4: VLANDIA (Rohan) ======================= -->

	<xsl:template match="NPCCharacter[@id='lord_4_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_4_1}Theoden</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_4_2}Elfhild</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_4_3}Theodwyn Eoforing</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_3_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_4_3_1}Eomer Eoforing</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_4']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_4_4}Elthild</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_5']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_4_5}Unthery</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_6']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_4_6}Grimbold Grimingas</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_6_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_4_6_1}Deorwyn Grimingas</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_7']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_4_7}Theodred Eorling</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_8']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_4_8}Furnhard</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_9']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_4_9}Thomund</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_10']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_4_10}Elys</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_11']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_4_11}Liena</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_12']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_4_12}Silvind</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_121']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_4_121}Lasand</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_13']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_4_13}Romund</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_14']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_4_14}Morcon</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_141']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_4_141}Amorcon</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_15']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_4_15}Erdurand</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_16']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_4_16}Erkenbrand Felanding</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_16_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_4_16_1}Merthu Felanding</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_17']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_4_17}Elbet</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_18']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_4_18}Amalgun</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_181']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_4_181}Arthamund</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_19']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_4_19}Asela</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_20']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_4_20}Varmund</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_20_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_4_20_1}Ingeltrud</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_21']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_4_21}Cuthraed Ordlacing</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_22']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_4_22}Wulf Celmunding</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_22_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_4_22_1}Sunnifa Celmunding</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_23']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_4_23}Marhath Marhad</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_23_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_4_23_1}Wulfwynn Marhad</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_23_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_4_23_2}Eleduran Marhad</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_23_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_4_23_3}Eleduran</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_24']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_4_24}Grima Grimmoding</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_24_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_4_24_1}Eowyn Eoforing</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_24_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_4_24_2}Elfgrim Grimingas</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_24_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_4_24_3}Herubrand Felanding</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_24_4']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_4_24_4}Siegeberht Ordlacing</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_25']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_4_25}Lucand</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_25_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_4_25_1}Bertliana</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_26']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_4_26}Peric</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_26_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_4_26_1}Reingarda</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_27']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_4_27}Aelle Aethellafing</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_27_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_4_27_1}Waerburg Aethellafing</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_28']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_4_28}Fasthelm Morcargas</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_28_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_4_28_1}Morcar</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_28_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_4_28_2}Hereswith</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- ======================= KINGDOM 5: BATTANIA (Khand) ======================= -->

	<xsl:template match="NPCCharacter[@id='lord_5_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_5_1}Vargul, the High Warlord of Khand</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_1_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_5_1_1}RandomDude</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_5_3}Ergeon</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_3_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_5_3_1}Ranaon</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_3_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_5_3_2}Ladogual</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_4']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_5_4}Nywin</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_5']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_5_5}Melidir</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_5_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_5_5_1}Eilidh</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_6']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_5_6}Alcaea</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_7']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_5_7}Khand PlaceHolder</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_8']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_5_8}Sein</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_9']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_5_9}Culharn</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_91']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_5_91}Tegan</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_10']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_5_10}Corein</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_11']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_5_11}Alynneth</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_12']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_5_12}Wythuin</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_13']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_5_13}Muinser</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_131']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_5_131}Rath</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_13_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_5_13_1}Beasag</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_14']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_5_14}Luichan</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_14_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_5_14_1}Eabyr</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_15']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_5_15}Pryndor</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_15_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_5_15_1}Floraidh</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_15_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_5_15_2}Beitrin</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_15_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_5_15_3}Diarbhain</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_16']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_5_16}Aeron</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_16_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_5_16_1}Liasin</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_16_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_5_16_2}Gawen</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_17']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_5_17}Aradwyr</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_17_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_5_17_1}Brighan</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_18']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_5_18}Branoc</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_18_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_5_18_1}Seonag</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_19']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_5_19}Fenagan</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_20']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_5_20}Siaramus</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_21']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_5_21}Carfyd</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_21_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_5_21_1}Beathag</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_21_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_5_21_2}Taorse</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_22']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_5_22}Fiarad</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- ======================= KINGDOM 6: KHUZAIT (Rhun) ======================= -->

	<xsl:template match="NPCCharacter[@id='lord_6_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_6_1}Zhamik Zhamian</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_6_2}Maraia</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_3']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_6_3}Bagai</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_4']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_6_4}Gurtilm Salurian</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_5']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_6_5}Irbo Nikathian</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_51']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_6_51}Khada</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_6']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_6_6}Suran</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_7']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_6_7}Chaghan</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_8']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_6_8}Esur</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_81']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_6_81}Nayantai</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_9']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_6_9}Temun</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_10']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_6_10}Alijin</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_101']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_6_101}Bolat</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_11']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_6_11}Yana</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_12']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_6_12}Abagai</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_13']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_6_13}Bortu</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_15']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_6_15}Oragur</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_15_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_6_15_1}Khorijin</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_15_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_6_15_2}Sechen</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_16']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_6_16}Akvoth Karmian</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_16_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_6_16_1}Chambui</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_16_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_6_16_2}Unagen</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_17']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_6_17}Amdur Amdurid</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_17_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_6_17_1}Ergene</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_17_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_6_17_2}Yesum</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_18']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_6_18}Luthkan Khundolar</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_18_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_6_18_1}Tilun</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_18_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_6_18_2}Chagun</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_19']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_6_19}Vakheraltan Khundolar</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_19_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_6_19_1}Sokhatai</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_19_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_6_19_2}Korte</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_20']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_6_20}Khurubra Mashakian</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_20_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_6_20_1}Jigur</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_21']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_6_21}Molluk Illnoria</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_21_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_6_21_1}Esachei</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_22']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_6_22}Shakhal II Shakhalian</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_22_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_6_22_1}Eselen</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_23']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_6_23}Huz-Margoz Huz</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_24']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'name']"/>
			<xsl:attribute name="name">{=TAOM_lord_6_24}Nikath Adekig</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

</xsl:stylesheet>
