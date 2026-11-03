#!/usr/bin/env python3
"""Run one Fords of War match in the real game with nobody at the keyboard, then collect the self-check report.

Run:  python tools/run_fords_autorun.py [--scene S] [--west W] [--east E] [--ai A] [--live-seconds N]
                                        [--wall-limit N] [--west-ai] [--speed 1|2|4|10] [--runs N] [--runs-root DIR]
                                        [--require-idle-minutes N] [--dry-run]
Test: python -m unittest tools.tests.test_run_fords_autorun

How it works. TAOM's client submodule polls an autorun driver from OnApplicationTick (docs/features/fords-of-war.md, "Autorun").
The driver is armed only by the environment variable TAOM_FORDS_AUTORUN, which this tool sets to the path of an arm file it writes
into the run folder. The driver waits for the main menu, starts the match, ends it after live_seconds of mission time through the
panel button's exit, waits for the final=1 report pass, writes fords_autorun_done.txt into the run folder and quits the game.
This tool launches Bannerlord.exe directly with the module list of Mike's F5 profile (Main/Properties/launchSettings.json, profile
"Bannerlord"), watches for the done file and for crashes, then copies the newest TAOM log and the JSON report it names into the run
folder (TAOM prunes them: 20 reports, about 30 logs).

It refuses to start while Bannerlord.exe or the game folder's Watchdog.exe already runs, which includes an open Modding Kit, and it refuses when it cannot list the
running processes. It also refuses to start while the PC has had keyboard or mouse input within the last --require-idle-minutes
(default 10; 0 disables the gate): the run takes the screen and the GPU, so it only starts unasked on an idle PC (the maintainer's
rule of 2026-11-01, docs/reference/unattended-game-runs.md). The input age comes from user32 GetLastInputInfo against kernel32
GetTickCount; an unreadable age refuses too. Input this tool posts to the game's dialog does not count as user input. It never kills anything but the Bannerlord.exe it started (through its Popen handle) and a Watchdog.exe whose
command line holds "-p <that pid>". It resets "safely_exited = 0" to 1 in engine_config.txt before the start and after any kill,
because a killed game leaves 0 and the next start then stops at a Safe Mode window. It does not touch BannerlordConfig.txt (it only
logs StopGameOnFocusLost) or the shader cache.

It answers exactly one game dialog: the engine's "Mod change detected" notice ("A change in your active mods was detected.
Deleting the runtime shader cache..."), which has a single OK button and blocks the start before the graphics device whenever the
shader cache was built for another module list. The answer is a posted WM_COMMAND for the OK button (no window activation, no wait),
logged once per window. Its OK only lets the engine rebuild the cache. A dialog whose title names a crash ends the run as a failure;
any other dialog is logged once and left alone.

Exit code: 0 when the done file says reason=final, the JSON report says final=true with no FAIL row, the TAOM log holds no
[CrashReport] error block from the run's start to the end of the log (a quit-time capture counts), and the game then exited by
itself with code 0 or -1 and no crash marker;
1 for anything else (preflight refused, crash, kill, timeout, stall, start_refused, never_live, wall_limit, ...). --dry-run prints
the plan, the idle state and the preflight, starts nothing.
"""
import argparse
import json
import math
import os
import re
import shutil
import struct
import subprocess
import sys
import time
import zlib
from datetime import datetime
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from _gamedir import DEFAULT_GAME_ROOT, game_dir  # noqa: E402

ENV_VAR = "TAOM_FORDS_AUTORUN"
DEFAULT_IDLE_MINUTES = 10.0
LAUNCH_SETTINGS = Path(__file__).resolve().parent.parent / "Main" / "Properties" / "launchSettings.json"
RUNS_ROOT = r"E:\Temp\fords-runs"
LOGS_DIR = r"C:\ProgramData\Mount and Blade II Bannerlord\logs"
DONE_FILE = "fords_autorun_done.txt"
STATUS_FILE = "fords_autorun_status.txt"
DEFAULT_WALL_LIMIT_SECONDS = 2400.0
POLL_SECONDS = 5
SCAN_SECONDS = 30
STALL_SECONDS = 600
OVERTIME_SECONDS = 15 * 60
EXIT_GRACE_SECONDS = 120
KILL_WAIT_SECONDS = 30
LOG_SLACK_SECONDS = 2
MOD_CHANGE_TITLE = "Mod change detected"
MOD_CHANGE_TEXT = "A change in your active mods was detected"
CRASH_TITLE = re.compile(r"problem|crash|fatal|exception", re.IGNORECASE)
NORMAL_EXIT_CODES = (0, -1, 4294967295)
CRASH_REPORT_LINE = re.compile(r"^\[(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2})\] \[ERROR\] \[CrashReport\]")
WM_COMMAND = 0x0111
PROCESS_LISTING_ERRORS = (OSError, ValueError, subprocess.TimeoutExpired)


# ---- pure helpers (unit-tested) -------------------------------------------------------------------------------------------

def launch_modules(path=LAUNCH_SETTINGS):
    """The module ids of Mike's F5 profile ("Bannerlord" in launchSettings.json), $(ModuleId) read as TAOM, in launch order."""
    settings = json.loads(Path(path).read_text(encoding="utf-8"))
    try:
        command_line = settings["profiles"]["Bannerlord"]["commandLineArgs"]
    except (KeyError, TypeError) as error:
        raise ValueError(f"{path} has no profile 'Bannerlord' with commandLineArgs ({error})") from error
    match = re.search(r"_MODULES_\*(.+)\*_MODULES_", command_line)
    if match is None:
        raise ValueError(f"{path}: the Bannerlord profile's commandLineArgs holds no _MODULES_*...*_MODULES_ list")
    return match.group(1).replace("$(ModuleId)", "TAOM").split("*")


def modules_arg(modules):
    return "_MODULES_*" + "*".join(modules) + "*_MODULES_"


def arm_text(out, west=None, east=None, ai=None, scene=None, live_seconds=None, live_wait_seconds=None, wall_limit_seconds=None,
             quit_when_done=True, west_ai=False, screenshots=None, speed=None):
    """The arm file the in-game FordsAutorunPlan parses: UTF-8 key=value lines, LF. Unset choices are left out (the game defaults them).
    west_ai puts an AI commander on the West side too, so both sides play. speed is the match speed from the first frame."""
    pairs = [("west", west), ("east", east), ("ai", ai), ("scene", scene), ("live_seconds", live_seconds),
             ("live_wait_seconds", live_wait_seconds), ("wall_limit_seconds", wall_limit_seconds),
             ("out", out), ("quit", 1 if quit_when_done else 0), ("west_ai", 1 if west_ai else None), ("screenshots", screenshots), ("speed", speed)]
    return "".join(f"{key}={value}\n" for key, value in pairs if value is not None and str(value) != "")


def arm_text_for(out, args):
    """(arm file text, wall limit). The runner's wall limit is always written, so the game and this tool never disagree on it."""
    wall_limit = args.wall_limit if args.wall_limit else DEFAULT_WALL_LIMIT_SECONDS
    text = arm_text(str(out), args.west, args.east, args.ai, args.scene, args.live_seconds, None, wall_limit, west_ai=args.west_ai, screenshots=getattr(args, "screenshots", None),
                    speed=getattr(args, "speed", None))
    return text, wall_limit


def write_arm_file(folder, args):
    """Writes arm.txt (UTF-8, LF) into the run folder. Returns (path, wall limit)."""
    text, wall_limit = arm_text_for(folder, args)
    arm = Path(folder) / "arm.txt"
    arm.write_text(text, encoding="utf-8", newline="\n")
    return arm, wall_limit


def resolved_runs_root(args):
    """The runs folder as an absolute path: the game rejects a relative out= folder."""
    return Path(args.runs_root).resolve()


def rewrite_safely_exited(data):
    """(new bytes, changed): 'safely_exited = 0' becomes 'safely_exited = 1', every other byte (CRLF included) untouched."""
    old, new = b"safely_exited = 0", b"safely_exited = 1"
    if old not in data:
        return data, False
    return data.replace(old, new), True


def rgl_crashed(text):
    """The engine's own crash marker in rgl_log_<pid>.txt."""
    return "Unhandled Exception Code" in text


def watchdog_crashed(text):
    """The watchdog's crash marker in watchdog_log_<pid>.txt."""
    return "Crash occurred" in text


def watchdog_targets_pid(command_line, pid):
    """True when a Watchdog.exe command line holds '-p <pid>' (the pid token, optionally quoted, not a longer number)."""
    if not command_line:
        return False
    return re.search(r'(^|\s)-p\s+"?' + re.escape(str(pid)) + r'"?(\s|$)', command_line) is not None


def pick_newest_log(candidates, started_epoch, slack=LOG_SLACK_SECONDS):
    """candidates: [(path, created_epoch)]. The newest log created at or after the start minus slack seconds, else None."""
    fresh = [(created, str(path)) for path, created in candidates if created >= started_epoch - slack]
    return max(fresh)[1] if fresh else None


def parse_done(text):
    """fords_autorun_done.txt: one key=value per line (the value may hold spaces and '='); a line without '=' is skipped."""
    done = {}
    for line in text.replace("\r", "").split("\n"):
        key, sep, value = line.partition("=")
        if sep and key.strip():
            done[key.strip()] = value.strip()
    return done


def final_verdict_lines(log_text):
    """The [FordsOfWar][Verdict] lines of the final=1 pass (16 rows and the total)."""
    return [line for line in log_text.splitlines() if "[FordsOfWar][Verdict]" in line and re.search(r"\bfinal=1\b", line)]


def read_shader_list(data):
    """(build, module list) from shader_mapping.bin: u32 format, u32 n + build, u32 m + ';'-joined module ids. None when it does not parse."""
    try:
        (build_len,) = struct.unpack_from("<I", data, 4)
        build = data[8:8 + build_len].decode("ascii")
        (list_len,) = struct.unpack_from("<I", data, 8 + build_len)
        modules = data[12 + build_len:12 + build_len + list_len].decode("ascii")
        return build, modules
    except (struct.error, UnicodeDecodeError):
        return None


def expected_shader_list(modules):
    """The module ids sorted ordinally and joined with ';', the form the engine stores."""
    return ";".join(sorted(modules))


def dialog_action(title, texts):
    """What to do with a dialog window (#32770) of the game: 'dismiss' for the engine's mod-change notice (that title and its
    shader cache text), 'fail' for a title that names a crash, else 'leave' (logged once, never answered)."""
    if title == MOD_CHANGE_TITLE and any(MOD_CHANGE_TEXT in text for text in texts):
        return "dismiss"
    if CRASH_TITLE.search(title or ""):
        return "fail"
    return "leave"


def local_time(iso_text):
    """The local wall time of an ISO stamp such as 2026-10-31T22:08:36.9869565-05:00, to the second and without the offset
    (the TAOM log stamps local time too). None when it does not parse."""
    try:
        return datetime.strptime((iso_text or "")[:19], "%Y-%m-%dT%H:%M:%S")
    except ValueError:
        return None


def crash_report_line(log_text, started):
    """The first [ERROR] [CrashReport] line of the TAOM log stamped at or after the run's start, else None. There is no upper
    bound: the driver writes the done file before it quits, so a capture during the quit comes after the done time and still
    belongs to this run. The INFO and WARNING [CrashReport] lines are startup notes. A start that does not parse is open."""
    first = local_time(started)
    for line in (log_text or "").splitlines():
        match = CRASH_REPORT_LINE.match(line)
        if match is None:
            continue
        when = datetime.strptime(match.group(1), "%Y-%m-%d %H:%M:%S")
        if first is None or when >= first:
            return line
    return None


def judge(done, report_json, log_text=None):
    """(exit code, why). Success is reason=final, a JSON report that says final=true with no FAIL row, and no [CrashReport] error
    block in the TAOM log from the run's start to its end (a swallowed engine exception passes every in-game check)."""
    if not done:
        return 1, "no done file"
    reason = done.get("reason", "")
    if reason != "final":
        return 1, f"reason={reason or '?'}"
    if report_json is None:
        return 1, "reason=final but the JSON report could not be read"
    if report_json.get("final") is not True:
        return 1, "the JSON report is not final"
    failed = [row for row in report_json.get("verdicts", []) if str(row.get("Result", "")).lower() == "fail"]
    if failed:
        names = ", ".join(f"check {int(row.get('Check', 0))} {row.get('Title', '')}".strip() for row in failed)
        return 1, f"the report has FAIL rows: {names}"
    block = crash_report_line(log_text, done.get("started"))
    if block is not None:
        return 1, "the TAOM log holds a CrashReport block during the run: " + block[:200]
    return 0, "reason=final, report final=true, no FAIL row, no CrashReport block"


def final_verdict(judged, crash, killed, exit_code=None):
    """(exit code, why) after the game has ended: a passing run still fails when a crash marker showed up after the done file, the
    game had to be killed because it did not exit by itself, or it exited with a code other than 0 or -1 (Bannerlord's normal
    code after its own quit, 4294967295 as a Windows exit code)."""
    code, _ = judged
    if code != 0:
        return judged
    if crash:
        return 1, "crash after the done file: " + crash
    if killed:
        return 1, "the game did not exit by itself after the done file and was killed"
    if exit_code is not None and exit_code not in NORMAL_EXIT_CODES:
        return 1, f"the game exited with code 0x{exit_code & 0xFFFFFFFF:08X} after the done file"
    return judged


def idle_ok(idle_seconds, required_minutes):
    """True when the PC has been idle long enough. A gate of 0 minutes is off. An unknown idle time (None) fails a gate that is on."""
    if required_minutes <= 0:
        return True
    if idle_seconds is None:
        return False
    return idle_seconds >= required_minutes * 60


def tick_delta_ms(now_tick, last_tick):
    """now minus last for two 32-bit millisecond tick counts (GetTickCount wraps every 49.7 days)."""
    return (now_tick - last_tick) & 0xFFFFFFFF


def idle_state_text(idle_seconds, required_minutes):
    """One line for --dry-run and the runner log: the input age against the gate."""
    if required_minutes <= 0:
        gate = "the gate is off (--require-idle-minutes 0)"
    elif idle_seconds is None:
        gate = f"the gate needs {required_minutes * 60:g} s: NOT idle (an unreadable age fails closed)"
    else:
        gate = f"the gate needs {required_minutes * 60:g} s: " + ("ok" if idle_ok(idle_seconds, required_minutes) else "NOT idle")
    if idle_seconds is None:
        return "input age unreadable; " + gate
    return f"input {idle_seconds:.0f} s ago; " + gate


def idle_refusal(idle_seconds, required_minutes):
    """The preflight reason for an idle gate that does not pass."""
    return (f"the PC had input {idle_seconds:.0f} s ago; the idle gate needs {required_minutes * 60:g} s; "
            "rerun with --require-idle-minutes 0 when the maintainer has said yes")


def positive_seconds(text):
    value = float(text)
    if math.isnan(value) or math.isinf(value) or value <= 0:
        raise argparse.ArgumentTypeError(f"'{text}' is not a positive number of seconds")
    return value


def non_negative_minutes(text):
    value = float(text)
    if math.isnan(value) or math.isinf(value) or value < 0:
        raise argparse.ArgumentTypeError(f"'{text}' is not a number of minutes (0 or more)")
    return value


# ---- machine access ---------------------------------------------------------------------------------------------------------

def powershell(command, timeout=60):
    """The stripped stdout. A non-zero exit raises OSError, so a failed query is never read as an empty answer."""
    result = subprocess.run(["powershell", "-NoProfile", "-NonInteractive", "-Command", command],
                            capture_output=True, text=True, timeout=timeout)
    if result.returncode != 0:
        raise OSError(f"powershell exited {result.returncode}: {(result.stderr or '').strip()[:200]}")
    return result.stdout.strip()


def my_documents():
    try:
        out = powershell("[Environment]::GetFolderPath('MyDocuments')")
    except PROCESS_LISTING_ERRORS:
        out = ""
    return Path(out) if out else Path.home() / "Documents"


def list_processes(names):
    """[{pid, name, path, cmd, created}] for processes with one of the names; created is an ISO local time."""
    where = " OR ".join(f"Name='{n}'" for n in names)
    command = (f'Get-CimInstance Win32_Process -Filter "{where}" | ForEach-Object {{ $c = ""; '
               "if ($_.CreationDate) { $c = $_.CreationDate.ToString('o') }; "
               "[pscustomobject]@{pid=[int]$_.ProcessId;name=$_.Name;path=$_.ExecutablePath;cmd=$_.CommandLine;created=$c} } "
               "| ConvertTo-Json -Compress")
    out = powershell(command)
    if not out:
        return []
    data = json.loads(out)
    return data if isinstance(data, list) else [data]


def kill_pid(pid):
    subprocess.run(["taskkill", "/PID", str(pid), "/F"], capture_output=True, text=True)


def _user32():
    """user32 with explicit argtypes and restypes: a handle is pointer sized, and ctypes would pass an untyped one as a 32-bit int."""
    import ctypes
    from ctypes import wintypes
    user32 = ctypes.WinDLL("user32", use_last_error=True)
    callback = ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)
    user32.EnumWindows.argtypes = [callback, wintypes.LPARAM]
    user32.EnumWindows.restype = wintypes.BOOL
    user32.EnumChildWindows.argtypes = [wintypes.HWND, callback, wintypes.LPARAM]
    user32.EnumChildWindows.restype = wintypes.BOOL
    user32.GetWindowThreadProcessId.argtypes = [wintypes.HWND, ctypes.POINTER(wintypes.DWORD)]
    user32.GetWindowThreadProcessId.restype = wintypes.DWORD
    user32.IsWindowVisible.argtypes = [wintypes.HWND]
    user32.IsWindowVisible.restype = wintypes.BOOL
    user32.GetClassNameW.argtypes = [wintypes.HWND, wintypes.LPWSTR, ctypes.c_int]
    user32.GetClassNameW.restype = ctypes.c_int
    user32.GetWindowTextW.argtypes = [wintypes.HWND, wintypes.LPWSTR, ctypes.c_int]
    user32.GetWindowTextW.restype = ctypes.c_int
    user32.GetDlgCtrlID.argtypes = [wintypes.HWND]
    user32.GetDlgCtrlID.restype = ctypes.c_int
    user32.GetDlgItem.argtypes = [wintypes.HWND, ctypes.c_int]
    user32.GetDlgItem.restype = wintypes.HWND
    user32.PostMessageW.argtypes = [wintypes.HWND, wintypes.UINT, wintypes.WPARAM, wintypes.LPARAM]
    user32.PostMessageW.restype = wintypes.BOOL
    return user32


def _input_libs():
    """(user32, kernel32) with explicit argtypes and restypes for the two calls the idle gate makes."""
    import ctypes
    from ctypes import wintypes
    user32 = ctypes.WinDLL("user32", use_last_error=True)
    kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
    user32.GetLastInputInfo.argtypes = [ctypes.c_void_p]
    user32.GetLastInputInfo.restype = wintypes.BOOL
    kernel32.GetTickCount.argtypes = []
    kernel32.GetTickCount.restype = wintypes.DWORD
    return user32, kernel32


def seconds_since_last_input():
    """Seconds since the last keyboard or mouse input of the interactive session (GetLastInputInfo.dwTime against GetTickCount, both
    32-bit tick counts, so the difference is taken modulo 2**32). Raises OSError when the query is refused: never read as idle."""
    import ctypes
    from ctypes import wintypes

    class LastInputInfo(ctypes.Structure):
        _fields_ = [("cbSize", wintypes.UINT), ("dwTime", wintypes.DWORD)]

    user32, kernel32 = _input_libs()
    info = LastInputInfo()
    info.cbSize = ctypes.sizeof(LastInputInfo)
    if not user32.GetLastInputInfo(ctypes.byref(info)):
        raise OSError(f"GetLastInputInfo failed (error {ctypes.get_last_error()})")
    return tick_delta_ms(kernel32.GetTickCount(), info.dwTime) / 1000.0


def current_idle_seconds():
    """(idle seconds or None, error text or None): seconds_since_last_input() with its failure turned into a value."""
    try:
        return seconds_since_last_input(), None
    except (OSError, AttributeError, ValueError) as error:
        return None, f"{type(error).__name__}: {error}"


def game_dialogs(pid):
    """[(hwnd, title, child texts, {button text: control id})] for the visible dialog windows (#32770) of the process."""
    import ctypes
    from ctypes import wintypes
    user32 = _user32()
    proc_type = ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)

    def text_of(hwnd, getter):
        buffer = ctypes.create_unicode_buffer(2048)
        getter(hwnd, buffer, 2048)
        return buffer.value

    found = []

    def top(hwnd, _):
        owner = wintypes.DWORD()
        user32.GetWindowThreadProcessId(hwnd, ctypes.byref(owner))
        if owner.value != pid or not user32.IsWindowVisible(hwnd) or text_of(hwnd, user32.GetClassNameW) != "#32770":
            return True
        texts, buttons = [], {}

        def child(handle, _):
            text = text_of(handle, user32.GetWindowTextW)
            texts.append(text)
            if text_of(handle, user32.GetClassNameW) == "Button":
                buttons[text] = user32.GetDlgCtrlID(handle)
            return True

        user32.EnumChildWindows(hwnd, proc_type(child), 0)
        found.append((hwnd, text_of(hwnd, user32.GetWindowTextW), texts, buttons))
        return True

    user32.EnumWindows(proc_type(top), 0)
    return found


def click_button(hwnd, control_id):
    """Presses a dialog button by posting WM_COMMAND (control id, BN_CLICKED = 0; the button handle as lParam) to the dialog: no
    window activation and no wait on a dialog that blocks its own thread. True when the button exists and the post was accepted."""
    user32 = _user32()
    button = user32.GetDlgItem(hwnd, control_id)
    if not button:
        return False
    return bool(user32.PostMessageW(hwnd, WM_COMMAND, control_id, button))


def handle_dialogs(pid, seen, say):
    """Dismisses the mod-change notice, reports a crash dialog (returns its text), logs any other dialog once. A dismissed window
    is logged once; the click is posted again on the next poll while the window lingers."""
    try:
        dialogs = game_dialogs(pid)
    except (OSError, AttributeError) as error:
        say(f"dialog check failed ({error})")
        return None
    for hwnd, title, texts, buttons in dialogs:
        action = dialog_action(title, texts)
        if action == "fail":
            return f"crash dialog '{title}': {' | '.join(t for t in texts if t)}"
        if action == "dismiss" and "OK" in buttons:
            if click_button(hwnd, buttons["OK"]) and hwnd not in seen:
                seen.add(hwnd)
                say(f"dismissed the engine notice '{title}' (the shader cache is rebuilt for this module list)")
        elif hwnd not in seen:
            seen.add(hwnd)
            say(f"dialog left open: '{title}': {' | '.join(t for t in texts if t)}")
    return None


class Runner:
    def __init__(self, folder):
        self.folder = Path(folder)
        self.log_path = self.folder / "runner_log.txt"

    def say(self, message):
        line = f"{datetime.now():%H:%M:%S} {message}"
        print(line, flush=True)
        with open(self.log_path, "a", encoding="utf-8") as handle:
            handle.write(line + "\n")


KIT_PROCESS = "TaleWorlds.MountAndBlade.Launcher.exe"
KIT_FOLDER = "win64_shipping_weditor"


def watches_the_kit(proc, running):
    """True for a Watchdog.exe whose target is the Modding Kit: the launcher process from the wEditor folder, never the game's."""
    if proc["name"].lower() != "watchdog.exe":
        return False
    return any(other["name"].lower() == KIT_PROCESS.lower() and KIT_FOLDER in (other.get("path") or "").lower()
               and watchdog_targets_pid(proc.get("cmd") or "", other["pid"]) for other in running)


def preflight(game, require_idle_minutes=0):
    """A list of reasons to refuse; empty means go. A process listing or an input age that cannot be read refuses too: not knowing
    is not a go. require_idle_minutes is the idle gate (0 is off; the command line defaults it to DEFAULT_IDLE_MINUTES)."""
    problems = []
    if require_idle_minutes > 0:
        idle, error = current_idle_seconds()
        if idle is None:
            problems.append(f"could not read the input idle time ({error}); refusing to start blind")
        elif not idle_ok(idle, require_idle_minutes):
            problems.append(idle_refusal(idle, require_idle_minutes))
    exe = Path(game) / "bin" / "Win64_Shipping_Client" / "Bannerlord.exe"
    if not exe.is_file():
        problems.append(f"{exe} is missing (set BANNERLORD_GAME_DIR)")
    prefix = str(game).lower().rstrip("\\/")
    try:
        running = list_processes(["Bannerlord.exe", "Watchdog.exe", KIT_PROCESS])
    except PROCESS_LISTING_ERRORS as error:
        problems.append(f"could not list the running processes ({type(error).__name__}: {error}); refusing to start blind")
        return problems
    for proc in running:
        if proc["name"].lower() == KIT_PROCESS.lower():
            continue
        path = (proc.get("path") or "").lower()
        if proc["name"].lower() == "bannerlord.exe" or path.startswith(prefix):
            if watches_the_kit(proc, running):
                problems.append(f"the Modding Kit is open (its {proc['name']} pid {proc['pid']}): it shares the shader cache, and starting a game beside it can crash either one")
            else:
                problems.append(f"{proc['name']} pid {proc['pid']} is already running")
    return problems


def reset_safely_exited(config_path, say):
    try:
        data = Path(config_path).read_bytes()
    except OSError as error:
        say(f"engine_config.txt not readable ({error}); safely_exited not reset")
        return
    new, changed = rewrite_safely_exited(data)
    if changed:
        try:
            Path(config_path).write_bytes(new)
        except OSError as error:
            say(f"engine_config.txt not written ({error}); safely_exited still 0")
            return
        say("safely_exited reset: was 0, now 1")
    elif b"safely_exited = 1" in data:
        say("safely_exited reset: already 1")
    else:
        say("safely_exited key not found in engine_config.txt; nothing reset")


def inspect_environment(docs, game, modules, say):
    configs = docs / "Mount and Blade II Bannerlord" / "Configs"
    try:
        text = (configs / "BannerlordConfig.txt").read_text(encoding="utf-8", errors="replace")
        match = re.search(r"StopGameOnFocusLost\s*=\s*(\S+)", text)
        say("BannerlordConfig StopGameOnFocusLost=" + (match.group(1) if match else "(absent)")
            + " (an armed run ignores it: its panel view declines the Escape menu that a lost focus would open)")
    except OSError:
        say("BannerlordConfig.txt not readable; StopGameOnFocusLost unknown")
    cache = Path(r"C:\ProgramData\Mount and Blade II Bannerlord\Shaders\CoreShaders\D3D11\shader_mapping.bin")
    try:
        parsed = read_shader_list(cache.read_bytes())
    except OSError:
        parsed = None
    if parsed is None:
        say("shader cache list unreadable; the engine may compile shaders once")
    elif parsed[1] == expected_shader_list(modules):
        say("shader cache list matches this module list")
    else:
        say("shader cache list differs; the engine will rebuild it once (about 40 s)")
    bin_dir = Path(game) / "bin" / "Win64_Shipping_Client"
    return configs / "engine_config.txt", bin_dir


def scan_crash(pid, say):
    for name, test in ((f"rgl_log_{pid}.txt", rgl_crashed), (f"watchdog_log_{pid}.txt", watchdog_crashed)):
        path = Path(LOGS_DIR) / name
        try:
            if path.is_file() and test(path.read_text(encoding="utf-8", errors="replace")):
                return f"{name} reports a crash"
        except OSError:
            continue
    return None


def stop_own_tree(proc, say):
    """Kills the Bannerlord.exe this tool started, through its Popen handle, and any Watchdog.exe that watches it. True when it killed."""
    killed = False
    if proc.poll() is None:
        say(f"stopping Bannerlord.exe pid {proc.pid}")
        proc.kill()
        killed = True
        try:
            proc.wait(KILL_WAIT_SECONDS)
        except subprocess.TimeoutExpired:
            say(f"Bannerlord.exe pid {proc.pid} did not exit {KILL_WAIT_SECONDS} s after the kill")
    try:
        dogs = list_processes(["Watchdog.exe"])
    except PROCESS_LISTING_ERRORS as error:
        say(f"could not list Watchdog.exe processes ({type(error).__name__}: {error}); none stopped")
        return killed
    for dog in dogs:
        if watchdog_targets_pid(dog.get("cmd"), proc.pid):
            say(f"stopping Watchdog.exe pid {dog['pid']} (watches {proc.pid})")
            kill_pid(dog["pid"])
            killed = True
    return killed


def watch(proc, folder, wall_limit, say):
    """Waits for the done file. Returns 'done' or the text of the failure (crash, stall, timeout, early exit)."""
    deadline = time.time() + wall_limit + OVERTIME_SECONDS
    done = Path(folder) / DONE_FILE
    status = Path(folder) / STATUS_FILE
    last_scan = last_text = None
    last_change = time.time()
    seen_dialogs = set()
    while True:
        if done.is_file():
            return "done"
        if proc.poll() is not None:
            time.sleep(1)
            return "done" if done.is_file() else f"the game exited (code {proc.returncode}) before the done file"
        crash_dialog = handle_dialogs(proc.pid, seen_dialogs, say)
        if crash_dialog:
            return "crash: " + crash_dialog
        now = time.time()
        if now > deadline:
            return "timeout: no done file before wall_limit plus 15 minutes"
        if last_scan is None or now - last_scan >= SCAN_SECONDS:
            last_scan = now
            crash = scan_crash(proc.pid, say)
            if crash:
                return "crash: " + crash
            text = status.read_text(encoding="utf-8", errors="replace") if status.is_file() else ""
            if text != last_text:
                last_text, last_change = text, now
                say("status " + " ".join(text.split()))
            elif now - last_change >= STALL_SECONDS:
                return "stall: the status heartbeat did not change for 10 minutes"
        time.sleep(POLL_SECONDS)


def safe_watch(proc, folder, wall_limit, say):
    """watch(), except that a runner error becomes a failure reason: the game is still stopped and the evidence still collected."""
    try:
        return watch(proc, folder, wall_limit, say)
    except Exception as error:  # noqa: BLE001 - any runner fault must still reach the stop and the collect
        return f"runner error: {type(error).__name__}: {error}"


def wait_after_crash(proc, seconds, say, sleep=time.sleep, now=time.time):
    """True when the crashed game ended by itself within the limit. The Watchdog holds the process and asks a person about a dump (a WPF
    window no script can press), and killing the game loses the dump, so with --keep-on-crash the launcher waits. 0 keeps the old behaviour."""
    if seconds <= 0:
        return False
    say(f"crash: waiting up to {seconds:g} s; answer the dump dialog on screen, then the game ends")
    deadline = now() + seconds
    while now() < deadline:
        if proc.poll() is not None:
            return True
        sleep(POLL_SECONDS)
    return False


def png_chunk(tag, data):
    return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)


def bmp_to_png(raw):
    """PNG bytes of an uncompressed 24-bit BMP, the format the engine's TakeScreenshot writes; None for anything else."""
    if raw[:2] != b"BM" or struct.unpack_from("<H", raw, 28)[0] != 24:
        return None
    offset = struct.unpack_from("<I", raw, 10)[0]
    width, height = struct.unpack_from("<ii", raw, 18)
    stride = ((width * 24 + 31) // 32) * 4
    rows = []
    for y in range(abs(height)):
        index = abs(height) - 1 - y if height > 0 else y
        line = raw[offset + index * stride:offset + index * stride + width * 3]
        rgb = bytearray(line)
        rgb[0::3], rgb[2::3] = line[2::3], line[0::3]
        rows.append(b"\0" + bytes(rgb))
    header = struct.pack(">IIBBBBB", width, abs(height), 8, 2, 0, 0, 0)
    return b"\x89PNG\r\n\x1a\n" + png_chunk(b"IHDR", header) + png_chunk(b"IDAT", zlib.compress(b"".join(rows), 6)) + png_chunk(b"IEND", b"")


def convert_screenshots(folder, say):
    """Turns each hud_*.bmp in the run folder into a PNG a reader can open, and removes the BMP. Returns how many it converted."""
    converted = 0
    for path in sorted(Path(folder).glob("hud_*.bmp")):
        png = bmp_to_png(path.read_bytes())
        if png is None:
            say("screenshot not converted, not a 24-bit BMP: " + path.name)
            continue
        path.with_suffix(".png").write_bytes(png)
        path.unlink()
        converted += 1
    if converted:
        say(f"screenshots: {converted} PNG file(s) in {folder}")
    return converted


def collect(folder, bin_dir, done, started, pid, say):
    """Copies the TAOM log, the JSON report and the engine logs into the run folder. Returns (log copy, report json)."""
    log = Path(done["log"]) if done.get("log", "none") != "none" else None
    if log is None or not log.is_file():
        logs = [(p, p.stat().st_ctime) for p in (bin_dir / "Logs").glob("taom_debug_*.log")]
        picked = pick_newest_log(logs, started)
        log = Path(picked) if picked else None
    report = Path(done["report"]) if done.get("report", "none") != "none" else None
    if report is None and log is not None:
        stamp = re.match(r"taom_debug_(.+)\.log$", log.name)
        report = log.with_name(f"fords_report_{stamp.group(1)}_run1.json") if stamp else None
    copies = {}
    for label, source in (("log", log), ("report", report), ("rgl", Path(LOGS_DIR) / f"rgl_log_{pid}.txt"),
                          ("watchdog", Path(LOGS_DIR) / f"watchdog_log_{pid}.txt")):
        if source is not None and source.is_file():
            shutil.copy2(source, Path(folder) / source.name)
            copies[label] = Path(folder) / source.name
            say(f"copied {source.name}")
        else:
            say(f"no {label} file to copy ({source})")
    report_json = None
    if "report" in copies:
        try:
            report_json = json.loads(copies["report"].read_text(encoding="utf-8"))
        except (OSError, ValueError) as error:
            say(f"report JSON unreadable: {error}")
    return copies.get("log"), report_json


def show_results(done_text, log_copy, report_json, say):
    print("---- fords_autorun_done.txt ----\n" + done_text.strip())
    if report_json is not None:
        print("---- report verdicts (final=%s, reason=%s) ----" % (report_json.get("final"), report_json.get("reason")))
        for row in report_json.get("verdicts", []):
            parts = " ".join(f"{p.get('Name')}={p.get('Result')}" for p in row.get("Parts", []))
            print(f"check {int(row.get('Check', 0)):02d} {row.get('Result')}: {row.get('Title')} | {parts}")
    if log_copy is not None:
        text = log_copy.read_text(encoding="utf-8", errors="replace")
        print("---- final=1 lines in the log ----")
        for line in final_verdict_lines(text):
            print(line)
        stamp = [line for line in text.splitlines() if "[BuildStamp]" in line]
        print(stamp[0] if stamp else "no [BuildStamp] line")
        say("clean module unload line: " + ("present" if "[Shutdown] clean module unload" in text else "ABSENT"))


def run_once(args, game, modules):
    stamp = datetime.now().strftime("%Y%m%d-%H%M%S")
    folder = resolved_runs_root(args) / stamp
    folder.mkdir(parents=True, exist_ok=True)
    runner = Runner(folder)
    say = runner.say
    arm, wall_limit = write_arm_file(folder, args)
    say(f"run folder {folder}")
    docs = my_documents()
    engine_config, bin_dir = inspect_environment(docs, game, modules, say)
    problems = preflight(game, args.require_idle_minutes)
    if problems:
        say("preflight refused: " + "; ".join(problems))
        return 1
    reset_safely_exited(engine_config, say)
    env = dict(os.environ, **{ENV_VAR: str(arm)})
    started = time.time()
    proc = subprocess.Popen([str(bin_dir / "Bannerlord.exe"), "/singleplayer", modules_arg(modules)], cwd=str(bin_dir), env=env,
                            stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    say(f"started Bannerlord.exe pid {proc.pid}")
    reason = safe_watch(proc, folder, wall_limit, say)
    say("watch ended: " + reason)
    if reason.startswith("crash"):
        wait_after_crash(proc, args.keep_on_crash, say)
    if reason == "done":
        try:
            proc.wait(EXIT_GRACE_SECONDS)
        except subprocess.TimeoutExpired:
            say("the game did not exit within 120 s of the done file")
    game_alive = proc.poll() is None
    killed = False
    if game_alive or reason != "done":
        killed = stop_own_tree(proc, say)
    say(f"game exit code {proc.returncode}")
    crash = scan_crash(proc.pid, say)
    if killed or reason != "done":
        reset_safely_exited(engine_config, say)
    done_path = folder / DONE_FILE
    done_text = done_path.read_text(encoding="utf-8") if done_path.is_file() else ""
    done = parse_done(done_text)
    log_copy, report_json = collect(folder, bin_dir, done, started, proc.pid, say)
    convert_screenshots(folder, say)
    show_results(done_text, log_copy, report_json, say)
    log_text = log_copy.read_text(encoding="utf-8", errors="replace") if log_copy is not None else None
    judged = judge(done, report_json, log_text) if reason == "done" else (1, reason)
    code, why = final_verdict(judged, crash, game_alive, None if killed else proc.returncode)
    (folder / "runner_status.txt").write_text(f"stop_reason={why}\nrunner_killed={killed}\nexit_code={code}\n", encoding="utf-8")
    say(("PASS: " if code == 0 else "FAIL: ") + why)
    return code


def build_parser():
    parser = argparse.ArgumentParser(description="Run an unattended Fords of War match and collect its self-check report.")
    parser.add_argument("--scene")
    parser.add_argument("--west")
    parser.add_argument("--east")
    parser.add_argument("--ai")
    parser.add_argument("--live-seconds", type=positive_seconds, help="mission seconds before the autorun ends the match (game default 600)")
    parser.add_argument("--wall-limit", type=positive_seconds,
                        help=f"wall seconds after the start before the match is ended (default {DEFAULT_WALL_LIMIT_SECONDS:g})")
    parser.add_argument("--keep-on-crash", type=non_negative_minutes, default=0,
                        help="after a crash wait this many seconds for a person to answer the dump dialog before stopping the game "
                             "(default 0: stop at once, which loses the dump)")
    parser.add_argument("--screenshots", help="live seconds at which the game saves a picture of the screen into the run folder, comma separated")
    parser.add_argument("--west-ai", action="store_true", help="an AI commands the West side too, so both sides attack")
    parser.add_argument("--speed", type=int, choices=[1, 2, 4, 10], help="the match speed from the first frame (default 1); live_seconds stay mission seconds, so 10 ends a match in about a tenth of the time")
    parser.add_argument("--runs", type=int, default=1, help="how many matches to run, one game process each")
    parser.add_argument("--runs-root", default=RUNS_ROOT, help="folder that holds one subfolder per run")
    parser.add_argument("--require-idle-minutes", type=non_negative_minutes, default=DEFAULT_IDLE_MINUTES,
                        help=f"refuse to start unless the PC has had no keyboard or mouse input for this many minutes "
                             f"(default {DEFAULT_IDLE_MINUTES:g}; 0 disables the gate, only when the maintainer has said yes)")
    parser.add_argument("--dry-run", action="store_true", help="print the plan, the idle state and the preflight, start nothing")
    return parser


def main(argv=None):
    args = build_parser().parse_args(argv)
    try:
        modules = launch_modules()
    except (OSError, ValueError) as error:
        print(f"cannot read the module list from {LAUNCH_SETTINGS}: {error}")
        return 1

    game = game_dir(DEFAULT_GAME_ROOT)
    if args.dry_run:
        print("game:", game)
        print("args:", modules_arg(modules))
        print("arm file would hold:\n" + arm_text_for(resolved_runs_root(args) / "<yyyyMMdd-HHmmss>", args)[0])
        idle, error = current_idle_seconds()
        print("idle:", idle_state_text(idle, args.require_idle_minutes) + (f" ({error})" if error else ""))
        problems = preflight(game, args.require_idle_minutes)
        print("preflight:", "; ".join(problems) if problems else "ok")
        return 1 if problems else 0

    worst = 0
    for index in range(max(1, args.runs)):
        print(f"=== run {index + 1} of {max(1, args.runs)} ===", flush=True)
        worst = max(worst, run_once(args, game, modules))
    return worst


if __name__ == "__main__":
    sys.exit(main())
