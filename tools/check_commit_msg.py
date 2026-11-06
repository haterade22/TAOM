#!/usr/bin/env python
"""Check a commit message against TAOM's commit rules (CLAUDE.md "Commits").

Usage: python tools/check_commit_msg.py <message file>
Run by .githooks/commit-msg (core.hooksPath = .githooks), so a commit made from a terminal meets the
same rules as one Claude runs, which .claude/hooks/check-commit-subject-version.sh checks:

- the subject reads <type>[(scope)][!]: vX.Y.Z - <description>, where vX.Y.Z is the <Version> of
  Main/_Module/SubModule.xml as the commit will carry it (the staged copy when staged);
- the subject is at most 72 characters;
- no AI attribution line (a Co-Authored-By naming an AI, or "Generated with Claude Code").

Exit 0 to accept, 1 to refuse (git aborts the commit and prints the reasons). A fixup!, squash! or
amend! subject is left to the later autosquash. A version it cannot read leaves only the label
check out, and says so.
"""
import re
import subprocess
import sys

SUBMODULE = "Main/_Module/SubModule.xml"
SUBJECT = re.compile(r"^[a-z][a-z0-9]*(?:\([^)]+\))?!?: (v\d+\.\d+\.\d+(?:\.\d+)?) - \S")
# Kept in step with .claude/hooks/check-commit-subject-version.sh.
AI_TRAILER = re.compile(
    r"^\s*(?:Co-Authored-By\s*[:=][^\n]*\b(?:claude|anthropic|openai|chatgpt|gpt-?\d|codex|copilot|gemini|kimi)\b"
    r"|[^\w\n]*Generated with \[?Claude Code)", re.I | re.M)
MAX_SUBJECT = 72


def check(message, version):
    """Return the list of problems with the message; empty when it passes."""
    lines = [line for line in message.splitlines() if not line.startswith("#")]
    text = "\n".join(lines)
    subject = next((line.strip() for line in lines if line.strip()), None)
    if subject is None or subject.startswith(("fixup! ", "squash! ", "amend! ")):
        return []
    problems = []
    if len(subject) > MAX_SUBJECT:
        problems.append(f"The subject is {len(subject)} characters; the limit is {MAX_SUBJECT}.")
    if version is not None:
        m = SUBJECT.match(subject)
        if not m:
            problems.append(f"The subject does not carry the version label. Write <type>[(scope)]: {version} - <description>,"
                            f" for example: fix(recruitment): {version} - Glanhir recruits the Ringlo Vale line.")
        elif m.group(1) != version:
            problems.append(f"The subject names {m.group(1)}, but {SUBMODULE} in this commit says {version}.")
    hit = AI_TRAILER.search(text)
    if hit:
        problems.append(f"The message carries an AI attribution line: '{hit.group(0).strip()}'. TAOM commits carry none.")
    return problems


def commit_version():
    """The <Version> the commit will carry: the staged SubModule.xml when it is staged, else the file."""
    text = ""
    try:
        staged = subprocess.run(["git", "diff", "--cached", "--name-only", "--", SUBMODULE],
                                capture_output=True, text=True, timeout=10).stdout
        if staged.strip():
            text = subprocess.run(["git", "show", ":" + SUBMODULE], capture_output=True, text=True, timeout=10).stdout
    except (OSError, subprocess.SubprocessError):
        text = ""
    if not text:
        try:
            with open(SUBMODULE, encoding="utf-8", errors="replace") as fh:
                text = fh.read()
        except OSError:
            return None
    m = re.search(r'<Version\s+value="([^"]+)"', text)
    return m.group(1) if m else None


def main(argv):
    if len(argv) != 2:
        print("usage: check_commit_msg.py <message file>", file=sys.stderr)
        return 2
    with open(argv[1], encoding="utf-8", errors="replace") as fh:
        message = fh.read()
    version = commit_version()
    if version is None:
        print(f"[commit-msg] could not read <Version> from {SUBMODULE}; the version label was NOT checked.", file=sys.stderr)
    problems = check(message, version)
    for p in problems:
        print("[commit-msg] " + p, file=sys.stderr)
    if problems:
        print("[commit-msg] Commit refused (CLAUDE.md 'Commits'). Your message is kept in .git/COMMIT_EDITMSG.", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
