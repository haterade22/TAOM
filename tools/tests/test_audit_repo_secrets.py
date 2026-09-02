#!/usr/bin/env python3
"""Unit tests for the repo-wide secret layer of tools/audit_claude_config.py.

Run:  python -m unittest discover -s tools/tests -p "test_*.py"

Secret-shaped tokens are ASSEMBLED FROM FRAGMENTS at runtime, never written as a
literal on one source line. The auditor scans every tracked file including this
one, and it matches per line, so a literal here would make the test suite the
loudest finding in the repo. Same reasoning as the fragment trick in
test_audit_skillspector.py, different hazard.
"""
import os
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import audit_claude_config as audit  # noqa: E402

# Shaped like the real thing, matches the rule, is not a credential.
ANTHROPIC_SHAPED = "sk-" + "ant-" + "api03-AbCdEfGhIjKlMnOpQrStUvWx"
AWS_SHAPED = "AK" + "IA" + "QRSTUVWX1234ABCD"


class TrackedPathRuleTests(unittest.TestCase):
    """A file whose NAME is key material should never be tracked at all."""

    def _rule(self, rel):
        got = audit._tracked_path_rule(rel)
        return got[0] if got else None

    def test_private_key_extensions_are_critical(self):
        for rel in ("certs/server.pem", "a/b/deploy.key", "x.p12", "x.pfx",
                    "x.ppk", "x.jks", "prod.keystore", "backup.gpg"):
            with self.subTest(rel=rel):
                self.assertEqual(self._rule(rel), "secret-tracked-key")
                self.assertEqual(audit._tracked_path_rule(rel)[1], "CRITICAL")

    def test_bare_ssh_key_names(self):
        for rel in ("id_rsa", ".ssh/id_ed25519", "keys/id_ecdsa", "id_dsa"):
            with self.subTest(rel=rel):
                self.assertEqual(self._rule(rel), "secret-tracked-key")

    def test_env_files(self):
        for rel in (".env", "tools/.env", ".env.local", ".env.production"):
            with self.subTest(rel=rel):
                self.assertEqual(self._rule(rel), "secret-tracked-env")

    def test_env_templates_are_not_findings(self):
        for rel in ("tools/.env.example", ".env.sample", ".env.template", ".env.dist"):
            with self.subTest(rel=rel):
                self.assertIsNone(audit._tracked_path_rule(rel))

    def test_credential_files(self):
        for rel in (".npmrc", ".pypirc", ".netrc", "_netrc",
                    "credentials.json", "config/secrets.json"):
            with self.subTest(rel=rel):
                self.assertEqual(self._rule(rel), "secret-tracked-cred")

    def test_public_certs_and_ordinary_files_are_clean(self):
        # .crt/.cer/.der are public certificate material, not secrets. The
        # .gitignore ignores them for tidiness; the auditor must not cry wolf.
        for rel in ("site.crt", "ca.cer", "ca.der", "Main/Features/x.cs",
                    "docs/env.md", "Main/_Module/ModuleData/troops.xml",
                    "release.asc", "notes.keynote"):
            with self.subTest(rel=rel):
                self.assertIsNone(audit._tracked_path_rule(rel))


class RepoSecretScanTests(unittest.TestCase):
    """Content scan over a fake tracked-file list."""

    def _run(self, files, already=None, **kw):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            for rel, content in files.items():
                p = root / rel
                p.parent.mkdir(parents=True, exist_ok=True)
                if isinstance(content, bytes):
                    p.write_bytes(content)
                else:
                    p.write_text(content, encoding="utf-8")
            res = audit.Result()
            audit.scan_repo_secrets(root, list(files), res, already or set(), **kw)
            return res

    def test_finds_a_key_in_an_ordinary_source_file(self):
        res = self._run({"tools/deploy.ps1": '$key = "' + ANTHROPIC_SHAPED + '"\n'})
        self.assertEqual([f.rule for f in res.findings], ["secret-anthropic"])
        self.assertEqual(res.findings[0].severity, "CRITICAL")
        self.assertEqual(res.findings[0].path, "tools/deploy.ps1")
        self.assertEqual(res.findings[0].line, 1)

    def test_finds_a_key_in_a_csharp_file(self):
        res = self._run({"Main/Svc.cs": 'const string K = "' + AWS_SHAPED + '";\n'})
        self.assertEqual([f.rule for f in res.findings], ["secret-aws"])

    def test_masks_the_value_it_reports(self):
        res = self._run({"a.py": 'k = "' + ANTHROPIC_SHAPED + '"\n'})
        self.assertNotIn(ANTHROPIC_SHAPED, res.findings[0].snippet)
        self.assertIn("...", res.findings[0].snippet)

    def test_clean_file_is_silent(self):
        res = self._run({"Main/Ok.cs": "public sealed class Ok { }\n"})
        self.assertEqual(res.findings, [])

    def test_already_scanned_paths_are_not_rescanned(self):
        files = {"CLAUDE.md": 'key: "' + ANTHROPIC_SHAPED + '"\n'}
        res = self._run(files, already={"CLAUDE.md"})
        self.assertEqual(res.findings, [])
        self.assertEqual(res.scanned_files, 0)

    def test_binary_files_are_skipped(self):
        payload = b"\x00\x01\x02" + ANTHROPIC_SHAPED.encode() + b"\x00"
        res = self._run({"art/mesh.tpac": payload})
        self.assertEqual(res.findings, [])

    def test_oversized_files_are_skipped(self):
        big = ("x" * 200 + "\n") * 6000  # > 1 MB of harmless text
        res = self._run({"data/huge.xml": big + ANTHROPIC_SHAPED}, max_bytes=1_000_000)
        self.assertEqual(res.findings, [])

    def test_missing_file_does_not_crash(self):
        with tempfile.TemporaryDirectory() as td:
            res = audit.Result()
            audit.scan_repo_secrets(Path(td), ["gone.txt"], res, set())
            self.assertEqual(res.findings, [])

    def test_audit_allow_still_suppresses(self):
        line = "`" + ANTHROPIC_SHAPED + "` <!-- audit-allow: secret-anthropic -->\n"
        res = self._run({"docs/x.md": line})
        self.assertEqual(res.findings, [])
        self.assertEqual(len(res.suppressed), 1)

    def test_tracked_path_rule_fires_without_reading_the_file(self):
        res = self._run({"certs/server.pem": "not actually a key\n"})
        self.assertIn("secret-tracked-key", {f.rule for f in res.findings})

    def test_scanned_file_count_increments(self):
        res = self._run({"a.cs": "// ok\n", "b.cs": "// ok\n"})
        self.assertEqual(res.scanned_files, 2)


class CollectTrackedTests(unittest.TestCase):

    def test_returns_none_outside_a_git_work_tree(self):
        with tempfile.TemporaryDirectory() as td:
            self.assertIsNone(audit.collect_tracked(Path(td)))

    def test_lists_this_repo(self):
        root = Path(__file__).resolve().parents[2]
        rels = audit.collect_tracked(root)
        if rels is None:
            self.skipTest("git not available")
        self.assertIn("CLAUDE.md", rels)
        self.assertIn("tools/audit_claude_config.py", rels)
        self.assertTrue(all("\\" not in r for r in rels), "paths must be forward-slashed")


class PrefilterEquivalenceTests(unittest.TestCase):
    """The whole-text prefilter is an optimization; it must not change results."""

    def _scan(self, text):
        res = audit.Result()
        audit.scan_secrets(Path("x"), "x.md", text, res)
        return [(f.rule, f.line) for f in res.findings]

    def test_hit_on_a_late_line_is_still_found(self):
        text = "\n".join(["ordinary line"] * 500 + ['k = "' + ANTHROPIC_SHAPED + '"'])
        self.assertEqual(self._scan(text), [("secret-anthropic", 501)])

    def test_no_hint_means_no_finding(self):
        self.assertEqual(self._scan("\n".join(["ordinary line"] * 500)), [])

    def test_generic_secret_literal_still_fires(self):
        self.assertEqual(self._scan('password = "hunter2hunter2"'), [("secret-generic", 1)])


if __name__ == "__main__":
    unittest.main()
