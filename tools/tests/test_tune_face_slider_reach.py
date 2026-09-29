"""Tests for tools/oneoff/tune_face_slider_reach.py: sliders shrink to the reference's reach, toward weight 0."""
import os
import sys
import unittest
import xml.etree.ElementTree as ET

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "oneoff"))
import tune_face_slider_reach as tf  # noqa: E402


def skin(mesh, keys):
    body = "".join('<deform_key id="%s" key_time_point="%d" key_min="%s" key_max="%s" name="x" group_id="1"/>'
                   % k for k in keys)
    return '<skin gender="0" face_meta_mesh="%s">%s<deform_key id="age" key_time_point="63" name="Age" group_id="0"/></skin>' % (mesh, body)


DOC = ("<base><race id='a'>" + skin("ref_head", [("wide", 0, "-1", "1"), ("tiny", 1, "0", "1"), ("bump", 2, "1", "1")])
       + "</race><race id='b'>" + skin("big_head", [("wide", 0, "-1", "1"), ("tiny", 1, "0", "1"), ("bump", 2, "1", "1")])
       + skin("big_head", [("wide", 0, "-0.5", "2")]) + "</race></base>")
REF = [0.010, 0.00005, 0.002]   # m of travel per channel at weight 1
BIG = [0.030, 0.004, 0.013]


def keys_of(text, mesh):
    out = []
    for s in ET.fromstring(text).iter("skin"):
        if s.get("face_meta_mesh") == mesh:
            out.append({k.get("id"): (k.get("key_min"), k.get("key_max")) for k in s.iter("deform_key")})
    return out


class PlanTests(unittest.TestCase):
    def test_a_slider_reaching_past_the_reference_shrinks_to_it_toward_zero(self):
        new, changes = tf.plan(DOC, "ref_head", REF, [("big_head", BIG)])
        first, second = keys_of(new, "big_head")
        self.assertEqual(first["wide"], ("-0.333", "0.333"))
        self.assertEqual(second["wide"], ("-0.083", "0.333"))   # both ends by one factor (1/6): weight 0 stays put

    def test_a_key_the_reference_barely_moves_is_left_alone(self):
        new, _ = tf.plan(DOC, "ref_head", REF, [("big_head", BIG)])
        self.assertEqual(keys_of(new, "big_head")[0]["tiny"], ("0", "1"))

    def test_zero_sets_a_key_to_no_weight(self):
        new, _ = tf.plan(DOC, "ref_head", REF, [("big_head", BIG)], zero=[("big_head", "bump")])
        self.assertEqual(keys_of(new, "big_head")[0]["bump"], ("0", "0"))

    def test_the_reference_and_unranged_keys_are_untouched(self):
        new, _ = tf.plan(DOC, "ref_head", REF, [("big_head", BIG)], zero=[("big_head", "bump")])
        self.assertEqual(keys_of(new, "ref_head")[0]["wide"], ("-1", "1"))
        self.assertEqual(keys_of(new, "big_head")[0]["age"], (None, None))

    def test_a_second_run_changes_nothing(self):
        once, _ = tf.plan(DOC, "ref_head", REF, [("big_head", BIG)], zero=[("big_head", "bump")])
        twice, changes = tf.plan(once, "ref_head", REF, [("big_head", BIG)], zero=[("big_head", "bump")])
        self.assertEqual((twice, changes), (once, []))

    def test_reach_zero_moves_the_nearer_end_to_zero(self):
        doc = "<base>" + skin("ref_head", [("ratio", 0, "0.5", "1")]) + skin("big_head", [("ratio", 0, "0.5", "1"), ("low", 1, "-1", "-0.2")]) + "</base>"
        new, _ = tf.plan(doc, "ref_head", [0.01, 0.01], [("big_head", [0.01, 0.01])],
                         reach_zero=[("big_head", "ratio"), ("big_head", "low")])
        k = keys_of(new, "big_head")[0]
        self.assertEqual((k["ratio"], k["low"]), (("0", "1"), ("-1", "0")))
        again, changes = tf.plan(new, "ref_head", [0.01, 0.01], [("big_head", [0.01, 0.01])],
                                 reach_zero=[("big_head", "ratio"), ("big_head", "low")])
        self.assertEqual((again, changes), (new, []))

    def test_set_pins_a_range_by_hand_and_wins_over_scaling(self):
        new, _ = tf.plan(DOC, "ref_head", REF, [("big_head", BIG)], set_ranges=[("big_head", "wide", 1.0, 0.5)])
        self.assertEqual(keys_of(new, "big_head")[0]["wide"], ("1", "0.5"))
        again, changes = tf.plan(new, "ref_head", REF, [("big_head", BIG)], set_ranges=[("big_head", "wide", 1.0, 0.5)])
        self.assertEqual((again, changes), (new, []))

    def test_set_parses_mesh_key_and_range(self):
        self.assertEqual(tf.parse_set("sk_dwarf_bm_f1_head:eye_depth=1:0.5"), ("sk_dwarf_bm_f1_head", "eye_depth", 1.0, 0.5))
        with self.assertRaises(SystemExit):
            tf.parse_set("no_equals_sign")

    def test_a_missing_face_mesh_is_refused(self):
        with self.assertRaises(SystemExit):
            tf.plan(DOC, "ref_head", REF, [("no_such_head", BIG)])
        with self.assertRaises(SystemExit):
            tf.plan(DOC, "no_such_ref", REF, [("big_head", BIG)])


if __name__ == "__main__":
    unittest.main()
