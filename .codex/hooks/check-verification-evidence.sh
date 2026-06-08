#!/bin/bash
# Stop hook: remind to verify when C# source changed but no build/test has run
# since the last source edit. Enforces the "verification before done" half of
# .claude/rules/evidence-over-claims.md.
#
# Mirrors check-deep-review.sh conventions: reads git state (NOT stdin), emits a
# soft reminder to stderr, always exits 0 (non-blocking — never traps a Stop).
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
    echo "REMINDER: C# source changed but no build/test has run since your last edit. Per .claude/rules/evidence-over-claims.md (verification before \"done\"), run ./build.ps1 -RunTests (or dotnet build Main/TAOM.csproj + dotnet test TAOM.Tests) and read the output before claiming the work complete. A subagent's self-report does not count as verification." >&2
    mkdir -p .claude/logs 2>/dev/null
    touch "$REMINDED" 2>/dev/null || true
  fi
else
  # Nothing left to verify (built since the edit, or edits reverted) — re-arm the
  # reminder for the next fresh edit.
  rm -f "$REMINDED" 2>/dev/null || true
fi

exit 0
