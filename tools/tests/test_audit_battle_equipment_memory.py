#!/usr/bin/env python3
"""Unit tests for the battle equipment memory audit (tools/audit_battle_equipment_memory.py)
and for the loose `Assets` tree option it adds to the shared map tool index.

Run:  python -m unittest discover -s tools/tests -t .
  or:  python -B -m unittest tools.tests.test_audit_battle_equipment_memory -v

Pure stdlib, synthetic inputs, no game install. Packs are written with encoders copied from
tools/tests/test_audit_map_scene_memory.py (that module imports pytest, so it cannot be
imported here; the CI runner only runs unittest modules).
"""
import contextlib
import hashlib
import io
import struct
import sys
import tempfile
import unittest
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent.parent

sys.path.insert(0, str(REPO / "tools"))
import audit_map_scene_memory as amsm  # noqa: E402

TYPE_GUID = {kind: guid for guid, kind in amsm.KIND_BY_TYPE_GUID.items()}


# --------------------------------------------------------------------------- #
# encoders (copied from tools/tests/test_audit_map_scene_memory.py:61-131)
# --------------------------------------------------------------------------- #
def sized(s: str) -> bytes:
    raw = s.encode("utf-8")
    return struct.pack("<i", len(raw)) + raw


def strs(items) -> bytes:
    return b"".join(sized(s) for s in items)


def guid_for(name: str) -> bytes:
    return hashlib.md5(name.encode("utf-8")).digest()


def encode_texture_meta(source, width, height, mips, fmt, faces=1, flags=(), flags2=()):
    b = struct.pack("<I", 3) + bytes(20) + sized(source) + bytes(8) + b"\x00" + struct.pack("<I", 0)
    b += struct.pack("<I", len(flags)) + strs(flags)
    b += struct.pack("<I", 2) + b"\x02" + struct.pack("<III", width, height, 1) + bytes([mips, faces, 0])
    b += sized(fmt) + struct.pack("<I", 0) + struct.pack("<I", len(flags2)) + strs(flags2) + sized("none")
    return b + bytes(45)


def encode_material_meta(shader_guid, textures, flags=(), tags=("bumpmap",), blend="no_alpha_blend", flags2=()):
    b = bytes(20) + struct.pack("<II", 2, 0) + struct.pack("<I", len(flags)) + strs(flags) + struct.pack("<I", 0)
    b += struct.pack("<I", len(tags)) + strs(tags) + sized(blend) + shader_guid + struct.pack("<I", len(textures))
    for slot, guid in textures:
        b += struct.pack("<I", slot) + guid
    b += struct.pack("<I", 0) + struct.pack("<I", len(flags2)) + strs(flags2) + bytes(112)
    return b


def encode_metamesh_meta(name, records, header_material=bytes(16)):
    """records: [(sub name, material guid, positions, faces, vertices)]"""
    b = struct.pack("<I", 1) + bytes(16) + struct.pack("<f", 3.4e38) + bytes(28)
    b += struct.pack("<III", len(records), 1, 512) + b"\x00" + header_material
    for sub, material, positions, faces, vertices in records:
        b += struct.pack("<I", 2) + guid_for("mesh:" + sub) + sized(sub) + bytes(8) + material + bytes(68)
        b += struct.pack("<III", positions, faces, vertices) + bytes(100)
    return b


def write_tpac(path, items):
    """items: [(kind, name, guid, meta, [(segment type guid, payload bytes)])]"""
    toc_items = []
    payloads = []
    for kind, name, guid, meta, segments in items:
        toc_items.append((TYPE_GUID[kind], name, guid, meta, segments))
        payloads.extend(p for _t, p in segments)

    def build(base):
        out = b"TPAC" + struct.pack("<I", 2) + guid_for("package") + struct.pack("<I", len(toc_items)) + bytes(8)
        running = base
        for type_guid, name, guid, meta, segments in toc_items:
            out += type_guid + guid + struct.pack("<I", 0) + sized(name) + struct.pack("<q", len(meta)) + meta
            out += bytes(8) + struct.pack("<i", len(segments))
            for seg_type, payload in segments:
                out += struct.pack("<QQQ", running, len(payload), len(payload)) + guid + seg_type
                out += struct.pack("<QI", 0, 0) + b"\x00"
                running += len(payload)
            out += struct.pack("<i", 0)
        return out

    toc = build(0)
    toc = build(len(toc))
    with open(path, "wb") as f:
        f.write(toc + b"".join(payloads))


# --------------------------------------------------------------------------- #
# small fixture helpers
# --------------------------------------------------------------------------- #
def mesh_item(name, material_guid=bytes(16), render=b"", edit=b"", other=b"", records=None, guid=None):
    """A metamesh tpac item. `records` defaults to one LOD 0 record of 10 faces."""
    if records is None:
        records = [(name, material_guid, 8, 10, 12)]
    segments = []
    if edit:
        segments.append((amsm.SEG_MESH_A, edit))
    if render:
        segments.append((amsm.SEG_MESH_B, render))
    if other:
        segments.append((amsm.SEG_MESH_TABLE, other))
    return ("metamesh", name, guid or guid_for("metamesh:" + name),
            encode_metamesh_meta(name, records), segments)


def write_pack(path, items):
    Path(path).parent.mkdir(parents=True, exist_ok=True)
    write_tpac(path, items)


# --------------------------------------------------------------------------- #
# Step 2: the shared index's `trees` option
# --------------------------------------------------------------------------- #
class SharedIndexTreesTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.root = Path(self._tmp.name)

    def tearDown(self):
        self._tmp.cleanup()

    def _module_with_loose_tree(self, also_c=False):
        mod = self.root / "M"
        packed = [mesh_item("a", render=b"r" * 4)]
        loose = [mesh_item("b", edit=b"e" * 4)]
        if also_c:
            packed.append(mesh_item("c", render=b"r" * 8))
            loose.append(mesh_item("c", edit=b"e" * 8, guid=guid_for("loose c")))
        write_pack(mod / "AssetPackages" / "pack0.tpac", packed)
        write_pack(mod / "Assets" / "x" / "b_geo.tpac", loose)
        return mod

    def test_default_trees_ignore_a_loose_assets_tree(self):
        mod = self._module_with_loose_tree()
        index = amsm.AssetIndex()
        index.add_module("M", mod)
        self.assertIsNotNone(index.get("metamesh", "a"))
        self.assertIsNone(index.get("metamesh", "b"))

    def test_loose_tree_replaces_the_pack_trees_of_a_module_that_has_one(self):
        # the engine loads a module's loose Assets tree and not the cooked packs it also ships
        # (docs/reference/armory-guide.md "Two asset trees"): the loose copy wins, and a name only the
        # cooked tree holds does not exist, by name or by guid
        mod = self._module_with_loose_tree(also_c=True)
        index = amsm.AssetIndex()
        index.add_module("M", mod, trees=amsm.PACK_TREES + ("Assets",))
        self.assertIsNone(index.get("metamesh", "a"))
        self.assertNotIn(guid_for("metamesh:a"), index.by_guid)
        self.assertIsNotNone(index.get("metamesh", "b"))
        c = index.get("metamesh", "c")
        self.assertTrue(c.pack.startswith("Assets/"), c.pack)
        self.assertEqual(c.seg_bytes(amsm.SEG_MESH_A), 8)
        self.assertNotIn(("metamesh", "c"), index.also_in)
        self.assertEqual(index.skipped_trees, [("M", "AssetPackages")])

    def test_a_module_without_a_loose_tree_keeps_its_packs_when_loose_is_asked(self):
        mod = self.root / "N"
        write_pack(mod / "AssetPackages" / "pack0.tpac", [mesh_item("a", render=b"r" * 4)])
        index = amsm.AssetIndex()
        index.add_module("N", mod, trees=amsm.PACK_TREES + ("Assets",))
        self.assertIsNotNone(index.get("metamesh", "a"))
        self.assertEqual(index.skipped_trees, [])

    def test_a_loose_folder_with_no_tpac_does_not_replace_the_packs(self):
        mod = self.root / "N"
        write_pack(mod / "AssetPackages" / "pack0.tpac", [mesh_item("a", render=b"r" * 4)])
        (mod / "Assets" / "x").mkdir(parents=True)
        (mod / "Assets" / "x" / "readme.txt").write_text("not a pack", encoding="utf-8")
        index = amsm.AssetIndex()
        index.add_module("N", mod, trees=amsm.PACK_TREES + ("Assets",))
        self.assertIsNotNone(index.get("metamesh", "a"))
        self.assertEqual(index.skipped_trees, [])

    def test_the_default_trees_skip_nothing_even_when_a_loose_tree_exists(self):
        mod = self._module_with_loose_tree(also_c=True)
        index = amsm.AssetIndex()
        index.add_module("M", mod)
        self.assertEqual(index.skipped_trees, [])
        self.assertTrue(index.get("metamesh", "c").pack.startswith("AssetPackages/"))

    def test_missing_trees_error_names_the_trees_searched(self):
        empty = self.root / "E"
        empty.mkdir()
        index = amsm.AssetIndex()
        index.add_module("E", empty)
        self.assertEqual(index.errors[-1][2], "no AssetPackages or EmAssetPackages directory")
        index2 = amsm.AssetIndex()
        index2.add_module("E", empty, trees=amsm.PACK_TREES + ("Assets",))
        self.assertEqual(index2.errors[-1][2], "no AssetPackages or EmAssetPackages or Assets directory")

    def test_map_cli_accepts_loose_assets(self):
        game = self.root / "game"
        scene = game / "Modules" / "TAOM_Map" / "SceneObj" / "Main_map"
        scene.mkdir(parents=True)
        (scene / "scene.xscene").write_text("<scene><entities/></scene>", encoding="utf-8")
        write_pack(game / "Modules" / "TAOM_Map" / "Assets" / "p_geo.tpac", [mesh_item("only_loose", edit=b"e")])
        out = io.StringIO()
        with contextlib.redirect_stdout(out):
            rc = amsm.main(["--game-dir", str(game), "--modules", "TAOM_Map",
                            "--out-dir", str(self.root / "out"), "--loose-assets"])
        self.assertEqual(rc, 0)
        self.assertIn("packs indexed: 1", out.getvalue())


# --------------------------------------------------------------------------- #
# synthetic module trees (SubModule.xml registrations plus ModuleData files)
# --------------------------------------------------------------------------- #
def write_module(root, name, registrations, files):
    """registrations: [(xml id, path, game types or None)]; files: {ModuleData relative path: text}."""
    mod = Path(root) / name
    (mod / "ModuleData").mkdir(parents=True, exist_ok=True)
    nodes = []
    for xml_id, path, game_types in registrations:
        inner = f'<XmlName id="{xml_id}" path="{path}"/>'
        if game_types is not None:
            inner += "<IncludedGameTypes>" + "".join(
                f'<GameType value="{g}"/>' for g in game_types) + "</IncludedGameTypes>"
        nodes.append(f"<XmlNode>{inner}</XmlNode>")
    (mod / "SubModule.xml").write_text(
        f'<Module><Id value="{name}"/><Xmls>{"".join(nodes)}</Xmls></Module>', encoding="utf-8")
    for rel, text in files.items():
        p = mod / "ModuleData" / rel
        p.parent.mkdir(parents=True, exist_ok=True)
        p.write_text(text, encoding="utf-8")
    return mod


def items_xml(*items):
    return "<Items>" + "".join(items) + "</Items>"


def chars_xml(*chars):
    return "<NPCCharacters>" + "".join(chars) + "</NPCCharacters>"


HUMAN_SKINS = """<skins>
  <race
      id="human">
    <skin gender="0" mesh_maturity_type="adult" body_meta_mesh="body_m" body_meta_mesh_shoulders=""
          body_meta_mesh_upperbody="" face_meta_mesh="head_m" hands_mesh="" legs_mesh=""
          underwear_bottom_mesh="" underwear_top_mesh="">
      <hair_meshes><hair_mesh name="hair_a" cover_type1="hair_a_c1"/></hair_meshes>
      <beard_meshes><beard_mesh name="beard_a"/></beard_meshes>
    </skin>
  </race>
</skins>"""


def make_modules(root):
    """Two modules, Native then TAOM, in load order. Returns [(name, path)]."""
    native = write_module(root, "Native", [
        ("Items", "items", None),
        ("CraftingPieces", "crafting_pieces", None),
        ("EquipmentRosters", "equipment_sets", None),
        ("NPCCharacters", "chars", None),
        ("Items", "mpitems", ["MultiplayerGame"]),
    ], {
        "items/a.xml": items_xml(
            '<Item id="h" mesh="h_mesh" Type="HeadArmor"><ItemComponent><Armor head_armor="1"/></ItemComponent></Item>',
            '<Item id="dup_item" mesh="native_dup" Type="Goods"/>'),
        "items/a.xml.bak-1": items_xml('<Item id="bak_item" mesh="bak" Type="Goods"/>'),
        "mpitems.xml": items_xml('<Item id="mp_only" mesh="mp" Type="Goods"/>'),
        "crafting_pieces.xml": "<CraftingPieces>"
            '<CraftingPiece id="p_blade" piece_type="Blade" mesh="p_blade_mesh">'
            '<BladeData body_name="bo_p_blade" holster_mesh="p_scabbard" holster_body_name="bo_p_scabbard"/>'
            "</CraftingPiece>"
            '<CraftingPiece id="p_handle" piece_type="Handle" mesh="p_handle_mesh"/>'
            "</CraftingPieces>",
        "equipment_sets.xml": "<EquipmentRosters>"
            '<EquipmentRoster id="std">'
            '<EquipmentSet><Equipment slot="Body" id="Item.std_body"/></EquipmentSet>'
            '<EquipmentSet equipmentType="Civilian"><Equipment slot="Body" id="Item.std_civ"/></EquipmentSet>'
            '<EquipmentSet equipmentType="Stealth"><Equipment slot="Body" id="Item.std_stealth"/></EquipmentSet>'
            "</EquipmentRoster>"
            "</EquipmentRosters>",
        "chars.xml": chars_xml('<NPCCharacter id="native_guy" culture="Culture.empire"/>'),
        "skins.xml": HUMAN_SKINS,
    })
    taom = write_module(root, "TAOM", [
        ("Items", "taom_items", None),
        ("NPCCharacters", "troops/t", None),
    ], {
        "taom_items.xml": items_xml('<Item id="dup_item" mesh="taom_dup" Type="Goods"/>'),
        "troops/t.xml": chars_xml(
            '<NPCCharacter id="soldier" culture="Culture.gondor" race="elf">'
            "<Equipments>"
            '<EquipmentRoster><equipment slot="Item0" id="Item.w"/><equipment slot="Body" id="Item.b"/></EquipmentRoster>'
            '<EquipmentRoster civilian="true"><equipment slot="Body" id="Item.civ_body"/></EquipmentRoster>'
            '<EquipmentSet id="std"/>'
            '<EquipmentSet id="std" equipmentType="Civilian"/>'
            '<equipment slot="Head" id="Item.h"/>'
            "</Equipments></NPCCharacter>"),
    })
    return [("Native", native), ("TAOM", taom)]


def reasons(defs):
    return [r for r, _s, _b in defs.unresolved]


class DefinitionsTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.root = Path(self._tmp.name)
        import audit_battle_equipment_memory as abem
        self.abem = abem

    def tearDown(self):
        self._tmp.cleanup()

    def test_registration_path_can_be_a_file_or_a_folder(self):
        modules = make_modules(self.root)
        native = modules[0][1]
        folder = self.abem.registered_files(native, "items")
        self.assertEqual([p.name for p in folder], ["a.xml"])
        single = self.abem.registered_files(native, "crafting_pieces")
        self.assertEqual([p.name for p in single], ["crafting_pieces.xml"])
        self.assertEqual(self.abem.registered_files(native, "nothing_here"), [])

    def test_registration_for_another_game_type_is_skipped(self):
        mod = write_module(self.root, "M", [
            ("Items", "mp", ["MultiplayerGame"]),
            ("Items", "both", ["Campaign", "CustomGame"]),
            ("NPCCharacters", "plain", None),
        ], {})
        self.assertEqual(self.abem.read_registrations(mod), [("Items", "both"), ("NPCCharacters", "plain")])

    def test_registered_but_missing_file_is_reported(self):
        mod = write_module(self.root, "M", [("NPCCharacters", "characters/gone", None)], {})
        defs = self.abem.load_definitions([("M", mod)])
        self.assertIn(("REGISTERED_FILE_MISSING", "M/characters/gone", "NPCCharacters"), defs.unresolved)

    def test_xslt_beside_a_registration_is_reported_not_applied(self):
        mod = write_module(self.root, "M", [("NPCCharacters", "lords", None)],
                           {"lords.xml": chars_xml('<NPCCharacter id="x"/>'), "lords.xslt": "<xsl/>"})
        defs = self.abem.load_definitions([("M", mod)])
        self.assertEqual(reasons(defs), ["XSLT_NOT_APPLIED"])
        self.assertIn("x", defs.characters)

    def test_later_module_definition_wins_and_the_duplicate_is_reported(self):
        defs = self.abem.load_definitions(make_modules(self.root))
        self.assertEqual(defs.items["dup_item"].module, "TAOM")
        self.assertEqual(defs.items["dup_item"].mesh, "taom_dup")
        dups = [row for row in defs.unresolved if row[0] == "DUPLICATE_DEFINITION"]
        self.assertEqual([row[1] for row in dups], ["dup_item"])
        self.assertNotIn("mp_only", defs.items)
        self.assertNotIn("bak_item", defs.items)

    def test_item_fields(self):
        mod = write_module(self.root, "M", [("Items", "i", None)], {"i.xml": items_xml(
            '<Item id="full" mesh="m" holster_mesh="hm" holster_mesh_with_weapon="hmw" flying_mesh="fm" '
            'body_name="bo" holster_body_name="hbo" shield_body_name="sbo" Type="BodyArmor">'
            '<ItemComponent><Armor has_gender_variations="false" reins_mesh="r"/></ItemComponent></Item>',
            '<Item id="plain" mesh="p" Type="LegArmor"><ItemComponent><Armor/></ItemComponent></Item>')})
        defs = self.abem.load_definitions([("M", mod)])
        it = defs.items["full"]
        self.assertEqual((it.mesh, it.holster_mesh, it.holster_mesh_with_weapon, it.flying_mesh),
                         ("m", "hm", "hmw", "fm"))
        self.assertEqual((it.body_name, it.holster_body_name, it.shield_body_name), ("bo", "hbo", "sbo"))
        self.assertEqual((it.type, it.component, it.has_gender_variations, it.reins_mesh),
                         ("BodyArmor", "Armor", False, "r"))
        self.assertEqual(it.tag, "Item")
        self.assertTrue(defs.items["plain"].has_gender_variations)

    def test_crafted_item_type_comes_from_its_template(self):
        mod = write_module(self.root, "M", [("Items", "i", None)], {"i.xml": items_xml(
            '<CraftedItem id="axe" crafting_template="OneHandedAxe"><Pieces>'
            '<Piece id="p_blade" Type="Blade"/><Piece id="p_handle" Type="Handle"/></Pieces></CraftedItem>',
            '<CraftedItem id="odd" crafting_template="Boomerang"><Pieces/></CraftedItem>')})
        defs = self.abem.load_definitions([("M", mod)])
        self.assertEqual(defs.items["axe"].type, "OneHandedWeapon")
        self.assertEqual(defs.items["axe"].tag, "CraftedItem")
        self.assertEqual(defs.items["axe"].pieces, [("p_blade", "Blade"), ("p_handle", "Handle")])
        self.assertEqual(defs.items["odd"].type, "UNKNOWN")
        self.assertIn(("CRAFTING_TEMPLATE_UNKNOWN", "Boomerang", "odd"), defs.unresolved)

    def test_crafting_piece_reads_mesh_and_blade_data(self):
        defs = self.abem.load_definitions(make_modules(self.root))
        blade = defs.pieces["p_blade"]
        self.assertEqual((blade.piece_type, blade.mesh), ("Blade", "p_blade_mesh"))
        self.assertEqual((blade.blade_body_name, blade.blade_holster_mesh, blade.blade_holster_body_name),
                         ("bo_p_blade", "p_scabbard", "bo_p_scabbard"))
        self.assertTrue(blade.has_blade)
        handle = defs.pieces["p_handle"]
        self.assertEqual(handle.mesh, "p_handle_mesh")
        self.assertFalse(handle.has_blade)

    def test_horse_reads_additional_meshes_and_materials(self):
        mod = write_module(self.root, "M", [("Items", "i", None)], {"i.xml": items_xml(
            '<Item id="horse" mesh="horse_m" Type="Horse"><ItemComponent><Horse monster="Monster.horse">'
            '<Materials><Material name="mat_a"/><Material name="mat_b"/></Materials>'
            '<AdditionalMeshes><Mesh name="mane"/><Mesh name="tail" affected_by_cover="true"/></AdditionalMeshes>'
            "</Horse></ItemComponent></Item>")})
        defs = self.abem.load_definitions([("M", mod)])
        h = defs.items["horse"]
        self.assertEqual(h.component, "Horse")
        self.assertEqual(h.horse_materials, ["mat_a", "mat_b"])
        self.assertEqual(h.additional_meshes, ["mane", "tail"])

    def test_character_battle_sets_follow_the_engine_rules(self):
        defs = self.abem.load_definitions(make_modules(self.root))
        soldier = defs.characters["soldier"]
        battle = self.abem.battle_sets(soldier, defs)
        self.assertEqual(len(battle), 2)
        self.assertEqual(battle[0], {"Weapon0": "w", "Body": "b", "Head": "h"})
        self.assertEqual(battle[1], {"Body": "std_body", "Head": "h"})
        both = self.abem.battle_sets(soldier, defs, include_civilian=True)
        self.assertEqual(len(both), 4)
        self.assertTrue(all(s["Head"] == "h" for s in both))
        bodies = sorted(s["Body"] for s in both)
        self.assertEqual(bodies, ["b", "civ_body", "std_body", "std_civ"])
        self.assertNotIn("std_stealth", [s.get("Body") for s in both])

    def test_override_replaces_the_same_slot_in_either_spelling(self):
        mod = write_module(self.root, "M", [("NPCCharacters", "c", None)], {"c.xml": chars_xml(
            '<NPCCharacter id="t"><Equipments>'
            '<EquipmentRoster><equipment slot="Weapon0" id="Item.a"/></EquipmentRoster>'
            '<equipment slot="Item0" id="Item.b"/></Equipments></NPCCharacter>')})
        defs = self.abem.load_definitions([("M", mod)])
        self.assertEqual(self.abem.battle_sets(defs.characters["t"], defs), [{"Weapon0": "b"}])

    def test_lowercase_equipment_roster_adds_no_set(self):
        mod = write_module(self.root, "M", [("NPCCharacters", "c", None)], {"c.xml": chars_xml(
            '<NPCCharacter id="lower"><equipments>'
            '<equipmentRoster><equipment slot="Body" id="Item.x"/></equipmentRoster>'
            '<EquipmentRoster><equipment slot="Leg" id="Item.y"/></EquipmentRoster>'
            "</equipments></NPCCharacter>")})
        defs = self.abem.load_definitions([("M", mod)])
        self.assertEqual(self.abem.battle_sets(defs.characters["lower"], defs), [{"Leg": "y"}])

    def test_item_id_takes_the_part_after_the_first_dot(self):
        mod = write_module(self.root, "M", [("NPCCharacters", "c", None)], {"c.xml": chars_xml(
            '<NPCCharacter id="t"><Equipments><EquipmentRoster>'
            '<equipment slot="Body" id="Item.x"/><equipment slot="Leg" id="x"/>'
            "</EquipmentRoster></Equipments></NPCCharacter>")})
        defs = self.abem.load_definitions([("M", mod)])
        self.assertEqual(self.abem.battle_sets(defs.characters["t"], defs), [{"Body": "x", "Leg": "x"}])

    def test_unresolved_roster_is_reported(self):
        mod = write_module(self.root, "M", [("NPCCharacters", "c", None)], {"c.xml": chars_xml(
            '<NPCCharacter id="t"><Equipments>'
            '<EquipmentRoster><equipment slot="Body" id="Item.x"/></EquipmentRoster>'
            '<EquipmentSet id="nowhere"/></Equipments></NPCCharacter>')})
        defs = self.abem.load_definitions([("M", mod)])
        self.assertEqual(defs.unresolved, [("ROSTER_UNRESOLVED", "nowhere", "t")])
        self.assertEqual(self.abem.battle_sets(defs.characters["t"], defs), [{"Body": "x"}])
        self.abem.battle_sets(defs.characters["t"], defs)
        self.assertEqual(len(defs.unresolved), 1)

    def test_skins_parse_multi_line_race_ids_and_the_adult_skin(self):
        mod = write_module(self.root, "M", [], {"skins.xml": """<skins>
  <race
      id="r">
    <skin gender="0" mesh_maturity_type="child" body_meta_mesh="child_body"/>
    <skin gender="0" mesh_maturity_type="adult" body_meta_mesh="b1" body_meta_mesh_shoulders="b2"
          body_meta_mesh_upperbody="b3" face_meta_mesh="b4" hands_mesh="b5" legs_mesh="b6"
          underwear_bottom_mesh="b7" underwear_top_mesh="b8">
      <hair_meshes><hair_mesh name="h1" cover_type1="h1c1" cover_type2="h1c2" cover_type3="h1c3" cover_type4="h1c4"/>
      </hair_meshes>
      <beard_meshes><beard_mesh name="d1" cover_type4="d1c4"/></beard_meshes>
    </skin>
  </race>
</skins>"""})
        defs = self.abem.load_definitions([("M", mod)])
        race = defs.races[("r", "0")]
        self.assertEqual(race.body, {
            "body_meta_mesh": "b1", "body_meta_mesh_shoulders": "b2", "body_meta_mesh_upperbody": "b3",
            "face_meta_mesh": "b4", "hands_mesh": "b5", "legs_mesh": "b6",
            "underwear_bottom_mesh": "b7", "underwear_top_mesh": "b8"})
        self.assertEqual(race.hair, ["h1", "h1c1", "h1c2", "h1c3", "h1c4"])
        self.assertEqual(race.beards, ["d1", "d1c4"])

    def test_blank_skin_names_are_dropped(self):
        mod = write_module(self.root, "M", [], {"skins.xml": """<skins><race id="r">
    <skin gender="1" mesh_maturity_type="adult" body_meta_mesh="b1" underwear_top_mesh="">
      <hair_meshes><hair_mesh name=""/><hair_mesh name="h"/></hair_meshes>
      <beard_meshes><beard_mesh><style_tags><style_tag name="Cleanshaven"/></style_tags></beard_mesh></beard_meshes>
    </skin></race></skins>"""})
        defs = self.abem.load_definitions([("M", mod)])
        race = defs.races[("r", "1")]
        self.assertEqual(race.body, {"body_meta_mesh": "b1"})
        self.assertEqual(race.hair, ["h"])
        self.assertEqual(race.beards, [])
        self.assertEqual(defs.unresolved, [])


# --------------------------------------------------------------------------- #
# Step 6: item -> asset names (PreloadHelper.AddItemObject, GetMultiMeshCopyWithGenderData)
# --------------------------------------------------------------------------- #
def physics_item(name, payload=b"p" * 10):
    return ("physics", name, guid_for("physics:" + name), b"", [(guid_for("physics segment"), payload)])


def build_index(root, items, module="P", tree="AssetPackages"):
    write_pack(Path(root) / module / tree / "pack0.tpac", items)
    index = amsm.AssetIndex()
    index.add_module(module, Path(root) / module,
                     trees=amsm.PACK_TREES + (("Assets",) if tree == "Assets" else ()))
    return index


class ItemAssetNamesTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.root = Path(self._tmp.name)
        import audit_battle_equipment_memory as abem
        self.abem = abem

    def tearDown(self):
        self._tmp.cleanup()

    def _defs(self, *item_xml):
        mod = write_module(self.root, "Native", [("Items", "i", None), ("CraftingPieces", "pc", None)], {
            "i.xml": items_xml(*item_xml),
            "pc.xml": "<CraftingPieces>"
                      '<CraftingPiece id="p_blade" piece_type="Blade" mesh="p_blade_mesh">'
                      '<BladeData body_name="bo_p_blade" holster_mesh="p_scabbard" holster_body_name="bo_p_scabbard"/>'
                      "</CraftingPiece>"
                      '<CraftingPiece id="x_blade" piece_type="Blade" mesh="x_blade_mesh">'
                      '<BladeData body_name="bo_x"/></CraftingPiece>'
                      '<CraftingPiece id="p_handle" piece_type="Handle" mesh="p_handle_mesh"/>'
                      "</CraftingPieces>"})
        return self.abem.load_definitions([("Native", mod)])

    def _names(self, item_xml, item_id):
        defs = self._defs(item_xml)
        return self.abem.item_asset_names(defs.items[item_id], defs)

    def test_armour_lists(self):
        n = self._names('<Item id="a" mesh="m" holster_mesh="hm" body_name="bo" shield_body_name="sbo" '
                        'holster_body_name="hbo" Type="BodyArmor"><ItemComponent><Armor reins_mesh="r"/>'
                        "</ItemComponent></Item>", "a")
        self.assertEqual(n.mesh_candidates, [
            (("m",), True), (("hm",), True),
            (("m_male", "m"), False), (("m_male", "m_slim", "m"), False),
            (("m_female", "m_converted", "m"), False), (("m_female", "m_converted_slim", "m"), False),
            (("r",), True), (("r_rope",), False)])
        self.assertEqual(n.bodies, ["sbo", "bo", "hbo"])
        self.assertEqual((n.materials, n.misses), ([], []))

    def test_no_gender_variations_reuses_the_male_lists(self):
        n = self._names('<Item id="a" mesh="m" Type="BodyArmor"><ItemComponent>'
                        '<Armor has_gender_variations="false"/></ItemComponent></Item>', "a")
        self.assertEqual(n.mesh_candidates, [
            (("m",), True),
            (("m_male", "m"), False), (("m_male", "m_slim", "m"), False),
            (("m_male", "m"), False), (("m_male", "m_slim", "m"), False)])
        self.assertEqual(n.bodies, [])

    def test_weapon_item_lists(self):
        n = self._names('<Item id="w" mesh="m" holster_mesh="hm" holster_mesh_with_weapon="hmw" flying_mesh="fm" '
                        'body_name="bo" Type="Arrows"><ItemComponent><Weapon weapon_class="Arrow"/>'
                        "</ItemComponent></Item>", "w")
        self.assertEqual(n.mesh_candidates, [
            (("m",), True), (("hm",), True), (("hmw",), True), (("fm",), True), (("m_male", "m"), False)])
        self.assertEqual(n.bodies, ["bo"])

    def test_banner_item_lists_follow_the_weapon_branch(self):
        # PreloadHelper.AddItemObject takes the holster-with-weapon and flying meshes of any item with a
        # WeaponComponent (PreloadHelper.cs:122-135), and a BannerComponent is one
        n = self._names('<Item id="b" mesh="m" holster_mesh="hm" holster_mesh_with_weapon="hmw" flying_mesh="fm" '
                        'Type="Banner"><ItemComponent><Banner banner_level="1" weapon_class="Banner" '
                        'effect="IncreasedMeleeDamage"/></ItemComponent></Item>', "b")
        self.assertEqual(n.mesh_candidates, [
            (("m",), True), (("hm",), True), (("hmw",), True), (("fm",), True), (("m_male", "m"), False)])

    def test_crafted_item_lists(self):
        n = self._names('<CraftedItem id="c" crafting_template="OneHandedSword"><Pieces>'
                        '<Piece id="p_blade" Type="Blade"/><Piece id="p_handle" Type="Handle"/>'
                        "</Pieces></CraftedItem>", "c")
        self.assertEqual(n.mesh_candidates, [
            (("p_blade_mesh",), True), (("p_handle_mesh",), True), (("p_scabbard",), True)])
        self.assertEqual(n.bodies, ["bo_p_blade", "bo_p_scabbard"])

    def test_crafted_blade_without_holster_body_uses_its_body_twice(self):
        n = self._names('<CraftedItem id="c" crafting_template="OneHandedSword"><Pieces>'
                        '<Piece id="x_blade" Type="Blade"/></Pieces></CraftedItem>', "c")
        self.assertEqual(n.mesh_candidates, [(("x_blade_mesh",), True)])
        self.assertEqual(n.bodies, ["bo_x", "bo_x"])

    def test_horse_lists(self):
        n = self._names('<Item id="h" mesh="m" body_name="bo" Type="Horse"><ItemComponent><Horse>'
                        '<Materials><Material name="mat_a"/></Materials>'
                        '<AdditionalMeshes><Mesh name="mane"/><Mesh name="tail"/></AdditionalMeshes>'
                        "</Horse></ItemComponent></Item>", "h")
        self.assertEqual(n.mesh_candidates, [
            (("m",), True), (("mane",), True), (("tail",), True), (("m_male", "m"), False)])
        self.assertEqual(n.materials, ["mat_a"])
        self.assertEqual(n.bodies, ["bo"])

    def test_other_item_lists(self):
        n = self._names('<Item id="g" mesh="m" holster_mesh="hm" Type="Goods"/>', "g")
        self.assertEqual(n.mesh_candidates, [(("m",), True), (("hm",), True), (("m_male", "m"), False)])

    def test_crafted_piece_missing_is_reported(self):
        n = self._names('<CraftedItem id="c" crafting_template="Mace"><Pieces>'
                        '<Piece id="gone" Type="Blade"/><Piece id="p_handle" Type="Handle"/>'
                        "</Pieces></CraftedItem>", "c")
        self.assertEqual(n.misses, [("PIECE_UNRESOLVED", "gone", "c")])
        self.assertEqual(n.mesh_candidates, [(("p_handle_mesh",), True)])
        self.assertEqual(n.bodies, [])

    # ---- resolve_names ---------------------------------------------------------
    def _index(self, *items):
        return build_index(self.root / "packs", list(items))

    def test_first_found_name_in_a_list_wins(self):
        index = self._index(mesh_item("m"), mesh_item("m_male"))
        n = self._names('<Item id="g" mesh="m" Type="Goods"/>', "g")
        metas, bodies, misses = self.abem.resolve_names(n, index)
        self.assertEqual([i.name for i in metas], ["m", "m_male"])
        self.assertEqual((bodies, misses), ([], []))

    def test_required_list_found_nowhere_is_a_miss(self):
        index = self._index(mesh_item("m"))
        n = self._names('<Item id="w" mesh="m" flying_mesh="fm" Type="Arrows"><ItemComponent>'
                        "<Weapon/></ItemComponent></Item>", "w")
        _metas, _bodies, misses = self.abem.resolve_names(n, index)
        self.assertEqual(misses, [("MESH_UNRESOLVED", "fm", "w")])

    def test_derived_list_found_nowhere_is_not_a_miss(self):
        index = self._index(mesh_item("r"))
        n = self._names('<Item id="a" mesh="" Type="BodyArmor"><ItemComponent><Armor reins_mesh="r"/>'
                        "</ItemComponent></Item>", "a")
        metas, _bodies, misses = self.abem.resolve_names(n, index)
        self.assertEqual([i.name for i in metas], ["r"])
        self.assertEqual(misses, [])

    def test_body_found_nowhere_is_a_miss(self):
        index = self._index(mesh_item("m"), physics_item("bo"))
        n = self._names('<Item id="s" mesh="m" body_name="bo" shield_body_name="sbo" Type="Shield">'
                        "<ItemComponent><Weapon/></ItemComponent></Item>", "s")
        _metas, bodies, misses = self.abem.resolve_names(n, index)
        self.assertEqual([b.name for b in bodies], ["bo"])
        self.assertEqual(misses, [("BODY_UNRESOLVED", "sbo", "s")])

    def test_resolve_names_returns_each_asset_once(self):
        index = self._index(mesh_item("x_blade_mesh"), physics_item("bo_x"), mesh_item("m"), mesh_item("m_slim"))
        crafted = self._names('<CraftedItem id="c" crafting_template="OneHandedSword"><Pieces>'
                              '<Piece id="x_blade" Type="Blade"/></Pieces></CraftedItem>', "c")
        metas, bodies, misses = self.abem.resolve_names(crafted, index)
        self.assertEqual([b.name for b in bodies], ["bo_x"])
        self.assertEqual([m.name for m in metas], ["x_blade_mesh"])
        armour = self._names('<Item id="a" mesh="m" Type="BodyArmor"><ItemComponent>'
                             '<Armor has_gender_variations="false"/></ItemComponent></Item>', "a")
        metas, bodies, misses = self.abem.resolve_names(armour, index)
        self.assertEqual([m.name for m in metas], ["m", "m_slim"])
        self.assertEqual((bodies, misses), ([], []))


# --------------------------------------------------------------------------- #
# Step 7: sizes, union, scopes and flags
# --------------------------------------------------------------------------- #
SHADER = guid_for("shader")


def texture_item(name, width=64, height=64, mips=7, fmt="DXT1", pixels=True):
    meta = encode_texture_meta(f"{name}.png", width, height, mips, fmt)
    chain = amsm.texture_chain_bytes(width, height, mips, fmt)
    segs = [(amsm.SEG_TEXTURE_PIXELS, bytes(chain))] if pixels else [(amsm.SEG_TEXTURE_STUB, bytes(16))]
    return ("texture", name, guid_for("texture:" + name), meta, segs)


def material_item(name, textures):
    return ("material", name, guid_for("material:" + name),
            encode_material_meta(SHADER, [(0, guid_for("texture:" + t)) for t in textures]), [])


def mat_guid(name):
    return guid_for("material:" + name)


SIZING_PACK = [
    texture_item("tex_shared"),                                   # 64x64 DXT1, 7 mips
    texture_item("tex_a", 128, 128, 8),
    texture_item("tex_big", 4096, 4096, 13, pixels=False),       # header only: no 11 MB fixture
    texture_item("tex_stub", 256, 256, 9, pixels=False),
    material_item("mat_a", ["tex_shared", "tex_a"]),
    material_item("mat_b", ["tex_shared"]),
    material_item("mat_big", ["tex_big"]),
    material_item("mat_horse", ["tex_stub"]),
    mesh_item("mesh_a", mat_guid("mat_a"), render=b"r" * 100, edit=b"e" * 50, other=b"o" * 7),
    mesh_item("mesh_b", mat_guid("mat_b"), render=b"r" * 40, edit=b"e" * 20),
    mesh_item("helm", mat_guid("mat_big"), render=b"r" * 10),
    mesh_item("horse_m", render=b"r" * 5),
    mesh_item("body_m", render=b"r" * 3),
    mesh_item("head_m", render=b"r" * 3),
    mesh_item("hair_a", render=b"r" * 3),
    mesh_item("hair_a_c1", render=b"r" * 3),
    mesh_item("beard_a", render=b"r" * 3),
    physics_item("bo_a", b"p" * 30),
]

SIZING_ITEMS = items_xml(
    '<Item id="item_a" mesh="mesh_a" body_name="bo_a" Type="BodyArmor"><ItemComponent><Armor/></ItemComponent></Item>',
    '<Item id="item_b" mesh="mesh_b" Type="BodyArmor"><ItemComponent><Armor/></ItemComponent></Item>',
    '<Item id="helmet" mesh="helm" Type="HeadArmor"><ItemComponent><Armor/></ItemComponent></Item>',
    '<Item id="horse" mesh="horse_m" Type="Horse"><ItemComponent><Horse>'
    '<Materials><Material name="mat_horse"/><Material name="mat_gone"/></Materials></Horse></ItemComponent></Item>',
    '<Item id="nomesh" mesh="nm" Type="BodyArmor"/>',
    '<Item id="nobody" mesh="mesh_b" body_name="bo_gone" Type="LegArmor"/>',
    '<Item id="greaves" mesh="mesh_b" Type="LegArmor"><ItemComponent><Armor/></ItemComponent></Item>')


def troop(cid, items, culture="gondor", race=None, extra=""):
    slots = "".join(f'<equipment slot="{s}" id="Item.{i}"/>' for s, i in items)
    race_attr = f' race="{race}"' if race else ""
    return (f'<NPCCharacter id="{cid}" culture="Culture.{culture}"{race_attr}{extra}>'
            f"<Equipments><EquipmentRoster>{slots}</EquipmentRoster></Equipments></NPCCharacter>")


class SizingAndUnionTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.root = Path(self._tmp.name)
        import audit_battle_equipment_memory as abem
        self.abem = abem
        self.index = build_index(self.root / "packs", SIZING_PACK)

    def tearDown(self):
        self._tmp.cleanup()

    def _defs(self, *troops):
        native = write_module(self.root / "mods", "Native", [("Items", "i", None)],
                              {"i.xml": SIZING_ITEMS, "skins.xml": HUMAN_SKINS})
        taom = write_module(self.root / "mods", "TAOM", [("NPCCharacters", "t", None)],
                            {"t.xml": chars_xml(*troops)})
        return self.abem.load_definitions([("Native", native), ("TAOM", taom)])

    def _bytes(self, name):
        return amsm._texture_row(self.index.get("texture", name), self.index, "")["resident_bytes"]

    def test_mesh_sizes_split_the_segments(self):
        s = self.abem.mesh_sizes(self.index.get("metamesh", "mesh_a"), self.index)
        self.assertEqual((s["render_bytes"], s["edit_bytes"], s["other_bytes"]), (100, 50, 7))
        self.assertEqual(s["lod0_faces"], 10)
        self.assertEqual(s["materials"], {mat_guid("mat_a")})

    def test_lod0_faces_sum_only_lod0_records(self):
        recs = [("m.0", bytes(16), 8, 10, 12), ("m.1", bytes(16), 8, 20, 12), ("m.lod1.0", bytes(16), 8, 5, 6),
                ("m.lod0.1", bytes(16), 8, 3, 6), ("m.skirt.lod2", bytes(16), 8, 1, 3)]
        index = build_index(self.root / "lod", [mesh_item("m", render=b"r", records=recs)])
        self.assertEqual(self.abem.mesh_sizes(index.get("metamesh", "m"), index)["lod0_faces"], 33)

    def test_lod0_faces_unknown_when_a_lod0_record_does_not_decode(self):
        recs = [("m.0", bytes(16), 0, 0, 0), ("m.lod1", bytes(16), 8, 5, 6)]
        index = build_index(self.root / "lod", [mesh_item("m", render=b"r", records=recs)])
        self.assertIsNone(self.abem.mesh_sizes(index.get("metamesh", "m"), index)["lod0_faces"])
        assets = self.abem.mesh_assets(index.get("metamesh", "m"), index)
        self.assertIn(("LOD0_FACES_UNKNOWN", "m", "P/AssetPackages/pack0.tpac"), assets.misses)

    def test_material_textures_and_texture_bytes(self):
        defs = self._defs()
        a = self.abem.item_assets("item_a", defs, self.index)
        self.assertEqual(sorted(k for k in a if k[0] == "texture"), [("texture", "tex_a"), ("texture", "tex_shared")])
        self.assertEqual(a[("texture", "tex_a")]["texture_bytes"],
                         amsm.texture_chain_bytes(128, 128, 8, "DXT1"))
        self.assertEqual(a[("material", "mat_a")]["texture_bytes"], 0)

    def test_stub_only_texture_takes_the_formula(self):
        defs = self._defs()
        h = self.abem.item_assets("horse", defs, self.index)
        stub = h[("texture", "tex_stub")]
        self.assertEqual(stub["texture_bytes"], amsm.texture_chain_bytes(256, 256, 9, "DXT1"))
        self.assertIn("PIXELS_NOT_IN_PACKS", stub["flags"])
        self.assertIn(("MATERIAL_UNRESOLVED", "mat_gone", "horse"), h.misses)

    def test_material_undecoded_falls_back_to_a_guid_scan(self):
        bad = ("material", "mat_bad", guid_for("material:mat_bad"),
               b"\xff" * 30 + guid_for("texture:t1") + b"\xff" * 30, [])
        index = build_index(self.root / "bad", [texture_item("t1"), bad,
                                               mesh_item("m", guid_for("material:mat_bad"), render=b"r")])
        assets = self.abem.mesh_assets(index.get("metamesh", "m"), index)
        self.assertIn(("texture", "t1"), assets)
        self.assertIn(("MATERIAL_UNDECODED", "mat_bad", "P/AssetPackages/pack0.tpac"), assets.misses)

    def test_unindexed_texture_guid_is_reported(self):
        index = build_index(self.root / "tx", [material_item("mat_lost", ["nowhere"]),
                                              mesh_item("m", mat_guid("mat_lost"), render=b"r")])
        assets = self.abem.mesh_assets(index.get("metamesh", "m"), index)
        self.assertIn(("TEXTURE_UNRESOLVED", guid_for("texture:nowhere").hex(), "mat_lost"), assets.misses)

    def test_physics_bytes(self):
        defs = self._defs()
        a = self.abem.item_assets("item_a", defs, self.index)
        self.assertEqual(a[("physics", "bo_a")]["physics_bytes"], 30)

    def test_asset_floor_and_upper_bytes(self):
        defs = self._defs()
        a = self.abem.item_assets("item_a", defs, self.index)
        mesh = a[("metamesh", "mesh_a")]
        self.assertEqual(self.abem.asset_floor(mesh), 100)
        self.assertEqual(self.abem.asset_upper(mesh), 157)
        tex = self._bytes("tex_shared") + self._bytes("tex_a")
        self.assertEqual(a.floor_bytes, 100 + tex + 30)
        self.assertEqual(a.upper_bytes, 157 + tex + 30)

    def test_shared_texture_counts_once(self):
        defs = self._defs()
        a = self.abem.item_assets("item_a", defs, self.index)
        b = self.abem.item_assets("item_b", defs, self.index)
        shared = self._bytes("tex_shared")
        self.assertEqual(b.floor_bytes, 40 + shared)
        u = self.abem.union(a, b)
        self.assertEqual(u.floor_bytes, a.floor_bytes + b.floor_bytes - shared)
        self.assertEqual(u.total("texture_bytes"), shared + self._bytes("tex_a"))

    def test_loose_mesh_without_render_buffers_is_flagged(self):
        index = build_index(self.root / "loose", [mesh_item("lm", edit=b"e" * 9)], tree="Assets")
        assets = self.abem.mesh_assets(index.get("metamesh", "lm"), index)
        value = assets[("metamesh", "lm")]
        self.assertEqual((value["render_bytes"], value["edit_bytes"]), (0, 9))
        self.assertIn("RENDER_BUFFERS_NOT_IN_TREE", value["flags"])
        self.assertIn(("RENDER_BUFFERS_NOT_IN_TREE", "lm", "P/Assets/pack0.tpac"), assets.misses)

    def test_troop_assets_include_race_meshes(self):
        defs = self._defs(troop("t1", [("Body", "item_b")]))
        t = self.abem.troop_assets(defs.characters["t1"], defs, self.index)
        for name in ("mesh_b", "body_m", "head_m", "hair_a", "hair_a_c1", "beard_a"):
            self.assertIn(("metamesh", name), t)
        r = self.abem.race_assets(defs.characters["t1"], defs, self.index)
        self.assertEqual(r.floor_bytes, 15)
        self.assertEqual(t.misses, [])

    def test_unknown_race_is_reported(self):
        defs = self._defs(troop("t1", [("Body", "item_b")], race="ent"))
        t = self.abem.troop_assets(defs.characters["t1"], defs, self.index)
        self.assertEqual(t.misses, [("RACE_UNRESOLVED", "ent/0", "t1")])
        self.assertIn(("metamesh", "mesh_b"), t)

    def test_culture_union_counts_a_shared_item_once(self):
        defs = self._defs(troop("t1", [("Body", "item_a")]), troop("t2", [("Body", "item_a"), ("Leg", "greaves")]),
                          troop("t3", [("Head", "helmet")], culture="mordor"))
        troops = [defs.characters[c] for c in ("t1", "t2", "t3")]
        g = self.abem.culture_assets("gondor", troops, defs, self.index)
        t2 = self.abem.troop_assets(defs.characters["t2"], defs, self.index)
        self.assertEqual(g.floor_bytes, t2.floor_bytes)
        self.assertNotIn(("metamesh", "helm"), g)

    def test_shared_floor_bytes_of_two_sides(self):
        defs = self._defs(troop("t1", [("Body", "item_a")]), troop("t2", [("Body", "item_b")]))
        s1 = self.abem.side_assets([defs.characters["t1"]], defs, self.index)
        s2 = self.abem.side_assets([defs.characters["t2"]], defs, self.index)
        battle = self.abem.battle_assets([s1, s2])
        race = self.abem.race_assets(defs.characters["t1"], defs, self.index).floor_bytes
        shared = self._bytes("tex_shared") + race
        self.assertEqual(self.abem.shared_floor_bytes(s1, [s2]), shared)
        self.assertEqual(self.abem.shared_floor_bytes(s2, [s1]), shared)
        self.assertEqual(battle.floor_bytes, s1.floor_bytes + s2.floor_bytes - shared)

    def test_population_takes_taom_soldiers_with_a_battle_set(self):
        defs = self._defs(troop("t1", [("Body", "item_a")]), troop("lord", [("Body", "item_a")], extra=' is_hero="true"'),
                          troop("tmpl", [("Body", "item_a")], extra=' is_template="true"'),
                          '<NPCCharacter id="naked" culture="Culture.gondor"/>')
        self.assertEqual([c.id for c in self.abem.population(defs)], ["t1"])
        self.assertEqual([c.id for c in self.abem.population(defs, include_heroes=True)], ["t1", "lord"])

    def test_flags(self):
        defs = self._defs()
        helm = self.abem.item_assets("helmet", defs, self.index)
        big = helm[("texture", "tex_big")]
        self.assertIn("TEXTURE_4K_ON_SMALL_ITEM", self.abem.asset_flags(big, {"HeadArmor"}))
        self.assertNotIn("TEXTURE_4K_ON_SMALL_ITEM", self.abem.asset_flags(big, {"BodyArmor"}))
        self.assertNotIn("TEXTURE_4K_ON_SMALL_ITEM", self.abem.asset_flags(big, {"HeadArmor"}, big_texture=8192))
        mesh = helm[("metamesh", "helm")]
        self.assertEqual(self.abem.asset_flags(mesh, set()), [])
        self.assertEqual(self.abem.asset_flags(mesh, set(), face_budget=5), ["LOD0_OVER_FACE_BUDGET"])
        unknown = dict(mesh, lod0_faces=None)
        self.assertEqual(self.abem.asset_flags(unknown, set()), ["LOD0_FACES_UNKNOWN"])

    def test_every_miss_is_kept(self):
        defs = self._defs(troop("t1", [("Item0", "ghost"), ("Body", "nomesh"), ("Leg", "nobody")]))
        t = self.abem.troop_assets(defs.characters["t1"], defs, self.index)
        self.assertEqual(sorted(t.misses), [("BODY_UNRESOLVED", "bo_gone", "nobody"),
                                            ("ITEM_UNRESOLVED", "ghost", "t1"),
                                            ("MESH_UNRESOLVED", "nm", "nomesh")])
        self.assertIn(("metamesh", "mesh_b"), t)
        self.assertGreater(t.floor_bytes, 40)

    def test_a_refused_item_adds_no_assets_and_is_reported(self):
        defs = self._defs(troop("t1", [("Cape", "item_a"), ("Leg", "greaves")]))
        t = self.abem.troop_assets(defs.characters["t1"], defs, self.index)
        self.assertNotIn(("metamesh", "mesh_a"), t)
        self.assertNotIn(("physics", "bo_a"), t)
        self.assertIn(("metamesh", "mesh_b"), t)
        self.assertEqual(t.misses, [("ITEM_SLOT_REJECTED", "item_a", "t1:Cape")])
        self.assertEqual(self.abem._defined_items(defs.characters["t1"], defs, False), ["greaves"])


# --------------------------------------------------------------------------- #
# Step 8: run log, outputs and CLI
# --------------------------------------------------------------------------- #
# Fixture sizes, worked by hand: tx is 64x64 DXT1 with 7 mips, 2048+512+128+32+8+8+8 = 2744 bytes.
# t1 (gondor, item_a): ma render 100 edit 50, tx 2744, bo_a 30, race 5 meshes x 3 = 15
#     -> floor 2889, upper 2939.  t2 (mordor, item_b): mb render 40 edit 20, race 15 -> floor 55, upper 75.
# Both: render 155, edit 70, texture 2744, physics 30 -> floor 2929, upper 2999; shared = race 15.
FIXTURE_PACK = [
    texture_item("tx"),
    material_item("mt", ["tx"]),
    mesh_item("ma", mat_guid("mt"), render=b"r" * 100, edit=b"e" * 50),
    mesh_item("mb", render=b"r" * 40, edit=b"e" * 20),
    mesh_item("body_m", render=b"r" * 3),
    mesh_item("head_m", render=b"r" * 3),
    mesh_item("hair_a", render=b"r" * 3),
    mesh_item("hair_a_c1", render=b"r" * 3),
    mesh_item("beard_a", render=b"r" * 3),
    physics_item("bo_a", b"p" * 30),
]


def make_battle_fixture(root):
    """A game install holding Native (items, skins, one pack) and a release folder holding TAOM
    (two troops, two registrations whose files are missing, no packs)."""
    root = Path(root)
    game = root / "game"
    release = root / "release"
    native = write_module(game / "Modules", "Native", [("Items", "items", None)], {
        "items.xml": items_xml(
            '<Item id="item_a" mesh="ma" body_name="bo_a" Type="BodyArmor"><ItemComponent><Armor/></ItemComponent></Item>',
            '<Item id="item_b" mesh="mb" Type="BodyArmor"/>'),
        "skins.xml": HUMAN_SKINS})
    write_pack(native / "AssetPackages" / "pack0.tpac", FIXTURE_PACK)
    write_module(release, "TAOM", [("NPCCharacters", "troops", None), ("NPCCharacters", "gone", None),
                                   ("NPCCharacters", "gone2", None)], {
        "troops.xml": chars_xml(troop("t1", [("Body", "item_a")]),
                                troop("t2", [("Body", "item_b")], culture="mordor"))})
    return game, release


class _MainFixture(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.root = Path(self._tmp.name)
        import audit_battle_equipment_memory as abem
        self.abem = abem
        self.game, self.release = make_battle_fixture(self.root)
        self.out = self.root / "out"

    def tearDown(self):
        self._tmp.cleanup()

    def run_main(self, *extra, out=None):
        argv = ["--release-modules", str(self.release), "--game-dir", str(self.game),
                "--xml-modules", "Native,TAOM", "--pack-modules", "TAOM,Native",
                "--out-dir", str(out or self.out)] + list(extra)
        buf = io.StringIO()
        with contextlib.redirect_stdout(buf):
            rc = self.abem.main(argv)
        return rc, buf.getvalue()

    def log_lines(self, text, kind):
        import re
        lines = [ln for ln in text.splitlines() if ln.startswith(f"[EquipMemAudit] {kind} ")]
        return [re.sub(r"elapsedS=\d+\.\d", "elapsedS=X", ln) for ln in lines]

    def side_files(self):
        s1 = self.root / "side1.txt"
        s1.write_text("# gondor side\nt1\n", encoding="utf-8")
        s2 = self.root / "side2.txt"
        s2.write_text("t2  # mordor side\n\n", encoding="utf-8")
        return s1, s2


class RunLogTests(_MainFixture):
    def test_config_line(self):
        rc, text = self.run_main()
        self.assertEqual(rc, 0)
        self.assertEqual(self.log_lines(text, "config"), [
            f"[EquipMemAudit] config releaseModules={self.release} gameModules={self.game / 'Modules'} "
            "xmlModules=Native,TAOM packModules=TAOM,Native trees=AssetPackages+EmAssetPackages sides=0 "
            "civilian=off heroes=off faceBudget=20000 bigTexture=4096"])
        _rc, text = self.run_main("--loose-assets", "--include-civilian", "--include-heroes",
                                  "--face-budget", "7", "--big-texture", "2048")
        self.assertIn("trees=AssetPackages+EmAssetPackages+Assets sides=0 civilian=on heroes=on "
                      "faceBudget=7 bigTexture=2048", self.log_lines(text, "config")[0])

    def test_module_lines(self):
        _rc, text = self.run_main()
        self.assertEqual(self.log_lines(text, "module"), [
            "[EquipMemAudit] module name=Native root=game registrations=1 files=1 items=2 craftingPieces=0 "
            "rosters=0 characters=0 races=1",
            "[EquipMemAudit] module name=TAOM root=release registrations=3 files=1 items=0 craftingPieces=0 "
            "rosters=0 characters=2 races=0"])

    def test_missing_module_line(self):
        _rc, text = self.run_main("--xml-modules", "Native,TAOM,Ghost")
        self.assertEqual(self.log_lines(text, "module")[-1],
                         "[EquipMemAudit] module name=Ghost root=missing registrations=0 files=0 items=0 "
                         "craftingPieces=0 rosters=0 characters=0 races=0")

    def test_index_lines(self):
        _rc, text = self.run_main()
        self.assertEqual(self.log_lines(text, "index"), [
            "[EquipMemAudit] index module=TAOM packs=0 metameshes=0 materials=0 textures=0 physics=0 errors=1",
            "[EquipMemAudit] index module=Native packs=1 metameshes=7 materials=1 textures=1 physics=1 errors=0"])

    def test_fallback_lines_one_per_code_with_count_and_first(self):
        _rc, text = self.run_main()
        self.assertEqual(self.log_lines(text, "fallback"), [
            "[EquipMemAudit] fallback reason=PACK_ERROR count=1 first=TAOM "
            f"consequence={self.abem.REASONS['PACK_ERROR']}",
            "[EquipMemAudit] fallback reason=REGISTERED_FILE_MISSING count=2 first=TAOM/gone "
            f"consequence={self.abem.REASONS['REGISTERED_FILE_MISSING']}"])

    def test_consequence_texts(self):
        self.assertEqual(self.abem.REASONS["PACK_ERROR"],
                         "that pack or module tree is not indexed; assets only it holds are unresolved")
        self.assertEqual(self.abem.REASONS["REGISTERED_FILE_MISSING"],
                         "the registration loads nothing; its definitions are absent")

    def test_side_and_battle_lines(self):
        s1, s2 = self.side_files()
        rc, text = self.run_main("--troops", str(s1), "--troops", str(s2))
        self.assertEqual(rc, 0)
        self.assertEqual(self.log_lines(text, "side"), [
            "[EquipMemAudit] side name=side1 troops=1 items=1 floorBytes=2889 upperBytes=2939 sharedFloorBytes=15",
            "[EquipMemAudit] side name=side2 troops=1 items=1 floorBytes=55 upperBytes=75 sharedFloorBytes=15"])
        self.assertEqual(self.log_lines(text, "battle"), [
            "[EquipMemAudit] battle sides=2 troops=2 floorBytes=2929 upperBytes=2999 sumOfSidesFloorBytes=2944"])
        self.assertIn(" sides=2 ", self.log_lines(text, "config")[0])

    def test_no_side_or_battle_line_without_sides(self):
        _rc, text = self.run_main()
        self.assertEqual((self.log_lines(text, "side"), self.log_lines(text, "battle")), ([], []))

    def test_summary_line_is_last(self):
        _rc, text = self.run_main()
        summary = ("[EquipMemAudit] summary troops=2 items=2 metameshes=7 textures=1 physics=1 renderBytes=155 "
                   "editBytes=70 otherMeshBytes=0 textureBytes=2744 physicsBytes=30 floorBytes=2929 upperBytes=2999 "
                   "maxTroop=t1:2889 maxAsset=texture:tx:2744 unresolved=3 elapsedS=X")
        self.assertEqual(self.log_lines(text, "summary"), [summary])
        log_lines = [ln for ln in text.splitlines() if ln.startswith("[EquipMemAudit] ")]
        self.assertTrue(log_lines[-1].startswith("[EquipMemAudit] summary "))

    def test_run_log_file_and_report_repeat_the_log(self):
        _rc, text = self.run_main()
        stdout_log = [ln for ln in text.splitlines() if ln.startswith("[EquipMemAudit] ")]
        file_log = (self.out / "run.log").read_text(encoding="utf-8").splitlines()
        self.assertEqual(file_log, stdout_log)
        report = (self.out / "battle-equipment-memory.md").read_text(encoding="utf-8")
        self.assertIn("## Run log", report)
        for line in stdout_log:
            self.assertIn(line, report)

    def test_every_code_has_a_consequence(self):
        import re
        source = (REPO / "tools" / "audit_battle_equipment_memory.py").read_text(encoding="utf-8")
        emitted = set(re.findall(r'\(\(?"([A-Z][A-Z0-9_]+)",', source))
        self.assertEqual(emitted, set(self.abem.REASONS))
        self.assertEqual(set(self.abem.REASONS), {
            "MODULE_MISSING", "REGISTERED_FILE_MISSING", "XML_PARSE_ERROR", "XSLT_NOT_APPLIED",
            "DUPLICATE_DEFINITION", "CRAFTING_TEMPLATE_UNKNOWN", "ROSTER_UNRESOLVED", "ITEM_UNRESOLVED",
            "PIECE_UNRESOLVED", "MESH_UNRESOLVED", "BODY_UNRESOLVED", "MATERIAL_UNRESOLVED",
            "MATERIAL_UNDECODED", "TEXTURE_UNRESOLVED", "RACE_UNRESOLVED", "RACE_MESH_UNRESOLVED",
            "RENDER_BUFFERS_NOT_IN_TREE", "LOD0_FACES_UNKNOWN", "PACK_ERROR", "SUBMODULE_MISSING",
            "TEXTURE_SIZE_FROM_HEADER", "TEXTURE_SIZE_UNKNOWN", "MATERIAL_GUID_UNRESOLVED",
            "ITEM_SLOT_REJECTED", "COOKED_TREE_NOT_READ"})


class CliTests(_MainFixture):
    def test_outputs_are_written(self):
        rc, _text = self.run_main()
        self.assertEqual(rc, 0)
        for name in ("battle-equipment-memory.md", "assets.tsv", "items.tsv", "troops.tsv", "cultures.tsv",
                     "unresolved.tsv", "run.log"):
            self.assertTrue((self.out / name).is_file(), name)
        self.assertFalse((self.out / "sides.tsv").exists())
        head = lambda n: (self.out / n).read_text(encoding="utf-8").splitlines()[0].split("\t")  # noqa: E731
        self.assertEqual(head("assets.tsv"), ["kind", "name", "module", "pack", "floor_bytes", "edit_bytes",
                                              "other_bytes", "format", "width", "height", "mips", "lod0_faces",
                                              "flags", "items", "troops", "cultures", "sample_items",
                                              "sample_troops"])
        self.assertEqual(head("items.tsv"), ["item_id", "module", "kind", "type", "meshes", "bodies",
                                             "floor_bytes", "upper_bytes", "troops", "unresolved"])
        self.assertEqual(head("troops.tsv"), ["troop_id", "module", "culture", "race", "is_hero", "battle_sets",
                                              "items", "floor_bytes", "upper_bytes", "race_floor_bytes",
                                              "unresolved"])
        self.assertEqual(head("cultures.tsv"), ["culture", "troops", "items", "metameshes", "textures", "physics",
                                                "render_bytes", "edit_bytes", "texture_bytes", "physics_bytes",
                                                "floor_bytes", "upper_bytes"])
        self.assertEqual(head("unresolved.tsv"), ["reason", "subject", "referenced_by"])
        s1, s2 = self.side_files()
        self.run_main("--troops", str(s1), "--troops", str(s2))
        self.assertEqual(head("sides.tsv"), ["side", "troops", "floor_bytes", "upper_bytes", "shared_floor_bytes"])

    def test_unknown_troop_id_exits_2(self):
        bad = self.root / "bad.txt"
        bad.write_text("t1\nnobody_defines_me\n", encoding="utf-8")
        rc, text = self.run_main("--troops", str(bad))
        self.assertEqual(rc, 2)
        self.assertIn("nobody_defines_me", text)

    def test_empty_side_exits_2(self):
        rc, text = self.run_main("--culture", "gondor", "--culture", "rohan")
        self.assertEqual(rc, 2)
        self.assertIn("rohan", text)
        empty = self.root / "empty.txt"
        empty.write_text("# nothing\n", encoding="utf-8")
        rc, _text = self.run_main("--troops", str(empty))
        self.assertEqual(rc, 2)

    def test_missing_release_or_game_modules_exits_2(self):
        for release, game in ((self.root / "nowhere", self.game), (self.release, self.root / "nogame")):
            buf = io.StringIO()
            with contextlib.redirect_stdout(buf):
                rc = self.abem.main(["--release-modules", str(release), "--game-dir", str(game),
                                     "--out-dir", str(self.out)])
            self.assertEqual(rc, 2)
            self.assertIn("not found", buf.getvalue())
        self.assertFalse(self.out.exists())

    def test_troops_and_culture_are_exclusive(self):
        s1, _s2 = self.side_files()
        with self.assertRaises(SystemExit) as cm, contextlib.redirect_stderr(io.StringIO()):
            self.run_main("--troops", str(s1), "--culture", "gondor")
        self.assertEqual(cm.exception.code, 2)

    def test_refuses_to_write_inside_the_game_or_release(self):
        for target in (self.game / "out", self.release / "out"):
            before = sorted(p for p in self.root.rglob("*"))
            with self.assertRaises(SystemExit) as cm:
                self.run_main(out=target)
            self.assertTrue(str(cm.exception.code).startswith("refusing to write inside the game install: "),
                            cm.exception.code)
            self.assertFalse(target.exists())
            self.assertEqual(sorted(p for p in self.root.rglob("*")), before)

    def test_two_sides_share_assets_once(self):
        rc, _text = self.run_main("--culture", "gondor", "--culture", "mordor")
        self.assertEqual(rc, 0)
        rows = (self.out / "sides.tsv").read_text(encoding="utf-8").splitlines()
        self.assertEqual(rows[1:], ["gondor\t1\t2889\t2939\t15", "mordor\t1\t55\t75\t15"])
        battle = (self.out / "run.log").read_text(encoding="utf-8")
        self.assertIn("floorBytes=2929 upperBytes=2999 sumOfSidesFloorBytes=2944", battle)

    def test_main_runs_end_to_end(self):
        rc, text = self.run_main("--top", "5")
        self.assertEqual(rc, 0)
        items = (self.out / "items.tsv").read_text(encoding="utf-8").splitlines()
        self.assertEqual(items[1:], ["item_a\tNative\tItem\tBodyArmor\tma\tbo_a\t2874\t2924\t1\t0",
                                     "item_b\tNative\tItem\tBodyArmor\tmb\t\t40\t60\t1\t0"])
        troops = (self.out / "troops.tsv").read_text(encoding="utf-8").splitlines()
        self.assertEqual(troops[1:], ["t1\tTAOM\tgondor\thuman\tfalse\t1\t1\t2889\t2939\t15\t0",
                                      "t2\tTAOM\tmordor\thuman\tfalse\t1\t1\t55\t75\t15\t0"])
        cultures = (self.out / "cultures.tsv").read_text(encoding="utf-8").splitlines()
        self.assertEqual(cultures[1:], ["gondor\t1\t1\t6\t1\t1\t115\t50\t2744\t30\t2889\t2939",
                                        "mordor\t1\t1\t6\t0\t0\t55\t20\t0\t0\t55\t75"])
        assets = (self.out / "assets.tsv").read_text(encoding="utf-8").splitlines()
        self.assertTrue(assets[1].startswith("texture\ttx\tNative\tAssetPackages/pack0.tpac\t2744\t0\t0\tDXT1\t64\t64\t7\t"),
                        assets[1])
        self.assertTrue(assets[1].endswith("\t1\t1\t1\titem_a\tt1"), assets[1])
        unresolved = (self.out / "unresolved.tsv").read_text(encoding="utf-8").splitlines()
        self.assertEqual(len(unresolved), 4)
        report = (self.out / "battle-equipment-memory.md").read_text(encoding="utf-8")
        for heading in ("## Run log", "## Per-culture totals", "## Top 5 assets by floor bytes",
                        "## Top 5 meshes by edit bytes", "## Top 5 items by floor bytes",
                        "## Top 5 troops by floor bytes", "## Flags", "## Unresolved by reason", "## Approximations"):
            self.assertIn(heading, report)
        self.assertIn("UNVERIFIED", report)
        self.assertIn("summary troops=2", text)


# --------------------------------------------------------------------------- #
# review follow-ups (deep review of plan 038)
# --------------------------------------------------------------------------- #
def texture_v2_item(name, segments):
    """A texture whose header is version 2, which the map tool's decoder does not read."""
    meta = struct.pack("<I", 2) + encode_texture_meta(f"{name}.png", 64, 64, 7, "DXT1")[4:]
    return ("texture", name, guid_for("texture:" + name), meta, segments)


class ReviewTextureTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.root = Path(self._tmp.name)
        import audit_battle_equipment_memory as abem
        self.abem = abem

    def tearDown(self):
        self._tmp.cleanup()

    def test_texture_reached_by_guid_uses_the_pixel_twin(self):
        mod = self.root / "N"
        write_pack(mod / "AssetPackages" / "core.tpac",
                   [texture_item("tw", 128, 128, 8, pixels=False), material_item("mtw", ["tw"]),
                    mesh_item("mw", mat_guid("mtw"), render=b"r")])
        write_pack(mod / "EmAssetPackages" / "x" / "big.tpac", [texture_item("tw", 128, 128, 8)])
        index = amsm.AssetIndex()
        index.add_module("N", mod)
        tex = self.abem.mesh_assets(index.get("metamesh", "mw"), index)[("texture", "tw")]
        self.assertEqual(tex["pack"], "EmAssetPackages/x/big.tpac")
        self.assertIn("FULL_CHAIN_ONLY_IN_EMASSETPACKAGES", tex["flags"])
        self.assertNotIn("PIXELS_NOT_IN_PACKS", tex["flags"])

    def test_texture_sized_from_the_header_is_a_fallback(self):
        index = build_index(self.root / "h", [texture_item("th", pixels=False), material_item("mh", ["th"]),
                                             mesh_item("m", mat_guid("mh"), render=b"r")])
        assets = self.abem.mesh_assets(index.get("metamesh", "m"), index)
        self.assertIn(("TEXTURE_SIZE_FROM_HEADER", "th", "P/AssetPackages/pack0.tpac"), assets.misses)

    def test_texture_of_unknown_size_is_a_fallback(self):
        index = build_index(self.root / "u", [
            texture_v2_item("stub_only", [(amsm.SEG_TEXTURE_STUB, bytes(16))]),
            texture_v2_item("nothing", []),
            material_item("mu", ["stub_only", "nothing"]),
            mesh_item("m", mat_guid("mu"), render=b"r")])
        assets = self.abem.mesh_assets(index.get("metamesh", "m"), index)
        self.assertIn(("TEXTURE_SIZE_UNKNOWN", "stub_only", "P/AssetPackages/pack0.tpac"), assets.misses)
        self.assertIn(("TEXTURE_SIZE_UNKNOWN", "nothing", "P/AssetPackages/pack0.tpac"), assets.misses)
        self.assertEqual(assets[("texture", "nothing")]["texture_bytes"], 0)

    def test_a_version_3_header_with_a_format_the_tables_lack_is_a_fallback_too(self):
        index = build_index(self.root / "f", [texture_item("odd", fmt="WEIRD", pixels=False),
                                             material_item("mo", ["odd"]),
                                             mesh_item("m", mat_guid("mo"), render=b"r")])
        assets = self.abem.mesh_assets(index.get("metamesh", "m"), index)
        self.assertIn(("TEXTURE_SIZE_UNKNOWN", "odd", "P/AssetPackages/pack0.tpac"), assets.misses)
        flags = assets[("texture", "odd")]["flags"]
        self.assertIn("UNKNOWN_FORMAT(WEIRD)", flags)
        self.assertFalse([f for f in flags if f.startswith("HEADER_UNDECODED")])

    def test_record_material_in_no_pack_is_reported(self):
        lost = guid_for("material:lost")
        index = build_index(self.root / "g", [mesh_item("m", lost, render=b"r")])
        assets = self.abem.mesh_assets(index.get("metamesh", "m"), index)
        self.assertIn(("MATERIAL_GUID_UNRESOLVED", lost.hex(), "m/m"), assets.misses)

    def test_misread_record_material_is_not_reported(self):
        recs = [("m", guid_for("material:misread"), 0, 0, 0)]
        index = build_index(self.root / "g", [mesh_item("m", render=b"r", records=recs)])
        assets = self.abem.mesh_assets(index.get("metamesh", "m"), index)
        self.assertEqual([r for r in assets.misses if r[0] == "MATERIAL_GUID_UNRESOLVED"], [])

    def test_cloth_string_read_as_a_guid_is_not_reported(self):
        # the live shape on spear_banner_12/16/17 (Native core_game.tpac): the "guid" is the sized
        # string "uses_cloth_s..." and the misread counts happen to look plausible
        misread = struct.pack("<I", 21) + b"uses_cloth_s"
        real = mat_guid("mreal")
        recs = [("m", misread, 4_161_536, 4_161_536, 4_161_536), ("m.lod1", real, 2029, 2068, 2119)]
        index = build_index(self.root / "c", [material_item("mreal", []),
                                             mesh_item("m", real, render=b"r", records=recs)])
        assets = self.abem.mesh_assets(index.get("metamesh", "m"), index)
        self.assertEqual([r for r in assets.misses if r[0] == "MATERIAL_GUID_UNRESOLVED"], [])
        self.assertIn(("material", "mreal"), assets)


class ReviewDefinitionTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.root = Path(self._tmp.name)
        import audit_battle_equipment_memory as abem
        self.abem = abem

    def tearDown(self):
        self._tmp.cleanup()

    def _crafted(self, template):
        mod = write_module(self.root, "Native", [("Items", "i", None), ("CraftingPieces", "pc", None)], {
            "i.xml": items_xml(f'<CraftedItem id="c" crafting_template="{template}"><Pieces>'
                               '<Piece id="p_blade" Type="Blade"/></Pieces></CraftedItem>'),
            "pc.xml": "<CraftingPieces>"
                      '<CraftingPiece id="p_blade" piece_type="Blade" mesh="p_blade_mesh">'
                      '<BladeData body_name="bo_p_blade" holster_mesh="p_scabbard" holster_body_name="bo_p_scabbard"/>'
                      "</CraftingPiece></CraftingPieces>"})
        defs = self.abem.load_definitions([("Native", mod)])
        return self.abem.item_asset_names(defs.items["c"], defs)

    def test_weapon_as_holster_template_builds_no_blade_holster(self):
        for template in ("OneHandedAxe", "TwoHandedAxe", "TwoHandedPolearm", "Pike", "Mace", "TwoHandedMace"):
            n = self._crafted(template)
            self.assertEqual(n.mesh_candidates, [(("p_blade_mesh",), True)], template)
            self.assertEqual(n.bodies, ["bo_p_blade", "bo_p_scabbard"], template)
        n = self._crafted("Javelin")
        self.assertEqual(n.mesh_candidates, [(("p_blade_mesh",), True), (("p_scabbard",), True)])

    def test_equipment_set_reference_uses_the_whole_id(self):
        mod = write_module(self.root, "M", [("EquipmentRosters", "r", None), ("NPCCharacters", "c", None)], {
            "r.xml": '<EquipmentRosters><EquipmentRoster id="std">'
                     '<EquipmentSet><Equipment slot="Body" id="Item.x"/></EquipmentSet>'
                     "</EquipmentRoster></EquipmentRosters>",
            "c.xml": chars_xml('<NPCCharacter id="t"><Equipments><EquipmentSet id="EquipmentRoster.std"/>'
                               "</Equipments></NPCCharacter>")})
        defs = self.abem.load_definitions([("M", mod)])
        self.assertEqual(self.abem.battle_sets(defs.characters["t"], defs), [])
        self.assertEqual(defs.unresolved, [("ROSTER_UNRESOLVED", "EquipmentRoster.std", "t")])

    def test_population_matches_the_module_name_in_any_case(self):
        mod = write_module(self.root, "taom", [("NPCCharacters", "c", None)], {
            "c.xml": chars_xml(troop("t1", [("Body", "x")]))})
        defs = self.abem.load_definitions([("taom", mod)])
        self.assertEqual([c.id for c in self.abem.population(defs)], ["t1"])

    def test_module_without_submodule_xml_is_reported(self):
        bare = self.root / "Bare"
        (bare / "ModuleData").mkdir(parents=True)
        defs = self.abem.load_definitions([("Bare", bare)])
        self.assertEqual(defs.unresolved, [("SUBMODULE_MISSING", "Bare/SubModule.xml", "--xml-modules")])

    def test_unparsable_registered_file_is_reported(self):
        mod = write_module(self.root, "M", [("Items", "i", None)], {"i.xml": "<Items><Item id='a'"})
        defs = self.abem.load_definitions([("M", mod)])
        self.assertEqual(reasons(defs), ["XML_PARSE_ERROR"])
        self.assertEqual(defs.unresolved[0][1], "M/i.xml")

    def test_skin_mesh_in_no_pack_is_reported(self):
        mod = write_module(self.root, "M", [("NPCCharacters", "c", None)], {
            "c.xml": chars_xml(troop("t1", [])), "skins.xml": HUMAN_SKINS})
        defs = self.abem.load_definitions([("M", mod)])
        index = build_index(self.root / "packs", [mesh_item("body_m", render=b"r")])
        r = self.abem.race_assets(defs.characters["t1"], defs, index)
        self.assertIn(("RACE_MESH_UNRESOLVED", "head_m", "race:human/0"), r.misses)
        self.assertIn(("metamesh", "body_m"), r)


class ReviewCliTests(_MainFixture):
    def run_log(self):
        return (self.out / "run.log").read_text(encoding="utf-8")

    def test_no_troop_selected_exits_2_and_says_why(self):
        rc, _text = self.run_main("--xml-modules", "Native")
        self.assertEqual(rc, 2)
        self.assertIn("[EquipMemAudit] abort reason=NO_TROOPS_SELECTED ", self.run_log())

    def test_troop_file_with_a_byte_order_mark_resolves(self):
        bom = self.root / "bom.txt"
        bom.write_text("t1\n", encoding="utf-8-sig")
        rc, _text = self.run_main("--troops", str(bom))
        self.assertEqual(rc, 0)

    def test_unreadable_troop_file_exits_2_and_says_why(self):
        rc, _text = self.run_main("--troops", str(self.root / "missing.txt"))
        self.assertEqual(rc, 2)
        self.assertIn("[EquipMemAudit] abort reason=TROOP_FILE_UNREADABLE ", self.run_log())
        self.assertNotIn("index module=", self.run_log())

    def test_unknown_troop_and_empty_side_say_why_in_the_run_log(self):
        bad = self.root / "bad.txt"
        bad.write_text("t1\nnobody_defines_me\n", encoding="utf-8")
        self.assertEqual(self.run_main("--troops", str(bad))[0], 2)
        self.assertIn("[EquipMemAudit] abort reason=TROOP_UNKNOWN detail=", self.run_log())
        self.assertIn("nobody_defines_me", self.run_log())
        self.assertEqual(self.run_main("--culture", "gondor", "--culture", "rohan")[0], 2)
        self.assertIn("[EquipMemAudit] abort reason=SIDE_EMPTY detail=", self.run_log())

    def test_fallback_lines_come_before_side_and_battle_lines(self):
        s1, s2 = self.side_files()
        _rc, text = self.run_main("--troops", str(s1), "--troops", str(s2))
        kinds = [ln.split(" ")[1] for ln in text.splitlines() if ln.startswith("[EquipMemAudit] ")]
        self.assertLess(max(i for i, k in enumerate(kinds) if k == "fallback"), kinds.index("side"))
        self.assertEqual(kinds[-2:], ["battle", "summary"])

    def test_corrupt_pack_is_a_fallback_named_by_its_path(self):
        bad = self.game / "Modules" / "Native" / "AssetPackages" / "broken.tpac"
        bad.write_bytes(b"TPAC" + b"\x00" * 3)
        _rc, text = self.run_main()
        self.assertIn("[EquipMemAudit] fallback reason=PACK_ERROR count=2 first=TAOM ", text)
        unresolved = (self.out / "unresolved.tsv").read_text(encoding="utf-8")
        self.assertIn("PACK_ERROR\tNative/AssetPackages/broken.tpac\t", unresolved)

    def test_refuses_a_report_or_tsv_folder_inside_the_release(self):
        for flag in ("--report", "--tsv-dir"):
            target = self.release / "out" / "x.md"
            with self.assertRaises(SystemExit) as cm:
                self.run_main(flag, str(target))
            self.assertTrue(str(cm.exception.code).startswith("refusing to write inside the game install: "))
            self.assertFalse((self.release / "out").exists())

    def test_missing_pack_module_is_a_fallback(self):
        _rc, text = self.run_main("--pack-modules", "TAOM,Native,Ghost")
        self.assertIn("[EquipMemAudit] fallback reason=MODULE_MISSING count=1 first=Ghost ", text)

    def test_report_has_a_battle_row_and_names_its_limits(self):
        s1, s2 = self.side_files()
        self.run_main("--troops", str(s1), "--troops", str(s2))
        report = (self.out / "battle-equipment-memory.md").read_text(encoding="utf-8")
        self.assertIn("| battle (union of the sides) | 2 | 2,929 | 2,999 |", report)
        self.assertIn("The sides' own floors sum to 2,944 bytes", report)
        self.assertIn("XSLT transforms are not applied", report)
        self.assertNotIn("real cost lies between the two", report)
        self.assertIn("outside `PreloadHelper`", report)


class ReviewMapToolTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.root = Path(self._tmp.name)
        self.game = self.root / "game"
        self.scene = self.game / "Modules" / "TAOM_Map" / "SceneObj" / "Main_map"
        self.scene.mkdir(parents=True)
        (self.scene / "scene.xscene").write_text("<scene><entities/></scene>", encoding="utf-8")
        (self.scene / "references.txt").write_text("mesh loose_big\n", encoding="utf-8")
        write_pack(self.game / "Modules" / "TAOM_Map" / "Assets" / "p_geo.tpac",
                   [mesh_item("loose_big", edit=b"e" * 1_000_001)])

    def tearDown(self):
        self._tmp.cleanup()

    def test_loose_mesh_is_not_flagged_as_editor_stream_bloat(self):
        import warnings
        with warnings.catch_warnings():
            warnings.simplefilter("ignore", ResourceWarning)
            m = amsm.build_manifest(self.game / "Modules", self.scene, ["TAOM_Map"],
                                    trees=amsm.PACK_TREES + ("Assets",))
        row = next(r for r in m.mesh_rows if r["name"] == "loose_big")
        self.assertNotIn("EDITOR_STREAM_BLOAT", row["flags"])

    def test_loose_run_names_the_loose_trees(self):
        import warnings
        out = io.StringIO()
        with warnings.catch_warnings(), contextlib.redirect_stdout(out):
            warnings.simplefilter("ignore", ResourceWarning)
            amsm.main(["--game-dir", str(self.game), "--modules", "TAOM_Map",
                       "--out-dir", str(self.root / "out"), "--loose-assets"])
        self.assertIn("item records seen across the trees the engine loads (a module's loose Assets tree, else its "
                      "pack trees)", out.getvalue())
        self.assertNotIn("both pack trees", out.getvalue())

    def _stdout_of_a_run(self, *extra):
        import warnings
        out = io.StringIO()
        with warnings.catch_warnings(), contextlib.redirect_stdout(out):
            warnings.simplefilter("ignore", ResourceWarning)
            amsm.main(["--game-dir", str(self.game), "--modules", "TAOM_Map",
                       "--out-dir", str(self.root / ("out" + "".join(extra))), *extra])
        return out.getvalue()

    def test_loose_run_says_which_cooked_trees_it_did_not_read(self):
        write_pack(self.game / "Modules" / "TAOM_Map" / "AssetPackages" / "pack0.tpac",
                   [mesh_item("cooked_only", render=b"r")])
        loose = self._stdout_of_a_run("--loose-assets")
        self.assertIn("note: TAOM_Map/AssetPackages not read: the module's loose Assets tree is loaded instead, "
                      "as the engine does", loose)
        self.assertNotIn("not read", self._stdout_of_a_run())

    def _report(self, *extra):
        import warnings
        out_dir = self.root / ("out" + "".join(extra))
        with warnings.catch_warnings(), contextlib.redirect_stdout(io.StringIO()):
            warnings.simplefilter("ignore", ResourceWarning)
            amsm.main(["--game-dir", str(self.game), "--modules", "TAOM_Map",
                       "--out-dir", str(out_dir), *extra])
        return (out_dir / "map-scene-memory.md").read_text(encoding="utf-8")

    def test_loose_report_says_loose_meshes_are_never_flagged(self):
        self.assertIn("Meshes from a loose Assets tree are never flagged", self._report("--loose-assets"))
        self.assertNotIn("loose Assets tree", self._report())


# --------------------------------------------------------------------------- #
# Codex review of plan 038 (2026-10-03): slot fit, stale outputs, loose trees, unsized textures
# --------------------------------------------------------------------------- #
ALL_SLOTS = ("Weapon0", "Weapon1", "Weapon2", "Weapon3", "ExtraWeaponSlot", "Head", "Body", "Leg", "Gloves", "Cape",
             "Horse", "HorseHarness")
WEAPON_SLOTS = ("Weapon0", "Weapon1", "Weapon2", "Weapon3")
# Equipment.IsItemFitsToSlot (v1.5.3 decompile): the slots each ItemObject.ItemTypeEnum member fits
ENGINE_SLOT_FIT = {
    "Invalid": (), "Goods": (), "ChestArmor": (), "Book": (),
    "Horse": ("Horse",), "Animal": ("Horse",), "HorseHarness": ("HorseHarness",),
    "HeadArmor": ("Head",), "BodyArmor": ("Body",), "LegArmor": ("Leg",), "HandArmor": ("Gloves",),
    "Cape": ("Cape",),
}
ENGINE_SLOT_FIT.update({t: WEAPON_SLOTS for t in (
    "OneHandedWeapon", "TwoHandedWeapon", "Polearm", "Arrows", "Bolts", "SlingStones", "Shield", "Bow", "Crossbow",
    "Sling", "Thrown", "Pistol", "Musket", "Bullets", "Banner")})
# WeaponComponentData.GetItemTypeFromWeaponClass (v1.5.3 decompile, WeaponComponentData.cs:265-320): the item type of
# every WeaponClass member, in the enum's order (WeaponClass.cs, 32 members). Written out here, not derived from the
# tool's table, so a slip in that table is not mirrored.
ENGINE_WEAPON_CLASS_TYPES = {
    "Undefined": "Invalid", "Dagger": "OneHandedWeapon", "OneHandedSword": "OneHandedWeapon",
    "TwoHandedSword": "TwoHandedWeapon", "OneHandedAxe": "OneHandedWeapon", "TwoHandedAxe": "TwoHandedWeapon",
    "Mace": "OneHandedWeapon", "Pick": "TwoHandedWeapon", "TwoHandedMace": "TwoHandedWeapon",
    "OneHandedPolearm": "Polearm", "TwoHandedPolearm": "Polearm", "LowGripPolearm": "Polearm",
    "Arrow": "Arrows", "Bolt": "Bolts", "SlingStone": "SlingStones", "Cartridge": "Bullets",
    "Bow": "Bow", "Crossbow": "Crossbow", "Sling": "Sling",
    "Stone": "Thrown", "Boulder": "Thrown", "ThrowingAxe": "Thrown", "ThrowingKnife": "Thrown",
    "Javelin": "Thrown", "Pistol": "Pistol", "Musket": "Musket",
    "BallistaBoulder": "Thrown", "BallistaStone": "Thrown",
    "SmallShield": "Shield", "LargeShield": "Shield", "Banner": "Banner", "NumClasses": "Invalid",
}


class SlotFitTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.root = Path(self._tmp.name)
        import audit_battle_equipment_memory as abem
        self.abem = abem

    def tearDown(self):
        self._tmp.cleanup()

    def _defs(self, *item_xml):
        mod = write_module(self.root, "M", [("Items", "i", None)], {"i.xml": items_xml(*item_xml)})
        return self.abem.load_definitions([("M", mod)])

    def _fits(self, defs, item_id):
        return [s for s in ALL_SLOTS if self.abem.item_fits_slot(defs.items[item_id], s)]

    def test_every_item_type_fits_the_slots_the_engine_gives_it(self):
        defs = self._defs(*(f'<Item id="{t}" Type="{t}"/>' for t in ENGINE_SLOT_FIT))
        for item_type, slots in ENGINE_SLOT_FIT.items():
            with self.subTest(item_type):
                self.assertEqual(self._fits(defs, item_type), [s for s in ALL_SLOTS if s in slots])

    def test_type_is_read_ignoring_case(self):
        defs = self._defs('<Item id="lower" Type="bodyarmor"/>', '<Item id="upper" Type="BODYARMOR"/>')
        self.assertEqual(self._fits(defs, "lower"), ["Body"])
        self.assertEqual(self._fits(defs, "upper"), ["Body"])

    def test_drop_flags_move_a_weapon_item_to_the_extra_slot(self):
        defs = self._defs(
            '<Item id="banner" Type="Banner"><Flags DropOnWeaponChange="true" '
            'ForceAttachOffHandPrimaryItemBone="true"/></Item>',
            '<Item id="stone" Type="Thrown"><Flags DropOnAnyAction="true"/></Item>',
            '<Item id="off" Type="Thrown"><Flags DropOnWeaponChange="false"/></Item>',
            '<Item id="caps" Type="Thrown"><Flags DropOnWeaponChange="False"/></Item>',
            '<Item id="other" Type="Thrown"><Flags UseTeamColor="true"/></Item>',
            '<Item id="two" Type="Thrown"><Flags UseTeamColor="true"/><Flags DropOnAnyAction="True"/></Item>',
            '<Item id="helm" Type="HeadArmor"><Flags DropOnWeaponChange="true"/></Item>')
        self.assertEqual(self._fits(defs, "banner"), ["ExtraWeaponSlot"])
        self.assertEqual(self._fits(defs, "stone"), ["ExtraWeaponSlot"])
        self.assertEqual(self._fits(defs, "off"), list(WEAPON_SLOTS))
        self.assertEqual(self._fits(defs, "caps"), list(WEAPON_SLOTS))
        self.assertEqual(self._fits(defs, "other"), list(WEAPON_SLOTS))
        self.assertEqual(self._fits(defs, "two"), ["ExtraWeaponSlot"])
        self.assertEqual(self._fits(defs, "helm"), ["Head"])

    def test_a_weapon_component_decides_the_type(self):
        defs = self._defs(
            '<Item id="goods_sword" Type="Goods"><ItemComponent><Weapon weapon_class="OneHandedSword"/>'
            "</ItemComponent></Item>",
            '<Item id="armour_bow" Type="BodyArmor"><ItemComponent><Weapon weapon_class="Bow"/></ItemComponent></Item>',
            '<Item id="sling" Type="Thrown"><ItemComponent><Weapon weapon_class="Sling"/></ItemComponent></Item>',
            '<Item id="undefined" Type="OneHandedWeapon"><ItemComponent><Weapon weapon_class="Undefined"/>'
            "</ItemComponent></Item>",
            '<Item id="no_class" Type="OneHandedWeapon"><ItemComponent><Weapon/></ItemComponent></Item>',
            '<Item id="no_type"><ItemComponent><Weapon weapon_class="OneHandedSword"/></ItemComponent></Item>')
        for item_id in ("goods_sword", "armour_bow", "sling"):
            self.assertEqual(self._fits(defs, item_id), list(WEAPON_SLOTS), item_id)
        for item_id in ("undefined", "no_class", "no_type"):
            self.assertEqual(self._fits(defs, item_id), [], item_id)
        self.assertEqual(self.abem.item_type(defs.items["goods_sword"]), "OneHandedWeapon")
        self.assertEqual(self.abem.item_type(defs.items["sling"]), "Sling")
        self.assertEqual(self.abem.item_type(defs.items["no_type"]), "Invalid")

    def test_every_weapon_class_gives_the_item_type_the_engine_gives_it(self):
        # Type="Goods" fits nowhere, so the weapon_class alone decides each item, and a class the tool maps to
        # Invalid, to another type or does not know shows in the fitted slots as well as in item_type
        defs = self._defs(*(f'<Item id="{c}" Type="Goods"><ItemComponent><Weapon weapon_class="{c}"/></ItemComponent>'
                            "</Item>" for c in ENGINE_WEAPON_CLASS_TYPES))
        for weapon_class, engine_type in ENGINE_WEAPON_CLASS_TYPES.items():
            with self.subTest(weapon_class):
                self.assertEqual(self.abem.item_type(defs.items[weapon_class]), engine_type)
                self.assertEqual(self._fits(defs, weapon_class),
                                 [s for s in ALL_SLOTS if s in ENGINE_SLOT_FIT[engine_type]])

    def test_a_banner_component_decides_the_type_as_a_weapon_component_does(self):
        # BannerComponent derives from WeaponComponent (BannerComponent.cs:7), so ItemObject.Deserialize replaces the
        # Type for a Banner element as for a Weapon one (ItemObject.cs:595-596, 629-636)
        defs = self._defs(
            '<Item id="goods_banner" Type="Goods"><ItemComponent><Banner banner_level="1" weapon_class="Banner" '
            'effect="IncreasedMeleeDamage"/></ItemComponent></Item>',
            '<Item id="no_class" Type="Banner"><ItemComponent><Banner banner_level="1" '
            'effect="IncreasedMeleeDamage"/></ItemComponent></Item>')
        self.assertEqual(self._fits(defs, "goods_banner"), list(WEAPON_SLOTS))
        self.assertEqual(self.abem.item_type(defs.items["goods_banner"]), "Banner")
        self.assertEqual(self._fits(defs, "no_class"), [])

    def test_a_banner_element_replaces_a_weapon_element_read_before_it(self):
        # ItemObject.Deserialize builds a fresh BannerComponent for a Banner element (ItemObject.cs:595-596), so the
        # weapon data it holds is the Banner's own and the type comes from the Banner's weapon_class
        defs = self._defs(
            '<Item id="both" Type="Goods"><ItemComponent><Weapon weapon_class="OneHandedSword"/>'
            '<Banner weapon_class="Banner" effect="IncreasedMeleeDamage"/></ItemComponent></Item>',
            '<Item id="classless" Type="OneHandedWeapon"><ItemComponent><Weapon weapon_class="OneHandedSword"/>'
            '<Banner effect="IncreasedMeleeDamage"/></ItemComponent></Item>')
        self.assertEqual(self.abem.item_type(defs.items["both"]), "Banner")
        self.assertEqual(self._fits(defs, "both"), list(WEAPON_SLOTS))
        self.assertEqual(self.abem.item_type(defs.items["classless"]), "Invalid")
        self.assertEqual(self._fits(defs, "classless"), [])

    def test_a_crafted_item_takes_its_template_type(self):
        mod = write_module(self.root, "M", [("Items", "i", None)], {"i.xml": items_xml(
            '<CraftedItem id="sword" crafting_template="OneHandedSword"><Pieces/></CraftedItem>',
            '<CraftedItem id="odd" crafting_template="Boomerang"><Pieces/></CraftedItem>')})
        defs = self.abem.load_definitions([("M", mod)])
        self.assertEqual(self._fits(defs, "sword"), list(WEAPON_SLOTS))
        self.assertEqual(self._fits(defs, "odd"), list(ALL_SLOTS))

    def test_the_audit_refuses_only_what_it_can_prove_the_engine_refuses(self):
        defs = self._defs('<Item id="armour" Type="BodyArmor"/>')
        self.assertTrue(self.abem.item_fits_slot(None, "Body"))
        self.assertTrue(self.abem.item_fits_slot(defs.items["armour"], "NumAllWeaponSlots"))
        self.assertTrue(self.abem.item_fits_slot(defs.items["armour"], "Banana"))


SET_ITEMS = ('<Item id="armour" Type="BodyArmor"/>', '<Item id="armour2" Type="BodyArmor"/>',
             '<Item id="goods" Type="Goods"/>', '<Item id="helmet" Type="HeadArmor"/>',
             '<Item id="skirt" Type="BodyArmor"/>')
SET_ROSTER = ('<EquipmentRosters><EquipmentRoster id="std">'
              '<EquipmentSet><Equipment slot="Body" id="Item.armour"/></EquipmentSet>'
              '<EquipmentSet equipmentType="Civilian"><Equipment slot="Body" id="Item.goods"/></EquipmentSet>'
              "</EquipmentRoster></EquipmentRosters>")


class SlotAssemblyTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.root = Path(self._tmp.name)
        import audit_battle_equipment_memory as abem
        self.abem = abem

    def tearDown(self):
        self._tmp.cleanup()

    def _defs(self, character_xml, roster_xml=""):
        registrations = [("Items", "i", None), ("NPCCharacters", "c", None)]
        files = {"i.xml": items_xml(*SET_ITEMS), "c.xml": chars_xml(character_xml)}
        if roster_xml:
            registrations.append(("EquipmentRosters", "r", None))
            files["r.xml"] = roster_xml
        return self.abem.load_definitions([("M", write_module(self.root, "M", registrations, files))])

    def test_a_refused_item_leaves_the_slot_as_it_was(self):
        defs = self._defs('<NPCCharacter id="t"><Equipments><EquipmentRoster>'
                          '<equipment slot="Body" id="Item.armour"/><equipment slot="Body" id="Item.goods"/>'
                          '<equipment slot="Head" id="Item.helmet"/></EquipmentRoster></Equipments></NPCCharacter>')
        t = defs.characters["t"]
        self.assertEqual(self.abem.battle_sets(t, defs), [{"Body": "armour", "Head": "helmet"}])
        self.assertEqual(self.abem.slot_rejections(t, defs), [("Body", "goods")])

    def test_the_release_skirt_in_a_cape_slot_is_refused(self):
        # troops_isengard.xml puts sk_uruk_hai_skirt_a1, a BodyArmor, in the Cape slot of urukhai_champion
        # and urukhai_berserker; the engine never equips it there
        defs = self._defs('<NPCCharacter id="t"><Equipments><EquipmentRoster>'
                          '<equipment slot="Body" id="Item.armour"/><equipment slot="Cape" id="Item.skirt"/>'
                          "</EquipmentRoster></Equipments></NPCCharacter>")
        t = defs.characters["t"]
        self.assertEqual(self.abem.battle_sets(t, defs), [{"Body": "armour"}])
        self.assertEqual(self.abem.slot_rejections(t, defs), [("Cape", "skirt")])
        self.assertEqual(self.abem.troop_item_ids(t, defs), ["armour"])

    def test_an_override_that_does_not_fit_keeps_the_roster_item(self):
        defs = self._defs('<NPCCharacter id="t"><Equipments><EquipmentSet id="std"/>'
                          '<equipment slot="Body" id="Item.goods"/></Equipments></NPCCharacter>', SET_ROSTER)
        t = defs.characters["t"]
        self.assertEqual(self.abem.battle_sets(t, defs), [{"Body": "armour"}])
        self.assertEqual(self.abem.slot_rejections(t, defs), [("Body", "goods")])

    def test_an_override_that_fits_replaces_the_roster_item(self):
        defs = self._defs('<NPCCharacter id="t"><Equipments><EquipmentSet id="std"/>'
                          '<equipment slot="Body" id="Item.armour2"/></Equipments></NPCCharacter>', SET_ROSTER)
        t = defs.characters["t"]
        self.assertEqual(self.abem.battle_sets(t, defs), [{"Body": "armour2"}])
        self.assertEqual(self.abem.slot_rejections(t, defs), [])

    def test_a_roster_set_item_that_does_not_fit_is_refused_for_the_character(self):
        roster = ('<EquipmentRosters><EquipmentRoster id="bad"><EquipmentSet>'
                  '<Equipment slot="Cape" id="Item.skirt"/><Equipment slot="Head" id="Item.helmet"/>'
                  "</EquipmentSet></EquipmentRoster></EquipmentRosters>")
        defs = self._defs('<NPCCharacter id="t"><Equipments><EquipmentSet id="bad"/></Equipments></NPCCharacter>',
                          roster)
        t = defs.characters["t"]
        self.assertEqual(self.abem.battle_sets(t, defs), [{"Head": "helmet"}])
        self.assertEqual(self.abem.slot_rejections(t, defs), [("Cape", "skirt")])

    def test_refusals_follow_the_sets_the_preload_walks(self):
        defs = self._defs('<NPCCharacter id="t"><Equipments><EquipmentSet id="std"/>'
                          '<EquipmentSet id="std" equipmentType="Civilian"/></Equipments></NPCCharacter>', SET_ROSTER)
        t = defs.characters["t"]
        self.assertEqual(self.abem.slot_rejections(t, defs), [])
        self.assertEqual(self.abem.slot_rejections(t, defs, include_civilian=True), [("Body", "goods")])
        self.assertEqual(self.abem.battle_sets(t, defs, include_civilian=True), [{"Body": "armour"}, {}])

    def test_an_override_refused_in_every_set_is_reported_once(self):
        roster = ('<EquipmentRosters><EquipmentRoster id="two">'
                  '<EquipmentSet><Equipment slot="Body" id="Item.armour"/></EquipmentSet>'
                  '<EquipmentSet><Equipment slot="Body" id="Item.armour2"/></EquipmentSet>'
                  "</EquipmentRoster></EquipmentRosters>")
        defs = self._defs('<NPCCharacter id="t"><Equipments><EquipmentSet id="two"/>'
                          '<equipment slot="Body" id="Item.goods"/></Equipments></NPCCharacter>', roster)
        t = defs.characters["t"]
        self.assertEqual(self.abem.battle_sets(t, defs), [{"Body": "armour"}, {"Body": "armour2"}])
        self.assertEqual(self.abem.slot_rejections(t, defs), [("Body", "goods")])

    @staticmethod
    def _rejected(assets):
        return [m for m in assets.misses if m[0] == "ITEM_SLOT_REJECTED"]

    def test_a_refusal_in_a_civilian_set_reaches_the_troop_only_with_civilian_sets(self):
        defs = self._defs('<NPCCharacter id="t"><Equipments><EquipmentRoster>'
                          '<equipment slot="Body" id="Item.armour"/></EquipmentRoster>'
                          '<EquipmentRoster equipmentType="Civilian"><equipment slot="Body" id="Item.goods"/>'
                          "</EquipmentRoster></Equipments></NPCCharacter>")
        t, index = defs.characters["t"], amsm.AssetIndex()
        self.assertEqual(self._rejected(self.abem.troop_assets(t, defs, index)), [])
        self.assertEqual(self._rejected(self.abem.troop_assets(t, defs, index, include_civilian=True)),
                         [("ITEM_SLOT_REJECTED", "goods", "t:Body")])

    def test_an_override_refused_where_only_a_civilian_set_exists_is_not_a_battle_refusal(self):
        defs = self._defs('<NPCCharacter id="t"><Equipments><EquipmentRoster equipmentType="Civilian">'
                          '<equipment slot="Body" id="Item.armour"/></EquipmentRoster>'
                          '<equipment slot="Body" id="Item.goods"/></Equipments></NPCCharacter>')
        t = defs.characters["t"]
        self.assertEqual(self.abem.slot_rejections(t, defs), [])
        self.assertEqual(self.abem.slot_rejections(t, defs, include_civilian=True), [("Body", "goods")])

    def test_an_item_refused_in_one_slot_still_counts_when_another_set_holds_it(self):
        defs = self._defs('<NPCCharacter id="t"><Equipments>'
                          '<EquipmentRoster><equipment slot="Body" id="Item.skirt"/></EquipmentRoster>'
                          '<EquipmentRoster><equipment slot="Cape" id="Item.skirt"/></EquipmentRoster>'
                          "</Equipments></NPCCharacter>")
        t = defs.characters["t"]
        self.assertEqual(self.abem.slot_rejections(t, defs), [("Cape", "skirt")])
        self.assertEqual(self.abem.troop_item_ids(t, defs), ["skirt"])
        self.assertEqual(self._rejected(self.abem.troop_assets(t, defs, amsm.AssetIndex())),
                         [("ITEM_SLOT_REJECTED", "skirt", "t:Cape")])


class CodexCliTests(_MainFixture):
    def _troops(self, *troops):
        (self.release / "TAOM" / "ModuleData" / "troops.xml").write_text(chars_xml(*troops), encoding="utf-8")

    def test_a_refused_item_is_reported_separately_and_not_counted(self):
        self._troops(troop("t1", [("Body", "item_a")]), troop("t3", [("Cape", "item_a")]))
        rc, text = self.run_main()
        self.assertEqual(rc, 0)
        self.assertIn("[EquipMemAudit] fallback reason=ITEM_SLOT_REJECTED count=1 first=item_a consequence="
                      + self.abem.REASONS["ITEM_SLOT_REJECTED"], text.splitlines())
        unresolved = (self.out / "unresolved.tsv").read_text(encoding="utf-8").splitlines()
        self.assertIn("ITEM_SLOT_REJECTED\titem_a\tt3:Cape", unresolved)
        troops = {ln.split("\t")[0]: ln.split("\t") for ln in
                  (self.out / "troops.tsv").read_text(encoding="utf-8").splitlines()[1:]}
        self.assertEqual(troops["t3"][5:], ["1", "0", "15", "15", "15", "1"])   # one set, no item, race meshes only
        self.assertEqual(troops["t1"][5:8], ["1", "1", "2889"])
        report = (self.out / "battle-equipment-memory.md").read_text(encoding="utf-8")
        self.assertIn("| ITEM_SLOT_REJECTED | 1 | item_a |", report)


class OutputFolderTests(_MainFixture):
    def test_a_rerun_without_sides_removes_the_previous_sides_table(self):
        s1, s2 = self.side_files()
        self.run_main("--troops", str(s1), "--troops", str(s2))
        self.assertTrue((self.out / "sides.tsv").is_file())
        self.assertEqual(self.run_main()[0], 0)
        self.assertFalse((self.out / "sides.tsv").exists())
        self.assertTrue((self.out / "assets.tsv").is_file())

    def test_an_aborted_rerun_leaves_only_the_run_log(self):
        self.run_main("--culture", "gondor", "--culture", "mordor")
        self.assertTrue((self.out / "assets.tsv").is_file())
        rc, _text = self.run_main("--culture", "gondor", "--culture", "rohan")
        self.assertEqual(rc, 2)
        self.assertEqual(sorted(p.name for p in self.out.iterdir()), ["run.log"])
        log = (self.out / "run.log").read_text(encoding="utf-8")
        self.assertIn("abort reason=SIDE_EMPTY", log)
        self.assertNotIn("] summary ", log)     # nothing of the earlier, successful run

    def test_a_rerun_keeps_files_it_does_not_own(self):
        self.out.mkdir()
        (self.out / "notes.txt").write_text("mine", encoding="utf-8")
        self.run_main()
        self.run_main("--culture", "gondor", "--culture", "rohan")
        self.assertEqual((self.out / "notes.txt").read_text(encoding="utf-8"), "mine")

    def test_the_report_and_tsv_flags_are_cleaned_where_they_point(self):
        report, tsv = self.root / "rep" / "r.md", self.root / "tsv"
        where = ["--report", str(report), "--tsv-dir", str(tsv)]
        self.run_main("--culture", "gondor", "--culture", "mordor", *where)
        self.assertTrue(report.is_file() and (tsv / "sides.tsv").is_file())
        self.assertEqual(self.run_main("--culture", "gondor", "--culture", "rohan", *where)[0], 2)
        self.assertFalse(report.exists())
        self.assertEqual(sorted(p.name for p in tsv.iterdir()), ["run.log"])

    def test_a_refused_folder_loses_nothing(self):
        target = self.release / "out"
        target.mkdir()
        (target / "assets.tsv").write_text("kept\n", encoding="utf-8")
        with self.assertRaises(SystemExit):
            self.run_main(out=target)
        self.assertEqual((target / "assets.tsv").read_text(encoding="utf-8"), "kept\n")


class LooseRunTests(_MainFixture):
    def _loose_native(self):
        write_pack(self.game / "Modules" / "Native" / "Assets" / "loose.tpac", [mesh_item("ma", render=b"r" * 7)])

    def _asset_rows(self):
        lines = (self.out / "assets.tsv").read_text(encoding="utf-8").splitlines()
        head = lines[0].split("\t")
        rows = (dict(zip(head, ln.split("\t"))) for ln in lines[1:])
        return {(r["kind"], r["name"]): r for r in rows}

    def test_a_module_with_a_loose_tree_reads_only_that_tree(self):
        self._loose_native()
        rc, text = self.run_main("--loose-assets")
        self.assertEqual(rc, 0)
        rows = self._asset_rows()
        ma = rows[("metamesh", "ma")]
        self.assertTrue(ma["pack"].startswith("Assets/"), ma["pack"])
        self.assertEqual(ma["floor_bytes"], "7")
        self.assertNotIn(("texture", "tx"), rows)
        self.assertIn("[EquipMemAudit] fallback reason=COOKED_TREE_NOT_READ count=1 first=Native/AssetPackages "
                      "consequence=" + self.abem.REASONS["COOKED_TREE_NOT_READ"], text.splitlines())
        self.assertIn("COOKED_TREE_NOT_READ\tNative/AssetPackages\t--loose-assets",
                      (self.out / "unresolved.tsv").read_text(encoding="utf-8").splitlines())

    def test_without_the_option_the_loose_tree_is_ignored(self):
        self._loose_native()
        _rc, text = self.run_main()
        ma = self._asset_rows()[("metamesh", "ma")]
        self.assertTrue(ma["pack"].startswith("AssetPackages/"), ma["pack"])
        self.assertEqual(ma["floor_bytes"], "100")
        self.assertNotIn("COOKED_TREE_NOT_READ", text)


class UnsizedTextureTests(_MainFixture):
    def _unsized_texture(self):
        pack = [texture_v2_item("tx", [])] + [item for item in FIXTURE_PACK if item[1] != "tx"]
        write_pack(self.game / "Modules" / "Native" / "AssetPackages" / "pack0.tpac", pack)

    def _report(self):
        return (self.out / "battle-equipment-memory.md").read_text(encoding="utf-8")

    def _note(self):
        return next((ln for ln in self._report().splitlines() if ln.startswith("**Incomplete totals.**")), "")

    def test_the_consequence_names_the_lower_bound(self):
        self.assertIn("lower bound", self.abem.REASONS["TEXTURE_SIZE_UNKNOWN"])

    def test_the_report_says_the_totals_are_incomplete_when_a_texture_has_no_size(self):
        self._unsized_texture()
        _rc, text = self.run_main()
        self.assertIn("[EquipMemAudit] fallback reason=TEXTURE_SIZE_UNKNOWN count=1 first=tx ", text)
        report = self._report()
        self.assertIn("**Incomplete totals.** TEXTURE_SIZE_UNKNOWN rows: 1.", report)
        self.assertIn("lower bound", report)

    def test_the_note_names_both_causes_and_blames_no_loose_texture_in_a_packed_run(self):
        self._unsized_texture()     # a version 2 header in a cooked pack; this run reads no loose tree
        self.run_main()
        note = self._note()
        self.assertIn("either did not decode (the shared decoder reads only texture metadata version 3) or "
                      "names a format the size tables lack", note)
        self.assertNotIn("loose", note)

    def test_the_report_has_no_incomplete_note_when_every_texture_is_sized(self):
        self.run_main()
        self.assertNotIn("Incomplete totals", self._report())


if __name__ == "__main__":
    unittest.main()
