#!/usr/bin/env python
"""graphify_taom.py: the one way TAOM builds and queries its graphify code graph (#677).

graphify (PyPI `graphifyy`) builds a C# and Python code graph that answers what Serena
does not: the reverse blast radius of changing a type, the architectural hubs, and a
one-screen neighbourhood of a class. Every graphify WRITE goes through this wrapper, and
`.claude/hooks/check-graphify-usage.sh` denies the raw verbs, because each one has a
silent trap:

  * `extract` without `--out` drops a graphify-out/ tree into the scanned repo, and even
    WITH `--out` an incremental re-run writes graphify-out/cache/stat-index.json there
    (reproduced 2026-09-26: graphify's cache.py fixes the stat-index location from the
    first caller, and one caller passes no cache root). The same file was committed once,
    in 8318e346. An absolute GRAPHIFY_OUT pins it, so every call here sets one. The
    semantic flags cost 18.2M input tokens for the same hub ranking `--code-only` gives.
  * `update` keeps only file-backed nodes and discards about 5,100 external-type nodes
    (TextObject, ExplainedNumber) without asking.
  * `cluster-only` and `label` name communities with an LLM unless told not to.
  * `install` / `hook install` rewrite CLAUDE.md, AGENTS.md, settings.json or git hooks.

Usage:
    python tools/graphify_taom.py status [--brief]           # exit 0 fresh, 1 stale, 2 missing
    python tools/graphify_taom.py refresh [--if-stale] [--force] [--detach]
    python tools/graphify_taom.py affected "IModLogger" --depth 2 [--no-interfaces]
    python tools/graphify_taom.py tests "TaomPartySizeModel" [--depth 2]   # dotnet test filter
    python tools/graphify_taom.py explain "TaomPartySizeModel"
    python tools/graphify_taom.py god-nodes --top 15
    python tools/graphify_taom.py path "A" "B"
    python tools/graphify_taom.py query "question"
    python tools/graphify_taom.py report                     # print GRAPH_REPORT.md's path
    python tools/graphify_taom.py gate < payload.json        # PreToolUse judge (the hook)

The graph lives outside the repo, at E:\\graphify\\TAOM (TAOM_GRAPHIFY_OUT overrides), and
carries a stamp (HEAD, build start time, deletions at build time) that `status` compares
with the working tree. It reads no XML or XSLT: game-data questions go to the
taom-moduledata MCP or tools/validate_moduledata.py. Exit 3 means the environment is missing
something (graphify, git, the E: drive): report it, do not install anything.

Design and the decision record: docs/features/graphify-code-graph.md, ADR-012.
"""
import argparse
import json
import os
import re
import shutil
import subprocess
import sys
import threading
import time
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
DEFAULT_OUT = Path(r"E:\graphify\TAOM")
STAMP_NAME = "taom-stamp.json"
LOCK_NAME = ".taom-refresh.lock"
LOCK_STALE_SECONDS = 1800
EXTRACT_TIMEOUT = 1500
CLUSTER_TIMEOUT = 600

QUERY_VERBS = ("explain", "affected", "path", "god-nodes", "query")
# Extensions graphify parses as code. JSON is left out on purpose: settings files churn and
# carry almost no graph (31 of 42,770 nodes on 2026-09-26).
CODE_EXTS = frozenset({".cs", ".py", ".ps1", ".psm1", ".sh", ".xaml", ".cpp", ".h",
                       ".js", ".ts", ".csproj", ".sln"})

EXIT_STALE, EXIT_MISSING, EXIT_ENV, EXIT_CONTAMINATED, EXIT_LOCKED = 1, 2, 3, 4, 5


def out_root(env=None, repo=None) -> Path:
    """TAOM_GRAPHIFY_OUT, else E:\\graphify\\TAOM for the main tree and
    E:\\graphify\\TAOM-wt-<name> for a linked worktree, so a builder working in
    `isolation: "worktree"` never overwrites the shared graph with its own code."""
    env = os.environ if env is None else env
    value = env.get("TAOM_GRAPHIFY_OUT")
    if value:
        return Path(value)
    repo = REPO if repo is None else Path(repo)
    try:
        git_dir = Path(_git(repo, "rev-parse", "--absolute-git-dir").strip())
        common = Path(os.path.abspath(repo / _git(repo, "rev-parse", "--git-common-dir").strip()))
    except (OSError, subprocess.CalledProcessError):
        return DEFAULT_OUT
    if os.path.normcase(git_dir) != os.path.normcase(common):
        return DEFAULT_OUT.parent / f"TAOM-wt-{repo.name}"
    return DEFAULT_OUT


def validate_out_root(out: Path, repo: Path) -> None:
    """Refuse an output root inside the repo: that is the graphify-out/ leak."""
    o, r = Path(os.path.abspath(out)), Path(os.path.abspath(repo))
    if o == r or r in o.parents:
        raise ValueError(f"graphify output root {out} is inside the repo {repo}; "
                         "it must live outside it (TAOM_GRAPHIFY_OUT)")


def graph_dir(out: Path) -> Path:
    return out / "graphify-out"


def graphify_env(out: Path, base=None) -> dict:
    """Environment for every graphify subprocess. graphify joins `--out` with GRAPHIFY_OUT,
    and an absolute GRAPHIFY_OUT replaces the base, so graph.json lands where `--out`
    alone puts it while the stat-index cache can no longer fall back to the scanned tree."""
    env = dict(os.environ if base is None else base)
    env["GRAPHIFY_OUT"] = os.path.abspath(graph_dir(out))
    env["PYTHONIOENCODING"] = "utf-8"
    env["PYTHONUTF8"] = "1"
    return env


def extract_command(exe: str, repo: Path, out: Path, force: bool = False) -> list:
    cmd = [exe, "extract", str(repo), "--code-only", "--out", str(out)]
    if force:
        cmd.append("--force")
    return cmd


def cluster_command(exe: str, out: Path) -> list:
    return [exe, "cluster-only", str(out), "--no-label", "--no-viz"]


def query_command(exe: str, verb: str, args: list, graph: Path) -> list:
    if verb not in QUERY_VERBS:
        raise ValueError(f"'{verb}' is not a read-only query verb ({', '.join(QUERY_VERBS)})")
    if "--graph" in args:
        raise ValueError("the wrapper pins --graph to the TAOM graph; drop the argument")
    return [exe, verb, *args, "--graph", str(graph)]


# --- git helpers -------------------------------------------------------------------------

def _git(repo: Path, *args: str) -> str:
    return subprocess.run(["git", "-c", "core.quotePath=false", *args], cwd=repo, check=True,
                          capture_output=True, text=True, encoding="utf-8",
                          errors="replace").stdout


def _git_paths(repo: Path, subcommand: str, *args: str) -> list:
    # -z right after the subcommand: after a `--` git would read it as a pathspec.
    return [p for p in _git(repo, subcommand, "-z", *args).split("\0") if p]


def _is_code(rel: str) -> bool:
    return Path(rel).suffix.lower() in CODE_EXTS


# --- stamp and staleness -----------------------------------------------------------------

def make_stamp(repo: Path, built_at: float) -> dict:
    """What the graph was built from. `built_at` is the extract's START, so a file saved
    while graphify ran counts as newer than the graph."""
    deleted = [p for p in _git_paths(repo, "diff", "--name-only", "--no-renames",
                                     "--diff-filter=D", "HEAD", "--") if _is_code(p)]
    return {
        "head": _git(repo, "rev-parse", "HEAD").strip(),
        "branch": _git(repo, "branch", "--show-current").strip(),
        "built_at": built_at,
        "built": time.strftime("%Y-%m-%d %H:%M", time.localtime(built_at)),
        "deleted_at_build": sorted(deleted),
    }


def write_stamp(gdir: Path, stamp: dict) -> None:
    gdir.mkdir(parents=True, exist_ok=True)
    (gdir / STAMP_NAME).write_text(json.dumps(stamp, indent=2) + "\n", encoding="utf-8")


def read_stamp(gdir: Path):
    try:
        return json.loads((gdir / STAMP_NAME).read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return None


def staleness(repo: Path, gdir: Path):
    """("fresh" | "stale" | "missing", detail). Stale means a code file changed after the
    build started, or a code file present at build time has since been deleted."""
    if not (gdir / "graph.json").is_file():
        return "missing", [f"no graph at {gdir / 'graph.json'}"]
    stamp = read_stamp(gdir)
    if not stamp or not stamp.get("head"):
        return "missing", [f"no {STAMP_NAME} beside the graph (built outside the wrapper?)"]
    try:
        changed = _git_paths(repo, "diff", "--name-only", "--no-renames", stamp["head"], "--")
    except subprocess.CalledProcessError:
        return "stale", [f"stamp commit {stamp['head'][:8]} is not in this repo"]
    untracked = _git_paths(repo, "ls-files", "--others", "--exclude-standard")
    built_at = float(stamp.get("built_at", 0))
    deleted_at_build = set(stamp.get("deleted_at_build", []))
    stale = []
    for rel in sorted(set(changed) | set(untracked)):
        if not _is_code(rel):
            continue
        path = repo / rel
        if not path.exists():
            if rel not in deleted_at_build:
                stale.append(rel)
        elif path.stat().st_mtime > built_at:
            stale.append(rel)
    return ("stale", stale) if stale else ("fresh", [])


# --- refresh lock and repo check ---------------------------------------------------------

def acquire_lock(out: Path, _retry: bool = True) -> bool:
    """One refresh at a time across sessions. A lock older than LOCK_STALE_SECONDS is an
    abandoned run (a killed session) and is taken over."""
    out.mkdir(parents=True, exist_ok=True)
    lock = out / LOCK_NAME
    try:
        fd = os.open(lock, os.O_CREAT | os.O_EXCL | os.O_WRONLY)
    except FileExistsError:
        try:
            age = time.time() - lock.stat().st_mtime
        except OSError:
            return False
        if age < LOCK_STALE_SECONDS or not _retry:
            return False
        try:
            lock.unlink()
        except OSError:
            return False
        return acquire_lock(out, _retry=False)
    with os.fdopen(fd, "w") as fh:
        fh.write(f"{os.getpid()} {time.time():.0f}\n")
    return True


def release_lock(out: Path) -> None:
    try:
        (out / LOCK_NAME).unlink()
    except OSError:
        pass


def repo_contamination(repo: Path) -> list:
    """graphify output inside the repo, ignored or not. .gitignore hides graphify-out/, so
    only an --ignored listing can see it, and `git status --ignored` is no good: it collapses
    a directory holding nothing but ignored files (Main/graphify-out/ reads as Main/)."""
    entries = (_git_paths(repo, "ls-files", "--others", "--ignored", "--exclude-standard",
                          "--directory")
               + _git_paths(repo, "ls-files", "--others", "--exclude-standard", "--directory"))
    hits = set()
    for path in entries:
        parts = path.rstrip("/").split("/")
        if "graphify-out" in parts or any(p.startswith(".graphify") for p in parts):
            hits.add(path)
    return sorted(hits)


# --- the PreToolUse gate -----------------------------------------------------------------

ALLOWED_VERBS = frozenset(QUERY_VERBS) | {"diagnose", "benchmark", "help", "version",
                                          "--help", "-h", "--version", "-v", "-V"}
_PREFIX_WORDS = frozenset({"&", ".", "call", "exec", "command", "nohup", "time", "env",
                           "sudo", "start", "uvx", "("})
_SHELLS = {"bash": "Bash", "sh": "Bash", "zsh": "Bash", "pwsh": "PowerShell",
           "powershell": "PowerShell", "cmd": "PowerShell"}
_SHELL_FLAGS = frozenset({"-c", "-lc", "-command", "/c", "/k", "-noprofile", "-nop",
                          "-noninteractive", "-l", "-e"})
_ASSIGNMENT = re.compile(r"^[A-Za-z_][A-Za-z0-9_]*=")
_INSTALL_WORDS = frozenset({"install", "uninstall", "hook"})

_HINT = ("Build or refresh with `python tools/graphify_taom.py refresh` (it pins --code-only, "
         "the out root outside the repo, --no-label and --no-viz, and checks the repo stays "
         "clean); query with `python tools/graphify_taom.py explain|affected|path|god-nodes|query "
         "...`. Do not retry the raw command in another spelling. (#677, "
         "docs/features/graphify-code-graph.md)")
_REASONS = {
    "update": "`graphify update` keeps only file-backed nodes and silently discards about 5,100 "
              "external-type nodes (TextObject, ExplainedNumber).",
    "extract": "a raw `graphify extract` depends on remembering --out (without it graphify-out/ "
               "lands in the scanned repo, as in 8318e346) and on leaving out the semantic "
               "flags (the full semantic pass cost 18.2M input tokens).",
    "cluster-only": "`graphify cluster-only` names communities with an LLM unless --no-label "
                    "is passed.",
    "label": "`graphify label` is an LLM dispatch, which needs Mike's explicit word.",
    "install": "graphify's install verbs write its own sections into CLAUDE.md, AGENTS.md, "
               ".claude/settings.json or git hooks.",
    "mcp": "the graphify MCP server is a standing context cost; TAOM uses the CLI.",
}


def _segments(command: str, tool: str) -> list:
    """Split at ; & | and newlines outside quotes, with the shell's own escape character."""
    esc = "`" if tool == "PowerShell" else "\\"
    command = command.replace("\r", "")
    out, cur, quote, i = [], [], "", 0
    while i < len(command):
        c = command[i]
        if c == esc and quote != "'":
            nxt = command[i + 1:i + 2]
            if nxt == "\n":
                cur.append(" ")
            else:
                cur.append(c + nxt)
            i += 2
            continue
        if quote:
            if c == quote:
                quote = ""
            cur.append(c)
        elif c in "\"'":
            quote = c
            cur.append(c)
        elif c in ";&|\n":
            out.append("".join(cur))
            cur = []
        else:
            cur.append(c)
        i += 1
    out.append("".join(cur))
    return [s for s in (seg.strip() for seg in out) if s]


def _tokens(segment: str, tool: str) -> list:
    """Words of one segment, quotes removed. Bash keeps a backslash inside double quotes
    unless it escapes \\ " $ or a backtick, so a quoted Windows path survives."""
    esc = "`" if tool == "PowerShell" else "\\"
    tokens, cur, quote, have, i = [], [], "", False, 0
    while i < len(segment):
        c = segment[i]
        if c == esc and quote != "'":
            nxt = segment[i + 1:i + 2]
            if quote == '"' and esc == "\\" and nxt not in ('"', "\\", "$", "`"):
                cur.append(c)
            else:
                cur.append(nxt)
                i += 1
            have = True
        elif quote:
            if c == quote:
                quote = ""
            else:
                cur.append(c)
        elif c in "\"'":
            quote, have = c, True
        elif c.isspace():
            if have or cur:
                tokens.append("".join(cur))
            cur, have = [], False
        else:
            cur.append(c)
            have = True
        i += 1
    if have or cur:
        tokens.append("".join(cur))
    return tokens


def _program(token: str) -> str:
    name = re.split(r"[\\/]", token)[-1].lower()
    for ext in (".exe", ".cmd", ".bat"):
        if name.endswith(ext):
            return name[:-len(ext)]
    return name


def _strip_prefixes(tokens: list) -> list:
    i = 0
    while i < len(tokens):
        t = tokens[i]
        low = t.lower()
        if _ASSIGNMENT.match(t) or low in _PREFIX_WORDS:
            i += 1
        elif low == "uv" and tokens[i + 1:i + 3] == ["tool", "run"]:
            i += 3
        elif low == "uv" and tokens[i + 1:i + 2] == ["run"]:
            i += 2
        elif low == "timeout":
            i += 1
            while i < len(tokens) and tokens[i].startswith("-"):
                i += 2 if tokens[i] in ("-k", "-s", "--kill-after", "--signal") else 1
            i += 1  # the duration
        else:
            break
    return tokens[i:]


def judge_command(command: str, tool: str = "Bash", _depth: int = 0):
    """The deny reason for a command that runs a graphify write verb, else None."""
    for segment in _segments(command, tool):
        words = _strip_prefixes(_tokens(segment, tool))
        if not words:
            continue
        prog = _program(words[0])
        if prog in _SHELLS and _depth < 2:
            rest = words[1:]
            for j, w in enumerate(rest):
                if w.lower() in ("-c", "-lc", "-command", "/c", "/k"):
                    reason = judge_command(" ".join(rest[j + 1:]), _SHELLS[prog], _depth + 1)
                    if reason:
                        return reason
                    break
                if w.lower() not in _SHELL_FLAGS:
                    break
            continue
        if prog == "graphify-mcp":
            return f"check-graphify-usage: {_REASONS['mcp']} {_HINT}"
        if prog != "graphify":
            continue
        verb = words[1].lower() if len(words) > 1 else ""
        if not verb or verb in ALLOWED_VERBS:
            continue
        nxt = words[2].lower() if len(words) > 2 else ""
        if verb in _INSTALL_WORDS or nxt in ("install", "uninstall"):
            key = "install"
        else:
            key = verb
        specific = _REASONS.get(key, f"`graphify {verb}` writes outside the pinned graph or "
                                     "into the repo.")
        return f"check-graphify-usage: {specific} {_HINT}"
    return None


def gate_decision(payload_text: str) -> dict:
    """PreToolUse output for one hook payload. Anything unreadable fails open ({})."""
    try:
        payload = json.loads(payload_text)
        command = (payload.get("tool_input") or {}).get("command")
        tool = payload.get("tool_name") or "Bash"
    except (ValueError, AttributeError):
        return {}
    if not isinstance(command, str) or not command:
        return {}
    reason = judge_command(command, tool)
    if not reason:
        return {}
    return {"hookSpecificOutput": {"hookEventName": "PreToolUse",
                                   "permissionDecision": "deny",
                                   "permissionDecisionReason": reason}}


# --- CLI ---------------------------------------------------------------------------------

def _graphify_exe():
    exe = shutil.which("graphify")
    if not exe:
        print("graphify_taom: graphify is not on PATH on this machine. It is a uv tool "
              "(`uv tool install --python 3.12 graphifyy`, see "
              "docs/features/graphify-code-graph.md). Report this to Mike; do not install it "
              "yourself.", file=sys.stderr)
    return exe


def _summary_line(state: str, detail: list, stamp) -> str:
    built = f"built {stamp.get('built', '?')} from {stamp.get('head', '')[:8]}" if stamp else ""
    if state == "fresh":
        return f"graphify code graph: current ({built})"
    if state == "missing":
        return f"graphify code graph: MISSING ({detail[0]})"
    shown = ", ".join(detail[:3]) + (f" and {len(detail) - 3} more" if len(detail) > 3 else "")
    return f"graphify code graph: STALE, {len(detail)} code file(s) changed since it was {built}: {shown}"


def cmd_status(args) -> int:
    out = out_root()
    gdir = graph_dir(out)
    try:
        state, detail = staleness(REPO, gdir)
    except (OSError, subprocess.CalledProcessError) as exc:
        print(f"graphify code graph: UNCHECKED ({exc})")
        return EXIT_ENV
    print(_summary_line(state, detail, read_stamp(gdir)))
    if state != "fresh":
        print("  refresh: python tools/graphify_taom.py refresh   (about 2.5 minutes; "
              "run it in the background)")
    if not args.brief and state == "stale" and len(detail) > 3:
        for rel in detail:
            print(f"    {rel}")
    return {"fresh": 0, "stale": EXIT_STALE, "missing": EXIT_MISSING}[state]


_PROGRESS = re.compile(r"^\s*AST extraction: \d+/\d+")
_WROTE = re.compile(r"wrote .*graph\.json: (\d+) nodes, (\d+) edges")


def _run_step(cmd: list, out: Path, timeout: int):
    try:
        proc = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8",
                              errors="replace", timeout=timeout, env=graphify_env(out))
    except subprocess.TimeoutExpired:
        return 124, f"timed out after {timeout}s"
    text = (proc.stdout or "") + (proc.stderr or "")
    return proc.returncode, text


def _tail(text: str, n: int = 12) -> str:
    lines = [l for l in text.splitlines() if l.strip() and not _PROGRESS.match(l)]
    return "\n".join(lines[-n:])


def cmd_refresh(args) -> int:
    out = out_root()
    try:
        validate_out_root(out, REPO)
    except ValueError as exc:
        print(f"graphify_taom: {exc}", file=sys.stderr)
        return EXIT_ENV
    gdir = graph_dir(out)
    if args.if_stale:
        state, detail = staleness(REPO, gdir)
        if state == "fresh":
            print(_summary_line(state, detail, read_stamp(gdir)))
            return 0
    exe = _graphify_exe()
    if not exe:
        return EXIT_ENV
    try:
        locked = acquire_lock(out)
    except OSError as exc:
        print(f"graphify_taom: cannot create {out} ({exc}). Is the E: drive present? "
              "Set TAOM_GRAPHIFY_OUT on another machine.", file=sys.stderr)
        return EXIT_ENV
    if not locked:
        print(f"graphify_taom: another refresh holds {out / LOCK_NAME}; wait for it, then run "
              "status.", file=sys.stderr)
        return EXIT_LOCKED
    try:
        started = time.time()
        print(f"graphify_taom: extracting {REPO} (code only) into {out} ...", flush=True)
        rc, text = _run_step(extract_command(exe, REPO, out, force=args.force), out,
                             EXTRACT_TIMEOUT)
        if rc != 0:
            print(_tail(text), file=sys.stderr)
            print(f"graphify_taom: extract failed (exit {rc}); the previous graph and stamp "
                  "are untouched.", file=sys.stderr)
            return rc
        wrote = _WROTE.search(text)
        print("graphify_taom: clustering (no LLM naming, no HTML) ...", flush=True)
        rc, ctext = _run_step(cluster_command(exe, out), out, CLUSTER_TIMEOUT)
        if rc != 0:
            print(_tail(ctext), file=sys.stderr)
            print(f"graphify_taom: cluster-only failed (exit {rc}).", file=sys.stderr)
            return rc
        leaked = repo_contamination(REPO)
        if leaked:
            print("graphify_taom: graphify wrote into the repo, which must never happen: "
                  + ", ".join(leaked) + ". Nothing was stamped. Look at what wrote it before "
                  "deleting anything.", file=sys.stderr)
            return EXIT_CONTAMINATED
        stamp = make_stamp(REPO, started)
        if wrote:
            stamp["nodes"], stamp["edges"] = int(wrote.group(1)), int(wrote.group(2))
        communities = re.search(r"Done - (\d+) communities", ctext)
        if communities:
            stamp["communities"] = int(communities.group(1))
        write_stamp(gdir, stamp)
        print(f"graphify_taom: refreshed in {time.time() - started:.0f}s: "
              f"{stamp.get('nodes', '?')} nodes, {stamp.get('edges', '?')} edges, "
              f"{stamp.get('communities', '?')} communities, from {stamp['head'][:8]} on "
              f"{stamp['branch'] or '(detached)'}. Report: {gdir / 'GRAPH_REPORT.md'}")
        return 0
    finally:
        release_lock(out)


# --- graph-aware answers: supertypes, candidates, answer-scoped staleness (#995) -----------

SUPER_RELATIONS = frozenset({"implements", "inherits", "extends"})
MEMBER_CALL_RELATIONS = frozenset({"calls", "references"})
MAX_HOP_FILES = 8
NO_MATCH = "No unique node match"
MAX_CANDIDATES = 10
MAX_SUPERTYPES = 6
STALE_NAMED = 10


def _is_repo_path(source_file: str) -> bool:
    """A node defined in TAOM's own code: a relative path with a code extension. Reference nodes
    for engine types (MissionLogic, MobileParty) carry no source file."""
    if not source_file or source_file.startswith(("/", "\\", "..")) or ":" in source_file[:3]:
        return False
    return _is_code(source_file)


class GraphData:
    """The parts of graph.json the wrapper reads: every node by id, and the 1,700 or so
    implements / inherits / extends links. One load per call (about 0.7 s for 150 MB)."""

    def __init__(self, nodes: dict, super_links: list, members=None, callers=None):
        self.nodes = nodes
        self.super_links = super_links
        self.members = members or {}   # type id -> ids of its member nodes (`method` links)
        self.callers = callers or {}   # member id -> [(caller id, relation, file, location)]

    @classmethod
    def load(cls, path: Path):
        try:
            raw = json.loads(Path(path).read_text(encoding="utf-8"))
            nodes = {n["id"]: n for n in raw["nodes"]}
            links = [(l["source"], l["target"]) for l in raw["links"]
                     if l.get("relation") in SUPER_RELATIONS]
            members = {}
            for l in raw["links"]:
                if l.get("relation") == "method":
                    members.setdefault(l["source"], []).append(l["target"])
            member_ids = {m for ids in members.values() for m in ids}
            callers = {}
            for l in raw["links"]:
                if l.get("relation") in MEMBER_CALL_RELATIONS and l["target"] in member_ids:
                    callers.setdefault(l["target"], []).append(
                        (l["source"], l["relation"], l.get("source_file") or "",
                         l.get("source_location") or ""))
        except (OSError, ValueError, KeyError, TypeError):
            return None
        return cls(nodes, links, members, callers)

    def _seeds(self, query: str) -> list:
        q = query.replace("\\", "/").rstrip("/")
        if q in self.nodes:
            return [q]
        low = q.lower()
        in_file = [i for i, n in self.nodes.items()
                   if (n.get("source_file") or "").replace("\\", "/").lower() == low]
        if in_file:
            return in_file
        named = [i for i, n in self.nodes.items() if str(n.get("label", "")).lower() == low]
        defined = [i for i in named if self.nodes[i].get("source_file")]
        return defined or named

    def supertypes(self, query: str) -> list:
        """(label, defining path) of each TAOM type the query's node implements or inherits."""
        seeds = set(self._seeds(query))
        found = []
        for src, tgt in self.super_links:
            if src not in seeds or tgt in seeds:
                continue
            node = self.nodes.get(tgt)
            if not node:
                continue
            sf = (node.get("source_file") or "").replace("\\", "/")
            item = (str(node.get("label", tgt)), sf)
            if _is_repo_path(sf) and item not in found:
                found.append(item)
        return found

    def member_callers(self, query: str):
        """(type label, lines): who calls or references the members of the type the query names.
        graphify's `affected` stops at the type node, so a caller that only uses an interface's
        methods (and never names the interface) is invisible there. A type's own members calling
        each other are left out."""
        owners = [i for i in self._seeds(query) if i in self.members]
        if not owners:
            return "", []
        member_ids = {m for o in owners for m in self.members[o]}
        lines = []
        for member in sorted(member_ids):
            mlabel = str(self.nodes[member].get("label", member)).lstrip(".")
            for src, rel, sf, loc in self.callers.get(member, []):
                if src in member_ids or src in owners:
                    continue
                node = self.nodes.get(src, {})
                where = f"{sf or node.get('source_file') or '-'}:{loc or node.get('source_location') or '-'}"
                line = f"- {str(node.get('label', src)).lstrip('.')} [{rel} {mlabel}] {where}"
                if line not in lines:
                    lines.append(line)
        return str(self.nodes[owners[0]].get("label", query)), lines

    @staticmethod
    def _bare(name: str) -> str:
        return re.split(r"[\\/]", name.strip())[-1]

    def candidates(self, name: str) -> list:
        low = self._bare(name).lower()
        ranked = []
        for nid, n in self.nodes.items():
            label = str(n.get("label", "")).lower()
            norm = str(n.get("norm_label") or label)
            exact = label in (low, low + ".cs") or nid == name
            if not exact and low not in norm:
                continue
            sf = n.get("source_file") or ""
            rank = (0 if sf else 1) if exact else (2 if sf else 3)
            ranked.append((rank, sf, str(nid), n))
        ranked.sort(key=lambda r: r[:3])
        return [r[3] for r in ranked[:MAX_CANDIDATES]]

    def sole_definition(self, name: str):
        """The one .cs path defining a type of this name, else None (none, or several)."""
        if re.search(r"[\\/]", name):
            return None
        low = name.lower()
        files = {n.get("source_file") for n in self.nodes.values()
                 if n.get("source_file") and str(n.get("label", "")).lower() in (low, low + ".cs")}
        return next(iter(files)) if len(files) == 1 else None

    def classes_in(self, source_file: str) -> list:
        want = source_file.replace("\\", "/")
        return sorted({str(n["label"]) for n in self.nodes.values()
                       if n.get("_callable_class")
                       and (n.get("source_file") or "").replace("\\", "/") == want
                       and not str(n["label"]).endswith(".cs")})


class GraphCache:
    """Loads graph.json at most once per wrapper call, and only when a feature needs it."""

    def __init__(self, path: Path):
        self.path = path
        self._tried = False
        self._data = None
        self.seconds = 0.0
        self._lock = threading.Lock()

    def get(self):
        with self._lock:
            if not self._tried:
                self._tried = True
                started = time.monotonic()
                self._data = GraphData.load(self.path)
                self.seconds = time.monotonic() - started
        return self._data


def _capture(cmd: list, env: dict):
    proc = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8",
                          errors="replace", env=env)
    return proc.returncode, proc.stdout or "", proc.stderr or ""


def _candidate_block(data: GraphData, name: str) -> str:
    found = data.candidates(name)
    if not found:
        return f"graphify_taom: No node label contains '{name}'. Check the spelling.\n"
    lines = [f"Candidates for {name}: rerun with the repo-relative .cs path or an id:"]
    for n in found:
        loc = f"{n.get('source_file') or '-'}:{n.get('source_location') or '-'}"
        lines.append(f"  {n['id']}  {n.get('label', '')}  {loc}")
    return "\n".join(lines) + "\n"


def _via_blocks(exe, gpath, env, rest, out, supers) -> str:
    seen = {l for l in out.splitlines() if l.startswith("- ")}
    text = []
    picked = supers[:MAX_SUPERTYPES]
    # Each graphify call re-reads the whole graph (about 3 s), so the supertypes run side by side.
    with ThreadPoolExecutor(max_workers=len(picked) or 1) as pool:
        answers = list(pool.map(
            lambda sp: _capture(query_command(exe, "affected", [sp[1], *rest[1:]], gpath), env),
            picked))
    for (label, path), (rc, sub, _) in zip(picked, answers):
        text.append(f"Via {label} ({path}):")
        new = [l for l in sub.splitlines() if l.startswith("- ") and l not in seen]
        seen.update(new)
        text.extend(new if new else ["  (nothing new)"])
    if len(supers) > MAX_SUPERTYPES:
        text.append(f"NOTE: {len(supers) - MAX_SUPERTYPES} more supertype(s) not queried.")
    return "\n".join(text) + "\n" if text else ""


def _member_blocks(data, queries, printed) -> str:
    seen = {l for l in printed.splitlines() if l.startswith("- ")}
    text = []
    for query in queries:
        label, lines = data.member_callers(query)
        new = [l for l in lines if l not in seen]
        seen.update(new)
        if new:
            text.append(f"Member callers of {label}:")
            text.extend(new)
    return "\n".join(text) + "\n" if text else ""


def affected_answer(exe, gpath, env, rest, interfaces, cache):
    """graphify's `affected`, plus callers of the target's TAOM supertypes and, for a name that
    does not resolve, a candidate list or one retry with its defining file. Returns
    (exit code, stdout text, stderr text). Everything beyond graphify's own answer is advisory,
    so a bug or an unreadable graph.json adds a NOTE line and keeps graphify's output."""
    with ThreadPoolExecutor(max_workers=1) as loader:
        loader.submit(cache.get)  # reads graph.json while graphify answers
        rc, out, err = _capture(query_command(exe, "affected", rest, gpath), env)
    target = rest[0] if rest and not rest[0].startswith("-") else None
    if target is None:
        return rc, out, err
    extra = ""
    try:
        if NO_MATCH in out:
            data = cache.get()
            if data is None:
                extra += "NOTE: graphify_taom: graph.json could not be read, so no candidates.\n"
            else:
                retry = data.sole_definition(target)
                if retry and retry != target:
                    out = (f"graphify_taom: no unique node match for {target}; retrying with "
                           f"{retry} (the file that defines it)\n")
                    rest = [retry, *rest[1:]]
                    rc, again, err2 = _capture(query_command(exe, "affected", rest, gpath), env)
                    out += again
                    err += err2
                    target = retry
                if NO_MATCH in out:
                    extra += _candidate_block(data, target)
        if "Affected nodes for" in out:
            data = cache.get()
            if data is None:
                extra += ("NOTE: graphify_taom: graph.json could not be read, so callers of "
                          "interfaces, base classes and members are not added." + "\n")
            else:
                supers = data.supertypes(target) if interfaces else []
                extra += _via_blocks(exe, gpath, env, [target, *rest[1:]], out, supers)
                queries = [target] + [path for _, path in supers[:MAX_SUPERTYPES]]
                extra += _member_blocks(data, queries, out + extra)
    except Exception as exc:  # advisory layer: never lose graphify's own answer
        extra += f"NOTE: graphify_taom: extra lookups failed ({type(exc).__name__}: {exc}).\n"
    return rc, out + ("" if out.endswith("\n") or not out else "\n") + extra, err


def stale_in_answer(detail: list, text: str) -> list:
    norm = text.replace("\\", "/")
    return [f for f in detail if f in norm]

def stale_warning(detail: list, text: str) -> list:
    """Warning lines for a stale graph: the changed files this answer mentions first, because
    those are the ones the answer can be wrong about, then the total and the refresh command."""
    lines = []
    hits = stale_in_answer(detail, text)
    if hits:
        shown = ", ".join(hits[:STALE_NAMED])
        if len(hits) > STALE_NAMED:
            shown += f" and {len(hits) - STALE_NAMED} more"
        lines.append(f"STALE in this answer: {shown}: Read them directly before acting.")
    lines.append(f"graphify_taom: the graph is STALE ({len(detail)} code file(s) changed since the "
                 "build); answers can miss them. Refresh: python tools/graphify_taom.py refresh")
    return lines


# --- the tests verb ----------------------------------------------------------------------

TEST_ROOT = "TAOM.Tests/"


def hit_files(text: str) -> list:
    """Repo paths of the `- Label [relation] path:Lnn` lines of an affected answer."""
    files = []
    for line in text.splitlines():
        if not line.startswith("- "):
            continue
        token = line.rsplit(" ", 1)[-1]
        match = re.match(r"^(.*\.\w+):[^:]*$", token)
        path = (match.group(1) if match else token).replace("\\", "/")
        if path not in files:
            files.append(path)
    return files


def member_caller_files(text: str) -> list:
    """Paths named in the `Member callers of ...` sections of an affected answer."""
    lines, in_section = [], False
    for line in text.splitlines():
        if line.startswith("Member callers of "):
            in_section = True
        elif not line.startswith("- "):
            in_section = False
        elif in_section:
            lines.append(line)
    return hit_files("\n".join(lines))


def test_filter_command(classes: list) -> str:
    flt = "|".join(f"FullyQualifiedName~{c}" for c in classes)
    return f'dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "{flt}"'


def tests_for(inputs: list, exe, gpath, env, depth: int, interfaces: bool):
    """(note, command): the test classes that depend on the inputs. One of the two is None
    unless some input would not resolve, which adds its own note beside a command."""
    cache = GraphCache(gpath)
    cache.get()
    files, notes, hop = [], [], []

    def collect(out, extra=()):
        paths = hit_files(out) + list(extra)
        files.extend(p for p in dict.fromkeys(paths) if p.startswith(TEST_ROOT) and p not in files)

    for item in inputs:
        _, out, _ = affected_answer(exe, gpath, env, [item, "--depth", str(depth)], interfaces,
                                    cache)
        if NO_MATCH in out:
            notes.append(f"NOTE: graphify_taom: '{item}' did not resolve; see "
                         "`affected` for candidates.")
        item_path = item.replace("\\", "/")
        collect(out, [item_path])
        hop += [f for f in member_caller_files(out)
                if not f.startswith(TEST_ROOT) and f not in hop and f != item_path]
    # A production caller of a member is itself a dependent: the tests that reach it count too.
    with ThreadPoolExecutor(max_workers=max(1, len(hop[:MAX_HOP_FILES]))) as pool:
        for _, out, _ in pool.map(
                lambda f: affected_answer(exe, gpath, env, [f, "--depth", str(depth)], False,
                                          cache), hop[:MAX_HOP_FILES]):
            collect(out)
    if len(hop) > MAX_HOP_FILES:
        notes.append(f"NOTE: graphify_taom: {len(hop) - MAX_HOP_FILES} more member caller "
                     "file(s) not followed.")
    classes = set()
    data = cache.get() if files else None
    for path in files:
        found = data.classes_in(path) if data else []
        classes.update(found or [Path(path).stem])
    if not classes:
        notes.insert(0, "NOTE: graphify_taom: no test in TAOM.Tests reaches "
                        + ", ".join(inputs) + " at this depth; run the filtered suite for the "
                        "area by hand.")
        return "\n".join(notes), None
    return ("\n".join(notes) or None), test_filter_command(sorted(classes))


# --- refresh --detach --------------------------------------------------------------------

DETACH_BUDGET = 1.5


def _bannerlord_running(timeout: float):
    """True or False, or None when the check could not be made in time."""
    try:
        proc = subprocess.run(["tasklist", "/FI", "IMAGENAME eq Bannerlord.exe", "/NH"],
                              capture_output=True, text=True, timeout=timeout,
                              errors="replace")
    except (subprocess.TimeoutExpired, OSError):
        return None
    if proc.returncode != 0:
        return None
    return "bannerlord.exe" in (proc.stdout or "").lower()


AUTOREFRESH_LOG_CAP = 1_000_000


def _spawn_detached(cmd: list, log: Path) -> int:
    log.parent.mkdir(parents=True, exist_ok=True)
    try:  # every stale session appends a refresh, so keep one previous log and no more
        if log.stat().st_size > AUTOREFRESH_LOG_CAP:
            os.replace(log, log.with_name(log.name + ".1"))
    except OSError:
        pass
    kwargs = {}
    if os.name == "nt":
        kwargs["creationflags"] = (getattr(subprocess, "DETACHED_PROCESS", 0x8)
                                   | getattr(subprocess, "CREATE_NEW_PROCESS_GROUP", 0x200))
    else:
        kwargs["start_new_session"] = True
    with open(log, "ab") as sink:
        proc = subprocess.Popen(cmd, stdin=subprocess.DEVNULL, stdout=sink,
                                stderr=subprocess.STDOUT, cwd=REPO, close_fds=True, **kwargs)
    return proc.pid


def _refresh_detached(args) -> int:
    """Start `refresh` as a detached child and return at once (the SessionStart hook's call).
    Every reason not to start is printed and exits 0: it is advice, never a failure."""
    started = time.monotonic()
    out = out_root()
    try:
        validate_out_root(out, REPO)
    except ValueError as exc:
        print(f"graphify_taom: {exc}", file=sys.stderr)
        return EXIT_ENV
    gdir = graph_dir(out)
    skip = "graphify_taom: background refresh not started: "
    if os.environ.get("TAOM_GRAPHIFY_AUTOREFRESH") == "0":
        print(skip + "TAOM_GRAPHIFY_AUTOREFRESH=0.")
        return 0
    lock = out / LOCK_NAME
    try:
        if time.time() - lock.stat().st_mtime < LOCK_STALE_SECONDS:
            print(skip + f"another refresh holds {lock}.")
            return 0
    except OSError:
        pass
    try:
        state, detail = staleness(REPO, gdir)
    except (OSError, subprocess.CalledProcessError) as exc:
        print(skip + f"staleness could not be checked ({exc}).")
        return 0
    if state == "fresh":
        print(skip + _summary_line(state, detail, read_stamp(gdir)))
        return 0
    running = _bannerlord_running(max(0.3, DETACH_BUDGET - (time.monotonic() - started)))
    if running is None:
        print(skip + "the Bannerlord process check was skipped (timeout or error).")
        return 0
    if running:
        print(skip + "Bannerlord is running; a refresh would compete with it for the disk.")
        return 0
    if not _graphify_exe():
        return EXIT_ENV
    cmd = [sys.executable, str(Path(__file__).resolve()), "refresh"]
    cmd += ["--if-stale"] if args.if_stale else []
    cmd += ["--force"] if args.force else []
    log = gdir / "autorefresh.log"
    try:
        pid = _spawn_detached(cmd, log)
    except OSError as exc:
        print(skip + f"the child could not be started ({exc}).")
        return 0
    print(f"graphify_taom: background refresh started (pid {pid}, log {log}).")
    return 0


def _stale_or_missing(gdir: Path):
    """(rc, detail): rc is EXIT_MISSING with the message printed, else None; detail lists the
    stale files (empty when fresh)."""
    state, detail = staleness(REPO, gdir)
    if state == "missing":
        print(f"graphify_taom: {detail[0]}. Build it first: python tools/graphify_taom.py "
              "refresh", file=sys.stderr)
        return EXIT_MISSING, []
    return None, detail if state == "stale" else []


def cmd_query(verb: str, rest: list) -> int:
    gdir = graph_dir(out_root())
    missing, stale = _stale_or_missing(gdir)
    if missing:
        return missing
    exe = _graphify_exe()
    if not exe:
        return EXIT_ENV
    env = graphify_env(out_root())
    gpath = gdir / "graph.json"
    try:
        if verb == "affected":
            interfaces = "--no-interfaces" not in rest
            rest = [a for a in rest if a != "--no-interfaces"]
            rc, out, err = affected_answer(exe, gpath, env, rest, interfaces, GraphCache(gpath))
        else:
            rc, out, err = _capture(query_command(exe, verb, rest, gpath), env)
    except ValueError as exc:
        print(f"graphify_taom: {exc}", file=sys.stderr)
        return 2
    if stale:
        print("\n".join(stale_warning(stale, out)), file=sys.stderr)
    sys.stdout.write(out)
    sys.stderr.write(err)
    return rc


def cmd_tests(rest: list) -> int:
    depth, interfaces, inputs, i = 2, True, [], 0
    while i < len(rest):
        if rest[i] == "--depth" and i + 1 < len(rest):
            depth, i = rest[i + 1], i + 1
        elif rest[i] == "--no-interfaces":
            interfaces = False
        else:
            inputs.append(rest[i])
        i += 1
    if not inputs:
        print("graphify_taom: tests needs at least one type name or .cs path.", file=sys.stderr)
        return 2
    gdir = graph_dir(out_root())
    missing, stale = _stale_or_missing(gdir)
    if missing:
        return missing
    exe = _graphify_exe()
    if not exe:
        return EXIT_ENV
    note, command = tests_for(inputs, exe, gdir / "graph.json", graphify_env(out_root()),
                              int(depth), interfaces)
    if stale:
        print("\n".join(stale_warning(stale, "")), file=sys.stderr)
    if note:
        print(note)
    if command:
        print(command)
    return 0


def main(argv=None) -> int:
    for stream in (sys.stdout, sys.stderr):
        try:
            stream.reconfigure(errors="replace")
        except (AttributeError, ValueError):
            pass
    argv = list(sys.argv[1:] if argv is None else argv)
    if argv and argv[0] == "gate":
        try:
            decision = gate_decision(sys.stdin.buffer.read().decode("utf-8", "replace"))
        except Exception:  # a judge bug must never block a tool call
            decision = {}
        print(json.dumps(decision))
        return 0
    if argv and argv[0] in QUERY_VERBS:
        return cmd_query(argv[0], argv[1:])
    if argv and argv[0] == "tests":
        return cmd_tests(argv[1:])
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)
    st = sub.add_parser("status", help="fresh / stale / missing (exit 0 / 1 / 2)")
    st.add_argument("--brief", action="store_true", help="one line, no path list")
    rf = sub.add_parser("refresh", help="rebuild the graph (about 2.5 minutes)")
    rf.add_argument("--if-stale", action="store_true", help="do nothing when current")
    rf.add_argument("--force", action="store_true",
                    help="full rescan, ignoring graphify's incremental cache")
    rf.add_argument("--detach", action="store_true",
                    help="start the refresh as a background process and return at once")
    sub.add_parser("report", help="print the GRAPH_REPORT.md path")
    sub.add_parser("tests", help="the dotnet test filter for the tests that reach the inputs")
    for verb in QUERY_VERBS:
        sub.add_parser(verb, help=f"graphify {verb} against the TAOM graph")
    args = ap.parse_args(argv)
    if args.cmd == "status":
        return cmd_status(args)
    if args.cmd == "refresh":
        return _refresh_detached(args) if args.detach else cmd_refresh(args)
    if args.cmd == "report":
        report = graph_dir(out_root()) / "GRAPH_REPORT.md"
        print(report)
        return 0 if report.is_file() else EXIT_MISSING
    return 2


if __name__ == "__main__":
    sys.exit(main())
