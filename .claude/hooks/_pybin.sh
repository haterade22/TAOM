#!/usr/bin/env bash
# _pybin.sh — resolve a Python interpreter that is safe to execute from a hook.
#
# WHY THIS EXISTS
# On this machine `python3` resolves to a Microsoft Store App Execution Alias:
#   /c/Users/mikew/AppData/Local/Microsoft/WindowsApps/python3
# Executed from Git Bash it does not print, does not exit 9009, and does not die
# on SIGTERM. It HANGS. A hook that pipes into it never returns, so the tool call
# it guards never returns either.
#
# Measured 2026-08-31: the harness runs all hooks matching an event in parallel,
# and an omitted `timeout` defaults to 600s. A wedged Bash call therefore paid one
# 600s PreToolUse batch plus one 600s PostToolUse batch: 1200s, which is exactly
# the 20.0-minute stalls seen in the overnight transcripts.
#
# Bare `python` is NOT affected: it resolves to /c/Python314/python (real CPython
# 3.14, ~41ms startup) because /c/Python314 precedes WindowsApps on PATH. Only the
# spelling `python3` is poisoned. Outside a hook, just write `python`.
#
# LOTRAOM's json-lib.sh anticipated Store stubs but guarded by checking the EXIT
# STATUS after running the candidate, which cannot help against something that
# never exits. The only safe move is to refuse to execute anything under
# WindowsApps, and to bound the probe.
#
# USAGE
#   source "$(dirname "${BASH_SOURCE[0]}")/_pybin.sh"
#   [ -n "$PYBIN" ] || { echo '{}'; exit 0; }   # fail OPEN, never block
#   ... | "$PYBIN" -c '...'
#
# The guard on line 2 of that snippet is the hook's job, not this file's: a helper
# that exits on the caller's behalf would turn a missing interpreter into a killed
# hook. Every call site is expected to apply it.

# Reject anything we must never execute, without executing it. `command -v`
# succeeding proves only that a file exists at that name, which is exactly the trap.
taom_pybin_is_safe() {
    local p="$1"
    [ -n "$p" ] || return 1
    case "$p" in
        *[Ww]indows[Aa]pps*) return 1 ;;
    esac
    [ -x "$p" ] || return 1
    return 0
}

taom_resolve_python() {
    # A pinned interpreter (settings.json env block) skips the probe entirely, but
    # is VALIDATED rather than trusted: an inherited value must clear the same
    # WindowsApps bar as a probed one, or the pin becomes a way to smuggle the
    # alias back in. A stale pin on another machine simply fails here and we probe.
    if taom_pybin_is_safe "${TAOM_PYBIN:-}"; then
        printf '%s' "$TAOM_PYBIN"
        return 0
    fi

    local py resolved
    # `python` first: on this machine it is the real interpreter and `python3` is
    # the alias, so the common case resolves without touching the trap at all.
    for py in python python3 py; do
        command -v "$py" >/dev/null 2>&1 || continue
        resolved=$(command -v "$py" 2>/dev/null) || continue
        taom_pybin_is_safe "$resolved" || continue

        # Even for a safe-looking candidate, probe under a hard bound so an
        # unexpected stall degrades to "no python" instead of wedging the hook.
        # -k matters: without it GNU timeout sends SIGTERM and then WAITS, so a
        # process that ignores SIGTERM hangs the guard itself. Empty stdin, so the
        # probe cannot consume the hook's payload.
        if printf '' | timeout -k 1 5 "$resolved" -c 'pass' >/dev/null 2>&1; then
            printf '%s' "$resolved"
            return 0
        fi
    done

    return 1
}

# NOTE: this resolution is per-process and cannot be shared. Each hook is spawned
# separately by the harness, so an `export` here reaches nothing: every hook pays
# the probe again. Measured cost of that on a Bash tool call: ~77ms per hook,
# ~850ms of CPU across the ten PreToolUse hooks. The fix is the TAOM_PYBIN pin in
# settings.json (validated above), not an export.
PYBIN=$(taom_resolve_python || true)
export PYBIN
