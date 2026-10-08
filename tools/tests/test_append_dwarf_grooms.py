"""Tests for append_dwarf_grooms.py on a synthetic skins.xml.

The synthetic file has two races. The dwarf race holds the four skins that carry groom lists (man, woman,
kid_2_male, kid_2_female), a younger skin with a self-closing <beard_meshes />, and a commented-out decoy
skin. The uruk race holds its own `man` skin with the same list shape, so an edit that leaks past the
dwarf race shows up as a changed uruk block.

    python -m pytest tools/tests/test_append_dwarf_grooms.py
"""
import contextlib
import glob
import io
import os
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET
from unittest import mock

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "oneoff"))
import append_dwarf_grooms as tool  # noqa: E402

FOUR = ("man", "woman", "kid_2_male", "kid_2_female")
HAIRS = ["h_a", "h_b"]
BEARDS = ["b_a", "b_b", "b_c"]
T = "\t"


def hair_xml(name, tag):
    i = T * 4
    return ('%s<hair_mesh\n%s\tname="%s"\n%s\tcover_type1="%s"\n%s\tcover_type2="%s"\n'
            '%s\tcover_type3="%s"\n%s\tcover_type4="%s">\n%s\t<style_tags>\n%s\t\t<style_tag\n'
            '%s\t\t\tname="%s" />\n%s\t</style_tags>\n%s</hair_mesh>\n'
            % (i, i, name, i, name, i, name, i, name, i, name, i, i, i, tag, i, i))


def beard_xml(name):
    i = T * 4
    return ('%s<beard_mesh\n%s\tname="%s"\n%s\tcover_type1="%s"\n%s\tcover_type2="%s"\n'
            '%s\tcover_type3="%s"\n%s\tcover_type4="%s">\n%s</beard_mesh>\n'
            % (i, i, name, i, name, i, name, i, name, i, name, i))


BALD = ('\t\t\t\t<hair_mesh>\n\t\t\t\t\t<style_tags>\n\t\t\t\t\t\t<style_tag\n\t\t\t\t\t\t\tname="Bald" />\n'
        '\t\t\t\t\t</style_tags>\n\t\t\t\t</hair_mesh>\n')
CLEAN = ('\t\t\t\t<beard_mesh>\n\t\t\t\t\t<style_tags>\n\t\t\t\t\t\t<style_tag\n'
         '\t\t\t\t\t\t\tname="Cleanshaven" />\n\t\t\t\t\t</style_tags>\n\t\t\t\t</beard_mesh>\n')


def skin(sid, hairs=HAIRS, beards=BEARDS, blank_between=True):
    hs = BALD + "".join(hair_xml(h, "TiedAcrossBack") for h in hairs)
    if beards is None:
        bs = "\t\t\t<beard_meshes />\n"
    else:
        sep = "\n" if blank_between else ""
        body = CLEAN + sep + sep.join(beard_xml(b) for b in beards)
        bs = "\t\t\t<beard_meshes>\n%s\t\t\t</beard_meshes>\n" % body
    return ('\t\t<skin\n\t\t\tgender="0"\n\t\t\tname="%s">\n\t\t\t<hair_meshes\n\t\t\t\tgroup_id="7">\n%s'
            '\t\t\t</hair_meshes>\n\t\t\t<eyebrow_meshes>\n\t\t\t\t<eyebrow_mesh\n\t\t\t\t\tname="" />\n'
            '\t\t\t</eyebrow_meshes>\n%s\t\t\t<tattoo_materials group_id="8" />\n\t\t</skin>\n'
            % (sid, hs, bs))


DECOY = ('\t\t<!-- <skin name="man"><beard_meshes><beard_mesh name="decoy" /></beard_meshes></skin> -->\n')


def parts(dwarf=None, uruk=None):
    dwarf = dwarf if dwarf is not None else [skin(s) for s in FOUR] + [skin("kid_1_male", [], None)]
    uruk = uruk if uruk is not None else [skin("man", ["u_h"], ["u_b"])]
    return (['<?xml version="1.0" encoding="utf-8"?>\n<skins>\n\t<race id="dwarf">\n', DECOY]
            + dwarf + ['\t</race>\n\t<race id="uruk">\n'] + uruk + ['\t</race>\n</skins>\n'])


def build(**kw):
    return "".join(parts(**kw))


def lists(text, race="dwarf", skin_id="man"):
    root = ET.fromstring(text.encode("utf-8"))
    r = next(r for r in root.findall("race") if r.get("id") == race)
    s = next(s for s in r.findall("skin") if s.get("name") == skin_id)
    return ([e.get("name") for e in s.find("hair_meshes")],
            [e.get("name") for e in s.find("beard_meshes")])


def uruk_block(text):
    return text[text.index('<race id="uruk">'):]


class AppendTests(unittest.TestCase):
    def test_appends_to_the_end_of_all_four_skins(self):
        text = build()
        p = tool.plan(text, ["n_1", "n_2"], [("hn_1", "Ponytail")], [])
        for sid in FOUR:
            hairs, beards = lists(p.new_text, skin_id=sid)
            self.assertEqual(beards, [None] + BEARDS + ["n_1", "n_2"], sid)
            self.assertEqual(hairs, [None] + HAIRS + ["hn_1"], sid)

    def test_existing_entries_keep_their_index(self):
        text = build()
        p = tool.plan(text, ["n_1"], [("hn_1", "Ponytail")], [])
        before_h, before_b = lists(text)
        after_h, after_b = lists(p.new_text)
        self.assertEqual(after_b[:len(before_b)], before_b)
        self.assertEqual(after_h[:len(before_h)], before_h)

    def test_beard_entry_layout_is_the_siblings_layout(self):
        p = tool.plan(build(), ["n_1"], [], [])
        self.assertIn(beard_xml("n_1") + "\t\t\t</beard_meshes>\n", p.new_text)
        self.assertEqual(p.new_text.count(beard_xml("n_1")), 4)

    def test_hair_entry_layout_is_the_siblings_layout(self):
        p = tool.plan(build(), [], [("hn_1", "TiedAcrossBack")], [])
        self.assertIn(hair_xml("hn_1", "TiedAcrossBack") + "\t\t\t</hair_meshes>\n", p.new_text)
        self.assertEqual(p.new_text.count(hair_xml("hn_1", "TiedAcrossBack")), 4)

    def test_cover_types_all_carry_the_entry_name(self):
        p = tool.plan(build(), ["n_1"], [("hn_1", "Ponytail")], [])
        root = ET.fromstring(p.new_text.encode("utf-8"))
        dwarf = root.findall("race")[0]
        for s in dwarf.findall("skin"):
            if s.get("name") not in FOUR:
                continue
            for lst, want in (("beard_meshes", "n_1"), ("hair_meshes", "hn_1")):
                e = s.find(lst)[-1]
                self.assertEqual([e.get(k) for k in ("name", "cover_type1", "cover_type2",
                                                      "cover_type3", "cover_type4")], [want] * 5)
        hair = dwarf.findall("skin")[0].find("hair_meshes")[-1]
        self.assertEqual([t.get("name") for t in hair.iter("style_tag")], ["Ponytail"])

    def test_only_the_dwarf_race_changes(self):
        text = build()
        p = tool.plan(text, ["n_1"], [("hn_1", "Ponytail")], [])
        self.assertEqual(uruk_block(p.new_text), uruk_block(text))
        self.assertEqual(lists(p.new_text, race="uruk"), lists(text, race="uruk"))
        head = text[:text.index("\t\t<!--")]
        self.assertTrue(p.new_text.startswith(head))

    def test_other_dwarf_skins_are_untouched(self):
        text = build()
        p = tool.plan(text, ["n_1"], [], [])
        self.assertEqual(lists(p.new_text, skin_id="kid_1_male"), lists(text, skin_id="kid_1_male"))
        self.assertIn(skin("kid_1_male", [], None), p.new_text)

    def test_commented_decoy_is_not_a_skin(self):
        p = tool.plan(build(), ["n_1"], [], [])
        self.assertIn(DECOY, p.new_text)

    def test_report_rows_carry_counts_and_names(self):
        p = tool.plan(build(), ["n_1", "n_2"], [("hn_1", "Ponytail")], [])
        rows = {(r.skin, r.kind): r for r in p.rows}
        self.assertEqual(len(rows), 8)
        b = rows[("man", "beard")]
        self.assertEqual((b.before, b.after, b.appended), (4, 6, ["n_1", "n_2"]))
        h = rows[("kid_2_female", "hair")]
        self.assertEqual((h.before, h.after, h.appended), (3, 4, ["hn_1"]))


class IdempotencyTests(unittest.TestCase):
    def test_second_run_changes_nothing_and_reports_skips(self):
        first = tool.plan(build(), ["n_1", "n_2"], [("hn_1", "Ponytail")], [])
        second = tool.plan(first.new_text, ["n_1", "n_2"], [("hn_1", "Ponytail")], [])
        self.assertEqual(second.new_text, first.new_text)
        self.assertTrue(all(r.appended == [] for r in second.rows))
        self.assertEqual([r for r in second.rows if r.kind == "beard"][0].skipped, ["n_1", "n_2"])

    def test_present_at_the_tail_is_skipped_and_the_rest_appended(self):
        p = tool.plan(build(), ["b_c", "n_1"], [], [])
        row = [r for r in p.rows if r.skin == "man" and r.kind == "beard"][0]
        self.assertEqual((row.skipped, row.appended), (["b_c"], ["n_1"]))
        self.assertEqual(lists(p.new_text)[1][-2:], ["b_c", "n_1"])

    def test_present_but_not_at_the_append_position_is_refused(self):
        with self.assertRaisesRegex(tool.GroomError, "b_b"):
            tool.plan(build(), ["b_b"], [], [])

    def test_present_hair_in_the_middle_is_refused(self):
        with self.assertRaisesRegex(tool.GroomError, "h_a"):
            tool.plan(build(), [], [("h_a", "TiedAcrossBack")], [])

    def test_a_later_request_present_while_an_earlier_one_is_absent_is_refused(self):
        with self.assertRaisesRegex(tool.GroomError, "order"):
            tool.plan(build(), ["n_1", "b_c"], [], [])

    def test_present_in_one_skin_only_is_handled_per_skin(self):
        dwarf = [skin("man", HAIRS, BEARDS + ["n_1"])] + [skin(s) for s in FOUR[1:]]
        p = tool.plan(build(dwarf=dwarf), ["n_1"], [], [])
        by = {r.skin: r for r in p.rows if r.kind == "beard"}
        self.assertEqual(by["man"].skipped, ["n_1"])
        self.assertEqual(by["woman"].appended, ["n_1"])


class RemoveTests(unittest.TestCase):
    def test_remove_of_the_appended_tail_restores_the_original_bytes(self):
        text = build()
        added = tool.plan(text, ["n_1", "n_2"], [("hn_1", "Ponytail")], [])
        back = tool.plan(added.new_text, [], [], ["n_1", "n_2", "hn_1"])
        self.assertEqual(back.new_text, text)

    def test_remove_reports_counts_and_names(self):
        added = tool.plan(build(), ["n_1", "n_2"], [], [])
        back = tool.plan(added.new_text, [], [], ["n_2"])
        row = [r for r in back.rows if r.skin == "woman" and r.kind == "beard"][0]
        self.assertEqual((row.before, row.after, row.removed), (6, 5, ["n_2"]))

    def test_refused_when_a_kept_entry_follows(self):
        with self.assertRaisesRegex(tool.GroomError, "shift"):
            tool.plan(build(), [], [], ["b_b"])

    def test_refused_when_only_part_of_a_run_is_removed(self):
        with self.assertRaisesRegex(tool.GroomError, "b_c"):
            tool.plan(build(), [], [], ["b_a", "b_b"])

    def test_a_contiguous_tail_run_is_allowed(self):
        p = tool.plan(build(), [], [], ["b_b", "b_c"])
        self.assertEqual(lists(p.new_text)[1], [None, "b_a"])

    def test_hair_removal_uses_the_same_rule(self):
        with self.assertRaises(tool.GroomError):
            tool.plan(build(), [], [], ["h_a"])
        p = tool.plan(build(), [], [], ["h_b"])
        self.assertEqual(lists(p.new_text)[0], [None, "h_a"])

    def test_absent_name_is_a_no_op_and_reported(self):
        text = build()
        p = tool.plan(text, [], [], ["sk_not_there"])
        self.assertEqual(p.new_text, text)
        self.assertTrue(all(r.absent == ["sk_not_there"] for r in p.rows))

    def test_removal_never_touches_the_other_race(self):
        dwarf = [skin(s) for s in FOUR] + [skin("kid_1_male", [], None)]
        text = build(dwarf=dwarf, uruk=[skin("man", HAIRS, BEARDS)])
        p = tool.plan(text, [], [], ["b_c"])
        self.assertEqual(lists(p.new_text, race="uruk")[1], [None] + BEARDS)
        self.assertEqual(uruk_block(p.new_text), uruk_block(text))

    def test_the_decoy_comment_entry_is_not_removable(self):
        p = tool.plan(build(), [], [], ["decoy"])
        self.assertIn(DECOY, p.new_text)


class RefusalTests(unittest.TestCase):
    def test_missing_dwarf_race(self):
        text = build().replace('<race id="dwarf">', '<race id="dwarf2">')
        with self.assertRaisesRegex(tool.GroomError, "dwarf"):
            tool.plan(text, ["n_1"], [], [])

    def test_missing_target_skin(self):
        text = build(dwarf=[skin(s) for s in FOUR[:3]])
        with self.assertRaisesRegex(tool.GroomError, "kid_2_female"):
            tool.plan(text, ["n_1"], [], [])

    def test_self_closing_target_list(self):
        text = build(dwarf=[skin("man", HAIRS, None)] + [skin(s) for s in FOUR[1:]])
        with self.assertRaisesRegex(tool.GroomError, "man"):
            tool.plan(text, ["n_1"], [], [])

    def test_a_result_that_does_not_parse_is_refused(self):
        with mock.patch.object(tool, "beard_entry", return_value="\t\t\t\t<beard_mesh\n"):
            with self.assertRaisesRegex(tool.GroomError, "parse"):
                tool.plan(build(), ["n_1"], [], [])

    def test_a_result_with_the_wrong_tail_is_refused(self):
        real = tool.beard_entry
        with mock.patch.object(tool, "beard_entry", side_effect=lambda n, *a: real("other_" + n, *a)):
            with self.assertRaisesRegex(tool.GroomError, "tail"):
                tool.plan(build(), ["n_1"], [], [])


class ByteFaithfulTests(unittest.TestCase):
    def test_crlf_and_bom_survive(self):
        text = build().replace("\n", "\r\n")
        data = b"\xef\xbb\xbf" + text.encode("utf-8")
        p = tool.plan(data.decode("utf-8"), ["n_1"], [("hn_1", "Ponytail")], [])
        out = p.new_text.encode("utf-8")
        self.assertTrue(out.startswith(b"\xef\xbb\xbf"))
        self.assertEqual(out.count(b"\n"), out.count(b"\r\n"))
        self.assertIn(beard_xml("n_1").replace("\n", "\r\n"), p.new_text)

    def test_lf_file_gets_lf_only(self):
        p = tool.plan(build(), ["n_1"], [], [])
        self.assertNotIn("\r", p.new_text)


class DiffTests(unittest.TestCase):
    def test_diff_shows_only_insertions(self):
        p = tool.plan(build(), ["n_1"], [], [])
        d = tool.unified_diff(p, "skins.xml")
        plus = [x for x in d if x.startswith("+") and not x.startswith("+++")]
        minus = [x for x in d if x.startswith("-") and not x.startswith("---")]
        self.assertEqual(len(plus), 4 * 7)
        self.assertEqual(minus, [])
        self.assertTrue(d[0].startswith("--- a/"))

    def test_diff_for_removal_shows_only_deletions(self):
        added = tool.plan(build(), ["n_1"], [], [])
        p = tool.plan(added.new_text, [], [], ["n_1"])
        d = tool.unified_diff(p, "skins.xml")
        self.assertEqual([x for x in d if x.startswith("+") and not x.startswith("+++")], [])
        self.assertEqual(len([x for x in d if x.startswith("-") and not x.startswith("---")]), 4 * 7)

    def test_hunk_header_line_numbers_are_right(self):
        text = build()
        p = tool.plan(text, ["n_1"], [], [])
        d = tool.unified_diff(p, "skins.xml")
        new_lines = p.new_text.splitlines(keepends=True)
        old_lines = text.splitlines(keepends=True)
        hunk = [i for i, x in enumerate(d) if x.startswith("@@")]
        self.assertEqual(len(hunk), 4)
        for h in hunk:
            head = d[h]
            import re
            a, b, c, e = map(int, re.match(r"@@ -(\d+),(\d+) \+(\d+),(\d+) @@", head).groups())
            body = d[h + 1:]
            nxt = [i for i, x in enumerate(body) if x.startswith("@@")]
            body = body[:nxt[0]] if nxt else body
            old = [x[1:] for x in body if x[0] in " -"]
            new = [x[1:] for x in body if x[0] in " +"]
            self.assertEqual(old, old_lines[a - 1:a - 1 + b])
            self.assertEqual(new, new_lines[c - 1:c - 1 + e])


def run_main(argv):
    out, err = io.StringIO(), io.StringIO()
    with contextlib.redirect_stdout(out), contextlib.redirect_stderr(err):
        try:
            code = tool.main(argv)
        except SystemExit as exc:
            code = exc.code
    return code, out.getvalue(), err.getvalue()


class CliTests(unittest.TestCase):
    def setUp(self):
        self.dir = tempfile.mkdtemp(prefix="grooms-")
        self.path = os.path.join(self.dir, "skins.xml")
        self.data = build().encode("utf-8")
        with open(self.path, "wb") as fh:
            fh.write(self.data)

    def tearDown(self):
        for f in glob.glob(os.path.join(self.dir, "*")):
            os.remove(f)
        os.rmdir(self.dir)

    def read(self):
        return open(self.path, "rb").read()

    def test_dry_run_writes_nothing_and_prints_the_summary(self):
        code, out, _ = run_main(["--skins", self.path, "--beard", "n_1", "--hair", "hn_1:Ponytail"])
        self.assertEqual(code, 0)
        self.assertEqual(self.read(), self.data)
        self.assertEqual(os.listdir(self.dir), ["skins.xml"])
        self.assertIn("man", out)
        self.assertIn("beard: 4 -> 5", out)
        self.assertIn("hair: 3 -> 4", out)
        self.assertIn("n_1", out)
        self.assertIn("DRY RUN", out)
        self.assertIn("+\t\t\t\t<beard_mesh", out)

    def test_diff_is_capped_at_120_lines(self):
        args = ["--skins", self.path]
        for i in range(30):
            args += ["--beard", "n_%02d" % i]
        code, out, _ = run_main(args)
        self.assertEqual(code, 0)
        lines = out.splitlines()
        start = next(i for i, x in enumerate(lines) if x.startswith("diff:"))
        stop = next(i for i, x in enumerate(lines) if x.startswith("... "))
        self.assertEqual(stop - start - 1, 120)
        self.assertIn("more diff lines", lines[stop])

    def test_apply_backs_up_then_writes(self):
        with mock.patch.object(tool, "_stamp", return_value="20260101-120000"):
            code, out, _ = run_main(["--skins", self.path, "--beard", "n_1", "--apply"])
        self.assertEqual(code, 0)
        bak = self.path + ".bak-grooms-20260101-120000"
        self.assertEqual(open(bak, "rb").read(), self.data)
        self.assertFalse(bak.endswith(".xml"))
        self.assertEqual(lists(self.read().decode("utf-8"))[1][-1], "n_1")
        self.assertEqual(glob.glob(os.path.join(self.dir, "*.xml")), [self.path])

    def test_apply_refuses_when_the_backup_exists(self):
        bak = self.path + ".bak-grooms-20260101-120000"
        with open(bak, "wb") as fh:
            fh.write(b"precious")
        with mock.patch.object(tool, "_stamp", return_value="20260101-120000"):
            code, _, err = run_main(["--skins", self.path, "--beard", "n_1", "--apply"])
        self.assertEqual(code, 1)
        self.assertEqual(open(bak, "rb").read(), b"precious")
        self.assertEqual(self.read(), self.data)
        self.assertIn("backup", err)

    def test_apply_with_nothing_to_do_writes_no_backup(self):
        run_main(["--skins", self.path, "--beard", "n_1", "--apply"])
        files = set(os.listdir(self.dir))
        code, out, _ = run_main(["--skins", self.path, "--beard", "n_1", "--apply"])
        self.assertEqual(code, 0)
        self.assertEqual(set(os.listdir(self.dir)), files)
        self.assertIn("nothing to do", out)

    def test_refusal_exits_1(self):
        code, _, err = run_main(["--skins", self.path, "--beard", "b_b"])
        self.assertEqual(code, 1)
        self.assertIn("REFUSED", err)

    def test_remove_refused_by_a_follower_exits_1_and_writes_nothing(self):
        code, _, err = run_main(["--skins", self.path, "--remove", "b_b", "--apply"])
        self.assertEqual(code, 1)
        self.assertEqual(self.read(), self.data)
        self.assertEqual(os.listdir(self.dir), ["skins.xml"])

    def test_remove_of_an_absent_name_is_a_reported_no_op(self):
        code, out, _ = run_main(["--skins", self.path, "--remove", "sk_not_there"])
        self.assertEqual(code, 0)
        self.assertIn("not present", out)

    def test_bad_arguments_exit_2(self):
        for argv in (["--skins", self.path],
                     ["--skins", self.path, "--hair", "no_colon"],
                     ["--skins", self.path, "--hair", ":Tag"],
                     ["--skins", self.path, "--beard", "bad name"],
                     ["--skins", self.path, "--beard", "n_1", "--beard", "n_1"],
                     ["--skins", self.path, "--beard", "n_1", "--remove", "b_c"],
                     ["--skins", os.path.join(self.dir, "missing.xml"), "--beard", "n_1"],
                     ["--skins", self.path, "--bogus"]):
            code, _, _ = run_main(argv)
            self.assertEqual(code, 2, argv)

    def test_default_path_is_the_live_armory_skins(self):
        norm = tool.LIVE.replace("\\", "/")
        self.assertTrue(norm.endswith("Modules/LOTRLOME_Armory/ModuleData/skins.xml"))


if __name__ == "__main__":
    unittest.main()
