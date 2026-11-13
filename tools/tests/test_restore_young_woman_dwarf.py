"""Tests for restore_young_woman_dwarf.py on a synthetic skins.xml.

The synthetic file holds the dwarf race (male adult, adult woman, four young female skins, one young male
skin) and an uruk race whose young female skin carries the same base values, so a global replace would hit it.

    python -m pytest tools/tests/test_restore_young_woman_dwarf.py
"""
import os
import sys
import unittest
import xml.etree.ElementTree as ET

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "oneoff"))
import restore_young_woman_dwarf as tool  # noqa: E402

BASE = dict((a, b) for a, b, _f in tool.HEADER)


def skin(name, gender, maturity, suffix="_fem"):
    attrs = " ".join('%s="%s"' % (a, v) for a, v in BASE.items())
    return (
        '\t\t<skin gender="%s" name="%s" mesh_maturity_type="%s" body_mesh_suffix="%s" min_scale="0.8" %s>\n'
        '\t\t\t<eyebrow_meshes>\n\t\t\t\t<eyebrow_mesh\n\t\t\t\t\tname="" />\n\t\t\t</eyebrow_meshes>\n'
        '\t\t\t<face_textures group_id="1">\n%s\t\t\t</face_textures>\n\t\t</skin>\n'
        % (gender, name, maturity, suffix, attrs,
           "".join('\t\t\t\t<face_texture name="m_dwarf_basemesh_a1" lod_material="m_dwarf_basemesh_a1" '
                   'tags="t%d" />\n' % i for i in range(4)))
    )


def build(eol="\n"):
    dwarf = (skin("man", 0, "adult", "") + skin("woman", 1, "adult", "")
             + skin("kid_2_female", 1, "teenager") + skin("kid_1_female", 1, "tween")
             + skin("kid_3_female", 1, "child") + skin("toddler_female", 1, "toddler", "")
             + skin("kid_2_male", 0, "teenager", ""))
    text = ('<?xml version="1.0" encoding="utf-8"?>\n<skins>\n\t<race id="dwarf">\n' + dwarf
            + '\t</race>\n\t<race id="uruk">\n' + skin("kid_2_female", 1, "teenager") + '\t</race>\n</skins>\n')
    return text.replace("\n", eol).encode("utf-8")


class YoungWomanDwarfTests(unittest.TestCase):
    def test_only_young_female_dwarf_skins_change(self):
        new, names = tool.plan(build(), True)
        self.assertEqual(names, ["kid_2_female", "kid_1_female", "kid_3_female", "toddler_female"])
        root = ET.fromstring(new)
        dwarf = next(r for r in root.iter("race") if r.get("id") == "dwarf")
        for s in dwarf.findall("skin"):
            young_f = s.get("gender") == "1" and s.get("mesh_maturity_type") != "adult"
            self.assertEqual(s.get("face_meta_mesh") == "sk_dwarf_bm_f1_head", young_f, s.get("name"))
            if young_f:
                self.assertEqual(s.get("body_mesh_suffix"), "")
                self.assertEqual(s.get("underwear_bottom_mesh"), "sk_dwarf_underwear_female_a")
                self.assertEqual([e.get("name") for e in s.iter("eyebrow_mesh")], tool.BROWS)
                self.assertEqual({f.get("name") for f in s.iter("face_texture")}, {tool.FACE_FEMALE})
        uruk = next(r for r in root.iter("race") if r.get("id") == "uruk")
        self.assertEqual(uruk.find("skin").get("face_meta_mesh"), BASE["face_meta_mesh"])

    def test_second_run_changes_nothing(self):
        new, _ = tool.plan(build(), True)
        again, names = tool.plan(new, True)
        self.assertIsNone(again)
        self.assertEqual(names, [])

    def test_crlf_survives(self):
        new, _ = tool.plan(build("\r\n"), True)
        self.assertEqual(new.count(b"\n"), new.count(b"\r\n"))

    def test_foreign_mesh_value_is_refused(self):
        data = build().replace(b'legs_mesh="sm_dwarf_basemesh_a1_legs"', b'legs_mesh="other"')
        with self.assertRaises(tool.Refused):
            tool.plan(data, True)

    def test_revert_restores_meshes(self):
        new, _ = tool.plan(build(), True)
        back, _ = tool.plan(new, False)
        root = ET.fromstring(back)
        kid = next(s for s in root.iter("skin") if s.get("name") == "kid_1_female")
        self.assertEqual(kid.get("face_meta_mesh"), BASE["face_meta_mesh"])


if __name__ == "__main__":
    unittest.main()
