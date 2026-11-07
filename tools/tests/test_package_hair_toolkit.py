from pathlib import Path
import tempfile
import unittest
import zipfile

from tools import package_hair_toolkit as pkg


ROOT = Path(__file__).resolve().parents[2]


class HairToolkitPackageTests(unittest.TestCase):
    def test_manifest_has_required_sources_and_no_proprietary_payloads(self):
        required = {
            "tools/blender/hair_tool_pipeline.py",
            "tools/blender/add_mesh_lods.py",
            "tools/blender/transfer_upper_mesh_weights.py",
            "tools/groom_contact_sheet.py",
            "tools/oneoff/append_dwarf_grooms.py",
            "tools/hair_toolkit/README.md",
            "tools/hair_toolkit/LICENSE.txt",
            "tools/hair_toolkit/NOTICE.txt",
            ".claude/skills/groom-hair/SKILL.md",
            "docs/features/hair-tool-workflow.md",
            "docs/research/hair-tool-4-6-1.md",
        }
        self.assertTrue(required <= set(pkg.PACKAGE_FILES))
        forbidden = {".blend", ".dll", ".exe", ".fbx", ".npz", ".png", ".pyc", ".zip"}
        self.assertFalse([name for name in pkg.PACKAGE_FILES if Path(name).suffix.lower() in forbidden])

    def test_builder_writes_exact_manifest_and_is_repeatable(self):
        with tempfile.TemporaryDirectory() as folder:
            first = Path(folder) / "first.zip"
            second = Path(folder) / "second.zip"
            pkg.build_package(ROOT, first)
            pkg.build_package(ROOT, second)
            self.assertEqual(first.read_bytes(), second.read_bytes())
            with zipfile.ZipFile(first) as archive:
                expected = {(Path("hair-toolkit") / name).as_posix() for name in pkg.PACKAGE_FILES}
                self.assertEqual(set(archive.namelist()), expected)

    def test_missing_source_fails_before_writing(self):
        with tempfile.TemporaryDirectory() as folder:
            output = Path(folder) / "missing.zip"
            original = list(pkg.PACKAGE_FILES)
            try:
                pkg.PACKAGE_FILES.append("tools/hair_toolkit/does-not-exist.txt")
                with self.assertRaises(FileNotFoundError):
                    pkg.build_package(ROOT, output)
                self.assertFalse(output.exists())
            finally:
                pkg.PACKAGE_FILES[:] = original

    def test_recipient_commands_name_actual_modules(self):
        text = (ROOT / "tools/hair_toolkit/README.md").read_text(encoding="utf-8")
        for token in ("tools.blender.hair_tool_pipeline", "tools.tests.test_hair_tool_pipeline",
                      "tools.package_hair_toolkit", "4.6.1"):
            self.assertIn(token, text)


if __name__ == "__main__":
    unittest.main()
