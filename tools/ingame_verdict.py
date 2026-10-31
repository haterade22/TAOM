#!/usr/bin/env python3
"""Read a session's logs and give each owed in-game check a verdict: PASS, FAIL or NOT REACHED.

Why: many closed issues still owe an in-game check, and the lines that prove most of them are
already in the logs TAOM, Coop and the dedicated server write. This tool reads those logs against
a manifest per issue and prints the verdict with its evidence lines, so a person is needed only
for what a log cannot show (#1069, phase 0: #1070).

Usage:
    python tools/ingame_verdict.py (--issue N [N ...] | --owed) [--session newest|PATH] [--sessions K]
                                   [--game DIR] [--ds-data DIR] [--ds-engine DIR]
                                   [--format text|md] [--post]

Manifests: tools/ingame_checks/<issue>.toml, read with tomllib. Top level: issue, title (optional),
fixed_in (a commit), logs (the kinds below; taom or taom-ds must be one), allow_dirty (default
false). Each [[check]] has an id (^[a-z0-9]+(\\.[a-z0-9-]+)+$), needs (what the tester does), an
optional scope regex that cuts each log into segments at its matches (lines before the first match
are dropped), the regex lists reached, pass (required) and fail, min (each pass pattern must match
at least that many lines, default 1) and absence ("fail", the default, or "not-reached"). [[human]]
items carry a claim; they are printed as still owed by a person and never counted. A pattern is
searched in one log line at a time; one that matches an empty line is refused, since it matches
every line, and a scope needs logs to name one kind. The file is UTF-8.

Verdict per check and per segment, in order:
    1. a fail pattern matches: FAIL;
    2. every pass pattern matches (min times each): PASS;
    3. every reached pattern matches (each on some line of the segment): absence decides. FAIL needs
       every log kind the manifest names to have been read for the session; otherwise the missing
       line proves nothing: NOT REACHED;
    4. otherwise NOT REACHED (a [Check] skip line for the check's id gives the reason).
Across segments FAIL wins, then PASS. Evidence counts only from a session whose TAOM logs carry a
[BuildStamp] sha that contains fixed_in (git merge-base --is-ancestor); a .dirty build only with
allow_dirty = true; otherwise every check of that session is NOT REACHED with the reason.
--sessions K evaluates the K newest sessions and keeps, per check, the newest PASS or FAIL; a PASS
names an older bound session's FAIL for the same check ("; FAIL in session N: reason"). A crash
report echoes the log's last lines back behind "[CrashReport] "; those lines are never matched. An
issue is PASS when every check is, FAIL when any check is, else NOT REACHED.

Logs (kinds): taom (<game>\\bin\\Win64_Shipping_Client\\Logs\\taom_debug_*.log), coop-client
(Coop_client*.log beside it), diag (<game>\\Modules\\TAOM.Dependencies\\diag.log), rgl
(C:\\ProgramData\\Mount and Blade II Bannerlord\\logs\\rgl_log_<pid>.txt), and with --ds-data: ds
(logs\\coop-server-*.log) and coop-server (logs\\Coop_server.log); with --ds-engine: taom-ds
(bin\\Win64_Shipping_Server\\Logs\\taom_debug_*.log). --game defaults to the install
Directory.Build.props builds against ($BANNERLORD_OVERRIDE_DIR when it holds the game, else
$BANNERLORD_GAME_DIR, else the root in tools/_gamedir.py). A session is one log of the first
taom, taom-ds or ds kind the manifest names that has files; its window runs from the file name's
stamp to its last line (a ds log: to its mtime). Other logs join when their own window overlaps:
taom, taom-ds and ds logs by name stamp and mtime; Coop logs, whose lines carry no date, by mtime
and their first HH:mm:ss (the tool says when it cannot place one); diag.log by its session blocks;
rgl logs when their mtime falls in the window or up to 5 minutes after it, but before the next
session's name stamp. --session PATH takes its kind from the file: a taom_debug log under
Win64_Shipping_Server is taom-ds, any other taom; a manifest whose logs do not name that kind is
NOT REACHED with a note.

--post comments the markdown report on each issue (gh issue comment N --body-file -) after
replacing the user-profile path with %USERPROFILE%. The repository is public, so an agent posts
only on Mike's word. Nothing here edits labels or closes an issue.

Exit codes: 0 every selected issue PASS; 1 any FAIL; 4 none FAIL, some NOT REACHED, or nothing
judged (--owed found no issue with a manifest); 2 bad arguments or manifest; 3 git missing, or gh
failed or is missing (the message says what; this tool reports, it never installs or logs in). A
build sha git cannot compare leaves that session NOT REACHED with git's exit code.
"""
import argparse
import json
import os
import re
import subprocess
import sys
import tomllib
from datetime import datetime, timedelta
from pathlib import Path

from _gamedir import DEFAULT_GAME_ROOT, game_dir

TOOLS = Path(__file__).resolve().parent
REPO = TOOLS.parent
CHECKS_DIR = TOOLS / "ingame_checks"
RGL_DIR = Path(r"C:\ProgramData\Mount and Blade II Bannerlord\logs")
LABEL = "triage-needs-ingame"
LIMIT = 1000
HINT = "run gh auth status"

# The one outcome line TAOM, Coop and the server share (#1069 design 3a); phase 1 emits it.
CHECK_LINE = re.compile(r"\[Check\] id=(\S+) result=(pass|fail|skip)(.*)$")
CHECK_ID = re.compile(r"^[a-z0-9]+(\.[a-z0-9-]+)+$")
# A [Check] id as a manifest pattern spells it: "\[Check\] id=taom\.x\.y".
NAMED_CHECK = re.compile(r"\\\[Check\\\] id=((?:[a-z0-9-]|\\\.)+)")
BUILD_STAMP = re.compile(r"\[BuildStamp\] TAOM=\S+ build\.\S*?\+([0-9a-f]{7,40})(\.dirty)?(?:\s|$)")
# FileLogger writes "[yyyy-MM-dd HH:mm:ss] [LEVEL] message"; CrashReportService.WriteToLog logs each report line
# (the last 500 TAOM lines among them) as "[CrashReport] " + line, so the log holds a second copy of them.
CRASH_ECHO = re.compile(r"^\[\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\] \[[A-Z]+\] \[CrashReport\] ")
SHUTDOWN = "[Shutdown] clean module unload"

LOG_KINDS = ("taom", "taom-ds", "diag", "rgl", "coop-client", "coop-server", "ds")
TAOM_KINDS = ("taom", "taom-ds")
ANCHOR_KINDS = ("taom", "taom-ds", "ds")
TAOM_NAME = re.compile(r"^taom_debug_(\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2})\.log$")
RGL_NAME = re.compile(r"^rgl_log_\d+\.txt$")
DS_NAME = re.compile(r"^coop-server-(\d{8}-\d{6})\.log$")
TAOM_TIME = re.compile(r"^\[(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2})\] ")
DIAG_TIME = re.compile(r"^(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2})\.\d{3} ")
DIAG_SESSION = re.compile(r"^# TAOM\.Dependencies session start: (\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2})")
COOP_TIME = re.compile(r"^\[(?:\(\d+\) )?(\d{2}:\d{2}:\d{2}) [A-Z]{3} ")
RGL_SLACK = timedelta(minutes=5)
EVIDENCE = 3

PASS, FAIL, NOT_REACHED = "PASS", "FAIL", "NOT REACHED"
MANIFEST_KEYS = {"issue", "title", "fixed_in", "logs", "allow_dirty", "check", "human"}
CHECK_KEYS = {"id", "needs", "scope", "reached", "pass", "fail", "min", "absence"}
USER_PROFILE_PATH = re.compile(r"[A-Za-z]:(\\\\|\\|/)Users\1[^\\/\s'\"]+", re.I)


def run_command(args, input=None):
    """Return (returncode, stdout); 127 when the program is missing. Never raises. Runs in the TAOM
    repo, because gh picks the repository from its working folder."""
    try:
        r = subprocess.run(args, capture_output=True, text=True, encoding="utf-8",
                           errors="replace", input=input, cwd=str(REPO))
    except OSError:
        return 127, ""
    return r.returncode, r.stdout


class VerdictError(Exception):
    def __init__(self, message, code):
        super().__init__(message)
        self.code = code


class Check:
    def __init__(self, raw, where):
        if not isinstance(raw, dict):
            raise VerdictError(f"{where}: each [[check]] must be a table", 2)
        unknown = set(raw) - CHECK_KEYS
        if unknown:
            raise VerdictError(f"{where}: unknown key(s) {', '.join(sorted(unknown))}", 2)
        self.id = raw.get("id")
        if not isinstance(self.id, str) or not CHECK_ID.match(self.id):
            raise VerdictError(f"{where}: id {self.id!r} must match {CHECK_ID.pattern}", 2)
        where = f"{where} ({self.id})"
        self.needs = raw.get("needs", "")
        if not isinstance(self.needs, str):
            raise VerdictError(f"{where}: needs must be a string", 2)
        self.scope = _compile(raw["scope"], where) if "scope" in raw else None
        self.reached = _patterns(raw, "reached", where)
        self.passes = _patterns(raw, "pass", where)
        self.fails = _patterns(raw, "fail", where)
        if not self.passes:
            raise VerdictError(f"{where}: pass needs at least one pattern", 2)
        self.min = raw.get("min", 1)
        if isinstance(self.min, bool) or not isinstance(self.min, int) or self.min < 1:
            raise VerdictError(f"{where}: min must be a whole number from 1", 2)
        self.absence = raw.get("absence", "fail")
        if self.absence not in ("fail", "not-reached"):
            raise VerdictError(f"{where}: absence must be \"fail\" or \"not-reached\"", 2)


def _compile(pattern, where):
    if not isinstance(pattern, str):
        raise VerdictError(f"{where}: pattern {pattern!r} is not a string", 2)
    try:
        rx = re.compile(pattern)
    except re.error as e:
        raise VerdictError(f"{where}: pattern {pattern!r} is not a regex ({e})", 2)
    # A pattern that matches the empty string matches every line: as a pass pattern, a silent false PASS.
    if rx.search(""):
        raise VerdictError(f"{where}: pattern {pattern!r} matches an empty line, so it matches every line", 2)
    return rx


def _patterns(raw, key, where):
    value = raw.get(key, [])
    if not isinstance(value, list):
        raise VerdictError(f"{where}: {key} must be a list of patterns", 2)
    return [_compile(p, f"{where} {key}") for p in value]


class Manifest:
    def __init__(self, raw, path, number):
        where = Path(path).name
        unknown = set(raw) - MANIFEST_KEYS
        if unknown:
            raise VerdictError(f"{where}: unknown key(s) {', '.join(sorted(unknown))}", 2)
        self.issue = raw.get("issue")
        if self.issue != number:
            raise VerdictError(f"{where}: issue = {self.issue!r}, but the file is named for #{number}", 2)
        self.title = raw.get("title", "")
        if not isinstance(self.title, str):
            raise VerdictError(f"{where}: title must be a string", 2)
        self.fixed_in = raw.get("fixed_in")
        if not isinstance(self.fixed_in, str) or not re.fullmatch(r"[0-9a-f]{7,40}", self.fixed_in):
            raise VerdictError(f"{where}: fixed_in must be a commit sha (7 to 40 hex digits)", 2)
        self.logs = raw.get("logs")
        if not isinstance(self.logs, list) or not self.logs or any(k not in LOG_KINDS for k in self.logs):
            raise VerdictError(f"{where}: logs must list kinds from {', '.join(LOG_KINDS)}", 2)
        if not any(k in TAOM_KINDS for k in self.logs):
            raise VerdictError(f"{where}: logs must name taom or taom-ds, whose [BuildStamp] binds the evidence", 2)
        self.allow_dirty = raw.get("allow_dirty", False)
        if not isinstance(self.allow_dirty, bool):
            raise VerdictError(f"{where}: allow_dirty must be true or false", 2)
        checks = raw.get("check", [])
        if not isinstance(checks, list) or not checks:
            raise VerdictError(f"{where}: no [[check]]", 2)
        self.checks = [Check(c, f"{where} check {i + 1}") for i, c in enumerate(checks)]
        # A scope cuts each log on its own, so a pass line in a second kind of log would fall outside every segment.
        if len(set(self.logs)) > 1 and any(c.scope for c in self.checks):
            raise VerdictError(f"{where}: a check with a scope needs logs to name one kind", 2)
        ids = [c.id for c in self.checks]
        if len(set(ids)) != len(ids):
            raise VerdictError(f"{where}: a check id appears twice", 2)
        human = raw.get("human", [])
        if not isinstance(human, list):
            raise VerdictError(f"{where}: human must be [[human]] tables", 2)
        self.human = []
        for h in human:
            if not isinstance(h, dict) or set(h) != {"claim"} or not isinstance(h["claim"], str):
                raise VerdictError(f"{where}: each [[human]] holds one claim string", 2)
            self.human.append(h["claim"])
        for check_id in named_check_ids(self):
            if not CHECK_ID.match(check_id):
                raise VerdictError(f"{where}: [Check] id {check_id!r} must match {CHECK_ID.pattern} "
                                   r"(escape each dot as \.)", 2)


def load_manifest(path, number):
    path = Path(path)
    if not path.is_file():
        raise VerdictError(f"no manifest for #{number} at {path}", 2)
    try:
        with open(path, "rb") as fh:
            raw = tomllib.load(fh)
    except tomllib.TOMLDecodeError as e:
        raise VerdictError(f"{path.name} is not valid TOML ({e})", 2)
    except UnicodeDecodeError as e:
        raise VerdictError(f"{path.name} is not UTF-8 ({e})", 2)
    return Manifest(raw, path, number)


def named_check_ids(manifest):
    """The [Check] ids a manifest's patterns name, unescaped."""
    ids = set()
    for c in manifest.checks:
        for p in c.reached + c.passes + c.fails + ([c.scope] if c.scope else []):
            ids.update(m.replace("\\.", ".") for m in NAMED_CHECK.findall(p.pattern))
    return ids


def parse_build_stamp(line):
    """(sha, dirty) from a [BuildStamp] line, else None."""
    m = BUILD_STAMP.search(line)
    return (m.group(1), m.group(2) is not None) if m else None


def scrub(text, profile=None):
    """Replace the user-profile path with %USERPROFILE% (the issue tracker is public)."""
    profile = os.environ.get("USERPROFILE", "") if profile is None else profile
    if profile.strip():
        text = re.compile(re.escape(profile), re.I).sub("%USERPROFILE%", text)
    return USER_PROFILE_PATH.sub("%USERPROFILE%", text)


def place_coop(lines, mtime):
    """The start of a Coop log, whose lines carry a time but no date: its mtime's date and its
    first HH:mm:ss, a day earlier when that time is later than the mtime's. None when no line has one."""
    for text in lines[:200]:
        m = COOP_TIME.match(text)
        if m:
            start = datetime.combine(mtime.date(), datetime.strptime(m.group(1), "%H:%M:%S").time())
            return start - timedelta(days=1) if start > mtime else start
    return None


def name_start(kind, path):
    m = (TAOM_NAME if kind in TAOM_KINDS else DS_NAME).match(Path(path).name)
    if not m:
        return None
    return datetime.strptime(m.group(1), "%Y-%m-%d_%H-%M-%S" if kind in TAOM_KINDS else "%Y%m%d-%H%M%S")


def path_kind(path):
    """The anchor kind of a --session file: a taom_debug log is the server's (taom-ds) in a
    Win64_Shipping_Server folder and the client's (taom) anywhere else. None for any other name."""
    path = Path(path).resolve()
    if TAOM_NAME.match(path.name):
        return "taom-ds" if any(p.name.lower() == "win64_shipping_server" for p in path.parents) else "taom"
    return "ds" if DS_NAME.match(path.name) else None


def mtime_of(path):
    return datetime.fromtimestamp(Path(path).stat().st_mtime)


def default_game():
    """The install Directory.Build.props builds against."""
    override = os.environ.get("BANNERLORD_OVERRIDE_DIR", "").strip()
    if override and (Path(override) / "bin" / "Win64_Shipping_Client" / "Bannerlord.exe").is_file():
        return override
    return game_dir(DEFAULT_GAME_ROOT)


def find_logs(game, ds_data, ds_engine, rgl_dir):
    """{kind: [paths]} and {kind: where it was looked for}."""
    client = Path(game) / "bin" / "Win64_Shipping_Client"
    found = {k: [] for k in LOG_KINDS}
    where = {"taom": str(client / "Logs"), "coop-client": str(client), "rgl": str(rgl_dir),
             "diag": str(Path(game) / "Modules" / "TAOM.Dependencies"),
             "ds": "pass --ds-data", "coop-server": "pass --ds-data", "taom-ds": "pass --ds-engine"}
    found["taom"] = [p for p in (client / "Logs").glob("taom_debug_*.log") if name_start("taom", p)]
    found["coop-client"] = sorted(client.glob("Coop_client*.log"))
    diag = Path(game) / "Modules" / "TAOM.Dependencies" / "diag.log"
    found["diag"] = [diag] if diag.is_file() else []
    # Only rgl_log_<pid>.txt: the engine writes rgl_log_errors_<pid>.txt as the next process starts.
    found["rgl"] = sorted(p for p in Path(rgl_dir).glob("rgl_log_*.txt") if RGL_NAME.match(p.name))
    if ds_data:
        logs = Path(ds_data) / "logs"
        where["ds"] = where["coop-server"] = str(logs)
        found["ds"] = [p for p in logs.glob("coop-server-*.log") if name_start("ds", p)]
        found["coop-server"] = [p for p in [logs / "Coop_server.log"] if p.is_file()]
    if ds_engine:
        logs = Path(ds_engine) / "bin" / "Win64_Shipping_Server" / "Logs"
        where["taom-ds"] = str(logs)
        found["taom-ds"] = [p for p in logs.glob("taom_debug_*.log") if name_start("taom-ds", p)]
    return found, where


class LogFile:
    def __init__(self, kind, path, lines):
        self.kind = kind
        self.path = Path(path)
        # [(line number, text)], numbers as in the file; a crash report's echo of the log is dropped.
        self.lines = [(n, t) for n, t in lines if not CRASH_ECHO.match(t)]

    @property
    def name(self):
        return self.path.name


class Session:
    def __init__(self, anchor, start, end):
        self.anchor = anchor
        self.logs = [anchor]
        self.start = start
        self.end = end
        self.notes = []
        self.missing = []
        self.bound = False
        self.build = ""


class Result:
    def __init__(self, verdict, reason, evidence=()):
        self.verdict = verdict
        self.reason = reason
        self.evidence = list(evidence)[:EVIDENCE]
        self.session = None


class Reader:
    """Reads each log once, however many issues and sessions use it."""

    def __init__(self):
        self.cache = {}

    def lines(self, path):
        key = str(path)
        if key not in self.cache:
            self.cache[key] = Path(path).read_text(encoding="utf-8-sig", errors="replace").splitlines()
        return self.cache[key]

    def log(self, kind, path):
        return LogFile(kind, path, list(enumerate(self.lines(path), 1)))


def last_taom_time(lines):
    for text in reversed(lines):
        m = TAOM_TIME.match(text)
        if m:
            return datetime.strptime(m.group(1), "%Y-%m-%d %H:%M:%S")
    return None


def diag_slice(lines, start, end):
    """The lines of each diag.log session block whose time span overlaps [start, end]."""
    blocks, cur = [], None
    for n, text in enumerate(lines, 1):
        m = DIAG_SESSION.match(text)
        if m:
            t = datetime.strptime(m.group(1), "%Y-%m-%d %H:%M:%S")
            cur = [t, t, []]
            blocks.append(cur)
        if cur is None:
            continue
        tm = DIAG_TIME.match(text)
        if tm:
            cur[1] = max(cur[1], datetime.strptime(tm.group(1), "%Y-%m-%d %H:%M:%S"))
        cur[2].append((n, text))
    return [line for s, e, ls in blocks if s <= end and e >= start for line in ls]


def make_session(kind, path, manifest, found, reader):
    lines = reader.lines(path)
    start = name_start(kind, path)
    end = (last_taom_time(lines) if kind in TAOM_KINDS else None) or mtime_of(path)
    s = Session(reader.log(kind, path), start, max(start, end))
    # A dedicated server never logs [Shutdown], so the note is a client's only.
    if kind == "taom" and not any(SHUTDOWN in t for t in lines):
        s.notes.append("no [Shutdown] line: still running, or ended by a debugger stop, a crash or a kill")
    # The engine writes its rgl log after TAOM's last line, but never into the next session.
    later = [name_start(kind, p) for p in found.get(kind, []) if name_start(kind, p) > start]
    rgl_end = min([s.end + RGL_SLACK] + later)
    for other in manifest.logs:
        if other == kind:
            continue
        for p in found.get(other, []):
            if other in TAOM_KINDS or other == "ds":
                if name_start(other, p) <= s.end and mtime_of(p) >= s.start:
                    s.logs.append(reader.log(other, p))
            elif other in ("coop-client", "coop-server"):
                placed = place_coop(reader.lines(p), mtime_of(p))
                if placed is None:
                    s.notes.append(f"could not place {p.name}: no line carries a time")
                elif placed <= s.end and mtime_of(p) >= s.start:
                    s.logs.append(reader.log(other, p))
            elif other == "rgl":
                if s.start <= mtime_of(p) <= s.end or s.end < mtime_of(p) < rgl_end:
                    s.logs.append(reader.log(other, p))
            elif other == "diag":
                part = diag_slice(reader.lines(p), s.start, s.end)
                if part:
                    s.logs.append(LogFile(other, p, part))
    s.missing = [k for k in manifest.logs if not any(log.kind == k for log in s.logs)]
    return s


def build_sessions(manifest, found, where, session_arg, k, reader):
    """(sessions newest first, a note or None)."""
    anchors = [kd for kd in manifest.logs if kd in ANCHOR_KINDS]
    if session_arg != "newest":
        path = Path(session_arg)
        kind = path_kind(path)
        if kind not in anchors:
            return [], f"--session {path.name} is a {kind} log, which this manifest does not read"
        return [make_session(kind, path, manifest, found, reader)], None
    kind = next((kd for kd in anchors if found.get(kd)), None)
    if kind is None:
        looked = "; ".join(f"{kd}: {where[kd]}" for kd in anchors)
        return [], f"no {' or '.join(anchors)} log found ({looked})"
    paths = sorted(found[kind], key=lambda p: name_start(kind, p), reverse=True)[:k]
    note = None if kind == anchors[0] else (f"no {anchors[0]} log found in {where[anchors[0]]}, "
                                            f"so the sessions come from the {kind} logs")
    return [make_session(kind, p, manifest, found, reader) for p in paths], note


class Binder:
    """Binds a session to a build through each of its TAOM logs' [BuildStamp]."""

    def __init__(self, runner, repo):
        self.runner = runner
        self.repo = repo
        self.cache = {}

    def ancestor(self, fixed_in, sha):
        key = (fixed_in, sha)
        if key not in self.cache:
            rc, _ = self.runner(["git", "-C", str(self.repo), "merge-base", "--is-ancestor", fixed_in, sha])
            if rc == 127:
                raise VerdictError("git is missing; install git or put it on PATH", 3)
            self.cache[key] = rc
        return self.cache[key]

    def bind(self, manifest, session):
        taom_logs = [log for log in session.logs if log.kind in TAOM_KINDS]
        if not taom_logs:
            return False, "no TAOM log in this session, so no [BuildStamp] binds its build"
        labels = []
        for log in taom_logs:
            stamp = next((s for s in (parse_build_stamp(t) for _, t in log.lines if "[BuildStamp]" in t) if s), None)
            if stamp is None:
                return False, f"no [BuildStamp] line with a git sha in {log.name}"
            sha, dirty = stamp
            label = sha[:9] + (".dirty" if dirty else "")
            if dirty and not manifest.allow_dirty:
                return False, f"build {label} is .dirty and the manifest does not set allow_dirty"
            rc = self.ancestor(manifest.fixed_in, sha)
            if rc == 1:
                return False, f"build {label} predates fixed_in {manifest.fixed_in}"
            if rc != 0:
                return False, (f"git could not compare fixed_in {manifest.fixed_in} with build {label} "
                               f"(exit {rc}); is that commit in this clone?")
            labels.append(label)
        return True, f"build {', '.join(labels)} contains fixed_in {manifest.fixed_in}"


def judge_segment(check, lines, missing):
    fails = [line for line in lines if any(p.search(line[2]) for p in check.fails)]
    if fails:
        return Result(FAIL, "a fail pattern matched", fails)
    hits = [[line for line in lines if p.search(line[2])] for p in check.passes]
    if all(len(h) >= check.min for h in hits):
        times = f", each at least {check.min} times" if check.min > 1 else ""
        return Result(PASS, "every pass pattern matched" + times, [h[0] for h in hits])
    short = next(i for i, h in enumerate(hits) if len(h) < check.min)
    pattern = check.passes[short].pattern
    if check.min > 1:
        detail = f"pass pattern '{pattern}' matched {len(hits[short])} of the {check.min} lines it needs"
    else:
        detail = f"no line matched pass pattern '{pattern}'"
    reached = [line for line in lines if any(p.search(line[2]) for p in check.reached)]
    if check.reached and all(any(p.search(line[2]) for line in lines) for p in check.reached):
        if check.absence == "not-reached":
            return Result(NOT_REACHED, f"reached, but {detail} (absence = not-reached)", reached)
        if missing:
            return Result(NOT_REACHED, f"reached, but {detail}; the {', '.join(missing)} log was not read, "
                                       "so a missing line proves nothing", reached)
        return Result(FAIL, f"reached, but {detail}", reached)
    for line in lines:
        m = CHECK_LINE.search(line[2])
        if m and m.group(1) == check.id and m.group(2) == "skip":
            return Result(NOT_REACHED, f"skipped: {m.group(3).strip() or 'no reason given'}", [line])
    some = [h[0] for h in hits if h]
    if some:
        return Result(NOT_REACHED, f"{len(some)} of the {len(hits)} pass patterns matched, but no reached line", some)
    return Result(NOT_REACHED, "no line matched a reached or pass pattern")


def judge_check(check, logs, missing):
    if check.scope is None:
        segments = [[(log, n, t) for log in logs for n, t in log.lines]]
    else:
        segments = []
        for log in logs:
            current = None
            for n, t in log.lines:
                if check.scope.search(t):
                    current = []
                    segments.append(current)
                if current is not None:
                    current.append((log, n, t))
        if not segments:
            return Result(NOT_REACHED, f"the scope pattern '{check.scope.pattern}' matched no line")
    results = [judge_segment(check, seg, missing) for seg in segments]
    for verdict in (FAIL, PASS):
        for i, r in enumerate(results):
            if r.verdict == verdict:
                if len(results) > 1:
                    r.reason += f" (segment {i + 1} of {len(results)})"
                return r
    return next((r for r in results if r.evidence), results[0])


class IssueReport:
    def __init__(self, manifest, sessions, results, note):
        self.manifest = manifest
        self.sessions = sessions
        self.results = results  # {check id: Result}
        self.note = note
        verdicts = [r.verdict for r in results.values()]
        self.verdict = FAIL if FAIL in verdicts else PASS if all(v == PASS for v in verdicts) else NOT_REACHED


def evaluate_issue(manifest, found, where, args, reader, binder):
    sessions, note = build_sessions(manifest, found, where, args.session, args.sessions, reader)
    per_session = []
    for s in sessions:
        s.bound, s.build = binder.bind(manifest, s)
        if s.bound:
            per_session.append({c.id: judge_check(c, s.logs, s.missing) for c in manifest.checks})
        else:
            per_session.append({c.id: Result(NOT_REACHED, f"evidence not counted: {s.build}")
                                for c in manifest.checks})
    # Per check: the newest PASS or FAIL; else the newest bound session's reason, which says more than "not bound".
    bound = [res for s, res in zip(sessions, per_session) if s.bound]
    results = {}
    for c in manifest.checks:
        chosen = None
        for i, res in enumerate(per_session, 1):
            res[c.id].session = i
            if chosen is None and res[c.id].verdict != NOT_REACHED:
                chosen = res[c.id]
        # The newest decisive verdict stands, but an older bound session's FAIL is not dropped from view.
        if chosen is not None and chosen.verdict == PASS:
            older = next((res[c.id] for s, res in zip(sessions, per_session)
                          if s.bound and res[c.id].verdict == FAIL and res[c.id].session > chosen.session), None)
            if older:
                chosen.reason += f"; FAIL in session {older.session}: {older.reason}"
        fallback = (bound or per_session)[0][c.id] if per_session else Result(NOT_REACHED, note)
        results[c.id] = chosen or fallback
    return IssueReport(manifest, sessions, results, note)


def short(text, limit=240):
    return text if len(text) <= limit else text[:limit - 3] + "..."


def evidence_text(line):
    log, n, text = line
    return f"{log.name}:{n}  {short(text)}"


def session_span(s):
    return f"{s.start:%Y-%m-%d %H:%M:%S} to {s.end:%H:%M:%S}"


def render_text(report):
    m = report.manifest
    out = [f"#{m.issue}{' ' + m.title if m.title else ''}: {report.verdict}",
           f"  fixed_in {m.fixed_in}; logs: {', '.join(m.logs)}; sessions read: {len(report.sessions)}"]
    if report.note:
        out.append(f"  note: {report.note}")
    for i, s in enumerate(report.sessions, 1):
        state = s.build if s.bound else f"not bound, {s.build}"
        out.append(f"  session {i}: {s.anchor.name}, {session_span(s)}; {state}")
        joined = [f"{log.name} ({log.kind})" for log in s.logs[1:]]
        if joined:
            out.append(f"    also read: {', '.join(joined)}")
        if s.missing:
            out.append(f"    not found for this session: {', '.join(s.missing)}")
        out += [f"    note: {n}" for n in s.notes]
    for c in m.checks:
        r = report.results[c.id]
        where = f"  (session {r.session})" if r.session else ""
        out.append(f"  {r.verdict:<11}  {c.id}{where}")
        out.append(f"      {r.reason}")
        if r.verdict != PASS and c.needs:
            out.append(f"      needs: {c.needs}")
        out += [f"      {evidence_text(line)}" for line in r.evidence]
    if m.human:
        out.append("  still owed by a person (never counted):")
        out += [f"    - {claim}" for claim in m.human]
    return "\n".join(out) + "\n"


def cell(text):
    return str(text).replace("|", "\\|").replace("\n", " ")


def render_md(report):
    m = report.manifest
    out = [f"### In-game verdict for #{m.issue}: {report.verdict}", "",
           f"Read by `tools/ingame_verdict.py`; evidence counts only from a build that contains `{m.fixed_in}`.", ""]
    if report.note:
        out += [cell(report.note), ""]
    if report.sessions:
        out += ["| Session | Logs | Build | Notes |", "|---|---|---|---|"]
        for i, s in enumerate(report.sessions, 1):
            logs = ", ".join(f"`{log.name}`" for log in s.logs)
            state = s.build if s.bound else f"not bound: {s.build}"
            notes = ([f"not found for this session: {', '.join(s.missing)}"] if s.missing else []) + s.notes
            out.append(f"| {i} ({session_span(s)}) | {logs} | {cell(state)} | {cell('; '.join(notes) or '-')} |")
        out.append("")
    out += ["| Check | Verdict | Session | Reason |", "|---|---|---|---|"]
    for c in m.checks:
        r = report.results[c.id]
        reason = r.reason + (f"; needs: {c.needs}" if r.verdict != PASS and c.needs else "")
        out.append(f"| `{c.id}` | {r.verdict} | {r.session or '-'} | {cell(reason)} |")
    lines = [f"{c.id}  {evidence_text(line)}" for c in m.checks for line in report.results[c.id].evidence]
    if lines:
        out += ["", "Evidence:", "", "```text"] + [line.replace("```", "'''") for line in lines] + ["```"]
    if m.human:
        out += ["", "Still owed by a person (never counted):", ""] + [f"- [ ] {claim}" for claim in m.human]
    return "\n".join(out) + "\n"


def fetch_owed(runner):
    rc, out = runner(["gh", "issue", "list", "--state", "all", "--label", LABEL,
                      "--limit", str(LIMIT), "--json", "number"])
    if rc != 0:
        raise VerdictError(f"gh issue list failed (exit {rc}); {HINT}", 3)
    try:
        return [int(row["number"]) for row in json.loads(out)]
    except (ValueError, TypeError, KeyError):
        raise VerdictError(f"gh returned unreadable output; {HINT}", 3)


def main(argv=None, runner=run_command, out=None, err=None, checks_dir=CHECKS_DIR, rgl_dir=RGL_DIR,
         repo=REPO):
    out = out or sys.stdout
    err = err or sys.stderr
    ap = argparse.ArgumentParser(description="Verdicts for owed in-game checks, read from session logs.")
    ap.add_argument("--issue", type=int, nargs="+", action="extend", default=[], metavar="N",
                    help="issues to judge; each needs tools/ingame_checks/<N>.toml")
    ap.add_argument("--owed", action="store_true",
                    help=f"also every issue labelled {LABEL} that has a manifest")
    ap.add_argument("--session", default="newest", metavar="newest|PATH",
                    help="the newest sessions (default) or the one session that starts with this log")
    ap.add_argument("--sessions", type=int, default=1, metavar="K",
                    help="evaluate the K newest sessions; per check the newest PASS or FAIL wins")
    ap.add_argument("--game", help="Bannerlord install (default: the one Directory.Build.props uses)")
    ap.add_argument("--ds-data", help="dedicated server data folder (adds its logs\\ folder)")
    ap.add_argument("--ds-engine", help="dedicated server engine folder (adds its TAOM logs)")
    ap.add_argument("--format", choices=("text", "md"), default="text")
    ap.add_argument("--post", action="store_true",
                    help="comment the markdown report on each issue through gh (public; on Mike's word only)")
    args = ap.parse_args(argv)

    def fail(message, code):
        print(f"ERROR: {message}", file=err)
        return code

    if not args.issue and not args.owed:
        return fail("name an issue with --issue N, or pass --owed", 2)
    if args.sessions < 1:
        return fail("--sessions must be 1 or more", 2)
    if args.session != "newest":
        if args.sessions != 1:
            return fail("--session PATH reads one session; drop --sessions", 2)
        if not Path(args.session).is_file():
            return fail(f"--session {args.session} is not a file", 2)
        if path_kind(args.session) is None:
            return fail(f"--session {Path(args.session).name} is not named like a taom_debug_*.log "
                        "or coop-server-*.log", 2)
    for flag, value in (("--game", args.game), ("--ds-data", args.ds_data), ("--ds-engine", args.ds_engine)):
        if value and not Path(value).is_dir():
            return fail(f"{flag} {value} is not a folder", 2)

    try:
        numbers = set(args.issue)
        no_manifest = []
        if args.owed:
            for n in fetch_owed(runner):
                (numbers.add(n) if (Path(checks_dir) / f"{n}.toml").is_file() else no_manifest.append(n))
        manifests = [load_manifest(Path(checks_dir) / f"{n}.toml", n) for n in sorted(numbers, reverse=True)]
        found, where = find_logs(args.game or default_game(), args.ds_data, args.ds_engine, rgl_dir)
        reader, binder = Reader(), Binder(runner, repo)
        reports = [evaluate_issue(m, found, where, args, reader, binder) for m in manifests]
    except VerdictError as e:
        return fail(str(e), e.code)

    if out is sys.stdout and hasattr(out, "reconfigure"):
        out.reconfigure(errors="backslashreplace")
    render = render_md if args.format == "md" else render_text
    out.write("\n".join(render(r) for r in reports))
    if no_manifest:
        out.write("no manifest: " + ", ".join(f"#{n}" for n in sorted(set(no_manifest), reverse=True)) + "\n")
    if not reports:
        out.write("nothing judged: no selected issue has a manifest\n")
        return 4
    if args.post:
        for r in reports:
            rc, _ = runner(["gh", "issue", "comment", str(r.manifest.issue), "--body-file", "-"],
                           input=scrub(render_md(r)))
            if rc != 0:
                return fail(f"gh issue comment {r.manifest.issue} failed (exit {rc}); {HINT}", 3)
    verdicts = [r.verdict for r in reports]
    if FAIL in verdicts:
        return 1
    return 0 if all(v == PASS for v in verdicts) else 4


if __name__ == "__main__":
    sys.exit(main())
