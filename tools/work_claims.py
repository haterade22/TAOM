"""Work claims (#1248): one live session owns an issue, a red test or another work item at a time.

Two sessions once ran the same fix of a red trunk at the same time, one in a sibling worktree and one in the main checkout, because neither could see the other's task. A session now
claims each task and each sub-task before its first edit:

    python tools/work_claims.py claim 1245 --note "trunk red: language rows"
    python tools/work_claims.py check 1245        exit 3 when another live session holds it
    python tools/work_claims.py release 1245      after the push
    python tools/work_claims.py list [--others] [--brief]

A key is an issue number alone (1245, #1245, issue-1245; "#1245: notes" is another key) or a short name ("trunk: LanguageDataXmlTests"). Claims are JSON files in
<git common dir>/taom-work-claims/, which every worktree of the checkout shares and git never tracks. A claim names the session (CLAUDE_CODE_SESSION_ID, CLAUDE_PID and the name in
~/.claude/sessions/<pid>.json), the worktree, the branch, the start and a note. A claim is live while the registry file of its pid names its session and that process runs; a claim whose
session ended is taken over with a note. Only the holder (or anyone, once the holder ended) releases a claim. Never read the registry's .key files: this tool reads <pid>.json only.
TAOM_CLAIMS_REGISTRY names another registry folder (the hook tests use it).

Exit codes: 0 done; 1 the claim store could not be written or read; 2 usage, an unusable key, no session or no git checkout; 3 another live session holds the key; 4 UNCONFIRMED: the
claim was written but the registry does not name this session, so other sessions read it as ended, or (list) there is no registry to judge claims by.
"""

import argparse
import json
import os
import re
import subprocess
import sys
import time
from dataclasses import dataclass
from pathlib import Path

HELD = 3
NO_SESSION = 2
UNCONFIRMED = 4
STORE_ERROR = 1


@dataclass(frozen=True)
class Me:
    session_id: str
    pid: int
    name: str
    worktree: str
    branch: str


@dataclass(frozen=True)
class Result:
    code: int
    message: str
    claim: dict | None = None


def normalize_key(raw):
    """The file-safe key: "issue-<n>" for an issue number in any form, else the text folded to lower case with every run of other characters as one hyphen."""
    text = (raw or "").strip().lower()
    issue = re.fullmatch(r"(?:issue-|#)?\s*(\d+)", text)
    if issue:
        return "issue-" + issue.group(1)
    key = re.sub(r"[^a-z0-9._]+", "-", text).strip("-.")
    key = re.sub(r"-{2,}", "-", key)[:80].strip("-.")
    if not key:
        raise ValueError(f"no usable key in {raw!r}")
    return key


def shown_key(key):
    return "#" + key[len("issue-"):] if key.startswith("issue-") else key


def pid_running(pid):
    """Whether a process with this pid runs."""
    if pid <= 0:
        return False
    if os.name == "nt":
        import ctypes

        k32 = ctypes.WinDLL("kernel32", use_last_error=True)
        k32.OpenProcess.restype = ctypes.c_void_p  # a HANDLE: the default int would cut a 64-bit value
        k32.GetExitCodeProcess.argtypes = (ctypes.c_void_p, ctypes.POINTER(ctypes.c_ulong))
        k32.CloseHandle.argtypes = (ctypes.c_void_p,)
        handle = k32.OpenProcess(0x1000, False, pid)  # PROCESS_QUERY_LIMITED_INFORMATION
        if not handle:
            return ctypes.get_last_error() == 5  # ERROR_ACCESS_DENIED: the process exists (an elevated one)
        code = ctypes.c_ulong()
        ok = k32.GetExitCodeProcess(handle, ctypes.byref(code))
        k32.CloseHandle(handle)
        return bool(ok) and code.value == 259  # STILL_ACTIVE
    try:
        os.kill(pid, 0)
    except OSError:
        return False
    return True


def _registry_row(registry, pid):
    try:
        row = json.loads((Path(registry) / f"{int(pid)}.json").read_text(encoding="utf-8"))
    except (OSError, ValueError, TypeError):
        return None
    return row if isinstance(row, dict) else None


def is_live(claim, registry, pid_alive=pid_running):
    """A claim is live while the registry file of its pid names its session and that process runs."""
    pid = claim.get("pid")
    if not isinstance(pid, int) or not pid_alive(pid):
        return False
    row = _registry_row(registry, pid)
    return row is not None and row.get("sessionId") == claim.get("session")


def _read(path):
    try:
        claim = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return None
    return claim if isinstance(claim, dict) and isinstance(claim.get("session"), str) else None


def _write(path, claim):
    path.parent.mkdir(parents=True, exist_ok=True)
    tmp = path.with_name(f"{path.name}.{os.getpid()}.tmp")
    tmp.write_text(json.dumps(claim, indent=1) + "\n", encoding="utf-8")
    os.replace(tmp, path)


def describe(claim):
    when = time.strftime("%Y-%m-%d %H:%M UTC", time.gmtime(claim.get("since", 0)))
    where = claim.get("worktree", "?") + (f" ({claim['branch']})" if claim.get("branch") else "")
    note = f": {claim['note']}" if claim.get("note") else ""
    sid = str(claim.get("session", "?"))[:8]
    who = f"{claim['name']} ({sid})" if claim.get("name") else sid
    return f"{shown_key(claim.get('key', '?'))} held by {who} in {where} since {when}{note}"


def holder(raw_key, me, store, registry, pid_alive=pid_running):
    """The live claim another session holds on the key, else None."""
    claim = _read(Path(store) / f"{normalize_key(raw_key)}.json")
    if claim is None or claim.get("session") == me.session_id or not is_live(claim, registry, pid_alive):
        return None
    return claim


def claim(raw_key, me, store, registry, now, note="", pid_alive=pid_running):
    """Takes the key for this session, keeps its start when this session holds it already, and refuses it while another live session holds it."""
    if not me.session_id or me.pid <= 0:
        return Result(NO_SESSION, "no session: CLAUDE_CODE_SESSION_ID and CLAUDE_PID are not set, so a claim cannot name its owner")
    key = normalize_key(raw_key)
    path = Path(store) / f"{key}.json"
    old = _read(path)
    taken_from = ""
    since = now
    if old is not None:
        if old.get("session") == me.session_id:
            since = old.get("since", now)
        elif is_live(old, registry, pid_alive):
            return Result(HELD, "HELD: " + describe(old) + ". Do not start it: ask that session (SendMessage) or Mike.", old)
        else:
            taken_from = f" (taken over from {old.get('name') or old.get('session', '?')[:8]}, whose session ended)"
    new = {"key": key, "session": me.session_id, "pid": me.pid, "name": me.name, "worktree": me.worktree, "branch": me.branch, "since": since, "note": note}
    _write(path, new)
    if not is_live(new, registry, pid_alive):
        return Result(UNCONFIRMED, f"claimed {shown_key(key)} but UNCONFIRMED: {Path(registry) / f'{me.pid}.json'} does not name this session, so other sessions read the claim as ended. "
                      "Tell Mike; name the task in your messages to other sessions.", new)
    return Result(0, "claimed " + describe(new) + taken_from, new)


def release(raw_key, me, store, registry, pid_alive=pid_running):
    key = normalize_key(raw_key)
    path = Path(store) / f"{key}.json"
    old = _read(path)
    if old is not None and old.get("session") != me.session_id and is_live(old, registry, pid_alive):
        return Result(HELD, "HELD: " + describe(old) + ". Only its holder releases it.", old)
    try:
        path.unlink()
    except FileNotFoundError:
        pass
    return Result(0, f"released {shown_key(key)}")


def live_claims(store, registry, pid_alive=pid_running):
    claims = []
    for path in sorted(Path(store).glob("*.json")) if Path(store).is_dir() else []:
        found = _read(path)
        if found is not None and is_live(found, registry, pid_alive):
            claims.append(found)
    return claims


def brief_lines(claims, me=None):
    """One line per claim, leaving out this session's own when me is given."""
    return ["CLAIM: " + describe(c) for c in claims if me is None or c.get("session") != me.session_id]


def _git(*args):
    try:
        out = subprocess.run(["git", *args], capture_output=True, text=True, timeout=10, check=False)
    except (OSError, subprocess.SubprocessError):
        return ""
    return out.stdout.strip() if out.returncode == 0 else ""


def default_store():
    common = _git("rev-parse", "--path-format=absolute", "--git-common-dir")
    return Path(common) / "taom-work-claims" if common else None


def default_registry():
    override = os.environ.get("TAOM_CLAIMS_REGISTRY", "")
    return Path(override) if override else Path.home() / ".claude" / "sessions"


def current_me(registry):
    sid = re.sub(r"[^A-Za-z0-9_-]", "", os.environ.get("CLAUDE_CODE_SESSION_ID", ""))[:64]
    try:
        pid = int(os.environ.get("CLAUDE_PID", "0"))
    except ValueError:
        pid = 0
    row = _registry_row(registry, pid) if pid > 0 else None
    name = row.get("name", "") if row and row.get("sessionId") == sid else ""
    worktree = _git("rev-parse", "--show-toplevel") or os.getcwd().replace("\\", "/")
    return Me(session_id=sid, pid=pid, name=name if isinstance(name, str) else "", worktree=worktree, branch=_git("branch", "--show-current"))


def main(argv=None, me=None, store=None, registry=None, pid_alive=pid_running, now=None):
    parser = argparse.ArgumentParser(description="Claim a task or sub-task so no other session starts it (#1248).")
    sub = parser.add_subparsers(dest="cmd", required=True)
    for name in ("claim", "check", "release"):
        p = sub.add_parser(name)
        p.add_argument("key")
        if name == "claim":
            p.add_argument("--note", default="")
    p = sub.add_parser("list")
    p.add_argument("--others", action="store_true", help="leave out this session's own claims")
    p.add_argument("--brief", action="store_true", help="one CLAIM: line each, nothing when there are none")
    p.add_argument("--session", default="", help="the session to leave out, when the environment has none (a hook passes its payload's session_id)")
    args = parser.parse_args(argv)

    registry = registry or default_registry()
    store = store or default_store()
    if store is None:
        print("work_claims: not inside a git checkout", file=sys.stderr)
        return NO_SESSION
    me = me or current_me(registry)
    if getattr(args, "session", ""):
        me = Me(session_id=re.sub(r"[^A-Za-z0-9_-]", "", args.session)[:64], pid=me.pid, name=me.name, worktree=me.worktree, branch=me.branch)
    now = int(time.time()) if now is None else now

    try:
        if args.cmd == "claim":
            result = claim(args.key, me, store, registry, now, note=args.note, pid_alive=pid_alive)
        elif args.cmd == "release":
            result = release(args.key, me, store, registry, pid_alive=pid_alive)
        elif args.cmd == "check":
            held = holder(args.key, me, store, registry, pid_alive=pid_alive)
            own = _read(Path(store) / f"{normalize_key(args.key)}.json")
            if held:
                result = Result(HELD, "HELD: " + describe(held), held)
            elif own is not None and me.session_id and own.get("session") == me.session_id:
                result = Result(0, "held by you: " + describe(own), own)
            else:
                result = Result(0, f"free: {shown_key(normalize_key(args.key))}")
        else:
            if not Path(registry).is_dir():
                print(f"NOTE: work claims UNCHECKED (no session registry at {registry})")
                return UNCONFIRMED
            lines = brief_lines(live_claims(store, registry, pid_alive), me if args.others else None)
            if lines or not args.brief:
                print("\n".join(lines) if lines else "no live claims")
            return 0
    except ValueError as e:
        print(f"work_claims: {e}", file=sys.stderr)
        return NO_SESSION
    except OSError as e:
        print(f"work_claims: {e}", file=sys.stderr)
        return STORE_ERROR
    print(result.message)
    return result.code


if __name__ == "__main__":
    sys.exit(main())
