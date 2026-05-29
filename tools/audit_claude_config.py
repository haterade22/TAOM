#!/usr/bin/env python3
"""Audit TAOM's Claude Code configuration surface for security issues.

Deterministic, offline, stdlib-only. A portable subset of affaan-m/ECC's
"AgentShield" rule set, CALIBRATED TO TAOM. The notable calibration: TAOM
*mandates* fail-open hooks (`|| true`, `2>/dev/null`, always `exit 0`) per
`.claude/rules/harness-facts.md`, so that pattern is NOT a finding here (it is
in upstream AgentShield). We keep only high-signal, low-false-positive rules.

Scans (relative to --root, default = repo root):
  .claude/{skills,agents,rules,hooks}/**, .claude/settings.json,
  .claude/settings.local.json, .mcp.json, CLAUDE.md, AGENTS.md

Categories:
  secrets            committed credentials (API keys, private keys, DB URIs)
  permissions        over-broad tool grants in settings allow-lists
  hook-exfil         network exfil / reverse shells / log-tampering in hooks
  mcp-risk           hardcoded MCP secrets, auto-approve, unpinned npx -y
  prompt-injection   hidden unicode / "ignore previous instructions" / fetch-exec

Exit codes:  2 = at least one CRITICAL finding (CI gate) · 1 = usage error · 0 = clean/non-critical
Flags:  --json  machine output · --root PATH  scan root · --min SEV  only report >= severity

Inline suppression: put `audit-allow: <rule-id>` in a comment on the SAME line
to suppress a known-safe match (e.g. an injection phrase quoted as an example in
a security doc). Use sparingly; suppressions are reported in the summary.
"""
from __future__ import annotations

import argparse
import json
import re
import sys
from dataclasses import dataclass, field
from pathlib import Path

SEVERITIES = ["INFO", "LOW", "MED", "HIGH", "CRITICAL"]
SEV_RANK = {s: i for i, s in enumerate(SEVERITIES)}


@dataclass
class Finding:
    rule: str
    severity: str
    category: str
    path: str
    line: int
    message: str
    snippet: str = ""


@dataclass
class Result:
    findings: list[Finding] = field(default_factory=list)
    suppressed: list[str] = field(default_factory=list)
    scanned_files: int = 0


# --------------------------------------------------------------------------- #
# Helpers
# --------------------------------------------------------------------------- #

def _read(path: Path) -> str:
    # Assumes config files are small (this audits .claude/ config, not large
    # binaries or general code). errors="replace" keeps non-UTF8 bytes from crashing.
    try:
        return path.read_text(encoding="utf-8-sig", errors="replace")
    except OSError:
        return ""


def _mask(s: str) -> str:
    s = s.strip()
    if len(s) <= 8:
        return "****"
    return f"{s[:4]}...{s[-2:]}"


# A line is suppressed for a rule if it carries `audit-allow: <rule>`.
def _suppressed(line: str, rule: str) -> bool:
    m = re.search(r"audit-allow:\s*([A-Za-z0-9_,\- ]+)", line)
    if not m:
        return False
    allowed = {t.strip() for t in re.split(r"[,\s]+", m.group(1)) if t.strip()}
    return rule in allowed or "all" in allowed


# Values that look like placeholders / env refs, not real secrets.
_PLACEHOLDER = re.compile(
    r"(\$\{|\$\(|\$env:|%[A-Z_]+%|<[^>]*>|\{\{|your[_-]?|example|placeholder|redact|"
    r"dummy|sample|changeme|insert[_-]?|xxx+|\.\.\.|=\s*$|test[_-]?key|fake)",
    re.IGNORECASE,
)


def _is_placeholder(value: str) -> bool:
    v = value.strip().strip("\"'")
    if not v or len(set(v)) <= 2:  # e.g. "xxxxxxxx", ""
        return True
    return bool(_PLACEHOLDER.search(v))


# --------------------------------------------------------------------------- #
# Secret rules  (run on every scanned file)
# --------------------------------------------------------------------------- #

_SECRET_PATTERNS = [
    ("secret-anthropic", r"sk-ant-[A-Za-z0-9_\-]{20,}", "Anthropic API key"),
    ("secret-openai", r"sk-(?:proj-)?[A-Za-z0-9]{32,}", "OpenAI API key"),
    ("secret-github-pat", r"gh[pousr]_[A-Za-z0-9]{36,}", "GitHub token"),
    ("secret-github-fine", r"github_pat_[A-Za-z0-9_]{22,}", "GitHub fine-grained PAT"),
    ("secret-aws", r"AKIA[0-9A-Z]{16}", "AWS access key id"),
    ("secret-google", r"AIza[0-9A-Za-z_\-]{35}", "Google API key"),
    ("secret-slack", r"xox[baprs]-[A-Za-z0-9-]{10,}", "Slack token"),
    ("secret-stripe", r"sk_(?:test|live)_[A-Za-z0-9]{16,}", "Stripe secret key"),
    ("secret-privatekey", r"-----BEGIN (?:RSA |EC |DSA |OPENSSH |PGP )?PRIVATE KEY-----", "private key material"),
    ("secret-db-uri", r"(?:postgres(?:ql)?|mysql|mongodb(?:\+srv)?|redis)://[^\s:@/]+:[^\s:@/]+@", "DB connection string with inline credentials"),
]
_SECRET_COMPILED = [(rid, re.compile(rx), desc) for rid, rx, desc in _SECRET_PATTERNS]

_GENERIC_SECRET = re.compile(
    r"""(?ix)
    \b(api[_-]?key|secret(?:[_-]?key)?|access[_-]?token|auth[_-]?token|password|passwd|pwd)\b
    \s*[:=]\s*
    ["']([^"']{8,})["']
    """
)


def scan_secrets(path: Path, rel: str, text: str, res: Result) -> None:
    for lineno, line in enumerate(text.splitlines(), 1):
        for rid, rx, desc in _SECRET_COMPILED:
            for m in rx.finditer(line):
                tok = m.group(0)
                if rid != "secret-privatekey" and _is_placeholder(tok):
                    continue
                if _suppressed(line, rid):
                    res.suppressed.append(f"{rel}:{lineno} {rid}")
                    continue
                res.findings.append(Finding(
                    rid, "CRITICAL", "secrets", rel, lineno,
                    f"Possible committed {desc}", _mask(tok)))
        for m in _GENERIC_SECRET.finditer(line):
            key, val = m.group(1), m.group(2)
            if _is_placeholder(val):
                continue
            if _suppressed(line, "secret-generic"):
                res.suppressed.append(f"{rel}:{lineno} secret-generic")
                continue
            res.findings.append(Finding(
                "secret-generic", "HIGH", "secrets", rel, lineno,
                f"Hardcoded {key.lower()} literal", _mask(val)))


# --------------------------------------------------------------------------- #
# Permission rules  (settings*.json allow-lists) + global flag scan
# --------------------------------------------------------------------------- #

def scan_permissions(path: Path, rel: str, text: str, res: Result) -> None:
    try:
        data = json.loads(text)
    except (ValueError, TypeError):
        return
    perms = data.get("permissions", {}) if isinstance(data, dict) else {}
    allow = perms.get("allow", []) if isinstance(perms, dict) else []
    for entry in allow if isinstance(allow, list) else []:
        e = str(entry)
        low = e.lower()
        if re.fullmatch(r"Bash\(\*[:)].*", e) or e in ("Bash(*)", "Bash(*:*)"):
            res.findings.append(Finding("perm-bash-wildcard", "CRITICAL", "permissions",
                rel, 0, f"Unrestricted shell grant: {e}"))
        elif e in ("Write(*)", "Edit(*)"):
            res.findings.append(Finding("perm-write-wildcard", "HIGH", "permissions",
                rel, 0, f"Unrestricted file-write grant: {e}"))
        elif "sudo" in low:
            res.findings.append(Finding("perm-sudo", "HIGH", "permissions",
                rel, 0, f"Privilege-escalation grant: {e}"))
        if "dangerously-skip-permissions" in low:
            res.findings.append(Finding("perm-skip", "CRITICAL", "permissions",
                rel, 0, f"--dangerously-skip-permissions in allow-list: {e}"))
    if isinstance(data, dict) and data.get("enableAllProjectMcpServers") is True:
        res.findings.append(Finding("mcp-auto-approve", "HIGH", "mcp-risk", rel, 0,
            "enableAllProjectMcpServers=true auto-approves untrusted project MCP servers"))


def scan_skip_flag(rel: str, text: str, res: Result) -> None:
    for lineno, line in enumerate(text.splitlines(), 1):
        if "dangerously-skip-permissions" in line and not _suppressed(line, "perm-skip-text"):
            res.findings.append(Finding("perm-skip-text", "HIGH", "permissions", rel, lineno,
                "--dangerously-skip-permissions referenced", line.strip()[:80]))


# --------------------------------------------------------------------------- #
# Hook exfiltration rules  (.claude/hooks scripts only; fail-open is OK in TAOM)
# --------------------------------------------------------------------------- #

_HOOK_RULES = [
    ("hook-revshell", "CRITICAL", r"/dev/tcp/|mkfifo\s+.*\bnc\b|\bnc\b[^\n]*\s-e\b|bash\s+-i\b|socket\.socket\([^)]*\)[^\n]*subprocess",
     "reverse-shell pattern in a hook"),
    ("hook-network", "HIGH", r"\b(curl|wget)\b[^\n]*https?://|Invoke-(WebRequest|RestMethod)|\bnc\b[^\n]*\d{2,5}",
     "outbound network call in a hook (hooks should not phone home)"),
    ("hook-logtamper", "HIGH", r"history\s+-c|journalctl\s+--vacuum|rm\s+[^\n]*(/var/log|\.bash_history)",
     "log/anti-forensics tampering in a hook"),
    ("hook-credread", "HIGH", r"/etc/shadow|security\s+find-generic-password|\.aws/credentials|~/\.ssh/id_",
     "credential-store read in a hook"),
    ("hook-clipboard", "MED", r"\b(pbcopy|xclip|xsel|wl-copy)\b|clip\.exe",
     "clipboard access in a hook (possible exfil)"),
]
_HOOK_COMPILED = [(rid, sev, re.compile(rx), desc) for rid, sev, rx, desc in _HOOK_RULES]


def scan_hook(rel: str, text: str, res: Result) -> None:
    for lineno, line in enumerate(text.splitlines(), 1):
        stripped = line.strip()
        if stripped.startswith("#") or stripped.startswith("REM "):
            continue  # comment line
        for rid, sev, rx, desc in _HOOK_COMPILED:
            if rx.search(line) and not _suppressed(line, rid):
                res.findings.append(Finding(rid, sev, "hook-exfil", rel, lineno, desc, stripped[:100]))


# --------------------------------------------------------------------------- #
# MCP rules  (.mcp.json)
# --------------------------------------------------------------------------- #

def scan_mcp(rel: str, text: str, res: Result) -> None:
    try:
        data = json.loads(text)
    except (ValueError, TypeError):
        return
    servers = {}
    if isinstance(data, dict):
        servers = data.get("mcpServers") or data.get("servers") or {}
    if not isinstance(servers, dict):
        return
    for name, cfg in servers.items():
        if not isinstance(cfg, dict):
            continue
        env = cfg.get("env", {})
        if isinstance(env, dict):
            for k, v in env.items():
                if isinstance(v, str) and v and not _is_placeholder(v) and len(v) >= 12:
                    res.findings.append(Finding("mcp-env-secret", "HIGH", "mcp-risk", rel, 0,
                        f"MCP server '{name}' env '{k}' looks like a hardcoded secret (use ${{VAR}} ref)",
                        _mask(v)))
        args = cfg.get("args", []) or []
        argstr = " ".join(str(a) for a in args) if isinstance(args, list) else str(args)
        cmd = str(cfg.get("command", ""))
        if "npx" in cmd or "npx" in argstr:
            if "-y" in (args if isinstance(args, list) else []) and not re.search(r"@\d", argstr):
                res.findings.append(Finding("mcp-npx-unpinned", "MED", "mcp-risk", rel, 0,
                    f"MCP server '{name}' uses `npx -y` without a pinned @version (auto-install/typosquat risk)"))
        if re.search(r"\.env\b|\.pem\b|credentials\.json|id_rsa", argstr):
            res.findings.append(Finding("mcp-sensitive-arg", "MED", "mcp-risk", rel, 0,
                f"MCP server '{name}' passes a sensitive file path as an argument"))


# --------------------------------------------------------------------------- #
# Prompt-injection rules  (markdown surfaces)
# --------------------------------------------------------------------------- #

_ZERO_WIDTH = re.compile(r"[​-‏‪-‮⁠⁦-⁩]")
_INJ_TEXT = [
    ("inj-ignore-prev", r"(?i)ignore\s+(all\s+)?(previous|prior|above)\s+(instructions|prompts)"),
    ("inj-disregard", r"(?i)disregard\s+(the\s+)?(above|previous|system)"),
    ("inj-override", r"(?i)new\s+system\s+prompt|developer\s+mode\s+enabled|ignore\s+(your|all)\s+(guidelines|safety|rules)|\bact\s+as\s+(DAN|an?\s+unrestricted)"),
    ("inj-output-manip", r"(?i)(always\s+(report|say|return)\s+(ok|success|passed|clean|no\s+issues))|(suppress|hide|ignore)\s+(the\s+)?(security|scan|audit|review)\s+(finding|warning|result|error|output)s?"),
]
_INJ_COMPILED = [(rid, re.compile(rx)) for rid, rx in _INJ_TEXT]
# Match only a TRUE pipe into a shell (curl ... | bash). A semicolon is sequential
# execution, not piping, so it is excluded to avoid flagging benign command chains.
_FETCH_EXEC = re.compile(r"(?i)(curl|wget|iwr|invoke-webrequest)\b[^\n|]*\|\s*(bash|sh|zsh|iex|python\b)")


def scan_injection(rel: str, text: str, res: Result, is_doc: bool) -> None:
    for lineno, line in enumerate(text.splitlines(), 1):
        if _ZERO_WIDTH.search(line) and not _suppressed(line, "inj-zerowidth"):
            res.findings.append(Finding("inj-zerowidth", "HIGH", "prompt-injection", rel, lineno,
                "hidden zero-width / bidi unicode in a model-facing file", repr(line.strip()[:60])))
        if _FETCH_EXEC.search(line) and not _suppressed(line, "inj-fetch-exec"):
            res.findings.append(Finding("inj-fetch-exec", "HIGH", "prompt-injection", rel, lineno,
                "fetch-and-execute instruction (download piped to a shell)", line.strip()[:90]))
        for rid, rx in _INJ_COMPILED:
            if rx.search(line) and not _suppressed(line, rid):
                # Documentation (rules/, docs/) legitimately quotes these as examples
                # of what to look for -> dampen to INFO so security docs don't self-flag.
                sev = "INFO" if is_doc else "MED"
                res.findings.append(Finding(rid, sev, "prompt-injection", rel, lineno,
                    "injection-style directive" + (" (in docs/rules — likely an example)" if is_doc else ""),
                    line.strip()[:90]))


# --------------------------------------------------------------------------- #
# File collection + dispatch
# --------------------------------------------------------------------------- #

_SKIP_DIRS = {"logs", "state", "tmp", "node_modules", ".git", "bin", "obj"}
_MD_INJECTION_DIRS = (".claude/skills", ".claude/agents", ".claude/rules")
# rules/ + docs are "documentation" context for injection-phrase dampening
_DOC_HINT = re.compile(r"(^|/)(\.claude/rules|docs)/|(^|/)harness-facts\.md$")


def collect(root: Path) -> list[Path]:
    out: list[Path] = []
    claude = root / ".claude"
    if claude.is_dir():
        for sub in ("skills", "agents", "rules", "hooks"):
            d = claude / sub
            if d.is_dir():
                for p in d.rglob("*"):
                    if p.is_file() and not (set(p.relative_to(root).parts) & _SKIP_DIRS):
                        out.append(p)
        for f in ("settings.json", "settings.local.json"):
            p = claude / f
            if p.is_file():
                out.append(p)
    for f in (".mcp.json", "CLAUDE.md", "AGENTS.md"):
        p = root / f
        if p.is_file():
            out.append(p)
    return out


def scan_file(path: Path, root: Path, res: Result) -> None:
    rel = str(path.relative_to(root)).replace("\\", "/")
    text = _read(path)
    res.scanned_files += 1
    is_doc = bool(_DOC_HINT.search(rel))

    scan_secrets(path, rel, text, res)
    scan_skip_flag(rel, text, res)

    name = path.name.lower()
    if name in ("settings.json", "settings.local.json"):
        scan_permissions(path, rel, text, res)
    if name == ".mcp.json":
        scan_mcp(rel, text, res)
    # "" suffix matches extensionless hook scripts (Unix convention, e.g. check-freeze).
    if "/hooks/" in ("/" + rel) and path.suffix in (".sh", ".bash", ".ps1", ".py", ".js", ".cmd", ""):
        scan_hook(rel, text, res)
    if path.suffix == ".md" or name in ("claude.md", "agents.md"):
        scan_injection(rel, text, res, is_doc)


# --------------------------------------------------------------------------- #
# Reporting
# --------------------------------------------------------------------------- #

def report_text(res: Result, min_rank: int) -> None:
    shown = [f for f in res.findings if SEV_RANK[f.severity] >= min_rank]
    counts = {s: sum(1 for f in res.findings if f.severity == s) for s in SEVERITIES}
    print(f"\nClaude config security audit - {res.scanned_files} files scanned")
    summary = "  ".join(f"{s}:{counts[s]}" for s in reversed(SEVERITIES) if counts[s])
    print("  " + (summary or "no findings"))
    if not shown:
        print("\n  No findings at or above the requested severity. [OK]")
    for f in sorted(shown, key=lambda x: (-SEV_RANK[x.severity], x.category, x.path)):
        loc = f"{f.path}:{f.line}" if f.line else f.path
        print(f"\n  [{f.severity}] {f.rule} ({f.category})\n    {loc}\n    {f.message}")
        if f.snippet:
            print(f"    > {f.snippet}")
    if res.suppressed:
        print(f"\n  {len(res.suppressed)} suppressed via audit-allow: " + ", ".join(res.suppressed[:10]))
    print()


def report_json(res: Result, min_rank: int) -> None:
    shown = [f for f in res.findings if SEV_RANK[f.severity] >= min_rank]
    print(json.dumps({
        "scanned_files": res.scanned_files,
        "counts": {s: sum(1 for f in res.findings if f.severity == s) for s in SEVERITIES},
        "suppressed": res.suppressed,
        "findings": [vars(f) for f in shown],
    }, indent=2))


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser(description="Audit TAOM's Claude Code config surface for security issues.")
    ap.add_argument("--root", default=".", help="repo root to scan (default: cwd)")
    ap.add_argument("--json", action="store_true", help="machine-readable JSON output")
    ap.add_argument("--min", default="INFO", choices=SEVERITIES, help="only report findings >= this severity")
    args = ap.parse_args(argv)

    root = Path(args.root).resolve()
    if not root.is_dir():
        print(f"error: --root {root} is not a directory", file=sys.stderr)
        return 1

    res = Result()
    for p in collect(root):
        scan_file(p, root, res)

    min_rank = SEV_RANK[args.min]
    (report_json if args.json else report_text)(res, min_rank)

    return 2 if any(f.severity == "CRITICAL" for f in res.findings) else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
