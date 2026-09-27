"""Tests for restore_adult_woman_dwarf.py on a synthetic skins.xml.

The synthetic file has two races and, in the dwarf race, the male adult, a commented-out decoy skin,
the adult woman and all four younger female skins. Every skin, and the uruk woman too, carries the
same stopgap values, so a global search-and-replace would hit them all.

    python -m pytest tools/tests/test_restore_adult_woman_dwarf.py
"""
import contextlib
import difflib
import glob
import io
import os
import sys
import tempfile
import unittest
from unittest import mock

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "oneoff"))
import restore_adult_woman_dwarf as tool  # noqa: E402

STOPGAP = {
    "header": [(a, s) for a, s, _f in tool.HEADER],
    "eyebrows": [s for s, _f in tool.EYEBROWS],
    "face": tool.FACE_STOPGAP,
}
FEMALE = {
    "header": [(a, f) for a, _s, f in tool.HEADER],
    "eyebrows": [f for _s, f in tool.EYEBROWS],
    "face": tool.FACE_FEMALE,
}


def skin(gender, name, maturity, vals=STOPGAP, extra=""):
    head = "".join('\t\t\t%s="%s"\n' % (a, v) for a, v in vals["header"])
    brows = "".join('\t\t\t\t<eyebrow_mesh\n\t\t\t\t\tname="%s" />\n' % v for v in vals["eyebrows"])
    faces = "".join('\t\t\t\t<face_texture name="%s"\n\t\t\t\t\t\t\t   lod_material="%s"\n'
                    '\t\t\t\t\t\t\t  tags="face_texture%d">\n\t\t\t\t</face_texture>\n'
                    % (vals["face"], vals["face"], i) for i in range(1, 5))
    # Padding keeps the three edited regions more than 2 x 3 context lines apart, as in the real file.
    pad = "".join('\t\t\t\t<deform_key id="k%d" />\n' % i for i in range(8))
    return ('\t\t<skin\n\t\t\tgender="%s"\n\t\t\tname="%s"\n\t\t\tmesh_maturity_type="%s"\n'
            '\t\t\tskeleton="dwarf_skeleton_a"\n%s\t\t\tunderwear_top_mesh="">\n'
            '\t\t\t<!-- was face_meta_mesh="sm_dwarf_basemesh_a1_head" before -->\n'
            '\t\t\t<deform_keys>\n%s\t\t\t</deform_keys>\n'
            '\t\t\t<eyebrow_meshes>\n%s\t\t\t</eyebrow_meshes>\n'
            '\t\t\t<beard_meshes>\n\t\t\t\t<beard_mesh\n\t\t\t\t\tname="" />\n\t\t\t</beard_meshes>\n'
            '\t\t\t<voice_keys>\n%s\t\t\t</voice_keys>\n'
            '\t\t\t<face_textures group_id="1">\n%s\t\t\t</face_textures>\n%s'
            '\t\t\t<mouth_textures>\n\t\t\t\t<mouth_texture name="m_dwarf_basemesh_mouth_a" />\n'
            '\t\t\t</mouth_textures>\n\t\t</skin>\n'
            % (gender, name, maturity, head, pad, brows, pad, faces, extra))


DECOY = ('\t\t<!-- <skin gender="1" name="woman" mesh_maturity_type="adult"\n'
         '\t\t\tface_meta_mesh="sm_dwarf_basemesh_a1_head"></skin> -->\n')
WOMAN = "dwarf woman"


def parts(woman=None):
    """[(key, text)] in file order; `woman` replaces the dwarf adult woman block."""
    return [
        ("prolog", '<?xml version="1.0" encoding="utf-8"?>\n<skins>\n\t<race id="dwarf">\n'),
        ("dwarf man", skin("0", "man", "adult")),
        ("decoy comment", DECOY),
        (WOMAN, woman if woman is not None else skin("1", "woman", "adult")),
        ("dwarf teen male", skin("0", "kid_2_male", "teenager")),
        ("dwarf teenager female", skin("1", "kid_2_female", "teenager")),
        ("dwarf tween female", skin("1", "kid_1_female", "tween")),
        ("dwarf child female", skin("1", "kid_3_female", "child")),
        ("dwarf toddler female", skin("1", "toddler_female", "toddler")),
        ("between races", '\t</race>\n\t<race id="uruk">\n'),
        ("uruk man", skin("0", "man", "adult")),
        ("uruk woman", skin("1", "woman", "adult")),
        ("epilog", "\t</race>\n</skins>\n"),
    ]


def encode(ps, crlf=False, bom=False):
    text = "".join(t for _k, t in ps)
    if crlf:
        text = text.replace("\n", "\r\n")
    return (b"\xef\xbb\xbf" if bom else b"") + text.encode("utf-8")


RESTORED_WOMAN = skin("1", "woman", "adult", FEMALE)


class RestoreTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.path = os.path.join(self.tmp.name, "skins.xml")
        self.stamps = iter("20260926-%06d" % i for i in range(100))
        self.patches = [mock.patch.object(tool, "game_or_kit_running", return_value=False),
                        mock.patch.object(tool, "_stamp", side_effect=lambda: next(self.stamps))]
        for p in self.patches:
            p.start()

    def tearDown(self):
        for p in self.patches:
            p.stop()
        self.tmp.cleanup()

    def put(self, data):
        with open(self.path, "wb") as fh:
            fh.write(data)
        return data

    def read(self):
        with open(self.path, "rb") as fh:
            return fh.read()

    def run_tool(self, *args):
        out, err = io.StringIO(), io.StringIO()
        with contextlib.redirect_stdout(out), contextlib.redirect_stderr(err):
            rc = tool.main(["--file", self.path] + list(args))
        return rc, out.getvalue() + err.getvalue()

    def backups(self):
        return sorted(glob.glob(self.path + ".bak-*"))

    def test_dry_run_writes_nothing_and_saves_the_diff(self):
        before = self.put(encode(parts()))
        diff_path = os.path.join(self.tmp.name, "out.diff")
        rc, out = self.run_tool("--diff", diff_path)
        self.assertEqual(rc, 0, out)
        self.assertIn("DRY RUN", out)
        self.assertEqual(self.read(), before)
        self.assertEqual(self.backups(), [])
        with open(diff_path, "rb") as fh:
            diff = fh.read().decode("utf-8")
        self.assertEqual(diff.count("\n@@"), 3)

    def test_apply_changes_only_the_adult_woman_block(self):
        self.put(encode(parts()))
        rc, out = self.run_tool("--apply")
        self.assertEqual(rc, 0, out)
        self.assertEqual(self.read(), encode(parts(RESTORED_WOMAN)))

    def test_every_other_block_is_byte_identical(self):
        ps = parts()
        self.put(encode(ps))
        self.assertEqual(self.run_tool("--apply")[0], 0)
        new = self.read().decode("utf-8")
        at = 0
        for key, text in parts(RESTORED_WOMAN):
            with self.subTest(block=key):
                self.assertEqual(new[at:at + len(text)], text if key == WOMAN else dict(ps)[key])
            at += len(text)
        self.assertEqual(at, len(new))

    def test_diff_is_19_lines_in_three_hunks_inside_the_block(self):
        ps = parts()
        old = encode(ps).decode("utf-8").splitlines(keepends=True)
        self.put(encode(ps))
        p = tool.plan(encode(ps), "restore")
        diff = tool.unified_diff(p, "skins.xml")
        self.assertEqual(self.run_tool("--apply")[0], 0)
        new = self.read().decode("utf-8").splitlines(keepends=True)
        self.assertEqual(len(new), len(old))
        changed = [n for n, (a, b) in enumerate(zip(old, new), 1) if a != b]
        first = "".join(t for k, t in ps[:3]).count("\n") + 1
        last = first + ps[3][1].count("\n") - 1
        self.assertEqual(len(changed), 19)
        self.assertTrue(all(first <= n <= last for n in changed), (first, last, changed))
        self.assertEqual(tool.changed_old_lines(diff), changed)
        self.assertEqual(sum(1 for d in diff if d.startswith("@@")), 3)

    def test_diff_matches_difflib_where_difflib_is_minimal(self):
        """On the real file difflib found the minimal diff; the line-by-line diff must agree with it
        on any edit whose changed lines are not repeated elsewhere."""
        old = ["line %d\n" % i for i in range(40)]
        new = list(old)
        for i in (5, 6, 20, 33):
            new[i] = "changed %d\n" % i
        p = tool.Plan("".join(old), None, [], None, "".join(new).encode("utf-8"))
        self.assertEqual(tool.unified_diff(p, "f"), list(difflib.unified_diff(old, new, "a/f", "b/f", n=3)))

    def test_lf_and_no_bom_survive(self):
        before = self.put(encode(parts()))
        self.assertEqual(self.run_tool("--apply")[0], 0)
        after = self.read()
        self.assertFalse(after.startswith(b"\xef\xbb\xbf"))
        self.assertNotIn(b"\r", after)
        self.assertEqual(after.count(b"\n"), before.count(b"\n"))

    def test_crlf_and_bom_survive(self):
        self.put(encode(parts(), crlf=True, bom=True))
        self.assertEqual(self.run_tool("--apply")[0], 0)
        self.assertEqual(self.read(), encode(parts(RESTORED_WOMAN), crlf=True, bom=True))

    def test_backup_holds_the_original_and_is_not_xml(self):
        before = self.put(encode(parts()))
        self.assertEqual(self.run_tool("--apply")[0], 0)
        baks = self.backups()
        self.assertEqual(baks, [self.path + ".bak-dwarf-woman-restore-20260926-000000"])
        self.assertFalse(baks[0].lower().endswith(".xml"))
        with open(baks[0], "rb") as fh:
            self.assertEqual(fh.read(), before)

    def test_second_run_is_a_noop(self):
        self.put(encode(parts()))
        self.assertEqual(self.run_tool("--apply")[0], 0)
        once, mtime, baks = self.read(), os.path.getmtime(self.path), self.backups()
        rc, out = self.run_tool("--apply")
        self.assertEqual(rc, 0, out)
        self.assertIn("already restored", out)
        self.assertEqual(self.read(), once)
        self.assertEqual(os.path.getmtime(self.path), mtime)
        self.assertEqual(self.backups(), baks)

    def test_revert_restores_the_original_bytes(self):
        before = self.put(encode(parts()))
        self.assertEqual(self.run_tool("--apply")[0], 0)
        rc, out = self.run_tool("--revert", "--apply")
        self.assertEqual(rc, 0, out)
        self.assertEqual(self.read(), before)
        self.assertEqual(len(glob.glob(self.path + ".bak-dwarf-woman-revert-*")), 1)
        rc, out = self.run_tool("--revert", "--apply")
        self.assertEqual(rc, 0, out)
        self.assertIn("already reverted", out)
        self.assertEqual(self.read(), before)

    def test_missing_or_unexpected_values_refuse_with_no_write(self):
        woman = skin("1", "woman", "adult")
        one_brow = '\t\t\t\t<eyebrow_mesh\n\t\t\t\t\tname="" />\n'
        one_face = ('\t\t\t\t<face_texture name="m_dwarf_basemesh_a1"\n\t\t\t\t\t\t\t   '
                    'lod_material="m_dwarf_basemesh_a1"\n\t\t\t\t\t\t\t  tags="x">\n\t\t\t\t</face_texture>\n')
        cases = {
            "a header value is foreign": woman.replace('hands_mesh="sm_dwarf_basemesh_a1_arms"',
                                                       'hands_mesh="sm_somebody_else_arms"'),
            "a header attribute is missing": woman.replace('\t\t\tlegs_mesh="sm_dwarf_basemesh_a1_legs"\n', ""),
            "a face_texture lod_material is foreign": woman.replace(
                'lod_material="m_dwarf_basemesh_a1"', 'lod_material="m_other"', 1),
            "an eyebrow entry is missing": woman.replace(one_brow, "", 1),
            "an extra face_texture": woman.replace("\t\t\t</face_textures>\n", one_face + "\t\t\t</face_textures>\n"),
            "the stopgap value twice in the block": skin(
                "1", "woman", "adult", extra='\t\t\t<extra face_meta_mesh="sm_dwarf_basemesh_a1_head" />\n'),
            "a partial edit": woman.replace('legs_mesh="sm_dwarf_basemesh_a1_legs"', 'legs_mesh="sk_dwarf_bm_f1_legs"'),
            "two adult woman skins": woman + woman,
            "the block is absent": skin("1", "woman", "teenager"),
        }
        for label, text in cases.items():
            with self.subTest(case=label):
                self.assertNotEqual(text, woman)
                before = self.put(encode(parts(text)))
                rc, out = self.run_tool("--apply")
                self.assertEqual(rc, 1, out)
                self.assertIn("REFUSED", out)
                self.assertEqual(self.read(), before)
                self.assertEqual(self.backups(), [])

    def test_existing_backup_name_refuses(self):
        before = self.put(encode(parts()))
        with open(self.path + ".bak-dwarf-woman-restore-20260926-000000", "wb") as fh:
            fh.write(b"older")
        rc, out = self.run_tool("--apply")
        self.assertEqual(rc, 1, out)
        self.assertIn("backup exists", out)
        self.assertEqual(self.read(), before)

    def test_refuses_while_the_game_or_kit_runs(self):
        before = self.put(encode(parts()))
        with mock.patch.object(tool, "game_or_kit_running", return_value=True):
            rc, out = self.run_tool("--apply")
        self.assertEqual(rc, 2, out)
        self.assertEqual(self.read(), before)
        self.assertEqual(self.backups(), [])

    def test_a_failed_check_after_the_write_puts_the_original_bytes_back(self):
        before = self.put(encode(parts()))
        real_plan = tool.plan
        calls = []

        def plan_then_fail(data, mode):
            calls.append(mode)
            if len(calls) == 2:
                raise tool.RestoreError("simulated re-plan failure")
            return real_plan(data, mode)

        with mock.patch.object(tool, "plan", side_effect=plan_then_fail):
            rc, out = self.run_tool("--apply")
        self.assertEqual(rc, 1, out)
        self.assertIn("original bytes are back", out)
        self.assertEqual(self.read(), before)

    def _planned(self):
        data = encode(parts())
        text = data.decode("utf-8")
        span = tool.locate(text)
        root = tool._parse(data, "test")
        fs = tool.fields(text, span)
        new_text = encode(parts(RESTORED_WOMAN)).decode("utf-8")
        return text, new_text, root, span, fs

    def test_verify_accepts_the_planned_edit(self):
        text, new_text, root, span, fs = self._planned()
        tool._verify(text, new_text, root, span, fs, "stopgap", "female")

    def test_verify_refuses_an_unparseable_result(self):
        text, new_text, root, span, fs = self._planned()
        at = new_text.index("</eyebrow_meshes>", span[0])     # inside the adult woman block
        broken = new_text[:at] + "</eyebrow_meshez>" + new_text[at + len("</eyebrow_meshes>"):]
        with self.assertRaisesRegex(tool.RestoreError, "does not parse"):
            tool._verify(text, broken, root, span, fs, "stopgap", "female")

    def test_verify_refuses_an_edit_outside_the_block(self):
        text, new_text, root, span, fs = self._planned()
        tail = new_text.rindex('hands_mesh="sm_dwarf_basemesh_a1_arms"')   # the uruk woman
        outside = new_text[:tail] + 'hands_mesh="x"' + new_text[tail + len('hands_mesh="sm_dwarf_basemesh_a1_arms"'):]
        with self.assertRaisesRegex(tool.RestoreError, "outside"):
            tool._verify(text, outside, root, span, fs, "stopgap", "female")


if __name__ == "__main__":
    unittest.main()
