#!/bin/bash
# Stop hook: remind to verify when C# source changed but no build/test has run
# since the last source edit. Enforces the "verification before done" half of
# .claude/rules/evidence-over-claims.md.
#
# Channel: one JSON {"decision":"block","reason":...} on stdout per unbuilt streak, through
# _stop_reminder.sh, the only Stop output Claude reads (exit-0 stderr goes to the debug log;
# until plan 011 this hook wrote there and nothing arrived). Silent when stop_hook_active is
# true, which only withholds the reminder: the marker still clears when a build ran during that
# continuation, or the next streak would stay muted. Detection reads git state; stdin is read
# only for that flag. Always exits 0, and any
# internal failure exits 0 with no output (fail open).
#
# Signal: a dirty *.cs file is NEWER than .claude/logs/.verification-ran (touched
# by mark-verification-run.sh when dotnet build/test or build.ps1 runs). If the
# marker is missing, bash "-nt" is true for any existing file => reminds (i.e.
# source is dirty and nothing was ever built). Doc/rule/XML-only sessions never
# match the *.cs case, so they stay silent.
#
# Muting: to avoid nagging on every Stop while the same edits stay unbuilt, the
# reminder fires ONCE per unbuilt streak. A .verification-reminded marker records
# "already reminded"; it is cleared the moment there is nothing left to verify
# (a build ran, or the edits were reverted), which re-arms the reminder for the
# next fresh edit.

source "$(dirname "${BASH_SOURCE[0]}")/_stop_reminder.sh" 2>/dev/null || exit 0
INPUT=$(cat)
# Anchor to the project, not the inherited cwd: mark-verification-run.sh writes its marker
# under CLAUDE_PROJECT_DIR, and tools/test_hooks.sh runs this hook against sandboxes.
cd "${CLAUDE_PROJECT_DIR:-$(pwd)}" 2>/dev/null || exit 0

MARKER=".claude/logs/.verification-ran"
REMINDED=".claude/logs/.verification-reminded"

CHANGED=$(git diff --name-only 2>/dev/null)
STAGED=$(git diff --cached --name-only 2>/dev/null)
UNTRACKED=$(git ls-files --others --exclude-standard 2>/dev/null)
ALL_FILES="$CHANGED"$'\n'"$STAGED"$'\n'"$UNTRACKED"

NEEDS_REMINDER=0
while IFS= read -r f; do
  case "$f" in
    *.cs)
      # Dirty C# file edited more recently than the last verification run?
      # The -f guard skips staged deletions (git diff --cached lists removed
      # files that are no longer on disk) so they never spuriously trigger.
      if [[ -f "$f" && "$f" -nt "$MARKER" ]]; then
        NEEDS_REMINDER=1
        break
      fi
      ;;
  esac
done <<< "$ALL_FILES"

if [[ $NEEDS_REMINDER -eq 1 ]]; then
  if [[ ! -f "$REMINDED" ]]; then
    taom_stop_hook_active "$INPUT" && exit 0
    taom_stop_block "check-verification-evidence: a C# file in this tree changed after the last recorded build or test (nothing newer in .claude/logs/.verification-ran). Before calling the work done, run dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId= or dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= and read the output (.claude/rules/evidence-over-claims.md); a subagent's report does not count. If the changed files belong to another session, or you are not claiming the work is done, say so in one line. This fires once per unbuilt streak."
    mkdir -p .claude/logs 2>/dev/null
    touch "$REMINDED" 2>/dev/null || true
  fi
else
  # Nothing left to verify (built since the edit, or edits reverted): re-arm the
  # reminder for the next fresh edit.
  rm -f "$REMINDED" 2>/dev/null || true
fi

exit 0
