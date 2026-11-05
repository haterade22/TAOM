"""Placeholder preservation check: lowercase engine tokens such as {newline}, and conversation animation tags such as [ib:closed]."""

import os
import sys
import unittest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import translate_with_claude as twc  # noqa: E402


class PlaceholderTests(unittest.TestCase):
    def test_dropped_newline_is_rejected(self):
        ok, why = twc.placeholders_match("Line one.{newline}Line two.", "Ligne un. Ligne deux.")
        self.assertFalse(ok)
        self.assertIn("{newline}", why)

    def test_kept_newline_passes(self):
        ok, _ = twc.placeholders_match("Line one.{newline}Line two.", "Ligne un.{newline}Ligne deux.")
        self.assertTrue(ok)

    def test_mixed_case_token_is_checked(self):
        ok, _ = twc.placeholders_match("{InvasionScenarioFactionId} rules.", "Il domine.")
        self.assertFalse(ok)

    def test_uppercase_still_checked(self):
        ok, _ = twc.placeholders_match("Hi {FIRSTNAME}", "Salut")
        self.assertFalse(ok)

    def test_key_ids_and_conditionals_not_counted(self):
        counts = twc.extract_placeholders("{=abc_key}Text {?COND}a{?}b{\\?}")
        self.assertEqual(
            {k: v for k, v in counts.items() if not k.startswith("__")}, {}
        )
        self.assertEqual(counts["__cond_open__"], 1)

    def test_expression_conditional_head_is_counted(self):
        counts = twc.extract_placeholders(r"{?DAYS>1}days{?}day{\?}")
        self.assertEqual(counts["__cond_open__"], 1)
        ok, _ = twc.placeholders_match(r"{?DAYS>1}days{?}day{\?}", r"days{?}day{\?}")
        self.assertFalse(ok)

    def test_dropped_expression_tag_is_rejected(self):
        ok, why = twc.placeholders_match("Go on.[ib:closed][rf:convo_bemused]", "Geh weiter.[rf:convo_bemused]")
        self.assertFalse(ok)
        self.assertIn("[ib:closed]", why)

    def test_changed_expression_tag_is_rejected(self):
        ok, _ = twc.placeholders_match("Go on.[ib:closed]", "Geh weiter.[ib:geschlossen]")
        self.assertFalse(ok)

    def test_kept_expression_tags_pass(self):
        ok, _ = twc.placeholders_match("Go on.[ib:closed][if:convo_angry]", "Geh weiter.[ib:closed][if:convo_angry]")
        self.assertTrue(ok)

    def test_moved_expression_tag_passes(self):
        # The tags are counted, not positioned: a language may need one at another point of the sentence.
        ok, _ = twc.placeholders_match("Go on.[ib:closed] Quickly.", "[ib:closed]Geh weiter. Schnell.")
        self.assertTrue(ok)

    def test_culture_tag_is_not_an_expression_tag(self):
        counts = twc.extract_placeholders("[Gondor] Sword [rb:x]")
        self.assertNotIn("[Gondor]", counts)
        self.assertEqual(counts["[rb:x]"], 1)


if __name__ == "__main__":
    unittest.main()
