#!/usr/bin/env python3
"""Audit TAOM's Claude Code configuration surface for security issues.

Deterministic and offline. Stdlib-only by default; one OPTIONAL layer (YARA)
activates if `yara-python` is importable and is skipped (INFO note) otherwise,
so the auditor always runs and the CI gate never hard-fails on a missing dep.

The base rule set is a portable subset of affaan-m/ECC's "AgentShield",
CALIBRATED TO TAOM. The notable calibration: TAOM *mandates* fail-open hooks
(`|| true`, `2>/dev/null`, always `exit 0`) per `.claude/rules/harness-facts.md`,
so that pattern is NOT a finding here (it is in upstream AgentShield). We keep
only high-signal, low-false-positive rules.

The agency/mempoison/promptleak/toolmisuse/rogue/output categories + the AST
scan are a portable, calibrated subset of NVIDIA SkillSpector's deterministic
`static_patterns_*` + `behavioral_ast` analyzers (Apache-2.0,
https://github.com/NVIDIA/SkillSpector). Per Apache-2.0 §4 the upstream
attribution is preserved here. We port only the offline regex/AST logic — NOT
the LangGraph runtime or the LLM "semantic" analyzers. The YARA rules in
tools/yara_rules/ are TAOM clean-room originals (the upstream DRL-1.1 /
unlicensed Neo23x0-derived .yar files were NOT vendored). See
docs/reviews/adopt-skillspector-2026-06-22.md.

CALIBRATION: the ported SkillSpector regex categories (agency/mempoison/
promptleak/toolmisuse/rogue/output) are ADVISORY (INFO) on a TAOM self-audit,
because TAOM's own hooks and docs legitimately reference attack patterns (the
hook that BLOCKS `--no-verify`; a doc quoting "from now on, always ..." as an
example). Pass --external when vetting an untrusted/foreign tree to raise them
to full severity (the "skills from the internet aren't harmless" use case). The
genuine code/IOC teeth — the separate hook-exfil, AST-chain, and YARA layers —
fire at full severity regardless of --external. `audit-allow: <rule>` suppresses
a line; lines that read like documentation examples are skipped automatically.

Scans (relative to --root, default = repo root):
  Config surface, all layers: .claude/{skills,agents,rules,hooks}/**,
  .claude/settings.json, .claude/settings.local.json, .mcp.json, CLAUDE.md,
  AGENTS.md
  Whole repo, secret rules only: every git-tracked text file under 2 MB. This
  is the sweep that catches a key pasted into a .ps1, a C# const, or a doc, and
  a tracked file that IS credential material by name. It needs a git work tree
  and says so with an INFO note rather than silently doing nothing when there
  is none. Turn it off with --no-repo-secrets.

Categories:
  secrets            committed credentials (API keys, private keys, DB URIs) in
                     any tracked file, plus tracked files that are credential
                     material by name (*.pem, id_rsa, .env, .npmrc)
  permissions        over-broad tool grants in settings allow-lists
  hook-exfil         network exfil / reverse shells / log-tampering in hooks
  mcp-risk           hardcoded MCP secrets, auto-approve, unpinned npx -y
  prompt-injection   hidden unicode / "ignore previous instructions" / fetch-exec
  excessive-agency   unrestricted tool grants, skip-confirmation, unbounded resource use
  memory-poisoning   persistent-context injection, memory wipe/poison, identity rewrite
  prompt-leakage     reveal/exfiltrate system prompt or instructions
  tool-misuse        dangerous params (shell=True, rm -rf /, --no-verify, TLS off)
  rogue-agent        self-modification, disable-safety, cron/rc persistence
  output-handling    model output piped to exec/eval/innerHTML/SQL; unbounded output
  ast-exec           (.py) exec/eval/compile/__import__/os-exec/subprocess/dynamic-getattr
  yara-*             (optional) clean-room webshell/malware/C2/miner/hacktool IOCs

Exit codes:  2 = at least one CRITICAL finding (CI gate) · 1 = usage error · 0 = clean/non-critical
Flags:  --json  machine output · --root PATH  scan root · --min SEV  only report >= severity
        --external  raise SkillSpector regex categories to full severity (foreign tree)
        --no-repo-secrets  config surface only, skip the git-tracked sweep

Inline suppression: put `audit-allow: <rule-id>` in a comment on the SAME line
to suppress a known-safe match (e.g. an injection phrase quoted as an example in
a security doc). Use sparingly; suppressions are reported in the summary.
"""
from __future__ import annotations

import argparse
import ast
import json
import re
import subprocess
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


def _has_secret_hint(text: str) -> bool:
    """Cheap whole-text gate in front of the per-line loop.

    Exact rather than heuristic: it runs the SAME compiled patterns once over
    the whole text. None of them is anchored, so a whole-text search is a strict
    superset of the per-line matches, and a file with no hint can skip the loop
    without changing a single finding. This is what lets the repo-wide sweep
    cross several thousand tracked files in seconds instead of minutes. Line
    numbers and `audit-allow` suppression still come from the per-line pass.
    """
    for _rid, rx, _desc in _SECRET_COMPILED:
        if rx.search(text):
            return True
    return bool(_GENERIC_SECRET.search(text))


def scan_secrets(path: Path, rel: str, text: str, res: Result) -> None:
    if not _has_secret_hint(text):
        return
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
# Repo-wide secret layer  (every git-tracked file, not just the config surface)
# --------------------------------------------------------------------------- #

# A tracked file whose NAME is credential material. .gitignore stops a NEW one
# from being added and has no say at all over a file that is already committed,
# which is exactly the case this catches. .crt / .cer / .der are deliberately
# absent: a certificate is public, and an auditor that cries wolf gets ignored.
_TRACKED_SECRET_FILES = [
    ("secret-tracked-key", "CRITICAL",
     r"\.(?:pem|key|p12|pfx|ppk|jks|keystore|gpg)$|(?:^|/)id_(?:rsa|dsa|ecdsa|ed25519)$",
     "private key or keystore material"),
    ("secret-tracked-env", "HIGH",
     r"(?:^|/)\.env(?:\.[^/]+)?$",
     "environment file (real values live in these)"),
    ("secret-tracked-cred", "HIGH",
     r"(?:^|/)(?:\.npmrc|\.pypirc|\.netrc|_netrc|credentials\.json|secrets\.json)$",
     "tool credential file"),
]
_TRACKED_COMPILED = [
    (rid, sev, re.compile(rx, re.IGNORECASE), desc)
    for rid, sev, rx, desc in _TRACKED_SECRET_FILES
]

# .env.example and friends are templates with no values in them. They are meant
# to be committed, so they are the one shape this layer stays quiet about.
_ENV_TEMPLATE = re.compile(r"\.(?:example|sample|template|dist)$", re.IGNORECASE)

# Both limits are about cost, not safety: the byte cap skips the handful of huge
# ModuleData XML, and a null byte in the first 8 KB is the cheapest reliable
# "this is a mesh, not source" test.
_REPO_MAX_BYTES = 2_000_000
_BINARY_SNIFF_BYTES = 8192


def _tracked_path_rule(rel: str) -> tuple[str, str, str] | None:
    """Return (rule, severity, description) if this path is credential-shaped."""
    for rid, sev, rx, desc in _TRACKED_COMPILED:
        if not rx.search(rel):
            continue
        if rid == "secret-tracked-env" and _ENV_TEMPLATE.search(rel):
            return None
        return (rid, sev, desc)
    return None


def collect_tracked(root: Path) -> list[str] | None:
    """Every git-tracked path under `root`, or None if it is not a work tree.

    git is the right source here rather than a tree walk: it answers precisely
    the question this layer asks (what is committed?), skips ignored and
    untracked junk for free, and costs one process instead of a full crawl of a
    repo that carries thousands of art assets.
    """
    try:
        proc = subprocess.run(
            ["git", "-C", str(root), "ls-files", "-z"],
            capture_output=True, timeout=120,
        )
    except (OSError, subprocess.SubprocessError):
        return None
    if proc.returncode != 0:
        return None
    raw = proc.stdout.decode("utf-8", "replace")
    return [r.replace("\\", "/") for r in raw.split("\0") if r]


def scan_repo_secrets(root: Path, rels: list[str], res: Result,
                      already: set[str], max_bytes: int = _REPO_MAX_BYTES) -> None:
    """Path-shape + content secret scan over the tracked files in `rels`.

    Only the secret rules run here. The permission / hook / injection /
    SkillSpector / AST layers stay scoped to the config surface where they are
    calibrated; turning them loose on several thousand source and data files
    would bury a real finding under advisory noise.
    """
    for rel in rels:
        hit = _tracked_path_rule(rel)
        if hit:
            rid, sev, desc = hit
            res.findings.append(Finding(
                rid, sev, "secrets", rel, 0,
                f"Tracked {desc}: .gitignore cannot protect a file that is "
                f"already committed, so remove it from the index and rotate"))
        if rel in already:
            continue
        path = root / rel
        try:
            size = path.stat().st_size
        except OSError:
            continue  # listed by git but absent from the worktree
        if size > max_bytes:
            continue
        try:
            with path.open("rb") as fh:
                head = fh.read(_BINARY_SNIFF_BYTES)
        except OSError:
            continue
        if b"\0" in head:
            continue
        res.scanned_files += 1
        scan_secrets(path, rel, _read(path), res)


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
        if e.strip() in ("*", "*:*"):
            res.findings.append(Finding("perm-star-wildcard", "CRITICAL", "permissions",
                rel, 0, f"Bare wildcard allow-grant (grants every tool): {e}"))
        elif re.fullmatch(r"Bash\(\*[:)].*", e) or e in ("Bash(*)", "Bash(*:*)"):
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
# SkillSpector-derived categories  (ported + calibrated to TAOM)
#
# Portable subset of NVIDIA SkillSpector's deterministic `static_patterns_*` +
# `behavioral_ast` analyzers (Apache-2.0; https://github.com/NVIDIA/SkillSpector).
# Apache-2.0 §4 attribution preserved. We port only offline regex/AST logic, not
# the LangGraph runtime or LLM analyzers. CALIBRATION: advisory (INFO) on a TAOM
# self-audit (our own hooks/docs legitimately reference attack patterns); full
# severity under --external (vetting a foreign tree). Lines that read like
# documentation examples (backticks, "never", "e.g.", prohibition phrasing) are
# skipped to keep the self-audit quiet. The full-severity teeth are the separate
# hook-exfil, AST, and YARA layers. `audit-allow: <rule>` suppresses a line.
# --------------------------------------------------------------------------- #

# (rule_id, severity, regex, message) — IGNORECASE compiled below.
_EXCESSIVE_AGENCY = [
    ("agency-wildcard-tools", "HIGH", r"['\"]?(?:tools?|permissions?|allowed-tools)['\"]?\s*:\s*\[?\s*['\"]?\*['\"]?\s*\]?", "wildcard tool/permission grant (no least-privilege boundary)"),
    ("agency-exec-arbitrary", "HIGH", r"(?:execute|run)\s+(?:arbitrary|any)\s+(?:commands?|code|scripts?)", "instruction to execute arbitrary commands/code"),
    ("agency-all-tools", "MED", r"(?:allow|grant|enable)\s+(?:access\s+to\s+)?(?:all|any|every)\s+tools?", "grants access to all/any tools"),
    ("agency-invoke-any-tool", "MED", r"(?:call|invoke|use|execute)\s+(?:any|all|every)\s+(?:available\s+)?tools?", "invoke any/all available tools"),
    ("agency-skip-confirm", "MED", r"(?:skip|bypass|disable)\s+(?:user\s+)?(?:confirmation|approval|consent|verification|prompt)", "skip/bypass user confirmation"),
    ("agency-auto-approve", "MED", r"(?:auto[_-]?approve|auto[_-]?confirm|auto[_-]?execute|auto[_-]?deploy)\b", "auto-approve / auto-execute directive"),
    ("agency-unbounded-retry", "MED", r"max[_-]?retries?\s*=\s*(?:None|0|float\s*\(\s*['\"]inf['\"]|math\.inf|infinity)", "unbounded retry budget"),
    ("agency-unlimited-calls", "LOW", r"(?:unlimited|infinite|unbounded)\s+(?:api\s+)?(?:calls?|requests?|queries?|invocations?)", "unbounded API/resource use"),
]
_MEMORY_POISONING = [
    ("mempoison-inject-false", "HIGH", r"(?:inject|insert|plant)\s+(?:false|fake|fabricated|malicious)\s+(?:memor(?:y|ies)|information|context|data|history)", "inject false/fabricated memory"),
    ("mempoison-poison", "HIGH", r"(?:poison|contaminate|corrupt|taint)\s+(?:your\s+)?(?:memory|context|state|knowledge|training)", "poison/corrupt memory or context"),
    ("mempoison-displace", "HIGH", r"(?:displace|push\s+out|overwrite|crowd\s+out|evict)\s+(?:the\s+)?(?:original|system|previous|existing|safety)\s+(?:instructions?|prompt|context|rules?)", "displace original/safety instructions"),
    ("mempoison-persist", "MED", r"(?:store|save|persist|retain)\s+(?:this|the\s+following)\s+(?:in|to|across)\s+(?:your\s+)?(?:memory|context|state|knowledge)", "persist content into agent memory/context"),
    ("mempoison-from-now-on", "MED", r"(?:from\s+now\s+on|henceforth|going\s+forward|permanently)\s*[,:]?\s*(?:always|you\s+must|you\s+should|you\s+will)\b", "persistent behavior-override directive"),
    ("mempoison-forget-prev", "MED", r"(?:forget|discard|drop|abandon)\s+(?:all\s+)?(?:previous|prior|earlier|past)\s+(?:instructions?|context|conversation|messages?|rules?)", "discard prior instructions/context"),
    ("mempoison-rewrite-identity", "MED", r"(?:rewrite|redefine)\s+(?:your\s+)?(?:personality|identity|purpose|mission|role|character)", "rewrite agent identity/purpose"),
]
_PROMPT_LEAKAGE = [
    ("promptleak-reveal", "HIGH", r"(?:print|output|show|display|reveal|expose|return|echo)\s+(?:your\s+|the\s+)?(?:full\s+)?(?:system\s+)?(?:prompt|instructions?|rules?|guidelines?|directives?)\b", "reveal/output the system prompt or instructions"),
    ("promptleak-exfil", "HIGH", r"(?:send|post|upload|transmit)\s+(?:your\s+)?(?:system\s+)?(?:prompt|instructions?|rules?)\s+(?:to|via)\s+(?:https?://|external|remote|webhook|api|server)", "exfiltrate the system prompt to an external sink"),
    ("promptleak-repeat-above", "MED", r"(?:repeat|recite|reproduce)\s+(?:everything|all|the\s+text)\s+(?:above|before|prior|preceding)\b", "repeat everything above (prompt extraction)"),
    ("promptleak-encode", "MED", r"(?:encode|encrypt|rot13|base64|reverse)\s+(?:your\s+)?(?:system\s+)?(?:prompt|instructions?|rules?)\b", "encode/obfuscate the system prompt for extraction"),
    ("promptleak-write-file", "MED", r"(?:write|save|store|log|dump)\s+(?:your\s+)?(?:system\s+)?(?:prompt|instructions?|rules?)\s+(?:to|into|in)\s+(?:a\s+)?(?:file|disk|log|database|storage)", "write the system prompt to a file/log"),
]
_TOOL_MISUSE = [
    ("toolmisuse-shell-true", "HIGH", r"(?:subprocess\.\w+|Popen)\s*\([^)]*shell\s*=\s*True", "subprocess with shell=True (command-injection vector)"),
    ("toolmisuse-rm-rf-root", "HIGH", r"\b(?:rm|del|erase)\s+[^|\n]*-(?:r|rf|fr)\s+[/~]", "recursive force-delete of a root/home path"),
    ("toolmisuse-rmtree-root", "HIGH", r"shutil\.rmtree\s*\(?[^)\n]{0,80}['\"]?\s*/\s*['\"]?", "shutil.rmtree on an absolute path"),
    ("toolmisuse-no-verify", "MED", r"--no-?(?:verify|check|validate|confirm|protect|safe)\b", "safety-bypassing CLI flag (--no-verify etc.)"),
    ("toolmisuse-chmod-777", "MED", r"(?:chmod|chown)\s+[^|\n]*(?:777|666|a\+rwx)", "world-writable permission grant"),
    ("toolmisuse-tls-off", "MED", r"(?:verify\s*=\s*False|NODE_TLS_REJECT_UNAUTHORIZED\s*=\s*['\"]?0|--no-check-certificate|--insecure\b|curl\s+[^|\n]*\s-k\b)", "TLS/cert verification disabled"),
]
_ROGUE_AGENT = [
    ("rogue-self-write", "HIGH", r"open\s*\(\s*__file__\s*,\s*['\"]w", "skill writing to its own file (__file__, 'w')"),
    ("rogue-self-modify", "HIGH", r"(?:self[_-]?modify|self[_-]?update|self[_-]?rewrite|self[_-]?patch|self[_-]?evolve)\b", "self-modification directive"),
    ("rogue-modify-skill", "HIGH", r"(?:write|modify|edit|update|overwrite|patch)\s+(?:this\s+)?(?:skill(?:'s)?|SKILL\.md)\b", "instruction to modify this skill's own files"),
    ("rogue-disable-safety", "HIGH", r"(?:disable|remove|delete|bypass)\s+(?:the\s+)?(?:safety|security|guard|protection|constraint)\s+(?:check|rule|mechanism|feature)", "disable a safety/security control at runtime"),
    ("rogue-cron", "MED", r"crontab\s+(?:-[el]\b|[^|\n]*>>?\s*/)", "cron persistence install"),
    ("rogue-rc-persist", "HIGH", r"(?:>>?\s*|(?:append|add|write|install)\s+(?:to|into)\s+)(?:~/)?\.(?:bashrc|zshrc|profile|bash_profile|login|cshrc)\b", "shell-rc autostart persistence"),
]
_OUTPUT_HANDLING = [
    ("output-exec-response", "HIGH", r"(?:exec|eval)\s*\(\s*(?:response|output|result|answer|completion|reply|generated)\b", "model output passed to exec/eval"),
    ("output-shell-response", "HIGH", r"(?:subprocess\.\w+|os\.system|os\.popen)\s*\([^)\n]*(?:response|output|result|answer|completion)\b", "model output passed to a shell sink"),
    ("output-innerhtml", "MED", r"(?:innerHTML|document\.write|dangerouslySetInnerHTML)\s*[=(]\s*(?:response|output|result|answer|completion|\{)", "model output injected into HTML (XSS sink)"),
    ("output-unbounded-tokens", "LOW", r"max[_-]?tokens?\s*=\s*(?:None|float\s*\(\s*['\"]inf['\"]|math\.inf|999999|1000000)", "unbounded output token limit"),
]

_SKILLSPECTOR_CATEGORIES = [
    ("excessive-agency", _EXCESSIVE_AGENCY),
    ("memory-poisoning", _MEMORY_POISONING),
    ("prompt-leakage", _PROMPT_LEAKAGE),
    ("tool-misuse", _TOOL_MISUSE),
    ("rogue-agent", _ROGUE_AGENT),
    ("output-handling", _OUTPUT_HANDLING),
]
# Flattened: (rule, severity, compiled, message, category)
_SKILL_COMPILED = [
    (rid, sev, re.compile(rx, re.IGNORECASE), msg, cat)
    for cat, rules in _SKILLSPECTOR_CATEGORIES
    for rid, sev, rx, msg in rules
]

# Lines that read like documentation examples (prohibition / quoting idioms).
# On a TAOM self-audit these almost always quote a pattern as an example, not
# enact it; skip them so the self-audit stays quiet. This skip is applied ONLY
# in self-audit (downgrade) mode — under --external we want maximum sensitivity
# and accept that a foreign doc quoting an attack phrase may fire. "example:" /
# "for example" are used (NOT bare "example", which matches URLs like evil.example).
_EXAMPLE_HINTS = (
    "`", "e.g.", "for example", "such as", "example:", "do not", "don't",
    "never ", "avoid", "instead of", "must not", "should not", "rather than",
)


def _looks_like_example(line: str) -> bool:
    low = line.lower()
    return any(h in low for h in _EXAMPLE_HINTS)


def scan_skillspector(rel: str, text: str, res: Result, downgrade: bool) -> None:
    for lineno, line in enumerate(text.splitlines(), 1):
        if downgrade and _looks_like_example(line):
            continue
        for rid, sev, rx, msg, cat in _SKILL_COMPILED:
            if not rx.search(line):
                continue
            if _suppressed(line, rid):
                res.suppressed.append(f"{rel}:{lineno} {rid}")
                continue
            eff = "INFO" if downgrade else sev
            res.findings.append(Finding(rid, eff, cat, rel, lineno, msg, line.strip()[:100]))


# --------------------------------------------------------------------------- #
# Behavioral AST scan  (.py files; stdlib `ast`, no dependency)
# --------------------------------------------------------------------------- #

_AST_SUBPROCESS = frozenset({
    "call", "run", "Popen", "check_output", "check_call", "getoutput", "getstatusoutput",
})
_AST_OS_EXEC = frozenset({
    "system", "popen", "execl", "execle", "execlp", "execlpe", "execv", "execve",
    "execvp", "execvpe", "spawnl", "spawnle", "spawnlp", "spawnlpe", "spawnv",
    "spawnve", "spawnvp", "spawnvpe", "posix_spawn", "posix_spawnp",
})
_AST_TAINT_HINTS = ("base64", "codecs", "marshal", "urllib", "requests", "httpx")


def _ast_call_name(node: ast.Call) -> str | None:
    f = node.func
    if isinstance(f, ast.Name):
        return f.id
    if isinstance(f, ast.Attribute):
        parts = [f.attr]
        cur: ast.expr = f.value
        while isinstance(cur, ast.Attribute):
            parts.append(cur.attr)
            cur = cur.value
        if isinstance(cur, ast.Name):
            parts.append(cur.id)
            return ".".join(reversed(parts))
    return None


def _ast_dangerous_source(node: ast.AST) -> str | None:
    for child in ast.walk(node):
        if not isinstance(child, ast.Call):
            continue
        name = _ast_call_name(child)
        if not name:
            continue
        if name in ("compile", "__import__") or name.startswith(("subprocess.", "os.")):
            return name
        if any(p in name for p in _AST_TAINT_HINTS):
            return name
    return None


def scan_ast(rel: str, text: str, res: Result) -> None:
    try:
        tree = ast.parse(text)
    except SyntaxError:
        return
    lines = text.splitlines()

    def emit(rule: str, sev: str, ln: int, msg: str) -> None:
        line_txt = lines[ln - 1] if 0 < ln <= len(lines) else ""
        if _suppressed(line_txt, rule):
            res.suppressed.append(f"{rel}:{ln} {rule}")
            return
        res.findings.append(Finding(rule, sev, "ast-exec", rel, ln, msg, line_txt.strip()[:100]))

    for node in ast.walk(tree):
        if not isinstance(node, ast.Call):
            continue
        name = _ast_call_name(node)
        if not name:
            continue
        ln = getattr(node, "lineno", 0)
        if name in ("exec", "eval"):
            if node.args:
                src = _ast_dangerous_source(node.args[0])
                if src:
                    emit("ast-exec-chain", "CRITICAL", ln, f"dangerous execution chain: {name}() wrapping {src}()")
            emit(f"ast-{name}", "HIGH", ln, f"{name}() call — arbitrary code execution")
        elif name == "__import__":
            emit("ast-dynimport", "MED", ln, "dynamic __import__() — bypasses static analysis")
        elif name == "compile":
            emit("ast-compile", "MED", ln, "compile() — code-object construction from a string")
        elif name.startswith("subprocess.") and name.split(".", 1)[1] in _AST_SUBPROCESS:
            emit("ast-subprocess", "MED", ln, "subprocess call — command execution")
        elif name.startswith("os.") and name.split(".", 1)[1] in _AST_OS_EXEC:
            emit("ast-os-exec", "HIGH", ln, "os.system/exec-family call — shell command execution")
        elif name == "getattr" and len(node.args) >= 2 and not isinstance(node.args[1], ast.Constant):
            emit("ast-dyn-getattr", "LOW", ln, "dynamic getattr() with non-literal attribute")


# --------------------------------------------------------------------------- #
# YARA scan  (OPTIONAL — clean-room rules in tools/yara_rules/; needs yara-python)
# --------------------------------------------------------------------------- #

_YARA_DIR = Path(__file__).resolve().parent / "yara_rules"
_YARA_SEV = {"MEDIUM": "MED"}  # normalize .yar severity strings to TAOM's set


def scan_yara(files: list[tuple[Path, str]], res: Result) -> None:
    try:
        import yara  # type: ignore  # optional dependency
    except ImportError:
        res.findings.append(Finding(
            "yara-unavailable", "INFO", "yara", ".", 0,
            "yara-python not installed; YARA signature layer skipped "
            "(pip install yara-python for webshell/malware/C2 coverage)"))
        return
    rule_files = sorted(_YARA_DIR.glob("*.yar")) if _YARA_DIR.is_dir() else []
    if not rule_files:
        return
    try:
        rules = yara.compile(filepaths={p.stem: str(p) for p in rule_files})
    except yara.Error as e:  # malformed rule — fail open, don't crash the audit
        res.findings.append(Finding("yara-compile-error", "INFO", "yara", ".", 0,
            f"YARA rules failed to compile: {e}"))
        return
    for path, rel in files:
        try:
            data = path.read_bytes()
        except OSError:
            continue
        try:
            matches = rules.match(data=data)
        except yara.Error:
            continue
        for m in matches:
            meta = getattr(m, "meta", {}) or {}
            sev = str(meta.get("severity", "HIGH")).upper()
            sev = _YARA_SEV.get(sev, sev)
            if sev not in SEV_RANK:
                sev = "HIGH"
            cat = str(meta.get("category", "yara-match"))
            res.findings.append(Finding(
                f"yara-{m.rule}", sev, cat, rel, 0,
                str(meta.get("description", f"YARA rule {m.rule} matched"))))


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


def scan_file(path: Path, root: Path, res: Result, external: bool = False) -> None:
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

    # On a TAOM self-audit the SkillSpector regex categories are advisory (INFO):
    # TAOM's own hooks/docs legitimately reference `--no-verify`, quote attack
    # phrases as examples, etc. Pass --external when vetting an untrusted/foreign
    # tree to raise them to full severity. Genuine code/IOC danger is caught at
    # full severity by the separate hook-exfil, AST, and YARA layers regardless.
    scan_skillspector(rel, text, res, downgrade=not external)
    if path.suffix == ".py":
        scan_ast(rel, text, res)


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
    ap.add_argument("--no-repo-secrets", action="store_true",
                    help="skip the repo-wide secret sweep over git-tracked files; the "
                         ".claude config surface is still scanned")
    ap.add_argument("--external", action="store_true",
                    help="scanning an external/untrusted tree (not TAOM's own): raise the "
                         "SkillSpector regex categories from advisory INFO to full severity")
    args = ap.parse_args(argv)

    root = Path(args.root).resolve()
    if not root.is_dir():
        print(f"error: --root {root} is not a directory", file=sys.stderr)
        return 1

    res = Result()
    collected = collect(root)
    for p in collected:
        scan_file(p, root, res, external=args.external)
    scan_yara([(p, str(p.relative_to(root)).replace("\\", "/")) for p in collected], res)

    # The config surface is where a malicious skill hides; the rest of the repo
    # is where a credential gets pasted by accident. Both are in scope, but only
    # the secret rules run over the second one.
    if not args.no_repo_secrets:
        tracked = collect_tracked(root)
        if tracked is None:
            res.findings.append(Finding(
                "repo-secrets-unavailable", "INFO", "secrets", ".", 0,
                "Repo-wide secret sweep skipped: --root is not a git work tree, or git "
                "is unavailable. The .claude config surface was still scanned."))
        else:
            already = {str(q.relative_to(root)).replace("\\", "/") for q in collected}
            scan_repo_secrets(root, tracked, res, already)

    min_rank = SEV_RANK[args.min]
    (report_json if args.json else report_text)(res, min_rank)

    return 2 if any(f.severity == "CRITICAL" for f in res.findings) else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
