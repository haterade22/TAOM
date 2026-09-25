"""bind_hill_troll_action_set.py: the standalone as_hill_troll_warrior body from Native's as_human_warrior, every
code kept, Fab clips first where the cave troll rules bind one, the retargeted human clip where the index has it,
the human clip inherited otherwise; byte-faithful replacement of that one set's body."""
import json
import os
import sys
import xml.etree.ElementTree as ET

import pytest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))
import bind_hill_troll_action_set as bh  # noqa: E402

NATIVE = """<?xml version="1.0" encoding="utf-8"?>
<action_sets>
\t<action_set
\t\tid="as_human_warrior"
\t\tskeleton="human_skeleton"
\t\tmovement_system="bipedal">
\t\t<action type="act_walk_forward_unarmed" animation="walk_forward_unarmed" />
\t\t<action type="act_idle_2h_1" animation="troop_stand_2h_1" alternative_group="idle_2h" />
\t\t<action type="act_idle_2h_1" animation="troop_stand_2h_1_b" alternative_group="idle_2h_b" />
\t\t<action type="act_ready_slashright_2h" animation="ready_slashright_2h" />
\t\t<!-- <action type="act_disabled_thing" animation="disabled_thing" /> -->
\t\t<action type="act_blocked_slashright_2h" animation="blocked_slashright_2h" />
\t\t<action type="act_swim_idle" animation="swim_idle" />
\t\t<action type="act_strike_chest_front" animation="strike_chest_front" />
\t</action_set>
</action_sets>
"""

LIVE = ("﻿<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n<action_sets>\r\n"
        "\t<action_set id=\"as_cave_troll_warrior\" skeleton=\"human_skeleton\" movement_system=\"bipedal\">\r\n"
        "\t\t<action type=\"act_walk_forward_unarmed\" animation=\"anim_troll_walk1\" />\r\n"
        "\t</action_set>\r\n"
        "\t<action_set id=\"as_hill_troll_warrior\" skeleton=\"troll_skeleton_a\" movement_system=\"bipedal\">\r\n"
        "\t\t<action\r\n\t\t\ttype=\"act_walk_forward_unarmed\"\r\n\t\t\tanimation=\"walk_forward_unarmed\" />\r\n"
        "\t</action_set>\r\n"
        "</action_sets>\r\n")

HUMAN = {"anim_hill_troll_ready_slashright_2h", "anim_hill_troll_blocked_slashright_2h",
         "anim_hill_troll_strike_chest_front", "anim_hill_troll_troop_stand_2h_1"}
FAB = {"anim_hill_troll_walk1", "anim_hill_troll_combat_idle1", "anim_hill_troll_combat_hit_front1"}


def actions():
    return bh.human_actions(NATIVE)


def test_fab_clip_wins_where_the_cave_troll_rules_bind_one():
    a = {"type": "act_walk_forward_unarmed", "animation": "walk_forward_unarmed"}
    assert bh.bind_hill("act_walk_forward_unarmed", a, HUMAN, FAB) == ("anim_hill_troll_walk1", "fab")
    a = {"type": "act_strike_chest_front", "animation": "strike_chest_front"}
    assert bh.bind_hill("act_strike_chest_front", a, HUMAN, FAB) == ("anim_hill_troll_combat_hit_front1", "fab")


def test_retargeted_human_clip_when_the_index_has_it():
    a = {"type": "act_ready_slashright_2h", "animation": "ready_slashright_2h"}
    assert bh.bind_hill("act_ready_slashright_2h", a, HUMAN, FAB) == ("anim_hill_troll_ready_slashright_2h", "human")


def test_fab_armed_idle_beats_the_retargeted_human_idle():
    a = {"type": "act_idle_2h_1", "animation": "troop_stand_2h_1", "alternative_group": "idle_2h"}
    assert bh.bind_hill("act_idle_2h_1", a, HUMAN, FAB) == ("anim_hill_troll_combat_idle1", "fab")


def test_inherits_the_human_clip_when_nothing_else_exists():
    a = {"type": "act_swim_idle", "animation": "swim_idle"}
    assert bh.bind_hill("act_swim_idle", a, HUMAN, FAB) == ("swim_idle", "inherited")


def test_fab_rule_without_the_fab_clip_falls_through():
    a = {"type": "act_walk_forward_unarmed", "animation": "walk_forward_unarmed"}
    assert bh.bind_hill("act_walk_forward_unarmed", a, HUMAN, set()) == ("walk_forward_unarmed", "inherited")


def test_body_keeps_every_active_node_and_its_other_attributes():
    lines, counts = bh.build_body(actions(), HUMAN, FAB)
    types = [ln.split('type="')[1].split('"')[0] for ln in lines]
    assert types == ["act_walk_forward_unarmed", "act_idle_2h_1", "act_idle_2h_1", "act_ready_slashright_2h",
                     "act_blocked_slashright_2h", "act_swim_idle", "act_strike_chest_front"]
    idles = [ln for ln in lines if 'act_idle_2h_1' in ln]
    assert 'alternative_group="idle_2h"' in idles[0] and 'alternative_group="idle_2h_b"' in idles[1]
    assert all('animation="anim_hill_troll_combat_idle1"' in ln for ln in idles)
    assert "act_disabled_thing" not in " ".join(lines)          # commented out in Native: not an active code
    assert counts == {"fab": 4, "human": 2, "inherited": 1}


def test_replace_touches_only_the_hill_troll_body_and_keeps_bom_and_crlf():
    lines, _ = bh.build_body(actions(), HUMAN, FAB)
    new_text, old_count = bh.replace_body(LIVE, lines, "\r\n")
    assert old_count == 1
    assert new_text.startswith("﻿") and "\n" not in new_text.replace("\r\n", "")
    cave = new_text.index('id="as_cave_troll_warrior"'), new_text.index('id="as_hill_troll_warrior"')
    assert new_text[cave[0]:cave[1]] == LIVE[LIVE.index('id="as_cave_troll_warrior"'):LIVE.index('id="as_hill_troll_warrior"')]
    root = ET.fromstring(new_text.lstrip("﻿").encode("utf-8"))
    hill = [s for s in root if s.get("id") == "as_hill_troll_warrior"][0]
    assert [a.get("type") for a in hill] == [ln.split('type="')[1].split('"')[0] for ln in lines]
    assert new_text.count("GENERATED by") == 1


def test_replace_is_idempotent():
    lines, _ = bh.build_body(actions(), HUMAN, FAB)
    once, _ = bh.replace_body(LIVE, lines, "\r\n")
    twice, _ = bh.replace_body(once, lines, "\r\n")
    assert once == twice


def test_available_clips_read_the_index_keys_and_the_fab_values(tmp_path):
    idx = tmp_path / "clips_index.json"
    idx.write_text(json.dumps({"ready_slashright_2h": {"master": "x"}, "stand_2h": {"master": "y"}}), encoding="utf-8-sig")
    names = tmp_path / "names.json"
    names.write_text(json.dumps({"_comment": "x", "cave_troll_free_walk_0": "anim_hill_troll_walk1"}), encoding="utf-8")
    human, fab = bh.available_clips([str(idx)], str(names))
    assert human == {"anim_hill_troll_ready_slashright_2h", "anim_hill_troll_stand_2h"}
    assert fab == {"anim_hill_troll_walk1"}


def test_main_dry_run_writes_nothing(tmp_path, capsys):
    live = tmp_path / "action_sets.xml"
    live.write_bytes(LIVE.encode("utf-8"))
    native = tmp_path / "native.xml"
    native.write_text(NATIVE, encoding="utf-8")
    idx = tmp_path / "clips_index.json"
    idx.write_text(json.dumps({k[len("anim_hill_troll_"):]: {} for k in HUMAN}), encoding="utf-8")
    names = tmp_path / "names.json"
    names.write_text(json.dumps({"a": "anim_hill_troll_walk1", "b": "anim_hill_troll_combat_idle1",
                                 "c": "anim_hill_troll_combat_hit_front1"}), encoding="utf-8")
    before = live.read_bytes()
    rc = bh.main(["--live", str(live), "--native", str(native), "--clips-index", str(idx), "--fab-names", str(names)])
    assert rc == 0 and live.read_bytes() == before
    out = capsys.readouterr().out
    assert "DRY RUN" in out and "fab" in out


if __name__ == "__main__":
    sys.exit(pytest.main([__file__, "-q"]))
