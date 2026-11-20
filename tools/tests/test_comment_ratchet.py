import os
import sys
import tempfile
import unittest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

import comment_ratchet  # noqa: E402


class CommentLineCountTests(unittest.TestCase):
    # Same cases as CommentRatchetGateTests.CommentLineCount_CountsEachLineThatHoldsAComment.
    CASES = [
        ("int x;\n", 0),
        ("// one\nint x;\n", 1),
        ("int x; // trailing\n", 1),
        ("/// <summary>Doc.</summary>\nint x;\n", 0),
        ("//// divider\n", 1),
        ("/* a\n b\n c */ int x;\n", 3),
        ("int x; /* a */ // b\n", 1),
        ("// a\r\n// b\r\n", 2),
        ("", 0),
        ("/// doc\rclass C { }\r// added\r", 1),
        ("/// doc\u0085// added\u2028// b\u2029", 2),
    ]

    def test_cases_match_the_csharp_gate(self):
        for source, expected in self.CASES:
            with self.subTest(source=source):
                self.assertEqual(expected, comment_line_count(source))

    def test_bom_and_utf16_files_decode(self):
        with tempfile.TemporaryDirectory() as folder:
            utf8 = os.path.join(folder, "a.cs")
            utf16 = os.path.join(folder, "b.cs")
            with open(utf8, "wb") as handle:
                handle.write(b"\xef\xbb\xbf// a\n")
            with open(utf16, "wb") as handle:
                handle.write("// b\n".encode("utf-16"))
            self.assertEqual(1, comment_line_count(comment_ratchet.read_text(utf8)))
            self.assertEqual(1, comment_line_count(comment_ratchet.read_text(utf16)))
            for name, encoding in (("c.cs", "utf-32-le"), ("d.cs", "utf-32-be")):
                path = os.path.join(folder, name)
                with open(path, "wb") as handle:
                    handle.write("\ufeff// e\n// f\n".encode(encoding))
                self.assertEqual(2, comment_line_count(comment_ratchet.read_text(path)), encoding)

    def test_scan_missing_root_raises(self):
        with tempfile.TemporaryDirectory() as repo:
            for root in comment_ratchet.ROOTS[:-1]:
                os.makedirs(os.path.join(repo, root))
            with self.assertRaises(FileNotFoundError):
                comment_ratchet.scan(repo)

    def test_scan_skips_obj_bin_and_non_cs_files(self):
        with tempfile.TemporaryDirectory() as repo:
            for root in comment_ratchet.ROOTS:
                os.makedirs(os.path.join(repo, root))
            for relative in ("Main/A.CS", "Main/obj/X.cs", "Client/bin/Y.cs", "Main/notes.txt"):
                path = os.path.join(repo, *relative.split("/"))
                os.makedirs(os.path.dirname(path), exist_ok=True)
                with open(path, "w", encoding="utf-8") as handle:
                    handle.write("// c\n")
            self.assertEqual({"Main/A.CS": 1}, comment_ratchet.scan(repo))

    def test_wrong_root_exits_2_and_writes_nothing(self):
        with tempfile.TemporaryDirectory() as repo:
            out = os.path.join(repo, "baseline.txt")
            self.assertEqual(2, comment_ratchet.main(["--regen", "--out", out], repo=repo))
            self.assertFalse(os.path.exists(out))

    def test_baseline_omits_zero_counts_and_sorts(self):
        with tempfile.TemporaryDirectory() as folder:
            out = os.path.join(folder, "baseline.txt")
            rows = comment_ratchet.write_baseline({"Main/B.cs": 2, "Main/A.cs": 1, "Main/C.cs": 0}, out)
            with open(out, encoding="utf-8") as handle:
                body = [line for line in handle.read().splitlines() if not line.startswith("#")]
            self.assertEqual(2, rows)
            self.assertEqual(["Main/A.cs\t1", "Main/B.cs\t2"], body)


def comment_line_count(source):
    return comment_ratchet.comment_line_count(source)


if __name__ == "__main__":
    unittest.main()
