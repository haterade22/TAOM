import hashlib
from pathlib import Path
import tempfile
import unittest
import zipfile

from tools.hair_engine import packaging


class ArchiveTests(unittest.TestCase):
    def make_zip(self, folder, entries):
        path = Path(folder) / "package.zip"
        with zipfile.ZipFile(path, "w") as archive:
            for name, body in entries.items():
                archive.writestr(name, body)
        return path

    def test_valid_archive_installs_and_preserves_backup(self):
        with tempfile.TemporaryDirectory() as folder:
            archive = self.make_zip(folder, {"engine/a.txt": "new"})
            destination = Path(folder) / "installed"
            destination.mkdir()
            (destination / "a.txt").write_text("old", encoding="utf-8")
            result = packaging.install_archive(archive, destination)
            self.assertEqual((destination / "engine" / "a.txt").read_text(encoding="utf-8"), "new")
            self.assertEqual((Path(result["backup"]) / "a.txt").read_text(encoding="utf-8"), "old")

    def test_path_traversal_is_rejected_before_writes(self):
        with tempfile.TemporaryDirectory() as folder:
            archive = self.make_zip(folder, {"../escape.txt": "bad"})
            destination = Path(folder) / "installed"
            with self.assertRaisesRegex(ValueError, "unsafe"):
                packaging.install_archive(archive, destination)
            self.assertFalse(destination.exists())
            self.assertFalse((Path(folder) / "escape.txt").exists())

    def test_absolute_member_is_rejected(self):
        with tempfile.TemporaryDirectory() as folder:
            archive = self.make_zip(folder, {"/absolute.txt": "bad"})
            with self.assertRaisesRegex(ValueError, "unsafe"):
                packaging.install_archive(archive, Path(folder) / "installed")

    def test_hash_mismatch_preserves_existing_install(self):
        with tempfile.TemporaryDirectory() as folder:
            archive = self.make_zip(folder, {"a.txt": "new"})
            destination = Path(folder) / "installed"
            destination.mkdir()
            (destination / "a.txt").write_text("old", encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "SHA-256"):
                packaging.install_archive(archive, destination, expected_sha256="0" * 64)
            self.assertEqual((destination / "a.txt").read_text(encoding="utf-8"), "old")

    def test_expected_hash_is_accepted(self):
        with tempfile.TemporaryDirectory() as folder:
            archive = self.make_zip(folder, {"a.txt": "new"})
            digest = hashlib.sha256(archive.read_bytes()).hexdigest()
            result = packaging.install_archive(archive, Path(folder) / "installed", digest)
            self.assertEqual(result["files"], ["a.txt"])

    def test_corrupt_member_preserves_existing_install(self):
        with tempfile.TemporaryDirectory() as folder:
            archive = self.make_zip(folder, {"a.txt": "new"})
            destination = Path(folder) / "installed"
            destination.mkdir()
            (destination / "a.txt").write_text("old", encoding="utf-8")
            with zipfile.ZipFile(archive, "a") as handle:
                handle.writestr("b.txt", "second")
            data = bytearray(archive.read_bytes())
            index = data.index(b"second")
            data[index] = ord("X")
            archive.write_bytes(data)
            with self.assertRaises(Exception):
                packaging.install_archive(archive, destination)
            self.assertEqual((destination / "a.txt").read_text(encoding="utf-8"), "old")


if __name__ == "__main__":
    unittest.main()
