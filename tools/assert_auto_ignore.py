#!/usr/bin/env python
"""Answer the engine's assert and warning dialogs while the Modding Kit loads, so nobody has to click Ignore on each one.

Run:  python tools/assert_auto_ignore.py          (start it before the Kit or the game; Ctrl+C stops it)
Test: python -m unittest tools.tests.test_assert_auto_ignore

Every 250 ms it reads the visible dialogs (#32770) of TaleWorlds.MountAndBlade.Launcher.exe and Bannerlord.exe and presses one
button by title (BUTTON_POSITION). The press is a posted WM_COMMAND (click_button): no window activation and
no wait, so a dialog that blocks its own thread cannot stall this tool. That is why it replaces AssertAutoIgnore.exe by poheniks,
which stopped after "Closing: ASSERT" (its UI Automation Invoke uses a synchronous send). The title table came from reading that
exe (docs/reference/provenance-register.md). It never presses a button named Abort, and it clicks only in the game's processes.

An ignored assert is not a pass. rglBuffer.cpp:899 has an unexplained editor-only case on the untouched Armory, and a tpac writer
bug produces the same text (docs/reference/lotrlome-warg-changes.md): judge asset work with a game launch. Each window gets one
log line with its text, so the output lists what was ignored.

Selecting text in the console window pauses print, and this tool with it: press Esc to clear the selection.
"""
import argparse
import json
import subprocess
import sys
import time
from datetime import datetime

PROCESS_NAMES = ["TaleWorlds.MountAndBlade.Launcher.exe", "Bannerlord.exe"]
POLL_SECONDS = 0.25
LIST_SECONDS = 5.0
WM_COMMAND = 0x0111
PROCESS_LISTING_ERRORS = (OSError, ValueError, subprocess.TimeoutExpired)
BUTTON_POSITION = {
    "Always Ignore?": 0,
    "RGL CONTENT WARNING": 0,
    "RGL CONTENT ERROR": 0,
    "RGL WARNING": 0,
    "Safe Mode": 1,
    "*_*": 1,
    "ASSERT": 2,
    "SAFE ASSERT": 2,
}


# The process listing and dialog helpers below were run_fords_autorun.py's on the 1.5.x line. That runner is not on 1.4.x,
# so they live here.


def powershell(command, timeout=60):
    """The stripped stdout. A non-zero exit raises OSError, so a failed query is never read as an empty answer."""
    result = subprocess.run(["powershell", "-NoProfile", "-NonInteractive", "-Command", command],
                            capture_output=True, text=True, timeout=timeout)
    if result.returncode != 0:
        raise OSError(f"powershell exited {result.returncode}: {(result.stderr or '').strip()[:200]}")
    return result.stdout.strip()



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

def choose_button(title, buttons):
    """The control id to press from {button text: control id} in child order, or None: unknown title, too few buttons, or Abort."""
    position = BUTTON_POSITION.get(title)
    if position is None or position >= len(buttons):
        return None
    text, control = list(buttons.items())[position]
    return None if text.replace("&", "").strip().lower() == "abort" else control


def answer_dialogs(pids, seen, clicked, say):
    """One poll pass: posts the chosen button on every open dialog and logs each window once per outcome. Returns the new total.
    seen holds (window or pid, outcome) pairs, and forgets a window once it has closed."""
    open_windows = set()
    all_read = True
    for pid in pids:
        try:
            dialogs = game_dialogs(pid)
        except (OSError, AttributeError) as error:
            all_read = False
            if (pid, "read failed") not in seen:
                seen.add((pid, "read failed"))
                say(f"dialog check failed for pid {pid} ({error})")
            continue
        for hwnd, title, texts, buttons in dialogs:
            open_windows.add(hwnd)
            control = choose_button(title, buttons)
            if control is None:
                outcome = "left open"
            else:
                outcome = "clicked" if click_button(hwnd, control) else "click refused"
            if (hwnd, outcome) in seen:
                continue
            seen.add((hwnd, outcome))
            if outcome == "clicked":
                clicked += 1
            detail = " | ".join(text for text in texts if text)
            say(f"{outcome} (total {clicked}): '{title}': {detail}")
    if all_read:
        seen.intersection_update({entry for entry in seen if entry[0] in open_windows})
    return clicked


def game_pids(previous, say):
    """The pids of the game processes, with one line for each change. A failed listing prints the error and keeps the known pids."""
    try:
        running = list_processes(PROCESS_NAMES)
    except PROCESS_LISTING_ERRORS as error:
        say(f"process listing failed ({type(error).__name__}: {error})")
        return previous
    names = {proc["pid"]: proc["name"] for proc in running}
    pids = set(names)
    for pid in sorted(pids - previous):
        say(f"watching {names[pid]} pid {pid}")
    for pid in sorted(previous - pids):
        say(f"process ended: pid {pid}")
    return pids


def say(message):
    print(f"{datetime.now():%H:%M:%S} {message}", flush=True)


def main():
    argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter).parse_args()
    say("looking for the Kit or the game (Ctrl+C stops)")
    pids, seen, clicked, next_list = set(), set(), 0, 0.0
    try:
        while True:
            if time.monotonic() >= next_list:
                pids = game_pids(pids, say)
                next_list = time.monotonic() + LIST_SECONDS
            clicked = answer_dialogs(sorted(pids), seen, clicked, say)
            time.sleep(POLL_SECONDS)
    except KeyboardInterrupt:
        say(f"stopped, {clicked} clicked")
    return 0


if __name__ == "__main__":
    sys.exit(main())
