"""docs/reviews/lessons/*.md merge with git's union driver, CHANGELOG.md never does (WP142, D18)."""
import pathlib
import subprocess
import unittest

REPO = pathlib.Path(__file__).resolve().parents[2]


def merge_attr(path):
    out = subprocess.run(["git", "check-attr", "merge", "--", path], cwd=REPO,
                         capture_output=True, text=True, check=True).stdout
    return out.strip().rsplit(": ", 1)[-1]


class LessonsMergeUnionTests(unittest.TestCase):
    def test_every_lessons_file_merges_with_union(self):
        files = sorted((REPO / "docs/reviews/lessons").glob("*.md"))
        self.assertTrue(files)
        for f in files:
            self.assertEqual(merge_attr(f.relative_to(REPO).as_posix()), "union", f.name)

    def test_changelog_never_merges_with_union(self):
        self.assertNotEqual(merge_attr("CHANGELOG.md"), "union")


if __name__ == "__main__":
    unittest.main()
