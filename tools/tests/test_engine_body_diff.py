"""Tests for tools/engine_body_diff.py, the bound-member body diff for engine bumps.

Run: python -m pytest tools/tests/test_engine_body_diff.py -q

Synthetic data only: a tiny ilspy-style old and new decompile and a tiny snapshot folder are built
in tmp_path, so no game install is needed.
"""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import engine_body_diff as ebd  # noqa: E402

EMPTY_MODELS = "# models\n"
EMPTY_REFLECTION = "# r\n\n## Category B - x\n\n| Engine type | Member |\n|---|---|\n\n## Category C\n"
PATCH_HEAD = "# patches\n\n| Patch class | Engine target |\n|---|---|\n"


def cs(body: str, ns: str = "Eng.Ns") -> str:
    """Wrap tab-indented type text (one tab already included) in an ilspy-style namespace."""
    return f"namespace {ns}\n{{\n{body}}}\n"


def klass(name: str, members: str) -> str:
    return f"\tpublic class {name}\n\t{{\n{members}\t}}\n"


def method(name: str, stmt: str, ret: str = "void") -> str:
    return f"\t\tpublic {ret} {name}()\n\t\t{{\n\t\t\t{stmt}\n\t\t}}\n"


class Env:
    def __init__(self, tmp_path: Path):
        self.root = tmp_path / "root"
        self.snap = tmp_path / "snap"
        self.out = tmp_path / "out"
        self.taom = tmp_path / "taom"
        self.taom.mkdir()
        self.snap.mkdir()
        self.set_snapshot()

    def decompile(self, suffix: str, text: str, name: str = "Eng.cs", sub: str = "_shipping_build"):
        d = self.root / (sub + suffix)
        d.mkdir(parents=True, exist_ok=True)
        (d / name).write_text(text, encoding="utf-8")

    def set_snapshot(self, patches="", models=EMPTY_MODELS, reflection=EMPTY_REFLECTION):
        (self.snap / "patch-targets.md").write_text(PATCH_HEAD + patches, encoding="utf-8")
        (self.snap / "gamemodel-bases.md").write_text(models, encoding="utf-8")
        (self.snap / "reflection-sites.md").write_text(reflection, encoding="utf-8")

    def run(self, capsys, *extra):
        code = ebd.main(["--root", str(self.root), "--old-suffix", "_old", "--snapshot", str(self.snap),
                         "--out", str(self.out), "--taom-src", str(self.taom), *extra])
        return code, capsys.readouterr().out


def patch_row(target: str, patch: str = "TAOM.Hooks.FooPatch") -> str:
    return f"| `{patch}` | `{target}` |\n"


def test_identical_body_is_same(tmp_path, capsys):
    env = Env(tmp_path)
    text = cs(klass("Widget", method("Run", "x();")))
    env.decompile("_old", text)
    env.decompile("", text)
    env.set_snapshot(patches=patch_row("Eng.Ns.Widget.Void Run()"))
    code, out = env.run(capsys)
    assert code == 0
    assert "rows=1 same=1 changed=0 unresolved=0" in out
    assert "rows_in_file=1 parsed=1 unparsed=0" in out


def test_changed_body_writes_diff(tmp_path, capsys):
    env = Env(tmp_path)
    env.decompile("_old", cs(klass("Widget", method("Run", "x();"))))
    env.decompile("", cs(klass("Widget", method("Run", "y();"))))
    env.set_snapshot(patches=patch_row("Eng.Ns.Widget.Void Run()"))
    code, out = env.run(capsys)
    assert code == 0
    assert "rows=1 same=0 changed=1 unresolved=0" in out
    assert "CHANGED  Eng.Ns.Widget.Run  [patch FooPatch]  ~2 lines" in out
    diff = (env.out / "Eng.Ns.Widget.Run.diff").read_text(encoding="utf-8")
    assert "-\t\t\tx();" in diff and "+\t\t\ty();" in diff
    assert (env.out / "summary.txt").is_file()


def test_generic_arity_row_is_parsed(tmp_path, capsys):
    env = Env(tmp_path)
    members = "\t\tpublic List<int> Get(List<int> a)\n\t\t{\n\t\t\treturn a;\n\t\t}\n"
    text = cs(klass("Game", members))
    env.decompile("_old", text)
    env.decompile("", text)
    env.set_snapshot(patches=patch_row("Eng.Ns.Game.MBList`1 Get(List`1 a)"))
    code, out = env.run(capsys)
    assert code == 0
    assert "rows=1 same=1" in out


def test_two_target_row_gives_two_members(tmp_path, capsys):
    env = Env(tmp_path)
    text = cs(klass("Widget", method("Open", "a();") + method("Close", "b();")))
    env.decompile("_old", text)
    env.decompile("", text)
    env.set_snapshot(patches=patch_row("Eng.Ns.Widget.Void Close()  ;  Eng.Ns.Widget.Void Open()"))
    code, out = env.run(capsys)
    assert code == 0
    assert "rows_in_file=1 parsed=1 unparsed=0" in out
    assert "rows=2 same=2" in out


def test_malformed_row_is_counted_and_fails(tmp_path, capsys):
    env = Env(tmp_path)
    text = cs(klass("Widget", method("Run", "x();")))
    env.decompile("_old", text)
    env.decompile("", text)
    env.set_snapshot(patches=patch_row("Eng.Ns.Widget.Void Run()")
                     + patch_row("garbage without a method")
                     + "| `TAOM.Hooks.Lonely` | not backticked |\n")
    code, out = env.run(capsys)
    assert code == 1
    assert "rows_in_file=3 parsed=1 unparsed=2" in out
    assert "UNPARSED  | `TAOM.Hooks.FooPatch` | `garbage without a method` |" in out
    assert "UNPARSED  | `TAOM.Hooks.Lonely` | not backticked |" in out


def test_nested_type(tmp_path, capsys):
    env = Env(tmp_path)

    def build(stmt):
        inner = f"\t\tpublic class Inner\n\t\t{{\n\t\t\tpublic void Go()\n\t\t\t{{\n\t\t\t\t{stmt}\n\t\t\t}}\n\t\t}}\n"
        return cs(f"\tpublic class Outer\n\t{{\n{inner}\t}}\n")

    env.decompile("_old", build("a();"))
    env.decompile("", build("b();"))
    env.set_snapshot(patches=patch_row("Eng.Ns.Outer+Inner.Void Go()"))
    code, out = env.run(capsys)
    assert code == 0
    assert "CHANGED  Eng.Ns.Outer.Inner.Go" in out


def test_property_accessor(tmp_path, capsys):
    env = Env(tmp_path)

    def build(value):
        prop = f"\t\tpublic int Size\n\t\t{{\n\t\t\tget\n\t\t\t{{\n\t\t\t\treturn {value};\n\t\t\t}}\n\t\t}}\n"
        return cs(klass("Widget", prop))

    env.decompile("_old", build(1))
    env.decompile("", build(2))
    env.set_snapshot(patches=patch_row("Eng.Ns.Widget.Int32 get_Size()"))
    code, out = env.run(capsys)
    assert code == 0
    assert "CHANGED  Eng.Ns.Widget.get_Size" in out


def test_constructor(tmp_path, capsys):
    env = Env(tmp_path)

    def build(stmt):
        ctor = f"\t\tpublic Widget(int a)\n\t\t{{\n\t\t\t{stmt}\n\t\t}}\n"
        return cs(klass("Widget", ctor))

    env.decompile("_old", build("a();"))
    env.decompile("", build("b();"))
    env.set_snapshot(patches=patch_row("Eng.Ns.Widget.Void .ctor(Int32 a)"))
    code, out = env.run(capsys)
    assert code == 0
    assert "CHANGED  Eng.Ns.Widget..ctor" in out


def test_reflection_category_b_row(tmp_path, capsys):
    env = Env(tmp_path)

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
    code, out = env.run(capsys)
    assert code == 0
    assert "reflection-sites.md: rows_in_file=2 parsed=2 unparsed=0" in out
    assert "CHANGED  Eng.Deep.Ns.Holder._count  [reflection]" in out
    assert "UNRESOLVED member-both  Eng.Deep.Ns.Holder.Count  [reflection]" in out


def test_deserialize_loader_matched_by_type(tmp_path, capsys):
    env = Env(tmp_path)

    def loader(stmt):
        m = f"\t\tpublic override void Deserialize(MBObjectManager m, XmlNode n)\n\t\t{{\n\t\t\t{stmt}\n\t\t}}\n"
        return m + method("Other", "same();")

    env.decompile("_old", cs(klass("ItemObj", loader("a();"))))
    env.decompile("", cs(klass("ItemObj", loader("b();"))))
    code, out = env.run(capsys)
    assert code == 0
    assert "CHANGED  Eng.Ns.ItemObj.Deserialize  [deserialize]" in out
    diff = (env.out / "Eng.Ns.ItemObj.Deserialize.diff").read_text(encoding="utf-8")
    assert "same();" not in diff


def test_non_ascii_only_difference_is_normalized(tmp_path, capsys):
    env = Env(tmp_path)
    old = cs(klass("Widget", method("Say", 'Log("caf?? ok?");')))
    new = cs(klass("Widget", method("Say", 'Log("café ok?");')))
    env.decompile("_old", old)
    env.decompile("", new)
    env.set_snapshot(patches=patch_row("Eng.Ns.Widget.Void Say()"))
    code, out = env.run(capsys)
    assert code == 0 and "same=1 changed=0" in out
    code, out = env.run(capsys, "--no-normalize")
    assert code == 0 and "same=0 changed=1" in out


def test_nag_line_is_dropped(tmp_path, capsys):
    env = Env(tmp_path)
    text = cs(klass("Widget", method("Run", "x();")))
    env.decompile("_old", "Latest version is 9.9.9\n" + text)
    env.decompile("", text)
    env.set_snapshot(patches=patch_row("Eng.Ns.Widget.Void Run()"))
    code, out = env.run(capsys)
    assert code == 0 and "same=1 changed=0" in out


def test_missing_type_is_unresolved(tmp_path, capsys):
    env = Env(tmp_path)
    env.decompile("_old", cs(klass("Widget", method("Run", "x();"))))
    env.decompile("", cs(klass("Other", method("Run", "x();"))))
    env.set_snapshot(patches=patch_row("Eng.Ns.Widget.Void Run()"))
    code, out = env.run(capsys)
    assert code == 0
    assert "unresolved=1" in out
    assert "UNRESOLVED type-new  Eng.Ns.Widget.Run  [patch FooPatch]" in out


def test_model_walks_a_taom_base_and_reports_a_missing_one(tmp_path, capsys):
    env = Env(tmp_path)
    text = cs(klass("DefaultThing", method("Calc", "x();")))
    env.decompile("_old", text)
    env.decompile("", text)
    models = ("## TaomThing : DefaultThing\n`Base: Eng.Ns.DefaultThing`\n\n- `Void Calc()`\n\n"
              "## TaomDeep : TaomThing\n`Base: TAOM.Models.TaomThing`\n\n- `Void Calc()`\n\n"
              "## TaomLost : TaomGone\n`Base: TAOM.Models.TaomGone`\n\n- `Void Calc()`\n")
    env.set_snapshot(models=models)
    code, out = env.run(capsys)
    assert code == 0
    assert "gamemodel-bases.md: rows_in_file=3 parsed=3 unparsed=0" in out
    assert "UNRESOLVED taom-base-unresolved  TAOM.Models.TaomGone.Calc  [model TaomLost]" in out
    assert "rows=2 same=1 changed=0 unresolved=1" in out


def test_taom_base_resolved_from_taom_source(tmp_path, capsys):
    env = Env(tmp_path)

    def build(stmt):
        return cs(klass("SandboxDamage", method("Apply", stmt)), ns="Eng.Sandbox")

    env.decompile("_old", build("a();"))
    env.decompile("", build("b();"))
    (env.taom / "Models.cs").write_text(
        "namespace TAOM.Models\n{\n\tpublic abstract class TaomMid : SandboxDamage\n\t{\n\t}\n"
        "\tpublic class TaomTop<T> : TaomMid, IThing\n\t{\n\t}\n}\n", encoding="utf-8")
    models = "## TaomCombat : TaomTop\n`Base: TAOM.Models.TaomTop`\n\n- `Void Apply()`\n- `Void Apply2()`\n"
    env.set_snapshot(models=models)
    code, out = env.run(capsys)
    assert code == 0
    assert "gamemodel-bases.md: rows_in_file=2 parsed=2 unparsed=0" in out
    assert "CHANGED  Eng.Sandbox.SandboxDamage.Apply  [model TaomCombat]" in out
    assert "UNRESOLVED member-both  Eng.Sandbox.SandboxDamage.Apply2" in out


def test_inherited_member_is_diffed_on_the_declaring_type(tmp_path, capsys):
    env = Env(tmp_path)

    def build(stmt):
        base = "\tpublic abstract class CalcModel : MBGameModel<CalcModel>\n\t{\n" + method("Cost", stmt) + "\t}\n"
        mid = "\tpublic class MidModel : CalcModel, IFoo\n\t{\n\t}\n"
        leaf = "\tpublic class LeafModel : MidModel\n\t{\n" + method("Other", "z();") + "\t}\n"
        iface = "\tpublic interface IFoo\n\t{\n\t}\n"
        return cs(iface + base + mid + leaf)

    env.decompile("_old", build("a();"))
    env.decompile("", build("a();"))
    env.set_snapshot(patches=patch_row("Eng.Ns.LeafModel.Void Cost()"))
    code, out = env.run(capsys)
    assert code == 0
    assert "rows=1 same=1 changed=0 unresolved=0" in out
    assert "INHERITED  Eng.Ns.LeafModel.Cost  declared on Eng.Ns.CalcModel  [patch FooPatch]" in out
    env.decompile("", build("b();"))
    code, out = env.run(capsys)
    assert "CHANGED  Eng.Ns.LeafModel.Cost  [patch FooPatch]  ~2 lines  (declared on Eng.Ns.CalcModel)" in out


def test_bad_input_exits_two(tmp_path, capsys):
    env = Env(tmp_path)
    code, out = env.run(capsys)
    assert code == 2
    env.decompile("_old", cs(klass("W", method("Run", "x();"))))
    env.decompile("", cs(klass("W", method("Run", "x();"))))
    (env.snap / "patch-targets.md").unlink()
    code, out = env.run(capsys)
    assert code == 2


def test_member_listed_by_patch_and_reflection_is_not_unresolved(tmp_path, capsys):
    env = Env(tmp_path)
    text = cs(klass("Widget", method("Run", "x();")))
    env.decompile("_old", text)
    env.decompile("", text)
    refl = "## Category B - x\n\n| Engine type | Member |\n|---|---|\n| `…Ns.Widget` | `Run` |\n"
    env.set_snapshot(patches=patch_row("Eng.Ns.Widget.Void Run()"), reflection=refl)
    code, out = env.run(capsys)
    assert code == 0
    assert "rows=1 same=1 changed=0 unresolved=0" in out
