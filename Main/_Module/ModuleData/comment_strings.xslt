<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
	<xsl:output omit-xml-declaration="no" indent="yes"/>

	<!-- Identity transformation - copies everything by default -->
	<xsl:template match="@*|node()">
		<xsl:copy>
			<xsl:apply-templates select="@*|node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- ============================================================ -->
	<!-- Remove "the" before faction/kingdom/clan names               -->
	<!-- ============================================================ -->

	<xsl:template match="string[@id='str_comment_vassal_introduces_self.default']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=L2GbDVja}I am {CONVERSATION_CHARACTER.LINK} of {CLAN_NAME}, my {?PLAYER.GENDER}lady{?}lord{\?}, and I am honored to serve you.</xsl:attribute>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_comment_noble_introduces_self.sturgia']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=dC8acIya}I am {CONVERSATION_CHARACTER.LINK} of {CLAN_NAME}.</xsl:attribute>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_comment_noble_introduces_self_and_clan.sturgia']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=dlkg2jQf}I am {CONVERSATION_CHARACTER.LINK}, of {CLAN_NAME}, one of the ancient lineages of the Sturgians.</xsl:attribute>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_comment_noble_introduces_self_and_clan.sturgia_boasting']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=Nq2dyiO1}I am {CONVERSATION_CHARACTER.LINK}, of {CLAN_NAME}, a name you will have heard many times in the sagas of the Sturgians.</xsl:attribute>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_comment_noble_introduces_self_and_clan.sturgia_boasting_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=PBgLvyOa}I am {CONVERSATION_CHARACTER.LINK}, of {CLAN_NAME}. Our ancestors were among those who came from the far north to take the princes' denar and swear an oath to defend these lands, and we have held true to that oath.</xsl:attribute>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_comment_noble_introduces_self.battania']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=3UucAOBW}I am {CONVERSATION_CHARACTER.LINK} of {CLAN_NAME}.</xsl:attribute>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_comment_noble_introduces_self_and_clan.battania']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=Ua0T0A7t}I am {CONVERSATION_CHARACTER.LINK}, of {CLAN_NAME}, one of the ancient lineages of the Battanians.</xsl:attribute>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_comment_noble_introduces_self_and_clan.battania_boasting']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=U770Bvy8}I am {CONVERSATION_CHARACTER.LINK}, of {CLAN_NAME}. The fame of our family is second to none in the songs of the Battanians.</xsl:attribute>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_comment_noble_introduces_self_and_clan.battania_boasting_harsh']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=rhTUDKhJ}I am {CONVERSATION_CHARACTER.LINK}, of {CLAN_NAME}. There's not a clan of the Battanians who hasn't contributed the skull of one of their champions to our collection of drinking cups.</xsl:attribute>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_comment_noble_introduces_self_and_clan.battania_boasting_ironic']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=JJAWbuNT}I am {CONVERSATION_CHARACTER.LINK}, of {CLAN_NAME}. They say our ancestor was among those who came in the golden ships that came to this land from the West, in the Time of the Heroes. Of course, if you believe that everyone said to be on those boats was on those boats, they'd have capsized from the weight.</xsl:attribute>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_comment_noble_introduces_self.vlandia']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=WH8P3Y5F}I am {CONVERSATION_CHARACTER.LINK} of {CLAN_NAME}.</xsl:attribute>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_comment_noble_introduces_self_and_clan.vlandia']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=eyxfHmCU}I am {CONVERSATION_CHARACTER.LINK}, of {CLAN_NAME}. Our family has served the kings of the Vlandians for generations.</xsl:attribute>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_comment_noble_introduces_self_and_clan.vlandia_2']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=cMO9JXvk}I am {CONVERSATION_CHARACTER.LINK}, of {CLAN_NAME}. We hold our lands in fief to {LIEGE_TITLE}, and we honor our oaths of fealty.</xsl:attribute>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_comment_noble_introduces_self_and_clan.vlandia_boasting']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=FFXqPFwl}I am {CONVERSATION_CHARACTER.LINK}, of {CLAN_NAME}, one of the most illustrious families in the annals of the Vlandian kingdom.</xsl:attribute>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_comment_noble_introduces_self_and_clan.vlandia_boasting_ironic']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=pn79h0aj}I am {CONVERSATION_CHARACTER.LINK}, of {CLAN_NAME}. We received our title from the Emperor Vortios himself. Not that he had much choice about it, as he was held captive in one of our raiding ships at the time, but I'm sure he recognized the nobility of our blood.</xsl:attribute>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_comment_noble_introduces_self.aserai']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=l2nEIZV5}I am {CONVERSATION_CHARACTER.LINK} of {CLAN_NAME}.</xsl:attribute>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_comment_noble_introduces_self_and_clan.aserai']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=S1vGzr8z}I am {CONVERSATION_CHARACTER.LINK}, of {CLAN_NAME}, a clan of the Aserai.</xsl:attribute>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_comment_noble_introduces_self_and_clan.aserai_boasting']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=zZ9FQJRI}I am {CONVERSATION_CHARACTER.LINK}, of {CLAN_NAME}, the most ancient and noble of the Aserai lineages.</xsl:attribute>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_comment_noble_introduces_self.khuzait']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=U4p70l9B}I am {CONVERSATION_CHARACTER.LINK} of {CLAN_NAME}.</xsl:attribute>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_comment_noble_introduces_self_and_clan.khuzait_boasting']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=a8584KD0}I am {CONVERSATION_CHARACTER.LINK}, of {CLAN_NAME}. I am sure you have heard the name of our clan in the epics of the steppe. We carry the name of one the 12 sons of the great she-wolf, mother of the Khuzaits.</xsl:attribute>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_comment_noble_introduces_self_and_clan.khuzait']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=z2yr4mc6}I am {CONVERSATION_CHARACTER.LINK}, of {CLAN_NAME}. Our clan has loyally served the Khuzait Khanate for generations.</xsl:attribute>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_comment_noble_introduces_self_and_clan.default']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=62vceTar}I am {CONVERSATION_CHARACTER.LINK}, of {CLAN_NAME}.</xsl:attribute>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="string[@id='str_comment_minor_faction_leader_introduces_self.default']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text']"/>
			<xsl:attribute name="text">{=99gE1ZoY}I am {CONVERSATION_CHARACTER.LINK}, of {FACTION}.</xsl:attribute>
		</xsl:copy>
	</xsl:template>

</xsl:stylesheet>
