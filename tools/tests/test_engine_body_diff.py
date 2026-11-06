"""Tests for tools/engine_body_diff.py, the bound-member body diff for engine bumps.

Run: python -m unittest tools.tests.test_engine_body_diff   (from the repo root)

Synthetic data only: a tiny ilspy-style old and new decompile and a tiny snapshot folder are built
in a temporary directory, so no game install is needed.
"""
import contextlib
import io
import shutil
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import engine_body_diff as ebd  # noqa: E402

EMPTY_MODELS = "# models\n"
EMPTY_REFLECTION = "# r\n\n## Category B - x\n\n| Engine type | Member |\n|---|---|\n\n## Category C\n"
PATCH_HEAD = "# patches\n\n| Patch class | Engine target |\n|---|---|\n"
OLD_VERSION = "v1.0.0.1"
NEW_VERSION = "v1.0.1.2"


def cs(body: str, ns: str = "Eng.Ns") -> str:
    """Wrap tab-indented type text (one tab already included) in an ilspy-style namespace."""
    return f"namespace {ns}\n{{\n{body}}}\n"


def klass(name: str, members: str) -> str:
    return f"\tpublic class {name}\n\t{{\n{members}\t}}\n"


def method(name: str, stmt: str, ret: str = "void") -> str:
    return f"\t\tpublic {ret} {name}()\n\t\t{{\n\t\t\t{stmt}\n\t\t}}\n"


def library(version) -> str:
    """A TaleWorlds.Library.cs holding BuildInfo.GameVersion (None: a file without it)."""
    field = "" if version is None else f'\t\tpublic const string GameVersion = "{version}";\n'
    return cs(klass("BuildInfo", field), ns="TaleWorlds.Library")


def patch_row(target: str, patch: str = "TAOM.Hooks.FooPatch") -> str:
    return f"| `{patch}` | `{target}` |\n"


class Env:
    """A decompile root with all four build folders (old suffix _old, new suffix none)."""

    def __init__(self, tmp: Path):
        self.root = tmp / "root"
        self.snap = tmp / "snap"
        self.out = tmp / "out"
        self.taom = tmp / "taom"
        self.taom.mkdir()
        self.snap.mkdir()
        for suffix, version in (("_old", OLD_VERSION), ("", NEW_VERSION)):
            self.decompile(suffix, library(version), name="TaleWorlds.Library.cs")
            (self.root / ("_modules_build" + suffix)).mkdir(parents=True, exist_ok=True)
        self.set_snapshot()

    def decompile(self, suffix: str, text: str, name: str = "Eng.cs", sub: str = "_shipping_build"):
        d = self.root / (sub + suffix)
        d.mkdir(parents=True, exist_ok=True)
        (d / name).write_text(text, encoding="utf-8")

    def set_snapshot(self, patches="", models=EMPTY_MODELS, reflection=EMPTY_REFLECTION):
        (self.snap / "patch-targets.md").write_text(PATCH_HEAD + patches, encoding="utf-8")
        (self.snap / "gamemodel-bases.md").write_text(models, encoding="utf-8")
        (self.snap / "reflection-sites.md").write_text(reflection, encoding="utf-8")

    def run(self, *extra):
        buf = io.StringIO()
        with contextlib.redirect_stdout(buf), contextlib.redirect_stderr(io.StringIO()):
            code = ebd.main(["--root", str(self.root), "--old-suffix", "_old", "--snapshot", str(self.snap),
                             "--out", str(self.out), "--taom-src", str(self.taom), *extra])
        return code, buf.getvalue()


class BodyDiffCase(unittest.TestCase):
    def setUp(self):
        tmp = tempfile.TemporaryDirectory()
        self.addCleanup(tmp.cleanup)
        self.env = Env(Path(tmp.name))

    def same_text(self, name="Widget", stmt="x();"):
        """Install an identical one-method type on both sides; return nothing."""
        text = cs(klass(name, method("Run", stmt)))
        self.env.decompile("_old", text)
        self.env.decompile("", text)

    def test_identical_body_is_same(self):
        env = self.env
        self.same_text()
        env.set_snapshot(patches=patch_row("Eng.Ns.Widget.Void Run()"))
        code, out = env.run()
        self.assertEqual(code, 0)
        self.assertIn(f"old={OLD_VERSION} new={NEW_VERSION}", out)
        self.assertIn("rows=1 same=1 changed=0 unresolved=0", out)
        self.assertIn("rows_in_file=1 parsed=1 unparsed=0", out)

    def test_third_party_engine_copy_is_ignored(self):
        # A mod that ships its own TaleWorlds.*.dll copies (the Coop module, 2026-10-09) puts a second
        # definition of every engine type into _modules_build; it must not count as an engine change.
        env = self.env
        text = cs(klass("Widget", method("Run", "x();")))
        env.decompile("_old", text, name="TaleWorlds.Eng.cs")
        env.decompile("", text, name="TaleWorlds.Eng.cs")
        env.decompile("", cs(klass("Widget", method("Run", "stale();"))),
                      name="Coop__TaleWorlds.Eng.cs", sub="_modules_build")
        taom = cs(klass("Mod", method("Tick", "t();")), ns="TAOM")
        env.decompile("_old", taom, name="TAOM__TAOM.cs", sub="_modules_build")
        env.decompile("", taom, name="TAOM__TAOM.cs", sub="_modules_build")
        env.set_snapshot(patches=patch_row("Eng.Ns.Widget.Void Run()") + patch_row("TAOM.Mod.Void Tick()"))
        code, out = env.run()
        self.assertEqual(code, 0)
        self.assertIn("rows=2 same=2 changed=0 unresolved=0", out)
        self.assertIn("skipped third-party engine copies (new): Coop=1", out)

    def test_vanilla_module_engine_assembly_is_read(self):
        env = self.env
        env.decompile("_old", cs(klass("Widget", method("Run", "x();"))),
                      name="SandBox__SandBox.View.cs", sub="_modules_build")
        env.decompile("", cs(klass("Widget", method("Run", "y();"))),
                      name="SandBox__SandBox.View.cs", sub="_modules_build")
        env.set_snapshot(patches=patch_row("Eng.Ns.Widget.Void Run()"))
        code, out = env.run()
        self.assertEqual(code, 0)
        self.assertIn("rows=1 same=0 changed=1 unresolved=0", out)
        self.assertNotIn("skipped third-party", out)

    def test_is_third_party_engine_copy(self):
        f = ebd.is_third_party_engine_copy
        self.assertTrue(f("Coop__TaleWorlds.Core.cs"))
        self.assertTrue(f("Coop__SandBox.View.cs"))
        self.assertFalse(f("SandBox__SandBox.View.cs"))
        self.assertFalse(f("NavalDLC__NavalDLC.cs"))
        self.assertFalse(f("TAOM__TAOM.cs"))
        self.assertFalse(f("Coop__Coop.Common.cs"))
        self.assertFalse(f("TaleWorlds.Core.cs"))

    def test_changed_body_writes_diff(self):
        env = self.env
        env.decompile("_old", cs(klass("Widget", method("Run", "x();"))))
        env.decompile("", cs(klass("Widget", method("Run", "y();"))))
        env.set_snapshot(patches=patch_row("Eng.Ns.Widget.Void Run()"))
        code, out = env.run()
        self.assertEqual(code, 0)
        self.assertIn("rows=1 same=0 changed=1 unresolved=0", out)
        self.assertIn("CHANGED  Eng.Ns.Widget.Run  [patch FooPatch]  ~2 lines", out)
        diff = (env.out / "Eng.Ns.Widget.Run.diff").read_text(encoding="utf-8")
        self.assertIn("-\t\t\tx();", diff)
        self.assertIn("+\t\t\ty();", diff)
        self.assertTrue((env.out / "summary.txt").is_file())

    def test_generic_arity_row_is_parsed(self):
        env = self.env
        members = "\t\tpublic List<int> Get(List<int> a)\n\t\t{\n\t\t\treturn a;\n\t\t}\n"
        text = cs(klass("Game", members))
        env.decompile("_old", text)
        env.decompile("", text)
        env.set_snapshot(patches=patch_row("Eng.Ns.Game.MBList`1 Get(List`1 a)"))
        code, out = env.run()
        self.assertEqual(code, 0)
        self.assertIn("rows=1 same=1", out)

    def test_two_target_row_gives_two_members(self):
        env = self.env
        text = cs(klass("Widget", method("Open", "a();") + method("Close", "b();")))
        env.decompile("_old", text)
        env.decompile("", text)
        env.set_snapshot(patches=patch_row("Eng.Ns.Widget.Void Close()  ;  Eng.Ns.Widget.Void Open()"))
        code, out = env.run()
        self.assertEqual(code, 0)
        self.assertIn("rows_in_file=1 parsed=1 unparsed=0", out)
        self.assertIn("rows=2 same=2", out)

    def test_malformed_row_is_counted_and_fails(self):
        env = self.env
        self.same_text()
        env.set_snapshot(patches=patch_row("Eng.Ns.Widget.Void Run()")
                         + patch_row("garbage without a method")
                         + "| `TAOM.Hooks.Lonely` | not backticked |\n")
        code, out = env.run()
        self.assertEqual(code, 1)
        self.assertIn("rows_in_file=3 parsed=1 unparsed=2", out)
        self.assertIn("UNPARSED  | `TAOM.Hooks.FooPatch` | `garbage without a method` |", out)
        self.assertIn("UNPARSED  | `TAOM.Hooks.Lonely` | not backticked |", out)

    def test_nested_type(self):
        env = self.env

        def build(stmt):
            inner = f"\t\tpublic class Inner\n\t\t{{\n\t\t\tpublic void Go()\n\t\t\t{{\n\t\t\t\t{stmt}\n\t\t\t}}\n\t\t}}\n"
            return cs(f"\tpublic class Outer\n\t{{\n{inner}\t}}\n")

        env.decompile("_old", build("a();"))
        env.decompile("", build("b();"))
        env.set_snapshot(patches=patch_row("Eng.Ns.Outer+Inner.Void Go()"))
        code, out = env.run()
        self.assertEqual(code, 0)
        self.assertIn("CHANGED  Eng.Ns.Outer.Inner.Go", out)

    def test_property_accessor(self):
        env = self.env

        def build(value):
            prop = f"\t\tpublic int Size\n\t\t{{\n\t\t\tget\n\t\t\t{{\n\t\t\t\treturn {value};\n\t\t\t}}\n\t\t}}\n"
            return cs(klass("Widget", prop))

        env.decompile("_old", build(1))
        env.decompile("", build(2))
        env.set_snapshot(patches=patch_row("Eng.Ns.Widget.Int32 get_Size()"))
        code, out = env.run()
        self.assertEqual(code, 0)
        self.assertIn("CHANGED  Eng.Ns.Widget.get_Size", out)

    def test_constructor(self):
        env = self.env

        def build(stmt):
            ctor = f"\t\tpublic Widget(int a)\n\t\t{{\n\t\t\t{stmt}\n\t\t}}\n"
            return cs(klass("Widget", ctor))

        env.decompile("_old", build("a();"))
        env.decompile("", build("b();"))
        env.set_snapshot(patches=patch_row("Eng.Ns.Widget.Void .ctor(Int32 a)"))
        code, out = env.run()
        self.assertEqual(code, 0)
        self.assertIn("CHANGED  Eng.Ns.Widget..ctor", out)

    def test_reflection_category_b_row(self):
        env = self.env

        def build(init):
            field = f"\t\tprivate int _count = {init};\n"
            return cs(klass("Holder", field), ns="Eng.Deep.Ns")

        env.decompile("_old", build(1))
        env.decompile("", build(2))
        refl = ("## Category B - x\n\n| Engine type | Member | Kind |\n|---|---|---|\n"
                "| `…Deep.Ns.Holder` | `_count` | field |\n"
                "| `Eng.Deep.Ns.Holder` | `<Count>k__BackingField` | field |\n\n"
                "## Category C\n\n| Not | Parsed |\n|---|---|\n| `x` | `y` |\n")
        env.set_snapshot(reflection=refl)
        code, out = env.run()
        self.assertEqual(code, 1)  # one unresolved row
        self.assertIn("reflection-sites.md: rows_in_file=2 parsed=2 unparsed=0", out)
        self.assertIn("CHANGED  Eng.Deep.Ns.Holder._count  [reflection]", out)
        self.assertIn("UNRESOLVED member-both  Eng.Deep.Ns.Holder.Count  [reflection]", out)

    def test_deserialize_loader_matched_by_type(self):
        env = self.env

        def loader(stmt):
            m = f"\t\tpublic override void Deserialize(MBObjectManager m, XmlNode n)\n\t\t{{\n\t\t\t{stmt}\n\t\t}}\n"
            return m + method("Other", "same();")

        env.decompile("_old", cs(klass("ItemObj", loader("a();"))))
        env.decompile("", cs(klass("ItemObj", loader("b();"))))
        code, out = env.run()
        self.assertEqual(code, 0)
        self.assertIn("CHANGED  Eng.Ns.ItemObj.Deserialize  [deserialize]", out)
        diff = (env.out / "Eng.Ns.ItemObj.Deserialize.diff").read_text(encoding="utf-8")
        self.assertNotIn("same();", diff)

    def test_non_ascii_only_difference_is_normalized(self):
        env = self.env
        old = cs(klass("Widget", method("Say", 'Log("caf?? ok?");')))
        new = cs(klass("Widget", method("Say", 'Log("café ok?");')))
        env.decompile("_old", old)
        env.decompile("", new)
        env.set_snapshot(patches=patch_row("Eng.Ns.Widget.Void Say()"))
        code, out = env.run()
        self.assertEqual(code, 0)
        self.assertIn("same=1 changed=0", out)
        code, out = env.run("--no-normalize")
        self.assertEqual(code, 0)
        self.assertIn("same=0 changed=1", out)

    def test_normalize_line_leaves_plain_ascii_alone(self):
        line = "\t\t\tvar a = b;"
        self.assertEqual(ebd.normalize_line(line), line)
        self.assertEqual(ebd.normalize_line('x = "a?b";'), 'x = "a¤b";')

    def test_nag_line_is_dropped(self):
        env = self.env
        text = cs(klass("Widget", method("Run", "x();")))
        env.decompile("_old", "Latest version is 9.9.9\n" + text)
        env.decompile("", text)
        env.set_snapshot(patches=patch_row("Eng.Ns.Widget.Void Run()"))
        code, out = env.run()
        self.assertEqual(code, 0)
        self.assertIn("same=1 changed=0", out)

    def test_missing_type_is_unresolved_and_fails(self):
        env = self.env
        env.decompile("_old", cs(klass("Widget", method("Run", "x();"))))
        env.decompile("", cs(klass("Other", method("Run", "x();"))))
        env.set_snapshot(patches=patch_row("Eng.Ns.Widget.Void Run()"))
        code, out = env.run()
        self.assertEqual(code, 1)
        self.assertIn("unresolved=1", out)
        self.assertIn("UNRESOLVED type-new  Eng.Ns.Widget.Run  [patch FooPatch]", out)

    def test_model_walks_a_taom_base_and_reports_a_missing_one(self):
        env = self.env
        text = cs(klass("DefaultThing", method("Calc", "x();")))
        env.decompile("_old", text)
        env.decompile("", text)
        models = ("## TaomThing : DefaultThing\n`Base: Eng.Ns.DefaultThing`\n\n- `Void Calc()`\n\n"
                  "## TaomDeep : TaomThing\n`Base: TAOM.Models.TaomThing`\n\n- `Void Calc()`\n\n"
                  "## TaomLost : TaomGone\n`Base: TAOM.Models.TaomGone`\n\n- `Void Calc()`\n")
        env.set_snapshot(models=models)
        code, out = env.run()
        self.assertEqual(code, 1)  # the unresolved TaomLost row
        self.assertIn("gamemodel-bases.md: rows_in_file=3 parsed=3 unparsed=0", out)
        self.assertIn("UNRESOLVED taom-base-unresolved  TAOM.Models.TaomGone.Calc  [model TaomLost]", out)
        self.assertIn("rows=2 same=1 changed=0 unresolved=1", out)

    def test_taom_base_resolved_from_taom_source(self):
        env = self.env

        def build(stmt):
            return cs(klass("SandboxDamage", method("Apply", stmt)), ns="Eng.Sandbox")

        env.decompile("_old", build("a();"))
        env.decompile("", build("b();"))
        (env.taom / "Models.cs").write_text(
            "namespace TAOM.Models\n{\n\tpublic abstract class TaomMid : SandboxDamage\n\t{\n\t}\n"
            "\tpublic class TaomTop<T> : TaomMid, IThing\n\t{\n\t}\n}\n", encoding="utf-8")
        models = "## TaomCombat : TaomTop\n`Base: TAOM.Models.TaomTop`\n\n- `Void Apply()`\n- `Void Apply2()`\n"
        env.set_snapshot(models=models)
        code, out = env.run()
        self.assertEqual(code, 1)  # Apply2 is member-both, unresolved
        self.assertIn("gamemodel-bases.md: rows_in_file=2 parsed=2 unparsed=0", out)
        self.assertIn("CHANGED  Eng.Sandbox.SandboxDamage.Apply  [model TaomCombat]", out)
        self.assertIn("UNRESOLVED member-both  Eng.Sandbox.SandboxDamage.Apply2", out)

    def test_model_item_under_an_unrecognised_heading_is_unparsed(self):
        env = self.env
        self.same_text()
        models = ("- `Void Orphan()`\n\n"
                  "## Weird heading without a base\n`Base: Eng.Ns.Widget`\n\n- `Void Run()`\n")
        env.set_snapshot(models=models)
        code, out = env.run()
        self.assertEqual(code, 1)
        self.assertIn("gamemodel-bases.md: rows_in_file=2 parsed=0 unparsed=2", out)
        self.assertIn("UNPARSED  - `Void Orphan()`", out)
        self.assertIn("UNPARSED  - `Void Run()`", out)

    def test_inherited_member_is_diffed_on_the_declaring_type(self):
        env = self.env

        def build(stmt):
            base = "\tpublic abstract class CalcModel : MBGameModel<CalcModel>\n\t{\n" + method("Cost", stmt) + "\t}\n"
            mid = "\tpublic class MidModel : CalcModel, IFoo\n\t{\n\t}\n"
            leaf = "\tpublic class LeafModel : MidModel\n\t{\n" + method("Other", "z();") + "\t}\n"
            iface = "\tpublic interface IFoo\n\t{\n\t}\n"
            return cs(iface + base + mid + leaf)

        env.decompile("_old", build("a();"))
        env.decompile("", build("a();"))
        env.set_snapshot(patches=patch_row("Eng.Ns.LeafModel.Void Cost()"))
        code, out = env.run()
        self.assertEqual(code, 0)
        self.assertIn("rows=1 same=1 changed=0 unresolved=0", out)
        self.assertIn("INHERITED  Eng.Ns.LeafModel.Cost  declared on Eng.Ns.CalcModel  [patch FooPatch]", out)
        env.decompile("", build("b();"))
        code, out = env.run()
        self.assertIn("CHANGED  Eng.Ns.LeafModel.Cost  [patch FooPatch]  ~2 lines  (declared on Eng.Ns.CalcModel)", out)

    def test_missing_root_or_snapshot_exits_two(self):
        env = self.env
        self.same_text()
        (env.snap / "patch-targets.md").unlink()
        code, _ = env.run()
        self.assertEqual(code, 2)
        env.set_snapshot()
        shutil.rmtree(env.root)
        code, _ = env.run()
        self.assertEqual(code, 2)

    def test_missing_build_folder_exits_two(self):
        env = self.env
        self.same_text()
        for sub in ("_modules_build_old", "_modules_build", "_shipping_build_old", "_shipping_build"):
            with self.subTest(missing=sub):
                keep = env.root / sub
                parked = env.root.parent / ("parked" + sub)
                keep.rename(parked)
                try:
                    code, _ = env.run()
                finally:
                    parked.rename(keep)
                self.assertEqual(code, 2)
        code, _ = env.run()
        self.assertEqual(code, 0)

    def test_mistyped_taom_src_exits_two(self):
        env = self.env
        self.same_text()
        code, _ = env.run("--taom-src", str(env.taom / "no_such_folder"))
        self.assertEqual(code, 2)

    def test_same_game_version_on_both_sides_exits_two(self):
        env = self.env
        self.same_text()
        env.decompile("", library(OLD_VERSION), name="TaleWorlds.Library.cs")
        code, _ = env.run()
        self.assertEqual(code, 2)

    def test_missing_game_version_exits_two(self):
        env = self.env
        self.same_text()
        env.decompile("", library(None), name="TaleWorlds.Library.cs")
        code, _ = env.run()
        self.assertEqual(code, 2)

    def test_stale_diffs_are_cleared_and_other_files_survive(self):
        env = self.env
        self.same_text()
        env.set_snapshot(patches=patch_row("Eng.Ns.Widget.Void Run()"))
        env.out.mkdir()
        (env.out / "Old.Member.diff").write_text("stale", encoding="utf-8")
        (env.out / "notes.txt").write_text("keep", encoding="utf-8")
        code, out = env.run()
        self.assertEqual(code, 0)
        self.assertIn("cleared 1 .diff file(s)", out)
        self.assertFalse((env.out / "Old.Member.diff").exists())
        self.assertEqual((env.out / "notes.txt").read_text(encoding="utf-8"), "keep")

    def test_member_listed_by_patch_and_reflection_is_not_unresolved(self):
        env = self.env
        self.same_text()
        refl = "## Category B - x\n\n| Engine type | Member |\n|---|---|\n| `…Ns.Widget` | `Run` |\n"
        env.set_snapshot(patches=patch_row("Eng.Ns.Widget.Void Run()"), reflection=refl)
        code, out = env.run()
        self.assertEqual(code, 0)
        self.assertIn("rows=1 same=1 changed=0 unresolved=0", out)


if __name__ == "__main__":
    unittest.main()
