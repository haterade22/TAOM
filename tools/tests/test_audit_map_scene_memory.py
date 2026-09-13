#!/usr/bin/env python3
"""Tests for the campaign map scene asset-payload manifest.

Pure stdlib, synthetic inputs, no game install. Two kinds of pin:

  - the metadata decoders run against REAL metadata blobs lifted from the installed
    packs (a texture, a material and a metamesh, a few hundred bytes each), so a
    layout regression shows up against bytes the engine actually wrote, not only
    against bytes this file's encoders produce;
  - the container walker, the flora walk, the DDS header and the whole
    build_manifest pipeline run against a synthetic module tree written into
    tmp_path, which pins module priority, prefab expansion, flora attribution,
    the flags and the totals.
"""
import hashlib
import os
import struct
import sys

import pytest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import audit_map_scene_memory as a  # noqa: E402

# --------------------------------------------------------------------------- #
# real metadata blobs from the installed 1.4.8 packs (2026-09-12)
# --------------------------------------------------------------------------- #
# TAOM_Map/AssetPackages/pack0.tpac, texture argonath_2_h: 4096x4096, 13 mips, BC4
REAL_TEXTURE_META = bytes.fromhex(
    "0300000000000000000000000000000000000000000000005200000024424153452f4d6f64756c65732f54414f4d5f4d61702f"
    "4173736574536f75726365732f43616d706169676e204d61702f4d61702049636f6e732f476f6e646f722f6172676f6e6174"
    "685f325f682e706e6703d5b17b0438702300000000000000000002000000020010000000100000010000000d01000300000042"
    "43340000000000000000040000006e6f6e650000000000efa38ddd515fdeab00000000000000001f0000000000803fc02f7486"
    "fe7f0000080000000000803f")
# Native/AssetPackages/core_game.tpac, material dirty_a: shader pbr_shading, textures dirty_a_d (slot 0)
# and dirty_a_n (slot 2)
REAL_MATERIAL_META = bytes.fromhex(
    "0000000000000000000000000000000000000000020000000000000003000000160000006e6f5f6d6f646966795f6465707468"
    "5f62756666657210000000646f6e745f636173745f736861646f7713000000676275666665725f616c7068615f626c656e6400"
    "000000010000000700000062756d706d6170080000006d6f64756c61746571b6a0ce4f381142a1a648a29a083c8f0200000000"
    "00000039f48546a2e76543b02e4b08dfd50bf00200000044ecbe1b45f75249ac1324af6c21fdd80000000001000000190000"
    "007573655f73706563756c61725f66726f6d5f646966667573650000803f6666263f0000803f0000c03f000000000000000000"
    "00000000000000000000000000000000000000000000000000803f0000803f0000803f0000803f0000803f0000803f0000803f"
    "0000803f00000000000000000000000000000040000000000000003f333333400000803f")
# Native/AssetPackages/meshes_shared_4.tpac, metamesh dirty_a: one LOD record, a quad (4, 2, 4)
REAL_METAMESH_META = bytes.fromhex(
    "01000000b35f84cbd8b1014d8ca37f602f1521f4ffff7f7f0000000000000000000000000000000000000000000000000000"
    "0000010000000100000000020000000000000000000000000000000000000002000000a4de1a0eefe5e545a8a444f95af1ed"
    "f30700000064697274795f610000000000000000c9fb3436f5fd214a8f4073c1d8a91b380000803f0000803f0000803f0000"
    "803f0000803f0000803f0000803f0000803f0000000000000000000000000000000000000000000000000000000000000000"
    "0000000004000000020000000400000000000000000000003e7589c0bcef87bf008014b80000803f38758940b8ef873f0080"
    "14380000803f0000c0b5000080b4000000000000803fbc988d4000000000000000000000803f000000000000003f0000003f"
    "0000003f0000803f3333733f0000803f0000003f0000003f0000803f780000000000000080bf0000803f0000000000000000"
    "000000000000000000000000000101")
DIRTY_A_MATERIAL_GUID = bytes.fromhex("c9fb3436f5fd214a8f4073c1d8a91b38")
DIRTY_A_D_GUID = bytes.fromhex("39f48546a2e76543b02e4b08dfd50bf0")
DIRTY_A_N_GUID = bytes.fromhex("44ecbe1b45f75249ac1324af6c21fdd8")
PBR_SHADING_GUID = bytes.fromhex("71b6a0ce4f381142a1a648a29a083c8f")

TYPE_GUID = {kind: guid for guid, kind in a.KIND_BY_TYPE_GUID.items()}


# --------------------------------------------------------------------------- #
# encoders (the write side of the layouts the tool decodes)
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


def write_flora_bin(path, records, header_count=None):
    body = b""
    for name, variation in records:
        body += sized(name) + struct.pack("<I", variation) + struct.pack("<16f", *([1.0] * 16)) + struct.pack("<4f", 1, 1, 1, 1)
    count = len(records) if header_count is None else header_count
    data = b"FLR2" + struct.pack("<I", 4 + len(body)) + struct.pack("<I", count) + body
    with open(path, "wb") as f:
        f.write(data)


def write_dds(path, width, height, mips, fourcc=b"DXT1"):
    head = b"DDS " + struct.pack("<IIIIIII", 124, 0x1007, height, width, 0, 0, mips) + bytes(44)
    head += struct.pack("<II4s", 32, 4, fourcc) + bytes(20) + bytes(16) + bytes(4)
    assert len(head) == 128
    with open(path, "wb") as f:
        f.write(head + bytes(64))


# --------------------------------------------------------------------------- #
# texture chain formula
# --------------------------------------------------------------------------- #
class TestTextureChain:
    def test_bc5_4096_full_chain_matches_the_real_segment(self):
        assert a.texture_chain_bytes(4096, 4096, 13, "BC5") == 22_369_648

    def test_dxt1_1024_full_chain(self):
        assert a.texture_chain_bytes(1024, 1024, 11, "DXT1") == 699_064

    def test_dxt5_16k_vista(self):
        assert a.texture_chain_bytes(16384, 16384, 15, "DXT5") == 357_913_968

    def test_cubemap_multiplies_by_faces(self):
        assert a.texture_chain_bytes(256, 256, 9, "R16G16B16A16F", faces=6) == 4_194_288

    def test_linear_rgba8_small_chain(self):
        assert a.texture_chain_bytes(4, 4, 3, "R8G8B8A8_UNORM") == (16 + 4 + 1) * 4

    def test_no_mips_is_the_top_level_only(self):
        assert a.texture_chain_bytes(1024, 1024, 1, "BC7") == 1_048_576

    def test_unknown_format_is_none(self):
        assert a.texture_chain_bytes(64, 64, 1, "SOMETHING_NEW") is None


# --------------------------------------------------------------------------- #
# decoders against real bytes
# --------------------------------------------------------------------------- #
class TestRealBlobs:
    def test_texture_header(self):
        assert len(REAL_TEXTURE_META) == 215
        t = a.parse_texture_meta(REAL_TEXTURE_META)
        assert (t.width, t.height, t.mips, t.faces, t.fmt) == (4096, 4096, 13, 1, "BC4")
        assert t.source.endswith("argonath_2_h.png")
        assert a.texture_chain_bytes(t.width, t.height, t.mips, t.fmt, t.faces) == 11_184_824

    def test_material_textures_and_slots(self):
        assert len(REAL_MATERIAL_META) == 341
        mtl = a.parse_material_meta(REAL_MATERIAL_META)
        assert mtl.shader_guid == PBR_SHADING_GUID
        assert mtl.textures == [(0, DIRTY_A_D_GUID), (2, DIRTY_A_N_GUID)]
        assert mtl.blend == "modulate"
        assert "dont_cast_shadow" in mtl.flags
        assert mtl.flags2 == ["use_specular_from_diffuse"]

    def test_metamesh_record_is_a_quad(self):
        assert len(REAL_METAMESH_META) == 365
        recs = a.parse_metamesh_records(REAL_METAMESH_META, "dirty_a")
        assert [r.name for r in recs] == ["dirty_a"]
        assert recs[0].material_guid == DIRTY_A_MATERIAL_GUID
        assert (recs[0].positions, recs[0].faces, recs[0].vertices) == (4, 2, 4)

    def test_guid_scan_finds_the_material_the_record_names(self):
        by_guid = {DIRTY_A_MATERIAL_GUID: a.TpacItem("Native", "x.tpac", "material", "dirty_a",
                                                     DIRTY_A_MATERIAL_GUID, b"", [])}
        assert a.scan_guids(REAL_METAMESH_META, by_guid, "material") == {DIRTY_A_MATERIAL_GUID}
        assert a.plausible_counts(a.parse_metamesh_records(REAL_METAMESH_META, "dirty_a")[0], by_guid)


class TestSyntheticDecoders:
    def test_texture_round_trip_with_flags(self):
        meta = encode_texture_meta("$BASE/x.png", 2048, 1024, 12, "BC7", flags=["for_terrain"], flags2=["has_alpha"])
        t = a.parse_texture_meta(meta)
        assert (t.width, t.height, t.mips, t.faces, t.fmt) == (2048, 1024, 12, 1, "BC7")
        assert t.flags == ["for_terrain"] and t.flags2 == ["has_alpha"]

    def test_texture_wrong_version_is_refused(self):
        meta = bytearray(encode_texture_meta("x", 8, 8, 1, "DXT1"))
        meta[0] = 4
        with pytest.raises(ValueError):
            a.parse_texture_meta(bytes(meta))

    def test_material_round_trip(self):
        g1, g2 = guid_for("t1"), guid_for("t2")
        meta = encode_material_meta(guid_for("shader"), [(0, g1), (4, g2)], flags=["dont_cast_shadow"],
                                    tags=["bumpmap", "doubleuv"], blend="factor")
        mtl = a.parse_material_meta(meta)
        assert mtl.textures == [(0, g1), (4, g2)]
        assert mtl.tags == ["bumpmap", "doubleuv"] and mtl.blend == "factor"

    def test_metamesh_records_are_found_by_name_prefix(self):
        m1, m2 = guid_for("m1"), guid_for("m2")
        meta = encode_metamesh_meta("rock_a", [("rock_a", m1, 100, 200, 120), ("rock_a.lod1", m2, 50, 90, 60)])
        recs = a.parse_metamesh_records(meta, "ROCK_A")
        assert [(r.name, r.material_guid, r.positions, r.faces, r.vertices) for r in recs] == [
            ("rock_a", m1, 100, 200, 120), ("rock_a.lod1", m2, 50, 90, 60)]


# --------------------------------------------------------------------------- #
# container walk, index priority, flora walk, dds header
# --------------------------------------------------------------------------- #
class TestContainer:
    def test_walk_yields_kinds_names_meta_and_segment_sizes(self, tmp_path):
        p = tmp_path / "pack0.tpac"
        write_tpac(p, [
            ("texture", "tex_a", guid_for("tex_a"), encode_texture_meta("s", 16, 16, 1, "DXT1"),
             [(a.SEG_TEXTURE_PIXELS, bytes(128))]),
            ("physics", "bo_a", guid_for("bo_a"), b"\x01\x02", [(bytes(16), bytes(7)), (bytes(16), bytes(3))]),
        ])
        items = list(a.iter_tpac_items(p, "TAOM_Map"))
        assert [(i.kind, i.name, i.module, i.pack) for i in items] == [
            ("texture", "tex_a", "TAOM_Map", "pack0.tpac"), ("physics", "bo_a", "TAOM_Map", "pack0.tpac")]
        assert items[0].seg_bytes(a.SEG_TEXTURE_PIXELS) == 128 and items[0].has_segment(a.SEG_TEXTURE_PIXELS)
        assert items[1].meta == b"\x01\x02" and items[1].seg_bytes() == 10

    def test_not_a_tpac_is_refused(self, tmp_path):
        p = tmp_path / "bad.tpac"
        p.write_bytes(b"NOPE" + bytes(64))
        with pytest.raises(ValueError):
            list(a.iter_tpac_items(p, "x"))

    def test_index_prefers_the_first_module_and_records_the_rest(self, tmp_path):
        taom = tmp_path / "TAOM_Map" / "AssetPackages"
        native = tmp_path / "Native" / "AssetPackages"
        taom.mkdir(parents=True)
        native.mkdir(parents=True)
        write_tpac(taom / "pack0.tpac", [("texture", "Shared_D", guid_for("taom"),
                                           encode_texture_meta("s", 16, 16, 1, "DXT1"), [(a.SEG_TEXTURE_PIXELS, bytes(128))])])
        write_tpac(native / "core.tpac", [("texture", "shared_d", guid_for("native"),
                                            encode_texture_meta("s", 16, 16, 1, "DXT1"), [(a.SEG_TEXTURE_PIXELS, bytes(128))])])
        idx = a.AssetIndex()
        idx.add_module("TAOM_Map", tmp_path / "TAOM_Map")
        idx.add_module("Native", tmp_path / "Native")
        idx.add_module("SandBox", tmp_path / "SandBox")
        assert idx.get("texture", "SHARED_D").module == "TAOM_Map"
        assert idx.get("texture", "SHARED_D").pack == "AssetPackages/pack0.tpac"
        assert idx.also_in[("texture", "shared_d")] == ["Native/AssetPackages/core.tpac"]
        assert idx.errors and idx.errors[0][0] == "SandBox"

    def test_index_replaces_a_stub_only_twin_inside_one_module(self, tmp_path):
        native = tmp_path / "Native" / "AssetPackages"
        native.mkdir(parents=True)
        write_tpac(native / "core.tpac", [("texture", "armor_d", guid_for("stub"),
                                            encode_texture_meta("s", 4096, 4096, 13, "DXT1"), [(a.SEG_TEXTURE_STUB, bytes(64))])])
        write_tpac(native / "pack_1.tpac", [("texture", "armor_d", guid_for("full"),
                                              encode_texture_meta("s", 4096, 4096, 13, "DXT1"), [(a.SEG_TEXTURE_PIXELS, bytes(256))])])
        idx = a.AssetIndex()
        idx.add_module("Native", tmp_path / "Native")
        item = idx.get("texture", "armor_d")
        assert item.pack == "AssetPackages/pack_1.tpac" and item.has_segment(a.SEG_TEXTURE_PIXELS)
        assert idx.also_in[("texture", "armor_d")] == ["Native/AssetPackages/core.tpac (stub)"]

    def test_index_walks_emassetpackages_recursively_after_assetpackages(self, tmp_path):
        native = tmp_path / "Native"
        (native / "AssetPackages").mkdir(parents=True)
        (native / "EmAssetPackages" / "text2").mkdir(parents=True)
        write_tpac(native / "AssetPackages" / "core.tpac", [("texture", "wall_d", guid_for("stub"),
                                                              encode_texture_meta("s", 256, 256, 9, "DXT1"), [(a.SEG_TEXTURE_STUB, bytes(64))])])
        write_tpac(native / "EmAssetPackages" / "text2" / "text2.tpac", [("texture", "wall_d", guid_for("full"),
                                                                           encode_texture_meta("s", 256, 256, 9, "DXT1"), [(a.SEG_TEXTURE_PIXELS, bytes(43_704))])])
        idx = a.AssetIndex()
        idx.add_module("Native", native)
        item = idx.get("texture", "wall_d")
        assert item.pack == "EmAssetPackages/text2/text2.tpac" and item.seg_bytes(a.SEG_TEXTURE_PIXELS) == 43_704
        assert [p[1] for p in idx.packs] == ["AssetPackages/core.tpac", "EmAssetPackages/text2/text2.tpac"]


class TestFloraBin:
    def test_walk_counts_per_kind_and_is_consistent(self, tmp_path):
        p = tmp_path / "flora.bin"
        write_flora_bin(p, [("flora_grass_a", 0), ("flora_grass_a", 3), ("tree_pines", 1)])
        fb = a.parse_flora_bin(p)
        assert fb.header_count == 3 and fb.walked == 3 and fb.walk_ok
        assert fb.per_kind == {"flora_grass_a": 2, "tree_pines": 1}
        assert fb.variations == {0: 1, 3: 1, 1: 1}

    def test_header_count_disagreeing_with_the_walk_is_flagged(self, tmp_path):
        p = tmp_path / "flora.bin"
        write_flora_bin(p, [("flora_grass_a", 0)], header_count=5)
        assert not a.parse_flora_bin(p).walk_ok

    def test_wrong_magic_is_not_walked(self, tmp_path):
        p = tmp_path / "flora.bin"
        p.write_bytes(b"XXXX" + bytes(40))
        fb = a.parse_flora_bin(p)
        assert fb.walked == 0 and not fb.walk_ok


class TestDds:
    def test_standard_header(self, tmp_path):
        p = tmp_path / "flowmap.dds"
        write_dds(p, 512, 256, 1)
        assert a.parse_dds_header(p) == (512, 256, 1, "DXT1")

    def test_non_dds_is_none(self, tmp_path):
        p = tmp_path / "x.dds"
        p.write_bytes(bytes(200))
        assert a.parse_dds_header(p) is None


# --------------------------------------------------------------------------- #
# end to end on a synthetic module tree
# --------------------------------------------------------------------------- #
SCENE_XML = """<?xml version="1.0"?>
<scene name="synthetic" version="2">
  <environment_properties>
    <water_properties version="1">
      <property name="water_material" value="water_default"/>
    </water_properties>
  </environment_properties>
  <entities>
    <game_entity prefab="house_a"><transform position="0, 0, 0"/></game_entity>
    <game_entity prefab="house_a"><transform position="1, 0, 0"/></game_entity>
    <game_entity prefab="ghost_prefab"><transform position="2, 0, 0"/></game_entity>
    <game_entity name="rock_a" old_prefab_name="">
      <physics shape="bo_rock_a"/>
      <components>
        <meta_mesh_component name="rock_a">
          <mesh name="rock_a" factor="1"/>
          <mesh name="rock_a.lod1" factor="1" material="mat_override"/>
        </meta_mesh_component>
        <particle_system_instanced_component base_effect="{X}"/>
      </components>
      <children>
        <game_entity name="child">
          <components><decal_component material="decal_a"/></components>
        </game_entity>
      </children>
    </game_entity>
  </entities>
  <terrain enabled="true" vista_diffuse_name="vista_16k" dynamic_flowmap_texture_name="missing_flowmap">
    <outer_mesh><variable name="outer_mesh_name" value=""/></outer_mesh>
    <layers version="1">
      <layer name="01.Dirt">
        <summer is_enabled="true" name="01.Dirt">
          <textures>
            <texture type="diffusemap" name="ground_d"/>
            <texture type="areamap" name="none"/>
          </textures>
          <meshes/>
        </summer>
      </layer>
    </layers>
  </terrain>
</scene>
"""

ATMOSPHERE_XML = """<atmosphere><values>
  <value name="skybox_background_texture_name" value="sky_a"/>
  <value name="cloud_amount" value="0.1"/>
</values></atmosphere>
"""

PREFABS_XML = """<prefabs>
  <game_entity name="house_a" old_prefab_name="">
    <physics shape="bo_house_a"/>
    <components><meta_mesh_component name="house_a_mesh"/></components>
    <children>
      <game_entity name="house_a_roof">
        <components>
          <meta_mesh_component name="rock_a"><mesh name="rock_a" material="mat_roof"/></meta_mesh_component>
        </components>
      </game_entity>
    </children>
  </game_entity>
</prefabs>
"""

FLORA_KINDS_XML = """<flora_kinds>
  <flora_kind name="kind_tree" view_distance="100">
    <seasonal_kind season="summer"><flora_variations>
      <flora_variation name="tree_a" body_name="bo_tree_a" density_multiplier="1">
        <mesh name="tree_a.0.lod0" material="mat_tree"/>
      </flora_variation>
    </flora_variations></seasonal_kind>
    <seasonal_kind season="winter"><flora_variations>
      <flora_variation name="tree_a" body_name="bo_tree_a" density_multiplier="1">
        <mesh name="tree_a.0.lod0" material="mat_tree"/>
      </flora_variation>
    </flora_variations></seasonal_kind>
  </flora_kind>
</flora_kinds>
"""

REFERENCES_TXT = "6\nflora_entity kind_tree\nmesh rock_a\ntexture ground_d\nprefab house_a\nprefab merlon_1\ntexture only_in_references\n"


@pytest.fixture
def module_tree(tmp_path):
    modules = tmp_path / "game" / "Modules"
    scene = modules / "TAOM_Map" / "SceneObj" / "Main_map"
    scene.mkdir(parents=True)
    (modules / "TAOM_Map" / "AssetPackages").mkdir()
    (modules / "TAOM_Map" / "Prefabs").mkdir()
    (modules / "Native" / "AssetPackages").mkdir(parents=True)
    (modules / "Native" / "EmAssetPackages" / "text2").mkdir(parents=True)
    (modules / "Native" / "ModuleData").mkdir()

    mat_roof, mat_override, mat_tree, decal_a, water = (guid_for(n) for n in
                                                          ("mat_roof", "mat_override", "mat_tree", "decal_a", "water_default"))
    tex = {n: guid_for(n) for n in ("roof_d", "override_d", "tree_d", "decal_d", "water_n", "ground_d", "vista_16k",
                                    "sky_a", "rgba_d", "only_in_references", "stub_only_d")}
    write_tpac(modules / "TAOM_Map" / "AssetPackages" / "pack0.tpac", [
        ("texture", "roof_d", tex["roof_d"], encode_texture_meta("s", 1024, 1024, 1, "DXT1"),
         [(a.SEG_TEXTURE_PIXELS, bytes(524_288))]),
        ("texture", "rgba_d", tex["rgba_d"], encode_texture_meta("s", 64, 64, 7, "R8G8B8A8_UNORM"),
         [(a.SEG_TEXTURE_PIXELS, bytes(a.texture_chain_bytes(64, 64, 7, "R8G8B8A8_UNORM")))]),
        ("texture", "vista_16k", tex["vista_16k"], encode_texture_meta("s", 256, 256, 9, "DXT5"),
         [(a.SEG_TEXTURE_PIXELS, bytes(87_408))]),
        ("texture", "ground_d", tex["ground_d"], encode_texture_meta("s", 128, 128, 8, "DXT1"),
         [(a.SEG_TEXTURE_PIXELS, bytes(10_936))]),
        ("material", "mat_roof", mat_roof, encode_material_meta(guid_for("sh"), [(0, tex["roof_d"]), (2, tex["rgba_d"])]), []),
        ("material", "mat_override", mat_override, encode_material_meta(guid_for("sh"), [(0, tex["override_d"])]), []),
        ("material", "decal_a", decal_a, encode_material_meta(guid_for("sh"), [(0, tex["decal_d"]), (2, tex["stub_only_d"])]), []),
        ("metamesh", "rock_a", guid_for("rock_a"),
         encode_metamesh_meta("rock_a", [("rock_a", mat_roof, 10, 20, 12), ("rock_a.lod1", mat_roof, 5, 8, 6)]),
         [(a.SEG_MESH_A, bytes(1000)), (a.SEG_MESH_A, bytes(500)), (a.SEG_MESH_TABLE, bytes(30)),
          (a.SEG_MESH_B, bytes(900)), (a.SEG_MESH_B, bytes(400))]),
        ("metamesh", "house_a_mesh", guid_for("house_a_mesh"),
         encode_metamesh_meta("house_a_mesh", [("house_a_mesh", bytes(16), 3, 1, 3)]),
         [(a.SEG_MESH_A, bytes(200)), (a.SEG_MESH_B, bytes(100))]),
        ("physics", "bo_rock_a", guid_for("bo_rock_a"), b"", [(bytes(16), bytes(50))]),
        ("physics", "bo_house_a", guid_for("bo_house_a"), b"", [(bytes(16), bytes(70))]),
    ])
    write_tpac(modules / "Native" / "AssetPackages" / "core.tpac", [
        ("texture", "ground_d", guid_for("ground_d_native"), encode_texture_meta("s", 128, 128, 8, "DXT1"),
         [(a.SEG_TEXTURE_PIXELS, bytes(10_936))]),
        ("texture", "override_d", tex["override_d"], encode_texture_meta("s", 256, 256, 9, "BC5"),
         [(a.SEG_TEXTURE_STUB, bytes(174_776))]),
        ("texture", "stub_only_d", tex["stub_only_d"], encode_texture_meta("s", 2048, 2048, 12, "BC5"),
         [(a.SEG_TEXTURE_STUB, bytes(174_776))]),
        ("texture", "tree_d", tex["tree_d"], encode_texture_meta("s", 512, 512, 10, "DXT5"),
         [(a.SEG_TEXTURE_PIXELS, bytes(349_552))]),
        ("texture", "decal_d", tex["decal_d"], encode_texture_meta("s", 256, 256, 9, "BC7"),
         [(a.SEG_TEXTURE_PIXELS, bytes(87_408))]),
        ("texture", "sky_a", tex["sky_a"], encode_texture_meta("s", 4096, 2048, 1, "DXT1"),
         [(a.SEG_TEXTURE_PIXELS, bytes(4_194_304))]),
        ("texture", "water_n", tex["water_n"], encode_texture_meta("s", 64, 64, 7, "BC5"),
         [(a.SEG_TEXTURE_PIXELS, bytes(5_488))]),
        ("material", "mat_tree", mat_tree, encode_material_meta(guid_for("sh"), [(0, tex["tree_d"])]), []),
        ("material", "water_default", water, encode_material_meta(guid_for("sh"), [(2, tex["water_n"])]), []),
        ("metamesh", "tree_a", guid_for("tree_a"),
         encode_metamesh_meta("tree_a", [("tree_a.0.lod0", mat_tree, 40, 60, 44)]),
         [(a.SEG_MESH_A, bytes(3000)), (a.SEG_MESH_B, bytes(2800))]),
        ("physics", "bo_tree_a", guid_for("bo_tree_a"), b"", [(bytes(16), bytes(90))]),
    ])
    write_tpac(modules / "Native" / "EmAssetPackages" / "text2" / "text2.tpac", [
        ("texture", "override_d", guid_for("override_d_full"), encode_texture_meta("s", 256, 256, 9, "BC5"),
         [(a.SEG_TEXTURE_PIXELS, bytes(87_408))]),
    ])
    (modules / "TAOM_Map" / "Prefabs" / "houses.xml").write_bytes(PREFABS_XML.encode("utf-8"))
    (modules / "Native" / "ModuleData" / "flora_kinds.xml").write_bytes(FLORA_KINDS_XML.encode("utf-8"))
    (scene / "scene.xscene").write_bytes(SCENE_XML.encode("utf-8"))
    (scene / "atmosphere.xml").write_bytes(ATMOSPHERE_XML.encode("utf-8"))
    (scene / "references.txt").write_bytes(REFERENCES_TXT.encode("utf-8"))
    write_flora_bin(scene / "flora.bin", [("kind_tree", 0)] * 3)
    write_dds(scene / "flowmap.dds", 32, 32, 1)
    (scene / "terrain.bin").write_bytes(bytes(1234))
    return modules, scene


class TestBuildManifest:
    def test_textures_resolve_by_module_priority_with_flags(self, module_tree):
        modules, scene = module_tree
        m = a.build_manifest(modules, scene)
        rows = {r["name"]: r for r in m.texture_rows}
        assert rows["ground_d"]["module"] == "TAOM_Map" and rows["ground_d"]["also_in"] == "Native/AssetPackages/core.tpac"
        assert rows["ground_d"]["resident_bytes"] == 10_936 and rows["ground_d"]["formula_bytes"] == 10_936
        assert "terrain layer 01.Dirt diffusemap" in rows["ground_d"]["referenced_by"]
        assert rows["roof_d"]["flags"] == ["NO_MIPS_ABOVE_512"]
        assert rows["rgba_d"]["flags"] == ["UNCOMPRESSED"]
        assert rows["stub_only_d"]["flags"] == ["PIXELS_NOT_IN_PACKS"]
        assert rows["stub_only_d"]["resident_bytes"] == a.texture_chain_bytes(2048, 2048, 12, "BC5")
        assert rows["stub_only_d"]["size_source"].startswith("formula")
        assert rows["override_d"]["flags"] == ["FULL_CHAIN_ONLY_IN_EMASSETPACKAGES"]
        assert rows["override_d"]["pack"] == "EmAssetPackages/text2/text2.tpac"
        assert rows["override_d"]["resident_bytes"] == 87_408 and rows["override_d"]["size_source"] == "pixel_segment"
        assert rows["override_d"]["also_in"] == "Native/AssetPackages/core.tpac (stub)"
        assert rows["sky_a"]["referenced_by"] == "atmosphere:skybox_background_texture_name=1"
        assert rows["water_n"]["referenced_by"] == "material:water_default=1"
        assert rows["tree_d"]["module"] == "Native" and rows["decal_d"]["referenced_by"] == "material:decal_a=1"
        assert "only_in_references" not in rows

    def test_meshes_count_direct_prefab_and_flora_references(self, module_tree):
        modules, scene = module_tree
        m = a.build_manifest(modules, scene)
        rows = {r["name"]: r for r in m.mesh_rows}
        rock = rows["rock_a"]
        assert (rock["scene_direct"], rock["via_prefabs"], rock["flora_kind_instances"]) == (1, 2, 0)
        assert rock["records"] == 2 and (rock["largest_positions"], rock["largest_faces"], rock["largest_vertices"]) == (10, 20, 12)
        assert rock["all_records_faces"] == 28 and rock["total_bytes"] == 1000 + 500 + 30 + 900 + 400
        assert rock["in_references_txt"] == "yes" and rock["materials"] == "mat_roof" and rock["flags"] == []
        house = rows["house_a_mesh"]
        assert (house["scene_direct"], house["via_prefabs"]) == (0, 2) and house["records"] == 1
        tree = rows["tree_a"]
        assert tree["flora_kind_instances"] == 3 and tree["module"] == "Native"

    def test_prefab_rows_and_unresolved(self, module_tree):
        modules, scene = module_tree
        m = a.build_manifest(modules, scene)
        prefab = {r["prefab"]: r for r in m.prefab_rows}
        assert prefab["house_a"]["scene_instances"] == 2 and prefab["house_a"]["entities_per_instance"] == 2
        assert prefab["house_a"]["distinct_meshes"] == 2 and prefab["house_a"]["physics_shapes"] == 1
        assert prefab["house_a"]["mesh_bytes"] == 2830 + 300
        assert prefab["ghost_prefab"]["module"] == "UNRESOLVED"
        unresolved = {(k, n) for k, n, _ in m.unresolved}
        assert ("prefab", "ghost_prefab") in unresolved
        assert ("texture", "missing_flowmap") in unresolved
        assert ("texture", "only_in_references") in unresolved
        assert ("references.txt prefab", "merlon_1") in unresolved
        assert not any(k == "material" for k, _, _ in m.unresolved)

    def test_flora_physics_and_scene_files(self, module_tree):
        modules, scene = module_tree
        m = a.build_manifest(modules, scene)
        assert m.flora_bin.walk_ok and m.flora_bin.header_count == 3
        flora = m.flora_rows[0]
        assert flora["kind"] == "kind_tree" and flora["instances"] == 3 and flora["defined_in"] == "Native"
        assert flora["seasons"] == "summer,winter" and flora["variation_meshes"] == 1 and flora["mesh_bytes"] == 5800
        physics = {r["name"]: r for r in m.physics_rows}
        assert physics["bo_tree_a"]["flora_kind_instances"] == 3
        assert physics["bo_house_a"]["via_prefabs"] == 2 and physics["bo_rock_a"]["scene_direct"] == 1
        files = {name: (size, note) for name, size, note in m.scene_files}
        assert files["terrain.bin"][0] == 1234
        assert files["flowmap.dds"][1] == "DDS 32x32, 1 mip(s), DXT1"
        assert files["flora.bin"][1].startswith("FLR2, 3 instances")

    def test_totals_by_module_and_category(self, module_tree):
        modules, scene = module_tree
        m = a.build_manifest(modules, scene)
        assert m.totals[("TAOM_Map", "meshes")] == (2, 2830 + 300)
        assert m.totals[("Native", "meshes")] == (1, 5800)
        assert m.totals[("TAOM_Map", "physics")] == (2, 120)
        assert m.totals[("Native", "physics")] == (1, 90)
        native_tex = m.totals[("Native", "textures")]
        assert native_tex[0] == 6
        assert native_tex[1] == 349_552 + 87_408 + 4_194_304 + 5_488 + 87_408 + a.texture_chain_bytes(2048, 2048, 12, "BC5")
        assert m.totals[("Native", "textures: chain only in EmAssetPackages")] == (1, 87_408)
        assert m.totals[("Native", "textures: pixels in no pack (formula)")] == (1, a.texture_chain_bytes(2048, 2048, 12, "BC5"))
        assert m.totals[("TAOM_Map", "textures")][0] == 4
        assert m.totals[("TAOM_Map", "meshes: segment 5f98413d")] == (2, 1000 + 500 + 200)
        assert m.totals[("TAOM_Map", "meshes: segment 97f81dbb")] == (2, 900 + 400 + 100)
        assert m.totals[("TAOM_Map (scene files)", "scene_files")][0] == 4
        grand_line = [line for line in a.summary_lines(m) if line.strip().startswith("GRAND TOTAL")][0]
        expected = (sum(b for (_, c), (_, b) in m.totals.items() if ":" not in c))
        assert f"({expected:,} bytes)" in grand_line

    def test_outputs_are_written(self, module_tree, tmp_path):
        modules, scene = module_tree
        m = a.build_manifest(modules, scene)
        out = tmp_path / "out"
        written = a.write_tsvs(m, out)
        assert {os.path.basename(p) for p in written} == {"textures.tsv", "meshes.tsv", "materials.tsv", "physics.tsv",
                                                          "prefabs.tsv", "flora.tsv", "totals.tsv", "unresolved.tsv"}
        header = (out / "textures.tsv").read_text(encoding="utf-8").splitlines()[0].split("\t")
        assert header == a.TEXTURE_COLUMNS
        a.write_report(m, out / "report.md", top=5)
        text = (out / "report.md").read_text(encoding="utf-8")
        assert "GRAND TOTAL" in text and "| house_a |" in text
        assert "—" not in text and "–" not in text
        lines = a.summary_lines(m)
        assert any(line.startswith("top 20 textures") for line in lines)

    def test_refuses_to_write_inside_the_game_install(self, tmp_path):
        with pytest.raises(SystemExit):
            a.refuse_inside_game(tmp_path / "Modules" / "TAOM_Map" / "out", tmp_path)
        a.refuse_inside_game(tmp_path.parent / "elsewhere", tmp_path)

    def test_main_runs_end_to_end(self, module_tree, tmp_path, capsys):
        modules, scene = module_tree
        out = tmp_path / "manifest"
        rc = a.main(["--game-dir", str(modules.parent), "--out-dir", str(out), "--top", "3"])
        assert rc == 0
        assert (out / "map-scene-memory.md").is_file() and (out / "totals.tsv").is_file()
        assert "GRAND TOTAL" in capsys.readouterr().out
