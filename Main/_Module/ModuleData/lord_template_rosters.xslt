<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
	<!-- Generated lords of the six renamed vanilla cultures (Rohan vlandia, Dunland empire, Harad aserai,
	     Easterlings khuzait, Dale sturgia, Khand battania) must draw TAOM gear.

	     DefaultEquipmentSelectionModel.GetSuitableEquipmentSet (v1.5.4) pools every EquipmentRoster whose
	     culture is the hero's and whose Flags equal the requested categories exactly, then picks one set at
	     random. SandBoxCore's own lord and ruler templates stay loaded, so a generated Dunland lord drew from
	     26 Empire sets and one TAOM set. This removes IsLordTemplate and IsKingdomRulerTemplate from those
	     vanilla rosters, leaving TAOM's taom_* templates (taom_lord_template_equipment.xml) as the pool.
	     That includes SandBox's noble teenager templates, so a noble teen of these cultures now wears the
	     TAOM teen template, as the other twelve cultures already do. The rosters themselves stay, so anything
	     naming one by id still resolves.

	     Child templates keep their flags in all six cultures: TAOM ships child templates for Khand only, and
	     an empty pool hands the other five cultures' children null equipment. Khand's children therefore
	     still draw vanilla Battanian child clothes alongside TAOM's.

	     The engine applies this stylesheet only to roster nodes merged before it, so its SubModule.xml node
	     must stay the last EquipmentRosters node. LordTemplateRosterTests pins all of this. -->
	<xsl:output omit-xml-declaration="no" indent="yes"/>

	<xsl:template match="@*|node()">
		<xsl:copy>
			<xsl:apply-templates select="@*|node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="EquipmentRoster[@culture='Culture.vlandia' or @culture='Culture.empire' or @culture='Culture.aserai'
	                                     or @culture='Culture.khuzait' or @culture='Culture.sturgia' or @culture='Culture.battania']
	                                    [not(starts-with(@id, 'taom_'))]
	                     /Flags[not(@IsChildEquipmentTemplate='true')]
	                     /@*[name()='IsLordTemplate' or name()='IsKingdomRulerTemplate']"/>
</xsl:stylesheet>
