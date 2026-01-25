<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
    <xsl:output method="xml" indent="yes" encoding="utf-8"/>

    <!-- Identity transformation - copies everything by default -->
    <xsl:template match="@*|node()">
        <xsl:copy>
            <xsl:apply-templates select="@*|node()"/>
        </xsl:copy>
    </xsl:template>


    <!-- ============================================== -->
    <!-- Faction 1 - Empire/Dunland -->
    <!-- ============================================== -->

    <!-- lord_1_1 -->
    <xsl:template match="NPCCharacter[@id='lord_1_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_1_1_name}Brenin Wulf, the Ironhand</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.3009" build="0.9977" key="00180800811414C2878F1FFF4F9F1EEE7F018877888888888888888F7718F5F5009F560309888888000000000000000000000000000000000000000043244142"/>
            </face>
            <skills>
                <skill id="OneHanded" value="260"/>
                <skill id="TwoHanded" value="190"/>
                <skill id="Polearm" value="160"/>
                <skill id="Bow" value="110"/>
                <skill id="Crossbow" value="60"/>
                <skill id="Throwing" value="150"/>
                <skill id="Riding" value="90"/>
                <skill id="Athletics" value="230"/>
                <skill id="Crafting" value="70"/>
                <skill id="Scouting" value="170"/>
                <skill id="Tactics" value="220"/>
                <skill id="Charm" value="190"/>
                <skill id="Leadership" value="200"/>
                <skill id="Trade" value="110"/>
                <skill id="Steward" value="210"/>
                <skill id="Medicine" value="90"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="1"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Calculating" value="0"/>
                <Trait id="Mercy" value="0"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="-1"/>
                <Trait id="Oligarchic" value="1"/>
                <Trait id="Authoritarian" value="2"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_2 -->
    <xsl:template match="NPCCharacter[@id='lord_1_2']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_1_2_name}Freya Wolfheart</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.6213" build="0.5278" key="000D74060814100168588D58B57BB7342987B6DA87388A678888888F8588656A0068960306888888000000000000000000000000000000000000000042CC1103"/>
            </face>
            <skills>
                <skill id="OneHanded" value="180"/>
                <skill id="TwoHanded" value="120"/>
                <skill id="Polearm" value="100"/>
                <skill id="Bow" value="140"/>
                <skill id="Crossbow" value="80"/>
                <skill id="Throwing" value="130"/>
                <skill id="Riding" value="70"/>
                <skill id="Athletics" value="190"/>
                <skill id="Crafting" value="100"/>
                <skill id="Scouting" value="180"/>
                <skill id="Tactics" value="160"/>
                <skill id="Charm" value="230"/>
                <skill id="Leadership" value="110"/>
                <skill id="Trade" value="150"/>
                <skill id="Steward" value="240"/>
                <skill id="Medicine" value="170"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="1"/>
                <Trait id="Generosity" value="2"/>
                <Trait id="Calculating" value="1"/>
                <Trait id="Mercy" value="1"/>
                <Trait id="Valor" value="0"/>
                <Trait id="Egalitarian" value="0"/>
                <Trait id="Oligarchic" value="1"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_3 -->
    <xsl:template match="NPCCharacter[@id='lord_1_3']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_1_3_name}Gorwulf, The Boar </xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.2051" build="0.2259" key="0018900FC22C29029E693487686495249959879685AD41D37B7BC6A72C72B2C5007336030746B8C400000000000000000000000000000000000000002EF41102"/>
            </face>
            <skills>
                <skill id="OneHanded" value="245"/>
                <skill id="TwoHanded" value="175"/>
                <skill id="Polearm" value="145"/>
                <skill id="Bow" value="110"/>
                <skill id="Crossbow" value="65"/>
                <skill id="Throwing" value="165"/>
                <skill id="Riding" value="55"/>
                <skill id="Athletics" value="225"/>
                <skill id="Crafting" value="85"/>
                <skill id="Scouting" value="140"/>
                <skill id="Tactics" value="175"/>
                <skill id="Charm" value="130"/>
                <skill id="Leadership" value="115"/>
                <skill id="Trade" value="80"/>
                <skill id="Steward" value="125"/>
                <skill id="Medicine" value="95"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="0"/>
                <Trait id="Generosity" value="-1"/>
                <Trait id="Calculating" value="0"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="-1"/>
                <Trait id="Oligarchic" value="0"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_4 -->
    <xsl:template match="NPCCharacter[@id='lord_1_4']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_1_4_name}Brunhild Ironclaw</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.3278" build="0.5208" key="0018900FC32C0004277D1767865C6D65C99ABB9D9A854973C599D87C96798E5B00785603076C374A00000000000000000000000000000000000000002EDC4083"/>
            </face>
            <skills>
                <skill id="OneHanded" value="230"/>
                <skill id="TwoHanded" value="165"/>
                <skill id="Polearm" value="135"/>
                <skill id="Bow" value="115"/>
                <skill id="Crossbow" value="65"/>
                <skill id="Throwing" value="155"/>
                <skill id="Riding" value="55"/>
                <skill id="Athletics" value="215"/>
                <skill id="Crafting" value="85"/>
                <skill id="Scouting" value="145"/>
                <skill id="Tactics" value="170"/>
                <skill id="Charm" value="135"/>
                <skill id="Leadership" value="110"/>
                <skill id="Trade" value="85"/>
                <skill id="Steward" value="125"/>
                <skill id="Medicine" value="95"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="0"/>
                <Trait id="Generosity" value="-1"/>
                <Trait id="Calculating" value="1"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="-1"/>
                <Trait id="Oligarchic" value="0"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_5 -->
    <xsl:template match="NPCCharacter[@id='lord_1_5']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_1_5_name}Othric The Wild</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.4676" build="0.8519" key="000A7C0E434413144D3B8B8895B7BAA1B892A95A7777C459352A36B6B836977600AD76030A78257900000000000000000000000000000000000000002F543002"/>
            </face>
            <skills>
                <skill id="OneHanded" value="260"/>
                <skill id="TwoHanded" value="190"/>
                <skill id="Polearm" value="160"/>
                <skill id="Bow" value="120"/>
                <skill id="Crossbow" value="70"/>
                <skill id="Throwing" value="170"/>
                <skill id="Riding" value="80"/>
                <skill id="Athletics" value="240"/>
                <skill id="Crafting" value="100"/>
                <skill id="Scouting" value="165"/>
                <skill id="Tactics" value="220"/>
                <skill id="Charm" value="190"/>
                <skill id="Leadership" value="150"/>
                <skill id="Trade" value="110"/>
                <skill id="Steward" value="210"/>
                <skill id="Medicine" value="100"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="1"/>
                <Trait id="Generosity" value="0"/>
                <Trait id="Calculating" value="0"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="-1"/>
                <Trait id="Oligarchic" value="1"/>
                <Trait id="Authoritarian" value="2"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_6 -->
    <xsl:template match="NPCCharacter[@id='lord_1_6']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_1_6_name}Torvald Oxhorn</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.4676" build="0.4745" key="000A5006C644045997988BCD74A733AD17786B9C6A5788AD62AA34835485628A0087760308A5527600000000000000000000000000000000000000002EFC10C2"/>
            </face>
            <skills>
                <skill id="OneHanded" value="240"/>
                <skill id="TwoHanded" value="170"/>
                <skill id="Polearm" value="140"/>
                <skill id="Bow" value="110"/>
                <skill id="Crossbow" value="60"/>
                <skill id="Throwing" value="160"/>
                <skill id="Riding" value="55"/>
                <skill id="Athletics" value="225"/>
                <skill id="Crafting" value="90"/>
                <skill id="Scouting" value="145"/>
                <skill id="Tactics" value="175"/>
                <skill id="Charm" value="130"/>
                <skill id="Leadership" value="65"/>
                <skill id="Trade" value="85"/>
                <skill id="Steward" value="125"/>
                <skill id="Medicine" value="95"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="0"/>
                <Trait id="Generosity" value="-1"/>
                <Trait id="Calculating" value="0"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="-1"/>
                <Trait id="Oligarchic" value="0"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_7 -->
    <xsl:template match="NPCCharacter[@id='lord_1_7']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_1_7_name}Denethor II, Steward of Gondor</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.4583" build="0.1343" key="0002540F007010024A6A02B8F08300011060730012EEF7017B0B010E7E918F4500927603091485F7000000000000000000000000000000000000000021342142"/>
            </face>
            <skills>
                <skill id="OneHanded" value="150"/>
                <skill id="TwoHanded" value="120"/>
                <skill id="Polearm" value="100"/>
                <skill id="Bow" value="100"/>
                <skill id="Crossbow" value="100"/>
                <skill id="Throwing" value="80"/>
                <skill id="Riding" value="150"/>
                <skill id="Athletics" value="100"/>
                <skill id="Crafting" value="80"/>
                <skill id="Scouting" value="100"/>
                <skill id="Tactics" value="250"/>
                <skill id="Roguery" value="280"/>
                <skill id="Charm" value="200"/>
                <skill id="Leadership" value="250"/>
                <skill id="Trade" value="150"/>
                <skill id="Steward" value="350"/>
                <skill id="Medicine" value="100"/>
                <skill id="Engineering" value="150"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="1"/>
                <Trait id="Generosity" value="0"/>
                <Trait id="Calculating" value="2"/>
                <Trait id="Mercy" value="0"/>
                <Trait id="Valor" value="0"/>
                <Trait id="Egalitarian" value="0"/>
                <Trait id="Oligarchic" value="1"/>
                <Trait id="Authoritarian" value="2"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_8 -->
    <xsl:template match="NPCCharacter[@id='lord_1_8']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_1_8_name}Vendelia</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5507" build="0.5759" key="000BDC0A000020028672362967865369A91887176B723646DCA3773773849A54056986570636A93300000000000000000000000000000000000000004ADC2082"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="Politician" value="2"/>
                <Trait id="Manager" value="4"/>
                <Trait id="Honor" value="1"/>
                <Trait id="Generosity" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_9 -->
    <xsl:template match="NPCCharacter[@id='lord_1_9']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_1_9_name}Imrahil, Prince of Dol Amroth</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.0185" build="0.7199" key="000A480FC00000022F70080860528544369289B2BF8A7E05776961088DB01839009B06030966530400000000000000000000000000000000000000003D3C1002"/>
            </face>
            <skills>
                <skill id="OneHanded" value="270"/>
                <skill id="TwoHanded" value="250"/>
                <skill id="Polearm" value="260"/>
                <skill id="Bow" value="150"/>
                <skill id="Crossbow" value="100"/>
                <skill id="Throwing" value="120"/>
                <skill id="Riding" value="280"/>
                <skill id="Athletics" value="200"/>
                <skill id="Crafting" value="50"/>
                <skill id="Scouting" value="150"/>
                <skill id="Tactics" value="250"/>
                <skill id="Charm" value="220"/>
                <skill id="Leadership" value="270"/>
                <skill id="Trade" value="100"/>
                <skill id="Steward" value="250"/>
                <skill id="Medicine" value="100"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="2"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Calculating" value="0"/>
                <Trait id="Mercy" value="1"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="1"/>
                <Trait id="Oligarchic" value="0"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_10 -->
    <xsl:template match="NPCCharacter[@id='lord_1_10']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_1_10_name}Melkea</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.4241" build="0.7095" key="000BAC0A00141004B77326194AC4402599338A6798924403D8C24F3379749A46047B6646071634C300000000000000000000000000000000000000001EF41083"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="Politician" value="6"/>
                <Trait id="Manager" value="2"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Honor" value="-1"/>
                <Trait id="Calculating" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_25 -->
    <xsl:template match="NPCCharacter[@id='lord_1_25']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_1_25_name}Eronyx</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.7886" build="0.5725" key="000BAC088000368E94800B923B3A288737AC765AF2D7A91197858F34731640730199C013097777C600000000000000000000000000000000000000005D204000"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="BalancedFightingSkills" value="3"/>
                <Trait id="Commander" value="2"/>
                <Trait id="Politician" value="2"/>
                <Trait id="Manager" value="4"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Honor" value="-1"/>
                <Trait id="Valor" value="1"/>
                <Trait id="Generosity" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_35 -->
    <xsl:template match="NPCCharacter[@id='lord_1_35']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_1_35_name}Amenon</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.6154" build="0.6899" key="00057C04400020029673264B47856554EA72881968858A46D9A28774766555770374963507334537000000000000000000000000000000000000000020FC2003"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="KnightFightingSkills" value="6"/>
                <Trait id="Commander" value="10"/>
                <Trait id="Politician" value="4"/>
                <Trait id="Manager" value="6"/>
                <Trait id="Valor" value="1"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Calculating" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_11 -->
    <xsl:template match="NPCCharacter[@id='lord_1_11']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_1_11_name}Ciryandur</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.0486" build="0.3495" key="0008A004001403840B8F40126041750131F6838530990000BB79500E79FBFEA200A516030A8BB57C1F6410605020004000000000000017670000000043444003"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="KnightFightingSkills" value="6"/>
                <Trait id="Commander" value="4"/>
                <Trait id="Politician" value="4"/>
                <Trait id="Manager" value="9"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Mercy" value="0"/>
                <Trait id="Honor" value="2"/>
                <Trait id="Calculating" value="0"/>
                <Trait id="Egalitarian" value="0"/>
                <Trait id="Oligarchic" value="1"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_111 -->
    <xsl:template match="NPCCharacter[@id='lord_1_111']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_1_111_name}Casinon</xsl:attribute>
            <xsl:attribute name="default_group">HorseArcher</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.4" key="000BD0058000328878DAE579759C4B844D469CDC9A3683A59A15AF927269E22C01F004780F5D7806000000000000000000000000000000000000000000C01184"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="HorseArcherFightingSkills" value="5"/>
                <Trait id="Commander" value="6"/>
                <Trait id="Politician" value="6"/>
                <Trait id="Manager" value="2"/>
                <Trait id="Honor" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_12 -->
    <xsl:template match="NPCCharacter[@id='lord_1_12']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_1_12_name}Lysica</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.4" build="0.5" key="000CA00E40BC2005478D688968868296A6779BB376D6A724D866C7B9A9A97B7D0007077C0D629B660000000000000FFE00000000000000000000000000001080"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="HorseArcherFightingSkills" value="4"/>
                <Trait id="Commander" value="4"/>
                <Trait id="Politician" value="8"/>
                <Trait id="Manager" value="2"/>
                <Trait id="Honor" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_14 -->
    <xsl:template match="NPCCharacter[@id='lord_1_14']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_1_14_name}Mouth of Sauron</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.1" build="0.99" key="0000F10E90FB50046958D3288B71766558599899863A9B25B8E39F6A978897400090876088487420000000000000000000000000000000000000000013C4040"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="KnightFightingSkills" value="10"/>
                <Trait id="Commander" value="10"/>
                <Trait id="Politician" value="10"/>
                <Trait id="Manager" value="8"/>
                <Trait id="Valor" value="0"/>
                <Trait id="Generosity" value="-2"/>
                <Trait id="Mercy" value="-2"/>
                <Trait id="Honor" value="-2"/>
                <Trait id="Calculating" value="2"/>
                <Trait id="Egalitarian" value="-2"/>
                <Trait id="Oligarchic" value="2"/>
                <Trait id="Authoritarian" value="2"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_15 -->
    <xsl:template match="NPCCharacter[@id='lord_1_15']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_1_15_name}Witch-King of Angmar</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.0116" build="1" key="0039F00FC000300010000010010100000001000000000100001011101000000000FF06030F000100000000000000000000000000000000000000000043F44142"/>
            </face>
            <skills>
                <skill id="OneHanded" value="350"/>
                <skill id="TwoHanded" value="320"/>
                <skill id="Polearm" value="330"/>
                <skill id="Bow" value="200"/>
                <skill id="Crossbow" value="150"/>
                <skill id="Throwing" value="180"/>
                <skill id="Riding" value="340"/>
                <skill id="Athletics" value="250"/>
                <skill id="Crafting" value="50"/>
                <skill id="Scouting" value="200"/>
                <skill id="Tactics" value="350"/>
                <skill id="Charm" value="100"/>
                <skill id="Leadership" value="300"/>
                <skill id="Trade" value="50"/>
                <skill id="Steward" value="250"/>
                <skill id="Medicine" value="80"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="-2"/>
                <Trait id="Generosity" value="-1"/>
                <Trait id="Calculating" value="2"/>
                <Trait id="Mercy" value="-2"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="-1"/>
                <Trait id="Oligarchic" value="1"/>
                <Trait id="Authoritarian" value="2"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_155 -->
    <xsl:template match="NPCCharacter[@id='lord_1_155']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_1_155_name}NazgÃ»l, The Dark Marshall </xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.0116" build="1" key="0039F00FC000300010000010010100000001000000000100001011101000000000FF06030F000100000000000000000000000000000000000000000043F44142"/>
            </face>
            <skills>
                <skill id="OneHanded" value="300"/>
                <skill id="TwoHanded" value="310"/>
                <skill id="Polearm" value="310"/>
                <skill id="Bow" value="200"/>
                <skill id="Crossbow" value="150"/>
                <skill id="Throwing" value="180"/>
                <skill id="Riding" value="350"/>
                <skill id="Athletics" value="310"/>
                <skill id="Crafting" value="50"/>
                <skill id="Scouting" value="300"/>
                <skill id="Tactics" value="350"/>
                <skill id="Charm" value="100"/>
                <skill id="Leadership" value="290"/>
                <skill id="Trade" value="50"/>
                <skill id="Steward" value="250"/>
                <skill id="Medicine" value="80"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="-2"/>
                <Trait id="Generosity" value="-1"/>
                <Trait id="Calculating" value="2"/>
                <Trait id="Mercy" value="-2"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="-1"/>
                <Trait id="Oligarchic" value="1"/>
                <Trait id="Authoritarian" value="2"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_16 -->
    <xsl:template match="NPCCharacter[@id='lord_1_16']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_1_16_name}NazgÃ»l, The Knight of Umbar</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.0116" build="1" key="0039F00FC000300010000010010100000001000000000100001011101000000000FF06030F000100000000000000000000000000000000000000000043F44142"/>
            </face>
            <skills>
                <skill id="OneHanded" value="300"/>
                <skill id="TwoHanded" value="310"/>
                <skill id="Polearm" value="310"/>
                <skill id="Bow" value="200"/>
                <skill id="Crossbow" value="150"/>
                <skill id="Throwing" value="180"/>
                <skill id="Riding" value="350"/>
                <skill id="Athletics" value="310"/>
                <skill id="Crafting" value="50"/>
                <skill id="Scouting" value="300"/>
                <skill id="Tactics" value="350"/>
                <skill id="Charm" value="100"/>
                <skill id="Leadership" value="290"/>
                <skill id="Trade" value="50"/>
                <skill id="Steward" value="250"/>
                <skill id="Medicine" value="80"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="-2"/>
                <Trait id="Generosity" value="-1"/>
                <Trait id="Calculating" value="2"/>
                <Trait id="Mercy" value="-2"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="-1"/>
                <Trait id="Oligarchic" value="1"/>
                <Trait id="Authoritarian" value="2"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_17 -->
    <xsl:template match="NPCCharacter[@id='lord_1_17']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_1_17_name}Sauron - Just To View Armor</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0" build="0.0162" key="00000C0FC000300010000011110000000000000010010001011001101010000000F006030F001101000000000000000000000000000000000000000005F40142"/>
            </face>
            <skills>
                <skill id="OneHanded" value="300"/>
                <skill id="TwoHanded" value="300"/>
                <skill id="Polearm" value="300"/>
                <skill id="Bow" value="200"/>
                <skill id="Crossbow" value="150"/>
                <skill id="Throwing" value="300"/>
                <skill id="Riding" value="300"/>
                <skill id="Athletics" value="300"/>
                <skill id="Crafting" value="550"/>
                <skill id="Scouting" value="300"/>
                <skill id="Tactics" value="350"/>
                <skill id="Charm" value="500"/>
                <skill id="Leadership" value="305"/>
                <skill id="Trade" value="350"/>
                <skill id="Steward" value="310"/>
                <skill id="Medicine" value="250"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="-2"/>
                <Trait id="Generosity" value="-1"/>
                <Trait id="Calculating" value="2"/>
                <Trait id="Mercy" value="-2"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="-1"/>
                <Trait id="Oligarchic" value="1"/>
                <Trait id="Authoritarian" value="2"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_177 -->
    <xsl:template match="NPCCharacter[@id='lord_1_177']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_1_177_name}Honoratus</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.6" build="0.6" key="0011CC0700000002755D98B886639678761559B8B7C879465D6A74A99895786C01E000090EA91E5500001105000030A30000001F0000000000000000009C2000"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="KnightFightingSkills" value="4"/>
                <Trait id="Commander" value="6"/>
                <Trait id="Politician" value="4"/>
                <Trait id="Manager" value="2"/>
                <Trait id="Calculating" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_18 -->
    <xsl:template match="NPCCharacter[@id='lord_1_18']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_1_18_name}Jathea</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.75" build="0.5" key="0005880FC000000E98AA77B76970A46A77E739675A3862468B759958865696BD00040008058788780000000000000FFE00000000000000000000000000000000"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="Politician" value="8"/>
                <Trait id="Manager" value="6"/>
                <Trait id="Valor" value="-1"/>
                <Trait id="Generosity" value="-1"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_21 -->
    <xsl:template match="NPCCharacter[@id='lord_1_21']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_1_21_name}Fenrik the Red Wolf</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.7019" build="0.3678" key="00026C09851423C2DE888372433B967E7F01649228AD4DB58888888F7718F5F500C6B6030C888888000000000000000000000000000000000000000043840142"/>
            </face>
            <skills>
                <skill id="OneHanded" value="240"/>
                <skill id="TwoHanded" value="170"/>
                <skill id="Polearm" value="140"/>
                <skill id="Bow" value="120"/>
                <skill id="Crossbow" value="70"/>
                <skill id="Throwing" value="160"/>
                <skill id="Riding" value="60"/>
                <skill id="Athletics" value="220"/>
                <skill id="Crafting" value="90"/>
                <skill id="Scouting" value="150"/>
                <skill id="Tactics" value="180"/>
                <skill id="Charm" value="140"/>
                <skill id="Leadership" value="110"/>
                <skill id="Trade" value="90"/>
                <skill id="Steward" value="130"/>
                <skill id="Medicine" value="100"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="0"/>
                <Trait id="Generosity" value="0"/>
                <Trait id="Calculating" value="0"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="-1"/>
                <Trait id="Oligarchic" value="0"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_22 -->
    <xsl:template match="NPCCharacter[@id='lord_1_22']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_1_22_name}Bjornric Strongarm</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.3278" build="0.9412" key="0018540FC32C1A546669AC7C84CA7269A89E996472AB6A254BC5AC69693A675500BE56030BB8DD4800000000000000000000000000000000000000002F7C4042"/>
            </face>
            <skills>
                <skill id="OneHanded" value="240"/>
                <skill id="TwoHanded" value="170"/>
                <skill id="Polearm" value="140"/>
                <skill id="Bow" value="110"/>
                <skill id="Crossbow" value="60"/>
                <skill id="Throwing" value="160"/>
                <skill id="Riding" value="50"/>
                <skill id="Athletics" value="220"/>
                <skill id="Crafting" value="90"/>
                <skill id="Scouting" value="145"/>
                <skill id="Tactics" value="170"/>
                <skill id="Charm" value="130"/>
                <skill id="Leadership" value="50"/>
                <skill id="Trade" value="80"/>
                <skill id="Steward" value="120"/>
                <skill id="Medicine" value="90"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="0"/>
                <Trait id="Generosity" value="-1"/>
                <Trait id="Calculating" value="0"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="-1"/>
                <Trait id="Oligarchic" value="0"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_23 -->
    <xsl:template match="NPCCharacter[@id='lord_1_23']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_1_23_name}Jastion</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.7" key="0000B002C000300DEB67D938447261A7658B5985A9A6594E143D5345AAE756A7000522540598D94800001105000030A30000001F000000000000000000787000"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="BalancedFightingSkills" value="4"/>
                <Trait id="Commander" value="6"/>
                <Trait id="Politician" value="8"/>
                <Trait id="Manager" value="8"/>
                <Trait id="Valor" value="1"/>
                <Trait id="Calculating" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_24 -->
    <xsl:template match="NPCCharacter[@id='lord_1_24']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_1_24_name}Tadeos</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.6" build="0.5" key="0016F00580FC334D7B57826CA4C27CA67949E6ACA88B3ACD7A45DA9768858686000329A20373767B00001105000030A30000001F000000000000000000A06000"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="KnightFightingSkills" value="5"/>
                <Trait id="Commander" value="8"/>
                <Trait id="Politician" value="8"/>
                <Trait id="Manager" value="8"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Calculating" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_75 -->
    <xsl:template match="NPCCharacter[@id='lord_1_75']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_1_75_name}Boromir</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.4236" build="0.7407" key="00020006C00016029A797961AA0103D841B0830030ECF600921531198EF3BC94009B6603098C7F4732430001330223550000000000001977000000004D245002"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="KnightFightingSkills" value="10"/>
                <Trait id="Commander" value="8"/>
                <Trait id="Politician" value="8"/>
                <Trait id="Manager" value="10"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Mercy" value="0"/>
                <Trait id="Honor" value="2"/>
                <Trait id="Calculating" value="1"/>
                <Trait id="Egalitarian" value="0"/>
                <Trait id="Oligarchic" value="1"/>
                <Trait id="Authoritarian" value="2"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_26 -->
    <xsl:template match="NPCCharacter[@id='lord_1_26']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_1_26_name}Meritor</xsl:attribute>
            <xsl:attribute name="default_group">HorseArcher</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.4" build="0.6" key="0016040E400003C67A776955EA791E79989C4B679A8C5255A77376D888C8D68800000DB2055C7BA600001105000030A30000001F000000000000000000048000"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="HorseArcherFightingSkills" value="5"/>
                <Trait id="Commander" value="6"/>
                <Trait id="Politician" value="4"/>
                <Trait id="Manager" value="10"/>
                <Trait id="Valor" value="-1"/>
                <Trait id="Honor" value="1"/>
                <Trait id="Calculating" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_27 -->
    <xsl:template match="NPCCharacter[@id='lord_1_27']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_1_27_name}Maugrash</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.6" build="0.3" key="000B6C02C0BC100679D8455AA9684859888735557C6493786867627857B2895800058893058735D900001105000030A30000001F000000000000000001686000"/>
            </face>
            <skills>
                <skill id="OneHanded" value="240"/>
                <skill id="TwoHanded" value="230"/>
                <skill id="Polearm" value="260"/>
                <skill id="Bow" value="200"/>
                <skill id="Crossbow" value="150"/>
                <skill id="Throwing" value="180"/>
                <skill id="Riding" value="250"/>
                <skill id="Athletics" value="290"/>
                <skill id="Crafting" value="250"/>
                <skill id="Scouting" value="300"/>
                <skill id="Tactics" value="290"/>
                <skill id="Charm" value="100"/>
                <skill id="Leadership" value="280"/>
                <skill id="Trade" value="50"/>
                <skill id="Steward" value="250"/>
                <skill id="Medicine" value="180"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="-2"/>
                <Trait id="Generosity" value="-1"/>
                <Trait id="Calculating" value="2"/>
                <Trait id="Mercy" value="-2"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="-1"/>
                <Trait id="Oligarchic" value="1"/>
                <Trait id="Authoritarian" value="2"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_28 -->
    <xsl:template match="NPCCharacter[@id='lord_1_28']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_1_28_name}NazgÃ»l, The Betrayer</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.0116" build="1" key="0039F00FC000300010000010010100000001000000000100001011101000000000FF06030F000100000000000000000000000000000000000000000043F44142"/>
            </face>
            <skills>
                <skill id="OneHanded" value="305"/>
                <skill id="TwoHanded" value="295"/>
                <skill id="Polearm" value="290"/>
                <skill id="Bow" value="200"/>
                <skill id="Crossbow" value="150"/>
                <skill id="Throwing" value="180"/>
                <skill id="Riding" value="350"/>
                <skill id="Athletics" value="309"/>
                <skill id="Crafting" value="250"/>
                <skill id="Scouting" value="300"/>
                <skill id="Tactics" value="290"/>
                <skill id="Charm" value="100"/>
                <skill id="Leadership" value="290"/>
                <skill id="Trade" value="50"/>
                <skill id="Steward" value="250"/>
                <skill id="Medicine" value="180"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="-2"/>
                <Trait id="Generosity" value="-1"/>
                <Trait id="Calculating" value="2"/>
                <Trait id="Mercy" value="-2"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="-1"/>
                <Trait id="Oligarchic" value="1"/>
                <Trait id="Authoritarian" value="2"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_29 -->
    <xsl:template match="NPCCharacter[@id='lord_1_29']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_1_29_name}Sanion</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.4" build="0.6" key="000BA00B40001008A43A8AB8557888874A6A94868B767788988844778A69247900058005079777A800001105000030A30000001F000000000000000000CC0000"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="KnightFightingSkills" value="8"/>
                <Trait id="Commander" value="8"/>
                <Trait id="Politician" value="6"/>
                <Trait id="Manager" value="10"/>
                <Trait id="Valor" value="1"/>
                <Trait id="Mercy" value="1"/>
                <Trait id="Calculating" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_31 -->
    <xsl:template match="NPCCharacter[@id='lord_1_31']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_1_31_name}Zrsa Blackfang</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.6213" build="0.5278" key="000DB80D0C141012B89AB3B6BA36B78C5289399A7B938997C8CDA86AA3BD1A270068960306C18788000000000000000000000000000000000000000042CC0083"/>
            </face>
            <skills>
                <skill id="OneHanded" value="235"/>
                <skill id="TwoHanded" value="165"/>
                <skill id="Polearm" value="135"/>
                <skill id="Bow" value="115"/>
                <skill id="Crossbow" value="65"/>
                <skill id="Throwing" value="155"/>
                <skill id="Riding" value="55"/>
                <skill id="Athletics" value="215"/>
                <skill id="Crafting" value="85"/>
                <skill id="Scouting" value="145"/>
                <skill id="Tactics" value="175"/>
                <skill id="Charm" value="135"/>
                <skill id="Leadership" value="115"/>
                <skill id="Trade" value="85"/>
                <skill id="Steward" value="125"/>
                <skill id="Medicine" value="95"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="0"/>
                <Trait id="Generosity" value="-1"/>
                <Trait id="Calculating" value="1"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="-1"/>
                <Trait id="Oligarchic" value="0"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_32 -->
    <xsl:template match="NPCCharacter[@id='lord_1_32']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_1_32_name}Eldra Boarsong</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.3278" build="0.5208" key="0018700FC52C300A287C737B6258466A7B9869A85BB485996716C57DCCA973BB0078560307993A5700000000000000000000000000000000000000002EDC20C3"/>
            </face>
            <skills>
                <skill id="OneHanded" value="230"/>
                <skill id="TwoHanded" value="165"/>
                <skill id="Polearm" value="135"/>
                <skill id="Bow" value="115"/>
                <skill id="Crossbow" value="65"/>
                <skill id="Throwing" value="155"/>
                <skill id="Riding" value="55"/>
                <skill id="Athletics" value="215"/>
                <skill id="Crafting" value="85"/>
                <skill id="Scouting" value="145"/>
                <skill id="Tactics" value="170"/>
                <skill id="Charm" value="135"/>
                <skill id="Leadership" value="160"/>
                <skill id="Trade" value="85"/>
                <skill id="Steward" value="125"/>
                <skill id="Medicine" value="95"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="0"/>
                <Trait id="Generosity" value="-1"/>
                <Trait id="Calculating" value="1"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="-1"/>
                <Trait id="Oligarchic" value="0"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_33 -->
    <xsl:template match="NPCCharacter[@id='lord_1_33']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_1_33_name}Brigid the Howling</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.2" build="0.3" key="0005640B4000000287B86998A4156688BC87B56726542344956C8B7576556786000350040397A6680000000000000FFE00000000000000000000000000F00000"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="Politician" value="10"/>
                <Trait id="Manager" value="2"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Honor" value="-1"/>
                <Trait id="Calculating" value="1"/>
                <Trait id="Generosity" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_422 -->
    <xsl:template match="NPCCharacter[@id='lord_1_422']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_1_422_name}Drengulf Irontusk</xsl:attribute>
            <xsl:attribute name="default_group">HorseArcher</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.3278" build="0.5208" key="0018900FC42C0A00277D1767865C6D65C99ABB9D9A854973C599D87C96798E5B00785603076C374A00000000000000000000000000000000000000002EDC7102"/>
            </face>
            <skills>
                <skill id="OneHanded" value="238"/>
                <skill id="TwoHanded" value="170"/>
                <skill id="Polearm" value="140"/>
                <skill id="Bow" value="110"/>
                <skill id="Crossbow" value="60"/>
                <skill id="Throwing" value="160"/>
                <skill id="Riding" value="50"/>
                <skill id="Athletics" value="220"/>
                <skill id="Crafting" value="90"/>
                <skill id="Scouting" value="145"/>
                <skill id="Tactics" value="170"/>
                <skill id="Charm" value="130"/>
                <skill id="Leadership" value="60"/>
                <skill id="Trade" value="80"/>
                <skill id="Steward" value="120"/>
                <skill id="Medicine" value="90"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="0"/>
                <Trait id="Generosity" value="-1"/>
                <Trait id="Calculating" value="0"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="-1"/>
                <Trait id="Oligarchic" value="0"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_34 -->
    <xsl:template match="NPCCharacter[@id='lord_1_34']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_1_34_name}Faramir</xsl:attribute>
            <xsl:attribute name="default_group">Ranged</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0" build="0.5671" key="0000000400000182107973B1A81103B300A189083A697600991871193FB17D5300990603093A2B0532430001330223550000000000001977000000004D246002"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="HorseArcherFightingSkills" value="7"/>
                <Trait id="Commander" value="5"/>
                <Trait id="Politician" value="4"/>
                <Trait id="Manager" value="2"/>
                <Trait id="Valor" value="1"/>
                <Trait id="Generosity" value="2"/>
                <Trait id="Mercy" value="1"/>
                <Trait id="Honor" value="2"/>
                <Trait id="Calculating" value="0"/>
                <Trait id="Egalitarian" value="1"/>
                <Trait id="Oligarchic" value="0"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_36 -->
    <xsl:template match="NPCCharacter[@id='lord_1_36']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_1_36_name}Phaea</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5664" build="0.7577" key="0011900A12142006A87321448876B529E920889893905436D8C49E55747A584504BB86450B3756930000000000000000000000000000000000000000716C3041"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="HorseArcherFightingSkills" value="5"/>
                <Trait id="Commander" value="2"/>
                <Trait id="Politician" value="8"/>
                <Trait id="Manager" value="10"/>
                <Trait id="Honor" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_37 -->
    <xsl:template match="NPCCharacter[@id='lord_1_37']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_1_37_name}Goshank</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.239" build="0.7027" key="001BF40E40FC10076C63396B58A46C7159DA89AADD789458B79BDE777836067600AB47030A8BE39B000000000000000000000000000000000000000001581180"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="CavalryFightingSkills" value="6"/>
                <Trait id="Commander" value="10"/>
                <Trait id="Politician" value="2"/>
                <Trait id="Manager" value="4"/>
                <Trait id="Calculating" value="-1"/>
                <Trait id="Generosity" value="-1"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Valor" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_38 -->
    <xsl:template match="NPCCharacter[@id='lord_1_38']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_1_38_name}NazgÃ»l, the Undying</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.0116" build="1" key="0039F00FC000300010000010010100000001000000000100001011101000000000FF06030F000100000000000000000000000000000000000000000043F44142"/>
            </face>
            <skills>
                <skill id="OneHanded" value="305"/>
                <skill id="TwoHanded" value="295"/>
                <skill id="Polearm" value="290"/>
                <skill id="Bow" value="200"/>
                <skill id="Crossbow" value="150"/>
                <skill id="Throwing" value="180"/>
                <skill id="Riding" value="350"/>
                <skill id="Athletics" value="309"/>
                <skill id="Crafting" value="250"/>
                <skill id="Scouting" value="300"/>
                <skill id="Tactics" value="290"/>
                <skill id="Charm" value="100"/>
                <skill id="Leadership" value="295"/>
                <skill id="Trade" value="50"/>
                <skill id="Steward" value="250"/>
                <skill id="Medicine" value="180"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="-2"/>
                <Trait id="Generosity" value="-1"/>
                <Trait id="Calculating" value="2"/>
                <Trait id="Mercy" value="-2"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="-1"/>
                <Trait id="Oligarchic" value="1"/>
                <Trait id="Authoritarian" value="2"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_39 -->
    <xsl:template match="NPCCharacter[@id='lord_1_39']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_1_39_name}Debana</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.3025" build="0.5377" key="0005F00CC02810048774371C48B5C916EA6489378A81A846D9926C347476669603A856350A065B26000000000000000000000000000000000000000061441002"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="Politician" value="6"/>
                <Trait id="Manager" value="10"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Valor" value="1"/>
                <Trait id="Honor" value="1"/>
                <Trait id="Generosity" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_41 -->
    <xsl:template match="NPCCharacter[@id='lord_1_41']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_1_41_name}Beregund Wolfborn</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.3009" build="0.9977" key="0018440543141382878F1FFF461C01FF7F018877888888888888888F7718F5F5009F560309888888000000000000000000000000000000000000000043244142"/>
            </face>
            <skills>
                <skill id="OneHanded" value="240"/>
                <skill id="TwoHanded" value="170"/>
                <skill id="Polearm" value="140"/>
                <skill id="Bow" value="120"/>
                <skill id="Crossbow" value="70"/>
                <skill id="Throwing" value="160"/>
                <skill id="Riding" value="60"/>
                <skill id="Athletics" value="210"/>
                <skill id="Crafting" value="80"/>
                <skill id="Scouting" value="150"/>
                <skill id="Tactics" value="180"/>
                <skill id="Charm" value="140"/>
                <skill id="Leadership" value="100"/>
                <skill id="Trade" value="90"/>
                <skill id="Steward" value="130"/>
                <skill id="Medicine" value="100"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="1"/>
                <Trait id="Generosity" value="0"/>
                <Trait id="Calculating" value="0"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="-1"/>
                <Trait id="Oligarchic" value="0"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_411 -->
    <xsl:template match="NPCCharacter[@id='lord_1_411']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_1_411_name}Grimwulf Ironfang</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.3009" build="0.6157" key="000D6C0E47142902DEB8BE1AB5C571CD7F01649228AD4DB58888888F7718F5F50079560307888888000000000000000000000000000000000000000042FC0142"/>
            </face>
            <skills>
                <skill id="OneHanded" value="230"/>
                <skill id="TwoHanded" value="160"/>
                <skill id="Polearm" value="130"/>
                <skill id="Bow" value="110"/>
                <skill id="Crossbow" value="60"/>
                <skill id="Throwing" value="150"/>
                <skill id="Riding" value="50"/>
                <skill id="Athletics" value="220"/>
                <skill id="Crafting" value="90"/>
                <skill id="Scouting" value="140"/>
                <skill id="Tactics" value="170"/>
                <skill id="Charm" value="130"/>
                <skill id="Leadership" value="110"/>
                <skill id="Trade" value="80"/>
                <skill id="Steward" value="120"/>
                <skill id="Medicine" value="90"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="0"/>
                <Trait id="Generosity" value="0"/>
                <Trait id="Calculating" value="1"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="-1"/>
                <Trait id="Oligarchic" value="0"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_42 -->
    <xsl:template match="NPCCharacter[@id='lord_1_42']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_1_42_name}Hrodgar Ironhide</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.3278" build="0.6875" key="0018540FC42C345347C7A946DB43476574B24E5B263494B3C1A8D7A142978A98008A5603088816A600000000000000000000000000000000000000002F0C7002"/>
            </face>
            <skills>
                <skill id="OneHanded" value="235"/>
                <skill id="TwoHanded" value="165"/>
                <skill id="Polearm" value="135"/>
                <skill id="Bow" value="115"/>
                <skill id="Crossbow" value="65"/>
                <skill id="Throwing" value="155"/>
                <skill id="Riding" value="55"/>
                <skill id="Athletics" value="215"/>
                <skill id="Crafting" value="85"/>
                <skill id="Scouting" value="145"/>
                <skill id="Tactics" value="175"/>
                <skill id="Charm" value="135"/>
                <skill id="Leadership" value="165"/>
                <skill id="Trade" value="85"/>
                <skill id="Steward" value="125"/>
                <skill id="Medicine" value="95"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="0"/>
                <Trait id="Generosity" value="-1"/>
                <Trait id="Calculating" value="0"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="-1"/>
                <Trait id="Oligarchic" value="0"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_43 -->
    <xsl:template match="NPCCharacter[@id='lord_1_43']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_1_43_name}Gormund Oxflank</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.4676" build="0.4745" key="000A9006C744199A9A99991D9C769875AD89C74564583655C8A8A9DD949AB9CA0087760308C46DA800000000000000000000000000000000000000002EFC5002"/>
            </face>
            <skills>
                <skill id="OneHanded" value="238"/>
                <skill id="TwoHanded" value="165"/>
                <skill id="Polearm" value="135"/>
                <skill id="Bow" value="115"/>
                <skill id="Crossbow" value="65"/>
                <skill id="Throwing" value="155"/>
                <skill id="Riding" value="55"/>
                <skill id="Athletics" value="220"/>
                <skill id="Crafting" value="85"/>
                <skill id="Scouting" value="145"/>
                <skill id="Tactics" value="170"/>
                <skill id="Charm" value="135"/>
                <skill id="Leadership" value="60"/>
                <skill id="Trade" value="85"/>
                <skill id="Steward" value="125"/>
                <skill id="Medicine" value="95"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="0"/>
                <Trait id="Generosity" value="-1"/>
                <Trait id="Calculating" value="0"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="-1"/>
                <Trait id="Oligarchic" value="0"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_44 -->
    <xsl:template match="NPCCharacter[@id='lord_1_44']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_1_44_name}Nemos</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="000BCC0CC0004008776C57C967305779596A85AB79877778859898977677345B0000000507A9688700001105000030A30000001F000000000000000000000003"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="BalancedFightingSkills" value="7"/>
                <Trait id="Commander" value="4"/>
                <Trait id="Politician" value="8"/>
                <Trait id="Manager" value="2"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Honor" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_47 -->
    <xsl:template match="NPCCharacter[@id='lord_1_47']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_1_47_name}Ulbos</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0011B40597BC038E94A8C26585AAA93992D5A78776779C82329B657437695D35000005A80DE9BA6200001105000030A30000001F000000000000000000000000"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="KnightFightingSkills" value="6"/>
                <Trait id="Commander" value="4"/>
                <Trait id="Politician" value="8"/>
                <Trait id="Manager" value="8"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Honor" value="-1"/>
                <Trait id="Generosity" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_49 -->
    <xsl:template match="NPCCharacter[@id='lord_1_49']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_1_49_name}Obron</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0011BC0CCE000102A5508899487841954B4F9A57D5C7D811C889B5068C4A7666012776130268769500000000000000000000000000000000000000003C4480C2"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="KnightFightingSkills" value="2"/>
                <Trait id="Commander" value="6"/>
                <Trait id="Politician" value="6"/>
                <Trait id="Manager" value="8"/>
                <Trait id="Valor" value="-1"/>
                <Trait id="Honor" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_20 -->
    <xsl:template match="NPCCharacter[@id='lord_1_20']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_1_20_name}Astrid Bearclaw</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.3171" build="0.331" key="0008600D81FC3001AB6396829678635A9E988688E7437D4C394454A8DC55696B00B556030B73DC76000000000000000000000000000000000000000043742083"/>
            </face>
            <skills>
                <skill id="OneHanded" value="150"/>
                <skill id="TwoHanded" value="260"/>
                <skill id="Polearm" value="120"/>
                <skill id="Bow" value="100"/>
                <skill id="Crossbow" value="60"/>
                <skill id="Throwing" value="130"/>
                <skill id="Riding" value="80"/>
                <skill id="Athletics" value="240"/>
                <skill id="Crafting" value="110"/>
                <skill id="Scouting" value="160"/>
                <skill id="Tactics" value="220"/>
                <skill id="Charm" value="200"/>
                <skill id="Leadership" value="150"/>
                <skill id="Trade" value="110"/>
                <skill id="Steward" value="210"/>
                <skill id="Medicine" value="100"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="1"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Calculating" value="0"/>
                <Trait id="Mercy" value="0"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="0"/>
                <Trait id="Oligarchic" value="1"/>
                <Trait id="Authoritarian" value="2"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_64 -->
    <xsl:template match="NPCCharacter[@id='lord_1_64']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_1_64_name}Thormund Grizzlyhew</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.3171" build="0.2245" key="0008E00FCDFC1A547589847271756C397A5E664B96794B555718B95CE9C357830083560308938B28000000000000000000000000000000000000000043044142"/>
            </face>
            <skills>
                <skill id="OneHanded" value="135"/>
                <skill id="TwoHanded" value="235"/>
                <skill id="Polearm" value="115"/>
                <skill id="Bow" value="100"/>
                <skill id="Crossbow" value="60"/>
                <skill id="Throwing" value="125"/>
                <skill id="Riding" value="55"/>
                <skill id="Athletics" value="220"/>
                <skill id="Crafting" value="90"/>
                <skill id="Scouting" value="145"/>
                <skill id="Tactics" value="175"/>
                <skill id="Charm" value="135"/>
                <skill id="Leadership" value="60"/>
                <skill id="Trade" value="85"/>
                <skill id="Steward" value="125"/>
                <skill id="Medicine" value="95"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="0"/>
                <Trait id="Generosity" value="0"/>
                <Trait id="Calculating" value="0"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="0"/>
                <Trait id="Oligarchic" value="0"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_50 -->
    <xsl:template match="NPCCharacter[@id='lord_1_50']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_1_50_name}Corwyn Raveneye</xsl:attribute>
            <xsl:attribute name="default_group">Ranged</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.2685" build="0.2662" key="00087C078158090285773B6C97789A495A4598548A747A346A86816BA837976B0074460307586266000000000000000000000000000000000000000042E41082"/>
            </face>
            <skills>
                <skill id="OneHanded" value="120"/>
                <skill id="TwoHanded" value="110"/>
                <skill id="Polearm" value="100"/>
                <skill id="Bow" value="240"/>
                <skill id="Crossbow" value="80"/>
                <skill id="Throwing" value="130"/>
                <skill id="Riding" value="50"/>
                <skill id="Athletics" value="210"/>
                <skill id="Crafting" value="85"/>
                <skill id="Scouting" value="160"/>
                <skill id="Tactics" value="170"/>
                <skill id="Charm" value="135"/>
                <skill id="Leadership" value="60"/>
                <skill id="Trade" value="85"/>
                <skill id="Steward" value="125"/>
                <skill id="Medicine" value="95"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="0"/>
                <Trait id="Generosity" value="-1"/>
                <Trait id="Calculating" value="1"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="-1"/>
                <Trait id="Oligarchic" value="0"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_66 -->
    <xsl:template match="NPCCharacter[@id='lord_1_66']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_1_66_name}Talric Crowcall</xsl:attribute>
            <xsl:attribute name="default_group">Ranged</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.2685" build="0.2662" key="00001007835813922BC272AA728143CDC46386D689785734584896A7987613AC00744603078AC199000000000000000000000000000000000000000042E41082"/>
            </face>
            <skills>
                <skill id="OneHanded" value="120"/>
                <skill id="TwoHanded" value="110"/>
                <skill id="Polearm" value="100"/>
                <skill id="Bow" value="230"/>
                <skill id="Crossbow" value="80"/>
                <skill id="Throwing" value="130"/>
                <skill id="Riding" value="50"/>
                <skill id="Athletics" value="210"/>
                <skill id="Crafting" value="85"/>
                <skill id="Scouting" value="150"/>
                <skill id="Tactics" value="170"/>
                <skill id="Charm" value="135"/>
                <skill id="Leadership" value="60"/>
                <skill id="Trade" value="85"/>
                <skill id="Steward" value="125"/>
                <skill id="Medicine" value="95"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="0"/>
                <Trait id="Generosity" value="-1"/>
                <Trait id="Calculating" value="1"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="-1"/>
                <Trait id="Oligarchic" value="0"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_51 -->
    <xsl:template match="NPCCharacter[@id='lord_1_51']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_1_51_name}Haldric Talonstrike</xsl:attribute>
            <xsl:attribute name="default_group">HorseArcher</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.2685" build="0.2662" key="00001004C3A013922B4A77BC993C89497B2786D689785734DA946A6D4229EA9300544603057983BB000000000000000000000000000000000000000042AC1082"/>
            </face>
            <skills>
                <skill id="OneHanded" value="120"/>
                <skill id="TwoHanded" value="100"/>
                <skill id="Polearm" value="110"/>
                <skill id="Bow" value="260"/>
                <skill id="Crossbow" value="80"/>
                <skill id="Throwing" value="130"/>
                <skill id="Riding" value="250"/>
                <skill id="Athletics" value="190"/>
                <skill id="Crafting" value="90"/>
                <skill id="Scouting" value="180"/>
                <skill id="Tactics" value="220"/>
                <skill id="Charm" value="200"/>
                <skill id="Leadership" value="50"/>
                <skill id="Trade" value="110"/>
                <skill id="Steward" value="200"/>
                <skill id="Medicine" value="100"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="1"/>
                <Trait id="Generosity" value="0"/>
                <Trait id="Calculating" value="1"/>
                <Trait id="Mercy" value="0"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="0"/>
                <Trait id="Oligarchic" value="1"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_58 -->
    <xsl:template match="NPCCharacter[@id='lord_1_58']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_1_58_name}Gorvin the Fell</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.7" build="0.8" key="0016B40CC0BC334056A4C39BA7B77CCD78918637473D7C8CD5E5A4648B7DBC3A000004730C774E6800001105000030A30000001F000000000000000000001000"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="BalancedFightingSkills" value="10"/>
                <Trait id="Commander" value="2"/>
                <Trait id="Politician" value="7"/>
                <Trait id="Manager" value="8"/>
                <Trait id="Valor" value="1"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Mercy" value="1"/>
                <Trait id="Honor" value="1"/>
                <Trait id="Calculating" value="1"/>
                <Trait id="Egalitarian" value="1"/>
                <Trait id="Oligarchic" value="0"/>
                <Trait id="Authoritarian" value="0"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_70 -->
    <xsl:template match="NPCCharacter[@id='lord_1_70']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_1_70_name}Veyra the Shadow</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.4" build="0.4" key="0000D40E4000228BED6277444258C875446A17B9ED8932923B61997568A479EB0000047303395E480000000000000FFE00000000000000000000000000004000"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="Politician" value="2"/>
                <Trait id="Manager" value="4"/>
                <Trait id="Honor" value="1"/>
                <Trait id="Generosity" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_67 -->
    <xsl:template match="NPCCharacter[@id='lord_1_67']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_1_67_name}Eorwyn Featherbolt</xsl:attribute>
            <xsl:attribute name="default_group">HorseArcher</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.2685" build="0.2662" key="00004C04C7A03782CB4C93C3247A752599557696691778A256BA99AB862778B9005446030566758B000000000000000000000000000000000000000042AC2102"/>
            </face>
            <skills>
                <skill id="OneHanded" value="110"/>
                <skill id="TwoHanded" value="90"/>
                <skill id="Polearm" value="100"/>
                <skill id="Bow" value="230"/>
                <skill id="Crossbow" value="70"/>
                <skill id="Throwing" value="120"/>
                <skill id="Riding" value="220"/>
                <skill id="Athletics" value="180"/>
                <skill id="Crafting" value="80"/>
                <skill id="Scouting" value="165"/>
                <skill id="Tactics" value="170"/>
                <skill id="Charm" value="135"/>
                <skill id="Leadership" value="60"/>
                <skill id="Trade" value="85"/>
                <skill id="Steward" value="125"/>
                <skill id="Medicine" value="95"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="0"/>
                <Trait id="Generosity" value="-1"/>
                <Trait id="Calculating" value="1"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="0"/>
                <Trait id="Oligarchic" value="0"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_40 -->
    <xsl:template match="NPCCharacter[@id='lord_1_40']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_1_40_name}Dervorin</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0" build="0.6157" key="0002000D8014168128800978B7B7C03A848782691464BBA87556440C8D964309009906030997930300000000000000000000000000000000000000003B347002"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="BalancedFightingSkills" value="4"/>
                <Trait id="Commander" value="2"/>
                <Trait id="Politician" value="10"/>
                <Trait id="Manager" value="8"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Generosity" value="2"/>
                <Trait id="Mercy" value="1"/>
                <Trait id="Honor" value="1"/>
                <Trait id="Calculating" value="0"/>
                <Trait id="Egalitarian" value="1"/>
                <Trait id="Oligarchic" value="0"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_46 -->
    <xsl:template match="NPCCharacter[@id='lord_1_46']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_1_46_name}Milos</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0016B80A00003205954C59997345585A4969B5769B85789865677B96475832690000000307A7968800001105000030A30000001F000000000000000000000000"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="BalancedFightingSkills" value="5"/>
                <Trait id="Commander" value="2"/>
                <Trait id="Politician" value="4"/>
                <Trait id="Manager" value="8"/>
                <Trait id="Mercy" value="1"/>
                <Trait id="Honor" value="1"/>
                <Trait id="Calculating" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_45 -->
    <xsl:template match="NPCCharacter[@id='lord_1_45']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_1_45_name}Forlong, Lord of Lossarnach</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.9769" build="0.9861" key="0005D40D80001A4304700800605DF10950388DB2BFEA1D017917FB0F8B00683B007FF60307911FFF00000000000000000000000000000000000000003CFC9002"/>
            </face>
            <skills>
                <skill id="OneHanded" value="230"/>
                <skill id="TwoHanded" value="210"/>
                <skill id="Polearm" value="200"/>
                <skill id="Bow" value="150"/>
                <skill id="Crossbow" value="100"/>
                <skill id="Throwing" value="120"/>
                <skill id="Riding" value="150"/>
                <skill id="Athletics" value="180"/>
                <skill id="Crafting" value="50"/>
                <skill id="Scouting" value="150"/>
                <skill id="Tactics" value="200"/>
                <skill id="Charm" value="180"/>
                <skill id="Leadership" value="230"/>
                <skill id="Trade" value="100"/>
                <skill id="Steward" value="200"/>
                <skill id="Medicine" value="80"/>
                <skill id="Engineering" value="80"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="2"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Calculating" value="0"/>
                <Trait id="Mercy" value="1"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="0"/>
                <Trait id="Oligarchic" value="1"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_57 -->
    <xsl:template match="NPCCharacter[@id='lord_1_57']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_1_57_name}Altenos</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0016D40E407C334E269C3C75A7984761A6AE8C7B19959CAB9CB685D9565BB66A00058BB2057385AB00001105000030A30000001F000000000000000001409000"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="BalancedFightingSkills" value="7"/>
                <Trait id="Commander" value="4"/>
                <Trait id="Politician" value="6"/>
                <Trait id="Manager" value="2"/>
                <Trait id="Valor" value="1"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Egalitarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_52 -->
    <xsl:template match="NPCCharacter[@id='lord_1_52']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_1_52_name}Hirluin, Lord of Pinnath Gelin</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.2176" build="0.4699" key="000594041214000E318040336E417501E7F0868F09990035BB7979087CF51E0A00873603080BB69F1F6410605020004000000000000017670000000043144003"/>
            </face>
            <skills>
                <skill id="OneHanded" value="230"/>
                <skill id="TwoHanded" value="210"/>
                <skill id="Polearm" value="200"/>
                <skill id="Bow" value="150"/>
                <skill id="Crossbow" value="100"/>
                <skill id="Throwing" value="120"/>
                <skill id="Riding" value="150"/>
                <skill id="Athletics" value="180"/>
                <skill id="Crafting" value="50"/>
                <skill id="Scouting" value="150"/>
                <skill id="Tactics" value="200"/>
                <skill id="Charm" value="180"/>
                <skill id="Leadership" value="230"/>
                <skill id="Trade" value="100"/>
                <skill id="Steward" value="200"/>
                <skill id="Medicine" value="80"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="2"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Calculating" value="0"/>
                <Trait id="Mercy" value="1"/>
                <Trait id="Valor" value="2"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_53 -->
    <xsl:template match="NPCCharacter[@id='lord_1_53']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_1_53_name}Angbor, Lord of Lamedon</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0" build="0.7037" key="000A140D801439C2A880010FB2807808943182694044AB107776900C8D96EA0900AB06030A9D9F080000000000000000000000000000000000000000194C9002"/>
            </face>
            <skills>
                <skill id="OneHanded" value="220"/>
                <skill id="TwoHanded" value="200"/>
                <skill id="Polearm" value="210"/>
                <skill id="Bow" value="150"/>
                <skill id="Crossbow" value="100"/>
                <skill id="Throwing" value="120"/>
                <skill id="Riding" value="150"/>
                <skill id="Athletics" value="180"/>
                <skill id="Crafting" value="50"/>
                <skill id="Scouting" value="150"/>
                <skill id="Tactics" value="200"/>
                <skill id="Charm" value="180"/>
                <skill id="Leadership" value="230"/>
                <skill id="Trade" value="100"/>
                <skill id="Steward" value="200"/>
                <skill id="Medicine" value="80"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="2"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Calculating" value="0"/>
                <Trait id="Mercy" value="1"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="0"/>
                <Trait id="Oligarchic" value="1"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_71 -->
    <xsl:template match="NPCCharacter[@id='lord_1_71']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_1_71_name}Golasgil, Lord of Anfalas</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0" build="0.7037" key="000DD00C4014168270800108B005450CDBA588004F440B0579A6906C8DE11697008B06030894B4050000000000000000000000000000000000000000190C4002"/>
            </face>
            <skills>
                <skill id="OneHanded" value="220"/>
                <skill id="TwoHanded" value="200"/>
                <skill id="Polearm" value="210"/>
                <skill id="Bow" value="150"/>
                <skill id="Crossbow" value="100"/>
                <skill id="Throwing" value="120"/>
                <skill id="Riding" value="150"/>
                <skill id="Athletics" value="180"/>
                <skill id="Crafting" value="50"/>
                <skill id="Scouting" value="150"/>
                <skill id="Tactics" value="200"/>
                <skill id="Charm" value="180"/>
                <skill id="Leadership" value="230"/>
                <skill id="Trade" value="100"/>
                <skill id="Steward" value="200"/>
                <skill id="Medicine" value="80"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="2"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Calculating" value="0"/>
                <Trait id="Mercy" value="1"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="0"/>
                <Trait id="Oligarchic" value="1"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_73 -->
    <xsl:template match="NPCCharacter[@id='lord_1_73']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_1_73_name}Ovagos</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0011B40B403C10088733440E80468F1FFF4F9274C862B448941B5637963E5D3E000008840B7247A6000000000000000000000000000000000000000000003101"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="BalancedFightingSkills" value="5"/>
                <Trait id="Commander" value="2"/>
                <Trait id="Politician" value="4"/>
                <Trait id="Manager" value="2"/>
                <Trait id="Honor" value="-1"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Calculating" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_62 -->
    <xsl:template match="NPCCharacter[@id='lord_1_62']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_1_62_name}Sejaron</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.8" key="000BB00E400003884897D665A3CD435C87D42767B77897C8637A678695873A870005D4B2077B64B500001105000030A30000001F000000000000000000281000"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="BalancedFightingSkills" value="7"/>
                <Trait id="Commander" value="4"/>
                <Trait id="Politician" value="6"/>
                <Trait id="Manager" value="2"/>
                <Trait id="Valor" value="1"/>
                <Trait id="Calculating" value="1"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Honor" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_30 -->
    <xsl:template match="NPCCharacter[@id='lord_1_30']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_1_30_name}Gothmog, Lieutenant of Morgul</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.1" build="0.95" key="0016C80E0000200DBC4597A5256457A995879939BBECA7A766C7893AA68945570005435309B1876400001105000030A30000001F000000000000000000A02000"/>
            </face>
            <skills>
                <skill id="OneHanded" value="280"/>
                <skill id="TwoHanded" value="260"/>
                <skill id="Polearm" value="270"/>
                <skill id="Bow" value="120"/>
                <skill id="Crossbow" value="100"/>
                <skill id="Throwing" value="130"/>
                <skill id="Riding" value="200"/>
                <skill id="Athletics" value="220"/>
                <skill id="Crafting" value="50"/>
                <skill id="Scouting" value="150"/>
                <skill id="Tactics" value="280"/>
                <skill id="Charm" value="80"/>
                <skill id="Leadership" value="250"/>
                <skill id="Trade" value="50"/>
                <skill id="Steward" value="200"/>
                <skill id="Medicine" value="60"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="-2"/>
                <Trait id="Generosity" value="-1"/>
                <Trait id="Calculating" value="1"/>
                <Trait id="Mercy" value="-2"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="-1"/>
                <Trait id="Oligarchic" value="1"/>
                <Trait id="Authoritarian" value="2"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_48 -->
    <xsl:template match="NPCCharacter[@id='lord_1_48']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_1_48_name}KhamÃ»l, the Easterling</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.0116" build="1" key="0039F00FC000300010000010010100000001000000000100001011101000000000FF06030F000100000000000000000000000000000000000000000043F44142"/>
            </face>
            <skills>
                <skill id="OneHanded" value="320"/>
                <skill id="TwoHanded" value="320"/>
                <skill id="Polearm" value="320"/>
                <skill id="Bow" value="280"/>
                <skill id="Crossbow" value="120"/>
                <skill id="Throwing" value="360"/>
                <skill id="Riding" value="320"/>
                <skill id="Athletics" value="305"/>
                <skill id="Crafting" value="450"/>
                <skill id="Scouting" value="480"/>
                <skill id="Tactics" value="420"/>
                <skill id="Charm" value="90"/>
                <skill id="Leadership" value="440"/>
                <skill id="Trade" value="450"/>
                <skill id="Steward" value="630"/>
                <skill id="Medicine" value="470"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="-2"/>
                <Trait id="Generosity" value="-1"/>
                <Trait id="Calculating" value="2"/>
                <Trait id="Mercy" value="-2"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="-1"/>
                <Trait id="Oligarchic" value="1"/>
                <Trait id="Authoritarian" value="2"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_54 -->
    <xsl:template match="NPCCharacter[@id='lord_1_54']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_1_54_name}Shagrat, Captain of Cirith Ungol</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.2" build="0.95" key="0005A00140BC100DA85B899C8A71E497A32461B62479196C55058E7245674416000506530C356542000000000000000000000000000000000000000000004180"/>
            </face>
            <skills>
                <skill id="OneHanded" value="240"/>
                <skill id="TwoHanded" value="230"/>
                <skill id="Polearm" value="260"/>
                <skill id="Bow" value="200"/>
                <skill id="Crossbow" value="150"/>
                <skill id="Throwing" value="180"/>
                <skill id="Riding" value="250"/>
                <skill id="Athletics" value="290"/>
                <skill id="Crafting" value="250"/>
                <skill id="Scouting" value="300"/>
                <skill id="Tactics" value="290"/>
                <skill id="Charm" value="100"/>
                <skill id="Leadership" value="250"/>
                <skill id="Trade" value="50"/>
                <skill id="Steward" value="250"/>
                <skill id="Medicine" value="180"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="-2"/>
                <Trait id="Generosity" value="-1"/>
                <Trait id="Calculating" value="2"/>
                <Trait id="Mercy" value="-2"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="-1"/>
                <Trait id="Oligarchic" value="1"/>
                <Trait id="Authoritarian" value="2"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_55 -->
    <xsl:template match="NPCCharacter[@id='lord_1_55']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_1_55_name}Mathmog</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.3" build="0.85" key="0011BC0CCE000102A5508899487841954B4F9A57D5C7D811C889B5068C4A7666012776130268769500000000000000000000000000000000000000003C4480C2"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="CavalryFightingSkills" value="8"/>
                <Trait id="Commander" value="7"/>
                <Trait id="Manager" value="2"/>
                <Trait id="Politician" value="5"/>
                <Trait id="Valor" value="1"/>
                <Trait id="Generosity" value="-2"/>
                <Trait id="Mercy" value="-2"/>
                <Trait id="Honor" value="-1"/>
                <Trait id="Calculating" value="2"/>
                <Trait id="Egalitarian" value="-2"/>
                <Trait id="Oligarchic" value="2"/>
                <Trait id="Authoritarian" value="2"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_63 -->
    <xsl:template match="NPCCharacter[@id='lord_1_63']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_1_63_name}Gorbag, Captain of Minas Morgul</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.2" build="0.9" key="00065C08807C000D88566B74DA8577A325C64A9B6AA35AD26A5B2DB39752565500065BA20657885E00001105000030A30000001F000000000000000000F03000"/>
            </face>
            <skills>
                <skill id="OneHanded" value="240"/>
                <skill id="TwoHanded" value="230"/>
                <skill id="Polearm" value="260"/>
                <skill id="Bow" value="200"/>
                <skill id="Crossbow" value="150"/>
                <skill id="Throwing" value="180"/>
                <skill id="Riding" value="250"/>
                <skill id="Athletics" value="290"/>
                <skill id="Crafting" value="250"/>
                <skill id="Scouting" value="300"/>
                <skill id="Tactics" value="290"/>
                <skill id="Charm" value="100"/>
                <skill id="Leadership" value="250"/>
                <skill id="Trade" value="50"/>
                <skill id="Steward" value="250"/>
                <skill id="Medicine" value="180"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="-2"/>
                <Trait id="Generosity" value="-1"/>
                <Trait id="Calculating" value="2"/>
                <Trait id="Mercy" value="-2"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="-1"/>
                <Trait id="Oligarchic" value="1"/>
                <Trait id="Authoritarian" value="2"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_56 -->
    <xsl:template match="NPCCharacter[@id='lord_1_56']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_1_56_name}Tormund, the Hammer</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.4" build="0.8" key="0005C8058000100E63727AC8926A687A1A85878578987C9B437E6347545ABCB7000C0892057768C900001105000030A30000001F000000000000000000004000"/>
            </face>
            <skills>
                <skill id="OneHanded" value="180"/>
                <skill id="TwoHanded" value="190"/>
                <skill id="Polearm" value="260"/>
                <skill id="Bow" value="110"/>
                <skill id="Crossbow" value="70"/>
                <skill id="Throwing" value="130"/>
                <skill id="Riding" value="250"/>
                <skill id="Athletics" value="200"/>
                <skill id="Crafting" value="90"/>
                <skill id="Scouting" value="160"/>
                <skill id="Tactics" value="220"/>
                <skill id="Charm" value="200"/>
                <skill id="Leadership" value="100"/>
                <skill id="Trade" value="110"/>
                <skill id="Steward" value="210"/>
                <skill id="Medicine" value="100"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="1"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Calculating" value="1"/>
                <Trait id="Mercy" value="0"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="0"/>
                <Trait id="Oligarchic" value="1"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_68 -->
    <xsl:template match="NPCCharacter[@id='lord_1_68']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_1_68_name}Tharos</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.7" build="0.9" key="000BB00598FC00059A75657577A39A87B3D89A8E9774BD7677A867A97D3A634D00060C520767C68700001105000030A30000001F000000000000000000287000"/>
            </face>
            <skills>
                <skill id="OneHanded" value="240"/>
                <skill id="TwoHanded" value="230"/>
                <skill id="Polearm" value="260"/>
                <skill id="Bow" value="200"/>
                <skill id="Crossbow" value="150"/>
                <skill id="Throwing" value="180"/>
                <skill id="Riding" value="250"/>
                <skill id="Athletics" value="290"/>
                <skill id="Crafting" value="250"/>
                <skill id="Scouting" value="300"/>
                <skill id="Tactics" value="290"/>
                <skill id="Charm" value="100"/>
                <skill id="Leadership" value="280"/>
                <skill id="Trade" value="50"/>
                <skill id="Steward" value="250"/>
                <skill id="Medicine" value="180"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="-2"/>
                <Trait id="Generosity" value="-1"/>
                <Trait id="Calculating" value="2"/>
                <Trait id="Mercy" value="-2"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="-1"/>
                <Trait id="Oligarchic" value="1"/>
                <Trait id="Authoritarian" value="2"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_69 -->
    <xsl:template match="NPCCharacter[@id='lord_1_69']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_1_69_name}Niphon</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.3" build="0.4" key="0016C40E403C1408E436C9A4849415C6909E955997C1BD57CB837C9596548C15000E4B820E71DA76000000000000000000000000000000000000000000003141"/>
            </face>
            <skills>
                <skill id="OneHanded" value="240"/>
                <skill id="TwoHanded" value="230"/>
                <skill id="Polearm" value="260"/>
                <skill id="Bow" value="200"/>
                <skill id="Crossbow" value="150"/>
                <skill id="Throwing" value="180"/>
                <skill id="Riding" value="250"/>
                <skill id="Athletics" value="290"/>
                <skill id="Crafting" value="250"/>
                <skill id="Scouting" value="300"/>
                <skill id="Tactics" value="290"/>
                <skill id="Charm" value="100"/>
                <skill id="Leadership" value="280"/>
                <skill id="Trade" value="50"/>
                <skill id="Steward" value="250"/>
                <skill id="Medicine" value="180"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="-2"/>
                <Trait id="Generosity" value="-1"/>
                <Trait id="Calculating" value="2"/>
                <Trait id="Mercy" value="-2"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="-1"/>
                <Trait id="Oligarchic" value="1"/>
                <Trait id="Authoritarian" value="2"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_72 -->
    <xsl:template match="NPCCharacter[@id='lord_1_72']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_1_72_name}Bolg</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.1" build="0.99" key="0011B40B402410088733440E80468F1FFF4F9274C862B448941B5637963E5D3E000E08830B7247A6000000000000000000000000000000000000000000003101"/>
            </face>
            <skills>
                <skill id="OneHanded" value="280"/>
                <skill id="TwoHanded" value="280"/>
                <skill id="Polearm" value="280"/>
                <skill id="Bow" value="250"/>
                <skill id="Crossbow" value="150"/>
                <skill id="Throwing" value="180"/>
                <skill id="Riding" value="250"/>
                <skill id="Athletics" value="300"/>
                <skill id="Crafting" value="250"/>
                <skill id="Scouting" value="300"/>
                <skill id="Tactics" value="290"/>
                <skill id="Charm" value="100"/>
                <skill id="Leadership" value="280"/>
                <skill id="Trade" value="50"/>
                <skill id="Steward" value="250"/>
                <skill id="Medicine" value="180"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="-2"/>
                <Trait id="Generosity" value="-1"/>
                <Trait id="Calculating" value="2"/>
                <Trait id="Mercy" value="-2"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="-1"/>
                <Trait id="Oligarchic" value="1"/>
                <Trait id="Authoritarian" value="2"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_1_74 -->
    <xsl:template match="NPCCharacter[@id='lord_1_74']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_1_74_name}Zachanis</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0011B40B402410088733440E80468F1FFF4F9274C862B448941B5637963E5D3E000E08840B7247A6000000000000000000000000000000000000000000003101"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="BalancedFightingSkills" value="5"/>
                <Trait id="Commander" value="2"/>
                <Trait id="Politician" value="4"/>
                <Trait id="Manager" value="2"/>
                <Trait id="Honor" value="-1"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Calculating" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>


    <!-- ============================================== -->
    <!-- Faction 2 - Sturgia/Misty Mountains -->
    <!-- ============================================== -->

    <!-- lord_2_1 -->
    <xsl:template match="NPCCharacter[@id='lord_2_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_2_1_name}Bard II</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.2" build="0.9" key="0000500140BC09429E895C86B34CA38993A369B8B287576979538AC46B678B940177781307C60384000000000000000000000000000000000000000000F05180"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="HuscarlFightingSkills" value="10"/>
                <Trait id="Commander" value="8"/>
                <Trait id="Manager" value="5"/>
                <Trait id="Politician" value="4"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Mercy" value="0"/>
                <Trait id="Honor" value="2"/>
                <Trait id="Calculating" value="0"/>
                <Trait id="Egalitarian" value="0"/>
                <Trait id="Oligarchic" value="1"/>
                <Trait id="Authoritarian" value="2"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_2_2 -->
    <xsl:template match="NPCCharacter[@id='lord_2_2']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_2_2_name}Asta</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.4526" build="0.5473" key="000A08044070300E4976245366B465B59A4B8308619547498764AA1275284A240898768909675A3A00000000000000000000000000000000000000006F3C2084"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="Politician" value="4"/>
                <Trait id="Manager" value="4"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Honor" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_2_3 -->
    <xsl:template match="NPCCharacter[@id='lord_2_3']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_2_3_name}Thorin III Stonehelm</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.3" build="0.9" key="00002C04405838836569E86B42547942A953B158E6A5883394A97756BC9B75A90197701309CE53B8000000000000000000000000000000000000000025283042"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="HuscarlFightingSkills" value="8"/>
                <Trait id="Commander" value="5"/>
                <Trait id="Manager" value="2"/>
                <Trait id="Politician" value="10"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Mercy" value="0"/>
                <Trait id="Honor" value="1"/>
                <Trait id="Calculating" value="0"/>
                <Trait id="Egalitarian" value="1"/>
                <Trait id="Oligarchic" value="0"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_2_4 -->
    <xsl:template match="NPCCharacter[@id='lord_2_4']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_2_4_name}Siga</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.8" key="00001C04407018C18873324163A357B4966D873971979A028B644405773A5B2401B776130B8B799C0000000000000000000000000000000000000000797C1081"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="PeltastFightingSkills" value="9"/>
                <Trait id="Commander" value="2"/>
                <Trait id="Politician" value="4"/>
                <Trait id="Manager" value="6"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Honor" value="1"/>
                <Trait id="Valor" value="1"/>
                <Trait id="Generosity" value="-1"/>
                <Trait id="Calculating" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_2_5 -->
    <xsl:template match="NPCCharacter[@id='lord_2_5']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_2_5_name}Ori</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.7289" build="0.7611" key="0000300140001384967132012584CCB36EC472158520143875848D198317281906BBB0680B68CB80000000000000000000000000000000000000000073788003"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="HopliteFightingSkills" value="8"/>
                <Trait id="Commander" value="2"/>
                <Trait id="Manager" value="10"/>
                <Trait id="Politician" value="8"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Mercy" value="0"/>
                <Trait id="Honor" value="1"/>
                <Trait id="Calculating" value="1"/>
                <Trait id="Egalitarian" value="1"/>
                <Trait id="Oligarchic" value="0"/>
                <Trait id="Authoritarian" value="0"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_2_6 -->
    <xsl:template match="NPCCharacter[@id='lord_2_6']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_2_6_name}Erta</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.6953" build="0.6326" key="00050405007020075B7445315583A1B9864D8716668A93288847E81474585B540589A6570833596C0000000000000000000000000000000000000000470420C1"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="Politician" value="10"/>
                <Trait id="Manager" value="4"/>
                <Trait id="Generosity" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_2_7 -->
    <xsl:template match="NPCCharacter[@id='lord_2_7']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_2_7_name}Simir</xsl:attribute>
            <xsl:attribute name="default_group">HorseArcher</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5176" build="0.6044" key="0000500440B407823E895C86B34CA38993A369B8B280570970538AC46B6781990079880307C60384000000000000000000000000000000000000000000F05000"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="HopliteFightingSkills" value="9"/>
                <Trait id="Commander" value="10"/>
                <Trait id="Politician" value="8"/>
                <Trait id="Manager" value="6"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Generosity" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_2_8 -->
    <xsl:template match="NPCCharacter[@id='lord_2_8']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_2_8_name}Urik</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.3353" build="0.6434" key="000074044B5834D49660886B42587948A153C21585A1143894A97756BC9575A900AA50030ACE53B8000000000000000000000000000000000000000059603042"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="HuscarlFightingSkills" value="7"/>
                <Trait id="Commander" value="4"/>
                <Trait id="Politician" value="4"/>
                <Trait id="Manager" value="10"/>
                <Trait id="Valor" value="1"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Honor" value="-1"/>
                <Trait id="Calculating" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_2_9 -->
    <xsl:template match="NPCCharacter[@id='lord_2_9']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_2_9_name}Lek</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.7176" build="0.8336" key="0000180000000344BC7048192589DD69784A747472708A0097A86A068116A60B00EDB0030E7AC393000000000000000000000000000000000000000053E02002"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="PeltastFightingSkills" value="5"/>
                <Trait id="Commander" value="4"/>
                <Trait id="Politician" value="2"/>
                <Trait id="Manager" value="6"/>
                <Trait id="Valor" value="1"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Calculating" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_2_10 -->
    <xsl:template match="NPCCharacter[@id='lord_2_10']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_2_10_name}Valla</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.2" build="0.4" key="0005AC0B4070015217A823EA6364A9577C7BCBE6B481144EA0B835CE9AE9AAC701B009140BA86C75000000000000000000000000000000000000000001683080"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="Politician" value="8"/>
                <Trait id="Manager" value="8"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Generosity" value="-1"/>
                <Trait id="Calculating" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_2_11 -->
    <xsl:template match="NPCCharacter[@id='lord_2_11']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_2_11_name}Idrun</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.4213" build="0.4275" key="0005140012B4300F2C654B99BBA57D7B9785373999A651AA78C8CCEC77955C9602C666240CE70A590000000000000000000000000000000000000000538C3041"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="Politician" value="6"/>
                <Trait id="Manager" value="10"/>
                <Trait id="Mercy" value="1"/>
                <Trait id="Generosity" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_2_111 -->
    <xsl:template match="NPCCharacter[@id='lord_2_111']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_2_111_name}Rozhivol</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.3353" build="0.6434" key="0000BC04405830855960886B42587948A1E37B8355E33A7994A97756BC9575A900AA52030A0E53B8000000000000000000000000000000000000000067603042"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="HuscarlFightingSkills" value="5"/>
                <Trait id="Commander" value="2"/>
                <Trait id="Politician" value="2"/>
                <Trait id="Manager" value="2"/>
                <Trait id="Valor" value="1"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Calculating" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_2_12 -->
    <xsl:template match="NPCCharacter[@id='lord_2_12']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_2_12_name}Svana</xsl:attribute>
            <xsl:attribute name="default_group">HorseArcher</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.3" build="0.554" key="00001C04528830021B7213026674749A968B7726758543278B44F71278394B350CD8A6C20D4B1750000000000000000000000000000000000000000077BC10C2"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="BalancedFightingSkills" value="6"/>
                <Trait id="Commander" value="10"/>
                <Trait id="Politician" value="8"/>
                <Trait id="Manager" value="10"/>
                <Trait id="Valor" value="1"/>
                <Trait id="Honor" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_2_121 -->
    <xsl:template match="NPCCharacter[@id='lord_2_121']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_2_121_name}Osven</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.7176" build="0.8336" key="000518000000024217704A092588FD5A701E74747250801097A86A06811A760B00EDB0030E7AC993000000000000000000000000000000000000000053E02002"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="BalancedFightingSkills" value="5"/>
                <Trait id="Commander" value="6"/>
                <Trait id="Politician" value="4"/>
                <Trait id="Manager" value="4"/>
                <Trait id="Valor" value="1"/>
                <Trait id="Honor" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_2_13 -->
    <xsl:template match="NPCCharacter[@id='lord_2_13']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_2_13_name}Vidar</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.4221" build="0.7302" key="00005802C000354A7788740C5598FA3C58DD3180B090DA078E6679877C84AFA801BB60130BCB1AF0000000000000000000000000000000000000000073789003"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="BalancedFightingSkills" value="7"/>
                <Trait id="Commander" value="8"/>
                <Trait id="Politician" value="4"/>
                <Trait id="Manager" value="10"/>
                <Trait id="Valor" value="1"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Calculating" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_2_14 -->
    <xsl:template match="NPCCharacter[@id='lord_2_14']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_2_14_name}Isvan</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.7" build="0.7" key="000B0005807015843C72310F67D8F06E801C853C01B5813CE9923E7C7F70530C009AA602096E8FE00000000000000000000000000000000000000000012C4042"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="BalancedFightingSkills" value="8"/>
                <Trait id="Commander" value="8"/>
                <Trait id="Politician" value="6"/>
                <Trait id="Manager" value="6"/>
                <Trait id="Valor" value="1"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Calculating" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_2_15 -->
    <xsl:template match="NPCCharacter[@id='lord_2_15']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_2_15_name}Ratagost</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.7552" build="0.7989" key="0000200880002445C67115035783A878646476227680A42482769E468C16291901ECB0130E9A6534000000000000000000000000000000000000000021E09103"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="BalancedFightingSkills" value="3"/>
                <Trait id="Commander" value="10"/>
                <Trait id="Politician" value="8"/>
                <Trait id="Manager" value="4"/>
                <Trait id="Mercy" value="1"/>
                <Trait id="Valor" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_2_16 -->
    <xsl:template match="NPCCharacter[@id='lord_2_16']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_2_16_name}Bofur</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.1836" build="0.2176" key="00000C040070009B6983CFFB7C433FF1971470640061701F776777774F60841F00D862030D840A74000000000000000000000000000000000000000067B08102"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="BalancedFightingSkills" value="6"/>
                <Trait id="Commander" value="10"/>
                <Trait id="Manager" value="8"/>
                <Trait id="Politician" value="8"/>
                <Trait id="Valor" value="1"/>
                <Trait id="Generosity" value="2"/>
                <Trait id="Mercy" value="1"/>
                <Trait id="Honor" value="1"/>
                <Trait id="Calculating" value="0"/>
                <Trait id="Egalitarian" value="1"/>
                <Trait id="Oligarchic" value="0"/>
                <Trait id="Authoritarian" value="0"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_2_17 -->
    <xsl:template match="NPCCharacter[@id='lord_2_17']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_2_17_name}Thorin II Oakenshield</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.338" build="0.9" key="00000402C000328C8A7115113683CCD8A6497254819028629A6C8B58811E5A1800F850030F9CCE93000000000000000000000000000000000000000061F05080"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="KnightFightingSkills" value="2"/>
                <Trait id="Commander" value="3"/>
                <Trait id="Manager" value="10"/>
                <Trait id="Politician" value="10"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Mercy" value="0"/>
                <Trait id="Honor" value="2"/>
                <Trait id="Calculating" value="0"/>
                <Trait id="Egalitarian" value="0"/>
                <Trait id="Oligarchic" value="1"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_2_18 -->
    <xsl:template match="NPCCharacter[@id='lord_2_18']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_2_18_name}GlÃ³in</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5077" build="0.85" key="00004401400033CD997241105582FE8CE00F7751A4E7A12886CB5915805F235100C880030C578112000000000000000000000000000000000000000033882042"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="KnightFightingSkills" value="7"/>
                <Trait id="Commander" value="2"/>
                <Trait id="Manager" value="10"/>
                <Trait id="Politician" value="8"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Mercy" value="1"/>
                <Trait id="Honor" value="1"/>
                <Trait id="Calculating" value="0"/>
                <Trait id="Egalitarian" value="1"/>
                <Trait id="Oligarchic" value="0"/>
                <Trait id="Authoritarian" value="0"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_2_19 -->
    <xsl:template match="NPCCharacter[@id='lord_2_19']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_2_19_name}Dwalin</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.4" build="0.99" key="0000140880001343987138114783EC9AE5067865A1A5939078866E0A8C66108A018AC0130868557500000000000000000000000000000000000000006B187040"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="KnightFightingSkills" value="9"/>
                <Trait id="Commander" value="6"/>
                <Trait id="Manager" value="2"/>
                <Trait id="Politician" value="10"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Mercy" value="0"/>
                <Trait id="Honor" value="1"/>
                <Trait id="Calculating" value="0"/>
                <Trait id="Egalitarian" value="0"/>
                <Trait id="Oligarchic" value="1"/>
                <Trait id="Authoritarian" value="2"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_2_20 -->
    <xsl:template match="NPCCharacter[@id='lord_2_20']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_2_20_name}Ã“in</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.0177" build="0.8336" key="00000004400006C06E804F030003E018F03F71A5C6A40691AE1AFD6571FA00F901CD07130C997484000000000000000000000000000000000000000001905002"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="KnightFightingSkills" value="5"/>
                <Trait id="Commander" value="10"/>
                <Trait id="Manager" value="2"/>
                <Trait id="Politician" value="6"/>
                <Trait id="Valor" value="1"/>
                <Trait id="Generosity" value="2"/>
                <Trait id="Mercy" value="1"/>
                <Trait id="Honor" value="1"/>
                <Trait id="Calculating" value="0"/>
                <Trait id="Egalitarian" value="1"/>
                <Trait id="Oligarchic" value="0"/>
                <Trait id="Authoritarian" value="0"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_2_21 -->
    <xsl:template match="NPCCharacter[@id='lord_2_21']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_2_21_name}Svedorn</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0" build="0.6725" key="00001C0B4B0004D4C9BC38753878986C9767762692703328AB5977498679886A00CA00030C5A99D9000000000000000000000000000000000000000039982082"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="BalancedFightingSkills" value="6"/>
                <Trait id="Commander" value="2"/>
                <Trait id="Politician" value="8"/>
                <Trait id="Manager" value="10"/>
                <Trait id="Honor" value="1"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Calculating" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_2_22 -->
    <xsl:template match="NPCCharacter[@id='lord_2_22']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_2_22_name}Lashonek</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.4463" build="0.6332" key="00007400000004500870001008F1EFFF800F78777781070E70E019518088880F05897057080FFEF0000000000000000000000000000000000000000011103002"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="BalancedFightingSkills" value="8"/>
                <Trait id="Commander" value="10"/>
                <Trait id="Politician" value="4"/>
                <Trait id="Manager" value="6"/>
                <Trait id="Honor" value="1"/>
                <Trait id="Valor" value="-1"/>
                <Trait id="Calculating" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_2_23 -->
    <xsl:template match="NPCCharacter[@id='lord_2_23']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_2_23_name}Galden</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.692" build="0.8011" key="00001000000003805971111366849B6739397673A3B03D03BCD6078CAA996146005CA00305E77987000000000000000000000000000000000000000012B050C0"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="BalancedFightingSkills" value="8"/>
                <Trait id="Commander" value="10"/>
                <Trait id="Politician" value="4"/>
                <Trait id="Manager" value="6"/>
                <Trait id="Honor" value="-1"/>
                <Trait id="Valor" value="-1"/>
                <Trait id="Calculating" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_2_24 -->
    <xsl:template match="NPCCharacter[@id='lord_2_24']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_2_24_name}Alvar</xsl:attribute>
            <xsl:attribute name="default_group">HorseArcher</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0" build="0" key="00005404400036125A8E4CB3489CDB9D6F67758266101B4FF0F70D2F8A84995E01A000140ADAA974000000000000000000000000000000000000000025585000"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="BalancedFightingSkills" value="8"/>
                <Trait id="Commander" value="10"/>
                <Trait id="Politician" value="4"/>
                <Trait id="Manager" value="6"/>
                <Trait id="Honor" value="-1"/>
                <Trait id="Valor" value="-1"/>
                <Trait id="Calculating" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>


    <!-- ============================================== -->
    <!-- Faction 3 - Aserai/Harad -->
    <!-- ============================================== -->

    <!-- lord_3_1 -->
    <xsl:template match="NPCCharacter[@id='lord_3_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_3_1_name}Khadurak</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.6" build="0.85" key="0028280FD7FC23C299C2761C4AC1A5954778548899A66659788878746A9D9629000F16530FA7699C000000000000000000000000000000000000000000F47140"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="KnightFightingSkills" value="7"/>
                <Trait id="Commander" value="8"/>
                <Trait id="Politician" value="5"/>
                <Trait id="Manager" value="6"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Generosity" value="2"/>
                <Trait id="Mercy" value="1"/>
                <Trait id="Honor" value="2"/>
                <Trait id="Calculating" value="1"/>
                <Trait id="Egalitarian" value="1"/>
                <Trait id="Oligarchic" value="0"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_3_2 -->
    <xsl:template match="NPCCharacter[@id='lord_3_2']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_3_2_name}Harad Place Holder</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.75" key="0022500FD13C0001A7E3C8A64757B4876BA6785E867C96D8249D763F3BB7D86400000C5401A663680000000000000CE600000000000000000000000000286000"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="HorseArcherFightingSkills" value="10"/>
                <Trait id="Commander" value="8"/>
                <Trait id="Manager" value="9"/>
                <Trait id="Politician" value="10"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Generosity" value="2"/>
                <Trait id="Mercy" value="1"/>
                <Trait id="Honor" value="2"/>
                <Trait id="Calculating" value="1"/>
                <Trait id="Egalitarian" value="2"/>
                <Trait id="Oligarchic" value="0"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_3_3 -->
    <xsl:template match="NPCCharacter[@id='lord_3_3']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_3_3_name}Calemir</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.7762" build="0.7083" key="0028F40E5A000189869C5A8B7780749AAB77564639947345843A8657994575E505BBC0560B9687970000000000000CE600000000000000000000000001680000"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="KnightFightingSkills" value="5"/>
                <Trait id="CavalryFightingSkills" value="0"/>
                <Trait id="HorseArcherFightingSkills" value="0"/>
                <Trait id="Commander" value="8"/>
                <Trait id="Manager" value="2"/>
                <Trait id="Politician" value="5"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Mercy" value="1"/>
                <Trait id="Honor" value="2"/>
                <Trait id="Calculating" value="0"/>
                <Trait id="Egalitarian" value="1"/>
                <Trait id="Oligarchic" value="0"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_3_4 -->
    <xsl:template match="NPCCharacter[@id='lord_3_4']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_3_4_name}Maraa</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" key="002DD40E40000001869C5A8B7786749AAB77564639947345843A8657994575E5000E00040E9687970000000000000CE600000000000000000000000001E00000"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="Politician" value="8"/>
                <Trait id="Manager" value="2"/>
                <Trait id="Honor" value="1"/>
                <Trait id="Generosity" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_3_5 -->
    <xsl:template match="NPCCharacter[@id='lord_3_5']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_3_5_name}Haldir</xsl:attribute>
            <xsl:attribute name="default_group">HorseArcher</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.6" build="0.85" key="001CE008800023959B83263A29717DE32CD881249354432699A85C598A0A58590588805708986484000000000000000000000000000000000000000035085041"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="HorseArcherFightingSkills" value="6"/>
                <Trait id="Commander" value="4"/>
                <Trait id="Politician" value="8"/>
                <Trait id="Manager" value="10"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Mercy" value="1"/>
                <Trait id="Honor" value="2"/>
                <Trait id="Calculating" value="1"/>
                <Trait id="Egalitarian" value="1"/>
                <Trait id="Oligarchic" value="0"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_3_51 -->
    <xsl:template match="NPCCharacter[@id='lord_3_51']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_3_51_name}Haqan</xsl:attribute>
            <xsl:attribute name="default_group">HorseArcher</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.526" build="0.7128" key="0022B00880003012988235094C712BA509598337A14534159A897A068A1E4933034B80350473C822000000000000000000000000000000000000000038807044"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="BalancedFightingSkills" value="5"/>
                <Trait id="Commander" value="10"/>
                <Trait id="Politician" value="6"/>
                <Trait id="Manager" value="6"/>
                <Trait id="Calculating" value="1"/>
                <Trait id="Honor" value="1"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Generosity" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_3_6 -->
    <xsl:template match="NPCCharacter[@id='lord_3_6']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_3_6_name}Ruma</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.3981" build="0.2729" key="0022D80B400020128982510588A2A7348A60717AC375A0229E99C736896F575501A466130A1CB332000000000000000000000000000000000000000007542043"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="Politician" value="2"/>
                <Trait id="Manager" value="10"/>
                <Trait id="Honor" value="1"/>
                <Trait id="Generosity" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_3_7 -->
    <xsl:template match="NPCCharacter[@id='lord_3_7']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_3_7_name}Dhiyul</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5262" build="0.6404" key="001CC80A00000341AB86473C7577ACD8BB5C83453698737279B68E18780D2B26029A86240968931500000000000000000000000000000000000000001D3C3002"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="CavalryFightingSkills" value="3"/>
                <Trait id="Commander" value="4"/>
                <Trait id="Politician" value="4"/>
                <Trait id="Manager" value="8"/>
                <Trait id="Valor" value="-1"/>
                <Trait id="Calculating" value="-1"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Mercy" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_3_8 -->
    <xsl:template match="NPCCharacter[@id='lord_3_8']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_3_8_name}Addas</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5959" build="0.39" key="002DD008800017569A8325164C717EB41DB68247C1766706898A77398C1C655704469045046695710000000000000000000000000000000000000000308850C5"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="BalancedFightingSkills" value="8"/>
                <Trait id="Commander" value="8"/>
                <Trait id="Politician" value="8"/>
                <Trait id="Manager" value="4"/>
                <Trait id="Valor" value="1"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Honor" value="-1"/>
                <Trait id="Calculating" value="-1"/>
                <Trait id="Egalitarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_3_9 -->
    <xsl:template match="NPCCharacter[@id='lord_3_9']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_3_9_name}Usair</xsl:attribute>
            <xsl:attribute name="default_group">HorseArcher</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.7171" build="0.6445" key="001CF40880002959988044094881DCF25D518415A18656179A8899478D2C5A5304AAB0460A3CC435000000000000000000000000000000000000000037402000"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="CavalryFightingSkills" value="6"/>
                <Trait id="Commander" value="2"/>
                <Trait id="Politician" value="10"/>
                <Trait id="Manager" value="2"/>
                <Trait id="Honor" value="1"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Mercy" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_3_10 -->
    <xsl:template match="NPCCharacter[@id='lord_3_10']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_3_10_name}Anidha</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" key="002DDC0E4000040497D3427635D69CA7A837954A579662459958A8898646766A0007600307B767790000000000000CE600000000000000000000000000500000"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="Politician" value="8"/>
                <Trait id="Manager" value="10"/>
                <Trait id="Valor" value="-1"/>
                <Trait id="Calculating" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_3_11 -->
    <xsl:template match="NPCCharacter[@id='lord_3_11']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_3_11_name}Arwa</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.7" build="0.5337" key="00268C0C110030026982442CABD127719C46724BA187A130BBA3EA457A6C57470288B6240802B35C000000000000000000000000000000000000000059044001"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="CavalryFightingSkills" value="7"/>
                <Trait id="Commander" value="2"/>
                <Trait id="Politician" value="4"/>
                <Trait id="Manager" value="6"/>
                <Trait id="Valor" value="1"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Honor" value="-1"/>
                <Trait id="Egalitarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_3_12 -->
    <xsl:template match="NPCCharacter[@id='lord_3_12']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_3_12_name}Manan</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.7751" build="0.2584" key="0022D80A800030055C816132B9B2D7429B34737A82668521BAD6A558884D5C690394C635093568AC00000000000000000000000000000000000000002B2C2044"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="Politician" value="4"/>
                <Trait id="Manager" value="6"/>
                <Trait id="Mercy" value="1"/>
                <Trait id="Calculating" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_3_13 -->
    <xsl:template match="NPCCharacter[@id='lord_3_13']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_3_13_name}Nuqar</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5519" build="0.7167" key="0022F80880002794C782350B3B71AFB7098A8017A2755525A9B9AD468B0C5D68055B80570564A704000000000000000000000000000000000000000060A87042"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="CavalryFightingSkills" value="8"/>
                <Trait id="Commander" value="2"/>
                <Trait id="Politician" value="6"/>
                <Trait id="Manager" value="8"/>
                <Trait id="Valor" value="1"/>
                <Trait id="Honor" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_3_14 -->
    <xsl:template match="NPCCharacter[@id='lord_3_14']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_3_14_name}Thamza</xsl:attribute>
            <xsl:attribute name="default_group">HorseArcher</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.7752" build="0.5081" key="001C980880001612BC80243829815A955AA18339B27543258DB878188B178D450368C0350659885500000000000000000000000000000000000000006AD07081"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="BalancedFightingSkills" value="4"/>
                <Trait id="Commander" value="4"/>
                <Trait id="Politician" value="10"/>
                <Trait id="Manager" value="10"/>
                <Trait id="Honor" value="1"/>
                <Trait id="Calculating" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_3_15 -->
    <xsl:template match="NPCCharacter[@id='lord_3_15']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_3_15_name}Ghuzid</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.6" key="003F000FD6FC29DA0CFC208F1001FBEF2D01FA0000F01110500B1EF068E672B006B970670B0D87810000000000000000000000000000000000000000217800C2"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="BalancedFightingSkills" value="9"/>
                <Trait id="Commander" value="8"/>
                <Trait id="Politician" value="8"/>
                <Trait id="Manager" value="10"/>
                <Trait id="Valor" value="1"/>
                <Trait id="Calculating" value="-1"/>
                <Trait id="Generosity" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_3_16 -->
    <xsl:template match="NPCCharacter[@id='lord_3_16']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_3_16_name}RÃºmil</xsl:attribute>
            <xsl:attribute name="default_group">Ranged</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.65" build="0.8" key="001CD808800003D2998335193B81CD944A998317B1718116AA98A7278C098A440399903509299520000000000000000000000000000000000000000057281041"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="HorseArcherFightingSkills" value="5"/>
                <Trait id="Commander" value="2"/>
                <Trait id="Politician" value="6"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Mercy" value="0"/>
                <Trait id="Honor" value="2"/>
                <Trait id="Calculating" value="0"/>
                <Trait id="Egalitarian" value="1"/>
                <Trait id="Oligarchic" value="0"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_3_17 -->
    <xsl:template match="NPCCharacter[@id='lord_3_17']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_3_17_name}Orophin</xsl:attribute>
            <xsl:attribute name="default_group">Ranged</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.68" build="0.85" key="0022EC08800018D57881460A2B7129E25BAA85149355561688997A298E1A795304689046064BC72600000000000000000000000000000000000000001CC070C5"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="HorseArcherFightingSkills" value="6"/>
                <Trait id="Commander" value="3"/>
                <Trait id="Politician" value="5"/>
                <Trait id="Manager" value="8"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Mercy" value="1"/>
                <Trait id="Honor" value="2"/>
                <Trait id="Calculating" value="0"/>
                <Trait id="Egalitarian" value="1"/>
                <Trait id="Oligarchic" value="0"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_3_18 -->
    <xsl:template match="NPCCharacter[@id='lord_3_18']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_3_18_name}Aranthon</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.7" build="0.8" key="001CA40880001141A982443A39718ED53DA28416A353712599A86A478E184657066AA068065A8854000000000000000000000000000000000000000008C040C4"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="CavalryFightingSkills" value="2"/>
                <Trait id="Commander" value="4"/>
                <Trait id="Politician" value="5"/>
                <Trait id="Manager" value="10"/>
                <Trait id="Valor" value="1"/>
                <Trait id="Generosity" value="2"/>
                <Trait id="Mercy" value="1"/>
                <Trait id="Honor" value="2"/>
                <Trait id="Calculating" value="1"/>
                <Trait id="Egalitarian" value="1"/>
                <Trait id="Oligarchic" value="0"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_3_19 -->
    <xsl:template match="NPCCharacter[@id='lord_3_19']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_3_19_name}Aethirion</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.72" build="0.82" key="0016C80880001597BA83341737814FC43B578027A2557115ABB98E678A0A6867049870460979E5240000000000000000000000000000000000000000253850C0"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="HorseArcherFightingSkills" value="8"/>
                <Trait id="Commander" value="3"/>
                <Trait id="Politician" value="6"/>
                <Trait id="Manager" value="9"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Mercy" value="0"/>
                <Trait id="Honor" value="2"/>
                <Trait id="Calculating" value="1"/>
                <Trait id="Egalitarian" value="1"/>
                <Trait id="Oligarchic" value="0"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_3_20 -->
    <xsl:template match="NPCCharacter[@id='lord_3_20']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_3_20_name}Karith</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.3371" build="0.6152" key="0028B00880001A14A78244373A714DB139C2813792826026AA79AC688B185E4503295035024675B0000000000000000000000000000000000000000068486084"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="BalancedFightingSkills" value="7"/>
                <Trait id="Commander" value="6"/>
                <Trait id="Politician" value="10"/>
                <Trait id="Manager" value="2"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Generosity" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_3_21 -->
    <xsl:template match="NPCCharacter[@id='lord_3_21']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_3_21_name}Ukhai</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.7348" build="0.4839" key="001CD40A00001582C9823315378118F46C84843482727016978A6A088D2B4E480487B04608774A7100000000000000000000000000000000000000003F182083"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="BalancedFightingSkills" value="8"/>
                <Trait id="Commander" value="2"/>
                <Trait id="Politician" value="8"/>
                <Trait id="Manager" value="10"/>
                <Trait id="Valor" value="1"/>
                <Trait id="Honor" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_3_22 -->
    <xsl:template match="NPCCharacter[@id='lord_3_22']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_3_22_name}Duilin</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.1088" build="0.5139" key="0000AC04C00016450E8A2E038706E098A87480B0D19560016758100A8FE00A450098260309798B0600000000000000000000000000000000000000003B343002"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="KnightFightingSkills" value="8"/>
                <Trait id="Commander" value="4"/>
                <Trait id="Politician" value="6"/>
                <Trait id="Manager" value="10"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Mercy" value="0"/>
                <Trait id="Honor" value="2"/>
                <Trait id="Calculating" value="0"/>
                <Trait id="Egalitarian" value="1"/>
                <Trait id="Oligarchic" value="0"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_3_23 -->
    <xsl:template match="NPCCharacter[@id='lord_3_23']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_3_23_name}Qahin</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.7114" build="0.7083" key="001670045300340A9772C4C436AA7887A435497444328E72DB8C48519D57749A05BBB1570BBA29780000387A00000AEB00000033000000000000000001705000"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="BalancedFightingSkills" value="8"/>
                <Trait id="Commander" value="2"/>
                <Trait id="Politician" value="8"/>
                <Trait id="Manager" value="10"/>
                <Trait id="Valor" value="1"/>
                <Trait id="Mercy" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>


    <!-- ============================================== -->
    <!-- Faction 4 - Vlandia/Rohan -->
    <!-- ============================================== -->

    <!-- lord_4_1 -->
    <xsl:template match="NPCCharacter[@id='lord_4_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_4_1_name}Théoden</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" key="0005280140001242947E068A709500460C7250703EB70F135C85021887733A070089B6030822BA9000000000000000000000000000000000000000003F1C7002"/>
            </face>
            <skills>
                <skill id="OneHanded" value="280"/>
                <skill id="TwoHanded" value="250"/>
                <skill id="Polearm" value="270"/>
                <skill id="Bow" value="150"/>
                <skill id="Crossbow" value="100"/>
                <skill id="Throwing" value="120"/>
                <skill id="Riding" value="300"/>
                <skill id="Athletics" value="200"/>
                <skill id="Crafting" value="50"/>
                <skill id="Scouting" value="250"/>
                <skill id="Tactics" value="300"/>
                <skill id="Charm" value="200"/>
                <skill id="Leadership" value="300"/>
                <skill id="Trade" value="100"/>
                <skill id="Steward" value="250"/>
                <skill id="Medicine" value="280"/>
                <skill id="Engineering" value="280"/>
                <skill id="Roguery" value="300"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="2"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Calculating" value="0"/>
                <Trait id="Mercy" value="1"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="0"/>
                <Trait id="Oligarchic" value="1"/>
                <Trait id="Authoritarian" value="2"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_4_2 -->
    <xsl:template match="NPCCharacter[@id='lord_4_2']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_4_2_name}Elfhild</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" key="000044000000000577707780B7C07777C2BB71F7EFF77AA7956746991C7775C70000000505C7966A000000000000000000000000000000000000000000000100"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="Politician" value="2"/>
                <Trait id="Manager" value="2"/>
                <Trait id="Mercy" value="1"/>
                <Trait id="Honor" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_4_4 -->
    <xsl:template match="NPCCharacter[@id='lord_4_4']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_4_4_name}Elthild</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.4706" build="0.6001" key="0005180A00CC00049172105733863178A76987A967716698AB94AE398C6A5582036976350666486B00000000000000000000000000000000000000002EDC2042"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="Politician" value="4"/>
                <Trait id="Manager" value="8"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Generosity" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_4_5 -->
    <xsl:template match="NPCCharacter[@id='lord_4_5']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_4_5_name}Unthery</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5517" build="0.5949" key="000B60014000234665734C57613665A5354588837351AC1455E8373672131432037980350768BB63000000000000000000000000000000000000000026E01082"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="BalancedFightingSkills" value="8"/>
                <Trait id="Commander" value="8"/>
                <Trait id="Politician" value="8"/>
                <Trait id="Manager" value="6"/>
                <Trait id="Valor" value="1"/>
                <Trait id="Calculating" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_4_6 -->
    <xsl:template match="NPCCharacter[@id='lord_4_6']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_4_6_name}Grimbold Grimingas </xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.01" build="0.61" key="0002B00000001345DD80090A71069A53BF55834D3FC9DF00500D53098CC0F04800A906030AAAA15A00000000000000000000000000000000000000003F4C3002"/>
            </face>
            <skills>
                <skill id="OneHanded" value="250"/>
                <skill id="TwoHanded" value="280"/>
                <skill id="Polearm" value="240"/>
                <skill id="Bow" value="140"/>
                <skill id="Crossbow" value="90"/>
                <skill id="Throwing" value="110"/>
                <skill id="Riding" value="270"/>
                <skill id="Athletics" value="190"/>
                <skill id="Crafting" value="50"/>
                <skill id="Scouting" value="140"/>
                <skill id="Tactics" value="230"/>
                <skill id="Charm" value="190"/>
                <skill id="Leadership" value="250"/>
                <skill id="Trade" value="100"/>
                <skill id="Steward" value="220"/>
                <skill id="Medicine" value="80"/>
                <skill id="Engineering" value="280"/>
                <skill id="Roguery" value="320"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="2"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Calculating" value="0"/>
                <Trait id="Mercy" value="1"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="0"/>
                <Trait id="Oligarchic" value="1"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_4_7 -->
    <xsl:template match="NPCCharacter[@id='lord_4_7']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_4_7_name}ThÃ©odred Eorling</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.1" build="0.5" key="00052C028000000200752A737881DC04A918866245A806069451821B8F4319050097260309119F0800000000000000000000000000000000000000003D2C2002"/>
            </face>
            <skills>
                <skill id="OneHanded" value="270"/>
                <skill id="TwoHanded" value="240"/>
                <skill id="Polearm" value="260"/>
                <skill id="Bow" value="150"/>
                <skill id="Crossbow" value="100"/>
                <skill id="Throwing" value="120"/>
                <skill id="Riding" value="290"/>
                <skill id="Athletics" value="200"/>
                <skill id="Crafting" value="50"/>
                <skill id="Scouting" value="150"/>
                <skill id="Tactics" value="250"/>
                <skill id="Charm" value="200"/>
                <skill id="Leadership" value="270"/>
                <skill id="Trade" value="100"/>
                <skill id="Steward" value="230"/>
                <skill id="Medicine" value="80"/>
                <skill id="Engineering" value="280"/>
                <skill id="Roguery" value="300"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="2"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Calculating" value="0"/>
                <Trait id="Mercy" value="1"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="0"/>
                <Trait id="Oligarchic" value="1"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_4_8 -->
    <xsl:template match="NPCCharacter[@id='lord_4_8']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_4_8_name}Furnhard</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" key="00006C02C000000977A84702762098CB27AD8D4D54C9895378C97B1E27408574000440040488276B000000000000000000000000000000000000000000780000"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="BalancedFightingSkills" value="5"/>
                <Trait id="Commander" value="8"/>
                <Trait id="Politician" value="10"/>
                <Trait id="Manager" value="8"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Honor" value="-1"/>
                <Trait id="Generosity" value="-1"/>
                <Trait id="Valor" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_4_9 -->
    <xsl:template match="NPCCharacter[@id='lord_4_9']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_4_9_name}Thomund</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.8124" build="0.6943" key="00041C044000200535813C56515565A7186478496341BE2334D8792573325632057AC057076ABD61000000000000000000000000000000000000000036E830C0"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="KnightFightingSkills" value="9"/>
                <Trait id="Commander" value="8"/>
                <Trait id="Politician" value="8"/>
                <Trait id="Manager" value="8"/>
                <Trait id="Valor" value="1"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Calculating" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_4_10 -->
    <xsl:template match="NPCCharacter[@id='lord_4_10']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_4_10_name}Elys</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" key="000080000000000B67874369563B575737063939BC84896C773744465293778C000000030F97CDD9000000000000000000000000000000000000000000000000"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="Politician" value="4"/>
                <Trait id="Manager" value="2"/>
                <Trait id="Mercy" value="1"/>
                <Trait id="Calculating" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_4_11 -->
    <xsl:template match="NPCCharacter[@id='lord_4_11']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_4_11_name}Liena</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" key="00127C04403C3004B7D8465988D98C845A24288FAD8AB786279B9CAA78AA48B500059A43059AA965000000000000000000000000000000000000000000282000"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="BalancedFightingSkills" value="6"/>
                <Trait id="Commander" value="6"/>
                <Trait id="Politician" value="8"/>
                <Trait id="Manager" value="4"/>
                <Trait id="Mercy" value="1"/>
                <Trait id="Honor" value="1"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Valor" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_4_12 -->
    <xsl:template match="NPCCharacter[@id='lord_4_12']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_4_12_name}Silvind</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.4524" build="0.5804" key="0005340140CC00017071527435736265B7BE7A79B0614997BAB7F9397C2A987203597635053CBA5A00000000000000000000000000000000000000005CB41044"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="BalancedFightingSkills" value="6"/>
                <Trait id="Commander" value="10"/>
                <Trait id="Politician" value="4"/>
                <Trait id="Manager" value="10"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Valor" value="1"/>
                <Trait id="Calculating" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_4_121 -->
    <xsl:template match="NPCCharacter[@id='lord_4_121']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_4_121_name}Lasand</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0" build="0" key="0005440440FC308E332B7C6D8A45CA881E6E4D6784343CC64C9A3A598476688901F006890F8C555B0000000000000000000000000000000000000000012C3144"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="KnightFightingSkills" value="5"/>
                <Trait id="Commander" value="8"/>
                <Trait id="Politician" value="4"/>
                <Trait id="Manager" value="4"/>
                <Trait id="Mercy" value="1"/>
                <Trait id="Honor" value="1"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Valor" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_4_13 -->
    <xsl:template match="NPCCharacter[@id='lord_4_13']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_4_13_name}Romund</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.6404" build="0.6682" key="0005B008800039CD48802D04476A22F69E19714BC2BB1B05FF6F00777B853103027AA0240759705000000000000000000000000000000000000000007CE82001"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="KnightFightingSkills" value="6"/>
                <Trait id="Commander" value="2"/>
                <Trait id="Politician" value="10"/>
                <Trait id="Manager" value="10"/>
                <Trait id="Valor" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_4_14 -->
    <xsl:template match="NPCCharacter[@id='lord_4_14']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_4_14_name}Morcon</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.7525" build="0.6973" key="000510030000038552722B57125576F5232384586361A9027B355D2470527234029AB02409682751000000000000000000000000000000000000000073301040"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="BalancedFightingSkills" value="5"/>
                <Trait id="Commander" value="4"/>
                <Trait id="Politician" value="10"/>
                <Trait id="Manager" value="10"/>
                <Trait id="Honor" value="1"/>
                <Trait id="Valor" value="-1"/>
                <Trait id="Generosity" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_4_141 -->
    <xsl:template match="NPCCharacter[@id='lord_4_141']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_4_141_name}Amorcon</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.6561" build="0.7254" key="000A1C044000200213731F53422567A40340782A62818C5644865A2982340431017BA01307534CA3000000000000000000000000000000000000000024F070C4"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="BalancedFightingSkills" value="4"/>
                <Trait id="Commander" value="6"/>
                <Trait id="Politician" value="6"/>
                <Trait id="Manager" value="10"/>
                <Trait id="Honor" value="1"/>
                <Trait id="Generosity" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_4_15 -->
    <xsl:template match="NPCCharacter[@id='lord_4_15']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_4_15_name}Erdurand</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.6214" build="0.5381" key="00004C044000030323834B56123476B60987754B71517D2544947746753675230178901307648B8400000000000000000000000000000000000000006CE84102"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="BalancedFightingSkills" value="7"/>
                <Trait id="Commander" value="4"/>
                <Trait id="Politician" value="6"/>
                <Trait id="Manager" value="10"/>
                <Trait id="Valor" value="1"/>
                <Trait id="Calculating" value="-1"/>
                <Trait id="Mercy" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_4_16 -->
    <xsl:template match="NPCCharacter[@id='lord_4_16']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_4_16_name}Erkenbrand FelÃ¡nding</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="1" build="0.6157" key="000AD40000001343EE7E000F4AAB0022AB5E70F090EDE6067446100F8B5B0F080099F60309F6E0FE00000000000000000000000000000000000000003D2C7002"/>
            </face>
            <skills>
                <skill id="OneHanded" value="260"/>
                <skill id="TwoHanded" value="240"/>
                <skill id="Polearm" value="250"/>
                <skill id="Bow" value="140"/>
                <skill id="Crossbow" value="90"/>
                <skill id="Throwing" value="110"/>
                <skill id="Riding" value="280"/>
                <skill id="Athletics" value="190"/>
                <skill id="Crafting" value="50"/>
                <skill id="Scouting" value="140"/>
                <skill id="Tactics" value="280"/>
                <skill id="Charm" value="190"/>
                <skill id="Leadership" value="290"/>
                <skill id="Trade" value="100"/>
                <skill id="Steward" value="250"/>
                <skill id="Medicine" value="180"/>
                <skill id="Engineering" value="280"/>
                <skill id="Roguery" value="300"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="2"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Calculating" value="0"/>
                <Trait id="Mercy" value="1"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="0"/>
                <Trait id="Oligarchic" value="1"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_4_17 -->
    <xsl:template match="NPCCharacter[@id='lord_4_17']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_4_17_name}Elbet</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.6956" build="0.6722" key="0000280440CC000E72603755334384AA85A98979B1628696AAB6E9298A3B9358019AA6130944AAB5000000000000000000000000000000000000000025342040"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="Politician" value="4"/>
                <Trait id="Manager" value="4"/>
                <Trait id="Honor" value="1"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Calculating" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_4_18 -->
    <xsl:template match="NPCCharacter[@id='lord_4_18']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_4_18_name}Amalgun</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" key="00055C0880BC0245678789A8C8B6C2728588854B98886843B5DCD6C879C689370005676409844933000000000000000000000000000000000000000000F08000"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="BalancedFightingSkills" value="6"/>
                <Trait id="Commander" value="10"/>
                <Trait id="Politician" value="6"/>
                <Trait id="Manager" value="10"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Honor" value="-1"/>
                <Trait id="Calculating" value="1"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Valor" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_4_181 -->
    <xsl:template match="NPCCharacter[@id='lord_4_181']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_4_181_name}Arthamund</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" key="00055C0880BC0245678789A8C8B6C2728588854B98886843B5DCD6C879C689370005676409844933000000000000000000000000000000000000000000F08000"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="BalancedFightingSkills" value="3"/>
                <Trait id="Commander" value="6"/>
                <Trait id="Politician" value="4"/>
                <Trait id="Manager" value="2"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Valor" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_4_19 -->
    <xsl:template match="NPCCharacter[@id='lord_4_19']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_4_19_name}Asela</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.6483" build="0.7136" key="0000440440CC000B92611464264492D99ADB8BA86151568799A9EE218B4A645603BBA6350B363A260000000000000000000000000000000000000000616C2084"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="Politician" value="2"/>
                <Trait id="Manager" value="2"/>
                <Trait id="Calculating" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_4_20 -->
    <xsl:template match="NPCCharacter[@id='lord_4_20']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_4_20_name}Varmund</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.6056" build="0.6666" key="000004044000308233821857532569D41066841B71415A515D56762987126334018A901308692C53000000000000000000000000000000000000000057001042"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="BalancedFightingSkills" value="8"/>
                <Trait id="Commander" value="4"/>
                <Trait id="Politician" value="4"/>
                <Trait id="Manager" value="8"/>
                <Trait id="Calculating" value="1"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Valor" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_4_21 -->
    <xsl:template match="NPCCharacter[@id='lord_4_21']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_4_21_name}CuthrÃ¦d Ordlacing </xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.7222" build="0.5995" key="000218000000394336706B475208F01E9F1686E2F9C7C211D1617519811A11190099B6030948B49500000000000000000000000000000000000000003F2C3002"/>
            </face>
            <skills>
                <skill id="OneHanded" value="250"/>
                <skill id="TwoHanded" value="230"/>
                <skill id="Polearm" value="240"/>
                <skill id="Bow" value="140"/>
                <skill id="Crossbow" value="90"/>
                <skill id="Throwing" value="110"/>
                <skill id="Riding" value="270"/>
                <skill id="Athletics" value="190"/>
                <skill id="Crafting" value="50"/>
                <skill id="Scouting" value="140"/>
                <skill id="Tactics" value="230"/>
                <skill id="Charm" value="190"/>
                <skill id="Leadership" value="250"/>
                <skill id="Trade" value="100"/>
                <skill id="Steward" value="220"/>
                <skill id="Medicine" value="80"/>
                <skill id="Engineering" value="280"/>
                <skill id="Roguery" value="300"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="2"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Calculating" value="0"/>
                <Trait id="Mercy" value="1"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="0"/>
                <Trait id="Oligarchic" value="1"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_4_22 -->
    <xsl:template match="NPCCharacter[@id='lord_4_22']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_4_22_name}Wulf Celmunding</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.7292" build="0.4074" key="00050806C00017822070081EF773F1B4A05380615D95FB11D1650217800A61110096B6030948E0B500000000000000000000000000000000000000003F349002"/>
            </face>
            <skills>
                <skill id="OneHanded" value="150"/>
                <skill id="TwoHanded" value="130"/>
                <skill id="Polearm" value="140"/>
                <skill id="Bow" value="140"/>
                <skill id="Crossbow" value="90"/>
                <skill id="Throwing" value="110"/>
                <skill id="Riding" value="170"/>
                <skill id="Athletics" value="90"/>
                <skill id="Crafting" value="50"/>
                <skill id="Scouting" value="140"/>
                <skill id="Tactics" value="30"/>
                <skill id="Charm" value="10"/>
                <skill id="Leadership" value="50"/>
                <skill id="Trade" value="10"/>
                <skill id="Steward" value="20"/>
                <skill id="Medicine" value="10"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="2"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Calculating" value="0"/>
                <Trait id="Mercy" value="1"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="0"/>
                <Trait id="Oligarchic" value="1"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_4_23 -->
    <xsl:template match="NPCCharacter[@id='lord_4_23']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_4_23_name}Marhath Marhad</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0" build="0.6134" key="00020800000013810070FB1FF911EFBBE0238BA15DDCF5450019011F890AB111008906030818E00800000000000000000000000000000000000000003F145002"/>
            </face>
            <skills>
                <skill id="OneHanded" value="250"/>
                <skill id="TwoHanded" value="230"/>
                <skill id="Polearm" value="240"/>
                <skill id="Bow" value="140"/>
                <skill id="Crossbow" value="90"/>
                <skill id="Throwing" value="110"/>
                <skill id="Riding" value="270"/>
                <skill id="Athletics" value="190"/>
                <skill id="Crafting" value="50"/>
                <skill id="Scouting" value="140"/>
                <skill id="Tactics" value="230"/>
                <skill id="Charm" value="190"/>
                <skill id="Leadership" value="250"/>
                <skill id="Trade" value="100"/>
                <skill id="Steward" value="220"/>
                <skill id="Medicine" value="80"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="2"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Calculating" value="0"/>
                <Trait id="Mercy" value="1"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="0"/>
                <Trait id="Oligarchic" value="1"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_4_24 -->
    <xsl:template match="NPCCharacter[@id='lord_4_24']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_4_24_name}GrÃ­ma GrimmÃ³ding</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.38" build="0.1644" key="0000500FC0CC100378A061BC61A180068D4BA177F0C1E601B0FF1B008FE78B00007276030720F81100000000000000000000000000000000000000003AF40102"/>
            </face>
            <skills>
                <skill id="OneHanded" value="100"/>
                <skill id="TwoHanded" value="80"/>
                <skill id="Polearm" value="80"/>
                <skill id="Bow" value="80"/>
                <skill id="Crossbow" value="80"/>
                <skill id="Throwing" value="80"/>
                <skill id="Riding" value="150"/>
                <skill id="Athletics" value="120"/>
                <skill id="Crafting" value="50"/>
                <skill id="Scouting" value="100"/>
                <skill id="Tactics" value="200"/>
                <skill id="Charm" value="250"/>
                <skill id="Leadership" value="150"/>
                <skill id="Trade" value="150"/>
                <skill id="Steward" value="200"/>
                <skill id="Medicine" value="80"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="-2"/>
                <Trait id="Generosity" value="-1"/>
                <Trait id="Calculating" value="2"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Valor" value="-1"/>
                <Trait id="Egalitarian" value="0"/>
                <Trait id="Oligarchic" value="1"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_4_25 -->
    <xsl:template match="NPCCharacter[@id='lord_4_25']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_4_25_name}Lucand</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.6867" build="0.7269" key="0000100456B4020DC66F69A052632C57D98458CAA8CA55C96B66BB47ACB9C85407ABA6780A886647000000000000000000000000000000000000000001602000"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="BalancedFightingSkills" value="8"/>
                <Trait id="Commander" value="4"/>
                <Trait id="Politician" value="8"/>
                <Trait id="Manager" value="8"/>
                <Trait id="Mercy" value="1"/>
                <Trait id="Honor" value="1"/>
                <Trait id="Valor" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_4_26 -->
    <xsl:template match="NPCCharacter[@id='lord_4_26']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_4_26_name}Peric</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" key="000B48058FFC300578E42AAB88C6A5BD924B6988339398973D8EA8676AA7686500056D2507336C86000000000000000000000000000000000000000000C81000"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="BalancedFightingSkills" value="6"/>
                <Trait id="Commander" value="8"/>
                <Trait id="Politician" value="6"/>
                <Trait id="Manager" value="8"/>
                <Trait id="Valor" value="-1"/>
                <Trait id="Generosity" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_4_27 -->
    <xsl:template match="NPCCharacter[@id='lord_4_27']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_4_27_name}Ã†lle Ã†thellafing </xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.4491" build="0.7222" key="000A14080000168200800A1F0530F00E660F8960B08860020019011F8E0BA101008B76030805E60600000000000000000000000000000000000000003F049002"/>
            </face>
            <skills>
                <skill id="OneHanded" value="250"/>
                <skill id="TwoHanded" value="230"/>
                <skill id="Polearm" value="240"/>
                <skill id="Bow" value="140"/>
                <skill id="Crossbow" value="90"/>
                <skill id="Throwing" value="110"/>
                <skill id="Riding" value="270"/>
                <skill id="Athletics" value="190"/>
                <skill id="Crafting" value="50"/>
                <skill id="Scouting" value="140"/>
                <skill id="Tactics" value="230"/>
                <skill id="Charm" value="190"/>
                <skill id="Leadership" value="250"/>
                <skill id="Trade" value="100"/>
                <skill id="Steward" value="220"/>
                <skill id="Medicine" value="80"/>
                <skill id="Engineering" value="280"/>
                <skill id="Roguery" value="300"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="2"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Calculating" value="0"/>
                <Trait id="Mercy" value="1"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="0"/>
                <Trait id="Oligarchic" value="1"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_4_28 -->
    <xsl:template match="NPCCharacter[@id='lord_4_28']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_4_28_name}Fasthelm Morcargas </xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0" build="0.5" key="0008D40A400010149E70FF0790609008AB9D709070EF9600744D70098DB2D404007706030707A09500000000000000000000000000000000000000003CF47042"/>
            </face>
            <skills>
                <skill id="OneHanded" value="250"/>
                <skill id="TwoHanded" value="230"/>
                <skill id="Polearm" value="240"/>
                <skill id="Bow" value="140"/>
                <skill id="Crossbow" value="90"/>
                <skill id="Throwing" value="110"/>
                <skill id="Riding" value="270"/>
                <skill id="Athletics" value="190"/>
                <skill id="Crafting" value="50"/>
                <skill id="Scouting" value="140"/>
                <skill id="Tactics" value="230"/>
                <skill id="Charm" value="190"/>
                <skill id="Leadership" value="250"/>
                <skill id="Trade" value="100"/>
                <skill id="Steward" value="220"/>
                <skill id="Medicine" value="80"/>
                <skill id="Engineering" value="280"/>
                <skill id="Roguery" value="300"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="2"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Calculating" value="0"/>
                <Trait id="Mercy" value="1"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="0"/>
                <Trait id="Oligarchic" value="1"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>


    <!-- ============================================== -->
    <!-- Faction 5 - Battania/Woodland Realm -->
    <!-- ============================================== -->

    <!-- lord_5_1 -->
    <xsl:template match="NPCCharacter[@id='lord_5_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_5_1_name}VargÃ»l, the High Warlord of Khand</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" key="00054408800027875CA466A563442BD406899779A794573B86A7C473581AB487000546550E46C99D000000000000000000000000000000000000000000F03180"/>
            </face>
            <skills>
                <skill id="OneHanded" value="280"/>
                <skill id="TwoHanded" value="220"/>
                <skill id="Polearm" value="270"/>
                <skill id="Bow" value="250"/>
                <skill id="Crossbow" value="40"/>
                <skill id="Throwing" value="180"/>
                <skill id="Riding" value="300"/>
                <skill id="Athletics" value="200"/>
                <skill id="Crafting" value="90"/>
                <skill id="Scouting" value="220"/>
                <skill id="Tactics" value="270"/>
                <skill id="Charm" value="230"/>
                <skill id="Leadership" value="290"/>
                <skill id="Trade" value="120"/>
                <skill id="Steward" value="180"/>
                <skill id="Medicine" value="110"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="1"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Calculating" value="2"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="0"/>
                <Trait id="Oligarchic" value="1"/>
                <Trait id="Authoritarian" value="2"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_5_7 -->
    <xsl:template match="NPCCharacter[@id='lord_5_7']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_5_7_name}Khand PlaceHolder</xsl:attribute>
            <xsl:attribute name="default_group">Ranged</xsl:attribute>
            <face>
                <BodyProperties version="4" key="0005B00CC000244277B884755A5572CC8776B9A6A95B558E342D38679CA7288A00055B82017BB770000000000000000000000000000000000000000000506000"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="HorseArcherFightingSkills" value="9"/>
                <Trait id="Commander" value="6"/>
                <Trait id="Manager" value="10"/>
                <Trait id="Politician" value="8"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Mercy" value="1"/>
                <Trait id="Honor" value="2"/>
                <Trait id="Calculating" value="0"/>
                <Trait id="Egalitarian" value="0"/>
                <Trait id="Oligarchic" value="1"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_5_3 -->
    <xsl:template match="NPCCharacter[@id='lord_5_3']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_5_3_name}Ergeon</xsl:attribute>
            <xsl:attribute name="default_group">HorseArcher</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.7083" build="0.6528" key="00083C070000238696333724947BC936C948765C8A747896ACA8A26C988A18B608CAB4890CA97C65000000000000000000000000000000000000000001985000"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="BalancedFightingSkills" value="4"/>
                <Trait id="Commander" value="10"/>
                <Trait id="Politician" value="4"/>
                <Trait id="Manager" value="6"/>
                <Trait id="Mercy" value="1"/>
                <Trait id="Honor" value="1"/>
                <Trait id="Valor" value="-1"/>
                <Trait id="Calculating" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_5_4 -->
    <xsl:template match="NPCCharacter[@id='lord_5_4']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_5_4_name}Nywin</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.7967" build="0.8773" key="00056802C07000108879465A53347C94973C75A5778495489D65EC3275332456029DC62409783641000000000000000000000000000000000000000073342082"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="Commander" value="2"/>
                <Trait id="Politician" value="10"/>
                <Trait id="Manager" value="10"/>
                <Trait id="Honor" value="-1"/>
                <Trait id="Mercy" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_5_5 -->
    <xsl:template match="NPCCharacter[@id='lord_5_5']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_5_5_name}Melidir</xsl:attribute>
            <xsl:attribute name="default_group">HorseArcher</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.7017" build="0.6334" key="000268070000244226700A207474A3848C9A751498816EA389779E6A832A155105AAB0560A65B64200000000000000000000000000000000000000005F5870C2"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="BalancedFightingSkills" value="4"/>
                <Trait id="Commander" value="8"/>
                <Trait id="Politician" value="2"/>
                <Trait id="Manager" value="10"/>
                <Trait id="Valor" value="-1"/>
                <Trait id="Honor" value="-1"/>
                <Trait id="Generosity" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_5_6 -->
    <xsl:template match="NPCCharacter[@id='lord_5_6']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_5_6_name}Alcaea</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.3595" build="0.3633" key="000B9805801420068771233587A43345B881874967868927DC887C58756876480485564608148946000000000000000000000000000000000000000047041081"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="Politician" value="8"/>
                <Trait id="Manager" value="6"/>
                <Trait id="Valor" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_5_8 -->
    <xsl:template match="NPCCharacter[@id='lord_5_8']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_5_8_name}Sein</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.7439" build="0.8603" key="00030C05800000069E712A2C005486A876977CB714119A7636787B8481162713009DB0030974D383000000000000000000000000000000000000000057308081"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="BalancedFightingSkills" value="4"/>
                <Trait id="Commander" value="8"/>
                <Trait id="Politician" value="4"/>
                <Trait id="Manager" value="6"/>
                <Trait id="Mercy" value="1"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Valor" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_5_9 -->
    <xsl:template match="NPCCharacter[@id='lord_5_9']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_5_9_name}Culharn</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.7749" build="0.8015" key="000570058000038185611D291633E7A8434977A39731567986A7574582060843049CC04609797871000000000000000000000000000000000000000027285004"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="BalancedFightingSkills" value="7"/>
                <Trait id="Commander" value="10"/>
                <Trait id="Politician" value="8"/>
                <Trait id="Manager" value="10"/>
                <Trait id="Honor" value="1"/>
                <Trait id="Calculating" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_5_91 -->
    <xsl:template match="NPCCharacter[@id='lord_5_91']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_5_91_name}Tegan</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.7328" build="0.7874" key="00055804400000096E711D2C02527AA4436974395B51CB6286A78917820A280602CCB0240C58C775000000000000000000000000000000000000000003A06100"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="BalancedFightingSkills" value="4"/>
                <Trait id="Commander" value="6"/>
                <Trait id="Politician" value="6"/>
                <Trait id="Manager" value="4"/>
                <Trait id="Honor" value="1"/>
                <Trait id="Calculating" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_5_10 -->
    <xsl:template match="NPCCharacter[@id='lord_5_10']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_5_10_name}Corein</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" key="00058002C0FC20035646D8A987844C9488F76DD9A2367A3858B6C6698734870800054003073C8C7A000000000000000000000000000000000000000000A04000"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="BalancedFightingSkills" value="7"/>
                <Trait id="Commander" value="4"/>
                <Trait id="Politician" value="2"/>
                <Trait id="Manager" value="4"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Generosity" value="-1"/>
                <Trait id="Valor" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_5_11 -->
    <xsl:template match="NPCCharacter[@id='lord_5_11']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_5_11_name}Alynneth</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.7986" build="0.4567" key="000558044070000A5786655953636782884A77758E779328A9718E51703467650487C6460874C3760000000000000000000000000000000000000000051C0044"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="Politician" value="10"/>
                <Trait id="Manager" value="6"/>
                <Trait id="Mercy" value="1"/>
                <Trait id="Generosity" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_5_12 -->
    <xsl:template match="NPCCharacter[@id='lord_5_12']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_5_12_name}Wythuin</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.4393" build="0.5087" key="000B4405807010093586572B40753C33983B74857E715B17ACD18D517535345605A876560A68B318000000000000000000000000000000000000000015441083"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="Politician" value="6"/>
                <Trait id="Manager" value="4"/>
                <Trait id="Honor" value="-1"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Calculating" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_5_13 -->
    <xsl:template match="NPCCharacter[@id='lord_5_13']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_5_13_name}Muinser</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.6324" build="0.8677" key="000044034000238A2972092942746CA81A1B74BB8451FE5C76679B788227362104BD90460B8AE336000000000000000000000000000000000000000039788040"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="BalancedFightingSkills" value="8"/>
                <Trait id="Commander" value="8"/>
                <Trait id="Politician" value="6"/>
                <Trait id="Manager" value="8"/>
                <Trait id="Valor" value="1"/>
                <Trait id="Calculating" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_5_131 -->
    <xsl:template match="NPCCharacter[@id='lord_5_131']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_5_131_name}Rath</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0" build="0" key="00006C07003C2153AA7896C4AC46539AAAB7C56E6B973AAAA85DEA793BB4C45C01F006890F76754B0000000000000000000000000000000000000000013C6044"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="BalancedFightingSkills" value="6"/>
                <Trait id="Commander" value="8"/>
                <Trait id="Politician" value="8"/>
                <Trait id="Manager" value="6"/>
                <Trait id="Valor" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_5_15 -->
    <xsl:template match="NPCCharacter[@id='lord_5_15']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_5_15_name}Pryndor</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.7362" build="0.6653" key="0000600700001383AB62191815359CA8721876941F21B4384969AC5A8215275203AAB0350A57868200000000000000000000000000000000000000006F483042"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="BalancedFightingSkills" value="8"/>
                <Trait id="Commander" value="4"/>
                <Trait id="Politician" value="10"/>
                <Trait id="Manager" value="8"/>
                <Trait id="Valor" value="1"/>
                <Trait id="Mercy" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_5_14 -->
    <xsl:template match="NPCCharacter[@id='lord_5_14']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_5_14_name}Luichan</xsl:attribute>
            <xsl:attribute name="default_group">Ranged</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.7876" build="0.6364" key="000030058000000189620C2443356B748A587DDD9DB1A81478979D548215430103CAC0350C495C1500000000000000000000000000000000000000006B904040"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="BalancedFightingSkills" value="8"/>
                <Trait id="Commander" value="2"/>
                <Trait id="Politician" value="6"/>
                <Trait id="Manager" value="10"/>
                <Trait id="Honor" value="1"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Calculating" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_5_16 -->
    <xsl:template match="NPCCharacter[@id='lord_5_16']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_5_16_name}Aeron</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.7814" build="0.7805" key="00015C02C0003A145A600A1A4572AAC767467AF814019D8867999F04822B2A42048CC04508953C42000000000000000000000000000000000000000003008082"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="BalancedFightingSkills" value="5"/>
                <Trait id="Commander" value="6"/>
                <Trait id="Politician" value="6"/>
                <Trait id="Manager" value="8"/>
                <Trait id="Valor" value="1"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Calculating" value="-1"/>
                <Trait id="Honor" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_5_17 -->
    <xsl:template match="NPCCharacter[@id='lord_5_17']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_5_17_name}Aradwyr</xsl:attribute>
            <xsl:attribute name="default_group">Ranged</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.67" build="0.8069" key="00050005800013481D61091C75439996538775CE780133A738967B5982154B12047CA04607449413000000000000000000000000000000000000000002F05100"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="BalancedFightingSkills" value="5"/>
                <Trait id="Commander" value="2"/>
                <Trait id="Politician" value="8"/>
                <Trait id="Manager" value="10"/>
                <Trait id="Calculating" value="1"/>
                <Trait id="Mercy" value="1"/>
                <Trait id="Honor" value="-1"/>
                <Trait id="Valor" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_5_18 -->
    <xsl:template match="NPCCharacter[@id='lord_5_18']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_5_18_name}Branoc</xsl:attribute>
            <xsl:attribute name="default_group">Ranged</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.771" build="0.6715" key="00007804400024092E611B2B242363C6799677AE9511745858A75E37832A312605DAC0570D8A4A7400000000000000000000000000000000000000007DB830C0"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="BalancedFightingSkills" value="7"/>
                <Trait id="Commander" value="4"/>
                <Trait id="Politician" value="2"/>
                <Trait id="Manager" value="4"/>
                <Trait id="Honor" value="1"/>
                <Trait id="Valor" value="1"/>
                <Trait id="Calculating" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_5_19 -->
    <xsl:template match="NPCCharacter[@id='lord_5_19']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_5_19_name}Fenagan</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.6005" build="0.6567" key="00052807000004432871092255527C67584A76DC34C1D85475798E598229351302DA90240D68175300000000000000000000000000000000000000006FC02082"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="BalancedFightingSkills" value="9"/>
                <Trait id="Commander" value="4"/>
                <Trait id="Politician" value="8"/>
                <Trait id="Manager" value="8"/>
                <Trait id="Valor" value="1"/>
                <Trait id="Honor" value="1"/>
                <Trait id="Mercy" value="1"/>
                <Trait id="Calculating" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_5_20 -->
    <xsl:template match="NPCCharacter[@id='lord_5_20']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_5_20_name}Siaramus</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.7857" build="0.893" key="00011002C00020082B722C3422645D877577756D05415DB7855AA713832A074102BDC0240B878444000000000000000000000000000000000000000017800100"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="BalancedFightingSkills" value="7"/>
                <Trait id="Commander" value="4"/>
                <Trait id="Politician" value="2"/>
                <Trait id="Manager" value="4"/>
                <Trait id="Calculating" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_5_21 -->
    <xsl:template match="NPCCharacter[@id='lord_5_21']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_5_21_name}Carfyd</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.7257" build="0.8089" key="00025C07000024CAB77216196072AAE6728B78827641ED973887AC168234162503ACB0350A99E955000000000000000000000000000000000000000005405082"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="BalancedFightingSkills" value="6"/>
                <Trait id="Commander" value="4"/>
                <Trait id="Politician" value="6"/>
                <Trait id="Manager" value="8"/>
                <Trait id="Honor" value="1"/>
                <Trait id="Generosity" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_5_22 -->
    <xsl:template match="NPCCharacter[@id='lord_5_22']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_5_22_name}Fiarad</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.7901" build="0.7686" key="00050C02C0001549CD71192B144364956988797B4E51924558999A068314332605CCC0570C673752000000000000000000000000000000000000000023902004"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="BalancedFightingSkills" value="6"/>
                <Trait id="Commander" value="4"/>
                <Trait id="Politician" value="6"/>
                <Trait id="Manager" value="8"/>
                <Trait id="Honor" value="1"/>
                <Trait id="Generosity" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>


    <!-- ============================================== -->
    <!-- Faction 6 - Khuzait/Rhun -->
    <!-- ============================================== -->

    <!-- lord_6_1 -->
    <xsl:template match="NPCCharacter[@id='lord_6_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_6_1_name}Zhamik Zhamian</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" key="0016FC0FC000274D9EBF61FFB762A46EA4959AF6F9A58E3DC8756149D64B84A8000C265309B1E944000000000000000000000000000000000000000000F07080"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="CavalryFightingSkills" value="8"/>
                <Trait id="Commander" value="7"/>
                <Trait id="Manager" value="10"/>
                <Trait id="Politician" value="10"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Generosity" value="0"/>
                <Trait id="Mercy" value="-2"/>
                <Trait id="Honor" value="1"/>
                <Trait id="Calculating" value="1"/>
                <Trait id="Egalitarian" value="-1"/>
                <Trait id="Oligarchic" value="2"/>
                <Trait id="Authoritarian" value="2"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_6_2 -->
    <xsl:template match="NPCCharacter[@id='lord_6_2']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_6_2_name}Maraia</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0" build="0.4769" key="0005C00CC0FC100ACF976462A77185200E018468F0EF0103B6830C71720A23D40517065601101915000000000000000000000000000000000000000000240000"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="Politician" value="8"/>
                <Trait id="Manager" value="4"/>
                <Trait id="Mercy" value="1"/>
                <Trait id="Valor" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_6_3 -->
    <xsl:template match="NPCCharacter[@id='lord_6_3']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_6_3_name}Bagai</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.9067" build="0.8765" key="001CA00B40002505A27021394CB83B284566793CD6CE760869ABA623872A251306BDE0670B5556350000000000000000000000000000000000000000256820C1"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="BalancedFightingSkills" value="7"/>
                <Trait id="Commander" value="2"/>
                <Trait id="Politician" value="10"/>
                <Trait id="Manager" value="2"/>
                <Trait id="Honor" value="1"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Mercy" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_6_4 -->
    <xsl:template match="NPCCharacter[@id='lord_6_4']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_6_4_name}GÃ»rtilm Salurian</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.054" build="0.4383" key="0011D40FDA002001E9761DA0AD922600CE626C60F0EF0710F7DA1E1A740E10510847168904634C0A000000000000000000000000000000000000000034944083"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="HorseArcherFightingSkills" value="2"/>
                <Trait id="Commander" value="3"/>
                <Trait id="Manager" value="3"/>
                <Trait id="Politician" value="10"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Generosity" value="-1"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Honor" value="0"/>
                <Trait id="Calculating" value="0"/>
                <Trait id="Egalitarian" value="-2"/>
                <Trait id="Oligarchic" value="2"/>
                <Trait id="Authoritarian" value="2"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_6_5 -->
    <xsl:template match="NPCCharacter[@id='lord_6_5']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_6_5_name}Irbo Nikathian</xsl:attribute>
            <xsl:attribute name="default_group">HorseArcher</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.3531" build="0.4722" key="0015940B402C3754C39748898767B25A78E6B7968A3D58947935C86A959177790987509A0887B667000000000000000000000000000000000000000067183040"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="HorseArcherFightingSkills" value="1"/>
                <Trait id="Commander" value="3"/>
                <Trait id="Manager" value="10"/>
                <Trait id="Politician" value="9"/>
                <Trait id="Valor" value="1"/>
                <Trait id="Generosity" value="0"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Honor" value="1"/>
                <Trait id="Calculating" value="2"/>
                <Trait id="Egalitarian" value="0"/>
                <Trait id="Oligarchic" value="2"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_6_51 -->
    <xsl:template match="NPCCharacter[@id='lord_6_51']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_6_51_name}Khada</xsl:attribute>
            <xsl:attribute name="default_group">HorseArcher</xsl:attribute>
            <face>
                <BodyProperties version="4" key="001C840CC7FC03896DACA8926B35885A859C14A35794A7B4D6698187A2756C3C000C5534099977690000037F0000000F00000037049140010000000000788000"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="BalancedFightingSkills" value="4"/>
                <Trait id="Commander" value="8"/>
                <Trait id="Politician" value="6"/>
                <Trait id="Manager" value="4"/>
                <Trait id="Valor" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_6_6 -->
    <xsl:template match="NPCCharacter[@id='lord_6_6']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_6_6_name}Suran</xsl:attribute>
            <xsl:attribute name="default_group">HorseArcher</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.195" build="0.2239" key="001ED40D80FC06C67988B899C63C8B19698954BC6B89698827479497A849D99900633603067B8965000000000000000000000000000000000000000070CC2103"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="BalancedFightingSkills" value="6"/>
                <Trait id="Commander" value="8"/>
                <Trait id="Politician" value="10"/>
                <Trait id="Manager" value="10"/>
                <Trait id="Valor" value="-1"/>
                <Trait id="Generosity" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_6_7 -->
    <xsl:template match="NPCCharacter[@id='lord_6_7']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_6_7_name}Chaghan</xsl:attribute>
            <xsl:attribute name="default_group">HorseArcher</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.8289" build="0.9085" key="001C9808800036519270212A4BB7076824B8763BDAAF7B916A9A1B238431A313026EC024064567A500000000000000000000000000000000000000005AD84005"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="BalancedFightingSkills" value="7"/>
                <Trait id="Commander" value="2"/>
                <Trait id="Politician" value="8"/>
                <Trait id="Manager" value="10"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Honor" value="1"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Calculating" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_6_8 -->
    <xsl:template match="NPCCharacter[@id='lord_6_8']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_6_8_name}Esur</xsl:attribute>
            <xsl:attribute name="default_group">HorseArcher</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.8457" build="0.7043" key="0016B00880002591A57052353CC733A81534763CD7EF7456798B483684294631076BD0780656661600000000000000000000000000000000000000004EC83044"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="BalancedFightingSkills" value="7"/>
                <Trait id="Commander" value="4"/>
                <Trait id="Politician" value="10"/>
                <Trait id="Manager" value="10"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Valor" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_6_81 -->
    <xsl:template match="NPCCharacter[@id='lord_6_81']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_6_81_name}Nayantai</xsl:attribute>
            <xsl:attribute name="default_group">HorseArcher</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.8507" build="0.6256" key="0016E40A000025C8657022264CB7032826D5774CC6DE541B6ACA1B21852589410449D04604556705000000000000000000000000000000000000000046882005"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="BalancedFightingSkills" value="7"/>
                <Trait id="Commander" value="8"/>
                <Trait id="Politician" value="4"/>
                <Trait id="Manager" value="4"/>
                <Trait id="Valor" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_6_9 -->
    <xsl:template match="NPCCharacter[@id='lord_6_9']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_6_9_name}Temun</xsl:attribute>
            <xsl:attribute name="default_group">HorseArcher</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.8812" build="0.7079" key="001CD80B40003709647023132CB71B380645780ED9CF69777A9D9525881A9224056BD0560645665700000000000000000000000000000000000000000AD030C2"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="BalancedFightingSkills" value="7"/>
                <Trait id="Commander" value="8"/>
                <Trait id="Politician" value="4"/>
                <Trait id="Manager" value="4"/>
                <Trait id="Honor" value="1"/>
                <Trait id="Generosity" value="-1"/>
                <Trait id="Calculating" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_6_10 -->
    <xsl:template match="NPCCharacter[@id='lord_6_10']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_6_10_name}Alijin</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" key="001CC40FC03C1004E586A65AB6332A27A66555BCBECB2AA6B87BAC8B8567A764000639A302628966000000000000000000000000000000000000000000F030C2"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="Politician" value="2"/>
                <Trait id="Manager" value="10"/>
                <Trait id="Generosity" value="-1"/>
                <Trait id="Calculating" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_6_101 -->
    <xsl:template match="NPCCharacter[@id='lord_6_101']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_6_101_name}Bolat</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0011D40FC0CC300BF17616D8AD922604EE116506F7EF8110E0C91E3974071051000776030000120E0000000000000000000000000000000000000000340C2083"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="Politician" value="6"/>
                <Trait id="Manager" value="6"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Calculating" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_6_11 -->
    <xsl:template match="NPCCharacter[@id='lord_6_11']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_6_11_name}Yana</xsl:attribute>
            <xsl:attribute name="default_group">HorseArcher</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0005FC0FDECC1004E9763E11D7E1A5309F306507F3EB0605C1BD1E06710F01F20167761406035305000000000000000000000000000000000000000034C40083"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="BalancedFightingSkills" value="4"/>
                <Trait id="Commander" value="4"/>
                <Trait id="Politician" value="6"/>
                <Trait id="Manager" value="4"/>
                <Trait id="Calculating" value="1"/>
                <Trait id="Valor" value="1"/>
                <Trait id="Mercy" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_6_12 -->
    <xsl:template match="NPCCharacter[@id='lord_6_12']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_6_12_name}Abagai</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0011FC0FC0B4300AF97613A1E4410600CF306C0A60EF0500F3B81E14741610460167761406016000000000000000000000000000000000000000000034C42083"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="BalancedFightingSkills" value="6"/>
                <Trait id="Commander" value="10"/>
                <Trait id="Politician" value="8"/>
                <Trait id="Manager" value="4"/>
                <Trait id="Calculating" value="-1"/>
                <Trait id="Valor" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_6_13 -->
    <xsl:template match="NPCCharacter[@id='lord_6_13']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_6_13_name}Bortu</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.8513" build="0.749" key="0011B80CC0002652917042372BB72A784427765EE8EF61166CAD772283331341068BD068084567D5000000000000000000000000000000000000000003084002"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="BalancedFightingSkills" value="5"/>
                <Trait id="Commander" value="2"/>
                <Trait id="Politician" value="6"/>
                <Trait id="Manager" value="10"/>
                <Trait id="Mercy" value="1"/>
                <Trait id="Valor" value="-1"/>
                <Trait id="Calculating" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_6_15 -->
    <xsl:template match="NPCCharacter[@id='lord_6_15']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_6_15_name}Oragur</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.9087" build="0.8402" key="0013F40CC0001550857042184BB725983516771EC6CE3A17798D571388226925068DE067084577C700000000000000000000000000000000000000004F0040C1"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="BalancedFightingSkills" value="6"/>
                <Trait id="Commander" value="2"/>
                <Trait id="Politician" value="6"/>
                <Trait id="Manager" value="8"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Generosity" value="-1"/>
                <Trait id="Honor" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_6_16 -->
    <xsl:template match="NPCCharacter[@id='lord_6_16']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_6_16_name}Akvoth Karmian</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.805" build="0.6475" key="0016E00B400006D3507044293BC820482534773EE5DE57546D6B291183394315056AC05706556766000000000000000000000000000000000000000028C82104"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="CavalryFightingSkills" value="8"/>
                <Trait id="Commander" value="4"/>
                <Trait id="Manager" value="4"/>
                <Trait id="Politician" value="9"/>
                <Trait id="Valor" value="1"/>
                <Trait id="Generosity" value="-1"/>
                <Trait id="Mercy" value="-2"/>
                <Trait id="Honor" value="0"/>
                <Trait id="Calculating" value="1"/>
                <Trait id="Egalitarian" value="-2"/>
                <Trait id="Oligarchic" value="2"/>
                <Trait id="Authoritarian" value="2"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_6_17 -->
    <xsl:template match="NPCCharacter[@id='lord_6_17']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_6_17_name}AmdÃ»r AmdÃ»rid</xsl:attribute>
            <xsl:attribute name="default_group">HorseArcher</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.8543" build="0.714" key="0015B8088000084AB37043164CC705582586776DF7CE17557B7B4A03851B7804026BD02406457607000000000000000000000000000000000000000050C84080"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="HorseArcherFightingSkills" value="9"/>
                <Trait id="Commander" value="10"/>
                <Trait id="Manager" value="3"/>
                <Trait id="Politician" value="3"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Generosity" value="0"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Honor" value="1"/>
                <Trait id="Calculating" value="2"/>
                <Trait id="Egalitarian" value="-1"/>
                <Trait id="Oligarchic" value="2"/>
                <Trait id="Authoritarian" value="2"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_6_18 -->
    <xsl:template match="NPCCharacter[@id='lord_6_18']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_6_18_name}LÃ»thkan Khundolar</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.9162" build="0.6798" key="001A980880000809737023143BB71B184445782FD9BF34777B8B47148025DA12038AE03508546644000000000000000000000000000000000000000035102005"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="KnightFightingSkills" value="3"/>
                <Trait id="Commander" value="3"/>
                <Trait id="Manager" value="10"/>
                <Trait id="Politician" value="8"/>
                <Trait id="Valor" value="1"/>
                <Trait id="Generosity" value="-1"/>
                <Trait id="Mercy" value="0"/>
                <Trait id="Honor" value="1"/>
                <Trait id="Calculating" value="0"/>
                <Trait id="Egalitarian" value="-2"/>
                <Trait id="Oligarchic" value="2"/>
                <Trait id="Authoritarian" value="2"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_6_19 -->
    <xsl:template match="NPCCharacter[@id='lord_6_19']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_6_19_name}Vakheraltan Khundolar</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.802" build="0.6066" key="0012B80CC0003809737023195BB843782617793FD6DF2B416B7DA901812178230259C02405557705000000000000000000000000000000000000000078A83040"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="CavalryFightingSkills" value="8"/>
                <Trait id="Commander" value="7"/>
                <Trait id="Manager" value="3"/>
                <Trait id="Politician" value="4"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Generosity" value="0"/>
                <Trait id="Mercy" value="0"/>
                <Trait id="Honor" value="1"/>
                <Trait id="Calculating" value="1"/>
                <Trait id="Egalitarian" value="-1"/>
                <Trait id="Oligarchic" value="1"/>
                <Trait id="Authoritarian" value="2"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_6_20 -->
    <xsl:template match="NPCCharacter[@id='lord_6_20']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_6_20_name}Khurubra Mashakian</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.8073" build="0.7638" key="001CB80B400028906370542B2CC8201805177A3EE5DE68166BBA982582121925046BC0460655761500000000000000000000000000000000000000005EC03042"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="KnightFightingSkills" value="6"/>
                <Trait id="Commander" value="7"/>
                <Trait id="Manager" value="3"/>
                <Trait id="Politician" value="3"/>
                <Trait id="Valor" value="1"/>
                <Trait id="Generosity" value="0"/>
                <Trait id="Mercy" value="0"/>
                <Trait id="Honor" value="1"/>
                <Trait id="Calculating" value="1"/>
                <Trait id="Egalitarian" value="-2"/>
                <Trait id="Oligarchic" value="2"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_6_21 -->
    <xsl:template match="NPCCharacter[@id='lord_6_21']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_6_21_name}Molluk Illnoria</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.8419" build="0.8066" key="001C940AC00018CD647043256BB8222824887A4ED3EF7A517C8B7E1184213914047CD0460754671400000000000000000000000000000000000000004AE03041"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="BalancedFightingSkills" value="5"/>
                <Trait id="Commander" value="3"/>
                <Trait id="Politician" value="8"/>
                <Trait id="Manager" value="5"/>
                <Trait id="Valor" value="1"/>
                <Trait id="Calculating" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_6_22 -->
    <xsl:template match="NPCCharacter[@id='lord_6_22']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_6_22_name}Shakhal II Shakhalian</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.8474" build="0.8778" key="0017B00C000007488370531C3CC826884583782EFABE43717BBC7536892A5612037DD0350754675600000000000000000000000000000000000000004CE82001"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="CavalryFightingSkills" value="4"/>
                <Trait id="Commander" value="4"/>
                <Trait id="Politician" value="10"/>
                <Trait id="Manager" value="5"/>
                <Trait id="Valor" value="0"/>
                <Trait id="Generosity" value="0"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Honor" value="2"/>
                <Trait id="Calculating" value="2"/>
                <Trait id="Egalitarian" value="0"/>
                <Trait id="Oligarchic" value="0"/>
                <Trait id="Authoritarian" value="2"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_6_23 -->
    <xsl:template match="NPCCharacter[@id='lord_6_23']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_6_23_name}HÃ»z-MargÃ´z HÃ»z</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.4676" build="0.4954" key="0011F00CC0B423940888751A1794B0496099B698B7A4AD33C63BA5535D78938903677635065B7912000000000000000000000000000000000000000000CC3042"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="CavalryFightingSkills" value="8"/>
                <Trait id="Commander" value="10"/>
                <Trait id="Politician" value="6"/>
                <Trait id="Manager" value="6"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Generosity" value="0"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Honor" value="1"/>
                <Trait id="Calculating" value="2"/>
                <Trait id="Egalitarian" value="0"/>
                <Trait id="Oligarchic" value="0"/>
                <Trait id="Authoritarian" value="2"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

    <!-- lord_6_24 -->
    <xsl:template match="NPCCharacter[@id='lord_6_24']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group']"/>
            <xsl:attribute name="name">{=aom_lord_6_24_name}Nikath Adekig</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <face>
                <BodyProperties version="4" key="0016FC0FC000274D9EBF61FFB762A46EA4959AF6F9A58E3DC8756149D64B84A8000C265309B1E944000000000000000000000000000000000000000000F07080"/>
            </face>
            <skills>
            </skills>
            <Traits>
                <Trait id="CavalryFightingSkills" value="5"/>
                <Trait id="Commander" value="5"/>
                <Trait id="Manager" value="5"/>
                <Trait id="Politician" value="10"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Mercy" value="1"/>
                <Trait id="Honor" value="2"/>
                <Trait id="Calculating" value="2"/>
                <Trait id="Egalitarian" value="-1"/>
                <Trait id="Oligarchic" value="1"/>
                <Trait id="Authoritarian" value="2"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>


    <!-- lord_1_1_1 -->
    <xsl:template match="NPCCharacter[@id='lord_1_1_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_1_1_1_name}Eldith Grey-Claw</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.6213" build="0.5278" key="000DB0054F14200F777D66238AC2D652B789B895A5C5A788785C677564695D840068960306654796000000000000000000000000000000000000000042CC30C3"/>
            </face>
            <skills>
                <skill id="OneHanded" value="225"/>
                <skill id="TwoHanded" value="160"/>
                <skill id="Polearm" value="130"/>
                <skill id="Bow" value="120"/>
                <skill id="Crossbow" value="60"/>
                <skill id="Throwing" value="150"/>
                <skill id="Riding" value="50"/>
                <skill id="Athletics" value="210"/>
                <skill id="Crafting" value="80"/>
                <skill id="Scouting" value="150"/>
                <skill id="Tactics" value="170"/>
                <skill id="Charm" value="130"/>
                <skill id="Leadership" value="110"/>
                <skill id="Trade" value="80"/>
                <skill id="Steward" value="120"/>
                <skill id="Medicine" value="100"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="1"/>
                <Trait id="Generosity" value="0"/>
                <Trait id="Calculating" value="0"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="-1"/>
                <Trait id="Oligarchic" value="0"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_1_1_2 -->
    <xsl:template match="NPCCharacter[@id='lord_1_1_2']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_1_1_2_name}Sigga Wyrmbane</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.2051" build="0.2259" key="0018400781140004A7B67379AA5D43AB9A8574A83CB8794A545964E544A185CC0073360307A6835A00000000000000000000000000000000000000002EF44083"/>
            </face>
            <skills>
                <skill id="OneHanded" value="230"/>
                <skill id="TwoHanded" value="165"/>
                <skill id="Polearm" value="135"/>
                <skill id="Bow" value="115"/>
                <skill id="Crossbow" value="65"/>
                <skill id="Throwing" value="155"/>
                <skill id="Riding" value="55"/>
                <skill id="Athletics" value="215"/>
                <skill id="Crafting" value="85"/>
                <skill id="Scouting" value="145"/>
                <skill id="Tactics" value="175"/>
                <skill id="Charm" value="135"/>
                <skill id="Leadership" value="115"/>
                <skill id="Trade" value="85"/>
                <skill id="Steward" value="125"/>
                <skill id="Medicine" value="95"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="0"/>
                <Trait id="Generosity" value="-1"/>
                <Trait id="Calculating" value="1"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="-1"/>
                <Trait id="Oligarchic" value="0"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_1_1_3 -->
    <xsl:template match="NPCCharacter[@id='lord_1_1_3']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_1_1_3_name}Sigrun Boarfang</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.3278" build="0.5208" key="0018400FCE2C300153BC5AEB293A86574795A8AAA94684B62987569589672E26007856030767353900000000000000000000000000000000000000002EDC20C3"/>
            </face>
            <skills>
                <skill id="OneHanded" value="230"/>
                <skill id="TwoHanded" value="165"/>
                <skill id="Polearm" value="135"/>
                <skill id="Bow" value="115"/>
                <skill id="Crossbow" value="65"/>
                <skill id="Throwing" value="155"/>
                <skill id="Riding" value="55"/>
                <skill id="Athletics" value="215"/>
                <skill id="Crafting" value="85"/>
                <skill id="Scouting" value="145"/>
                <skill id="Tactics" value="170"/>
                <skill id="Charm" value="135"/>
                <skill id="Leadership" value="60"/>
                <skill id="Trade" value="85"/>
                <skill id="Steward" value="125"/>
                <skill id="Medicine" value="95"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="0"/>
                <Trait id="Generosity" value="-1"/>
                <Trait id="Calculating" value="1"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="-1"/>
                <Trait id="Oligarchic" value="0"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_1_1_4 -->
    <xsl:template match="NPCCharacter[@id='lord_1_1_4']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_1_1_4_name}Thyra Bloodtusk</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.3278" build="0.5208" key="0018A40FCE2C000551C968762E626B3BBA56396A8CCE86366B478689697773B900785603072B7F7800000000000000000000000000000000000000002EDC1083"/>
            </face>
            <skills>
                <skill id="OneHanded" value="235"/>
                <skill id="TwoHanded" value="170"/>
                <skill id="Polearm" value="140"/>
                <skill id="Bow" value="110"/>
                <skill id="Crossbow" value="60"/>
                <skill id="Throwing" value="160"/>
                <skill id="Riding" value="50"/>
                <skill id="Athletics" value="220"/>
                <skill id="Crafting" value="85"/>
                <skill id="Scouting" value="145"/>
                <skill id="Tactics" value="175"/>
                <skill id="Charm" value="130"/>
                <skill id="Leadership" value="60"/>
                <skill id="Trade" value="80"/>
                <skill id="Steward" value="120"/>
                <skill id="Medicine" value="90"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="0"/>
                <Trait id="Generosity" value="-1"/>
                <Trait id="Calculating" value="0"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="-1"/>
                <Trait id="Oligarchic" value="0"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_1_1_5 -->
    <xsl:template match="NPCCharacter[@id='lord_1_1_5']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_1_1_5_name}Hilda Bonecrusher</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.6389" build="0.9861" key="00155C0FCA2C300D3C6B797D7726C92756836865445B388DC6588889AB94CCC500BFA6030B29C34A00000000000000000000000000000000000000002F7C4103"/>
            </face>
            <skills>
                <skill id="OneHanded" value="245"/>
                <skill id="TwoHanded" value="175"/>
                <skill id="Polearm" value="140"/>
                <skill id="Bow" value="110"/>
                <skill id="Crossbow" value="60"/>
                <skill id="Throwing" value="165"/>
                <skill id="Riding" value="50"/>
                <skill id="Athletics" value="225"/>
                <skill id="Crafting" value="85"/>
                <skill id="Scouting" value="140"/>
                <skill id="Tactics" value="170"/>
                <skill id="Charm" value="130"/>
                <skill id="Leadership" value="60"/>
                <skill id="Trade" value="80"/>
                <skill id="Steward" value="120"/>
                <skill id="Medicine" value="90"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="0"/>
                <Trait id="Generosity" value="-1"/>
                <Trait id="Calculating" value="0"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="-1"/>
                <Trait id="Oligarchic" value="0"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_1_1_6 -->
    <xsl:template match="NPCCharacter[@id='lord_1_1_6']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_1_1_6_name}Yrsa, the Winter Boar</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.4676" build="0.5023" key="0015840E4C2C300C48E753769785B8663E48599D438E731B1D578153584559560068760306B6967600000000000000000000000000000000000000002EBC00C3"/>
            </face>
            <skills>
                <skill id="OneHanded" value="230"/>
                <skill id="TwoHanded" value="165"/>
                <skill id="Polearm" value="135"/>
                <skill id="Bow" value="115"/>
                <skill id="Crossbow" value="65"/>
                <skill id="Throwing" value="155"/>
                <skill id="Riding" value="55"/>
                <skill id="Athletics" value="220"/>
                <skill id="Crafting" value="85"/>
                <skill id="Scouting" value="155"/>
                <skill id="Tactics" value="170"/>
                <skill id="Charm" value="135"/>
                <skill id="Leadership" value="60"/>
                <skill id="Trade" value="85"/>
                <skill id="Steward" value="125"/>
                <skill id="Medicine" value="95"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="0"/>
                <Trait id="Generosity" value="-1"/>
                <Trait id="Calculating" value="0"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="-1"/>
                <Trait id="Oligarchic" value="0"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_1_1_7 -->
    <xsl:template match="NPCCharacter[@id='lord_1_1_7']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_1_1_7_name}Freydis Oxmane</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.4676" build="0.4745" key="000A9006CA44300A9A99991D9C769875AD89C74564583655C8A8A9DD949AB9CA0067760306C46DA800000000000000000000000000000000000000002ED400C3"/>
            </face>
            <skills>
                <skill id="OneHanded" value="230"/>
                <skill id="TwoHanded" value="165"/>
                <skill id="Polearm" value="135"/>
                <skill id="Bow" value="115"/>
                <skill id="Crossbow" value="65"/>
                <skill id="Throwing" value="155"/>
                <skill id="Riding" value="55"/>
                <skill id="Athletics" value="215"/>
                <skill id="Crafting" value="85"/>
                <skill id="Scouting" value="145"/>
                <skill id="Tactics" value="170"/>
                <skill id="Charm" value="135"/>
                <skill id="Leadership" value="60"/>
                <skill id="Trade" value="85"/>
                <skill id="Steward" value="125"/>
                <skill id="Medicine" value="95"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="0"/>
                <Trait id="Generosity" value="-1"/>
                <Trait id="Calculating" value="1"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="-1"/>
                <Trait id="Oligarchic" value="0"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_1_1_8 -->
    <xsl:template match="NPCCharacter[@id='lord_1_1_8']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_1_1_8_name}Eira Shadowclaw</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="00165C000014000EE93966A9966848AA5746E9558867A392654767867CA775120197761309B278C50000000000000000000000000000000000000000593C30C3"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Politician" value="6"/>
                <Trait id="Manager" value="8"/>
                <Trait id="Generosity" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_1_1_9 -->
    <xsl:template match="NPCCharacter[@id='lord_1_1_9']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_1_1_9_name}Sifra Bonewalker</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0011980E40B400053A8CBE5B2B965C4467898ACEB6A65CBA147249983692C3950167761306B469D400000000000000000000000000000000000000005AD40083"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Politician" value="10"/>
                <Trait id="Manager" value="4"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Calculating" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_1_1_10 -->
    <xsl:template match="NPCCharacter[@id='lord_1_1_10']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_1_1_10_name}Haldis Redmist</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="000BEC0E51143006C8BA78AAC869CA6992CA8461387A78A63736488C67497CC9013776130357449200000000000000000000000000000000000000003C642084"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Politician" value="6"/>
                <Trait id="Manager" value="4"/>
                <Trait id="Mercy" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_1_1_11 -->
    <xsl:template match="NPCCharacter[@id='lord_1_1_11']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_1_1_11_name}Yrla Ghostpelt</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.4" build="0.4" key="0016280B40A0000DB8DDDB4B342988B1C25BA5786832765C997877AB8579697401B776130B78386A00000000000000000000000000000000000000001D7C00C3"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Politician" value="8"/>
                <Trait id="Manager" value="6"/>
                <Trait id="Mercy" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_1_1_15 -->
    <xsl:template match="NPCCharacter[@id='lord_1_1_15']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_1_1_15_name}Aelwyn Hawkeye</xsl:attribute>
            <xsl:attribute name="default_group">HorseArcher</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.2685" build="0.2662" key="00001004C9A0100D2BC272AA728143CDC46386D689785734584896A7987613AC00744603078AC199000000000000000000000000000000000000000042E41083"/>
            </face>
            <skills>
                <skill id="OneHanded" value="110"/>
                <skill id="TwoHanded" value="90"/>
                <skill id="Polearm" value="100"/>
                <skill id="Bow" value="235"/>
                <skill id="Crossbow" value="75"/>
                <skill id="Throwing" value="125"/>
                <skill id="Riding" value="215"/>
                <skill id="Athletics" value="185"/>
                <skill id="Crafting" value="80"/>
                <skill id="Scouting" value="170"/>
                <skill id="Tactics" value="165"/>
                <skill id="Charm" value="140"/>
                <skill id="Leadership" value="60"/>
                <skill id="Trade" value="85"/>
                <skill id="Steward" value="125"/>
                <skill id="Medicine" value="100"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="0"/>
                <Trait id="Generosity" value="-1"/>
                <Trait id="Calculating" value="1"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="0"/>
                <Trait id="Oligarchic" value="0"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_1_1_16 -->
    <xsl:template match="NPCCharacter[@id='lord_1_1_16']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_1_1_16_name}Brianna Wingdart</xsl:attribute>
            <xsl:attribute name="default_group">HorseArcher</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.2685" build="0.2662" key="00001004CAA000032B4A77BC993C89497B2786D689785734DA946A6D4229EA9300544603057983BB000000000000000000000000000000000000000042AC1103"/>
            </face>
            <skills>
                <skill id="OneHanded" value="110"/>
                <skill id="TwoHanded" value="90"/>
                <skill id="Polearm" value="100"/>
                <skill id="Bow" value="230"/>
                <skill id="Crossbow" value="75"/>
                <skill id="Throwing" value="125"/>
                <skill id="Riding" value="215"/>
                <skill id="Athletics" value="185"/>
                <skill id="Crafting" value="80"/>
                <skill id="Scouting" value="170"/>
                <skill id="Tactics" value="165"/>
                <skill id="Charm" value="140"/>
                <skill id="Leadership" value="160"/>
                <skill id="Trade" value="85"/>
                <skill id="Steward" value="125"/>
                <skill id="Medicine" value="100"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="0"/>
                <Trait id="Generosity" value="-1"/>
                <Trait id="Calculating" value="1"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="0"/>
                <Trait id="Oligarchic" value="0"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_1_27_1 -->
    <xsl:template match="NPCCharacter[@id='lord_1_27_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_1_27_1_name}Verina</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.2" build="0.2" key="0005B8071070000A987877861A2999B848987C68EA6BB7869423371443C8EA4801377613036AB66200000000000000000000000000000000000000005A6C3003"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Politician" value="8"/>
                <Trait id="Manager" value="6"/>
                <Trait id="Mercy" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_1_27_2 -->
    <xsl:template match="NPCCharacter[@id='lord_1_27_2']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_1_27_2_name}Throznak</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <xsl:attribute name="is_female">false</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.4" build="0.4" key="000B8802C0BC1005B9D8455AA96848594597AD77996A54B899D967BA74979778010008930FBA9A7900001105000030A30000001F000000000000000001683100"/>
            </face>
            <skills>
                <skill id="OneHanded" value="240"/>
                <skill id="TwoHanded" value="230"/>
                <skill id="Polearm" value="260"/>
                <skill id="Bow" value="200"/>
                <skill id="Crossbow" value="150"/>
                <skill id="Throwing" value="180"/>
                <skill id="Riding" value="250"/>
                <skill id="Athletics" value="290"/>
                <skill id="Crafting" value="250"/>
                <skill id="Scouting" value="300"/>
                <skill id="Tactics" value="290"/>
                <skill id="Charm" value="100"/>
                <skill id="Leadership" value="280"/>
                <skill id="Trade" value="50"/>
                <skill id="Steward" value="250"/>
                <skill id="Medicine" value="180"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="-2"/>
                <Trait id="Generosity" value="-1"/>
                <Trait id="Calculating" value="2"/>
                <Trait id="Mercy" value="-2"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="-1"/>
                <Trait id="Oligarchic" value="1"/>
                <Trait id="Authoritarian" value="2"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_1_27_3 -->
    <xsl:template match="NPCCharacter[@id='lord_1_27_3']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_1_27_3_name}Vasilia</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.2" build="0.3" key="000B8802C0BC1005B9D8455AA96848594597AD77996A54B899D967BA74979778010008930FBA9A7900001105000030A30000001F000000000000000001683100"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="HorseArcherFightingSkills" value="3"/>
                <Trait id="Commander" value="4"/>
                <Trait id="Politician" value="2"/>
                <Trait id="Manager" value="2"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Calculating" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_1_47_1 -->
    <xsl:template match="NPCCharacter[@id='lord_1_47_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_1_47_1_name}Mina</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="001CD00FD114000AE46398467C698988876DB2B863D57A6D87B96C58967BE7660187761308364A49000000000000000000000000000000000000000019142003"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Politician" value="6"/>
                <Trait id="Manager" value="4"/>
                <Trait id="Honor" value="-1"/>
                <Trait id="Valor" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_1_47_2 -->
    <xsl:template match="NPCCharacter[@id='lord_1_47_2']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_1_47_2_name}Casyrea</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="001C880A111400012857DAA3C88BD254869B9BB7C7796796A6987253285773960137761303A670BD00000000000000000000000000000000000000005A741083"/>
            </face>
            <skills/>
            <Traits/>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_1_47_3 -->
    <xsl:template match="NPCCharacter[@id='lord_1_47_3']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_1_47_3_name}Colambea</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="00115C0A13702007997A77A685B88487865116663789A798A6A897926887369901A776130A8B49B500000000000000000000000000000000000000001F543083"/>
            </face>
            <skills/>
            <Traits/>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_1_49_1 -->
    <xsl:template match="NPCCharacter[@id='lord_1_49_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_1_49_1_name}Tristania</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0000200FD170300DB38B674C24E68B77237687A658475B47BC78592D78C593BB01C776130C2C8799000000000000000000000000000000000000000031840003"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Commander" value="8"/>
                <Trait id="Politician" value="8"/>
                <Trait id="Manager" value="10"/>
                <Trait id="Valor" value="1"/>
                <Trait id="Mercy" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_1_49_2 -->
    <xsl:template match="NPCCharacter[@id='lord_1_49_2']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_1_49_2_name}Gordiana</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0011B00CD2A03006743ADC795BA745B786DABD951B876537A8C6AA5B96B893590167761306485E6600000000000000000000000000000000000000004ACC00C3"/>
            </face>
            <skills/>
            <Traits/>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_1_1_12 -->
    <xsl:template match="NPCCharacter[@id='lord_1_1_12']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_1_1_12_name}Freya Clawrend</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.3171" build="0.331" key="0008780E42FC30076B9BCC4B879C2322B95787ECA9B959838E7C94A8754CD83800955603096BB79F000000000000000000000000000000000000000043340083"/>
            </face>
            <skills>
                <skill id="OneHanded" value="130"/>
                <skill id="TwoHanded" value="230"/>
                <skill id="Polearm" value="110"/>
                <skill id="Bow" value="100"/>
                <skill id="Crossbow" value="60"/>
                <skill id="Throwing" value="120"/>
                <skill id="Riding" value="55"/>
                <skill id="Athletics" value="215"/>
                <skill id="Crafting" value="90"/>
                <skill id="Scouting" value="145"/>
                <skill id="Tactics" value="170"/>
                <skill id="Charm" value="140"/>
                <skill id="Leadership" value="60"/>
                <skill id="Trade" value="85"/>
                <skill id="Steward" value="125"/>
                <skill id="Medicine" value="95"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="0"/>
                <Trait id="Generosity" value="0"/>
                <Trait id="Calculating" value="0"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="0"/>
                <Trait id="Oligarchic" value="0"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_1_1_13 -->
    <xsl:template match="NPCCharacter[@id='lord_1_1_13']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_1_1_13_name}Gundrun Ironpaw</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.3171" build="0.2245" key="0008E00FC8FC20087589847271756C397A5E664B96794B555718B95CE9C357830083560308938B28000000000000000000000000000000000000000043043083"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Politician" value="10"/>
                <Trait id="Manager" value="8"/>
                <Trait id="Calculating" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_1_1_14 -->
    <xsl:template match="NPCCharacter[@id='lord_1_1_14']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_1_1_14_name}Morgith</xsl:attribute>
            <xsl:attribute name="default_group">Ranged</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.2685" build="0.2662" key="00001007835810122BC272AA728143CDC46386D689785734584896A7987613AC00744603078AC199000000000000000000000000000000000000000042E41083"/>
            </face>
            <skills>
                <skill id="OneHanded" value="115"/>
                <skill id="TwoHanded" value="105"/>
                <skill id="Polearm" value="100"/>
                <skill id="Bow" value="235"/>
                <skill id="Crossbow" value="75"/>
                <skill id="Throwing" value="125"/>
                <skill id="Riding" value="50"/>
                <skill id="Athletics" value="205"/>
                <skill id="Crafting" value="80"/>
                <skill id="Scouting" value="155"/>
                <skill id="Tactics" value="165"/>
                <skill id="Charm" value="140"/>
                <skill id="Leadership" value="60"/>
                <skill id="Trade" value="85"/>
                <skill id="Steward" value="120"/>
                <skill id="Medicine" value="100"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="0"/>
                <Trait id="Generosity" value="-1"/>
                <Trait id="Calculating" value="1"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="-1"/>
                <Trait id="Oligarchic" value="0"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_1_1_17 -->
    <xsl:template match="NPCCharacter[@id='lord_1_1_17']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_1_1_17_name}Branoc Feathershaft</xsl:attribute>
            <xsl:attribute name="default_group">Ranged</xsl:attribute>
            <xsl:attribute name="is_female">false</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.2685" build="0.2662" key="00087C07835809538579765AA95984A5844298548A747A34548663635992CD7B0074460307A8E449000000000000000000000000000000000000000042E41102"/>
            </face>
            <skills>
                <skill id="OneHanded" value="115"/>
                <skill id="TwoHanded" value="105"/>
                <skill id="Polearm" value="100"/>
                <skill id="Bow" value="235"/>
                <skill id="Crossbow" value="75"/>
                <skill id="Throwing" value="125"/>
                <skill id="Riding" value="50"/>
                <skill id="Athletics" value="205"/>
                <skill id="Crafting" value="80"/>
                <skill id="Scouting" value="155"/>
                <skill id="Tactics" value="165"/>
                <skill id="Charm" value="130"/>
                <skill id="Leadership" value="60"/>
                <skill id="Trade" value="85"/>
                <skill id="Steward" value="120"/>
                <skill id="Medicine" value="90"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="0"/>
                <Trait id="Generosity" value="-1"/>
                <Trait id="Calculating" value="1"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="-1"/>
                <Trait id="Oligarchic" value="0"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_NE7_u -->
    <xsl:template match="NPCCharacter[@id='lord_NE7_u']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_NE7_u_name}Haldric the Black Fang</xsl:attribute>
            <xsl:attribute name="default_group">HorseArcher</xsl:attribute>
            <xsl:attribute name="is_female">false</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0000D40E4000228BED6277444258C875446A17B9ED8932923B61997568A479EB0000047303395E480000000000000FFE00000000000000000000000000004000"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="BalancedFightingSkills" value="7"/>
                <Trait id="Commander" value="4"/>
                <Trait id="Politician" value="8"/>
                <Trait id="Manager" value="6"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Calculating" value="1"/>
                <Trait id="Mercy" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_1_40_1 -->
    <xsl:template match="NPCCharacter[@id='lord_1_40_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_1_40_1_name}Catella</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">True</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.4" build="0.4" key="000BAC089200100DB976648E6774B835537D86629511323BDCB177278A84F667017776130748B49500000000000000000000000000000000000000003EFC4003"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Politician" value="10"/>
                <Trait id="Manager" value="2"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Calculating" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_1_46_1 -->
    <xsl:template match="NPCCharacter[@id='lord_1_46_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_1_46_1_name}Seorgys</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">false</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.4" build="0.5" key="000B500A1114300BB69199998669E37568754C66A53693B2BA7A9855C96751D601677613062B368C000000000000000000000000000000000000000040CC4083"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="BalancedFightingSkills" value="4"/>
                <Trait id="Commander" value="8"/>
                <Trait id="Politician" value="10"/>
                <Trait id="Manager" value="4"/>
                <Trait id="Valor" value="-1"/>
                <Trait id="Honor" value="-1"/>
                <Trait id="Mercy" value="1"/>
                <Trait id="Calculating" value="1"/>
                <Trait id="Egalitarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_1_45_1 -->
    <xsl:template match="NPCCharacter[@id='lord_1_45_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_1_45_1_name}Vanyalos</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">True</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.0023" build="0.5116" key="000AF80010FC0005B0801020FC60E000A74780010BBD1001654A350A7FFE930400A806030A97B6481F6410605020004000000000000017670000000045441003"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Politician" value="6"/>
                <Trait id="Manager" value="8"/>
                <Trait id="Honor" value="1"/>
                <Trait id="Generosity" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_1_45_2 -->
    <xsl:template match="NPCCharacter[@id='lord_1_45_2']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_1_45_2_name}Brandir</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">false</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.1782" build="0.4236" key="0010D808C00010063E8415A71000FF529EC9833001BCE003075810017F905E52009636030980A70600000000000000000000000000000000000000003B2C4002"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="BalancedFightingSkills" value="4"/>
                <Trait id="Commander" value="6"/>
                <Trait id="Politician" value="6"/>
                <Trait id="Manager" value="8"/>
                <Trait id="Honor" value="1"/>
                <Trait id="Generosity" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_1_45_3 -->
    <xsl:template match="NPCCharacter[@id='lord_1_45_3']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_1_45_3_name}Borlong</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">false</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.4236" build="0.4236" key="000AD806C00011420E8015A716F0F012E910830001B90601675810067F9A5C04007666030720804000000000000000000000000000000000000000003AF43002"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="BalancedFightingSkills" value="4"/>
                <Trait id="Commander" value="6"/>
                <Trait id="Politician" value="6"/>
                <Trait id="Manager" value="8"/>
                <Trait id="Honor" value="1"/>
                <Trait id="Generosity" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_1_57_1 -->
    <xsl:template match="NPCCharacter[@id='lord_1_57_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_1_57_1_name}Sophalia</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="000BD80B5114300AC8832858A669275A3AB4CAA26D66686547C4969575DA72990177761307A39755000000000000000000000000000000000000000042E40003"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Politician" value="4"/>
                <Trait id="Manager" value="6"/>
                <Trait id="Mercy" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_1_57_2 -->
    <xsl:template match="NPCCharacter[@id='lord_1_57_2']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_1_57_2_name}Jephalia</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="00003C02D0CC200FCECB7755385C79B87764EA7A87439E635759266A4A5A653B01177613012CB56100000000000000000000000000000000000000004A344043"/>
            </face>
            <skills/>
            <Traits/>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_1_52_1 -->
    <xsl:template match="NPCCharacter[@id='lord_1_52_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_1_52_1_name}Arador</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <xsl:attribute name="is_female">false</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.2269" build="0.7361" key="0008700E4000138897700694F0015167328081FFB2570E00756880008F96C80300AB36030A39320100000000000000000000000000000000000000003D3C1002"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="BalancedFightingSkills" value="6"/>
                <Trait id="Commander" value="3"/>
                <Trait id="Politician" value="9"/>
                <Trait id="Manager" value="7"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Mercy" value="0"/>
                <Trait id="Honor" value="2"/>
                <Trait id="Calculating" value="0"/>
                <Trait id="Egalitarian" value="0"/>
                <Trait id="Oligarchic" value="1"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_1_52_2 -->
    <xsl:template match="NPCCharacter[@id='lord_1_52_2']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_1_52_2_name}Arvedui</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <xsl:attribute name="is_female">false</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.3981" build="0.4861" key="0020FC0980001282248414071B75011484C884CF8169800109FA00017FF95EA2009766030971A00800000000000000000000000000000000000000003B2C4002"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="BalancedFightingSkills" value="6"/>
                <Trait id="Commander" value="3"/>
                <Trait id="Politician" value="9"/>
                <Trait id="Manager" value="7"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Mercy" value="0"/>
                <Trait id="Honor" value="2"/>
                <Trait id="Calculating" value="0"/>
                <Trait id="Egalitarian" value="0"/>
                <Trait id="Oligarchic" value="1"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_WE8_c -->
    <xsl:template match="NPCCharacter[@id='lord_WE8_c']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_WE8_c_name}Icratia</xsl:attribute>
            <xsl:attribute name="default_group">HorseArcher</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0005580B4000300DA55B8899724689763869956B7B86578578658A96886733790003000203B8789800001105000030A30000001F000000000000000000000000"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Politician" value="6"/>
                <Trait id="Manager" value="10"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Calculating" value="1"/>
                <Trait id="Egalitarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_WE8_u -->
    <xsl:template match="NPCCharacter[@id='lord_WE8_u']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_WE8_u_name}Varmyros</xsl:attribute>
            <xsl:attribute name="default_group">HorseArcher</xsl:attribute>
            <xsl:attribute name="is_female">false</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="000A80034014168D54C4BC37777515B7D845A4BA65A0A9BB59628895AB9747530077760307872A4A000000000000000000000000000000000000000052DC30C2"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="BalancedFightingSkills" value="7"/>
                <Trait id="Commander" value="10"/>
                <Trait id="Politician" value="8"/>
                <Trait id="Manager" value="10"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Calculating" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_1_73_1 -->
    <xsl:template match="NPCCharacter[@id='lord_1_73_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_1_73_1_name}Popilia</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0022CC0580CC100B5867B786DC6B9968B4367A58547AB48D79EAB69452B75745016776130655B895000000000000000000000000000000000000000030C40083"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Manager" value="8"/>
                <Trait id="Generosity" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_1_62_1 -->
    <xsl:template match="NPCCharacter[@id='lord_1_62_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_1_62_1_name}Arytha</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">True</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0011AC071100300FA554C37EA53BA67A8B6E7AA94D9BAE666B76EB5953835CAB01977613097C478900000000000000000000000000000000000000004D243043"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Politician" value="2"/>
                <Trait id="Manager" value="6"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Honor" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_1_30_1 -->
    <xsl:template match="NPCCharacter[@id='lord_1_30_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_1_30_1_name}Svala Redfang</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0011A40CC0B4300AA943DB689A336188B8984B56679A5AB8497A4CB9D8A6232501877613088C45970000000000000000000000000000000000000000350C1003"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Manager" value="6"/>
                <Trait id="Calculating" value="1"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_1_30_2 -->
    <xsl:template match="NPCCharacter[@id='lord_1_30_2']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_1_30_2_name}Callinia</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.2" build="0.3" key="00117C0E50CC200D73C3265E79C3998BBE65798569D73495C6D87BD3AA57B6D501C776130CD465380000000000000000000000000000000000000000358C20C3"/>
            </face>
            <skills/>
            <Traits/>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_1_30_3 -->
    <xsl:template match="NPCCharacter[@id='lord_1_30_3']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_1_30_3_name}Synesios</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">false</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.4" build="0.3" key="00117C0E50CC200D73C3265E79C3998BBE65798569D73495C6D87BD3AA57B6D501C776130CD465380000000000000000000000000000000000000000358C20C3"/>
            </face>
            <skills/>
            <Traits/>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_1_48_1 -->
    <xsl:template match="NPCCharacter[@id='lord_1_48_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_1_48_1_name}NazgÃ»l, the Tainted</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <xsl:attribute name="is_female">false</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.0116" build="1" key="0039F00FC000300010000010010100000001000000000100001011101000000000FF06030F000100000000000000000000000000000000000000000043F44142"/>
            </face>
            <skills>
                <skill id="OneHanded" value="305"/>
                <skill id="TwoHanded" value="295"/>
                <skill id="Polearm" value="290"/>
                <skill id="Bow" value="200"/>
                <skill id="Crossbow" value="150"/>
                <skill id="Throwing" value="180"/>
                <skill id="Riding" value="350"/>
                <skill id="Athletics" value="309"/>
                <skill id="Crafting" value="250"/>
                <skill id="Scouting" value="300"/>
                <skill id="Tactics" value="290"/>
                <skill id="Charm" value="100"/>
                <skill id="Leadership" value="295"/>
                <skill id="Trade" value="50"/>
                <skill id="Steward" value="250"/>
                <skill id="Medicine" value="180"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="-2"/>
                <Trait id="Generosity" value="-1"/>
                <Trait id="Calculating" value="2"/>
                <Trait id="Mercy" value="-2"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="-1"/>
                <Trait id="Oligarchic" value="1"/>
                <Trait id="Authoritarian" value="2"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_1_48_2 -->
    <xsl:template match="NPCCharacter[@id='lord_1_48_2']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_1_48_2_name}NazgÃ»l, the Shadow of Northmen</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <xsl:attribute name="is_female">false</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.0116" build="1" key="0039F00FC000300010000010010100000001000000000100001011101000000000FF06030F000100000000000000000000000000000000000000000043F44142"/>
            </face>
            <skills>
                <skill id="OneHanded" value="305"/>
                <skill id="TwoHanded" value="295"/>
                <skill id="Polearm" value="290"/>
                <skill id="Bow" value="200"/>
                <skill id="Crossbow" value="150"/>
                <skill id="Throwing" value="180"/>
                <skill id="Riding" value="350"/>
                <skill id="Athletics" value="309"/>
                <skill id="Crafting" value="250"/>
                <skill id="Scouting" value="300"/>
                <skill id="Tactics" value="290"/>
                <skill id="Charm" value="100"/>
                <skill id="Leadership" value="295"/>
                <skill id="Trade" value="50"/>
                <skill id="Steward" value="250"/>
                <skill id="Medicine" value="180"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="-2"/>
                <Trait id="Generosity" value="-1"/>
                <Trait id="Calculating" value="2"/>
                <Trait id="Mercy" value="-2"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="-1"/>
                <Trait id="Oligarchic" value="1"/>
                <Trait id="Authoritarian" value="2"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_1_48_3 -->
    <xsl:template match="NPCCharacter[@id='lord_1_48_3']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_1_48_3_name}NazgÃ»l, the Shadow of Umbar</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <xsl:attribute name="is_female">false</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.0116" build="1" key="0039F00FC000300010000010010100000001000000000100001011101000000000FF06030F000100000000000000000000000000000000000000000043F44142"/>
            </face>
            <skills>
                <skill id="OneHanded" value="305"/>
                <skill id="TwoHanded" value="295"/>
                <skill id="Polearm" value="290"/>
                <skill id="Bow" value="200"/>
                <skill id="Crossbow" value="150"/>
                <skill id="Throwing" value="180"/>
                <skill id="Riding" value="350"/>
                <skill id="Athletics" value="309"/>
                <skill id="Crafting" value="250"/>
                <skill id="Scouting" value="300"/>
                <skill id="Tactics" value="290"/>
                <skill id="Charm" value="100"/>
                <skill id="Leadership" value="305"/>
                <skill id="Trade" value="50"/>
                <skill id="Steward" value="250"/>
                <skill id="Medicine" value="180"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="-2"/>
                <Trait id="Generosity" value="-1"/>
                <Trait id="Calculating" value="2"/>
                <Trait id="Mercy" value="-2"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="-1"/>
                <Trait id="Oligarchic" value="1"/>
                <Trait id="Authoritarian" value="2"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_1_54_1 -->
    <xsl:template match="NPCCharacter[@id='lord_1_54_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_1_54_1_name}Constalia</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0000780450B40005274C088C838671B78588B5549A7B79D979386C9D75A83863016776130698556700000000000000000000000000000000000000003ACC4083"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Politician" value="2"/>
                <Trait id="Manager" value="10"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Calculating" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_1_55_1 -->
    <xsl:template match="NPCCharacter[@id='lord_1_55_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_1_55_1_name}Megethia</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="000B5C070058300454262B4CA6A899758988B348E4A3854B7B2BD7728796946401A776130A3359560000000000000000000000000000000000000000375410C3"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Manager" value="10"/>
                <Trait id="Mercy" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_1_63_1 -->
    <xsl:template match="NPCCharacter[@id='lord_1_63_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_1_63_1_name}Valaria</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.7" build="0.8" key="001C700CC0CC30106C969C364498D4369D8A57A9A772768983E88354BB2B1BCE0167761306D7CD8B000000000000000000000000000000000000000050DC4083"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="BalancedFightingSkills" value="3"/>
                <Trait id="Commander" value="4"/>
                <Trait id="Politician" value="6"/>
                <Trait id="Manager" value="6"/>
                <Trait id="Calculating" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_1_63_2 -->
    <xsl:template match="NPCCharacter[@id='lord_1_63_2']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_1_63_2_name}Comatasa</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="001C580892B400107792D84678A387599DAA67447A8C77C9D7D366699576C7CB0167761306D83BB700000000000000000000000000000000000000005CDC0003"/>
            </face>
            <skills/>
            <Traits/>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_1_63_3 -->
    <xsl:template match="NPCCharacter[@id='lord_1_63_3']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_1_63_3_name}Elidilea</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="00168C0CD3E4300B746DA4A836974259CA53A496D7AA6425822AA1429A5CACCA01677613063C365E00000000000000000000000000000000000000003CC42043"/>
            </face>
            <skills/>
            <Traits/>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_1_56_1 -->
    <xsl:template match="NPCCharacter[@id='lord_1_56_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_1_56_1_name}Gwenna</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0005240892FC1005B69C29549444BE8759679A7778478C8D78BA785C77A5C7880177761307D8873D000000000000000000000000000000000000000054F40083"/>
            </face>
            <skills>
                <skill id="OneHanded" value="140"/>
                <skill id="TwoHanded" value="120"/>
                <skill id="Polearm" value="260"/>
                <skill id="Bow" value="110"/>
                <skill id="Crossbow" value="70"/>
                <skill id="Throwing" value="130"/>
                <skill id="Riding" value="250"/>
                <skill id="Athletics" value="200"/>
                <skill id="Crafting" value="90"/>
                <skill id="Scouting" value="160"/>
                <skill id="Tactics" value="220"/>
                <skill id="Charm" value="200"/>
                <skill id="Leadership" value="50"/>
                <skill id="Trade" value="110"/>
                <skill id="Steward" value="210"/>
                <skill id="Medicine" value="100"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="1"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Calculating" value="1"/>
                <Trait id="Mercy" value="0"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="0"/>
                <Trait id="Oligarchic" value="1"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_1_56_2 -->
    <xsl:template match="NPCCharacter[@id='lord_1_56_2']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_1_56_2_name}Rustica</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0011440CD000000F85883C187A9326A926589A94B9889854776498DB88864ED701877613084688A80000000000000000000000000000000000000000411410C3"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Commander" value="2"/>
                <Trait id="Politician" value="2"/>
                <Trait id="Manager" value="4"/>
                <Trait id="Mercy" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_1_68_1 -->
    <xsl:template match="NPCCharacter[@id='lord_1_68_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_1_68_1_name}Silvina</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="00115C0890B42010C486966CA6A39396395EBC15947764737B762A7963D6584201A776130A5A596B00000000000000000000000000000000000000005B544003"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Manager" value="6"/>
                <Trait id="Valor" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_1_69_1 -->
    <xsl:template match="NPCCharacter[@id='lord_1_69_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_1_69_1_name}Areliana</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.3" build="0.4" key="000BBC0FC0143010E1789C45A6A4429352B8238BB9B683DBAB95A8778B89BAC501977613097656360000000000000000000000000000000000000000372410C3"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Manager" value="4"/>
                <Trait id="Generosity" value="-1"/>
                <Trait id="Calculating" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_1_69_2 -->
    <xsl:template match="NPCCharacter[@id='lord_1_69_2']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_1_69_2_name}Dorathila</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.3" build="0.4" key="0000900A10143006685D493BBC780A4686D18674A5379AA596B62D5A727BC485017776130799B93A000000000000000000000000000000000000000068FC4083"/>
            </face>
            <skills/>
            <Traits/>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_1_72_1 -->
    <xsl:template match="NPCCharacter[@id='lord_1_72_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_1_72_1_name}Viviana</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="00055C0E40CC3010AB37736CB777CA594C4C561A72B5767D2385A776BA71778B015776130579877A00000000000000000000000000000000000000003CAC40C3"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Politician" value="4"/>
                <Trait id="Manager" value="6"/>
                <Trait id="Generosity" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_SE8_c -->
    <xsl:template match="NPCCharacter[@id='lord_SE8_c']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_SE8_c_name}Itaria</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.7" key="0011B40B402410088733440E80468F1FFF4F9274C862B448941B5637963E5D3E000E08830B7247A6000000000000000000000000000000000000000000003101"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="BalancedFightingSkills" value="4"/>
                <Trait id="Commander" value="8"/>
                <Trait id="Politician" value="4"/>
                <Trait id="Manager" value="6"/>
                <Trait id="Valor" value="-1"/>
                <Trait id="Honor" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_1_74_1 -->
    <xsl:template match="NPCCharacter[@id='lord_1_74_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_1_74_1_name}Zena</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="00115C0FD1A0000E3A7C7A85CC64D75ABE9B4974647554498CAA97499766E5860147761304B85D0800000000000000000000000000000000000000005C943003"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Manager" value="4"/>
                <Trait id="Calculating" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_NE8_l -->
    <xsl:template match="NPCCharacter[@id='lord_NE8_l']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_NE8_l_name}Eadric, The Swift</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">false</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.6" build="0.75" key="00009C0FC0CC3286677834B57545557BA665A4BB733C359B45DA64699C84577801377613034C439C00000000000000000000000000000000000000006A644002"/>
            </face>
            <skills>
                <skill id="OneHanded" value="140"/>
                <skill id="TwoHanded" value="120"/>
                <skill id="Polearm" value="260"/>
                <skill id="Bow" value="110"/>
                <skill id="Crossbow" value="70"/>
                <skill id="Throwing" value="130"/>
                <skill id="Riding" value="250"/>
                <skill id="Athletics" value="200"/>
                <skill id="Crafting" value="90"/>
                <skill id="Scouting" value="160"/>
                <skill id="Tactics" value="220"/>
                <skill id="Charm" value="200"/>
                <skill id="Leadership" value="150"/>
                <skill id="Trade" value="110"/>
                <skill id="Steward" value="210"/>
                <skill id="Medicine" value="100"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="1"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Calculating" value="1"/>
                <Trait id="Mercy" value="0"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="0"/>
                <Trait id="Oligarchic" value="1"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_NE8_s -->
    <xsl:template match="NPCCharacter[@id='lord_NE8_s']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_NE8_s_name}Aeloria</xsl:attribute>
            <xsl:attribute name="default_group">HorseArcher</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.4" build="0.5" key="0000200FD0CC200E978EB857E594B2FE6929595C8998514499BBA261B3BB76B201377613037A459900000000000000000000000000000000000000006A6400C3"/>
            </face>
            <skills>
                <skill id="OneHanded" value="140"/>
                <skill id="TwoHanded" value="120"/>
                <skill id="Polearm" value="260"/>
                <skill id="Bow" value="110"/>
                <skill id="Crossbow" value="70"/>
                <skill id="Throwing" value="130"/>
                <skill id="Riding" value="250"/>
                <skill id="Athletics" value="200"/>
                <skill id="Crafting" value="90"/>
                <skill id="Scouting" value="160"/>
                <skill id="Tactics" value="220"/>
                <skill id="Charm" value="200"/>
                <skill id="Leadership" value="50"/>
                <skill id="Trade" value="110"/>
                <skill id="Steward" value="210"/>
                <skill id="Medicine" value="100"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="1"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Calculating" value="1"/>
                <Trait id="Mercy" value="0"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="0"/>
                <Trait id="Oligarchic" value="1"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_NE8_c1 -->
    <xsl:template match="NPCCharacter[@id='lord_NE8_c1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_NE8_c1_name}Cerdic, the Anvil</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">false</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0000D40FC0CC12C897B8079E76258849839B767CB9CA744999BBA2614817BB7801377613037A459900000000000000000000000000000000000000006A642102"/>
            </face>
            <skills>
                <skill id="OneHanded" value="140"/>
                <skill id="TwoHanded" value="120"/>
                <skill id="Polearm" value="260"/>
                <skill id="Bow" value="110"/>
                <skill id="Crossbow" value="70"/>
                <skill id="Throwing" value="130"/>
                <skill id="Riding" value="250"/>
                <skill id="Athletics" value="200"/>
                <skill id="Crafting" value="90"/>
                <skill id="Scouting" value="160"/>
                <skill id="Tactics" value="220"/>
                <skill id="Charm" value="200"/>
                <skill id="Leadership" value="50"/>
                <skill id="Trade" value="110"/>
                <skill id="Steward" value="210"/>
                <skill id="Medicine" value="100"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="1"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Calculating" value="1"/>
                <Trait id="Mercy" value="0"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="0"/>
                <Trait id="Oligarchic" value="1"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_NE8_c2 -->
    <xsl:template match="NPCCharacter[@id='lord_NE8_c2']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_NE8_c2_name}Morwen</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0000D40FD0CC100497B8079E76258849839B767CB9CA744999BBA2614817BB7801377613037A459900000000000000000000000000000000000000006A6420C3"/>
            </face>
            <skills>
                <skill id="OneHanded" value="140"/>
                <skill id="TwoHanded" value="120"/>
                <skill id="Polearm" value="260"/>
                <skill id="Bow" value="110"/>
                <skill id="Crossbow" value="70"/>
                <skill id="Throwing" value="130"/>
                <skill id="Riding" value="250"/>
                <skill id="Athletics" value="200"/>
                <skill id="Crafting" value="90"/>
                <skill id="Scouting" value="160"/>
                <skill id="Tactics" value="220"/>
                <skill id="Charm" value="200"/>
                <skill id="Leadership" value="50"/>
                <skill id="Trade" value="110"/>
                <skill id="Steward" value="210"/>
                <skill id="Medicine" value="100"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="1"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Calculating" value="1"/>
                <Trait id="Mercy" value="0"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="0"/>
                <Trait id="Oligarchic" value="1"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_NE9_l -->
    <xsl:template match="NPCCharacter[@id='lord_NE9_l']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_NE9_l_name}Valthor Scalecleaver</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">false</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.6" build="0.75" key="000AAC080000165AB989A184241CB80B537D86629511323BDCB177278A49B6A7007776030748B49500000000000000000000000000000000000000003EFC5002"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="BalancedFightingSkills" value="8"/>
                <Trait id="Commander" value="4"/>
                <Trait id="Politician" value="4"/>
                <Trait id="Manager" value="5"/>
                <Trait id="Valor" value="1"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Mercy" value="1"/>
                <Trait id="Honor" value="1"/>
                <Trait id="Calculating" value="0"/>
                <Trait id="Egalitarian" value="1"/>
                <Trait id="Oligarchic" value="0"/>
                <Trait id="Authoritarian" value="0"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_NE9_s -->
    <xsl:template match="NPCCharacter[@id='lord_NE9_s']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_NE9_s_name}Rurik Fanghew</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">false</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="000AAC0800001001B957635424174307567986659534566BDCB5774788493637008776030848B59500000000000000000000000000000000000000003F0C5002"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="BalancedFightingSkills" value="7"/>
                <Trait id="Commander" value="2"/>
                <Trait id="Politician" value="8"/>
                <Trait id="Manager" value="6"/>
                <Trait id="Valor" value="1"/>
                <Trait id="Calculating" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_NE9_d -->
    <xsl:template match="NPCCharacter[@id='lord_NE9_d']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_NE9_d_name}Sylva Serpent-Strike</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.6" build="0.5" key="00009C0FC0CC3286677834B57545557BA665A4BB733C359B45DA64699C84577801377613034C439C00000000000000000000000000000000000000006A644002"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Politician" value="8"/>
                <Trait id="Manager" value="6"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Calculating" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_WE9_l -->
    <xsl:template match="NPCCharacter[@id='lord_WE9_l']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_WE9_l_name}Duinhir, Lord of Morthond</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <xsl:attribute name="is_female">false</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.3588" build="0.4884" key="000A580E401403C201810D5A6209FC0C85E4826975040B007556440C6BC8A90200B756030B13BA9100000000000000000000000000000000000000003B643002"/>
            </face>
            <skills>
                <skill id="OneHanded" value="200"/>
                <skill id="TwoHanded" value="180"/>
                <skill id="Polearm" value="150"/>
                <skill id="Bow" value="250"/>
                <skill id="Crossbow" value="150"/>
                <skill id="Throwing" value="120"/>
                <skill id="Riding" value="150"/>
                <skill id="Athletics" value="150"/>
                <skill id="Crafting" value="80"/>
                <skill id="Scouting" value="200"/>
                <skill id="Tactics" value="200"/>
                <skill id="Charm" value="180"/>
                <skill id="Leadership" value="230"/>
                <skill id="Trade" value="100"/>
                <skill id="Steward" value="200"/>
                <skill id="Medicine" value="100"/>
                <skill id="Engineering" value="80"/>
                <skill id="Roguery" value="50"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="2"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Calculating" value="0"/>
                <Trait id="Mercy" value="1"/>
                <Trait id="Valor" value="2"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_WE9_u -->
    <xsl:template match="NPCCharacter[@id='lord_WE9_u']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_WE9_u_name}Euresa</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0000AC001170000E68844A7B5955A69256989B97A185847C36C937ABA3698C8C01977613097BD71A000000000000000000000000000000000000000061341003"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Politician" value="6"/>
                <Trait id="Manager" value="10"/>
                <Trait id="Calculating" value="1"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Egalitarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_WE9_u2 -->
    <xsl:template match="NPCCharacter[@id='lord_WE9_u2']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_WE9_u2_name}Torvasis</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">false</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0000AC001170000E68844A7B5955A69256989B97A185847C36C937ABA3698C8C01977613097BD71A000000000000000000000000000000000000000061341003"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="BalancedFightingSkills" value="6"/>
                <Trait id="Commander" value="4"/>
                <Trait id="Politician" value="4"/>
                <Trait id="Manager" value="3"/>
                <Trait id="Valor" value="-1"/>
                <Trait id="Generosity" value="-1"/>
                <Trait id="Honor" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_SE9_l -->
    <xsl:template match="NPCCharacter[@id='lord_SE9_l']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_SE9_l_name}GrishnÃ¡kh, Uruk Captain</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <xsl:attribute name="is_female">false</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.6" key="000BDC0FCFCC038F3D38B65122600AD02FEE2A646411F7F894359F648858075D064976670485629500000000000000000000000044FC1002000000007E7C4084"/>
            </face>
            <skills>
                <skill id="OneHanded" value="240"/>
                <skill id="TwoHanded" value="220"/>
                <skill id="Polearm" value="230"/>
                <skill id="Bow" value="150"/>
                <skill id="Crossbow" value="110"/>
                <skill id="Throwing" value="140"/>
                <skill id="Riding" value="170"/>
                <skill id="Athletics" value="210"/>
                <skill id="Crafting" value="50"/>
                <skill id="Scouting" value="130"/>
                <skill id="Tactics" value="220"/>
                <skill id="Charm" value="50"/>
                <skill id="Leadership" value="210"/>
                <skill id="Trade" value="50"/>
                <skill id="Steward" value="150"/>
                <skill id="Medicine" value="40"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="-2"/>
                <Trait id="Generosity" value="-2"/>
                <Trait id="Calculating" value="1"/>
                <Trait id="Mercy" value="-2"/>
                <Trait id="Valor" value="1"/>
                <Trait id="Egalitarian" value="-1"/>
                <Trait id="Oligarchic" value="1"/>
                <Trait id="Authoritarian" value="2"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_SE9_s -->
    <xsl:template match="NPCCharacter[@id='lord_SE9_s']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_SE9_s_name}Jonna</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0000080FD0FC100D4776686376266B93426B9C989782C82458739F1CB876963401577613051867C8000000000000000000000000000000000000000022A420C3"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Politician" value="6"/>
                <Trait id="Manager" value="6"/>
                <Trait id="Generosity" value="-1"/>
                <Trait id="Calculating" value="1"/>
                <Trait id="Honor" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_SE9_c1 -->
    <xsl:template match="NPCCharacter[@id='lord_SE9_c1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_SE9_c1_name}Pagarios</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">false</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="000B700E402C325968215CB4D9779D947BABE1EA12D7B9649975B5856DB7C355016776130655A58500000000000000000000000000000000000000002AD42042"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="BalancedFightingSkills" value="6"/>
                <Trait id="Commander" value="4"/>
                <Trait id="Politician" value="2"/>
                <Trait id="Manager" value="8"/>
                <Trait id="Valor" value="1"/>
                <Trait id="Calculating" value="1"/>
                <Trait id="Honor" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_SE9_c2 -->
    <xsl:template match="NPCCharacter[@id='lord_SE9_c2']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_SE9_c2_name}Diasca</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.4" build="0.3" key="0000080FCEFC290B4756896A6356B7D0C06B9C989782C82458739F1C6A48C62501877613081867C80000000000000000000000000000000000000000370C2102"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Politician" value="8"/>
                <Trait id="Manager" value="8"/>
                <Trait id="Calculating" value="1"/>
                <Trait id="Generosity" value="-1"/>
                <Trait id="Honor" value="-1"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_2_4_1 -->
    <xsl:template match="NPCCharacter[@id='lord_2_4_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_2_4_1_name}Apolanea</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.4" build="0.6" key="000B100151141010627A8278C8B6825989B65558B77AAC5795A778895979A76301C776130C385387000000000000000000000000000000000000000043A400C4"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="PeltastFightingSkills" value="4"/>
                <Trait id="Commander" value="6"/>
                <Trait id="Politician" value="6"/>
                <Trait id="Manager" value="4"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Honor" value="1"/>
                <Trait id="Valor" value="1"/>
                <Trait id="Generosity" value="-1"/>
                <Trait id="Calculating" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_2_7_1 -->
    <xsl:template match="NPCCharacter[@id='lord_2_7_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_2_7_1_name}Mimir</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <xsl:attribute name="is_female">false</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5089" build="0.4047" key="0000500440B4008E9E895C86B34CA389699869B8B28B576979538AC46CB4C7740076880307C60384000000000000000000000000000000000000000000F05000"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="HopliteFightingSkills" value="3"/>
                <Trait id="Commander" value="2"/>
                <Trait id="Politician" value="2"/>
                <Trait id="Manager" value="2"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Generosity" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_2_13_1 -->
    <xsl:template match="NPCCharacter[@id='lord_2_13_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_2_13_1_name}Lilizha</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="001108000070200FD33892A397AC34867B393C86ADDA750B66EE13B68C999CD90157761305747845000000000000000000000000000000000000000034A40083"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Politician" value="6"/>
                <Trait id="Manager" value="4"/>
                <Trait id="Valor" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_2_13_2 -->
    <xsl:template match="NPCCharacter[@id='lord_2_13_2']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_2_13_2_name}Andruta</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0005B002D0CC101077CA5CE759465522795799844375862757BAB56484A9788E0157761305DBD96A00000000000000000000000000000000000000005C9C4043"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Politician" value="6"/>
                <Trait id="Manager" value="6"/>
                <Trait id="Valor" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_2_13_3 -->
    <xsl:template match="NPCCharacter[@id='lord_2_13_3']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_2_13_3_name}Luda</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="00059C02D070300374CAA96A797A77C79CE76A5875B54198B5561928897C487E01D776130DD75BC500000000000000000000000000000000000000001BAC2083"/>
            </face>
            <skills/>
            <Traits/>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_2_13_4 -->
    <xsl:template match="NPCCharacter[@id='lord_2_13_4']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_2_13_4_name}Teta</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0005400151143004747C738A7A775752715CC25D4B5787956D76849495A949330177761307C5ADB800000000000000000000000000000000000000002EEC2043"/>
            </face>
            <skills/>
            <Traits/>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_2_14_1 -->
    <xsl:template match="NPCCharacter[@id='lord_2_14_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_2_14_1_name}Valkava</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0000400891A0200FDD78477A6AB5556B977C4D59CBAD8427CD47DE9AB8BC887A01B776130BC9A5B80000000000000000000000000000000000000000517C20C3"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Politician" value="2"/>
                <Trait id="Manager" value="10"/>
                <Trait id="Calculating" value="1"/>
                <Trait id="Mercy" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_2_14_2 -->
    <xsl:template match="NPCCharacter[@id='lord_2_14_2']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_2_14_2_name}Zaverena</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0000400890A03003DDC8CA9988D94DBA7A554D59CBAD8427C8146BB6B8B9587801B776130B553C280000000000000000000000000000000000000000517C2083"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Politician" value="4"/>
                <Trait id="Manager" value="4"/>
                <Trait id="Calculating" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_2_14_3 -->
    <xsl:template match="NPCCharacter[@id='lord_2_14_3']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_2_14_3_name}Vizhduna</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0000400890A0300A4A768CA3AAA5765D7A55444685D6AC2A856B544CB8B9587801B776130BC746C60000000000000000000000000000000000000000517C1003"/>
            </face>
            <skills/>
            <Traits/>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_2_15_1 -->
    <xsl:template match="NPCCharacter[@id='lord_2_15_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_2_15_1_name}Yachana</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="001C68070FE40009776A7BC4C8767889A85B469799583A24DB916477A389668101C776130C89899300000000000000000000000000000000000000005B8C30C3"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Politician" value="4"/>
                <Trait id="Manager" value="8"/>
                <Trait id="Mercy" value="1"/>
                <Trait id="Valor" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_2_15_2 -->
    <xsl:template match="NPCCharacter[@id='lord_2_15_2']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_2_15_2_name}Milanka</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="001C680710E4200777476D84B87649B77CC4469799583A24967922B69496277B01C776130CB42C5D00000000000000000000000000000000000000005B8C30C3"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Politician" value="4"/>
                <Trait id="Manager" value="8"/>
                <Trait id="Mercy" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_2_15_3 -->
    <xsl:template match="NPCCharacter[@id='lord_2_15_3']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_2_15_3_name}Velina</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="000B4C0710E4100D9BB5B85867759DB27C84967BC8BB966AF371F5599496277B00C776030CE4B6C500000000000000000000000000000000000000005B8C3043"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Politician" value="10"/>
                <Trait id="Manager" value="8"/>
                <Trait id="Calculating" value="1"/>
                <Trait id="Mercy" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_2_15_4 -->
    <xsl:template match="NPCCharacter[@id='lord_2_15_4']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_2_15_4_name}Bovan</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">false</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.6" build="0.6" key="00164C02C0882342BAB7B9566DA53AD368A6628171B9A586A7883546559D974501977613094979CE000000000000000000000000000000000000000041341002"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="BalancedFightingSkills" value="4"/>
                <Trait id="Commander" value="4"/>
                <Trait id="Politician" value="4"/>
                <Trait id="Manager" value="8"/>
                <Trait id="Mercy" value="1"/>
                <Trait id="Valor" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_2_16_1 -->
    <xsl:template match="NPCCharacter[@id='lord_2_16_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_2_16_1_name}Tyaska</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="000B2802D088000AC4886AA8789859A797CAD63996786A57CBB8885895D177B60197761309D8CA7600000000000000000000000000000000000000002B2C1083"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Politician" value="8"/>
                <Trait id="Manager" value="4"/>
                <Trait id="Mercy" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_2_17_1 -->
    <xsl:template match="NPCCharacter[@id='lord_2_17_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_2_17_1_name}Dracha</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="000B78071114200E8D1723DA7A67949A66266E8653525564594C4368786D87B30187761308288EC7000000000000000000000000000000000000000031143003"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Politician" value="2"/>
                <Trait id="Manager" value="8"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Generosity" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_2_18_1 -->
    <xsl:template match="NPCCharacter[@id='lord_2_18_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_2_18_1_name}Zorika</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="000B740A11001001583A7894D487CE78C5AA46B87A7486657B1B789866D6823901277613028B77670000000000000000000000000000000000000000623C3003"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Manager" value="6"/>
                <Trait id="Politician" value="8"/>
                <Trait id="Mercy" value="1"/>
                <Trait id="Valor" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_2_19_1 -->
    <xsl:template match="NPCCharacter[@id='lord_2_19_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_2_19_1_name}Vitomira</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0005680000882007AD47C86E48547C993A35BCC45C5A3488EB979A9B364B66A701377613031CB39B00000000000000000000000000000000000000002C5C3003"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Manager" value="6"/>
                <Trait id="Honor" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_2_20_1 -->
    <xsl:template match="NPCCharacter[@id='lord_2_20_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_2_20_1_name}Kisha</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="000B080150CC30064899E76527D86898746C92BAB8DAA66B7B8B3698896A983101B776130BB47BA900000000000000000000000000000000000000005B6C0083"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Manager" value="6"/>
                <Trait id="Valor" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_S8_u -->
    <xsl:template match="NPCCharacter[@id='lord_S8_u']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_S8_u_name}Galyk</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">false</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0015C8054044068CB7A388848DA2B46798378B58269B28772C23873661A7A63600477603042744BE000000000000000000000000000000000000000030940102"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="BalancedFightingSkills" value="4"/>
                <Trait id="Commander" value="10"/>
                <Trait id="Politician" value="8"/>
                <Trait id="Manager" value="6"/>
                <Trait id="Valor" value="-1"/>
                <Trait id="Mercy" value="1"/>
                <Trait id="Calculating" value="1"/>
                <Trait id="Oligarchic" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_2_21_1 -->
    <xsl:template match="NPCCharacter[@id='lord_2_21_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_2_21_1_name}Izdenka</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="000B0C02D2B410069419993888AD9C89477AB8686789AB88845B929CA6AC5C670197761309E3999800000000000000000000000000000000000000002D341003"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Manager" value="10"/>
                <Trait id="Politician" value="8"/>
                <Trait id="Honor" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_2_22_1 -->
    <xsl:template match="NPCCharacter[@id='lord_2_22_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_2_22_1_name}Zheneva</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.6097" build="0.6476" key="00004803800002C4867232085784BBA666987367C530B7547972892A88150405049A904609888D01000000000000000000000000000000000000000073381041"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="BalancedFightingSkills" value="3"/>
                <Trait id="Commander" value="10"/>
                <Trait id="Politician" value="4"/>
                <Trait id="Manager" value="8"/>
                <Trait id="Valor" value="1"/>
                <Trait id="Honor" value="1"/>
                <Trait id="Calculating" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_2_23_1 -->
    <xsl:template match="NPCCharacter[@id='lord_2_23_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_2_23_1_name}Zlatka</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0005BC001114200DEA72114080326643E10B706785AD15119FF6AFA17086F0510137761303105059000000000000000000000000000000000000000020641044"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Commander" value="10"/>
                <Trait id="Politician" value="2"/>
                <Trait id="Manager" value="2"/>
                <Trait id="Calculating" value="-1"/>
                <Trait id="Generosity" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_2_24_1 -->
    <xsl:template match="NPCCharacter[@id='lord_2_24_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_2_24_1_name}Zorina</xsl:attribute>
            <xsl:attribute name="default_group">HorseArcher</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="00054C0140B400039659749D884A5B5E6754C9967C75966FAF9645C8B6BA9BC5015776130587C7630000000000000000000000000000000000000000629C0083"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Commander" value="4"/>
                <Trait id="Politician" value="4"/>
                <Trait id="Manager" value="8"/>
                <Trait id="Calculating" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_S9_l -->
    <xsl:template match="NPCCharacter[@id='lord_S9_l']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_S9_l_name}Bombur</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <xsl:attribute name="is_female">false</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.696" build="0.9" key="00054C0140B409C39659749D884A5B5E98B6C9967C75966FAF9645C85A918D950068A6030687C763000000000000000000000000000000000000000062BC2083"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="KnightFightingSkills" value="5"/>
                <Trait id="Commander" value="10"/>
                <Trait id="Manager" value="2"/>
                <Trait id="Politician" value="6"/>
                <Trait id="Valor" value="1"/>
                <Trait id="Generosity" value="2"/>
                <Trait id="Mercy" value="1"/>
                <Trait id="Honor" value="1"/>
                <Trait id="Calculating" value="0"/>
                <Trait id="Egalitarian" value="1"/>
                <Trait id="Oligarchic" value="0"/>
                <Trait id="Authoritarian" value="0"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_S9_m -->
    <xsl:template match="NPCCharacter[@id='lord_S9_m']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_S9_m_name}Dakhila</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="00054C0140B400039659749D884A5B5E6754C9967C75966FAF9645C8B6BA9BC5015776130587C7630000000000000000000000000000000000000000629C0083"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Commander" value="2"/>
                <Trait id="Politician" value="6"/>
                <Trait id="Manager" value="8"/>
                <Trait id="Valor" value="1"/>
                <Trait id="Generosity" value="-1"/>
                <Trait id="Oligarchic" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_S9_c -->
    <xsl:template match="NPCCharacter[@id='lord_S9_c']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_S9_c_name}Forim</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">false</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="00054C0140B400039659749D884A5B5E6754C9967C75966FAF9645C8B6BA9BC5015776130587C7630000000000000000000000000000000000000000629C0083"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="CavalryFightingSkills" value="6"/>
                <Trait id="Commander" value="8"/>
                <Trait id="Politician" value="6"/>
                <Trait id="Manager" value="8"/>
                <Trait id="Mercy" value="1"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Calculating" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_S9_u -->
    <xsl:template match="NPCCharacter[@id='lord_S9_u']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_S9_u_name}Chastimir</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">false</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.696" build="0.5478" key="00054C0280B432419636BC4C477176F96754C9967C75966FAF9645C8B6BA9BC500A8A6030A87C7630000000000000000000000000000000000000000633C2083"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="CavalryFightingSkills" value="6"/>
                <Trait id="Commander" value="8"/>
                <Trait id="Politician" value="4"/>
                <Trait id="Manager" value="6"/>
                <Trait id="Valor" value="1"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Calculating" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_3_3_1 -->
    <xsl:template match="NPCCharacter[@id='lord_3_3_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_3_3_1_name}Tariq</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">false</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0" build="0" key="001C900FC0FC180886899931423946DB88E588765AA3947958825CAD467C7D4701E006890E57A12A000000000000000000000000000000000000000000843044"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="BalancedFightingSkills" value="3"/>
                <Trait id="Commander" value="10"/>
                <Trait id="Politician" value="8"/>
                <Trait id="Manager" value="6"/>
                <Trait id="Calculating" value="1"/>
                <Trait id="Generosity" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_3_13_1 -->
    <xsl:template match="NPCCharacter[@id='lord_3_13_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_3_13_1_name}Sira</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0022880E504430048D6DA84AA9B668434696F8AC7F826778683B65553B844B7A01A776130A85794900000000000000000000000000000000000000005D542044"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Manager" value="10"/>
                <Trait id="Honor" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_3_13_2 -->
    <xsl:template match="NPCCharacter[@id='lord_3_13_2']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_3_13_2_name}Razana</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">false</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0028D40B51FC000279615D88977453634B8B895C74B382164C93D947647785A7016776130625634100000000000000000000000000000000000000006ACC40C3"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Manager" value="2"/>
                <Trait id="Honor" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_3_14_1 -->
    <xsl:template match="NPCCharacter[@id='lord_3_14_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_3_14_1_name}Sasaitha</xsl:attribute>
            <xsl:attribute name="default_group">HorseArcher</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="002D700CD0881003A5C589888A5675AAC7B2869B794AB978A79C47747B19B58801577613057CC3E6000000000000000000000000000000000000000042B41003"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Politician" value="4"/>
                <Trait id="Manager" value="6"/>
                <Trait id="Calculating" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_3_15_1 -->
    <xsl:template match="NPCCharacter[@id='lord_3_15_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_3_15_1_name}Shimra</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="00288C0CD2880004A395E7AA463A6E1C4BAA43633B1986E5A89857745CAB736C0167761306667684000000000000000000000000000000000000000066C410C3"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Politician" value="10"/>
                <Trait id="Manager" value="10"/>
                <Trait id="Generosity" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_3_15_2 -->
    <xsl:template match="NPCCharacter[@id='lord_3_15_2']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_3_15_2_name}Bushila</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0022500CD2E4000457A8EAA7469C9552DEABB756A9349BC76A7768A8D898CA880187761308BAA6890000000000000000000000000000000000000000731C0083"/>
            </face>
            <skills/>
            <Traits/>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_3_16_1 -->
    <xsl:template match="NPCCharacter[@id='lord_3_16_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_3_16_1_name}Farina</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="001CA80CD1883007A555B462CE8A55A6578B58BB799A8ACCAA7D8CC7765CB31201277613025AAA3B0000000000000000000000000000000000000000583C4043"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Politician" value="10"/>
                <Trait id="Manager" value="6"/>
                <Trait id="Honor" value="-1"/>
                <Trait id="Calculating" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_3_17_1 -->
    <xsl:template match="NPCCharacter[@id='lord_3_17_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_3_17_1_name}Shaima</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="003F880FC0B4000D22AA8868B68367A9346A54A66984B75BB1AA59782B2B6773015776130586542600000000000000000000000000000000000000003E9C2043"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Commander" value="4"/>
                <Trait id="Politician" value="4"/>
                <Trait id="Manager" value="6"/>
                <Trait id="Mercy" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_3_17_2 -->
    <xsl:template match="NPCCharacter[@id='lord_3_17_2']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_3_17_2_name}Sanit</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0028A40FC044200D7BA421B1984A6141392DDD94B48251365E6857BBD67B978B01C776130C9683850000000000000000000000000000000000000000338C3003"/>
            </face>
            <skills/>
            <Traits/>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_3_18_1 -->
    <xsl:template match="NPCCharacter[@id='lord_3_18_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_3_18_1_name}Farzana</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="00333C0E502C10107A75B5227776955DAA32B8696C098CE8698934134582A37801A776130AC954A5000000000000000000000000000000000000000031440003"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Manager" value="6"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Honor" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_3_18_2 -->
    <xsl:template match="NPCCharacter[@id='lord_3_18_2']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_3_18_2_name}Hafisa</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="00281C0B40FC10014495955AC76645777ACE633B7A944788995887A99C8DDBA50157761305557C45000000000000000000000000000000000000000030B410C3"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Manager" value="10"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Honor" value="1"/>
                <Trait id="Calculating" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_3_18_3 -->
    <xsl:template match="NPCCharacter[@id='lord_3_18_3']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_3_18_3_name}Zuad</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0033640B52FC3011677343968A67954791464A7576DB8A4BD358B8497D53B6AB0187761308C59A69000000000000000000000000000000000000000031042003"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="CavalryFightingSkills" value="3"/>
                <Trait id="Commander" value="6"/>
                <Trait id="Valor" value="1"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Calculating" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_3_18_4 -->
    <xsl:template match="NPCCharacter[@id='lord_3_18_4']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_3_18_4_name}Jalfar</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">false</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0028580E40CC130D755797AA334A25ED82C384356C3687C239B7CAA5876879580137761303A69CA6000000000000000000000000000000000000000030541002"/>
            </face>
            <skills/>
            <Traits/>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_3_19_1 -->
    <xsl:template match="NPCCharacter[@id='lord_3_19_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_3_19_1_name}Salma</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.4784" build="0.5191" key="0016C80880001597BA83341737814FC43B578027A2557115ABB98E678A0A6867049870460979E5240000000000000000000000000000000000000000253850C0"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Politician" value="8"/>
                <Trait id="Manager" value="6"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Calculating" value="1"/>
                <Trait id="Honor" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_3_19_2 -->
    <xsl:template match="NPCCharacter[@id='lord_3_19_2']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_3_19_2_name}Zulaika</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0028780FD1B40004C774C93FC6B607A25B78358D77AA3A167785D8575938737C0167761306A6AB4600000000000000000000000000000000000000004ABC10C4"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Politician" value="4"/>
                <Trait id="Manager" value="4"/>
                <Trait id="Generosity" value="-1"/>
                <Trait id="Calculating" value="1"/>
                <Trait id="Honor" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_3_19_3 -->
    <xsl:template match="NPCCharacter[@id='lord_3_19_3']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_3_19_3_name}Sulhana</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="001C240FC00010037AC4EAE9849538381157953AB4819A17898B5984272D3C660187761308B6393700000000000000000000000000000000000000002F0440C4"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Politician" value="4"/>
                <Trait id="Manager" value="10"/>
                <Trait id="Valor" value="-1"/>
                <Trait id="Honor" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_3_20_1 -->
    <xsl:template match="NPCCharacter[@id='lord_3_20_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_3_20_1_name}Judira</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0028A40CD21410068A9277BB6B3788C8B8A9C36AB6AAC46A436AB719877B887401B776130BA539B70000000000000000000000000000000000000000197C3003"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Manager" value="8"/>
                <Trait id="Generosity" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_3_20_2 -->
    <xsl:template match="NPCCharacter[@id='lord_3_20_2']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_3_20_2_name}Azina</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0028540FD114000ABB6D8984A3778C5557A97866E85A9D87A87587757CA534BB0187761308A5866B00000000000000000000000000000000000000006D042003"/>
            </face>
            <skills/>
            <Traits/>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_3_21_1 -->
    <xsl:template match="NPCCharacter[@id='lord_3_21_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_3_21_1_name}Ashisa</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0039580CD1A0000598A7094768948C7BBC777A3B9939A865A968C3CE8A95D3CA0137761303865A560000000000000000000000000000000000000000587440C3"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Manager" value="6"/>
                <Trait id="Generosity" value="-1"/>
                <Trait id="Honor" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_3_22_1 -->
    <xsl:template match="NPCCharacter[@id='lord_3_22_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_3_22_1_name}Yamina</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0022640A215820064A37ACFE612B9A4AACB69A98683BB57DB7965D8AC65346250187761308627CDB0000000000000000000000000000000000000000270C3043"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Commander" value="8"/>
                <Trait id="Politician" value="6"/>
                <Trait id="Manager" value="6"/>
                <Trait id="Honor" value="-1"/>
                <Trait id="Mercy" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_3_22_2 -->
    <xsl:template match="NPCCharacter[@id='lord_3_22_2']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_3_22_2_name}Suna</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0028540E52143001722BD61A88448B8B8985B5B635BF97A4BABEA16D8882527C0167761306957C88000000000000000000000000000000000000000048D400C3"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Politician" value="8"/>
                <Trait id="Manager" value="6"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Generosity" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_3_22_3 -->
    <xsl:template match="NPCCharacter[@id='lord_3_22_3']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_3_22_3_name}Zanuwa</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0022700CC014101263B8C87941868498779B3D75E97B5678C27788B8C8AE759501B776130B95A6650000000000000000000000000000000000000000396C00C3"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Manager" value="2"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_3_22_4 -->
    <xsl:template match="NPCCharacter[@id='lord_3_22_4']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_3_22_4_name}Hajara</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0028240E5088200E38B5E59AB258BA6668952E7A5AD5B8797BC7A8E8796233690177761307C6896400000000000000000000000000000000000000003EEC10C3"/>
            </face>
            <skills/>
            <Traits/>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_3_23_1 -->
    <xsl:template match="NPCCharacter[@id='lord_3_23_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_3_23_1_name}Sukayna</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0022700CD114300258D8AD87875435B9EA4385AA274487967CCCCB676A89755A0137761303B03A99000000000000000000000000000000000000000038643004"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Commander" value="3"/>
                <Trait id="Politician" value="8"/>
                <Trait id="Manager" value="6"/>
                <Trait id="Calculating" value="1"/>
                <Trait id="Honor" value="1"/>
                <Trait id="Generosity" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_A9_l -->
    <xsl:template match="NPCCharacter[@id='lord_A9_l']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_A9_l_name}Caradan</xsl:attribute>
            <xsl:attribute name="default_group">Infantry</xsl:attribute>
            <xsl:attribute name="is_female">false</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.7" build="0.8" key="0022700CD114300258D8AD87875435B9EA4385AA274487967CCCCB676A89755A0137761303B03A99000000000000000000000000000000000000000038643004"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="BalancedFightingSkills" value="2"/>
                <Trait id="Commander" value="10"/>
                <Trait id="Politician" value="5"/>
                <Trait id="Manager" value="8"/>
                <Trait id="Valor" value="1"/>
                <Trait id="Generosity" value="2"/>
                <Trait id="Mercy" value="1"/>
                <Trait id="Honor" value="2"/>
                <Trait id="Calculating" value="0"/>
                <Trait id="Egalitarian" value="1"/>
                <Trait id="Oligarchic" value="0"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_A9_s -->
    <xsl:template match="NPCCharacter[@id='lord_A9_s']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_A9_s_name}Thiqa</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0028240E5088200E38B5E59AB258BA6668952E7A5AD5B8797BC7A8E8796233690177761307C6896400000000000000000000000000000000000000003EEC10C3"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Politician" value="8"/>
                <Trait id="Manager" value="6"/>
                <Trait id="Honor" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_A9_c -->
    <xsl:template match="NPCCharacter[@id='lord_A9_c']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_A9_c_name}Dhila</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.6389" build="0.5926" key="0023840E401410059AB8C8794186849896635A43A91AA68AC27788B88A37A6B600C9A6030C95A6650000000000000000000000000000000000000000478C00C3"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Politician" value="8"/>
                <Trait id="Manager" value="6"/>
                <Trait id="Generosity" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_A9_u -->
    <xsl:template match="NPCCharacter[@id='lord_A9_u']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_A9_u_name}Qaban</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">false</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0022700CC014101263B8C87941868498779B3D75E97B5678C27788B8C8AE759501B776130B95A6650000000000000000000000000000000000000000396C00C3"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="BalancedFightingSkills" value="6"/>
                <Trait id="Commander" value="2"/>
                <Trait id="Politician" value="8"/>
                <Trait id="Manager" value="6"/>
                <Trait id="Valor" value="1"/>
                <Trait id="Calculating" value="-1"/>
                <Trait id="Egalitarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_4_3_1 -->
    <xsl:template match="NPCCharacter[@id='lord_4_3_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_4_3_1_name}Ã‰omer Eoforing</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">false</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.1" build="0.5" key="0002B404000011826B700D6F80961035305E80B2BFEB0E00775161048C771F0400AB36030A62AA0300000000000000000000000000000000000000003D447002"/>
            </face>
            <skills>
                <skill id="OneHanded" value="270"/>
                <skill id="TwoHanded" value="250"/>
                <skill id="Polearm" value="260"/>
                <skill id="Bow" value="150"/>
                <skill id="Crossbow" value="100"/>
                <skill id="Throwing" value="150"/>
                <skill id="Riding" value="280"/>
                <skill id="Athletics" value="220"/>
                <skill id="Crafting" value="50"/>
                <skill id="Scouting" value="150"/>
                <skill id="Tactics" value="290"/>
                <skill id="Charm" value="180"/>
                <skill id="Leadership" value="250"/>
                <skill id="Trade" value="80"/>
                <skill id="Steward" value="200"/>
                <skill id="Medicine" value="180"/>
                <skill id="Engineering" value="280"/>
                <skill id="Roguery" value="300"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="2"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Calculating" value="0"/>
                <Trait id="Mercy" value="1"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="1"/>
                <Trait id="Oligarchic" value="1"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_4_6_1 -->
    <xsl:template match="NPCCharacter[@id='lord_4_6_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_4_6_1_name}DÃ©orwyn Grimingas</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="000B040B4070300A59453CA886740D767888B63281AA8878BC957C5659C2945701777613078AB58600000000000000000000000000000000000000006AF420C3"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Commander" value="8"/>
                <Trait id="Politician" value="8"/>
                <Trait id="Manager" value="6"/>
                <Trait id="Calculating" value="-1"/>
                <Trait id="Generosity" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_4_16_1 -->
    <xsl:template match="NPCCharacter[@id='lord_4_16_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_4_16_1_name}MÃ©rthÃº FelÃ¡nding</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.4524" build="0.5804" key="0005340140CC00017071527435736265B7BE7A79B0614997BAB7F9397C2A987203597635053CBA5A00000000000000000000000000000000000000005CB41044"/>
            </face>
            <skills>
                <skill id="OneHanded" value="180"/>
                <skill id="TwoHanded" value="120"/>
                <skill id="Polearm" value="200"/>
                <skill id="Bow" value="260"/>
                <skill id="Crossbow" value="60"/>
                <skill id="Throwing" value="140"/>
                <skill id="Riding" value="290"/>
                <skill id="Athletics" value="170"/>
                <skill id="Crafting" value="40"/>
                <skill id="Scouting" value="160"/>
                <skill id="Tactics" value="200"/>
                <skill id="Charm" value="220"/>
                <skill id="Leadership" value="240"/>
                <skill id="Trade" value="80"/>
                <skill id="Steward" value="140"/>
                <skill id="Medicine" value="120"/>
                <skill id="Engineering" value="280"/>
                <skill id="Roguery" value="300"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="2"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Calculating" value="0"/>
                <Trait id="Mercy" value="1"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="1"/>
                <Trait id="Oligarchic" value="0"/>
                <Trait id="Authoritarian" value="0"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_4_20_1 -->
    <xsl:template match="NPCCharacter[@id='lord_4_20_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_4_20_1_name}Ingeltrud</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="000B680A1070000AB27B84CD4CC38B9CBA75B4AABB9AC152965A9D22CEADBE45019776130998732700000000000000000000000000000000000000001B2C2084"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Manager" value="8"/>
                <Trait id="Generosity" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_4_22_1 -->
    <xsl:template match="NPCCharacter[@id='lord_4_22_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_4_22_1_name}Sunnifa Celmunding</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.2639" build="0.7222" key="000D3C0140FC0001E08010E3E2E7E2117EF887621E988104A009041F8A870A07009B4603090CE6F500000000000000000000000000000000000000003F1C2003"/>
            </face>
            <skills>
                <skill id="OneHanded" value="150"/>
                <skill id="TwoHanded" value="120"/>
                <skill id="Polearm" value="100"/>
                <skill id="Bow" value="100"/>
                <skill id="Crossbow" value="80"/>
                <skill id="Throwing" value="80"/>
                <skill id="Riding" value="200"/>
                <skill id="Athletics" value="150"/>
                <skill id="Crafting" value="80"/>
                <skill id="Scouting" value="100"/>
                <skill id="Tactics" value="150"/>
                <skill id="Charm" value="220"/>
                <skill id="Leadership" value="200"/>
                <skill id="Trade" value="120"/>
                <skill id="Steward" value="220"/>
                <skill id="Medicine" value="150"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="1"/>
                <Trait id="Generosity" value="2"/>
                <Trait id="Calculating" value="0"/>
                <Trait id="Mercy" value="2"/>
                <Trait id="Valor" value="0"/>
                <Trait id="Egalitarian" value="1"/>
                <Trait id="Oligarchic" value="0"/>
                <Trait id="Authoritarian" value="0"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_4_23_1 -->
    <xsl:template match="NPCCharacter[@id='lord_4_23_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_4_23_1_name}Wulfwynn Marhad</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="1" build="1" key="0002A8001114300AE08F900FBB63021176F8806C1F970104A009F01B8A87009A009FF603090FE7FC00000000000000000000000000000000000000003F1C2003"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Politician" value="6"/>
                <Trait id="Manager" value="6"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Generosity" value="-1"/>
                <Trait id="Calculating" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_4_23_2 -->
    <xsl:template match="NPCCharacter[@id='lord_4_23_2']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_4_23_2_name}Eleduran Marhad</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">false</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0000740E4044000B3969339927553498A84BB8A877D96D6645597984592A439501477613046A3907000000000000000000000000000000000000000050942043"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="BalancedFightingSkills" value="5"/>
                <Trait id="Commander" value="8"/>
                <Trait id="Politician" value="6"/>
                <Trait id="Manager" value="6"/>
                <Trait id="Calculating" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_4_23_3 -->
    <xsl:template match="NPCCharacter[@id='lord_4_23_3']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_4_23_3_name}Eleduran</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">false</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0000740E4044000B3969339927553498A84BB8A877D96D6645597984592A439501477613046A3907000000000000000000000000000000000000000050942043"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="BalancedFightingSkills" value="5"/>
                <Trait id="Commander" value="8"/>
                <Trait id="Politician" value="6"/>
                <Trait id="Manager" value="6"/>
                <Trait id="Calculating" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_4_25_1 -->
    <xsl:template match="NPCCharacter[@id='lord_4_25_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_4_25_1_name}Bertliana</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0000540E4070100B9D95A3A74EA8C8B848778365688C85437456344D43777968014776130475B2940000000000000000000000000000000000000000268C30C4"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Politician" value="8"/>
                <Trait id="Manager" value="8"/>
                <Trait id="Honor" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_4_27_1 -->
    <xsl:template match="NPCCharacter[@id='lord_4_27_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_4_27_1_name}WÃ¦rburg Ã†thellafing</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.4769" build="0.6042" key="000D3006D014200A8E8000120BC13021A78A806C0FC77034A4591B1C8A87119900797603070CEF0C00000000000000000000000000000000000000003EFC2003"/>
            </face>
            <skills>
                <skill id="OneHanded" value="270"/>
                <skill id="TwoHanded" value="150"/>
                <skill id="Polearm" value="200"/>
                <skill id="Bow" value="120"/>
                <skill id="Crossbow" value="70"/>
                <skill id="Throwing" value="130"/>
                <skill id="Riding" value="260"/>
                <skill id="Athletics" value="220"/>
                <skill id="Crafting" value="50"/>
                <skill id="Scouting" value="140"/>
                <skill id="Tactics" value="180"/>
                <skill id="Charm" value="200"/>
                <skill id="Leadership" value="220"/>
                <skill id="Trade" value="80"/>
                <skill id="Steward" value="160"/>
                <skill id="Medicine" value="140"/>
                <skill id="Engineering" value="280"/>
                <skill id="Roguery" value="300"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="2"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Calculating" value="0"/>
                <Trait id="Mercy" value="1"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="1"/>
                <Trait id="Oligarchic" value="0"/>
                <Trait id="Authoritarian" value="0"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_4_28_1 -->
    <xsl:template match="NPCCharacter[@id='lord_4_28_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_4_28_1_name}Morcar</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">false</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0" build="0.5" key="0008240D800013820E70FF0790680008AB9D709079EF9B01744D7B098942D474007706030707A3F500000000000000000000000000000000000000003CF45042"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="KnightFightingSkills" value="8"/>
                <Trait id="Commander" value="5"/>
                <Trait id="Manager" value="2"/>
                <Trait id="Politician" value="8"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Mercy" value="0"/>
                <Trait id="Honor" value="2"/>
                <Trait id="Calculating" value="0"/>
                <Trait id="Egalitarian" value="1"/>
                <Trait id="Oligarchic" value="0"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_4_28_2 -->
    <xsl:template match="NPCCharacter[@id='lord_4_28_2']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_4_28_2_name}Hereswith</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.1574" build="0.5602" key="000A200150B4000A6E8000100BC79001A269846C0A811013A579051C89F71BA6007826030703760000000000000000000000000000000000000000003EE40003"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Commander" value="6"/>
                <Trait id="Politician" value="8"/>
                <Trait id="Manager" value="4"/>
                <Trait id="Mercy" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_V11_l -->
    <xsl:template match="NPCCharacter[@id='lord_V11_l']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_V11_l_name}LÃ©omund DeÃ¡fringas</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">false</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.4306" build="0.4699" key="0005CC0D800013436E8F6EF11905A009717CC2604DB77E03C01904008E50E60400776603075AC59900000000000000000000000000000000000000003EFC1002"/>
            </face>
            <skills>
                <skill id="OneHanded" value="250"/>
                <skill id="TwoHanded" value="230"/>
                <skill id="Polearm" value="240"/>
                <skill id="Bow" value="140"/>
                <skill id="Crossbow" value="90"/>
                <skill id="Throwing" value="110"/>
                <skill id="Riding" value="270"/>
                <skill id="Athletics" value="190"/>
                <skill id="Crafting" value="50"/>
                <skill id="Scouting" value="140"/>
                <skill id="Tactics" value="230"/>
                <skill id="Charm" value="190"/>
                <skill id="Leadership" value="250"/>
                <skill id="Trade" value="100"/>
                <skill id="Steward" value="220"/>
                <skill id="Medicine" value="80"/>
            </skills>
            <Traits>
                <Trait id="Honor" value="2"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Calculating" value="0"/>
                <Trait id="Mercy" value="1"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Egalitarian" value="0"/>
                <Trait id="Oligarchic" value="1"/>
                <Trait id="Authoritarian" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_5_1_1 -->
    <xsl:template match="NPCCharacter[@id='lord_5_1_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_5_1_1_name}RandomDude</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0005A80A11002011D329A8667A46389A549D8877684B673EB664968A87B0D94901477613048EB3AC0000000000000000000000000000000000000000467C2084"/>
            </face>
            <skills/>
            <Traits/>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_5_3_1 -->
    <xsl:template match="NPCCharacter[@id='lord_5_3_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_5_3_1_name}Ranaon</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0000240FD100300D453C497C68747AA8916C58A9C85518B98B75847C3A1893520197761309AB86730000000000000000000000000000000000000000472420C3"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Commander" value="4"/>
                <Trait id="Politician" value="4"/>
                <Trait id="Manager" value="6"/>
                <Trait id="Honor" value="1"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Calculating" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_5_3_2 -->
    <xsl:template match="NPCCharacter[@id='lord_5_3_2']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_5_3_2_name}Ladogual</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0000240FD100300D453C497C68747AA8916C58A9C85518B98B75847C3A1893520197761309AB86730000000000000000000000000000000000000000472420C3"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="BalancedFightingSkills" value="4"/>
                <Trait id="Commander" value="10"/>
                <Trait id="Politician" value="8"/>
                <Trait id="Manager" value="6"/>
                <Trait id="Valor" value="-1"/>
                <Trait id="Honor" value="-1"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Calculating" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_5_5_1 -->
    <xsl:template match="NPCCharacter[@id='lord_5_5_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_5_5_1_name}Eilidh</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="001C580FC058100F59AB88926A889A68AA779A998D8ED4767782AC7E2256AA740167761306A4744300000000000000000000000000000000000000003AC42003"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Politician" value="6"/>
                <Trait id="Manager" value="8"/>
                <Trait id="Generosity" value="-1"/>
                <Trait id="Honor" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_5_13_1 -->
    <xsl:template match="NPCCharacter[@id='lord_5_13_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_5_13_1_name}Beasag</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="000BB80893A02008B8C78A899993B688B48B5878B837C8B57B879938C5B85B14019776130989CA850000000000000000000000000000000000000000452C0004"/>
            </face>
            <skills/>
            <Traits/>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_5_15_1 -->
    <xsl:template match="NPCCharacter[@id='lord_5_15_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_5_15_1_name}Floraidh</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0005300E4B2C10106286C88D999368A86A9678899D4447A323667BC9A844A3BA01877613083838690000000000000000000000000000000000000000430410C3"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Commander" value="6"/>
                <Trait id="Politician" value="10"/>
                <Trait id="Manager" value="4"/>
                <Trait id="Valor" value="1"/>
                <Trait id="Mercy" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_5_15_2 -->
    <xsl:template match="NPCCharacter[@id='lord_5_15_2']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_5_15_2_name}Beitrin</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">false</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="00112C0A0FA0000A72557A7C9B879A8897CB1947DBA9DA87A53D947974F6DCB70167761306E83DA3000000000000000000000000000000000000000010CC0083"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="BalancedFightingSkills" value="3"/>
                <Trait id="Commander" value="4"/>
                <Trait id="Politician" value="8"/>
                <Trait id="Manager" value="8"/>
                <Trait id="Valor" value="-1"/>
                <Trait id="Honor" value="-1"/>
                <Trait id="Generosity" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_5_15_3 -->
    <xsl:template match="NPCCharacter[@id='lord_5_15_3']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_5_15_3_name}Diarbhain</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="000B1C0154440001B544487899A76DA897C66DA64B699855366D7BA96969758401577613059EA3A8000000000000000000000000000000000000000042AC3043"/>
            </face>
            <skills/>
            <Traits/>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_5_14_1 -->
    <xsl:template match="NPCCharacter[@id='lord_5_14_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_5_14_1_name}Eabyr</xsl:attribute>
            <xsl:attribute name="default_group">Ranged</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0005880A07CC0008B68B4646A8A6554A7A3983E856C787A768A8682A9457427301C776130C6CA4680000000000000000000000000000000000000000238C2043"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Politician" value="4"/>
                <Trait id="Manager" value="10"/>
                <Trait id="Honor" value="1"/>
                <Trait id="Generosity" value="-1"/>
                <Trait id="Valor" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_5_16_1 -->
    <xsl:template match="NPCCharacter[@id='lord_5_16_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_5_16_1_name}Liasin</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0005600A002C2004569443C64645995CDA6B7ABA5BC4357B7C8694BC995E487801A776130A87872B00000000000000000000000000000000000000001B443083"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Politician" value="2"/>
                <Trait id="Manager" value="2"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Calculating" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_5_16_2 -->
    <xsl:template match="NPCCharacter[@id='lord_5_16_2']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_5_16_2_name}Gawen</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0005E0070000000D976577688DC974CDB7648B819B9E7598D9BC69B331354EA40187761308552777000000000000000000000000000000000000000022FC1004"/>
            </face>
            <skills/>
            <Traits/>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_5_17_1 -->
    <xsl:template match="NPCCharacter[@id='lord_5_17_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_5_17_1_name}Brighan</xsl:attribute>
            <xsl:attribute name="default_group">Ranged</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0005600CC0FC300B799DA3319A46DB4C77497515CB8AB436568CB7A5B9CA676801B776130B4BAA360000000000000000000000000000000000000000236C4003"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Politician" value="4"/>
                <Trait id="Manager" value="10"/>
                <Trait id="Calculating" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_5_18_1 -->
    <xsl:template match="NPCCharacter[@id='lord_5_18_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_5_18_1_name}Seonag</xsl:attribute>
            <xsl:attribute name="default_group">Ranged</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" build="0.5" key="000B900145882012209D8E3BA67B69A87A486958763A58B9B675C3C78686798901977613091866AB000000000000000000000000000000000000000071241083"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Politician" value="6"/>
                <Trait id="Manager" value="6"/>
                <Trait id="Honor" value="1"/>
                <Trait id="Valor" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_5_21_1 -->
    <xsl:template match="NPCCharacter[@id='lord_5_21_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_5_21_1_name}Beathag</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.7257" build="0.8089" key="00025C07000024CAB77216196072AAE6728B78827641ED973887AC168234162503ACB0350A99E955000000000000000000000000000000000000000005405082"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="BalancedFightingSkills" value="2"/>
                <Trait id="Commander" value="6"/>
                <Trait id="Politician" value="4"/>
                <Trait id="Manager" value="8"/>
                <Trait id="Honor" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_5_21_2 -->
    <xsl:template match="NPCCharacter[@id='lord_5_21_2']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_5_21_2_name}Taorse</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="000B2402C4CC000F46983AA68546B58A13047B958748D17A5AA943439A5AA44B018776130835D45200000000000000000000000000000000000000004F1C30C3"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Politician" value="4"/>
                <Trait id="Manager" value="8"/>
                <Trait id="Honor" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_B8_l -->
    <xsl:template match="NPCCharacter[@id='lord_B8_l']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_B8_l_name}Maireas</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.7901" build="0.7686" key="0011F40FC00015C31F80050E3578F78FC00E740704D0F71BF0C8EC145B060A4F013006440385C177000000000000000000000000000000000000000000143144"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Commander" value="4"/>
                <Trait id="Politician" value="2"/>
                <Trait id="Manager" value="8"/>
                <Trait id="Honor" value="-1"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Generosity" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_B8_s -->
    <xsl:template match="NPCCharacter[@id='lord_B8_s']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_B8_s_name}Rodarac</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">false</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="000B2402C4CC000F46983AA68546B58A13047B958748D17A5AA943439A5AA44B018776130835D45200000000000000000000000000000000000000004F1C30C3"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="BalancedFightingSkills" value="6"/>
                <Trait id="Commander" value="2"/>
                <Trait id="Politician" value="4"/>
                <Trait id="Manager" value="8"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Calculating" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_B8_c -->
    <xsl:template match="NPCCharacter[@id='lord_B8_c']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_B8_c_name}Guaran</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">false</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="00058C02C03C07C249A67962000F269EE3EFFE7528914639236B99548BFBD5A5010006890F9CA7B60000000000000000000000000000000000000000019C5104"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="BalancedFightingSkills" value="6"/>
                <Trait id="Commander" value="10"/>
                <Trait id="Politician" value="6"/>
                <Trait id="Manager" value="10"/>
                <Trait id="Mercy" value="1"/>
                <Trait id="Generosity" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_6_15_1 -->
    <xsl:template match="NPCCharacter[@id='lord_6_15_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_6_15_1_name}Khorijin</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0016FC0E5000100158708BD6CDC852299D4AB89EB77F390E84269783AA69586C017776130756C3A900000000000000000000000000000000000000003EF43083"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Politician" value="6"/>
                <Trait id="Manager" value="8"/>
                <Trait id="Honor" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_6_15_2 -->
    <xsl:template match="NPCCharacter[@id='lord_6_15_2']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_6_15_2_name}Sechen</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0016F80E5000200EB8708BD6CDC85229D3698B3ABDFE344CD22D3DD538898868017776130796723B00000000000000000000000000000000000000003EF41003"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Manager" value="8"/>
                <Trait id="Politician" value="10"/>
                <Trait id="Honor" value="-1"/>
                <Trait id="Generosity" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_6_16_1 -->
    <xsl:template match="NPCCharacter[@id='lord_6_16_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_6_16_1_name}Chambui</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.805" build="0.6475" key="0016E00B400006D3507044293BC820482534773EE5DE57546D6B291183394315056AC05706556766000000000000000000000000000000000000000028C82104"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Politician" value="6"/>
                <Trait id="Manager" value="6"/>
                <Trait id="Valor" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_6_16_2 -->
    <xsl:template match="NPCCharacter[@id='lord_6_16_2']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_6_16_2_name}Unagen</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0011D40FD4CC300BCF7616D8AD922604F4116550F7EF4310E0C91E1974071056016776130600120E000000000000000000000000000000000000000034C42083"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Commander" value="10"/>
                <Trait id="Politician" value="4"/>
                <Trait id="Manager" value="4"/>
                <Trait id="Valor" value="1"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Calculating" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_6_17_1 -->
    <xsl:template match="NPCCharacter[@id='lord_6_17_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_6_17_1_name}Ergene</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0016F80E4000200AB8B5A8AB37C5891C4BA88B3ABDFE344C5ADD18434234CBA6017776130746491E00000000000000000000000000000000000000003EF410C3"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Commander" value="4"/>
                <Trait id="Politician" value="10"/>
                <Trait id="Manager" value="6"/>
                <Trait id="Calculating" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_6_17_2 -->
    <xsl:template match="NPCCharacter[@id='lord_6_17_2']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_6_17_2_name}Yesum</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0011D40FC0003001E9761DA0AD922600C2226C60F0EF0710F7DA1E1A740E10500167761306634C0A000000000000000000000000000000000000000034C44083"/>
            </face>
            <skills/>
            <Traits/>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_6_18_1 -->
    <xsl:template match="NPCCharacter[@id='lord_6_18_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_6_18_1_name}Tilun</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0016F80CD400100318B5A8AB37C5891C4BA88B3A22AF3A4C6388C1954234CBA601777613076B5C2300000000000000000000000000000000000000003EF41003"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Politician" value="8"/>
                <Trait id="Manager" value="10"/>
                <Trait id="Calculating" value="1"/>
                <Trait id="Honor" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_6_18_2 -->
    <xsl:template match="NPCCharacter[@id='lord_6_18_2']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_6_18_2_name}Chagun</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0011DC0FD3FC300E488386A697AAB55B61978D49E9FE8438EC731556996786800137761303C76C170000000000000000000000000000000000000000426C4083"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Politician" value="8"/>
                <Trait id="Manager" value="4"/>
                <Trait id="Mercy" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_6_19_1 -->
    <xsl:template match="NPCCharacter[@id='lord_6_19_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_6_19_1_name}Sokhatai</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.3" build="0.4" key="000BF80CD100100318B664517CBB94C8D5398B3A22AF3A4C5B2C76989C692815017776130799894800000000000000000000000000000000000000003EF41043"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Politician" value="6"/>
                <Trait id="Manager" value="2"/>
                <Trait id="Honor" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_6_19_2 -->
    <xsl:template match="NPCCharacter[@id='lord_6_19_2']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_6_19_2_name}Korte</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.4" build="0.4" key="0011C80E50E410026053AA569B16689986BA77528958D98CA8622BA78AC9695201577613052AB956000000000000000000000000000000000000000042B41084"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Politician" value="6"/>
                <Trait id="Manager" value="2"/>
                <Trait id="Valor" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_6_20_1 -->
    <xsl:template match="NPCCharacter[@id='lord_6_20_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_6_20_1_name}Jigur</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="000BC80FD1000010185895BAB8A7A974AA7C383AE2FF3A45868B383997479BD70177761307BB856400000000000000000000000000000000000000003EF410C3"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Commander" value="4"/>
                <Trait id="Politician" value="6"/>
                <Trait id="Manager" value="4"/>
                <Trait id="Calculating" value="1"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Generosity" value="-1"/>
                <Trait id="Honor" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_K8_u -->
    <xsl:template match="NPCCharacter[@id='lord_K8_u']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_K8_u_name}Boronchar</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">false</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.7" build="0.7" key="0026F80E40002343B8708B896D682237D5D88B3D557E344C17243FB5B88988680077760307967D3B00000000000000000000000000000000000000003EF41002"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Commander" value="4"/>
                <Trait id="Politician" value="2"/>
                <Trait id="Manager" value="4"/>
                <Trait id="Calculating" value="-1"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Generosity" value="1"/>
                <Trait id="Honor" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_6_21_1 -->
    <xsl:template match="NPCCharacter[@id='lord_6_21_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_6_21_1_name}Esachei</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="0011FC0FC0B4300AF97613A1E4410600CB306C0A60EF0500F3B81E14741610460167761306016000000000000000000000000000000000000000000034C42083"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Politician" value="6"/>
                <Trait id="Manager" value="8"/>
                <Trait id="Calculating" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_6_22_1 -->
    <xsl:template match="NPCCharacter[@id='lord_6_22_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_6_22_1_name}Eselen</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.3" build="0.3" key="000BF80CD100300418762BAD894AB3A687C58B3A22AF3A4C5B2C769832963D44017776130799894800000000000000000000000000000000000000003EF41043"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Politician" value="8"/>
                <Trait id="Manager" value="8"/>
                <Trait id="Honor" value="1"/>
                <Trait id="Generosity" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_K9_l -->
    <xsl:template match="NPCCharacter[@id='lord_K9_l']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_K9_l_name}Valathmir Mashakian</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">false</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="000BF80CD100300418762BAD894AB3A687C58B3A22AF3A4C5B2C769832963D44017776130799894800000000000000000000000000000000000000003EF41043"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="CavalryFightingSkills" value="3"/>
                <Trait id="Commander" value="2"/>
                <Trait id="Manager" value="10"/>
                <Trait id="Politician" value="7"/>
                <Trait id="Valor" value="2"/>
                <Trait id="Generosity" value="0"/>
                <Trait id="Mercy" value="-1"/>
                <Trait id="Honor" value="1"/>
                <Trait id="Calculating" value="1"/>
                <Trait id="Egalitarian" value="0"/>
                <Trait id="Oligarchic" value="2"/>
                <Trait id="Authoritarian" value="2"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_K9_s -->
    <xsl:template match="NPCCharacter[@id='lord_K9_s']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_K9_s_name}Sevin</xsl:attribute>
            <xsl:attribute name="default_group">HorseArcher</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="000C100E007C03CD7748DA3B57429988D86DBD6945B4B664644589C5283414BC000427330A7661CB0000037F0000000F00000037049140010000000000288002"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Politician" value="8"/>
                <Trait id="Manager" value="6"/>
                <Trait id="Honor" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_K9_c1 -->
    <xsl:template match="NPCCharacter[@id='lord_K9_c1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_K9_c1_name}Altu</xsl:attribute>
            <xsl:attribute name="default_group">HorseArcher</xsl:attribute>
            <xsl:attribute name="is_female">false</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.3781" build="0.5478" key="000AB40CC0141840AD48BBECA574462983C78887724DBABB65D6686C8527AA8800D866030D8459A5000000000000000000000000000000000000000063AC10C6"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="HorseArcherFightingSkills" value="6"/>
                <Trait id="Commander" value="2"/>
                <Trait id="Politician" value="8"/>
                <Trait id="Manager" value="6"/>
                <Trait id="Valor" value="1"/>
                <Trait id="Calculating" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_K9_c2 -->
    <xsl:template match="NPCCharacter[@id='lord_K9_c2']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_K9_c2_name}Mela</xsl:attribute>
            <xsl:attribute name="default_group">HorseArcher</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.142" build="0.5648" key="001EC00CC0FC100463B8C87941868498779B3336466B8678C67EEEDEC8AE75950088260308C7A5050000000000000000000000000000000000000000711C10C3"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="HorseArcherFightingSkills" value="3"/>
                <Trait id="Commander" value="8"/>
                <Trait id="Politician" value="8"/>
                <Trait id="Manager" value="4"/>
                <Trait id="Honor" value="1"/>
                <Trait id="Generosity" value="-1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>
    <!-- lord_4_26_1 -->
    <xsl:template match="NPCCharacter[@id='lord_4_26_1']">
        <xsl:copy>
            <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'default_group' and local-name() != 'is_female']"/>
            <xsl:attribute name="name">{=aom_lord_4_26_1_name}Reingarda</xsl:attribute>
            <xsl:attribute name="default_group">Cavalry</xsl:attribute>
            <xsl:attribute name="is_female">true</xsl:attribute>
            <face>
                <BodyProperties version="4" weight="0.5" build="0.5" key="00164005802C100244849395E765385463ABA4846AA46E7A788D797974658C8901777613077C2D3C00000000000000000000000000000000000000002CEC40C3"/>
            </face>
            <skills/>
            <Traits>
                <Trait id="Politician" value="10"/>
                <Trait id="Manager" value="2"/>
                <Trait id="Valor" value="-1"/>
                <Trait id="Generosity" value="-1"/>
                <Trait id="Mercy" value="1"/>
            </Traits>
            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>
        </xsl:copy>
    </xsl:template>

</xsl:stylesheet>
