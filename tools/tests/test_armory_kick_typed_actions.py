"""No Armory action may join the engine's kick family unless it is listed here (#1499).

While an agent's own kick is in progress, the engine's kick code (the function at RVA 0x670270 of the v1.5.7
TaleWorlds.Native.dll) takes the channel whose action type is 28 to 31 (Kick, KickContinue, KickHit, WeaponBash),
reads the combat parameter id of that channel's clip and indexes the combat parameter table with it. It does not test
for "none" (-1): a clip with no combat parameter makes it read the record before the table and take a hit bone index
from foreign bytes, and it then waits on a lock word at an address made from that index. In the dump of #1499 that
word never cleared. The two dwarf attacks were typed actt_kick with such clips, and a battle hung.

So an action that TAOM plays itself, with a clip that has no combat parameter, must not carry one of these types.
The clips are binary, and this test cannot read them; it holds the list of typed actions fixed instead. It reads
every file of the Armory that declares action types: action_types.xml, the rows action_types.xslt adds, and the
files under Animations/. The repo's snapshot holds action_types.xml alone.
"""
import glob
import os
import unittest
import xml.etree.ElementTree as ET

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SNAPSHOT = os.path.join(REPO, "docs", "reference", "lotrlome-armory-snapshot", "action_types.xml")
LIVE = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory\ModuleData"
KICK_FAMILY = ("actt_kick", "actt_kick_continue", "actt_kick_hit", "actt_weapon_bash")   # Agent.ActionCodeType 28 to 31
# The mount attacks that carried the kick type before #1499; their clips have no combat parameter either (the war
# ram's recipe). By the code read for #1499 a mount's own channel does not reach the faulty read: the engine starts
# an agent's kick only with the CanKick flag (the function at RVA 0x677C60, called under agent flag 0x80000), and
# only the humanoid monsters carry that flag in monsters.xml. Not settled there: a rider's channel (act_spider_kick
# is bound to a rider clip too), and two more readers of a kick-typed clip's combat parameter (RVA 0x676AB0 and
# 0x67ACA0), which would read wrong numbers, not a lock. Do not add a name here without that proof for its agent, or
# without a clip whose combat parameter names a hit bone; give a new attack no type, as the troll's smash and the
# dwarf attacks have none.
KNOWN = {"act_spider_kick", "act_elephant_kick", "act_war_ram_butt", "act_animalia_elk_antler", "act_animalia_moose_antler"}


def kick_typed(data):
    """The names of the <action> elements whose type is in the kick family; data is the bytes of an XML or XSLT file."""
    return {a.get("name") for a in ET.fromstring(data).iter("action")
            if (a.get("type") or "").strip().lower() in KICK_FAMILY}


def live_files(root=LIVE):
    """Every file under the Armory's ModuleData that declares action types (backups have another extension)."""
    return sorted(p for ext in ("xml", "xslt") for p in glob.glob(os.path.join(root, "**", "action_types*." + ext), recursive=True))


def read(path):
    with open(path, "rb") as f:
        return f.read()


class KickTypedTests(unittest.TestCase):
    def test_the_reader_sees_every_family_type_however_the_row_is_written_and_skips_comments(self):
        data = (b'\xef\xbb\xbf<?xml version="1.0" encoding="utf-8"?>\r\n<action_types>\r\n'
                b'<action name="a" type="actt_kick" />\r\n<action type=\'actt_weapon_bash\' name=\'b\'/>\r\n'
                b'<action name="c" type = "actt_kick_hit" />\r\n<action\r\n\tname="d"\r\n\ttype="actt_kick_continue" />\r\n'
                b'<action name="e" type="ACTT_KICK"></action>\r\n'
                b'<action name="f" />\r\n<action name="g" type="actt_fall" />\r\n'
                b'<!-- <action name="h" type="actt_kick" /> -->\r\n</action_types>')
        self.assertEqual(kick_typed(data), {"a", "b", "c", "d", "e"})

    def test_the_reader_sees_a_row_that_a_stylesheet_adds(self):
        data = (b'<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">'
                b'<xsl:template match="action_types"><xsl:copy><xsl:apply-templates/>'
                b'<action name="x" type="actt_kick"/><action name="y"/></xsl:copy></xsl:template></xsl:stylesheet>')
        self.assertEqual(kick_typed(data), {"x"})

    def test_a_file_that_does_not_parse_fails_the_read(self):
        with self.assertRaises(ET.ParseError):
            kick_typed(b"<action_types><action name='a' type='actt_kick'></action_types>")

    def test_the_snapshot_types_only_the_listed_mount_attacks_as_kicks(self):
        self.assertEqual(kick_typed(read(SNAPSHOT)), KNOWN)

    @unittest.skipUnless(os.path.isdir(LIVE), "the Armory is not installed here")
    def test_the_live_armory_types_only_the_listed_mount_attacks_as_kicks(self):
        files = live_files()
        self.assertIn(os.path.join(LIVE, "action_types.xml"), files)
        found = {}
        for path in files:
            for name in kick_typed(read(path)):
                found.setdefault(name, []).append(os.path.relpath(path, LIVE))
        self.assertEqual(set(found), KNOWN, found)


if __name__ == "__main__":
    unittest.main()
