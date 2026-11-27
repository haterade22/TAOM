"""Tests for tools/oneoff/build_donor_pieces_page.py (the donor piece picture page)."""
import json
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "oneoff"))

import build_donor_pieces_page as page  # noqa: E402


def test_family_takes_the_first_matching_pattern():
    assert page.family_of("SM_Library_Tower_Walls") == "tower"
    assert page.family_of("SM_Gate_Wall") == "gate"
    assert page.family_of("SM_Exterior_Wall_Rect") == "wall"
    assert page.family_of("SM_Support_Pillar_Grouped") == "pillar"
    assert page.family_of("SM_Balcony_Railing") == "railing"


def test_family_of_an_unknown_name_is_other():
    assert page.family_of("SM_Boat") == "other"


def _work_folder(tmp_path, with_control):
    kit = tmp_path / "kit"
    kit.mkdir()
    parts = {"parts": [
        {"name": "SM_Tower", "size_m": [7.3, 7.29, 23.29], "tris": 5593,
         "materials": ["MI_Tower"], "uv_layer_count_max": 3},
        {"name": "SM_Boat", "tris": 10, "materials": []},
    ]}
    (kit / "kit_parts.json").write_text(json.dumps(parts), encoding="utf-8")
    if with_control:
        views = tmp_path / "control" / "wall_solid" / "views"
        views.mkdir(parents=True)
        (views / "3q_front_left.png").write_bytes(b"png")
        (tmp_path / "control" / "control_geometry.json").write_text("{}", encoding="utf-8")
    return tmp_path


def test_build_writes_one_card_row_per_kit_part(tmp_path):
    work = _work_folder(tmp_path, with_control=False)
    out = tmp_path / "page.html"
    assert page.build(str(work), str(out)) == 2
    text = out.read_text(encoding="utf-8")
    assert '"name": "SM_Tower"' in text
    assert "kit/thumbs/SM_Tower.png" in text
    assert "__ROWS__" not in text


def test_build_lists_only_the_control_pieces_that_have_a_render(tmp_path):
    work = _work_folder(tmp_path, with_control=True)
    out = tmp_path / "page.html"
    assert page.build(str(work), str(out)) == 3
    text = out.read_text(encoding="utf-8")
    assert "gondor_castle_wall_20m_l3_a" in text
    assert "gondor_castle_gatehouse_l1_a" not in text


def test_a_part_with_no_size_gets_zeros(tmp_path):
    work = _work_folder(tmp_path, with_control=False)
    rows = page.kit_rows(str(work))
    boat = [r for r in rows if r["name"] == "SM_Boat"][0]
    assert boat["size"] == [0, 0, 0]
    assert boat["family"] == "other"
