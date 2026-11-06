#!/usr/bin/env python
"""Answer the engine's assert and warning dialogs while the Modding Kit loads, so nobody has to click Ignore on each one.

Run:  python tools/assert_auto_ignore.py          (start it before the Kit or the game; Ctrl+C stops it)
Test: python -m unittest tools.tests.test_assert_auto_ignore

Every 250 ms it reads the visible dialogs (#32770) of TaleWorlds.MountAndBlade.Launcher.exe, Bannerlord.exe and the game folder's
Watchdog.exe, and presses one button by title (BUTTON_POSITION). Watchdog's crash question ('*_*') gets No, so no crash files are
collected while this tool runs. That question is a system message box owned by csrss.exe, so while a game process runs the tool
also reads csrss's dialogs, and answers only the '*_*' title there. The press is the posted WM_COMMAND of run_fords_autorun.click_button: no window activation and
no wait, so a dialog that blocks its own thread cannot stall this tool. That is why it replaces AssertAutoIgnore.exe by poheniks,
which stopped after "Closing: ASSERT" (its UI Automation Invoke uses a synchronous send). The title table came from reading that
exe (docs/reference/provenance-register.md). It never presses a button named Abort, and it clicks only in the game's processes.

An ignored assert is not a pass. rglBuffer.cpp:899 has an unexplained editor-only case on the untouched Armory, and a tpac writer
bug produces the same text (docs/reference/lotrlome-warg-changes.md): judge asset work with a game launch. Each window gets one
log line with its text, so the output lists what was ignored.

Selecting text in the console window pauses print, and this tool with it: press Esc to clear the selection.
"""
import argparse
import os
import sys
import time
from datetime import datetime

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from run_fords_autorun import PROCESS_LISTING_ERRORS, click_button, game_dialogs, list_processes  # noqa: E402

PROCESS_NAMES = ["TaleWorlds.MountAndBlade.Launcher.exe", "Bannerlord.exe", "Watchdog.exe"]
GAME_FOLDER = "mount & blade ii bannerlord"
# Watchdog's crash question is a system message box: csrss.exe owns the window, not Watchdog.exe.
HOST_TITLES = {"*_*"}
POLL_SECONDS = 0.25
LIST_SECONDS = 5.0
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


def choose_button(title, buttons):
    """The control id to press from {button text: control id} in child order, or None: unknown title, too few buttons, or Abort."""
    position = BUTTON_POSITION.get(title)
    if position is None or position >= len(buttons):
        return None
    text, control = list(buttons.items())[position]
    return None if text.replace("&", "").strip().lower() == "abort" else control


def answer_dialogs(pids, seen, clicked, say, hosts=()):
    """One poll pass: posts the chosen button on every open dialog and logs each window once per outcome. Returns the new total.
    seen holds (window or pid, outcome) pairs, and forgets a window once it has closed. hosts are csrss.exe pids: of their
    dialogs only the HOST_TITLES ones are touched, because csrss shows every program's system message boxes."""
    open_windows = set()
    all_read = True
    for pid in list(pids) + list(hosts):
        try:
            dialogs = game_dialogs(pid)
            if pid in hosts:
                dialogs = [dialog for dialog in dialogs if dialog[1] in HOST_TITLES]
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
    # The game's Watchdog.exe asks the crash question ('*_*'); a Watchdog.exe outside the game folder is another program's.
    names = {proc["pid"]: proc["name"] for proc in running
             if proc["name"].lower() != "watchdog.exe" or GAME_FOLDER in (proc.get("path") or "").lower()}
    pids = set(names)
    for pid in sorted(pids - previous):
        say(f"watching {names[pid]} pid {pid}")
    for pid in sorted(previous - pids):
        say(f"process ended: pid {pid}")
    return pids


def host_pids(say):
    """The csrss.exe pids, or none when the listing fails (printed)."""
    try:
        return {proc["pid"] for proc in list_processes(["csrss.exe"])}
    except PROCESS_LISTING_ERRORS as error:
        say(f"csrss listing failed ({type(error).__name__}: {error})")
        return set()


def say(message):
    print(f"{datetime.now():%H:%M:%S} {message}", flush=True)


def main():
    argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter).parse_args()
    say("looking for the Kit or the game (Ctrl+C stops)")
    pids, hosts, seen, clicked, next_list = set(), set(), set(), 0, 0.0
    try:
        while True:
            if time.monotonic() >= next_list:
                pids = game_pids(pids, say)
                hosts = host_pids(say) if pids else set()
                next_list = time.monotonic() + LIST_SECONDS
            clicked = answer_dialogs(sorted(pids), seen, clicked, say, hosts)
            time.sleep(POLL_SECONDS)
    except KeyboardInterrupt:
        say(f"stopped, {clicked} clicked")
    return 0


if __name__ == "__main__":
    sys.exit(main())
