#!/usr/bin/env python
"""Unit tests for tools/check_coop_upstream_rules.py (the Coop team's issue and PR rules).

Run:  python -m unittest discover -s tools/tests -t .
"""
import os
import sys
import unittest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import check_coop_upstream_rules as rules  # noqa: E402

GOOD_PR_BODY = "## PR Checklist\n\n## What is the new behavior?\nResolves #3982\n\n## Bot Changelog Entry\n"


def pr(**over):
    base = {"number": 1, "title": "Fix it", "body": GOOD_PR_BODY, "baseRefName": "bannerlord-1.5.x",
            "assignees": ["haterade22"]}
    base.update(over)
    return base


def issue(**over):
    base = {"number": 2, "title": "[SAVES] Hero is empty", "body": "text", "assignees": ["haterade22"],
            "issueType": "Bug", "labels": ["has-client-logs", "has-server-logs"]}
    base.update(over)
    return base


class PullRequestRules(unittest.TestCase):
    def test_a_pr_that_follows_every_rule_has_no_finding(self):
        self.assertEqual([], rules.audit_pr(pr()))

    def test_a_pr_on_development_is_allowed(self):
        self.assertEqual([], rules.audit_pr(pr(baseRefName="development")))

    def test_the_bot_changelog_heading_is_required(self):
        found = rules.audit_pr(pr(body="## PR Checklist\nResolves #3\n"))
        self.assertTrue(any("Bot Changelog Entry" in f for f in found), found)

    def test_a_bare_issue_number_does_not_count_as_resolves(self):
        found = rules.audit_pr(pr(body="## PR Checklist\nFor #3982\n## Bot Changelog Entry\n"))
        self.assertTrue(any("Resolves #N" in f for f in found), found)

    def test_closes_counts_as_a_closing_keyword(self):
        self.assertEqual([], rules.audit_pr(pr(body="## PR Checklist\nCloses #7\n## Bot Changelog Entry\n")))

    def test_the_empty_template_placeholder_does_not_count(self):
        found = rules.audit_pr(pr(body="## PR Checklist\nResolves #\n## Bot Changelog Entry\n"))
        self.assertTrue(any("Resolves #N" in f for f in found), found)

    def test_a_pr_without_the_template_is_reported(self):
        found = rules.audit_pr(pr(body="Resolves #3\n## Bot Changelog Entry\n"))
        self.assertTrue(any("template" in f for f in found), found)

    def test_a_pr_needs_an_assignee(self):
        found = rules.audit_pr(pr(assignees=[]))
        self.assertTrue(any("assignee" in f for f in found), found)

    def test_a_pr_on_a_task_branch_is_reported(self):
        found = rules.audit_pr(pr(baseRefName="task/4593-merge-development"))
        self.assertTrue(any("base" in f for f in found), found)


class IssueRules(unittest.TestCase):
    def test_an_issue_that_follows_every_rule_has_no_finding(self):
        self.assertEqual([], rules.audit_issue(issue()))

    def test_the_title_needs_an_area_tag(self):
        found = rules.audit_issue(issue(title="Hero is empty"))
        self.assertTrue(any("area tag" in f for f in found), found)

    def test_an_issue_needs_an_assignee(self):
        found = rules.audit_issue(issue(assignees=[]))
        self.assertTrue(any("assignee" in f for f in found), found)

    def test_an_issue_needs_a_type(self):
        found = rules.audit_issue(issue(issueType=None))
        self.assertTrue(any("type" in f for f in found), found)

    def test_a_bug_without_log_labels_is_reported(self):
        found = rules.audit_issue(issue(labels=["has-client-logs"]))
        self.assertTrue(any("has-server-logs" in f for f in found), found)

    def test_a_task_needs_no_logs(self):
        self.assertEqual([], rules.audit_issue(issue(issueType="Task", labels=[])))


class GraphQlShape(unittest.TestCase):
    def test_a_search_node_flattens_to_the_audit_shape(self):
        node = {"__typename": "Issue", "number": 5, "title": "[SAVES] x", "body": "b", "url": "u",
                "assignees": {"nodes": [{"login": "haterade22"}]}, "issueType": {"name": "Bug"},
                "labels": {"nodes": [{"name": "has-client-logs"}]}}
        flat = rules.flatten(node)
        self.assertEqual(["haterade22"], flat["assignees"])
        self.assertEqual("Bug", flat["issueType"])
        self.assertEqual(["has-client-logs"], flat["labels"])

    def test_a_node_with_no_type_flattens_to_none(self):
        node = {"__typename": "Issue", "number": 5, "title": "t", "body": "", "url": "u",
                "assignees": {"nodes": []}, "issueType": None, "labels": {"nodes": []}}
        self.assertIsNone(rules.flatten(node)["issueType"])


if __name__ == "__main__":
    unittest.main()
