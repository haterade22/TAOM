"""Tests for bind_elf_voice.py on a synthetic skins.xml.

The synthetic file holds a dwarf race, the elf race (written `<race` + newline + `id="elf">` as the live file
writes it) and a sauron race that is a copy of the elf skins, so a text replace of the vanilla pool would hit
three skins.

    python -m pytest tools/tests/test_bind_elf_voice.py
"""
import os
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "oneoff"))
import bind_elf_voice as tool  # noqa: E402

VANILLA = ["male_%02d" % i for i in range(2, 9)]
FEMALE = ["female_%02d" % i for i in range(1, 6)]
TEEN = ["male_%02d" % i for i in range(2, 7)]


def pool(names):
    rows = "".join('\t\t\t\t<voice_type\n\t\t\t\t\tname="%s" />\n' % n for n in names)
    return "\t\t\t<voice_types>\n%s\t\t\t</voice_types>\n" % rows


def skin(name, gender, maturity, names, before="", eq="="):
    return ('\t\t<skin\n\t\t\tgender="%s"\n\t\t\tname="%s"\n\t\t\tmesh_maturity_type%s"%s">\n%s%s'
            '\t\t\t<hair_meshes />\n\t\t</skin>\n' % (gender, name, eq, maturity, before, pool(names)))


def elf_skins(adult_male=None, before="", eq="="):
    return (skin("man", 0, "adult", VANILLA if adult_male is None else adult_male, before, eq)
            + skin("woman", 1, "adult", FEMALE) + skin("kid_2_male", 0, "teenager", TEEN))


def build(elf=None, eol="\n", head=""):
    text = ('<?xml version="1.0" encoding="utf-8"?>\n<skins>\n' + head
            + '\t<race id="dwarf">\n' + skin("man", 0, "adult", ["dwarf_01"]) + '\t</race>\n'
            + '\t<race\n\t\tid="elf">\n' + (elf_skins() if elf is None else elf) + '\t</race>\n'
            + '\t<race\n\t\tid="sauron">\n' + elf_skins() + '\t</race>\n</skins>\n')
    return text.replace("\n", eol).encode("utf-8")


def pools(data):
    out = {}
    for race in ET.fromstring(data).iter("race"):
        for s in race.findall("skin"):
            out[(race.get("id"), s.get("name"))] = [v.get("name") for v in s.iter("voice_type")]
    return out


def in_elf_only(data, old, new):
    """Replace old by new inside the elf race only (the sauron race holds the same text)."""
    start = data.index(b'id="elf">')
    end = data.index(b"</race>", start)
    assert data.count(old, start, end) >= 1
    return data[:start] + data[start:end].replace(old, new) + data[end:]


class BindElfVoiceTests(unittest.TestCase):
    def test_only_the_elf_adult_male_pool_changes(self):
        new, _ = tool.plan(build(), True)
        after = pools(new)
        self.assertEqual(after[("elf", "man")], ["elf_01"])
        self.assertEqual(after[("sauron", "man")], VANILLA)
        self.assertEqual(after[("elf", "woman")], FEMALE)
        self.assertEqual(after[("elf", "kid_2_male")], TEEN)
        self.assertEqual(after[("dwarf", "man")], ["dwarf_01"])

    def test_the_written_pool_keeps_the_file_layout(self):
        new, _ = tool.plan(build(), True)
        self.assertIn(b'\t\t\t<voice_types>\n\t\t\t\t<voice_type\n\t\t\t\t\tname="elf_01" />\n\t\t\t</voice_types>\n', new)

    def test_revert_gives_back_the_original_bytes(self):
        data = build()
        new, _ = tool.plan(data, True)
        back, _ = tool.plan(new, False)
        self.assertEqual(back, data)

    def test_second_run_changes_nothing(self):
        new, _ = tool.plan(build(), True)
        again, state = tool.plan(new, True)
        self.assertIsNone(again)
        self.assertEqual(state, ["elf_01"])

    def test_crlf_survives(self):
        new, _ = tool.plan(build(eol="\r\n"), True)
        self.assertEqual(new.count(b"\n"), new.count(b"\r\n"))
        self.assertEqual(pools(new)[("elf", "man")], ["elf_01"])

    def test_lf_file_gains_no_carriage_return(self):
        new, _ = tool.plan(build(), True)
        self.assertEqual(new.count(b"\r"), 0)

    def test_spaced_maturity_attribute_is_found(self):
        new, _ = tool.plan(build(elf=elf_skins(eq=" =")), True)
        self.assertEqual(pools(new)[("elf", "man")], ["elf_01"])

    def test_unexpected_pool_is_refused(self):
        with self.assertRaises(tool.Refused):
            tool.plan(build(elf=elf_skins(adult_male=["male_02", "male_03"])), True)

    def test_pool_that_mixes_the_voice_with_vanilla_is_refused(self):
        with self.assertRaises(tool.Refused):
            tool.plan(build(elf=elf_skins(adult_male=VANILLA + ["elf_01"])), True)

    def test_commented_pool_in_the_skin_is_not_the_one_edited(self):
        comment = "\t\t\t<!--\n" + pool(VANILLA) + "\t\t\t-->\n"
        data = build(elf=elf_skins(before=comment))
        new, _ = tool.plan(data, True)
        self.assertEqual(pools(new)[("elf", "man")], ["elf_01"])
        self.assertIn(comment.encode("utf-8"), new)

    def test_commented_elf_race_is_not_the_one_edited(self):
        ghost = '\t<!--\n\t<race\n\t\tid="elf">\n' + elf_skins() + '\t</race>\n\t-->\n'
        data = build(head=ghost)
        new, _ = tool.plan(data, True)
        self.assertEqual(pools(new)[("elf", "man")], ["elf_01"])
        self.assertIn(ghost.encode("utf-8"), new)

    def test_comment_inside_the_pool_is_refused(self):
        data = build().replace(b'\t\t\t\t<voice_type\n\t\t\t\t\tname="male_02" />\n',
                               b'\t\t\t\t<!-- note -->\n\t\t\t\t<voice_type\n\t\t\t\t\tname="male_02" />\n', 2)
        with self.assertRaises(tool.Refused):
            tool.plan(data, True)

    def test_missing_elf_race_is_refused(self):
        with self.assertRaises(tool.Refused):
            tool.plan(build().replace(b'id="elf"', b'id="elv"'), True)

    def test_two_adult_male_elf_skins_are_refused(self):
        with self.assertRaises(tool.Refused):
            tool.plan(build(elf=elf_skins() + skin("man2", 0, "adult", VANILLA)), True)

    def test_two_pools_in_the_skin_are_refused(self):
        with self.assertRaises(tool.Refused):
            tool.plan(build(elf=elf_skins(before=pool(["male_02"]))), True)

    def test_skin_the_text_scan_misses_is_refused(self):
        # A second adult male skin whose gender is an entity: a parser reads "0", the text scan does not.
        data = build(elf=elf_skins() + skin("man2", "&#48;", "adult", VANILLA))
        self.assertEqual(pools(data)[("elf", "man2")], VANILLA)
        with self.assertRaises(tool.Refused):
            tool.plan(data, True)

    def test_decoy_race_the_text_scan_picks_is_refused(self):
        data = (build().replace(b'\t<race\n\t\tid="elf">', b"\t<race\n\t\tid='elf'>")
                .replace(b'\t<race\n\t\tid="sauron">', b'\t<race\n\t\tid="sauron" x-id="elf">'))
        with self.assertRaises(tool.Refused):
            tool.plan(data, True)

    def test_lookalike_attribute_is_not_read_as_the_name(self):
        bound, _ = tool.plan(build(), True)
        data = bound.replace(b'\t\t\t\t<voice_type\n\t\t\t\t\tname="elf_01" />\n',
                             b'\t\t\t\t<voice_type\n\t\t\t\t\told-name="elf_01" name="male_02" />\n')
        self.assertEqual(pools(data)[("elf", "man")], ["male_02"])
        with self.assertRaises(tool.Refused):
            tool.plan(data, True)

    def test_extra_attribute_in_the_pool_is_refused(self):
        data = in_elf_only(build(), b'name="male_03" />', b'name="male_03" weight="3" />')
        self.assertEqual(pools(data)[("elf", "man")], VANILLA)
        with self.assertRaises(tool.Refused):
            tool.plan(data, True)

    def test_pool_in_another_layout_is_refused(self):
        flat = "\t\t\t<voice_types>" + "".join('<voice_type name="%s" />' % n for n in VANILLA) + "</voice_types>\n"
        data = in_elf_only(build(), pool(VANILLA).encode("utf-8"), flat.encode("utf-8"))
        self.assertEqual(pools(data)[("elf", "man")], VANILLA)
        with self.assertRaises(tool.Refused):
            tool.plan(data, True)

    def test_document_that_does_not_parse_is_refused_even_when_the_pool_reads_done(self):
        bound, _ = tool.plan(build(), True)
        with self.assertRaises(tool.Refused):
            tool.plan(bound.replace(b"</skins>", b"</skin>"), True)


class BindElfVoiceMainTests(unittest.TestCase):
    def setUp(self):
        self.dir = tempfile.TemporaryDirectory()
        self.addCleanup(self.dir.cleanup)
        self.path = os.path.join(self.dir.name, "skins.xml")
        with open(self.path, "wb") as fh:
            fh.write(build(eol="\r\n"))
        self.running = tool.game_or_kit_running
        tool.game_or_kit_running = lambda: False
        self.addCleanup(lambda: setattr(tool, "game_or_kit_running", self.running))

    def files(self):
        return sorted(os.listdir(self.dir.name))

    def on_disk(self):
        with open(self.path, "rb") as fh:
            return fh.read()

    def test_dry_run_writes_nothing(self):
        self.assertEqual(tool.main(["--file", self.path]), 0)
        self.assertEqual(self.on_disk(), build(eol="\r\n"))
        self.assertEqual(self.files(), ["skins.xml"])

    def test_apply_writes_the_file_and_a_backup_that_is_not_xml(self):
        self.assertEqual(tool.main(["--file", self.path, "--apply"]), 0)
        self.assertEqual(pools(self.on_disk())[("elf", "man")], ["elf_01"])
        backups = [f for f in self.files() if f != "skins.xml"]
        self.assertEqual(len(backups), 1)
        self.assertFalse(backups[0].lower().endswith(".xml"))
        with open(os.path.join(self.dir.name, backups[0]), "rb") as fh:
            self.assertEqual(fh.read(), build(eol="\r\n"))

    def test_apply_without_backup_writes_only_the_file(self):
        self.assertEqual(tool.main(["--file", self.path, "--apply", "--no-backup"]), 0)
        self.assertEqual(self.files(), ["skins.xml"])
        self.assertEqual(pools(self.on_disk())[("elf", "man")], ["elf_01"])

    def test_apply_is_refused_while_the_game_runs(self):
        tool.game_or_kit_running = lambda: True
        self.assertEqual(tool.main(["--file", self.path, "--apply"]), 2)
        self.assertEqual(self.on_disk(), build(eol="\r\n"))

    def test_refused_pool_exits_one_and_writes_nothing(self):
        refused = build(elf=elf_skins(adult_male=["male_02"]))
        with open(self.path, "wb") as fh:
            fh.write(refused)
        self.assertEqual(tool.main(["--file", self.path, "--apply"]), 1)
        self.assertEqual(self.files(), ["skins.xml"])
        self.assertEqual(self.on_disk(), refused)

    def test_no_backup_is_refused_on_the_live_file(self):
        live = tool.LIVE
        tool.LIVE = self.path
        self.addCleanup(lambda: setattr(tool, "LIVE", live))
        self.assertEqual(tool.main(["--file", self.path, "--apply", "--no-backup"]), 1)
        self.assertEqual(self.on_disk(), build(eol="\r\n"))
        self.assertEqual(self.files(), ["skins.xml"])

    def test_a_written_file_that_does_not_replan_is_put_back(self):
        real, calls = tool.plan, []

        def flaky(data, bind):
            calls.append(1)
            if len(calls) > 1:
                raise tool.Refused("the re-read failed")
            return real(data, bind)

        tool.plan = flaky
        self.addCleanup(lambda: setattr(tool, "plan", real))
        self.assertEqual(tool.main(["--file", self.path, "--apply", "--no-backup"]), 1)
        self.assertEqual(self.on_disk(), build(eol="\r\n"))


if __name__ == "__main__":
    unittest.main()
