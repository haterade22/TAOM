"""Offline regression checks for Codex onboarding and shared workflow links."""

from pathlib import Path
import re
import unittest


ROOT = Path(__file__).resolve().parents[2]
SKILLS = {
    "taom-build": ".ai/roles/builder.md",
    "taom-review": ".ai/roles/reviewer.md",
    "taom-adjudicate": ".ai/roles/adjudicator.md",
    "taom-research": "docs/ai-includes/taleworlds-research-guide.md",
    "taom-verify": ".ai/verification.md",
}
GUIDE = "docs/ai-includes/codex-operating-guide.md"


def local_links(document):
    """Resolve ordinary local Markdown links, omitting fenced examples and URLs."""
    content = document.read_text(encoding="utf-8-sig")
    content = re.sub(r"(?ms)^```[^\n]*\n.*?^```\s*$", "", content)
    targets = []
    for target in re.findall(r"\]\(([^)]+)\)", content):
        if re.match(r"[a-zA-Z][a-zA-Z0-9+.-]*:", target):
            continue
        relative = target.split("#", 1)[0]
        if relative:
            targets.append((document.parent / relative).resolve())
    return targets


class CodexDocumentationTests(unittest.TestCase):
    def test_expected_workflows_are_discoverable_and_point_to_canonical_instructions(self):
        # Independent required set: deleting a skill must not remove its test input.
        for name, canonical in SKILLS.items():
            with self.subTest(skill=name):
                skill = ROOT / ".agents" / "skills" / name / "SKILL.md"
                self.assertTrue(skill.is_file(), f"Missing discoverable skill: {skill}")
                links = local_links(skill)
                self.assertIn((ROOT / canonical).resolve(), links)
                self.assertIn((ROOT / ".ai/policy.md").resolve(), links)
                self.assertIn((ROOT / GUIDE).resolve(), links)

    def test_skill_metadata_is_unique_and_minimal(self):
        seen = set()
        for name in SKILLS:
            path = ROOT / ".agents/skills" / name / "SKILL.md"
            self.assertTrue(path.is_file(), str(path))
            content = path.read_text(encoding="utf-8")
            match = re.match(r"\A---\n(.*?)\n---\n", content, re.DOTALL)
            self.assertIsNotNone(match, str(path))
            # These instruction-only skills intentionally use two plain scalar fields.
            fields = dict(line.split(": ", 1) for line in match[1].splitlines())
            self.assertEqual(set(fields), {"name", "description"})
            self.assertEqual(fields["name"], name)
            self.assertNotIn(name, seen)
            self.assertTrue(fields["description"].strip())
            seen.add(name)

    def test_onboarding_is_reachable_from_each_entry_point(self):
        target = (ROOT / GUIDE).resolve()
        for entry in ("AGENTS.md", "README.md", ".codex/README.md", ".ai/README.md",
                      ".ai/providers.md", "docs/INDEX.md", "docs/reference/codex-integration.md"):
            with self.subTest(entry=entry):
                self.assertTrue(target in local_links(ROOT / entry),
                                f"{entry} must link directly to the Codex operating guide")

    def test_new_documentation_and_skill_links_resolve_inside_the_repository(self):
        documents = [ROOT / GUIDE, ROOT / ".codex/README.md"]
        documents += sorted((ROOT / ".ai").glob("*.md"))
        documents += sorted((ROOT / ".ai/roles").glob("*.md"))
        documents += [ROOT / ".agents/skills" / name / "SKILL.md" for name in SKILLS]
        for document in documents:
            self.assertTrue(document.is_file(), str(document))
            for target in local_links(document):
                with self.subTest(document=document.name, target=str(target)):
                    self.assertTrue(target.is_relative_to(ROOT), "Link escapes repository")
                    self.assertTrue(target.exists(), "Broken local documentation link")


if __name__ == "__main__":
    unittest.main()
