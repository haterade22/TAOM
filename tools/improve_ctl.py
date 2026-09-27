#!/usr/bin/env python
"""Run control for /improve: Workflow args, plan status cells, Codex branch prompts, issue filing
and a liveness watch for unattended runs.

  args <fanout|plans|execute|review|draft-issues> --items F [...]   write a Workflow's args JSON
  status <readme> <num> <text>                    set one plan row's Status cell, bytes kept
  codex-prompt --branch B --base R [--tag T] --out P   adversarial review prompt for a branch
  file-issue <draft.md>                           file a drafted issue with gh (maintainer's word only)
  watch --dir P [--stale-min N]                   exit 1 on an agent stalled on a hook ask or silent

`python tools/improve_ctl.py <sub> --help` documents each. Pure stdlib. Reads git, never writes
to it; writes only the file a subcommand names (file-issue also writes the body file it posts).
Every revision it is given is checked with `git rev-parse --verify --end-of-options`, so one that
starts with - is refused, never read as a git option. Exit 0 on success, 1 when the subcommand's
own check fails, 2 on bad input, 3 on an unexpected error.
"""
from __future__ import annotations

import argparse
import datetime
import json
import os
import re
import subprocess
import sys
import time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

import check_public_text  # noqa: E402

WORKFLOWS = ("fanout", "plans", "execute", "review", "draft-issues")
ROLES = ("lane", "checker", "writer", "reviewer", "reviser", "executor", "lead", "convergence",
         "fix", "drafter")
RULES_PATH = ".claude/skills/improve/references/dispatch-rules.md"
PROMPT_FIXED_PATH = ".claude/skills/review-codex/references/prompt-fixed.md"
SUBMODULE_XML = "Main/_Module/SubModule.xml"

# deep-review SKILL.md Step 1 split and Step 2 "Which lenses run".
CODE_EXT = (".cs", ".cpp", ".h", ".hpp", ".csproj", ".vcxproj", ".props", ".targets")
XML_EXT = (".xml", ".xslt", ".xsl", ".xsd", ".mbproj")
SCRIPT_DIRS = ("tools/", ".claude/hooks/")
LENS_ORDER = ("1", "2", "3", "4", "5", "6", "7", "tooling")
GROUP_TITLES = {"cs": "C# and C++", "xml": "XML and XSLT", "scripts": "Scripts and hooks",
                "harness": "Harness", "docs": "Docs and other"}

GENERIC_SUSPECTS = (
    "Fail-safe defaults: any new or changed '?? true' / '?? false', config default or MCM default. "
    "Is the failure direction the safe one? Does a changed MCM default use a renamed setting "
    "(json2 persists old values)?",
    "Stale state across lifecycle boundaries: singletons, statics or caches that survive a save "
    "load, a new campaign or a mission end.",
    "Tests that pass without proving the behaviour: assertions on source text, mocks that test "
    "themselves, Assert.Inconclusive paths that report green.",
    "Dead or no-op code introduced by the change; a gate that can never fire.",
)


class Fail(Exception):
    """A refusal with its exit code: 2 for bad input, 1 when the subcommand's own check fails."""

    def __init__(self, message: str, code: int = 2):
        super().__init__(message)
        self.code = code


def git(cwd, *args: str) -> str:
    try:
        proc = subprocess.run(["git", "-c", "core.quotePath=false", *args], cwd=cwd,
                              capture_output=True, text=True, encoding="utf-8", errors="replace")
    except OSError as exc:
        raise Fail(f"git {' '.join(args)} in {cwd}: {exc}") from exc
    if proc.returncode:
        raise Fail(f"git {' '.join(args)} in {cwd}: {proc.stderr.strip()}")
    return proc.stdout


def toplevel() -> str:
    return git(os.getcwd(), "rev-parse", "--show-toplevel").strip()


def verify_commit(cwd, rev: str, what: str) -> str:
    """The commit a revision names; --end-of-options keeps a leading - from being an option."""
    try:
        return git(cwd, "rev-parse", "-q", "--verify", "--end-of-options", f"{rev}^{{commit}}").strip()
    except Fail:
        raise Fail(f"{what} {rev!r} is not a commit in {cwd}") from None


def read_text(path) -> str:
    """The file's text exactly as stored: BOM and line endings kept."""
    try:
        return Path(path).read_bytes().decode("utf-8")
    except (OSError, UnicodeDecodeError) as exc:
        raise Fail(f"cannot read {path}: {exc}") from exc


def write_out(path, text: str) -> None:
    path = Path(path).resolve()
    path.parent.mkdir(parents=True, exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as handle:
        handle.write(text)
    print(path)


# --- lens routing -------------------------------------------------------------------------------

def is_harness(path: str) -> bool:
    return path.startswith((".claude/", ".ai/")) or path in ("CLAUDE.md", "AGENTS.md")


def kind_of(path: str) -> str:
    low = path.lower()
    if low.endswith(CODE_EXT):
        return "cs"
    if low.endswith(XML_EXT):
        return "xml"
    if path.startswith(SCRIPT_DIRS) and not low.endswith(".md"):
        return "scripts"
    return "harness" if is_harness(path) else "docs"


def group_files(paths) -> dict:
    groups = {kind: [] for kind in GROUP_TITLES}
    for path in paths:
        groups[kind_of(path)].append(path)
    return groups


def lenses_for(paths) -> list:
    kinds = {kind_of(p) for p in paths}
    lenses = {"4", "5", "6"}
    if "cs" in kinds:
        lenses |= {"1", "2", "3"}
    if any(is_harness(p) for p in paths):
        lenses.add("1")
    if "scripts" in kinds:
        lenses |= {"3", "tooling"}
    if "xml" in kinds:
        lenses.add("7")
    return [lens for lens in LENS_ORDER if lens in lenses]


# --- args ---------------------------------------------------------------------------------------

def review_item(number: int, item) -> dict:
    """A review item with its head, its changed files by kind, and its lenses: the ones it
    carries unioned with deep-review's routing for base..head. An empty range is refused."""
    where = f"review item {number}"
    if not isinstance(item, dict) or not item.get("wt") or not item.get("base"):
        raise Fail(f"{where} needs \"wt\" (its worktree) and \"base\"")
    own = item.get("lenses", [])
    if not isinstance(own, list):
        raise Fail(f"{where}: lenses must be a list")
    verify_commit(item["wt"], item["base"], f"{where} base")
    head = item.get("head") or "HEAD"
    tip = verify_commit(item["wt"], head, f"{where} head")
    head = item.get("head") or tip
    rng = f"{item['base']}..{head}"
    names = [n for n in git(item["wt"], "diff", "--name-only", "--end-of-options", rng).splitlines() if n]
    if not names:
        raise Fail(f"{where}: {rng} changes no file, so there is nothing to review")
    wanted = set(lenses_for(names)) | {str(lens) for lens in own}
    lenses = [lens for lens in LENS_ORDER if lens in wanted] + sorted(wanted - set(LENS_ORDER))
    return dict(item, head=head, files=group_files(names), lenses=lenses)


def cmd_args(a) -> int:
    top = toplevel()
    try:
        doc = json.loads(read_text(a.items).lstrip("\ufeff"))
    except ValueError as exc:
        raise Fail(f"{a.items}: not JSON: {exc}") from exc
    out = {}
    if isinstance(doc, dict) and isinstance(doc.get("items"), list):
        out = {k: v for k, v in doc.items() if k != "items"}
        doc = doc["items"]
    if not isinstance(doc, list):
        raise Fail(f"{a.items}: expected an items array, or an object with an \"items\" array")
    model = None
    for pair in a.model or []:
        role, _, model_id = pair.partition("=")
        if role not in ROLES or not model_id:
            raise Fail(f"--model {pair}: expected role=id, role one of {', '.join(ROLES)}")
        model = model or {}
        model[role] = model_id
    # A flag wins; without one, a field the items object carries stands; otherwise the default.
    flags = {"rules": a.rules and read_text(a.rules), "repo": a.repo and os.path.abspath(a.repo),
             "runRoot": a.run_root and os.path.abspath(a.run_root),
             "scratch": a.scratch and os.path.abspath(a.scratch),
             "tmp": a.tmp and os.path.abspath(a.tmp), "date": a.date, "version": a.version,
             "model": model, "pool": a.pool, "maxRounds": a.max_rounds}
    out.update({k: v for k, v in flags.items() if v is not None})
    # TAOM_IMPROVE_ROOT is the worktree root outside the repo, never the run folder.
    root = os.environ.get("TAOM_IMPROVE_ROOT")
    if root:
        out.setdefault("scratch", os.path.abspath(os.path.join(root, "scratch")))
        out.setdefault("tmp", os.path.abspath(os.path.join(root, "scratch", "tmp")))
    if "runRoot" not in out:
        raise Fail("--run-root (the run folder) is required")
    if "scratch" not in out or "tmp" not in out:
        raise Fail("--scratch and --tmp are required unless TAOM_IMPROVE_ROOT is set")
    if "rules" not in out:
        out["rules"] = read_text(os.path.join(top, RULES_PATH))
    if "repo" not in out:
        out["repo"] = str(Path(git(os.getcwd(), "rev-parse", "--path-format=absolute",
                                   "--git-common-dir").strip()).parent)
    if "version" not in out:
        match = re.search(r'<Version\s+value="([^"]+)"', read_text(os.path.join(top, SUBMODULE_XML)))
        if not match:
            raise Fail(f"no <Version value=...> in {SUBMODULE_XML}; pass --version")
        out["version"] = match.group(1)
    out.setdefault("date", datetime.date.today().isoformat())
    out.setdefault("model", {})
    out.setdefault("pool", 4)
    out["items"] = [review_item(n, i) for n, i in enumerate(doc, 1)] if a.workflow == "review" else doc
    text = json.dumps(out, indent=2) + "\n"
    if a.out:
        write_out(a.out, text)
    else:
        sys.stdout.write(text)
    return 0


# --- status -------------------------------------------------------------------------------------

def _cells(row: str) -> list:
    return re.split(r"(?<!\\)\|", row)


def _is_separator(row: str) -> bool:
    inner = _cells(row)[1:-1]
    return bool(inner) and all(re.fullmatch(r"\s*:?-{3,}:?\s*", c) for c in inner)


def set_status(data: bytes, num: str, text: str) -> bytes:
    """Replace the Status cell of plan row `num`; every other byte stays as it was."""
    if "|" in text or "\n" in text or "\r" in text:
        raise Fail("the status text may not hold a | or a line break")
    lines = data.decode("utf-8").splitlines(keepends=True)
    column, hits = None, []
    for i, line in enumerate(lines):
        row = line.rstrip("\r\n")
        if not row.lstrip().startswith("|"):
            column = None
            continue
        if _is_separator(row):
            continue
        cells = _cells(row)
        following = lines[i + 1].rstrip("\r\n") if i + 1 < len(lines) else ""
        if following.lstrip().startswith("|") and _is_separator(following):
            names = [c.strip().lower() for c in cells]
            column = names.index("status") if "status" in names else None
            continue
        if (column is not None and len(cells) > column + 1
                and re.match(rf"\[?{re.escape(num)}\]?(?:\(|$)", cells[1].strip())):
            hits.append((i, row, cells, column))
    if len(hits) != 1:
        where = "not found" if not hits else f"found {len(hits)} times"
        raise Fail(f"plan row {num} {where} in a table with a Status column", code=1)
    i, row, cells, column = hits[0]
    cells[column] = f" {text} "
    lines[i] = "|".join(cells) + lines[i][len(row):]
    return "".join(lines).encode("utf-8")


def cmd_status(a) -> int:
    path = Path(a.readme)
    try:
        data = path.read_bytes()
    except OSError as exc:
        raise Fail(f"cannot read {path}: {exc}") from exc
    path.write_bytes(set_status(data, a.num, a.text))
    print(f"{path}: plan {a.num} Status set")
    return 0


# --- codex-prompt -------------------------------------------------------------------------------

def split_fixed(text: str, source: str = PROMPT_FIXED_PATH) -> tuple:
    """The cheatsheet and lessons blocks: the fenced block under each one's `## ` heading."""
    text = text.replace("\r\n", "\n")
    blocks = []
    for heading in ("TAOM ID CHEATSHEET", "Prior review lessons"):
        section = re.search(rf"^## {re.escape(heading)}[ \t]*\n(.*?)(?=^## |\Z)", text, re.M | re.S)
        fence = section and re.search(r"^```[^\n]*\n(.*?)^```[ \t]*$", section.group(1), re.M | re.S)
        if not fence:
            raise Fail(f"{source}: no fenced block under \"## {heading}\"")
        blocks.append(fence.group(1).rstrip("\n"))
    return tuple(blocks)


def _section(text: str, heading: str) -> str:
    match = re.search(rf"^## {re.escape(heading)}\s*\n(.*?)(?=^## |\Z)", text, re.M | re.S)
    return match.group(1) if match else ""


def _bullets(section: str) -> list:
    """Each `- ` or `* ` item of a section, its indented continuation lines joined on."""
    items = []
    for line in section.splitlines():
        bullet = re.match(r"\s*[-*]\s+(.+)$", line)
        if bullet:
            items.append(bullet.group(1).strip())
        elif items and line[:1].isspace() and line.strip():
            items[-1] += " " + line.strip()
        elif line.strip() and items:
            break
    return items


def find_plan(cwd, branch: str) -> tuple:
    """plans/<num>-*.md on the branch, when the branch's last path part starts with <num>-."""
    match = re.match(r"(\d+)-", branch.rsplit("/", 1)[-1])
    if not match:
        return "", ""
    names = git(cwd, "ls-tree", "--name-only", branch, "plans/").splitlines()
    hits = [n for n in names if n.startswith(f"plans/{match.group(1)}-") and n.endswith(".md")]
    if len(hits) != 1:
        return "", ""
    return hits[0], git(cwd, "show", f"{branch}:{hits[0]}")


def codex_prompt(cwd, branch: str, base: str, tag: str, cheatsheet: str, lessons: str) -> str:
    rng = f"{base}..{branch}"
    names = [n for n in git(cwd, "diff", "--name-only", rng).splitlines() if n]
    stat = (git(cwd, "diff", "--stat", rng).strip().splitlines() or [""])[-1].strip()
    plan_path, plan = find_plan(cwd, branch)
    title = re.search(r"^# Plan \d+: (.+)$", plan, re.M) or re.search(r"^# (.+)$", plan, re.M)
    why = " ".join(_section(plan, "Why this matters").split())
    stops = _bullets(_section(plan, "STOP conditions"))

    out = [f"# Codex adversarial review: branch {branch}", ""]
    if plan_path:
        out += [f"Feature: {title.group(1).strip() if title else plan_path}. {why}".rstrip(), ""]
    else:
        out += [f"Feature: no plan was found for this branch; infer the intent from the diff and "
                f"git log {rng}.", ""]
    if tag:
        out += [f"THIS IS A SECOND REVIEW ({tag}). The branch was already reviewed; the diff {rng} "
                "is the follow-up applied on top of that review, and the decisions it implements "
                "are recorded in the branch's review report under docs/reviews/. Review this diff "
                "only; earlier commits are in scope only where the new diff changes their "
                "behaviour.", ""]
    out += ["HOW TO READ THE CODE (important): the working tree you run in may hold another "
            "session's unrelated uncommitted edits. Review ONLY the branch under test, through git "
            "refs:",
            f"- The change: git diff {rng}",
            f"- Any file as the branch has it: git show {branch}:<path>",
            f"- The base for comparison: git show {base}:<path>",
            "Do NOT modify any file anywhere. Do NOT run builds, tests or the game. Read-only review.",
            "", cheatsheet, "", "READ FIRST:"]
    if plan_path:
        out.append(f"- The plan the change implements: git show {branch}:{plan_path} (intent, "
                   "scope, STOP conditions)")
    out += ["- AGENTS.md (the project's rules; ADR-002 thin entry points, ADR-007 adapters, TDD, "
            "banned constructs)",
            "- Any feature doc under docs/features/ the diff touches (git show on the branch)",
            "- Engine behaviour: AGENTS.md \"Research first\" (docs/reference/engine/, then the "
            "decompile dump; signatures come from the installed DLLs). The dump can lag an engine "
            "bump, so treat it as a guide and say UNVERIFIED where it matters.",
            "", "KNOWN SUSPECTS (CONFIRM or DISPUTE each with file:line evidence):"]
    suspects = [f"From the plan's STOP conditions, did the change hit or mishandle this risk? {s}"
                for s in stops] + list(GENERIC_SUSPECTS)
    out += [f"{n}. {s}" for n, s in enumerate(suspects, 1)]
    out += ["", f"CHANGED FILES ({stat}):"]
    for kind, files in group_files(names).items():
        if files:
            out += [f"{GROUP_TITLES[kind]}:"] + [f"- {f}" for f in files]
    out += ["", "REQUIRED SECTIONS:",
            "1. VANILLA CODE: for every Harmony patch, GameModel override or engine API the diff "
            "touches or relies on, paste the relevant decompile excerpt as a code block and state "
            "what the change assumes about it.",
            "2. DEEP ANALYSIS: walk concrete scenarios through the changed code (first boot, save "
            "load, new campaign in the same process, mission start and end, co-op or dedicated "
            "server where relevant, the failure path of every try/catch the diff adds or changes).",
            "3. CONFIG CROSS-REFERENCE: every ID, path, setting name or config key the diff adds or "
            "reads, checked against its source of truth.",
            "4. FINDINGS OR OBSERVATIONS: numbered, each with severity (P1 crash/data "
            "loss/security, P2 wrong behaviour, P3 quality), file:line on the branch, the "
            "reasoning, and a concrete fix. Say explicitly when the plan itself is wrong.",
            "",
            "QUALITY GATES: cite file:line for every claim; do not flag vanilla-matching code as a "
            "bug; do not assume empire=Rohan; do not skip hard sections; mark engine behaviour you "
            "could not read as UNVERIFIED.",
            "", lessons, "",
            "OUTPUT: return the full review as your FINAL MESSAGE (the dispatcher redirects stdout "
            "into a file; do NOT write any file yourself). The very last line of your final "
            "message must be exactly: END OF CODEX REVIEW"]
    return "\n".join(out) + "\n"


def cmd_codex_prompt(a) -> int:
    top = toplevel()
    verify_commit(top, a.branch, "--branch")
    verify_commit(top, a.base, "--base")
    source = os.path.join(top, PROMPT_FIXED_PATH)
    cheatsheet, lessons = split_fixed(read_text(source), source)
    write_out(a.out, codex_prompt(top, a.branch, a.base, a.tag or "", cheatsheet, lessons))
    return 0


# --- file-issue ---------------------------------------------------------------------------------

def file_issue(path, run=subprocess.run) -> int:
    """File the draft with `gh issue create`; refuse it when it fails the public-text check."""
    path = Path(path)
    lines = read_text(path).lstrip("\ufeff").splitlines()
    if (len(lines) < 3 or not lines[0].startswith("TITLE:") or not lines[1].startswith("LABEL:")
            or lines[2].strip() or not lines[0][6:].strip()):
        raise Fail(f"{path}: a draft starts with a TITLE: line, a LABEL: line and a blank line")
    findings = check_public_text.check_text("\n".join(lines))
    if findings:
        for number, rule, raw in findings:
            print(f"{path}:{number}: {rule}: {raw}", file=sys.stderr)
        print(f"{path}: not filed; fix the draft and rerun", file=sys.stderr)
        return 1
    title, label = lines[0][6:].strip(), lines[1][6:].strip()
    body_file = path.with_name(path.stem + ".body.md")
    with open(body_file, "w", encoding="utf-8", newline="\n") as handle:
        handle.write("\n".join(lines[3:]).strip("\n") + "\n")
    cmd = ["gh", "issue", "create", "--title", title, "--body-file", str(body_file)]
    if label:
        cmd += ["--label", label]
    proc = run(cmd, capture_output=True, text=True, encoding="utf-8", errors="replace")
    if proc.returncode:
        print(f"{path}: gh issue create failed: {(proc.stderr or proc.stdout).strip()}", file=sys.stderr)
        return 1
    print(([line for line in proc.stdout.splitlines() if line.strip()] or [""])[-1].strip())
    return 0


def cmd_file_issue(a) -> int:
    return file_issue(a.draft)


# --- watch --------------------------------------------------------------------------------------

def journal_states(path: Path):
    """agentId -> (state, label) from a workflow journal, None without one. The state is running,
    done, failed, or superseded: its key was started again later (a retry or a resumed run), so
    that attempt is over even with no failed row."""
    if not path.is_file():
        return None
    states, key_of, latest = {}, {}, {}
    for line in path.read_text(encoding="utf-8", errors="replace").splitlines():
        try:
            row = json.loads(line)
        except ValueError:
            continue  # a row still being written
        agent = row.get("agentId") if isinstance(row, dict) else None
        state = {"started": "running", "result": "done", "failed": "failed"}.get(row.get("type")
                                                                                 if agent else None)
        if state:
            states[agent] = (state, row.get("label") or states.get(agent, ("", ""))[1])
        if state == "running":
            key_of[agent] = row.get("key") or agent
            latest[key_of[agent]] = agent
    for agent, (state, label) in states.items():
        if state == "running" and latest[key_of[agent]] != agent:
            states[agent] = ("superseded", label)
    return states


def pending_ask(path: Path):
    """The reason of the transcript's latest PreToolUse hook "ask" when no later tool_result
    answers its toolUseID: the agent waits on a prompt nobody sees. None otherwise."""
    pending = None
    with open(path, encoding="utf-8", errors="replace") as handle:
        for line in handle:
            if "permissionDecision" not in line and "tool_result" not in line:
                continue
            try:
                record = json.loads(line)
            except ValueError:
                continue
            if not isinstance(record, dict):
                continue
            hook = record.get("attachment")
            if isinstance(hook, dict) and hook.get("hookEvent") == "PreToolUse":
                try:
                    said = json.loads(hook.get("stdout") or "{}")
                except ValueError:
                    said = {}
                decision = said.get("hookSpecificOutput") if isinstance(said, dict) else None
                if isinstance(decision, dict) and decision.get("permissionDecision") == "ask":
                    pending = (hook.get("toolUseID"), str(decision.get("permissionDecisionReason") or "ask"))
            message = record.get("message")
            content = message.get("content") if isinstance(message, dict) else None
            if pending and isinstance(content, list) and any(
                    isinstance(c, dict) and c.get("type") == "tool_result"
                    and c.get("tool_use_id") == pending[0] for c in content):
                pending = None
    return pending[1] if pending else None


def cmd_watch(a) -> int:
    root = Path(a.dir)
    if not root.is_dir():
        raise Fail(f"no such folder: {root}")
    now, journals = time.time(), {}
    counts = {"total": 0, "running": 0, "stale": 0, "asking": 0}
    for transcript in sorted(root.rglob("*.jsonl")):
        if transcript.name == "journal.jsonl":
            continue
        if transcript.parent not in journals:
            journals[transcript.parent] = journal_states(transcript.parent / "journal.jsonl")
        states = journals[transcript.parent]
        agent = transcript.stem[len("agent-"):] if transcript.stem.startswith("agent-") else transcript.stem
        state, label = ("no journal", "") if states is None else states.get(agent, ("not in journal", ""))
        age = (now - transcript.stat().st_mtime) / 60
        # A finished, failed or superseded agent waits on nothing, whatever its transcript ends with.
        ask = pending_ask(transcript) if state not in ("done", "failed", "superseded") else None
        flag = "STALLED-ASK" if ask else "STALE" if state == "running" and age > a.stale_min else ""
        counts["total"] += 1
        counts["running"] += state == "running"
        counts["stale"] += flag == "STALE"
        counts["asking"] += flag == "STALLED-ASK"
        print(f"{flag:<12}{age:8.1f} min  {state:<14}  {label or '-':<24}  {transcript}"
              + (f"  ask: {ask[:120]}" if ask else ""))
    print(f"{counts['total']} transcript(s), {counts['running']} running, {counts['asking']} stalled "
          f"on a hook ask, {counts['stale']} stale (running and silent for over {a.stale_min:g} min)")
    return 1 if counts["stale"] or counts["asking"] else 0


# --- CLI ----------------------------------------------------------------------------------------

def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    sub = parser.add_subparsers(dest="command", required=True)

    p = sub.add_parser("args", help="write the args JSON for one Workflow script",
                       description="Write the args JSON for a Workflow script, embedding the "
                       "dispatch rules verbatim as `rules`. For review, each item (needs wt and base; "
                       "head defaults to the worktree's HEAD) gains files by kind and the deep-review "
                       "lenses for git diff --name-only base..head, unioned with any lenses it "
                       "carries; a range that changes no file is refused. An items file may be an "
                       "array, or an object whose other fields are top-level args: each stands "
                       "unless a flag is given for it (pool, model and maxRounds included).")
    p.add_argument("workflow", choices=WORKFLOWS)
    p.add_argument("--items", required=True, help="JSON file: the items array (or {items, ...})")
    p.add_argument("--repo", help="main checkout (default: the parent of git's common dir)")
    p.add_argument("--run-root", help="the run folder, absolute (required)")
    p.add_argument("--scratch", help="scratch root (default: $TAOM_IMPROVE_ROOT/scratch)")
    p.add_argument("--tmp", help="temp root (default: $TAOM_IMPROVE_ROOT/scratch/tmp)")
    p.add_argument("--date", help="YYYY-MM-DD (default: today)")
    p.add_argument("--version", help=f"vX.Y.Z (default: <Version value> in {SUBMODULE_XML})")
    p.add_argument("--model", nargs="+", metavar="ROLE=ID",
                   help=f"model per role, roles: {', '.join(ROLES)} (the scripts default each)")
    p.add_argument("--pool", type=int, help="agents in flight at once (default 4)")
    p.add_argument("--max-rounds", type=int, help="convergence rounds, written as maxRounds")
    p.add_argument("--rules", help=f"rules file (default: {RULES_PATH} in this checkout)")
    p.add_argument("--out", help="output file (default: stdout)")
    p.set_defaults(func=cmd_args)

    p = sub.add_parser("status", help="set the Status cell of one plan row",
                       description="Set the Status cell of plan row NUM in a plans index. The "
                       "Status column is found from the table's header row; every other byte, "
                       "line endings included, is kept. Exit 1 when the row is not found once.")
    p.add_argument("readme")
    p.add_argument("num")
    p.add_argument("text")
    p.set_defaults(func=cmd_status)

    p = sub.add_parser("codex-prompt", help="write the adversarial review prompt for a branch",
                       description="Write the Codex adversarial review prompt for BASE..BRANCH. "
                       "The fixed blocks are the fenced blocks under the TAOM ID CHEATSHEET and "
                       f"Prior review lessons headings of {PROMPT_FIXED_PATH} in this checkout; the "
                       "plan is plans/<num>-*.md on the branch when the branch's last path part "
                       "starts with <num>-.")
    p.add_argument("--branch", required=True)
    p.add_argument("--base", required=True)
    p.add_argument("--tag", help="marks a second review of the same branch (for example -decisions)")
    p.add_argument("--out", required=True)
    p.set_defaults(func=cmd_codex_prompt)

    p = sub.add_parser("file-issue", help="file a drafted issue (only on the maintainer's word)",
                       description="File a draft whose first lines are TITLE: and LABEL:, then a "
                       "blank line and the body. A draft failing tools/check_public_text.py is "
                       "refused. Runs gh issue create --body-file and prints the URL. Public: run "
                       "it only on the maintainer's word.")
    p.add_argument("draft")
    p.set_defaults(func=cmd_file_issue)

    p = sub.add_parser("watch", help="list agent transcripts; exit 1 on a stalled or silent agent",
                       description="List every agent transcript (*.jsonl) under DIR (the running "
                       "workflow's transcript folder or a session's subagents folder) with its last "
                       "write age. STALLED-ASK: the transcript's latest PreToolUse hook ask has no "
                       "later tool_result for its tool call, so the agent waits on a prompt nobody "
                       "sees (direct spawns included, which have no journal). STALE: a running "
                       "agent silent for over --stale-min minutes. In a workflow folder, an agent "
                       "whose journal key was started again later is superseded, not running. "
                       "Exit 1 on any STALLED-ASK or STALE.")
    p.add_argument("--dir", required=True)
    p.add_argument("--stale-min", type=float, default=30.0, help="minutes (default 30)")
    p.set_defaults(func=cmd_watch)
    return parser


def main(argv: list[str] | None = None) -> int:
    args = build_parser().parse_args(argv)
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(errors="backslashreplace")
    try:
        return args.func(args)
    except Fail as exc:
        print(f"improve_ctl {args.command}: {exc}", file=sys.stderr)
        return exc.code
    except Exception as exc:  # a distinct exit code and one line, never a traceback read as exit 1
        print(f"improve_ctl {args.command}: unexpected error: {exc!r}", file=sys.stderr)
        return 3


if __name__ == "__main__":
    sys.exit(main())
