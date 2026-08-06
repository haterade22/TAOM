#!/usr/bin/env python3
"""Unit tests for the career passive re-tuner (tools/retune_career_health.py).

Run:  python -m unittest discover -s tools/tests -p "test_*.py"
  or:  python tools/tests/test_retune_career_health.py

These tests are SYNTHETIC — they build tiny in-memory `<Choice>` XML rather than reading the real
559 KB `taom_career_choices.xml`, so they encode the tool's contract independently of shipped data.

The bug class they exist for (deep review 2026-08-06, CRITICAL):
    A retune mapping whose OLD keys and NEW values overlap cannot be made idempotent per pip. The
    `troopdamage` profile maps {0.03,0.05,0.06,0.08,0.10,0.12,0.15,0.20} -> {0.02..0.08}, so four
    magnitudes are BOTH a key and a value. After a correct apply, a pip sitting on 0.05 is an
    already-retuned 0.10 — but `process_choices` saw 0.05 in the key set and would have shifted it
    again to 0.03. A second `--apply` would have silently double-shifted 71 of 105 pips across the
    magnitude, the English description, the source string, 12 language files and 12 caches.

    The fix is `already_applied()`, which decides at FILE level: if every pip already sits on a
    target value and none sits on an unambiguously-old key, the retune has run. These tests pin
    that for EVERY profile, so adding a new one with an overlapping mapping cannot regress it.
"""
import importlib.util
import os
import sys
import unittest

_REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
_TOOL = os.path.join(_REPO, "tools", "retune_career_health.py")

_spec = importlib.util.spec_from_file_location("retune_career_health", _TOOL)
retune = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(retune)


def choices_xml(effect_type, magnitudes, scale=1):
    """Build a minimal choices document with one <Choice> per magnitude."""
    rows = []
    for i, mag in enumerate(magnitudes):
        printed = retune._fmt(float(mag) * scale)
        pct = "%" if scale != 1 else ""
        word = "max health" if effect_type == "Health" else "troop damage"
        rows.append(
            f'  <Choice id="c{i}" type="Passive" '
            f'description="{{=k{i}}}+{printed}{pct} {word}.">\n'
            f'    <PassiveEffect type="{effect_type}" magnitude="{retune._fmt(mag)}" />\n'
            f'  </Choice>'
        )
    return "<Choices>\n" + "\n".join(rows) + "\n</Choices>\n"


class OverlapDetection(unittest.TestCase):
    def test_troopdamage_mapping_overlaps_and_health_does_not(self):
        """Pins the precondition that makes per-pip detection impossible."""
        retune.select_effect("health")
        self.assertEqual(set(), retune.overlapping_values(),
                         "health maps 25-100 -> 5-10; the sets must stay disjoint")

        retune.select_effect("troopdamage")
        self.assertEqual({0.03, 0.05, 0.06, 0.08}, retune.overlapping_values(),
                         "troopdamage overlaps — this is exactly why already_applied() is needed")


class AlreadyApplied(unittest.TestCase):
    def test_every_profile_detects_its_own_post_retune_output(self):
        """The regression pin: after a retune, every profile must report ALREADY APPLIED."""
        for name, profile in retune.EFFECTS.items():
            with self.subTest(effect=name):
                retune.select_effect(name)
                post = list(profile["mapping"].values())
                self.assertTrue(
                    retune.already_applied(choices_xml(profile["type"], post, profile["scale"])),
                    f"{name}: post-retune data must be recognised as applied, or a second "
                    f"--apply double-shifts it")

    def test_every_profile_does_not_false_positive_on_pre_retune_data(self):
        for name, profile in retune.EFFECTS.items():
            with self.subTest(effect=name):
                retune.select_effect(name)
                pre = list(profile["mapping"].keys())
                self.assertFalse(
                    retune.already_applied(choices_xml(profile["type"], pre, profile["scale"])),
                    f"{name}: un-retuned data must NOT be skipped")

    def test_troopdamage_overlap_values_alone_do_not_look_applied(self):
        """The subtle case: pips sitting ONLY on the four overlapping values.

        {0.03,0.05,0.06,0.08} are all both keys and targets, so this input is genuinely ambiguous.
        already_applied() resolves it as APPLIED (subset of targets, no unambiguously-old value),
        which is the safe direction — refusing to act cannot corrupt data, whereas acting on
        already-correct data can.
        """
        retune.select_effect("troopdamage")
        self.assertTrue(retune.already_applied(
            choices_xml("TroopDamage", [0.03, 0.05, 0.06, 0.08], 100)))

    def test_one_unambiguously_old_value_forces_a_run(self):
        retune.select_effect("troopdamage")
        self.assertFalse(retune.already_applied(
            choices_xml("TroopDamage", [0.03, 0.05, 0.20], 100)),
            "0.20 is a key that is not a target, so the retune has demonstrably not run")

    def test_empty_document_is_not_applied(self):
        retune.select_effect("health")
        self.assertFalse(retune.already_applied("<Choices></Choices>"))


class DescriptionRewriting(unittest.TestCase):
    def test_health_prints_the_flat_magnitude(self):
        retune.select_effect("health")
        out, changed = retune.rewrite_description("+75 max health.", 75, 9)
        self.assertTrue(changed)
        self.assertEqual("+9 max health.", out)

    def test_troopdamage_prints_magnitude_times_one_hundred(self):
        retune.select_effect("troopdamage")
        out, changed = retune.rewrite_description("+15% troop damage.", 0.15, 0.07)
        self.assertTrue(changed)
        self.assertEqual("+7% troop damage.", out)

    def test_health_profile_cannot_touch_mount_or_hero_healing_wording(self):
        """Anchoring is what stops a sibling effect's number being rewritten."""
        retune.select_effect("health")
        for text in ("+15% horse health.", "+15% hero health regeneration."):
            out, changed = retune.rewrite_description(text, 15, 9)
            self.assertFalse(changed, f"health profile must not match {text!r}")
            self.assertEqual(text, out)

    def test_troopdamage_profile_cannot_touch_the_hero_damage_pip(self):
        retune.select_effect("troopdamage")
        for text in ("+18% melee damage.", "+18% ranged damage."):
            out, changed = retune.rewrite_description(text, 0.18, 0.08)
            self.assertFalse(changed, f"troopdamage profile must not match {text!r}")
            self.assertEqual(text, out)

    def test_a_different_number_on_the_right_phrase_is_left_alone(self):
        retune.select_effect("health")
        out, changed = retune.rewrite_description("+30 max health.", 75, 9)
        self.assertFalse(changed)
        self.assertEqual("+30 max health.", out)


class Formatting(unittest.TestCase):
    def test_fmt_matches_how_the_files_author_numbers(self):
        self.assertEqual("5", retune._fmt(5))
        self.assertEqual("5", retune._fmt(5.0))
        self.assertEqual("0.02", retune._fmt(0.02))
        self.assertEqual("0.1", retune._fmt(0.1))

    def test_fmt_absorbs_float_multiplication_noise(self):
        """0.07 * 100 is 7.000000000000001 in IEEE-754; the description must read '7'."""
        retune.select_effect("troopdamage")
        self.assertEqual("7", retune._fmt(retune._desc_num(0.07)))


class ChoiceBlockMatching(unittest.TestCase):
    def test_choice_regex_does_not_match_choicegroup(self):
        text = '<ChoiceGroup tier="2"><Choice id="a"><PassiveEffect type="Health" magnitude="25" /></Choice></ChoiceGroup>'
        ids = [m.group(0) for m in retune._CHOICE.finditer(text)]
        self.assertEqual(1, len(ids))
        self.assertTrue(ids[0].startswith("<Choice "))


if __name__ == "__main__":
    unittest.main(verbosity=2)
