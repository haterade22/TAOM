"""lords.xslt must not regrow the runaway skill-block indentation removed for #950 (5e45d2782 on the
1.5.x line, re-derived for 1.4.x by plans/118-campaign-load-time.md): no <skills> block sits deeper than the
</face> line before it, and no line carries more than 200 leading spaces."""
import unittest
from pathlib import Path

LORDS_XSLT = Path(__file__).resolve().parents[2] / "Main" / "_Module" / "ModuleData" / "lords.xslt"
MAX_LEADING_SPACES = 200


def leading_spaces(line):
    return len(line) - len(line.lstrip(" "))


class LordsXsltIndentationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.lines = LORDS_XSLT.read_text(encoding="utf-8-sig").replace("\r\n", "\n").split("\n")

    def test_no_skills_block_sits_deeper_than_its_face_line(self):
        deeper = []
        for i, line in enumerate(self.lines):
            if line.strip() != "<skills>":
                continue
            j = i - 1
            while j >= 0 and not self.lines[j].strip():
                j -= 1
            if j >= 0 and self.lines[j].strip() == "</face>" and leading_spaces(line) > leading_spaces(self.lines[j]):
                deeper.append(i + 1)
        self.assertEqual([], deeper[:10], f"{len(deeper)} <skills> blocks sit deeper than their </face> line")

    def test_no_line_has_runaway_indentation(self):
        runaway = [i + 1 for i, line in enumerate(self.lines) if leading_spaces(line) > MAX_LEADING_SPACES]
        self.assertEqual([], runaway[:10], f"{len(runaway)} lines carry more than {MAX_LEADING_SPACES} leading spaces")


if __name__ == "__main__":
    unittest.main()
