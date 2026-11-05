#!/usr/bin/env python
"""Answer the engine's assert and warning dialogs while the Modding Kit loads, so nobody has to click Ignore on each one.

Run:  python tools/assert_auto_ignore.py          (start it before the Kit or the game; Ctrl+C stops it)
Test: python -m unittest tools.tests.test_assert_auto_ignore

Every 250 ms it reads the visible dialogs (#32770) of TaleWorlds.MountAndBlade.Launcher.exe and Bannerlord.exe and presses one
button by title (BUTTON_POSITION). The press is the posted WM_COMMAND of run_fords_autorun.click_button: no window activation and
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

PROCESS_NAMES = ["TaleWorlds.MountAndBlade.Launcher.exe", "Bannerlord.exe"]
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
