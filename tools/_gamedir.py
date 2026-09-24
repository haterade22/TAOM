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
import subprocess
import sys
from pathlib import Path

ENV_VAR = "BANNERLORD_GAME_DIR"
MODULES_ENV_VAR = "BANNERLORD_GAME_MODULES"
# The lotraom-assets mirror of LOTRLOME_Armory for the 1.5 line (the v1.4 tree is gone), which
# the Armory writers keep in step with the live install.
ASSET_REPO = Path(r"E:\repos\lotraom-assets") / "v1.5" / "LOTRLOME_Armory"


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


def armory_trees(armory_md, asset_repo):
    """[(label, ModuleData)] for an Armory writer: the live Armory, then the mirror when it
    exists. A missing mirror is a warning, never an error: the live install is what the game
    loads. The caller checks the live Armory itself (each tool words that refusal its own way)."""
    trees = [("armory", Path(armory_md))]
    mirror = Path(asset_repo) / "ModuleData"
    if mirror.is_dir():
        trees.append(("mirror", mirror))
    else:
        print(f"WARNING: assets mirror not found at {asset_repo}; only the live Armory is touched")
    return trees


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


def game_or_kit_running():
    """True when the game or the Modding Kit runs, and also when the check itself could not
    run: a writer that guards on this refuses to write rather than risk racing a live process
    (fail-closed, `.claude/rules/environment-failures.md`). Called by apply_animalia_armory.py and
    skeleton_hit_capsules.py; the two Animalia .ps1 writers carry a PowerShell equivalent (a process-name
    prefix match through Get-Process). tpac_fix_item_checksums.py does not guard yet.

    tasklist writes the OEM code page, not the ANSI one a text-mode read assumes, so the output is read as
    bytes and decoded as ASCII with replacement: the two names looked for are ASCII, and a process name
    outside it can no longer turn the read into an exception."""
    try:
        raw = subprocess.run(["tasklist", "/FO", "CSV", "/NH"], capture_output=True,
                             timeout=20, check=True).stdout
    except (OSError, subprocess.SubprocessError) as exc:
        print("WARNING: could not list processes (%s); refusing to write" % exc)
        return True
    if raw is None:
        print("WARNING: the process list came back empty-handed; refusing to write")
        return True
    out = raw.decode("ascii", errors="replace")
    return any(k in out for k in ("TaleWorlds.MountAndBlade", "Bannerlord"))
