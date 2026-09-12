"""Offline contracts for the provider-neutral review workflow (no AI calls)."""

import copy
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

from tools import reviewctl


class ReviewWorkflowTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.repo = Path(self.temp.name) / "repo"
        self.repo.mkdir()
        self.git("init", "-q")
        self.git("config", "user.email", "test@example.invalid")
        self.git("config", "user.name", "Review workflow test")
        self.git("config", "core.autocrlf", "false")
        self.put(".gitignore", ".ai/runs/\n")
        self.put(".ai/policy.md", "Trusted test policy\n")
        self.put(".ai/scopes.md", "Review all routed files\n")
        self.routing = {
            "version": 1,
            "minimum_independent_providers": 2,
            "instructions": [".ai/policy.md", ".ai/scopes.md"],
            "lanes": [
                {"id": "general", "patterns": ["*"], "checks": []},
                {"id": "code", "patterns": ["*.cs"], "checks": ["build", "tests"]},
                {"id": "data", "patterns": ["*.xml", "*.json"], "checks": ["data"]},
            ],
        }
        self.put(".ai/routing.json", json.dumps(self.routing))
        self.put("Main/a.cs", "old code\n")
        self.put("Main/old.xml", "<old/>\n")
        self.base = self.commit("base")
        self.put("Main/a.cs", "new code\n")
        self.put("Main/new.xml", "<new/>\n")
        (self.repo / "Main/old.xml").unlink()
        self.head = self.commit("candidate")
        self.builder = {"provider": "anthropic", "model": "actual-builder-model", "session": "build-1"}
        self.packet = self.prepare()

    def git(self, *args):
        result = subprocess.run(["git", "-C", str(self.repo), *args], capture_output=True, check=True)
        return result.stdout.decode("utf-8").strip()

    def put(self, path, contents):
        target = self.repo / path
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(contents, encoding="utf-8")

    def commit(self, message):
        self.git("add", "--all")
        self.git("commit", "-qm", message)
        return self.git("rev-parse", "HEAD")

    def prepare(self, **kwargs):
        return reviewctl.create_packet(self.repo, self.base, self.head, self.builder,
                                       "Verify the requested behavior and regressions.", **kwargs)

    def report(self, provider="openai", session="review-1"):
        return {
            "version": 1, "review_id": session,
            "packet_id": self.packet["packet_id"], "head_sha": self.head,
            "reviewer": {"provider": provider, "model": "actual-review-model", "session": session},
            "status": "complete", "coverage": copy.deepcopy(self.packet["scope"]),
            "summary": "Examined behavior, consumers, negative cases, and test oracles.",
            "findings": [], "unverified": [],
        }

    def checks(self):
        return {
            "version": 1, "packet_id": self.packet["packet_id"], "head_sha": self.head,
            "results": [{"id": name, "status": "pass", "command": "actual command",
                         "exit_code": 0, "evidence": "ci://run/123/job/" + name}
                        for name in self.packet["required_checks"]],
        }

    def reports(self):
        return [self.report(), self.report("moonshot", "review-2")]

    def assess(self, reports=None, checks=None, dispositions=None):
        return reviewctl.assess(self.packet, self.reports() if reports is None else reports,
                                self.checks() if checks is None else checks, dispositions)

    def finding(self):
        return {"id": "F1", "severity": "HIGH", "path": "Main/a.cs", "line": 1,
                "rule": "regression", "claim": "Behavior fails for an empty input.",
                "impact": "The requested operation cannot finish.",
                "recommendation": "Handle the empty case at the service boundary.",
                "evidence": "Main/a.cs:1 and reproduced test case EmptyInput.",
                "engine_dependent": False, "engine_evidence": ""}

    def dispositions(self, decision="refuted", kind="human"):
        return {"version": 1, "packet_id": self.packet["packet_id"], "head_sha": self.head,
                "decisions": [{"finding": "review-1/F1", "decision": decision,
                               "actor": {"kind": kind, "name": "maintainer"},
                               "rationale": "The independent reproduction disproves the claim.",
                               "evidence": "test://independent-reproduction/456"}]}

    def test_packet_routes_additions_modifications_and_deletions(self):
        self.assertEqual({c["path"]: c["status"] for c in self.packet["changes"]},
                         {"Main/a.cs": "M", "Main/new.xml": "A", "Main/old.xml": "D"})
        self.assertEqual(self.packet["scope"]["code"], ["Main/a.cs"])
        self.assertEqual(self.packet["scope"]["data"], ["Main/new.xml", "Main/old.xml"])
        self.assertEqual(self.packet["required_checks"], ["build", "data", "tests"])

    def test_packet_routes_unknown_extensions_to_general(self):
        self.put("Assets/unknown.weird", "opaque")
        self.head = self.commit("unknown asset")
        self.assertIn("Assets/unknown.weird", self.prepare()["scope"]["general"])

    def test_rename_keeps_both_old_and_new_paths_in_scope(self):
        self.git("mv", "Main/new.xml", "Main/renamed.xml")
        self.head = self.commit("rename")
        packet = reviewctl.create_packet(self.repo, self.packet["head_sha"], self.head,
                                          self.builder, "Review rename")
        self.assertEqual(packet["scope"]["data"], ["Main/new.xml", "Main/renamed.xml"])

    def test_full_audit_includes_unchanged_files(self):
        packet = self.prepare(full=True)
        self.assertIn(".gitignore", packet["scope"]["general"])
        self.assertNotIn("Main/old.xml", packet["scope"]["general"])

    def test_dirty_tracked_file_prevents_packet(self):
        self.put("Main/a.cs", "uncommitted")
        with self.assertRaisesRegex(reviewctl.ReviewError, "clean"):
            self.prepare()

    def test_untracked_file_prevents_packet(self):
        self.put("new.txt", "untracked")
        with self.assertRaisesRegex(reviewctl.ReviewError, "clean"):
            self.prepare()

    def test_staged_file_prevents_packet(self):
        self.put("Main/a.cs", "staged")
        self.git("add", "Main/a.cs")
        with self.assertRaisesRegex(reviewctl.ReviewError, "clean"):
            self.prepare()

    def test_empty_change_packet_is_not_an_approval(self):
        with self.assertRaisesRegex(reviewctl.ReviewError, "empty"):
            reviewctl.create_packet(self.repo, self.head, self.head, self.builder, "Empty diff")

    def test_head_must_match_the_checkout(self):
        with self.assertRaisesRegex(reviewctl.ReviewError, "checkout"):
            reviewctl.create_packet(self.repo, self.base, self.base, self.builder, "Wrong checkout", full=True)

    def test_manifest_tampering_is_rejected(self):
        packet = copy.deepcopy(self.packet)
        packet["scope"]["code"] = []
        with self.assertRaisesRegex(reviewctl.ReviewError, "manifest"):
            reviewctl.validate_packet(self.repo, packet)

    def test_manifest_json_types_cannot_change_without_invalidation(self):
        packet = copy.deepcopy(self.packet)
        packet["version"] = True
        with self.assertRaisesRegex(reviewctl.ReviewError, "manifest"):
            reviewctl.validate_packet(self.repo, packet)

    def test_self_rehashed_manifest_cannot_drop_scope(self):
        packet = copy.deepcopy(self.packet)
        packet["scope"].pop("data")
        packet.pop("packet_id")
        packet["packet_id"] = reviewctl.digest(packet)
        with self.assertRaisesRegex(reviewctl.ReviewError, "manifest"):
            reviewctl.validate_packet(self.repo, packet)

    def test_commit_after_review_invalidates_packet(self):
        self.put("Main/a.cs", "fixed")
        self.commit("fix")
        with self.assertRaisesRegex(reviewctl.ReviewError, "checkout"):
            reviewctl.validate_packet(self.repo, self.packet)

    def test_unchanged_packet_revalidates(self):
        reviewctl.validate_packet(self.repo, self.packet)

    def test_packet_writer_refuses_to_overwrite(self):
        output = Path(self.temp.name) / "packet"
        reviewctl.write_packet(output, self.packet)
        self.assertTrue((output / "prompt.md").is_file())
        with self.assertRaises(reviewctl.ReviewError):
            reviewctl.write_packet(output, self.packet)

    def test_templates_never_claim_work_has_been_done(self):
        template = reviewctl.report_template(self.packet)
        self.assertEqual(template["status"], "incomplete")
        self.assertTrue(all(not files for files in template["coverage"].values()))
        self.assertTrue(self.assess([template]))

    def test_two_different_non_builder_providers_can_pass(self):
        self.assertEqual(self.assess(), [])

    def test_multiple_slices_can_supply_complete_independent_coverage(self):
        reports = []
        for provider in ("openai", "moonshot"):
            for lane, paths in self.packet["scope"].items():
                report = self.report(provider, provider + "-" + lane)
                report["coverage"] = {lane: paths}
                reports.append(report)
        self.assertEqual(self.assess(reports), [])

    def test_builder_provider_does_not_count_as_independent(self):
        self.assertTrue(self.assess([self.report(), self.report("Anthropic", "review-2")]))

    def test_same_provider_with_another_model_is_not_two_independent_providers(self):
        reports = [self.report(), self.report("OpenAI", "review-2")]
        reports[1]["reviewer"]["model"] = "another-model"
        self.assertTrue(self.assess(reports))

    def test_blank_and_unknown_provider_names_are_rejected(self):
        for provider in (" ", "unknown", "TODO", "anthropic "):
            with self.subTest(provider=provider):
                report = self.report(provider)
                self.assertTrue(self.assess([report, self.reports()[1]]))

    def test_duplicate_review_ids_and_sessions_do_not_count(self):
        reports = self.reports()
        reports[1]["review_id"] = reports[0]["review_id"]
        self.assertTrue(self.assess(reports))
        reports = self.reports()
        reports[1]["reviewer"]["session"] = reports[0]["reviewer"]["session"]
        self.assertTrue(self.assess(reports))

    def test_review_on_an_old_sha_is_rejected(self):
        reports = self.reports()
        reports[1]["head_sha"] = self.base
        self.assertTrue(self.assess(reports))

    def test_wrong_packet_report_is_rejected(self):
        reports = self.reports()
        reports[1]["packet_id"] = "another-packet"
        self.assertTrue(self.assess(reports))

    def test_incomplete_or_unverified_review_is_not_clean(self):
        for key, value in (("status", "incomplete"), ("unverified", ["Installed engine unavailable"])):
            reports = self.reports()
            reports[1][key] = value
            self.assertTrue(self.assess(reports))

    def test_coverage_cannot_silently_drop_a_deleted_file(self):
        reports = self.reports()
        reports[1]["coverage"]["data"].remove("Main/old.xml")
        self.assertTrue(self.assess(reports))

    def test_unknown_coverage_path_is_rejected(self):
        reports = self.reports()
        reports[1]["coverage"]["general"].append("not-in-packet.txt")
        self.assertTrue(self.assess(reports))

    def test_missing_check_cannot_pass(self):
        checks = self.checks()
        checks["results"].pop()
        self.assertTrue(self.assess(checks=checks))

    def test_skipped_failed_or_unproven_check_cannot_pass(self):
        for key, value in (("status", "skipped"), ("exit_code", 1), ("evidence", ""), ("exit_code", False)):
            with self.subTest(key=key, value=value):
                checks = self.checks()
                checks["results"][0][key] = value
                self.assertTrue(self.assess(checks=checks))

    def test_stale_checks_cannot_pass(self):
        checks = self.checks()
        checks["head_sha"] = self.base
        self.assertTrue(self.assess(checks=checks))

    def test_duplicate_check_is_rejected(self):
        checks = self.checks()
        checks["results"].append(copy.deepcopy(checks["results"][0]))
        self.assertTrue(self.assess(checks=checks))

    def test_unresolved_finding_blocks_even_if_low_severity(self):
        reports = self.reports()
        finding = self.finding()
        finding["severity"] = "LOW"
        reports[0]["findings"] = [finding]
        self.assertTrue(self.assess(reports))

    def test_duplicate_finding_ids_are_rejected(self):
        reports = self.reports()
        reports[0]["findings"] = [self.finding(), self.finding()]
        self.assertTrue(self.assess(reports, dispositions=self.dispositions()))

    def test_unverified_engine_claim_cannot_be_refuted_into_a_pass(self):
        reports = self.reports()
        finding = self.finding()
        finding["engine_dependent"] = True
        reports[0]["findings"] = [finding]
        self.assertTrue(self.assess(reports, dispositions=self.dispositions()))

    def test_human_disposition_can_resolve_a_false_positive(self):
        reports = self.reports()
        reports[0]["findings"] = [self.finding()]
        self.assertEqual(self.assess(reports, dispositions=self.dispositions()), [])

    def test_ai_cannot_sign_a_human_risk_acceptance(self):
        reports = self.reports()
        reports[0]["findings"] = [self.finding()]
        self.assertTrue(self.assess(reports, dispositions=self.dispositions("accepted_risk", "ai")))

    def test_disposition_cannot_claim_a_fix_on_the_same_sha(self):
        reports = self.reports()
        reports[0]["findings"] = [self.finding()]
        self.assertTrue(self.assess(reports, dispositions=self.dispositions("fixed")))

    def test_unknown_disposition_finding_is_rejected(self):
        self.assertTrue(self.assess(dispositions=self.dispositions()))

    def test_missing_or_malformed_report_fields_are_not_silently_defaulted(self):
        for report in ({}, [], None, {"version": 1, "coverage": []}):
            self.assertTrue(self.assess([report]))

    def test_repo_contract_is_well_formed(self):
        reviewctl.lint(Path(__file__).resolve().parents[2])

    def test_real_routing_covers_each_non_csharp_surface(self):
        routing = reviewctl.lint(Path(__file__).resolve().parents[2])
        expected = {
            "Main/a.cs": "managed", "Main/_Module/ModuleData/configs/test.json": "moduledata",
            "Main/_Module/GUI/Prefabs/Test.xml": "ui", "tools/check.py": "tooling",
            ".kimi-code/config.toml": "ai-harness", ".claude/hooks/check.sh": "claude-hooks",
            "Dependencies/Test/a.cpp": "native-dependencies", "Assets/a.tpac": "assets-localization",
            "docs/Test.md": "docs-policy", "future/unknown.ext": "general",
        }
        scope = reviewctl.route(expected, routing)
        for path, lane in expected.items():
            self.assertIn(path, scope[lane])
            self.assertIn(path, scope["general"])

    def test_duplicate_json_object_keys_are_rejected(self):
        path = Path(self.temp.name) / "duplicate.json"
        path.write_text('{"version": 1, "version": 2}', encoding="utf-8")
        with self.assertRaisesRegex(reviewctl.ReviewError, "Duplicate JSON key"):
            reviewctl.load_json(path)

    def test_cli_exports_and_validates_then_rejects_stale_evidence(self):
        script = Path(reviewctl.__file__).resolve()
        out = Path(self.temp.name) / "cli-packet"
        def run(*args):
            return subprocess.run([sys.executable, str(script), "--repo", str(self.repo), *args],
                                  capture_output=True, text=True, timeout=30)
        result = run("prepare", "--base", self.base, "--head", self.head,
                     "--builder-provider", self.builder["provider"],
                     "--builder-model", self.builder["model"],
                     "--builder-session", self.builder["session"],
                     "--task", self.packet["task"], "--out", str(out))
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(reviewctl.load_json(out / "manifest.json"), self.packet)
        for name, document in (("r1.json", self.reports()[0]), ("r2.json", self.reports()[1]),
                               ("checks.json", self.checks())):
            (out / name).write_text(json.dumps(document), encoding="utf-8")
        args = ("validate", "--packet", str(out), "--report", str(out / "r1.json"),
                "--report", str(out / "r2.json"), "--checks", str(out / "checks.json"))
        result = run(*args)
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertIn("advisory", result.stdout)
        report = self.report()
        report["head_sha"] = self.base
        (out / "r1.json").write_text(json.dumps(report), encoding="utf-8")
        result = run(*args)
        self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
        self.assertIn("stale", result.stdout)


if __name__ == "__main__":
    unittest.main()
