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


	<!-- ======================= FACE/BODYPROPERTIES TRANSFORMATIONS ======================= -->
	<!-- These templates override vanilla BodyProperties with LOTR-themed appearances -->
	<xsl:template match="NPCCharacter[@id='lord_1_1']/face">
		<face>
			<BodyProperties version="4" weight="0.3009" build="0.9977" key="00180800811414C2878F1FFF4F9F1EEE7F018877888888888888888F7718F5F5009F560309888888000000000000000000000000000000000000000043244142"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_2']/face">
		<face>
			<BodyProperties version="4" weight="0.6213" build="0.5278" key="000D74060814100168588D58B57BB7342987B6DA87388A678888888F8588656A0068960306888888000000000000000000000000000000000000000042CC1103"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_1_1']/face">
		<face>
			<BodyProperties version="4" weight="0.6213" build="0.5278" key="000DB0054F14200F777D66238AC2D652B789B895A5C5A788785C677564695D840068960306654796000000000000000000000000000000000000000042CC30C3"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_1_2']/face">
		<face>
			<BodyProperties version="4" weight="0.2051" build="0.2259" key="0018400781140004A7B67379AA5D43AB9A8574A83CB8794A545964E544A185CC0073360307A6835A00000000000000000000000000000000000000002EF44083"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_1_3']/face">
		<face>
			<BodyProperties version="4" weight="0.3278" build="0.5208" key="0018400FCE2C300153BC5AEB293A86574795A8AAA94684B62987569589672E26007856030767353900000000000000000000000000000000000000002EDC20C3"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_1_4']/face">
		<face>
			<BodyProperties version="4" weight="0.3278" build="0.5208" key="0018A40FCE2C000551C968762E626B3BBA56396A8CCE86366B478689697773B900785603072B7F7800000000000000000000000000000000000000002EDC1083"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_1_5']/face">
		<face>
			<BodyProperties version="4" weight="0.6389" build="0.9861" key="00155C0FCA2C300D3C6B797D7726C92756836865445B388DC6588889AB94CCC500BFA6030B29C34A00000000000000000000000000000000000000002F7C4103"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_1_6']/face">
		<face>
			<BodyProperties version="4" weight="0.4676" build="0.5023" key="0015840E4C2C300C48E753769785B8663E48599D438E731B1D578153584559560068760306B6967600000000000000000000000000000000000000002EBC00C3"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_1_7']/face">
		<face>
			<BodyProperties version="4" weight="0.4676" build="0.4745" key="000A9006CA44300A9A99991D9C769875AD89C74564583655C8A8A9DD949AB9CA0067760306C46DA800000000000000000000000000000000000000002ED400C3"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_1_8']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="00165C000014000EE93966A9966848AA5746E9558867A392654767867CA775120197761309B278C50000000000000000000000000000000000000000593C30C3"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_1_9']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0011980E40B400053A8CBE5B2B965C4467898ACEB6A65CBA147249983692C3950167761306B469D400000000000000000000000000000000000000005AD40083"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_1_10']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="000BEC0E51143006C8BA78AAC869CA6992CA8461387A78A63736488C67497CC9013776130357449200000000000000000000000000000000000000003C642084"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_1_11']/face">
		<face>
			<BodyProperties version="4" weight="0.4" build="0.4" key="0016280B40A0000DB8DDDB4B342988B1C25BA5786832765C997877AB8579697401B776130B78386A00000000000000000000000000000000000000001D7C00C3"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_1_15']/face">
		<face>
			<BodyProperties version="4" weight="0.2685" build="0.2662" key="00001004C9A0100D2BC272AA728143CDC46386D689785734584896A7987613AC00744603078AC199000000000000000000000000000000000000000042E41083"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_1_16']/face">
		<face>
			<BodyProperties version="4" weight="0.2685" build="0.2662" key="00001004CAA000032B4A77BC993C89497B2786D689785734DA946A6D4229EA9300544603057983BB000000000000000000000000000000000000000042AC1103"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_3']/face">
		<face>
			<BodyProperties version="4" weight="0.2051" build="0.2259" key="0018900FC22C29029E693487686495249959879685AD41D37B7BC6A72C72B2C5007336030746B8C400000000000000000000000000000000000000002EF41102"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_4']/face">
		<face>
			<BodyProperties version="4" weight="0.3278" build="0.5208" key="0018900FC32C0004277D1767865C6D65C99ABB9D9A854973C599D87C96798E5B00785603076C374A00000000000000000000000000000000000000002EDC4083"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_5']/face">
		<face>
			<BodyProperties version="4" weight="0.4676" build="0.8519" key="000A7C0E434413144D3B8B8895B7BAA1B892A95A7777C459352A36B6B836977600AD76030A78257900000000000000000000000000000000000000002F543002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_6']/face">
		<face>
			<BodyProperties version="4" weight="0.4676" build="0.4745" key="000A5006C644045997988BCD74A733AD17786B9C6A5788AD62AA34835485628A0087760308A5527600000000000000000000000000000000000000002EFC10C2"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_7']/face">
		<face>
			<BodyProperties version="4" weight="0.4583" build="0.1343" key="0002540F007010024A6A02B8F08300011060730012EEF7017B0B010E7E918F4500927603091485F7000000000000000000000000000000000000000021342142"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_8']/face">
		<face>
			<BodyProperties version="4" weight="0.5507" build="0.5759" key="000BDC0A000020028672362967865369A91887176B723646DCA3773773849A54056986570636A93300000000000000000000000000000000000000004ADC2082"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_9']/face">
		<face>
			<BodyProperties version="4" weight="0.0185" build="0.7199" key="000A480FC00000022F70080860528544369289B2BF8A7E05776961088DB01839009B06030966530400000000000000000000000000000000000000003D3C1002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_9_1']/face">
		<face>
			<BodyProperties version="4" weight="0.2292" build="0.7361" key="0000480E400003415870080A6671BA68776789B2BFA60E52754B68098F804B2600AB36030A49A30600000000000000000000000000000000000000003D4C2002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_9_2']/face">
		<face>
			<BodyProperties version="4" weight="0.2431" build="0.9884" key="0000480FC000001C5870380D66C2D60A7B778AB2B7A80E5273593A0C86C04645008F46030805550300000000000000000000000000000000000000003D1C7002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_9_3']/face">
		<face>
			<BodyProperties version="4" weight="0.2292" build="0.7361" key="00002C0E400003C88770080F61B1F06577C789FBB2A9BE13756905058FB04F0200AB36030A45A30100000000000000000000000000000000000000003D4C1002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_9_4']/face">
		<face>
			<BodyProperties version="4" weight="0.2847" build="0.4352" key="000A580E50140004108730267EF0FB24C4B0881608638801789AF94F7FFF812400A746030A49B7081F64106050200040000000000000176700000000434C1003"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_9_5']/face">
		<face>
			<BodyProperties version="4" weight="0" build="0.2778" key="0002540E40B430150A7000391900C1012A278BFF80A61508B0090A0F7FFBA8230074060307176B051F6410605020004000000000000017670000000042EC1003"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_10']/face">
		<face>
			<BodyProperties version="4" weight="0.4241" build="0.7095" key="000BAC0A00141004B77326194AC4402599338A6798924403D8C24F3379749A46047B6646071634C300000000000000000000000000000000000000001EF41083"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_25']/face">
		<face>
			<BodyProperties version="4" weight="0.7886" build="0.5725" key="000BAC088000368E94800B923B3A288737AC765AF2D7A91197858F34731640730199C013097777C600000000000000000000000000000000000000005D204000"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_35']/face">
		<face>
			<BodyProperties version="4" weight="0.6154" build="0.6899" key="00057C04400020029673264B47856554EA72881968858A46D9A28774766555770374963507334537000000000000000000000000000000000000000020FC2003"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_11']/face">
		<face>
			<BodyProperties version="4" weight="0.0486" build="0.3495" key="0008A004001403840B8F40126041750131F6838530990000BB79500E79FBFEA200A516030A8BB57C1F6410605020004000000000000017670000000043444003"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_11_1']/face">
		<face>
			<BodyProperties version="4" weight="0.0486" build="0.5741" key="000A240D8000368300802F541370F012ACFA80F5B172060707683B067F945E01009916030905C6140000000000000000000000000000000000000000331C9002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_11_2']/face">
		<face>
			<BodyProperties version="4" weight="0.2407" build="0.875" key="00058C08C00003884E801A1E1808F012A9A88397B1ABD60167584B0C7F9A5C0100AD46030A839A16000000000000000000000000000000000000000033543002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_11_3']/face">
		<face>
			<BodyProperties version="4" weight="0.2731" build="0.6366" key="000AB00B90B40015818010816860B102710789D3D8B91A01800AAF0D75CE59D3007A4603070878961F6410605020004000000000000017670000000044F42003"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_11_4']/face">
		<face>
			<BodyProperties version="4" weight="0" build="0.3218" key="0005940D92143015A78040307EC1CF01A7D88B8F04930820994B09087CF97E0400850603080A8B0A1F6410605020004000000000000017670000000043141003"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_11_5']/face">
		<face>
			<BodyProperties version="4" weight="0.3912" build="0.9583" key="0005D00B9114300501801000FFA0E105A15187D3E3901002802D9609736E0D66009E66030946D7071F64106050200040000000000000176700000000453C2003"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_111']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.4" key="000BD0058000328878DAE579759C4B844D469CDC9A3683A59A15AF927269E22C01F004780F5D7806000000000000000000000000000000000000000000C01184"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_12']/face">
		<face>
			<BodyProperties version="4" weight="0.4" build="0.5" key="000CA00E40BC2005478D688968868296A6779BB376D6A724D866C7B9A9A97B7D0007077C0D629B660000000000000FFE00000000000000000000000000001080"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_14']/face">
		<face>
			<BodyProperties version="4" weight="0.1" build="0.99" key="0000F10E90FB50046958D3288B71766558599899863A9B25B8E39F6A978897400090876088487420000000000000000000000000000000000000000013C4040"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_15']/face">
		<face>
			<BodyProperties version="4" weight="0.0116" build="1" key="0039F00FC000300010000010010100000001000000000100001011101000000000FF06030F000100000000000000000000000000000000000000000043F44142"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_155']/face">
		<face>
			<BodyProperties version="4" weight="0.0116" build="1" key="0039F00FC000300010000010010100000001000000000100001011101000000000FF06030F000100000000000000000000000000000000000000000043F44142"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_16']/face">
		<face>
			<BodyProperties version="4" weight="0.0116" build="1" key="0039F00FC000300010000010010100000001000000000100001011101000000000FF06030F000100000000000000000000000000000000000000000043F44142"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_17']/face">
		<face>
			<BodyProperties version="4" weight="0" build="0.0162" key="00000C0FC000300010000011110000000000000010010001011001101010000000F006030F001101000000000000000000000000000000000000000005F40142"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_177']/face">
		<face>
			<BodyProperties version="4" weight="0.6" build="0.6" key="0011CC0700000002755D98B886639678761559B8B7C879465D6A74A99895786C01E000090EA91E5500001105000030A30000001F0000000000000000009C2000"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_18']/face">
		<face>
			<BodyProperties version="4" weight="0.75" build="0.5" key="0005880FC000000E98AA77B76970A46A77E739675A3862468B759958865696BD00040008058788780000000000000FFE00000000000000000000000000000000"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_21']/face">
		<face>
			<BodyProperties version="4" weight="0.7019" build="0.3678" key="00026C09851423C2DE888372433B967E7F01649228AD4DB58888888F7718F5F500C6B6030C888888000000000000000000000000000000000000000043840142"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_22']/face">
		<face>
			<BodyProperties version="4" weight="0.3278" build="0.9412" key="0018540FC32C1A546669AC7C84CA7269A89E996472AB6A254BC5AC69693A675500BE56030BB8DD4800000000000000000000000000000000000000002F7C4042"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_23']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.7" key="0000B002C000300DEB67D938447261A7658B5985A9A6594E143D5345AAE756A7000522540598D94800001105000030A30000001F000000000000000000787000"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_24']/face">
		<face>
			<BodyProperties version="4" weight="0.6" build="0.5" key="0016F00580FC334D7B57826CA4C27CA67949E6ACA88B3ACD7A45DA9768858686000329A20373767B00001105000030A30000001F000000000000000000A06000"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_75']/face">
		<face>
			<BodyProperties version="4" weight="0.4236" build="0.7407" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_26']/face">
		<face>
			<BodyProperties version="4" weight="0.4" build="0.6" key="0016040E400003C67A776955EA791E79989C4B679A8C5255A77376D888C8D68800000DB2055C7BA600001105000030A30000001F000000000000000000048000"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_27']/face">
		<face>
			<BodyProperties version="4" weight="0.6" build="0.3" key="000B6C02C0BC100679D8455AA9684859888735557C6493786867627857B2895800058893058735D900001105000030A30000001F000000000000000001686000"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_27_1']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.2" key="0005B8071070000A987877861A2999B848987C68EA6BB7869423371443C8EA4801377613036AB66200000000000000000000000000000000000000005A6C3003"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_27_2']/face">
		<face>
			<BodyProperties version="4" weight="0.4" build="0.4" key="000B8802C0BC1005B9D8455AA96848594597AD77996A54B899D967BA74979778010008930FBA9A7900001105000030A30000001F000000000000000001683100"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_27_3']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.3" key="000B8802C0BC1005B9D8455AA96848594597AD77996A54B899D967BA74979778010008930FBA9A7900001105000030A30000001F000000000000000001683100"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_28']/face">
		<face>
			<BodyProperties version="4" weight="0.0116" build="1" key="0039F00FC000300010000010010100000001000000000100001011101000000000FF06030F000100000000000000000000000000000000000000000043F44142"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_29']/face">
		<face>
			<BodyProperties version="4" weight="0.4" build="0.6" key="000BA00B40001008A43A8AB8557888874A6A94868B767788988844778A69247900058005079777A800001105000030A30000001F000000000000000000CC0000"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_31']/face">
		<face>
			<BodyProperties version="4" weight="0.6213" build="0.5278" key="000DB80D0C141012B89AB3B6BA36B78C5289399A7B938997C8CDA86AA3BD1A270068960306C18788000000000000000000000000000000000000000042CC0083"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_32']/face">
		<face>
			<BodyProperties version="4" weight="0.3278" build="0.5208" key="0018700FC52C300A287C737B6258466A7B9869A85BB485996716C57DCCA973BB0078560307993A5700000000000000000000000000000000000000002EDC20C3"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_33']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.3" key="0005640B4000000287B86998A4156688BC87B56726542344956C8B7576556786000350040397A6680000000000000FFE00000000000000000000000000F00000"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_422']/face">
		<face>
			<BodyProperties version="4" weight="0.3278" build="0.5208" key="0018900FC42C0A00277D1767865C6D65C99ABB9D9A854973C599D87C96798E5B00785603076C374A00000000000000000000000000000000000000002EDC7102"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_34']/face">
		<face>
			<BodyProperties version="4" weight="0" build="0.5671" key="0000000400000182107973B1A81103B300A189083A697600991871193FB17D5300990603093A2B0532430001330223550000000000001977000000004D246002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_36']/face">
		<face>
			<BodyProperties version="4" weight="0.5664" build="0.7577" key="0011900A12142006A87321448876B529E920889893905436D8C49E55747A584504BB86450B3756930000000000000000000000000000000000000000716C3041"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_37']/face">
		<face>
			<BodyProperties version="4" weight="0.239" build="0.7027" key="001BF40E40FC10076C63396B58A46C7159DA89AADD789458B79BDE777836067600AB47030A8BE39B000000000000000000000000000000000000000001581180"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_38']/face">
		<face>
			<BodyProperties version="4" weight="0.0116" build="1" key="0039F00FC000300010000010010100000001000000000100001011101000000000FF06030F000100000000000000000000000000000000000000000043F44142"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_39']/face">
		<face>
			<BodyProperties version="4" weight="0.3025" build="0.5377" key="0005F00CC02810048774371C48B5C916EA6489378A81A846D9926C347476669603A856350A065B26000000000000000000000000000000000000000061441002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_41']/face">
		<face>
			<BodyProperties version="4" weight="0.3009" build="0.9977" key="0018440543141382878F1FFF461C01FF7F018877888888888888888F7718F5F5009F560309888888000000000000000000000000000000000000000043244142"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_411']/face">
		<face>
			<BodyProperties version="4" weight="0.3009" build="0.6157" key="000D6C0E47142902DEB8BE1AB5C571CD7F01649228AD4DB58888888F7718F5F50079560307888888000000000000000000000000000000000000000042FC0142"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_42']/face">
		<face>
			<BodyProperties version="4" weight="0.3278" build="0.6875" key="0018540FC42C345347C7A946DB43476574B24E5B263494B3C1A8D7A142978A98008A5603088816A600000000000000000000000000000000000000002F0C7002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_43']/face">
		<face>
			<BodyProperties version="4" weight="0.4676" build="0.4745" key="000A9006C744199A9A99991D9C769875AD89C74564583655C8A8A9DD949AB9CA0087760308C46DA800000000000000000000000000000000000000002EFC5002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_44']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="000BCC0CC0004008776C57C967305779596A85AB79877778859898977677345B0000000507A9688700001105000030A30000001F000000000000000000000003"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_47']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0011B40597BC038E94A8C26585AAA93992D5A78776779C82329B657437695D35000005A80DE9BA6200001105000030A30000001F000000000000000000000000"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_47_1']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="001CD00FD114000AE46398467C698988876DB2B863D57A6D87B96C58967BE7660187761308364A49000000000000000000000000000000000000000019142003"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_47_2']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="001C880A111400012857DAA3C88BD254869B9BB7C7796796A6987253285773960137761303A670BD00000000000000000000000000000000000000005A741083"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_47_3']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="00115C0A13702007997A77A685B88487865116663789A798A6A897926887369901A776130A8B49B500000000000000000000000000000000000000001F543083"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_49']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0011BC0CCE000102A5508899487841954B4F9A57D5C7D811C889B5068C4A7666012776130268769500000000000000000000000000000000000000003C4480C2"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_49_1']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0000200FD170300DB38B674C24E68B77237687A658475B47BC78592D78C593BB01C776130C2C8799000000000000000000000000000000000000000031840003"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_49_2']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0011B00CD2A03006743ADC795BA745B786DABD951B876537A8C6AA5B96B893590167761306485E6600000000000000000000000000000000000000004ACC00C3"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_20']/face">
		<face>
			<BodyProperties version="4" weight="0.3171" build="0.331" key="0008600D81FC3001AB6396829678635A9E988688E7437D4C394454A8DC55696B00B556030B73DC76000000000000000000000000000000000000000043742083"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_1_12']/face">
		<face>
			<BodyProperties version="4" weight="0.3171" build="0.331" key="0008780E42FC30076B9BCC4B879C2322B95787ECA9B959838E7C94A8754CD83800955603096BB79F000000000000000000000000000000000000000043340083"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_64']/face">
		<face>
			<BodyProperties version="4" weight="0.3171" build="0.2245" key="0008E00FCDFC1A547589847271756C397A5E664B96794B555718B95CE9C357830083560308938B28000000000000000000000000000000000000000043044142"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_1_13']/face">
		<face>
			<BodyProperties version="4" weight="0.3171" build="0.2245" key="0008E00FC8FC20087589847271756C397A5E664B96794B555718B95CE9C357830083560308938B28000000000000000000000000000000000000000043043083"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_50']/face">
		<face>
			<BodyProperties version="4" weight="0.2685" build="0.2662" key="00087C078158090285773B6C97789A495A4598548A747A346A86816BA837976B0074460307586266000000000000000000000000000000000000000042E41082"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_66']/face">
		<face>
			<BodyProperties version="4" weight="0.2685" build="0.2662" key="00001007835813922BC272AA728143CDC46386D689785734584896A7987613AC00744603078AC199000000000000000000000000000000000000000042E41082"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_1_14']/face">
		<face>
			<BodyProperties version="4" weight="0.2685" build="0.2662" key="00001007835810122BC272AA728143CDC46386D689785734584896A7987613AC00744603078AC199000000000000000000000000000000000000000042E41083"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_1_17']/face">
		<face>
			<BodyProperties version="4" weight="0.2685" build="0.2662" key="00087C07835809538579765AA95984A5844298548A747A34548663635992CD7B0074460307A8E449000000000000000000000000000000000000000042E41102"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_51']/face">
		<face>
			<BodyProperties version="4" weight="0.2685" build="0.2662" key="00001004C3A013922B4A77BC993C89497B2786D689785734DA946A6D4229EA9300544603057983BB000000000000000000000000000000000000000042AC1082"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_58']/face">
		<face>
			<BodyProperties version="4" weight="0.7" build="0.8" key="0016B40CC0BC334056A4C39BA7B77CCD78918637473D7C8CD5E5A4648B7DBC3A000004730C774E6800001105000030A30000001F000000000000000000001000"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_70']/face">
		<face>
			<BodyProperties version="4" weight="0.4" build="0.4" key="0000D40E4000228BED6277444258C875446A17B9ED8932923B61997568A479EB0000047303395E480000000000000FFE00000000000000000000000000004000"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_NE7_u']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0000D40E4000228BED6277444258C875446A17B9ED8932923B61997568A479EB0000047303395E480000000000000FFE00000000000000000000000000004000"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_67']/face">
		<face>
			<BodyProperties version="4" weight="0.2685" build="0.2662" key="00004C04C7A03782CB4C93C3247A752599557696691778A256BA99AB862778B9005446030566758B000000000000000000000000000000000000000042AC2102"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_40']/face">
		<face>
			<BodyProperties version="4" weight="0" build="0.6157" key="0002000D8014168128800978B7B7C03A848782691464BBA87556440C8D964309009906030997930300000000000000000000000000000000000000003B347002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_40_1']/face">
		<face>
			<BodyProperties version="4" weight="0.4" build="0.4" key="000BAC089200100DB976648E6774B835537D86629511323BDCB177278A84F667017776130748B49500000000000000000000000000000000000000003EFC4003"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_46']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0016B80A00003205954C59997345585A4969B5769B85789865677B96475832690000000307A7968800001105000030A30000001F000000000000000000000000"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_46_1']/face">
		<face>
			<BodyProperties version="4" weight="0.4" build="0.5" key="000B500A1114300BB69199998669E37568754C66A53693B2BA7A9855C96751D601677613062B368C000000000000000000000000000000000000000040CC4083"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_45']/face">
		<face>
			<BodyProperties version="4" weight="0.9769" build="0.9861" key="0005D40D80001A4304700800605DF10950388DB2BFEA1D017917FB0F8B00683B007FF60307911FFF00000000000000000000000000000000000000003CFC9002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_45_1']/face">
		<face>
			<BodyProperties version="4" weight="0.0023" build="0.5116" key="000AF80010FC0005B0801020FC60E000A74780010BBD1001654A350A7FFE930400A806030A97B6481F6410605020004000000000000017670000000045441003"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_45_2']/face">
		<face>
			<BodyProperties version="4" weight="0.1782" build="0.4236" key="0010D808C00010063E8415A71000FF529EC9833001BCE003075810017F905E52009636030980A70600000000000000000000000000000000000000003B2C4002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_45_3']/face">
		<face>
			<BodyProperties version="4" weight="0.4236" build="0.4236" key="000AD806C00011420E8015A716F0F012E910830001B90601675810067F9A5C04007666030720804000000000000000000000000000000000000000003AF43002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_45_4']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="000B500E518830018883625946BD4C74CD2A878665347B498B83461C3686D569012776130286C8B500000000000000000000000000000000000000004A442003"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_45_5']/face">
		<face>
			<BodyProperties version="4" weight="0.2176" build="0.4699" key="0005400551140001A180A0316F8175017750868F099900153779BC0C74955E040087360308B046981F6410605020004000000000000017670000000043141003"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_45_6']/face">
		<face>
			<BodyProperties version="4" weight="0" build="0.0093" key="0010E80C50000005818000C16601FE018369868F099900A9BB790A0A78B82F8500600603061082091F6410605020004000000000000017670000000042DC2003"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_57']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0016D40E407C334E269C3C75A7984761A6AE8C7B19959CAB9CB685D9565BB66A00058BB2057385AB00001105000030A30000001F000000000000000001409000"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_57_1']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="000BD80B5114300AC8832858A669275A3AB4CAA26D66686547C4969575DA72990177761307A39755000000000000000000000000000000000000000042E40003"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_57_2']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="00003C02D0CC200FCECB7755385C79B87764EA7A87439E635759266A4A5A653B01177613012CB56100000000000000000000000000000000000000004A344043"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_52']/face">
		<face>
			<BodyProperties version="4" weight="0.2176" build="0.4699" key="000594041214000E318040336E417501E7F0868F09990035BB7979087CF51E0A00873603080BB69F1F6410605020004000000000000017670000000043144003"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_52_1']/face">
		<face>
			<BodyProperties version="4" weight="0.2269" build="0.7361" key="0008700E4000138897700694F0015167328081FFB2570E00756880008F96C80300AB36030A39320100000000000000000000000000000000000000003D3C1002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_52_2']/face">
		<face>
			<BodyProperties version="4" weight="0.3981" build="0.4861" key="0020FC0980001282248414071B75011484C884CF8169800109FA00017FF95EA2009766030971A00800000000000000000000000000000000000000003B2C4002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_52_3']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0005580B4000300DA55B8899724689763869956B7B86578578658A96886733790003000203B8789800001105000030A30000001F000000000000000000000000"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_52_4']/face">
		<face>
			<BodyProperties version="4" weight="0.0185" build="0" key="0000C00FD0FC000480801020FC600020A1B68570086D38016649C50F7FFE9BA1009006030906B4051F64106050200040000000000000176700000000452C4003"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_53']/face">
		<face>
			<BodyProperties version="4" weight="0" build="0.7037" key="000A140D801439C2A880010FB2807808943182694044AB107776900C8D96EA0900AB06030A9D9F080000000000000000000000000000000000000000194C9002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_53_1']/face">
		<face>
			<BodyProperties version="4" weight="0.1088" build="0.5139" key="000DA80D80001008E0802AF380E6E048606483B0D895690567565D098EA03245009826030929800700000000000000000000000000000000000000003B345002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_53_2']/face">
		<face>
			<BodyProperties version="4" weight="0.9769" build="0.9259" key="0015F00D91140015D48000177F0EFB05B801831B3EEA1909798C400F75F5ECF3009EF603090271761F6410605020004000000000000017670000000043241003"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_53_3']/face">
		<face>
			<BodyProperties version="4" weight="0.3889" build="0.6134" key="0005C00E52003015168000006BA200588727888B0CA31A0072796A0F56EAAF1200596603050944941F6410605020004000000000000017670000000056AC1003"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_71']/face">
		<face>
			<BodyProperties version="4" weight="0" build="0.7037" key="000DD00C4014168270800108B005450CDBA588004F440B0579A6906C8DE11697008B06030894B4050000000000000000000000000000000000000000190C4002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_71_1']/face">
		<face>
			<BodyProperties version="4" weight="0" build="0.3981" key="000A080E51003004318000C16601FE0486278B8915A61A04BB79880A7FFCA8E3007606030710660E1F6410605020004000000000000017670000000042FC2003"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_WE8_c']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0005580B4000300DA55B8899724689763869956B7B86578578658A96886733790003000203B8789800001105000030A30000001F000000000000000000000000"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_WE8_u']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="000A80034014168D54C4BC37777515B7D845A4BA65A0A9BB59628895AB9747530077760307872A4A000000000000000000000000000000000000000052DC30C2"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_73']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0011B40B403C10088733440E80468F1FFF4F9274C862B448941B5637963E5D3E000008840B7247A6000000000000000000000000000000000000000000003101"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_73_1']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0022CC0580CC100B5867B786DC6B9968B4367A58547AB48D79EAB69452B75745016776130655B895000000000000000000000000000000000000000030C40083"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_59']/face">
		<face>
			<BodyProperties version="4" weight="0.0486" build="0.3611" key="00025C04001403030B82005700C07504944685853CB98070BB79530E7ECBFEA200B516030B3BB5731F6410605020004000000000000017670000000043647003"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_59_1']/face">
		<face>
			<BodyProperties version="4" weight="0.4444" build="0.6574" key="000DCC0B90B43003718910085515EF0319E18409739A1302802D96097FF9AD66007A76030772D4A21F6410605020004000000000000017670000000044DC2003"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_59_2']/face">
		<face>
			<BodyProperties version="4" weight="0.1" build="0.98" key="00027001400001C3547317A5A360004400318908384666579918711981B003170099060309683B0632430001330223550000000000001977000000004D347002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_59_3']/face">
		<face>
			<BodyProperties version="4" weight="0.1" build="0.98" key="00027001400001C3547317A5A360004400318908384666579918711981B003170099060309683B0632430001330223550000000000001977000000004D347002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_60']/face">
		<face>
			<BodyProperties version="4" weight="0.1" build="0.98" key="00027001400001C3547317A5A360004400318908384666579918711981B003170099060309683B0632430001330223550000000000001977000000004D347002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_60_1']/face">
		<face>
			<BodyProperties version="4" weight="0.1" build="0.98" key="00027001400001C3547317A5A360004400318908384666579918711981B003170099060309683B0632430001330223550000000000001977000000004D347002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_60_2']/face">
		<face>
			<BodyProperties version="4" weight="0.1" build="0.98" key="00027001400001C3547317A5A360004400318908384666579918711981B003170099060309683B0632430001330223550000000000001977000000004D347002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_62']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.8" key="000BB00E400003884897D665A3CD435C87D42767B77897C8637A678695873A870005D4B2077B64B500001105000030A30000001F000000000000000000281000"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_62_1']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0011AC071100300FA554C37EA53BA67A8B6E7AA94D9BAE666B76EB5953835CAB01977613097C478900000000000000000000000000000000000000004D243043"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_30']/face">
		<face>
			<BodyProperties version="4" weight="0.1" build="0.95" key="0016C80E0000200DBC4597A5256457A995879939BBECA7A766C7893AA68945570005435309B1876400001105000030A30000001F000000000000000000A02000"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_30_1']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0011A40CC0B4300AA943DB689A336188B8984B56679A5AB8497A4CB9D8A6232501877613088C45970000000000000000000000000000000000000000350C1003"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_30_2']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.3" key="00117C0E50CC200D73C3265E79C3998BBE65798569D73495C6D87BD3AA57B6D501C776130CD465380000000000000000000000000000000000000000358C20C3"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_30_3']/face">
		<face>
			<BodyProperties version="4" weight="0.4" build="0.3" key="00117C0E50CC200D73C3265E79C3998BBE65798569D73495C6D87BD3AA57B6D501C776130CD465380000000000000000000000000000000000000000358C20C3"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_48']/face">
		<face>
			<BodyProperties version="4" weight="0.0116" build="1" key="0039F00FC000300010000010010100000001000000000100001011101000000000FF06030F000100000000000000000000000000000000000000000043F44142"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_48_1']/face">
		<face>
			<BodyProperties version="4" weight="0.0116" build="1" key="0039F00FC000300010000010010100000001000000000100001011101000000000FF06030F000100000000000000000000000000000000000000000043F44142"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_48_2']/face">
		<face>
			<BodyProperties version="4" weight="0.0116" build="1" key="0039F00FC000300010000010010100000001000000000100001011101000000000FF06030F000100000000000000000000000000000000000000000043F44142"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_48_3']/face">
		<face>
			<BodyProperties version="4" weight="0.0116" build="1" key="0039F00FC000300010000010010100000001000000000100001011101000000000FF06030F000100000000000000000000000000000000000000000043F44142"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_54']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.95" key="0005A00140BC100DA85B899C8A71E497A32461B62479196C55058E7245674416000506530C356542000000000000000000000000000000000000000000004180"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_54_1']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0000780450B40005274C088C838671B78588B5549A7B79D979386C9D75A83863016776130698556700000000000000000000000000000000000000003ACC4083"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_55']/face">
		<face>
			<BodyProperties version="4" weight="0.3" build="0.85" key="0011BC0CCE000102A5508899487841954B4F9A57D5C7D811C889B5068C4A7666012776130268769500000000000000000000000000000000000000003C4480C2"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_55_1']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="000B5C070058300454262B4CA6A899758988B348E4A3854B7B2BD7728796946401A776130A3359560000000000000000000000000000000000000000375410C3"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_63']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.9" key="00065C08807C000D88566B74DA8577A325C64A9B6AA35AD26A5B2DB39752565500065BA20657885E00001105000030A30000001F000000000000000000F03000"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_63_1']/face">
		<face>
			<BodyProperties version="4" weight="0.7" build="0.8" key="001C700CC0CC30106C969C364498D4369D8A57A9A772768983E88354BB2B1BCE0167761306D7CD8B000000000000000000000000000000000000000050DC4083"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_63_2']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="001C580892B400107792D84678A387599DAA67447A8C77C9D7D366699576C7CB0167761306D83BB700000000000000000000000000000000000000005CDC0003"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_63_3']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="00168C0CD3E4300B746DA4A836974259CA53A496D7AA6425822AA1429A5CACCA01677613063C365E00000000000000000000000000000000000000003CC42043"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_56']/face">
		<face>
			<BodyProperties version="4" weight="0.4" build="0.8" key="0005C8058000100E63727AC8926A687A1A85878578987C9B437E6347545ABCB7000C0892057768C900001105000030A30000001F000000000000000000004000"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_56_1']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0005240892FC1005B69C29549444BE8759679A7778478C8D78BA785C77A5C7880177761307D8873D000000000000000000000000000000000000000054F40083"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_56_2']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0011440CD000000F85883C187A9326A926589A94B9889854776498DB88864ED701877613084688A80000000000000000000000000000000000000000411410C3"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_68']/face">
		<face>
			<BodyProperties version="4" weight="0.7" build="0.9" key="000BB00598FC00059A75657577A39A87B3D89A8E9774BD7677A867A97D3A634D00060C520767C68700001105000030A30000001F000000000000000000287000"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_68_1']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="00115C0890B42010C486966CA6A39396395EBC15947764737B762A7963D6584201A776130A5A596B00000000000000000000000000000000000000005B544003"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_69']/face">
		<face>
			<BodyProperties version="4" weight="0.3" build="0.4" key="0016C40E403C1408E436C9A4849415C6909E955997C1BD57CB837C9596548C15000E4B820E71DA76000000000000000000000000000000000000000000003141"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_69_1']/face">
		<face>
			<BodyProperties version="4" weight="0.3" build="0.4" key="000BBC0FC0143010E1789C45A6A4429352B8238BB9B683DBAB95A8778B89BAC501977613097656360000000000000000000000000000000000000000372410C3"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_69_2']/face">
		<face>
			<BodyProperties version="4" weight="0.3" build="0.4" key="0000900A10143006685D493BBC780A4686D18674A5379AA596B62D5A727BC485017776130799B93A000000000000000000000000000000000000000068FC4083"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_72']/face">
		<face>
			<BodyProperties version="4" weight="0.1" build="0.99" key="0011B40B402410088733440E80468F1FFF4F9274C862B448941B5637963E5D3E000E08830B7247A6000000000000000000000000000000000000000000003101"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_72_1']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="00055C0E40CC3010AB37736CB777CA594C4C561A72B5767D2385A776BA71778B015776130579877A00000000000000000000000000000000000000003CAC40C3"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_SE8_c']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.7" key="0011B40B402410088733440E80468F1FFF4F9274C862B448941B5637963E5D3E000E08830B7247A6000000000000000000000000000000000000000000003101"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_74']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0011B40B402410088733440E80468F1FFF4F9274C862B448941B5637963E5D3E000E08840B7247A6000000000000000000000000000000000000000000003101"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_1_74_1']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="00115C0FD1A0000E3A7C7A85CC64D75ABE9B4974647554498CAA97499766E5860147761304B85D0800000000000000000000000000000000000000005C943003"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_NE8_l']/face">
		<face>
			<BodyProperties version="4" weight="0.6" build="0.75" key="00009C0FC0CC3286677834B57545557BA665A4BB733C359B45DA64699C84577801377613034C439C00000000000000000000000000000000000000006A644002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_NE8_s']/face">
		<face>
			<BodyProperties version="4" weight="0.4" build="0.5" key="0000200FD0CC200E978EB857E594B2FE6929595C8998514499BBA261B3BB76B201377613037A459900000000000000000000000000000000000000006A6400C3"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_NE8_c1']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0000D40FC0CC12C897B8079E76258849839B767CB9CA744999BBA2614817BB7801377613037A459900000000000000000000000000000000000000006A642102"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_NE8_c2']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0000D40FD0CC100497B8079E76258849839B767CB9CA744999BBA2614817BB7801377613037A459900000000000000000000000000000000000000006A6420C3"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_NE9_l']/face">
		<face>
			<BodyProperties version="4" weight="0.6" build="0.75" key="000AAC080000165AB989A184241CB80B537D86629511323BDCB177278A49B6A7007776030748B49500000000000000000000000000000000000000003EFC5002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_NE9_s']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="000AAC0800001001B957635424174307567986659534566BDCB5774788493637008776030848B59500000000000000000000000000000000000000003F0C5002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_NE9_d']/face">
		<face>
			<BodyProperties version="4" weight="0.6" build="0.5" key="00009C0FC0CC3286677834B57545557BA665A4BB733C359B45DA64699C84577801377613034C439C00000000000000000000000000000000000000006A644002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_WE9_l']/face">
		<face>
			<BodyProperties version="4" weight="0.3588" build="0.4884" key="000A580E401403C201810D5A6209FC0C85E4826975040B007556440C6BC8A90200B756030B13BA9100000000000000000000000000000000000000003B643002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_WE9_l_1']/face">
		<face>
			<BodyProperties version="4" weight="0.75" build="0.85" key="0011840B40CC12CBB7B2B8C953C76674299A5DC93264A4A938C659683379847901777613077D5972000000000000000000000000000000000000000040E42102"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_WE9_l_2']/face">
		<face>
			<BodyProperties version="4" weight="0.1088" build="0.5139" key="0000AC04C00018440E802E038706E098A89483B0D1956001675810068FA00A450098260309798B0600000000000000000000000000000000000000003B343002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_WE9_u']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0000AC001170000E68844A7B5955A69256989B97A185847C36C937ABA3698C8C01977613097BD71A000000000000000000000000000000000000000061341003"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_WE9_u2']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0000AC001170000E68844A7B5955A69256989B97A185847C36C937ABA3698C8C01977613097BD71A000000000000000000000000000000000000000061341003"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_SE9_l']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.6" key="000BDC0FCFCC038F3D38B65122600AD02FEE2A646411F7F894359F648858075D064976670485629500000000000000000000000044FC1002000000007E7C4084"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_SE9_s']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0000080FD0FC100D4776686376266B93426B9C989782C82458739F1CB876963401577613051867C8000000000000000000000000000000000000000022A420C3"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_SE9_c1']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="000B700E402C325968215CB4D9779D947BABE1EA12D7B9649975B5856DB7C355016776130655A58500000000000000000000000000000000000000002AD42042"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_SE9_c2']/face">
		<face>
			<BodyProperties version="4" weight="0.4" build="0.3" key="0000080FCEFC290B4756896A6356B7D0C06B9C989782C82458739F1C6A48C62501877613081867C80000000000000000000000000000000000000000370C2102"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_1']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.9" key="0000500140BC09429E895C86B34CA38993A369B8B287576979538AC46B678B940177781307C60384000000000000000000000000000000000000000000F05180"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_2']/face">
		<face>
			<BodyProperties version="4" weight="0.4526" build="0.5473" key="000A08044070300E4976245366B465B59A4B8308619547498764AA1275284A240898768909675A3A00000000000000000000000000000000000000006F3C2084"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_3']/face">
		<face>
			<BodyProperties version="4" weight="0.3" build="0.9" key="00002C04405838836569E86B42547942A953B158E6A5883394A97756BC9B75A90197701309CE53B8000000000000000000000000000000000000000025283042"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_4']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.8" key="00001C04407018C18873324163A357B4966D873971979A028B644405773A5B2401B776130B8B799C0000000000000000000000000000000000000000797C1081"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_4_1']/face">
		<face>
			<BodyProperties version="4" weight="0.4" build="0.6" key="000B100151141010627A8278C8B6825989B65558B77AAC5795A778895979A76301C776130C385387000000000000000000000000000000000000000043A400C4"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_5']/face">
		<face>
			<BodyProperties version="4" weight="0.7289" build="0.7611" key="0000300140001384967132012584CCB36EC472158520143875848D198317281906BBB0680B68CB80000000000000000000000000000000000000000073788003"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_6']/face">
		<face>
			<BodyProperties version="4" weight="0.6953" build="0.6326" key="00050405007020075B7445315583A1B9864D8716668A93288847E81474585B540589A6570833596C0000000000000000000000000000000000000000470420C1"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_7']/face">
		<face>
			<BodyProperties version="4" weight="0.5176" build="0.6044" key="0000500440B407823E895C86B34CA38993A369B8B280570970538AC46B6781990079880307C60384000000000000000000000000000000000000000000F05000"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_7_1']/face">
		<face>
			<BodyProperties version="4" weight="0.5089" build="0.4047" key="0000500440B4008E9E895C86B34CA389699869B8B28B576979538AC46CB4C7740076880307C60384000000000000000000000000000000000000000000F05000"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_8']/face">
		<face>
			<BodyProperties version="4" weight="0.3353" build="0.6434" key="000074044B5834D49660886B42587948A153C21585A1143894A97756BC9575A900AA50030ACE53B8000000000000000000000000000000000000000059603042"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_9']/face">
		<face>
			<BodyProperties version="4" weight="0.7176" build="0.8336" key="0000180000000344BC7048192589DD69784A747472708A0097A86A068116A60B00EDB0030E7AC393000000000000000000000000000000000000000053E02002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_10']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.4" key="0005AC0B4070015217A823EA6364A9577C7BCBE6B481144EA0B835CE9AE9AAC701B009140BA86C75000000000000000000000000000000000000000001683080"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_11']/face">
		<face>
			<BodyProperties version="4" weight="0.4213" build="0.4275" key="0005140012B4300F2C654B99BBA57D7B9785373999A651AA78C8CCEC77955C9602C666240CE70A590000000000000000000000000000000000000000538C3041"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_111']/face">
		<face>
			<BodyProperties version="4" weight="0.3353" build="0.6434" key="0000BC04405830855960886B42587948A1E37B8355E33A7994A97756BC9575A900AA52030A0E53B8000000000000000000000000000000000000000067603042"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_12']/face">
		<face>
			<BodyProperties version="4" weight="0.3" build="0.554" key="00001C04528830021B7213026674749A968B7726758543278B44F71278394B350CD8A6C20D4B1750000000000000000000000000000000000000000077BC10C2"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_121']/face">
		<face>
			<BodyProperties version="4" weight="0.7176" build="0.8336" key="000518000000024217704A092588FD5A701E74747250801097A86A06811A760B00EDB0030E7AC993000000000000000000000000000000000000000053E02002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_13']/face">
		<face>
			<BodyProperties version="4" weight="0.4221" build="0.7302" key="00005802C000354A7788740C5598FA3C58DD3180B090DA078E6679877C84AFA801BB60130BCB1AF0000000000000000000000000000000000000000073789003"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_13_1']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="001108000070200FD33892A397AC34867B393C86ADDA750B66EE13B68C999CD90157761305747845000000000000000000000000000000000000000034A40083"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_13_2']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0005B002D0CC101077CA5CE759465522795799844375862757BAB56484A9788E0157761305DBD96A00000000000000000000000000000000000000005C9C4043"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_13_3']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="00059C02D070300374CAA96A797A77C79CE76A5875B54198B5561928897C487E01D776130DD75BC500000000000000000000000000000000000000001BAC2083"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_13_4']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0005400151143004747C738A7A775752715CC25D4B5787956D76849495A949330177761307C5ADB800000000000000000000000000000000000000002EEC2043"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_14']/face">
		<face>
			<BodyProperties version="4" weight="0.7" build="0.7" key="000B0005807015843C72310F67D8F06E801C853C01B5813CE9923E7C7F70530C009AA602096E8FE00000000000000000000000000000000000000000012C4042"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_14_1']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0000400891A0200FDD78477A6AB5556B977C4D59CBAD8427CD47DE9AB8BC887A01B776130BC9A5B80000000000000000000000000000000000000000517C20C3"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_14_2']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0000400890A03003DDC8CA9988D94DBA7A554D59CBAD8427C8146BB6B8B9587801B776130B553C280000000000000000000000000000000000000000517C2083"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_14_3']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0000400890A0300A4A768CA3AAA5765D7A55444685D6AC2A856B544CB8B9587801B776130BC746C60000000000000000000000000000000000000000517C1003"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_15']/face">
		<face>
			<BodyProperties version="4" weight="0.7552" build="0.7989" key="0000200880002445C67115035783A878646476227680A42482769E468C16291901ECB0130E9A6534000000000000000000000000000000000000000021E09103"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_15_1']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="001C68070FE40009776A7BC4C8767889A85B469799583A24DB916477A389668101C776130C89899300000000000000000000000000000000000000005B8C30C3"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_15_2']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="001C680710E4200777476D84B87649B77CC4469799583A24967922B69496277B01C776130CB42C5D00000000000000000000000000000000000000005B8C30C3"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_15_3']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="000B4C0710E4100D9BB5B85867759DB27C84967BC8BB966AF371F5599496277B00C776030CE4B6C500000000000000000000000000000000000000005B8C3043"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_15_4']/face">
		<face>
			<BodyProperties version="4" weight="0.6" build="0.6" key="00164C02C0882342BAB7B9566DA53AD368A6628171B9A586A7883546559D974501977613094979CE000000000000000000000000000000000000000041341002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_16']/face">
		<face>
			<BodyProperties version="4" weight="0.1836" build="0.2176" key="00000C040070009B6983CFFB7C433FF1971470640061701F776777774F60841F00D862030D840A74000000000000000000000000000000000000000067B08102"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_16_1']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="000B2802D088000AC4886AA8789859A797CAD63996786A57CBB8885895D177B60197761309D8CA7600000000000000000000000000000000000000002B2C1083"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_17']/face">
		<face>
			<BodyProperties version="4" weight="0.338" build="0.9" key="00000402C000328C8A7115113683CCD8A6497254819028629A6C8B58811E5A1800F850030F9CCE93000000000000000000000000000000000000000061F05080"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_17_1']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="000B78071114200E8D1723DA7A67949A66266E8653525564594C4368786D87B30187761308288EC7000000000000000000000000000000000000000031143003"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_18']/face">
		<face>
			<BodyProperties version="4" weight="0.5077" build="0.85" key="00004401400033CD997241105582FE8CE00F7751A4E7A12886CB5915805F235100C880030C578112000000000000000000000000000000000000000033882042"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_18_1']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="000B740A11001001583A7894D487CE78C5AA46B87A7486657B1B789866D6823901277613028B77670000000000000000000000000000000000000000623C3003"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_19']/face">
		<face>
			<BodyProperties version="4" weight="0.4" build="0.99" key="0000140880001343987138114783EC9AE5067865A1A5939078866E0A8C66108A018AC0130868557500000000000000000000000000000000000000006B187040"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_19_1']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0005680000882007AD47C86E48547C993A35BCC45C5A3488EB979A9B364B66A701377613031CB39B00000000000000000000000000000000000000002C5C3003"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_20']/face">
		<face>
			<BodyProperties version="4" weight="0.0177" build="0.8336" key="00000004400006C06E804F030003E018F03F71A5C6A40691AE1AFD6571FA00F901CD07130C997484000000000000000000000000000000000000000001905002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_20_1']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="000B080150CC30064899E76527D86898746C92BAB8DAA66B7B8B3698896A983101B776130BB47BA900000000000000000000000000000000000000005B6C0083"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_S8_u']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0015C8054044068CB7A388848DA2B46798378B58269B28772C23873661A7A63600477603042744BE000000000000000000000000000000000000000030940102"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_21']/face">
		<face>
			<BodyProperties version="4" weight="0" build="0.6725" key="00001C0B4B0004D4C9BC38753878986C9767762692703328AB5977498679886A00CA00030C5A99D9000000000000000000000000000000000000000039982082"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_21_1']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="000B0C02D2B410069419993888AD9C89477AB8686789AB88845B929CA6AC5C670197761309E3999800000000000000000000000000000000000000002D341003"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_22']/face">
		<face>
			<BodyProperties version="4" weight="0.4463" build="0.6332" key="00007400000004500870001008F1EFFF800F78777781070E70E019518088880F05897057080FFEF0000000000000000000000000000000000000000011103002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_22_1']/face">
		<face>
			<BodyProperties version="4" weight="0.6097" build="0.6476" key="00004803800002C4867232085784BBA666987367C530B7547972892A88150405049A904609888D01000000000000000000000000000000000000000073381041"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_23']/face">
		<face>
			<BodyProperties version="4" weight="0.692" build="0.8011" key="00001000000003805971111366849B6739397673A3B03D03BCD6078CAA996146005CA00305E77987000000000000000000000000000000000000000012B050C0"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_23_1']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0005BC001114200DEA72114080326643E10B706785AD15119FF6AFA17086F0510137761303105059000000000000000000000000000000000000000020641044"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_24']/face">
		<face>
			<BodyProperties version="4" weight="0" build="0" key="00005404400036125A8E4CB3489CDB9D6F67758266101B4FF0F70D2F8A84995E01A000140ADAA974000000000000000000000000000000000000000025585000"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_2_24_1']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="00054C0140B400039659749D884A5B5E6754C9967C75966FAF9645C8B6BA9BC5015776130587C7630000000000000000000000000000000000000000629C0083"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_S9_l']/face">
		<face>
			<BodyProperties version="4" weight="0.696" build="0.9" key="00054C0140B409C39659749D884A5B5E98B6C9967C75966FAF9645C85A918D950068A6030687C763000000000000000000000000000000000000000062BC2083"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_S9_m']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="00054C0140B400039659749D884A5B5E6754C9967C75966FAF9645C8B6BA9BC5015776130587C7630000000000000000000000000000000000000000629C0083"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_S9_c']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="00054C0140B400039659749D884A5B5E6754C9967C75966FAF9645C8B6BA9BC5015776130587C7630000000000000000000000000000000000000000629C0083"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_S9_u']/face">
		<face>
			<BodyProperties version="4" weight="0.696" build="0.5478" key="00054C0280B432419636BC4C477176F96754C9967C75966FAF9645C8B6BA9BC500A8A6030A87C7630000000000000000000000000000000000000000633C2083"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_1']/face">
		<face>
			<BodyProperties version="4" weight="0.6" build="0.85" key="0028280FD7FC23C299C2761C4AC1A5954778548899A66659788878746A9D9629000F16530FA7699C000000000000000000000000000000000000000000F47140"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_2']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.75" key="0022500FD13C0001A7E3C8A64757B4876BA6785E867C96D8249D763F3BB7D86400000C5401A663680000000000000CE600000000000000000000000000286000"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_3']/face">
		<face>
			<BodyProperties version="4" weight="0.7762" build="0.7083" key="0028F40E5A000189869C5A8B7780749AAB77564639947345843A8657994575E505BBC0560B9687970000000000000CE600000000000000000000000001680000"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_3_1']/face">
		<face>
			<BodyProperties version="4" weight="0" build="0" key="001C900FC0FC180886899931423946DB88E588765AA3947958825CAD467C7D4701E006890E57A12A000000000000000000000000000000000000000000843044"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_4']/face">
		<face>
			<BodyProperties version="4" key="002DD40E40000001869C5A8B7786749AAB77564639947345843A8657994575E5000E00040E9687970000000000000CE600000000000000000000000001E00000"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_5']/face">
		<face>
			<BodyProperties version="4" weight="0.6" build="0.85" key="001CE008800023959B83263A29717DE32CD881249354432699A85C598A0A58590588805708986484000000000000000000000000000000000000000035085041"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_51']/face">
		<face>
			<BodyProperties version="4" weight="0.526" build="0.7128" key="0022B00880003012988235094C712BA509598337A14534159A897A068A1E4933034B80350473C822000000000000000000000000000000000000000038807044"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_6']/face">
		<face>
			<BodyProperties version="4" weight="0.3981" build="0.2729" key="0022D80B400020128982510588A2A7348A60717AC375A0229E99C736896F575501A466130A1CB332000000000000000000000000000000000000000007542043"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_7']/face">
		<face>
			<BodyProperties version="4" weight="0.5262" build="0.6404" key="001CC80A00000341AB86473C7577ACD8BB5C83453698737279B68E18780D2B26029A86240968931500000000000000000000000000000000000000001D3C3002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_8']/face">
		<face>
			<BodyProperties version="4" weight="0.5959" build="0.39" key="002DD008800017569A8325164C717EB41DB68247C1766706898A77398C1C655704469045046695710000000000000000000000000000000000000000308850C5"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_9']/face">
		<face>
			<BodyProperties version="4" weight="0.7171" build="0.6445" key="001CF40880002959988044094881DCF25D518415A18656179A8899478D2C5A5304AAB0460A3CC435000000000000000000000000000000000000000037402000"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_10']/face">
		<face>
			<BodyProperties version="4" key="002DDC0E4000040497D3427635D69CA7A837954A579662459958A8898646766A0007600307B767790000000000000CE600000000000000000000000000500000"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_11']/face">
		<face>
			<BodyProperties version="4" weight="0.7" build="0.5337" key="00268C0C110030026982442CABD127719C46724BA187A130BBA3EA457A6C57470288B6240802B35C000000000000000000000000000000000000000059044001"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_12']/face">
		<face>
			<BodyProperties version="4" weight="0.7751" build="0.2584" key="0022D80A800030055C816132B9B2D7429B34737A82668521BAD6A558884D5C690394C635093568AC00000000000000000000000000000000000000002B2C2044"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_13']/face">
		<face>
			<BodyProperties version="4" weight="0.5519" build="0.7167" key="0022F80880002794C782350B3B71AFB7098A8017A2755525A9B9AD468B0C5D68055B80570564A704000000000000000000000000000000000000000060A87042"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_13_1']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0022880E504430048D6DA84AA9B668434696F8AC7F826778683B65553B844B7A01A776130A85794900000000000000000000000000000000000000005D542044"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_13_2']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0028D40B51FC000279615D88977453634B8B895C74B382164C93D947647785A7016776130625634100000000000000000000000000000000000000006ACC40C3"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_14']/face">
		<face>
			<BodyProperties version="4" weight="0.7752" build="0.5081" key="001C980880001612BC80243829815A955AA18339B27543258DB878188B178D450368C0350659885500000000000000000000000000000000000000006AD07081"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_14_1']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="002D700CD0881003A5C589888A5675AAC7B2869B794AB978A79C47747B19B58801577613057CC3E6000000000000000000000000000000000000000042B41003"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_15']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.6" key="003F000FD6FC29DA0CFC208F1001FBEF2D01FA0000F01110500B1EF068E672B006B970670B0D87810000000000000000000000000000000000000000217800C2"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_15_1']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="00288C0CD2880004A395E7AA463A6E1C4BAA43633B1986E5A89857745CAB736C0167761306667684000000000000000000000000000000000000000066C410C3"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_15_2']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0022500CD2E4000457A8EAA7469C9552DEABB756A9349BC76A7768A8D898CA880187761308BAA6890000000000000000000000000000000000000000731C0083"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_16']/face">
		<face>
			<BodyProperties version="4" weight="0.65" build="0.8" key="001CD808800003D2998335193B81CD944A998317B1718116AA98A7278C098A440399903509299520000000000000000000000000000000000000000057281041"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_16_1']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="001CA80CD1883007A555B462CE8A55A6578B58BB799A8ACCAA7D8CC7765CB31201277613025AAA3B0000000000000000000000000000000000000000583C4043"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_17']/face">
		<face>
			<BodyProperties version="4" weight="0.68" build="0.85" key="0022EC08800018D57881460A2B7129E25BAA85149355561688997A298E1A795304689046064BC72600000000000000000000000000000000000000001CC070C5"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_17_1']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="003F880FC0B4000D22AA8868B68367A9346A54A66984B75BB1AA59782B2B6773015776130586542600000000000000000000000000000000000000003E9C2043"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_17_2']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0028A40FC044200D7BA421B1984A6141392DDD94B48251365E6857BBD67B978B01C776130C9683850000000000000000000000000000000000000000338C3003"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_18']/face">
		<face>
			<BodyProperties version="4" weight="0.7" build="0.8" key="001CA40880001141A982443A39718ED53DA28416A353712599A86A478E184657066AA068065A8854000000000000000000000000000000000000000008C040C4"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_18_1']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="00333C0E502C10107A75B5227776955DAA32B8696C098CE8698934134582A37801A776130AC954A5000000000000000000000000000000000000000031440003"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_18_2']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="00281C0B40FC10014495955AC76645777ACE633B7A944788995887A99C8DDBA50157761305557C45000000000000000000000000000000000000000030B410C3"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_18_3']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0033640B52FC3011677343968A67954791464A7576DB8A4BD358B8497D53B6AB0187761308C59A69000000000000000000000000000000000000000031042003"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_18_4']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0028580E40CC130D755797AA334A25ED82C384356C3687C239B7CAA5876879580137761303A69CA6000000000000000000000000000000000000000030541002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_19']/face">
		<face>
			<BodyProperties version="4" weight="0.72" build="0.82" key="0016C80880001597BA83341737814FC43B578027A2557115ABB98E678A0A6867049870460979E5240000000000000000000000000000000000000000253850C0"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_19_1']/face">
		<face>
			<BodyProperties version="4" weight="0.4784" build="0.5191" key="0016C80880001597BA83341737814FC43B578027A2557115ABB98E678A0A6867049870460979E5240000000000000000000000000000000000000000253850C0"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_19_2']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0028780FD1B40004C774C93FC6B607A25B78358D77AA3A167785D8575938737C0167761306A6AB4600000000000000000000000000000000000000004ABC10C4"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_19_3']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="001C240FC00010037AC4EAE9849538381157953AB4819A17898B5984272D3C660187761308B6393700000000000000000000000000000000000000002F0440C4"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_20']/face">
		<face>
			<BodyProperties version="4" weight="0.3371" build="0.6152" key="0028B00880001A14A78244373A714DB139C2813792826026AA79AC688B185E4503295035024675B0000000000000000000000000000000000000000068486084"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_20_1']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0028A40CD21410068A9277BB6B3788C8B8A9C36AB6AAC46A436AB719877B887401B776130BA539B70000000000000000000000000000000000000000197C3003"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_20_2']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0028540FD114000ABB6D8984A3778C5557A97866E85A9D87A87587757CA534BB0187761308A5866B00000000000000000000000000000000000000006D042003"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_21']/face">
		<face>
			<BodyProperties version="4" weight="0.7348" build="0.4839" key="001CD40A00001582C9823315378118F46C84843482727016978A6A088D2B4E480487B04608774A7100000000000000000000000000000000000000003F182083"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_21_1']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0039580CD1A0000598A7094768948C7BBC777A3B9939A865A968C3CE8A95D3CA0137761303865A560000000000000000000000000000000000000000587440C3"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_22']/face">
		<face>
			<BodyProperties version="4" weight="0.1088" build="0.5139" key="0000AC04C00016450E8A2E038706E098A87480B0D19560016758100A8FE00A450098260309798B0600000000000000000000000000000000000000003B343002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_22_1']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0022640A215820064A37ACFE612B9A4AACB69A98683BB57DB7965D8AC65346250187761308627CDB0000000000000000000000000000000000000000270C3043"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_22_2']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0028540E52143001722BD61A88448B8B8985B5B635BF97A4BABEA16D8882527C0167761306957C88000000000000000000000000000000000000000048D400C3"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_22_3']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0022700CC014101263B8C87941868498779B3D75E97B5678C27788B8C8AE759501B776130B95A6650000000000000000000000000000000000000000396C00C3"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_22_4']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0028240E5088200E38B5E59AB258BA6668952E7A5AD5B8797BC7A8E8796233690177761307C6896400000000000000000000000000000000000000003EEC10C3"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_23']/face">
		<face>
			<BodyProperties version="4" weight="0.7114" build="0.7083" key="001670045300340A9772C4C436AA7887A435497444328E72DB8C48519D57749A05BBB1570BBA29780000387A00000AEB00000033000000000000000001705000"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_3_23_1']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0022700CD114300258D8AD87875435B9EA4385AA274487967CCCCB676A89755A0137761303B03A99000000000000000000000000000000000000000038643004"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_A9_l']/face">
		<face>
			<BodyProperties version="4" weight="0.7" build="0.8" key="0022700CD114300258D8AD87875435B9EA4385AA274487967CCCCB676A89755A0137761303B03A99000000000000000000000000000000000000000038643004"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_A9_s']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0028240E5088200E38B5E59AB258BA6668952E7A5AD5B8797BC7A8E8796233690177761307C6896400000000000000000000000000000000000000003EEC10C3"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_A9_c']/face">
		<face>
			<BodyProperties version="4" weight="0.6389" build="0.5926" key="0023840E401410059AB8C8794186849896635A43A91AA68AC27788B88A37A6B600C9A6030C95A6650000000000000000000000000000000000000000478C00C3"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_A9_u']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0022700CC014101263B8C87941868498779B3D75E97B5678C27788B8C8AE759501B776130B95A6650000000000000000000000000000000000000000396C00C3"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_1']/face">
		<face>
			<BodyProperties version="4" key="0005280140001242947E068A709500460C7250703EB70F135C85021887733A070089B6030822BA9000000000000000000000000000000000000000003F1C7002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_2']/face">
		<face>
			<BodyProperties version="4" key="000044000000000577707780B7C07777C2BB71F7EFF77AA7956746991C7775C70000000505C7966A000000000000000000000000000000000000000000000100"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_3_1']/face">
		<face>
			<BodyProperties version="4" weight="0.1" build="0.5" key="0002B404000011826B700D6F80961035305E80B2BFEB0E00775161048C771F0400AB36030A62AA0300000000000000000000000000000000000000003D447002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_3_2']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0011A407144400062897A829649497DB8C988BB9BCD4B379A5868D485A689A9A0167761306297985000000000000000000000000000000000000000038D42003"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_3_3']/face">
		<face>
			<BodyProperties version="4" weight="0.1852" build="0.7199" key="0000240011B40004007033909950BA113BEE80A17FBF0E017751B80B8C7D1F04007B36030763A11700000000000000000000000000000000000000003CF420C3"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_4']/face">
		<face>
			<BodyProperties version="4" weight="0.4706" build="0.6001" key="0005180A00CC00049172105733863178A76987A967716698AB94AE398C6A5582036976350666486B00000000000000000000000000000000000000002EDC2042"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_5']/face">
		<face>
			<BodyProperties version="4" weight="0.5517" build="0.5949" key="000B60014000234665734C57613665A5354588837351AC1455E8373672131432037980350768BB63000000000000000000000000000000000000000026E01082"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_6']/face">
		<face>
			<BodyProperties version="4" weight="0.01" build="0.61" key="0002B00000001345DD80090A71069A53BF55834D3FC9DF00500D53098CC0F04800A906030AAAA15A00000000000000000000000000000000000000003F4C3002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_6_1']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="000B040B4070300A59453CA886740D767888B63281AA8878BC957C5659C2945701777613078AB58600000000000000000000000000000000000000006AF420C3"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_6_2']/face">
		<face>
			<BodyProperties version="4" weight="0" build="0.5" key="000D0C04000016028F70E00E0D505035708D86909FC8E2054996B74686D4A835009706030944F85500000000000000000000000000000000000000003F3C4002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_7']/face">
		<face>
			<BodyProperties version="4" weight="0.1" build="0.5" key="00052C028000000200752A737881DC04A918866245A806069451821B8F4319050097260309119F0800000000000000000000000000000000000000003D2C2002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_8']/face">
		<face>
			<BodyProperties version="4" key="00006C02C000000977A84702762098CB27AD8D4D54C9895378C97B1E27408574000440040488276B000000000000000000000000000000000000000000780000"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_9']/face">
		<face>
			<BodyProperties version="4" weight="0.8124" build="0.6943" key="00041C044000200535813C56515565A7186478496341BE2334D8792573325632057AC057076ABD61000000000000000000000000000000000000000036E830C0"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_10']/face">
		<face>
			<BodyProperties version="4" key="000080000000000B67874369563B575737063939BC84896C773744465293778C000000030F97CDD9000000000000000000000000000000000000000000000000"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_11']/face">
		<face>
			<BodyProperties version="4" key="00127C04403C3004B7D8465988D98C845A24288FAD8AB786279B9CAA78AA48B500059A43059AA965000000000000000000000000000000000000000000282000"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_12']/face">
		<face>
			<BodyProperties version="4" weight="0.4524" build="0.5804" key="0005340140CC00017071527435736265B7BE7A79B0614997BAB7F9397C2A987203597635053CBA5A00000000000000000000000000000000000000005CB41044"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_121']/face">
		<face>
			<BodyProperties version="4" weight="0" build="0" key="0005440440FC308E332B7C6D8A45CA881E6E4D6784343CC64C9A3A598476688901F006890F8C555B0000000000000000000000000000000000000000012C3144"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_13']/face">
		<face>
			<BodyProperties version="4" weight="0.6404" build="0.6682" key="0005B008800039CD48802D04476A22F69E19714BC2BB1B05FF6F00777B853103027AA0240759705000000000000000000000000000000000000000007CE82001"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_14']/face">
		<face>
			<BodyProperties version="4" weight="0.7525" build="0.6973" key="000510030000038552722B57125576F5232384586361A9027B355D2470527234029AB02409682751000000000000000000000000000000000000000073301040"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_141']/face">
		<face>
			<BodyProperties version="4" weight="0.6561" build="0.7254" key="000A1C044000200213731F53422567A40340782A62818C5644865A2982340431017BA01307534CA3000000000000000000000000000000000000000024F070C4"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_15']/face">
		<face>
			<BodyProperties version="4" weight="0.6214" build="0.5381" key="00004C044000030323834B56123476B60987754B71517D2544947746753675230178901307648B8400000000000000000000000000000000000000006CE84102"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_16']/face">
		<face>
			<BodyProperties version="4" weight="1" build="0.6157" key="000AD40000001343EE7E000F4AAB0022AB5E70F090EDE6067446100F8B5B0F080099F60309F6E0FE00000000000000000000000000000000000000003D2C7002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_16_1']/face">
		<face>
			<BodyProperties version="4" weight="0.4524" build="0.5804" key="0005340140CC00017071527435736265B7BE7A79B0614997BAB7F9397C2A987203597635053CBA5A00000000000000000000000000000000000000005CB41044"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_16_2']/face">
		<face>
			<BodyProperties version="4" weight="0.5602" build="0.5718" key="00057805400013C10F72496C008D58E89E91831000BB8633B74194048FD29F3600A986030A8C97F900000000000000000000000000000000000000002B5C7002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_17']/face">
		<face>
			<BodyProperties version="4" weight="0.6956" build="0.6722" key="0000280440CC000E72603755334384AA85A98979B1628696AAB6E9298A3B9358019AA6130944AAB5000000000000000000000000000000000000000025342040"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_18']/face">
		<face>
			<BodyProperties version="4" key="00055C0880BC0245678789A8C8B6C2728588854B98886843B5DCD6C879C689370005676409844933000000000000000000000000000000000000000000F08000"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_181']/face">
		<face>
			<BodyProperties version="4" key="00055C0880BC0245678789A8C8B6C2728588854B98886843B5DCD6C879C689370005676409844933000000000000000000000000000000000000000000F08000"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_19']/face">
		<face>
			<BodyProperties version="4" weight="0.6483" build="0.7136" key="0000440440CC000B92611464264492D99ADB8BA86151568799A9EE218B4A645603BBA6350B363A260000000000000000000000000000000000000000616C2084"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_20']/face">
		<face>
			<BodyProperties version="4" weight="0.6056" build="0.6666" key="000004044000308233821857532569D41066841B71415A515D56762987126334018A901308692C53000000000000000000000000000000000000000057001042"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_20_1']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="000B680A1070000AB27B84CD4CC38B9CBA75B4AABB9AC152965A9D22CEADBE45019776130998732700000000000000000000000000000000000000001B2C2084"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_21']/face">
		<face>
			<BodyProperties version="4" weight="0.7222" build="0.5995" key="000218000000394336706B475208F01E9F1686E2F9C7C211D1617519811A11190099B6030948B49500000000000000000000000000000000000000003F2C3002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_21_1']/face">
		<face>
			<BodyProperties version="4" weight="0.2778" build="0.3148" key="000A3C02800031832F708B47F200F05E901E86E2FE61320BD16505198B2A8111008546030846B09500000000000000000000000000000000000000003F145002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_21_2']/face">
		<face>
			<BodyProperties version="4" weight="0.3102" build="0.7199" key="000810009114301590700780D492773138FE80A17FBF02027751B10B8C7A1F0400AB56030A6BA31900000000000000000000000000000000000000003D442003"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_21_3']/face">
		<face>
			<BodyProperties version="4" weight="0.7292" build="0.4074" key="00023C028000000320703B47F209A05A5A8E8361FE95BB27D16502178B2A81110096B6030948E0B500000000000000000000000000000000000000003F341002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_21_4']/face">
		<face>
			<BodyProperties version="4" weight="0.7593" build="0.5" key="00052C0200FC000D4080100EE018F005974F87629E913201A009041F8A8706610077B603070CEAF000000000000000000000000000000000000000003EFC1003"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_22']/face">
		<face>
			<BodyProperties version="4" weight="0.7292" build="0.4074" key="00050806C00017822070081EF773F1B4A05380615D95FB11D1650217800A61110096B6030948E0B500000000000000000000000000000000000000003F349002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_22_1']/face">
		<face>
			<BodyProperties version="4" weight="0.2639" build="0.7222" key="000D3C0140FC0001E08010E3E2E7E2117EF887621E988104A009041F8A870A07009B4603090CE6F500000000000000000000000000000000000000003F1C2003"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_23']/face">
		<face>
			<BodyProperties version="4" weight="0" build="0.6134" key="00020800000013810070FB1FF911EFBBE0238BA15DDCF5450019011F890AB111008906030818E00800000000000000000000000000000000000000003F145002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_23_1']/face">
		<face>
			<BodyProperties version="4" weight="1" build="1" key="0002A8001114300AE08F900FBB63021176F8806C1F970104A009F01B8A87009A009FF603090FE7FC00000000000000000000000000000000000000003F1C2003"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_23_2']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0000740E4044000B3969339927553498A84BB8A877D96D6645597984592A439501477613046A3907000000000000000000000000000000000000000050942043"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_23_3']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0000740E4044000B3969339927553498A84BB8A877D96D6645597984592A439501477613046A3907000000000000000000000000000000000000000050942043"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_24']/face">
		<face>
			<BodyProperties version="4" weight="0.38" build="0.1644" key="0000500FC0CC100378A061BC61A180068D4BA177F0C1E601B0FF1B008FE78B00007276030720F81100000000000000000000000000000000000000003AF40102"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_25']/face">
		<face>
			<BodyProperties version="4" weight="0.6867" build="0.7269" key="0000100456B4020DC66F69A052632C57D98458CAA8CA55C96B66BB47ACB9C85407ABA6780A886647000000000000000000000000000000000000000001602000"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_25_1']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0000540E4070100B9D95A3A74EA8C8B848778365688C85437456344D43777968014776130475B2940000000000000000000000000000000000000000268C30C4"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_26']/face">
		<face>
			<BodyProperties version="4" key="000B48058FFC300578E42AAB88C6A5BD924B6988339398973D8EA8676AA7686500056D2507336C86000000000000000000000000000000000000000000C81000"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_27']/face">
		<face>
			<BodyProperties version="4" weight="0.4491" build="0.7222" key="000A14080000168200800A1F0530F00E660F8960B08860020019011F8E0BA101008B76030805E60600000000000000000000000000000000000000003F049002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_27_1']/face">
		<face>
			<BodyProperties version="4" weight="0.4769" build="0.6042" key="000D3006D014200A8E8000120BC13021A78A806C0FC77034A4591B1C8A87119900797603070CEF0C00000000000000000000000000000000000000003EFC2003"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_27_2']/face">
		<face>
			<BodyProperties version="4" weight="0.5741" build="0.4699" key="000D6008000023C227806EF1F00B3007D0FC8B60D164EC006FF97B048B50F00400779603075AC59900000000000000000000000000000000000000003EFC5002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_27_3']/face">
		<face>
			<BodyProperties version="4" weight="0.3565" build="0.6042" key="000A300280143004B08001420BC1D041A76A806C0FC77001A789101B8A871B99007956030706EF0C00000000000000000000000000000000000000003EEC1003"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_27_4']/face">
		<face>
			<BodyProperties version="4" weight="0.5741" build="0.4699" key="000A60080000100510806F61F007FF07B8058C60D0646C0460194B168B50D104008796030854C59E00000000000000000000000000000000000000003EFC2002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_28']/face">
		<face>
			<BodyProperties version="4" weight="0" build="0.5" key="0008D40A400010149E70FF0790609008AB9D709070EF9600744D70098DB2D404007706030707A09500000000000000000000000000000000000000003CF47042"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_28_1']/face">
		<face>
			<BodyProperties version="4" weight="0" build="0.5" key="0008240D800013820E70FF0790680008AB9D709079EF9B01744D7B098942D474007706030707A3F500000000000000000000000000000000000000003CF45042"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_28_2']/face">
		<face>
			<BodyProperties version="4" weight="0.1574" build="0.5602" key="000A200150B4000A6E8000100BC79001A269846C0A811013A579051C89F71BA6007826030703760000000000000000000000000000000000000000003EE40003"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_28_3']/face">
		<face>
			<BodyProperties version="4" weight="0.0093" build="0.5602" key="000D200150B400032E8000170BCA5004A2698A6C008D1011A579051C89F71BA6007806030703760000000000000000000000000000000000000000003EE40003"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_29']/face">
		<face>
			<BodyProperties version="4" weight="0" build="0.5926" key="00056005400012C3308166EF1A6B90598049BB608F98FF004006000F3C072660009906030919FD8C00000000000000000000000000000000000000003F2C4002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_29_1']/face">
		<face>
			<BodyProperties version="4" weight="0.4514" build="0.6806" key="00022C029100300D9280000694B5E006C014C590007DB103A428260C8B9B1B06008A76030805BC6200000000000000000000000000000000000000003F140003"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_29_2']/face">
		<face>
			<BodyProperties version="4" weight="0" build="0.5926" key="0005240140001342308166E1190590568049BB608F98FF004006000F7CA72640009906030919F31A00000000000000000000000000000000000000003F2C4002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_V11_l']/face">
		<face>
			<BodyProperties version="4" weight="0.4306" build="0.4699" key="0005CC0D800013436E8F6EF11905A009717CC2604DB77E03C01904008E50E60400776603075AC59900000000000000000000000000000000000000003EFC1002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_V11_l_1']/face">
		<face>
			<BodyProperties version="4" weight="0.4514" build="0.6806" key="000D300290B41004378E608F0B4200049464C53A05CDD001A00C491C89F71BA6007A7603070023F400000000000000000000000000000000000000003EFC1003"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_V11_l_2']/face">
		<face>
			<BodyProperties version="4" weight="0.3704" build="0.5926" key="00054003400013836E816EF119089109863FC260BD959095C01905008E57B60000896603081AC50C00000000000000000000000000000000000000003F141002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_V11_l_3']/face">
		<face>
			<BodyProperties version="4" weight="0.3704" build="0.5926" key="000AC40C400010026E8116E1190BE10981FFBA60BD97563353567D083E57B60000896603081AD0F900000000000000000000000000000000000000003F141002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_V11_l_4']/face">
		<face>
			<BodyProperties version="4" weight="0.4514" build="0.6806" key="000DB40D901410042780000001440004C014C59A097D4102A74FF90C8B9B1B06007A76030700281400000000000000000000000000000000000000003EFC1003"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_rohan_2_1']/face">
		<face>
			<BodyProperties version="4" weight="0.5139" build="0.6435" key="0005580E40001244259D9156CB621D5BCA8897A6427AC8C999B649B77BA58952008A86030856D79700000000000000000000000000000000000000003F142142"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_rohan_5_1']/face">
		<face>
			<BodyProperties version="4" weight="0.5139" build="0.6435" key="00053C0E40001244B989CC38CDA88487C99ABBC693A3837531D36743B4896B2D009A8603092A795700000000000000000000000000000000000000003F340082"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_rohan_9_1']/face">
		<face>
			<BodyProperties version="4" weight="0.5139" build="0.6435" key="0008F00D800002423BB625668464386847289A9B61E6937AC7573C1D9156C97600BA86030B9F786800000000000000000000000000000000000000003F7C8142"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_rohan_10_1']/face">
		<face>
			<BodyProperties version="4" weight="0.5139" build="0.6435" key="0008AC0A400013834BA6777C4487B836784BA7484671467588584B5C68A96BC200BA86030BBC875700000000000000000000000000000000000000003F742002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_rohan_11_1']/face">
		<face>
			<BodyProperties version="4" weight="0.5139" build="0.6435" key="00087C04C00002413BB625668464386847289A9B61E6937AC7573C1D9156C97600AA86030A9F786800000000000000000000000000000000000000003F4C8142"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_rohan_13_1']/face">
		<face>
			<BodyProperties version="4" weight="0.5139" build="0.6435" key="0008E80E400014834BA6777C4487B8367157A74846714675754EB64756944366009A86030964858C00000000000000000000000000000000000000003F342042"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_rohan_14_1']/face">
		<face>
			<BodyProperties version="4" weight="0.5139" build="0.6435" key="000A7C08C000081B7C79BCB0E5E576D786C3A99A38A2B864C4E62D76D73499D8009A8603095BD36800000000000000000000000000000000000000003F246142"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_rohan_15_1']/face">
		<face>
			<BodyProperties version="4" weight="0.5139" build="0.6435" key="000A4C08C0003001678599B73553916664999358666643565967991567553944009A8603093D6A8300000000000000000000000000000000000000003F243003"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_rohan_16_1']/face">
		<face>
			<BodyProperties version="4" key="0005280140001242947E068A709500460C7250703EB70F135C85021887733A070089B6030822BA9000000000000000000000000000000000000000003F1C7002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_rohan_17_1']/face">
		<face>
			<BodyProperties version="4" weight="0.5139" build="0.6435" key="0008D40A40001344355B9985B42884476B6E6A6A754456D77C8886674396586D008A860308939C7700000000000000000000000000000000000000003F1490C2"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_rohan_21_1']/face">
		<face>
			<BodyProperties version="4" weight="0.5139" build="0.6435" key="0008D4054000320535A79EA5D58A86628A836A6A754456D7A18E73532678BC6B008A860308DA65B800000000000000000000000000000000000000003F149042"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_rohan_22_1']/face">
		<face>
			<BodyProperties version="4" weight="0.5139" build="0.6435" key="0008AC0A400013434BB775D797887753784AA748467146752C7CB7865956A66B00BA86030B99C41600000000000000000000000000000000000000003F742042"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_1']/face">
		<face>
			<BodyProperties version="4" key="00054408800027875CA466A563442BD406899779A794573B86A7C473581AB487000546550E46C99D000000000000000000000000000000000000000000F03180"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_7']/face">
		<face>
			<BodyProperties version="4" key="0005B00CC000244277B884755A5572CC8776B9A6A95B558E342D38679CA7288A00055B82017BB770000000000000000000000000000000000000000000506000"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_1_1']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0005A80A11002011D329A8667A46389A549D8877684B673EB664968A87B0D94901477613048EB3AC0000000000000000000000000000000000000000467C2084"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_3']/face">
		<face>
			<BodyProperties version="4" weight="0.7083" build="0.6528" key="00083C070000238696333724947BC936C948765C8A747896ACA8A26C988A18B608CAB4890CA97C65000000000000000000000000000000000000000001985000"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_4']/face">
		<face>
			<BodyProperties version="4" weight="0.7967" build="0.8773" key="00056802C07000108879465A53347C94973C75A5778495489D65EC3275332456029DC62409783641000000000000000000000000000000000000000073342082"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_3_1']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0000240FD100300D453C497C68747AA8916C58A9C85518B98B75847C3A1893520197761309AB86730000000000000000000000000000000000000000472420C3"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_3_2']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0000240FD100300D453C497C68747AA8916C58A9C85518B98B75847C3A1893520197761309AB86730000000000000000000000000000000000000000472420C3"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_5']/face">
		<face>
			<BodyProperties version="4" weight="0.7017" build="0.6334" key="000268070000244226700A207474A3848C9A751498816EA389779E6A832A155105AAB0560A65B64200000000000000000000000000000000000000005F5870C2"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_5_1']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="001C580FC058100F59AB88926A889A68AA779A998D8ED4767782AC7E2256AA740167761306A4744300000000000000000000000000000000000000003AC42003"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_6']/face">
		<face>
			<BodyProperties version="4" weight="0.3595" build="0.3633" key="000B9805801420068771233587A43345B881874967868927DC887C58756876480485564608148946000000000000000000000000000000000000000047041081"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_8']/face">
		<face>
			<BodyProperties version="4" weight="0.7439" build="0.8603" key="00030C05800000069E712A2C005486A876977CB714119A7636787B8481162713009DB0030974D383000000000000000000000000000000000000000057308081"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_9']/face">
		<face>
			<BodyProperties version="4" weight="0.7749" build="0.8015" key="000570058000038185611D291633E7A8434977A39731567986A7574582060843049CC04609797871000000000000000000000000000000000000000027285004"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_91']/face">
		<face>
			<BodyProperties version="4" weight="0.7328" build="0.7874" key="00055804400000096E711D2C02527AA4436974395B51CB6286A78917820A280602CCB0240C58C775000000000000000000000000000000000000000003A06100"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_10']/face">
		<face>
			<BodyProperties version="4" key="00058002C0FC20035646D8A987844C9488F76DD9A2367A3858B6C6698734870800054003073C8C7A000000000000000000000000000000000000000000A04000"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_11']/face">
		<face>
			<BodyProperties version="4" weight="0.7986" build="0.4567" key="000558044070000A5786655953636782884A77758E779328A9718E51703467650487C6460874C3760000000000000000000000000000000000000000051C0044"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_12']/face">
		<face>
			<BodyProperties version="4" weight="0.4393" build="0.5087" key="000B4405807010093586572B40753C33983B74857E715B17ACD18D517535345605A876560A68B318000000000000000000000000000000000000000015441083"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_13']/face">
		<face>
			<BodyProperties version="4" weight="0.6324" build="0.8677" key="000044034000238A2972092942746CA81A1B74BB8451FE5C76679B788227362104BD90460B8AE336000000000000000000000000000000000000000039788040"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_131']/face">
		<face>
			<BodyProperties version="4" weight="0" build="0" key="00006C07003C2153AA7896C4AC46539AAAB7C56E6B973AAAA85DEA793BB4C45C01F006890F76754B0000000000000000000000000000000000000000013C6044"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_13_1']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="000BB80893A02008B8C78A899993B688B48B5878B837C8B57B879938C5B85B14019776130989CA850000000000000000000000000000000000000000452C0004"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_15']/face">
		<face>
			<BodyProperties version="4" weight="0.7362" build="0.6653" key="0000600700001383AB62191815359CA8721876941F21B4384969AC5A8215275203AAB0350A57868200000000000000000000000000000000000000006F483042"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_15_1']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0005300E4B2C10106286C88D999368A86A9678899D4447A323667BC9A844A3BA01877613083838690000000000000000000000000000000000000000430410C3"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_15_2']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="00112C0A0FA0000A72557A7C9B879A8897CB1947DBA9DA87A53D947974F6DCB70167761306E83DA3000000000000000000000000000000000000000010CC0083"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_15_3']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="000B1C0154440001B544487899A76DA897C66DA64B699855366D7BA96969758401577613059EA3A8000000000000000000000000000000000000000042AC3043"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_14']/face">
		<face>
			<BodyProperties version="4" weight="0.7876" build="0.6364" key="000030058000000189620C2443356B748A587DDD9DB1A81478979D548215430103CAC0350C495C1500000000000000000000000000000000000000006B904040"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_14_1']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0005880A07CC0008B68B4646A8A6554A7A3983E856C787A768A8682A9457427301C776130C6CA4680000000000000000000000000000000000000000238C2043"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_16']/face">
		<face>
			<BodyProperties version="4" weight="0.7814" build="0.7805" key="00015C02C0003A145A600A1A4572AAC767467AF814019D8867999F04822B2A42048CC04508953C42000000000000000000000000000000000000000003008082"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_16_1']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0005600A002C2004569443C64645995CDA6B7ABA5BC4357B7C8694BC995E487801A776130A87872B00000000000000000000000000000000000000001B443083"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_16_2']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0005E0070000000D976577688DC974CDB7648B819B9E7598D9BC69B331354EA40187761308552777000000000000000000000000000000000000000022FC1004"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_17']/face">
		<face>
			<BodyProperties version="4" weight="0.67" build="0.8069" key="00050005800013481D61091C75439996538775CE780133A738967B5982154B12047CA04607449413000000000000000000000000000000000000000002F05100"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_17_1']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0005600CC0FC300B799DA3319A46DB4C77497515CB8AB436568CB7A5B9CA676801B776130B4BAA360000000000000000000000000000000000000000236C4003"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_18']/face">
		<face>
			<BodyProperties version="4" weight="0.771" build="0.6715" key="00007804400024092E611B2B242363C6799677AE9511745858A75E37832A312605DAC0570D8A4A7400000000000000000000000000000000000000007DB830C0"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_18_1']/face">
		<face>
			<BodyProperties version="4" build="0.5" key="000B900145882012209D8E3BA67B69A87A486958763A58B9B675C3C78686798901977613091866AB000000000000000000000000000000000000000071241083"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_19']/face">
		<face>
			<BodyProperties version="4" weight="0.6005" build="0.6567" key="00052807000004432871092255527C67584A76DC34C1D85475798E598229351302DA90240D68175300000000000000000000000000000000000000006FC02082"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_20']/face">
		<face>
			<BodyProperties version="4" weight="0.7857" build="0.893" key="00011002C00020082B722C3422645D877577756D05415DB7855AA713832A074102BDC0240B878444000000000000000000000000000000000000000017800100"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_21']/face">
		<face>
			<BodyProperties version="4" weight="0.7257" build="0.8089" key="00025C07000024CAB77216196072AAE6728B78827641ED973887AC168234162503ACB0350A99E955000000000000000000000000000000000000000005405082"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_21_1']/face">
		<face>
			<BodyProperties version="4" weight="0.7257" build="0.8089" key="00025C07000024CAB77216196072AAE6728B78827641ED973887AC168234162503ACB0350A99E955000000000000000000000000000000000000000005405082"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_21_2']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="000B2402C4CC000F46983AA68546B58A13047B958748D17A5AA943439A5AA44B018776130835D45200000000000000000000000000000000000000004F1C30C3"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_5_22']/face">
		<face>
			<BodyProperties version="4" weight="0.7901" build="0.7686" key="00050C02C0001549CD71192B144364956988797B4E51924558999A068314332605CCC0570C673752000000000000000000000000000000000000000023902004"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_B8_l']/face">
		<face>
			<BodyProperties version="4" weight="0.7901" build="0.7686" key="0011F40FC00015C31F80050E3578F78FC00E740704D0F71BF0C8EC145B060A4F013006440385C177000000000000000000000000000000000000000000143144"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_B8_s']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="000B2402C4CC000F46983AA68546B58A13047B958748D17A5AA943439A5AA44B018776130835D45200000000000000000000000000000000000000004F1C30C3"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_B8_c']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="00058C02C03C07C249A67962000F269EE3EFFE7528914639236B99548BFBD5A5010006890F9CA7B60000000000000000000000000000000000000000019C5104"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_1']/face">
		<face>
			<BodyProperties version="4" key="0016FC0FC000274D9EBF61FFB762A46EA4959AF6F9A58E3DC8756149D64B84A8000C265309B1E944000000000000000000000000000000000000000000F07080"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_2']/face">
		<face>
			<BodyProperties version="4" weight="0" build="0.4769" key="0005C00CC0FC100ACF976462A77185200E018468F0EF0103B6830C71720A23D40517065601101915000000000000000000000000000000000000000000240000"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_3']/face">
		<face>
			<BodyProperties version="4" weight="0.9067" build="0.8765" key="001CA00B40002505A27021394CB83B284566793CD6CE760869ABA623872A251306BDE0670B5556350000000000000000000000000000000000000000256820C1"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_4']/face">
		<face>
			<BodyProperties version="4" weight="0.054" build="0.4383" key="0011D40FDA002001E9761DA0AD922600CE626C60F0EF0710F7DA1E1A740E10510847168904634C0A000000000000000000000000000000000000000034944083"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_5']/face">
		<face>
			<BodyProperties version="4" weight="0.3531" build="0.4722" key="0015940B402C3754C39748898767B25A78E6B7968A3D58947935C86A959177790987509A0887B667000000000000000000000000000000000000000067183040"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_51']/face">
		<face>
			<BodyProperties version="4" key="001C840CC7FC03896DACA8926B35885A859C14A35794A7B4D6698187A2756C3C000C5534099977690000037F0000000F00000037049140010000000000788000"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_6']/face">
		<face>
			<BodyProperties version="4" weight="0.195" build="0.2239" key="001ED40D80FC06C67988B899C63C8B19698954BC6B89698827479497A849D99900633603067B8965000000000000000000000000000000000000000070CC2103"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_7']/face">
		<face>
			<BodyProperties version="4" weight="0.8289" build="0.9085" key="001C9808800036519270212A4BB7076824B8763BDAAF7B916A9A1B238431A313026EC024064567A500000000000000000000000000000000000000005AD84005"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_8']/face">
		<face>
			<BodyProperties version="4" weight="0.8457" build="0.7043" key="0016B00880002591A57052353CC733A81534763CD7EF7456798B483684294631076BD0780656661600000000000000000000000000000000000000004EC83044"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_81']/face">
		<face>
			<BodyProperties version="4" weight="0.8507" build="0.6256" key="0016E40A000025C8657022264CB7032826D5774CC6DE541B6ACA1B21852589410449D04604556705000000000000000000000000000000000000000046882005"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_9']/face">
		<face>
			<BodyProperties version="4" weight="0.8812" build="0.7079" key="001CD80B40003709647023132CB71B380645780ED9CF69777A9D9525881A9224056BD0560645665700000000000000000000000000000000000000000AD030C2"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_10']/face">
		<face>
			<BodyProperties version="4" key="001CC40FC03C1004E586A65AB6332A27A66555BCBECB2AA6B87BAC8B8567A764000639A302628966000000000000000000000000000000000000000000F030C2"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_101']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0011D40FC0CC300BF17616D8AD922604EE116506F7EF8110E0C91E3974071051000776030000120E0000000000000000000000000000000000000000340C2083"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_11']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0005FC0FDECC1004E9763E11D7E1A5309F306507F3EB0605C1BD1E06710F01F20167761406035305000000000000000000000000000000000000000034C40083"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_12']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0011FC0FC0B4300AF97613A1E4410600CF306C0A60EF0500F3B81E14741610460167761406016000000000000000000000000000000000000000000034C42083"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_13']/face">
		<face>
			<BodyProperties version="4" weight="0.8513" build="0.749" key="0011B80CC0002652917042372BB72A784427765EE8EF61166CAD772283331341068BD068084567D5000000000000000000000000000000000000000003084002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_15']/face">
		<face>
			<BodyProperties version="4" weight="0.9087" build="0.8402" key="0013F40CC0001550857042184BB725983516771EC6CE3A17798D571388226925068DE067084577C700000000000000000000000000000000000000004F0040C1"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_15_1']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0016FC0E5000100158708BD6CDC852299D4AB89EB77F390E84269783AA69586C017776130756C3A900000000000000000000000000000000000000003EF43083"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_15_2']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0016F80E5000200EB8708BD6CDC85229D3698B3ABDFE344CD22D3DD538898868017776130796723B00000000000000000000000000000000000000003EF41003"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_16']/face">
		<face>
			<BodyProperties version="4" weight="0.805" build="0.6475" key="0016E00B400006D3507044293BC820482534773EE5DE57546D6B291183394315056AC05706556766000000000000000000000000000000000000000028C82104"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_16_1']/face">
		<face>
			<BodyProperties version="4" weight="0.805" build="0.6475" key="0016E00B400006D3507044293BC820482534773EE5DE57546D6B291183394315056AC05706556766000000000000000000000000000000000000000028C82104"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_16_2']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0011D40FD4CC300BCF7616D8AD922604F4116550F7EF4310E0C91E1974071056016776130600120E000000000000000000000000000000000000000034C42083"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_17']/face">
		<face>
			<BodyProperties version="4" weight="0.8543" build="0.714" key="0015B8088000084AB37043164CC705582586776DF7CE17557B7B4A03851B7804026BD02406457607000000000000000000000000000000000000000050C84080"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_17_1']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0016F80E4000200AB8B5A8AB37C5891C4BA88B3ABDFE344C5ADD18434234CBA6017776130746491E00000000000000000000000000000000000000003EF410C3"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_17_2']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0011D40FC0003001E9761DA0AD922600C2226C60F0EF0710F7DA1E1A740E10500167761306634C0A000000000000000000000000000000000000000034C44083"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_18']/face">
		<face>
			<BodyProperties version="4" weight="0.9162" build="0.6798" key="001A980880000809737023143BB71B184445782FD9BF34777B8B47148025DA12038AE03508546644000000000000000000000000000000000000000035102005"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_18_1']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0016F80CD400100318B5A8AB37C5891C4BA88B3A22AF3A4C6388C1954234CBA601777613076B5C2300000000000000000000000000000000000000003EF41003"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_18_2']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0011DC0FD3FC300E488386A697AAB55B61978D49E9FE8438EC731556996786800137761303C76C170000000000000000000000000000000000000000426C4083"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_19']/face">
		<face>
			<BodyProperties version="4" weight="0.802" build="0.6066" key="0012B80CC0003809737023195BB843782617793FD6DF2B416B7DA901812178230259C02405557705000000000000000000000000000000000000000078A83040"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_19_1']/face">
		<face>
			<BodyProperties version="4" weight="0.3" build="0.4" key="000BF80CD100100318B664517CBB94C8D5398B3A22AF3A4C5B2C76989C692815017776130799894800000000000000000000000000000000000000003EF41043"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_19_2']/face">
		<face>
			<BodyProperties version="4" weight="0.4" build="0.4" key="0011C80E50E410026053AA569B16689986BA77528958D98CA8622BA78AC9695201577613052AB956000000000000000000000000000000000000000042B41084"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_20']/face">
		<face>
			<BodyProperties version="4" weight="0.8073" build="0.7638" key="001CB80B400028906370542B2CC8201805177A3EE5DE68166BBA982582121925046BC0460655761500000000000000000000000000000000000000005EC03042"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_20_1']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="000BC80FD1000010185895BAB8A7A974AA7C383AE2FF3A45868B383997479BD70177761307BB856400000000000000000000000000000000000000003EF410C3"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_K8_u']/face">
		<face>
			<BodyProperties version="4" weight="0.7" build="0.7" key="0026F80E40002343B8708B896D682237D5D88B3D557E344C17243FB5B88988680077760307967D3B00000000000000000000000000000000000000003EF41002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_21']/face">
		<face>
			<BodyProperties version="4" weight="0.8419" build="0.8066" key="001C940AC00018CD647043256BB8222824887A4ED3EF7A517C8B7E1184213914047CD0460754671400000000000000000000000000000000000000004AE03041"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_21_1']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0011FC0FC0B4300AF97613A1E4410600CB306C0A60EF0500F3B81E14741610460167761306016000000000000000000000000000000000000000000034C42083"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_22']/face">
		<face>
			<BodyProperties version="4" weight="0.8474" build="0.8778" key="0017B00C000007488370531C3CC826884583782EFABE43717BBC7536892A5612037DD0350754675600000000000000000000000000000000000000004CE82001"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_22_1']/face">
		<face>
			<BodyProperties version="4" weight="0.3" build="0.3" key="000BF80CD100300418762BAD894AB3A687C58B3A22AF3A4C5B2C769832963D44017776130799894800000000000000000000000000000000000000003EF41043"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_23']/face">
		<face>
			<BodyProperties version="4" weight="0.4676" build="0.4954" key="0011F00CC0B423940888751A1794B0496099B698B7A4AD33C63BA5535D78938903677635065B7912000000000000000000000000000000000000000000CC3042"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_K9_l']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="000BF80CD100300418762BAD894AB3A687C58B3A22AF3A4C5B2C769832963D44017776130799894800000000000000000000000000000000000000003EF41043"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_K9_s']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="000C100E007C03CD7748DA3B57429988D86DBD6945B4B664644589C5283414BC000427330A7661CB0000037F0000000F00000037049140010000000000288002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_K9_c1']/face">
		<face>
			<BodyProperties version="4" weight="0.3781" build="0.5478" key="000AB40CC0141840AD48BBECA574462983C78887724DBABB65D6686C8527AA8800D866030D8459A5000000000000000000000000000000000000000063AC10C6"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_K9_c2']/face">
		<face>
			<BodyProperties version="4" weight="0.142" build="0.5648" key="001EC00CC0FC100463B8C87941868498779B3336466B8678C67EEEDEC8AE75950088260308C7A5050000000000000000000000000000000000000000711C10C3"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_24']/face">
		<face>
			<BodyProperties version="4" key="0016FC0FC000274D9EBF61FFB762A46EA4959AF6F9A58E3DC8756149D64B84A8000C265309B1E944000000000000000000000000000000000000000000F07080"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_25']/face">
		<face>
			<BodyProperties version="4" key="0016FC0FC000274D9EBF61FFB762A46EA4959AF6F9A58E3DC8756149D64B84A8000C265309B1E944000000000000000000000000000000000000000000F07080"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_26']/face">
		<face>
			<BodyProperties version="4" key="0016FC0FC000274D9EBF61FFB762A46EA4959AF6F9A58E3DC8756149D64B84A8000C265309B1E944000000000000000000000000000000000000000000F07080"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_27']/face">
		<face>
			<BodyProperties version="4" key="0016FC0FC000274D9EBF61FFB762A46EA4959AF6F9A58E3DC8756149D64B84A8000C265309B1E944000000000000000000000000000000000000000000F07080"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_28']/face">
		<face>
			<BodyProperties version="4" key="0016FC0FC000274D9EBF61FFB762A46EA4959AF6F9A58E3DC8756149D64B84A8000C265309B1E944000000000000000000000000000000000000000000F07080"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_29']/face">
		<face>
			<BodyProperties version="4" key="0016FC0FC000274D9EBF61FFB762A46EA4959AF6F9A58E3DC8756149D64B84A8000C265309B1E944000000000000000000000000000000000000000000F07080"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_6_30']/face">
		<face>
			<BodyProperties version="4" key="0016FC0FC000274D9EBF61FFB762A46EA4959AF6F9A58E3DC8756149D64B84A8000C265309B1E944000000000000000000000000000000000000000000F07080"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_4_26_1']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="00164005802C100244849395E765385463ABA4846AA46E7A788D797974658C8901777613077C2D3C00000000000000000000000000000000000000002CEC40C3"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_E1_1']/face">
		<face>
			<BodyProperties version="4" weight="0.0254" build="0.0231" key="000040078000314278800E5DEFFE8FEFEE8078778FF7766F8888788888283F4600A006030AFFF0AE0000000000000000000000000000000000000000054C0142"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_E1_2']/face">
		<face>
			<BodyProperties version="4" weight="0.5648" build="0.5347" key="00002008C000314A377A9476A99A87A74B88B9A95638D789D9AA387495D7DA650088860308876555000000000000000000000000000000000000000043040040"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_E1_3']/face">
		<face>
			<BodyProperties version="4" weight="0.375" build="0.3704" key="002EA80FC000200567867061100A07F77BF8877718F181F184CD43B6886870FC00766603078BA5AA000000000000000000000000000000000000000042F43080"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_E1_4']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_E1_5']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_E1_6']/face">
		<face>
			<BodyProperties version="4" weight="0.375" build="0.3704" key="0010A8040000200567867061100A07F77BF8877718F181F184CD43B6886870FC00766603078BA5AA000000000000000000000000000000000000000042F43080"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_E1_11']/face">
		<face>
			<BodyProperties version="4" weight="0.375" build="0.3704" key="00103804C000200267867061100A07F77BF8877718F181F184CD43B6886870FC00766603078BA5AA000000000000000000000000000000000000000042F43080"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_E2_1']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_E2_2']/face">
		<face>
			<BodyProperties version="4" weight="0.375" build="0.3704" key="00103804C000200267867061100A07F77BF8877718F181F184CD43B6886870FC00766603078BA5AA000000000000000000000000000000000000000042F43080"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_E2_3']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_E2_4']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_E2_11']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_E3_1']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_E3_2']/face">
		<face>
			<BodyProperties version="4" weight="0.375" build="0.3704" key="00103804C000200267867061100A07F77BF8877718F181F184CD43B6886870FC00766603078BA5AA000000000000000000000000000000000000000042F43080"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_E3_3']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_E3_4']/face">
		<face>
			<BodyProperties version="4" weight="0.375" build="0.3704" key="00103804C000200267867061100A07F77BF8877718F181F184CD43B6886870FC00766603078BA5AA000000000000000000000000000000000000000042F43080"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_E3_5']/face">
		<face>
			<BodyProperties version="4" weight="0.375" build="0.3704" key="001080060000200267867061100A07F77BF8877718F181F184CD43B6886870FC00766603078BA5AA000000000000000000000000000000000000000042F43080"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_E3_11']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_E4_1']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_E4_2']/face">
		<face>
			<BodyProperties version="4" weight="0.375" build="0.3704" key="001080078000200267867061100A07F77BF8877718F181F184CD43B6886870FC00766603078BA5AA000000000000000000000000000000000000000042F43080"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_E4_3']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_E4_4']/face">
		<face>
			<BodyProperties version="4" weight="0.375" build="0.3704" key="001080008000200567867061100A07F77BF8877718F181F184CD43B6886870FC00766603078BA5AA000000000000000000000000000000000000000042F43080"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_E4_11']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_E5_1']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_E5_2']/face">
		<face>
			<BodyProperties version="4" weight="0.375" build="0.3704" key="0010800C4000200567867061100A07F77BF8877718F181F184CD43B6886870FC00766603078BA5AA000000000000000000000000000000000000000042F43080"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_E5_3']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_E5_4']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_E5_5']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_E5_11']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_E6_1']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_E6_2']/face">
		<face>
			<BodyProperties version="4" weight="0.375" build="0.3704" key="0010800C4000200567867061100A07F77BF8877718F181F184CD43B6886870FC00766603078BA5AA000000000000000000000000000000000000000042F43080"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_E6_3']/face">
		<face>
			<BodyProperties version="4" weight="0.375" build="0.3704" key="0010800C4000200567867061100A07F77BF8877718F181F184CD43B6886870FC00766603078BA5AA000000000000000000000000000000000000000042F43080"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_E6_4']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_E6_11']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_E7_1']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_E7_11']/face">
		<face>
			<BodyProperties version="4" weight="0.375" build="0.3704" key="0010800C4000200567867061100A07F77BF8877718F181F184CD43B6886870FC00766603078BA5AA000000000000000000000000000000000000000042F43080"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_R1_1']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="0005000FC00016039A797961AA0103D841B0830030ECF600921531198EF3BC94009B3603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_R1_2']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020000000016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B3603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_R1_3']/face">
		<face>
			<BodyProperties version="4" weight="0.0727" build="0.3588" key="0000000F000016029A797961AA0103D841B0830030ECF600921531198EF3BC9400951603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_R1_4']/face">
		<face>
			<BodyProperties version="4" weight="0.0727" build="0.3588" key="0000000F000016029A797961AA0103D841B0830030ECF600921531198EF3BC9400951603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_R1_5']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="0002000FC00010029A797961AA0103D841B0830030ECF600921531198EF3BC94009B3603098C7F4732430001330223550000000000001977000000004D244003"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_R1_11']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_R2_1']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020000000010029AD7969B978B596C89A4830030ECF600CA384656B1351968009B3603094A67A932430001330223550000000000001977000000004D2450C2"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_R2_11']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_M1_1']/face">
		<face>
			<BodyProperties version="4" weight="0.037" build="0.5602" key="00005400000000031683A74D6581724774C78063588BC601C4B6C72B8DE5863200B816030B85A80000000000000000000000000000000000000000003B6C9002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_M1_2']/face">
		<face>
			<BodyProperties version="4" weight="0.037" build="0.5602" key="00005400000000031683A74D6581724774C78063588BC601C4B6C72B8DE5863200B816030B85A80000000000000000000000000000000000000000003B6C9002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_M1_3']/face">
		<face>
			<BodyProperties version="4" weight="0.037" build="0.3634" key="00001C00000000031980F72F6853461660648330588B86016756B50C88A64A7500B516030B01EA0000000000000000000000000000000000000000003B6C4002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_M1_4']/face">
		<face>
			<BodyProperties version="4" weight="0.037" build="0.3634" key="00001C00000000031980F72F6853461660648330588B86016756B50C88A64A7500B516030B01EA0000000000000000000000000000000000000000003B6C4002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_M1_5']/face">
		<face>
			<BodyProperties version="4" weight="0.037" build="0.5602" key="00005400000000031683A74D6581724774C78063588BC601C4B6C72B8DE5863200B816030B85A80000000000000000000000000000000000000000003B6C9002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_M1_11']/face">
		<face>
			<BodyProperties version="4" weight="0.037" build="0.3634" key="00001C00000000031980F72F6853461660648330588B86016756B50C88A64A7500B516030B01EA0000000000000000000000000000000000000000003B6C4002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_M1_12']/face">
		<face>
			<BodyProperties version="4" weight="0.037" build="0.3634" key="00001C00000000031980F72F6853461660648330588B86016756B50C88A64A7500B516030B01EA0000000000000000000000000000000000000000003B6C4002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_M2_1']/face">
		<face>
			<BodyProperties version="4" weight="0.037" build="0.5602" key="00005400000000031683A74D6581724774C78063588BC601C4B6C72B8DE5863200B816030B85A80000000000000000000000000000000000000000003B6C9002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_M2_2']/face">
		<face>
			<BodyProperties version="4" weight="0.037" build="0.5602" key="00005400000000031683A74D6581724774C78063588BC601C4B6C72B8DE5863200B816030B85A80000000000000000000000000000000000000000003B6C9002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_M2_3']/face">
		<face>
			<BodyProperties version="4" weight="0.037" build="0.3634" key="00001C00000000031980F72F6853461660648330588B86016756B50C88A64A7500B516030B01EA0000000000000000000000000000000000000000003B6C4002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_M2_4']/face">
		<face>
			<BodyProperties version="4" weight="0.037" build="0.5602" key="00005400000000031683A74D6581724774C78063588BC601C4B6C72B8DE5863200B816030B85A80000000000000000000000000000000000000000003B6C9002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_M2_5']/face">
		<face>
			<BodyProperties version="4" weight="0.037" build="0.3634" key="00001C00000000031980F72F6853461660648330588B86016756B50C88A64A7500B516030B01EA0000000000000000000000000000000000000000003B6C4002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_M3_1']/face">
		<face>
			<BodyProperties version="4" weight="0.037" build="0.3634" key="00001C00000000031980F72F6853461660648330588B86016756B50C88A64A7500B516030B01EA0000000000000000000000000000000000000000003B6C4002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_M3_2']/face">
		<face>
			<BodyProperties version="4" weight="0.037" build="0.5602" key="00005400000000031683A74D6581724774C78063588BC601C4B6C72B8DE5863200B816030B85A80000000000000000000000000000000000000000003B6C9002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_M3_3']/face">
		<face>
			<BodyProperties version="4" weight="0.037" build="0.3634" key="00001C00000000031980F72F6853461660648330588B86016756B50C88A64A7500B516030B01EA0000000000000000000000000000000000000000003B6C4002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_M3_4']/face">
		<face>
			<BodyProperties version="4" weight="0.037" build="0.5602" key="00005400000000031683A74D6581724774C78063588BC601C4B6C72B8DE5863200B816030B85A80000000000000000000000000000000000000000003B6C9002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_M4_1']/face">
		<face>
			<BodyProperties version="4" weight="0.037" build="0.3634" key="00001C00000000031980F72F6853461660648330588B86016756B50C88A64A7500B516030B01EA0000000000000000000000000000000000000000003B6C4002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_M4_2']/face">
		<face>
			<BodyProperties version="4" weight="0.037" build="0.5602" key="00005400000000031683A74D6581724774C78063588BC601C4B6C72B8DE5863200B816030B85A80000000000000000000000000000000000000000003B6C9002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_M4_3']/face">
		<face>
			<BodyProperties version="4" weight="0.037" build="0.3634" key="00001C00000000031980F72F6853461660648330588B86016756B50C88A64A7500B516030B01EA0000000000000000000000000000000000000000003B6C4002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_M4_4']/face">
		<face>
			<BodyProperties version="4" weight="0.037" build="0.5602" key="00005400000000031683A74D6581724774C78063588BC601C4B6C72B8DE5863200B816030B85A80000000000000000000000000000000000000000003B6C9002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_M4_5']/face">
		<face>
			<BodyProperties version="4" weight="0.037" build="0.3634" key="00001C00000000031980F72F6853461660648330588B86016756B50C88A64A7500B516030B01EA0000000000000000000000000000000000000000003B6C4002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_M5_1']/face">
		<face>
			<BodyProperties version="4" weight="0.037" build="0.3634" key="00001C00000000031980F72F6853461660648330588B86016756B50C88A64A7500B516030B01EA0000000000000000000000000000000000000000003B6C4002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_M5_2']/face">
		<face>
			<BodyProperties version="4" weight="0.037" build="0.5602" key="00005400000000031683A74D6581724774C78063588BC601C4B6C72B8DE5863200B816030B85A80000000000000000000000000000000000000000003B6C9002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_M5_3']/face">
		<face>
			<BodyProperties version="4" weight="0.037" build="0.3634" key="00001C00000000031980F72F6853461660648330588B86016756B50C88A64A7500B516030B01EA0000000000000000000000000000000000000000003B6C4002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_M5_4']/face">
		<face>
			<BodyProperties version="4" weight="0.037" build="0.5602" key="00005400000000031683A74D6581724774C78063588BC601C4B6C72B8DE5863200B816030B85A80000000000000000000000000000000000000000003B6C9002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_M6_1']/face">
		<face>
			<BodyProperties version="4" weight="0.037" build="0.3634" key="00001C00000000031980F72F6853461660648330588B86016756B50C88A64A7500B516030B01EA0000000000000000000000000000000000000000003B6C4002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_M6_2']/face">
		<face>
			<BodyProperties version="4" weight="0.037" build="0.5602" key="00005400000000031683A74D6581724774C78063588BC601C4B6C72B8DE5863200B816030B85A80000000000000000000000000000000000000000003B6C9002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_M6_3']/face">
		<face>
			<BodyProperties version="4" weight="0.037" build="0.3634" key="00001C00000000031980F72F6853461660648330588B86016756B50C88A64A7500B516030B01EA0000000000000000000000000000000000000000003B6C4002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_M6_4']/face">
		<face>
			<BodyProperties version="4" weight="0.037" build="0.3634" key="00001C00000000031980F72F6853461660648330588B86016756B50C88A64A7500B516030B01EA0000000000000000000000000000000000000000003B6C4002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_M6_5']/face">
		<face>
			<BodyProperties version="4" weight="0.037" build="0.5602" key="00005400000000031683A74D6581724774C78063588BC601C4B6C72B8DE5863200B816030B85A80000000000000000000000000000000000000000003B6C9002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_L1_1']/face">
		<face>
			<BodyProperties version="4" weight="0.0139" build="0.9583" key="0000700000003005E77CE80EEFF01FF018FE88105FBF11800FF60FEE8EBF68F100DE06030D71E0E0000000000000000000000000000000000000000043AC2103"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_L1_2']/face">
		<face>
			<BodyProperties version="4" weight="0.1968" build="0.9583" key="0000CC0000003003E77FF80E81B7100118EE88105FBF11800FF60FEE8F86686000EE36030E71E0E0000000000000000000000000000000000000000043CC4142"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_L1_3']/face">
		<face>
			<BodyProperties version="4" weight="0.6" build="0.85" key="0028280FD7FC23C299C2761C4AC1A5954778548899A66659788878746A9D9629000F16530FA7699C000000000000000000000000000000000000000000F47140"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_L2_1']/face">
		<face>
			<BodyProperties version="4" weight="0.0294" build="0.128" key="0005B40A400000028771188F38770004088D88778888888888886F8D546AF01100F206030FE8888800000000000000000000000000000000000000007FF43005"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_I1_1']/face">
		<face>
			<BodyProperties version="4" weight="0.6713" build="0.9606" key="0000F00E40CC3002F18111F1FAFE111F010186EE87FE5F001010F10E8550FF4A008EA60308FFE01E000000000000000000000000000000000000000043040142"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_I1_11']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_I2_1']/face">
		<face>
			<BodyProperties version="4" weight="0.4282" build="0.5139" key="0000F008C0CC3002F181116178B9111F00F086EE87FE5F0010100100807000120088660308FEE01A000000000000000000000000000000000000000043040042"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_I2_11']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_I3_1']/face">
		<face>
			<BodyProperties version="4" weight="0.6829" build="0.6343" key="0005F00E40CC3003D68111100061000E00F086EE87FE5F101010010080700012009AA60309FEE0160000000000000000000000000000000000000000432C0042"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_I3_11']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_I4_1']/face">
		<face>
			<BodyProperties version="4" weight="0.6829" build="0.6343" key="0002F00FC0CC3003FF80F0F006FFF01F000077E1BE3E700F0111EFEE80100012009AA60309FFE00B0000000000000000000000000000000000000000432C0042"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_I4_11']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_I1_2']/face">
		<face>
			<BodyProperties version="4" weight="0.6821" build="0.9512" key="0000F00E40CC3002E28112F2FBFE112F020187EE88FE6F001110F20E8660FF5A008EA70408FFE02E000000000000000000000000000000000000000044050143"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_I1_3']/face">
		<face>
			<BodyProperties version="4" weight="0.6732" build="0.9623" key="0000F00E40CC3002C48114F4FDFE114F040189EE8AFE8F001310F40E8880FF7A008EA90608FFE04E000000000000000000000000000000000000000046070145"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_I1_31']/face">
		<face>
			<BodyProperties version="4" weight="0.23" build="0.73" key="00020006C00016029D799264AD0106D844B0860033ECF9009515341981F3BF97009B690309BC7F4A324300013302235800000000000019AA0000000050245005"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_I2_2']/face">
		<face>
			<BodyProperties version="4" weight="0.6954" build="0.9731" key="0000F00E40CC3002D38113F3FCFE113F030188EE89FE7F001210F30E8770FF6A008EA80508FFE03E000000000000000000000000000000000000000045060144"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_I2_3']/face">
		<face>
			<BodyProperties version="4" weight="0.1435" build="0.1597" key="0000000FC0000000E77F0FEEEFFF008F01FA76160877EFEF8888888881F87FF90082260308888888000000000000000000000000000000000000000043040140"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_I2_31']/face">
		<face>
			<BodyProperties version="4" weight="0.20" build="0.69" key="00020006C0001602A0799567B00109D847B0890036ECFC009815371984F3C29A009B6C0309EC7F4D324300013302235B00000000000019DD0000000053245008"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_I3_2']/face">
		<face>
			<BodyProperties version="4" weight="0.7041" build="0.9842" key="0000F00E40CC3002B58115F5FEFE115F05018AEE8BFE9F001410F50E8990FF8A008EAA0708FFE05E000000000000000000000000000000000000000047080146"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_I3_21']/face">
		<face>
			<BodyProperties version="4" weight="0.21" build="0.70" key="00020006C00016029E799365AE0107D845B0870034ECFA009615351982F3C098009B6A0309CC7F4B324300013302235900000000000019BB0000000051245006"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_I3_3']/face">
		<face>
			<BodyProperties version="4" weight="0.1435" build="0.1597" key="0000000FC0000000E77F0FEEEFFF008F01FA76160877EFEF8888888881F87FF90082260308888888000000000000000000000000000000000000000043040140"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_I3_31']/face">
		<face>
			<BodyProperties version="4" weight="0.22" build="0.71" key="00020006C00016029F799466AF0108D846B0880035ECFB009715361983F3C199009B6B0309DC7F4C324300013302235A00000000000019CC0000000052245007"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_I4_2']/face">
		<face>
			<BodyProperties version="4" weight="0.0417" build="0.3079" key="000DF00FC000300087714AE109AE7FEF01FE8877888888888888888881400FF70095160309888888000000000000000000000000000000000000000043240140"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_I4_21']/face">
		<face>
			<BodyProperties version="4" weight="0.20" build="0.69" key="00020006C0001602A0799567B00109D847B0890036ECFC009815371984F3C29A009B6C0309EC7F4D324300013302235B00000000000019DD0000000053245008"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_I4_3']/face">
		<face>
			<BodyProperties version="4" weight="0.2824" build="0.3079" key="001BF00FC0003000875A49C689838DDC01FE8877888888888888888881400FF70095460309888888000000000000000000000000000000000000000043240140"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_I4_31']/face">
		<face>
			<BodyProperties version="4" weight="0.19" build="0.68" key="00020006C0001602A1799668B1010AD848B08A0037ECFD009915381985F3C39B009B6D0309FC7F4E324300013302235C00000000000019EE0000000054245009"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_I4_4']/face">
		<face>
			<BodyProperties version="4" weight="0.4768" build="0.3079" key="000DF00FC0001000877523756B77468C01FE8877888888888888888881400FF7008576030888888800000000000000000000000000000000000000000AFC0140"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_I4_41']/face">
		<face>
			<BodyProperties version="4" weight="0.18" build="0.67" key="00020006C0001602A2799769B2010BD849B08B0038ECFE009A15391986F3C49C009B6E030A0C7F4F324300013302235D00000000000019FF000000005524500A"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_I1_4']/face">
		<face>
			<BodyProperties version="4" weight="0.7142" build="0.8956" key="0000F00E40CC3002A68116F6FFFE116F06018BEE8CFE9F001510F60E89A0FF9A008EAB0808FFE06E000000000000000000000000000000000000000048090147"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_I1_5']/face">
		<face>
			<BodyProperties version="4" weight="0.8234" build="0.9234" key="0000F00E40CC3002978117F7F0FE117F07018CEE8DFE9F001610F70E89B0FFAA008EAC0908FFE07E000000000000000000000000000000000000000049010148"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_I1_6']/face">
		<face>
			<BodyProperties version="4" weight="0.5678" build="0.8123" key="0000F00E40CC3002888118F8F1FE118F08018DEE8EFEBF001710F80E89C0FFBA008EAD0A08FFE08E000000000000000000000000000000000000000040020149"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_I1_7']/face">
		<face>
			<BodyProperties version="4" weight="0.7856" build="0.9456" key="0000F00E40CC3002798119F9F2FE119F09018EEE8FFEDF001810F90E89D0FFCA008EAE0B08FFE09E000000000000000000000000000000000000000041030140"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_I1_8']/face">
		<face>
			<BodyProperties version="4" weight="0.6945" build="0.8845" key="0000F00E40CC300260811AFAFAFE11AF0A018FEE80FEFF001910FA0E89E0FFDA008EAF0C08FFE0AE000000000000000000000000000000000000000042040141"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_I2_4']/face">
		<face>
			<BodyProperties version="4" weight="0.5234" build="0.7234" key="0000F00E40CC300251811BFBFBFE11BF0B0180EE81FE0F002010FB0E89F0FFEA008EA00D08FFE0BE000000000000000000000000000000000000000043050142"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_I2_5']/face">
		<face>
			<BodyProperties version="4" weight="0.7123" build="0.8567" key="0000F00E40CC300242811CFCFCFE11CF0C0181EE82FE1F002110FC0E8900FF0A008EA10E08FFE0CE000000000000000000000000000000000000000044060143"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_I2_6']/face">
		<face>
			<BodyProperties version="4" weight="0.6234" build="0.7890" key="0000F00E40CC300233811DFDFDFE11DF0D0182EE83FE2F002210FD0E8910FF1A008EA20F08FFE0DE000000000000000000000000000000000000000045070144"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_I2_7']/face">
		<face>
			<BodyProperties version="4" weight="0.6789" build="0.8234" key="0000F00E40CC300224811EFEFEFEFE1EF0E0183EE84FE3F002310FE0E8920FF2A008EA30008FFE0EE000000000000000000000000000000000000000046080145"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_I2_8']/face">
		<face>
			<BodyProperties version="4" weight="0.7345" build="0.8789" key="0000F00E40CC300215811FF0F0FE11FF0F0184EE85FE4F002410FF0E8930FF3A008EA40108FFE0FE000000000000000000000000000000000000000047090146"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_G1_1']/face">
		<face>
			<BodyProperties version="4" weight="0.2084" build="0.5231" key="0000F00FC00030008771188F38770F8801F188778888888888888888546AF0F900C836030C8888880000000000000000000000000000000000000000439C0140"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_G1_2']/face">
		<face>
			<BodyProperties version="4" weight="0.2084" build="0.5231" key="0000F00FC00030008771188F38770F8801F188778888888888888888546AF0F900C836030C8888880000000000000000000000000000000000000000439C0140"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_G1_3']/face">
		<face>
			<BodyProperties version="4" weight="0.2084" build="0.5231" key="0000F00FC00030008771188F38770F8801F188778888888888888888546AF0F900C836030C8888880000000000000000000000000000000000000000439C0140"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_G1_4']/face">
		<face>
			<BodyProperties version="4" weight="0.2084" build="0.5231" key="0000F00FC00030008771188F38770F8801F188778888888888888888546AF0F900C836030C8888880000000000000000000000000000000000000000439C0140"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_G1_5']/face">
		<face>
			<BodyProperties version="4" weight="0.2084" build="0.5231" key="0000F00FC00030008771188F38770F8801F188778888888888888888546AF0F900C836030C8888880000000000000000000000000000000000000000439C0140"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_G1_6']/face">
		<face>
			<BodyProperties version="4" weight="0.2084" build="0.5231" key="0000F00FC00030008771188F38770F8801F188778888888888888888546AF0F900C836030C8888880000000000000000000000000000000000000000439C0140"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_G1_7']/face">
		<face>
			<BodyProperties version="4" weight="0.2084" build="0.5231" key="0000F00FC00030008771188F38770F8801F188778888888888888888546AF0F900C836030C8888880000000000000000000000000000000000000000439C0140"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_G1_8']/face">
		<face>
			<BodyProperties version="4" weight="0.2084" build="0.5231" key="0000F00FC00030008771188F38770F8801F188778888888888888888546AF0F900C836030C8888880000000000000000000000000000000000000000439C0140"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_G1_9']/face">
		<face>
			<BodyProperties version="4" weight="0.2084" build="0.5231" key="0000F00FC00030008771188F38770F8801F188778888888888888888546AF0F900C836030C8888880000000000000000000000000000000000000000439C0140"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_G1_10']/face">
		<face>
			<BodyProperties version="4" weight="0.2084" build="0.5231" key="0000F00FC00030008771188F38770F8801F188778888888888888888546AF0F900C836030C8888880000000000000000000000000000000000000000439C0140"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_G2_1']/face">
		<face>
			<BodyProperties version="4" weight="0.2084" build="0.5231" key="0000F00FC00030008771188F38770F8801F188778888888888888888546AF0F900C836030C8888880000000000000000000000000000000000000000439C0140"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_G2_2']/face">
		<face>
			<BodyProperties version="4" weight="0.2084" build="0.5231" key="0000F00FC00030008771188F38770F8801F188778888888888888888546AF0F900C836030C8888880000000000000000000000000000000000000000439C0140"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_G2_3']/face">
		<face>
			<BodyProperties version="4" weight="0.2084" build="0.5231" key="0000F00FC00030008771188F38770F8801F188778888888888888888546AF0F900C836030C8888880000000000000000000000000000000000000000439C0140"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_G2_4']/face">
		<face>
			<BodyProperties version="4" weight="0.2084" build="0.5231" key="0000F00FC00030008771188F38770F8801F188778888888888888888546AF0F900C836030C8888880000000000000000000000000000000000000000439C0140"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_G2_5']/face">
		<face>
			<BodyProperties version="4" weight="0.2084" build="0.5231" key="0000F00FC00030008771188F38770F8801F188778888888888888888546AF0F900C836030C8888880000000000000000000000000000000000000000439C0140"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_G2_6']/face">
		<face>
			<BodyProperties version="4" weight="0.2084" build="0.5231" key="0000F00FC00030008771188F38770F8801F188778888888888888888546AF0F900C836030C8888880000000000000000000000000000000000000000439C0140"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_G2_7']/face">
		<face>
			<BodyProperties version="4" weight="0.2084" build="0.5231" key="0000F00FC00030008771188F38770F8801F188778888888888888888546AF0F900C836030C8888880000000000000000000000000000000000000000439C0140"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_G2_8']/face">
		<face>
			<BodyProperties version="4" weight="0.2084" build="0.5231" key="0000F00FC00030008771188F38770F8801F188778888888888888888546AF0F900C836030C8888880000000000000000000000000000000000000000439C0140"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_G2_9']/face">
		<face>
			<BodyProperties version="4" weight="0.2084" build="0.5231" key="0000F00FC00030008771188F38770F8801F188778888888888888888546AF0F900C836030C8888880000000000000000000000000000000000000000439C0140"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_G2_10']/face">
		<face>
			<BodyProperties version="4" weight="0.2084" build="0.5231" key="0000F00FC00030008771188F38770F8801F188778888888888888888546AF0F900C836030C8888880000000000000000000000000000000000000000439C0140"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_G3_1']/face">
		<face>
			<BodyProperties version="4" weight="0.2084" build="0.5231" key="0000F00FC00030008771188F38770F8801F188778888888888888888546AF0F900C836030C8888880000000000000000000000000000000000000000439C0140"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_G3_2']/face">
		<face>
			<BodyProperties version="4" weight="0.2084" build="0.5231" key="0000F00FC00030008771188F38770F8801F188778888888888888888546AF0F900C836030C8888880000000000000000000000000000000000000000439C0140"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_G3_3']/face">
		<face>
			<BodyProperties version="4" weight="0.2084" build="0.5231" key="0000F00FC00030008771188F38770F8801F188778888888888888888546AF0F900C836030C8888880000000000000000000000000000000000000000439C0140"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_G3_4']/face">
		<face>
			<BodyProperties version="4" weight="0.2084" build="0.5231" key="0000F00FC00030008771188F38770F8801F188778888888888888888546AF0F900C836030C8888880000000000000000000000000000000000000000439C0140"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_G3_5']/face">
		<face>
			<BodyProperties version="4" weight="0.2084" build="0.5231" key="0000F00FC00030008771188F38770F8801F188778888888888888888546AF0F900C836030C8888880000000000000000000000000000000000000000439C0140"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_G3_6']/face">
		<face>
			<BodyProperties version="4" weight="0.2084" build="0.5231" key="0000F00FC00030008771188F38770F8801F188778888888888888888546AF0F900C836030C8888880000000000000000000000000000000000000000439C0140"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_G3_7']/face">
		<face>
			<BodyProperties version="4" weight="0.2084" build="0.5231" key="0000F00FC00030008771188F38770F8801F188778888888888888888546AF0F900C836030C8888880000000000000000000000000000000000000000439C0140"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_G3_8']/face">
		<face>
			<BodyProperties version="4" weight="0.2084" build="0.5231" key="0000F00FC00030008771188F38770F8801F188778888888888888888546AF0F900C836030C8888880000000000000000000000000000000000000000439C0140"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_G3_9']/face">
		<face>
			<BodyProperties version="4" weight="0.2084" build="0.5231" key="0000F00FC00030008771188F38770F8801F188778888888888888888546AF0F900C836030C8888880000000000000000000000000000000000000000439C0140"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_G3_10']/face">
		<face>
			<BodyProperties version="4" weight="0.2084" build="0.5231" key="0000F00FC00030008771188F38770F8801F188778888888888888888546AF0F900C836030C8888880000000000000000000000000000000000000000439C0140"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_G4_1']/face">
		<face>
			<BodyProperties version="4" weight="0.2084" build="0.5231" key="0000F00FC00030008771188F38770F8801F188778888888888888888546AF0F900C836030C8888880000000000000000000000000000000000000000439C0140"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_G4_2']/face">
		<face>
			<BodyProperties version="4" weight="0.2084" build="0.5231" key="0000F00FC00030008771188F38770F8801F188778888888888888888546AF0F900C836030C8888880000000000000000000000000000000000000000439C0140"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_G4_3']/face">
		<face>
			<BodyProperties version="4" weight="0.2084" build="0.5231" key="0000F00FC00030008771188F38770F8801F188778888888888888888546AF0F900C836030C8888880000000000000000000000000000000000000000439C0140"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_G4_4']/face">
		<face>
			<BodyProperties version="4" weight="0.2084" build="0.5231" key="0000F00FC00030008771188F38770F8801F188778888888888888888546AF0F900C836030C8888880000000000000000000000000000000000000000439C0140"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_G4_5']/face">
		<face>
			<BodyProperties version="4" weight="0.2084" build="0.5231" key="0000F00FC00030008771188F38770F8801F188778888888888888888546AF0F900C836030C8888880000000000000000000000000000000000000000439C0140"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_G4_6']/face">
		<face>
			<BodyProperties version="4" weight="0.2084" build="0.5231" key="0000F00FC00030008771188F38770F8801F188778888888888888888546AF0F900C836030C8888880000000000000000000000000000000000000000439C0140"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_G4_7']/face">
		<face>
			<BodyProperties version="4" weight="0.2084" build="0.5231" key="0000F00FC00030008771188F38770F8801F188778888888888888888546AF0F900C836030C8888880000000000000000000000000000000000000000439C0140"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_G4_8']/face">
		<face>
			<BodyProperties version="4" weight="0.2084" build="0.5231" key="0000F00FC00030008771188F38770F8801F188778888888888888888546AF0F900C836030C8888880000000000000000000000000000000000000000439C0140"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_G4_9']/face">
		<face>
			<BodyProperties version="4" weight="0.2084" build="0.5231" key="0000F00FC00030008771188F38770F8801F188778888888888888888546AF0F900C836030C8888880000000000000000000000000000000000000000439C0140"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_G4_10']/face">
		<face>
			<BodyProperties version="4" weight="0.2084" build="0.5231" key="0000F00FC00030008771188F38770F8801F188778888888888888888546AF0F900C836030C8888880000000000000000000000000000000000000000439C0140"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_G5_1']/face">
		<face>
			<BodyProperties version="4" weight="0.2084" build="0.5231" key="0000F00FC00030008771188F38770F8801F188778888888888888888546AF0F900C836030C8888880000000000000000000000000000000000000000439C0140"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_G5_2']/face">
		<face>
			<BodyProperties version="4" weight="0.2084" build="0.5231" key="0000F00FC00030008771188F38770F8801F188778888888888888888546AF0F900C836030C8888880000000000000000000000000000000000000000439C0140"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_G5_3']/face">
		<face>
			<BodyProperties version="4" weight="0.2084" build="0.5231" key="0000F00FC00030008771188F38770F8801F188778888888888888888546AF0F900C836030C8888880000000000000000000000000000000000000000439C0140"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_G5_4']/face">
		<face>
			<BodyProperties version="4" weight="0.2084" build="0.5231" key="0000F00FC00030008771188F38770F8801F188778888888888888888546AF0F900C836030C8888880000000000000000000000000000000000000000439C0140"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_G5_5']/face">
		<face>
			<BodyProperties version="4" weight="0.2084" build="0.5231" key="0000F00FC00030008771188F38770F8801F188778888888888888888546AF0F900C836030C8888880000000000000000000000000000000000000000439C0140"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_G5_6']/face">
		<face>
			<BodyProperties version="4" weight="0.2084" build="0.5231" key="0000F00FC00030008771188F38770F8801F188778888888888888888546AF0F900C836030C8888880000000000000000000000000000000000000000439C0140"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_G5_7']/face">
		<face>
			<BodyProperties version="4" weight="0.2084" build="0.5231" key="0000F00FC00030008771188F38770F8801F188778888888888888888546AF0F900C836030C8888880000000000000000000000000000000000000000439C0140"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_G5_8']/face">
		<face>
			<BodyProperties version="4" weight="0.2084" build="0.5231" key="0000F00FC00030008771188F38770F8801F188778888888888888888546AF0F900C836030C8888880000000000000000000000000000000000000000439C0140"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_G5_9']/face">
		<face>
			<BodyProperties version="4" weight="0.2084" build="0.5231" key="0000F00FC00030008771188F38770F8801F188778888888888888888546AF0F900C836030C8888880000000000000000000000000000000000000000439C0140"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_G5_10']/face">
		<face>
			<BodyProperties version="4" weight="0.2084" build="0.5231" key="0000F00FC00030008771188F38770F8801F188778888888888888888546AF0F900C836030C8888880000000000000000000000000000000000000000439C0140"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_U1_1']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_U1_11']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_U1_12']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_U1_13']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_D1_2']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_D1_3']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_D1_4']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_D1_5']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_D1_6']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_D1_7']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_D1_8']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_D1_9']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_D1_10']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_D2_1']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_D2_2']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_D2_3']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_D2_4']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_D2_5']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_D2_6']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_D2_7']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_D2_8']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_D2_9']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_D2_10']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_D3_1']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_D3_2']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_D3_3']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_D3_4']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_D3_5']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_D3_6']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_D3_7']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_D3_8']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_D3_9']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_D3_10']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_D4_1']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_D4_2']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_D4_3']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_D4_4']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_D4_5']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_D4_6']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_D4_7']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_D4_8']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_D4_9']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_D4_10']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_D5_1']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_D5_2']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_D5_3']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_D5_4']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_D5_5']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_D5_6']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_D5_7']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_D5_8']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_D5_9']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_D5_10']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_D6_1']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_D6_2']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_D6_3']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_D6_4']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_D6_5']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_D6_6']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_D6_7']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_D6_8']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_D6_9']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_D6_10']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_M10_1']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_M10_11']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_M11_1']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_M11_11']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_M12_1']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_M12_11']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_M13_1']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_M13_11']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_M14_1']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_M14_11']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_M15_1']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_M15_11']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_M16_1']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_M16_11']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_M17_1']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_M17_11']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_M18_1']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_M18_11']/face">
		<face>
			<BodyProperties version="4" weight="0.2" build="0.74" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_EW_1']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0039F00FC000300010000010010100000001000000000100001011101000000000FF06030F000100000000000000000000000000000000000000000043F44142"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_EW_2']/face">
		<face>
			<BodyProperties version="4" weight="0" build="0.7222" key="0010D80FC00008836B8ACCBDD7B986785FFDCA8B7C8B677A67895C1885BB9A60009B06030909B70500000000000000000000000000000000000000003F3C9002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_EW_3']/face">
		<face>
			<BodyProperties version="4" weight="0" build="0.7222" key="0010A80AC0000383848ACCBDD7B9867816B89C9D99E968E14766495A757A1696009B060309AD13C400000000000000000000000000000000000000003F3C3002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_EW_4']/face">
		<face>
			<BodyProperties version="4" weight="0" build="0.7222" key="0010A80FC0000401848ACCBDD7B9867816B89C9D99E968E14766495A757A1696009B060309AD13C400000000000000000000000000000000000000003F3C3002"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_EW_5']/face">
		<face>
			<BodyProperties version="4" weight="0" build="0.7222" key="0010A80FC0000001848ACCBDD7B9867816B89C9D99E968E14766495A757A1696009B060309AD13C400000000000000000000000000000000000000003F3C3003"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_EW_6']/face">
		<face>
			<BodyProperties version="4" weight="0.2534" build="0.5733" key="00057C0D0000131AC186C84D4697AC7356B46C8983BEB92A379743C66986654300B946030BACA5A60000000000000000000000000000000000000000676C6082"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_EW_7']/face">
		<face>
			<BodyProperties version="4" weight="0.2534" build="0.5733" key="00057C0D000014C5C186C84D4697AC7356B46C8983BEB92A379743C66986654300B946030BACA5A60000000000000000000000000000000000000000676C6082"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_EW_8']/face">
		<face>
			<BodyProperties version="4" weight="0.2534" build="0.5733" key="000AD406C000140B7B86C84D4697AC7372796289843BB979379743C6C9AC9BD900B946030BACA5A60000000000000000000000000000000000000000676C3082"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_EW_9']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0039F00FC000300010000010010100000001000000000100001011101000000000FF06030F000100000000000000000000000000000000000000000043F44142"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_EW_10']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0039F00FC000300010000010010100000001000000000100001011101000000000FF06030F000100000000000000000000000000000000000000000043F44142"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_EW_11']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0039F00FC000300010000010010100000001000000000100001011101000000000FF06030F000100000000000000000000000000000000000000000043F44142"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_EW_12']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0039F00FC000300010000010010100000001000000000100001011101000000000FF06030F000100000000000000000000000000000000000000000043F44142"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_EW_13']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0039F00FC000300010000010010100000001000000000100001011101000000000FF06030F000100000000000000000000000000000000000000000043F44142"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_EW_14']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0039F00FC000300010000010010100000001000000000100001011101000000000FF06030F000100000000000000000000000000000000000000000043F44142"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_EW_15']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0039F00FC000300010000010010100000001000000000100001011101000000000FF06030F000100000000000000000000000000000000000000000043F44142"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_EW_16']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0039F00FC000300010000010010100000001000000000100001011101000000000FF06030F000100000000000000000000000000000000000000000043F44142"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_EW_17']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0039F00FC000300010000010010100000001000000000100001011101000000000FF06030F000100000000000000000000000000000000000000000043F44142"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_EW_18']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0039F00FC000300010000010010100000001000000000100001011101000000000FF06030F000100000000000000000000000000000000000000000043F44142"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_EW_19']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0039F00FC000300010000010010100000001000000000100001011101000000000FF06030F000100000000000000000000000000000000000000000043F44142"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_EW_20']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0039F00FC000300010000010010100000001000000000100001011101000000000FF06030F000100000000000000000000000000000000000000000043F44142"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_EW_21']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0039F00FC000300010000010010100000001000000000100001011101000000000FF06030F000100000000000000000000000000000000000000000043F44142"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_EW_22']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0039F00FC000300010000010010100000001000000000100001011101000000000FF06030F000100000000000000000000000000000000000000000043F44142"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_EW_23']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0039F00FC000300010000010010100000001000000000100001011101000000000FF06030F000100000000000000000000000000000000000000000043F44142"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_EW_24']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0039F00FC000300010000010010100000001000000000100001011101000000000FF06030F000100000000000000000000000000000000000000000043F44142"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_EW_25']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0039F00FC000300010000010010100000001000000000100001011101000000000FF06030F000100000000000000000000000000000000000000000043F44142"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_EW_26']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0039F00FC000300010000010010100000001000000000100001011101000000000FF06030F000100000000000000000000000000000000000000000043F44142"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_EW_27']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0039F00FC000300010000010010100000001000000000100001011101000000000FF06030F000100000000000000000000000000000000000000000043F44142"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>

	<xsl:template match="NPCCharacter[@id='lord_EW_28']/face">
		<face>
			<BodyProperties version="4" weight="0.5" build="0.5" key="0039F00FC000300010000010010100000001000000000100001011101000000000FF06030F000100000000000000000000000000000000000000000043F44142"/>
			<xsl:copy-of select="hair_tags"/>
			<xsl:copy-of select="beard_tags"/>
			<xsl:copy-of select="tattoo_tags"/>
		</face>
	</xsl:template>
</xsl:stylesheet>

