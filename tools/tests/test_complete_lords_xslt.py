"""complete_lords_xslt.py regenerates lords.xslt from scratch, so the #758 vanilla-wanderer retag must come
from the generator itself, or a regeneration silently puts Calradian wanderers back in the spawn pool."""
import sys
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import complete_lords_xslt  # noqa: E402

RETAG_MATCH = "NPCCharacter[@is_template='true' and @occupation='Wanderer']/@occupation"
XSL = "{http://www.w3.org/1999/XSL/Transform}"


class GenerateXsltTests(unittest.TestCase):
    def test_generate_xslt_emits_the_vanilla_wanderer_retag(self):
        root = ET.fromstring(complete_lords_xslt.generate_xslt({}, []).encode("utf-8"))

        retag = [t for t in root.iter(f"{XSL}template") if t.get("match") == RETAG_MATCH]

        self.assertEqual(1, len(retag), "regenerating lords.xslt would drop the #758 retag")
        attribute = retag[0].find(f"{XSL}attribute")
        self.assertEqual("occupation", attribute.get("name"))
        self.assertEqual("NotAssigned", attribute.text)

    def test_shipped_lords_xslt_carries_the_generators_retag_verbatim(self):
        shipped = Path(__file__).resolve().parents[2] / "Main" / "_Module" / "ModuleData" / "lords.xslt"
        text = shipped.read_bytes().decode("utf-8-sig").replace("\r\n", "\n")

        self.assertIn("\n".join(complete_lords_xslt.VANILLA_WANDERER_RETAG), text,
                      "lords.xslt and the generator's retag block have drifted apart")


if __name__ == "__main__":
    unittest.main()
