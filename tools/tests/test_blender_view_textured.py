"""Tests for the pure parts of tools/oneoff/blender_view_textured.py (no Blender needed)."""
import math
import os
import sys

import pytest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "oneoff"))

import blender_view_textured as view  # noqa: E402


def test_a_final_material_uses_its_own_set_through_the_mesh_uvs():
    route = view.material_route("t_rivendell_wall_stone_a")
    assert route == {"set": "t_rivendell_wall_stone_a", "mode": "uv"}


def test_a_blockout_material_borrows_a_set_by_box_projection():
    assert view.material_route("rivendell_stone") == {"set": "t_rivendell_wall_stone_a", "mode": "box"}
    assert view.material_route("rivendell_rubble") == {"set": "t_rivendell_wall_rubble_a", "mode": "box"}


def test_blockout_trim_takes_the_plain_strip_of_the_trim_sheet():
    route = view.material_route("rivendell_trim")
    assert route["set"] == "t_rivendell_wall_trim_a"
    assert route["mode"] == "box_strip"
    assert route["strip"] == view.STRIPS["S1"]


def test_blender_duplicate_suffix_and_case_do_not_change_the_route():
    assert view.material_route("Rivendell_Stone.001") == view.material_route("rivendell_stone")


def test_a_material_with_no_set_gets_a_flat_measured_colour():
    route = view.material_route("rivendell_slate")
    assert route == {"mode": "flat", "color": "#34405f"}


def test_an_unknown_material_is_left_alone():
    assert view.material_route("m_gondor_bricks_large_a_dirty") == {"mode": "keep"}


def test_strips_cover_the_sheet_in_order_without_gaps():
    names = ["S1", "S2", "S3", "S4", "S5"]
    assert view.STRIPS["S1"][0] == 0.0
    assert view.STRIPS["S5"][1] == 1.0
    for a, b in zip(names, names[1:]):
        assert view.STRIPS[a][1] == view.STRIPS[b][0]


def test_texture_files_finds_the_three_maps_and_reports_a_missing_one(tmp_path):
    for suffix in ("_d", "_n"):
        (tmp_path / f"t_rivendell_wall_stone_a{suffix}.png").write_bytes(b"x")
    found = view.texture_files(str(tmp_path), "t_rivendell_wall_stone_a")
    assert found["d"].endswith("t_rivendell_wall_stone_a_d.png")
    assert found["n"].endswith("t_rivendell_wall_stone_a_n.png")
    assert found["s"] is None


def test_row_offsets_put_pieces_side_by_side_with_the_gap():
    boxes = [((-10.0, -3.0, 0.0), (10.0, 3.0, 14.0)), ((-8.0, -6.0, 0.0), (8.0, 6.0, 20.0))]
    offsets = view.row_offsets(boxes, gap=4.0)
    first_right = boxes[0][1][0] + offsets[0]
    second_left = boxes[1][0][0] + offsets[1]
    assert second_left - first_right == pytest.approx(4.0)


def test_row_offsets_centre_the_row_on_zero():
    boxes = [((-10.0, 0, 0), (10.0, 1, 1)), ((-10.0, 0, 0), (10.0, 1, 1))]
    offsets = view.row_offsets(boxes, gap=4.0)
    left = boxes[0][0][0] + offsets[0]
    right = boxes[1][1][0] + offsets[1]
    assert left == pytest.approx(-right)


def test_row_offsets_of_no_pieces_is_empty():
    assert view.row_offsets([], gap=4.0) == []


def test_the_density_preview_scales_stone_and_rubble_but_never_the_trim_sheet():
    assert view.uv_scale_for("t_rivendell_wall_stone_a", 0.6) == 0.6
    assert view.uv_scale_for("t_rivendell_wall_rubble_a", 0.6) == 0.6
    assert view.uv_scale_for("t_rivendell_wall_trim_a", 0.6) == 1.0


def test_parse_args_reads_the_density_preview_factor_and_defaults_to_one():
    base = ["--piece", "a.fbx", "--tex", "t", "--out", "o"]
    assert view.parse_args(base).masonry_uv_scale == 1.0
    assert view.parse_args(base + ["--masonry-uv-scale", "0.6"]).masonry_uv_scale == 0.6


def test_parse_args_reads_the_green_sense_of_the_normal_maps_and_defaults_to_up():
    base = ["--piece", "a.fbx", "--tex", "t", "--out", "o"]
    assert view.parse_args(base).normal_green == "up"
    assert view.parse_args(base + ["--normal-green", "down"]).normal_green == "down"
    with pytest.raises(SystemExit):
        view.parse_args(base + ["--normal-green", "sideways"])


def test_parse_piece_reads_a_label_and_a_path():
    assert view.parse_piece("wall=E:/a b/wall.fbx") == ("wall", "E:/a b/wall.fbx")


def test_parse_piece_without_a_label_uses_the_file_stem():
    assert view.parse_piece("E:/x/rivendell_castle_wall_20m_a.fbx") == (
        "rivendell_castle_wall_20m_a", "E:/x/rivendell_castle_wall_20m_a.fbx")


# ------------------------------------------------------------------ the low raking sun

BASE = ["--piece", "a.fbx", "--tex", "t", "--out", "o"]


def test_parse_args_sun_defaults_to_the_present_light_only_and_accepts_low():
    assert view.parse_args(BASE).sun == "main"
    assert view.parse_args(BASE + ["--sun", "low"]).sun == "low"
    with pytest.raises(SystemExit):
        view.parse_args(BASE + ["--sun", "midnight"])


def test_low_sun_direction_is_a_unit_vector_at_eighteen_degrees_above_the_horizon():
    d = view.low_sun_direction()
    assert math.sqrt(sum(c * c for c in d)) == pytest.approx(1.0)
    assert math.degrees(math.asin(d[2])) == pytest.approx(18.0)


def test_low_sun_comes_along_the_wall_at_twenty_five_degrees_to_its_face():
    x, y, z = view.low_sun_direction()
    # The wall face is the XZ plane and the outside is +Y. The horizontal part of the light makes
    # 25 degrees with the face, so its +Y share is sin(25) of the horizontal length.
    horizontal = math.hypot(x, y)
    assert math.degrees(math.asin(y / horizontal)) == pytest.approx(25.0)
    assert y > 0.0  # it lights the outside


def test_low_sun_can_come_from_either_end_of_the_wall():
    left = view.low_sun_direction(from_x=-1)
    right = view.low_sun_direction(from_x=1)
    assert left[0] < 0.0 < right[0]
    assert left[1] == pytest.approx(right[1])
    assert left[2] == pytest.approx(right[2])


# ------------------------------------------------------------------ the control piece

def _dump(tmp_path):
    """A small stand-in for gondor_materials_dump.json and gondor_bricks_dump.json, with the files."""
    materials = [
        {"item": "m_gondor_bricks_large_a_dirty", "slots": "0=g-d;1=g-d;2=g-n;4=g-s"},
        {"item": "gondor_bricks_a_dirty_mat", "slots": "0=h-d;2=h-n"},
        {"item": "m_gondor_stone_trim_a1", "slots": "0=unknown-d;2=unknown-n;4=unknown-s"},
        {"item": "gondor_tiles_a_dirty_mat", "slots": "0=lost-d;2=lost-n"},
    ]
    textures = [
        {"guid": "g-d", "source": "$BASE/Modules/TAOM_Map/AssetSources/Scenes/Gondor/textures/t_large_dirty_d2.png"},
        {"guid": "g-n", "source": "$BASE/Modules/TAOM_Map/AssetSources/Scenes/Gondor/textures/t_large_dirty_n.png"},
        {"guid": "g-s", "source": "$BASE/Modules/TAOM_Map/AssetSources/Scenes/Gondor/textures/t_large_dirty_s.png"},
        {"guid": "h-d", "source": "$BASE/x/t_small_d.png"},
        {"guid": "h-n", "source": "$BASE/x/t_small_n.png"},
    ]
    for name in ("t_large_dirty_d2", "t_large_dirty_n", "t_large_dirty_s", "t_small_d", "t_small_n",
                 "t_gondor_stone_trim_a1_d", "t_gondor_stone_trim_a1_n", "t_gondor_stone_trim_a1_s"):
        (tmp_path / (name + ".png")).write_bytes(b"x")
    return materials, textures


def test_parse_slots_reads_the_slot_numbers_and_their_texture_ids():
    assert view.parse_slots("0=aa;2=bb;4=cc") == {"0": "aa", "2": "bb", "4": "cc"}
    assert view.parse_slots("") == {}


def test_a_control_material_finds_its_textures_through_the_dump(tmp_path):
    materials, textures = _dump(tmp_path)
    found = view.control_textures("m_gondor_bricks_large_a_dirty", materials, textures, str(tmp_path))
    assert found["route"] == "dump"
    assert os.path.basename(found["d"]) == "t_large_dirty_d2.png"
    assert os.path.basename(found["n"]) == "t_large_dirty_n.png"
    assert os.path.basename(found["s"]) == "t_large_dirty_s.png"


def test_the_material_name_of_the_fbx_matches_in_any_case_and_without_the_duplicate_suffix(tmp_path):
    materials, textures = _dump(tmp_path)
    found = view.control_textures("M_Gondor_Bricks_Large_A_Dirty.001", materials, textures, str(tmp_path))
    assert found["route"] == "dump"
    assert os.path.basename(found["d"]) == "t_large_dirty_d2.png"


def test_a_slot_that_the_material_does_not_have_stays_none(tmp_path):
    materials, textures = _dump(tmp_path)
    found = view.control_textures("gondor_bricks_a_dirty_mat", materials, textures, str(tmp_path))
    assert os.path.basename(found["d"]) == "t_small_d.png"
    assert found["s"] is None


def test_a_texture_id_that_the_texture_dump_lacks_falls_back_to_the_file_named_after_the_material(tmp_path):
    materials, textures = _dump(tmp_path)
    found = view.control_textures("M_Gondor_Stone_Trim_A1", materials, textures, str(tmp_path))
    assert found["route"] == "name"
    assert os.path.basename(found["d"]) == "t_gondor_stone_trim_a1_d.png"
    assert os.path.basename(found["s"]) == "t_gondor_stone_trim_a1_s.png"


def test_a_material_with_no_texture_anywhere_has_route_none(tmp_path):
    materials, textures = _dump(tmp_path)
    found = view.control_textures("gondor_tiles_a_dirty_mat", materials, textures, str(tmp_path))
    assert found == {"d": None, "n": None, "s": None, "route": "none"}
    assert view.control_textures("not_in_the_dump", materials, textures, str(tmp_path))["route"] == "none"


def test_a_dump_texture_that_is_not_in_the_folder_is_not_used(tmp_path):
    materials, textures = _dump(tmp_path)
    os.remove(tmp_path / "t_large_dirty_n.png")
    found = view.control_textures("m_gondor_bricks_large_a_dirty", materials, textures, str(tmp_path))
    assert found["n"] is None
    assert found["d"] is not None


def test_decal_materials_are_recognised_by_name():
    assert view.is_decal("corner_dirt_decal")
    assert view.is_decal("decal_piled_leaks_a_mat")
    assert not view.is_decal("M_Gondor_Stone_Trim_A1")


def test_parse_args_control_options_and_defaults():
    got = view.parse_args(BASE)
    assert got.control is None
    got = view.parse_args(BASE + ["--control", "c.fbx", "--control-tex", "ct"])
    assert got.control == "c.fbx" and got.control_tex == "ct"
    assert got.control_lift == 0.0
    assert got.control_normal_green == "down"
    assert got.control_gap == 3.0
    got = view.parse_args(BASE + ["--control", "c.fbx", "--control-tex", "ct", "--control-lift", "-3",
                                  "--control-normal-green", "up", "--control-gap", "8"])
    assert got.control_lift == -3.0 and got.control_normal_green == "up" and got.control_gap == 8.0


def test_a_control_without_its_texture_folder_is_an_error():
    with pytest.raises(SystemExit):
        view.parse_args(BASE + ["--control", "c.fbx"])


def test_the_control_stands_at_the_plus_x_end_of_the_row_with_the_gap_and_the_lift():
    row = ((-20.0, -2.6, 0.0), (20.0, 3.5, 14.4))
    control = ((-10.0, -3.5, -20.0), (10.0, 4.2, 17.9))
    dx, dy, dz = view.control_offset(row, control, gap=6.0, lift=-3.0)
    assert control[0][0] + dx == pytest.approx(20.0 + 6.0)
    assert dy == 0.0  # both pieces keep their own frame: +Y is the outer face
    assert dz == -3.0


def test_the_eye_level_view_looks_at_the_faces_from_the_outside_at_standing_height():
    loc, target = view.eye_view(x=21.0, face_y=4.2, standoff=9.0, eye_height=1.7)
    assert loc == (21.0, 13.2, 1.7)
    assert target == (21.0, 4.2, 1.7)



def test_the_eye_level_standoff_shows_the_gap_and_three_metres_of_each_face():
    width = 3.0 + 2 * 3.0
    assert view.eye_standoff(gap=3.0) == pytest.approx(width / (2.0 * math.tan(view.FOV / 2.0)))
    assert view.eye_standoff(gap=6.0) > view.eye_standoff(gap=3.0)
    assert view.eye_standoff(gap=3.0, face_shown=1.5) < view.eye_standoff(gap=3.0)


# ------------------------------------------------------------------ vertex colour

def test_parse_args_vertex_colour_defaults_to_on():
    assert view.parse_args(BASE).vertex_colour == "on"
    assert view.parse_args(BASE + ["--vertex-colour", "off"]).vertex_colour == "off"
    with pytest.raises(SystemExit):
        view.parse_args(BASE + ["--vertex-colour", "maybe"])


def test_vertex_colour_multiplies_only_when_it_is_on_and_the_mesh_has_the_attribute():
    assert view.vertex_colour_wanted("on", has_attribute=True)
    assert not view.vertex_colour_wanted("on", has_attribute=False)
    assert not view.vertex_colour_wanted("off", has_attribute=True)


# ------------------------------------------------------------------ detail normal

def test_parse_args_detail_normal_options_and_defaults():
    got = view.parse_args(BASE)
    assert got.detail_normal is None
    assert got.detail_scale == 1.0  # the engine's own default for DetailnormalScale
    got = view.parse_args(BASE + ["--detail-normal", "d.png", "--detail-scale", "8"])
    assert got.detail_normal == "d.png" and got.detail_scale == 8.0


def test_the_detail_normal_goes_on_stone_and_rubble_but_not_on_the_trim_sheet():
    assert view.uses_detail("t_rivendell_wall_stone_a")
    assert view.uses_detail("t_rivendell_wall_rubble_a")
    assert not view.uses_detail("t_rivendell_wall_trim_a")


def test_the_detail_uv_is_the_base_uv_times_the_scale():
    # The base uv already carries the density preview factor, so the two multiply.
    assert view.detail_uv_scale(masonry_factor=0.6, detail_scale=8.0) == pytest.approx(4.8)
    assert view.detail_uv_scale(masonry_factor=1.0, detail_scale=1.0) == 1.0


# ------------------------------------------------------------------ the far front view

def test_the_far_front_view_stands_twice_as_far():
    assert view.FAR_FACTOR == 2.0
    assert view.scaled_distance(70.0, view.FAR_FACTOR) == 140.0
    assert view.scaled_distance(70.0, 1.0) == 70.0


def test_the_ground_reaches_beyond_the_far_camera():
    # 40 m of wall, 7 m deep, 14.4 m high: the ground must lie under the far camera too.
    box = ((-20.0, -2.6, 0.0), (20.0, 3.5, 14.4))
    half = math.sqrt(sum((box[1][k] - box[0][k]) ** 2 for k in range(3))) / 2.0
    far = view.scaled_distance(view.perspective_distance(half, view.FOV), view.FAR_FACTOR)
    assert view.ground_span(box) >= 2.0 * far
    assert view.ground_span(box) >= 40.0 * 3.0 + 60.0  # never smaller than before


# ------------------------------------------------------------------ face_close view (round 2b)
def test_the_face_close_view_stands_in_front_of_the_bay_field_above_the_plinth():
    loc, target = view.face_close_view(x=21.0)
    assert loc == (21.0, view.FACE_Y + view.FACE_CLOSE_STANDOFF, view.PLINTH_TOP_Z + view.EYE_HEIGHT)
    assert target == (21.0, view.FACE_Y, view.PLINTH_TOP_Z + view.EYE_HEIGHT)
    assert view.FACE_CLOSE_STANDOFF == 2.5


def test_the_face_close_view_can_stand_on_another_face_and_base():
    loc, target = view.face_close_view(x=0.0, face_y=4.0, base_z=0.0, standoff=1.0)
    assert loc == (0.0, 5.0, view.EYE_HEIGHT)
    assert target == (0.0, 4.0, view.EYE_HEIGHT)


def test_the_vertex_colour_multiplies_with_its_stored_value_not_the_linear_one():
    # Blender hands a byte colour of 0.80 to a material as about 0.604 (linear). The engine's shader
    # multiplies the diffuse by the stored value, so the preview turns the linear value back.
    assert 0.604 ** view.VERTEX_COLOUR_GAMMA == pytest.approx(0.80, abs=0.01)
    assert 1.0 ** view.VERTEX_COLOUR_GAMMA == 1.0


# ------------------------------------------------------------------ round 4: two more sets, the sun face view, the pair face view
ROOF = "t_rivendell_roof_slate_a"
HEAP = "t_rivendell_wall_heap_a"
RUBBLE = "t_rivendell_wall_rubble_a"


def _touch_set(folder, name):
    for suffix in ("_d", "_n", "_s"):
        (folder / f"{name}{suffix}.png").write_bytes(b"x")


def test_the_roof_slate_and_the_heap_are_final_sets_dressed_through_the_uvs():
    assert view.material_route(ROOF) == {"set": ROOF, "mode": "uv"}
    assert view.material_route(HEAP + ".001") == {"set": HEAP, "mode": "uv"}
    assert ROOF in view.FINAL_SETS and HEAP in view.FINAL_SETS


def test_the_detail_normal_and_the_uv_scale_treat_the_new_sets_like_the_stone_set():
    for name in (ROOF, HEAP):
        assert view.uses_detail(name)
        assert view.uv_scale_for(name, 0.6) == 0.6


def test_a_set_that_is_in_the_folder_is_used_as_it_is(tmp_path):
    _touch_set(tmp_path, ROOF)
    got = view.resolve_set(str(tmp_path), ROOF)
    assert got["mode"] == "files"
    assert got["note"] is None
    assert got["files"]["d"].endswith(ROOF + "_d.png")


def test_the_roof_slate_without_files_falls_back_to_the_flat_colour(tmp_path):
    got = view.resolve_set(str(tmp_path), ROOF)
    assert got["mode"] == "flat"
    assert got["color"] == "#34405f"
    assert ROOF in got["note"] and "#34405f" in got["note"]


def test_the_heap_without_files_falls_back_to_the_rubble_files(tmp_path):
    _touch_set(tmp_path, RUBBLE)
    got = view.resolve_set(str(tmp_path), HEAP)
    assert got["mode"] == "files"
    assert got["files"]["d"].endswith(RUBBLE + "_d.png")
    assert HEAP in got["note"] and RUBBLE in got["note"]


def test_the_heap_with_no_rubble_either_has_no_files(tmp_path):
    got = view.resolve_set(str(tmp_path), HEAP)
    assert got["mode"] == "none"
    assert got["note"]


def test_a_present_set_without_a_fallback_and_without_files_has_no_files(tmp_path):
    assert view.resolve_set(str(tmp_path), "t_rivendell_wall_stone_a")["mode"] == "none"


def test_the_main_sun_meets_the_outer_face_from_the_front_and_above():
    d = view.main_sun_direction()
    assert math.sqrt(sum(c * c for c in d)) == pytest.approx(1.0)
    assert d[1] > 0.0 and d[2] > 0.0  # toward +Y (the outer face) and up
    assert view.face_cosine(d) == pytest.approx(d[1])
    assert math.degrees(math.asin(d[2])) == pytest.approx(38.0, abs=0.01)


def test_the_face_sun_is_forty_degrees_up_and_forty_five_from_the_side():
    d = view.face_sun_direction()
    assert math.sqrt(sum(c * c for c in d)) == pytest.approx(1.0)
    assert math.degrees(math.asin(d[2])) == pytest.approx(40.0)
    assert math.degrees(math.atan2(-d[0], d[1])) == pytest.approx(45.0)
    assert view.face_cosine(d) == pytest.approx(math.cos(math.radians(40)) * math.cos(math.radians(45)))


def test_the_face_sun_can_come_from_the_other_end():
    assert view.face_sun_direction(from_x=1)[0] > 0.0


def test_the_pair_face_view_stands_six_metres_in_front_at_mid_bay_height():
    loc, target = view.pair_face_view(x=1.0)
    assert loc == (1.0, view.FACE_Y + 6.0, 5.5)
    assert target == (1.0, view.FACE_Y, 5.5)
    assert view.PAIR_FACE_STANDOFF == 6.0 and view.PAIR_FACE_Z == 5.5



# ------------------------------------------------------------------ round 4: two more sets, the sun face view, the pair face view
ROOF = "t_rivendell_roof_slate_a"
HEAP = "t_rivendell_wall_heap_a"
RUBBLE = "t_rivendell_wall_rubble_a"


def _touch_set(folder, name):
    for suffix in ("_d", "_n", "_s"):
        (folder / f"{name}{suffix}.png").write_bytes(b"x")


def test_the_roof_slate_and_the_heap_are_final_sets_dressed_through_the_uvs():
    assert view.material_route(ROOF) == {"set": ROOF, "mode": "uv"}
    assert view.material_route(HEAP + ".001") == {"set": HEAP, "mode": "uv"}
    assert ROOF in view.FINAL_SETS and HEAP in view.FINAL_SETS


def test_the_detail_normal_and_the_uv_scale_treat_the_new_sets_like_the_stone_set():
    for name in (ROOF, HEAP):
        assert view.uses_detail(name)
        assert view.uv_scale_for(name, 0.6) == 0.6


def test_a_set_that_is_in_the_folder_is_used_as_it_is(tmp_path):
    _touch_set(tmp_path, ROOF)
    got = view.resolve_set(str(tmp_path), ROOF)
    assert got["mode"] == "files"
    assert got["note"] is None
    assert got["files"]["d"].endswith(ROOF + "_d.png")


def test_the_roof_slate_without_files_falls_back_to_the_flat_colour(tmp_path):
    got = view.resolve_set(str(tmp_path), ROOF)
    assert got["mode"] == "flat"
    assert got["color"] == "#34405f"
    assert ROOF in got["note"] and "#34405f" in got["note"]


def test_the_heap_without_files_falls_back_to_the_rubble_files(tmp_path):
    _touch_set(tmp_path, RUBBLE)
    got = view.resolve_set(str(tmp_path), HEAP)
    assert got["mode"] == "files"
    assert got["files"]["d"].endswith(RUBBLE + "_d.png")
    assert HEAP in got["note"] and RUBBLE in got["note"]


def test_the_heap_with_no_rubble_either_has_no_files(tmp_path):
    got = view.resolve_set(str(tmp_path), HEAP)
    assert got["mode"] == "none"
    assert got["note"]


def test_a_present_set_without_a_fallback_and_without_files_has_no_files(tmp_path):
    assert view.resolve_set(str(tmp_path), "t_rivendell_wall_stone_a")["mode"] == "none"


def test_the_main_sun_meets_the_outer_face_from_the_front_and_above():
    d = view.main_sun_direction()
    assert math.sqrt(sum(c * c for c in d)) == pytest.approx(1.0)
    assert d[1] > 0.0 and d[2] > 0.0  # toward +Y (the outer face) and up
    assert view.face_cosine(d) == pytest.approx(d[1])
    assert math.degrees(math.asin(d[2])) == pytest.approx(38.0, abs=0.01)


def test_the_face_sun_is_forty_degrees_up_and_forty_five_from_the_side():
    d = view.face_sun_direction()
    assert math.sqrt(sum(c * c for c in d)) == pytest.approx(1.0)
    assert math.degrees(math.asin(d[2])) == pytest.approx(40.0)
    assert math.degrees(math.atan2(-d[0], d[1])) == pytest.approx(45.0)
    assert view.face_cosine(d) == pytest.approx(math.cos(math.radians(40)) * math.cos(math.radians(45)))


def test_the_face_sun_can_come_from_the_other_end():
    assert view.face_sun_direction(from_x=1)[0] > 0.0


def test_the_pair_face_view_stands_six_metres_in_front_at_mid_bay_height():
    loc, target = view.pair_face_view(x=1.0)
    assert loc == (1.0, view.FACE_Y + 6.0, 5.5)
    assert target == (1.0, view.FACE_Y, 5.5)
    assert view.PAIR_FACE_STANDOFF == 6.0 and view.PAIR_FACE_Z == 5.5


# ------------------------------------------------------------------ round 5: close piece, grazing and warm views
def test_close_piece_defaults_to_the_first_solid_wall_20m_a():
    pieces = [("g", "x/rivendell_gatehouse_a.fbx"), ("w", "x/rivendell_castle_wall_20m_a.fbx"),
              ("v", "x/rivendell_castle_wall_20m_a_other.fbx")]
    assert view.close_piece_index(pieces) == 1


def test_close_piece_skips_a_dest_wall():
    pieces = [("d", "x/rivendell_castle_wall_20m_a_dest.fbx"), ("w", "x/rivendell_castle_wall_20m_a.fbx")]
    assert view.close_piece_index(pieces) == 1


def test_close_piece_without_a_solid_wall_is_the_first_piece():
    pieces = [("t", "x/tower.fbx"), ("g", "x/gate.fbx")]
    assert view.close_piece_index(pieces) == 0


def test_close_piece_label_wins_and_an_unknown_label_is_an_error():
    pieces = [("t", "x/tower.fbx"), ("w", "x/rivendell_castle_wall_20m_a.fbx")]
    assert view.close_piece_index(pieces, "t") == 0
    with pytest.raises(ValueError):
        view.close_piece_index(pieces, "nope")


def test_parse_args_close_piece_defaults_to_none():
    base = ["--piece", "a=x.fbx", "--tex", "t", "--out", "o"]
    assert view.parse_args(base).close_piece is None
    assert view.parse_args(base + ["--close-piece", "a"]).close_piece == "a"


def test_graze_sun_meets_the_face_at_cosine_point_two_thirty_degrees_up_from_the_low_x_side():
    d = view.graze_sun_direction()
    assert math.sqrt(sum(c * c for c in d)) == pytest.approx(1.0)
    assert view.face_cosine(d) == pytest.approx(0.2)
    assert math.degrees(math.asin(d[2])) == pytest.approx(30.0)
    assert d[0] < 0.0


def test_warm_light_is_a_5500_k_sun_and_a_sky_of_point_three():
    assert view.WARM_SUN_COLOUR == (1.0, 0.93, 0.84)
    assert view.WARM_SKY_STRENGTH == 0.3
