#!/usr/bin/env python3
"""Mine this project's Claude Code session transcripts for evidence about the AI harness.

The evidence step of the weekly harness retro. Read-only, stdlib only, no model calls. It streams
every main and subagent transcript in the window and writes `report.md` and `signals.json` OUTSIDE
the repo (a --out inside --repo is refused).

    python tools/harness_retro.py --since 2026-09-21
    python tools/harness_retro.py --top 30 --out E:/Temp/retro

Exit codes: 0 ok; 2 bad arguments or refused out dir; 3 zero records in the window (UNCHECKED).

The transcript format is internal to Claude Code and can change, so every access is defensive: a
line that does not parse is counted as a parse failure, a parsed value that is not an object is
skipped, and neither is fatal. Secrets are redacted with the patterns of `audit_claude_config.py`
(the named ones and the generic key-equals-value one) before anything is written. Record dates are
UTC and `--since` is a calendar date, so the window edge can be off by the hours of the UTC offset.
"""
import argparse
import json
import re
import sys
import tempfile
from collections import Counter, defaultdict
from datetime import date, datetime, timedelta, timezone
from pathlib import Path

TOOLS_DIR = Path(__file__).resolve().parent
if str(TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(TOOLS_DIR))

from audit_claude_config import _GENERIC_SECRET, _SECRET_COMPILED  # noqa: E402

# The hooks' command reader, so section 9 reads a command the way the git gates do.
HOOKS_DIR = TOOLS_DIR.parent / ".claude" / "hooks"
if str(HOOKS_DIR) not in sys.path:
    sys.path.insert(0, str(HOOKS_DIR))
sys.dont_write_bytecode = True  # no __pycache__ folder inside .claude/hooks
import _shellwords  # noqa: E402

_VALUE_OPTS = _shellwords.GIT_OPTS_WITH_VALUE | {"-R", "--repo"}   # gh -R owner/repo

DEFAULT_SLUG = "e--repos-TAOM"
RECURRING_SESSIONS = 3
RUNAWAY_LEN = 3
STALE_CARD_DAYS = 21
TOKEN_GROWTH_POINTS = 0.05   # an agent whose share of cache creation plus output grew by 5 points or more
TOP_TRANSCRIPTS = 5
MODEL_FAMILIES = ("sonnet", "opus", "haiku", "fable")
CACHE_BREAK_TOKENS = 100_000   # one request that writes more than this to the cache re-sent the prompt
UNATTRIBUTED = "subagents (unattributed)"
# USD per million tokens: input, output, cache read, 5-minute write, 1-hour write. Input, output and
# cache read are the API list rates in Claude Code's bundled claude-api skill (cached 2026-10-06); the
# writes are 1.25x and 2x of input. A subscription weighs tokens by rates it does not publish, so these
# dollars show relative size and are not a bill. A model missing here is reported, never guessed.
LIST_PRICES = {
    "claude-opus-5-5": (4, 20, 0.2, 5, 8),
    "claude-opus-5": (5, 25, 0.5, 6.25, 10),
    "claude-sonnet-5-5": (2, 10, 0.2, 2.5, 4),
    "claude-sonnet-5": (2, 10, 0.2, 2.5, 4),
    "claude-fable-5-1": (10, 50, 0.25, 12.5, 20),
    "claude-haiku-5-5": (0.1, 0.5, 0.01, 0.125, 0.2),
    "claude-haiku-4-5-20251001": (1, 5, 0.1, 1.25, 2),
}
CONTEXT_BUCKETS = ((200_000, "under 200k"), (300_000, "200k to 300k"), (500_000, "300k to 500k"),
                   (None, "500k and over"))
REVIEW_AGENT = "deep-reviewer"
REVIEW_RUN_GAP_SECONDS = 3600   # lenses of one session further apart than this are separate review runs

# The SessionStart banners CLAUDE.md names, the skill each asks for, and the skill's main command as it
# appears in a shell call. Section 7 pairs them, so a banner nobody acts on shows as such.
BANNERS = (
    ("GAME VERSION DRIFT", "engine-bump", re.compile(r"(?:^|[\\/])(?:native_archive\.py|decompile_bannerlord\.ps1)$")),
    ("ARMORY ART DRIFT", "armory-audit", re.compile(r"(?:^|[\\/])audit_armory_refs\.py$")),
    ("UPSTREAM AHEAD", "upstream-review", re.compile(r"(?:^|[\\/])upstream_watch\.py$")),
    ("HARNESS RETRO DUE", "harness-retro", re.compile(r"(?:^|[\\/])harness_retro\.py$")),
)
RULES_MARKER = "/.claude/rules/"

HOOK_DENY_RE = re.compile(r"^(PreToolUse|PostToolUse):(\S+) hook error: \[([^\]]+)\](.*)", re.S)
CLASSIFIER_PREFIX = "Permission for this action was denied by the Claude Code auto mode classifier"
REJECTION_PREFIX = "The user doesn't want to proceed with this tool use"
COMMAND_NAME_RE = re.compile(r"<command-name>\s*/?([^<\s]+)\s*</command-name>")
HOOK_ID_RE = re.compile(r"^([a-z0-9-]+):")
SCRIPT_RE = re.compile(r"([^\s\"'/\\]+\.(?:sh|py|ps1|js|cmd|bat))")

CORRECTION_RES = [re.compile(p, re.I) for p in (
    r"^\s*(no|nope|stop|wait|wrong|undo|revert|don't|do not|not that)\b",
    r"\bI (said|asked|told you)\b",
    r"\bwhy did you\b",
    r"\byou (didn't|did not|forgot|missed|ignored)\b",
    r"\bthat's (wrong|not what)\b",
)]

_WIN_PATH = re.compile(r"[A-Za-z]:\\[^\s'\"]*")
_POSIX_PATH = re.compile(r"(?<![\w<])/[\w.\-@~+]+(?:/[\w.\-@~+]+)+")
_GUID = re.compile(r"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b")
_HEX = re.compile(r"\b[0-9a-fA-F]{8,}\b")
_DIGITS = re.compile(r"\d+")
_SPACE = re.compile(r"\s+")
_EXIT_LINE = re.compile(r"^\s*Exit code \d+\s*\n?")
_ERR_TAG = re.compile(r"</?tool_use_error>")
_NOT_A_FAMILY = {"cd", "chdir", "pushd", "popd", "set-location", "sl", "push-location", "pop-location",
                 "for", "select", "case"}
FAMILY_HEAD = 4096
_SUBCOMMAND_TOOLS = {"git", "dotnet", "gh"}
_SCRIPT_TOOLS = {"python", "py", "pwsh", "powershell", "bash", "sh"}
_INLINE_FLAGS = {"-c", "-command", "-encodedcommand", "-e", "-ec"}
_PWSH_VALUE_OPTS = {"-executionpolicy", "-ep", "-workingdirectory", "-wd", "-windowstyle", "-outputformat",
                    "-inputformat", "-configurationname"}


def _redact_generic(m):
    whole, start = m.group(0), m.start(0)
    return whole[:m.start(2) - start] + "[REDACTED:secret-generic]" + whole[m.end(2) - start:]


def redact(text):
    for rule_id, rx, _desc in _SECRET_COMPILED:
        text = rx.sub("[REDACTED:%s]" % rule_id, text)
    return _GENERIC_SECRET.sub(_redact_generic, text)


def normalize_signature(text):
    """Stable signature of an error text: same failure, different paths and numbers, same string."""
    text = redact(text or "")
    text = _EXIT_LINE.sub("", text.strip())
    text = _ERR_TAG.sub("", text).strip()
    lines = [ln.strip() for ln in text.splitlines() if ln.strip()]
    if not lines:
        return "(empty result)"
    if "Traceback (most recent call last)" in text:
        line = lines[-1]
    else:
        line = lines[0]
    line = _WIN_PATH.sub("<path>", line)
    line = _POSIX_PATH.sub("<path>", line)
    line = _GUID.sub("<id>", line)
    line = _HEX.sub("<id>", line)
    line = _DIGITS.sub("#", line)
    line = _SPACE.sub(" ", line).strip()
    return line[:100]


def hook_basename(text):
    """`"$DIR"/.claude/hooks/x.sh` -> `x`."""
    name = re.split(r"[\\/]", text.strip())[-1].strip("\"' ")
    return re.sub(r"\.(sh|py|ps1)$", "", name)


def script_of(command):
    found = SCRIPT_RE.findall(command or "")
    if found:
        return found[-1]
    return redact((command or "").strip())[:40] or "(unknown)"


def command_family(command, tool="Bash"):
    """`cd /x && FOO=1 git -C y diff a` -> `git diff`; `python -B tools/x.py` -> `python x.py`.
    The command is read by the git gates' own reader, so `timeout 600 dotnet test` and
    `{ git status; }` count as their command here too (#985; the idea of rtk's command reader,
    docs/reviews/adopt-rtk-2026-10-23.md). The first segment that runs something other than a
    directory change or a loop header names the family."""
    try:
        # The family is the first command, so the head of a long script is enough to read.
        text, _bodies = _shellwords.strip_heredocs(_shellwords.to_posix((command or "")[:FAMILY_HEAD], tool))
        segs = _shellwords.segments(text).split("\n")
    except Exception:  # a reader failure costs one bucket, never the run
        segs = [command or ""]
    for name, _exe, rest in _segment_commands(segs, tool):
        if name not in _NOT_A_FAMILY:
            return name + _family_detail(name, rest)
    return "(unknown)"


def _segment_commands(segs, tool):
    """(lower-case name, the command word as written, its arguments) of what each segment runs."""
    found = []
    for seg in segs:
        words = [w[2] for w in _shellwords._lead_words(seg, 40)]
        k = _shellwords.command_index(words, tool)
        if k < len(words):
            found.append((re.sub(r"\.exe$", "", re.split(r"[\\/]", words[k])[-1].lower()), words[k], words[k + 1:]))
    return found


def command_runs_script(command, rx, tool="Bash"):
    """True when a segment of the command runs a script `rx` matches: the first operand of a script
    runner (`python -B tools/x.py`), or the command word itself (`./tools/x.sh`). A command that only
    names the script (`git diff tools/x.py`, `grep y tools/x.py`) does not."""
    try:
        text, _bodies = _shellwords.strip_heredocs(_shellwords.to_posix((command or "")[:FAMILY_HEAD], tool))
        segs = _shellwords.segments(text).split("\n")
    except Exception:   # a reader failure costs one command, never the run
        return False
    for name, exe, rest in _segment_commands(segs, tool):
        target = exe
        if name in _SCRIPT_TOOLS:
            target, skip = None, False
            for w in rest:
                lw = w.lower()
                if skip:
                    skip = False
                elif lw in _INLINE_FLAGS:
                    break
                elif lw in _PWSH_VALUE_OPTS:
                    skip = True
                elif not w.startswith("-"):
                    target = w
                    break
        if target and rx.search(target):
            return True
    return False


def _family_detail(name, rest):
    """` <subcommand>` for git, dotnet and gh; ` <script basename>` for a script runner, and nothing
    for inline code (`-c`, `bash -lc`, `pwsh -EncodedCommand`, a heredoc), so a script never
    becomes its own bucket."""
    skip = False
    for w in rest:
        lw = w.lower()
        if skip:
            skip = False
        elif name in _SUBCOMMAND_TOOLS:
            if w in _VALUE_OPTS:
                skip = True
            elif not w.startswith("-"):
                return " " + w
        elif name in _SCRIPT_TOOLS:
            if lw in _INLINE_FLAGS or lw.startswith("<") or (name in ("bash", "sh") and re.fullmatch(r"-[a-z]*c[a-z]*", lw)):
                return ""
            if lw in _PWSH_VALUE_OPTS:
                skip = True
            elif not w.startswith("-"):
                return "" if re.search(r"\s", w) else " " + re.split(r"[\\/]", w)[-1]
        else:
            return ""
    return ""


def default_since(repo, today=None):
    today = today or date.today()
    newest = None
    reviews = Path(repo) / "docs" / "reviews"
    if reviews.is_dir():
        for p in reviews.glob("harness-retro-*.md"):
            m = re.fullmatch(r"harness-retro-(\d{4}-\d{2}-\d{2})\.md", p.name)
            if m:
                try:
                    d = date.fromisoformat(m.group(1))
                except ValueError:
                    continue
                if newest is None or d > newest:
                    newest = d
    return newest or (today - timedelta(days=30))


def result_text(content):
    if isinstance(content, str):
        return content
    if isinstance(content, list):
        return "\n".join(b.get("text", "") for b in content
                         if isinstance(b, dict) and isinstance(b.get("text"), str))
    return ""


class Group:
    __slots__ = ("count", "sessions", "sample", "example", "first", "last")

    def __init__(self):
        self.count = 0
        self.sessions = set()
        self.sample = ""
        self.example = ""
        self.first = ""
        self.last = ""

    def add(self, sid, sample="", ts=None):
        self.count += 1
        self.sessions.add(sid)
        if not self.sample and sample:
            self.sample = sample
        if not self.example:
            self.example = sid
        day = (ts or "")[:10]
        if day:
            # The dates let a retro tell a problem a later commit fixed from one still happening.
            self.first = min(self.first or day, day)
            self.last = max(self.last, day)


class Stats:
    def __init__(self):
        self.main_files = 0
        self.subagent_files = 0
        self.records = 0
        self.parse_failures = 0
        self.compactions = 0
        self.newest_date = None
        self.denies = defaultdict(Group)
        self.blocks = defaultdict(Group)
        self.broken = defaultdict(Group)
        self.cancelled = defaultdict(Group)
        self.cancel_timeouts = Counter()
        self.classifier = Counter()
        self.rejections = Counter()
        self.tool_errors = defaultdict(Group)
        self.runaways = []
        self.corr_count = 0
        self.corr_sessions = set()
        self.corr_excerpts = []
        self.skills = Counter()
        self.slash_commands = Counter()
        self.subagent_types = Counter()
        self.models = Counter()
        self.out_tools = defaultdict(lambda: [0, 0, 0])  # tool -> [results, chars, max]
        self.out_families = defaultdict(lambda: [0, 0, 0])
        self.out_reads = defaultdict(lambda: [0, 0, 0])   # read_path -> [results, chars, max]
        self.read_whole = Counter()                        # read_path -> reads with no offset or limit
        self.seen_requests = set()
        self.tok = [0, 0, 0, 0, 0]   # input, output, cache read, cache creation, requests
        self.output_partial = 0
        self.tok_skills = defaultdict(lambda: [0, 0, 0, 0])   # skill -> input, output, cache read, cache creation
        self.tok_agents = defaultdict(lambda: [0, 0, 0, 0])
        self.cache_breaks = 0
        # Budget by agent type and model (#1008; the idea of paperclipai/paperclip's budget tracking,
        # docs/reference/provenance-register.md).
        self.by_agent_model = defaultdict(lambda: [0, 0, 0, 0, 0])  # (agent, model) -> requests, in, out, read, create
        self.file_tokens = []                                       # (agent, file stem, total tokens)
        # agent -> requests, USD input, output, cache read, cache write; context tokens, tool calls;
        # thinking tokens and the output of the requests that report thinking.
        self.cost = defaultdict(lambda: [0, 0.0, 0.0, 0.0, 0.0, 0, 0, 0, 0])
        self.unpriced = Counter()
        self.main_context = defaultdict(lambda: [0, 0.0])    # bucket -> requests, USD
        self.rebuilds = defaultdict(lambda: [0, 0, 0.0])     # (who, idle gap) -> events, tokens, USD
        self.lenses = []                                     # (session, start, USD, requests, peak context)
        self.banner_sessions = defaultdict(set)              # banner -> sessions whose SessionStart printed it
        self.direct_sessions = defaultdict(set)              # banner -> sessions that ran its skill's command
        self.versions = defaultdict(set)                     # Claude Code version -> sessions
        self.rules_loaded = defaultdict(lambda: [set(), set()])   # rule file -> main sessions, subagent files


def add_output(table, key, size):
    row = table[key]
    row[0] += 1
    row[1] += size
    row[2] = max(row[2], size)


def read_path(path):
    """A Read path as one key across checkouts: the part inside `<x>/repos/<repo>/` (so a worktree's
    copy counts with the main checkout's), `~/.claude/...` for the user config, else the last three
    parts. Lower-cased, since Windows paths are case-insensitive (#1002)."""
    p = str(path or "").replace("\\", "/").lower()
    if not p:
        return "(unknown)"
    m = re.search(r"/repos/[^/]+/(.+)$", p)
    if m:
        return m.group(1)
    m = re.search(r"/(\.claude/.+)$", p)
    if m:
        return "~/" + m.group(1)
    return ".../" + "/".join(p.split("/")[-3:])


def _num(v):
    return v if isinstance(v, (int, float)) and not isinstance(v, bool) else 0


def read_usage(r):
    """(request key, (input, output, cache read, cache creation), final, model) of an assistant record, or None.
    One API request is written as several records (one per content block) that share a requestId.
    Input and cache fields are identical across them. Output_tokens grows as the request streams, so
    the record with the most output is used, but some requests (many in subagent transcripts) are
    written only as mid-stream snapshots. `final` is True when this record has a non-null
    stop_reason; a request none of whose records is final has an output count that is a floor."""
    msg = r.get("message")
    usage = msg.get("usage") if isinstance(msg, dict) else None
    if not isinstance(usage, dict):
        return None
    key = r.get("requestId") or msg.get("id") or "uuid:%s" % r.get("uuid")
    counts = (_num(usage.get("input_tokens")), _num(usage.get("output_tokens")),
              _num(usage.get("cache_read_input_tokens")), _num(usage.get("cache_creation_input_tokens")))
    return str(key), counts, msg.get("stop_reason") is not None, str(msg.get("model") or "")


def commit_usage(stats, calls, agent, stem=""):
    """Add one file's deduplicated requests. `agent` is the subagent type, None for a main file."""
    label = agent or "main"
    spent = 0
    for key, (usage, skill, final, model) in calls.items():
        if key in stats.seen_requests:
            continue
        stats.seen_requests.add(key)
        spent += sum(usage)
        if model != "<synthetic>" and any(usage):
            row = stats.by_agent_model[(label, model or "(unknown)")]
            row[0] += 1
            for i in range(4):
                row[i + 1] += usage[i]
        for i in range(4):
            stats.tok[i] += usage[i]
        stats.tok[4] += 1
        if not final:
            stats.output_partial += 1
        bucket = stats.tok_agents[agent] if agent is not None else stats.tok_skills[skill] if skill else None
        if bucket is not None:
            for i in range(4):
                bucket[i] += usage[i]
        if usage[3] > CACHE_BREAK_TOKENS:
            stats.cache_breaks += 1
    if spent:
        stats.file_tokens.append((label, stem, spent))


def note_request(details, key, r, ts):
    """Keep what the cost section needs of one API request, whose records each hold one content block:
    its first timestamp, its 1-hour cache writes, its thinking count and its distinct tool calls."""
    msg = r["message"]
    usage = msg["usage"]
    d = details.setdefault(key, {"ts": ts, "one_hour": 0, "thinking": None, "tools": set()})
    split = usage.get("cache_creation")
    if isinstance(split, dict):
        d["one_hour"] = max(d["one_hour"], _num(split.get("ephemeral_1h_input_tokens")))
    extra = usage.get("output_tokens_details")
    if isinstance(extra, dict) and "thinking_tokens" in extra:
        d["thinking"] = max(d["thinking"] or 0, _num(extra["thinking_tokens"]))
    blocks = msg.get("content")
    for b in blocks if isinstance(blocks, list) else []:
        if isinstance(b, dict) and b.get("type") == "tool_use":
            d["tools"].add(str(b.get("id")))


def request_cost(model, usage, one_hour):
    """(input, output, cache read, cache write) in USD, or None for a model with no list price."""
    p = LIST_PRICES.get(model.split("[")[0])
    if p is None:
        return None
    inp, out, read, create = usage
    return (inp * p[0] / 1e6, out * p[1] / 1e6, read * p[2] / 1e6,
            ((create - one_hour) * p[3] + one_hour * p[4]) / 1e6)


def parse_time(ts):
    try:
        t = datetime.fromisoformat(str(ts).replace("Z", "+00:00"))
    except ValueError:
        return None
    return t if t.tzinfo else t.replace(tzinfo=timezone.utc)


def idle_bucket(first, gap):
    if first:
        return "first request"
    if not gap or gap <= 300:
        return "under 5 min"
    return "5 min to 1 h" if gap <= 3600 else "over 1 h"


def commit_cost(stats, calls, details, agent, path):
    """Add one file's requests to the cost section. Runs before commit_usage, whose seen set says which
    are new. A repeated request (a resumed session) adds nothing but still moves the idle clock."""
    who = agent or "main"
    prev = None
    start = None
    lens = [0.0, 0, 0]   # USD, requests, peak context
    for key, (usage, _skill, _final, model) in calls.items():
        d = details[key]
        t = parse_time(d["ts"])
        gap = (t - prev).total_seconds() if t and prev else None
        first = prev is None
        prev = t or prev
        if key in stats.seen_requests or model == "<synthetic>" or not any(usage):
            continue
        priced = request_cost(model, usage, d["one_hour"])
        if priced is None:
            stats.unpriced[model or "(unknown)"] += 1
            priced = (0.0, 0.0, 0.0, 0.0)
        spent = sum(priced)
        context = usage[0] + usage[2] + usage[3]
        row = stats.cost[who]
        row[0] += 1
        for i in range(4):
            row[i + 1] += priced[i]
        row[5] += context
        row[6] += len(d["tools"])
        if d["thinking"] is not None:
            row[7] += d["thinking"]
            row[8] += usage[1]
        if not agent:
            bucket = stats.main_context[next(lb for lim, lb in CONTEXT_BUCKETS if lim is None or context < lim)]
            bucket[0] += 1
            bucket[1] += spent
        if usage[3] > CACHE_BREAK_TOKENS:
            rebuild = stats.rebuilds[("subagent" if agent else "main", idle_bucket(first, gap))]
            rebuild[0] += 1
            rebuild[1] += usage[3]
            rebuild[2] += priced[3]
        start = start or t
        lens[0] += spent
        lens[1] += 1
        lens[2] = max(lens[2], context)
    if agent == REVIEW_AGENT and lens[1] and start:
        session = path.parts[path.parts.index("subagents") - 1]
        stats.lenses.append((session, start, lens[0], lens[1], lens[2]))


def agent_type(path):
    """The agentType of a subagent transcript's sibling .meta.json, else UNATTRIBUTED."""
    try:
        meta = json.loads(path.with_suffix(".meta.json").read_text(encoding="utf-8"))
        kind = meta.get("agentType") if isinstance(meta, dict) else None
        return kind if isinstance(kind, str) and kind else UNATTRIBUTED
    except (OSError, ValueError):
        return UNATTRIBUTED


def classify_error(stats, text, tool, sid, ts=None):
    """Record one is_error tool result. Returns the (tool, signature) key of a plain tool error."""
    m = HOOK_DENY_RE.match(text)
    if m:
        event, hook_tool, bracket, rest = m.groups()
        reason = redact(rest.lstrip(": ").strip())[:100]
        stats.denies[(hook_basename(bracket), event, hook_tool)].add(sid, reason, ts)
        return None
    if text.startswith(CLASSIFIER_PREFIX):
        stats.classifier[tool] += 1
        return None
    if text.startswith(REJECTION_PREFIX):
        stats.rejections[tool] += 1
        return None
    sig = normalize_signature(text)
    stats.tool_errors[(sig, tool)].add(sid, ts=ts)
    return (tool, sig)


def blocking_reason(err):
    """The reason text of a hook_blocking_error, which recent versions nest one dict deep."""
    for _ in range(3):
        if isinstance(err, dict):
            err = err.get("blockingError")
    return err if isinstance(err, str) else ""


def handle_attachment(stats, att, sid, ts=None):
    if not isinstance(att, dict):
        return
    kind = att.get("type")
    if kind == "hook_blocking_error":
        err = blocking_reason(att.get("blockingError"))
        m = HOOK_ID_RE.match(err)
        name = m.group(1) if m else str(att.get("hookName", "(unknown)"))
        stats.blocks[(name, str(att.get("hookEvent", "")))].add(sid, redact(err)[:100], ts)
    elif kind == "hook_non_blocking_error":
        stderr = att.get("stderr")
        first = next((ln for ln in (stderr if isinstance(stderr, str) else "").splitlines()
                      if ln.strip()), "")
        stats.broken[(script_of(str(att.get("command", ""))), normalize_signature(first))].add(sid, ts=ts)
    elif kind == "hook_cancelled":
        cmd = att.get("command")
        label = str(att.get("hookName", "(unknown)"))
        if isinstance(cmd, str) and cmd:
            label += " " + script_of(cmd)
        stats.cancelled[label].add(sid, ts=ts)
        if att.get("timedOut") in (True, "true", "True"):
            # Killed at its registered timeout: a gate in that state failed open (hook-authoring.md).
            stats.cancel_timeouts[label] += 1


def note_banners(stats, att, sid):
    """Record which banners a SessionStart hook printed in this session."""
    if not isinstance(att, dict) or att.get("type") != "hook_success" or att.get("hookEvent") != "SessionStart":
        return
    text = att.get("content")
    if isinstance(text, str):
        for banner, _skill, _rx in BANNERS:
            if banner in text:
                stats.banner_sessions[banner].add(sid)


def rule_name(path):
    """The rule file under .claude/rules/ that `path` names, else None."""
    if not isinstance(path, str):
        return None
    norm = path.replace("\\", "/")
    at = norm.lower().find(RULES_MARKER)
    return norm[at + len(RULES_MARKER):] if at >= 0 and norm.lower().endswith(".md") else None


def note_rules_loaded(stats, att, sid, agent, stem):
    """Record rule files named by an `instructions` attachment (the files loaded at launch) or a
    `nested_memory` one (a path rule loaded on a matching read). A subagent transcript counts once per
    file, because the attachment can repeat inside it."""
    if not isinstance(att, dict):
        return
    if att.get("type") == "instructions" and isinstance(att.get("files"), list):
        paths = [f.get("path") for f in att["files"] if isinstance(f, dict)]
    elif att.get("type") == "nested_memory":
        paths = [att.get("path")]
    else:
        return
    for name in filter(None, (rule_name(p) for p in paths)):
        stats.rules_loaded[name][1 if agent else 0].add(stem if agent else sid)


def queued_prompt_text(prompt):
    """The text of a queued_command prompt: a string, or content blocks of which only text counts."""
    if isinstance(prompt, str):
        return prompt
    if isinstance(prompt, list):
        return "\n".join(b.get("text", "") for b in prompt
                         if isinstance(b, dict) and b.get("type") == "text" and isinstance(b.get("text"), str))
    return ""


def human_text(r):
    """The text a person typed in record `r`, or None when it is not a human turn. A prompt typed while
    the model works has no `user` record: it is an `attachment` of type queued_command with
    humanTurn true, and its prompt holds the text (possibly empty for an image-only prompt)."""
    if r.get("type") == "attachment":
        att = r.get("attachment")
        if isinstance(att, dict) and att.get("type") == "queued_command" and att.get("humanTurn") is True:
            return queued_prompt_text(att.get("prompt"))
        return None
    msg = r.get("message")
    content = msg.get("content") if isinstance(msg, dict) else None
    if isinstance(content, str):
        return content
    if isinstance(content, list):
        if any(isinstance(b, dict) and b.get("type") == "tool_result" for b in content):
            return None
        parts = [b.get("text", "") for b in content
                 if isinstance(b, dict) and b.get("type") == "text" and isinstance(b.get("text"), str)]
        return "\n".join(parts) if parts else None
    return None


_INJECTED_BLOCK_RE = re.compile(r"\A\s*<([A-Za-z_][\w-]*)[^>]*>.*?</\1>", re.S)


def human_marked(r):
    """True when the transcript itself marks a user record as typed by a person (`origin.kind` or
    `turnOrigin` is "human"), even when it opens with an injected block such as <ide_opened_file>."""
    if r.get("type") != "user":
        return False
    origin = r.get("origin")
    return r.get("turnOrigin") == "human" or (isinstance(origin, dict) and origin.get("kind") == "human")


def strip_injected_blocks(text):
    """`text` without its leading <tag>...</tag> blocks: what a person typed after injected context."""
    while True:
        m = _INJECTED_BLOCK_RE.match(text)
        if not m:
            return text.strip()
        text = text[m.end():]


def handle_human(stats, r, sid, ts):
    """Count one user or queued-prompt record that carries no tool result. True when a person typed it."""
    if r.get("isSidechain") or r.get("isMeta") or r.get("isCompactSummary"):
        return False
    text = human_text(r)
    marked = human_marked(r)
    if text is None:
        if not marked:
            return False
        text = ""
    stripped = text.strip()
    queued = r.get("type") == "attachment"
    cmd = COMMAND_NAME_RE.search(stripped)
    if cmd:
        # Built-ins such as /model share this tag, so slash commands are counted apart from skills.
        stats.slash_commands[cmd.group(1)] += 1
        return True
    if marked:
        stripped = strip_injected_blocks(stripped)   # the typed text after pasted or IDE context
        if not stripped:
            return True
    elif not stripped or stripped.startswith("<"):
        return queued   # an image-only queued prompt is still a human turn; a user record like it is not
    if any(rx.search(stripped) for rx in CORRECTION_RES):
        stats.corr_count += 1
        stats.corr_sessions.add(sid)
        stats.corr_excerpts.append((sid, ts or "", _SPACE.sub(" ", redact(stripped))[:160]))
    return True


def handle_assistant(stats, r, tools, count):
    """Map each tool_use id to (tool name, the id of the assistant message that sent it, a detail): the
    shell command, or for Read (path key, whole read), else None. The family is read only when a
    result is counted, so records outside the window cost no parse."""
    msg = r.get("message")
    content = msg.get("content") if isinstance(msg, dict) else None
    if not isinstance(content, list):
        return
    for b in content:
        if not isinstance(b, dict) or b.get("type") != "tool_use":
            continue
        name = str(b.get("name", "?"))
        inp = b.get("input") if isinstance(b.get("input"), dict) else {}
        if b.get("id"):
            if name in ("Bash", "PowerShell"):
                detail = str(inp.get("command", ""))[:FAMILY_HEAD]
            elif name == "Read":
                detail = (read_path(inp.get("file_path")), not (inp.get("offset") or inp.get("limit")))
            else:
                detail = None
            tools[b["id"]] = (name, str(msg.get("id") or b["id"]), detail)
        if not count:
            continue
        if name in ("Bash", "PowerShell"):
            for banner, _skill, rx in BANNERS:
                if command_runs_script(str(inp.get("command", "")), rx, name):
                    stats.direct_sessions[banner].add(str(r.get("sessionId") or ""))
        if name == "Skill" and inp.get("skill"):
            stats.skills[str(inp["skill"])] += 1
        elif name in ("Agent", "Task"):
            stats.subagent_types[str(inp.get("subagent_type") or "general-purpose")] += 1
            stats.models[str(inp.get("model") or "(definition)")] += 1


def scan_file(stats, path, since_str, agent=None):
    tools = {}
    calls = {}        # request key -> ((in, out, read, create), skill, final), the max-output record wins
    details = {}      # request key -> what the cost section needs (note_request)
    skill = None      # the skill invoked most recently in this file, until a person types again
    last_ts = None
    # [key, sid, set of assistant message ids]. A run grows by MESSAGES, not by results: parallel
    # calls sent in one message that fail alike are one batch, not a retry loop.
    run = None

    def flush():
        nonlocal run
        if run and len(run[2]) >= RUNAWAY_LEN:
            stats.runaways.append({"session": run[1], "tool": run[0][0],
                                   "signature": run[0][1], "length": len(run[2])})
        run = None

    with open(path, encoding="utf-8", errors="replace") as fh:
        for line in fh:
            line = line.strip()
            if not line:
                continue
            try:
                r = json.loads(line)
            except ValueError:
                stats.parse_failures += 1
                continue
            if not isinstance(r, dict):
                continue
            ts = r.get("timestamp")
            if isinstance(ts, str) and ts:
                last_ts = ts
            else:
                ts = last_ts
            rtype = r.get("type")
            in_window = not (ts and ts[:10] < since_str)
            if rtype == "assistant":
                handle_assistant(stats, r, tools, in_window)  # ids are needed even before the window
            if not in_window:
                continue
            stats.records += 1
            if ts and (stats.newest_date is None or ts[:10] > stats.newest_date):
                stats.newest_date = ts[:10]
            sid = str(r.get("sessionId") or path.stem)
            if isinstance(r.get("version"), str) and r["version"]:
                stats.versions[r["version"]].add(sid)
            if rtype == "assistant":
                got = read_usage(r)
                if got:
                    note_request(details, got[0], r, ts)
                    prev = calls.get(got[0])
                    final = got[2] or (prev is not None and prev[2])
                    if prev is None or got[1][1] >= prev[0][1]:
                        calls[got[0]] = (got[1], skill, final, got[3])
                    else:
                        calls[got[0]] = prev[:2] + (final, prev[3])
                msg = r.get("message")
                blocks = msg.get("content") if isinstance(msg, dict) else None
                for b in blocks if isinstance(blocks, list) else []:
                    if isinstance(b, dict) and b.get("type") == "tool_use" and b.get("name") == "Skill":
                        inp = b.get("input") if isinstance(b.get("input"), dict) else {}
                        skill = str(inp["skill"]) if inp.get("skill") else skill
                continue
            if rtype == "system":
                if r.get("subtype") == "compact_boundary":
                    stats.compactions += 1
            elif rtype == "attachment":
                att = r.get("attachment")
                handle_attachment(stats, att, sid, ts)
                note_banners(stats, att, sid)
                note_rules_loaded(stats, att, sid, agent, path.stem)
                if handle_human(stats, r, sid, ts):
                    flush()   # typed while the model worked: no twin user record, still a human turn
                    skill = None
            elif rtype == "user":
                msg = r.get("message")
                content = msg.get("content") if isinstance(msg, dict) else None
                results = [b for b in content if isinstance(b, dict) and b.get("type") == "tool_result"] \
                    if isinstance(content, list) else []
                if not results and handle_human(stats, r, sid, ts):
                    flush()  # a person answered between the failures: not a runaway
                    skill = None
                for b in results:
                    tool, msg_id, detail = tools.get(b.get("tool_use_id"), ("?", b.get("tool_use_id"), None))
                    text = result_text(b.get("content"))
                    add_output(stats.out_tools, tool, len(text))
                    if isinstance(detail, str):
                        add_output(stats.out_families, command_family(detail, tool), len(text))
                    elif isinstance(detail, tuple):
                        add_output(stats.out_reads, detail[0], len(text))
                        stats.read_whole[detail[0]] += detail[1]
                    if b.get("is_error") is True:
                        key = classify_error(stats, text, tool, sid, ts)
                    else:
                        key = None
                    if key is not None and run and run[0] == key:
                        run[2].add(msg_id)
                    else:
                        flush()
                        if key is not None:
                            run = [key, sid, {msg_id}]
    flush()
    commit_cost(stats, calls, details, agent, path)
    commit_usage(stats, calls, agent, path.stem)


def discover(transcripts, since_dt, excluded=()):
    """Return (main, subagent) transcript paths whose mtime is not older than the window.

    A session in `excluded` loses its main transcript and everything under its folder."""
    cutoff = (since_dt - timedelta(days=1)).timestamp()
    skip = set(excluded)

    def fresh(p):
        try:
            return p.stat().st_mtime >= cutoff
        except OSError:
            return False

    main = sorted(p for p in transcripts.glob("*.jsonl") if fresh(p) and p.stem not in skip)
    # `**` because a workflow's agents sit one level down, in subagents/workflows/<run>/.
    sub = sorted(p for p in transcripts.glob("*/subagents/**/agent-*.jsonl")
                 if fresh(p) and p.relative_to(transcripts).parts[0] not in skip)
    return main, sub


def settings_hook_scripts(repo):
    """(basename without extension, file name) of every PreToolUse and Stop script in settings.json."""
    path = Path(repo) / ".claude" / "settings.json"
    names = []
    try:
        hooks = json.loads(path.read_text(encoding="utf-8-sig")).get("hooks", {})
    except (OSError, ValueError, AttributeError):
        return names
    for event in ("PreToolUse", "Stop"):
        for entry in hooks.get(event, []) if isinstance(hooks, dict) else []:
            for h in entry.get("hooks", []) if isinstance(entry, dict) else []:
                cmd = h.get("command") if isinstance(h, dict) else None
                if isinstance(cmd, str) and SCRIPT_RE.search(cmd):
                    item = (hook_basename(script_of(cmd)), script_of(cmd))
                    if item not in names:
                        names.append(item)
    return names


_ASK_RE = re.compile(r"permissionDecision\W+ask\b")
# Every other way a TAOM gate stops a call: a deny decision, exit 2, a Stop block (directly or
# through _stop_reminder.sh).
_STOPS_RE = re.compile(r"permissionDecision\W+deny\b|\bexit 2\b|_stop_reminder|\"block\"")


def is_ask_only(repo, filename):
    """True for a gate whose code can only ask, never deny or block. An approved ask leaves no trace
    in a transcript, so such a gate's zero fires says nothing about it."""
    try:
        text = (Path(repo) / ".claude" / "hooks" / filename).read_text(encoding="utf-8", errors="replace")
    except OSError:
        return False
    code = "\n".join(ln for ln in text.splitlines() if not ln.lstrip().startswith("#"))
    return bool(_ASK_RE.search(code)) and not _STOPS_RE.search(code)


def unused_skills(repo, used):
    root = Path(repo) / ".claude" / "skills"
    if not root.is_dir():
        return []
    return sorted(p.parent.name for p in root.glob("*/SKILL.md") if p.parent.name not in used)


def memory_health(memory, today):
    memory = Path(memory)
    out = {"memory_md_bytes": None, "memory_md_lines": None, "stale_cards": []}
    md = memory / "MEMORY.md"
    if md.is_file():
        data = md.read_bytes()
        out["memory_md_bytes"] = len(data)
        out["memory_md_lines"] = len(data.decode("utf-8", errors="replace").splitlines())
    feats = memory / "features"
    if feats.is_dir():
        for p in sorted(feats.glob("*.md")):
            modified = None
            try:
                head = p.read_text(encoding="utf-8", errors="replace")[:2000]
                fm = re.match(r"---\s*\n(.*?)\n---", head, re.S)
                m = re.search(r"^modified:\s*(\d{4}-\d{2}-\d{2})", fm.group(1), re.M) if fm else None
                if m:
                    modified = date.fromisoformat(m.group(1))
                else:
                    modified = datetime.fromtimestamp(p.stat().st_mtime).date()
            except (OSError, ValueError):
                continue
            age = (today - modified).days
            if age > STALE_CARD_DAYS:
                out["stale_cards"].append({"name": p.name, "age_days": age})
    out["stale_cards"].sort(key=lambda c: -c["age_days"])
    return out


def top_groups(groups, n, keys):
    rows = []
    for key, g in groups.items():
        row = dict(zip(keys, key))
        row.update(count=g.count, sessions=len(g.sessions), sample=g.sample, example=g.example,
                   first_seen=g.first, last_seen=g.last)
        rows.append(row)
    rows.sort(key=lambda r: (-r["sessions"], -r["count"]))
    return rows[:n]


def output_rows(table, label, total, n, shell_total=None):
    ranked = sorted(table.items(), key=lambda kv: -kv[1][1])
    if len(ranked) > n:
        rest = ranked[n:]
        ranked = ranked[:n] + [("(other)", [sum(v[0] for _k, v in rest), sum(v[1] for _k, v in rest),
                                            max(v[2] for _k, v in rest)])]
    rows = []
    for key, (results, chars, biggest) in ranked:
        row = {label: redact(key), "results": results, "chars": chars, "max": biggest,
               "share": round(chars / total, 4) if total else 0,
               "mean": round(chars / results) if results else 0}
        if shell_total is not None:
            row["shell_share"] = round(chars / shell_total, 4) if shell_total else 0
        rows.append(row)
    return rows


GROWTH_POINTS = 0.02   # a family whose share of all output grew by 2 points or more is flagged


def previous_signals(out):
    """(path, signals) of the newest other run beside `out` that has section 9, else (None, None).
    Runs land in dated folders under one root by default, so the last retro's run is a sibling."""
    best = None
    for p in out.parent.glob("*/signals.json"):
        if p.parent == out:
            continue
        try:
            d = json.loads(p.read_text(encoding="utf-8"))
        except (OSError, ValueError):
            continue
        if not isinstance(d, dict) or not isinstance(d.get("output_size"), dict):
            continue
        key = (str((d.get("window") or {}).get("today", "")), p.stat().st_mtime)
        if best is None or key > best[0]:
            best = (key, p, d)
    return (best[1], best[2]) if best else (None, None)


def output_growth(rows, out, label="family", section="families", share="share",
                  container="output_size", points=GROWTH_POINTS):
    """Rows whose `share` grew by GROWTH_POINTS since the previous run: shell families (share of all
    output) or files (share of Read output, #1002). A key the previous run folded into `(other)` has
    no known share and is not flagged as new; a previous run without this section is no baseline.
    `container` and `points` let section 10 reuse this against its own table and threshold."""
    path, prev = previous_signals(out)
    held = prev.get(container) if prev else None
    if not isinstance(held, dict) or section not in held:
        return {"baseline": None, "looked_in": str(out.parent), "rows": []}
    old = [r for r in held.get(section, []) if isinstance(r, dict)]
    then = {r.get(label): r.get(share, 0) for r in old}
    complete = "(other)" not in then
    grown = []
    for r in rows:
        if r[label] == "(other)" or (r[label] not in then and not complete):
            continue
        before = then.get(r[label], 0)
        if r[share] - before >= points:
            grown.append({label: r[label], "then": before, "now": r[share], "delta": round(r[share] - before, 4)})
    grown.sort(key=lambda r: -r["delta"])
    return {"baseline": str(path), "baseline_today": (prev.get("window") or {}).get("today"), "rows": grown}


def read_rows(stats, total, n):
    """Top files the Read tool returned, with whole reads and share of Read output (#1002)."""
    read_total = sum(v[1] for v in stats.out_reads.values())
    rows = output_rows(stats.out_reads, "file", total, n)
    ranked = [k for k, _v in sorted(stats.out_reads.items(), key=lambda kv: -kv[1][1])]
    for i, row in enumerate(rows):
        # output_rows ranks the same way, so row i is ranked[i]; a folded `(other)` row is last.
        keys = ranked[i:i + 1] if i < n else ranked[n:]
        row["whole"] = sum(stats.read_whole[k] for k in keys)
        row["read_share"] = round(row["chars"] / read_total, 4) if read_total else 0
    return rows


def output_size(stats, n):
    total = sum(v[1] for v in stats.out_tools.values())
    shell = sum(v[1] for v in stats.out_families.values())
    return {"total_chars": total,
            "tools": output_rows(stats.out_tools, "tool", total, n),
            "families": output_rows(stats.out_families, "family", total, n, shell),
            "files": read_rows(stats, total, n)}


def token_rows(table, n):
    """Top rows by total, each with the four fields: the total is about 98% cache reads, so the
    output and cache-write columns are what show who did the work and who rebuilt the cache."""
    ranked = sorted(table.items(), key=lambda kv: -sum(kv[1]))[:n]
    return [{"name": redact(k), "total": sum(v), "input": v[0], "output": v[1],
             "cache_read": v[2], "cache_creation": v[3]} for k, v in ranked]


def token_summary(stats, n):
    inp, out, read, create, requests = stats.tok
    seen = inp + read + create
    return {"requests": requests, "input": inp, "output": out, "cache_read": read, "cache_creation": create,
            "cache_read_share": round(read / seen, 4) if seen else 0,
            "cache_breaks": stats.cache_breaks, "output_partial": stats.output_partial,
            "skills": token_rows(stats.tok_skills, n),
            "subagent_types": token_rows(stats.tok_agents, n)}


def token_agents(stats, n, repo):
    """Section 10 additions: tokens by agent type and model, the costliest files, the model overrides."""
    def spend(v):
        return v[2] + v[4]
    ranked = sorted(stats.by_agent_model.items(), key=lambda kv: -spend(kv[1]))
    if len(ranked) > n:
        rest = ranked[n:]
        ranked = ranked[:n] + [("(other)", [sum(v[i] for _k, v in rest) for i in range(5)])]
    total = sum(spend(v) for v in stats.by_agent_model.values())
    rows = []
    for key, v in ranked:
        name = key if isinstance(key, str) else "%s / %s" % key
        rows.append({"agent": redact(name), "messages": v[0], "input": v[1], "output": v[2],
                     "cache_read": v[3], "cache_creation": v[4],
                     "share": round(spend(v) / total, 4) if total else 0})
    costly = sorted(stats.file_tokens, key=lambda t: -t[2])[:TOP_TRANSCRIPTS]
    return {"by_agent_model": rows,
            "transcripts": [{"transcript": redact("%s %s" % (a, re.sub(r"^agent-", "", stem)[:8])), "tokens": t}
                            for a, stem, t in costly],
            "overrides": model_overrides(stats, repo)}


def nearest_rank(values, p):
    ordered = sorted(values)
    return ordered[min(len(ordered) - 1, int(len(ordered) * p))] if ordered else 0


def review_runs(stats):
    """Group the review lenses into runs: same session, each lens within the gap of the one before."""
    runs = []     # [session, last start, lenses, USD]
    for session, start, spent, _requests, _peak in sorted(stats.lenses, key=lambda x: x[:2]):
        if not runs or runs[-1][0] != session or (start - runs[-1][1]).total_seconds() > REVIEW_RUN_GAP_SECONDS:
            runs.append([session, start, 0, 0.0])
        runs[-1][1] = start
        runs[-1][2] += 1
        runs[-1][3] += spent
    sizes = [r[2] for r in runs]
    run_costs = [r[3] for r in runs]
    lens_costs = [x[2] for x in stats.lenses]
    requests = [x[3] for x in stats.lenses]
    peaks = [x[4] for x in stats.lenses]
    return {"runs": len(runs), "lenses": len(stats.lenses),
            "lenses_per_run_p50": nearest_rank(sizes, 0.5), "lenses_per_run_p90": nearest_rank(sizes, 0.9),
            "lenses_per_run_max": max(sizes, default=0),
            "run_cost_p50": round(nearest_rank(run_costs, 0.5), 2), "run_cost_p90": round(nearest_rank(run_costs, 0.9), 2),
            "run_cost_max": round(max(run_costs, default=0), 2),
            "lens_cost_p50": round(nearest_rank(lens_costs, 0.5), 2), "lens_cost_p90": round(nearest_rank(lens_costs, 0.9), 2),
            "lens_requests_p50": nearest_rank(requests, 0.5), "lens_requests_p90": nearest_rank(requests, 0.9),
            "lens_peak_context_p50": nearest_rank(peaks, 0.5), "lens_peak_context_p90": nearest_rank(peaks, 0.9)}


def cost_summary(stats):
    """Section 11: where the usage goes, at list prices."""
    def spent(v):
        return sum(v[1:5])
    total = sum(spent(v) for v in stats.cost.values())
    rows = [{"agent": redact(k), "requests": v[0], "cost": round(spent(v), 4),
             "share": round(spent(v) / total, 4) if total else 0,
             "input": round(v[1], 4), "output": round(v[2], 4), "cache_read": round(v[3], 4),
             "cache_write": round(v[4], 4), "mean_context": v[5] // v[0],
             "tool_calls_per_request": round(v[6] / v[0], 2),
             "thinking_share": round(v[7] / v[8], 4) if v[8] else None}
            for k, v in sorted(stats.cost.items(), key=lambda kv: (-spent(kv[1]), kv[0]))]
    main = stats.cost.get("main")
    main_total = spent(main) if main else 0
    buckets = [{"bucket": label, "requests": stats.main_context[label][0], "cost": round(stats.main_context[label][1], 4),
                "share": round(stats.main_context[label][1] / main_total, 4) if main_total else 0}
               for _limit, label in CONTEXT_BUCKETS]
    rebuilds = [{"who": who, "gap": gap, "events": v[0], "mean_tokens": v[1] // v[0], "cost": round(v[2], 4)}
                for (who, gap), v in sorted(stats.rebuilds.items(), key=lambda kv: -kv[1][2])]
    return {"total": round(total, 4), "unpriced": dict(stats.unpriced), "by_agent": rows,
            "main_context": {"mean": main[5] // main[0] if main else 0, "buckets": buckets},
            "cache_rebuilds": rebuilds, "review_runs": review_runs(stats)}


def family_of(model):
    low = str(model).lower()
    return next((f for f in MODEL_FAMILIES if f in low), None)


def defined_model(repo, kind):
    """The `model:` of .claude/agents/<kind>.md, or None (no definition, no model line)."""
    try:
        text = (Path(repo) / ".claude" / "agents" / (kind + ".md")).read_text(encoding="utf-8-sig")
    except (OSError, ValueError):
        return None
    fm = re.match(r"---\s*\n(.*?)\n---", text, re.S)
    m = re.search(r"^model:\s*[\"']?([^\s\"'#]+)", fm.group(1), re.M) if fm else None
    return m.group(1) if m else None


def model_overrides(stats, repo):
    """Agent types whose definition sets a model whose messages ran on another model family."""
    found = []
    for (kind, model), v in sorted(stats.by_agent_model.items()):
        want = defined_model(repo, kind) if re.fullmatch(r"[\w.-]+", kind) else None
        if not want or want == "inherit":
            continue
        fam, seen = family_of(want), family_of(model)
        if fam and seen and fam != seen:
            found.append({"agent": kind, "defined": want, "observed": model, "messages": v[0]})
    return found


def banner_rows(stats):
    """Per banner: sessions that printed it, invocations of the skill it names (the Skill tool plus a
    typed slash command), and sessions that ran the skill's main command in a shell call."""
    return [{"banner": banner, "skill": skill,
             "sessions_printing": len(stats.banner_sessions.get(banner, ())),
             "invocations": stats.skills.get(skill, 0) + stats.slash_commands.get(skill, 0),
             "direct_sessions": len(stats.direct_sessions.get(banner, ()))}
            for banner, skill, _rx in BANNERS]


def version_key(v):
    return [int(x) for x in re.findall(r"\d+", v)] or [0]


def version_rows(stats):
    """Each Claude Code version seen in the window with its session count, oldest first."""
    return [{"version": v, "sessions": len(s)}
            for v, s in sorted(stats.versions.items(), key=lambda kv: (version_key(kv[0]), kv[0]))]


def rule_rows(stats):
    """Rule files loaded: main sessions that listed the file, and subagent transcripts that did."""
    return [{"rule": name, "sessions": len(loaded[0]), "subagents": len(loaded[1])}
            for name, loaded in sorted(stats.rules_loaded.items(),
                                       key=lambda kv: (-len(kv[1][0]) - len(kv[1][1]), kv[0]))]


def build_signals(stats, args, since, today, main, sub):
    n = args.top
    tool_rows = top_groups(stats.tool_errors, n, ("signature", "tool"))
    for r in tool_rows:
        r["recurring"] = r["sessions"] >= RECURRING_SESSIONS
    fired = {k[0] for k in stats.denies} | {k[0] for k in stats.blocks}
    gates = settings_hook_scripts(args.repo)
    ask_only = [name for name, filename in gates if name not in fired and is_ask_only(args.repo, filename)]
    used = set(stats.skills) | set(stats.slash_commands)
    runaways = sorted(stats.runaways, key=lambda r: -r["length"])[:n]
    return {
        "window": {"since": since.isoformat(), "newest": stats.newest_date, "today": today.isoformat(),
                   "excluded_sessions": list(args.exclude_session)},
        "sessions": {"main_files": len(main), "subagent_files": len(sub), "records": stats.records,
                     "parse_failures": stats.parse_failures, "compactions": stats.compactions},
        "versions": version_rows(stats),
        "banners": banner_rows(stats),
        "rules_loaded": rule_rows(stats),
        "hook_denies": top_groups(stats.denies, n, ("hook", "event", "tool")),
        "hook_blocks": top_groups(stats.blocks, n, ("hook", "event")),
        "hook_broken": top_groups(stats.broken, n, ("script", "stderr_signature")),
        "hook_cancelled": [{"hook": k, "count": g.count, "timed_out": stats.cancel_timeouts[k],
                            "sessions": len(g.sessions), "first_seen": g.first, "last_seen": g.last,
                            "example": g.example}
                           for k, g in sorted(stats.cancelled.items(), key=lambda kv: -kv[1].count)[:n]],
        "no_fires": [name for name, _f in gates if name not in fired and name not in ask_only],
        "ask_only_gates": ask_only,
        "classifier_denials": dict(stats.classifier),
        "user_rejections": dict(stats.rejections),
        "tool_errors": tool_rows,
        "runaways": runaways,
        "corrections": {"count": stats.corr_count, "sessions": len(stats.corr_sessions),
                        "excerpts": [{"session": s, "timestamp": t, "text": x}
                                     for s, t, x in stats.corr_excerpts[:n]]},
        "usage": {"skills": dict(stats.skills), "slash_commands": dict(stats.slash_commands),
                  "subagent_types": dict(stats.subagent_types), "models": dict(stats.models)},
        "unused_skills": unused_skills(args.repo, used),
        "memory": memory_health(args.memory, today),
        "output_size": output_size(stats, n),
        "tokens": dict(token_summary(stats, 5), **token_agents(stats, n, args.repo)),
        "cost": cost_summary(stats),
    }


def cell(v):
    return str(v).replace("|", "\\|").replace("\n", " ")


def table(headers, rows):
    if not rows:
        return ["_none_", ""]
    out = ["| " + " | ".join(headers) + " |", "|" + "---|" * len(headers)]
    out += ["| " + " | ".join(cell(c) for c in row) + " |" for row in rows]
    return out + [""]


def render(sig):
    w, s = sig["window"], sig["sessions"]
    L = ["# Harness retro evidence", "",
         "Window %s to %s (generated %s). Evidence only, no verdicts." % (
             w["since"], w["newest"] or "?", w["today"]), "",
         "## 1. Sessions", ""]
    L += table(["Measure", "Value"], [
        ["Main transcripts", s["main_files"]], ["Subagent transcripts", s["subagent_files"]],
        ["Records parsed", s["records"]], ["Parse failures", s["parse_failures"]],
        ["Compactions", s["compactions"]],
        ["Excluded sessions", ", ".join(w["excluded_sessions"]) or "none"]])
    L += ["### Claude Code versions seen", "",
          "Compare with the `since vX` notes in `.claude/rules/harness-*.md`: a version past a fact's "
          "last check may have changed it.", ""]
    L += table(["Version", "Sessions"], [[v["version"], v["sessions"]] for v in sig["versions"]])
    L += ["## 2. Hook outcomes", "", "### Denies", ""]
    L += table(["Hook", "Event", "Tool", "Count", "Sessions", "Last seen", "Sample reason"],
               [[r["hook"], r["event"], r["tool"], r["count"], r["sessions"], r["last_seen"], r["sample"]]
                for r in sig["hook_denies"]])
    L += ["### Stop and other blocks", ""]
    L += table(["Hook", "Event", "Count", "Sessions", "Last seen", "Sample reason"],
               [[r["hook"], r["event"], r["count"], r["sessions"], r["last_seen"], r["sample"]]
                for r in sig["hook_blocks"]])
    L += ["### Broken hook runs", ""]
    L += table(["Script", "Stderr signature", "Count", "Sessions", "First seen", "Last seen"],
               [[r["script"], r["stderr_signature"], r["count"], r["sessions"], r["first_seen"], r["last_seen"]]
                for r in sig["hook_broken"]])
    L += ["### Cancelled hooks", "", "A timed-out gate was killed and failed open.", ""]
    L += table(["Hook", "Count", "Timed out", "Sessions", "First seen", "Last seen"],
               [[r["hook"], r["count"], r["timed_out"], r["sessions"], r["first_seen"], r["last_seen"]]
                for r in sig["hook_cancelled"]])
    L += ["### No fires in window (rare-event guard? check before removing)", ""]
    L += ["- `%s`" % h for h in sig["no_fires"]] or ["_none_"]
    L += ["", "### Ask-only gates (an approved ask leaves no trace; fires not observable)", ""]
    L += ["- `%s`" % h for h in sig["ask_only_gates"]] or ["_none_"]
    L += ["", "## 3. Permission outcomes", ""]
    tools = sorted(set(sig["classifier_denials"]) | set(sig["user_rejections"]))
    L += table(["Tool", "Classifier denials", "User rejections"],
               [[t, sig["classifier_denials"].get(t, 0), sig["user_rejections"].get(t, 0)] for t in tools])
    L += ["## 4. Tool error signatures", ""]
    L += table(["Signature", "Tool", "Count", "Sessions", "Last seen", "Example session", "Mark"],
               [[r["signature"], r["tool"], r["count"], r["sessions"], r["last_seen"], r["example"],
                 "RECURRING" if r["recurring"] else ""] for r in sig["tool_errors"]])
    L += ["## 5. Runaway loops", ""]
    L += table(["Session", "Tool", "Signature", "Run length"],
               [[r["session"], r["tool"], r["signature"], r["length"]] for r in sig["runaways"]])
    c = sig["corrections"]
    L += ["## 6. User corrections", "",
          "%d corrections across %d sessions." % (c["count"], c["sessions"]), ""]
    L += table(["Session", "Timestamp", "Excerpt"],
               [[e["session"], e["timestamp"], e["text"]] for e in c["excerpts"]])
    u = sig["usage"]
    L += ["## 7. Usage", "", "### Skill invocations (Skill tool)", ""]
    L += table(["Skill", "Count"], sorted(u["skills"].items(), key=lambda kv: -kv[1]))
    L += ["### Slash commands typed (skills and built-ins)", ""]
    L += table(["Command", "Count"], sorted(u["slash_commands"].items(), key=lambda kv: -kv[1]))
    L += ["### Subagent spawns by type", ""]
    L += table(["Type", "Count"], sorted(u["subagent_types"].items(), key=lambda kv: -kv[1]))
    L += ["### Subagent spawns by model", ""]
    L += table(["Model", "Count"], sorted(u["models"].items(), key=lambda kv: -kv[1]))
    L += ["### Skills with zero invocations in window", ""]
    L += ["- `%s`" % x for x in sig["unused_skills"]] or ["_none_"]
    L += ["", "### Banners against the skills they name", "",
          "Sessions whose SessionStart output printed the banner, invocations of the skill it names (Skill "
          "tool plus typed slash command), and sessions that ran the skill's main command in a shell call. "
          "No verdict.", ""]
    L += table(["Banner", "Skill", "Sessions printing", "Invocations", "Sessions running the command"],
               [[b["banner"], b["skill"], b["sessions_printing"], b["invocations"], b["direct_sessions"]]
                for b in sig["banners"]])
    L += ["### Rule files loaded", "",
          "From the `instructions` and `nested_memory` attachments: main sessions that listed the file, and "
          "subagent transcripts that did. A path rule appears only after a matching read.", ""]
    L += table(["Rule", "Sessions", "Subagent transcripts"],
               [[r["rule"], r["sessions"], r["subagents"]] for r in sig["rules_loaded"]])
    m = sig["memory"]
    L += ["", "## 8. Memory health", "",
          "MEMORY.md: %s bytes, %s lines (numbers only, no pass or fail)." % (
              m["memory_md_bytes"], m["memory_md_lines"]), "",
          "### Feature cards older than %d days" % STALE_CARD_DAYS, ""]
    L += table(["Card", "Age (days)"], [[x["name"], x["age_days"]] for x in m["stale_cards"]])
    o = sig["output_size"]
    L += ["## 9. Tool output size", "",
          "%d characters of tool results (errors included) in the window. Shares are of all results; "
          "shell share is of Bash and PowerShell results." % o["total_chars"], "", "### By tool", ""]
    L += table(["Tool", "Results", "Chars", "Share", "Mean"],
               [[r["tool"], r["results"], r["chars"], "%.1f%%" % (100 * r["share"]), r["mean"]]
                for r in o["tools"]])
    L += ["### By shell command family", ""]
    L += table(["Family", "Results", "Chars", "Share of all", "Share of shell", "Mean", "Max"],
               [[r["family"], r["results"], r["chars"], "%.1f%%" % (100 * r["share"]),
                 "%.1f%%" % (100 * r["shell_share"]), r["mean"], r["max"]] for r in o["families"]])
    L += render_growth(o.get("growth") or {}, "family", "Growth since the previous run", "of all output")
    L += ["### Top files read", "",
          "Paths inside a repository are folded across worktrees. Whole reads gave no offset or limit.", ""]
    L += table(["File", "Reads", "Whole", "Chars", "Share of Read", "Mean", "Max"],
               [[r["file"], r["results"], r["whole"], r["chars"], "%.1f%%" % (100 * r["read_share"]),
                 r["mean"], r["max"]] for r in o.get("files", [])])
    L += render_growth(o.get("file_growth") or {}, "file", "File growth since the previous run", "of Read output")
    t = sig["tokens"]
    L += ["## 10. Tokens and cache", ""]
    if not t["requests"]:
        L += ["no usage data in the window.", ""]
    else:
        L += ["%d API requests (deduplicated by request id). Input %d, output %d, cache "
              "read %d, cache creation %d. Cache read is %.1f%% of input. %d cache breaks (one request "
              "writing more than %d tokens to the cache)." % (
                  t["requests"], t["input"], t["output"], t["cache_read"], t["cache_creation"],
                  100 * t["cache_read_share"], t["cache_breaks"], CACHE_BREAK_TOKENS), ""]
        if t["output_partial"]:
            L += ["Output is a floor: %d requests (%.0f%%) were written mid-stream with no final count." % (
                t["output_partial"], 100 * t["output_partial"] / t["requests"]), ""]
        L += ["### Top skills by total tokens (main-session tokens after the skill was invoked)", ""]
        L += table(["Skill", "Output", "Cache write", "Tokens"],
                   [[r["name"], r["output"], r["cache_creation"], r["total"]] for r in t["skills"]])
        L += ["### Top subagent types by total tokens", ""]
        L += table(["Type", "Output", "Cache write", "Tokens"],
                   [[r["name"], r["output"], r["cache_creation"], r["total"]] for r in t["subagent_types"]])
        L += ["### By agent type and model", "",
              "Requests counted once by request id, as above; models `<synthetic>` and all-zero requests are "
              "skipped. No prices are applied. Share is of cache creation plus output.", ""]
        L += table(["Agent and model", "Requests", "Input", "Output", "Cache read", "Cache creation", "Share"],
                   [[r["agent"], r["messages"], r["input"], r["output"], r["cache_read"], r["cache_creation"],
                     "%.1f%%" % (100 * r["share"])] for r in t["by_agent_model"]])
        L += ["### Model overrides", "",
              "Agent types whose definition sets `model:` but whose requests ran on another model family.", ""]
        L += table(["Agent", "Defined", "Observed", "Requests"],
                   [[r["agent"], r["defined"], r["observed"], r["messages"]] for r in t["overrides"]])
        L += ["### Costliest transcripts", "", "By total tokens (all four counts).", ""]
        L += table(["Transcript", "Tokens"], [[r["transcript"], r["tokens"]] for r in t["transcripts"]])
        L += render_growth(t.get("growth") or {}, "agent", "Token growth since the previous run",
                           "of cache creation plus output")
    L += render_cost(sig["cost"])
    return "\n".join(L) + "\n"


def render_cost(c):
    L = ["## 11. Cost at list prices", ""]
    if not c["total"] and not c["unpriced"]:
        return L + ["no priced requests in the window.", ""]
    L += ["A proxy for relative size, not a bill: a subscription weighs tokens by rates it does not publish. "
          "Total $%.2f." % c["total"], ""]
    if c["unpriced"]:
        L += ["No list price for these models, so they count as $0: %s." % ", ".join(
            "%s (%d requests)" % kv for kv in sorted(c["unpriced"].items())), ""]
    L += ["### By agent", "",
          "Mean context is the tokens each request sends, so one request re-reads about that many. Thinking "
          "share covers only the requests that report it.", ""]
    L += table(["Agent", "Requests", "USD", "Share", "Output", "Cache read", "Cache write", "Mean context",
                "Tool calls per request", "Thinking share"],
               [[r["agent"], r["requests"], "%.2f" % r["cost"], "%.1f%%" % (100 * r["share"]), "%.2f" % r["output"],
                 "%.2f" % r["cache_read"], "%.2f" % r["cache_write"], r["mean_context"], r["tool_calls_per_request"],
                 "n/a" if r["thinking_share"] is None else "%.0f%%" % (100 * r["thinking_share"])]
                for r in c["by_agent"]])
    m = c["main_context"]
    L += ["### Main session by context size", "", "Mean context %d tokens." % m["mean"], ""]
    L += table(["Context", "Requests", "USD", "Share of main"],
               [[b["bucket"], b["requests"], "%.2f" % b["cost"], "%.1f%%" % (100 * b["share"])] for b in m["buckets"]])
    L += ["### Cache rebuilds", "",
          "One request writing more than %d tokens to the cache, by the idle gap before it. A gap past the cache "
          "lifetime (1 hour for main sessions, 5 minutes for subagents) writes the whole context again." %
          CACHE_BREAK_TOKENS, ""]
    L += table(["Who", "Idle gap", "Events", "Mean tokens", "USD"],
               [[r["who"], r["gap"], r["events"], r["mean_tokens"], "%.2f" % r["cost"]] for r in c["cache_rebuilds"]])
    r = c["review_runs"]
    L += ["### Review runs", "",
          "`%s` transcripts of one session, each started within %d minutes of the one before, are one run." % (
              REVIEW_AGENT, REVIEW_RUN_GAP_SECONDS // 60), ""]
    L += table(["Measure", "p50", "p90", "Max"],
               [["Lenses per run (%d runs, %d lenses)" % (r["runs"], r["lenses"]), r["lenses_per_run_p50"],
                 r["lenses_per_run_p90"], r["lenses_per_run_max"]],
                ["USD per run", r["run_cost_p50"], r["run_cost_p90"], r["run_cost_max"]],
                ["USD per lens", r["lens_cost_p50"], r["lens_cost_p90"], ""],
                ["Requests per lens", r["lens_requests_p50"], r["lens_requests_p90"], ""],
                ["Peak context per lens", r["lens_peak_context_p50"], r["lens_peak_context_p90"], ""]])
    return L


def render_growth(g, label, heading, unit):
    """One growth table (shell families or files), or the explicit UNCHECKED line."""
    L = ["### " + heading, ""]
    if not g.get("baseline"):
        return L + ["No earlier signals.json with this table found beside this run (looked in %s): growth "
                    "UNCHECKED, not absent." % g.get("looked_in", "?"), ""]
    L += ["Against the run of %s (%s); shares are %s. Each row is a proposal candidate for the retro." % (
        g.get("baseline_today"), g["baseline"], unit), ""]
    return L + table([label.capitalize(), "Then", "Now", "Grew by"],
                     [[r[label], "%.1f%%" % (100 * r["then"]), "%.1f%%" % (100 * r["now"]),
                       "%.1f points" % (100 * r["delta"])] for r in g["rows"]])


def parse_args(argv):
    repo_default = TOOLS_DIR.parent
    home = Path.home() / ".claude" / "projects" / DEFAULT_SLUG
    p = argparse.ArgumentParser(description="Mine Claude Code transcripts for harness evidence.")
    p.add_argument("--transcripts", type=Path, default=home)
    p.add_argument("--since", help="YYYY-MM-DD (default: newest harness-retro file, else 30 days ago)")
    p.add_argument("--repo", type=Path, default=repo_default)
    p.add_argument("--memory", type=Path, default=home / "memory")
    p.add_argument("--out", type=Path, default=None)
    p.add_argument("--top", type=int, default=20)
    p.add_argument("--exclude-session", action="append", default=[], metavar="ID",
                   help="leave this session's transcripts out, for work that would distort a comparison (repeatable)")
    return p.parse_args(argv)


def main(argv=None):
    try:
        args = parse_args(argv)
    except SystemExit as e:
        return 2 if e.code else 0
    today = date.today()
    if args.top < 1:
        print("error: --top must be at least 1", file=sys.stderr)
        return 2
    try:
        since = date.fromisoformat(args.since) if args.since else default_since(args.repo, today)
    except ValueError:
        print("error: --since must be YYYY-MM-DD", file=sys.stderr)
        return 2
    if not args.transcripts.is_dir():
        print("error: transcripts directory not found: %s" % args.transcripts, file=sys.stderr)
        return 2
    out = (args.out or Path(tempfile.gettempdir()) / "taom-harness-retro" / today.isoformat()).resolve()
    repo = args.repo.resolve()
    if out == repo or repo in out.parents:
        print("error: --out %s is inside --repo %s; the report must stay outside the repo" % (out, repo),
              file=sys.stderr)
        return 2

    matched = [sid for sid in args.exclude_session
               if (args.transcripts / (sid + ".jsonl")).is_file() or (args.transcripts / sid).is_dir()]
    for sid in args.exclude_session:
        if sid not in matched:
            print("warning: --exclude-session %s matches no transcript in %s" % (sid, args.transcripts),
                  file=sys.stderr)
    args.exclude_session = matched   # the report names only what was left out

    stats = Stats()
    main_files, sub_files = discover(args.transcripts, datetime.combine(since, datetime.min.time()),
                                     args.exclude_session)
    for path in main_files + sub_files:
        try:
            scan_file(stats, path, since.isoformat(), agent_type(path) if path in sub_files else None)
        except OSError as e:
            print("warning: cannot read %s: %s" % (path, e), file=sys.stderr)
    if stats.records == 0:
        print("UNCHECKED: no transcript records parsed in %s since %s" % (args.transcripts, since.isoformat()))
        return 3

    sig = build_signals(stats, args, since, today, main_files, sub_files)
    sig["output_size"]["growth"] = output_growth(sig["output_size"]["families"], out)
    sig["output_size"]["file_growth"] = output_growth(sig["output_size"]["files"], out, "file", "files", "read_share")
    sig["tokens"]["growth"] = output_growth(sig["tokens"]["by_agent_model"], out, "agent", "by_agent_model",
                                            "share", "tokens", TOKEN_GROWTH_POINTS)
    out.mkdir(parents=True, exist_ok=True)
    (out / "report.md").write_text(render(sig), encoding="utf-8")
    (out / "signals.json").write_text(json.dumps(sig, indent=2), encoding="utf-8")

    s = sig["sessions"]
    g = sig["output_size"]["growth"]
    print("report:  %s" % (out / "report.md"))
    print("signals: %s" % (out / "signals.json"))
    print("sessions: %d main, %d subagent transcripts, %d records, %d parse failures, %d compactions" % (
        s["main_files"], s["subagent_files"], s["records"], s["parse_failures"], s["compactions"]))
    print("hooks: %d deny rows, %d block rows, %d broken-run rows, %d cancelled, %d with no fires" % (
        len(sig["hook_denies"]), len(sig["hook_blocks"]), len(sig["hook_broken"]),
        len(sig["hook_cancelled"]), len(sig["no_fires"])))
    print("permissions: %d classifier denials, %d user rejections" % (
        sum(sig["classifier_denials"].values()), sum(sig["user_rejections"].values())))
    print("tool errors: %d signature rows, %d RECURRING" % (
        len(sig["tool_errors"]), sum(1 for r in sig["tool_errors"] if r["recurring"])))
    print("runaways: %d" % len(sig["runaways"]))
    print("corrections: %d across %d sessions" % (sig["corrections"]["count"], sig["corrections"]["sessions"]))
    print("usage: %d skills used, %d subagent spawns, %d skills unused" % (
        sum(1 for _ in sig["usage"]["skills"]), sum(sig["usage"]["subagent_types"].values()),
        len(sig["unused_skills"])))
    tk = sig["tokens"]
    print("tokens: %s" % ("no usage data" if not tk["requests"] else "%d requests, %.1f%% cache read, %d cache breaks" % (
        tk["requests"], 100 * tk["cache_read_share"], tk["cache_breaks"])))
    print("cost: $%.2f at list prices, %d review runs, %d models unpriced" % (
        sig["cost"]["total"], sig["cost"]["review_runs"]["runs"], len(sig["cost"]["unpriced"])))
    print("memory: %s bytes, %s lines, %d stale cards" % (
        sig["memory"]["memory_md_bytes"], sig["memory"]["memory_md_lines"], len(sig["memory"]["stale_cards"])))
    if g["baseline"]:
        print("output growth: %d shell family(ies) grew %d+ points since %s%s" % (
            len(g["rows"]), round(100 * GROWTH_POINTS), g["baseline_today"],
            "" if not g["rows"] else ": " + ", ".join(r["family"] for r in g["rows"])))
    else:
        print("output growth: UNCHECKED, no earlier run beside %s" % out)
    fg = sig["output_size"]["file_growth"]
    if fg["baseline"]:
        print("file growth: %d file(s) grew %d+ points of Read output since %s%s" % (
            len(fg["rows"]), round(100 * GROWTH_POINTS), fg["baseline_today"],
            "" if not fg["rows"] else ": " + ", ".join(r["file"] for r in fg["rows"])))
    else:
        print("file growth: UNCHECKED, no earlier run with a files table beside %s" % out)
    tg = sig["tokens"]["growth"]
    if tg["baseline"]:
        print("token growth: %d agent(s) grew %d+ points since %s%s" % (
            len(tg["rows"]), round(100 * TOKEN_GROWTH_POINTS), tg["baseline_today"],
            "" if not tg["rows"] else ": " + ", ".join(r["agent"] for r in tg["rows"])))
    else:
        print("token growth: UNCHECKED, no earlier run with tokens.by_agent_model beside %s" % out)
    print("model overrides: %d" % len(sig["tokens"]["overrides"]))
    return 0


if __name__ == "__main__":
    sys.exit(main())
