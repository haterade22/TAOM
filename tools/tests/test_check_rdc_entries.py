#!/usr/bin/env python3
"""tools/check_rdc_entries.py on a synthetic module tree (#616).

Run:  python -m unittest tools.tests.test_check_rdc_entries
"""
import contextlib
import io
import os
import sys
import tempfile
import unittest
import uuid
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import check_rdc_entries as cre  # noqa: E402

GUID = uuid.UUID("12345678-1234-5678-1234-567812345678")


def tpac(path, guid=GUID):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(b"TPAC" + (2).to_bytes(4, "little") + guid.bytes_le + b"\0" * 12)


class CheckRdcEntriesTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.module = Path(self._tmp.name)

    def run_main(self, *argv):
        out = io.StringIO()
        with contextlib.redirect_stdout(out):
            code = cre.main(["--module", str(self.module), *argv])
        return code, out.getvalue()

    def test_no_runtime_data_cache_folder_is_a_failure(self):
        tpac(self.module / "Assets" / "creature" / "x_geo.tpac")
        code, out = self.run_main()
        self.assertEqual(1, code)
        self.assertIn("no RuntimeDataCache folder", out)

    def test_a_package_with_its_entry_passes(self):
        tpac(self.module / "Assets" / "creature" / "x_geo.tpac")
        (self.module / "RuntimeDataCache").mkdir()
        (self.module / "RuntimeDataCache" / f"{GUID}.rdc").write_bytes(b"")
        code, out = self.run_main()
        self.assertEqual(0, code)
        self.assertIn("without-rdc=0", out)

    def test_a_package_without_its_entry_is_listed_and_an_rtemp_does_not_count(self):
        tpac(self.module / "Assets" / "creature" / "x_geo.tpac")
        (self.module / "RuntimeDataCache").mkdir()
        (self.module / "RuntimeDataCache" / f"{GUID}.rdc.rtemp").write_bytes(b"")
        code, out = self.run_main()
        self.assertEqual(1, code)
        self.assertIn("NO RDC", out)
        self.assertIn("rtemp pending", out)

    def test_materials_are_not_checked_by_default(self):
        tpac(self.module / "Assets" / "x_mtl.tpac")
        (self.module / "RuntimeDataCache").mkdir()
        code, out = self.run_main()
        self.assertEqual(1, code)  # a gate that checked nothing is not a pass
        self.assertIn("packages=0", out)

    def test_a_skeletal_animation_master_without_an_entry_is_not_a_failure(self):
        # The Kit writes no .rdc for a master; measured 2026-09-18 (warg 56/56, elephant 31/31).
        master = self.module / "Assets" / "creature" / "x_anm.tpac"
        tpac(master)
        with open(master, "ab") as fh:
            fh.write(cre.SKELETAL_ANIMATION_TYPE)
        (self.module / "RuntimeDataCache").mkdir()
        code, out = self.run_main()
        self.assertEqual(0, code)
        self.assertIn("animation-masters-without-entry=1", out)


if __name__ == "__main__":
    unittest.main()
