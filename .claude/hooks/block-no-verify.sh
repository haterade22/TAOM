#!/bin/bash

# PreToolUse (Bash and PowerShell): refuse every spelling that skips the commit-msg hook: --no-verify
# (git expands --no-veri), `git commit -n`, and a one-call `-c core.hooksPath`. `git push -n` is a dry
# run and stays allowed. tools/test_hooks.sh 7e pins each form.
#
# WHY THIS IS NOT ALSO A BUILD GATE
# This shipped as check-build-before-commit.sh and ran `dotnet build` before every
# `git commit`. It never once did so: it read tool_input.command with a bare `jq`, and
# jq is NOT on PATH in this Git Bash install (verified 2026-08-20), so COMMAND was always
# empty, the `git commit` test never matched, and BOTH halves of the hook were inert.
#
# Fixing the parse alone would have re-armed the build on every commit, which is wrong
# here for two measured reasons. Hooks run in the session's directory (the MAIN project
# directory unless an earlier Bash `cd` moved it, #690), not where the command's own `cd`
# leads (proven 2026-08-20: a `dotnet build` run from
# a worktree updated the main tree's .verification-ran marker), so a commit made in a
# worktree would be gated on a different tree's build state. And the build lacked
# `-p:DisableModuleCopy=true -p:ModuleId=`, so with Bannerlord running the module copy
# fails and would block the commit for a reason unrelated to the code.
#
# Verification is already covered without either flaw: check-verification-evidence.sh
# reminds at Stop when C# changed with no build/test since, and /verify runs the real
# thing. So this keeps the half that is cheap and correct.
#
# Returns exit 2 to block, 0 to allow. Fail-open: an unparseable payload allows.

INPUT=$(cat)

# Prefilter: the block below needs `--no-veri`, `commit` or a hooksPath key in the command, and Claude
# Code never escapes an ASCII letter, so a raw payload without one of those cannot concern this gate.
# Exiting here skips the _pybin.sh probe and the parse (two Python starts) on every Bash call
# that does not carry the flag, other git calls included. Match the raw text, never a token
# regex: a newline before a command arrives as \n. tools/test_hooks.sh 4c checks it.
# Never skip on an escape: JSON writes a letter either literally or as a \u escape,
# so a payload holding any \u takes the full parse, and the raw test is safe
# whatever writes the payload.
[[ "$INPUT" == *no-ver* || "$INPUT" == *commit* || "$INPUT" == *[Hh][Oo][Oo][Kk][Ss][Pp][Aa][Tt][Hh]* || "$INPUT" == *'\u'* ]] || exit 0

# Resolve a safe Python (never a Microsoft Store alias — those hang forever).
source "$(dirname "${BASH_SOURCE[0]}")/_pybin.sh"

# Fail open, but never fail silent: for a gate, no output reads as "nothing to report".
taom_pybin_degraded "block-no-verify" "the --no-verify ban" jq && { echo '{}'; exit 0; }

refuse() {
  echo "BLOCKED: $1 is not allowed. Fix what is making the hook fail instead of bypassing it." >&2
  exit 2
}

# One Python run reads the whole command (_shellwords.py bypass), the way bash reads it: past quotes,
# subshells, a glued separator, a `bash -c` script and every global option, and never taking a message
# for a flag. Bounded inside the gate (hook-authoring.md, "a timeout is a kill").
if [[ -n "${PYBIN:-}" ]]; then
  if REASON=$(printf '%s' "$INPUT" | timeout -k 1 "${TAOM_READER_TIMEOUT:-20}" "$PYBIN" "$TAOM_HOOKS_DIR/_shellwords.py" bypass 2>/dev/null); then
    [[ -n "$REASON" ]] && refuse "$REASON"
    exit 0
  fi
  echo "block-no-verify: _shellwords.py bypass failed, so the command is read as raw text" >&2
fi

# No reader: the raw text, line by line. Quoted spans go first, so a message that names a flag is not
# read as one, but a flag quoted as a whole word and a quoted hooksPath value stay. A flag counts only on
# a line that runs git, so a heredoc line that names one is not read. A body line that itself runs git
# with the flag still refuses, which is the safe direction.
COMMAND=$(taom_hook_command posix block-no-verify)

# Fail-open: nothing to inspect -> allow.
[[ -z "${COMMAND:-}" ]] && exit 0

# Only git commands. --no-verify means something else entirely to other tools.
[[ ! "$COMMAND" =~ (^|[[:space:]])git([[:space:]]|$) ]] && exit 0

BARE=$(printf '%s' "$COMMAND" | sed -E "s/([[:space:]])[\"']+(--no-veri[a-z]*)[\"']+/\1\2/g; s/(-c[[:space:]]+)[\"']+([Cc][Oo][Rr][Ee]\.)/\1\2/g; s/\"[^\"]*\"//g; s/'[^']*'//g")
BARE=${BARE//$'\\\n'/ }
GIT_LINES=$(printf '%s\n' "$BARE" | grep -E '(^|[[:space:];&|(])git([[:space:]]|$)')
# --no-veri is the shortest spelling git accepts: --no-ver is ambiguous with --no-verbose.
[[ "$GIT_LINES" =~ (^|[[:space:]])--no-veri ]] && refuse "--no-verify"
# A config key is case-insensitive.
[[ "${GIT_LINES,,}" =~ (^|[[:space:]])-c[[:space:]]+core\.hookspath= ]] && refuse "-c core.hooksPath"
# A word holding a separator or a line end ends the call, so the -n of a later `git log -n 1` or
# `tail -n 3` is not read.
BARE=${BARE//$'\n'/ ; }
[[ "$BARE" =~ (^|[[:space:]])git[[:space:]]+commit([[:space:]]+[^-\;\&\|[:space:]][^[:space:]]*|[[:space:]]+-[^[:space:]]*)*[[:space:]]+-[a-zA-Z]*n[a-zA-Z]*([[:space:]]|$) ]] && refuse "git commit -n"

exit 0
