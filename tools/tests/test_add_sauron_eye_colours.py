"""Tests for tools/oneoff/add_sauron_eye_colours.py: only the sauron race gains the gold and red eye bands."""
import os
import sys
import unittest
import xml.etree.ElementTree as ET

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "oneoff"))
import add_sauron_eye_colours as asc  # noqa: E402

GRADIENT = ('\t\t\t<eye_color_gradient_points>\n\t\t\t\t<eye_color_gradient_point\n'
            '\t\t\t\t\tpoint="0.02, 0.01, 0.01" />\n\t\t\t</eye_color_gradient_points>\n')
DOC = ('<base>\n\t<race\n\t\tid="elf">\n\t\t<skin>\n' + GRADIENT + '\t\t</skin>\n\t</race>\n'
       '\t<race\n\t\tid="sauron">\n\t\t<skin>\n' + GRADIENT + '\t\t</skin>\n\t\t<skin>\n' + GRADIENT
       + '\t\t</skin>\n\t</race>\n</base>\n')


def points(text, race):
    root = ET.fromstring(text)
    r = [x for x in root.iter("race") if x.get("id") == race][0]
    return [[p.get("point") for p in g.iter("eye_color_gradient_point")] for g in r.iter("eye_color_gradient_points")]


class PlanTests(unittest.TestCase):
    def test_every_sauron_skin_gains_gold_then_red(self):
        new, changed, skipped = asc.plan(DOC)
        self.assertEqual((changed, skipped), (2, 0))
        for pts in points(new, "sauron"):
            self.assertEqual(pts, ["0.02, 0.01, 0.01", asc.GOLD, asc.GOLD, asc.RED, asc.RED])

    def test_the_elf_race_is_untouched(self):
        new, _, _ = asc.plan(DOC)
        self.assertEqual(points(new, "elf"), [["0.02, 0.01, 0.01"]])

    def test_a_second_run_changes_nothing(self):
        once, _, _ = asc.plan(DOC)
        twice, changed, skipped = asc.plan(once)
        self.assertEqual((twice, changed, skipped), (once, 0, 2))

    def test_line_endings_are_kept(self):
        new, _, _ = asc.plan(DOC)
        self.assertNotIn("\r", new)
        crlf, _, _ = asc.plan(DOC.replace("\n", "\r\n"))
        self.assertNotIn("\r\r", crlf)
        self.assertEqual(crlf.count("\n"), crlf.count("\r\n"))

    def test_a_missing_race_is_refused(self):
        with self.assertRaises(SystemExit):
            asc.plan("<base><race id=\"elf\"></race></base>")

    @staticmethod
    def _with_stops(n):
        many = "".join('<eye_color_gradient_point point="0.%02d, 0.1, 0.1" />' % i for i in range(n))
        return ('<base><race id="sauron"><skin><eye_color_gradient_points>' + many
                + '</eye_color_gradient_points></skin></race></base>')

    def test_a_gradient_past_the_engine_cap_is_refused(self):
        with self.assertRaisesRegex(SystemExit, "33 eye colour stops"):
            asc.plan(self._with_stops(29))

    def test_a_gradient_reaching_exactly_the_engine_cap_is_accepted(self):
        _, changed, _ = asc.plan(self._with_stops(28))
        self.assertEqual(changed, 1)

    def test_a_race_whose_skins_hold_no_gradient_is_refused(self):
        with self.assertRaises(SystemExit):
            asc.plan('<base><race id="sauron"><skin></skin><skin></skin></race></base>')

    def test_a_race_with_no_skins_is_refused(self):
        with self.assertRaises(SystemExit):
            asc.plan('<base><race id="sauron"></race></base>')


class CheckTests(unittest.TestCase):
    def _write(self, text):
        import tempfile
        fd, path = tempfile.mkstemp(suffix=".xml")
        os.close(fd)
        open(path, "wb").write(text.encode("utf-8"))
        self.addCleanup(os.remove, path)
        return path

    def test_check_fails_while_a_skin_lacks_the_bands(self):
        self.assertEqual(asc.main(["--skins", self._write(DOC), "--check"]), 1)

    def test_check_passes_once_every_skin_has_them(self):
        done, _, _ = asc.plan(DOC)
        self.assertEqual(asc.main(["--skins", self._write(done), "--check"]), 0)


if __name__ == "__main__":
    unittest.main()
