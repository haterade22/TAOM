#!/usr/bin/env python3
"""tools/_loc_sources.py is the one table of TAOM English sources the translator, the template generator
and the name generator read. These pin it to the files that exist, so a new strings file cannot be wired
into one tool and forgotten in another (2026-10-02: four hand-kept copies had drifted to 22, 15, 13, 10)."""
import os
import sys
import unittest
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import generate_name_localization_strings as gen  # noqa: E402
import rebuild_translation_files as rebuild  # noqa: E402
from _loc_sources import TAOM_SOURCES  # noqa: E402
from translate_with_claude import _parse_string_xml  # noqa: E402

MODULE_DATA = Path(__file__).resolve().parents[2] / "Main" / "_Module" / "ModuleData"


class LocSourcesTests(unittest.TestCase):
    def test_Table_EqualsEveryEnglishStringsFileOutsideLanguages(self):
        on_disk = {p.relative_to(MODULE_DATA).as_posix() for p in MODULE_DATA.rglob("*.xml")
                   if "Languages" not in p.parts
                   and (p.name == "global_strings.xml" or p.name.endswith("_strings.xml"))}
        self.assertEqual({src for src, _ in TAOM_SOURCES}, on_disk)

    def test_Table_HasNoDuplicateSourceOrTarget(self):
        self.assertEqual(len({s for s, _ in TAOM_SOURCES}), len(TAOM_SOURCES))
        self.assertEqual(len({t for _, t in TAOM_SOURCES}), len(TAOM_SOURCES))

    def test_Table_ModuleStringsAheadOfGlobalStrings(self):
        # key_owners gives a shared key to the first source.
        order = [s for s, _ in TAOM_SOURCES]
        self.assertLess(order.index("taom_module_strings.xml"), order.index("global_strings.xml"))

    def test_EveryGeneratorOutput_IsATableRow(self):
        rows = {(MODULE_DATA / s).resolve() for s, _ in TAOM_SOURCES}
        for cat, spec in gen.CATEGORIES.items():
            self.assertIn(spec["output"].resolve(), rows, cat)

    def test_RebuildTool_ListEqualsTheTableWithTheLocale(self):
        self.assertEqual([(src, tpl.format(locale="rus-RU")) for src, tpl in TAOM_SOURCES], rebuild.taom_sources("rus-RU"))

    def test_RebuildTool_ASharedKeyBelongsToItsOwnersFileOnly(self):
        # taom_module_strings.xml and global_strings.xml share no key on 1.4.x, so this names its own files.
        module, keybinds = MODULE_DATA / "taom_module_strings.xml", MODULE_DATA / "global_strings.xml"
        owners = {"shared_key": module}
        self.assertFalse(rebuild.owned_elsewhere(owners, "shared_key", module))
        self.assertTrue(rebuild.owned_elsewhere(owners, "shared_key", keybinds))


if __name__ == "__main__":
    unittest.main()
