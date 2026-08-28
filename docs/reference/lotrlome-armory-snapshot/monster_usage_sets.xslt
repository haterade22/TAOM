<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
	<xsl:output method="xml" indent="yes"/>
	<xsl:template match="@* | node()">
		<xsl:copy>
			<xsl:apply-templates select="@* | node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="monster_usage_set[@id='human']/monster_usage_mountings">
		<xsl:copy>
			<xsl:apply-templates select="@* | node()"/>
			<monster_usage_mounting mount_id="elephant" is_mounted="False" is_fast="False" direction="left" action="act_mount_horse_from_left" />
			<monster_usage_mounting mount_id="elephant" is_mounted="False" is_fast="True" direction="left" action="act_mount_horse_fast_from_left" />
			<monster_usage_mounting mount_id="elephant" is_mounted="False" is_fast="False" direction="right" action="act_mount_horse_from_right" />
			<monster_usage_mounting mount_id="elephant" is_mounted="False" is_fast="True" direction="right" action="act_mount_horse_fast_from_right" />
			<monster_usage_mounting mount_id="elephant" is_mounted="True" is_fast="False" direction="left" action="act_dismount_elephant_to_left" />
			<monster_usage_mounting mount_id="elephant" is_mounted="True" is_fast="False" direction="right" action="act_dismount_elephant_to_right" />
			<!-- War chariot (Rhûn Wainriders — ROT 8.0 port, issue #279). Mount = chariot-specific
			     actt_mount actions (clip chariot_mount_rider_from_right); dismount = vanilla horse. -->
			<monster_usage_mounting mount_id="chariot" is_mounted="False" is_fast="False" direction="left" action="act_mount_chariot_from_left" />
			<monster_usage_mounting mount_id="chariot" is_mounted="False" is_fast="True" direction="left" action="act_mount_chariot_fast_from_left" />
			<monster_usage_mounting mount_id="chariot" is_mounted="False" is_fast="False" direction="right" action="act_mount_chariot_from_right" />
			<monster_usage_mounting mount_id="chariot" is_mounted="False" is_fast="True" direction="right" action="act_mount_chariot_fast_from_right" />
			<monster_usage_mounting mount_id="chariot" is_mounted="True" is_fast="False" direction="left" action="act_dismount_horse_to_left" />
			<monster_usage_mounting mount_id="chariot" is_mounted="True" is_fast="False" direction="right" action="act_dismount_horse_to_right" />
			<!-- Warg (absorbed from Alliance.Wargs 2026-08-28, verbatim from lotr_monster_usage_warg.xslt). -->
			<monster_usage_mounting mount_id="warg" is_mounted="False" is_fast="False" direction="left" action="act_mount_horse_from_left" />
			<monster_usage_mounting mount_id="warg" is_mounted="False" is_fast="True" direction="left" action="act_mount_horse_fast_from_left" />
			<monster_usage_mounting mount_id="warg" is_mounted="False" is_fast="False" direction="right" action="act_mount_horse_from_right" />
			<monster_usage_mounting mount_id="warg" is_mounted="False" is_fast="True" direction="right" action="act_mount_horse_fast_from_right" />
			<monster_usage_mounting mount_id="warg" is_mounted="True" is_fast="False" direction="left" action="act_dismount_horse_to_left" />
			<monster_usage_mounting mount_id="warg" is_mounted="True" is_fast="False" direction="right" action="act_dismount_horse_to_right" />
		</xsl:copy>
	</xsl:template>

	<xsl:template match="monster_usage_set[@id='human']/monster_usage_falls">
		<xsl:copy>
			<xsl:apply-templates select="@* | node()"/>
			<monster_usage_fall mount_id="elephant" is_heavy="False" is_left_stance="False" direction="right" body_part="none" death_type="other" action="act_elephant_rider_fall_left" />
			<monster_usage_fall mount_id="elephant" is_heavy="False" is_left_stance="False" direction="left" body_part="none" death_type="other" action="act_elephant_rider_fall_right" />
			<monster_usage_fall mount_id="elephant" is_heavy="True" is_left_stance="False" direction="right" body_part="none" death_type="other" action="act_elephant_rider_fall_left" />
			<monster_usage_fall mount_id="elephant" is_heavy="True" is_left_stance="False" direction="left" body_part="none" death_type="other" action="act_elephant_rider_fall_right" />
			<monster_usage_fall mount_id="elephant" is_heavy="True" is_left_stance="False" direction="front" body_part="none" death_type="other" action="act_elephant_rider_fall_left" />
			<monster_usage_fall mount_id="elephant" is_heavy="False" is_left_stance="False" direction="front" body_part="none" death_type="other" action="act_elephant_rider_fall_right" />
			<monster_usage_fall mount_id="elephant" is_heavy="False" is_left_stance="False" direction="back" body_part="none" death_type="other" action="act_elephant_rider_fall_left" />
			<monster_usage_fall mount_id="elephant" is_heavy="True" is_left_stance="False" direction="back" body_part="none" death_type="other" action="act_elephant_rider_fall_right" />
			<!-- War chariot rider falls (Rhûn Wainriders — ROT 8.0 port, issue #279): vanilla act_fall_rider_* actions, 1-for-1 from ROT. -->
			<monster_usage_fall mount_id="chariot" is_heavy="False" is_left_stance="False" direction="right" body_part="none" death_type="other" action="act_fall_rider_forward_left" />
			<monster_usage_fall mount_id="chariot" is_heavy="False" is_left_stance="False" direction="left" body_part="none" death_type="other" action="act_fall_rider_forward_right" />
			<monster_usage_fall mount_id="chariot" is_heavy="True" is_left_stance="False" direction="right" body_part="none" death_type="other" action="act_fall_rider_forward_left" />
			<monster_usage_fall mount_id="chariot" is_heavy="True" is_left_stance="False" direction="left" body_part="none" death_type="other" action="act_fall_rider_forward_right" />
			<monster_usage_fall mount_id="chariot" is_heavy="True" is_left_stance="False" direction="front" body_part="none" death_type="other" action="act_fall_rider_back" />
			<monster_usage_fall mount_id="chariot" is_heavy="False" is_left_stance="False" direction="front" body_part="none" death_type="other" action="act_fall_rider_back" />
			<monster_usage_fall mount_id="chariot" is_heavy="False" is_left_stance="False" direction="back" body_part="none" death_type="other" action="act_fall_rider_forward_right_slow" />
			<monster_usage_fall mount_id="chariot" is_heavy="True" is_left_stance="False" direction="back" body_part="none" death_type="other" action="act_fall_rider_forward_right_slow" />
			<!-- Warg (absorbed from Alliance.Wargs 2026-08-28, verbatim from lotr_monster_usage_warg.xslt). -->
			<monster_usage_fall mount_id="warg" is_heavy="False" is_left_stance="False" direction="right" body_part="none" death_type="other" action="act_fall_rider_forward_left" />
			<monster_usage_fall mount_id="warg" is_heavy="False" is_left_stance="False" direction="left" body_part="none" death_type="other" action="act_fall_rider_forward_right" />
			<monster_usage_fall mount_id="warg" is_heavy="True" is_left_stance="False" direction="right" body_part="none" death_type="other" action="act_fall_rider_forward_left" />
			<monster_usage_fall mount_id="warg" is_heavy="True" is_left_stance="False" direction="left" body_part="none" death_type="other" action="act_fall_rider_forward_right" />
			<monster_usage_fall mount_id="warg" is_heavy="True" is_left_stance="False" direction="front" body_part="none" death_type="other" action="act_fall_rider_back" />
			<monster_usage_fall mount_id="warg" is_heavy="False" is_left_stance="False" direction="front" body_part="none" death_type="other" action="act_fall_rider_back" />
			<monster_usage_fall mount_id="warg" is_heavy="False" is_left_stance="False" direction="back" body_part="none" death_type="other" action="act_fall_rider_forward_right_slow" />
			<monster_usage_fall mount_id="warg" is_heavy="True" is_left_stance="False" direction="back" body_part="none" death_type="other" action="act_fall_rider_forward_right_slow" />
		</xsl:copy>
	</xsl:template>

	<xsl:template match="monster_usage_set[@id='human']/monster_usage_strikes">
		<xsl:copy>
			<xsl:apply-templates select="@* | node()"/>
			<monster_usage_strike mount_id="elephant" is_heavy="False" is_left_stance="False" direction="front_right" body_part="chest" impact="5" action="act_elephant_rider_fall_right" />
			<monster_usage_strike mount_id="elephant" is_heavy="False" is_left_stance="False" direction="front_left" body_part="chest" impact="5" action="act_elephant_rider_fall_left" />
			<monster_usage_strike mount_id="elephant" is_heavy="False" is_left_stance="False" direction="back_right" body_part="chest" impact="5" action="act_elephant_rider_fall_right" />
			<monster_usage_strike mount_id="elephant" is_heavy="False" is_left_stance="False" direction="back_left" body_part="chest" impact="5" action="act_elephant_rider_fall_left" />
			<monster_usage_strike mount_id="elephant" is_heavy="True" is_left_stance="False" direction="front_right" body_part="chest" impact="5" action="act_elephant_rider_fall_right" />
			<monster_usage_strike mount_id="elephant" is_heavy="True" is_left_stance="False" direction="front_left" body_part="chest" impact="5" action="act_elephant_rider_fall_left" />
			<monster_usage_strike mount_id="elephant" is_heavy="True" is_left_stance="False" direction="back_right" body_part="chest" impact="5" action="act_elephant_rider_fall_right" />
			<monster_usage_strike mount_id="elephant" is_heavy="True" is_left_stance="False" direction="back_left" body_part="chest" impact="5" action="act_elephant_rider_fall_left" />
			<!-- War chariot rider strikes (Rhûn Wainriders — ROT 8.0 port, issue #279): vanilla act_rider_only_fall_* actions, 1-for-1 from ROT. -->
			<monster_usage_strike mount_id="chariot" is_heavy="False" is_left_stance="False" direction="front_right" body_part="chest" impact="5" action="act_rider_only_fall_right_front" />
			<monster_usage_strike mount_id="chariot" is_heavy="False" is_left_stance="False" direction="front_left" body_part="chest" impact="5" action="act_rider_only_fall_left_front" />
			<monster_usage_strike mount_id="chariot" is_heavy="False" is_left_stance="False" direction="back_right" body_part="chest" impact="5" action="act_rider_only_fall_right_back" />
			<monster_usage_strike mount_id="chariot" is_heavy="False" is_left_stance="False" direction="back_left" body_part="chest" impact="5" action="act_rider_only_fall_left_back" />
			<monster_usage_strike mount_id="chariot" is_heavy="True" is_left_stance="False" direction="front_right" body_part="chest" impact="5" action="act_rider_only_fall_right_front_heavy" />
			<monster_usage_strike mount_id="chariot" is_heavy="True" is_left_stance="False" direction="front_left" body_part="chest" impact="5" action="act_rider_only_fall_left_front_heavy" />
			<monster_usage_strike mount_id="chariot" is_heavy="True" is_left_stance="False" direction="back_right" body_part="chest" impact="5" action="act_rider_only_fall_right_back_heavy" />
			<monster_usage_strike mount_id="chariot" is_heavy="True" is_left_stance="False" direction="back_left" body_part="chest" impact="5" action="act_rider_only_fall_left_back_heavy" />
			<!-- Warg (absorbed from Alliance.Wargs 2026-08-28, verbatim from lotr_monster_usage_warg.xslt). -->
			<monster_usage_strike mount_id="warg" is_heavy="False" is_left_stance="False" direction="front_right" body_part="chest" impact="5" action="act_rider_only_fall_right_front" />
			<monster_usage_strike mount_id="warg" is_heavy="False" is_left_stance="False" direction="front_left" body_part="chest" impact="5" action="act_rider_only_fall_left_front" />
			<monster_usage_strike mount_id="warg" is_heavy="False" is_left_stance="False" direction="back_right" body_part="chest" impact="5" action="act_rider_only_fall_right_back" />
			<monster_usage_strike mount_id="warg" is_heavy="False" is_left_stance="False" direction="back_left" body_part="chest" impact="5" action="act_rider_only_fall_left_back" />
			<monster_usage_strike mount_id="warg" is_heavy="True" is_left_stance="False" direction="front_right" body_part="chest" impact="5" action="act_rider_only_fall_right_front_heavy" />
			<monster_usage_strike mount_id="warg" is_heavy="True" is_left_stance="False" direction="front_left" body_part="chest" impact="5" action="act_rider_only_fall_left_front_heavy" />
			<monster_usage_strike mount_id="warg" is_heavy="True" is_left_stance="False" direction="back_right" body_part="chest" impact="5" action="act_rider_only_fall_right_back_heavy" />
			<monster_usage_strike mount_id="warg" is_heavy="True" is_left_stance="False" direction="back_left" body_part="chest" impact="5" action="act_rider_only_fall_left_back_heavy" />
		</xsl:copy>
	</xsl:template>
</xsl:stylesheet>