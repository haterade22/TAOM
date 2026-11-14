#!/usr/bin/env python3
"""Data rules for Harad war paint (#1100). The tests read repo files only.

Run: python -m unittest tools/tests/test_harad_war_paint.py

The tattoo numbers in a saved face key are positions in the merged tattoo lists. The rules below keep those
positions stable and keep the paint on Harad only. docs/features/harad-war-paint.md gives the reasons.
"""
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path

MODULE_DATA = Path(__file__).resolve().parents[2] / "Main" / "_Module" / "ModuleData"
ASSETS = Path(__file__).resolve().parents[2] / "Main" / "_Module" / "Assets" / "tattoos"

MALE_DESIGNS = [
    ("tattoo_harad_a", "HaradA"),
    ("tattoo_harad_b", "HaradA2"),
    ("tattoo_harad_c", "HaradB"),
    ("tattoo_harad_d", "HaradHunter"),
    ("tattoo_harad_e", "HaradRitual"),
    ("tattoo_harad_f", "HaradElite"),
    ("tattoo_harad_g", "HaradAsym"),
    ("tattoo_harad_h", "HaradScarred"),
]
FEMALE_DESIGNS = [
    ("tattoo_harad_w1", "HaradCrown"),
    ("tattoo_harad_w2", "HaradFilm"),
    ("tattoo_harad_w3", "HaradPriestess"),
    ("tattoo_harad_w4", "HaradElegant"),
    ("tattoo_harad_w5", "HaradWarrior"),
    ("tattoo_harad_w6", "HaradDots"),
    ("tattoo_harad_w7", "HaradRitualElite"),
]
# Native's zero_probability per child skin. The value in the last block parsed can replace Native's.
CHILD_ZERO_PROBABILITY = {
    "kid_2_male": "0",
    "kid_2_female": "0",
    "kid_1_male": "85",
    "kid_1_female": "85",
    "kid_3_male": "85",
    "kid_3_female": "85",
    "toddler_male": "85",
    "toddler_female": "85",
}
HARAD_PRESET = "BodyProperty.fighter_haradrim"
HARAD_CULTURES = {"Culture.aserai", "Culture.shaghana", "Culture.abanissa", "Culture.harad_raiders"}
XSL = "{http://www.w3.org/1999/XSL/Transform}"
# Merged list positions of the designs: after Native's 31 male and 34 female entries.
MALE_DESIGN_INDEXES = range(31, 39)
FEMALE_DESIGN_INDEXES = range(34, 41)
# A key stores the colour as 0 to 63, a position along the merged palette. Indexes 12 to 23 are the Harad colours.
HARAD_COLOUR_VALUES = range(33, 64)
# Lords with a fixed key, each counted once. All 53 are in lords.xslt: #648 folded the 28 that also had a
# characters/lords.xml row into their templates.
FIXED_KEY_HARAD_LORDS = 53
TATTOO_KEY_BITS = (0x3F << 24) | (0x3F << 18)
# The native skin stores gradient points in a fixed array: 0x3C4 to 0x5C4, 16 bytes each (FUN_180578810, v1.5.6).
NATIVE_PALETTE_CAPACITY = 32
NATIVE_PALETTE_SIZE = 12
HARAD_PALETTE = [
    "0.60, 0.02, 0.05",
    "0.18, 0.00, 0.01",
    "0.42, 0.14, 0.03",
    "0.55, 0.22, 0.10",
    "0.80, 0.45, 0.02",
    "0.75, 0.55, 0.12",
    "0.72, 0.66, 0.52",
    "0.06, 0.05, 0.30",
    "0.00, 0.38, 0.36",
    "0.05, 0.55, 0.55",
    "0.40, 0.05, 0.35",
    "0.03, 0.03, 0.04",
]


def parse(path):
    return ET.fromstring(path.read_bytes())


def tattoo_entries(skin):
    block = skin.find("tattoo_materials")
    return block, block.findall("tattoo_material")


def style_tags(entry):
    return [tag.get("name") for tag in entry.iter("style_tag")]


def presets():
    return {p.get("id"): p for p in parse(MODULE_DATA / "TAOM_bodyproperties.xml").findall("BodyProperty")}


def tattoo_tags(element):
    return [tag.get("name") for tag in element.iter("tattoo_tag")]


def tattoo_bits(key):
    """(design index, colour value) from a face key: KeyPart1 bits 24 to 29 and 18 to 23 (FUN_18057b4e0, v1.5.6)."""
    key_part_1 = int(key[:16], 16)
    return (key_part_1 >> 24) & 63, (key_part_1 >> 18) & 63


def item_name(package):
    """The item name in a tpac: a 4-byte little-endian length at offset 72, then the name."""
    return package[76 : 76 + int.from_bytes(package[72:76], "little")]


def has_fixed_key(face):
    return face is not None and face.find("BodyProperties") is not None and face.find("face_key_template") is None


def fixed_key_characters():
    """(id, culture, is_female, face) of each character with its own key. lords.xml comes first: it wins the merge."""
    for character in parse(MODULE_DATA / "characters" / "lords.xml").iter("NPCCharacter"):
        if has_fixed_key(character.find("face")):
            yield (
                character.get("id"),
                character.get("culture"),
                character.get("is_female") == "true",
                character.find("face"),
            )
    for template in parse(MODULE_DATA / "lords.xslt").iter(f"{XSL}template"):
        attributes = {row.get("name"): row.text for row in template.findall(f"{XSL}copy/{XSL}attribute")}
        if "id" in attributes and has_fixed_key(template.find(f"{XSL}copy/face")):
            yield (
                attributes["id"],
                attributes.get("culture"),
                attributes.get("is_female") == "true",
                template.find(f"{XSL}copy/face"),
            )


def fixed_key_harad_lords():
    """(id, is_female, face) of each fixed-key Harad lord, once: the first definition fixed_key_characters yields."""
    lords = {}
    for lord, culture, female, face in fixed_key_characters():
        if culture in HARAD_CULTURES:
            lords.setdefault(lord, (lord, female, face))
    return list(lords.values())


def harad_culture_characters():
    for path in sorted(MODULE_DATA.rglob("*.xml")):
        if b"NPCCharacter" not in path.read_bytes():
            continue
        for character in parse(path).iter("NPCCharacter"):
            if character.get("culture") in HARAD_CULTURES:
                yield path.name, character


def characters_using_harad_preset():
    for path in sorted(MODULE_DATA.rglob("*.xml")):
        if HARAD_PRESET.encode() not in path.read_bytes():
            continue
        for character in parse(path).iter("NPCCharacter"):
            template = character.find("face/face_key_template")
            if template is not None and template.get("value") == HARAD_PRESET:
                yield path.name, character


class SkinsTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.races = parse(MODULE_DATA / "skins.xml").findall("race")
        cls.skin_names = [skin.get("name") for skin in cls.races[0].findall("skin")]
        cls.skins = {skin.get("name"): skin for skin in cls.races[0].findall("skin")}

    def test_only_the_human_race_is_extended(self):
        self.assertEqual(["human"], [race.get("id") for race in self.races])

    def test_every_skin_has_one_tattoo_block_and_one_palette(self):
        for skin_name, skin in self.skins.items():
            with self.subTest(skin=skin_name):
                self.assertEqual(1, len(skin.findall("tattoo_materials")))
                self.assertEqual(1, len(skin.findall("tattoo_color_gradient_points")))

    def assert_adult_list(self, skin_name, designs):
        block, entries = tattoo_entries(self.skins[skin_name])
        self.assertEqual({"group_id": "8", "zero_probability": "0"}, block.attrib)
        self.assertEqual([name for name, _ in designs], [entry.get("name") for entry in entries])
        for entry, (_, own_tag) in zip(entries, designs):
            self.assertEqual("capon", entry.get("tags"))
            self.assertEqual({own_tag, "aserai", "harad"}, set(style_tags(entry)))

    def test_men_get_their_designs_in_fixed_order(self):
        self.assert_adult_list("man", MALE_DESIGNS)

    def test_women_get_their_designs_in_fixed_order(self):
        self.assert_adult_list("woman", FEMALE_DESIGNS)

    def test_every_child_skin_is_padded_to_keep_numbers_aligned(self):
        for skin_name, zero_probability in CHILD_ZERO_PROBABILITY.items():
            with self.subTest(skin=skin_name):
                designs = FEMALE_DESIGNS if skin_name.endswith("female") else MALE_DESIGNS
                block, entries = tattoo_entries(self.skins[skin_name])
                self.assertEqual({"group_id": "8", "zero_probability": zero_probability}, block.attrib)
                self.assertEqual(len(designs), len(entries))
                for entry, (_, own_tag) in zip(entries, designs):
                    self.assertEqual({}, entry.attrib)
                    self.assertEqual({own_tag, "aserai", "harad"}, set(style_tags(entry)))

    def test_every_skin_appends_the_same_harad_palette(self):
        for skin_name, skin in self.skins.items():
            with self.subTest(skin=skin_name):
                points = [p.get("point") for p in skin.find("tattoo_color_gradient_points")]
                self.assertEqual(HARAD_PALETTE, points)

    def test_palette_fits_the_native_array_when_appended(self):
        self.assertLessEqual(NATIVE_PALETTE_SIZE + len(HARAD_PALETTE), NATIVE_PALETTE_CAPACITY)

    def test_no_other_skin_is_touched(self):
        self.assertEqual(sorted(["man", "woman", *CHILD_ZERO_PROBABILITY]), sorted(self.skin_names))


class PackageTests(unittest.TestCase):
    """The Kit reads a name that ends in _h as a height map, which drops the colour and coverage channels."""

    def test_every_design_has_a_texture_and_a_material_package(self):
        expected = set()
        for material, _ in MALE_DESIGNS + FEMALE_DESIGNS:
            expected |= {f"{material}_tex.tpac", f"{material}_mtl.tpac"}
        self.assertEqual(expected, {path.name for path in ASSETS.glob("*.tpac")})

    def test_skins_names_the_material_each_package_declares(self):
        # A name with no material gives an entry that draws nothing and logs nothing.
        race = parse(MODULE_DATA / "skins.xml").find("race")
        for skin_name in ("man", "woman"):
            for entry in tattoo_entries(next(s for s in race.findall("skin") if s.get("name") == skin_name))[1]:
                with self.subTest(material=entry.get("name")):
                    package = (ASSETS / f"{entry.get('name')}_mtl.tpac").read_bytes()
                    self.assertEqual(entry.get("name").encode(), item_name(package))

    def test_each_material_binds_its_own_texture(self):
        # A material copied from another design keeps that design's texture unless the texture is swapped.
        for material, _ in MALE_DESIGNS + FEMALE_DESIGNS:
            with self.subTest(material=material):
                texture = (ASSETS / f"{material}_tex.tpac").read_bytes()
                self.assertEqual(material.encode(), item_name(texture))
                self.assertIn(f"AssetSources/tattoos/{material}.png".encode(), texture)
                self.assertIn(texture[52:68], (ASSETS / f"{material}_mtl.tpac").read_bytes())

    def test_every_texture_is_imported_as_albedo_with_alpha(self):
        textures = sorted(ASSETS.glob("*_tex.tpac"))
        self.assertEqual(len(MALE_DESIGNS + FEMALE_DESIGNS), len(textures))
        for path in textures:
            with self.subTest(package=path.name):
                data = path.read_bytes()
                self.assertIn(b"albedo", data)
                self.assertIn(b"has_alpha", data)
                self.assertIn(b"DXT5", data)
                self.assertNotIn(b"heightmap", data)


class ProjectTests(unittest.TestCase):
    def test_skins_file_is_registered_with_the_native_id(self):
        rows = [
            row.attrib
            for row in parse(MODULE_DATA / "project.mbproj").findall("file")
            if row.get("type") == "skin"
        ]
        self.assertEqual([{"id": "soln_skins", "name": "ModuleData/skins.xml", "type": "skin"}], rows)


class PresetTests(unittest.TestCase):
    def test_harad_preset_selects_the_war_paint(self):
        self.assertEqual(["harad"], tattoo_tags(presets()["fighter_haradrim"]))

    def test_umbar_preset_is_clean(self):
        self.assertEqual(["Cleanface"], tattoo_tags(presets()["fighter_umbar"]))

    def test_harad_preset_rolls_across_every_harad_colour(self):
        # The roll is continuous between the two keys, so these two ends set the reachable colours.
        preset = presets()["fighter_haradrim"]
        colours = [tattoo_bits(preset.find(row).get("key"))[1] for row in ("BodyPropertiesMin", "BodyPropertiesMax")]
        self.assertEqual([34, 63], sorted(colours))

    def test_umbar_preset_keeps_the_harad_face_range_and_hair(self):
        all_presets = presets()
        harad, umbar = all_presets["fighter_haradrim"], all_presets["fighter_umbar"]
        for child in ("BodyPropertiesMin", "BodyPropertiesMax"):
            self.assertEqual(harad.find(child).attrib, umbar.find(child).attrib)
        for group, row in (("hair_tags", "hair_tag"), ("beard_tags", "beard_tag")):
            self.assertEqual(
                [tag.get("name") for tag in harad.find(group).findall(row)],
                [tag.get("name") for tag in umbar.find(group).findall(row)],
            )


class CharacterTests(unittest.TestCase):
    def test_only_harad_cultures_use_the_painted_preset(self):
        self.assertEqual(HARAD_CULTURES, {character.get("culture") for _, character in characters_using_harad_preset()})

    def test_igor_stays_in_aragorns_face_group_and_rolls_from_the_tag(self):
        # LordFaceVariety rolls a group member from its tags. A painted key would take him out of the group.
        companions = parse(MODULE_DATA / "named_companions" / "named_companions.xml")
        faces = {character.get("id"): character.find("face") for character in companions.iter("NPCCharacter")}
        igor, aragorn = faces["named_companion_igor"], faces["named_companion_aragorn"]
        self.assertEqual(aragorn.find("BodyProperties").get("key"), igor.find("BodyProperties").get("key"))
        self.assertEqual(["harad"], tattoo_tags(igor))

    def test_preset_lords_leave_the_tattoo_tag_to_the_preset(self):
        offenders = [
            character.get("id")
            for _, character in characters_using_harad_preset()
            if character.find("face/tattoo_tags") is not None
        ]
        self.assertEqual([], offenders)

    def test_no_harad_character_overrides_the_paint_with_a_clean_face(self):
        offenders = [
            (file_name, character.get("id"))
            for file_name, character in harad_culture_characters()
            if character.find("face") is not None and "Cleanface" in tattoo_tags(character.find("face"))
        ]
        self.assertEqual([], offenders)


class FixedKeyLordTests(unittest.TestCase):
    """A lord with its own key never rolls, so the design and colour must be in the key."""

    @classmethod
    def setUpClass(cls):
        cls.lords = list(fixed_key_harad_lords())

    def test_the_scan_finds_every_fixed_key_lord(self):
        self.assertGreaterEqual(len(self.lords), FIXED_KEY_HARAD_LORDS)

    def test_each_key_holds_a_design_for_the_lords_gender(self):
        for lord_id, is_female, face in self.lords:
            with self.subTest(lord=lord_id):
                design, _ = tattoo_bits(face.find("BodyProperties").get("key"))
                self.assertIn(design, FEMALE_DESIGN_INDEXES if is_female else MALE_DESIGN_INDEXES)

    def test_each_key_holds_a_harad_colour(self):
        for lord_id, _, face in self.lords:
            with self.subTest(lord=lord_id):
                self.assertIn(tattoo_bits(face.find("BodyProperties").get("key"))[1], HARAD_COLOUR_VALUES)

    def test_each_lord_is_tagged_harad(self):
        # Children and the face variety pass roll from the tags, not from the key. A set: 28 templates hold the tag
        # twice, the engine's merge of their template and old characters/lords.xml row, copied in by #648.
        for lord_id, _, face in self.lords:
            with self.subTest(lord=lord_id):
                self.assertEqual({"harad"}, set(tattoo_tags(face)))

    def test_lords_with_one_face_also_share_one_key(self):
        # LordFaceVariety groups on the whole key, so two lords with one face must hold the same paint to be separated.
        faces = {}
        for _, is_female, face in self.lords:
            key = face.find("BodyProperties").get("key")
            faces.setdefault((is_female, int(key[:16], 16) & ~TATTOO_KEY_BITS, key[16:]), set()).add(key)
        self.assertEqual([], [keys for keys in faces.values() if len(keys) > 1])

    def test_no_other_culture_holds_a_harad_design(self):
        offenders = []
        for lord_id, culture, is_female, face in fixed_key_characters():
            design, _ = tattoo_bits(face.find("BodyProperties").get("key"))
            if culture not in HARAD_CULTURES and design in (FEMALE_DESIGN_INDEXES if is_female else MALE_DESIGN_INDEXES):
                offenders.append(lord_id)
        self.assertEqual([], offenders)


if __name__ == "__main__":
    unittest.main()
