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
    python tools/graphify_taom.py refresh [--if-stale] [--force]
    python tools/graphify_taom.py affected "IModLogger" --depth 2
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
import time
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


def cmd_query(verb: str, rest: list) -> int:
    gdir = graph_dir(out_root())
    state, detail = staleness(REPO, gdir)
    if state == "missing":
        print(f"graphify_taom: {detail[0]}. Build it first: python tools/graphify_taom.py "
              "refresh", file=sys.stderr)
        return EXIT_MISSING
    if state == "stale":
        print(f"graphify_taom: the graph is STALE ({len(detail)} code file(s) changed since the "
              "build, e.g. " + ", ".join(detail[:3]) + "); answers can miss them. Refresh: "
              "python tools/graphify_taom.py refresh", file=sys.stderr)
    exe = _graphify_exe()
    if not exe:
        return EXIT_ENV
    try:
        cmd = query_command(exe, verb, rest, gdir / "graph.json")
    except ValueError as exc:
        print(f"graphify_taom: {exc}", file=sys.stderr)
        return 2
    return subprocess.run(cmd, env=graphify_env(out_root())).returncode


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
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)
    st = sub.add_parser("status", help="fresh / stale / missing (exit 0 / 1 / 2)")
    st.add_argument("--brief", action="store_true", help="one line, no path list")
    rf = sub.add_parser("refresh", help="rebuild the graph (about 2.5 minutes)")
    rf.add_argument("--if-stale", action="store_true", help="do nothing when current")
    rf.add_argument("--force", action="store_true",
                    help="full rescan, ignoring graphify's incremental cache")
    sub.add_parser("report", help="print the GRAPH_REPORT.md path")
    for verb in QUERY_VERBS:
        sub.add_parser(verb, help=f"graphify {verb} against the TAOM graph")
    args = ap.parse_args(argv)
    if args.cmd == "status":
        return cmd_status(args)
    if args.cmd == "refresh":
        return cmd_refresh(args)
    if args.cmd == "report":
        report = graph_dir(out_root()) / "GRAPH_REPORT.md"
        print(report)
        return 0 if report.is_file() else EXIT_MISSING
    return 2


if __name__ == "__main__":
    sys.exit(main())
