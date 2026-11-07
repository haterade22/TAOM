#!/usr/bin/env python3
"""Unit tests for the dire wolf armory writer (tools/apply_dire_wolf_armory.py, #1081).

Pure stdlib, synthetic game dir, no real install needed (--game-dir points the whole script at a temp tree). Every case
builds its own tree, because the script mutates files in place.
"""
import io
import os
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET
from contextlib import redirect_stdout
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import apply_dire_wolf_armory as dw  # noqa: E402

BOM = b"\xef\xbb\xbf"
SETS = (
    "<action_sets>\r\n"
    "\t<!-- ============================== WARG ============================== -->\r\n"
    "\t<action_set id=\"as_warg\" skeleton=\"skeleton_warg\" movement_system=\"quadrupedal\">\r\n"
    "\t\t<action type=\"act_warg_attack_stand\" animation=\"warg_attack_stand\" />\r\n"
    "\t\t<action type=\"act_warg_stand_1\" animation=\"warg_idle\" alternative_group=\"warg_stand_alternative_group\" />\r\n"
    "\t</action_set>\r\n"
    "\t<action_set id=\"as_warg_town_and_village\" skeleton=\"skeleton_warg\" base_set=\"as_warg\">\r\n"
    "\t\t<action type=\"act_warg_stand_1\" animation=\"warg_idle\" />\r\n"
    "\t</action_set>\r\n"
    "\t<action_set id=\"as_warg_map\" skeleton=\"skeleton_warg\" base_set=\"as_warg\">\r\n"
    "\t\t<action type=\"act_map_mount_attack_1h\" animation=\"warg_attack_stand\" />\r\n"
    "\t</action_set>\r\n"
    "\t<!-- ============================== /WARG ============================== -->\r\n"
    "</action_sets>\r\n"
)
HORSES = "<Items>\r\n\t<Item id=\"fell_warg\" Type=\"Horse\" />\r\n</Items>\r\n"
LOC = ("<base><strings>\r\n    <string id=\"fell_warg\" text=\"[Dol Guldur] Fell Warg\"/>\r\n"
       "</strings></base>\r\n")
SUBMODULE = (
    "<Module><Xmls>\r\n\t\t\t<XmlNode>\r\n"
    "\t\t\t\t<XmlName id=\"Monsters\" path=\"Monsters/LOTR/lotr_monster_fell_warg\"/>\r\n"
    "\t\t\t\t<IncludedGameTypes><GameType value = \"Campaign\"/></IncludedGameTypes>\r\n"
    "\t\t\t</XmlNode>\r\n</Xmls></Module>\r\n"
)
PROJECT = ("<base>\r\n\t<file id=\"soln_monsters\" name=\"ModuleData/Monsters/LOTR/lotr_monster_fell_warg.xml\" "
           "type=\"monster\" />\r\n</base>\r\n")


class Tree:
    def __init__(self, root):
        self.armory = os.path.join(root, "Modules", "LOTRLOME_Armory")
        self.data = os.path.join(self.armory, "ModuleData")
        self.sets = self._put("ModuleData/action_sets.xml", SETS, bom=True)
        self.horses = self._put("ModuleData/LOTRLOME_items/LOTRAOM_horses.xml", HORSES)
        self.loc = self._put("ModuleData/Languages/loc_LOTRAOM_horses.xml", LOC)
        self.submodule = self._put("SubModule.xml", SUBMODULE)
        self.project = self._put("ModuleData/project.mbproj", PROJECT)
        self.monster = os.path.join(self.data, "Monsters", "LOTR", "lotr_monster_dire_wolf.xml")
        os.makedirs(os.path.dirname(self.monster))
        for clip in ("attack_stand", "idle"):
            self._put("Assets/creature/direwolf/animations/dire_wolf_%s_anm.tpac" % clip, "x")

    def _put(self, rel, text, bom=False):
        path = os.path.join(self.armory, rel)
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, "wb") as fh:
            fh.write((BOM if bom else b"") + text.encode("utf-8"))
        return path


def _bytes(path):
    with open(path, "rb") as fh:
        return fh.read()


def _set(path, set_id):
    root = ET.fromstring(_bytes(path).decode("utf-8-sig"))
    return next(s for s in root if s.get("id") == set_id)


class WriterTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.tree = Tree(self.tmp.name)

    def tearDown(self):
        self.tmp.cleanup()

    def _run(self, *extra):
        out = io.StringIO()
        with redirect_stdout(out), mock.patch.object(dw, "game_or_kit_running", return_value=False):
            code = dw.main(["--game-dir", self.tmp.name, *extra])
        return code, out.getvalue()

    def test_dry_run_writes_nothing(self):
        before = _bytes(self.tree.sets)
        code, out = self._run()
        self.assertEqual(code, 0, out)
        self.assertIn("dry run", out)
        self.assertEqual(_bytes(self.tree.sets), before)
        self.assertFalse(os.path.exists(self.tree.monster))

    def test_the_sets_copy_the_warg_with_the_wolf_clips_and_skeleton(self):
        code, out = self._run("--apply")
        self.assertEqual(code, 0, out)
        wolf = _set(self.tree.sets, "as_dire_wolf")
        self.assertEqual(wolf.get("skeleton"), "adod_wolf_armature_unused")
        self.assertEqual(wolf.get("movement_system"), "quadrupedal")
        rows = [(a.get("type"), a.get("animation"), a.get("alternative_group")) for a in wolf]
        self.assertEqual(rows, [("act_warg_attack_stand", "dire_wolf_attack_stand", None),
                                ("act_warg_stand_1", "dire_wolf_idle", "warg_stand_alternative_group")])
        for twin in ("as_dire_wolf_map", "as_dire_wolf_town_and_village"):
            self.assertEqual(_set(self.tree.sets, twin).get("base_set"), "as_dire_wolf")

    def test_the_warg_sets_stay_byte_identical_and_bom_and_crlf_survive(self):
        self._run("--apply")
        data = _bytes(self.tree.sets)
        self.assertTrue(data.startswith(BOM))
        self.assertEqual(data.count(b"\n"), data.count(b"\r\n"))
        start = SETS.index("\t<action_set")
        end = SETS.index("\t<!-- ============================== /WARG")
        self.assertIn(SETS[start:end].encode("utf-8"), data)

    def test_the_monster_is_warg_behaved_on_wolf_bones(self):
        self._run("--apply")
        monster = ET.parse(self.tree.monster).getroot().find("Monster")
        self.assertEqual(monster.get("id"), "dire_wolf")
        self.assertEqual(monster.get("monster_usage"), "warg")
        self.assertEqual(monster.get("action_set"), "as_dire_wolf")
        self.assertEqual(monster.get("family_type"), "1")
        self.assertEqual(monster.get("taom_body_length"), "115")
        self.assertEqual(monster.get("rider_sit_bone"), "Spine1")
        bones = [v for k, v in monster.attrib.items() if k.endswith("_bone") or "_bone_" in k]
        self.assertTrue(bones)
        self.assertFalse([b for b in bones if b.endswith("_M") or b.endswith("_L") or b.endswith("_R")], bones)
        self.assertEqual(monster.find("Flags").get("Mountable"), "true")

    def test_the_monster_lengths_are_the_pack_wolf_at_rig_scale(self):
        # The rig is 2.08 times the pack's wolf (prep_dire_wolf_meshes.RIG_SCALE), so every model-unit length is too.
        # The body capsule is in model space, so it also moves RIG_BACK toward the tail with the rig.
        self._run("--apply")
        monster = ET.parse(self.tree.monster).getroot().find("Monster")
        self.assertEqual(monster.get("standing_eye_height"), "1.83")
        self.assertEqual(monster.get("standing_pelvis_height"), "1.331")
        self.assertEqual(monster.get("rider_camera_height_adder"), "-0.437")
        self.assertEqual(monster.get("rein_handle_left_local_pos"), "0,-0.874,0.125")
        capsule = monster.find("Capsules/body_capsule")
        self.assertEqual((capsule.get("radius"), capsule.get("pos1"), capsule.get("pos2")),
                         ("0.416", "0, 0.624, 1.04", "0, -1.04, 1.04"))

    def test_the_header_says_where_the_size_lives(self):
        # Battle size is the rig times taom_body_length; portraits and the inventory draw the rig alone.
        text = dw.monster_text("\n")
        self.assertIn("RIG_SCALE", text)
        self.assertIn("battle only", text)

    def test_the_items_cover_each_skin_and_the_saddle_shares_the_family(self):
        self._run("--apply")
        root = ET.parse(self.tree.horses).getroot()
        items = {i.get("id"): i for i in root}
        for skin, culture in (("grey", "gundabad"), ("white", "gundabad"), ("black", "goblin"), ("brown", "goblin")):
            item = items["dire_wolf_" + skin]
            self.assertEqual(item.get("mesh"), "dire_wolf_" + skin)
            self.assertEqual(item.get("culture"), "Culture." + culture)
            self.assertEqual(item.find("ItemComponent/Horse").get("monster"), "Monster.dire_wolf")
        saddle = items["dire_wolf_saddle"]
        self.assertEqual(saddle.get("Type"), "HorseHarness")
        self.assertEqual(saddle.find("ItemComponent/Armor").get("family_type"), "1")

    def test_every_item_has_an_english_row(self):
        self._run("--apply")
        loc = _bytes(self.tree.loc).decode("utf-8")
        for item_id in dw.ITEM_NAMES:
            self.assertIn('<string id="%s" text="%s"/>' % (item_id, dw.ITEM_NAMES[item_id]), loc)

    def test_the_monster_is_registered_once_in_submodule_and_project(self):
        self._run("--apply")
        self._run("--apply")
        self.assertEqual(_bytes(self.tree.submodule).count(b"lotr_monster_dire_wolf"), 1)
        self.assertEqual(_bytes(self.tree.project).count(
            b'<file id="soln_monsters" name="ModuleData/Monsters/LOTR/lotr_monster_dire_wolf.xml" type="monster" />'), 1)
        ET.parse(self.tree.submodule)
        ET.parse(self.tree.project)

    def test_a_second_run_changes_nothing_and_a_backup_is_written_once(self):
        self._run("--apply")
        after = _bytes(self.tree.sets), _bytes(self.tree.horses), _bytes(self.tree.monster)
        code, out = self._run("--apply")
        self.assertEqual(code, 0, out)
        self.assertIn("nothing to write", out)
        self.assertEqual((_bytes(self.tree.sets), _bytes(self.tree.horses), _bytes(self.tree.monster)), after)
        folder = os.path.dirname(self.tree.sets)
        self.assertEqual(len([f for f in os.listdir(folder) if f.startswith("action_sets.xml.bak-direwolf-")]), 1)

    def test_a_backup_never_has_an_xml_extension(self):
        self._run("--apply")
        for folder, _, files in os.walk(self.tree.armory):
            self.assertFalse([f for f in files if ".bak-" in f and f.endswith(".xml")], folder)

    def test_a_missing_clip_is_refused_and_nothing_is_written(self):
        os.remove(os.path.join(self.tree.armory, "Assets/creature/direwolf/animations/dire_wolf_idle_anm.tpac"))
        before = _bytes(self.tree.sets)
        code, out = self._run("--apply")
        self.assertEqual(code, 1)
        self.assertIn("CLIP MISSING: dire_wolf_idle", out)
        self.assertEqual(_bytes(self.tree.sets), before)
        self.assertFalse(os.path.exists(self.tree.monster))

    def test_a_warg_binding_with_no_warg_prefix_is_refused(self):
        self.tree._put("ModuleData/action_sets.xml", SETS.replace('animation="warg_idle" />', 'animation="horse_idle" />'))
        code, out = self._run("--apply")
        self.assertEqual(code, 1)
        self.assertIn("horse_idle", out)

    def test_a_partly_present_set_family_is_refused(self):
        self.tree._put("ModuleData/action_sets.xml", SETS.replace(
            "</action_sets>", '<action_set id="as_dire_wolf" skeleton="x" /></action_sets>'))
        code, out = self._run("--apply")
        self.assertEqual(code, 1)
        self.assertIn("partly present", out)

    def test_a_missing_anchor_is_refused(self):
        self.tree._put("ModuleData/Languages/loc_LOTRAOM_horses.xml", "<base><strings></strings></base>")
        code, out = self._run("--apply")
        self.assertEqual(code, 1)
        self.assertIn("loc_LOTRAOM_horses", out)
        self.assertFalse(os.path.exists(self.tree.monster))

    def test_a_partly_present_item_group_is_refused(self):
        self.tree._put("ModuleData/LOTRLOME_items/LOTRAOM_horses.xml", HORSES.replace(
            "</Items>", '<Item id="dire_wolf_grey" Type="Horse" /></Items>'))
        code, out = self._run("--apply")
        self.assertEqual(code, 1)
        self.assertIn("partly present", out)

    def test_a_partly_present_loc_group_is_refused(self):
        self.tree._put("ModuleData/Languages/loc_LOTRAOM_horses.xml", LOC.replace(
            "</strings>", '<string id="dire_wolf_grey" text="x"/></strings>'))
        code, out = self._run("--apply")
        self.assertEqual(code, 1)
        self.assertIn("partly present", out)

    def test_a_commented_out_item_counts_as_absent(self):
        rows = "".join('<Item id="%s" Type="Horse" />' % i for i in dw.ITEM_NAMES if i != "dire_wolf_saddle")
        self.tree._put("ModuleData/LOTRLOME_items/LOTRAOM_horses.xml", HORSES.replace(
            "</Items>", rows + '<!-- <Item id="dire_wolf_saddle" /> --></Items>'))
        code, out = self._run()
        self.assertEqual(code, 1, out)
        self.assertIn("partly present", out)

    def test_a_commented_out_loc_row_counts_as_absent(self):
        rows = "".join('<string id="%s" text="x"/>' % i for i in dw.ITEM_NAMES if i != "dire_wolf_saddle")
        self.tree._put("ModuleData/Languages/loc_LOTRAOM_horses.xml", LOC.replace(
            "</strings>", rows + '<!-- <string id="dire_wolf_saddle" text="x"/> --></strings>'))
        code, out = self._run()
        self.assertEqual(code, 1, out)
        self.assertIn("partly present", out)

    def test_a_monster_file_without_the_monster_is_refused(self):
        self.tree._put("ModuleData/Monsters/LOTR/lotr_monster_dire_wolf.xml", '<Monsters><Monster id="other" /></Monsters>')
        code, out = self._run()
        self.assertEqual(code, 1)
        self.assertIn("MONSTER", out)

    def test_a_monster_file_that_does_not_parse_is_refused(self):
        self.tree._put("ModuleData/Monsters/LOTR/lotr_monster_dire_wolf.xml", "<Monsters>")
        code, out = self._run()
        self.assertEqual(code, 1)
        self.assertIn("does not parse", out)

    def test_a_warg_set_on_another_skeleton_is_refused(self):
        self.tree._put("ModuleData/action_sets.xml", SETS.replace(
            'id="as_warg_map" skeleton="skeleton_warg"', 'id="as_warg_map" skeleton="other"'))
        code, out = self._run()
        self.assertEqual(code, 1)
        self.assertIn("DERIVED: as_dire_wolf_map", out)

    def test_each_missing_anchor_is_refused(self):
        for rel, text, word in (("SubModule.xml", "<Module></Module>", "SubModule"),
                                ("ModuleData/project.mbproj", "<base></base>", "project.mbproj"),
                                ("ModuleData/action_sets.xml", SETS.replace("/WARG", "/OTHER"), "/WARG"),
                                ("ModuleData/LOTRLOME_items/LOTRAOM_horses.xml", "<Items />", "</Items>")):
            with self.subTest(rel), tempfile.TemporaryDirectory() as root:
                tree = Tree(root)
                tree._put(rel, text)
                out = io.StringIO()
                with redirect_stdout(out), mock.patch.object(dw, "game_or_kit_running", return_value=False):
                    code = dw.main(["--game-dir", root, "--apply"])
                self.assertEqual(code, 1, out.getvalue())
                self.assertIn(word, out.getvalue())
                self.assertFalse(os.path.exists(tree.monster))

    def test_a_document_that_stops_parsing_writes_nothing(self):
        self.tree._put("ModuleData/project.mbproj", PROJECT.replace("</base>", "</base><broken"))
        before = _bytes(self.tree.sets)
        code, out = self._run("--apply")
        self.assertEqual(code, 1, out)
        self.assertIn("PARSE FAILED", out)
        self.assertEqual(_bytes(self.tree.sets), before)

    def test_an_empty_game_dir_exits_2(self):
        out = io.StringIO()
        with redirect_stdout(out):
            self.assertEqual(dw.main(["--game-dir", "  "]), 2)
        self.assertIn("--game-dir is empty", out.getvalue())

    def test_apply_refuses_while_the_game_or_kit_runs(self):
        out = io.StringIO()
        with redirect_stdout(out), mock.patch.object(dw, "game_or_kit_running", return_value=True):
            code = dw.main(["--game-dir", self.tmp.name, "--apply"])
        self.assertEqual(code, 2)
        self.assertFalse(os.path.exists(self.tree.monster))


class LiveRecipeParityTests(unittest.TestCase):
    """The Monster's bones against the shipped skeleton: a name the rig lacks reaches the engine's bone lookups. Skipped
    where the Armory or the tpac libraries are absent (CI, the laptop)."""

    def test_every_bone_the_monster_names_is_on_the_shipped_skeleton(self):
        root = os.environ.get("BANNERLORD_GAME_DIR") or dw._DEFAULT_GAME_DIR
        grey = os.path.join(root, "Modules", "LOTRLOME_Armory", "Assets", "creature", "direwolf", "mesh", "dire_wolf_grey_geo.tpac")
        if not os.path.isfile(grey):
            self.skipTest("the dire wolf grey package is not installed here")
        try:
            import tpac_skeleton_copy_physics as cp
        except ImportError as exc:
            self.skipTest("the tpac libraries are missing: %s" % exc)
        names = {b["name"] for b in cp.load_skeleton(_bytes(grey), dw.SKELETON)["bones"]}
        self.assertFalse(dw.monster_bones() - names)

    def test_the_live_monster_is_the_recipe(self):
        # The writer leaves an existing Monster alone, so a hand tune in the live file would revert on a reinstall.
        root = os.environ.get("BANNERLORD_GAME_DIR") or dw._DEFAULT_GAME_DIR
        armory = os.path.join(root, "Modules", "LOTRLOME_Armory")
        if not os.path.isdir(armory):
            self.skipTest("the Armory is not installed here")
        text = _bytes(os.path.join(armory, *dw.MONSTER_FILE.split("/"))).decode("utf-8")
        self.assertEqual(text, dw.monster_text("\r\n" if "\r\n" in text else "\n"))


class RigScaleTests(unittest.TestCase):
    """Five tools carry the rig's size; one value of RIG_SCALE must hold in all of them."""

    def test_every_tool_names_the_same_rig_scale(self):
        import re
        tools = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
        found = {}
        for rel in ("apply_dire_wolf_armory.py", "apply_dire_wolf_skeleton.py", "gen_dire_wolf_anim_clips.ps1",
                    os.path.join("blender", "prep_dire_wolf_meshes.py"), os.path.join("blender", "scale_dire_wolf_anims.py")):
            with open(os.path.join(tools, rel), encoding="utf-8") as fh:
                m = re.search(r"^\$?RIG_SCALE\s*=\s*([0-9.]+)", fh.read(), re.M)
            self.assertIsNotNone(m, rel)
            found[rel] = float(m.group(1))
        self.assertEqual(set(found.values()), {dw.RIG_SCALE}, found)

    def test_every_tool_names_the_same_rig_back(self):
        # The rig moves RIG_BACK toward the tail in the meshes, the masters, the skeleton and the body capsule.
        import re
        tools = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
        found = {}
        for rel in ("apply_dire_wolf_armory.py", "apply_dire_wolf_skeleton.py",
                    os.path.join("blender", "prep_dire_wolf_meshes.py"), os.path.join("blender", "scale_dire_wolf_anims.py")):
            with open(os.path.join(tools, rel), encoding="utf-8") as fh:
                m = re.search(r"^RIG_BACK\s*=\s*([0-9.]+)", fh.read(), re.M)
            self.assertIsNotNone(m, rel)
            found[rel] = float(m.group(1))
        self.assertEqual(set(found.values()), {dw.RIG_BACK}, found)

    def test_the_mesh_and_animation_tools_name_the_same_tail(self):
        # The meshes move inside the armature and the masters move the object; one TAIL keeps both moves one vector.
        import re
        tools = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
        found = {}
        for rel in (os.path.join("blender", "prep_dire_wolf_meshes.py"), os.path.join("blender", "scale_dire_wolf_anims.py")):
            with open(os.path.join(tools, rel), encoding="utf-8") as fh:
                m = re.search(r"^TAIL\s*=\s*\(([^)]*)\)", fh.read(), re.M)
            self.assertIsNotNone(m, rel)
            found[rel] = tuple(float(v) for v in m.group(1).split(","))
        self.assertEqual(len(set(found.values())), 1, found)


if __name__ == "__main__":
    unittest.main()
