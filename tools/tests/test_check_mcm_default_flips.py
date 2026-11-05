#!/usr/bin/env python
"""tools/check_mcm_default_flips.py: a changed MCM default under an unchanged name fails (#1017).

Run:  python -m unittest tools.tests.test_check_mcm_default_flips
"""
import contextlib
import io
import json
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import check_mcm_default_flips as gate  # noqa: E402


def src(body, extra=""):
    return "public class S : AttributeGlobalSettings<S>\n{\n" + body + "\n}\n" + extra


def prop(name, value, typ="int"):
    return f"public {typ} {name} {{ get; set; }} = {value};"


class Repo:
    """A throwaway git repo with Main/S.cs (and optional extra files)."""

    def __init__(self, d):
        self.path = Path(d)
        self.git("init", "-q", "-b", "main")
        self.git("config", "commit.gpgsign", "false")
        self.git("config", "tag.gpgsign", "false")

    def git(self, *a):
        subprocess.run(["git", "-C", str(self.path), "-c", "user.name=t", "-c", "user.email=t@t", *a],
                       check=True, capture_output=True)

    def write(self, rel, text):
        p = self.path / rel
        p.parent.mkdir(parents=True, exist_ok=True)
        p.write_bytes(text.encode())

    def commit(self, msg, tag=None):
        self.git("add", "-A")
        self.git("commit", "-q", "--allow-empty", "-m", msg)
        if tag:
            self.git("tag", tag)


def run_main(*argv):
    out, err = io.StringIO(), io.StringIO()
    with contextlib.redirect_stdout(out), contextlib.redirect_stderr(err):
        code = gate.main(list(argv))
    return code, out.getvalue(), err.getvalue()


def settings_parse(text):
    return gate.parse_settings(text)[0]


class ParseTests(unittest.TestCase):
    def test_multiline_declaration_and_comments(self):
        got = settings_parse(src('[Attr("x", HintText = "a; b")]\npublic int A\n{ get; set; }\n    = 5; // was 5'))
        self.assertEqual(("ok", "5"), got[("S", "A")][:2])

    def test_commented_out_property_is_ignored(self):
        self.assertEqual({}, settings_parse(src("// public int A { get; set; } = 1;")))

    def test_float_suffix_normalised(self):
        a = settings_parse(src(prop("A", "1.5f", "float")))[("S", "A")]
        b = settings_parse(src(prop("A", "1.50F", "float")))[("S", "A")]
        self.assertEqual(a[:2], b[:2])

    def test_action_button_is_skipped(self):
        got, skipped = gate.parse_settings(src("public Action Go { get; set; } = null;\n" + prop("A", 1)))
        self.assertNotIn(("S", "Go"), got)
        self.assertEqual([], skipped)

    def test_unreadable_settable_property_is_reported(self):
        got, skipped = gate.parse_settings(src("public int B { get => _b; set => _b = value; }"))
        self.assertEqual([("S", "B")], skipped)

    def test_constant_reference_forms(self):
        for raw in ("Limits.Max", "(int)Limits.Max", "TAOM.Features.Limits.Max"):
            got = settings_parse(src(prop("A", raw)))[("S", "A")]
            self.assertEqual(("const", ("Limits", "Max")), got[:2], raw)


class GateTests(unittest.TestCase):
    def setUp(self):
        self._d = tempfile.TemporaryDirectory()
        self.addCleanup(self._d.cleanup)
        self.r = Repo(self._d.name)
        self.allow = str(Path(self._d.name) / "allow.json")

    def go(self, *extra):
        return run_main("--repo", str(self.r.path), "--allowlist", self.allow, *extra)

    def settings(self, body, tag=None, extra=""):
        self.r.write("Main/S.cs", src(body, extra))
        self.r.commit("c", tag)

    def test_every_tag_is_compared_and_oldest_is_named(self):
        self.settings(prop("A", 1), "v1.0.0")
        self.settings(prop("A", 2), "v1.0.1")
        self.settings(prop("A", 2))
        code, out, _ = self.go()
        self.assertEqual(1, code)
        self.assertIn("FLIP S.A: 1 -> 2 (oldest differing release v1.0.0)", out)
        self.assertIn("compared 1 settings against 2 release tags", out)

    def test_unchanged_passes(self):
        self.settings(prop("A", 1), "v1.0.0")
        self.settings(prop("A", 1))
        self.assertEqual(0, self.go()[0])

    def test_renamed_property_passes(self):
        self.settings(prop("A", 1), "v1.0.0")
        self.settings(prop("B", 2))
        self.assertEqual(0, self.go()[0])

    def test_unreachable_tag_is_not_compared(self):
        self.settings(prop("A", 1), "v1.0.0")
        self.r.git("checkout", "-q", "-b", "frozen")
        self.settings(prop("A", 9), "v9.0.0")
        self.r.git("checkout", "-q", "main")
        self.settings(prop("A", 1))
        code, out, _ = self.go()
        self.assertEqual(0, code, out)
        self.assertIn("against 1 release tags", out)

    def test_moved_file_is_still_compared(self):
        self.settings(prop("A", 1), "v1.0.0")
        (self.r.path / "Main" / "S.cs").unlink()
        self.r.write("Main/Sub/Moved.cs", src(prop("A", 2)))
        self.r.commit("move")
        self.assertEqual(1, self.go()[0])

    def test_changed_constant_is_a_flip(self):
        consts = "public static class Limits { public const int Max = {v}; }\n"
        self.settings(prop("A", "Limits.Max"), "v1.0.0", consts.replace("{v}", "1"))
        self.settings(prop("A", "Limits.Max"), None, consts.replace("{v}", "2"))
        code, out, _ = self.go()
        self.assertEqual(1, code, out)
        self.assertIn("FLIP S.A: 1 -> 2", out)

    def test_literal_moved_into_same_value_constant_reports_nothing(self):
        self.settings(prop("A", 7), "v1.0.0")
        self.settings(prop("A", "Limits.Max"), None, "public static class Limits { public const int Max = 7; }\n")
        code, out, _ = self.go()
        self.assertEqual(0, code, out)

    def test_unresolvable_constant_that_differs_is_unparsed(self):
        self.settings(prop("A", 7), "v1.0.0")
        self.settings(prop("A", "Limits.Max"))
        code, out, _ = self.go()
        self.assertEqual(1, code)
        self.assertIn("UNPARSED S.A", out)

    def test_expression_default_that_differs_is_unparsed(self):
        self.settings(prop("A", "Make(1)", "Foo"), "v1.0.0")
        self.settings(prop("A", "Make(2)", "Foo"))
        out = self.go()[1]
        self.assertIn("UNPARSED S.A", out)

    def test_bad_base_exits_2(self):
        self.settings(prop("A", 1), "v1.0.0")
        self.assertEqual(2, self.go("--base", "nope")[0])

    def test_good_base_is_used(self):
        self.settings(prop("A", 1), "v1")
        self.settings(prop("A", 2), "v2")
        self.settings(prop("A", 2))
        self.assertEqual(0, self.go("--base", "v2")[0])
        self.assertEqual(1, self.go()[0])

    def test_constant_inside_expression_default_is_resolved(self):
        consts = "public static class Limits { public const int Max = {v}; }\n"
        self.settings(prop("A", "Limits.Max * 2"), "v1.0.0", consts.replace("{v}", "3"))
        self.settings(prop("A", "Limits.Max * 2"), None, consts.replace("{v}", "4"))
        code, out, _ = self.go()
        self.assertEqual(1, code, out)
        self.assertIn("UNPARSED S.A", out)

    def test_expression_default_with_unchanged_constant_passes(self):
        consts = "public static class Limits { public const int Max = 3; }\n"
        self.settings(prop("A", "Limits.Max * 2"), "v1.0.0", consts)
        self.settings(prop("A", "Limits.Max * 2"), None, consts)
        self.assertEqual(0, self.go()[0])

    def test_enum_constant_default_change_is_reported(self):
        consts = "public static class Limits { public static readonly Mode Def = Mode.{v}; }\n"
        self.settings(prop("A", "Limits.Def", "Mode"), "v1.0.0", consts.replace("{v}", "Fast"))
        self.settings(prop("A", "Limits.Def", "Mode"), None, consts.replace("{v}", "Slow"))
        code, out, _ = self.go()
        self.assertEqual(1, code, out)
        self.assertIn("UNPARSED S.A", out)

    def test_colour_always_git_config_does_not_hide_files(self):
        self.r.git("config", "color.ui", "always")
        self.settings(prop("A", 1), "v1.0.0")
        self.settings(prop("A", 2))
        self.assertEqual(1, self.go()[0])

    def test_base_class_on_the_next_line_is_found(self):
        nxt = ("public class S\n    : AttributeGlobalSettings<S>\n{\n"
               + prop("A", "{v}") + "\n}\n")
        self.r.write("Main/S.cs", nxt.replace("{v}", "1"))
        self.r.commit("c", "v1.0.0")
        self.r.write("Main/S.cs", nxt.replace("{v}", "2"))
        self.r.commit("c")
        self.assertEqual(1, self.go()[0])

    def test_partial_settings_class_exits_2(self):
        self.settings(prop("A", 1), "v1.0.0")
        self.r.write("Main/S.cs", "public partial class S : AttributeGlobalSettings<S>\n{\n" + prop("A", 1) + "\n}\n")
        self.r.write("Main/S2.cs", "public partial class S\n{\n" + prop("B", 5) + "\n}\n")
        self.r.commit("partial")
        code, _, err = self.go()
        self.assertEqual(2, code)
        self.assertIn("partial", err)

    def test_no_tag_yields_a_setting_exits_2(self):
        self.r.write("Main/Other.cs", "public class Plain { }\n")
        self.r.commit("none", "v1.0.0")
        self.settings(prop("A", 1))
        self.assertEqual(2, self.go()[0])

    def test_no_reachable_tag_exits_2_fetch_tags(self):
        self.settings(prop("A", 1))
        code, _, err = self.go()
        self.assertEqual(2, code)
        self.assertIn("fetch tags", err)

    def test_no_settings_in_working_tree_exits_2(self):
        self.settings(prop("A", 1), "v1.0.0")
        self.r.write("Main/S.cs", "public class Plain { }\n")
        self.r.commit("none")
        self.assertEqual(2, self.go()[0])

    def test_not_a_repo_and_not_the_root_exit_2(self):
        with tempfile.TemporaryDirectory() as d:
            self.assertEqual(2, run_main("--repo", d)[0])
        self.settings(prop("A", 1), "v1.0.0")
        self.assertEqual(2, run_main("--repo", str(self.r.path / "Main"), "--allowlist", self.allow)[0])

    def test_allowlisted_flip_passes(self):
        self.settings(prop("A", 1), "v1.0.0")
        self.settings(prop("A", 2))
        Path(self.allow).write_text(json.dumps({"S.A": {"from": "1", "to": "2", "reason": "harmless"}}))
        code, out, _ = self.go()
        self.assertEqual(0, code, out)
        self.assertIn("ALLOWED S.A: 1 -> 2 (oldest differing release v1.0.0;", out)

    def test_allowlist_with_wrong_values_does_not_cover_and_is_stale(self):
        self.settings(prop("A", 1), "v1.0.0")
        self.settings(prop("A", 2))
        Path(self.allow).write_text(json.dumps({"S.A": {"from": "1", "to": "3", "reason": "x"}}))
        code, out, _ = self.go()
        self.assertEqual(1, code)
        self.assertIn("FLIP S.A", out)
        self.assertIn("STALE-ALLOWLIST S.A", out)

    def test_stale_allowlist_entry_is_reported(self):
        self.settings(prop("A", 1), "v1.0.0")
        self.settings(prop("A", 1))
        Path(self.allow).write_text(json.dumps({"S.Gone": {"from": "1", "to": "2", "reason": "x"}}))
        code, out, _ = self.go()
        self.assertEqual(1, code)
        self.assertIn("STALE-ALLOWLIST S.Gone", out)

    def test_allowlist_validation_exits_2(self):
        self.settings(prop("A", 1), "v1.0.0")
        for bad in ('["S.A"]', '{"S.A": "reason"}', '{"S.A": {"from": "1", "to": "2"}}',
                    '{"S.A": {"from": "1", "to": "2", "reason": " "}}', "not json"):
            Path(self.allow).write_text(bad)
            self.assertEqual(2, self.go()[0], bad)

    def test_unreadable_settable_property_fails_the_gate(self):
        self.settings(prop("A", 1) + "\npublic int B { get => _b; set => _b = value; }", "v1.0.0")
        self.settings(prop("A", 1) + "\npublic int B { get => _b; set => _b = value; }")
        code, out, _ = self.go()
        self.assertEqual(1, code)
        self.assertIn("UNPARSED S.B", out)


if __name__ == "__main__":
    unittest.main()
