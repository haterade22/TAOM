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

# The command as POSIX-shell text (plan 027): _pybin.sh taom_hook_command hands a PowerShell
# command back as the Bash text of the same command and names git `git` wherever it is the
# command (`GIT`, `git.exe`, a path). Without Python it reads the raw command with jq, as Bash text.
COMMAND=$(taom_hook_command posix block-no-verify)

# Fail-open: nothing to inspect -> allow.
[[ -z "${COMMAND:-}" ]] && exit 0

# Only git commands. --no-verify means something else entirely to other tools.
[[ ! "$COMMAND" =~ (^|[[:space:]])git([[:space:]]|$) ]] && exit 0

refuse() {
  echo "BLOCKED: $1 is not allowed. Fix what is making the hook fail instead of bypassing it." >&2
  exit 2
}

# --no-veri is the shortest spelling git accepts: --no-ver is ambiguous with --no-verbose.
[[ "$COMMAND" =~ --no-veri ]] && refuse "--no-verify"
# A config key is case-insensitive.
[[ "${COMMAND,,}" =~ (^|[[:space:]])-c[[:space:]]+core\.hookspath= ]] && refuse "-c core.hooksPath"

# `-n` means --no-verify only on `git commit`, so read it per segment, as block-broad-git-add.sh does.
# A cheap test first: without a short flag holding n after `commit`, no segment can match.
[[ "$COMMAND" =~ commit.*[[:space:]]-[a-zA-Z]*n ]] || exit 0
SEGMENTS=$(taom_hook_segments block-no-verify)
set -f; IFS=$'\n'; SEG_LIST=($SEGMENTS); IFS=$' \t\n'; set +f
for seg in "${SEG_LIST[@]}"; do
  seg="${seg#"${seg%%[![:space:]]*}"}"
  while [[ "$seg" =~ ^[A-Za-z_][A-Za-z0-9_]*=[^[:space:]]*[[:space:]]+(.*)$ ]]; do
    seg="${BASH_REMATCH[1]}"; seg="${seg#"${seg%%[![:space:]]*}"}"
  done
  [[ "$seg" =~ ^git([[:space:]]|$) ]] || continue
  rest="${seg#git}"; rest="${rest#"${rest%%[![:space:]]*}"}"
  while [[ "$rest" =~ ^(-c[[:space:]]+[^[:space:]]+|-C[[:space:]]+[^[:space:]]+|--git-dir[=[:space:]][^[:space:]]+|--work-tree[=[:space:]][^[:space:]]+|--no-pager|-p|--paginate|--bare|--no-replace-objects|--literal-pathspecs)[[:space:]]+(.*)$ ]]; do
    rest="${BASH_REMATCH[2]}"; rest="${rest#"${rest%%[![:space:]]*}"}"
  done
  [[ "$rest" =~ ^commit([[:space:]]|$) ]] || continue
  # Quoted spans first, so a message that mentions -n is never read as the flag.
  rest=$(printf '%s' "$rest" | sed -E "s/\"[^\"]*\"//g; s/'[^']*'//g")
  [[ "$rest" =~ [[:space:]]-[a-zA-Z]*n[a-zA-Z]*([[:space:]]|$) ]] && refuse "git commit -n"
done

exit 0
