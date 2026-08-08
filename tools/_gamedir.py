#!/usr/bin/env python3
"""One place to answer "where is the Bannerlord install".

Tools under tools/ carry an install path as their default. That literal is one
machine's; BANNERLORD_GAME_DIR is the override README.md requires and
setup-dev-env.ps1 sets. Keeping the resolution here rather than inline buys two
things a plain `os.environ.get(VAR, default)` does not:

  * A set-but-blank variable is treated as unset. `os.environ.get` returns `""`
    for an exported-but-empty variable and `Path("")` is `.`, so the tool goes
    on to report every file missing instead of saying the root is wrong.

  * `ensure_exists` states plainly that a root is not there. Several tools
    otherwise finish with a clean-looking result on a wrong root — printing
    SKIP lines, or "0 of 13 remaps match", and exiting 0. A wrong root is the
    caller's to fix, so say so (`.claude/rules/environment-failures.md`).

Both were raised in issue #404. Import it as a sibling module — tools run as
`python tools/<name>.py`, so tools/ is already on sys.path:

    from _gamedir import game_dir
    GAME = game_dir(r"E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord")
"""
import os
import sys
from pathlib import Path

ENV_VAR = "BANNERLORD_GAME_DIR"
MODULES_ENV_VAR = "BANNERLORD_GAME_MODULES"


def game_dir(default):
    """The install root: $BANNERLORD_GAME_DIR when it names something, else `default`.

    Returns a `str`, verbatim, so a caller composing `GAME + r"\\Modules\\..."`
    keeps the separator style its own literal used. A variable that is set but
    blank (or only whitespace) counts as unset.
    """
    value = os.environ.get(ENV_VAR)
    if value is None or not value.strip():
        return default
    return value


def game_modules(default):
    """The `Modules` folder, as a `Path`, from whichever variable is set.

    $BANNERLORD_GAME_MODULES predates $BANNERLORD_GAME_DIR and names the folder
    directly, so it stays as the explicit override and wins. Otherwise this
    derives from the install root, which means setting one variable is enough
    for a correct setup — a server answering queries against the previous
    install is hard to notice. `default` is the install root, not the Modules
    folder.
    """
    override = os.environ.get(MODULES_ENV_VAR)
    if override is not None and override.strip():
        return Path(override)
    return Path(game_dir(default)) / "Modules"


def ensure_exists(path, what="the Bannerlord install"):
    """Return `path` as a `Path`, or exit 2 saying it is not there.

    Call this where the tool would otherwise carry on and report a result that
    reads as clean. Exit 2 rather than 1 keeps "bad input" distinct from "found
    something wrong with the data".
    """
    resolved = Path(path)
    if resolved.exists():
        return resolved
    print(f"ERROR: {what} not found at {resolved}", file=sys.stderr)
    print(f"       Set ${ENV_VAR} to your Bannerlord install root.", file=sys.stderr)
    raise SystemExit(2)
