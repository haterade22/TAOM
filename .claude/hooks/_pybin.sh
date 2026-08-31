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
    local p="$1" lp
    [ -n "$p" ] || return 1

    # Lowercase via bash expansion, NOT `tr`. This function runs for every candidate in
    # every hook on every tool call, so a subprocess here is paid hundreds of times a
    # session; ${p,,} is a builtin and free. (Measured 2026-08-31: readlink + tr as
    # subprocesses doubled the PreToolUse critical path from 204ms to 414ms.)
    # Substring-match rather than guessing casings: the previous *[Ww]indows[Aa]pps*
    # glob varied exactly two letters, so WINDOWSAPPS passed as safe.
    lp="${p,,}"
    case "$lp" in
        *windowsapps*) return 1 ;;
    esac

    # Only resolve a link when there IS one. The WindowsApps entries present as symlinks
    # into C:\Program Files\WindowsApps\..., so a link from elsewhere could still point at
    # the alias, but `[ -L ]` is a builtin and the common case is a real file that skips
    # the readlink subprocess entirely.
    if [ -L "$p" ]; then
        p=$(readlink -f "$p" 2>/dev/null || printf '%s' "$p")
        lp="${p,,}"
        case "$lp" in
            *windowsapps*) return 1 ;;
        esac
    fi

    # -x alone is TRUE FOR A DIRECTORY. A pin of "C:/Python314" with the filename left
    # off would be accepted, every "$PYBIN" -c call would fail, and every gate would go
    # silently dead again. Require a regular file.
    [ -f "$p" ] || return 1
    [ -x "$p" ] || return 1
    return 0
}

# Prove the candidate is a live Python, under a hard bound.
#
# Check the OUTPUT, not the exit status. `/usr/bin/true -c 'import json,sys'` exits 0
# because true ignores its arguments, so an exit-status probe accepts any always-succeeding
# binary as a Python interpreter. Requiring the marker on stdout is what actually proves it
# ran Python. (This is the same shape as the bug _pybin.sh exists to fix: LOTRAOM's
# json-lib.sh also trusted an exit status to answer a question exit status cannot answer.)
#
# The bound is per candidate and the loop tries three, so the worst case must stay under
# the smallest registered timeout: 12 of the 27 registrations are 5s. `-k 0.2 0.8` means a
# SIGTERM-ignoring candidate costs at most 1.0s, so 3 x 1.0 = 3.0s < 5s. -k is essential:
# without it GNU timeout sends SIGTERM then WAITS forever on a process that ignores it,
# which is precisely the hazard being guarded against. Empty stdin so the probe cannot
# consume the hook's payload. A successful probe measures ~40-80ms here.
# -S -E: skip site-packages and ignore PYTHON* env for the probe only. It measures 86ms
# instead of 98ms and cannot be perturbed by a broken PYTHONPATH. Callers still get a
# normal interpreter; the flags apply to this one-shot check, not to later invocations.
taom_pybin_probe() {
    [ "$(printf '' | timeout -k 0.2 0.8 "$1" -S -E -c 'import sys; sys.stdout.write("taompy")' 2>/dev/null)" = "taompy" ]
}

taom_resolve_python() {
    # A pinned interpreter (settings.json env block) skips the SEARCH, but is still
    # validated AND probed rather than trusted. An inherited value must clear the same
    # bar as a discovered one, or the pin becomes a way to smuggle the alias back in;
    # and a pin that is stale on another machine must fall through to discovery rather
    # than take every hook down with it. Probing costs ~40ms and buys that guarantee.
    if taom_pybin_is_safe "${TAOM_PYBIN:-}" && taom_pybin_probe "$TAOM_PYBIN"; then
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
        if taom_pybin_probe "$resolved"; then
            printf '%s' "$resolved"
            return 0
        fi
    done

    return 1
}

# Announce a gate that cannot run.
#
# harness-facts.md mandates fail-OPEN: a hook's own bug must never block the user. But
# hook-authoring.md's 2026-08-10 addition is the other half, and it is the one that keeps
# getting dropped: for a gate, NO OUTPUT IS ITSELF A CLAIM. A gate that allows silently
# because it could not parse its input is indistinguishable from one that allowed because
# it looked and found nothing. On 2026-08-31 exactly one of the ten PreToolUse gates
# (validate-push.sh) said anything in that state; the other nine returned rc=0 mute.
#
#   $1 gate name, $2 what is now unchecked, $3 "jq" if this hook has a jq fallback
# Returns 0 (and warns) when the hook cannot parse, so callers write:
#   taom_pybin_degraded "name" "what" jq && { echo '{}'; exit 0; }
taom_pybin_degraded() {
    [ -n "${PYBIN:-}" ] && return 1
    if [ "${3:-}" = "jq" ] && command -v jq >/dev/null 2>&1; then
        return 1
    fi
    printf '%s: no usable JSON parser (no safe python%s); %s NOT checked. Gate failed OPEN. Diagnose: bash tools/test_hooks.sh\n' \
        "$1" "$([ "${3:-}" = "jq" ] && printf ' and no jq')" "$2" >&2
    return 0
}

# NOTE: this resolution is per-process and cannot be shared. Each hook is spawned
# separately by the harness, so an `export` here reaches nothing: every hook pays
# the probe again. Measured cost of that on a Bash tool call: ~77ms per hook,
# ~850ms of CPU across the ten PreToolUse hooks. The fix is the TAOM_PYBIN pin in
# settings.json (validated above), not an export.
PYBIN=$(taom_resolve_python || true)
export PYBIN
