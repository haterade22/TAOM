#!/usr/bin/env python3
"""The ModuleData regions a generator owns must still match a fresh run of that generator.

Run:  python -m unittest tools.tests.test_generator_regions_in_sync

A hand edit inside a generated region passes every data gate and is silently reverted by the
generator's next --apply. This shipped three times in three days (2026-10-16 KEYforce javelin rows,
2026-10-18 orc binder region, 2026-10-18 Arthedain template and start war, #818); the lessons said
"check who owns the region" and that check was skipped each time. Each case here runs the owner's
own --check, so the edit fails /verify instead of waiting for a regeneration.

promote_borrowed_cultures.py --check is covered for every promoter target except the two it refuses. It reads the culture start points from the live
TAOM_Map settlements.xml, so those cases skip, never fake a pass, when that install is absent.
"""
import os
import subprocess
import sys
import unittest

TOOLS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
REPO = os.path.dirname(TOOLS)

sys.path.insert(0, TOOLS)
import importlib.util  # noqa: E402
import generate_new_faction_kingdoms as kingdoms  # noqa: E402
import promote_borrowed_cultures as promoter  # noqa: E402
from _gamedir import game_dir  # noqa: E402

# (generator, its check arguments) for every generator that owns a marker region in the repo's
# ModuleData and has a --check that passes today. The realm list is the kingdom generator's own.
OWNERS = (
    [("generate_%s_troops.py" % c, ["--check"]) for c in kingdoms.REGENERATABLE]
    + [("generate_new_faction_kingdoms.py", ["--only", c, "--check"]) for c in kingdoms.REGENERATABLE]
)

# Promoter targets whose --check is not run here. Lindon: 13 enlist_lindon_* ids already shipped,
# so the promoter refuses it. Blue Craig: one id is minted twice, so the promoter refuses it.
PROMOTER_REFUSES = {"lindon", "bluecraig"}

# Owners whose output depends on the live (unversioned) TAOM_Map install. Culture blocks rendered
# from vanilla (a target with a culture_xslt) also need lxml.
PROMOTER_CHECKED = sorted(set(promoter.TARGETS) - PROMOTER_REFUSES)
LIVE_OWNERS = [("promote_borrowed_cultures.py", ["--only", c, "--check"]) for c in PROMOTER_CHECKED]
NEEDS_LXML = {c for c in PROMOTER_CHECKED if promoter.TARGETS[c].get("culture_xslt")}

LIVE_SETTLEMENTS = os.path.join(game_dir(r"E:/Steam/steamapps/common/Mount & Blade II Bannerlord"),
                                "Modules", "TAOM_Map", "ModuleData", "settlements.xml")


class GeneratorRegionsInSyncTests(unittest.TestCase):
    def test_every_owned_region_matches_a_fresh_run(self):
        live = os.path.exists(LIVE_SETTLEMENTS)
        self.assertLessEqual(set(kingdoms.REGENERATABLE), set(promoter.TARGETS))
        for script, args in OWNERS + LIVE_OWNERS:
            with self.subTest(script=script, args=" ".join(args)):
                if (script, args) in LIVE_OWNERS and not live:
                    self.skipTest("live TAOM_Map install not present: %s" % LIVE_SETTLEMENTS)
                if script.startswith("promote_") and args[1] in NEEDS_LXML and importlib.util.find_spec("lxml") is None:
                    self.skipTest("lxml not installed: the Dale-cut culture blocks are rendered with it")
                run = subprocess.run(
                    [sys.executable, "-B", os.path.join(TOOLS, script)] + args,
                    cwd=REPO, capture_output=True, text=True, timeout=600)
                self.assertEqual(
                    run.returncode, 0,
                    "%s %s reports drift: a hand edit inside its region, or the generator changed "
                    "without --apply.\n%s%s" % (script, " ".join(args), run.stdout[-2000:], run.stderr[-2000:]))


if __name__ == "__main__":
    unittest.main()
