import hashlib
import json
from pathlib import Path
import tempfile
import unittest
import zipfile

from tools import package_retopology_toolkit as pkg


ROOT = Path(__file__).resolve().parents[2]


class RetopologyToolkitPackageTests(unittest.TestCase):
    def test_manifest_contains_complete_workflow_and_no_proprietary_payloads(self):
        required = {
            "tools/retopo_engine/__init__.py",
            "tools/retopo_engine/model.py",
            "tools/retopo_engine/geometry.py",
            "tools/retopo_engine/engine.py",
            "tools/retopo_engine/differential.py",
            "tools/blender/retopo_adapter.py",
            "tools/blender/retopo_operators.py",
            "tools/blender/retopo_selftest.py",
            "tools/blender/retopo_reference_differential.py",
            "tools/blender/petik_selftest.py",
            "tools/examples/retopo_active.py",
            "tools/petik/Petik/__init__.py",
            "tools/petik/Petik/op_md_petik.py",
            "tools/petik/Petik/petik_preferences.py",
            "tools/petik/Petik/blender_manifest.toml",
            "tools/petik/Petik/LICENSE.txt",
            "tools/retopo_addon/__init__.py",
            "tools/retopo_addon/blender_manifest.toml",
            "tools/retopo_toolkit/README.md",
            "tools/retopo_toolkit/LICENSE.txt",
            "tools/retopo_toolkit/NOTICE.txt",
            "docs/features/retopology-toolkit.md",
            "docs/research/retopo-planes-1-6-3-clean-room.md",
            "docs/research/retopo-planes-complete-capability-matrix.md",
            "tools/tests/test_retopo_engine.py",
            "tools/tests/test_retopo_differential.py",
            "tools/tests/test_retopo_contract_regressions.py",
            "tools/tests/test_petik_compat.py",
            "tools/tests/test_package_retopology_toolkit.py",
            "tools/tests/fixtures/retopo_reference.json",
        }
        self.assertTrue(required <= set(pkg.PACKAGE_FILES))
        forbidden = {".blend", ".dll", ".exe", ".fbx", ".npz", ".png", ".pyc"}
        self.assertFalse([name for name in pkg.PACKAGE_FILES if Path(name).suffix.lower() in forbidden])

    def test_petik_preserves_upstream_files_except_recorded_compat_fixes(self):
        expected = {
            "petik_preferences.py": "6a3a17b0266033ecdd059daebd4ac7299bffce7e437c3cd9913fd51acf324497",
            "blender_manifest.toml": "838e5f8f31c2a71f6ab125bf61f91d4935b74b340f1b0f2ef9914801bf2c1468",
        }
        for name, digest in expected.items():
            source = ROOT / "tools/petik/Petik" / name
            normalized = source.read_bytes().replace(b"\r\n", b"\n")
            self.assertEqual(digest, hashlib.sha256(normalized).hexdigest())
        notice = (ROOT / "tools/petik/Petik/NOTICE.txt").read_text(encoding="utf-8")
        for digest in (
            "70394d62ab45732773d59e7bee1e80604a543474dcf64e46944b76d37f0e48c2",
            "f7fe9402aeaaa8830d280a42ae397b9348dc45a507a8129f83932c30fa4b2a4f",
            *expected.values(),
        ):
            self.assertIn(digest, notice)
        self.assertIn("compatibility", notice.lower())

    def test_builder_is_repeatable_and_contains_exact_manifest_and_installers(self):
        with tempfile.TemporaryDirectory() as folder:
            first = Path(folder) / "first.zip"
            second = Path(folder) / "second.zip"
            digest = pkg.build_package(ROOT, first)
            pkg.build_package(ROOT, second)
            self.assertEqual(first.read_bytes(), second.read_bytes())
            self.assertEqual(digest, hashlib.sha256(first.read_bytes()).hexdigest())
            with zipfile.ZipFile(first) as archive:
                self.assertIsNone(archive.testzip())
                names = set(archive.namelist())
                expected = {
                    *(Path("retopo-toolkit") / name for name in pkg.PACKAGE_FILES),
                    "retopo-toolkit/installable/taom_retopology.zip",
                    "retopo-toolkit/installable/Petik.zip",
                    "retopo-toolkit/manifest.json",
                    "retopo-toolkit/README.md",
                    "retopo-toolkit/LICENSE.txt",
                    "retopo-toolkit/NOTICE.txt",
                }
                self.assertEqual({str(path).replace("\\", "/") for path in expected}, names)
                manifest = json.loads(archive.read("retopo-toolkit/manifest.json"))
                self.assertEqual(len(pkg.PACKAGE_FILES), len(manifest["files"]))
                for relative, recorded in manifest["files"].items():
                    self.assertEqual(
                        hashlib.sha256((ROOT / relative).read_bytes().replace(b"\r\n", b"\n").replace(b"\r", b"\n")).hexdigest(), recorded
                    )
                for installer, prefix in (
                    ("taom_retopology.zip", "taom_retopology/"),
                    ("Petik.zip", "Petik/"),
                ):
                    nested = archive.read("retopo-toolkit/installable/" + installer)
                    nested_path = Path(folder) / installer
                    nested_path.write_bytes(nested)
                    with zipfile.ZipFile(nested_path) as addon:
                        self.assertTrue(all(name.startswith(prefix) for name in addon.namelist()))
                        self.assertIn(prefix + "blender_manifest.toml", addon.namelist())
                        self.assertIn(prefix + "LICENSE.txt", addon.namelist())
                        self.assertNotIn(prefix + "RetopoPlanes.blend", addon.namelist())
                        if prefix.startswith("taom"):
                            self.assertIn(prefix + "retopo_engine/engine.py", addon.namelist())
                            self.assertIn(prefix + "retopo_operators.py", addon.namelist())

    def test_builder_is_identical_for_lf_and_crlf_checkouts(self):
        with tempfile.TemporaryDirectory() as folder:
            roots = [Path(folder) / "lf", Path(folder) / "crlf"]
            for root in roots:
                for relative in pkg.PACKAGE_FILES:
                    source = ROOT / relative
                    target = root / relative
                    target.parent.mkdir(parents=True, exist_ok=True)
                    data = source.read_bytes().replace(b"\r\n", b"\n").replace(b"\r", b"\n")
                    if root.name == "crlf":
                        data = data.replace(b"\n", b"\r\n")
                    target.write_bytes(data)
            first = Path(folder) / "lf.zip"
            second = Path(folder) / "crlf.zip"
            pkg.build_package(roots[0], first)
            pkg.build_package(roots[1], second)
            self.assertEqual(first.read_bytes(), second.read_bytes())

    def test_missing_source_fails_before_writing(self):
        with tempfile.TemporaryDirectory() as folder:
            output = Path(folder) / "missing.zip"
            original = list(pkg.PACKAGE_FILES)
            try:
                pkg.PACKAGE_FILES.append("tools/retopo_toolkit/does-not-exist.txt")
                with self.assertRaises(FileNotFoundError):
                    pkg.build_package(ROOT, output)
                self.assertFalse(output.exists())
            finally:
                pkg.PACKAGE_FILES[:] = original

    def test_recipient_commands_name_actual_modules(self):
        text = (ROOT / "tools/retopo_toolkit/README.md").read_text(encoding="utf-8")
        for token in (
            "tools.tests.test_retopo_engine",
            "tools.tests.test_retopo_differential",
            "tools.tests.test_retopo_contract_regressions",
            "tools.tests.test_package_retopology_toolkit",
            "tools/blender/retopo_selftest.py",
            "tools/blender/petik_selftest.py",
            "tools.package_retopology_toolkit",
            "taom_retopology.zip",
            "Petik.zip",
            "5.2.3",
        ):
            self.assertIn(token, text)


if __name__ == "__main__":
    unittest.main()
