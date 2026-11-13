#!/usr/bin/env python
"""Check our open BannerlordCoop issues and PRs against the Coop team's rules.

The rules are in .claude/skills/coop-upstream/SKILL.md (Coodsz and Curzek, 2026-11-13). This reads
the open issues and PRs that the logged-in gh user authored in Bannerlord-Coop-Team/BannerlordCoop
and prints each rule miss. Read-only: it changes nothing on GitHub.

Usage:  python tools/check_coop_upstream_rules.py [--repo OWNER/NAME] [--author LOGIN]
Exit:   0 no finding, 1 findings, 3 UNCHECKED (gh failed or returned no data).
"""
import argparse
import json
import re
import subprocess
import sys

REPO = "Bannerlord-Coop-Team/BannerlordCoop"
PR_BASES = ("bannerlord-1.5.x", "development")
CLOSING = re.compile(r"\b(?:Resolves|Closes|Fixes)\s+#\d+", re.IGNORECASE)
AREA_TAG = re.compile(r"^\[[A-Z][A-Z0-9 _/-]*\]")
LOG_LABELS = ("has-client-logs", "has-server-logs")

QUERY = """
query($q: String!) {
  search(query: $q, type: ISSUE, first: 100) {
    nodes {
      __typename
      ... on Issue { number title body url assignees(first: 10) { nodes { login } }
                     issueType { name } labels(first: 30) { nodes { name } } }
      ... on PullRequest { number title body url baseRefName assignees(first: 10) { nodes { login } } }
    }
  }
}
"""


def audit_pr(pr):
    """Return the rule misses of one PR (a flattened dict)."""
    body = pr.get("body") or ""
    found = []
    if "## PR Checklist" not in body:
        found.append("body is not built from .github/pull_request_template.md")
    if not re.search(r"^## Bot Changelog Entry\s*$", body, re.MULTILINE):
        found.append("missing the exact '## Bot Changelog Entry' heading")
    if not CLOSING.search(body):
        found.append("no 'Resolves #N' with an issue number")
    if not pr.get("assignees"):
        found.append("no assignee (--assignee \"@me\")")
    if pr.get("baseRefName") not in PR_BASES:
        found.append(f"base is '{pr.get('baseRefName')}', not bannerlord-1.5.x or development")
    return found


def audit_issue(issue):
    """Return the rule misses of one issue (a flattened dict)."""
    found = []
    if not AREA_TAG.match(issue.get("title") or ""):
        found.append("title has no area tag such as [SAVES]")
    if not issue.get("assignees"):
        found.append("no assignee (--assignee \"@me\")")
    if not issue.get("issueType"):
        found.append("no issue type (Bug, Feature, Task, Epic)")
    if issue.get("issueType") == "Bug":
        labels = issue.get("labels") or []
        for label in LOG_LABELS:
            if label not in labels:
                found.append(f"bug without the '{label}' label: attach the log, then add the label")
    return found


def flatten(node):
    """Turn one GraphQL search node into the dict the audit functions read."""
    issue_type = node.get("issueType")
    return {
        "kind": node.get("__typename"),
        "number": node.get("number"),
        "title": node.get("title"),
        "body": node.get("body"),
        "url": node.get("url"),
        "baseRefName": node.get("baseRefName"),
        "assignees": [a["login"] for a in (node.get("assignees") or {}).get("nodes", [])],
        "issueType": issue_type["name"] if issue_type else None,
        "labels": [lab["name"] for lab in (node.get("labels") or {}).get("nodes", [])],
    }


def fetch(repo, author):
    query = f"repo:{repo} author:{author} is:open"
    proc = subprocess.run(["gh", "api", "graphql", "-f", f"query={QUERY}", "-f", f"q={query}"],
                          capture_output=True, text=True, encoding="utf-8")
    if proc.returncode != 0:
        raise RuntimeError(proc.stderr.strip() or f"gh exit {proc.returncode}")
    return [flatten(n) for n in json.loads(proc.stdout)["data"]["search"]["nodes"] if n]


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--repo", default=REPO)
    parser.add_argument("--author", default="@me")
    args = parser.parse_args(argv)
    try:
        items = fetch(args.repo, args.author)
    except (RuntimeError, OSError, ValueError, KeyError) as exc:
        print(f"UNCHECKED: could not read {args.repo}: {exc}")
        return 3
    misses = 0
    for item in sorted(items, key=lambda i: i["number"]):
        audit = audit_pr if item["kind"] == "PullRequest" else audit_issue
        for finding in audit(item):
            misses += 1
            print(f"{item['kind']} #{item['number']} {item['title'][:60]}: {finding}")
    print(f"{len(items)} open items checked, {misses} rule misses")
    return 1 if misses else 0


if __name__ == "__main__":
    sys.exit(main())
