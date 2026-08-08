#!/usr/bin/env python3
"""Unit tests for the GameModel registry check (tools/lint_docs.py check_model_registry).

Run:  python -m unittest discover -s tools/tests -p "test_*.py"
  or:  python tools/tests/test_model_registry.py

Two catalogues list TAOM's GameModel overrides and neither is ordinary documentation:
`.claude/rules/gamemodels.md` is `paths:`-scoped to the model files, so its table is the
briefing whoever edits a model receives; `docs/reference/gamemodel-registry.md` opens with
"Every TAOM GameModel override". Both had drifted (12 and 6 models missing, a count claiming
34 against 47 real classes) and nothing said so.

Every test here builds a SYNTHETIC tree in a tempdir and repoints lint_docs's roots at it,
following test_lint_docs.py. Each guard is exercised in BOTH directions — a clean tree that
must stay silent AND a broken tree that must speak — because a check that has only ever been
seen passing is not known to be able to fail.
"""
import os
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import lint_docs as ld  # noqa: E402


def _write(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")


class _SyntheticRepo(unittest.TestCase):
    """A tree with two model classes and two catalogues, both correct by default."""

    MODELS = ("TaomFooModel", "TaomBarModel")

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.root = Path(self._tmp.name)
        self._saved = (ld.REPO_ROOT, ld.DOCS_DIR, ld.MODEL_RULES_DOC,
                       ld.MODEL_REGISTRY_DOC, ld.MODELS_SEARCH_ROOT)

        ld.REPO_ROOT = self.root
        ld.DOCS_DIR = self.root / "docs"
        ld.MODEL_RULES_DOC = self.root / ".claude" / "rules" / "gamemodels.md"
        ld.MODEL_REGISTRY_DOC = self.root / "docs" / "reference" / "gamemodel-registry.md"
        ld.MODELS_SEARCH_ROOT = self.root / "Main"

        _write(self.root / "Main" / "Features" / "Foo" / "Models" / "TaomFooModel.cs",
               "namespace TAOM;\npublic class TaomFooModel : DefaultFooModel\n{\n}\n")
        _write(self.root / "Main" / "Features" / "Bar" / "Models" / "TaomBarModel.cs",
               "namespace TAOM;\npublic class TaomBarModel : DefaultBarModel\n{\n}\n")
        self.write_catalogues(self.MODELS, count=2)

    def tearDown(self):
        (ld.REPO_ROOT, ld.DOCS_DIR, ld.MODEL_RULES_DOC,
         ld.MODEL_REGISTRY_DOC, ld.MODELS_SEARCH_ROOT) = self._saved
        self._tmp.cleanup()

    def write_catalogues(self, names, count=None):
        rows = "\n".join(f"| `{n}` | `Default` | `Feature` |" for n in names)
        header = f"TAOM has {count} GameModel overrides.\n\n" if count is not None else ""
        _write(ld.MODEL_RULES_DOC, f"---\npaths:\n  - \"Main/**\"\n---\n\n{header}{rows}\n")
        _write(ld.MODEL_REGISTRY_DOC, f"> Every TAOM GameModel override.\n\n{rows}\n")

    def kinds(self):
        return [k for _f, _l, k, _m in ld.check_model_registry()]


class CleanTree(_SyntheticRepo):
    def test_a_correct_tree_reports_nothing(self):
        # The negative half of every test below: without this, a check that always fired
        # would still make them all pass.
        self.assertEqual([], ld.check_model_registry())

    def test_both_models_are_discovered(self):
        self.assertEqual(set(self.MODELS), set(ld.discover_model_classes()))

    def test_obj_and_bin_copies_are_not_discovered(self):
        # Main/obj and Main/bin hold compiler copies of the same sources; counting them
        # would double every model and make the count claim permanently wrong.
        _write(self.root / "Main" / "obj" / "Debug" / "TaomGhostModel.cs",
               "public class TaomGhostModel : DefaultGhostModel { }\n")
        _write(self.root / "Main" / "bin" / "Debug" / "TaomSpectreModel.cs",
               "public class TaomSpectreModel : DefaultSpectreModel { }\n")
        self.assertEqual(set(self.MODELS), set(ld.discover_model_classes()))


class Drift(_SyntheticRepo):
    def test_a_model_missing_from_the_catalogues_is_reported(self):
        self.write_catalogues(["TaomFooModel"], count=2)   # TaomBarModel dropped from both
        kinds = self.kinds()
        self.assertEqual(2, kinds.count("unlisted-model"), "expected one per catalogue")
        self.assertTrue(any("TaomBarModel" in m for _f, _l, k, m in ld.check_model_registry()
                            if k == "unlisted-model"))

    def test_a_catalogue_entry_with_no_class_is_reported(self):
        # The other direction of rot: a model gets renamed or deleted and its row stays,
        # reading as though someone still maintains it.
        self.write_catalogues(list(self.MODELS) + ["TaomDeletedModel"], count=2)
        self.assertEqual(2, self.kinds().count("phantom-model"))

    def test_a_stale_count_claim_is_reported(self):
        self.write_catalogues(self.MODELS, count=34)
        counts = [m for _f, _l, k, m in ld.check_model_registry() if k == "model-count"]
        self.assertEqual(1, len(counts), "only the rules doc carries a count claim")
        self.assertIn("claims 34", counts[0])
        self.assertIn("2 exist", counts[0])

    def test_the_second_count_shape_is_also_checked(self):
        # ".claude/rules/gamemodels.md" states its total twice, in two different sentences.
        # Catching only the first would leave the table heading permanently wrong.
        _write(ld.MODEL_RULES_DOC, "## Existing Overrides (34 total)\n\n"
               + "\n".join(f"| `{n}` |" for n in self.MODELS) + "\n")
        self.assertEqual(1, self.kinds().count("model-count"))

    def test_a_correct_count_is_silent(self):
        self.write_catalogues(self.MODELS, count=2)
        self.assertEqual(0, self.kinds().count("model-count"))

    def test_a_missing_catalogue_is_reported(self):
        ld.MODEL_REGISTRY_DOC.unlink()
        self.assertEqual(1, self.kinds().count("missing-doc"))

    def test_an_empty_search_root_is_reported_rather_than_passing(self):
        # The failure that would matter most: if Main/ moves, "no models found" must not
        # read as "every model is catalogued".
        ld.MODELS_SEARCH_ROOT = self.root / "NoSuchDirectory"
        kinds = self.kinds()
        self.assertEqual(["no-models"], kinds)


class RealRepo(unittest.TestCase):
    def test_the_real_catalogues_are_in_sync(self):
        # Regression pin. This is the check that fires when someone adds a model without
        # cataloguing it — the whole point of the check existing.
        findings = ld.check_model_registry()
        self.assertEqual([], findings,
                         "GameModel registry drift:\n" + "\n".join(m for *_ , m in findings))


if __name__ == "__main__":
    unittest.main(verbosity=2)
