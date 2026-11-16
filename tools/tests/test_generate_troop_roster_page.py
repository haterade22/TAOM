"""generate_troop_roster_page reads items by the catalogue's module rule (#1372).

NavalDLC never loads beside TAOM, and it redefines SandBoxCore items as bare stubs
(`northern_round_shield` with no Type). Read in folder order, the stub won and the page showed
an Umbar shield with no hit points.
"""
from __future__ import annotations

import shutil
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import generate_troop_roster_page as page  # noqa: E402
import melee_catalogue as mc  # noqa: E402


def write(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")


@unittest.skipIf(mc.LET is None, "needs lxml")
class PlainItemTests(unittest.TestCase):
    def test_an_incompatible_modules_stub_does_not_replace_the_real_item(self):
        tmp = Path(tempfile.mkdtemp())
        self.addCleanup(shutil.rmtree, tmp, ignore_errors=True)
        modules = tmp / "Modules"
        write(modules / "NavalDLC" / "ModuleData" / "items.xml", '<Items><Item id="round_shield"/></Items>\n')
        write(modules / "SandBoxCore" / "ModuleData" / "shields.xml",
              '<Items><Item id="round_shield" Type="Shield"/></Items>\n')
        own = tmp / "own" / "SubModule.xml"
        write(own, '<Module><Id value="TAOM"/><IncompatibleModules><Module Id="NavalDLC"/>'
                   '</IncompatibleModules></Module>\n')
        old = mc.TAOM_SUBMODULE
        mc.TAOM_SUBMODULE = own
        self.addCleanup(setattr, mc, "TAOM_SUBMODULE", old)
        self.assertEqual(page.load_plain_items(modules)["round_shield"].type, "Shield")


if __name__ == "__main__":
    unittest.main()
