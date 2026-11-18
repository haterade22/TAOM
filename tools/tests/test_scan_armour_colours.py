#!/usr/bin/env python3
"""Tests for tools/scan_armour_colours.py and the colour rule of tools/generate_armour_band_clones.py.

Run:  python -m unittest tools.tests.test_scan_armour_colours

THE CONTRACT
------------
Every armour piece has one colour family. A hand override wins, then a colour word in the id, mesh or
a diffuse texture, then the measured texture colour (warm first, then silver that is not cloth, else
neutral). gold and bronze are warm, silver is cold; a warm piece beside a cold one clashes, nothing
else does. The upgrade pick of the band clone tool never puts a piece into a set where it clashes
(the maintainer, 2026-11-18: a silver chest beside bronze pauldrons on the Mirkwood guards).
"""
import contextlib
import importlib
import io
import json
import os
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import scan_armour_colours as sac  # noqa: E402
import generate_armour_band_clones as gabc  # noqa: E402

try:   # the pixel tests need numpy and Pillow; a machine without them skips those tests, not the module
    import numpy  # noqa: F401
    import PIL  # noqa: F401
    HAVE_IMAGING = True
except ImportError:
    HAVE_IMAGING = False


class WordFamilyTests(unittest.TestCase):
    def test_silvergold_wins_over_silver_and_gold(self):
        self.assertEqual(sac.word_family("rivendell_helmet_infantry_tier3_silvergold"), ("silvergold", "silvergold"))

    def test_a_word_must_stand_as_a_token(self):
        self.assertEqual(sac.word_family("mkwd_inf3_chest_silver"), ("silver", "silver"))
        self.assertEqual(sac.word_family("goldenrod_cape"), (None, None))   # "gold" inside a word
        self.assertEqual(sac.word_family("sk_reddish_cape"), (None, None))

    def test_the_first_name_that_names_a_colour_wins(self):
        self.assertEqual(sac.word_family("mkwd_inf3_pauldrons", "mkwd_inf3_pauldron", "x_bronze_d"),
                         ("bronze", "bronze"))

    def test_digits_bound_a_token(self):
        self.assertEqual(sac.word_family("helmet_gold2"), ("gold", "gold"))

    def test_iron_is_a_place_not_a_colour(self):
        # "iron" names the Iron Hills line of the Dwarves, not a silver piece.
        self.assertEqual(sac.word_family("sk_dwarf_iron_chest_elite_a"), (None, None))
        self.assertEqual(sac.word_family("sk_dwarf_iron_boots_elite_a1", "sk_dwarf_iron_boots"), (None, None))


@unittest.skipUnless(HAVE_IMAGING, "numpy and Pillow are not installed")
class PixelClassTests(unittest.TestCase):
    def _px(self, *rgb):
        import numpy as np
        return np.array(rgb, dtype=float) / 255.0

    def test_each_class(self):
        shares = sac.pixel_classes(self._px((200, 200, 205), (210, 170, 70), (150, 90, 40), (20, 20, 20)))
        self.assertEqual(shares, {"black": 0.25, "bronze": 0.25, "gold": 0.25, "silver": 0.25})

    def test_transparent_pixels_do_not_count(self):
        import numpy as np
        shares = sac.pixel_classes(self._px((200, 200, 205), (210, 170, 70)), np.array([1.0, 0.0]))
        self.assertEqual(shares, {"silver": 1.0})


class ClassifyTests(unittest.TestCase):
    def test_a_warm_texture_beats_a_silver_one(self):
        fam, tex = sac.classify_textures([("plate_d", {"gold": 0.9}), ("cloth_d", {"silver": 1.0})])
        self.assertEqual((fam, tex), ("gold", "plate_d"))

    def test_bronze_when_the_warm_textures_hold_more_bronze(self):
        fam, _ = sac.classify_textures([("a_d", {"gold": 0.2, "bronze": 0.5}), ("b_d", {"bronze": 0.4})])
        self.assertEqual(fam, "bronze")

    def test_grey_cloth_is_not_silver(self):
        self.assertEqual(sac.classify_textures([("m_elves_cloths_a_d", {"silver": 1.0})])[0], "neutral")
        self.assertEqual(sac.classify_textures([("t_tunic_a_d", {"silver": 1.0})])[0], "neutral")

    def test_silver_plate_is_silver(self):
        self.assertEqual(sac.classify_textures([("torso_silver_d", {"silver": 0.7})])[0], "silver")

    def test_weak_shares_are_neutral_and_nothing_is_unknown(self):
        self.assertEqual(sac.classify_textures([("a_d", {"gold": 0.2, "silver": 0.3})])[0], "neutral")
        self.assertEqual(sac.classify_textures([]), ("unknown", ""))


class ClashTests(unittest.TestCase):
    def test_warm_beside_silver_clashes_both_ways(self):
        for warm in ("gold", "bronze"):
            self.assertTrue(sac.clashes(warm, "silver"))
            self.assertTrue(sac.clashes("silver", warm))

    def test_gold_beside_bronze_and_the_neutral_families_do_not(self):
        self.assertFalse(sac.clashes("gold", "bronze"))
        for other in ("silvergold", "neutral", "black", "unknown", "red"):
            self.assertFalse(sac.clashes("silver", other))
            self.assertFalse(sac.clashes("gold", other))

    def test_set_clashes_names_each_pair_once(self):
        pairs = sac.set_clashes({"Body": "silver", "Cape": "bronze", "Head": "gold", "Leg": "neutral"})
        self.assertEqual(pairs, [("Body", "Cape"), ("Body", "Head")])


class OverrideTests(unittest.TestCase):
    def test_an_id_row_wins_then_the_longest_prefix(self):
        ids = {"sk_a_b": {"family": "gold"}}
        prefixes = {"sk_a_": {"family": "silver"}, "sk_": {"family": "neutral"}}
        self.assertEqual(sac.override_for("sk_a_b", ids, prefixes)["family"], "gold")
        self.assertEqual(sac.override_for("sk_a_c", ids, prefixes)["family"], "silver")
        self.assertEqual(sac.override_for("sk_z", ids, prefixes)["family"], "neutral")
        self.assertIsNone(sac.override_for("other", ids, prefixes))

    def test_an_unknown_family_is_refused(self):
        with tempfile.TemporaryDirectory() as d:
            p = Path(d) / "o.json"
            p.write_text(json.dumps({"overrides": {"x": {"family": "purple"}}}), encoding="utf-8")
            with self.assertRaises(sac.ColourError):
                sac.load_overrides(p)

    def test_the_shipped_override_file_loads(self):
        ids, prefixes = sac.load_overrides()
        self.assertTrue(all(v.get("why") for v in {**ids, **prefixes}.values()), "every row says why")


class OverrideReachTests(unittest.TestCase):
    """An override names a source piece; a clone or lord variant of it is the same art (review 2026-11-18)."""

    def test_a_band_clone_takes_the_override_of_its_source(self):
        ids = {"sk_ll_x_chest": {"family": "neutral", "why": "w"}}
        self.assertEqual(sac.override_chain("taom_band_lt_sk_ll_x_chest", ids, {})["family"], "neutral")
        self.assertEqual(sac.override_chain("taom_band_hv_sk_ll_x_chest", {}, {"sk_ll_x_": {"family": "gold"}})["family"],
                         "gold")

    def test_a_clone_row_of_its_own_wins_over_the_source_row(self):
        ids = {"sk_a": {"family": "gold"}, "taom_band_lt_sk_a": {"family": "silver"}}
        self.assertEqual(sac.override_chain("taom_band_lt_sk_a", ids, {})["family"], "silver")

    def test_a_lord_variant_takes_the_override_of_its_source(self):
        import derive_armor_tiers as dat
        variants = sorted(dat.lord_variant_cultures())
        if not variants:
            self.skipTest("the shipped lord variant spec lists none")
        source = dat.lord_variant_source(variants[0])
        ids = {source: {"family": "black", "why": "w"}}
        self.assertEqual(sac.override_chain(variants[0], ids, {})["family"], "black")

    def test_a_piece_with_no_row_anywhere_has_no_override(self):
        self.assertIsNone(sac.override_chain("taom_band_lt_other", {"sk_a": {"family": "gold"}}, {}))

    def test_an_override_naming_no_item_is_reported_both_ways(self):
        items = {"sk_a_chest", "sk_b_chest"}
        self.assertEqual(sac.unused_overrides({"sk_a_chest": {}}, {"sk_b_": {}}, items), [])
        self.assertEqual(sac.unused_overrides({"sk_zz": {}, "sk_a_chest": {}}, {"sk_c_": {}, "sk_b_": {}}, items),
                         ["sk_zz", "sk_c_"])


class OverrideFileTests(unittest.TestCase):
    def test_a_byte_order_mark_is_read(self):
        with tempfile.TemporaryDirectory() as d:
            p = Path(d) / "o.json"
            p.write_bytes(b"\xef\xbb\xbf" + json.dumps({"overrides": {"x": {"family": "gold", "why": "w"}}}).encode())
            ids, prefixes = sac.load_overrides(p)
            self.assertEqual(ids["x"]["family"], "gold")
            self.assertEqual(prefixes, {})

    def test_the_ranger_chest_is_a_row_of_its_own_with_a_reason(self):
        _ids, prefixes = sac.load_overrides()
        self.assertEqual(prefixes["sk_ll_rg_chest_"]["family"], "neutral")
        self.assertIn("t_ll_rg_chest_a_d", prefixes["sk_ll_rg_chest_"]["why"])


class FamilyOfTests(unittest.TestCase):
    TABLE = {"sk_a": "gold", "taom_band_lt_sk_b": "silver", "sk_b": "bronze"}

    def test_a_piece_reads_its_own_row_then_its_clone_source_then_unknown(self):
        self.assertEqual(sac.family_of("sk_a", self.TABLE), "gold")
        self.assertEqual(sac.family_of("taom_band_md_sk_a", self.TABLE), "gold")
        self.assertEqual(sac.family_of("taom_band_lt_sk_b", self.TABLE), "silver")   # its own row first
        self.assertEqual(sac.family_of("vanilla_helm", self.TABLE), "unknown")
        self.assertEqual(sac.family_of("taom_band_lt_vanilla_helm", self.TABLE), "unknown")


class MeshTexturesTests(unittest.TestCase):
    """mesh_diffuse reads the slots first and the guid-scan texture names only when no material decodes."""

    class FakeIndex:
        def __init__(self, names):
            self.names = names

        def get(self, kind, name):
            return object() if name in self.names else None

    def test_the_male_mesh_is_read_before_the_plain_one(self):
        import audit_battle_equipment_memory as abm
        slots = {0: {"a_d"}}
        with mock.patch.object(abm, "material_texture_slots", return_value=slots) as m, \
                mock.patch.object(abm, "mesh_assets", side_effect=AssertionError("mesh_assets ran")):
            got = sac.mesh_diffuse(abm, self.FakeIndex({"x_male", "x"}), "x")
        self.assertEqual(got, ["a_d"])
        self.assertEqual(m.call_count, 1)

    def test_mesh_assets_runs_only_when_no_slot_data_exists(self):
        import audit_battle_equipment_memory as abm
        assets = {("texture", "a_d"): {"name": "a_d"}, ("texture", "a_n"): {"name": "a_n"},
                  ("material", "m"): {"name": "m"}}
        with mock.patch.object(abm, "material_texture_slots", return_value={}), \
                mock.patch.object(abm, "mesh_assets", return_value=assets) as m:
            got = sac.mesh_diffuse(abm, self.FakeIndex({"x"}), "x")
        self.assertEqual(got, ["a_d"])   # the name rule
        self.assertEqual(m.call_count, 1)

    def test_a_mesh_with_no_metamesh_has_no_texture(self):
        import audit_battle_equipment_memory as abm
        self.assertEqual(sac.mesh_diffuse(abm, self.FakeIndex(set()), "x"), [])
        self.assertEqual(sac.mesh_diffuse(abm, self.FakeIndex({"x"}), ""), [])


class DiffuseSlotTests(unittest.TestCase):
    def test_slot_zero_is_the_diffuse_whatever_its_name(self):
        slots = {0: {"t_ll_rg_chest_a_d", "weird_tex"}, 1: {"t_ll_rg_chest_a_n"}, 2: {"t_x_d"}}
        self.assertEqual(sac.pick_diffuse(slots, ["t_x_d", "t_ll_rg_chest_a_n"]), ["t_ll_rg_chest_a_d", "weird_tex"])

    def test_a_texture_named_color_is_not_a_diffuse_when_the_slot_says_so(self):
        self.assertEqual(sac.pick_diffuse({0: {"a_d"}, 1: {"a_color"}}, ["a_d", "a_color"]), ["a_d"])

    def test_without_slot_data_the_name_rule_decides(self):
        self.assertEqual(sac.pick_diffuse({}, ["a_d", "a_n", "b_albedo"]), ["a_d", "b_albedo"])


class MainExitTests(unittest.TestCase):
    """Exit codes: 0 clean or written, 1 stale (--check) or a bad override, 2 nothing could be measured, 3 a crash."""

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.root = Path(self._tmp.name)
        self.modules = self.root / "Modules"
        self.table = self.root / "table.json"
        self.table.write_text("OLD", encoding="utf-8")
        patch = mock.patch.object(sac, "TABLE", self.table)
        patch.start()
        self.addCleanup(patch.stop)

    def run_main(self, argv, **env):
        out, err = io.StringIO(), io.StringIO()
        with mock.patch.dict(os.environ, env), contextlib.redirect_stdout(out), contextlib.redirect_stderr(err):
            code = sac.entry(argv)
        return code, out.getvalue() + err.getvalue()

    def armory(self, image=None):
        (self.modules / "LOTRLOME_Armory").mkdir(parents=True)
        if image:
            folder = self.modules / "LOTRLOME_Armory" / "AssetSources"
            folder.mkdir()
            (folder / image).write_bytes(b"x")

    def must_not_build(self, *_a, **_k):
        raise AssertionError("build ran")

    def test_no_image_under_the_install_exits_two_and_writes_nothing(self):
        self.armory()
        with mock.patch.object(sac, "missing_imaging", return_value=[]), mock.patch.object(sac, "build", self.must_not_build):
            code, text = self.run_main(["--apply", "--game-modules", str(self.modules)])
        self.assertEqual(code, 2)
        self.assertIn("no texture image", text)
        self.assertIn("nothing was checked or written", text)
        self.assertEqual(self.table.read_text(encoding="utf-8"), "OLD")

    def test_check_with_no_image_is_two_not_clean(self):
        self.armory()
        with mock.patch.object(sac, "missing_imaging", return_value=[]), mock.patch.object(sac, "build", self.must_not_build):
            self.assertEqual(self.run_main(["--check", "--game-modules", str(self.modules)])[0], 2)

    def test_an_explicit_modules_path_without_the_armory_is_an_error(self):
        self.modules.mkdir()
        code, text = self.run_main(["--game-modules", str(self.modules)])
        self.assertEqual(code, 2)
        self.assertIn("ERROR", text)

    def test_the_default_install_comes_from_the_modules_variable_and_a_missing_armory_is_skipped_two(self):
        self.modules.mkdir()
        code, text = self.run_main([], BANNERLORD_GAME_MODULES=str(self.modules))
        self.assertEqual(code, 2)
        self.assertIn("SKIPPED", text)
        self.assertIn(str(self.modules), text)

    def test_missing_numpy_or_pillow_exits_two_with_the_names(self):
        self.armory(image="a.png")
        with mock.patch.object(sac, "missing_imaging", return_value=["numpy"]), \
                mock.patch.object(sac, "build", self.must_not_build):
            code, text = self.run_main(["--game-modules", str(self.modules)])
        self.assertEqual(code, 2)
        self.assertIn("numpy", text)

    def test_missing_imaging_is_found_by_import(self):
        with mock.patch.object(importlib, "import_module", side_effect=ImportError("no")):
            self.assertEqual(sac.missing_imaging(), ["numpy", "PIL"])

    def test_a_bad_override_is_one(self):
        self.armory(image="a.png")
        with mock.patch.object(sac, "missing_imaging", return_value=[]), \
                mock.patch.object(sac, "build", side_effect=sac.ColourError("bad row")):
            code, text = self.run_main(["--game-modules", str(self.modules)])
        self.assertEqual(code, 1)
        self.assertIn("bad row", text)

    def test_a_crash_is_three_with_a_traceback(self):
        self.armory(image="a.png")
        with mock.patch.object(sac, "missing_imaging", return_value=[]), \
                mock.patch.object(sac, "build", side_effect=RuntimeError("boom")):
            code, text = self.run_main(["--game-modules", str(self.modules)])
        self.assertEqual(code, 3)
        self.assertIn("Traceback", text)
        self.assertIn("crashed; no verdict, exit 3", text)

    def test_the_summary_line_counts_the_images_and_a_dry_run_writes_nothing(self):
        self.armory(image="a.png")
        items = {"x": {"family": "gold", "how": "word gold", "texture": ""}}
        with mock.patch.object(sac, "missing_imaging", return_value=[]), mock.patch.object(sac, "build", return_value=items):
            code, text = self.run_main(["--game-modules", str(self.modules)])
        self.assertEqual(code, 0)
        self.assertIn("images 1", text)
        self.assertEqual(self.table.read_text(encoding="utf-8"), "OLD")

    def test_check_is_stale_until_apply_has_written(self):
        self.armory(image="a.png")
        items = {"x": {"family": "gold", "how": "word gold", "texture": ""}}
        self.table.write_text(sac.render({}), encoding="utf-8")
        with mock.patch.object(sac, "missing_imaging", return_value=[]), mock.patch.object(sac, "build", return_value=items):
            self.assertEqual(self.run_main(["--check", "--game-modules", str(self.modules)])[0], 1)
            self.assertEqual(self.run_main(["--apply", "--game-modules", str(self.modules)])[0], 0)
            self.assertEqual(self.run_main(["--check", "--game-modules", str(self.modules)])[0], 0)


class ShippedTableTests(unittest.TestCase):
    def test_the_table_names_the_mirkwood_pieces_of_the_report(self):
        table = sac.load_table()
        self.assertEqual(table.get("mkwd_inf3_chest_silver"), "silver")
        self.assertEqual(table.get("mkwd_inf3_pauldrons_bronze"), "bronze")
        self.assertTrue(set(table.values()) <= set(sac.FAMILIES))


class UpgradeColourRuleTests(unittest.TestCase):
    COLOURS = {"chest_bronze": "bronze", "chest_silver": "silver", "pauld_bronze": "bronze",
               "helm_gold": "gold", "boots": "neutral"}

    def test_a_silver_chest_clashes_with_bronze_pauldrons(self):
        sets = [{"Body": "chest_bronze", "Cape": "pauld_bronze", "Leg": "boots"}]
        self.assertEqual(gabc.colour_clash("chest_silver", "chest_bronze", sets, self.COLOURS),
                         "Cape pauld_bronze bronze")

    def test_a_same_family_pick_passes(self):
        sets = [{"Body": "chest_bronze", "Head": "helm_gold", "Leg": "boots"}]
        self.assertIsNone(gabc.colour_clash("chest_bronze", "chest_bronze", sets, self.COLOURS))

    def test_a_set_without_the_source_is_not_judged(self):
        sets = [{"Body": "other_chest", "Cape": "pauld_bronze"}]
        self.assertIsNone(gabc.colour_clash("chest_silver", "chest_bronze", sets, self.COLOURS))

    def test_an_unknown_piece_never_clashes(self):
        sets = [{"Body": "chest_bronze", "Cape": "pauld_bronze"}]
        self.assertIsNone(gabc.colour_clash("not_in_table", "chest_bronze", sets, self.COLOURS))


if __name__ == "__main__":
    unittest.main()
